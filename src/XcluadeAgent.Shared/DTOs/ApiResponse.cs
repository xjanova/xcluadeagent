namespace XcluadeAgent.Shared.DTOs;

/// <summary>
/// Standard API response wrapper
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = [];
    public Dictionary<string, string[]>? ValidationErrors { get; set; }
    public ApiMetadata? Meta { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null) => new()
    {
        Success = true,
        Data = data,
        Message = message
    };

    public static ApiResponse<T> Fail(string error) => new()
    {
        Success = false,
        Errors = [error]
    };

    public static ApiResponse<T> Fail(string error, T? data) => new()
    {
        Success = false,
        Data = data,
        Errors = [error]
    };

    public static ApiResponse<T> Fail(List<string> errors) => new()
    {
        Success = false,
        Errors = errors
    };

    public static ApiResponse<T> ValidationFail(Dictionary<string, string[]> validationErrors) => new()
    {
        Success = false,
        Message = "Validation failed",
        ValidationErrors = validationErrors
    };
}

/// <summary>
/// API response without data
/// </summary>
public class ApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = [];

    public static ApiResponse Ok(string? message = null) => new()
    {
        Success = true,
        Message = message
    };

    public static ApiResponse Fail(string error) => new()
    {
        Success = false,
        Errors = [error]
    };
}

/// <summary>
/// Paginated response
/// </summary>
public class PagedResponse<T>
{
    public bool Success { get; set; } = true;
    public List<T> Data { get; set; } = [];
    public PaginationMeta Pagination { get; set; } = new();
}

/// <summary>
/// Pagination metadata
/// </summary>
public class PaginationMeta
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public bool HasPrevious { get; set; }
    public bool HasNext { get; set; }
}

/// <summary>
/// API metadata
/// </summary>
public class ApiMetadata
{
    public string? RequestId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public long? DurationMs { get; set; }
}

/// <summary>
/// Pagination request
/// </summary>
public class PaginationRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 20 : (value > 100 ? 100 : value);
    }

    public int Skip => (Page - 1) * PageSize;
}
