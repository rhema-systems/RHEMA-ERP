using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities;

namespace ErpSystem.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid, 
    Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>, ApplicationUserRole, 
    Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>,
    Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>, 
    Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>
{
    private readonly Guid? _tenantId;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, Guid? tenantId) : base(options)
    {
        _tenantId = tenantId;
    }

    // Core entities
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantModule> TenantModules { get; set; }
    
    // Settings entities
    public DbSet<EmailSettings> EmailSettings { get; set; }
    public DbSet<PasswordPolicy> PasswordPolicies { get; set; }
    public DbSet<SystemSettings> SystemSettings { get; set; }
    
    // Logging entities
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<SecurityLog> SecurityLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure Identity tables with custom names
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => new { e.TenantId, e.UserName }).IsUnique();
            entity.HasOne(e => e.Tenant).WithMany(t => t.Users).HasForeignKey(e => e.TenantId);
        });

        builder.Entity<ApplicationRole>(entity =>
        {
            entity.ToTable("Roles");
        });

        builder.Entity<ApplicationUserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasOne(ur => ur.User).WithMany(u => u.UserRoles).HasForeignKey(ur => ur.UserId);
            entity.HasOne(ur => ur.Role).WithMany(r => r.UserRoles).HasForeignKey(ur => ur.RoleId);
        });

        // Configure Tenant entity
        builder.Entity<Tenant>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure TenantModule entity
        builder.Entity<TenantModule>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ModuleName }).IsUnique();
        });
        
        // Configure EmailSettings entity
        builder.Entity<EmailSettings>(entity =>
        {
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.HasIndex(e => e.TenantId); // One email setting per tenant
        });
        
        // Configure PasswordPolicy entity
        builder.Entity<PasswordPolicy>(entity =>
        {
            entity.HasOne(p => p.Tenant).WithMany().HasForeignKey(p => p.TenantId);
            entity.HasIndex(p => p.TenantId); // One password policy per tenant
        });
        
        // Configure SystemSettings entity
        builder.Entity<SystemSettings>(entity =>
        {
            entity.HasOne(s => s.Tenant).WithMany().HasForeignKey(s => s.TenantId);
            entity.HasIndex(s => new { s.TenantId, s.Key }).IsUnique();
        });
        
        // Configure AuditLog entity
        builder.Entity<AuditLog>(entity =>
        {
            entity.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(a => a.Tenant).WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(a => a.Timestamp);
            entity.HasIndex(a => a.UserId);
            entity.HasIndex(a => a.Resource);
        });
        
        // Configure SecurityLog entity
        builder.Entity<SecurityLog>(entity =>
        {
            entity.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(s => s.Tenant).WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(s => s.Timestamp);
            entity.HasIndex(s => s.IpAddress);
            entity.HasIndex(s => s.Action);
        });

        // Apply global query filters for soft delete and multitenancy
        ApplyGlobalFilters(builder);

        // Seed initial data
        SeedData(builder);
    }

    private void ApplyGlobalFilters(ModelBuilder builder)
    {
        // Apply soft delete filter to all entities that inherit from BaseEntity
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var type = entityType.ClrType;
            if (typeof(BaseEntity).IsAssignableFrom(type))
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(SetSoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(type);
                method.Invoke(null, new object[] { builder, entityType });
            }

            // Apply tenant filter to all entities that inherit from TenantEntity
            if (typeof(TenantEntity).IsAssignableFrom(type) && _tenantId.HasValue)
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(SetTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(type);
                method.Invoke(null, new object[] { builder, entityType, _tenantId.Value });
            }
        }
    }

    private static void SetSoftDeleteFilter<TEntity>(ModelBuilder builder, Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType)
        where TEntity : BaseEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }

    private static void SetTenantFilter<TEntity>(ModelBuilder builder, Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType, Guid tenantId)
        where TEntity : TenantEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == tenantId);
    }

    private void SeedData(ModelBuilder builder)
    {
        // Seed default tenant
        var defaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        builder.Entity<Tenant>().HasData(
            new Tenant
            {
                Id = defaultTenantId,
                Name = "Default Tenant",
                Code = "DEFAULT",
                Description = "Default system tenant",
                Status = Shared.TenantStatus.Active,
                CreatedAt = DateTime.UtcNow
            }
        );

        // Seed default roles
        var superAdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var tenantAdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var managerRoleId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var employeeRoleId = Guid.Parse("00000000-0000-0000-0000-000000000004");

        builder.Entity<ApplicationRole>().HasData(
            new ApplicationRole { Id = superAdminRoleId, Name = Shared.Constants.Roles.SuperAdmin, NormalizedName = Shared.Constants.Roles.SuperAdmin.ToUpper(), IsSystemRole = true },
            new ApplicationRole { Id = tenantAdminRoleId, Name = Shared.Constants.Roles.TenantAdmin, NormalizedName = Shared.Constants.Roles.TenantAdmin.ToUpper(), IsSystemRole = true },
            new ApplicationRole { Id = managerRoleId, Name = Shared.Constants.Roles.Manager, NormalizedName = Shared.Constants.Roles.Manager.ToUpper(), IsSystemRole = true },
            new ApplicationRole { Id = employeeRoleId, Name = Shared.Constants.Roles.Employee, NormalizedName = Shared.Constants.Roles.Employee.ToUpper(), IsSystemRole = true }
        );

        // Seed default modules for default tenant
        var moduleIds = new List<Guid>
        {
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
        };

        var modules = new[]
        {
            Shared.Constants.Modules.Finance,
            Shared.Constants.Modules.HR,
            Shared.Constants.Modules.Sales,
            Shared.Constants.Modules.Procurement,
            Shared.Constants.Modules.Inventory,
            Shared.Constants.Modules.Marketing,
            Shared.Constants.Modules.WorkflowEngine
        };

        for (int i = 0; i < modules.Length; i++)
        {
            builder.Entity<TenantModule>().HasData(
                new TenantModule
                {
                    Id = moduleIds[i],
                    TenantId = defaultTenantId,
                    ModuleName = modules[i],
                    Status = Shared.ModuleStatus.Enabled,
                    EnabledDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                }
            );
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditableEntities();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        UpdateAuditableEntities();
        return base.SaveChanges();
    }

    private void UpdateAuditableEntities()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = DateTime.UtcNow;
                    break;
            }
        }
    }
}