# Database-Agnostic Development Best Practices

## 🎯 **Overview**

This document outlines best practices for maintaining database portability in the ERP system. Following these guidelines ensures the application can migrate between different database providers (SQL Server, PostgreSQL, MySQL, Oracle, SQLite) with minimal code changes.

---

## 📊 **1. Entity Design Best Practices**

### ✅ **Use Standard Data Types**

```csharp
// ✅ GOOD - Works on all databases
public class Product : TenantEntity
{
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [Precision(18, 2)]  // Explicit precision for money
    public decimal Price { get; set; }
    
    public DateTime CreatedDate { get; set; }
    
    public Guid CategoryId { get; set; }
}

// ❌ AVOID - SQL Server specific types
public class ProductBad
{
    public string Name { get; set; }  // No length limit
    public SqlMoney Price { get; set; }  // SQL Server specific
    public SqlDateTime CreatedDate { get; set; }  // SQL Server specific
    public int Identity { get; set; }  // Database-specific identity
}
```

### ✅ **Primary Key Standards**

```csharp
// ✅ GOOD - Consistent GUID approach
public abstract class BaseEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
}

// ❌ AVOID - Database-specific identity columns
public class BadEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }  // Not portable to all databases
}
```

### ✅ **String Length Limits**

```csharp
// ✅ GOOD - Explicit length constraints
public class User : BaseEntity
{
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
}

// ❌ AVOID - Unlimited strings
public class BadUser
{
    public string FirstName { get; set; }  // Could be nvarchar(max) or text
    public string Email { get; set; }      // Unpredictable behavior
}
```

---

## 🏗️ **2. Entity Configuration Best Practices**

### ✅ **Portable Index Definitions**

```csharp
// ✅ GOOD - Works on all databases
protected override void OnModelCreating(ModelBuilder builder)
{
    builder.Entity<Tenant>(entity =>
    {
        // Composite index approach
        entity.HasIndex(t => new { t.Code, t.IsDeleted })
            .IsUnique()
            .HasDatabaseName("IX_Tenants_Code_NotDeleted");
            
        // Simple index
        entity.HasIndex(t => t.Name)
            .HasDatabaseName("IX_Tenants_Name");
    });
}

// ❌ AVOID - SQL Server specific filters
builder.Entity<Tenant>(entity =>
{
    entity.HasIndex(t => t.Code)
        .IsUnique()
        .HasFilter("[IsDeleted] = 0");  // T-SQL specific syntax
});
```

### ✅ **Database-Agnostic Constraints**

```csharp
// ✅ GOOD - EF Core abstractions
builder.Entity<User>(entity =>
{
    entity.Property(e => e.Email)
        .IsRequired()
        .HasMaxLength(255);
        
    entity.HasIndex(e => new { e.TenantId, e.Email, e.IsDeleted })
        .IsUnique();
});

// ❌ AVOID - Raw SQL constraints
builder.Entity<User>()
    .HasCheckConstraint("CK_User_Email", "Email LIKE '%@%'");  // SQL Server specific
```

### ✅ **Foreign Key Relationships**

```csharp
// ✅ GOOD - Standard EF Core relationships
builder.Entity<ApplicationUser>(entity =>
{
    entity.HasOne(u => u.Tenant)
        .WithMany(t => t.Users)
        .HasForeignKey(u => u.TenantId)
        .OnDelete(DeleteBehavior.Restrict);  // Portable behavior
});

// ❌ AVOID - Database-specific cascade rules
entity.HasOne(u => u.Tenant)
    .WithMany(t => t.Users)
    .HasForeignKey(u => u.TenantId)
    .OnDelete(DeleteBehavior.ClientCascade);  // Not supported everywhere
```

---

## 🔍 **3. Query Writing Best Practices**

### ✅ **Portable LINQ Queries**

```csharp
// ✅ GOOD - Works on all databases
public async Task<List<User>> GetActiveUsersAsync(Guid tenantId, string searchTerm)
{
    return await _context.Users
        .Where(u => u.TenantId == tenantId && 
                   u.IsActive && 
                   !u.IsDeleted)
        .Where(u => u.FirstName.ToLower().Contains(searchTerm.ToLower()) ||
                   u.LastName.ToLower().Contains(searchTerm.ToLower()) ||
                   u.Email.ToLower().Contains(searchTerm.ToLower()))
        .OrderBy(u => u.LastName)
        .ThenBy(u => u.FirstName)
        .ToListAsync();
}

// ❌ AVOID - Database-specific SQL
public async Task<List<User>> GetActiveUsersBad(Guid tenantId)
{
    return await _context.Users
        .FromSqlRaw(@"
            SELECT * FROM Users 
            WHERE TenantId = {0} 
            AND IsActive = 1 
            AND ISNULL(IsDeleted, 0) = 0", tenantId)  // SQL Server specific
        .ToListAsync();
}
```

### ✅ **Date/Time Handling**

```csharp
// ✅ GOOD - UTC and portable date functions
public async Task<List<Order>> GetRecentOrdersAsync()
{
    var cutoffDate = DateTime.UtcNow.AddDays(-30);
    
    return await _context.Orders
        .Where(o => o.CreatedAt >= cutoffDate)
        .OrderByDescending(o => o.CreatedAt)
        .ToListAsync();
}

// ❌ AVOID - Database-specific date functions
var orders = _context.Orders
    .Where(o => EF.Functions.DateDiffDay(o.CreatedAt, DateTime.Now) <= 30)  // SQL Server specific
    .ToList();
```

### ✅ **Null Handling**

```csharp
// ✅ GOOD - Explicit null checks
public async Task<List<Product>> GetProductsWithDescriptionAsync()
{
    return await _context.Products
        .Where(p => p.Description != null && p.Description != "")
        .ToListAsync();
}

// ❌ AVOID - Database-specific null functions
var products = _context.Products
    .Where(p => EF.Functions.IsNull(p.Description, "") != "")  // SQL Server specific
    .ToList();
```

---

## 📝 **4. Migration Best Practices**

### ✅ **Portable Migration Code**

```csharp
// ✅ GOOD - Uses EF Core abstractions
public partial class AddUserTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                FirstName = table.Column<string>(maxLength: 100, nullable: false),
                Email = table.Column<string>(maxLength: 255, nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            table: "Users",
            column: "Email",
            unique: true);
    }
}

// ❌ AVOID - Raw SQL in migrations
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(@"
        CREATE TABLE Users (
            Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
            FirstName NVARCHAR(100) NOT NULL,
            Email NVARCHAR(255) NOT NULL
        )");  // SQL Server specific syntax
}
```

### ✅ **Data Seeding**

```csharp
// ✅ GOOD - Use HasData for seeding
protected override void OnModelCreating(ModelBuilder builder)
{
    builder.Entity<Tenant>().HasData(
        new Tenant
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = "Default Tenant",
            Code = "DEFAULT",
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }
    );
}

// ❌ AVOID - Raw SQL seeding
migrationBuilder.Sql("INSERT INTO Tenants VALUES (NEWID(), 'Default', 'DEFAULT', GETUTCDATE())");
```

---

## 🔧 **5. Connection and Configuration Best Practices**

### ✅ **Provider-Agnostic Configuration**

```csharp
// ✅ GOOD - Configurable database providers
public static class DatabaseConfiguration
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        var provider = configuration.GetValue<string>("Database:Provider");
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            switch (provider?.ToLowerInvariant())
            {
                case "sqlserver":
                    options.UseSqlServer(connectionString, ConfigureSqlServer);
                    break;
                case "postgresql":
                    options.UseNpgsql(connectionString, ConfigurePostgreSQL);
                    break;
                case "mysql":
                    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), ConfigureMySQL);
                    break;
                case "oracle":
                    options.UseOracle(connectionString, ConfigureOracle);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported database provider: {provider}");
            }
        });

        return services;
    }

    private static void ConfigureSqlServer(SqlServerDbContextOptionsBuilder options)
    {
        options.MigrationsAssembly("ErpSystem.Data");
        options.EnableRetryOnFailure(maxRetryCount: 3);
    }

    private static void ConfigurePostgreSQL(NpgsqlDbContextOptionsBuilder options)
    {
        options.MigrationsAssembly("ErpSystem.Data");
        options.EnableRetryOnFailure(maxRetryCount: 3);
    }

    // Similar configurations for other providers...
}
```

### ✅ **Configuration File Structure**

```json
// appsettings.json
{
  "Database": {
    "Provider": "SqlServer"  // Change to: PostgreSQL, MySQL, Oracle, SQLite
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ERP;..."
  }
}
```

---

## ⚡ **6. Performance Best Practices**

### ✅ **Database-Agnostic Performance Patterns**

```csharp
// ✅ GOOD - Efficient queries that work everywhere
public async Task<PagedResult<User>> GetUsersPagedAsync(int page, int pageSize, string? search = null)
{
    var query = _context.Users
        .Where(u => !u.IsDeleted && u.IsActive);

    if (!string.IsNullOrEmpty(search))
    {
        var searchLower = search.ToLower();
        query = query.Where(u => 
            u.FirstName.ToLower().Contains(searchLower) ||
            u.LastName.ToLower().Contains(searchLower) ||
            u.Email.ToLower().Contains(searchLower));
    }

    var totalCount = await query.CountAsync();
    
    var items = await query
        .OrderBy(u => u.LastName)
        .ThenBy(u => u.FirstName)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return new PagedResult<User>
    {
        Items = items,
        TotalCount = totalCount,
        Page = page,
        PageSize = pageSize
    };
}
```

### ✅ **Bulk Operations**

```csharp
// ✅ GOOD - Use EF Core bulk extensions (works across databases)
public async Task BulkInsertUsersAsync(List<User> users)
{
    // Using EFCore.BulkExtensions (supports multiple databases)
    await _context.BulkInsertAsync(users);
}

// ✅ FALLBACK - Standard EF Core (slower but universal)
public async Task BulkInsertUsersFallbackAsync(List<User> users)
{
    _context.Users.AddRange(users);
    await _context.SaveChangesAsync();
}

// ❌ AVOID - Database-specific bulk operations
_context.Database.ExecuteSqlRaw("BULK INSERT Users FROM...");  // SQL Server only
```

---

## 🧪 **7. Testing Best Practices**

### ✅ **Database-Agnostic Testing**

```csharp
// ✅ GOOD - In-memory database for unit tests
public class UserServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _userService = new UserService(_context);
    }

    [Fact]
    public async Task CreateUser_ShouldWork_OnAllDatabases()
    {
        // Arrange
        var user = new User
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            TenantId = Guid.NewGuid()
        };

        // Act
        await _userService.CreateUserAsync(user);

        // Assert
        var savedUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
        Assert.NotNull(savedUser);
    }
}
```

### ✅ **Integration Testing with TestContainers**

```csharp
// ✅ GOOD - Test against real databases using TestContainers
[Collection("Database")]
public class DatabaseIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().Build();
    private ApplicationDbContext _context;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
            
        _context = new ApplicationDbContext(options);
        await _context.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task ComplexQuery_ShouldWork_OnPostgreSQL()
    {
        // Test complex queries against real PostgreSQL
    }
}
```

---

## 📋 **8. Code Review Checklist**

### **Before Merging - Database Portability Check**

- [ ] ✅ No raw SQL queries with database-specific syntax
- [ ] ✅ All string columns have explicit `MaxLength` attributes
- [ ] ✅ Decimal/money fields use `[Precision(18, 2)]`
- [ ] ✅ No database-specific functions in LINQ queries
- [ ] ✅ Indexes use EF Core fluent API, not raw SQL
- [ ] ✅ Migrations use `MigrationBuilder` methods, not raw SQL
- [ ] ✅ Date/time operations use UTC consistently
- [ ] ✅ No hard-coded database provider assumptions
- [ ] ✅ Foreign key relationships use standard EF Core syntax
- [ ] ✅ No database-specific data types in entities

---

## 🚀 **9. Migration Strategy Template**

### **When Client Requests Database Change**

#### **Phase 1: Assessment (1-2 days)**
```bash
# 1. Audit current codebase for database-specific code
grep -r "SqlServer\|T-SQL\|MSSQL" src/
grep -r "FromSqlRaw\|ExecuteSqlRaw" src/
grep -r "HasFilter\|NEWID\|GETUTCDATE" src/

# 2. Review migrations for raw SQL
find . -name "*Migration.cs" -exec grep -l "migrationBuilder.Sql" {} \;
```

#### **Phase 2: Code Changes (2-4 hours)**
1. Update NuGet packages
2. Change connection configuration
3. Fix any portability issues found in audit
4. Test migrations on new database

#### **Phase 3: Testing (1-2 days)**
1. Run full test suite against new database
2. Performance testing and optimization
3. Data migration testing

#### **Phase 4: Production Migration (Planned downtime)**
1. Export production data
2. Deploy new database
3. Import data
4. Switch connection strings
5. Verify functionality

---

## ⚠️ **10. Common Pitfalls to Avoid**

| **Pitfall** | **Problem** | **Solution** |
|-------------|-------------|--------------|
| **Raw SQL in queries** | `FromSqlRaw("SELECT * FROM Users WHERE...")` | Use LINQ: `.Where(u => ...)` |
| **Unlimited strings** | `public string Name { get; set; }` | `[MaxLength(200)] public string Name { get; set; }` |
| **Database functions** | `EF.Functions.DateDiff(...)` | Use C# DateTime operations |
| **Identity columns** | `[DatabaseGenerated(DatabaseGeneratedOption.Identity)]` | Use `Guid` primary keys |
| **Raw SQL migrations** | `migrationBuilder.Sql("CREATE TABLE...")` | Use `migrationBuilder.CreateTable(...)` |
| **Provider assumptions** | Hard-coding SQL Server behavior | Use configuration-based provider selection |

---

## 💡 **Key Takeaways**

1. **🎯 Follow EF Core Abstractions**: Let Entity Framework handle database differences
2. **📏 Be Explicit**: Always specify string lengths, decimal precision, and constraints
3. **🧪 Test Early**: Use TestContainers to verify portability during development  
4. **📝 Document Decisions**: Track any database-specific choices and their alternatives
5. **🔄 Regular Audits**: Periodically scan codebase for database-specific code
6. **🚀 Plan for Change**: Design with portability in mind from day one

**Following these practices ensures your ERP system remains database-agnostic and can migrate to any enterprise database with minimal effort and risk!** 🎉

---

## 📞 **Need Help?**

If you encounter database-specific code during development:
1. ✅ Check this document first
2. ✅ Search for EF Core equivalent
3. ✅ Test on SQLite in-memory for verification
4. ✅ Document any exceptions with business justification

**Remember: Today's database choice shouldn't lock us into tomorrow's architecture!** 🔓