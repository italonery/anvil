using System.Text.Json.Serialization;

namespace Anvil.Primitives;

public enum ErrorType
{
    None = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5,
    Failure = 6,
    Unavailable = 7,
    Timeout = 8
}

public readonly record struct ErrorCode
{
    public int Id { get; }

    public string Name { get; }

    [JsonConstructor]
    public ErrorCode(int id, string name)
    {
        Id = id;
        Name = name;
    }

    // 0 - Default
    public static readonly ErrorCode None = new(0, nameof(None));

    // 100..199 - Validation & Input (Universal)
    public static readonly ErrorCode InvalidArgument = new(100, nameof(InvalidArgument));
    public static readonly ErrorCode InvalidPeriod = new(101, nameof(InvalidPeriod));

    // 200..299 - Domain & State (Universal)
    public static readonly ErrorCode NotFound = new(200, nameof(NotFound));
    public static readonly ErrorCode AlreadyExists = new(201, nameof(AlreadyExists));
    public static readonly ErrorCode Conflict = new(202, nameof(Conflict));
    public static readonly ErrorCode ConcurrencyConflict = new(203, nameof(ConcurrencyConflict));
    public static readonly ErrorCode BusinessRuleViolation = new(204, nameof(BusinessRuleViolation));
    public static readonly ErrorCode OperationCanceled = new(205, nameof(OperationCanceled));

    // 300..399 - Security & Access (Universal)
    public static readonly ErrorCode Unauthorized = new(300, nameof(Unauthorized));
    public static readonly ErrorCode Forbidden = new(301, nameof(Forbidden));

    // 500..599 - Infrastructure & External Systems (Universal)
    public static readonly ErrorCode DatabaseError = new(500, nameof(DatabaseError));
    public static readonly ErrorCode DatabaseUnavailable = new(501, nameof(DatabaseUnavailable));
    public static readonly ErrorCode NetworkError = new(502, nameof(NetworkError));
    public static readonly ErrorCode ExternalServiceError = new(503, nameof(ExternalServiceError));
    public static readonly ErrorCode Timeout = new(504, nameof(Timeout));
    public static readonly ErrorCode SchedulerError = new(505, nameof(SchedulerError));
    public static readonly ErrorCode MessagingError = new(506, nameof(MessagingError));
    public static readonly ErrorCode UnhandledError = new(599, nameof(UnhandledError));
}

public readonly record struct FieldViolation
{
    public string Field { get; }
    public string Description { get; }

    [JsonConstructor]
    public FieldViolation(string field, string description)
    {
        Field = field;
        Description = description;
    }
}

public readonly record struct Error
{
    public static readonly Error None = new(ErrorCode.None, string.Empty, ErrorType.None);
    public static readonly Error NullValue = new(ErrorCode.InvalidArgument, "A null value was provided.", ErrorType.Validation);
    public ErrorCode Code { get; }
    public string Description { get; }
    public ErrorType Type { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<FieldViolation>? Violations { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SystemCode { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Details { get; }

    [JsonConstructor]
    public Error(ErrorCode code, string description, ErrorType type = ErrorType.Failure, IReadOnlyList<FieldViolation>? violations = null, string? systemCode = null, string? details = null)
    {
        Code = code;
        Description = description;
        Type = type;
        Violations = violations;
        SystemCode = systemCode;
        Details = details;
    }

    public static Error Validation(string field, string description)
    {
        return new Error(ErrorCode.InvalidArgument, description, ErrorType.Validation, [new FieldViolation(field, description)]);
    }

    public static Error Validation(IReadOnlyList<FieldViolation> violations, string description = "One or more validation errors occurred.")
    {
        return new Error(ErrorCode.InvalidArgument, description, ErrorType.Validation, violations);
    }

    public static Error Validation(ErrorCode code, string description, string? details = null)
    {
        return new Error(code, description, ErrorType.Validation, null, null, details);
    }

    public static Error NotFound(string description, ErrorCode? code = null)
    {
        return new Error(code ?? ErrorCode.NotFound, description, ErrorType.NotFound);
    }

    public static Error Conflict(string description, ErrorCode? code = null)
    {
        return new Error(code ?? ErrorCode.Conflict, description, ErrorType.Conflict);
    }

    public static Error Unauthorized(string description, ErrorCode? code = null)
    {
        return new Error(code ?? ErrorCode.Unauthorized, description, ErrorType.Unauthorized);
    }

    public static Error Forbidden(string description, ErrorCode? code = null)
    {
        return new Error(code ?? ErrorCode.Forbidden, description, ErrorType.Forbidden);
    }

    public static Error Failure(ErrorCode code, string description, string? systemCode = null, string? details = null)
    {
        return new Error(code, description, ErrorType.Failure, null, systemCode, details);
    }

    public static Error Unavailable(ErrorCode code, string description, string? systemCode = null)
    {
        return new Error(code, description, ErrorType.Unavailable, null, systemCode);
    }
}
