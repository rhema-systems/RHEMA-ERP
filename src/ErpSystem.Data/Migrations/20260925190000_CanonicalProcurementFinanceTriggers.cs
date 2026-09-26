using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Rebinds retained Procurement and QS guards after Finance's canonical partner cutover.
/// The baseline intentionally retains its original columns; this repair runs after the
/// canonical renames and before any later migration updates the protected AP tables.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260925190000_CanonicalProcurementFinanceTriggers")]
public sealed class CanonicalProcurementFinanceTriggers : Migration
{
    public const string RepairSql = """
        IF COL_LENGTH(N'dbo.VendorInvoice', N'BusinessPartnerId') IS NULL
           OR COL_LENGTH(N'dbo.VendorInvoice', N'SupplierId') IS NOT NULL
           OR COL_LENGTH(N'dbo.VendorPayment', N'BusinessPartnerId') IS NULL
           OR COL_LENGTH(N'dbo.VendorPayment', N'SupplierId') IS NOT NULL
           OR COL_LENGTH(N'dbo.SupplierDebitNotes', N'VendorId') IS NULL
           OR COL_LENGTH(N'dbo.SupplierDebitNotes', N'BusinessPartnerRoleId') IS NULL
           OR COL_LENGTH(N'dbo.SupplierDebitNotes', N'SupplierId') IS NOT NULL
           OR OBJECT_ID(N'dbo.BusinessPartners', N'U') IS NULL
            THROW 51949, 'CANONICAL_PROCUREMENT_TRIGGER_REPAIR_REQUIRED: canonical Finance predecessor schema is missing.', 1;

        DECLARE @modules TABLE (Sequence int PRIMARY KEY, TriggerName sysname NOT NULL, TableName sysname NOT NULL);
        INSERT @modules VALUES
            (1, N'TR_VendorInvoice_TDC0504MandatoryMatch', N'VendorInvoice'),
            (2, N'TR_VendorPayment_OptionalApproval', N'VendorPayment'),
            (3, N'TR_InventorySupplierReturnPostings_Immutable', N'InventorySupplierReturnPostings'),
            (4, N'TR_SupplierDebitNotes_InventoryReturnCreditGuard', N'SupplierDebitNotes'),
            (5, N'TR_QsAdvanceRecoveryAgreements_QS0505Guard', N'QuantitySurveyAdvanceRecoveryAgreements');

        DECLARE @repairs TABLE (Sequence int PRIMARY KEY, ModuleSequence int NOT NULL, OldText nvarchar(1000) NOT NULL, NewText nvarchar(1000) NOT NULL, ExpectedCount int NOT NULL);
        INSERT @repairs VALUES
            (1, 1, N'currentRow.SupplierId <> priorRow.SupplierId', N'currentRow.BusinessPartnerId <> priorRow.BusinessPartnerId', 2),
            (2, 2, N'i.SupplierId<>d.SupplierId', N'i.BusinessPartnerId<>d.BusinessPartnerId', 1),
            (3, 3,
             N'LEFT JOIN dbo.ApSupplierIdentityLinks s ON s.TenantId=i.TenantId AND s.BusinessPartnerId=r.SupplierId AND s.SupplierId=v.SupplierId AND s.IsDeleted=0',
             N'LEFT JOIN dbo.BusinessPartners s ON s.TenantId=i.TenantId AND s.Id=r.SupplierId AND s.Id=v.BusinessPartnerId AND s.IsDeleted=0', 1),
            (4, 4, N'v.SupplierId<>i.SupplierId', N'v.BusinessPartnerId<>i.VendorId', 1),
            (5, 5, N'LEFT JOIN [dbo].[Suppliers] s', N'LEFT JOIN [dbo].[BusinessPartners] s', 1),
            (6, 5, N'vp.[SupplierId]', N'vp.[BusinessPartnerId]', 1);

        -- Validate every module first. Only allow exact retained predicates or their
        -- already-canonical replacements; an unknown local definition requires review.
        DECLARE @prepared TABLE (Sequence int PRIMARY KEY, Definition nvarchar(max) NOT NULL, Changed bit NOT NULL);
        DECLARE @moduleSequence int = 1, @triggerName sysname, @tableName sysname,
            @definition nvarchar(max), @original nvarchar(max), @objectId int,
            @repairSequence int, @old nvarchar(1000), @new nvarchar(1000), @expected int,
            @oldCount int, @newCount int, @triggerKeyword int, @header nvarchar(max), @changed bit;
        WHILE @moduleSequence <= 5
        BEGIN
            SELECT @triggerName=TriggerName, @tableName=TableName FROM @modules WHERE Sequence=@moduleSequence;
            SET @objectId=OBJECT_ID(N'dbo.' + QUOTENAME(@triggerName), N'TR');
            SET @definition=NULL;
            SELECT @definition=m.definition FROM sys.sql_modules m JOIN sys.triggers t ON t.object_id=m.object_id
            WHERE m.object_id=@objectId AND t.parent_id=OBJECT_ID(N'dbo.' + QUOTENAME(@tableName), N'U')
              AND t.is_disabled=0 AND t.is_instead_of_trigger=0;
            IF @definition IS NULL
                THROW 51949, 'CANONICAL_PROCUREMENT_TRIGGER_REPAIR_REQUIRED: expected enabled guard trigger is missing or has an unsupported definition.', 1;
            -- SQL Server persists CREATE OR ALTER as CREATE (including replacement
            -- whitespace). Recognize only a supported leading DDL header, not arbitrary
            -- occurrences in the body, and preserve already-canonical module text.
            SET @triggerKeyword=CHARINDEX(N'TRIGGER',@definition COLLATE Latin1_General_100_BIN2);
            IF @triggerKeyword=0
                THROW 51949, 'CANONICAL_PROCUREMENT_TRIGGER_REPAIR_REQUIRED: expected trigger DDL header is missing.', 1;
            SET @header=REPLACE(REPLACE(REPLACE(REPLACE(SUBSTRING(@definition,1,@triggerKeyword-1),N' ',N''),NCHAR(9),N''),NCHAR(13),N''),NCHAR(10),N'');
            IF @header COLLATE Latin1_General_100_BIN2 NOT IN (N'CREATE',N'ALTER',N'CREATEORALTER')
                THROW 51949, 'CANONICAL_PROCUREMENT_TRIGGER_REPAIR_REQUIRED: unsupported trigger DDL header requires review.', 1;
            SET @original=@definition;
            SET @repairSequence=0;
            WHILE EXISTS (SELECT 1 FROM @repairs WHERE ModuleSequence=@moduleSequence AND Sequence>@repairSequence)
            BEGIN
                SELECT TOP (1) @repairSequence=Sequence, @old=OldText, @new=NewText, @expected=ExpectedCount
                FROM @repairs WHERE ModuleSequence=@moduleSequence AND Sequence>@repairSequence ORDER BY Sequence;
                SET @oldCount=(DATALENGTH(@definition)-DATALENGTH(REPLACE(@definition COLLATE Latin1_General_100_BIN2,@old,N'')))/DATALENGTH(@old);
                SET @newCount=(DATALENGTH(@definition)-DATALENGTH(REPLACE(@definition COLLATE Latin1_General_100_BIN2,@new,N'')))/DATALENGTH(@new);
                IF NOT ((@oldCount=@expected AND @newCount=0) OR (@oldCount=0 AND @newCount=@expected))
                    THROW 51949, 'CANONICAL_PROCUREMENT_TRIGGER_REPAIR_REQUIRED: retained identity predicate does not match the reviewed legacy or canonical form.', 1;
                IF @oldCount=@expected SET @definition=REPLACE(@definition COLLATE Latin1_General_100_BIN2,@old,@new);
            END;

            IF @moduleSequence=5
            BEGIN
                -- Replace only the reviewed QS identity fallback. Supplier names and codes
                -- cannot establish identity once payment counterparties are canonical.
                DECLARE @whereStart int=CHARINDEX(N'WHERE p.[Id] IS NULL OR c.[Id] IS NULL OR bp.[Id] IS NULL OR vp.[Id] IS NULL OR s.[Id] IS NULL',@definition COLLATE Latin1_General_100_BIN2);
                DECLARE @predicateStart int=CHARINDEX(N'OR NOT',@definition COLLATE Latin1_General_100_BIN2,@whereStart);
                DECLARE @predicateEnd int=CHARINDEX(N'OR UPPER(vp.[CurrencyCode])',@definition COLLATE Latin1_General_100_BIN2,@predicateStart);
                IF @whereStart=0 OR @predicateStart=0 OR @predicateEnd<=@predicateStart
                    THROW 51949, 'CANONICAL_PROCUREMENT_TRIGGER_REPAIR_REQUIRED: QS counterparty predicate boundaries are missing.', 1;
                DECLARE @predicate nvarchar(max)=SUBSTRING(@definition,@predicateStart,@predicateEnd-@predicateStart);
                DECLARE @compact nvarchar(max)=REPLACE(REPLACE(REPLACE(REPLACE(@predicate,N' ',N''),NCHAR(9),N''),NCHAR(13),N''),NCHAR(10),N'');
                DECLARE @legacyPredicate nvarchar(max)=N'ORNOT(s.[Id]=bp.[Id]OR(NULLIF(LTRIM(RTRIM(s.[SupplierCode])),'''')ISNOTNULLANDs.[SupplierCode]=bp.[PartnerCode])OR(NULLIF(LTRIM(RTRIM(s.[Name])),'''')ISNOTNULLANDs.[Name]=bp.[PartnerName]))';
                IF @compact COLLATE Latin1_General_100_BIN2=@legacyPredicate COLLATE Latin1_General_100_BIN2
                    SET @definition=STUFF(@definition,@predicateStart,@predicateEnd-@predicateStart,N'OR NOT (s.[Id] = bp.[Id])' + NCHAR(13) + NCHAR(10) + N'           ');
                ELSE IF @compact COLLATE Latin1_General_100_BIN2<>N'ORNOT(s.[Id]=bp.[Id])' COLLATE Latin1_General_100_BIN2
                    THROW 51949, 'CANONICAL_PROCUREMENT_TRIGGER_REPAIR_REQUIRED: QS counterparty predicate differs from its reviewed form.', 1;
            END;

            SET @changed=CASE WHEN @definition COLLATE Latin1_General_100_BIN2=@original COLLATE Latin1_General_100_BIN2 THEN 0 ELSE 1 END;
            IF @changed=1
                SET @definition=STUFF(@definition,1,@triggerKeyword+LEN(N'TRIGGER')-1,N'CREATE OR ALTER TRIGGER');
            INSERT @prepared VALUES (@moduleSequence,@definition,@changed);
            SET @moduleSequence+=1;
        END;

        SET @moduleSequence=1;
        WHILE @moduleSequence<=5
        BEGIN
            IF EXISTS (SELECT 1 FROM @prepared WHERE Sequence=@moduleSequence AND Changed=1)
            BEGIN
                SELECT @definition=Definition FROM @prepared WHERE Sequence=@moduleSequence;
                EXEC sys.sp_executesql @definition;
            END;
            SET @moduleSequence+=1;
        END;
        """;

    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(RepairSql);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        THROW 51949, 'CANONICAL_PROCUREMENT_TRIGGER_REPAIR_REQUIRED: independent rollback cannot restore retired supplier identity guards; use an explicitly reviewed Finance cutover recovery.', 1;
        """);
}
