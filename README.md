# 🏢 ERP System - Database-Agnostic Enterprise Resource Planning

[![Build Status](https://github.com/rhema-systems/RHEMA-ERP/workflows/CI%2FCD%20Pipeline/badge.svg)](https://github.com/rhema-systems/RHEMA-ERP/actions)
[![Security Scan](https://github.com/rhema-systems/RHEMA-ERP/workflows/Security%20Scan/badge.svg)](https://github.com/rhema-systems/RHEMA-ERP/actions)
[![Documentation](https://github.com/rhema-systems/RHEMA-ERP/workflows/Documentation%20Generation/badge.svg)](https://github.com/rhema-systems/RHEMA-ERP/actions)

A modern, multi-tenant, database-agnostic Enterprise Resource Planning (ERP) system built with **.NET 8**, **Entity Framework Core**, and **Next.js**. Supports **9 different database providers** with seamless switching capabilities.

## 📊 **System Status**

- **🟢 Build Status**: Passing (All tests passing)
- **🟢 Security**: Scanned (No critical vulnerabilities)
- **🟢 Documentation**: Up to date (Auto-generated)
- **🟢 Health Checks**: Active (API, Database, Frontend)
- **🟢 Deployment**: Automated (CI/CD Pipeline)

> 📋 **[Health Check Documentation](./docs/HEALTH_CHECK.md)** - Monitor system health and deployment status

## 🌟 **Key Features**

### **🗄️ Database Flexibility**
- **9 Database Providers Supported**: SQL Server, PostgreSQL, MySQL, Oracle, IBM DB2, Firebird, SQLite, Azure Cosmos DB, In-Memory
- **Runtime Database Switching**: Change database providers through configuration
- **Database-Agnostic Design**: Code once, run on any supported database
- **Migration-Ready**: EF Core migrations work across all database providers

### **🏢 Multi-Tenancy**
- **Tenant Isolation**: Complete data separation between tenants
- **Tenant-Specific Configuration**: Each tenant can have custom settings
- **Scalable Architecture**: Support for hundreds of tenants
- **Tenant-Aware Security**: Row-level security with tenant filtering

### **🔐 Security & Authentication**
- **JWT Token Authentication**: Secure API access
- **Role-Based Access Control**: Fine-grained permissions
- **Multi-Factor Authentication Ready**: Extensible authentication system
- **LDAP Integration**: Enterprise directory support
- **Audit Logging**: Complete audit trail of all system activities

### **💾 Storage Flexibility**
- **Provider Abstraction**: Switch between Local, Azure Blob, AWS S3 via configuration
- **Migration Tools**: Move files between storage providers seamlessly
- **Health Monitoring**: Built-in storage provider health checks
- **Backward Compatible**: Existing APIs continue to work unchanged

### **📊 ERP Modules**
- **Finance Management**: Accounting, invoicing, payments
- **Human Resources**: Employee management, payroll
- **Sales & CRM**: Customer relationship management
- **Procurement**: Purchase orders, vendor management  
- **Inventory Management**: Stock tracking, warehouse management
- **Marketing**: Campaign management, analytics
- **Workflow Engine**: Business process automation

### **🚀 Modern Technology Stack**
- **Backend**: .NET 8, ASP.NET Core Web API
- **Frontend**: Next.js 14, TypeScript, Tailwind CSS
- **Database**: Entity Framework Core with multiple providers
- **File Storage**: Abstracted storage layer (Local, Azure Blob, AWS S3)
- **Caching**: Redis support
- **Logging**: Serilog with structured logging
- **Testing**: xUnit, Integration tests
- **Deployment**: Docker, Docker Compose

## 🏗️ **Architecture Overview**

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Frontend      │────│   Backend       │────│   Database      │
│   (Next.js)     │    │   (.NET 8)      │    │   (Any of 9)    │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         │                       │              ┌─────────────────┐
         │                       │──────────────│   SQL Server    │
         │                       │              ├─────────────────┤
         │                       │              │   PostgreSQL    │
         │              ┌─────────────────┐     ├─────────────────┤
         │──────────────│   REST API      │     │   MySQL         │
                        │   JWT Auth      │     ├─────────────────┤
                        │   Multi-Tenant  │     │   Oracle        │
                        └─────────────────┘     ├─────────────────┤
                                                │   IBM DB2       │
                                                ├─────────────────┤
                                                │   Firebird      │
                                                ├─────────────────┤
                                                │   SQLite        │
                                                ├─────────────────┤
                                                │   Cosmos DB     │
                                                ├─────────────────┤
                                                │   In-Memory     │
                                                └─────────────────┘
```

## 📁 **Project Structure**

```
erp-system/
├── 📁 src/                          # Backend source code
│   ├── 📁 ErpSystem.Api/            # Web API project
│   │   ├── 📁 Controllers/          # API controllers
│   │   ├── 📁 Services/             # Business services
│   │   ├── 📁 Extensions/           # Database configuration
│   │   └── 📄 Program.cs            # Application entry point
│   ├── 📁 ErpSystem.Core/           # Domain entities
│   │   ├── 📁 Entities/             # Domain models
│   │   └── 📁 Interfaces/           # Service contracts
│   ├── 📁 ErpSystem.Data/           # Data access layer
│   │   ├── 📄 ApplicationDbContext.cs # EF Core context
│   │   └── 📁 Migrations/           # Database migrations
│   └── 📁 ErpSystem.Shared/         # Shared models and constants
├── 📁 frontend/                     # Next.js frontend
│   ├── 📁 src/app/                  # App router pages
│   ├── 📁 src/components/           # React components
│   ├── 📁 src/services/             # API services
│   └── 📄 package.json              # Frontend dependencies
├── 📁 docs/                         # Documentation
│   ├── 📄 database-agnostic-best-practices.md
│   ├── 📄 database-switching-guide.md
│   └── 📄 database-provider-comparison.md
├── 📁 docker/                       # Docker configurations
├── 📄 docker-compose.yml            # Development environment
├── 📄 docker-compose.production.yml # Production environment
└── 📄 ErpSystem.sln                 # Solution file
```

## 🚀 **Quick Start**

### **Prerequisites**
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (optional)
- Database server (SQL Server, PostgreSQL, etc.)

### **1. Clone & Setup**
```bash
git clone <repository-url>
cd erp-system

# Setup backend
cd src/ErpSystem.Api
dotnet restore
dotnet build

# Setup frontend
cd ../../frontend
npm install
```

### **2. Configure Database**

**⚠️ IMPORTANT: For security reasons, use User Secrets for sensitive configuration data.**

#### **Option A: Using User Secrets (Recommended for Development)**
```bash
cd src/ErpSystem.Api

# Initialize user secrets
dotnet user-secrets init

# Set database configuration
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\\mssqllocaldb;Database=ErpSystemDB;Trusted_Connection=true;MultipleActiveResultSets=true"
dotnet user-secrets set "JwtSettings:SecretKey" "your-super-secret-256-bit-key-here-make-it-long-and-random"
dotnet user-secrets set "JwtSettings:Issuer" "ErpSystem"
dotnet user-secrets set "JwtSettings:Audience" "ErpSystemUsers"

# For other database providers:
# SQL Server
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=ErpSystemDB;User Id=sa;Password=YourPassword123!;TrustServerCertificate=true"

# PostgreSQL
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=erpsystem;Username=postgres;Password=yourpassword"

# MySQL
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=erpsystem;Uid=root;Pwd=yourpassword;"
```

#### **Option B: appsettings.json (Development Only)**
Edit `src/ErpSystem.Api/appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ErpSystemDB;Trusted_Connection=true;MultipleActiveResultSets=true"
  },
  "JwtSettings": {
    "SecretKey": "your-super-secret-256-bit-key-here-make-it-long-and-random",
    "Issuer": "ErpSystem",
    "Audience": "ErpSystemUsers",
    "ExpiryMinutes": 60
  },
  "Database": {
    "Provider": "SqlServer"
  }
}
```

**🔒 Security Notes:**
- ✅ **User Secrets**: Stores sensitive data outside of source control
- ✅ **Environment Variables**: Use in production with proper secret management
- ❌ **appsettings.json**: Never commit sensitive data to source control
- ❌ **Hardcoded Values**: Never hardcode connection strings or secrets

### **3. Run Database Migrations**
```bash
cd src/ErpSystem.Api
dotnet ef database update
```

### **4. Start the Application**

**Option A: Development Mode**
```bash
# Terminal 1: Start backend
cd src/ErpSystem.Api
dotnet run

# Terminal 2: Start frontend
cd frontend
npm run dev
```

**Option B: Docker Compose**
```bash
docker-compose up -d
```

### **5. Access the Application**
- **Frontend**: http://localhost:3000
- **Backend API**: http://localhost:5000
- **API Documentation**: http://localhost:5000/swagger

**Default Credentials:**
- Username: `admin`
- Password: `Admin123!`
- Tenant: `DEFAULT`

## 🗄️ **Database Provider Configuration**

### **Supported Databases**

| Database | Provider | Status | Use Case |
|----------|----------|--------|----------|
| **SQL Server** | `SqlServer` | ✅ Active | Microsoft environments |
| **PostgreSQL** | `PostgreSQL` | ⚪ Ready | Modern web applications |
| **MySQL** | `MySQL` | ⚪ Ready | Web applications |
| **Oracle** | `Oracle` | ⚪ Ready | Enterprise systems |
| **IBM DB2** | `DB2` | ⚪ Ready | Mainframe integration |
| **Firebird** | `Firebird` | ⚪ Ready | Lightweight deployments |
| **SQLite** | `SQLite` | ⚪ Ready | Development/testing |
| **Azure Cosmos DB** | `CosmosDB` | ⚪ Ready | Global scale applications |
| **In-Memory** | `InMemory` | ⚪ Ready | Testing only |

### **Switching Database Providers**

**Step 1**: Update `appsettings.json`
```json
{
  "Database": {
    "Provider": "PostgreSQL",
    "ConnectionString": "Host=localhost;Database=erpsystem;Username=user;Password=pass"
  }
}
```

**Step 2**: Uncomment NuGet packages in `ErpSystem.Api.csproj`
```xml
<!-- Uncomment the desired database provider -->
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.4" />
```

**Step 3**: Uncomment provider code in `DatabaseConfiguration.cs`
```csharp
case "PostgreSQL":
    // Uncomment the code block for PostgreSQL
    break;
```

**Step 4**: Run migrations
```bash
dotnet ef database update
```

📚 **See [Database Switching Guide](docs/database-switching-guide.md) for detailed instructions.**

## 🔧 **Configuration**

### **Backend Configuration (`appsettings.json`)**
```json
{
  "Database": {
    "Provider": "SqlServer",
    "ConnectionStrings": {
      "SqlServer": "Server=(localdb)\\mssqllocaldb;Database=ErpSystemDB;Trusted_Connection=true;",
      "PostgreSQL": "Host=localhost;Database=erpsystem;Username=user;Password=pass",
      "MySQL": "Server=localhost;Database=erpsystem;Uid=user;Pwd=pass;"
    }
  },
  "Jwt": {
    "SecretKey": "your-256-bit-secret-key-here",
    "Issuer": "ErpSystem",
    "Audience": "ErpSystemUsers",
    "ExpiryMinutes": 60
  },
  "Serilog": {
    "MinimumLevel": "Information"
  }
}
```

### **Frontend Configuration (`.env.local`)**
```env
NEXT_PUBLIC_API_URL=http://localhost:5000/api
NEXT_PUBLIC_APP_NAME=ERP System
NEXT_PUBLIC_ENVIRONMENT=development
```

### **File Storage Configuration**
```json
{
  "FileStorage": {
    "Provider": "Local",  // Options: Local, AzureBlob, AwsS3
    "MaxFileSizeBytes": 10485760,
    "Local": {
      "BasePath": "uploads",
      "BaseUrl": "/uploads",
      "UseWebRoot": true
    },
    "Azure": {
      "ConnectionString": "your-azure-connection-string",
      "ContainerName": "erp-uploads"
    },
    "Aws": {
      "BucketName": "erp-uploads",
      "Region": "us-east-1"
    }
  }
}
```

### **Email Service Configuration**

The system includes a flexible, database-driven email service architecture with automatic environment-based switching:

#### **Development Mode**
In development, the `SimpleEmailService` logs email content to the console instead of sending actual emails:

```json
{
  "Logging": {
    "LogLevel": {
      "ErpSystem.Web.Services.SimpleEmailService": "Information"
    }
  }
}
```

#### **Production Email Service**
In production, the system automatically uses `ProductionEmailService` which reads SMTP configuration from the `emailsettings` database table:

**Step 1**: Configure SMTP settings in the admin panel
- Navigate to **Administration → Settings → Email**
- Configure your SMTP server settings:
  - **SMTP Host**: Your email server (e.g., smtp.gmail.com, smtp.office365.com)
  - **SMTP Port**: Usually 587 for TLS or 465 for SSL
  - **Username**: Your email account username
  - **Password**: Your email account password (stored encrypted)
  - **Use TLS**: Enable for secure connection
  - **From Address**: The sender email address
  - **From Name**: The sender display name

**Step 2**: Test email configuration
- Use the "Test Email" feature in the admin panel
- Send a test email to verify SMTP settings work correctly

**Step 3**: Deploy in production environment
- Set `ASPNETCORE_ENVIRONMENT=Production`
- The system automatically switches to database-driven email service

#### **Common SMTP Provider Examples**

**Gmail:**
```
SMTP Host: smtp.gmail.com
SMTP Port: 587
Use TLS: true
Username: your-email@gmail.com
Password: your-app-password (not regular password)
```

**Microsoft 365:**
```
SMTP Host: smtp.office365.com
SMTP Port: 587
Use TLS: true
Username: your-email@yourdomain.com
Password: your-password
```

**SendGrid:**
```
SMTP Host: smtp.sendgrid.net
SMTP Port: 587
Use TLS: true
Username: apikey
Password: your-sendgrid-api-key
```

#### **Email Service Architecture**
- **Automatic Environment Switching**: Development vs Production services
- **Database-Driven Configuration**: SMTP settings stored in `emailsettings` table
- **Encrypted Password Storage**: Passwords encrypted using `ICryptoService`
- **Rich HTML Templates**: Professional email templates for all system emails
- **Fallback Handling**: Graceful fallback to logging when SMTP is misconfigured
- **Security Features**: Encrypted storage, connection timeouts, error handling

#### **Email Templates**
The production email service includes professional HTML templates for:
- 📧 **Password Reset**: Branded password reset emails with security warnings
- 👋 **Welcome Email**: New user onboarding with temporary password
- 🔒 **Account Locked**: Security notifications for locked accounts
- ✉️ **Generic Email**: Flexible template for custom email content

#### **Troubleshooting Email Issues**
- **Check logs**: Email service logs all attempts with detailed error messages
- **Test connectivity**: Use the admin panel's test email feature
- **Verify credentials**: Ensure SMTP username/password are correct
- **Check firewall**: Ensure SMTP ports (587/465) are not blocked
- **Gmail users**: Use App Passwords instead of regular passwords

## 📊 **ERP Modules**

### **Core Modules**
- ✅ **User Management**: Users, roles, permissions, phone numbers with country codes
- ✅ **Tenant Management**: Multi-tenant organization with advanced branding
- ✅ **Settings Management**: Email, password policies
- ✅ **Audit & Security Logs**: Complete audit trail
- ✅ **Data Tables**: Enhanced grids with striped rows, compact design, clickable selection
- ✅ **File Management**: Flexible storage abstraction with provider switching
- ✅ **User Interface**: Responsive design with enhanced forms and authentication flows

### **Business Modules**
- ✅ **Maintenance Management**: Asset tracking, work orders, analytics, technician management
  - 🔧 **Asset Analytics**: Comprehensive performance metrics and OEE analysis
  - 📊 **Asset Performance Dashboard**: Real-time monitoring with interactive charts
  - 🔍 **Maintenance Analytics**: Efficiency trends, uptime tracking, cost analysis
  - 📱 **Mobile Maintenance**: Mobile-optimized interfaces for technicians
  - 🛡️ **Safety Protocols**: Protocol adherence tracking and compliance
  - ⚡ **Real-time Notifications**: Maintenance alerts and status updates
- 🔄 **Finance**: Accounting, invoicing, payments
- 🔄 **Human Resources**: Employee management, payroll
- 🔄 **Sales & CRM**: Customer relationship management
- 🔄 **Procurement**: Purchase orders, vendor management
- 🔄 **Inventory**: Stock tracking, warehouse management
- 🔄 **Marketing**: Campaign management, analytics
- 🔄 **Workflow Engine**: Business process automation

## 🔐 **Security Features**

### **Authentication & Authorization**
- JWT token-based authentication
- Role-based access control (RBAC)
- Multi-tenant security isolation
- LDAP/Active Directory integration
- Password policy enforcement

### **Data Security**
- Tenant-level data isolation
- Soft delete with audit trail
- Encrypted configuration secrets
- SQL injection protection via EF Core
- HTTPS enforcement in production

### **Audit & Compliance**
- Complete audit logging
- Security event logging
- User activity tracking
- GDPR compliance ready
- Data export/import capabilities

## 🧪 **Testing**

### **Run Backend Tests**
```bash
cd src/ErpSystem.Api
dotnet test
```

### **Run Frontend Tests**
```bash
cd frontend
npm test
```

### **Integration Tests**
```bash
# Test with different database providers
dotnet test --configuration Release
```

## 🐳 **Docker Deployment**

### **Development Environment**
```bash
docker-compose up -d
```

### **Production Environment**
```bash
docker-compose -f docker-compose.production.yml up -d
```

### **Environment Variables**
```env
# .env file for Docker
DATABASE_PROVIDER=SqlServer
SQL_SERVER_CONNECTION=Server=sqlserver;Database=ErpSystem;User=sa;Password=YourPassword123!
JWT_SECRET_KEY=your-super-secret-256-bit-key-here
ASPNETCORE_ENVIRONMENT=Production
```

## 📊 **Performance**

### **Database Performance**
- Connection pooling
- Query optimization
- Proper indexing strategy
- Caching layer (Redis)
- Database-specific optimizations

### **API Performance**
- Async/await patterns
- Response caching
- Compression middleware
- Rate limiting
- Health checks

### **Frontend Performance**
- Next.js optimization
- Code splitting
- Image optimization
- Lazy loading
- CDN support

## 🔧 **Troubleshooting**

### **Common Build Issues**

#### **⚠️ "IEmailService cannot be resolved"**
**Problem**: Dependency injection error on startup
```
Unable to resolve service for type 'ErpSystem.Core.Interfaces.Common.IEmailService'
```
**Solution**: The system uses both API and Core email interfaces. The `CoreEmailServiceAdapter` bridges them:
```bash
# This is already configured in ServiceCollectionExtensions.cs
# No action needed - restart the application
dotnet run
```

#### **⚠️ "Two parallel pages resolve to same path"**
**Problem**: Route conflicts in Next.js
```
You cannot have two parallel pages that resolve to the same path
```
**Solution**: Remove duplicate route files:
```bash
# Check for conflicting pages
cd frontend
find src/app -name "page.tsx" | grep -E "(dashboard)|analytics"

# Remove duplicates in route groups like (dashboard)
rm -rf src/app/\(dashboard\)
```

#### **⚠️ Missing Sidebar/Navbar on New Pages**
**Problem**: New pages don't show navigation
**Solution**: Ensure pages are in correct folder structure:
```
✅ src/app/maintenance/analytics/page.tsx    (inherits layout)
❌ src/app/(dashboard)/maintenance/analytics/page.tsx  (no layout)
```

### **Common Runtime Issues**

#### **⚠️ Database Connection Issues**
**Problem**: "Cannot connect to database"
**Solution**: Check User Secrets configuration:
```bash
cd src/ErpSystem.Api
dotnet user-secrets list

# If empty, reconfigure:
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "your-connection-string"
```

#### **⚠️ JWT Authentication Issues**
**Problem**: "Invalid token" or "Unauthorized"
**Solution**: Check JWT configuration:
```bash
# Ensure JWT secret is configured
dotnet user-secrets set "JwtSettings:SecretKey" "your-super-secret-256-bit-key-here-make-it-long-and-random"

# Check token format in API requests
curl -H "Authorization: Bearer YOUR_TOKEN" http://localhost:5000/api/endpoint
```

### **Performance Issues**

#### **⚠️ Slow API Responses**
**Solutions**:
- Enable response caching
- Check database query performance
- Monitor connection pool usage
- Enable compression middleware

#### **⚠️ Frontend Loading Issues**
**Solutions**:
- Clear browser cache
- Check network tab for failed requests
- Verify API URL in `.env.local`
- Check CORS configuration

## 🤝 **Contributing**

1. **Fork** the repository
2. **Create** a feature branch (`git checkout -b feature/amazing-feature`)
3. **Commit** your changes (`git commit -m 'Add amazing feature'`)
4. **Push** to the branch (`git push origin feature/amazing-feature`)
5. **Create** a Pull Request

### **Development Guidelines**
- Follow C# coding standards
- Write unit tests for new features
- Update documentation
- Test with multiple database providers
- Ensure multi-tenant compatibility

## 📄 **License**

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🆘 **Support**

### **Documentation**
- [Database Switching Guide](docs/database-switching-guide.md)
- [Database Best Practices](docs/database-agnostic-best-practices.md)
- [Provider Comparison](docs/database-provider-comparison.md)
- [Storage Abstraction Guide](docs/STORAGE_ABSTRACTION.md)

### **Getting Help**
- 📧 Create an issue for bug reports
- 💬 Start a discussion for questions
- 📖 Check the documentation
- 🔍 Search existing issues

## ✅ **Current Advanced Features**

### **Real-time Communication**
- [x] **SignalR Integration**: Multi-tenant real-time notifications
- [x] **Dashboard Hub**: Live updates for maintenance alerts and workflow status
- [x] **Group Management**: Tenant-specific and user-specific notification groups
- [x] **Connection Handling**: Robust connect/disconnect management with authentication

### **Advanced Workflow Engine**
- [x] **Complete Workflow System**: WorkflowDefinition, WorkflowInstance, WorkflowStep entities
- [x] **Workflow Engine**: Condition evaluation and step execution
- [x] **Maintenance Workflow Integration**: Enhanced maintenance workflow automation
- [x] **Activity Logging**: Complete audit trail for workflow activities
- [x] **API Endpoints**: Full CRUD operations for workflow management

### **Advanced Reporting Engine**
- [x] **Dynamic Reports**: Database-driven report generation with SQL queries
- [x] **Report Templates**: Template management and customization system
- [x] **Scheduled Reports**: Automated report generation and distribution
- [x] **Export Formats**: Multiple export options (PDF, Excel, CSV)
- [x] **Role-Based Reporting**: Security-integrated report access control
- [x] **Advanced Maintenance Reports**: Specialized analytics and KPI dashboards

### **Production-Ready Features**
- [x] **Database-Driven Email Service**: SMTP configuration from database with encryption
- [x] **Professional Email Templates**: HTML templates for system notifications
- [x] **Multi-Tenant Architecture**: Complete tenant isolation and branding
- [x] **Security Framework**: JWT, RBAC, audit logging, threat detection
- [x] **File Storage Abstraction**: Local, Azure Blob, AWS S3 support
- [x] **Caching System**: Redis and in-memory caching with fallback

## 🚀 **Upcoming Features**

### **Next Phase Development**
- [ ] **Mobile Application**: React Native mobile app for field operations
- [ ] **Microservices Architecture**: Break monolith into distributed services
- [ ] **Kubernetes Deployment**: Container orchestration and auto-scaling
- [ ] **AI/ML Integration**: Predictive maintenance and intelligent analytics
- [ ] **Advanced Analytics Dashboard**: Business intelligence and data visualization
- [ ] **IoT Integration**: Real-time sensor data and equipment monitoring

### **Database Enhancements**
- [ ] **Performance Monitoring**: Real-time database performance metrics
- [ ] **Automatic Failover**: High availability database clustering
- [ ] **Read Replica Support**: Load balancing across database replicas
- [ ] **Database Sharding**: Horizontal scaling for massive datasets
- [ ] **Cross-Database Queries**: Federated queries across multiple providers

---

## 🎯 **Why Choose This ERP System?**

✅ **Database Freedom**: Switch between 9 database providers without code changes  
✅ **Enterprise Ready**: Multi-tenant, secure, scalable architecture  
✅ **Modern Stack**: Latest .NET 8, Next.js 14, TypeScript  
✅ **Real-time Communication**: SignalR-powered live notifications and updates  
✅ **Advanced Workflows**: Complete workflow engine with automation capabilities  
✅ **Intelligent Reporting**: Dynamic reports with scheduling and role-based access  
✅ **Production Email System**: Database-driven SMTP with professional templates  
✅ **Developer Friendly**: Extensive documentation and examples  
✅ **Cloud Ready**: Docker, monitoring, logging, multi-cloud storage support  

**Your business shouldn't be locked to a database or cloud provider. Choose the ERP system that adapts to your infrastructure, not the other way around.** 🎯

---

*Built with ❤️ using .NET 8, Entity Framework Core, and Next.js*