namespace Idevs.Models;

public class ResponseModel
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public object? Data { get; set; }
    public T? GetData<T>() => Data is null ? default : (T)Data;
    public IEnumerable<T>? GetCollection<T>() => Data is null ? default : (IEnumerable<T>)Data;
    public Exception? Exception { get; set; }

    public ResponseModel(bool isSuccess = true, string? errorMessage = null, object? data = null, Exception? exception = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Data = data;
        Exception = exception;
    }

    public ResponseModel()
    {
    }
}
