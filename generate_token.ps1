# Script to generate a JWT token by logging in to the API
param(
    [string]$BaseUrl = "https://localhost:53484/api/auth/login",
    [string]$Username = "admin",
    [string]$Password = "Admin123!",
    [string]$TenantCode = "DEFAULT" 
)

# Disable SSL validation for local testing
if (-not ([System.Management.Automation.PSTypeName]'ServerCertificateValidationCallback').Type) {
    $certCallback = @"
    using System;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    public class ServerCertificateValidationCallback
    {
        public static void Ignore()
        {
            if(ServicePointManager.ServerCertificateValidationCallback ==null)
            {
                ServicePointManager.ServerCertificateValidationCallback += 
                    delegate
                    (
                        Object obj, 
                        X509Certificate certificate, 
                        X509Chain chain, 
                        SslPolicyErrors errors
                    )
                    {
                        return true;
                    };
            }
        }
    }
"@
    Add-Type $certCallback
}
[ServerCertificateValidationCallback]::Ignore()

$body = @{
    Username   = $Username
    Password   = $Password
    TenantCode = $TenantCode
} | ConvertTo-Json

try {
    Write-Host "Attempting login to $BaseUrl..."
    $response = Invoke-RestMethod -Uri $BaseUrl -Method Post -Body $body -ContentType "application/json"
    
    if ($response.token) {
        Write-Host "Login successful!" -ForegroundColor Green
        return $response.token
    }
    else {
        Write-Host "Login failed: No token received." -ForegroundColor Red
        Write-Host $response
        exit 1
    }
}
catch {
    Write-Host "Login failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $errorBody = $reader.ReadToEnd()
        Write-Host "Error Details: $errorBody" -ForegroundColor Yellow
    }
    exit 1
}
