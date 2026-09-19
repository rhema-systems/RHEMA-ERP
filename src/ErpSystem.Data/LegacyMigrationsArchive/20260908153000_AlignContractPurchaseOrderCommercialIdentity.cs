using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Repairs already-installed contract capacity triggers without replacing RFQ
/// protections or changing commercial limits. Tender-backed approved lines use
/// description identity; subsequently selecting stock must not change that key.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260908153000_AlignContractPurchaseOrderCommercialIdentity")]
public sealed class AlignContractPurchaseOrderCommercialIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(RepairSql);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep this consistency repair on downgrade. Reinstating incompatible
        // keys would prevent valid contract drafts and subsequent transitions.
    }

    internal const string RepairSql = """
        DECLARE @targets TABLE (Name sysname);
        INSERT @targets VALUES (N'TR_PurchaseOrders_ApprovedCommercialCapacity'),
                               (N'TR_PurchaseOrderItems_ApprovedCommercialCapacity');
        DECLARE @name sysname;
        WHILE EXISTS (SELECT 1 FROM @targets)
        BEGIN
            SELECT TOP (1) @name = Name FROM @targets ORDER BY Name;
            DECLARE @definition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.' + @name, N'TR'));
            IF @definition IS NULL
                THROW 51983, 'Both purchase-order commercial capacity triggers must exist before contract identity repair.', 1;

            -- Bound the repair to the contract ACTUAL projection and grouping.
            -- RFQ identity branches and the authoritative approved join stay intact.
            DECLARE @anchor nvarchar(100) = N'purchaseOrder.ProcurementSourceId AS ContractId,';
            DECLARE @start int = CHARINDEX(@anchor, @definition);
            DECLARE @finish int = CHARINDEX(N') actual', @definition, @start);
            IF @start = 0 OR @finish <= @start OR CHARINDEX(@anchor, @definition, @start + 1) > 0
                THROW 51984, 'The contract commercial capacity projection has drifted from the verified baseline.', 1;
            DECLARE @actual nvarchar(max) = SUBSTRING(@definition, @start, @finish - @start);
            DECLARE @repaired nvarchar(max) = @actual;
            DECLARE @description nvarchar(200) = N'CONCAT(''description:'',LOWER(LTRIM(RTRIM(ISNULL(item.ItemDescription,'''')))))';
            DECLARE @old nvarchar(1000) = N'casewhenitem.inventoryitemidisnotnullthenconcat(''inventory:'',lower(convert(varchar(36),item.inventoryitemid)))elseconcat(''description:'',lower(ltrim(rtrim(isnull(item.itemdescription,'''')))))end';
            DECLARE @rfqOld nvarchar(1500) = N'casewhenitem.sourcerfqitemidisnotnullthendbo.fn_procurementrfqsourcelineidentity(item.tenantid,item.sourcerfqitemid)whenitem.inventoryitemidisnotnullthenconcat(''inventory:'',lower(convert(varchar(36),item.inventoryitemid)))elseconcat(''description:'',lower(ltrim(rtrim(isnull(item.itemdescription,'''')))))end';
            DECLARE @count int = 0;
            DECLARE @caseStart int = CHARINDEX(N'CASE', @repaired);
            WHILE @caseStart > 0
            BEGIN
                DECLARE @caseEnd int = CHARINDEX(N'END', @repaired, @caseStart);
                IF @caseEnd = 0
                    THROW 51984, 'The contract identity expression has drifted from the verified baseline.', 1;
                DECLARE @expression nvarchar(max) = SUBSTRING(@repaired, @caseStart, @caseEnd + 3 - @caseStart);
                DECLARE @compact nvarchar(max) = LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                    @expression, N'-- TDC0502_RFQ_ITEM_MASTER_LINEAGE', N''), CHAR(13), N''), CHAR(10), N''), CHAR(9), N''), N' ', N''));
                IF @compact COLLATE Latin1_General_100_BIN2 NOT IN (@old, @rfqOld)
                    THROW 51984, 'An unrecognized contract identity expression cannot be replaced safely.', 1;
                SET @repaired = STUFF(@repaired, @caseStart, LEN(@expression), @description);
                SET @count += 1;
                SET @caseStart = CHARINDEX(N'CASE', @repaired);
            END;
            IF @count NOT IN (0, 2)
                THROW 51984, 'Both contract projection and grouping identities must be consistent.', 1;

            SET @compact = LOWER(REPLACE(REPLACE(REPLACE(REPLACE(@repaired, CHAR(13), N''), CHAR(10), N''), CHAR(9), N''), N' ', N''));
            DECLARE @key nvarchar(200) = LOWER(@description);
            IF (LEN(@compact) - LEN(REPLACE(@compact, @key, N''))) / LEN(@key) <> 2
                OR CHARINDEX(N'item.inventoryitemid', @compact) > 0
                OR CHARINDEX(N'item.sourcerfqitemid', @compact) > 0
                OR CHARINDEX(N'purchaseOrder.ProcurementSourceType = 2', @repaired) = 0
                OR CHARINDEX(N'actual.Quantity > approved.Quantity', @definition) = 0
                OR CHARINDEX(N'actual.LineTotal > approved.LineTotal', @definition) = 0
                OR CHARINDEX(N'Contracts contract WITH (UPDLOCK, HOLDLOCK)', @definition) = 0
                THROW 51984, 'Contract capacity safeguards or source identity are not at the verified baseline.', 1;

            IF @count = 2
            BEGIN
                SET @definition = STUFF(@definition, @start, @finish - @start, @repaired);
                DECLARE @triggerPosition int = CHARINDEX(N'TRIGGER', UPPER(@definition));
                IF @triggerPosition = 0
                    THROW 51984, 'The commercial capacity trigger declaration cannot be altered safely.', 1;
                SET @definition = N'ALTER ' + SUBSTRING(@definition, @triggerPosition, LEN(@definition));
                EXEC sys.sp_executesql @definition;
            END;
            DELETE @targets WHERE Name = @name;
        END;
        """;
}
