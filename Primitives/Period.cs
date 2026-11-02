namespace Anvil.Primitives;

public readonly record struct Period
{
    public DateTimeOffset StartUtc { get; }
    public DateTimeOffset EndUtc { get; }
    public TimeSpan Duration => EndUtc - StartUtc;

    private Period(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    public static Result<Period> Create(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        if (endUtc < startUtc)
        {
            return Result.Failure<Period>(Error.Validation(ErrorCode.InvalidPeriod, "End date must be greater than or equal to start date."));
        }

        return Result.Success(new Period(startUtc.ToUniversalTime(), endUtc.ToUniversalTime()));
    }

    public bool Contains(DateTimeOffset instantUtc)
    {
        var normalized = instantUtc.ToUniversalTime();
        return normalized >= StartUtc && normalized <= EndUtc;
    }
}
