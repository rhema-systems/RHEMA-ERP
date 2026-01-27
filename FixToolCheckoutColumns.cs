using Microsoft.EntityFrameworkCore;
using ErpSystem.Data;

var connectionString = "Server=rhema-michael\\sql2017;Database=rhemaerp;User Id=sa;Password=password@123;TrustServerCertificate=True;";

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer(connectionString)
    .Options;

using (var context = new ApplicationDbContext(options))
{
    Console.WriteLine("Attempting to add missing columns to ToolCheckouts...");
    
    try
    {
        // Execute raw SQL to add columns
        var sql = @"
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
";
        
        context.Database.ExecuteSqlRaw(sql);
        Console.WriteLine("✓ Columns added successfully!");
        
        // Verify columns
        var verifySql = @"
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'ToolCheckouts' 
ORDER BY COLUMN_NAME";
        
        var columns = context.Database.SqlQueryRaw<dynamic>(verifySql).ToList();
        Console.WriteLine("\nToolCheckouts columns:");
        foreach (var col in columns)
        {
            Console.WriteLine($"  - {col}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Error: {ex.Message}");
        Console.WriteLine(ex.InnerException?.Message);
    }
}
