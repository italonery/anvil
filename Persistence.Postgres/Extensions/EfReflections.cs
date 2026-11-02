using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Anvil.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Anvil.Persistence.Postgres.Extensions;

public sealed class EfReflections<TContext, TEntity> where TContext : DbContext where TEntity : class
{
    private string? _tableName;
    private readonly ConcurrentDictionary<string, PropertyInfo> _propertyCache = new();
    private readonly ConcurrentDictionary<string, string> _propertyToColumnCache = new();
    private readonly ConcurrentDictionary<Type, Expression<Func<TEntity, object>>[]> _allColumnSelectorsCache = new();

    public string GetTableName(TContext context)
    {
        if (_tableName is not null)
        {
            return _tableName;
        }

        _tableName = context.Model.FindEntityType(typeof(TEntity))?.GetTableName()
            ?? throw new ResultErrorException(Error.Validation(ErrorCode.InvalidArgument, $"Table for entity {typeof(TEntity).Name} not found."));

        return _tableName;
    }

    public Expression<Func<TEntity, object>>[] GetAllUpdatableColumnSelectors(DbContext context)
    {
        return _allColumnSelectorsCache.GetOrAdd(typeof(TEntity), _ =>
        {
            var entityType = context.Model.FindEntityType(typeof(TEntity));
            if (entityType is null)
            {
                throw new ResultErrorException(
                    Error.Validation(ErrorCode.InvalidArgument, $"Entity type {typeof(TEntity).Name} not found in model."));
            }

            var keyNames = entityType.FindPrimaryKey()?.Properties.Select(p => p.Name).ToHashSet() ?? [];
            var ignoredNames = entityType.GetProperties()
                .Where(p => p.IsShadowProperty() || p.IsIndexerProperty())
                .Select(p => p.Name)
                .ToHashSet();

            var propertyInfos = entityType.GetProperties()
                .Where(p => !keyNames.Contains(p.Name)
                    && !ignoredNames.Contains(p.Name)
                    && !p.IsPrimaryKey()
                    && p.ValueGenerated == ValueGenerated.Never)
                .ToList();

            var param = Expression.Parameter(typeof(TEntity), "e");
            var selectors = propertyInfos.Select(p =>
                Expression.Lambda<Func<TEntity, object>>(
                    Expression.Convert(Expression.Property(param, p.Name), typeof(object)),
                    param)).ToArray();

            return selectors;
        });
    }

    public string GetPropertyName(Expression<Func<TEntity, object>> selector)
    {
        if (selector.Body is MemberExpression member)
        {
            return member.Member.Name;
        }

        if (selector.Body is UnaryExpression unary && unary.Operand is MemberExpression unaryMember)
        {
            return unaryMember.Member.Name;
        }

        throw new ResultErrorException(Error.Validation(ErrorCode.InvalidArgument, "Invalid property selector expression."));
    }

    public object? GetPropertyValue(DbContext context, TEntity entity, Expression<Func<TEntity, object>> selector)
    {
        var propName = GetPropertyName(selector);
        var prop = _propertyCache.GetOrAdd(propName, name =>
            typeof(TEntity).GetProperty(name)
            ?? throw new ResultErrorException(Error.Validation(ErrorCode.InvalidArgument, $"Property '{name}' not found on type {typeof(TEntity).Name}.")));

        var value = prop.GetValue(entity);
        var entityType = context.Model.FindEntityType(typeof(TEntity));
        var efProp = entityType?.GetProperties().FirstOrDefault(p => p.Name == propName);
        var converter = efProp?.GetValueConverter();

        if (converter is ValueConverter vc && efProp is not null)
        {
            value = vc.ConvertToProvider(value);
        }

        return value;
    }

    public string GetPropertyColumnName(DbContext context, string propertyName)
    {
        return _propertyToColumnCache.GetOrAdd(propertyName, _ =>
        {
            var entityType = context.Model.FindEntityType(typeof(TEntity));
            var efProp = entityType?.GetProperties().FirstOrDefault(p => p.Name == propertyName);
            return efProp?.GetColumnName() ?? propertyName;
        });
    }
}
