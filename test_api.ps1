# PowerShell script to test the API fix

# Step 1: Login to get a fresh JWT token
Write-Host "Step 1: Logging in..." -ForegroundColor Yellow
$loginBody = @{
    username = "Admin"
    password = "Admin123!"
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" -Method POST -Body $loginBody -ContentType "application/json"
    $token = $loginResponse.token
    Write-Host "Login successful! Token received." -ForegroundColor Green
    Write-Host "User: $($loginResponse.user.username)" -ForegroundColor Cyan
    Write-Host "TenantId: $($loginResponse.user.currentTenantId)" -ForegroundColor Cyan
    Write-Host "TenantCode: $($loginResponse.user.currentTenantCode)" -ForegroundColor Cyan
    Write-Host "Full login response: $($loginResponse | ConvertTo-Json -Depth 5)" -ForegroundColor DarkGray
    
    # Decode JWT token (basic decode without verification)
    $tokenParts = $token.Split('.')
    if ($tokenParts.Length -ge 2) {
        $payload = $tokenParts[1]
        # Add padding if needed
        while ($payload.Length % 4 -ne 0) {
            $payload += "="
        }
        try {
            $payloadBytes = [System.Convert]::FromBase64String($payload)
            $payloadJson = [System.Text.Encoding]::UTF8.GetString($payloadBytes)
            $payloadObj = $payloadJson | ConvertFrom-Json
            Write-Host "JWT Payload: $($payloadObj | ConvertTo-Json -Depth 3)" -ForegroundColor DarkCyan
        } catch {
            Write-Host "Could not decode JWT payload: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "Login failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 2: Test simple API endpoints first
Write-Host "`nStep 2: Testing simple endpoints first..." -ForegroundColor Yellow
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Test current user endpoint
try {
    $userResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/me" -Method GET -Headers $headers
    Write-Host "Current user API successful!" -ForegroundColor Green
    Write-Host "Current user tenant: $($userResponse.currentTenantId)" -ForegroundColor Cyan
} catch {
    Write-Host "Current user API failed: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Test diagnostic endpoint to check database
try {
    Write-Host "Testing diagnostic endpoint..." -ForegroundColor Cyan
    $diagResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/diagnostic/tenant-data" -Method GET -Headers $headers
    Write-Host "Diagnostics successful!" -ForegroundColor Green
    Write-Host "Tenant exists: $($diagResponse.tenantExists)" -ForegroundColor Cyan
    Write-Host "Tenant name: $($diagResponse.tenantName)" -ForegroundColor Cyan
    Write-Host "Users in tenant: $($diagResponse.userCount)" -ForegroundColor Cyan
    Write-Host "Sessions in tenant: $($diagResponse.sessionCount)" -ForegroundColor Cyan
    Write-Host "Full diagnostics: $($diagResponse | ConvertTo-Json -Depth 3)" -ForegroundColor DarkGray
} catch {
    Write-Host "Diagnostic API failed: $($_.Exception.Message)" -ForegroundColor Yellow
    if ($_.Exception.Response) {
        try {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $responseBody = $reader.ReadToEnd()
            Write-Host "Diagnostic error response: $responseBody" -ForegroundColor Red
        } catch {
            Write-Host "Could not read diagnostic error response" -ForegroundColor Red
        }
    }
}

# Test simple online users endpoint
try {
    Write-Host "Testing simple online users endpoint..." -ForegroundColor Cyan
    $simpleResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/diagnostic/simple-online-users" -Method GET -Headers $headers
    Write-Host "Simple online users successful!" -ForegroundColor Green
    Write-Host "Online users count: $($simpleResponse.count)" -ForegroundColor Cyan
    Write-Host "Simple response: $($simpleResponse | ConvertTo-Json -Depth 3)" -ForegroundColor DarkGray
} catch {
    Write-Host "Simple online users API failed: $($_.Exception.Message)" -ForegroundColor Yellow
    if ($_.Exception.Response) {
        try {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $responseBody = $reader.ReadToEnd()
            Write-Host "Simple online users error response: $responseBody" -ForegroundColor Red
        } catch {
            Write-Host "Could not read simple online users error response" -ForegroundColor Red
        }
    }
}

# Step 3: Test the dashboard endpoints
Write-Host "`nStep 3: Testing dashboard endpoints..." -ForegroundColor Yellow

# Try the main dashboard endpoint first
try {
    Write-Host "Testing main dashboard endpoint..." -ForegroundColor Cyan
    $dashResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/dashboard" -Method GET -Headers $headers
    Write-Host "Main dashboard API successful!" -ForegroundColor Green
    Write-Host "Dashboard has online users: $($dashResponse.onlineUsers.Count)" -ForegroundColor Cyan
} catch {
    Write-Host "Main dashboard API failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        try {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $responseBody = $reader.ReadToEnd()
            Write-Host "Dashboard error response: $responseBody" -ForegroundColor Red
        } catch {
            Write-Host "Could not read dashboard error response" -ForegroundColor Red
        }
    }
}

# Now try the online-users endpoint
Write-Host "`nTesting online-users endpoint..." -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/dashboard/online-users" -Method GET -Headers $headers
    Write-Host "API call successful!" -ForegroundColor Green
    Write-Host "Response: $($response | ConvertTo-Json -Depth 3)" -ForegroundColor Cyan
} catch {
    Write-Host "API call failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    Write-Host "Status Description: $($_.Exception.Response.StatusDescription)" -ForegroundColor Red
    
    if ($_.Exception.Response) {
        try {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $responseBody = $reader.ReadToEnd()
            Write-Host "Error response body: $responseBody" -ForegroundColor Red
        } catch {
            Write-Host "Could not read error response body" -ForegroundColor Red
        }
    }
}

Write-Host "`nTest completed!" -ForegroundColor Yellow