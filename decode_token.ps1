# Decode JWT Token to see claims
Write-Host "Generating Token..." -ForegroundColor Cyan
$tokenOutput = & ".\TestLoginApp\bin\Debug\net9.0\TestLoginApp.exe" 2>&1 | Out-String
Write-Host "Raw Login Output: $tokenOutput" -ForegroundColor Yellow

# Extract token
$tokenMatch = $tokenOutput | Select-String -Pattern "TOKEN_START(.+?)TOKEN_END" 
if ($tokenMatch) {
    $token = $tokenMatch.Matches.Groups[1].Value.Trim()
    Write-Host "`nExtracted Token (first 50 chars): $($token.Substring(0, [Math]::Min(50, $token.Length)))..." -ForegroundColor Green
    
    # Decode JWT token (base64 decode the payload)
    $parts = $token.Split('.')
    if ($parts.Length -eq 3) {
        # Decode the payload (second part)
        $payload = $parts[1]
        # Add padding if needed
        while ($payload.Length % 4 -ne 0) {
            $payload += "="
        }
        $payloadBytes = [Convert]::FromBase64String($payload)
        $payloadJson = [System.Text.Encoding]::UTF8.GetString($payloadBytes)
        
        Write-Host "`nJWT Payload:" -ForegroundColor Cyan
        Write-Host $payloadJson -ForegroundColor White
        
        # Parse and display claims
        $claims = $payloadJson | ConvertFrom-Json
        Write-Host "`nClaims:" -ForegroundColor Cyan
        $claims.PSObject.Properties | ForEach-Object {
            Write-Host "  $($_.Name): $($_.Value)" -ForegroundColor White
        }
    }
}
else {
    Write-Host "Failed to extract token" -ForegroundColor Red
    Write-Host "Token Output: $tokenOutput" -ForegroundColor Yellow
}
