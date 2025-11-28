# Test Debug Claims Endpoint
Write-Host "Generating Token..." -ForegroundColor Cyan
$tokenOutput = & ".\TestLoginApp\bin\Debug\net9.0\TestLoginApp.exe" 2>&1 | Out-String

# Extract token
$tokenMatch = $tokenOutput | Select-String -Pattern "TOKEN_START(.+?)TOKEN_END" 
if ($tokenMatch) {
    $token = $tokenMatch.Matches.Groups[1].Value.Trim()
    Write-Host "`nToken extracted successfully!" -ForegroundColor Green
    
    # Call debug endpoint
    Write-Host "`nCalling debug/claims endpoint..." -ForegroundColor Cyan
    $headers = @{
        "Authorization" = "Bearer $token"
        "Content-Type"  = "application/json"
    }
    
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:53484/api/finance/debug/claims" -Method GET -Headers $headers
        Write-Host "`nDebug Claims Response:" -ForegroundColor Green
        $response | ConvertTo-Json -Depth 10 | Write-Host
    }
    catch {
        Write-Host "`nError calling debug endpoint:" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host "Response: $responseBody" -ForegroundColor Yellow
        }
    }
}
else {
    Write-Host "Failed to extract token" -ForegroundColor Red
}
