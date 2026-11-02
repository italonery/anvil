using System.Collections.Concurrent;
using System.Data.Common;
using Anvil.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Anvil.Persistence.Postgres;

public interface IEfDbContextFactory
{
    TContext GetContext<TContext>() where TContext : DbContext;
    Task<Result<T>> SafeExecAsync<TContext, T>(Func<TContext, Task<T>> action) where TContext : DbContext;
}

public sealed class EfDbContextFactory : IEfDbContextFactory, IAsyncDisposable
{
    private readonly ConcurrentDictionary<Type, EfSafeDbContextFactory> _factoryPool = new();
    private readonly IConfiguration _config;
    private readonly ILogger<EfDbContextFactory> _logger;

    public EfDbContextFactory(IConfiguration config, ILogger<EfDbContextFactory> logger)
    {
        _config = config;
        _logger = logger;
    }

    public TContext GetContext<TContext>() where TContext : DbContext
    {
        var type = typeof(TContext);
        var factory = _factoryPool.GetOrAdd(type, t => new EfSafeDbContextFactory(_config, t));
        return (TContext)factory.CreateDbContext();
    }

    public async Task<Result<T>> SafeExecAsync<TContext, T>(Func<TContext, Task<T>> action) where TContext : DbContext
    {
        try
        {
            await using var context = GetContext<TContext>();
            var data = await action(context).ConfigureAwait(false);
            return Result.Success(data);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict on {Context}", typeof(TContext).Name);
            return Result.Failure<T>(Error.Conflict(ex.Message, ErrorCode.ConcurrencyConflict));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database update error on {Context}", typeof(TContext).Name);
            return Result.Failure<T>(Error.Failure(ErrorCode.DatabaseError, ex.Message, ex.HResult.ToString(), ex.InnerException?.Message));
        }
        catch (DbException ex)
        {
            _logger.LogError(ex, "Database provider error on {Context}", typeof(TContext).Name);
            return Result.Failure<T>(Error.Unavailable(ErrorCode.DatabaseUnavailable, ex.Message, ex.SqlState));
        }
        catch (ResultErrorException ex)
        {
            return Result.Failure<T>(ex.Error);
        }
        catch (OperationCanceledException ex)
        {
            return Result.Failure<T>(new Error(ErrorCode.OperationCanceled, ex.Message, ErrorType.Timeout));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled database error on {Context}", typeof(TContext).Name);
            return Result.Failure<T>(Error.Failure(ErrorCode.UnhandledError, ex.Message, ex.HResult.ToString()));
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var factory in _factoryPool.Values)
        {
            await factory.DisposeAsync().ConfigureAwait(false);
        }

        _factoryPool.Clear();
    }
}

internal sealed class EfSafeDbContextFactory : IDbContextFactory<DbContext>, IAsyncDisposable
{
    private readonly DbContextOptions _options;
    private readonly Type _dbContextType;

    public EfSafeDbContextFactory(IConfiguration config, Type dbContextType)
    {
        var connectionString = config.GetConnectionString(dbContextType.Name) ?? throw new ArgumentException($"Connection string '{dbContextType.Name}' not found in configuration.");

        _dbContextType = dbContextType;
        _options = new DbContextOptionsBuilder().UseNpgsql(connectionString).Options;
    }

    public DbContext CreateDbContext()
    {
        var context = (DbContext?)Activator.CreateInstance(_dbContextType, _options);
        if (context is null)
        {
            throw new InvalidOperationException($"Could not create an instance of DbContext type '{_dbContextType.Name}'.");
        }

        return context;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
