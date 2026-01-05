using Microsoft.EntityFrameworkCore;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Infrastructure.Persistence;

/// <summary>
/// Application database context
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<SyncHistory> SyncHistories => Set<SyncHistory>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ignore types that are serialized as JSON within owned types
        // These are nested in ProjectConfig which is stored as JSON
        modelBuilder.Ignore<FilePermissions>();
        modelBuilder.Ignore<HealthCheckConfig>();
        modelBuilder.Ignore<ProjectBackupConfig>();

        // Project configuration
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.Repository);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Enabled);

            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Repository).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Branch).HasMaxLength(100);
            entity.Property(e => e.LocalPath).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.Environment).HasMaxLength(50);
            entity.Property(e => e.LastSyncedVersion).HasMaxLength(100);
            entity.Property(e => e.LastError).HasMaxLength(5000);
            entity.Property(e => e.WebhookSecret).HasMaxLength(100);

            // Store Config as JSON (includes Permissions and HealthCheck)
            entity.OwnsOne(e => e.Config, config =>
            {
                config.ToJson();
            });
        });

        // SyncHistory configuration
        modelBuilder.Entity<SyncHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.StartedAt);
            entity.HasIndex(e => e.Success);
            entity.HasIndex(e => new { e.ProjectId, e.StartedAt });

            entity.Property(e => e.ProjectName).HasMaxLength(100);
            entity.Property(e => e.InitiatedBy).HasMaxLength(100);
            entity.Property(e => e.FromVersion).HasMaxLength(100);
            entity.Property(e => e.ToVersion).HasMaxLength(100);
            entity.Property(e => e.ErrorMessage).HasMaxLength(5000);
            entity.Property(e => e.BackupPath).HasMaxLength(1000);

            // Store complex properties as JSON
            entity.OwnsMany(e => e.CommandResults, cmd =>
            {
                cmd.ToJson();
            });

            entity.OwnsOne(e => e.HealthCheckResult, hc =>
            {
                hc.ToJson();
            });

            entity.OwnsOne(e => e.AiAnalysis, ai =>
            {
                ai.ToJson();
            });
        });

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.GitHubId);

            entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(100);
            entity.Property(e => e.LastLoginIp).HasMaxLength(50);
            entity.Property(e => e.TwoFactorSecret).HasMaxLength(100);
            entity.Property(e => e.RefreshToken).HasMaxLength(100);
            entity.Property(e => e.GitHubId).HasMaxLength(50);
            entity.Property(e => e.GitHubUsername).HasMaxLength(50);
            entity.Property(e => e.GitHubAccessToken).HasMaxLength(500);
            entity.Property(e => e.GitHubTokenScopes).HasMaxLength(200);

            entity.OwnsOne(e => e.NotificationSettings, ns =>
            {
                ns.ToJson();
            });

            // Store ProjectAccess as JSON array
            entity.Property(e => e.ProjectAccess)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<Guid>()
                );
        });

        // AuditLog configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => e.EntityType);
            entity.HasIndex(e => e.CreatedAt);

            entity.Property(e => e.Username).HasMaxLength(50);
            entity.Property(e => e.Action).HasMaxLength(100).IsRequired();
            entity.Property(e => e.EntityType).HasMaxLength(50);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
        });

        // Seed default admin user
        // SECURITY: Default password must be changed on first login
        var adminId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        modelBuilder.Entity<User>().HasData(new User
        {
            Id = adminId,
            Username = "admin",
            Email = "admin@localhost",
            // Default password: admin123 - MUST be changed on first login
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            DisplayName = "Administrator",
            Role = Core.Enums.UserRole.SuperAdmin,
            IsActive = true,
            RequirePasswordChange = true, // Force password change on first login
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }
}
