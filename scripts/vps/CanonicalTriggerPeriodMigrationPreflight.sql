-- Read-only catalog preflight; only table variables and local text are changed.
-- Guard predicates are copied from the two reviewed migrations. Their normalized
-- source hashes in CanonicalMigrationPreflight.json make changed migrations fail closed.
-- Future canonical renames are accepted only when their predecessor is pending;
-- independent Finance data probes must also pass before deployment can continue.
SET NOCOUNT ON;
DECLARE @R TABLE(CheckName nvarchar(1000),AffectedRows bigint);
DECLARE @Applied TABLE(MigrationId nvarchar(150) PRIMARY KEY);
IF OBJECT_ID(N'dbo.__EFMigrationsHistory',N'U') IS NULL
BEGIN
    SELECT N'CanonicalTriggerPeriod.HistoryMissing' CheckName,CAST(1 AS bigint) AffectedRows;
    RETURN;
END;
INSERT @Applied EXEC sys.sp_executesql N'SELECT MigrationId FROM dbo.__EFMigrationsHistory';
IF NOT EXISTS(SELECT 1 FROM @Applied WHERE MigrationId=N'20260925190000_CanonicalProcurementFinanceTriggers')
BEGIN TRY
    IF OBJECT_ID(N'dbo.BusinessPartners',N'U') IS NULL OR COL_LENGTH(N'dbo.SupplierDebitNotes',N'VendorId') IS NULL
        THROW 51949,'Canonical trigger repair predecessor table or VendorId is missing.',1;
    IF EXISTS(SELECT 1 FROM @Applied WHERE MigrationId=N'20260924045510_CanonicalVendorInvoiceBusinessPartnerIdentity')
    BEGIN
        IF COL_LENGTH(N'dbo.VendorInvoice',N'BusinessPartnerId') IS NULL OR COL_LENGTH(N'dbo.VendorInvoice',N'SupplierId') IS NOT NULL
            THROW 51949,'Applied canonical invoice predecessor schema is inconsistent.',1;
    END
    ELSE IF COL_LENGTH(N'dbo.VendorInvoice',N'SupplierId') IS NULL
        THROW 51949,'Pending canonical invoice predecessor has no legacy identity to rename.',1;
    IF EXISTS(SELECT 1 FROM @Applied WHERE MigrationId=N'20260924095137_CanonicalApSettlementBusinessPartnerIdentity')
    BEGIN
        IF COL_LENGTH(N'dbo.VendorPayment',N'BusinessPartnerId') IS NULL OR COL_LENGTH(N'dbo.VendorPayment',N'SupplierId') IS NOT NULL
           OR COL_LENGTH(N'dbo.SupplierDebitNotes',N'BusinessPartnerRoleId') IS NULL OR COL_LENGTH(N'dbo.SupplierDebitNotes',N'SupplierId') IS NOT NULL
            THROW 51949,'Applied canonical settlement predecessor schema is inconsistent.',1;
    END
    ELSE IF COL_LENGTH(N'dbo.VendorPayment',N'SupplierId') IS NULL OR COL_LENGTH(N'dbo.SupplierDebitNotes',N'SupplierId') IS NULL
        THROW 51949,'Pending canonical settlement predecessor has no legacy identity to rename.',1;
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


END TRY
BEGIN CATCH
    INSERT @R VALUES(N'CanonicalTriggerRepair:'+ERROR_MESSAGE(),1);
END CATCH;
IF NOT EXISTS(SELECT 1 FROM @Applied WHERE MigrationId=N'20260925210000_ReconcileAccountingBookPeriodInitializationSchema')
BEGIN TRY
    DECLARE @C4TableCount int=(SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(N'dbo') AND name IN(N'AccountingBookInitializations',N'AccountingBookPeriods',N'AccountingBookInitializationLines'));
    IF @C4TableCount NOT IN(0,3)
        THROW 51000,'C4_RECONCILE_PARTIAL_SCHEMA: incomplete authority schema requires explicit repair.',1;
    DECLARE @TranslationApplied bit=CASE WHEN EXISTS(SELECT 1 FROM @Applied WHERE MigrationId=N'20260921174134_AccountingBookTranslationEvidence') THEN 1 ELSE 0 END;
    IF @C4TableCount=0 AND @TranslationApplied=0
        THROW 51000,'C4_TRANSLATION_PREDECESSOR: pending translation migration requires the three baseline authority tables.',1;
    IF OBJECT_ID(N'dbo.AccountingBooks',N'U') IS NULL OR OBJECT_ID(N'dbo.FiscalPeriods',N'U') IS NULL
       OR OBJECT_ID(N'dbo.Accounts',N'U') IS NULL OR OBJECT_ID(N'dbo.Tenants',N'U') IS NULL OR OBJECT_ID(N'dbo.ExchangeRates',N'U') IS NULL
        THROW 51000,'C4_RECONCILE_PREDECESSOR: required Finance tables are missing.',1;
    -- With zero tables Up creates the full C4 schema. With all three present it
    -- repairs nothing: check every existing column/key/FK/check exactly as Up does.
    IF @C4TableCount=3
    BEGIN
DECLARE @ExpectedColumns TABLE (TableName sysname, ColumnName sysname, TypeName sysname, MaxLength smallint NULL, [Precision] tinyint NULL, Scale tinyint NULL, IsNullable bit);
        INSERT @ExpectedColumns VALUES
        (N'AccountingBookInitializations',N'Id',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'AccountingBookId',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'Version',N'int',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'SupersedesInitializationId',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'Mode',N'int',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'TranslationMethod',N'int',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'InitializationStatus',N'int',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'CutoffDate',N'datetime2',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'CutoffFiscalPeriodId',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'SourceAccountingBookId',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'IdempotencyKey',N'nvarchar',200,NULL,NULL,0),
        (N'AccountingBookInitializations',N'Reason',N'nvarchar',1000,NULL,NULL,0),
        (N'AccountingBookInitializations',N'TotalDebits',N'decimal',NULL,18,2,0),
        (N'AccountingBookInitializations',N'TotalCredits',N'decimal',NULL,18,2,0),
        (N'AccountingBookInitializations',N'RequiredAccountCount',N'int',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'CoveredAccountCount',N'int',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'EvidenceFingerprint',N'nvarchar',128,NULL,NULL,0),
        (N'AccountingBookInitializations',N'ReconciliationFingerprint',N'nvarchar',128,NULL,NULL,0),
        (N'AccountingBookInitializations',N'PreparedByUserId',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'PreparedAtUtc',N'datetime2',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'WorkflowInstanceId',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'ApprovedByUserId',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'ApprovedAtUtc',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'RejectedByUserId',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'RejectedAtUtc',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'DecidedByUserId',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'DecidedAtUtc',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'DecisionReason',N'nvarchar',1000,NULL,NULL,1),
        (N'AccountingBookInitializations',N'RowVersion',N'timestamp',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'CreatedAt',N'datetime2',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'UpdatedAt',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'CreatedBy',N'nvarchar',-1,NULL,NULL,1),
        (N'AccountingBookInitializations',N'UpdatedBy',N'nvarchar',-1,NULL,NULL,1),
        (N'AccountingBookInitializations',N'CreatedById',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'LastModifiedById',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'IsDeleted',N'bit',NULL,NULL,NULL,0),
        (N'AccountingBookInitializations',N'DeletedAt',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookInitializations',N'DeletedBy',N'nvarchar',-1,NULL,NULL,1),
        (N'AccountingBookInitializations',N'TenantId',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookPeriods',N'Id',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookPeriods',N'AccountingBookId',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookPeriods',N'FiscalPeriodId',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookPeriods',N'PeriodStatus',N'int',NULL,NULL,NULL,0),
        (N'AccountingBookPeriods',N'PendingStatus',N'int',NULL,NULL,NULL,1),
        (N'AccountingBookPeriods',N'PendingReason',N'nvarchar',1000,NULL,NULL,1),
        (N'AccountingBookPeriods',N'RequestedByUserId',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookPeriods',N'RequestedAtUtc',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookPeriods',N'WorkflowInstanceId',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookPeriods',N'DecidedByUserId',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookPeriods',N'DecidedAtUtc',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookPeriods',N'DecisionReason',N'nvarchar',1000,NULL,NULL,1),
        (N'AccountingBookPeriods',N'RowVersion',N'timestamp',NULL,NULL,NULL,0),
        (N'AccountingBookPeriods',N'CreatedAt',N'datetime2',NULL,NULL,NULL,0),
        (N'AccountingBookPeriods',N'UpdatedAt',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookPeriods',N'CreatedBy',N'nvarchar',-1,NULL,NULL,1),
        (N'AccountingBookPeriods',N'UpdatedBy',N'nvarchar',-1,NULL,NULL,1),
        (N'AccountingBookPeriods',N'CreatedById',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookPeriods',N'LastModifiedById',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookPeriods',N'IsDeleted',N'bit',NULL,NULL,NULL,0),
        (N'AccountingBookPeriods',N'DeletedAt',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookPeriods',N'DeletedBy',N'nvarchar',-1,NULL,NULL,1),
        (N'AccountingBookPeriods',N'TenantId',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookInitializationLines',N'Id',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookInitializationLines',N'AccountingBookInitializationId',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookInitializationLines',N'AccountId',N'uniqueidentifier',NULL,NULL,NULL,0),
        (N'AccountingBookInitializationLines',N'CurrencyCode',N'nvarchar',6,NULL,NULL,0),
        (N'AccountingBookInitializationLines',N'OpeningDebit',N'decimal',NULL,18,4,0),
        (N'AccountingBookInitializationLines',N'OpeningCredit',N'decimal',NULL,18,4,0),
        (N'AccountingBookInitializationLines',N'BaseBookSignedBalance',N'decimal',NULL,18,4,0),
        (N'AccountingBookInitializationLines',N'OpeningAdjustment',N'decimal',NULL,18,4,0),
        (N'AccountingBookInitializationLines',N'TranslationExchangeRateId',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'TranslationRate',N'decimal',NULL,18,6,1),
        (N'AccountingBookInitializationLines',N'TranslationRateDate',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'TranslationRateSource',N'nvarchar',200,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'TranslationRateType',N'nvarchar',60,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'CreatedAt',N'datetime2',NULL,NULL,NULL,0),
        (N'AccountingBookInitializationLines',N'UpdatedAt',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'CreatedBy',N'nvarchar',-1,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'UpdatedBy',N'nvarchar',-1,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'CreatedById',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'LastModifiedById',N'uniqueidentifier',NULL,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'IsDeleted',N'bit',NULL,NULL,NULL,0),
        (N'AccountingBookInitializationLines',N'DeletedAt',N'datetime2',NULL,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'DeletedBy',N'nvarchar',-1,NULL,NULL,1),
        (N'AccountingBookInitializationLines',N'TenantId',N'uniqueidentifier',NULL,NULL,NULL,0);
        IF @TranslationApplied=0
        BEGIN
            -- The earlier pending migration adds these nullable fields and their
            -- constraints. Require a clean pre-translation shape, then validate
            -- every baseline column/key below; never call their absence drift.
            IF EXISTS(SELECT 1 FROM @ExpectedColumns e JOIN sys.columns c ON c.object_id=OBJECT_ID(N'dbo.'+e.TableName) AND c.name=e.ColumnName WHERE e.ColumnName LIKE N'Translation%')
               OR EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.ExchangeRates') AND name=N'AK_ExchangeRates_TenantId_Id')
                THROW 51000,'C4_TRANSLATION_PARTIAL_SCHEMA: translation objects exist without predecessor history.',1;
            DELETE @ExpectedColumns WHERE ColumnName LIKE N'Translation%';
        END;
        IF EXISTS (SELECT 1 FROM @ExpectedColumns e LEFT JOIN sys.columns c ON c.object_id = OBJECT_ID(N'dbo.' + e.TableName) AND c.name = e.ColumnName
            LEFT JOIN sys.types ty ON ty.user_type_id = c.user_type_id
            WHERE c.column_id IS NULL OR ty.name <> e.TypeName OR c.is_nullable <> e.IsNullable
               OR (e.MaxLength IS NOT NULL AND c.max_length <> e.MaxLength)
               OR (e.[Precision] IS NOT NULL AND c.precision <> e.[Precision]) OR (e.Scale IS NOT NULL AND c.scale <> e.Scale))
            THROW 51000, 'C4_RECONCILE_COLUMN_DRIFT: existing C4 columns do not match the governed model.', 1;

        IF @TranslationApplied=1
        BEGIN
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND i.name = N'IX_AccountingBookInitializationLines_TenantId_TranslationExchangeRateId' AND i.is_unique = 0 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 2
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'TranslationExchangeRateId')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookInitializationLines_TenantId_TranslationExchangeRateId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND f.name=N'FK_AccountingBookInitializationLines_ExchangeRates_TenantId_TranslationExchangeRateId' AND f.referenced_object_id=OBJECT_ID(N'dbo.ExchangeRates') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=2
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=2 AND pc.name=N'TranslationExchangeRateId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookInitializationLines_ExchangeRates_TenantId_TranslationExchangeRateId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_TranslationMethod' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([TranslationMethod] IS NULL OR ([TranslationMethod]=(2) OR [TranslationMethod]=(1)))' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_TranslationMethod', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND c.name=N'CK_AccountingBookInitializationLines_TranslationEvidence' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([TranslationExchangeRateId] IS NULL AND [TranslationRate] IS NULL AND [TranslationRateDate] IS NULL AND [TranslationRateType] IS NULL AND [TranslationRateSource] IS NULL OR [TranslationExchangeRateId] IS NOT NULL AND [TranslationRate]>(0) AND [TranslationRateDate] IS NOT NULL AND [TranslationRateType] IS NOT NULL AND [TranslationRateSource] IS NOT NULL)' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializationLines_TranslationEvidence', 1;
        END;

        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializations') AND i.name = N'PK_AccountingBookInitializations' AND i.is_unique = 1 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 1
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'Id')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: PK_AccountingBookInitializations', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializations') AND i.name = N'AK_AccountingBookInitializations_TenantId_Id' AND i.is_unique = 1 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 2
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'Id')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: AK_AccountingBookInitializations_TenantId_Id', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializations') AND i.name = N'AK_AccountingBookInitializations_TenantId_AccountingBookId_Id' AND i.is_unique = 1 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 3
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'AccountingBookId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=3 AND c.name=N'Id')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: AK_AccountingBookInitializations_TenantId_AccountingBookId_Id', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookPeriods') AND i.name = N'PK_AccountingBookPeriods' AND i.is_unique = 1 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 1
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'Id')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: PK_AccountingBookPeriods', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND i.name = N'PK_AccountingBookInitializationLines' AND i.is_unique = 1 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 1
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'Id')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: PK_AccountingBookInitializationLines', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND i.name = N'IX_AccountingBookInitializationLines_TenantId_AccountId' AND i.is_unique = 0 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 2
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'AccountId')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookInitializationLines_TenantId_AccountId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND i.name = N'IX_AccountingBookInitializationLines_TenantId_AccountingBookInitializationId_AccountId' AND i.is_unique = 1 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 3
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'AccountingBookInitializationId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=3 AND c.name=N'AccountId')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookInitializationLines_TenantId_AccountingBookInitializationId_AccountId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializations') AND i.name = N'IX_AccountingBookInitializations_TenantId_AccountingBookId_InitializationStatus' AND i.is_unique = 1 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 3
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'AccountingBookId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=3 AND c.name=N'InitializationStatus')
            AND i.has_filter = 1 AND REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(i.filter_definition, N'(', N''), N')', N''), N'[', N''), N']', N''), N' ', N''), NCHAR(13), N''), NCHAR(10), N''), NCHAR(9), N'') COLLATE Latin1_General_100_BIN2 = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(N'[IsDeleted] = 0 AND [InitializationStatus] = 3', N'(', N''), N')', N''), N'[', N''), N']', N''), N' ', N''), NCHAR(13), N''), NCHAR(10), N''), NCHAR(9), N'') COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookInitializations_TenantId_AccountingBookId_InitializationStatus', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializations') AND i.name = N'IX_AccountingBookInitializations_TenantId_AccountingBookId_Version' AND i.is_unique = 1 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 3
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'AccountingBookId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=3 AND c.name=N'Version')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookInitializations_TenantId_AccountingBookId_Version', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializations') AND i.name = N'IX_AccountingBookInitializations_TenantId_IdempotencyKey' AND i.is_unique = 1 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 2
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'IdempotencyKey')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookInitializations_TenantId_IdempotencyKey', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializations') AND i.name = N'IX_AccountingBookInitializations_TenantId_SourceAccountingBookId' AND i.is_unique = 0 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 2
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'SourceAccountingBookId')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookInitializations_TenantId_SourceAccountingBookId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializations') AND i.name = N'IX_AccountingBookInitializations_TenantId_AccountingBookId_SupersedesInitializationId' AND i.is_unique = 0 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 3
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'AccountingBookId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=3 AND c.name=N'SupersedesInitializationId')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookInitializations_TenantId_AccountingBookId_SupersedesInitializationId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookInitializations') AND i.name = N'IX_AccountingBookInitializations_TenantId_CutoffFiscalPeriodId' AND i.is_unique = 0 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 2
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'CutoffFiscalPeriodId')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookInitializations_TenantId_CutoffFiscalPeriodId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookPeriods') AND i.name = N'IX_AccountingBookPeriods_TenantId_AccountingBookId_FiscalPeriodId' AND i.is_unique = 1 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 3
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'AccountingBookId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=3 AND c.name=N'FiscalPeriodId')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookPeriods_TenantId_AccountingBookId_FiscalPeriodId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.AccountingBookPeriods') AND i.name = N'IX_AccountingBookPeriods_TenantId_FiscalPeriodId' AND i.is_unique = 0 AND i.is_disabled = 0 AND i.is_hypothetical = 0
            AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0) = 2
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'FiscalPeriodId')
            AND i.has_filter = 0)
            THROW 51000, 'C4_RECONCILE_KEY_DRIFT: IX_AccountingBookPeriods_TenantId_FiscalPeriodId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND f.name=N'FK_AccountingBookInitializations_AccountingBookInitializations_TenantId_AccountingBookId_SupersedesInitializationId' AND f.referenced_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=3
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=2 AND pc.name=N'AccountingBookId' AND rc.name=N'AccountingBookId')
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=3 AND pc.name=N'SupersedesInitializationId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookInitializations_AccountingBookInitializations_TenantId_AccountingBookId_SupersedesInitializationId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND f.name=N'FK_AccountingBookInitializations_AccountingBooks_TenantId_AccountingBookId' AND f.referenced_object_id=OBJECT_ID(N'dbo.AccountingBooks') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=2
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=2 AND pc.name=N'AccountingBookId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookInitializations_AccountingBooks_TenantId_AccountingBookId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND f.name=N'FK_AccountingBookInitializations_AccountingBooks_TenantId_SourceAccountingBookId' AND f.referenced_object_id=OBJECT_ID(N'dbo.AccountingBooks') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=2
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=2 AND pc.name=N'SourceAccountingBookId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookInitializations_AccountingBooks_TenantId_SourceAccountingBookId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND f.name=N'FK_AccountingBookInitializations_FiscalPeriods_TenantId_CutoffFiscalPeriodId' AND f.referenced_object_id=OBJECT_ID(N'dbo.FiscalPeriods') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=2
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=2 AND pc.name=N'CutoffFiscalPeriodId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookInitializations_FiscalPeriods_TenantId_CutoffFiscalPeriodId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND f.name=N'FK_AccountingBookInitializations_Tenants_TenantId' AND f.referenced_object_id=OBJECT_ID(N'dbo.Tenants') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=1
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookInitializations_Tenants_TenantId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookPeriods') AND f.name=N'FK_AccountingBookPeriods_AccountingBooks_TenantId_AccountingBookId' AND f.referenced_object_id=OBJECT_ID(N'dbo.AccountingBooks') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=2
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=2 AND pc.name=N'AccountingBookId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookPeriods_AccountingBooks_TenantId_AccountingBookId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookPeriods') AND f.name=N'FK_AccountingBookPeriods_FiscalPeriods_TenantId_FiscalPeriodId' AND f.referenced_object_id=OBJECT_ID(N'dbo.FiscalPeriods') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=2
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=2 AND pc.name=N'FiscalPeriodId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookPeriods_FiscalPeriods_TenantId_FiscalPeriodId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookPeriods') AND f.name=N'FK_AccountingBookPeriods_Tenants_TenantId' AND f.referenced_object_id=OBJECT_ID(N'dbo.Tenants') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=1
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookPeriods_Tenants_TenantId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND f.name=N'FK_AccountingBookInitializationLines_AccountingBookInitializations_TenantId_AccountingBookInitializationId' AND f.referenced_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=2
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=2 AND pc.name=N'AccountingBookInitializationId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookInitializationLines_AccountingBookInitializations_TenantId_AccountingBookInitializationId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND f.name=N'FK_AccountingBookInitializationLines_Accounts_TenantId_AccountId' AND f.referenced_object_id=OBJECT_ID(N'dbo.Accounts') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=2
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'TenantId')
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=2 AND pc.name=N'AccountId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookInitializationLines_Accounts_TenantId_AccountId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND f.name=N'FK_AccountingBookInitializationLines_Tenants_TenantId' AND f.referenced_object_id=OBJECT_ID(N'dbo.Tenants') AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=1
            AND EXISTS (SELECT 1 FROM sys.foreign_key_columns x JOIN sys.columns pc ON pc.object_id=x.parent_object_id AND pc.column_id=x.parent_column_id JOIN sys.columns rc ON rc.object_id=x.referenced_object_id AND rc.column_id=x.referenced_column_id WHERE x.constraint_object_id=f.object_id AND x.constraint_column_id=1 AND pc.name=N'TenantId' AND rc.name=N'Id'))
            THROW 51000, 'C4_RECONCILE_FK_DRIFT: FK_AccountingBookInitializationLines_Tenants_TenantId', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_ApprovalShape' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'(([InitializationStatus]=(2) OR [InitializationStatus]=(1)) AND [ApprovedByUserId] IS NULL AND [ApprovedAtUtc] IS NULL AND [RejectedByUserId] IS NULL AND [RejectedAtUtc] IS NULL OR [InitializationStatus]=(3) AND [ApprovedByUserId] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL AND [RejectedByUserId] IS NULL AND [RejectedAtUtc] IS NULL OR [InitializationStatus]=(4) AND [ApprovedByUserId] IS NULL AND [ApprovedAtUtc] IS NULL AND [RejectedByUserId] IS NOT NULL AND [RejectedAtUtc] IS NOT NULL)' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_ApprovalShape', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_Balanced' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([IsDeleted]=(1) OR [TotalDebits]=[TotalCredits])' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_Balanced', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_Coverage' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([RequiredAccountCount]>=(0) AND [CoveredAccountCount]>=(0) AND [CoveredAccountCount]<=[RequiredAccountCount])' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_Coverage', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_DecisionMakerChecker' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([DecidedByUserId] IS NULL OR [DecidedByUserId]<>[PreparedByUserId])' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_DecisionMakerChecker', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_EvidenceFingerprint' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'(len([EvidenceFingerprint])=(64) AND [EvidenceFingerprint]=rtrim([EvidenceFingerprint]) AND NOT ([EvidenceFingerprint]) collate Latin1_General_100_BIN2 like ''%[^0-9A-F]%'')' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_EvidenceFingerprint', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_MakerChecker' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([ApprovedByUserId] IS NULL OR [ApprovedByUserId]<>[PreparedByUserId])' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_MakerChecker', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_Mode' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([IsDeleted]=(1) OR ([Mode]=(3) OR [Mode]=(2) OR [Mode]=(1)))' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_Mode', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_NoDelete' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([IsDeleted]=(0))' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_NoDelete', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_ReconciliationFingerprint' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'(len([ReconciliationFingerprint])=(64) AND [ReconciliationFingerprint]=rtrim([ReconciliationFingerprint]) AND NOT ([ReconciliationFingerprint]) collate Latin1_General_100_BIN2 like ''%[^0-9A-F]%'')' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_ReconciliationFingerprint', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_SourceShape' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([Mode]=(1) AND [SourceAccountingBookId] IS NULL OR ([Mode]=(3) OR [Mode]=(2)) AND [SourceAccountingBookId] IS NOT NULL AND [SourceAccountingBookId]<>[AccountingBookId])' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_SourceShape', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializations') AND c.name=N'CK_AccountingBookInitializations_Status' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([IsDeleted]=(1) OR ([InitializationStatus]=(4) OR [InitializationStatus]=(3) OR [InitializationStatus]=(2) OR [InitializationStatus]=(1)))' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializations_Status', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookPeriods') AND c.name=N'CK_AccountingBookPeriods_NoDelete' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([IsDeleted]=(0))' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookPeriods_NoDelete', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookPeriods') AND c.name=N'CK_AccountingBookPeriods_PendingStatus' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([PendingStatus] IS NULL OR ([PendingStatus]=(4) OR [PendingStatus]=(3) OR [PendingStatus]=(2) OR [PendingStatus]=(1)))' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookPeriods_PendingStatus', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookPeriods') AND c.name=N'CK_AccountingBookPeriods_Status' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([IsDeleted]=(1) OR ([PeriodStatus]=(4) OR [PeriodStatus]=(3) OR [PeriodStatus]=(2) OR [PeriodStatus]=(1)))' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookPeriods_Status', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND c.name=N'CK_AccountingBookInitializationLines_Amounts' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([OpeningDebit]>=(0) AND [OpeningCredit]>=(0) AND NOT ([OpeningDebit]>(0) AND [OpeningCredit]>(0)))' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializationLines_Amounts', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND c.name=N'CK_AccountingBookInitializationLines_Currency' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'(len([CurrencyCode])=(3) AND [CurrencyCode]=rtrim([CurrencyCode]) AND ([CurrencyCode]) collate Latin1_General_100_BIN2 like ''[A-Z][A-Z][A-Z]'')' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializationLines_Currency', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.AccountingBookInitializationLines') AND c.name=N'CK_AccountingBookInitializationLines_NoDelete' AND c.is_disabled=0 AND c.is_not_trusted=0 AND c.definition COLLATE Latin1_General_100_BIN2 = N'([IsDeleted]=(0))' COLLATE Latin1_General_100_BIN2)
            THROW 51000, 'C4_RECONCILE_CHECK_DRIFT: CK_AccountingBookInitializationLines_NoDelete', 1;

    END;
END TRY
BEGIN CATCH
    INSERT @R VALUES(N'CanonicalPeriodSchema:'+ERROR_MESSAGE(),1);
END CATCH;
SELECT CheckName,AffectedRows FROM @R ORDER BY CheckName;
