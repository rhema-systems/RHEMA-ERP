$baseUrl = "https://localhost:53484/api/finance/segments"
$tenantId = "00000000-0000-0000-0000-000000000001" # Using default tenant ID from seed data

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

# Get Token
Write-Host "Generating Token..." -ForegroundColor Cyan
try {
    # Run the C# app to get the token
    $output = dotnet run --project TestLoginApp
    
    # Parse output to find token between markers
    $token = $null
    $capture = $false
    foreach ($line in $output) {
        if ($line -eq "TOKEN_START") {
            $capture = $true
            continue
        }
        if ($line -eq "TOKEN_END") {
            $capture = $false
            break
        }
        if ($capture) {
            $token = $line
        }
    }

    if (-not $token) {
        Write-Host "Failed to get token from TestLoginApp. Output:" -ForegroundColor Red
        $output | ForEach-Object { Write-Host $_ }
        exit 1
    }
    Write-Host "Token generated successfully." -ForegroundColor Green
}
catch {
    Write-Host "Error generating token: $_" -ForegroundColor Red
    exit 1
}

function Test-Endpoint {
    param (
        [string]$Method,
        [string]$Url,
        [hashtable]$Body = $null,
        [string]$Description
    )

    Write-Host "Testing: $Description" -ForegroundColor Cyan
    try {
        $params = @{
            Uri         = $Url
            Method      = $Method
            ContentType = "application/json"
            Headers     = @{ 
                "X-Tenant-ID"   = $tenantId
                "Authorization" = "Bearer $token"
            }
        }
        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
        }

        $response = Invoke-RestMethod @params
        Write-Host "SUCCESS" -ForegroundColor Green
        return $response
    }
    catch {
        Write-Host "FAILED: $_" -ForegroundColor Red
        if ($_.Exception.Response) {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $errorBody = $reader.ReadToEnd()
            Write-Host "Response Body: $errorBody" -ForegroundColor Yellow
            $errorBody | Out-File "last_error.txt"
        }
    }
}

# 1. Create a new segment structure
$createBody = @{
    segmentName          = "Department"
    segmentCode          = "DEPT"
    segmentPosition      = 1
    segmentLength        = 3
    dataType             = "Alphanumeric"
    separatorCharacter   = "-"
    lookupTableRequired  = $true
    isMandatory          = $true
    isReportingDimension = $true
    isNaturalAccount     = $false
    isActive             = $true
    description          = "Department Segment"
}

$createdSegment = Test-Endpoint -Method "POST" -Url "$baseUrl" -Body $createBody -Description "Create Segment Structure"

if ($createdSegment) {
    $segmentId = $createdSegment.id
    Write-Host "Created Segment ID: $segmentId" -ForegroundColor Gray

    # 2. Get the created segment
    Test-Endpoint -Method "GET" -Url "$baseUrl/$segmentId" -Description "Get Segment by ID"

    # 3. Update the segment
    $updateBody = $createBody.Clone()
    $updateBody.description = "Updated Department Segment Description"
    $updateBody.id = $segmentId
    Test-Endpoint -Method "PUT" -Url "$baseUrl/$segmentId" -Body $updateBody -Description "Update Segment Structure"

    # 4. Get all segments
    Test-Endpoint -Method "GET" -Url "$baseUrl" -Description "Get All Segments"

    # 5. Delete the segment (cleanup)
    # Test-Endpoint -Method "DELETE" -Url "$baseUrl/$segmentId" -Description "Delete Segment Structure"
}
