# Navigate to the Data project
Set-Location "C:\erp-system\erp-system\src\ErpSystem.Data"

Write-Host "Updating database with latest migrations..." -ForegroundColor Cyan

# Update database
dotnet ef database update --startup-project ..\ErpSystem.Api\ErpSystem.Api.csproj --project ErpSystem.Data.csproj

Write-Host "Database updated successfully!" -ForegroundColor Green
