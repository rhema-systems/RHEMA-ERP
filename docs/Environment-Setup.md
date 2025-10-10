# Environment Configuration Setup

## 🔒 Secure Configuration Management

This guide shows how to securely configure the ERP System using environment variables and user secrets, following security best practices.

## 📋 Option 1: User Secrets (Recommended for Development)

### Initialize User Secrets
```powershell
cd C:\erp-system\erp-system\src\ErpSystem.Api
dotnet user-secrets init
```

### Set Database Configuration
```powershell
# SQL Server Connection String
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=YOUR_SERVER_NAME;Database=ErpSystemDB;User Id=YOUR_USERNAME;Password=YOUR_PASSWORD;TrustServerCertificate=true;MultipleActiveResultSets=true;"

# Redis Connection (optional)
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"
```

### Set Security Keys
```powershell
# JWT Secret Key (at least 32 characters)
dotnet user-secrets set "JwtSettings:SecretKey" "YourVerySecureJwtSecretKeyThatIsAtLeast32CharactersLong123456789"

# Encryption Key (at least 32 characters)
dotnet user-secrets set "Security:EncryptionKey" "YourVerySecureEncryptionKeyThatIsAtLeast32Characters"
```

### Common Connection String Examples

#### Local SQL Server Express:
```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost\\SQLEXPRESS;Database=ErpSystemDB;User Id=sa;Password=YourPassword123;TrustServerCertificate=true;MultipleActiveResultSets=true;"
```

#### Windows Authentication:
```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=ErpSystemDB;Integrated Security=true;TrustServerCertificate=true;MultipleActiveResultSets=true;"
```

#### SQL Server LocalDB:
```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\\MSSQLLocalDB;Database=ErpSystemDB;Integrated Security=true;TrustServerCertificate=true;"
```

### View Current Secrets
```powershell
dotnet user-secrets list
```

## 📋 Option 2: Environment Variables (Production/CI/CD)

### Windows PowerShell (Current Session)
```powershell
# Database Connection
$env:ConnectionStrings__DefaultConnection = "Server=YOUR_SERVER_NAME;Database=ErpSystemDB;User Id=YOUR_USERNAME;Password=YOUR_PASSWORD;TrustServerCertificate=true;MultipleActiveResultSets=true;"
$env:ConnectionStrings__Redis = "localhost:6379"

# Security Keys
$env:JwtSettings__SecretKey = "YourVerySecureJwtSecretKeyThatIsAtLeast32CharactersLong123456789"
$env:Security__EncryptionKey = "YourVerySecureEncryptionKeyThatIsAtLeast32Characters"
```

### Windows System Environment Variables (Permanent)
```powershell
# Set system-wide environment variables (requires admin)
[System.Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=YOUR_SERVER_NAME;Database=ErpSystemDB;User Id=YOUR_USERNAME;Password=YOUR_PASSWORD;TrustServerCertificate=true;MultipleActiveResultSets=true;", "User")
[System.Environment]::SetEnvironmentVariable("JwtSettings__SecretKey", "YourVerySecureJwtSecretKeyThatIsAtLeast32CharactersLong123456789", "User")
[System.Environment]::SetEnvironmentVariable("Security__EncryptionKey", "YourVerySecureEncryptionKeyThatIsAtLeast32Characters", "User")
```

### Docker Environment Variables
```yaml
# docker-compose.yml
version: '3.8'
services:
  erp-api:
    build: .
    environment:
      - ConnectionStrings__DefaultConnection=Server=db;Database=ErpSystemDB;User Id=sa;Password=YourPassword123;TrustServerCertificate=true;
      - JwtSettings__SecretKey=YourVerySecureJwtSecretKeyThatIsAtLeast32CharactersLong123456789
      - Security__EncryptionKey=YourVerySecureEncryptionKeyThatIsAtLeast32Characters
      - ConnectionStrings__Redis=redis:6379
```

## 🔐 Security Best Practices

### Generate Secure Keys
Use these commands to generate cryptographically secure keys:

```powershell
# Generate JWT Secret Key (64 characters)
$jwtKey = [System.Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
Write-Host "JWT Secret Key: $jwtKey"

# Generate Encryption Key (44 characters base64 = 32 bytes)
$encKey = [System.Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
Write-Host "Encryption Key: $encKey"
```

### Key Requirements
- **JWT Secret Key**: Minimum 32 characters, recommended 64+ characters
- **Encryption Key**: Exactly 32 bytes (44 base64 characters)
- **Database Password**: Strong password with special characters
- **Never commit secrets to version control**

## 🚀 Quick Start Commands

### 1. Initialize User Secrets
```powershell
cd C:\erp-system\erp-system\src\ErpSystem.Api
dotnet user-secrets init
```

### 2. Set Your Database Connection
Choose your SQL Server setup:

**Local SQL Express:**
```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost\\SQLEXPRESS;Database=ErpSystemDB;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=true;MultipleActiveResultSets=true;"
```

**Windows Authentication:**
```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=ErpSystemDB;Integrated Security=true;TrustServerCertificate=true;MultipleActiveResultSets=true;"
```

### 3. Set Security Keys
```powershell
dotnet user-secrets set "JwtSettings:SecretKey" "YourVerySecureJwtSecretKeyThatIsAtLeast32CharactersLong123456789"
dotnet user-secrets set "Security:EncryptionKey" "YourVerySecureEncryptionKeyThatIsAtLeast32Characters"
```

### 4. Optional: Redis Connection
```powershell
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"
```

### 5. Apply Database Migrations
```powershell
cd C:\erp-system\erp-system
dotnet ef database update --project src/ErpSystem.Data --startup-project src/ErpSystem.Api
```

### 6. Run the Application
```powershell
dotnet run --project src/ErpSystem.Api
```

## 🔍 Troubleshooting

### Check User Secrets Location
```powershell
cd C:\erp-system\erp-system\src\ErpSystem.Api
dotnet user-secrets list
```

### Verify Environment Variables
```powershell
# Check if environment variables are set
Write-Host "DefaultConnection: $($env:ConnectionStrings__DefaultConnection)"
Write-Host "JWT Key: $($env:JwtSettings__SecretKey)"
```

### Clear User Secrets (if needed)
```powershell
dotnet user-secrets clear
```

## 📁 Configuration Priority

.NET Core loads configuration in this order (later overrides earlier):
1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. User secrets (Development environment only)
4. Environment variables
5. Command-line arguments

## 🛡️ Production Deployment

For production environments:
- Use environment variables or secure configuration providers
- Never use user secrets in production
- Consider Azure Key Vault, AWS Secrets Manager, or similar
- Use secure CI/CD pipelines to inject secrets
- Enable encryption at rest for configuration data

## 📝 Notes

- User secrets are stored locally and not committed to version control
- Environment variables use double underscores (`__`) for nested configuration
- The template file `secrets-template.json` shows the expected structure but should not contain real secrets
- Always validate your connection string before running migrations