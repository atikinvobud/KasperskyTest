namespace Backend.Share;

public class Result<T>
{
    public bool IsSuccess { get; set; }

    public T? Value{ get; set; }
    public string? ErrorMessage { get; set; }
    public ErrorCode? Code { get; set; }

    private Result(bool IsSuccess, T? Value, ErrorCode? code = null)
    {
        this.IsSuccess = IsSuccess;
        this.Value = Value;
        if(code.HasValue)
        {
            ErrorMessage = ErrorService.GetMessage(code);
            this.Code = code;
        }
    }

    public static Result<T> Success(T value) =>new(true, value);
    public static Result<T> Error(ErrorCode code) =>new(false, default, code);

}