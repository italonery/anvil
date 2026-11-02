using System.Collections.Concurrent;
using System.Linq.Expressions;
using Anvil.Primitives;
using Anvil.Primitives.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Anvil.Persistence.Postgres.Extensions;

public sealed class PgNullableJsonbConverter : ValueConverter<object?, string?>
{
    public PgNullableJsonbConverter()
        : base(
            v => v.JsonSerialize(false),
            v => v.JsonDeserialize<object>())
    {
    }
}

public sealed class PgNullableTimestampConverter : ValueConverter<DateTime?, DateTime?>
{
    public PgNullableTimestampConverter()
        : base(
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : null,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : null)
    {
    }
}

public sealed class PgTimestampConverter : ValueConverter<DateTime, DateTime>
{
    public PgTimestampConverter()
        : base(
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}

public sealed class PgCommandBuilder<TContext, TEntity> where TContext : DbContext where TEntity : class
{
    private readonly EfReflections<TContext, TEntity> _reflections = new();
    private readonly ConcurrentDictionary<Type, Dictionary<string, string?>> _columnTypeCache = new();

    public string GetTableName(TContext context)
    {
        return _reflections.GetTableName(context);
    }

    public (string Sql, List<object?> Parameters) BuildUpsertCommand(
        TContext context,
        TEntity entity,
        Expression<Func<TEntity, object>>[] uniqueIndexSelectors,
        Expression<Func<TEntity, object>>[]? columnSelectors = null,
        bool ignoreNullValues = true)
    {
        if (uniqueIndexSelectors is null || uniqueIndexSelectors.Length == 0)
        {
            throw new ResultErrorException(Error.Validation(ErrorCode.InvalidArgument, "At least one unique index selector must be provided."));
        }

        if (columnSelectors is null || columnSelectors.Length == 0)
        {
            columnSelectors = _reflections.GetAllUpdatableColumnSelectors(context);
        }

        var uniqueIndexCols = uniqueIndexSelectors
            .Select(p => _reflections.GetPropertyColumnName(context, _reflections.GetPropertyName(p)))
            .ToList();

        var filteredColumns = columnSelectors
            .Select(p => _reflections.GetPropertyColumnName(context, _reflections.GetPropertyName(p)))
            .ToList();

        var filteredValues = columnSelectors
            .Select(p => _reflections.GetPropertyValue(context, entity, p))
            .ToList();

        if (ignoreNullValues)
        {
            var nonNullColumns = new List<string>(filteredColumns.Count);
            var nonNullValues = new List<object?>(filteredValues.Count);

            for (var i = 0; i < filteredValues.Count; i++)
            {
                if (filteredValues[i] is not null)
                {
                    nonNullColumns.Add(filteredColumns[i]);
                    nonNullValues.Add(filteredValues[i]);
                }
            }

            filteredColumns = nonNullColumns;
            filteredValues = nonNullValues;
        }

        var tableName = _reflections.GetTableName(context);
        var columnsSql = string.Join(", ", filteredColumns.Select(c => $"\"{c}\""));
        var paramsSql = BuildParamsSqlWithCasting(context, filteredColumns);
        var conflictSql = string.Join(", ", uniqueIndexCols.Select(c => $"\"{c}\""));
        var updateSql = string.Join(", ", filteredColumns.Select(col => $"\"{col}\" = EXCLUDED.\"{col}\""));

        var sql = $"INSERT INTO \"{tableName}\" ({columnsSql}) VALUES ({paramsSql}) " + $"ON CONFLICT ({conflictSql}) DO UPDATE SET {updateSql}";

        return (sql, filteredValues);
    }

    private Dictionary<string, string?> GetColumnTypeMapping(TContext context)
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity));
        return _columnTypeCache.GetOrAdd(typeof(TEntity), _ =>
        {
            return entityType!.GetProperties().ToDictionary(p => p.GetColumnName() ?? p.Name, p => (string?)p.GetColumnType());
        });
    }

    private string BuildParamsSqlWithCasting(TContext context, List<string> filteredColumns)
    {
        var columnTypeMap = GetColumnTypeMapping(context);
        return string.Join(", ", filteredColumns.Select((col, i) =>
        {
            if (columnTypeMap.TryGetValue(col, out var dbType) && !string.IsNullOrEmpty(dbType))
            {
                return $"@p{i}::{dbType}";
            }
            else
            {
                return $"@p{i}";
            }
        }));
    }
}
