namespace ErpSystem.Core.DTOs.Common;

/// <summary>
/// Generic paged result wrapper for API responses
/// </summary>
public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

/// <summary>
/// Standard API response wrapper
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
    
    public static ApiResponse<T> SuccessResponse(T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message
        };
    }
    
    public static ApiResponse<T> ErrorResponse(string error)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Error = error
        };
    }
}

/// <summary>
/// Standard API response without data
/// </summary>
public class ApiResponse
{
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
    public string? Error { get; set; }
    
    public static ApiResponse SuccessResponse(string? message = null)
    {
        return new ApiResponse
        {
            Success = true,
            Message = message
        };
    }
    
    public static ApiResponse ErrorResponse(string error)
    {
        return new ApiResponse
        {
            Success = false,
            Error = error
        };
    }
}