# E2E Maintenance Test Data Seeder Script
# This script will seed all master tables required for end-to-end maintenance workflow testing

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  E2E MAINTENANCE TEST DATA SEEDER" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Step 1: Set Encryption Key (if not already set)
Write-Host "Step 1: Checking Encryption Key..." -ForegroundColor Yellow
cd ..\src\ErpSystem.Api

$keyExists = dotnet user-secrets list | Select-String "Security:EncryptionKey"

if (-not $keyExists) {
    Write-Host "  → Generating new encryption key..." -ForegroundColor Gray
    $bytes = New-Object byte[] 32
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $key = [Convert]::ToBase64String($bytes)
    
    dotnet user-secrets set "Security:EncryptionKey" $key
    Write-Host "  ✓ Encryption key generated and saved" -ForegroundColor Green
} else {
    Write-Host "  ✓ Encryption key already exists" -ForegroundColor Green
}

Write-Host ""

# Step 2: Build the project
Write-Host "Step 2: Building project..." -ForegroundColor Yellow
dotnet build --configuration Debug
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ✗ Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "  ✓ Build successful" -ForegroundColor Green
Write-Host ""

# Step 3: Run the application (seeding will happen automatically on startup in Development)
Write-Host "Step 3: Starting application to run seeder..." -ForegroundColor Yellow
Write-Host "  → The seeder will run automatically on startup" -ForegroundColor Gray
Write-Host "  → Press Ctrl+C to stop after seeding completes" -ForegroundColor Gray
Write-Host ""

dotnet run --configuration Debug

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  SEEDING COMPLETED!" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
