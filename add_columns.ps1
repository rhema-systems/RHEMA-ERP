[System.Reflection.Assembly]::LoadWithPartialName("Microsoft.SqlServer.SMO") | out-null

$serverName = "rhema-michael\sql2017"
$databaseName = "rhemaerp"
$userName = "sa"
$password = "password@123"

try {
    $server = New-Object Microsoft.SqlServer.Management.Smo.Server("$serverName")
    $server.ConnectionContext.LoginSecure = $false
    $server.ConnectionContext.Login = $userName
    $server.ConnectionContext.Password = $password
    $db = $server.Databases[$databaseName]
    
    Write-Host "Connected to database: $databaseName"
    
    # SQL to add columns
    $sql = @"
IF COL_LENGTH('dbo.ToolCheckouts', 'CheckoutDate') IS NULL
    ALTER TABLE [ToolCheckouts] ADD [CheckoutDate] [datetime2] NOT NULL DEFAULT GETUTCDATE();

IF COL_LENGTH('dbo.ToolCheckouts', 'ExpectedReturnDate') IS NULL
    ALTER TABLE [ToolCheckouts] ADD [ExpectedReturnDate] [datetime2] NULL;

IF COL_LENGTH('dbo.ToolCheckouts', 'ActualReturnDate') IS NULL
    ALTER TABLE [ToolCheckouts] ADD [ActualReturnDate] [datetime2] NULL;

IF COL_LENGTH('dbo.ToolCheckouts', 'Status') IS NULL
    ALTER TABLE [ToolCheckouts] ADD [Status] [nvarchar](20) NOT NULL DEFAULT 'CheckedOut';

IF COL_LENGTH('dbo.ToolCheckouts', 'CheckoutNotes') IS NULL
    ALTER TABLE [ToolCheckouts] ADD [CheckoutNotes] [nvarchar](1000) NULL;

IF COL_LENGTH('dbo.ToolCheckouts', 'ReturnNotes') IS NULL
    ALTER TABLE [ToolCheckouts] ADD [ReturnNotes] [nvarchar](1000) NULL;

IF COL_LENGTH('dbo.ToolCheckouts', 'ConditionOnCheckout') IS NULL
    ALTER TABLE [ToolCheckouts] ADD [ConditionOnCheckout] [nvarchar](20) NULL;

IF COL_LENGTH('dbo.ToolCheckouts', 'ConditionOnReturn') IS NULL
    ALTER TABLE [ToolCheckouts] ADD [ConditionOnReturn] [nvarchar](20) NULL;

IF COL_LENGTH('dbo.ToolCheckouts', 'DamageReported') IS NULL
    ALTER TABLE [ToolCheckouts] ADD [DamageReported] [bit] NOT NULL DEFAULT CAST(0 AS bit);

IF COL_LENGTH('dbo.ToolCheckouts', 'DamageDescription') IS NULL
    ALTER TABLE [ToolCheckouts] ADD [DamageDescription] [nvarchar](2000) NULL;

IF COL_LENGTH('dbo.ToolCheckouts', 'DamageCost') IS NULL
    ALTER TABLE [ToolCheckouts] ADD [DamageCost] [decimal](18, 2) NULL;

IF COL_LENGTH('dbo.ToolCheckouts', 'CreatedById') IS NULL
    ALTER TABLE [ToolCheckouts] ADD [CreatedById] [uniqueidentifier] NULL;

IF COL_LENGTH('dbo.ToolCheckouts', 'LastModifiedById') IS NULL
    ALTER TABLE [ToolCheckouts] ADD [LastModifiedById] [uniqueidentifier] NULL;

IF COL_LENGTH('dbo.ToolCheckouts', 'DeletedAt') IS NULL
    ALTER TABLE [ToolCheckouts] ADD [DeletedAt] [datetime2] NULL;

IF COL_LENGTH('dbo.ToolCheckouts', 'DeletedBy') IS NULL
    ALTER TABLE [ToolCheckouts] ADD [DeletedBy] [nvarchar](max) NULL;
"@
    
    $db.ExecuteNonQuery($sql)
    Write-Host "Columns added successfully"
    
    # Verify columns exist
    $verifySql = "SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ToolCheckouts' ORDER BY COLUMN_NAME"
    $dt = $db.ExecuteWithResults($verifySql).Tables[0]
    
    Write-Host "Columns in ToolCheckouts table:"
    $dt | Format-Table -AutoSize
    
} catch {
    Write-Host "Error: $_"
    Write-Host $_.Exception.Message
}
