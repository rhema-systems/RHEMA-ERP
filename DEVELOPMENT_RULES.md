# ERP System Development Environment Rules

## Critical Environment Configuration

### 1. Always Set ASPNETCORE_ENVIRONMENT for Backend API

**REQUIRED**: The backend API MUST run in Development mode to load user secrets properly.

```powershell
# Set environment variable before running API
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run
```

**Why**: Production mode doesn't load user secrets, causing JWT and database connection failures.

### 2. Database Configuration

**Connection String Location**: User secrets (NOT appsettings.json)
```
Server=localhost;Database=RHEMA-ERP;User Id=sa;Password=sa;TrustServerCertificate=true;MultipleActiveResultSets=true;
```

**Verify SQL Server is Running**:
```powershell
Get-Service -Name "MSSQLSERVER" | Where-Object { $_.Status -eq "Running" }
```

### 3. User Secrets Configuration

**Current User Secrets** (verify with `dotnet user-secrets list`):
- `JwtSettings:SecretKey` = YourVerySecureJwtSecretKeyThatIsAtLeast32CharactersLong123456789
- `Security:EncryptionKey` = YourVerySecureEncryptionKeyThatIsAtLeast32Characters
- `ConnectionStrings:DefaultConnection` = Server=localhost;Database=RHEMA-ERP;User Id=sa;Password=sa;TrustServerCertificate=true;MultipleActiveResultSets=true;
- `ConnectionStrings:Redis` = localhost:6379

### 4. Port Configuration

**Backend API**: http://localhost:5000
**Frontend**: http://localhost:3000

**Frontend API Configuration** (.env.local):
```
NEXT_PUBLIC_API_URL=http://localhost:5000/api
```

### 5. CORS Configuration

Backend allows these origins:
- http://localhost:3000
- https://localhost:3000
- http://localhost:3001
- https://localhost:3001

## Startup Sequence

### 1. Start Backend API (Development Mode)
```powershell
cd C:\erp-system\erp-system\src\ErpSystem.Api
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run
```

**Verify**: http://localhost:5000/api/tenant should return 200 OK with tenant data

### 2. Start Frontend
```powershell
cd C:\erp-system\erp-system\frontend
npm run dev
```

**Verify**: http://localhost:3000 should load without "Failed to fetch" errors

## Troubleshooting Common Issues

### "Failed to fetch" Error
**Root Cause**: Backend API not running or configuration issues
**Solution**:
1. Check if API process is running: `Get-Process -Name "ErpSystem.Api"`
2. Verify API responds: `Invoke-WebRequest -Uri "http://localhost:5000/api/tenant"`
3. Check environment is Development: API logs should show "Development" not "Production"

### JWT Error: "key length is zero"
**Root Cause**: User secrets not loaded (running in Production mode)
**Solution**: Set `$env:ASPNETCORE_ENVIRONMENT = "Development"` before starting API

### Database Connection Error
**Root Cause**: SQL Server not running or wrong connection string
**Solution**:
1. Start SQL Server: `Start-Service MSSQLSERVER`
2. Test connection: `sqlcmd -S localhost -U sa -P sa -Q "SELECT 1"`
3. Verify user secrets: `dotnet user-secrets list`

### CORS Errors
**Root Cause**: Frontend running on wrong port or CORS misconfigured
**Solution**: Ensure frontend runs on localhost:3000

## Development Commands Reference

### Check Services Status
```powershell
# SQL Server
Get-Service -Name "MSSQLSERVER"

# API Process
Get-Process -Name "ErpSystem.Api" -ErrorAction SilentlyContinue

# Node processes (Frontend)
Get-Process | Where-Object { $_.ProcessName -like "*node*" }
```

### Test Connections
```powershell
# Test API
Invoke-WebRequest -Uri "http://localhost:5000/api/tenant" -Method GET

# Test Frontend
Invoke-WebRequest -Uri "http://localhost:3000" -Method HEAD

# Test Database
sqlcmd -S localhost -U sa -P sa -Q "SELECT 1"
```

### User Secrets Management
```powershell
# List all secrets
dotnet user-secrets list

# Set a secret
dotnet user-secrets set "JwtSettings:SecretKey" "YourNewSecretKey"

# Remove a secret
dotnet user-secrets remove "JwtSettings:SecretKey"
```

## File Locations

- **Backend API**: `C:\erp-system\erp-system\src\ErpSystem.Api`
- **Frontend**: `C:\erp-system\erp-system\frontend`
- **API Config**: `C:\erp-system\erp-system\src\ErpSystem.Api\appsettings.Development.json`
- **Frontend Config**: `C:\erp-system\erp-system\frontend\.env.local`
- **User Secrets**: Managed via `dotnet user-secrets` commands in API directory

## Never Do This

❌ **Don't run API without setting ASPNETCORE_ENVIRONMENT=Development**
❌ **Don't put secrets in appsettings.json files**
❌ **Don't assume API is running - always verify**
❌ **Don't ignore SQL Server service status**
❌ **Don't modify CORS settings without understanding implications**

## Always Do This

✅ **Set Development environment before starting API**
✅ **Verify all services are running before troubleshooting**
✅ **Check user secrets are configured properly**
✅ **Test API endpoints directly when debugging frontend issues**
✅ **Use consistent ports (5000 for API, 3000 for frontend)**