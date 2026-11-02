using System.Linq.Expressions;
using Anvil.Persistence.Postgres.Extensions;
using Anvil.Primitives;
using Microsoft.EntityFrameworkCore;

namespace Anvil.Persistence.Postgres;

public abstract class EfBaseRepository<TContext, TEntity> where TContext : DbContext where TEntity : class
{
    protected readonly IEfDbContextFactory ContextFactory;
    protected readonly PgCommandBuilder<TContext, TEntity> CommandBuilder = new();

    protected EfBaseRepository(IEfDbContextFactory contextFactory)
    {
        ContextFactory = contextFactory;
    }

    protected async Task<Result<T>> SafeExecAsync<T>(Func<TContext, Task<T>> action)
    {
        return await ContextFactory.SafeExecAsync(action).ConfigureAwait(false);
    }

    public async Task<Result<TEntity?>> GetAsync(object id)
    {
        return await SafeExecAsync<TEntity?>(async context =>
        {
            var entity = await context.Set<TEntity>().FindAsync(id).ConfigureAwait(false);
            if (entity is null)
            {
                throw new ResultErrorException(Error.NotFound($"Entity {typeof(TEntity).Name} was not found."));
            }

            return entity;
        }).ConfigureAwait(false);
    }

    public async Task<Result<List<TEntity>>> GetListAsync(Expression<Func<TEntity, bool>> predicate)
    {
        return await SafeExecAsync(async context =>
        {
            return await context.Set<TEntity>()
                .AsNoTracking()
                .Where(predicate)
                .ToListAsync()
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task<Result<TEntity>> AddAsync(TEntity entity)
    {
        return await SafeExecAsync(async context =>
        {
            await context.Set<TEntity>().AddAsync(entity).ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);
            return entity;
        }).ConfigureAwait(false);
    }

    public async Task<Result<ResultNone>> UpdateAsync(TEntity entity)
    {
        return await SafeExecAsync(async context =>
        {
            context.Set<TEntity>().Update(entity);
            var affected = await context.SaveChangesAsync().ConfigureAwait(false);
            if (affected == 0)
            {
                throw new ResultErrorException(Error.NotFound($"Entity of type {typeof(TEntity).Name} not found for update."));
            }

            return new ResultNone();
        }).ConfigureAwait(false);
    }

    public async Task<Result<ResultNone>> DeleteAsync(object id)
    {
        return await SafeExecAsync(async context =>
        {
            var pgCasting = id is Guid ? "::uuid" : string.Empty;
            var tableName = CommandBuilder.GetTableName(context);
            var sql = $"DELETE FROM \"{tableName}\" WHERE id = @p0{pgCasting}";
            var affected = await context.Database.ExecuteSqlRawAsync(sql, id).ConfigureAwait(false);
            if (affected == 0)
            {
                throw new ResultErrorException(Error.NotFound($"Entity of type {typeof(TEntity).Name} with ID {id} not found."));
            }

            return new ResultNone();
        }).ConfigureAwait(false);
    }

    protected async Task<Result<TEntity?>> UpsertAsync(
        TEntity entity,
        Expression<Func<TEntity, object>>[] uniqueIndexSelectors,
        Expression<Func<TEntity, object>>[]? columnSelectors = null,
        bool ignoreNullValues = true,
        bool returnEntity = true)
    {
        return await SafeExecAsync(async context =>
        {
            var commandData = CommandBuilder.BuildUpsertCommand(context, entity, uniqueIndexSelectors, columnSelectors, ignoreNullValues);
            if (returnEntity)
            {
                var sql = commandData.Sql + " RETURNING *";
                var results = await context.Set<TEntity>()
                    .FromSqlRaw(sql, commandData.Parameters.ToArray()!)
                    .AsNoTracking()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return results.FirstOrDefault();
            }
            else
            {
                await context.Database.ExecuteSqlRawAsync(commandData.Sql, commandData.Parameters.ToArray()!).ConfigureAwait(false);
                return null;
            }
        }).ConfigureAwait(false);
    }
}
