# EF Core Entities & DbContext - Sprint 1 Authentication

## 🏗️ **Entity Classes for ErpSystem.Api**

### **Base Entity Classes**

```csharp path=null start=null
// ErpSystem.Api/Models/Common/BaseEntity.cs
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Api.Models.Common
{
    public abstract class BaseEntity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public Guid CreatedBy { get; set; }
        
        public DateTime? UpdatedAt { get; set; }
        
        public Guid? UpdatedBy { get; set; }
        
        public bool IsDeleted { get; set; } = false;
        
        public DateTime? DeletedAt { get; set; }
        
        public Guid? DeletedBy { get; set; }
    }

    public abstract class TenantEntity : BaseEntity
    {
        [Required]
        public Guid TenantId { get; set; }
        
        public virtual Tenant? Tenant { get; set; }
    }
}
```

### **Identity Extensions**

```csharp path=null start=null
// ErpSystem.Api/Models/Identity/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Api.Models.Identity
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        [Required]
        public Guid TenantId { get; set; }
        
        [MaxLength(100)]
        public string? FirstName { get; set; }
        
        [MaxLength(100)]
        public string? LastName { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public DateTime? LastLoginDate { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public Guid CreatedBy { get; set; }
        
        public DateTime? UpdatedAt { get; set; }
        
        public Guid? UpdatedBy { get; set; }
        
        public bool IsDeleted { get; set; } = false;
        
        public DateTime? DeletedAt { get; set; }
        
        public Guid? DeletedBy { get; set; }
        
        // Navigation Properties
        public virtual Tenant? Tenant { get; set; }
        public virtual ICollection<UserSession> UserSessions { get; set; } = new List<UserSession>();
        
        // Computed Properties
        public string FullName => $"{FirstName} {LastName}".Trim();
        public string DisplayName => string.IsNullOrEmpty(FullName) ? UserName ?? Email ?? "Unknown" : FullName;
    }

    public class ApplicationRole : IdentityRole<Guid>
    {
        [Required]
        public Guid TenantId { get; set; }
        
        [MaxLength(500)]
        public string? Description { get; set; }
        
        public bool IsSystemRole { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public Guid CreatedBy { get; set; }
        
        public DateTime? UpdatedAt { get; set; }
        
        public Guid? UpdatedBy { get; set; }
        
        // Navigation Properties
        public virtual Tenant? Tenant { get; set; }
        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
```

### **Tenant Management Entities**

```csharp path=null start=null
// ErpSystem.Api/Models/Tenants/Tenant.cs
using ErpSystem.Api.Models.Common;
using ErpSystem.Api.Models.Identity;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Api.Models.Tenants
{
    public class Tenant : BaseEntity
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        
        [Required, MaxLength(200)]
        public string DisplayName { get; set; } = string.Empty;
        
        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;
        
        [MaxLength(100)]
        public string? Domain { get; set; }
        
        [MaxLength(255), EmailAddress]
        public string? ContactEmail { get; set; }
        
        [MaxLength(50)]
        public string? ContactPhone { get; set; }
        
        [MaxLength(500)]
        public string? Address { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        [MaxLength(50)]
        public string? SubscriptionPlan { get; set; }
        
        public DateTime? SubscriptionExpiry { get; set; }
        
        public int MaxUsers { get; set; } = 100;
        
        [MaxLength(1000)]
        public string? ConnectionString { get; set; } // For future multi-DB support
        
        // Navigation Properties
        public virtual ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
        public virtual ICollection<ApplicationRole> Roles { get; set; } = new List<ApplicationRole>();
        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
        
        // Computed Properties
        public bool IsExpired => SubscriptionExpiry.HasValue && SubscriptionExpiry.Value < DateTime.UtcNow;
        public int CurrentUserCount => Users?.Count(u => !u.IsDeleted && u.IsActive) ?? 0;
    }
}
```

### **Permission System Entities**

```csharp path=null start=null
// ErpSystem.Api/Models/Authorization/Permission.cs
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Api.Models.Authorization
{
    public class Permission
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required, MaxLength(200)]
        public string DisplayName { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string? Description { get; set; }
        
        [Required, MaxLength(50)]
        public string Module { get; set; } = string.Empty;
        
        [MaxLength(50)]
        public string? Category { get; set; }
        
        public bool IsSystemPermission { get; set; } = false;
        
        // Navigation Properties
        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }

    public class RolePermission
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        public Guid RoleId { get; set; }
        
        [Required]
        public Guid PermissionId { get; set; }
        
        [Required]
        public Guid TenantId { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public Guid CreatedBy { get; set; }
        
        // Navigation Properties
        public virtual ApplicationRole? Role { get; set; }
        public virtual Permission? Permission { get; set; }
        public virtual Tenant? Tenant { get; set; }
    }
}
```

### **Session Management Entity**

```csharp path=null start=null
// ErpSystem.Api/Models/Security/UserSession.cs
using ErpSystem.Api.Models.Common;
using ErpSystem.Api.Models.Identity;
using ErpSystem.Api.Models.Tenants;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Api.Models.Security
{
    public class UserSession : BaseEntity
    {
        [Required]
        public Guid UserId { get; set; }
        
        [Required]
        public Guid TenantId { get; set; }
        
        [Required, MaxLength(500)]
        public string SessionToken { get; set; } = string.Empty;
        
        [MaxLength(45)]
        public string? IPAddress { get; set; }
        
        [MaxLength(1000)]
        public string? UserAgent { get; set; }
        
        [Required]
        public DateTime ExpiresAt { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        // Navigation Properties
        public virtual ApplicationUser? User { get; set; }
        public virtual Tenant? Tenant { get; set; }
        
        // Computed Properties
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        public TimeSpan TimeRemaining => ExpiresAt > DateTime.UtcNow ? ExpiresAt - DateTime.UtcNow : TimeSpan.Zero;
    }
}
```

---

## 🗄️ **Application DbContext**

```csharp path=null start=null
// ErpSystem.Api/Data/ApplicationDbContext.cs
using ErpSystem.Api.Models.Authorization;
using ErpSystem.Api.Models.Identity;
using ErpSystem.Api.Models.Security;
using ErpSystem.Api.Models.Tenants;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Data
{
    public class ApplicationDbContext : IdentityDbContext<
        ApplicationUser, 
        ApplicationRole, 
        Guid,
        IdentityUserClaim<Guid>,
        IdentityUserRole<Guid>,
        IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>,
        IdentityUserToken<Guid>>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
        
        // DbSets
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            
            ConfigureIdentityTables(builder);
            ConfigureTenantTables(builder);
            ConfigureAuthorizationTables(builder);
            ConfigureSecurityTables(builder);
            ConfigureIndexes(builder);
            SeedDefaultData(builder);
        }
        
        private void ConfigureIdentityTables(ModelBuilder builder)
        {
            // Customize table names
            builder.Entity<ApplicationUser>().ToTable("AspNetUsers");
            builder.Entity<ApplicationRole>().ToTable("AspNetRoles");
            builder.Entity<IdentityUserRole<Guid>>().ToTable("AspNetUserRoles");
            builder.Entity<IdentityUserClaim<Guid>>().ToTable("AspNetUserClaims");
            builder.Entity<IdentityUserLogin<Guid>>().ToTable("AspNetUserLogins");
            builder.Entity<IdentityUserToken<Guid>>().ToTable("AspNetUserTokens");
            builder.Entity<IdentityRoleClaim<Guid>>().ToTable("AspNetRoleClaims");
            
            // ApplicationUser configuration
            builder.Entity<ApplicationUser>(entity =>
            {
                entity.HasOne(u => u.Tenant)
                    .WithMany(t => t.Users)
                    .HasForeignKey(u => u.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                    
                entity.HasIndex(u => new { u.TenantId, u.Email })
                    .IsUnique()
                    .HasFilter("[Email] IS NOT NULL AND [IsDeleted] = 0");
                    
                entity.HasIndex(u => new { u.TenantId, u.UserName })
                    .IsUnique()
                    .HasFilter("[UserName] IS NOT NULL AND [IsDeleted] = 0");
            });
            
            // ApplicationRole configuration
            builder.Entity<ApplicationRole>(entity =>
            {
                entity.HasOne(r => r.Tenant)
                    .WithMany(t => t.Roles)
                    .HasForeignKey(r => r.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                    
                entity.HasIndex(r => new { r.TenantId, r.Name })
                    .IsUnique()
                    .HasFilter("[Name] IS NOT NULL");
            });
        }
        
        private void ConfigureTenantTables(ModelBuilder builder)
        {
            builder.Entity<Tenant>(entity =>
            {
                entity.HasIndex(t => t.Code)
                    .IsUnique()
                    .HasFilter("[IsDeleted] = 0");
                    
                entity.HasIndex(t => t.Domain)
                    .IsUnique()
                    .HasFilter("[Domain] IS NOT NULL AND [IsDeleted] = 0");
            });
        }
        
        private void ConfigureAuthorizationTables(ModelBuilder builder)
        {
            builder.Entity<Permission>(entity =>
            {
                entity.HasIndex(p => p.Name).IsUnique();
                entity.HasIndex(p => new { p.Module, p.Category });
            });
            
            builder.Entity<RolePermission>(entity =>
            {
                entity.HasIndex(rp => new { rp.RoleId, rp.PermissionId, rp.TenantId })
                    .IsUnique();
                    
                entity.HasOne(rp => rp.Role)
                    .WithMany(r => r.RolePermissions)
                    .HasForeignKey(rp => rp.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
                    
                entity.HasOne(rp => rp.Permission)
                    .WithMany(p => p.RolePermissions)
                    .HasForeignKey(rp => rp.PermissionId)
                    .OnDelete(DeleteBehavior.Cascade);
                    
                entity.HasOne(rp => rp.Tenant)
                    .WithMany(t => t.RolePermissions)
                    .HasForeignKey(rp => rp.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
        
        private void ConfigureSecurityTables(ModelBuilder builder)
        {
            builder.Entity<UserSession>(entity =>
            {
                entity.HasOne(us => us.User)
                    .WithMany(u => u.UserSessions)
                    .HasForeignKey(us => us.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                    
                entity.HasOne(us => us.Tenant)
                    .WithMany()
                    .HasForeignKey(us => us.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                    
                entity.HasIndex(us => us.SessionToken).IsUnique();
                entity.HasIndex(us => new { us.UserId, us.IsActive });
                entity.HasIndex(us => us.ExpiresAt);
            });
        }
        
        private void ConfigureIndexes(ModelBuilder builder)
        {
            // Performance indexes for tenant isolation
            builder.Entity<ApplicationUser>()
                .HasIndex(u => new { u.TenantId, u.IsActive, u.IsDeleted });
                
            builder.Entity<ApplicationRole>()
                .HasIndex(r => new { r.TenantId, r.IsSystemRole });
                
            builder.Entity<RolePermission>()
                .HasIndex(rp => new { rp.TenantId, rp.RoleId });
        }
        
        private void SeedDefaultData(ModelBuilder builder)
        {
            // System Permissions
            var systemPermissions = new[]
            {
                new Permission { Id = Guid.NewGuid(), Name = "users.view", DisplayName = "View Users", Module = "UserManagement", Category = "Users", IsSystemPermission = true },
                new Permission { Id = Guid.NewGuid(), Name = "users.create", DisplayName = "Create Users", Module = "UserManagement", Category = "Users", IsSystemPermission = true },
                new Permission { Id = Guid.NewGuid(), Name = "users.edit", DisplayName = "Edit Users", Module = "UserManagement", Category = "Users", IsSystemPermission = true },
                new Permission { Id = Guid.NewGuid(), Name = "users.delete", DisplayName = "Delete Users", Module = "UserManagement", Category = "Users", IsSystemPermission = true },
                new Permission { Id = Guid.NewGuid(), Name = "roles.view", DisplayName = "View Roles", Module = "UserManagement", Category = "Roles", IsSystemPermission = true },
                new Permission { Id = Guid.NewGuid(), Name = "roles.create", DisplayName = "Create Roles", Module = "UserManagement", Category = "Roles", IsSystemPermission = true },
                new Permission { Id = Guid.NewGuid(), Name = "roles.edit", DisplayName = "Edit Roles", Module = "UserManagement", Category = "Roles", IsSystemPermission = true },
                new Permission { Id = Guid.NewGuid(), Name = "roles.delete", DisplayName = "Delete Roles", Module = "UserManagement", Category = "Roles", IsSystemPermission = true },
                new Permission { Id = Guid.NewGuid(), Name = "tenants.view", DisplayName = "View Tenants", Module = "TenantManagement", Category = "Tenants", IsSystemPermission = true },
                new Permission { Id = Guid.NewGuid(), Name = "tenants.create", DisplayName = "Create Tenants", Module = "TenantManagement", Category = "Tenants", IsSystemPermission = true },
                new Permission { Id = Guid.NewGuid(), Name = "tenants.edit", DisplayName = "Edit Tenants", Module = "TenantManagement", Category = "Tenants", IsSystemPermission = true },
                new Permission { Id = Guid.NewGuid(), Name = "tenants.delete", DisplayName = "Delete Tenants", Module = "TenantManagement", Category = "Tenants", IsSystemPermission = true }
            };
            
            builder.Entity<Permission>().HasData(systemPermissions);
            
            // Default System Tenant
            var systemTenantId = Guid.NewGuid();
            var systemTenant = new Tenant
            {
                Id = systemTenantId,
                Name = "System",
                DisplayName = "System Administration",
                Code = "SYSTEM",
                IsActive = true,
                MaxUsers = int.MaxValue,
                CreatedBy = Guid.Empty,
                CreatedAt = DateTime.UtcNow
            };
            
            builder.Entity<Tenant>().HasData(systemTenant);
        }
        
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateAuditFields();
            return await base.SaveChangesAsync(cancellationToken);
        }
        
        public override int SaveChanges()
        {
            UpdateAuditFields();
            return base.SaveChanges();
        }
        
        private void UpdateAuditFields()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is BaseEntity && (e.State == EntityState.Added || e.State == EntityState.Modified));
                
            foreach (var entry in entries)
            {
                var entity = (BaseEntity)entry.Entity;
                
                if (entry.State == EntityState.Added)
                {
                    entity.CreatedAt = DateTime.UtcNow;
                    // CreatedBy should be set by the service layer
                }
                else if (entry.State == EntityState.Modified)
                {
                    entity.UpdatedAt = DateTime.UtcNow;
                    // UpdatedBy should be set by the service layer
                }
            }
        }
    }
}
```

---

## ⚙️ **Service Registration**

```csharp path=null start=null
// ErpSystem.Api/Program.cs - Database Configuration
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly("ErpSystem.Api")
    ));

builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();
```

---

## 📋 **Initial Migration Command**

Run these commands in your ErpSystem.Api project:

```bash
# Install EF Core tools if not already installed
dotnet tool install --global dotnet-ef

# Add initial migration
dotnet ef migrations add InitialCreate

# Update database
dotnet ef database update
```

---

## 🎯 **Next Steps**

1. **Create the entities** in your ErpSystem.Api project following the folder structure above
2. **Update your connection string** in appsettings.json
3. **Run the migration** to create the database schema
4. **Implement the API controllers** using these entities
5. **Add tenant context middleware** for automatic tenant filtering

Would you like me to create the API controllers and DTOs next, or the tenant context middleware for automatic data isolation? 🚀