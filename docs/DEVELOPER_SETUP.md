# Developer Setup Guide

## Prerequisites
- .NET 8 SDK
- SQL Server (or SQL Server Express)
- Redis (optional, for caching)

## Setting Up User Secrets

After cloning the repository, you need to set up your local user secrets:

### 1. Navigate to the API project
```bash
cd src/ErpSystem.Api
```

### 2. Set up your database connection
```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=RHEMA-ERP;User Id=YOUR_USERNAME;Password=YOUR_PASSWORD;TrustServerCertificate=true;MultipleActiveResultSets=true;"
```

### 3. Set up Redis connection (if using Redis)
```bash
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"
```

### 4. Set up JWT Secret Key
```bash
dotnet user-secrets set "JwtSettings:SecretKey" "YourVerySecureJwtSecretKeyThatIsAtLeast32CharactersLong123456789"
```

### 5. Set up Encryption Key
```bash
dotnet user-secrets set "Security:EncryptionKey" "YourVerySecureEncryptionKeyThatIsAtLeast32Characters"
```

### 6. Verify your secrets
```bash
dotnet user-secrets list
```

## Database Setup

### 1. Create the database
Make sure SQL Server is running and create the database:
```sql
CREATE DATABASE [RHEMA-ERP];
```

### 2. Run migrations
```bash
dotnet ef database update --project src/ErpSystem.Data --startup-project src/ErpSystem.Api
```

## Running the Application

### Development Mode
```bash
dotnet run --project src/ErpSystem.Api --environment Development
```

The application will be available at:
- API: https://localhost:5001
- Swagger: https://localhost:5001/swagger

## Alternative Configuration Methods

### Option 1: appsettings.Development.json (Not Recommended for Production)
You can create `src/ErpSystem.Api/appsettings.Development.json` with your local settings:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=RHEMA-ERP;User Id=YOUR_USERNAME;Password=YOUR_PASSWORD;TrustServerCertificate=true;MultipleActiveResultSets=true;",
    "Redis": "localhost:6379"
  },
  "JwtSettings": {
    "SecretKey": "YourVerySecureJwtSecretKeyThatIsAtLeast32CharactersLong123456789"
  },
  "Security": {
    "EncryptionKey": "YourVerySecureEncryptionKeyThatIsAtLeast32Characters"
  }
}
```

**Note**: Add `appsettings.Development.json` to `.gitignore` if using this approach.

### Option 2: Environment Variables
Set environment variables:
```bash
$env:ConnectionStrings__DefaultConnection="Server=localhost;Database=RHEMA-ERP;User Id=YOUR_USERNAME;Password=YOUR_PASSWORD;TrustServerCertificate=true;MultipleActiveResultSets=true;"
$env:JwtSettings__SecretKey="YourVerySecureJwtSecretKeyThatIsAtLeast32CharactersLong123456789"
$env:Security__EncryptionKey="YourVerySecureEncryptionKeyThatIsAtLeast32Characters"
```

## Security Notes

- **Never commit secrets to version control**
- Use different database credentials for each developer
- Generate unique JWT and encryption keys for production
- Consider using Azure Key Vault or similar for production secrets