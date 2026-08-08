# Production Secrets Management

## Environment Variables (Recommended)

### Setting Environment Variables

**Windows Server/IIS:**
```powershell
# System-wide environment variables (requires admin)
[Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=prod-server;Database=RHEMA-ERP;User Id=erp_user;Password=SecurePassword123!;TrustServerCertificate=true;MultipleActiveResultSets=true;", "Machine")
[Environment]::SetEnvironmentVariable("JwtSettings__SecretKey", "YourProductionJwtSecretKeyThatIsAtLeast32CharactersLongAndVerySecure!", "Machine")
[Environment]::SetEnvironmentVariable("Security__EncryptionKey", "YourProductionEncryptionKeyThatIs32Chars", "Machine")

# Application Pool specific (IIS)
# Set in IIS Manager -> Application Pool -> Advanced Settings -> Process Model -> Environment Variables
```

**Linux/Docker:**
```bash
# In your deployment script or docker-compose.yml
export ConnectionStrings__DefaultConnection="Server=prod-server;Database=RHEMA-ERP;User Id=erp_user;Password=SecurePassword123!;TrustServerCertificate=true;MultipleActiveResultSets=true;"
export JwtSettings__SecretKey="YourProductionJwtSecretKeyThatIsAtLeast32CharactersLongAndVerySecure!"
export Security__EncryptionKey="YourProductionEncryptionKeyThatIs32Chars"
```

### Docker Compose Example
```yaml
version: '3.8'
services:
  erp-api:
    image: erp-system-api:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Server=sql-server;Database=RHEMA-ERP;User Id=erp_user;Password=SecurePassword123!;TrustServerCertificate=true;MultipleActiveResultSets=true;
      - JwtSettings__SecretKey=YourProductionJwtSecretKeyThatIsAtLeast32CharactersLongAndVerySecure!
      - Security__EncryptionKey=YourProductionEncryptionKeyThatIs32Chars
    ports:
      - "80:8080"
```

### Kubernetes Example
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: erp-secrets
type: Opaque
stringData:
  connection-string: "Server=sql-server;Database=RHEMA-ERP;User Id=erp_user;Password=SecurePassword123!;TrustServerCertificate=true;MultipleActiveResultSets=true;"
  jwt-secret: "YourProductionJwtSecretKeyThatIsAtLeast32CharactersLongAndVerySecure!"
  encryption-key: "YourProductionEncryptionKeyThatIs32Chars"
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: erp-api
spec:
  template:
    spec:
      containers:
      - name: api
        image: erp-system-api:latest
        env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: erp-secrets
              key: connection-string
        - name: JwtSettings__SecretKey
          valueFrom:
            secretKeyRef:
              name: erp-secrets
              key: jwt-secret
        - name: Security__EncryptionKey
          valueFrom:
            secretKeyRef:
              name: erp-secrets
              key: encryption-key
```

## Cloud-Specific Secret Management

### Azure Key Vault (Recommended for Azure)
```csharp
// Add to your Program.cs for Azure Key Vault integration
builder.Configuration.AddAzureKeyVault(
    vaultUri: new Uri("https://your-keyvault.vault.azure.net/"),
    credential: new DefaultAzureCredential());
```

**Setup in Azure:**
1. Create Azure Key Vault
2. Add secrets:
   - `ConnectionStrings--DefaultConnection`
   - `JwtSettings--SecretKey`
   - `JwtSettings--PortalSecretKey`  (external-portal token key — REQUIRED, MUST differ from `JwtSettings--SecretKey`)
   - `Security--EncryptionKey`
3. Grant your app's managed identity access

### AWS Systems Manager Parameter Store
```csharp
// Add AWS Systems Manager configuration
builder.Configuration.AddSystemsManager("/erp-system/");
```

### Google Secret Manager
```csharp
// Add Google Secret Manager configuration
builder.Configuration.AddGoogleSecretManager();
```

## appsettings.Production.json (Non-Sensitive Settings Only)

**Safe for version control:**
```json
{
  "Database": {
    "Provider": "SqlServer"
  },
  "JwtSettings": {
    "Issuer": "ErpSystem.Api",
    "Audience": "ErpSystem.Client",
    "ExpiryInHours": 1,
    "PortalAudience": "ErpSystem.Portal"
  },
  "CandidatePortal": {
    "PortalUrl": "https://portal.example.com"
  },
  "Security": {
    "EnableHsts": true,
    "RequireHttps": true
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Warning",
      "Override": {
        "ErpSystem": "Information"
      }
    }
  }
}
```

## Configuration Priority in Production

1. **appsettings.json** (base settings)
2. **appsettings.Production.json** (production overrides)
3. **Environment Variables** (secrets - HIGHEST PRIORITY)
4. **Command Line Arguments** (deployment overrides)

## Security Best Practices

### ✅ DO:
- Use environment variables for secrets
- Use cloud secret managers (Azure Key Vault, AWS Secrets Manager)
- Rotate secrets regularly
- Use different secrets for each environment
- Limit access to production secrets
- Use managed identities/service accounts
- Encrypt secrets at rest and in transit

### ❌ DON'T:
- Put secrets in appsettings files
- Commit secrets to version control
- Share production secrets via email/chat
- Use the same secrets across environments
- Store secrets in plain text files
- Use weak or default passwords

## Example Deployment Script

```powershell
# Production deployment script
param(
    [string]$ConnectionString,
    [string]$JwtSecret,
    [string]$EncryptionKey
)

# Set environment variables
[Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", $ConnectionString, "Machine")
[Environment]::SetEnvironmentVariable("JwtSettings__SecretKey", $JwtSecret, "Machine")
[Environment]::SetEnvironmentVariable("Security__EncryptionKey", $EncryptionKey, "Machine")
[Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production", "Machine")

# Deploy application
dotnet publish src/ErpSystem.Api -c Release -o ./publish
# Copy to IIS or run as service
```

## Testing Configuration

Create a simple endpoint to verify configuration is loaded correctly (without exposing secrets):

```csharp
[ApiController]
[Route("api/[controller]")]
public class ConfigTestController : ControllerBase
{
    private readonly IConfiguration _config;

    public ConfigTestController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("status")]
    public IActionResult GetConfigStatus()
    {
        return Ok(new
        {
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            HasConnectionString = !string.IsNullOrEmpty(_config.GetConnectionString("DefaultConnection")),
            HasJwtSecret = !string.IsNullOrEmpty(_config["JwtSettings:SecretKey"]),
            HasEncryptionKey = !string.IsNullOrEmpty(_config["Security:EncryptionKey"]),
            DatabaseProvider = _config["Database:Provider"]
        });
    }
}
```