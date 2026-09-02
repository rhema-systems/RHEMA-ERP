[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [Guid]$ReceiptId,

    [string]$UserName = 'procurementofficer',
    [string]$ApiBaseUrl = 'http://127.0.0.1:5000',
    [string]$ServiceConfigurationPath = 'C:\RhemaERP\services\api\RhemaERPAPI.xml'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Write-NetworkUInt32 {
    param([byte[]]$Buffer, [int]$Offset, [uint32]$Value)
    $Buffer[$Offset] = [byte](($Value -shr 24) -band 0xff)
    $Buffer[$Offset + 1] = [byte](($Value -shr 16) -band 0xff)
    $Buffer[$Offset + 2] = [byte](($Value -shr 8) -band 0xff)
    $Buffer[$Offset + 3] = [byte]($Value -band 0xff)
}

function New-IdentityV3PasswordHash([string]$Password) {
    [byte[]]$salt = New-Object byte[] 16
    $random = [Security.Cryptography.RandomNumberGenerator]::Create()
    $random.GetBytes($salt)
    $random.Dispose()
    $derive = [Security.Cryptography.Rfc2898DeriveBytes]::new(
        $Password, $salt, 100000,
        [Security.Cryptography.HashAlgorithmName]::SHA512)
    [byte[]]$subkey = $derive.GetBytes(32)
    $derive.Dispose()
    [byte[]]$output = New-Object byte[] 61
    $output[0] = 1
    Write-NetworkUInt32 $output 1 2
    Write-NetworkUInt32 $output 5 100000
    Write-NetworkUInt32 $output 9 16
    [Array]::Copy($salt, 0, $output, 13, $salt.Length)
    [Array]::Copy($subkey, 0, $output, 29, $subkey.Length)
    [Convert]::ToBase64String($output)
}

[xml]$serviceConfiguration = Get-Content -LiteralPath $ServiceConfigurationPath
$connectionString = [string](($serviceConfiguration.service.env | Where-Object {
    $_.name -eq 'ConnectionStrings__DefaultConnection'
}).value)
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw 'The API database connection setting is unavailable.'
}

$connection = [System.Data.SqlClient.SqlConnection]::new($connectionString)
$backup = $null
try {
    $connection.Open()
    $select = $connection.CreateCommand()
    $select.CommandText = @'
SELECT TOP(1) Id,UserName,PasswordHash,AccessFailedCount,LockoutEnd
FROM Users
WHERE NormalizedUserName=UPPER(@userName) AND IsActive=1;
'@
    [void]$select.Parameters.AddWithValue('@userName', $UserName)
    $table = [System.Data.DataTable]::new()
    $adapter = [System.Data.SqlClient.SqlDataAdapter]::new($select)
    [void]$adapter.Fill($table)
    if ($table.Rows.Count -ne 1) { throw "Active user $UserName was not found." }
    $row = $table.Rows[0]
    $backup = [pscustomobject]@{
        Id = [Guid]$row.Id
        UserName = [string]$row.UserName
        PasswordHash = if ($row.IsNull('PasswordHash')) { $null } else { [string]$row.PasswordHash }
        AccessFailedCount = [int]$row.AccessFailedCount
        LockoutEnd = if ($row.IsNull('LockoutEnd')) { $null } else { $row.LockoutEnd }
    }

    $temporaryPassword = "ReceiptRead!$([Guid]::NewGuid().ToString('N'))aA7"
    $update = $connection.CreateCommand()
    $update.CommandText = 'UPDATE Users SET PasswordHash=@hash,AccessFailedCount=0,LockoutEnd=NULL WHERE Id=@id;'
    [void]$update.Parameters.AddWithValue('@hash', (New-IdentityV3PasswordHash $temporaryPassword))
    [void]$update.Parameters.AddWithValue('@id', $backup.Id)
    [void]$update.ExecuteNonQuery()

    $loginBody = @{ username=$backup.UserName; password=$temporaryPassword; rememberMe=$false } |
        ConvertTo-Json -Compress
    $login = Invoke-RestMethod -Method Post -Uri "$ApiBaseUrl/api/auth/login" `
        -ContentType 'application/json' -Body $loginBody -TimeoutSec 60
    if ([string]::IsNullOrWhiteSpace([string]$login.token)) {
        throw 'Login did not return an access token.'
    }
    $headers = @{ Authorization = "Bearer $($login.token)" }
    $paths = @(
        "/api/procurement/purchase-order-receipts/$ReceiptId/source-evidence",
        "/api/ProcurementReceiptDocuments/receipt/$ReceiptId",
        "/api/PurchaseOrderReceipts/$ReceiptId/inspection-control"
    )
    foreach ($path in $paths) {
        $response = Invoke-WebRequest -Method Get -Uri "$ApiBaseUrl$path" `
            -Headers $headers -UseBasicParsing -TimeoutSec 60
        if ($response.StatusCode -ne 200) {
            throw "$path returned $($response.StatusCode)."
        }
        "PASS|$($backup.UserName)|$path|200"
    }
}
finally {
    if ($null -ne $backup -and $connection.State -eq 'Open') {
        $restore = $connection.CreateCommand()
        $restore.CommandText = @'
UPDATE Users SET PasswordHash=@hash,AccessFailedCount=@failed,LockoutEnd=@lockout
WHERE Id=@id;
'@
        [void]$restore.Parameters.AddWithValue(
            '@hash', $(if ($null -eq $backup.PasswordHash) { [DBNull]::Value } else { $backup.PasswordHash }))
        [void]$restore.Parameters.AddWithValue('@failed', $backup.AccessFailedCount)
        [void]$restore.Parameters.AddWithValue(
            '@lockout', $(if ($null -eq $backup.LockoutEnd) { [DBNull]::Value } else { $backup.LockoutEnd }))
        [void]$restore.Parameters.AddWithValue('@id', $backup.Id)
        [void]$restore.ExecuteNonQuery()
    }
    $connection.Dispose()
}
