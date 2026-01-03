using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Shared.DTOs;

namespace XcluadeAgent.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<AuditController> _logger;

    public AuditController(
        IAuditLogRepository auditLogRepository,
        ILogger<AuditController> logger)
    {
        _auditLogRepository = auditLogRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get audit logs with pagination and filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResponse<AuditLogDto>>>> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? action = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? entityType = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var allLogs = await _auditLogRepository.GetRecentAsync(1000);

        // Apply filters
        var filtered = allLogs.AsEnumerable();

        if (!string.IsNullOrEmpty(action))
        {
            filtered = filtered.Where(l => l.Action.Contains(action, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(userId))
        {
            filtered = filtered.Where(l => l.UserId.ToString() == userId);
        }

        if (!string.IsNullOrEmpty(entityType))
        {
            filtered = filtered.Where(l => l.EntityType == entityType);
        }

        if (from.HasValue)
        {
            filtered = filtered.Where(l => l.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            filtered = filtered.Where(l => l.Timestamp <= to.Value);
        }

        var total = filtered.Count();
        var logs = filtered
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new AuditLogDto
            {
                Id = l.Id,
                UserId = l.UserId,
                Username = l.Username,
                Action = l.Action,
                EntityType = l.EntityType,
                EntityId = l.EntityId,
                EntityName = l.EntityName,
                Details = l.Details,
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent,
                Timestamp = l.Timestamp,
                TimeAgo = GetTimeAgo(l.Timestamp),
                ActionColor = GetActionColor(l.Action),
                ActionIcon = GetActionIcon(l.Action)
            })
            .ToList();

        var response = new PagedResponse<AuditLogDto>
        {
            Success = true,
            Data = logs,
            Pagination = new PaginationMeta
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                HasPrevious = page > 1,
                HasNext = page * pageSize < total
            }
        };

        return Ok(ApiResponse<PagedResponse<AuditLogDto>>.Ok(response));
    }

    /// <summary>
    /// Get audit log statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<AuditStatsDto>>> GetStats()
    {
        var logs = await _auditLogRepository.GetRecentAsync(1000);

        var today = DateTime.UtcNow.Date;
        var weekAgo = today.AddDays(-7);

        var stats = new AuditStatsDto
        {
            TotalLogs = logs.Count,
            TodayLogs = logs.Count(l => l.Timestamp >= today),
            WeekLogs = logs.Count(l => l.Timestamp >= weekAgo),
            ByAction = logs
                .GroupBy(l => l.Action.Split('.').First())
                .ToDictionary(g => g.Key, g => g.Count()),
            ByUser = logs
                .Where(l => !string.IsNullOrEmpty(l.Username))
                .GroupBy(l => l.Username!)
                .ToDictionary(g => g.Key, g => g.Count()),
            ByEntityType = logs
                .Where(l => !string.IsNullOrEmpty(l.EntityType))
                .GroupBy(l => l.EntityType!)
                .ToDictionary(g => g.Key, g => g.Count()),
            RecentActivity = logs
                .Take(5)
                .Select(l => new RecentActivityDto
                {
                    Action = l.Action,
                    Username = l.Username,
                    Timestamp = l.Timestamp,
                    TimeAgo = GetTimeAgo(l.Timestamp)
                })
                .ToList()
        };

        return Ok(ApiResponse<AuditStatsDto>.Ok(stats));
    }

    /// <summary>
    /// Get unique action types for filtering
    /// </summary>
    [HttpGet("actions")]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetActionTypes()
    {
        var logs = await _auditLogRepository.GetRecentAsync(1000);
        var actions = logs
            .Select(l => l.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToList();

        return Ok(ApiResponse<List<string>>.Ok(actions));
    }

    /// <summary>
    /// Export audit logs
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> ExportLogs(
        [FromQuery] string format = "csv",
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var logs = await _auditLogRepository.GetRecentAsync(10000);

        if (from.HasValue)
        {
            logs = logs.Where(l => l.Timestamp >= from.Value).ToList();
        }

        if (to.HasValue)
        {
            logs = logs.Where(l => l.Timestamp <= to.Value).ToList();
        }

        if (format.ToLower() == "json")
        {
            var jsonData = logs.Select(l => new
            {
                l.Id,
                l.UserId,
                l.Username,
                l.Action,
                l.EntityType,
                l.EntityId,
                l.EntityName,
                l.Details,
                l.IpAddress,
                l.Timestamp
            });

            return Ok(jsonData);
        }

        // CSV format
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Id,UserId,Username,Action,EntityType,EntityId,EntityName,Details,IpAddress,Timestamp");

        foreach (var log in logs)
        {
            csv.AppendLine($"\"{log.Id}\",\"{log.UserId}\",\"{EscapeCsv(log.Username)}\",\"{EscapeCsv(log.Action)}\",\"{EscapeCsv(log.EntityType)}\",\"{log.EntityId}\",\"{EscapeCsv(log.EntityName)}\",\"{EscapeCsv(log.Details)}\",\"{EscapeCsv(log.IpAddress)}\",\"{log.Timestamp:O}\"");
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", $"audit-logs-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    #region Private Methods

    private static string GetTimeAgo(DateTime time)
    {
        var diff = DateTime.UtcNow - time;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 30) return $"{(int)diff.TotalDays}d ago";
        return time.ToString("MMM dd, yyyy");
    }

    private static string GetActionColor(string action) => action switch
    {
        var a when a.Contains("Create") || a.Contains("Add") => "green",
        var a when a.Contains("Delete") || a.Contains("Remove") => "red",
        var a when a.Contains("Update") || a.Contains("Edit") => "blue",
        var a when a.Contains("Login") => "purple",
        var a when a.Contains("Sync") => "cyan",
        var a when a.Contains("Error") || a.Contains("Fail") => "red",
        _ => "gray"
    };

    private static string GetActionIcon(string action) => action switch
    {
        var a when a.Contains("Create") || a.Contains("Add") => "➕",
        var a when a.Contains("Delete") || a.Contains("Remove") => "🗑️",
        var a when a.Contains("Update") || a.Contains("Edit") => "✏️",
        var a when a.Contains("Login") => "🔐",
        var a when a.Contains("Logout") => "🚪",
        var a when a.Contains("Sync") => "🔄",
        var a when a.Contains("Rollback") => "⏪",
        var a when a.Contains("Error") || a.Contains("Fail") => "❌",
        _ => "📝"
    };

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "");
    }

    #endregion
}
