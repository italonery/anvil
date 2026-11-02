using System.Text.Json.Serialization;

namespace Anvil.Primitives;

public readonly record struct ResultNone;

public readonly record struct ResultPagination
{
    public const int DefaultPageSize = 50;
    public int Page { get; }
    public int PageSize { get; }
    public int TotalItems { get; }
    public int TotalPages
    {
        get
        {
            if (PageSize <= 0)
            {
                return 0;
            }

            return (int)Math.Ceiling((double)TotalItems / PageSize);
        }
    }

    [JsonConstructor]
    public ResultPagination(int page, int pageSize, int totalItems)
    {
        Page = page < 0 ? 0 : page;
        PageSize = pageSize <= 0 ? DefaultPageSize : pageSize;
        TotalItems = totalItems < 0 ? 0 : totalItems;
    }
}

public readonly record struct Result
{
    public bool IsSuccess { get; }

    [JsonIgnore]
    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    [JsonConstructor]
    public Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success()
    {
        return new Result(true, Error.None);
    }

    public static Result Failure(Error error)
    {
        return new Result(false, error);
    }

    public static Result<TValue> Success<TValue>(TValue value, ResultPagination? paging = null)
    {
        return Result<TValue>.Success(value, paging);
    }

    public static Result<TValue> Failure<TValue>(Error error)
    {
        return Result<TValue>.Failure(error);
    }
}

public readonly record struct Result<TValue>
{
    public bool IsSuccess { get; }

    [JsonIgnore]
    public bool IsFailure => !IsSuccess;

    public TValue? Data { get; }
    public Error Error { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResultPagination? Paging { get; }

    [JsonConstructor]
    public Result(bool isSuccess, TValue? data, Error error, ResultPagination? paging = null)
    {
        IsSuccess = isSuccess;
        Data = data;
        Error = error;
        Paging = paging;
    }

    public static Result<TValue> Success(TValue data, ResultPagination? paging = null)
    {
        return new Result<TValue>(true, data, Error.None, paging);
    }

    public static Result<TValue> Failure(Error error)
    {
        return new Result<TValue>(false, default, error, null);
    }

    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<Error, TResult> onFailure)
    {
        if (IsSuccess)
        {
            return onSuccess(Data!);
        }
        else
        {
            return onFailure(Error);
        }
    }
}

public sealed class ResultErrorException : Exception
{
    public Error Error { get; }

    public ResultErrorException(Error error) : base(error.Description)
    {
        Error = error;
    }

    public ResultErrorException(Error error, Exception? innerException) : base(error.Description, innerException)
    {
        Error = error;
    }
}
