# 🏢 ERP System - Database-Agnostic Enterprise Resource Planning

A modern, multi-tenant, database-agnostic Enterprise Resource Planning (ERP) system built with **.NET 8**, **Entity Framework Core**, and **Next.js**. Supports **9 different database providers** with seamless switching capabilities.

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
Edit `src/ErpSystem.Api/appsettings.json`:
```json
{
  "Database": {
    "Provider": "SqlServer",  // Choose: SqlServer, PostgreSQL, MySQL, etc.
    "ConnectionString": "your-connection-string-here"
  }
}
```

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

## 📊 **ERP Modules**

### **Core Modules**
- ✅ **User Management**: Users, roles, permissions, phone numbers with country codes
- ✅ **Tenant Management**: Multi-tenant organization with advanced branding
- ✅ **Settings Management**: Email, password policies
- ✅ **Audit & Security Logs**: Complete audit trail
- ✅ **Data Tables**: Enhanced grids with striped rows, compact design, clickable selection
- ✅ **File Management**: Flexible storage abstraction with provider switching
- ✅ **User Interface**: Responsive design with enhanced forms and authentication flows

### **Business Modules** (Roadmap)
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

## 📈 **Performance**

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

## 🚀 **What's Next?**

### **Upcoming Features**
- [ ] Real-time notifications (SignalR)
- [ ] Advanced reporting engine
- [ ] Mobile application (React Native)
- [ ] Microservices architecture
- [ ] Kubernetes deployment
- [ ] Advanced workflow engine
- [ ] AI/ML integration
- [ ] Advanced analytics dashboard

### **Database Enhancements**
- [ ] Database performance monitoring
- [ ] Automatic failover support
- [ ] Read replica support
- [ ] Database sharding
- [ ] Cross-database queries

---

## 🎯 **Why Choose This ERP System?**

✅ **Database Freedom**: Switch between 9 database providers without code changes  
✅ **Enterprise Ready**: Multi-tenant, secure, scalable architecture  
✅ **Modern Stack**: Latest .NET 8, Next.js 14, TypeScript  
✅ **Comprehensive**: Full ERP functionality with modular design  
✅ **Developer Friendly**: Extensive documentation and examples  
✅ **Production Ready**: Docker, monitoring, logging, testing included  

**Your business shouldn't be locked to a database. Choose the ERP system that adapts to your infrastructure, not the other way around.** 🎯

---

*Built with ❤️ using .NET 8, Entity Framework Core, and Next.js*