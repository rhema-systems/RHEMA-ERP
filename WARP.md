# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Project Overview

This is a multi-tenant Enterprise Resource Planning (ERP) system built with ASP.NET Core 8.0, Entity Framework Core, and Blazor Server. The system supports multiple tenants with modular ERP functionality including Finance, HR, Sales, Inventory, Procurement, Marketing, and Workflow Engine modules.

## Architecture Overview

### Solution Structure
- **ErpSystem.Shared**: Common enums, constants, and shared types
- **ErpSystem.Core**: Business entities, interfaces, and core logic
- **ErpSystem.Data**: Entity Framework DbContext, repositories, and data layer
- **ErpSystem.Web**: Blazor Server web application with APIs and UI

### Key Architectural Patterns
- **Multi-tenancy**: Global query filters and tenant-scoped data isolation
- **Modular Design**: ERP modules can be enabled/disabled per tenant
- **Repository Pattern**: Generic and specific repositories for data access
- **Service Layer**: Business logic abstraction with dependency injection
- **Identity System**: ASP.NET Core Identity with custom roles (SuperAdmin, TenantAdmin, Manager, Employee)
- **Clean Architecture**: Separation of concerns with proper layer dependencies

### Core Entities
- **Tenant**: Multi-tenant organization with LDAP integration support
- **ApplicationUser**: Custom identity user with tenant association
- **TenantModule**: Tracks which ERP modules are enabled per tenant
- **ApplicationRole**: Custom roles with system-defined permissions

## Development Commands

### Building and Running
```powershell
# Build the entire solution
dotnet build

# Build specific project
dotnet build src/ErpSystem.Web/ErpSystem.Web.csproj

# Run the web application (from src/ErpSystem.Web directory)
dotnet run

# Run with specific environment
dotnet run --environment Development
dotnet run --environment Production

# Watch mode for development (auto-restart on changes)
dotnet watch run
```

### Database Operations
```powershell
# Add new migration
dotnet ef migrations add [MigrationName] --project src/ErpSystem.Data --startup-project src/ErpSystem.Web

# Update database
dotnet ef database update --project src/ErpSystem.Data --startup-project src/ErpSystem.Web

# Remove last migration
dotnet ef migrations remove --project src/ErpSystem.Data --startup-project src/ErpSystem.Web

# Generate SQL script for migrations
dotnet ef migrations script --project src/ErpSystem.Data --startup-project src/ErpSystem.Web

# Seed test users (custom command)
dotnet run seed --project src/ErpSystem.Web
```

### Testing Commands
```powershell
# Run all tests (when test projects exist)
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test [TestProjectName]

# Run tests in watch mode
dotnet watch test
```

### Docker Operations
```powershell
# Build Docker image
docker build -t erpsystem .

# Run with Docker Compose (includes SQL Server and Redis)
docker-compose up

# Run in detached mode
docker-compose up -d

# Stop services
docker-compose down

# View logs
docker-compose logs erpsystem-web
```

## Development Environment Setup

### Prerequisites
- .NET 8.0 SDK
- SQL Server (LocalDB for development, or Docker container)
- Redis (optional, for distributed caching)
- Docker Desktop (for containerized development)

### Configuration Files
- `appsettings.Development.json`: Development-specific settings with debug features enabled
- `appsettings.Production.json`: Production settings with security optimizations
- Environment variables can override any configuration setting

### Key Configuration Sections
- **ConnectionStrings**: Database and Redis connection strings
- **Features**: Feature flags for development tools and debugging
- **Authentication**: LDAP and local authentication settings
- **Performance**: Caching and optimization settings
- **Modules**: ERP module configuration

## Health Monitoring

### Health Check Endpoints
- `GET /health` - Overall application health status
- `GET /health/ready` - Kubernetes readiness probe
- `GET /health/live` - Kubernetes liveness probe  
- `GET /health/shutdown` - Graceful shutdown status monitoring

### Development APIs (Development Environment Only)
- `GET /api/dev/features` - View and manage feature flags
- `GET /api/dev/system` - System information and metrics
- `GET /api/dev/health` - Detailed health information
- `GET /api/dev/environment` - Environment configuration details
- `GET /api/dev/routes` - List of available API endpoints
- `POST /api/dev/test-exception` - Test exception handling

## Infrastructure Services

### Application Lifecycle Management
- **Graceful Shutdown**: 60-second timeout with proper resource cleanup
- **Application Warmup**: Database connection pooling and service initialization
- **Resource Cleanup**: Automatic disposal of database connections, caches, and files
- **Configuration Validation**: Startup validation of all configuration settings

### Middleware Pipeline (in order)
1. Application Lifecycle Management (shutdown handling)
2. Developer Exception Page (development only)
3. Simple Development Middleware (request tracking, development only)
4. Security Headers Middleware (all environments)
5. Serilog Request Logging
6. Standard ASP.NET Core middleware (HTTPS, Static Files, Routing, etc.)

### Logging and Monitoring
- **Serilog**: Structured logging with JSON output and multiple sinks
- **Request Tracking**: Unique correlation IDs for request tracing
- **Performance Monitoring**: Request timing and response headers
- **Health Check Integration**: Comprehensive system health monitoring

## Key Patterns and Conventions

### Service Registration
All services are registered through extension methods in `ServiceCollectionExtensions.cs`:
- Database and Identity services
- Business logic services and repositories  
- Caching and web farm configuration
- Health checks and monitoring
- Development and lifecycle services

### Entity Framework Patterns
- **Global Query Filters**: Automatic soft delete and tenant filtering
- **Audit Fields**: Automatic CreatedAt, UpdatedAt, DeletedAt tracking
- **Multi-tenancy**: Tenant-scoped data isolation with global filters
- **Migrations**: Stored in ErpSystem.Data project with seeded default data

### Security Considerations
- **ASP.NET Core Identity**: Custom user and role management
- **Authorization Policies**: Role and module-based access control
- **LDAP Integration**: Optional LDAP authentication per tenant
- **Security Headers**: Automatically applied security headers
- **Configuration Validation**: Prevents insecure deployments

### Development Experience Features
- **Feature Flags**: Configuration-driven feature toggles
- **Development Middleware**: Request logging and timing in development
- **Hot Reload**: Supports .NET hot reload for rapid development
- **Container Support**: Full Docker development environment

## Testing Strategy

### Unit Testing Approach
- **Repository Testing**: Mock DbContext for data layer tests
- **Service Testing**: Mock dependencies and test business logic
- **Controller Testing**: Test API endpoints with mocked services
- **Entity Testing**: Validate entity relationships and constraints

### Integration Testing
- **Database Testing**: Use test database with migrations
- **API Testing**: Full HTTP pipeline testing
- **Health Check Testing**: Validate monitoring endpoints
- **Authentication Testing**: Test identity and authorization flows

## Deployment Considerations

### Environment-Specific Settings
- **Development**: Debug logging, detailed error pages, development APIs
- **Production**: Optimized logging, security headers, performance monitoring
- **Feature Flags**: Environment-specific feature enablement

### Database Deployment
- **Migrations**: Automatic migration on application startup
- **Seeding**: Default tenant, roles, and modules created automatically
- **Connection Resilience**: Retry policies for database connections

### Container Deployment
- **Multi-stage Dockerfile**: Optimized production image
- **Non-root User**: Security-focused container configuration
- **Health Checks**: Kubernetes-ready liveness and readiness probes
- **Graceful Shutdown**: Proper handling of container termination

## Module Development Guidelines

### Adding New ERP Modules
1. Define module constants in `ErpSystem.Shared/Constants.cs`
2. Add module to tenant seeding in `ApplicationDbContext.cs`
3. Create authorization policies in `ServiceCollectionExtensions.cs`
4. Implement module-specific services and repositories
5. Add module configuration to appsettings files
6. Update health checks if module has external dependencies

### Entity Development Patterns
- Inherit from `BaseEntity` for audit fields and soft delete
- Inherit from `TenantEntity` for tenant-scoped entities  
- Use proper Entity Framework configurations in `OnModelCreating`
- Apply appropriate indexes for performance
- Include proper validation attributes

### Service Implementation
- Use dependency injection for all service dependencies
- Implement interfaces in `ErpSystem.Core` for testability
- Follow repository pattern for data access
- Use async/await for all database operations
- Include proper error handling and logging

## Performance Optimization

### Database Performance
- **Connection Pooling**: Automatic connection pool management
- **Query Optimization**: Proper indexing and global query filters
- **Lazy Loading**: Configured for optimal performance
- **Retry Policies**: Resilience for transient database failures

### Caching Strategy
- **Memory Caching**: In-process caching for frequently accessed data
- **Distributed Caching**: Redis support for web farm scenarios
- **Response Caching**: HTTP response caching for static content
- **Cache Warmup**: Automatic cache population on application startup

### Monitoring and Diagnostics
- **Health Checks**: Comprehensive system health monitoring
- **Performance Counters**: Memory, disk space, and database health
- **Request Metrics**: Timing and performance tracking
- **Error Correlation**: Request tracing across service boundaries

## Security Best Practices

### Authentication and Authorization
- **Strong Password Policies**: Enforced password requirements
- **Account Lockout**: Protection against brute force attacks
- **Role-Based Access**: Hierarchical role system (SuperAdmin > TenantAdmin > Manager > Employee)
- **Module Permissions**: Granular access control per ERP module

### Data Protection
- **Multi-tenancy**: Complete data isolation between tenants
- **Soft Delete**: Data preservation with logical deletion
- **Audit Trails**: Automatic tracking of data changes
- **Configuration Security**: Validation prevents insecure settings

This WARP.md provides the essential information needed to effectively work with this ERP system codebase, covering architecture, development workflows, deployment, and best practices specific to this multi-tenant enterprise application.