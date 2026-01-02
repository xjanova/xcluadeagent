using Microsoft.EntityFrameworkCore;
using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Infrastructure.Persistence;

/// <summary>
/// Project repository implementation
/// </summary>
public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _context;

    public ProjectRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Projects.FindAsync([id], cancellationToken);
    }

    public async Task<Project?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.Projects
            .FirstOrDefaultAsync(p => p.Name == name, cancellationToken);
    }

    public async Task<IEnumerable<Project>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Projects
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Project>> GetByStatusAsync(ProjectStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Projects
            .Where(p => p.Status == status)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Project>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Projects
            .Where(p => p.Enabled)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Project> CreateAsync(Project project, CancellationToken cancellationToken = default)
    {
        project.CreatedAt = DateTime.UtcNow;
        project.UpdatedAt = DateTime.UtcNow;

        _context.Projects.Add(project);
        await _context.SaveChangesAsync(cancellationToken);

        return project;
    }

    public async Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        project.UpdatedAt = DateTime.UtcNow;

        _context.Projects.Update(project);
        await _context.SaveChangesAsync(cancellationToken);

        return project;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await _context.Projects.FindAsync([id], cancellationToken);
        if (project == null) return false;

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Projects.CountAsync(cancellationToken);
    }
}

/// <summary>
/// Sync history repository implementation
/// </summary>
public class SyncHistoryRepository : ISyncHistoryRepository
{
    private readonly AppDbContext _context;

    public SyncHistoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SyncHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SyncHistories.FindAsync([id], cancellationToken);
    }

    public async Task<IEnumerable<SyncHistory>> GetByProjectAsync(Guid projectId, int limit = 50, CancellationToken cancellationToken = default)
    {
        return await _context.SyncHistories
            .Where(h => h.ProjectId == projectId)
            .OrderByDescending(h => h.StartedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<SyncHistory>> GetRecentAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        return await _context.SyncHistories
            .OrderByDescending(h => h.StartedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<SyncHistory?> GetLastSuccessfulAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return await _context.SyncHistories
            .Where(h => h.ProjectId == projectId && h.Success && !h.IsRollback)
            .OrderByDescending(h => h.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SyncHistory> CreateAsync(SyncHistory history, CancellationToken cancellationToken = default)
    {
        _context.SyncHistories.Add(history);
        await _context.SaveChangesAsync(cancellationToken);

        return history;
    }

    public async Task<SyncHistory> UpdateAsync(SyncHistory history, CancellationToken cancellationToken = default)
    {
        _context.SyncHistories.Update(history);
        await _context.SaveChangesAsync(cancellationToken);

        return history;
    }

    public async Task<IEnumerable<SyncHistory>> SearchAsync(SyncHistorySearch search, CancellationToken cancellationToken = default)
    {
        var query = _context.SyncHistories.AsQueryable();

        if (search.ProjectId.HasValue)
            query = query.Where(h => h.ProjectId == search.ProjectId.Value);

        if (search.FromDate.HasValue)
            query = query.Where(h => h.StartedAt >= search.FromDate.Value);

        if (search.ToDate.HasValue)
            query = query.Where(h => h.StartedAt <= search.ToDate.Value);

        if (search.Success.HasValue)
            query = query.Where(h => h.Success == search.Success.Value);

        if (search.Trigger.HasValue)
            query = query.Where(h => h.Trigger == search.Trigger.Value);

        if (!string.IsNullOrEmpty(search.SearchText))
            query = query.Where(h =>
                h.ProjectName.Contains(search.SearchText) ||
                (h.ToVersion != null && h.ToVersion.Contains(search.SearchText)));

        return await query
            .OrderByDescending(h => h.StartedAt)
            .Skip(search.Offset)
            .Take(search.Limit)
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
/// User repository implementation
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users.FindAsync([id], cancellationToken);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .OrderBy(u => u.Username)
            .ToListAsync(cancellationToken);
    }

    public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        user.UpdatedAt = DateTime.UtcNow;

        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FindAsync([id], cancellationToken);
        if (user == null) return false;

        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users.CountAsync(cancellationToken);
    }
}

/// <summary>
/// Audit log repository implementation
/// </summary>
public class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _context;

    public AuditLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AuditLog> CreateAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        log.CreatedAt = DateTime.UtcNow;

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);

        return log;
    }

    public async Task<IEnumerable<AuditLog>> GetRecentAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .OrderByDescending(l => l.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<AuditLog>> SearchAsync(AuditLogSearch search, CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (search.UserId.HasValue)
            query = query.Where(l => l.UserId == search.UserId.Value);

        if (!string.IsNullOrEmpty(search.Action))
            query = query.Where(l => l.Action == search.Action);

        if (!string.IsNullOrEmpty(search.EntityType))
            query = query.Where(l => l.EntityType == search.EntityType);

        if (search.EntityId.HasValue)
            query = query.Where(l => l.EntityId == search.EntityId.Value);

        if (search.FromDate.HasValue)
            query = query.Where(l => l.CreatedAt >= search.FromDate.Value);

        if (search.ToDate.HasValue)
            query = query.Where(l => l.CreatedAt <= search.ToDate.Value);

        return await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip(search.Offset)
            .Take(search.Limit)
            .ToListAsync(cancellationToken);
    }
}
