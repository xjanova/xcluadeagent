using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Core.Interfaces;

/// <summary>
/// Repository for project data
/// </summary>
public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Project?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IEnumerable<Project>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Project>> GetByStatusAsync(ProjectStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<Project>> GetEnabledAsync(CancellationToken cancellationToken = default);
    Task<Project> CreateAsync(Project project, CancellationToken cancellationToken = default);
    Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for sync history
/// </summary>
public interface ISyncHistoryRepository
{
    Task<SyncHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<SyncHistory>> GetByProjectAsync(Guid projectId, int limit = 50, CancellationToken cancellationToken = default);
    Task<IEnumerable<SyncHistory>> GetRecentAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task<SyncHistory?> GetLastSuccessfulAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<SyncHistory> CreateAsync(SyncHistory history, CancellationToken cancellationToken = default);
    Task<SyncHistory> UpdateAsync(SyncHistory history, CancellationToken cancellationToken = default);
    Task<IEnumerable<SyncHistory>> SearchAsync(SyncHistorySearch search, CancellationToken cancellationToken = default);
}

/// <summary>
/// Search criteria for sync history
/// </summary>
public class SyncHistorySearch
{
    public Guid? ProjectId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool? Success { get; set; }
    public SyncTrigger? Trigger { get; set; }
    public string? SearchText { get; set; }
    public int Offset { get; set; } = 0;
    public int Limit { get; set; } = 50;
}

/// <summary>
/// Repository for users
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<User> CreateAsync(User user, CancellationToken cancellationToken = default);
    Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for audit logs
/// </summary>
public interface IAuditLogRepository
{
    Task<AuditLog> CreateAsync(AuditLog log, CancellationToken cancellationToken = default);
    Task<IEnumerable<AuditLog>> GetRecentAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<AuditLog>> SearchAsync(AuditLogSearch search, CancellationToken cancellationToken = default);
}

/// <summary>
/// Search criteria for audit logs
/// </summary>
public class AuditLogSearch
{
    public Guid? UserId { get; set; }
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Offset { get; set; } = 0;
    public int Limit { get; set; } = 100;
}
