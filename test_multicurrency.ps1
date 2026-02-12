# Test Multi-Currency Operations
# This script verifies the functionality of multi-currency features:
# 1. Add Currency Link to Account
# 2. Get Account Currency Links
# 3. Inactivate Currency Link
# 4. Remove Currency Link

$baseUrl = "http://localhost:53484"
$token = ""

# 1. Get Token using TestLoginApp
Write-Host "Generating Token..." -ForegroundColor Cyan
try {
    # Run TestLoginApp and save output to file
    & ".\TestLoginApp\bin\Debug\net9.0\TestLoginApp.exe" > token_output_temp.txt 2>&1
    
    # Extract token from file
    $output = Get-Content token_output_temp.txt -Raw
    $start = $output.IndexOf('TOKEN_START') + 11
    $end = $output.IndexOf('TOKEN_END')
    
    if ($start -gt 10 -and $end -gt $start) {
        $token = $output.Substring($start, $end - $start).Trim()
        Write-Host "Token generated successfully." -ForegroundColor Green
        Write-Host "Token (first 50 chars): $($token.Substring(0, [Math]::Min(50, $token.Length)))..." -ForegroundColor Gray
    }
    else {
        Write-Error "Failed to extract token from TestLoginApp output."
        Write-Host "Output: $output" -ForegroundColor Yellow
        exit 1
    }
}
catch {
    Write-Error "Failed to run TestLoginApp: $_"
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}

# Helper function to make API requests
function Invoke-ApiRequest {
    param (
        [string]$Method,
        [string]$Uri,
        [hashtable]$Body = $null
    )
    
    try {
        $params = @{
            Method      = $Method
            Uri         = $baseUrl + $Uri
            Headers     = $headers
            ErrorAction = "Stop"
        }
        
        if ($Body) {
            $params.Body = $Body | ConvertTo-Json -Depth 10
        }
        
        $response = Invoke-RestMethod @params
        return $response
    }
    catch {
        Write-Host "Error calling $Uri" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host "Response Body: $responseBody" -ForegroundColor Red
        }
        return $null
    }
}

# --- Test Data ---
Write-Host "`nCreating Test Account for Multi-Currency..." -ForegroundColor Cyan
# Use valid department codes from lookup table (100, 200, 300, 400, 500, 600, 700, 800, 900)
$validDeptCodes = @("100", "200", "300", "400", "500", "600", "700", "800", "900")
$accountCode = $validDeptCodes | Get-Random
$createAccountBody = @{
    accountCode     = $accountCode
    accountNumber   = $accountCode
    accountName     = "Multi-Currency Test Account"
    accountType     = "Asset"
    currencyCode    = "GHS"
    isMultiCurrency = $true
}

$account = Invoke-ApiRequest -Method Post -Uri "/api/finance/accounts" -Body $createAccountBody

if (-not $account) {
    Write-Error "Failed to create test account. Aborting."
    exit 1
}

$accountId = $account.id
Write-Host "Created Account ID: $accountId ($accountCode)" -ForegroundColor Green

# --- 1. Add Currency Link ---
Write-Host "`nTesting: Add Currency Link (USD)" -ForegroundColor Cyan
$addLinkBody = @{
    currencyCode = "USD"
    isActive     = $true
}

$linkResult = Invoke-ApiRequest -Method Post -Uri "/api/finance/accounts/$accountId/currencies" -Body $addLinkBody

if ($linkResult) {
    Write-Host "SUCCESS: Added USD link" -ForegroundColor Green
    $linkResult | Format-List
}
else {
    Write-Host "FAILED to add currency link" -ForegroundColor Red
}

# --- 2. Get Account Currency Links ---
Write-Host "`nTesting: Get Account Currency Links" -ForegroundColor Cyan
$links = Invoke-ApiRequest -Method Get -Uri "/api/finance/accounts/$accountId/currencies"

if ($links -and $links.Count -gt 0) {
    Write-Host "SUCCESS: Retrieved $($links.Count) links" -ForegroundColor Green
    $links | Format-Table currencyCode, isActive, foreignCurrencyBalance
}
else {
    Write-Host "FAILED to retrieve links or no links found" -ForegroundColor Red
}

# --- 3. Inactivate Currency Link ---
Write-Host "`nTesting: Inactivate Currency Link (USD)" -ForegroundColor Cyan
# Route: PATCH /api/finance/accounts/{id}/currencies/{currencyCode}/inactivate

$deactivateResult = Invoke-ApiRequest -Method Patch -Uri "/api/finance/accounts/$accountId/currencies/USD/inactivate"

if ($deactivateResult) {
    Write-Host "SUCCESS: Deactivated USD link" -ForegroundColor Green
}
else {
    Write-Host "FAILED to deactivate link (might be route issue)" -ForegroundColor Red
}

# --- 4. Remove Currency Link ---
Write-Host "`nTesting: Remove Currency Link (USD)" -ForegroundColor Cyan
# DELETE /api/finance/accounts/{id}/currencies/{currencyCode}

$removeResult = Invoke-ApiRequest -Method Delete -Uri "/api/finance/accounts/$accountId/currencies/USD"

if ($removeResult) {
    Write-Host "SUCCESS: Removed USD link" -ForegroundColor Green
}
else {
    Write-Host "FAILED to remove link" -ForegroundColor Red
}

Write-Host "`nMulti-Currency Test Complete" -ForegroundColor Cyan
