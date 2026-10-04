-- Read-only Up precondition for linking Opportunity.CustomerId to a tenant-owned
-- Business Partner. Only a temporary table is written; application and migration
-- data are never changed.
SET NOCOUNT ON;

CREATE TABLE #Checks
(
    CheckName nvarchar(300) NOT NULL,
    AffectedRows bigint NOT NULL
);

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    SELECT CAST(N'CrmOpportunityCustomer.MigrationHistoryMissing' AS nvarchar(300)) AS CheckName,
           CAST(1 AS bigint) AS AffectedRows;
    RETURN;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.__EFMigrationsHistory
    WHERE MigrationId = N'20261004010000_LinkOpportunityCustomerToBusinessPartner'
)
BEGIN
    IF OBJECT_ID(N'dbo.Opportunities', N'U') IS NULL
        INSERT #Checks VALUES(N'CrmOpportunityCustomer.RequiredTableMissing:Opportunities', 1);
    IF OBJECT_ID(N'dbo.BusinessPartners', N'U') IS NULL
        INSERT #Checks VALUES(N'CrmOpportunityCustomer.RequiredTableMissing:BusinessPartners', 1);

    DECLARE @RequiredColumns TABLE
    (
        TableName sysname NOT NULL,
        ColumnName sysname NOT NULL,
        PRIMARY KEY (TableName, ColumnName)
    );
    INSERT @RequiredColumns VALUES
        (N'Opportunities', N'CustomerId'),
        (N'Opportunities', N'TenantId'),
        (N'BusinessPartners', N'Id'),
        (N'BusinessPartners', N'TenantId');

    INSERT #Checks
    SELECT N'CrmOpportunityCustomer.RequiredColumnMissing:' + TableName + N'.' + ColumnName, 1
    FROM @RequiredColumns
    WHERE COL_LENGTH(N'dbo.' + TableName, ColumnName) IS NULL;

    IF EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Opportunities')
          AND name = N'IX_Opportunities_CustomerId'
    )
        INSERT #Checks VALUES(N'CrmOpportunityCustomer.UnexpectedPendingSchema:IX_Opportunities_CustomerId', 1);

    IF EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.Opportunities')
          AND name = N'FK_Opportunities_BusinessPartners_CustomerId'
    )
        INSERT #Checks VALUES(N'CrmOpportunityCustomer.UnexpectedPendingSchema:FK_Opportunities_BusinessPartners_CustomerId', 1);

    IF NOT EXISTS (SELECT 1 FROM #Checks WHERE CheckName LIKE N'CrmOpportunityCustomer.Required%')
    BEGIN
        EXEC sys.sp_executesql N'
            INSERT #Checks
            SELECT N''CrmOpportunityCustomer.UnmatchedBusinessPartner'', COUNT_BIG(*)
            FROM dbo.Opportunities AS opportunity
            WHERE opportunity.CustomerId IS NOT NULL
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.BusinessPartners AS partner
                  WHERE partner.Id = opportunity.CustomerId
                    AND partner.TenantId = opportunity.TenantId
              )
            HAVING COUNT_BIG(*) > 0;';
    END;
END;

SELECT CheckName, AffectedRows
FROM #Checks
WHERE AffectedRows > 0
ORDER BY CheckName;

DROP TABLE #Checks;
