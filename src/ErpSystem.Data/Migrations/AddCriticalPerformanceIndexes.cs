using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Adds critical performance indexes for high-frequency queries
    /// </summary>
    public partial class AddCriticalPerformanceIndexes : Migration
    {
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
                CREATE NONCLUSTERED INDEX [IX_UserSessions_UserId_IsActive_ExpiresAt] 
                ON [UserSessions] ([UserId] ASC, [IsActive] ASC, [ExpiresAt] DESC)
                INCLUDE ([JwtTokenId], [IpAddress], [UserAgent], [CreatedAt])");

            // Session cleanup queries
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_UserSessions_ExpiresAt_IsActive] 
                ON [UserSessions] ([ExpiresAt] ASC, [IsActive] ASC)
                INCLUDE ([UserId], [JwtTokenId])");

            // JWT token validation
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_UserSessions_JwtTokenId_IsActive] 
                ON [UserSessions] ([JwtTokenId] ASC, [IsActive] ASC)
                INCLUDE ([UserId], [ExpiresAt], [IpAddress])");

            // IP-based session analysis
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_UserSessions_IpAddress_CreatedAt] 
                ON [UserSessions] ([IpAddress] ASC, [CreatedAt] DESC)
                INCLUDE ([UserId], [UserAgent], [IsActive])");

            // *** RefreshTokens Table Indexes ***
            
            // Token validation queries
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_RefreshTokens_Token_IsRevoked_ExpiresAt] 
                ON [RefreshTokens] ([Token] ASC, [IsRevoked] ASC, [ExpiresAt] DESC)
                INCLUDE ([UserId], [IsUsed])");

            // User token cleanup
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_RefreshTokens_UserId_ExpiresAt_IsRevoked] 
                ON [RefreshTokens] ([UserId] ASC, [ExpiresAt] DESC, [IsRevoked] ASC)
                INCLUDE ([Token], [IsUsed])");

            // Token cleanup job
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_RefreshTokens_ExpiresAt_IsRevoked] 
                ON [RefreshTokens] ([ExpiresAt] ASC, [IsRevoked] ASC)
                INCLUDE ([UserId], [Token])");

            // *** BlacklistedTokens Table Indexes ***
            
            // Token validation (primary use case)
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_BlacklistedTokens_TokenHash_ExpiresAt] 
                ON [BlacklistedTokens] ([TokenHash] ASC, [ExpiresAt] DESC)
                INCLUDE ([Reason])");

            // Cleanup job
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_BlacklistedTokens_ExpiresAt] 
                ON [BlacklistedTokens] ([ExpiresAt] ASC)");

            // *** SystemSettings Table Indexes ***
            
            // Settings retrieval by tenant and key
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_SystemSettings_TenantId_Key] 
                ON [SystemSettings] ([TenantId] ASC, [Key] ASC)
                INCLUDE ([Value], [DataType], [IsEncrypted])");

            // Settings by category
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_SystemSettings_TenantId_Category] 
                ON [SystemSettings] ([TenantId] ASC, [Category] ASC)
                INCLUDE ([Key], [Value], [DataType])");

            // *** EmailTemplates Table Indexes ***
            
            // Template lookup by tenant and name
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_EmailTemplates_TenantId_Name] 
                ON [EmailTemplates] ([TenantId] ASC, [Name] ASC)
                INCLUDE ([Subject], [HtmlBody], [IsActive])");

            // Templates by module and category
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_EmailTemplates_Module_Category_IsActive] 
                ON [EmailTemplates] ([Module] ASC, [Category] ASC, [IsActive] ASC)
                INCLUDE ([TenantId], [Name], [Subject])");

            // *** Permissions and Roles Indexes ***
            
            // Role permissions lookup
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_RolePermissions_RoleId] 
                ON [RolePermissions] ([RoleId] ASC)
                INCLUDE ([PermissionId])");

            // Permission roles lookup
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_RolePermissions_PermissionId] 
                ON [RolePermissions] ([PermissionId] ASC)
                INCLUDE ([RoleId])");

            // User roles lookup
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_UserRoles_UserId] 
                ON [UserRoles] ([UserId] ASC)
                INCLUDE ([RoleId])");

            // Role users lookup
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_UserRoles_RoleId] 
                ON [UserRoles] ([RoleId] ASC)
                INCLUDE ([UserId])");

            // ===========================================
            // CLEANUP OLD/EXPIRED DATA INDEXES
            // ===========================================

            // These indexes optimize cleanup jobs for maintenance

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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop all indexes in reverse order
            
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
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_SystemSettings_TenantId_Category] ON [SystemSettings]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_SystemSettings_TenantId_Key] ON [SystemSettings]");
            
            // Token indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_BlacklistedTokens_ExpiresAt] ON [BlacklistedTokens]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_BlacklistedTokens_TokenHash_ExpiresAt] ON [BlacklistedTokens]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_RefreshTokens_ExpiresAt_IsRevoked] ON [RefreshTokens]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_RefreshTokens_UserId_ExpiresAt_IsRevoked] ON [RefreshTokens]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_RefreshTokens_Token_IsRevoked_ExpiresAt] ON [RefreshTokens]");
            
            // Session indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_UserSessions_IpAddress_CreatedAt] ON [UserSessions]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_UserSessions_JwtTokenId_IsActive] ON [UserSessions]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_UserSessions_ExpiresAt_IsActive] ON [UserSessions]");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_UserSessions_UserId_IsActive_ExpiresAt] ON [UserSessions]");
            
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
        }
    }
}