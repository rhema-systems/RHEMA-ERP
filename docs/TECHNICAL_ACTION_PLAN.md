# ERP System - Technical Action Plan

**Document Type**: Development Roadmap  
**Priority**: Critical  
**Timeline**: Next 4 Weeks  
**Team**: Technical Development  

---

## 🚨 **Week 1: Critical Security Fixes**

### **Day 1-2: Environment Configuration**

#### **1. Move Secrets to Environment Variables**
```bash
# Create .env files for different environments
cp src/ErpSystem.Api/appsettings.json src/ErpSystem.Api/appsettings.Template.json

# Remove sensitive data from template
# Update docker-compose.yml environment variables
```

**Files to Update:**
- `src/ErpSystem.Api/appsettings.json` ❌ Remove hardcoded secrets
- `src/ErpSystem.Api/appsettings.Production.json` ➕ Create
- `docker-compose.yml` 🔄 Update environment variables
- `.env.example` ➕ Create template

**Code Changes Required:**
```csharp
// Program.cs - Update configuration loading
var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
    ?? throw new InvalidOperationException("JWT_SECRET_KEY environment variable is required");

var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
    ?? throw new InvalidOperationException("DATABASE_CONNECTION_STRING environment variable is required");
```

#### **2. Implement Security Headers Middleware**
```csharp
// Create: src/ErpSystem.Api/Middleware/SecurityHeadersMiddleware.cs
public class SecurityHeadersMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Add HSTS
        context.Response.Headers.Add("Strict-Transport-Security", 
            "max-age=31536000; includeSubDomains");
        
        // Add CSP
        context.Response.Headers.Add("Content-Security-Policy", 
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'");
        
        // Add X-Frame-Options
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        
        // Add X-Content-Type-Options
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        
        await next(context);
    }
}
```

### **Day 3-4: CORS and API Security**

#### **3. Fix CORS Configuration**
```csharp
// src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs
public static IServiceCollection AddErpSystemCors(this IServiceCollection services, IConfiguration configuration)
{
    var allowedOrigins = configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>() 
        ?? new[] { "http://localhost:3000" };
    
    services.AddCors(options =>
    {
        options.AddPolicy("ErpSystemCorsPolicy", builder =>
        {
            builder
                .WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });
    
    return services;
}
```

#### **4. Add Rate Limiting**
```csharp
// Install: Microsoft.AspNetCore.RateLimiting
services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("ApiPolicy", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.PermitLimit = 60;
        limiterOptions.QueueLimit = 10;
    });
});

// Apply to controllers
[EnableRateLimiting("ApiPolicy")]
public class AuthController : ControllerBase
```

### **Day 5: Global Exception Handling**

#### **5. Implement Global Exception Middleware**
```csharp
// Create: src/ErpSystem.Api/Middleware/GlobalExceptionHandlingMiddleware.cs
public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        var response = new
        {
            error = new
            {
                message = "An error occurred while processing your request.",
                type = exception.GetType().Name,
                traceId = context.TraceIdentifier
            }
        };

        context.Response.StatusCode = exception switch
        {
            NotFoundException => 404,
            UnauthorizedException => 401,
            ValidationException => 400,
            _ => 500
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
```

---

## ⚡ **Week 2: Performance Optimization**

### **Day 1-2: Database Optimization**

#### **1. Add Critical Indexes**
```sql
-- Create migration: Add_Performance_Indexes
CREATE INDEX IX_Users_TenantId ON Users(TenantId) WHERE IsDeleted = 0;
CREATE INDEX IX_UserTenants_UserId_TenantId ON UserTenants(UserId, TenantId) WHERE IsDeleted = 0;
CREATE INDEX IX_AuditLogs_TenantId_Timestamp ON AuditLogs(TenantId, Timestamp DESC);
CREATE INDEX IX_SecurityLogs_IpAddress_Timestamp ON SecurityLogs(IpAddress, Timestamp DESC);
CREATE INDEX IX_UserSessions_UserId_IsActive ON UserSessions(UserId, IsActive);
```

```bash
# Generate and run migration
dotnet ef migrations add Add_Performance_Indexes --project src/ErpSystem.Data --startup-project src/ErpSystem.Api
dotnet ef database update --project src/ErpSystem.Data --startup-project src/ErpSystem.Api
```

#### **2. Fix N+1 Queries**
```csharp
// Update UserService.cs
public async Task<IEnumerable<ApplicationUser>> GetAllUsersAsync()
{
    return await _context.Users
        .Include(u => u.UserTenants)
            .ThenInclude(ut => ut.Tenant)
        .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
        .AsSplitQuery() // Prevents cartesian explosion
        .Where(u => !u.IsDeleted && u.TenantId == _currentUserService.GetTenantId())
        .ToListAsync();
}
```

### **Day 3-4: Caching Implementation**

#### **3. Implement Query Result Caching**
```csharp
// Create: src/ErpSystem.Core/Services/CachingService.cs
public interface ICachingService
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class;
    Task RemoveAsync(string key);
    Task RemoveByPatternAsync(string pattern);
}

public class CachingService : ICachingService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    
    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        // Check memory cache first
        if (_memoryCache.TryGetValue(key, out T? cachedValue))
            return cachedValue;
        
        // Check distributed cache
        var distributedValue = await _distributedCache.GetStringAsync(key);
        if (!string.IsNullOrEmpty(distributedValue))
        {
            var deserializedValue = JsonSerializer.Deserialize<T>(distributedValue);
            _memoryCache.Set(key, deserializedValue, TimeSpan.FromMinutes(5));
            return deserializedValue;
        }
        
        return null;
    }
}
```

#### **4. Add Response Caching**
```csharp
// Update controllers with caching
[HttpGet("tenants")]
[ResponseCache(Duration = 300)] // 5 minutes
public async Task<ActionResult<IEnumerable<TenantDto>>> GetTenants()
{
    var cacheKey = $"tenants:{_currentUserService.GetTenantId()}";
    var cachedTenants = await _cachingService.GetAsync<IEnumerable<TenantDto>>(cacheKey);
    
    if (cachedTenants != null)
        return Ok(cachedTenants);
    
    var tenants = await _tenantService.GetAllTenantsAsync();
    await _cachingService.SetAsync(cacheKey, tenants, TimeSpan.FromMinutes(15));
    
    return Ok(tenants);
}
```

### **Day 5: Frontend Optimization**

#### **5. Implement Code Splitting**
```typescript
// frontend/src/app/dashboard/page.tsx
import { lazy, Suspense } from 'react';

const DashboardMetrics = lazy(() => import('@/components/dashboard/DashboardMetrics'));
const RecentActivities = lazy(() => import('@/components/dashboard/RecentActivities'));
const OnlineUsers = lazy(() => import('@/components/dashboard/OnlineUsers'));

export default function DashboardPage() {
  return (
    <div className="space-y-6">
      <Suspense fallback={<MetricsSkeleton />}>
        <DashboardMetrics />
      </Suspense>
      
      <Suspense fallback={<ActivitiesSkeleton />}>
        <RecentActivities />
      </Suspense>
      
      <Suspense fallback={<UsersSkeleton />}>
        <OnlineUsers />
      </Suspense>
    </div>
  );
}
```

#### **6. Optimize Bundle Size**
```javascript
// frontend/next.config.js
module.exports = {
  experimental: {
    optimizePackageImports: ['@radix-ui/react-icons', 'lucide-react']
  },
  
  webpack: (config, { isServer }) => {
    if (!isServer) {
      config.optimization.splitChunks.chunks = 'all';
      config.optimization.splitChunks.cacheGroups = {
        vendor: {
          test: /[\\/]node_modules[\\/]/,
          name: 'vendors',
          chunks: 'all',
        },
      };
    }
    return config;
  }
};
```

---

## 🧪 **Week 3: Testing Framework**

### **Day 1-2: Unit Testing Setup**

#### **1. Install Testing Dependencies**
```xml
<!-- src/ErpSystem.Tests/ErpSystem.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="MSTest.TestAdapter" Version="3.1.1" />
    <PackageReference Include="MSTest.TestFramework" Version="3.1.1" />
    <PackageReference Include="Moq" Version="4.20.69" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../ErpSystem.Core/ErpSystem.Core.csproj" />
    <ProjectReference Include="../ErpSystem.Data/ErpSystem.Data.csproj" />
    <ProjectReference Include="../ErpSystem.Api/ErpSystem.Api.csproj" />
  </ItemGroup>
</Project>
```

#### **2. Create Base Test Classes**
```csharp
// src/ErpSystem.Tests/TestBase.cs
public abstract class TestBase
{
    protected readonly ApplicationDbContext Context;
    protected readonly IFixture Fixture;

    protected TestBase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        Context = new ApplicationDbContext(options);
        Fixture = new Fixture();
        
        // Configure AutoFixture to ignore circular references
        Fixture.Behaviors.OfType<ThrowingRecursionBehavior>()
            .ToList()
            .ForEach(b => Fixture.Behaviors.Remove(b));
        Fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    public void Dispose()
    {
        Context.Dispose();
    }
}
```

### **Day 3-4: Critical Path Tests**

#### **3. Authentication Tests**
```csharp
// src/ErpSystem.Tests/Services/AuthServiceTests.cs
[TestClass]
public class AuthServiceTests : TestBase
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<IJwtTokenService> _tokenServiceMock;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _userManagerMock = CreateUserManagerMock();
        _tokenServiceMock = new Mock<IJwtTokenService>();
        _authService = new AuthService(_userManagerMock.Object, _tokenServiceMock.Object);
    }

    [TestMethod]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        // Arrange
        var user = Fixture.Create<ApplicationUser>();
        var loginRequest = new LoginRequest 
        { 
            Username = user.Email, 
            Password = "TestPassword123!" 
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(user.Email))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.CheckPasswordAsync(user, loginRequest.Password))
            .ReturnsAsync(true);
        _tokenServiceMock.Setup(x => x.GenerateTokenAsync(user))
            .ReturnsAsync("test-token");

        // Act
        var result = await _authService.LoginAsync(loginRequest);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Success);
        Assert.AreEqual("test-token", result.Token);
    }
}
```

#### **4. Repository Tests**
```csharp
// src/ErpSystem.Tests/Repositories/UserRepositoryTests.cs
[TestClass]
public class UserRepositoryTests : TestBase
{
    private readonly IUserRepository _userRepository;

    public UserRepositoryTests()
    {
        _userRepository = new UserRepository(Context);
    }

    [TestMethod]
    public async Task GetByTenantIdAsync_ExistingTenant_ReturnsUsers()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var users = Fixture.Build<ApplicationUser>()
            .With(u => u.TenantId, tenantId)
            .Without(u => u.UserRoles)
            .Without(u => u.UserTenants)
            .CreateMany(3);

        Context.Users.AddRange(users);
        await Context.SaveChangesAsync();

        // Act
        var result = await _userRepository.GetByTenantIdAsync(tenantId);

        // Assert
        Assert.AreEqual(3, result.Count());
        Assert.IsTrue(result.All(u => u.TenantId == tenantId));
    }
}
```

### **Day 5: Integration Tests**

#### **5. API Integration Tests**
```csharp
// src/ErpSystem.Tests/Integration/AuthControllerTests.cs
[TestClass]
public class AuthControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuthControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Replace database with in-memory for testing
                var descriptor = services.SingleOrDefault(d => 
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb"));
            });
        });

        _client = _factory.CreateClient();
    }

    [TestMethod]
    public async Task Login_ValidCredentials_Returns200()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Username = "admin@test.com",
            Password = "AdminPassword123!",
            TenantCode = "TEST"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.IsNotNull(content?.Token);
    }
}
```

---

## 📊 **Week 4: Monitoring & Documentation**

### **Day 1-2: Health Checks Enhancement**

#### **1. Comprehensive Health Checks**
```csharp
// src/ErpSystem.Api/Extensions/HealthCheckExtensions.cs
public static IServiceCollection AddErpSystemHealthChecks(
    this IServiceCollection services, IConfiguration configuration)
{
    services.AddHealthChecks()
        .AddDbContext<ApplicationDbContext>(name: "database")
        .AddRedis(configuration.GetConnectionString("Redis")!, name: "redis")
        .AddCheck<CustomHealthCheck>("custom-health-check")
        .AddCheck("external-api", async () =>
        {
            using var client = new HttpClient();
            var response = await client.GetAsync("https://api.external-service.com/health");
            return response.IsSuccessStatusCode ? 
                HealthCheckResult.Healthy("External API is responsive") :
                HealthCheckResult.Unhealthy("External API is not responding");
        });
    
    return services;
}
```

#### **2. Application Metrics**
```csharp
// src/ErpSystem.Api/Services/MetricsService.cs
public class MetricsService
{
    private static readonly Counter LoginAttempts = 
        Metrics.CreateCounter("erp_login_attempts_total", "Total login attempts");
    
    private static readonly Histogram ApiDuration = 
        Metrics.CreateHistogram("erp_api_duration_seconds", "API request duration");
    
    private static readonly Gauge ActiveUsers = 
        Metrics.CreateGauge("erp_active_users", "Number of active users");

    public void RecordLoginAttempt(bool successful)
    {
        LoginAttempts.WithLabels(successful ? "success" : "failure").Inc();
    }

    public void RecordApiRequest(string endpoint, double duration)
    {
        ApiDuration.WithLabels(endpoint).Observe(duration);
    }
}
```

### **Day 3-4: Documentation Updates**

#### **3. API Documentation Enhancement**
```csharp
// src/ErpSystem.Api/Extensions/SwaggerExtensions.cs
public static IServiceCollection AddErpSystemSwagger(this IServiceCollection services)
{
    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "ERP System API",
            Version = "v1",
            Description = "Enterprise Resource Planning System API",
            Contact = new OpenApiContact
            {
                Name = "Development Team",
                Email = "dev@erpsystem.com"
            }
        });

        // Add JWT authentication
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });

        // Include XML comments
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        c.IncludeXmlComments(xmlPath);
    });

    return services;
}
```

#### **4. Update README and Deployment Guides**
```markdown
<!-- Update README.md with current status -->
# ERP System - Production Ready

## Current Status ✅
- [x] Multi-tenant architecture
- [x] Enterprise authentication & security
- [x] Performance optimized
- [x] Comprehensive testing
- [x] Production monitoring

## Quick Start
```bash
# Development
docker-compose up -d

# Production  
docker-compose -f docker-compose.production.yml up -d
```

## Security Features
- JWT authentication with refresh tokens
- LDAP/Active Directory integration
- Role-based access control
- Comprehensive audit logging
- Rate limiting and DDoS protection
```

### **Day 5: CI/CD Pipeline Setup**

#### **5. GitHub Actions Workflow**
```yaml
# .github/workflows/ci-cd.yml
name: ERP System CI/CD

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

env:
  DOTNET_VERSION: '8.0.x'
  NODE_VERSION: '18'

jobs:
  test:
    name: 🧪 Test
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}
    
    - name: Setup Node.js
      uses: actions/setup-node@v4
      with:
        node-version: ${{ env.NODE_VERSION }}
    
    - name: Restore .NET dependencies
      run: dotnet restore
    
    - name: Install frontend dependencies
      run: cd frontend && npm ci
    
    - name: Run backend tests
      run: dotnet test --configuration Release --collect:"XPlat Code Coverage"
    
    - name: Run frontend tests
      run: cd frontend && npm test
    
    - name: Upload coverage reports
      uses: codecov/codecov-action@v3
      with:
        files: ./coverage.xml

  security-scan:
    name: 🔒 Security Scan
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    
    - name: Run Trivy vulnerability scanner
      uses: aquasecurity/trivy-action@master
      with:
        scan-type: 'fs'
        format: 'sarif'
        output: 'trivy-results.sarif'
    
    - name: Upload Trivy scan results
      uses: github/codeql-action/upload-sarif@v3
      with:
        sarif_file: 'trivy-results.sarif'

  build-and-deploy:
    name: 🚀 Build & Deploy
    needs: [test, security-scan]
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
    - uses: actions/checkout@v4
    
    - name: Build Docker images
      run: |
        docker build -t erp-api:${{ github.sha }} -f src/ErpSystem.Api/Dockerfile .
        docker build -t erp-frontend:${{ github.sha }} -f frontend/Dockerfile ./frontend
    
    - name: Deploy to staging
      run: |
        echo "🚀 Deploying to staging environment"
        # Add deployment scripts here
```

---

## 📋 **Checklist & Validation**

### **Week 1 Deliverables:**
- [ ] All secrets moved to environment variables
- [ ] Security headers middleware implemented
- [ ] CORS properly configured
- [ ] Rate limiting active
- [ ] Global exception handling working
- [ ] Security scan shows 0 high/critical issues

### **Week 2 Deliverables:**
- [ ] Database indexes created and optimized
- [ ] N+1 queries eliminated
- [ ] Caching service implemented
- [ ] Response caching active
- [ ] Frontend bundle size reduced by 30%+
- [ ] API response times improved by 50%+

### **Week 3 Deliverables:**
- [ ] Unit test framework setup
- [ ] 80%+ test coverage for critical components
- [ ] Integration tests for API endpoints
- [ ] Automated testing in CI/CD pipeline
- [ ] Test reports generated

### **Week 4 Deliverables:**
- [ ] Enhanced health checks implemented
- [ ] Application metrics tracking
- [ ] API documentation complete
- [ ] CI/CD pipeline operational
- [ ] Performance monitoring active

---

## 🚨 **Risk Mitigation**

### **Potential Issues:**
1. **Database Migration Failures**
   - **Solution**: Test migrations in staging environment first
   - **Rollback**: Keep migration scripts for reverting changes

2. **Performance Regression**
   - **Solution**: Load testing before deployment
   - **Monitoring**: Continuous performance monitoring

3. **Security Vulnerabilities**
   - **Solution**: Automated security scanning in CI/CD
   - **Response**: Incident response plan ready

### **Rollback Procedures:**
```bash
# Quick rollback commands
docker-compose down
git checkout previous-stable-commit
docker-compose up -d

# Database rollback
dotnet ef database update PreviousMigration --project src/ErpSystem.Data
```

---

**Next Steps:**
1. **Week 5+**: Begin business module development
2. **Ongoing**: Weekly security scans and performance monitoring
3. **Monthly**: Architecture review and technical debt assessment

**Success Criteria:**
- All security vulnerabilities resolved ✅
- Performance improved by 50%+ ✅  
- Test coverage >80% ✅
- Zero production incidents 🎯