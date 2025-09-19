# 🔄 Database Switching Quick Guide

## 🎯 **How to Switch Databases in Your ERP System**

Your ERP system now supports multiple database providers with easy switching. Follow these steps:

---

## 📋 **Step-by-Step Instructions**

### **🔧 Step 1: Choose Your Database**

| **Database** | **Best For** | **License** | **Effort** |
|--------------|-------------|-------------|------------|
| **SQL Server** | Microsoft ecosystem, Windows | Commercial | ✅ Current |
| **PostgreSQL** | Open source, advanced features | Free | 🔄 Easy |
| **MySQL** | Web applications, popular | Free | 🔄 Easy |
| **Oracle** | Enterprise, large scale | Commercial | 🔄 Medium |
| **SQLite** | Development, small apps | Free | 🔄 Easy |
| **IBM DB2** | Enterprise, mainframe integration | Commercial | 🔄 Medium |
| **Firebird** | Embedded, lightweight | Free | 🔄 Easy |
| **Cosmos DB** | Cloud-native, NoSQL | Pay-per-use | 🔄 Advanced |
| **In-Memory** | Testing, development | Free | 🔄 Easy |

---

### **⚙️ Step 2: Update Configuration**

#### **2.1 Update appsettings.json**
```json
{
  "Database": {
    "Provider": "PostgreSQL"  // ← Change this value
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=RhemaERPTDC;Username=postgres;Password=your_password;"
  }
}
```

#### **2.2 Provider Values:**
- `"SqlServer"` - Microsoft SQL Server
- `"PostgreSQL"` - PostgreSQL  
- `"MySQL"` - MySQL/MariaDB
- `"Oracle"` - Oracle Database
- `"SQLite"` - SQLite
- `"DB2"` - IBM DB2
- `"Firebird"` - Firebird Database
- `"CosmosDB"` - Azure Cosmos DB
- `"InMemory"` - In-Memory Database (testing only)

---

### **📦 Step 3: Uncomment NuGet Package**

Edit `ErpSystem.Api.csproj` and uncomment the package you need:

#### **For PostgreSQL:**
```xml
<!-- Uncomment this line -->
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
```

#### **For MySQL:**
```xml  
<!-- Uncomment this line -->
<PackageReference Include="Pomelo.EntityFrameworkCore.MySql" Version="8.0.0" />
```

#### **For Oracle:**
```xml
<!-- Uncomment this line -->  
<PackageReference Include="Oracle.EntityFrameworkCore" Version="8.0.0" />
```

#### **For SQLite:**
```xml
<!-- Uncomment this line -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.0" />
```

#### **For IBM DB2:**
```xml
<!-- Uncomment this line -->
<PackageReference Include="IBM.EntityFrameworkCore" Version="8.0.0" />
```

#### **For Firebird:**
```xml
<!-- Uncomment this line -->
<PackageReference Include="FirebirdSql.EntityFrameworkCore.Firebird" Version="11.0.0" />
```

#### **For Azure Cosmos DB:**
```xml
<!-- Uncomment this line -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Cosmos" Version="8.0.0" />
```

#### **For In-Memory (Testing):**
```xml
<!-- Uncomment this line -->
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.0" />
```

---

### **🔓 Step 4: Uncomment Database Code**

Edit `Extensions/DatabaseConfiguration.cs` and uncomment the method for your chosen database:

#### **For PostgreSQL:**
```csharp
private static void ConfigurePostgreSQL(DbContextOptionsBuilder options, string connectionString)
{
    // Remove the NotImplementedException and uncomment:
    /*
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.MigrationsAssembly("ErpSystem.Data");
        npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30));
        npgsqlOptions.CommandTimeout(30);
    });
    */
}
```

#### **Similar for other databases** - uncomment the respective method.

---

### **🗄️ Step 5: Update Connection String**

#### **SQL Server (Current):**
```json
"DefaultConnection": "Server=localhost\\sql2017;Database=RhemaERPTDC;User Id=sa;Password=sa;MultipleActiveResultSets=true;TrustServerCertificate=true;"
```

#### **PostgreSQL:**
```json
"DefaultConnection": "Host=localhost;Database=RhemaERPTDC;Username=postgres;Password=your_password;Port=5432;"
```

#### **MySQL:**
```json
"DefaultConnection": "Server=localhost;Database=RhemaERPTDC;Uid=root;Pwd=your_password;Port=3306;"
```

#### **Oracle:**
```json
"DefaultConnection": "Data Source=localhost:1521/XE;User Id=hr;Password=your_password;"
```

#### **SQLite:**
```json
"DefaultConnection": "Data Source=RhemaERPTDC.db"
```

#### **IBM DB2:**
```json
"DefaultConnection": "Server=localhost:50000;Database=RhemaERPTDC;UID=db2admin;PWD=your_password;"
```

#### **Firebird:**
```json
"DefaultConnection": "DataSource=localhost;Database=RhemaERPTDC.fdb;User=SYSDBA;Password=your_password;Port=3050;"
```

#### **Azure Cosmos DB:**
```json
"DefaultConnection": "AccountEndpoint=https://your-account.documents.azure.com:443/;AccountKey=your_primary_key;DatabaseName=RhemaERPTDC;"
```

#### **In-Memory (Testing):**
```json
"DefaultConnection": "TestDatabase"
```

---

### **🔄 Step 6: Migrate Database**

```bash
# Remove old migrations (if switching completely)
dotnet ef migrations remove

# Add new initial migration
dotnet ef migrations add InitialMigration_PostgreSQL

# Update database
dotnet ef database update
```

---

### **🚀 Step 7: Test & Verify**

```bash
# Build the project
dotnet build

# Run the application
dotnet run

# Check the logs for database provider confirmation
```

---

## 🎯 **Example: Switching to PostgreSQL**

### **Complete Example:**

#### **1. Install PostgreSQL** (if not already installed)

#### **2. Update appsettings.json:**
```json
{
  "Database": {
    "Provider": "PostgreSQL"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=RhemaERPTDC;Username=postgres;Password=your_password;"
  }
}
```

#### **3. Uncomment in ErpSystem.Api.csproj:**
```xml
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
```

#### **4. Uncomment in DatabaseConfiguration.cs:**
```csharp
private static void ConfigurePostgreSQL(DbContextOptionsBuilder options, string connectionString)
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.MigrationsAssembly("ErpSystem.Data");
        npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30));
        npgsqlOptions.CommandTimeout(30);
    });
}
```

#### **5. Run migrations:**
```bash
dotnet ef database update
```

#### **6. Start application:**
```bash
dotnet run
```

✅ **Done!** Your ERP system is now running on PostgreSQL!

---

## ⚠️ **Important Notes**

1. **Data Migration**: Switching databases doesn't migrate existing data automatically
2. **Connection Strings**: Make sure your database server is running and accessible
3. **Package Versions**: Keep database provider packages updated
4. **Testing**: Always test thoroughly after switching databases
5. **Backup**: Backup your SQL Server data before switching

---

## 🆘 **Troubleshooting**

### **"Provider not found" Error:**
- Check that the NuGet package is uncommented and restored
- Verify the provider name in appsettings.json matches exactly

### **"Connection failed" Error:**
- Verify database server is running
- Check connection string parameters
- Confirm database exists

### **"Migration failed" Error:**
- Remove old migrations specific to previous database
- Create fresh migrations for new database
- Check for database-specific syntax in custom SQL

---

## 📞 **Need Help?**

If you encounter issues:
1. Check the database-agnostic best practices guide
2. Verify all steps were followed correctly  
3. Check application logs for specific error messages
4. Test with a simple connection first

**Your ERP system is designed to be database-agnostic - switching should be smooth!** 🚀