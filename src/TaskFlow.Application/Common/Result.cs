namespace TaskFlow.Application.Common;

/// <summary>
/// Lightweight outcome type for service methods so expected business failures (not found,
/// invalid transition, concurrency conflict) are returned as data instead of thrown as
/// exceptions — exceptions stay reserved for truly exceptional, unrecoverable situations.
/// </summary>
public class Result
{
    public bool Succeeded { get; }
    public string? Error { get; }
    public ResultErrorType ErrorType { get; }

    protected Result(bool succeeded, string? error, ResultErrorType errorType)
    {
        Succeeded = succeeded;
        Error = error;
        ErrorType = errorType;
    }

    public static Result Success() => new(true, null, ResultErrorType.None);
    public static Result Failure(string error, ResultErrorType type = ResultErrorType.Validation) => new(false, error, type);
}

public sealed class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool succeeded, T? value, string? error, ResultErrorType type) : base(succeeded, error, type)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, null, ResultErrorType.None);
    public static new Result<T> Failure(string error, ResultErrorType type = ResultErrorType.Validation) => new(false, default, error, type);
}

/// <summary>Lets controllers map a Result to the right HTTP status without string-matching the Error message.</summary>
public enum ResultErrorType
{
    None,
    Validation,
    NotFound,
    Conflict
}
