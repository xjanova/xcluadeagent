using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace XcluadeAgent.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Projects table
            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Repository = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Branch = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UseReleases = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReleaseTagPattern = table.Column<string>(type: "TEXT", nullable: true),
                    LocalPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSyncedVersion = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    Framework = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoDetectFramework = table.Column<bool>(type: "INTEGER", nullable: false),
                    Environment = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Config = table.Column<string>(type: "TEXT", nullable: false),
                    WebhookSecret = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            // SyncHistories table
            migrationBuilder.CreateTable(
                name: "SyncHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Trigger = table.Column<int>(type: "INTEGER", nullable: false),
                    InitiatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FromVersion = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ToVersion = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    FilesChanged = table.Column<int>(type: "INTEGER", nullable: false),
                    BytesTransferred = table.Column<long>(type: "INTEGER", nullable: false),
                    BackupPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsRollback = table.Column<bool>(type: "INTEGER", nullable: false),
                    RollbackFromId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CommandResults = table.Column<string>(type: "TEXT", nullable: false),
                    HealthCheckResult = table.Column<string>(type: "TEXT", nullable: true),
                    AiAnalysis = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncHistories", x => x.Id);
                });

            // Users table
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastLoginIp = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    FailedLoginAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwoFactorSecret = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    GitHubId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    GitHubUsername = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    NotificationSettings = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectAccess = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            // AuditLogs table
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            // Indexes
            migrationBuilder.CreateIndex(
                name: "IX_Projects_Name",
                table: "Projects",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Repository",
                table: "Projects",
                column: "Repository");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Status",
                table: "Projects",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Enabled",
                table: "Projects",
                column: "Enabled");

            migrationBuilder.CreateIndex(
                name: "IX_SyncHistories_ProjectId",
                table: "SyncHistories",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncHistories_StartedAt",
                table: "SyncHistories",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SyncHistories_Success",
                table: "SyncHistories",
                column: "Success");

            migrationBuilder.CreateIndex(
                name: "IX_SyncHistories_ProjectId_StartedAt",
                table: "SyncHistories",
                columns: new[] { "ProjectId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_GitHubId",
                table: "Users",
                column: "GitHubId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType",
                table: "AuditLogs",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            // Seed default admin user
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Username", "Email", "PasswordHash", "DisplayName", "Role", "IsActive", "FailedLoginAttempts", "TwoFactorEnabled", "NotificationSettings", "ProjectAccess", "CreatedAt", "UpdatedAt" },
                values: new object[] {
                    new Guid("00000000-0000-0000-0000-000000000001"),
                    "admin",
                    "admin@localhost",
                    "$2a$11$rBLRstOZETqF2HP.Xn/bW.8Yk3XZbQEBNMblDUKCaGpZ7f.UL6Rfy", // admin123
                    "Administrator",
                    3, // SuperAdmin
                    true,
                    0,
                    false,
                    "{}",
                    "[]",
                    DateTime.UtcNow,
                    DateTime.UtcNow
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AuditLogs");
            migrationBuilder.DropTable(name: "Users");
            migrationBuilder.DropTable(name: "SyncHistories");
            migrationBuilder.DropTable(name: "Projects");
        }
    }
}
