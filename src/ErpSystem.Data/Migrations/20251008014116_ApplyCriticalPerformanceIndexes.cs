using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApplyCriticalPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ===========================================
            // CRITICAL PERFORMANCE INDEXES
            // ===========================================

            // *** AuditLogs Table Indexes ***
            // High-frequency queries: date range, user, resource, action searches
            
            // Composite index for common filter combinations
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_AuditLogs_TenantId_Timestamp_Action] 
                ON [AuditLogs] ([TenantId] ASC, [Timestamp] DESC, [Action] ASC)
                INCLUDE ([UserId], [Username], [Resource], [ResourceId], [IpAddress])");

            // User-specific audit queries  
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_AuditLogs_UserId_Timestamp] 
                ON [AuditLogs] ([UserId] ASC, [Timestamp] DESC)
                INCLUDE ([Action], [Resource], [ResourceId])");

            // Resource-specific queries
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_AuditLogs_Resource_Timestamp] 
                ON [AuditLogs] ([Resource] ASC, [Timestamp] DESC)
                INCLUDE ([UserId], [Username], [Action], [ResourceId])");

            // Date range queries (most common)
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_AuditLogs_Timestamp_TenantId] 
                ON [AuditLogs] ([Timestamp] DESC, [TenantId] ASC)
                INCLUDE ([UserId], [Username], [Action], [Resource])");

            // IP Address security analysis
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_AuditLogs_IpAddress_Timestamp] 
                ON [AuditLogs] ([IpAddress] ASC, [Timestamp] DESC)
                INCLUDE ([UserId], [Action], [Resource])");

            // *** SecurityLogs Table Indexes ***
            
            // Security monitoring queries
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_SecurityLogs_TenantId_Action_Timestamp] 
                ON [SecurityLogs] ([TenantId] ASC, [Action] ASC, [Timestamp] DESC)
                INCLUDE ([UserId], [Username], [IpAddress], [Success], [Details])");

            // Failed login attempts analysis
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_SecurityLogs_Success_IpAddress_Timestamp] 
                ON [SecurityLogs] ([Success] ASC, [IpAddress] ASC, [Timestamp] DESC)
                INCLUDE ([Action], [Username], [Details])");

            // User security events
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_SecurityLogs_UserId_Timestamp] 
                ON [SecurityLogs] ([UserId] ASC, [Timestamp] DESC)
                INCLUDE ([Action], [Success], [IpAddress])");

            // *** Users Table Indexes ***
            
            // Login and authentication queries
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_Users_Email_IsActive] 
                ON [Users] ([Email] ASC, [IsActive] ASC)
                INCLUDE ([UserName], [TenantId], [FirstName], [LastName])");

            // Username lookups (if UserName is not already indexed)
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_Users_UserName_IsActive] 
                ON [Users] ([UserName] ASC, [IsActive] ASC)
                INCLUDE ([Email], [TenantId], [FirstName], [LastName])");

            // Tenant user queries
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_Users_TenantId_IsActive] 
                ON [Users] ([TenantId] ASC, [IsActive] ASC)
                INCLUDE ([UserName], [Email], [FirstName], [LastName], [LastLoginDate])");

            // Last login analysis
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_Users_LastLoginDate_IsActive] 
                ON [Users] ([LastLoginDate] DESC, [IsActive] ASC)
                INCLUDE ([UserName], [Email], [TenantId])");

            // *** UserTenants Table Indexes ***
            
            // Primary user-tenant relationships
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_UserTenants_UserId_Status_ExpiresAt] 
                ON [UserTenants] ([UserId] ASC, [Status] ASC, [ExpiresAt] ASC)
                INCLUDE ([TenantId], [AccessLevel], [IsDefault], [GrantedAt])");

            // Tenant user listing
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_UserTenants_TenantId_Status_IsDeleted] 
                ON [UserTenants] ([TenantId] ASC, [Status] ASC, [IsDeleted] ASC)
                INCLUDE ([UserId], [AccessLevel], [IsDefault], [GrantedAt])");

            // Default tenant lookup
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_UserTenants_UserId_IsDefault_Status] 
                ON [UserTenants] ([UserId] ASC, [IsDefault] ASC, [Status] ASC)
                INCLUDE ([TenantId], [AccessLevel])");

            // *** Tenants Table Indexes ***
            
            // Active tenants lookup
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_Tenants_Status_IsDeleted] 
                ON [Tenants] ([Status] ASC, [IsDeleted] ASC)
                INCLUDE ([Name], [Code], [Domain])");

            // Domain-based tenant resolution
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_Tenants_Domain_Status] 
                ON [Tenants] ([Domain] ASC, [Status] ASC)
                INCLUDE ([Name], [Code])");

            // Tenant code lookups
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_Tenants_Code_Status] 
                ON [Tenants] ([Code] ASC, [Status] ASC)
                INCLUDE ([Name], [Domain])");

            // *** UserSessions Table Indexes ***
            
            // Active sessions lookup
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_UserSessions_UserId_IsActive_LastActivityTime] 
                ON [UserSessions] ([UserId] ASC, [IsActive] ASC, [LastActivityTime] DESC)
                INCLUDE ([JwtTokenId], [IpAddress], [UserAgent], [LoginTime])");

            // Session cleanup queries
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_UserSessions_LastActivityTime_IsActive] 
                ON [UserSessions] ([LastActivityTime] ASC, [IsActive] ASC)
                INCLUDE ([UserId], [JwtTokenId])");

            // JWT token validation
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_UserSessions_JwtTokenId_IsActive] 
                ON [UserSessions] ([JwtTokenId] ASC, [IsActive] ASC)
                INCLUDE ([UserId], [LastActivityTime], [IpAddress])");

            // IP-based session analysis
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_UserSessions_IpAddress_LoginTime] 
                ON [UserSessions] ([IpAddress] ASC, [LoginTime] DESC)
                INCLUDE ([UserId], [UserAgent], [IsActive])");

            // *** RefreshTokens Table Indexes ***
            
            // Token validation queries
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_RefreshTokens_TokenHash_IsRevoked_ExpiresAt] 
                ON [RefreshTokens] ([TokenHash] ASC, [IsRevoked] ASC, [ExpiresAt] DESC)
                INCLUDE ([UserId], [UsageCount])");

            // User token cleanup
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_RefreshTokens_UserId_ExpiresAt_IsRevoked] 
                ON [RefreshTokens] ([UserId] ASC, [ExpiresAt] DESC, [IsRevoked] ASC)
                INCLUDE ([TokenHash], [UsageCount])");

            // Token cleanup job
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_RefreshTokens_ExpiresAt_IsRevoked] 
                ON [RefreshTokens] ([ExpiresAt] ASC, [IsRevoked] ASC)
                INCLUDE ([UserId], [TokenHash])");

            // *** BlacklistedTokens Table Indexes ***
            
            // Token validation (primary use case)
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_BlacklistedTokens_Jti_ExpiresAt] 
                ON [BlacklistedTokens] ([Jti] ASC, [ExpiresAt] DESC)
                INCLUDE ([Reason])");

            // Cleanup job (skip if already exists)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BlacklistedTokens_ExpiresAt' AND object_id = OBJECT_ID('BlacklistedTokens'))
                CREATE NONCLUSTERED INDEX [IX_BlacklistedTokens_ExpiresAt] 
                ON [BlacklistedTokens] ([ExpiresAt] ASC)");

            // *** SystemSettings Table Indexes ***
            
            // Settings retrieval by tenant and key (skip if already exists)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SystemSettings_TenantId_Key' AND object_id = OBJECT_ID('SystemSettings'))
                CREATE NONCLUSTERED INDEX [IX_SystemSettings_TenantId_Key] 
                ON [SystemSettings] ([TenantId] ASC, [Key] ASC)
                INCLUDE ([Value], [Description], [IsEncrypted])");

            // Settings by encryption status
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_SystemSettings_TenantId_IsEncrypted] 
                ON [SystemSettings] ([TenantId] ASC, [IsEncrypted] ASC)
                INCLUDE ([Key], [Value], [Description])");

            // *** EmailTemplates Table Indexes ***
            
            // Template lookup by tenant and name (skip if already exists)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmailTemplates_TenantId_Name' AND object_id = OBJECT_ID('EmailTemplates'))
                CREATE NONCLUSTERED INDEX [IX_EmailTemplates_TenantId_Name] 
                ON [EmailTemplates] ([TenantId] ASC, [Name] ASC)
                INCLUDE ([Subject], [HtmlBody], [IsActive])");

            // Templates by module and category
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmailTemplates_Module_Category_IsActive' AND object_id = OBJECT_ID('EmailTemplates'))
                CREATE NONCLUSTERED INDEX [IX_EmailTemplates_Module_Category_IsActive] 
                ON [EmailTemplates] ([Module] ASC, [Category] ASC, [IsActive] ASC)
                INCLUDE ([TenantId], [Name], [Subject])");

            // *** Permissions and Roles Indexes ***
            
            // Role permissions lookup (skip if already exists)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RolePermissions_RoleId' AND object_id = OBJECT_ID('RolePermissions'))
                CREATE NONCLUSTERED INDEX [IX_RolePermissions_RoleId] 
                ON [RolePermissions] ([RoleId] ASC)
                INCLUDE ([PermissionId])");

            // Permission roles lookup (skip if already exists)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RolePermissions_PermissionId' AND object_id = OBJECT_ID('RolePermissions'))
                CREATE NONCLUSTERED INDEX [IX_RolePermissions_PermissionId] 
                ON [RolePermissions] ([PermissionId] ASC)
                INCLUDE ([RoleId])");

            // User roles lookup (skip if already exists)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserRoles_UserId' AND object_id = OBJECT_ID('UserRoles'))
                CREATE NONCLUSTERED INDEX [IX_UserRoles_UserId] 
                ON [UserRoles] ([UserId] ASC)
                INCLUDE ([RoleId])");

            // Role users lookup (skip if already exists)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserRoles_RoleId' AND object_id = OBJECT_ID('UserRoles'))
                CREATE NONCLUSTERED INDEX [IX_UserRoles_RoleId] 
                ON [UserRoles] ([RoleId] ASC)
                INCLUDE ([UserId])");

            // ===========================================
            // CLEANUP OLD/EXPIRED DATA INDEXES
            // ===========================================

            // Audit logs cleanup by creation date
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_AuditLogs_CreatedAt_TenantId] 
                ON [AuditLogs] ([CreatedAt] ASC, [TenantId] ASC)");

            // Security logs cleanup
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_SecurityLogs_CreatedAt_TenantId] 
                ON [SecurityLogs] ([CreatedAt] ASC, [TenantId] ASC)");

            // ===========================================
            // ANALYTICS AND REPORTING INDEXES
            // ===========================================

            // User activity analysis
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_AuditLogs_TenantId_UserId_Timestamp] 
                ON [AuditLogs] ([TenantId] ASC, [UserId] ASC, [Timestamp] DESC)
                INCLUDE ([Action], [Resource])");

            // Resource usage analysis
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_AuditLogs_TenantId_Resource_Action] 
                ON [AuditLogs] ([TenantId] ASC, [Resource] ASC, [Action] ASC)
                INCLUDE ([Timestamp], [UserId])");

            // Security trends analysis
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_SecurityLogs_TenantId_Timestamp_Action] 
                ON [SecurityLogs] ([TenantId] ASC, [Timestamp] DESC, [Action] ASC)
                INCLUDE ([Success], [IpAddress])");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("01161dc9-56bf-4cc7-a73d-93910712d089"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("0eafc39c-2d6f-4de5-a312-a9210dd7fc02"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("315433c4-0dd6-4c62-8cb1-02573cf77774"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("63a3ef5d-b4a9-4dc3-b183-5afeaa928e42"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("9d95d543-1e7b-46c1-aae7-09063d382975"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("dda691ce-783a-436f-b8a0-485376bc53b5"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("fa728d45-d9c8-4d0f-b209-a56e878d9fc7"));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7358));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7391));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7442));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7444));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7604));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7612));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7616));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7619));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7628));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7632));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7635));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7638));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7645));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7652));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7655));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7658));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7697));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7701));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7703));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7706));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7741));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7746));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7747));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7748));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7749));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7750));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7751));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7752));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7752));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7753));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7754));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7755));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7756));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7756));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7757));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7757));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7782));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7784));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7784));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7785));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7786));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7786));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7787));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7787));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7788));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7788));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7789));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7790));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7790));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7791));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7791));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7820));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7822));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7823));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7823));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7824));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7825));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7825));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7826));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7826));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7827));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7837));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7839));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7840));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("300cf359-158b-43ff-8bc4-894112586878"), null, new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7487), null, null, null, null, null, new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7484), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("3a4235d0-33ed-4b72-b360-77206198c1e2"), null, new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7545), null, null, null, null, null, new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7545), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("6ea6924e-4945-421e-a152-877837aa0f84"), null, new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7526), null, null, null, null, null, new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7526), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("795d9774-8433-44bd-9376-04be70e80e3b"), null, new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7537), null, null, null, null, null, new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7536), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("c5e44028-33d7-4ce2-ba6a-3b919ab840f0"), null, new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7499), null, null, null, null, null, new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7499), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("d8ed6a60-c1ec-4f5e-bd7e-40492f0ef242"), null, new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7518), null, null, null, null, null, new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7517), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("e2f14b95-99e8-4be5-8fe5-56758b0d5fa3"), null, new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7509), null, null, null, null, null, new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7508), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 41, 15, 885, DateTimeKind.Utc).AddTicks(7243));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop all performance indexes in reverse order
            
            // Analytics indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_SecurityLogs_TenantId_Timestamp_Action] ON [SecurityLogs]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_AuditLogs_TenantId_Resource_Action] ON [AuditLogs]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_AuditLogs_TenantId_UserId_Timestamp] ON [AuditLogs]");
            
            // Cleanup indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_SecurityLogs_CreatedAt_TenantId] ON [SecurityLogs]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_AuditLogs_CreatedAt_TenantId] ON [AuditLogs]");
            
            // Role and permission indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_UserRoles_RoleId] ON [UserRoles]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_UserRoles_UserId] ON [UserRoles]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_RolePermissions_PermissionId] ON [RolePermissions]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_RolePermissions_RoleId] ON [RolePermissions]");
            
            // Email template indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_EmailTemplates_Module_Category_IsActive] ON [EmailTemplates]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_EmailTemplates_TenantId_Name] ON [EmailTemplates]");
            
            // System settings indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_SystemSettings_TenantId_IsEncrypted] ON [SystemSettings]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_SystemSettings_TenantId_Key] ON [SystemSettings]");
            
            // Token indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_BlacklistedTokens_ExpiresAt] ON [BlacklistedTokens]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_BlacklistedTokens_Jti_ExpiresAt] ON [BlacklistedTokens]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_RefreshTokens_ExpiresAt_IsRevoked] ON [RefreshTokens]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_RefreshTokens_UserId_ExpiresAt_IsRevoked] ON [RefreshTokens]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_RefreshTokens_TokenHash_IsRevoked_ExpiresAt] ON [RefreshTokens]");
            
            // Session indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_UserSessions_IpAddress_LoginTime] ON [UserSessions]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_UserSessions_JwtTokenId_IsActive] ON [UserSessions]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_UserSessions_LastActivityTime_IsActive] ON [UserSessions]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_UserSessions_UserId_IsActive_LastActivityTime] ON [UserSessions]");
            
            // Tenant indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_Tenants_Code_Status] ON [Tenants]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_Tenants_Domain_Status] ON [Tenants]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_Tenants_Status_IsDeleted] ON [Tenants]");
            
            // User-tenant relationship indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_UserTenants_UserId_IsDefault_Status] ON [UserTenants]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_UserTenants_TenantId_Status_IsDeleted] ON [UserTenants]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_UserTenants_UserId_Status_ExpiresAt] ON [UserTenants]");
            
            // User indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_Users_LastLoginDate_IsActive] ON [Users]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_Users_TenantId_IsActive] ON [Users]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_Users_UserName_IsActive] ON [Users]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_Users_Email_IsActive] ON [Users]");
            
            // Security log indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_SecurityLogs_UserId_Timestamp] ON [SecurityLogs]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_SecurityLogs_Success_IpAddress_Timestamp] ON [SecurityLogs]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_SecurityLogs_TenantId_Action_Timestamp] ON [SecurityLogs]");
            
            // Audit log indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_AuditLogs_IpAddress_Timestamp] ON [AuditLogs]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_AuditLogs_Timestamp_TenantId] ON [AuditLogs]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_AuditLogs_Resource_Timestamp] ON [AuditLogs]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_AuditLogs_UserId_Timestamp] ON [AuditLogs]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_AuditLogs_TenantId_Timestamp_Action] ON [AuditLogs]");
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("300cf359-158b-43ff-8bc4-894112586878"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("3a4235d0-33ed-4b72-b360-77206198c1e2"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("6ea6924e-4945-421e-a152-877837aa0f84"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("795d9774-8433-44bd-9376-04be70e80e3b"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("c5e44028-33d7-4ce2-ba6a-3b919ab840f0"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("d8ed6a60-c1ec-4f5e-bd7e-40492f0ef242"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("e2f14b95-99e8-4be5-8fe5-56758b0d5fa3"));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(7881));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(7908));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(7910));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(7912));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8138));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8145));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8148));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8151));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8158));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8162));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8165));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8168));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8176));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8181));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8185));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8188));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8194));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8197));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8200));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8202));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8234));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8235));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8236));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8237));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8262));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8264));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8264));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8265));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8266));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8267));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8268));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8268));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8269));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8270));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8270));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8271));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8296));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8297));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8298));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8298));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8299));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8299));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8300));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8300));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8301));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8301));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8302));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8302));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8303));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8303));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8304));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8336));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8337));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8338));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8339));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8339));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8340));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8340));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8341));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8341));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8342));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8354));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8354));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8355));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("01161dc9-56bf-4cc7-a73d-93910712d089"), null, new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(7977), null, null, null, null, null, new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(7977), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("0eafc39c-2d6f-4de5-a312-a9210dd7fc02"), null, new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8065), null, null, null, null, null, new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8065), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("315433c4-0dd6-4c62-8cb1-02573cf77774"), null, new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8075), null, null, null, null, null, new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8074), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("63a3ef5d-b4a9-4dc3-b183-5afeaa928e42"), null, new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(7967), null, null, null, null, null, new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(7967), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("9d95d543-1e7b-46c1-aae7-09063d382975"), null, new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8056), null, null, null, null, null, new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8056), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("dda691ce-783a-436f-b8a0-485376bc53b5"), null, new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(7955), null, null, null, null, null, new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(7953), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("fa728d45-d9c8-4d0f-b209-a56e878d9fc7"), null, new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8046), null, null, null, null, null, new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(8046), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 1, 37, 52, 254, DateTimeKind.Utc).AddTicks(7781));
        }
    }
}
