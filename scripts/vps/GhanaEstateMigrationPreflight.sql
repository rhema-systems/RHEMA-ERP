-- Read-only Up preconditions for the Ghana statutory WHT FX evidence and
-- estate demarcation child-reference migrations. Only temporary tables are
-- written; application and migration data are never changed.
SET NOCOUNT ON;

CREATE TABLE #Checks
(
    CheckName nvarchar(300) NOT NULL,
    AffectedRows bigint NOT NULL
);

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    SELECT CAST(N'GhanaEstate.MigrationHistoryMissing' AS nvarchar(300)) AS CheckName,
           CAST(1 AS bigint) AS AffectedRows;
    RETURN;
END;

DECLARE @GhanaWhtApplied bit = 0;
DECLARE @EstateReferenceApplied bit = 0;
SET @GhanaWhtApplied = CASE WHEN EXISTS
(
    SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId = N'20260930000700_GhanaStatutoryWhtFxEvidence'
) THEN 1 ELSE 0 END;
SET @EstateReferenceApplied = CASE WHEN EXISTS
(
    SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId = N'20260930202402_UniqueEstateLandDemarcationChildReference'
) THEN 1 ELSE 0 END;

IF @GhanaWhtApplied = 0
BEGIN
    IF OBJECT_ID(N'dbo.VendorPaymentAllocation', N'U') IS NULL
        INSERT #Checks VALUES(N'GhanaWhtFx.RequiredTableMissing:VendorPaymentAllocation', 1);
    IF OBJECT_ID(N'dbo.ExchangeRates', N'U') IS NULL
        INSERT #Checks VALUES(N'GhanaWhtFx.RequiredTableMissing:ExchangeRates', 1);

    DECLARE @WhtRequiredColumns TABLE
    (
        TableName sysname NOT NULL,
        ColumnName sysname NOT NULL,
        PRIMARY KEY (TableName, ColumnName)
    );
    INSERT @WhtRequiredColumns VALUES
        (N'VendorPaymentAllocation', N'TenantId'),
        (N'ExchangeRates', N'Id');
    INSERT #Checks
    SELECT N'GhanaWhtFx.RequiredColumnMissing:' + TableName + N'.' + ColumnName, 1
    FROM @WhtRequiredColumns
    WHERE COL_LENGTH(N'dbo.' + TableName, ColumnName) IS NULL;

    DECLARE @WhtColumns TABLE (ColumnName sysname NOT NULL PRIMARY KEY);
    INSERT @WhtColumns VALUES
        (N'WithholdingTaxStatutoryExchangeRateId'),
        (N'WithholdingTaxStatutoryExchangeRate'),
        (N'WithholdingTaxStatutoryExchangeRateDate'),
        (N'WithholdingTaxStatutoryExchangeRateSource'),
        (N'WithholdingTaxStatutoryExchangeRateReference');

    INSERT #Checks
    SELECT N'GhanaWhtFx.UnexpectedPendingSchema:VendorPaymentAllocation.' + ColumnName, 1
    FROM @WhtColumns
    WHERE COL_LENGTH(N'dbo.VendorPaymentAllocation', ColumnName) IS NOT NULL;

    IF EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.VendorPaymentAllocation')
          AND name = N'IX_VendorPaymentAllocation_TenantId_WithholdingTaxStatutoryExchangeRateId'
    )
        INSERT #Checks VALUES(
            N'GhanaWhtFx.UnexpectedPendingSchema:IX_VendorPaymentAllocation_TenantId_WithholdingTaxStatutoryExchangeRateId', 1);
    IF EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.VendorPaymentAllocation')
          AND name = N'FK_VendorPaymentAllocation_ExchangeRates_WithholdingTaxStatutoryExchangeRateId'
    )
        INSERT #Checks VALUES(
            N'GhanaWhtFx.UnexpectedPendingSchema:FK_VendorPaymentAllocation_ExchangeRates_WithholdingTaxStatutoryExchangeRateId', 1);
END;

IF @EstateReferenceApplied = 0
BEGIN
    IF OBJECT_ID(N'dbo.EstateLandDemarcations', N'U') IS NULL
        INSERT #Checks VALUES(N'EstateLandReference.RequiredTableMissing:EstateLandDemarcations', 1);
    IF OBJECT_ID(N'dbo.EstateManagedAssets', N'U') IS NULL
        INSERT #Checks VALUES(N'EstateLandReference.RequiredTableMissing:EstateManagedAssets', 1);

    DECLARE @EstateColumns TABLE (TableName sysname NOT NULL, ColumnName sysname NOT NULL);
    INSERT @EstateColumns VALUES
        (N'EstateLandDemarcations', N'Id'),
        (N'EstateLandDemarcations', N'TenantId'),
        (N'EstateLandDemarcations', N'EstateManagedAssetId'),
        (N'EstateLandDemarcations', N'ParentDemarcationId'),
        (N'EstateLandDemarcations', N'DemarcationNumber'),
        (N'EstateLandDemarcations', N'ChildFixedAssetReference'),
        (N'EstateManagedAssets', N'Id'),
        (N'EstateManagedAssets', N'AssetCode');

    INSERT #Checks
    SELECT N'EstateLandReference.RequiredColumnMissing:' + TableName + N'.' + ColumnName, 1
    FROM @EstateColumns
    WHERE COL_LENGTH(N'dbo.' + TableName, ColumnName) IS NULL;

    IF EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.EstateLandDemarcations')
          AND name = N'IX_EstateLandDemarcations_TenantId_ChildFixedAssetReference'
    )
        INSERT #Checks VALUES(
            N'EstateLandReference.UnexpectedPendingSchema:IX_EstateLandDemarcations_TenantId_ChildFixedAssetReference', 1);

    IF NOT EXISTS (SELECT 1 FROM #Checks WHERE CheckName LIKE N'EstateLandReference.Required%')
    BEGIN
        EXEC sys.sp_executesql N'
            CREATE TABLE #ProjectedReferences
            (
                Id uniqueidentifier NOT NULL PRIMARY KEY,
                TenantId uniqueidentifier NOT NULL,
                ProjectedReference nvarchar(max) NULL
            );

            ;WITH Projection AS
            (
                SELECT
                    parcel.Id,
                    parcel.TenantId,
                    CAST(CASE
                        WHEN NULLIF(LTRIM(RTRIM(parcel.ChildFixedAssetReference)), N'''') IS NOT NULL
                            THEN parcel.ChildFixedAssetReference
                        ELSE CONCAT(asset.AssetCode, N''-D'',
                            FORMAT(parcel.DemarcationNumber, N''000'', N''en-US''))
                    END AS nvarchar(max)) AS ProjectedReference
                FROM dbo.EstateLandDemarcations AS parcel
                LEFT JOIN dbo.EstateManagedAssets AS asset
                    ON asset.Id = parcel.EstateManagedAssetId
                WHERE NULLIF(LTRIM(RTRIM(parcel.ChildFixedAssetReference)), N'''') IS NOT NULL
                   OR (parcel.ParentDemarcationId IS NULL AND asset.Id IS NOT NULL)

                UNION ALL

                SELECT
                    child.Id,
                    child.TenantId,
                    CAST(CONCAT(parent.ProjectedReference, N''-D'',
                        FORMAT(child.DemarcationNumber, N''000'', N''en-US'')) AS nvarchar(max))
                FROM dbo.EstateLandDemarcations AS child
                INNER JOIN Projection AS parent
                    ON parent.Id = child.ParentDemarcationId
                WHERE NULLIF(LTRIM(RTRIM(child.ChildFixedAssetReference)), N'''') IS NULL
                  AND NULLIF(LTRIM(RTRIM(parent.ProjectedReference)), N'''') IS NOT NULL
            )
            INSERT #ProjectedReferences (Id, TenantId, ProjectedReference)
            SELECT Id, TenantId, ProjectedReference
            FROM Projection
            OPTION (MAXRECURSION 0);

            INSERT #Checks
            SELECT N''EstateLandReference.UnresolvedAfterBackfill'', COUNT_BIG(*)
            FROM dbo.EstateLandDemarcations AS parcel
            LEFT JOIN #ProjectedReferences AS projected ON projected.Id = parcel.Id
            WHERE projected.Id IS NULL
               OR NULLIF(LTRIM(RTRIM(projected.ProjectedReference)), N'''') IS NULL
            HAVING COUNT_BIG(*) > 0;

            INSERT #Checks
            SELECT N''EstateLandReference.ProjectedValueTooLong'', COUNT_BIG(*)
            FROM #ProjectedReferences
            WHERE DATALENGTH(ProjectedReference) > 240
            HAVING COUNT_BIG(*) > 0;

            INSERT #Checks
            SELECT N''EstateLandReference.DuplicateProjectedReference'', SUM(DuplicateCount)
            FROM
            (
                SELECT COUNT_BIG(*) AS DuplicateCount
                FROM #ProjectedReferences
                GROUP BY TenantId, ProjectedReference
                HAVING COUNT_BIG(*) > 1
            ) AS duplicates
            HAVING SUM(DuplicateCount) > 0;

            DROP TABLE #ProjectedReferences;';
    END;
END;

SELECT CheckName, AffectedRows
FROM #Checks
WHERE AffectedRows > 0
ORDER BY CheckName;

DROP TABLE #Checks;
