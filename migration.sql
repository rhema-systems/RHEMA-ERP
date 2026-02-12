IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [AspNetRoles] (
    [Id] uniqueidentifier NOT NULL,
    [Description] nvarchar(500) NULL,
    [IsSystemRole] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [MaintenanceContractor] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [ContractorCode] nvarchar(50) NULL,
    [Description] nvarchar(1000) NULL,
    [ContactInfo] nvarchar(max) NOT NULL,
    [Capabilities] nvarchar(max) NOT NULL,
    [ServiceAreas] nvarchar(max) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [Rating] float NULL,
    [LicenseInfo] nvarchar(max) NULL,
    [InsuranceInfo] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL,
    [LastModifiedDate] datetime2 NULL,
    [CreatedById] uniqueidentifier NOT NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MaintenanceContractor] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Permissions] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [DisplayName] nvarchar(200) NOT NULL,
    [Description] nvarchar(500) NULL,
    [Category] nvarchar(100) NOT NULL,
    [IsSystemPermission] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [QualityControlChecklists] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [WorkOrderType] nvarchar(50) NULL,
    [AssetCategory] nvarchar(50) NULL,
    [MaintenanceType] nvarchar(50) NULL,
    [IsMandatory] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [ChecklistItems] nvarchar(max) NOT NULL,
    [MinimumPassingScore] int NOT NULL,
    [Version] int NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    [LastModifiedDate] datetime2 NULL,
    [CreatedById] uniqueidentifier NOT NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_QualityControlChecklists] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [QualityMetrics] (
    [Id] uniqueidentifier NOT NULL,
    [MetricsDate] datetime2 NOT NULL,
    [TechnicianId] uniqueidentifier NULL,
    [TeamId] uniqueidentifier NULL,
    [AssetCategory] nvarchar(50) NULL,
    [TotalWorkOrders] int NOT NULL,
    [FirstTimePassCount] int NOT NULL,
    [ReworkCount] int NOT NULL,
    [RejectedCount] int NOT NULL,
    [FirstTimeFixRate] float NOT NULL,
    [AverageQualityScore] float NOT NULL,
    [CustomerSatisfactionScore] float NULL,
    [AverageInspectionTime] float NOT NULL,
    [SafetyViolations] int NOT NULL,
    [CompliancePercentage] float NOT NULL,
    [CalculatedDate] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_QualityMetrics] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [SafetyAudits] (
    [Id] uniqueidentifier NOT NULL,
    [AuditName] nvarchar(100) NOT NULL,
    [AuditDate] datetime2 NOT NULL,
    [LeadAuditor] nvarchar(100) NOT NULL,
    [AuditTeam] nvarchar(max) NOT NULL,
    [AuditScope] nvarchar(1000) NOT NULL,
    [OverallScore] int NOT NULL,
    [Findings] nvarchar(2000) NOT NULL,
    [Recommendations] nvarchar(2000) NOT NULL,
    [AuditStatus] nvarchar(20) NOT NULL,
    [FollowUpDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_SafetyAudits] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Tenants] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Description] nvarchar(500) NULL,
    [Status] int NOT NULL,
    [Domain] nvarchar(200) NULL,
    [LogoUrl] nvarchar(500) NULL,
    [PrimaryColor] nvarchar(7) NULL,
    [SecondaryColor] nvarchar(7) NULL,
    [FaviconUrl] nvarchar(500) NULL,
    [CoverImageUrl] nvarchar(500) NULL,
    [ContactEmail] nvarchar(200) NULL,
    [ContactPhone] nvarchar(50) NULL,
    [Address] nvarchar(500) NULL,
    [SubscriptionStartDate] datetime2 NULL,
    [SubscriptionEndDate] datetime2 NULL,
    [LdapServer] nvarchar(max) NULL,
    [LdapPort] int NULL,
    [LdapBaseDn] nvarchar(max) NULL,
    [LdapBindDn] nvarchar(max) NULL,
    [LdapBindPassword] nvarchar(max) NULL,
    [LdapEnabled] bit NOT NULL,
    [IsDefaultForPublicUsers] bit NOT NULL,
    [IsDefaultForInternalUsers] bit NOT NULL,
    [AllowSelfRegistration] bit NOT NULL,
    [PublicRegistrationDomains] nvarchar(max) NULL,
    [RequireEmailVerification] bit NOT NULL,
    [UserAudience] int NOT NULL,
    [WelcomeMessage] nvarchar(max) NULL,
    [DefaultPriority] int NOT NULL,
    [EnableAutoSelection] bit NOT NULL,
    [BaseCurrency] nvarchar(3) NOT NULL,
    [BaseCurrencyName] nvarchar(50) NULL,
    [CurrencySymbol] nvarchar(5) NULL,
    [CurrencyDecimalPlaces] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] uniqueidentifier NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ContractorInvoice] (
    [Id] uniqueidentifier NOT NULL,
    [ContractorId] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [InvoiceNumber] nvarchar(50) NOT NULL,
    [InvoiceDate] datetime2 NOT NULL,
    [DueDate] datetime2 NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [TaxAmount] decimal(18,2) NULL,
    [Status] nvarchar(20) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [LineItems] nvarchar(max) NOT NULL,
    [AttachmentPaths] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL,
    [ApprovedDate] datetime2 NULL,
    [PaidDate] datetime2 NULL,
    [ApprovedById] uniqueidentifier NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [MaintenanceContractorId] uniqueidentifier NULL,
    CONSTRAINT [PK_ContractorInvoice] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ContractorInvoice_MaintenanceContractor_MaintenanceContractorId] FOREIGN KEY ([MaintenanceContractorId]) REFERENCES [MaintenanceContractor] ([Id])
);
GO

CREATE TABLE [ContractorPerformanceReview] (
    [Id] uniqueidentifier NOT NULL,
    [ContractorId] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [ReviewDate] datetime2 NOT NULL,
    [ReviewedById] uniqueidentifier NOT NULL,
    [OverallRating] int NOT NULL,
    [QualityRating] int NULL,
    [TimelinessRating] int NULL,
    [CommunicationRating] int NULL,
    [CostRating] int NULL,
    [Comments] nvarchar(2000) NULL,
    [Recommendations] nvarchar(1000) NULL,
    [WouldRecommend] bit NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ContractorPerformanceReview] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ContractorPerformanceReview_MaintenanceContractor_ContractorId] FOREIGN KEY ([ContractorId]) REFERENCES [MaintenanceContractor] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [RolePermissions] (
    [RoleId] uniqueidentifier NOT NULL,
    [PermissionId] uniqueidentifier NOT NULL,
    [GrantedAt] datetime2 NOT NULL,
    [GrantedBy] nvarchar(100) NULL,
    CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleId], [PermissionId]),
    CONSTRAINT [FK_RolePermissions_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Accounts] (
    [Id] uniqueidentifier NOT NULL,
    [AccountCode] nvarchar(50) NOT NULL,
    [AccountNumber] nvarchar(100) NOT NULL,
    [AccountName] nvarchar(200) NOT NULL,
    [AccountType] int NOT NULL,
    [AccountCategory] nvarchar(100) NULL,
    [AccountSubCategory] nvarchar(100) NULL,
    [Description] nvarchar(1000) NULL,
    [ParentAccountId] uniqueidentifier NULL,
    [IsSegmented] bit NOT NULL,
    [CurrencyCode] nvarchar(3) NOT NULL,
    [IsMultiCurrency] bit NOT NULL,
    [IsIFRSClassified] bit NOT NULL,
    [IsBaseClassified] bit NOT NULL,
    [IsLocalClassified] bit NOT NULL,
    [IFRSLineItem] nvarchar(100) NULL,
    [BaseLineItem] nvarchar(100) NULL,
    [LocalLineItem] nvarchar(100) NULL,
    [AllowDirectPosting] bit NOT NULL,
    [IsControlAccount] bit NOT NULL,
    [RequireDepartmentCode] bit NOT NULL,
    [RequireProjectCode] bit NOT NULL,
    [BudgetTrackingEnabled] bit NOT NULL,
    [Status] int NOT NULL,
    [Balance] decimal(18,4) NOT NULL,
    [DebitBalance] decimal(18,4) NOT NULL,
    [CreditBalance] decimal(18,4) NOT NULL,
    [OpeningBalance] decimal(18,4) NOT NULL,
    [LastTransactionDate] datetime2 NULL,
    [EstateModuleLinkId] uniqueidentifier NULL,
    [PayrollModuleLinkId] uniqueidentifier NULL,
    [ProcurementModuleLinkId] uniqueidentifier NULL,
    [TaxReportingCategory] nvarchar(50) NULL,
    [CashFlowClassification] nvarchar(50) NULL,
    [IsSystemAccount] bit NOT NULL,
    [InactivatedDate] datetime2 NULL,
    [InactivationReason] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [ReferenceNumber] nvarchar(50) NOT NULL,
    [EffectiveDate] datetime2 NULL,
    [ExpirationDate] datetime2 NULL,
    [Metadata] nvarchar(max) NULL,
    [Tags] nvarchar(500) NULL,
    [Priority] int NOT NULL,
    CONSTRAINT [PK_Accounts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Accounts_Accounts_ParentAccountId] FOREIGN KEY ([ParentAccountId]) REFERENCES [Accounts] ([Id]),
    CONSTRAINT [FK_Accounts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AccountSegmentStructures] (
    [Id] uniqueidentifier NOT NULL,
    [SegmentName] nvarchar(100) NOT NULL,
    [SegmentCode] nvarchar(20) NOT NULL,
    [SegmentPosition] int NOT NULL,
    [SegmentLength] int NOT NULL,
    [DataType] nvarchar(20) NOT NULL,
    [SeparatorCharacter] nvarchar(1) NULL,
    [LookupTableRequired] bit NOT NULL,
    [IsMandatory] bit NOT NULL,
    [IsReportingDimension] bit NOT NULL,
    [IsNaturalAccount] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [Description] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AccountSegmentStructures] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AccountSegmentStructures_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AssetTypes] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Description] nvarchar(500) NULL,
    [Color] nvarchar(7) NULL,
    [Icon] nvarchar(50) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [RequiresLocation] bit NOT NULL,
    [RequiresOperatingHours] bit NOT NULL,
    [RequiresMileageTracking] bit NOT NULL,
    [RequiresLicensing] bit NOT NULL,
    [RequiresInspections] bit NOT NULL,
    [SupportsHierarchy] bit NOT NULL,
    [RequiresSpecializedFields] bit NOT NULL,
    [DefaultMaintenanceIntervalDays] int NOT NULL DEFAULT 30,
    [RequiresPreventiveMaintenance] bit NOT NULL,
    [RequiresConditionMonitoring] bit NOT NULL,
    [RequiresSafetyChecks] bit NOT NULL,
    [RequiresLockoutTagout] bit NOT NULL,
    [RequiresPermits] bit NOT NULL,
    [DefaultWorkOrderPriority] int NOT NULL,
    [DefaultEstimatedHours] float NOT NULL,
    [DefaultWorkInstructions] nvarchar(2000) NULL,
    [CustomFieldsConfig] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [UpdatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AssetTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetTypes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ContractorSpecializations] (
    [Id] uniqueidentifier NOT NULL,
    [SpecializationCode] nvarchar(50) NOT NULL,
    [SpecializationName] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [RequiresLicense] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ContractorSpecializations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ContractorSpecializations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Countries] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(3) NOT NULL,
    [Alpha2Code] nvarchar(2) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Countries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Countries_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EmailSettings] (
    [Id] uniqueidentifier NOT NULL,
    [SmtpHost] nvarchar(255) NOT NULL,
    [SmtpPort] int NOT NULL,
    [SmtpUsername] nvarchar(255) NOT NULL,
    [SmtpPassword] nvarchar(255) NOT NULL,
    [UseTLS] bit NOT NULL,
    [FromAddress] nvarchar(255) NOT NULL,
    [FromName] nvarchar(255) NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_EmailSettings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmailSettings_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [EmailTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Module] nvarchar(50) NOT NULL,
    [TableName] nvarchar(100) NULL,
    [Subject] nvarchar(200) NOT NULL,
    [HtmlBody] nvarchar(max) NOT NULL,
    [PlainTextBody] nvarchar(max) NULL,
    [SelectedFields] nvarchar(max) NULL,
    [TemplateVariables] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [Description] nvarchar(500) NULL,
    [Category] nvarchar(50) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmailTemplates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmailTemplates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ExchangeRates] (
    [Id] uniqueidentifier NOT NULL,
    [BaseCurrencyCode] nvarchar(3) NOT NULL,
    [TargetCurrencyCode] nvarchar(3) NOT NULL,
    [Rate] decimal(18,4) NOT NULL,
    [InverseRate] decimal(18,4) NOT NULL,
    [EffectiveDate] datetime2 NOT NULL,
    [EndDate] datetime2 NULL,
    [RateType] int NOT NULL,
    [IsActive] bit NOT NULL,
    [Priority] int NOT NULL,
    [RateSource] nvarchar(100) NOT NULL,
    [IsManualEntry] bit NOT NULL,
    [APIEndpoint] nvarchar(500) NULL,
    [APIResponseMetadata] nvarchar(1000) NULL,
    [HasBeenUsedInTransactions] bit NOT NULL,
    [TransactionCount] int NOT NULL,
    [FirstUsedDate] datetime2 NULL,
    [LastUsedDate] datetime2 NULL,
    [RateChangePercentage] decimal(18,4) NULL,
    [RateChangeAmount] decimal(18,2) NULL,
    [PreviousRateId] uniqueidentifier NULL,
    [ExceedsVarianceThreshold] bit NOT NULL,
    [ApprovalStatus] int NOT NULL,
    [ApprovedByUserId] uniqueidentifier NULL,
    [ApprovalDate] datetime2 NULL,
    [Comments] nvarchar(1000) NULL,
    [CreatedByUserId] uniqueidentifier NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    [ModifiedByUserId] uniqueidentifier NULL,
    [ModifiedDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [ReferenceNumber] nvarchar(50) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [ExpirationDate] datetime2 NULL,
    [Metadata] nvarchar(max) NULL,
    [Tags] nvarchar(500) NULL,
    CONSTRAINT [PK_ExchangeRates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ExchangeRates_ExchangeRates_PreviousRateId] FOREIGN KEY ([PreviousRateId]) REFERENCES [ExchangeRates] ([Id]),
    CONSTRAINT [FK_ExchangeRates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [InspectionChecklistTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Category] nvarchar(50) NOT NULL,
    [ApplicableAssetTypes] nvarchar(max) NULL,
    [ApplicableMaintenanceTypes] nvarchar(max) NULL,
    [ApplicableWorkOrderTypes] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [IsDefault] bit NOT NULL,
    [SortOrder] int NOT NULL,
    [Version] int NOT NULL,
    [VersionNotes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InspectionChecklistTemplates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InspectionChecklistTemplates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [InspectionTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Category] nvarchar(100) NULL,
    [InspectionType] nvarchar(50) NOT NULL,
    [IsActive] bit NOT NULL,
    [ChecklistItems] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InspectionTemplates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InspectionTemplates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [InventoryCategories] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [ParentCategoryId] uniqueidentifier NULL,
    [Color] nvarchar(7) NULL,
    [Icon] nvarchar(50) NULL,
    [IsActive] bit NOT NULL,
    [DefaultUnitOfMeasure] nvarchar(20) NULL,
    [DefaultSerialTracking] bit NOT NULL,
    [DefaultLotTracking] bit NOT NULL,
    [DefaultRequiresInspection] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InventoryCategories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InventoryCategories_InventoryCategories_ParentCategoryId] FOREIGN KEY ([ParentCategoryId]) REFERENCES [InventoryCategories] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InventoryCategories_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [LicenseTypes] (
    [Id] uniqueidentifier NOT NULL,
    [LicenseCode] nvarchar(50) NOT NULL,
    [LicenseName] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [IssuingAuthority] nvarchar(200) NULL,
    [ValidityPeriodMonths] int NULL,
    [IsMandatory] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_LicenseTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LicenseTypes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [MaintenanceAssetCategories] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Code] nvarchar(50) NULL,
    [Color] nvarchar(7) NULL,
    [Icon] nvarchar(50) NULL,
    [IsActive] bit NOT NULL,
    [AssetType] nvarchar(50) NULL,
    [ParentCategoryId] uniqueidentifier NULL,
    [MaintenanceScheduleType] nvarchar(20) NOT NULL,
    [AutoGenerateSchedules] bit NOT NULL,
    [MaintenanceType] nvarchar(20) NOT NULL,
    [MaintenanceFrequency] nvarchar(50) NULL,
    [MaintenanceValue] float NULL,
    [MaintenanceUnit] nvarchar(20) NULL,
    [SecondaryMaintenanceType] nvarchar(20) NULL,
    [SecondaryMaintenanceFrequency] nvarchar(50) NULL,
    [SecondaryMaintenanceValue] float NULL,
    [SecondaryMaintenanceUnit] nvarchar(20) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MaintenanceAssetCategories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceAssetCategories_MaintenanceAssetCategories_ParentCategoryId] FOREIGN KEY ([ParentCategoryId]) REFERENCES [MaintenanceAssetCategories] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_MaintenanceAssetCategories_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [MaintenanceNotifications] (
    [Id] uniqueidentifier NOT NULL,
    [NotificationType] nvarchar(50) NOT NULL,
    [EntityType] nvarchar(50) NOT NULL,
    [EntityId] uniqueidentifier NOT NULL,
    [RecipientId] uniqueidentifier NOT NULL,
    [RecipientRole] nvarchar(100) NULL,
    [Title] nvarchar(200) NOT NULL,
    [Message] nvarchar(1000) NOT NULL,
    [Priority] nvarchar(20) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [ScheduledFor] datetime2 NOT NULL,
    [SentAt] datetime2 NULL,
    [ReadAt] datetime2 NULL,
    [DismissedAt] datetime2 NULL,
    [AttemptCount] int NOT NULL,
    [LastError] nvarchar(500) NULL,
    [AdditionalData] nvarchar(max) NULL,
    [ActionUrl] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MaintenanceNotifications] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceNotifications_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [MaintenanceNotificationTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [NotificationType] nvarchar(50) NOT NULL,
    [Description] nvarchar(500) NULL,
    [TitleTemplate] nvarchar(200) NOT NULL,
    [MessageTemplate] nvarchar(2000) NOT NULL,
    [DefaultPriority] nvarchar(20) NOT NULL,
    [LeadTimeMinutes] int NOT NULL,
    [IsActive] bit NOT NULL,
    [DefaultRoles] nvarchar(500) NULL,
    [DeliveryMethods] nvarchar(200) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MaintenanceNotificationTemplates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceNotificationTemplates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [MaintenanceTools] (
    [Id] uniqueidentifier NOT NULL,
    [ToolCode] nvarchar(50) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Category] nvarchar(50) NOT NULL,
    [Manufacturer] nvarchar(100) NULL,
    [Model] nvarchar(100) NULL,
    [SerialNumber] nvarchar(100) NULL,
    [Status] nvarchar(20) NOT NULL,
    [CurrentLocation] nvarchar(200) NULL,
    [HomeLocation] nvarchar(200) NULL,
    [LastMaintenanceDate] datetime2 NULL,
    [NextMaintenanceDate] datetime2 NULL,
    [LastCalibrationDate] datetime2 NULL,
    [NextCalibrationDate] datetime2 NULL,
    [PurchasePrice] decimal(18,2) NOT NULL,
    [CurrentValue] decimal(18,4) NOT NULL,
    [DailyRentalRate] decimal(18,4) NOT NULL,
    [TotalUsageDays] int NOT NULL,
    [LastUsedDate] datetime2 NULL,
    [RequiresCertification] bit NOT NULL,
    [RequiresTraining] bit NOT NULL,
    [SafetyNotes] nvarchar(1000) NULL,
    [DocumentPaths] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MaintenanceTools] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceTools_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [MaintenanceTypes] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Category] nvarchar(30) NOT NULL,
    [MaintenanceClass] nvarchar(30) NOT NULL,
    [Location] nvarchar(20) NOT NULL,
    [IsConditionBased] bit NOT NULL,
    [IsUsageBased] bit NOT NULL,
    [IsTimeBased] bit NOT NULL,
    [MileageTrigger] decimal(18,4) NULL,
    [HoursTrigger] decimal(18,4) NULL,
    [CycleTrigger] decimal(18,4) NULL,
    [ConditionCriteria] nvarchar(max) NULL,
    [FrequencyDays] int NULL,
    [FrequencyWeeks] int NULL,
    [FrequencyMonths] int NULL,
    [ApplicableAssetTypes] nvarchar(max) NULL,
    [RequiredSkills] nvarchar(max) NULL,
    [RequiredTools] nvarchar(max) NULL,
    [RequiresSafetyPermit] bit NOT NULL,
    [RequiresShutdown] bit NOT NULL,
    [RequiresSpecialTraining] bit NOT NULL,
    [SafetyRequirements] nvarchar(1000) NULL,
    [RequiresApproval] bit NOT NULL,
    [ApprovalLevels] int NOT NULL,
    [EstimatedHours] float NOT NULL,
    [EstimatedCost] decimal(18,2) NOT NULL,
    [DefaultPriority] int NOT NULL,
    [Criticality] nvarchar(20) NOT NULL,
    [LeadTimeDays] int NOT NULL,
    [DowntimeMinutes] int NOT NULL,
    [RequiresQualityCheck] bit NOT NULL,
    [RequiresDocumentation] bit NOT NULL,
    [RequiresCertification] bit NOT NULL,
    [Color] nvarchar(7) NULL,
    [Icon] nvarchar(50) NULL,
    [IsActive] bit NOT NULL,
    [SortOrder] int NOT NULL,
    [TaskTemplate] nvarchar(max) NULL,
    [ChecklistTemplate] nvarchar(max) NULL,
    [PartsTemplate] nvarchar(max) NULL,
    [SchedulingRules] nvarchar(max) NULL,
    [AverageCompletionHours] float NOT NULL,
    [AverageCost] decimal(18,2) NOT NULL,
    [LastPerformanceUpdate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MaintenanceTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceTypes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Notifications] (
    [Id] uniqueidentifier NOT NULL,
    [NotificationType] nvarchar(max) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Message] nvarchar(max) NOT NULL,
    [Priority] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [RecipientId] uniqueidentifier NOT NULL,
    [IsRead] bit NOT NULL,
    [ReadAt] datetime2 NULL,
    [DismissedAt] datetime2 NULL,
    [ScheduledFor] datetime2 NOT NULL,
    [SentAt] datetime2 NULL,
    [AttemptCount] int NOT NULL,
    [LastError] nvarchar(max) NULL,
    [EntityType] nvarchar(max) NULL,
    [EntityId] uniqueidentifier NULL,
    [AdditionalData] nvarchar(max) NULL,
    [ActionUrl] nvarchar(max) NULL,
    [DeliveryMethods] nvarchar(max) NULL,
    [EmailAddress] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Notifications_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [PartnerCategories] (
    [Id] uniqueidentifier NOT NULL,
    [CategoryCode] nvarchar(50) NOT NULL,
    [CategoryName] nvarchar(200) NOT NULL,
    [CategoryType] nvarchar(20) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [ParentCategoryId] uniqueidentifier NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PartnerCategories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PartnerCategories_PartnerCategories_ParentCategoryId] FOREIGN KEY ([ParentCategoryId]) REFERENCES [PartnerCategories] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PartnerCategories_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [PriorityLevels] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(50) NOT NULL,
    [Level] int NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Color] nvarchar(7) NULL,
    [ResponseTimeHours] int NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PriorityLevels] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PriorityLevels_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [RatioDefinitions] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(30) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [NumeratorType] int NOT NULL,
    [NumeratorAccountId] uniqueidentifier NULL,
    [NumeratorConstant] decimal(18,4) NULL,
    [DenominatorType] int NOT NULL,
    [DenominatorAccountId] uniqueidentifier NULL,
    [DenominatorConstant] decimal(18,4) NULL,
    [ResultFormat] int NOT NULL,
    [DecimalPlaces] int NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_RatioDefinitions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RatioDefinitions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ReportTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    [Category] nvarchar(50) NOT NULL,
    [Type] nvarchar(20) NOT NULL,
    [ChartType] nvarchar(20) NULL,
    [IsCustom] bit NOT NULL,
    [LastUsed] datetime2 NULL,
    [UsageCount] int NOT NULL,
    [Tags] nvarchar(max) NULL,
    [PreviewImage] nvarchar(500) NULL,
    [Configuration] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ReportTemplates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReportTemplates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [SafetyProtocols] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(20) NOT NULL,
    [Description] nvarchar(500) NOT NULL,
    [Category] nvarchar(50) NOT NULL,
    [Severity] nvarchar(20) NOT NULL,
    [RegulatoryStandard] nvarchar(100) NOT NULL,
    [Procedures] nvarchar(max) NOT NULL,
    [RequiredEquipment] nvarchar(max) NOT NULL,
    [RequiredTraining] nvarchar(max) NOT NULL,
    [RequiredCertifications] nvarchar(max) NOT NULL,
    [EmergencyProcedures] nvarchar(2000) NOT NULL,
    [PreventiveMeasures] nvarchar(2000) NOT NULL,
    [ApplicableMaintenanceTypes] nvarchar(max) NOT NULL,
    [ApplicableAssetTypes] nvarchar(max) NOT NULL,
    [IsMandatory] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [ReviewDate] datetime2 NULL,
    [NextReviewDate] datetime2 NULL,
    [ReviewFrequencyMonths] int NOT NULL,
    [MinimumTrainingLevel] nvarchar(50) NOT NULL,
    [EffectiveDate] datetime2 NULL,
    [ExpirationDate] datetime2 NULL,
    [ReviewedBy] nvarchar(100) NOT NULL,
    [IsRegulatory] bit NOT NULL,
    [ComplianceCheckpoints] nvarchar(max) NOT NULL,
    [RegulatorySources] nvarchar(max) NOT NULL,
    [ApplicableEnvironments] nvarchar(max) NOT NULL,
    [ApprovalStatus] nvarchar(20) NOT NULL,
    [ApprovedBy] nvarchar(100) NOT NULL,
    [ApprovalDate] datetime2 NULL,
    [Version] nvarchar(10) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SafetyProtocols] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SafetyProtocols_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Securities] (
    [Id] uniqueidentifier NOT NULL,
    [PasswordMinLength] int NOT NULL,
    [PasswordRequireUppercase] bit NOT NULL,
    [PasswordRequireLowercase] bit NOT NULL,
    [PasswordRequireDigits] bit NOT NULL,
    [PasswordRequireSpecialChars] bit NOT NULL,
    [PasswordMaxAge] int NULL,
    [PasswordPreventReuse] int NULL,
    [CaptchaEnabled] bit NOT NULL,
    [CaptchaProvider] nvarchar(20) NOT NULL,
    [RecaptchaSiteKey] nvarchar(255) NULL,
    [RecaptchaSecretKey] nvarchar(255) NULL,
    [HCaptchaSiteKey] nvarchar(255) NULL,
    [HCaptchaSecretKey] nvarchar(255) NULL,
    [RateLimitLoginMaxAttempts] int NOT NULL,
    [RateLimitLoginWindowMinutes] int NOT NULL,
    [RateLimitLoginBlockDurationMinutes] int NOT NULL,
    [RateLimitRegisterMaxAttempts] int NOT NULL,
    [RateLimitRegisterWindowMinutes] int NOT NULL,
    [RateLimitRegisterBlockDurationMinutes] int NOT NULL,
    [RateLimitForgotPasswordMaxAttempts] int NOT NULL,
    [RateLimitForgotPasswordWindowMinutes] int NOT NULL,
    [RateLimitForgotPasswordBlockDurationMinutes] int NOT NULL,
    [SessionTimeoutMinutes] int NOT NULL,
    [JwtTokenLifetimeMinutes] int NOT NULL,
    [MaxFailedLoginAttempts] int NOT NULL,
    [AccountLockoutMinutes] int NOT NULL,
    [PreventConcurrentLogin] int NOT NULL,
    [TermsOfServiceUrl] nvarchar(2048) NULL,
    [PrivacyPolicyUrl] nvarchar(2048) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_Securities] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Securities_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [SecurityMetricsSet] (
    [Id] uniqueidentifier NOT NULL,
    [MetricDate] datetime2 NOT NULL,
    [TwoFactorAdoptionRate] int NOT NULL,
    [FailedLoginAttempts] int NOT NULL,
    [ActiveSessions] int NOT NULL,
    [SecurityIncidents] int NOT NULL,
    [PasswordCompliance] int NOT NULL,
    [AuditEventsToday] int NOT NULL,
    [TotalUsers] int NOT NULL,
    [UsersWithTwoFactorEnabled] int NOT NULL,
    [LockedAccounts] int NOT NULL,
    [PasswordExpiringSoon] int NOT NULL,
    [SuspiciousActivityCount] int NOT NULL,
    [TwoFactorAdoptionTrend] int NOT NULL,
    [FailedLoginAttemptsTrend] int NOT NULL,
    [ActiveSessionsTrend] int NOT NULL,
    [SecurityIncidentsTrend] int NOT NULL,
    [PasswordComplianceTrend] int NOT NULL,
    [AuditEventsTrend] int NOT NULL,
    [LastUpdated] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SecurityMetricsSet] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SecurityMetricsSet_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [SecurityPolicies] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [Type] int NOT NULL,
    [IsActive] bit NOT NULL,
    [IsEnforced] bit NOT NULL,
    [EffectiveFrom] datetime2 NULL,
    [EffectiveTo] datetime2 NULL,
    [PolicyRules] nvarchar(max) NOT NULL,
    [Exceptions] nvarchar(2000) NULL,
    [Priority] int NOT NULL,
    [LastEvaluated] datetime2 NOT NULL,
    [ViolationCount] int NOT NULL,
    [LastViolation] datetime2 NULL,
    [NotificationSettings] nvarchar(1000) NULL,
    [RequiresApproval] bit NOT NULL,
    [ApprovedBy] nvarchar(100) NULL,
    [ApprovedAt] datetime2 NULL,
    [ApprovalNotes] nvarchar(500) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_SecurityPolicies] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SecurityPolicies_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Shifts] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [StartTime] time NOT NULL,
    [EndTime] time NOT NULL,
    [IsActive] bit NOT NULL,
    [Color] nvarchar(7) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Shifts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Shifts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Skills] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Category] nvarchar(100) NULL,
    [IsActive] bit NOT NULL,
    [RequiresCertification] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Skills] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Skills_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Suppliers] (
    [Id] uniqueidentifier NOT NULL,
    [SupplierCode] nvarchar(100) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [SupplierType] nvarchar(50) NOT NULL,
    [Address] nvarchar(500) NULL,
    [City] nvarchar(100) NULL,
    [State] nvarchar(50) NULL,
    [ZipCode] nvarchar(20) NULL,
    [Country] nvarchar(50) NULL,
    [Phone] nvarchar(50) NULL,
    [Email] nvarchar(100) NULL,
    [Website] nvarchar(200) NULL,
    [PrimaryContactName] nvarchar(100) NULL,
    [PrimaryContactTitle] nvarchar(100) NULL,
    [PrimaryContactPhone] nvarchar(50) NULL,
    [PrimaryContactEmail] nvarchar(100) NULL,
    [TaxId] nvarchar(50) NULL,
    [PaymentTerms] nvarchar(100) NULL,
    [ShippingTerms] nvarchar(100) NULL,
    [CreditLimit] decimal(18,4) NULL,
    [LeadTimeDays] int NOT NULL,
    [IsActive] bit NOT NULL,
    [IsPreferred] bit NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [Rating] int NULL,
    [Notes] nvarchar(2000) NULL,
    [LastOrderDate] datetime2 NULL,
    [ContractStartDate] datetime2 NULL,
    [ContractEndDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Suppliers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Suppliers_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [SystemSettings] (
    [Id] uniqueidentifier NOT NULL,
    [Key] nvarchar(100) NOT NULL,
    [Value] nvarchar(max) NOT NULL,
    [Description] nvarchar(500) NULL,
    [IsEncrypted] bit NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_SystemSettings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SystemSettings_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [TaxTypes] (
    [Id] uniqueidentifier NOT NULL,
    [TaxCode] nvarchar(50) NOT NULL,
    [TaxName] nvarchar(200) NOT NULL,
    [Description] nvarchar(500) NULL,
    [CalculationMethod] int NOT NULL,
    [ApplicabilityScope] int NOT NULL,
    [CalculationOrder] int NOT NULL,
    [IsInputTaxDeductible] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [TaxPayableAccountId] uniqueidentifier NULL,
    [TaxReceivableAccountId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TaxTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TaxTypes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TechnicalSkills] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(20) NOT NULL,
    [Description] nvarchar(500) NOT NULL,
    [Category] nvarchar(50) NOT NULL,
    [SkillLevel] nvarchar(20) NOT NULL,
    [Complexity] nvarchar(20) NOT NULL,
    [RiskLevel] nvarchar(20) NOT NULL,
    [Prerequisites] nvarchar(max) NOT NULL,
    [Certifications] nvarchar(max) NOT NULL,
    [EstimatedLearningHours] int NOT NULL,
    [ToolsRequired] nvarchar(max) NOT NULL,
    [SafetyRequirements] nvarchar(1000) NOT NULL,
    [CompetencyAreas] nvarchar(max) NOT NULL,
    [RelatedMaintenanceTypes] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    [IsFromHRModule] bit NOT NULL,
    [LastSyncDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TechnicalSkills] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TechnicalSkills_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TechnicianShifts] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [StartTime] time NOT NULL,
    [EndTime] time NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Color] nvarchar(7) NULL,
    [IsActive] bit NOT NULL,
    [DaysOfWeek] nvarchar(20) NOT NULL,
    [ScheduledHours] float NOT NULL,
    [BreakMinutes] float NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TechnicianShifts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TechnicianShifts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TechnicianSkills] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Category] nvarchar(100) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TechnicianSkills] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TechnicianSkills_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TenantModules] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [ModuleName] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [Status] int NOT NULL,
    [EnabledDate] datetime2 NULL,
    [DisabledDate] datetime2 NULL,
    [Configuration] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_TenantModules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TenantModules_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [UnitTypes] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(20) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [DecimalPlaces] int NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_UnitTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UnitTypes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Users] (
    [Id] uniqueidentifier NOT NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [LastName] nvarchar(100) NOT NULL,
    [AuthenticationProvider] int NOT NULL,
    [LdapDn] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [LastLoginDate] datetime2 NULL,
    [ProfilePictureUrl] nvarchar(max) NULL,
    [EmployeeId] uniqueidentifier NULL,
    [AuthenticatorKey] nvarchar(32) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Users_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Warehouses] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Address] nvarchar(500) NULL,
    [City] nvarchar(100) NULL,
    [State] nvarchar(50) NULL,
    [ZipCode] nvarchar(20) NULL,
    [Country] nvarchar(50) NULL,
    [IsActive] bit NOT NULL,
    [IsDefault] bit NOT NULL,
    [WarehouseType] nvarchar(20) NOT NULL,
    [ContactPerson] nvarchar(100) NULL,
    [Phone] nvarchar(50) NULL,
    [Email] nvarchar(100) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Warehouses] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Warehouses_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [WorkflowEntityTypes] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [EntityClassName] nvarchar(200) NULL,
    [PropertySchema] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [DisplayOrder] int NOT NULL,
    [Icon] nvarchar(50) NULL,
    [ColorCode] nvarchar(7) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkflowEntityTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowEntityTypes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [WorkOrderTypes] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Color] nvarchar(7) NULL,
    [Icon] nvarchar(50) NULL,
    [IsActive] bit NOT NULL,
    [RequiresApproval] bit NOT NULL,
    [DefaultPriority] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderTypes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AccountCurrencyLinks] (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [LinkedCurrencyCode] nvarchar(3) NOT NULL,
    [RevaluationRequired] bit NOT NULL,
    [RevaluationFrequency] int NOT NULL,
    [TransactionRateType] nvarchar(20) NOT NULL,
    [RevaluationRateType] nvarchar(20) NOT NULL,
    [IsActive] bit NOT NULL,
    [EffectiveDate] datetime2 NOT NULL,
    [EffectiveEndDate] datetime2 NULL,
    [InactivationReason] nvarchar(500) NULL,
    [HasTransactionHistory] bit NOT NULL,
    [TransactionCount] int NOT NULL,
    [FirstTransactionDate] datetime2 NULL,
    [LastTransactionDate] datetime2 NULL,
    [ForeignCurrencyBalance] decimal(18,4) NOT NULL,
    [BaseCurrencyEquivalent] decimal(18,4) NOT NULL,
    [CurrentExchangeRate] decimal(18,4) NOT NULL,
    [RateEffectiveDate] datetime2 NULL,
    [LastRevaluationDate] datetime2 NULL,
    [LastRevaluationAdjustment] decimal(18,4) NOT NULL,
    [CumulativeRevaluationAdjustment] decimal(18,4) NOT NULL,
    [CreatedByUserId] uniqueidentifier NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    [ModifiedByUserId] uniqueidentifier NULL,
    [ModifiedDate] datetime2 NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [ReferenceNumber] nvarchar(50) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [ExpirationDate] datetime2 NULL,
    [Metadata] nvarchar(max) NULL,
    [Tags] nvarchar(500) NULL,
    [Priority] int NOT NULL,
    CONSTRAINT [PK_AccountCurrencyLinks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AccountCurrencyLinks_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AccountCurrencyLinks_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [FinanceSettings] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CoaType] nvarchar(max) NOT NULL,
    [CoaConfigurationLocked] bit NOT NULL,
    [BaseCurrency] nvarchar(max) NOT NULL,
    [RetainedEarningsAccountId] uniqueidentifier NULL,
    [UnrealizedGainLossAccountId] uniqueidentifier NULL,
    [RealizedGainLossAccountId] uniqueidentifier NULL,
    [SuspenseAccountId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [ReferenceNumber] nvarchar(50) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [EffectiveDate] datetime2 NULL,
    [ExpirationDate] datetime2 NULL,
    [Metadata] nvarchar(max) NULL,
    [Tags] nvarchar(500) NULL,
    [Priority] int NOT NULL,
    CONSTRAINT [PK_FinanceSettings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FinanceSettings_Accounts_RealizedGainLossAccountId] FOREIGN KEY ([RealizedGainLossAccountId]) REFERENCES [Accounts] ([Id]),
    CONSTRAINT [FK_FinanceSettings_Accounts_RetainedEarningsAccountId] FOREIGN KEY ([RetainedEarningsAccountId]) REFERENCES [Accounts] ([Id]),
    CONSTRAINT [FK_FinanceSettings_Accounts_SuspenseAccountId] FOREIGN KEY ([SuspenseAccountId]) REFERENCES [Accounts] ([Id]),
    CONSTRAINT [FK_FinanceSettings_Accounts_UnrealizedGainLossAccountId] FOREIGN KEY ([UnrealizedGainLossAccountId]) REFERENCES [Accounts] ([Id]),
    CONSTRAINT [FK_FinanceSettings_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [SegmentLookupValues] (
    [Id] uniqueidentifier NOT NULL,
    [SegmentStructureId] uniqueidentifier NOT NULL,
    [SegmentValue] nvarchar(10) NOT NULL,
    [Description] nvarchar(200) NOT NULL,
    [ParentValueId] uniqueidentifier NULL,
    [EffectiveDate] datetime2 NOT NULL,
    [ExpiryDate] datetime2 NULL,
    [IsActive] bit NOT NULL,
    [DisplayOrder] int NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SegmentLookupValues] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SegmentLookupValues_AccountSegmentStructures_SegmentStructureId] FOREIGN KEY ([SegmentStructureId]) REFERENCES [AccountSegmentStructures] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SegmentLookupValues_SegmentLookupValues_ParentValueId] FOREIGN KEY ([ParentValueId]) REFERENCES [SegmentLookupValues] ([Id]),
    CONSTRAINT [FK_SegmentLookupValues_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AssetTypeFields] (
    [Id] uniqueidentifier NOT NULL,
    [AssetTypeId] uniqueidentifier NOT NULL,
    [FieldName] nvarchar(100) NOT NULL,
    [DisplayName] nvarchar(150) NOT NULL,
    [FieldType] nvarchar(50) NOT NULL,
    [IsRequired] bit NOT NULL DEFAULT CAST(0 AS bit),
    [DisplayOrder] int NOT NULL,
    [ValidationRules] nvarchar(max) NULL,
    [Options] nvarchar(max) NULL,
    [DefaultValue] nvarchar(500) NULL,
    [HelpText] nvarchar(500) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AssetTypeFields] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetTypeFields_AssetTypes_AssetTypeId] FOREIGN KEY ([AssetTypeId]) REFERENCES [AssetTypes] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AssetTypeFields_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [InspectionChecklistItems] (
    [Id] uniqueidentifier NOT NULL,
    [TemplateId] uniqueidentifier NOT NULL,
    [ItemText] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Category] nvarchar(50) NOT NULL,
    [ItemType] nvarchar(20) NOT NULL,
    [IsRequired] bit NOT NULL,
    [IsCritical] bit NOT NULL,
    [SortOrder] int NOT NULL,
    [ValidationRules] nvarchar(max) NULL,
    [ChoiceOptions] nvarchar(max) NULL,
    [DefaultValue] nvarchar(500) NULL,
    [HelpText] nvarchar(1000) NULL,
    [RequiresPhoto] bit NOT NULL,
    [MinPhotos] int NOT NULL,
    [MaxPhotos] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InspectionChecklistItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InspectionChecklistItems_InspectionChecklistTemplates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [InspectionChecklistTemplates] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InspectionChecklistItems_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [InventoryItems] (
    [Id] uniqueidentifier NOT NULL,
    [ItemCode] nvarchar(100) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [CategoryId] uniqueidentifier NOT NULL,
    [Brand] nvarchar(50) NULL,
    [Manufacturer] nvarchar(50) NULL,
    [Model] nvarchar(100) NULL,
    [UnitOfMeasure] nvarchar(20) NOT NULL,
    [StandardCost] decimal(18,2) NOT NULL,
    [AverageCost] decimal(18,2) NOT NULL,
    [LastPurchaseCost] decimal(18,2) NOT NULL,
    [SalePrice] decimal(18,2) NOT NULL,
    [CurrentStock] decimal(18,4) NOT NULL,
    [AvailableStock] decimal(18,4) NOT NULL,
    [AllocatedStock] decimal(18,4) NOT NULL,
    [OnOrderStock] decimal(18,4) NOT NULL,
    [MinimumLevel] decimal(18,4) NOT NULL,
    [MaximumLevel] decimal(18,4) NOT NULL,
    [ReorderLevel] decimal(18,4) NOT NULL,
    [ReorderQuantity] decimal(18,4) NOT NULL,
    [SafetyStock] decimal(18,4) NOT NULL,
    [LeadTimeDays] int NOT NULL,
    [SafetyLeadTimeDays] int NOT NULL,
    [ItemType] int NOT NULL,
    [ABCClass] nvarchar(20) NOT NULL,
    [Status] int NOT NULL,
    [Weight] decimal(18,4) NULL,
    [Length] decimal(18,4) NULL,
    [Width] decimal(18,4) NULL,
    [Height] decimal(18,4) NULL,
    [Volume] decimal(18,4) NULL,
    [IsSerialTracked] bit NOT NULL,
    [IsLotTracked] bit NOT NULL,
    [IsExpirationTracked] bit NOT NULL,
    [IsLocationTracked] bit NOT NULL,
    [RequiresInspection] bit NOT NULL,
    [ShelfLifeDays] int NULL,
    [PrimarySupplier] nvarchar(200) NULL,
    [SupplierItemCode] nvarchar(100) NULL,
    [LastStockDate] datetime2 NULL,
    [LastPurchaseDate] datetime2 NULL,
    [LastSaleDate] datetime2 NULL,
    [LastCountDate] datetime2 NULL,
    [CustomFields] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InventoryItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InventoryItems_InventoryCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [InventoryCategories] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InventoryItems_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [MaintenanceEscalationRules] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [EntityType] nvarchar(50) NOT NULL,
    [TriggerCondition] nvarchar(max) NOT NULL,
    [HoursOverdue] int NOT NULL,
    [TriggerPriority] nvarchar(20) NULL,
    [EscalationRole] nvarchar(100) NOT NULL,
    [EscalationUserId] uniqueidentifier NULL,
    [NotificationTemplateId] uniqueidentifier NULL,
    [IsActive] bit NOT NULL,
    [Priority] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MaintenanceEscalationRules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceEscalationRules_MaintenanceNotificationTemplates_NotificationTemplateId] FOREIGN KEY ([NotificationTemplateId]) REFERENCES [MaintenanceNotificationTemplates] ([Id]),
    CONSTRAINT [FK_MaintenanceEscalationRules_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ProtocolAdherences] (
    [Id] uniqueidentifier NOT NULL,
    [ProtocolId] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [AdherenceDate] datetime2 NOT NULL,
    [WasFollowed] bit NOT NULL,
    [ComplianceScore] int NOT NULL,
    [AdherenceLevel] nvarchar(20) NOT NULL,
    [Notes] nvarchar(1000) NOT NULL,
    [VerifiedBy] nvarchar(100) NOT NULL,
    [VerificationDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ProtocolAdherences] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProtocolAdherences_SafetyProtocols_ProtocolId] FOREIGN KEY ([ProtocolId]) REFERENCES [SafetyProtocols] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProtocolAdherences_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ProtocolAuditDetails] (
    [Id] uniqueidentifier NOT NULL,
    [AuditId] uniqueidentifier NOT NULL,
    [ProtocolId] uniqueidentifier NOT NULL,
    [ComplianceScore] int NOT NULL,
    [Passed] bit NOT NULL,
    [ProtocolFindings] nvarchar(1000) NOT NULL,
    [ProtocolRecommendations] nvarchar(1000) NOT NULL,
    [Priority] nvarchar(20) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_ProtocolAuditDetails] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProtocolAuditDetails_SafetyAudits_AuditId] FOREIGN KEY ([AuditId]) REFERENCES [SafetyAudits] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProtocolAuditDetails_SafetyProtocols_ProtocolId] FOREIGN KEY ([ProtocolId]) REFERENCES [SafetyProtocols] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ProtocolTrainings] (
    [Id] uniqueidentifier NOT NULL,
    [ProtocolId] uniqueidentifier NOT NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [TrainingDate] datetime2 NOT NULL,
    [TrainingMethod] nvarchar(50) NOT NULL,
    [TrainerName] nvarchar(100) NOT NULL,
    [TrainingHours] decimal(18,4) NOT NULL,
    [TestScore] int NULL,
    [Completed] bit NOT NULL,
    [CompletionStatus] nvarchar(20) NOT NULL,
    [CertificationIssued] bit NOT NULL,
    [ExpirationDate] datetime2 NULL,
    [Notes] nvarchar(500) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ProtocolTrainings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProtocolTrainings_SafetyProtocols_ProtocolId] FOREIGN KEY ([ProtocolId]) REFERENCES [SafetyProtocols] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProtocolTrainings_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ProtocolViolations] (
    [Id] uniqueidentifier NOT NULL,
    [ProtocolId] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [ViolationDate] datetime2 NOT NULL,
    [ViolationType] nvarchar(50) NOT NULL,
    [Severity] nvarchar(20) NOT NULL,
    [ViolationSeverity] nvarchar(20) NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    [RootCause] nvarchar(500) NOT NULL,
    [ImmediateActions] nvarchar(1000) NOT NULL,
    [PreventiveActions] nvarchar(1000) NOT NULL,
    [InjuryOccurred] bit NOT NULL,
    [PropertyDamageOccurred] bit NOT NULL,
    [EstimatedCost] decimal(18,2) NOT NULL,
    [InvestigationStatus] nvarchar(20) NOT NULL,
    [InvestigatorAssigned] nvarchar(100) NOT NULL,
    [TargetCompletionDate] datetime2 NULL,
    [ActualCompletionDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ProtocolViolations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProtocolViolations_SafetyProtocols_ProtocolId] FOREIGN KEY ([ProtocolId]) REFERENCES [SafetyProtocols] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProtocolViolations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [SupplierContacts] (
    [Id] uniqueidentifier NOT NULL,
    [SupplierId] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Title] nvarchar(100) NULL,
    [Department] nvarchar(100) NULL,
    [Phone] nvarchar(50) NULL,
    [Email] nvarchar(100) NULL,
    [IsPrimary] bit NOT NULL,
    [ContactType] nvarchar(50) NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SupplierContacts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SupplierContacts_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SupplierContacts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TaxCalculations] (
    [Id] uniqueidentifier NOT NULL,
    [DocumentType] nvarchar(50) NOT NULL,
    [DocumentId] uniqueidentifier NOT NULL,
    [TaxTypeId] uniqueidentifier NOT NULL,
    [BaseAmount] decimal(18,2) NOT NULL,
    [TaxRate] decimal(18,4) NOT NULL,
    [TaxAmount] decimal(18,2) NOT NULL,
    [CalculationMethod] int NOT NULL,
    [CalculationDate] datetime2 NOT NULL,
    [IsManualOverride] bit NOT NULL,
    [OverrideReason] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TaxCalculations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TaxCalculations_TaxTypes_TaxTypeId] FOREIGN KEY ([TaxTypeId]) REFERENCES [TaxTypes] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TaxCalculations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TaxRates] (
    [Id] uniqueidentifier NOT NULL,
    [TaxTypeId] uniqueidentifier NOT NULL,
    [Rate] decimal(18,4) NOT NULL,
    [EffectiveFrom] datetime2 NOT NULL,
    [EffectiveTo] datetime2 NULL,
    [IsActive] bit NOT NULL,
    [Notes] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TaxRates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TaxRates_TaxTypes_TaxTypeId] FOREIGN KEY ([TaxTypeId]) REFERENCES [TaxTypes] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TaxRates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TaxRules] (
    [Id] uniqueidentifier NOT NULL,
    [TaxTypeId] uniqueidentifier NOT NULL,
    [RuleName] nvarchar(200) NOT NULL,
    [TransactionType] int NULL,
    [CustomerId] uniqueidentifier NULL,
    [SupplierId] uniqueidentifier NULL,
    [ItemCategory] nvarchar(100) NULL,
    [IsDefault] bit NOT NULL,
    [Priority] int NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TaxRules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TaxRules_TaxTypes_TaxTypeId] FOREIGN KEY ([TaxTypeId]) REFERENCES [TaxTypes] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TaxRules_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TaxThresholds] (
    [Id] uniqueidentifier NOT NULL,
    [TaxTypeId] uniqueidentifier NOT NULL,
    [EntityType] nvarchar(50) NOT NULL,
    [EntityId] uniqueidentifier NOT NULL,
    [FiscalYear] int NOT NULL,
    [CumulativeAmount] decimal(18,2) NOT NULL,
    [ThresholdAmount] decimal(18,2) NOT NULL,
    [IsThresholdExceeded] bit NOT NULL,
    [ThresholdExceededDate] datetime2 NULL,
    [LastUpdatedDate] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TaxThresholds] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TaxThresholds_TaxTypes_TaxTypeId] FOREIGN KEY ([TaxTypeId]) REFERENCES [TaxTypes] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TaxThresholds_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Reports] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    [Type] nvarchar(50) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [Query] nvarchar(max) NULL,
    [Parameters] nvarchar(max) NULL,
    [Columns] nvarchar(max) NULL,
    [Visualization] nvarchar(max) NULL,
    [Tags] nvarchar(max) NULL,
    [LastRun] datetime2 NULL,
    [NextRun] datetime2 NULL,
    [IsScheduled] bit NOT NULL,
    [ModuleId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Reports] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Reports_TenantModules_ModuleId] FOREIGN KEY ([ModuleId]) REFERENCES [TenantModules] ([Id]),
    CONSTRAINT [FK_Reports_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [UnitAccounts] (
    [Id] uniqueidentifier NOT NULL,
    [AccountNumber] nvarchar(20) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [UnitTypeId] uniqueidentifier NOT NULL,
    [ParentAccountId] uniqueidentifier NULL,
    [AccountLevel] int NOT NULL,
    [IsPostingAccount] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [CurrentBalance] decimal(18,4) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_UnitAccounts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UnitAccounts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UnitAccounts_UnitAccounts_ParentAccountId] FOREIGN KEY ([ParentAccountId]) REFERENCES [UnitAccounts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UnitAccounts_UnitTypes_UnitTypeId] FOREIGN KEY ([UnitTypeId]) REFERENCES [UnitTypes] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] uniqueidentifier NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserTokens] (
    [UserId] uniqueidentifier NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AuditLogs] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Username] nvarchar(255) NOT NULL,
    [Action] nvarchar(100) NOT NULL,
    [Resource] nvarchar(255) NOT NULL,
    [ResourceId] nvarchar(100) NULL,
    [OldValues] nvarchar(max) NULL,
    [NewValues] nvarchar(max) NULL,
    [IpAddress] nvarchar(45) NOT NULL,
    [UserAgent] nvarchar(500) NULL,
    [Timestamp] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AuditLogs_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AuditLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [BlacklistedTokens] (
    [Id] uniqueidentifier NOT NULL,
    [Jti] nvarchar(100) NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [BlacklistedAt] datetime2 NOT NULL,
    [Reason] nvarchar(200) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_BlacklistedTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BlacklistedTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [BusinessPartners] (
    [Id] uniqueidentifier NOT NULL,
    [PartnerCode] nvarchar(50) NOT NULL,
    [PartnerName] nvarchar(200) NOT NULL,
    [PartnerType] nvarchar(20) NOT NULL,
    [LegalName] nvarchar(200) NULL,
    [BusinessRegistrationNumber] nvarchar(100) NULL,
    [TaxIdentificationNumber] nvarchar(100) NULL,
    [VATNumber] nvarchar(100) NULL,
    [RegistrationDate] datetime2 NULL,
    [IncorporationDate] datetime2 NULL,
    [PrimaryContactName] nvarchar(100) NULL,
    [PrimaryContactTitle] nvarchar(100) NULL,
    [PrimaryEmail] nvarchar(100) NULL,
    [PrimaryPhone] nvarchar(50) NULL,
    [SecondaryPhone] nvarchar(50) NULL,
    [Website] nvarchar(200) NULL,
    [PhysicalAddress] nvarchar(500) NULL,
    [PhysicalCity] nvarchar(100) NULL,
    [PhysicalState] nvarchar(100) NULL,
    [PhysicalCountry] nvarchar(100) NULL,
    [PhysicalPostalCode] nvarchar(20) NULL,
    [MailingAddress] nvarchar(500) NULL,
    [MailingCity] nvarchar(100) NULL,
    [MailingState] nvarchar(100) NULL,
    [MailingCountry] nvarchar(100) NULL,
    [MailingPostalCode] nvarchar(20) NULL,
    [BankName] nvarchar(200) NULL,
    [BankAccountNumber] nvarchar(100) NULL,
    [BankAccountName] nvarchar(200) NULL,
    [BankBranch] nvarchar(200) NULL,
    [BankSwiftCode] nvarchar(50) NULL,
    [BankIBAN] nvarchar(100) NULL,
    [IndustryClassification] nvarchar(100) NULL,
    [CompanySize] nvarchar(50) NULL,
    [GeographicCoverage] nvarchar(200) NULL,
    [RegistrationStatus] nvarchar(50) NOT NULL,
    [ApprovalStatus] nvarchar(50) NULL,
    [ApprovedById] uniqueidentifier NULL,
    [ApprovedDate] datetime2 NULL,
    [RejectionReason] nvarchar(1000) NULL,
    [PerformanceRating] decimal(18,4) NULL,
    [RiskLevel] nvarchar(20) NULL,
    [IsPreferred] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [IsBlacklisted] bit NOT NULL,
    [BlacklistReason] nvarchar(1000) NULL,
    [BlacklistDate] datetime2 NULL,
    [BlacklistExpiryDate] datetime2 NULL,
    [AnnualTurnover] decimal(18,4) NULL,
    [CreditRating] nvarchar(20) NULL,
    [InsuranceCoverage] decimal(18,4) NULL,
    [Notes] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_BusinessPartners] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BusinessPartners_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_BusinessPartners_Users_ApprovedById] FOREIGN KEY ([ApprovedById]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [PasswordResetTokens] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Token] nvarchar(max) NOT NULL,
    [ExpiryTime] datetime2 NOT NULL,
    [IsUsed] bit NOT NULL,
    [UsedAt] datetime2 NULL,
    [RequestedFromIpAddress] nvarchar(max) NULL,
    [RequestedFromUserAgent] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PasswordResetTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PasswordResetTokens_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PasswordResetTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [PurchaseOrders] (
    [Id] uniqueidentifier NOT NULL,
    [OrderNumber] nvarchar(50) NOT NULL,
    [SupplierId] uniqueidentifier NOT NULL,
    [OrderDate] datetime2 NOT NULL,
    [RequiredDate] datetime2 NULL,
    [PromisedDate] datetime2 NULL,
    [ReceivedDate] datetime2 NULL,
    [Status] nvarchar(20) NOT NULL,
    [RequestedById] uniqueidentifier NULL,
    [ApprovedById] uniqueidentifier NULL,
    [ApprovedAt] datetime2 NULL,
    [SubTotal] decimal(18,2) NOT NULL,
    [TaxAmount] decimal(18,2) NOT NULL,
    [ShippingCost] decimal(18,2) NOT NULL,
    [DiscountAmount] decimal(18,2) NOT NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [PaymentTerms] nvarchar(100) NULL,
    [ShippingTerms] nvarchar(100) NULL,
    [Terms] nvarchar(2000) NULL,
    [Notes] nvarchar(2000) NULL,
    [DeliveryWarehouseId] uniqueidentifier NULL,
    [DeliveryAddress] nvarchar(500) NULL,
    [DeliveryInstructions] nvarchar(2000) NULL,
    [SupplierOrderNumber] nvarchar(100) NULL,
    [ReferenceNumber] nvarchar(100) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PurchaseOrders] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PurchaseOrders_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PurchaseOrders_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PurchaseOrders_Users_ApprovedById] FOREIGN KEY ([ApprovedById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_PurchaseOrders_Users_RequestedById] FOREIGN KEY ([RequestedById]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [PurchaseRequisitions] (
    [Id] uniqueidentifier NOT NULL,
    [RequisitionNumber] nvarchar(50) NOT NULL,
    [RequisitionDate] datetime2 NOT NULL,
    [RequestedById] uniqueidentifier NOT NULL,
    [RequiredDate] datetime2 NULL,
    [Status] nvarchar(20) NOT NULL,
    [Priority] nvarchar(50) NOT NULL,
    [Department] nvarchar(100) NULL,
    [CostCenter] nvarchar(100) NULL,
    [Justification] nvarchar(2000) NULL,
    [Notes] nvarchar(2000) NULL,
    [ApprovedById] uniqueidentifier NULL,
    [ApprovedAt] datetime2 NULL,
    [RejectionReason] nvarchar(2000) NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PurchaseRequisitions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PurchaseRequisitions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PurchaseRequisitions_Users_ApprovedById] FOREIGN KEY ([ApprovedById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_PurchaseRequisitions_Users_RequestedById] FOREIGN KEY ([RequestedById]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [RefreshTokens] (
    [Id] uniqueidentifier NOT NULL,
    [TokenHash] nvarchar(255) NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [IsRevoked] bit NOT NULL,
    [RevokedAt] datetime2 NULL,
    [RevokedBy] uniqueidentifier NULL,
    [RevocationReason] nvarchar(200) NULL,
    [LastUsedAt] datetime2 NULL,
    [UsageCount] int NOT NULL,
    [MaxUsageCount] int NOT NULL,
    [IpAddress] nvarchar(45) NULL,
    [UserAgent] nvarchar(500) NULL,
    [DeviceId] nvarchar(100) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RefreshTokens_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ReportDataSources] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Type] int NOT NULL,
    [Host] nvarchar(100) NULL,
    [Port] int NULL,
    [DatabaseName] nvarchar(200) NULL,
    [Username] nvarchar(200) NULL,
    [EncryptedPassword] nvarchar(1000) NULL,
    [AdditionalSettings] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [LastConnectionTest] datetime2 NULL,
    [LastConnectionSuccess] bit NULL,
    [LastConnectionError] nvarchar(1000) NULL,
    [LastUsed] datetime2 NULL,
    [UsageCount] int NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedByUserId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_ReportDataSources] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReportDataSources_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [SecurityAlerts] (
    [Id] uniqueidentifier NOT NULL,
    [Type] nvarchar(50) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Message] nvarchar(1000) NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    [Dismissed] bit NOT NULL,
    [DismissedAt] datetime2 NULL,
    [DismissedBy] nvarchar(max) NULL,
    [Severity] int NOT NULL,
    [Category] nvarchar(50) NOT NULL,
    [Source] nvarchar(100) NOT NULL,
    [AffectedUser] nvarchar(100) NULL,
    [IpAddress] nvarchar(45) NULL,
    [Location] nvarchar(200) NULL,
    [Metadata] nvarchar(2048) NULL,
    [AffectedUserId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SecurityAlerts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SecurityAlerts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SecurityAlerts_Users_AffectedUserId] FOREIGN KEY ([AffectedUserId]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [SecurityLogs] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NULL,
    [Username] nvarchar(255) NULL,
    [Action] nvarchar(100) NOT NULL,
    [IpAddress] nvarchar(45) NOT NULL,
    [UserAgent] nvarchar(500) NULL,
    [Success] bit NOT NULL,
    [Details] nvarchar(1000) NULL,
    [FailureReason] nvarchar(255) NULL,
    [Timestamp] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_SecurityLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SecurityLogs_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SecurityLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [SecurityPolicyViolations] (
    [Id] uniqueidentifier NOT NULL,
    [SecurityPolicyId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NULL,
    [UserName] nvarchar(100) NULL,
    [ViolationType] nvarchar(100) NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    [Severity] int NOT NULL,
    [IpAddress] nvarchar(45) NULL,
    [UserAgent] nvarchar(200) NULL,
    [Location] nvarchar(200) NULL,
    [Metadata] nvarchar(2048) NULL,
    [DetectedAt] datetime2 NOT NULL,
    [IsResolved] bit NOT NULL,
    [ResolvedAt] datetime2 NULL,
    [ResolvedBy] nvarchar(100) NULL,
    [Resolution] nvarchar(1000) NULL,
    [ActionTaken] bit NOT NULL,
    [ActionDescription] nvarchar(500) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_SecurityPolicyViolations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SecurityPolicyViolations_SecurityPolicies_SecurityPolicyId] FOREIGN KEY ([SecurityPolicyId]) REFERENCES [SecurityPolicies] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SecurityPolicyViolations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]),
    CONSTRAINT [FK_SecurityPolicyViolations_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [ThreatDetections] (
    [Id] uniqueidentifier NOT NULL,
    [ThreatType] nvarchar(100) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    [Severity] int NOT NULL,
    [Status] int NOT NULL,
    [IpAddress] nvarchar(45) NULL,
    [UserAgent] nvarchar(200) NULL,
    [Location] nvarchar(200) NULL,
    [AffectedUserId] uniqueidentifier NULL,
    [AffectedUserName] nvarchar(100) NULL,
    [Metadata] nvarchar(2048) NULL,
    [DetectedAt] datetime2 NOT NULL,
    [ResolvedAt] datetime2 NULL,
    [Resolution] nvarchar(1000) NULL,
    [ResolvedBy] nvarchar(100) NULL,
    [RiskScore] int NOT NULL,
    [IsBlocked] bit NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_ThreatDetections] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ThreatDetections_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ThreatDetections_Users_AffectedUserId] FOREIGN KEY ([AffectedUserId]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [UserRoles] (
    [UserId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_UserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [UserSessions] (
    [SessionId] nvarchar(450) NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [LoginTime] datetime2 NOT NULL,
    [LastActivityTime] datetime2 NOT NULL,
    [LogoutTime] datetime2 NULL,
    [IpAddress] nvarchar(500) NOT NULL,
    [UserAgent] nvarchar(1000) NOT NULL,
    [DeviceFingerprint] nvarchar(100) NOT NULL,
    [DeviceType] nvarchar(50) NOT NULL,
    [Browser] nvarchar(100) NOT NULL,
    [OperatingSystem] nvarchar(100) NOT NULL,
    [Location] nvarchar(200) NOT NULL,
    [IsActive] bit NOT NULL,
    [WasTerminatedByConcurrentLogin] bit NOT NULL,
    [TerminationReason] nvarchar(500) NOT NULL,
    [JwtTokenId] nvarchar(100) NULL,
    CONSTRAINT [PK_UserSessions] PRIMARY KEY ([SessionId]),
    CONSTRAINT [FK_UserSessions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserSessions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [UserTenants] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [AccessLevel] int NOT NULL,
    [Status] int NOT NULL,
    [IsDefault] bit NOT NULL,
    [GrantedAt] datetime2 NOT NULL,
    [ExpiresAt] datetime2 NULL,
    [SuspendedAt] datetime2 NULL,
    [ReactivatedAt] datetime2 NULL,
    [GrantedBy] nvarchar(max) NULL,
    [StatusChangedBy] nvarchar(max) NULL,
    [Notes] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_UserTenants] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserTenants_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UserTenants_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [WarehouseLocations] (
    [Id] uniqueidentifier NOT NULL,
    [WarehouseId] uniqueidentifier NOT NULL,
    [LocationCode] nvarchar(100) NOT NULL,
    [Name] nvarchar(200) NULL,
    [Description] nvarchar(1000) NULL,
    [LocationType] nvarchar(50) NOT NULL,
    [ParentLocationId] uniqueidentifier NULL,
    [IsActive] bit NOT NULL,
    [IsPickingLocation] bit NOT NULL,
    [IsReceivingLocation] bit NOT NULL,
    [MaxWeight] decimal(18,4) NULL,
    [MaxVolume] decimal(18,4) NULL,
    [MaxItems] int NULL,
    [CurrentWeight] decimal(18,4) NOT NULL,
    [CurrentVolume] decimal(18,4) NOT NULL,
    [CurrentItemCount] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WarehouseLocations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WarehouseLocations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WarehouseLocations_WarehouseLocations_ParentLocationId] FOREIGN KEY ([ParentLocationId]) REFERENCES [WarehouseLocations] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WarehouseLocations_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [WorkflowDefinitions] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [EntityTypeId] uniqueidentifier NOT NULL,
    [Version] int NOT NULL,
    [IsActive] bit NOT NULL,
    [Configuration] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkflowDefinitions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowDefinitions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkflowDefinitions_WorkflowEntityTypes_EntityTypeId] FOREIGN KEY ([EntityTypeId]) REFERENCES [WorkflowEntityTypes] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AccountSegmentValues] (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [SegmentStructureId] uniqueidentifier NOT NULL,
    [SegmentValue] nvarchar(10) NOT NULL,
    [SegmentLookupValueId] uniqueidentifier NULL,
    [SegmentValueDescription] nvarchar(200) NULL,
    [SegmentPosition] int NOT NULL,
    [IsLocked] bit NOT NULL,
    [EffectiveDate] datetime2 NOT NULL,
    [EndDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AccountSegmentValues] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AccountSegmentValues_AccountSegmentStructures_SegmentStructureId] FOREIGN KEY ([SegmentStructureId]) REFERENCES [AccountSegmentStructures] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AccountSegmentValues_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AccountSegmentValues_SegmentLookupValues_SegmentLookupValueId] FOREIGN KEY ([SegmentLookupValueId]) REFERENCES [SegmentLookupValues] ([Id]),
    CONSTRAINT [FK_AccountSegmentValues_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [StockAdjustments] (
    [Id] uniqueidentifier NOT NULL,
    [AdjustmentNumber] nvarchar(50) NOT NULL,
    [AdjustmentDate] datetime2 NOT NULL,
    [ReasonCode] nvarchar(50) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Status] nvarchar(20) NOT NULL,
    [ApprovedById] uniqueidentifier NULL,
    [ApprovedAt] datetime2 NULL,
    [TotalAdjustmentValue] decimal(18,2) NOT NULL,
    [InventoryItemId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_StockAdjustments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StockAdjustments_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems] ([Id]),
    CONSTRAINT [FK_StockAdjustments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StockAdjustments_Users_ApprovedById] FOREIGN KEY ([ApprovedById]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [SupplierItemCatalogs] (
    [Id] uniqueidentifier NOT NULL,
    [SupplierId] uniqueidentifier NOT NULL,
    [InventoryItemId] uniqueidentifier NOT NULL,
    [SupplierItemCode] nvarchar(100) NULL,
    [SupplierItemName] nvarchar(200) NULL,
    [Description] nvarchar(1000) NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [UnitOfMeasure] nvarchar(20) NULL,
    [MinimumOrderQuantity] decimal(18,4) NOT NULL,
    [LeadTimeDays] int NOT NULL,
    [IsPreferred] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [EffectiveDate] datetime2 NULL,
    [ExpiryDate] datetime2 NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SupplierItemCatalogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SupplierItemCatalogs_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SupplierItemCatalogs_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SupplierItemCatalogs_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [WarehouseQuantities] (
    [Id] uniqueidentifier NOT NULL,
    [InventoryItemId] uniqueidentifier NOT NULL,
    [WarehouseId] uniqueidentifier NOT NULL,
    [CurrentStock] decimal(18,4) NOT NULL,
    [AvailableStock] decimal(18,4) NOT NULL,
    [AllocatedStock] decimal(18,4) NOT NULL,
    [ReorderLevel] decimal(18,4) NOT NULL,
    [MaxStock] decimal(18,4) NOT NULL,
    [AverageCost] decimal(18,2) NOT NULL,
    [LastMovementDate] datetime2 NULL,
    [LastStockTakeDate] datetime2 NULL,
    [NextStockTakeDate] datetime2 NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WarehouseQuantities] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WarehouseQuantities_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_WarehouseQuantities_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WarehouseQuantities_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ReportExecutions] (
    [Id] uniqueidentifier NOT NULL,
    [ReportId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [ExecutedAt] datetime2 NOT NULL,
    [ExecutionTime] time NOT NULL,
    [TotalRows] int NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [ErrorMessage] nvarchar(1000) NULL,
    [Parameters] nvarchar(max) NULL,
    [ResultMetadata] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ReportExecutions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReportExecutions_Reports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [Reports] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ReportExecutions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ReportExecutions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [ReportExports] (
    [Id] uniqueidentifier NOT NULL,
    [ReportId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Format] nvarchar(10) NOT NULL,
    [FileName] nvarchar(500) NOT NULL,
    [FileSize] bigint NOT NULL,
    [ExportedAt] datetime2 NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [ErrorMessage] nvarchar(1000) NULL,
    [Parameters] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ReportExports] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReportExports_Reports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [Reports] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ReportExports_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ReportExports_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [ReportRoleAssignments] (
    [Id] uniqueidentifier NOT NULL,
    [ReportId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [CanRead] bit NOT NULL,
    [CanExecute] bit NOT NULL,
    [CanExport] bit NOT NULL,
    [CanEdit] bit NOT NULL,
    [CanSchedule] bit NOT NULL,
    [AssignedAt] datetime2 NOT NULL,
    [AssignedBy] nvarchar(200) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ReportRoleAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReportRoleAssignments_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ReportRoleAssignments_Reports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [Reports] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ReportRoleAssignments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ReportSchedules] (
    [Id] uniqueidentifier NOT NULL,
    [ReportId] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Frequency] nvarchar(20) NOT NULL,
    [TimeOfDay] time NOT NULL,
    [DayOfWeek] int NULL,
    [DayOfMonth] int NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NULL,
    [NextExecutionDate] datetime2 NULL,
    [LastExecutionDate] datetime2 NULL,
    [EmailRecipients] nvarchar(max) NULL,
    [ExportFormat] nvarchar(10) NULL,
    [Parameters] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ReportSchedules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReportSchedules_Reports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [Reports] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ReportSchedules_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [UserReportFavorites] (
    [ReportId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [FavoritedAt] datetime2 NOT NULL,
    [Id] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_UserReportFavorites] PRIMARY KEY ([ReportId], [UserId]),
    CONSTRAINT [FK_UserReportFavorites_Reports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [Reports] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserReportFavorites_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UserReportFavorites_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AllocationRules] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(30) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [SourceAccountId] uniqueidentifier NOT NULL,
    [AllocationType] int NOT NULL,
    [DriverUnitAccountId] uniqueidentifier NULL,
    [IsActive] bit NOT NULL,
    [AutoReverse] bit NOT NULL,
    [LastRunDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AllocationRules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AllocationRules_Accounts_SourceAccountId] FOREIGN KEY ([SourceAccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AllocationRules_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AllocationRules_UnitAccounts_DriverUnitAccountId] FOREIGN KEY ([DriverUnitAccountId]) REFERENCES [UnitAccounts] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [BusinessPartnerCategories] (
    [Id] uniqueidentifier NOT NULL,
    [BusinessPartnerId] uniqueidentifier NOT NULL,
    [CategoryId] uniqueidentifier NOT NULL,
    [IsPrimary] bit NOT NULL,
    CONSTRAINT [PK_BusinessPartnerCategories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BusinessPartnerCategories_BusinessPartners_BusinessPartnerId] FOREIGN KEY ([BusinessPartnerId]) REFERENCES [BusinessPartners] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_BusinessPartnerCategories_PartnerCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [PartnerCategories] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [BusinessPartnerContacts] (
    [Id] uniqueidentifier NOT NULL,
    [BusinessPartnerId] uniqueidentifier NOT NULL,
    [ContactName] nvarchar(100) NOT NULL,
    [ContactTitle] nvarchar(100) NULL,
    [Department] nvarchar(100) NULL,
    [Email] nvarchar(100) NULL,
    [Phone] nvarchar(50) NULL,
    [Mobile] nvarchar(50) NULL,
    [IsPrimary] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_BusinessPartnerContacts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BusinessPartnerContacts_BusinessPartners_BusinessPartnerId] FOREIGN KEY ([BusinessPartnerId]) REFERENCES [BusinessPartners] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_BusinessPartnerContacts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [BusinessPartnerDocuments] (
    [Id] uniqueidentifier NOT NULL,
    [BusinessPartnerId] uniqueidentifier NOT NULL,
    [DocumentType] nvarchar(100) NOT NULL,
    [DocumentName] nvarchar(200) NOT NULL,
    [DocumentPath] nvarchar(500) NOT NULL,
    [FileSize] bigint NULL,
    [MimeType] nvarchar(100) NULL,
    [IssueDate] datetime2 NULL,
    [ExpiryDate] datetime2 NULL,
    [IsVerified] bit NOT NULL,
    [VerifiedById] uniqueidentifier NULL,
    [VerifiedDate] datetime2 NULL,
    [VerificationNotes] nvarchar(1000) NULL,
    [UploadedById] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_BusinessPartnerDocuments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BusinessPartnerDocuments_BusinessPartners_BusinessPartnerId] FOREIGN KEY ([BusinessPartnerId]) REFERENCES [BusinessPartners] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_BusinessPartnerDocuments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_BusinessPartnerDocuments_Users_UploadedById] FOREIGN KEY ([UploadedById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_BusinessPartnerDocuments_Users_VerifiedById] FOREIGN KEY ([VerifiedById]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [BusinessPartnerFinancials] (
    [Id] uniqueidentifier NOT NULL,
    [BusinessPartnerId] uniqueidentifier NOT NULL,
    [FiscalYear] int NOT NULL,
    [AnnualRevenue] decimal(18,4) NULL,
    [NetProfit] decimal(18,4) NULL,
    [TotalAssets] decimal(18,2) NULL,
    [TotalLiabilities] decimal(18,2) NULL,
    [CreditRating] nvarchar(20) NULL,
    [FinancialStatementPath] nvarchar(500) NULL,
    [AuditorName] nvarchar(200) NULL,
    [AuditDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_BusinessPartnerFinancials] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BusinessPartnerFinancials_BusinessPartners_BusinessPartnerId] FOREIGN KEY ([BusinessPartnerId]) REFERENCES [BusinessPartners] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_BusinessPartnerFinancials_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [BusinessPartnerLicenses] (
    [Id] uniqueidentifier NOT NULL,
    [BusinessPartnerId] uniqueidentifier NOT NULL,
    [LicenseTypeId] uniqueidentifier NOT NULL,
    [LicenseNumber] nvarchar(100) NOT NULL,
    [IssuingAuthority] nvarchar(200) NULL,
    [IssueDate] datetime2 NULL,
    [ExpiryDate] datetime2 NULL,
    [Status] nvarchar(50) NOT NULL,
    [DocumentPath] nvarchar(500) NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_BusinessPartnerLicenses] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BusinessPartnerLicenses_BusinessPartners_BusinessPartnerId] FOREIGN KEY ([BusinessPartnerId]) REFERENCES [BusinessPartners] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_BusinessPartnerLicenses_LicenseTypes_LicenseTypeId] FOREIGN KEY ([LicenseTypeId]) REFERENCES [LicenseTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_BusinessPartnerLicenses_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [BusinessPartnerRegistrations] (
    [Id] uniqueidentifier NOT NULL,
    [RegistrationNumber] nvarchar(50) NOT NULL,
    [ApplicantName] nvarchar(200) NOT NULL,
    [ApplicantEmail] nvarchar(100) NOT NULL,
    [ApplicantPhone] nvarchar(50) NULL,
    [PartnerType] nvarchar(20) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [SubmittedDate] datetime2 NULL,
    [ReviewedDate] datetime2 NULL,
    [ReviewedById] uniqueidentifier NULL,
    [ApprovedDate] datetime2 NULL,
    [ApprovedById] uniqueidentifier NULL,
    [RegistrationDataJson] nvarchar(max) NULL,
    [BusinessPartnerId] uniqueidentifier NULL,
    [ApplicantNotes] nvarchar(max) NULL,
    [InternalNotes] nvarchar(max) NULL,
    [RejectionReason] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_BusinessPartnerRegistrations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BusinessPartnerRegistrations_BusinessPartners_BusinessPartnerId] FOREIGN KEY ([BusinessPartnerId]) REFERENCES [BusinessPartners] ([Id]),
    CONSTRAINT [FK_BusinessPartnerRegistrations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_BusinessPartnerRegistrations_Users_ApprovedById] FOREIGN KEY ([ApprovedById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_BusinessPartnerRegistrations_Users_ReviewedById] FOREIGN KEY ([ReviewedById]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [BusinessPartnerSpecializations] (
    [Id] uniqueidentifier NOT NULL,
    [BusinessPartnerId] uniqueidentifier NOT NULL,
    [SpecializationId] uniqueidentifier NOT NULL,
    [YearsOfExperience] int NULL,
    [IsPrimary] bit NOT NULL,
    CONSTRAINT [PK_BusinessPartnerSpecializations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BusinessPartnerSpecializations_BusinessPartners_BusinessPartnerId] FOREIGN KEY ([BusinessPartnerId]) REFERENCES [BusinessPartners] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_BusinessPartnerSpecializations_ContractorSpecializations_SpecializationId] FOREIGN KEY ([SpecializationId]) REFERENCES [ContractorSpecializations] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [PurchaseOrderItems] (
    [Id] uniqueidentifier NOT NULL,
    [PurchaseOrderId] uniqueidentifier NOT NULL,
    [InventoryItemId] uniqueidentifier NOT NULL,
    [SupplierItemCode] nvarchar(100) NULL,
    [ItemDescription] nvarchar(200) NULL,
    [OrderedQuantity] decimal(18,4) NOT NULL,
    [ReceivedQuantity] decimal(18,4) NOT NULL,
    [RemainingQuantity] decimal(18,4) NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [LineTotal] decimal(18,2) NOT NULL,
    [ExpectedDeliveryDate] datetime2 NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PurchaseOrderItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PurchaseOrderItems_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PurchaseOrderItems_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrders] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PurchaseOrderItems_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [PurchaseOrderReceipts] (
    [Id] uniqueidentifier NOT NULL,
    [PurchaseOrderId] uniqueidentifier NOT NULL,
    [ReceiptNumber] nvarchar(50) NOT NULL,
    [ReceiptDate] datetime2 NOT NULL,
    [DeliveryNote] nvarchar(100) NULL,
    [CarrierName] nvarchar(100) NULL,
    [TrackingNumber] nvarchar(100) NULL,
    [ReceivedById] uniqueidentifier NULL,
    [InspectedById] uniqueidentifier NULL,
    [Status] nvarchar(20) NOT NULL,
    [Notes] nvarchar(2000) NULL,
    [RequiresInspection] bit NOT NULL,
    [InspectionDate] datetime2 NULL,
    [InspectionResult] nvarchar(50) NULL,
    [InspectionNotes] nvarchar(2000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PurchaseOrderReceipts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PurchaseOrderReceipts_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrders] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PurchaseOrderReceipts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PurchaseOrderReceipts_Users_InspectedById] FOREIGN KEY ([InspectedById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_PurchaseOrderReceipts_Users_ReceivedById] FOREIGN KEY ([ReceivedById]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [PurchaseRequisitionItems] (
    [Id] uniqueidentifier NOT NULL,
    [RequisitionId] uniqueidentifier NOT NULL,
    [InventoryItemId] uniqueidentifier NULL,
    [ItemDescription] nvarchar(200) NOT NULL,
    [Quantity] decimal(18,4) NOT NULL,
    [UnitOfMeasure] nvarchar(20) NOT NULL,
    [EstimatedUnitPrice] decimal(18,2) NOT NULL,
    [LineTotal] decimal(18,2) NOT NULL,
    [RequiredDate] datetime2 NULL,
    [PreferredSupplierId] uniqueidentifier NULL,
    [Notes] nvarchar(1000) NULL,
    [Specifications] nvarchar(1000) NULL,
    [Status] nvarchar(20) NOT NULL,
    [PurchaseOrderId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PurchaseRequisitionItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PurchaseRequisitionItems_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems] ([Id]),
    CONSTRAINT [FK_PurchaseRequisitionItems_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrders] ([Id]),
    CONSTRAINT [FK_PurchaseRequisitionItems_PurchaseRequisitions_RequisitionId] FOREIGN KEY ([RequisitionId]) REFERENCES [PurchaseRequisitions] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PurchaseRequisitionItems_Suppliers_PreferredSupplierId] FOREIGN KEY ([PreferredSupplierId]) REFERENCES [Suppliers] ([Id]),
    CONSTRAINT [FK_PurchaseRequisitionItems_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ReportDataSourceUsageLogs] (
    [Id] uniqueidentifier NOT NULL,
    [DataSourceId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [AccessedAt] datetime2 NOT NULL,
    [OperationType] nvarchar(100) NULL,
    [ExecutionTime] time NULL,
    [QueryExecuted] nvarchar(2000) NULL,
    [RecordsReturned] int NULL,
    [Success] bit NOT NULL,
    [ErrorMessage] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_ReportDataSourceUsageLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReportDataSourceUsageLogs_ReportDataSources_DataSourceId] FOREIGN KEY ([DataSourceId]) REFERENCES [ReportDataSources] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ThreatIndicators] (
    [Id] uniqueidentifier NOT NULL,
    [IndicatorType] nvarchar(100) NOT NULL,
    [Value] nvarchar(500) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Confidence] int NOT NULL,
    [IsActive] bit NOT NULL,
    [FirstSeen] datetime2 NOT NULL,
    [LastSeen] datetime2 NOT NULL,
    [HitCount] int NOT NULL,
    [ThreatDetectionId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_ThreatIndicators] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ThreatIndicators_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]),
    CONSTRAINT [FK_ThreatIndicators_ThreatDetections_ThreatDetectionId] FOREIGN KEY ([ThreatDetectionId]) REFERENCES [ThreatDetections] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [InventoryAllocations] (
    [Id] uniqueidentifier NOT NULL,
    [InventoryItemId] uniqueidentifier NOT NULL,
    [WarehouseId] uniqueidentifier NOT NULL,
    [LocationId] uniqueidentifier NULL,
    [AllocationType] nvarchar(50) NOT NULL,
    [ReferenceNumber] nvarchar(50) NULL,
    [ReferenceId] uniqueidentifier NULL,
    [AllocatedQuantity] decimal(18,4) NOT NULL,
    [ConsumedQuantity] decimal(18,4) NOT NULL,
    [RemainingQuantity] decimal(18,4) NOT NULL,
    [AllocationDate] datetime2 NOT NULL,
    [RequiredDate] datetime2 NULL,
    [ExpirationDate] datetime2 NULL,
    [Status] nvarchar(20) NOT NULL,
    [SerialNumber] nvarchar(100) NULL,
    [LotNumber] nvarchar(100) NULL,
    [Notes] nvarchar(1000) NULL,
    [AllocatedById] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InventoryAllocations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InventoryAllocations_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InventoryAllocations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InventoryAllocations_Users_AllocatedById] FOREIGN KEY ([AllocatedById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_InventoryAllocations_WarehouseLocations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [WarehouseLocations] ([Id]),
    CONSTRAINT [FK_InventoryAllocations_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [InventoryLocations] (
    [Id] uniqueidentifier NOT NULL,
    [InventoryItemId] uniqueidentifier NOT NULL,
    [LocationId] uniqueidentifier NOT NULL,
    [Quantity] decimal(18,4) NOT NULL,
    [AllocatedQuantity] decimal(18,4) NOT NULL,
    [AvailableQuantity] decimal(18,4) NOT NULL,
    [AverageCost] decimal(18,2) NOT NULL,
    [LastMovementDate] datetime2 NULL,
    [LastCountDate] datetime2 NULL,
    [NextCountDate] datetime2 NULL,
    [CountFrequencyDays] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InventoryLocations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InventoryLocations_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InventoryLocations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InventoryLocations_WarehouseLocations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [WarehouseLocations] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [StockMovements] (
    [Id] uniqueidentifier NOT NULL,
    [InventoryItemId] uniqueidentifier NOT NULL,
    [MovementType] nvarchar(50) NOT NULL,
    [Quantity] decimal(18,4) NOT NULL,
    [UnitCost] decimal(18,2) NOT NULL,
    [TotalValue] decimal(18,2) NOT NULL,
    [MovementDate] datetime2 NOT NULL,
    [ReferenceType] int NOT NULL,
    [ReferenceNumber] nvarchar(50) NULL,
    [ReferenceId] uniqueidentifier NULL,
    [LocationId] uniqueidentifier NULL,
    [Notes] nvarchar(1000) NULL,
    [SerialNumber] nvarchar(100) NULL,
    [LotNumber] nvarchar(100) NULL,
    [ExpirationDate] datetime2 NULL,
    [RunningBalance] decimal(18,4) NOT NULL,
    [RunningValue] decimal(18,4) NOT NULL,
    [ProcessedById] uniqueidentifier NULL,
    [ApprovedById] uniqueidentifier NULL,
    [ApprovedAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_StockMovements] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StockMovements_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StockMovements_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StockMovements_Users_ApprovedById] FOREIGN KEY ([ApprovedById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_StockMovements_Users_ProcessedById] FOREIGN KEY ([ProcessedById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_StockMovements_WarehouseLocations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [WarehouseLocations] ([Id]) ON DELETE SET NULL
);
GO

CREATE TABLE [WorkflowSteps] (
    [Id] uniqueidentifier NOT NULL,
    [WorkflowDefinitionId] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [StepType] int NOT NULL,
    [Order] int NOT NULL,
    [IsStartStep] bit NOT NULL,
    [IsEndStep] bit NOT NULL,
    [AssignmentType] nvarchar(50) NULL,
    [AssignmentConfiguration] nvarchar(max) NULL,
    [IsRequired] bit NOT NULL,
    [RequiredRole] nvarchar(100) NULL,
    [EstimatedHours] decimal(5,2) NULL,
    [Configuration] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkflowSteps] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowSteps_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkflowSteps_WorkflowDefinitions_WorkflowDefinitionId] FOREIGN KEY ([WorkflowDefinitionId]) REFERENCES [WorkflowDefinitions] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [StockAdjustmentItems] (
    [Id] uniqueidentifier NOT NULL,
    [AdjustmentId] uniqueidentifier NOT NULL,
    [InventoryItemId] uniqueidentifier NOT NULL,
    [LocationId] uniqueidentifier NULL,
    [SerialNumber] nvarchar(100) NULL,
    [LotNumber] nvarchar(100) NULL,
    [SystemQuantity] decimal(18,4) NOT NULL,
    [PhysicalQuantity] decimal(18,4) NOT NULL,
    [AdjustmentQuantity] decimal(18,4) NOT NULL,
    [UnitCost] decimal(18,2) NOT NULL,
    [AdjustmentValue] decimal(18,4) NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_StockAdjustmentItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StockAdjustmentItems_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StockAdjustmentItems_StockAdjustments_AdjustmentId] FOREIGN KEY ([AdjustmentId]) REFERENCES [StockAdjustments] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_StockAdjustmentItems_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StockAdjustmentItems_WarehouseLocations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [WarehouseLocations] ([Id]) ON DELETE SET NULL
);
GO

CREATE TABLE [AllocationTargets] (
    [Id] uniqueidentifier NOT NULL,
    [AllocationRuleId] uniqueidentifier NOT NULL,
    [TargetAccountId] uniqueidentifier NOT NULL,
    [FixedPercentage] decimal(18,4) NULL,
    [TargetDriverUnitAccountId] uniqueidentifier NULL,
    [CostCenterCode] nvarchar(50) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AllocationTargets] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AllocationTargets_Accounts_TargetAccountId] FOREIGN KEY ([TargetAccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AllocationTargets_AllocationRules_AllocationRuleId] FOREIGN KEY ([AllocationRuleId]) REFERENCES [AllocationRules] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AllocationTargets_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AllocationTargets_UnitAccounts_TargetDriverUnitAccountId] FOREIGN KEY ([TargetDriverUnitAccountId]) REFERENCES [UnitAccounts] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [BusinessPartnerRegistrationDocuments] (
    [Id] uniqueidentifier NOT NULL,
    [RegistrationId] uniqueidentifier NOT NULL,
    [DocumentType] nvarchar(100) NOT NULL,
    [DocumentName] nvarchar(200) NOT NULL,
    [DocumentPath] nvarchar(500) NOT NULL,
    [FileSize] bigint NULL,
    [MimeType] nvarchar(100) NULL,
    [IsVerified] bit NOT NULL,
    [VerifiedById] uniqueidentifier NULL,
    [VerifiedDate] datetime2 NULL,
    [VerificationNotes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_BusinessPartnerRegistrationDocuments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BusinessPartnerRegistrationDocuments_BusinessPartnerRegistrations_RegistrationId] FOREIGN KEY ([RegistrationId]) REFERENCES [BusinessPartnerRegistrations] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_BusinessPartnerRegistrationDocuments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_BusinessPartnerRegistrationDocuments_Users_VerifiedById] FOREIGN KEY ([VerifiedById]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [BusinessPartnerRegistrationStatusHistories] (
    [Id] uniqueidentifier NOT NULL,
    [RegistrationId] uniqueidentifier NOT NULL,
    [FromStatus] nvarchar(50) NULL,
    [ToStatus] nvarchar(50) NOT NULL,
    [ChangedById] uniqueidentifier NULL,
    [ChangedAt] datetime2 NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_BusinessPartnerRegistrationStatusHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BusinessPartnerRegistrationStatusHistories_BusinessPartnerRegistrations_RegistrationId] FOREIGN KEY ([RegistrationId]) REFERENCES [BusinessPartnerRegistrations] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_BusinessPartnerRegistrationStatusHistories_Users_ChangedById] FOREIGN KEY ([ChangedById]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [PurchaseOrderReceiptItems] (
    [Id] uniqueidentifier NOT NULL,
    [ReceiptId] uniqueidentifier NOT NULL,
    [PurchaseOrderItemId] uniqueidentifier NOT NULL,
    [ReceivedQuantity] decimal(18,4) NOT NULL,
    [AcceptedQuantity] decimal(18,4) NOT NULL,
    [RejectedQuantity] decimal(18,4) NOT NULL,
    [LocationId] uniqueidentifier NULL,
    [SerialNumber] nvarchar(100) NULL,
    [LotNumber] nvarchar(100) NULL,
    [ExpirationDate] datetime2 NULL,
    [Notes] nvarchar(1000) NULL,
    [RejectionReason] nvarchar(1000) NULL,
    [QualityStatus] nvarchar(50) NULL,
    [QualityNotes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PurchaseOrderReceiptItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PurchaseOrderReceiptItems_PurchaseOrderItems_PurchaseOrderItemId] FOREIGN KEY ([PurchaseOrderItemId]) REFERENCES [PurchaseOrderItems] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PurchaseOrderReceiptItems_PurchaseOrderReceipts_ReceiptId] FOREIGN KEY ([ReceiptId]) REFERENCES [PurchaseOrderReceipts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PurchaseOrderReceiptItems_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [WorkflowInstances] (
    [Id] uniqueidentifier NOT NULL,
    [WorkflowDefinitionId] uniqueidentifier NOT NULL,
    [EntityId] uniqueidentifier NOT NULL,
    [EntityTypeId] uniqueidentifier NOT NULL,
    [Status] int NOT NULL,
    [Priority] int NOT NULL,
    [InitiatedById] uniqueidentifier NOT NULL,
    [StartedById] uniqueidentifier NULL,
    [CreatedDate] datetime2 NOT NULL,
    [StartedDate] datetime2 NULL,
    [CompletedDate] datetime2 NULL,
    [CancelledDate] datetime2 NULL,
    [DataContext] nvarchar(max) NULL,
    [Data] nvarchar(max) NULL,
    [Notes] nvarchar(max) NULL,
    [CurrentStepId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkflowInstances] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowInstances_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkflowInstances_Users_InitiatedById] FOREIGN KEY ([InitiatedById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_WorkflowInstances_WorkflowDefinitions_WorkflowDefinitionId] FOREIGN KEY ([WorkflowDefinitionId]) REFERENCES [WorkflowDefinitions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkflowInstances_WorkflowEntityTypes_EntityTypeId] FOREIGN KEY ([EntityTypeId]) REFERENCES [WorkflowEntityTypes] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_WorkflowInstances_WorkflowSteps_CurrentStepId] FOREIGN KEY ([CurrentStepId]) REFERENCES [WorkflowSteps] ([Id])
);
GO

CREATE TABLE [WorkflowTransitions] (
    [Id] uniqueidentifier NOT NULL,
    [WorkflowDefinitionId] uniqueidentifier NOT NULL,
    [FromStepId] uniqueidentifier NOT NULL,
    [ToStepId] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [Condition] nvarchar(max) NULL,
    [IsDefault] bit NOT NULL,
    [Priority] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkflowTransitions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowTransitions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkflowTransitions_WorkflowDefinitions_WorkflowDefinitionId] FOREIGN KEY ([WorkflowDefinitionId]) REFERENCES [WorkflowDefinitions] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_WorkflowTransitions_WorkflowSteps_FromStepId] FOREIGN KEY ([FromStepId]) REFERENCES [WorkflowSteps] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkflowTransitions_WorkflowSteps_ToStepId] FOREIGN KEY ([ToStepId]) REFERENCES [WorkflowSteps] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [WorkflowStepInstances] (
    [Id] uniqueidentifier NOT NULL,
    [WorkflowInstanceId] uniqueidentifier NOT NULL,
    [WorkflowStepId] uniqueidentifier NOT NULL,
    [Status] int NOT NULL,
    [AssignedToId] uniqueidentifier NULL,
    [CreatedDate] datetime2 NOT NULL,
    [StartedDate] datetime2 NULL,
    [CompletedDate] datetime2 NULL,
    [DueDate] datetime2 NULL,
    [ResultData] nvarchar(max) NULL,
    [Comments] nvarchar(max) NULL,
    [RetryCount] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkflowStepInstances] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowStepInstances_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkflowStepInstances_Users_AssignedToId] FOREIGN KEY ([AssignedToId]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_WorkflowStepInstances_WorkflowInstances_WorkflowInstanceId] FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [WorkflowInstances] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_WorkflowStepInstances_WorkflowSteps_WorkflowStepId] FOREIGN KEY ([WorkflowStepId]) REFERENCES [WorkflowSteps] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [WorkflowActivityLogs] (
    [Id] uniqueidentifier NOT NULL,
    [WorkflowInstanceId] uniqueidentifier NOT NULL,
    [StepInstanceId] uniqueidentifier NULL,
    [ActivityType] int NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(max) NULL,
    [Data] nvarchar(max) NULL,
    [PerformedById] uniqueidentifier NULL,
    [ActivityDate] datetime2 NOT NULL,
    [IpAddress] nvarchar(45) NULL,
    [UserAgent] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkflowActivityLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowActivityLogs_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkflowActivityLogs_Users_PerformedById] FOREIGN KEY ([PerformedById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_WorkflowActivityLogs_WorkflowInstances_WorkflowInstanceId] FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [WorkflowInstances] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_WorkflowActivityLogs_WorkflowStepInstances_StepInstanceId] FOREIGN KEY ([StepInstanceId]) REFERENCES [WorkflowStepInstances] ([Id])
);
GO

CREATE TABLE [WorkflowApprovals] (
    [Id] uniqueidentifier NOT NULL,
    [StepInstanceId] uniqueidentifier NOT NULL,
    [ApproverId] uniqueidentifier NULL,
    [ApproverRole] nvarchar(100) NULL,
    [Status] int NOT NULL,
    [RequestedDate] datetime2 NOT NULL,
    [ProcessedDate] datetime2 NULL,
    [DueDate] datetime2 NULL,
    [Comments] nvarchar(max) NULL,
    [ProcessedById] uniqueidentifier NULL,
    [Priority] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkflowApprovals] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowApprovals_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkflowApprovals_Users_ApproverId] FOREIGN KEY ([ApproverId]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_WorkflowApprovals_Users_ProcessedById] FOREIGN KEY ([ProcessedById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_WorkflowApprovals_WorkflowStepInstances_StepInstanceId] FOREIGN KEY ([StepInstanceId]) REFERENCES [WorkflowStepInstances] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AccountBalances] (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [FiscalPeriodId] uniqueidentifier NOT NULL,
    [BookClassification] nvarchar(20) NOT NULL,
    [Currency] nvarchar(3) NULL,
    [OpeningBalance] decimal(18,4) NOT NULL,
    [OpeningBalanceType] nvarchar(2) NOT NULL,
    [PeriodDebits] decimal(18,4) NOT NULL,
    [PeriodCredits] decimal(18,4) NOT NULL,
    [PeriodNetMovement] decimal(18,4) NOT NULL,
    [ClosingBalance] decimal(18,4) NOT NULL,
    [ClosingBalanceType] nvarchar(2) NOT NULL,
    [YearToDateDebits] decimal(18,4) NOT NULL,
    [YearToDateCredits] decimal(18,4) NOT NULL,
    [YearToDateNetMovement] decimal(18,4) NOT NULL,
    [SegmentString] nvarchar(200) NULL,
    [DepartmentSegment] nvarchar(20) NULL,
    [CostCenterSegment] nvarchar(20) NULL,
    [ProjectSegment] nvarchar(20) NULL,
    [LocationSegment] nvarchar(20) NULL,
    [ExchangeRate] decimal(18,4) NULL,
    [BaseCurrencyEquivalent] decimal(18,4) NULL,
    [UnrealizedGainLoss] decimal(18,4) NULL,
    [TransactionCount] int NOT NULL,
    [LastTransactionDate] datetime2 NULL,
    [LastTransactionUserId] uniqueidentifier NULL,
    [LastUpdated] datetime2 NOT NULL,
    [IsReconciled] bit NOT NULL,
    [LastReconciledDate] datetime2 NULL,
    [ReconciliationDiscrepancy] decimal(18,4) NULL,
    [IsLocked] bit NOT NULL,
    [LockedDate] datetime2 NULL,
    [LockedByUserId] uniqueidentifier NULL,
    [HasActivity] bit NOT NULL,
    [IsZeroBalance] bit NOT NULL,
    [IsNegativeBalance] bit NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [ReferenceNumber] nvarchar(50) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [EffectiveDate] datetime2 NULL,
    [ExpirationDate] datetime2 NULL,
    [Metadata] nvarchar(max) NULL,
    [Tags] nvarchar(500) NULL,
    [Priority] int NOT NULL,
    CONSTRAINT [PK_AccountBalances] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AccountBalances_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AccountBalances_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AccountTransactions] (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [JournalEntryId] uniqueidentifier NOT NULL,
    [TransactionDate] datetime2 NOT NULL,
    [Description] nvarchar(500) NULL,
    [DebitAmount] decimal(18,2) NOT NULL,
    [CreditAmount] decimal(18,2) NOT NULL,
    [TransactionCurrency] nvarchar(3) NULL,
    [ForeignCurrencyAmount] decimal(18,2) NULL,
    [ExchangeRate] decimal(18,4) NULL,
    [ExchangeRateSource] nvarchar(100) NULL,
    [ExchangeRateDate] datetime2 NULL,
    [SourceModule] nvarchar(50) NULL,
    [SourceDocumentId] uniqueidentifier NULL,
    [SourceDocumentType] nvarchar(100) NULL,
    [SourceReferenceNumber] nvarchar(100) NULL,
    [BookClassification] nvarchar(20) NOT NULL,
    [FiscalPeriodId] uniqueidentifier NOT NULL,
    [PostedDate] datetime2 NULL,
    [PostingStatus] nvarchar(20) NOT NULL,
    [IsReversed] bit NOT NULL,
    [ReversalDate] datetime2 NULL,
    [ReversalTransactionId] uniqueidentifier NULL,
    [OriginalTransactionId] uniqueidentifier NULL,
    [ReversalType] nvarchar(20) NULL,
    [ReversalReason] nvarchar(500) NULL,
    [SegmentString] nvarchar(200) NULL,
    [IsRevaluationEntry] bit NOT NULL,
    [RevaluationBatchNumber] nvarchar(50) NULL,
    [RevaluationType] nvarchar(20) NULL,
    [LineNumber] int NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [TransactionTag] nvarchar(50) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [ReferenceNumber] nvarchar(50) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [EffectiveDate] datetime2 NULL,
    [ExpirationDate] datetime2 NULL,
    [Metadata] nvarchar(max) NULL,
    [Tags] nvarchar(500) NULL,
    [Priority] int NOT NULL,
    CONSTRAINT [PK_AccountTransactions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AccountTransactions_AccountTransactions_OriginalTransactionId] FOREIGN KEY ([OriginalTransactionId]) REFERENCES [AccountTransactions] ([Id]),
    CONSTRAINT [FK_AccountTransactions_AccountTransactions_ReversalTransactionId] FOREIGN KEY ([ReversalTransactionId]) REFERENCES [AccountTransactions] ([Id]),
    CONSTRAINT [FK_AccountTransactions_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AccountTransactions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AssetAdmissions] (
    [Id] uniqueidentifier NOT NULL,
    [AdmissionNumber] nvarchar(50) NOT NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [JobCardId] uniqueidentifier NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [AdmissionDate] datetime2 NOT NULL,
    [AdmittedById] uniqueidentifier NOT NULL,
    [AdmissionType] nvarchar(20) NOT NULL,
    [AssetConditionOnAdmission] nvarchar(20) NOT NULL,
    [AdmissionNotes] nvarchar(2000) NULL,
    [ObservedProblems] nvarchar(2000) NULL,
    [MileageReading] decimal(18,4) NULL,
    [HoursReading] decimal(18,4) NULL,
    [FuelLevel] decimal(18,4) NULL,
    [AdmissionChecklist] nvarchar(max) NULL,
    [PhotoPaths] nvarchar(max) NULL,
    [DocumentPaths] nvarchar(max) NULL,
    [AdmissionLocation] nvarchar(100) NULL,
    [BayOrStation] nvarchar(100) NULL,
    [EstimatedCompletionDate] datetime2 NULL,
    [EstimatedDischargeDate] datetime2 NULL,
    [Status] nvarchar(20) NOT NULL,
    [DischargeId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AssetAdmissions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetAdmissions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AssetAdmissions_Users_AdmittedById] FOREIGN KEY ([AdmittedById]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [AssetDischarges] (
    [Id] uniqueidentifier NOT NULL,
    [DischargeNumber] nvarchar(50) NOT NULL,
    [AdmissionId] uniqueidentifier NOT NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [JobCardId] uniqueidentifier NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [DischargeDate] datetime2 NOT NULL,
    [DischargedById] uniqueidentifier NOT NULL,
    [AssetConditionOnDischarge] nvarchar(20) NOT NULL,
    [DischargeNotes] nvarchar(2000) NULL,
    [WorkCompleted] nvarchar(2000) NULL,
    [RemainingIssues] nvarchar(2000) NULL,
    [MileageReading] decimal(18,4) NULL,
    [HoursReading] decimal(18,4) NULL,
    [FuelLevel] decimal(18,4) NULL,
    [QualityCheckPassed] bit NOT NULL,
    [QualityCheckedById] uniqueidentifier NULL,
    [QualityCheckDate] datetime2 NULL,
    [QualityCheckNotes] nvarchar(1000) NULL,
    [DischargeChecklist] nvarchar(max) NULL,
    [PhotoPaths] nvarchar(max) NULL,
    [DocumentPaths] nvarchar(max) NULL,
    [CertificateGenerated] bit NOT NULL,
    [CertificateGeneratedDate] datetime2 NULL,
    [CertificatePath] nvarchar(500) NULL,
    [CustomerAcceptance] bit NOT NULL,
    [AcceptedById] uniqueidentifier NULL,
    [AcceptedDate] datetime2 NULL,
    [AcceptanceNotes] nvarchar(1000) NULL,
    [RequiresFollowUp] bit NOT NULL,
    [FollowUpDate] datetime2 NULL,
    [FollowUpInstructions] nvarchar(1000) NULL,
    [WarrantyDays] int NOT NULL,
    [WarrantyExpiration] datetime2 NULL,
    [WarrantyTerms] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AssetDischarges] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetDischarges_AssetAdmissions_AdmissionId] FOREIGN KEY ([AdmissionId]) REFERENCES [AssetAdmissions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AssetDischarges_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AssetDischarges_Users_AcceptedById] FOREIGN KEY ([AcceptedById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_AssetDischarges_Users_DischargedById] FOREIGN KEY ([DischargedById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_AssetDischarges_Users_QualityCheckedById] FOREIGN KEY ([QualityCheckedById]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [AssetDowntimes] (
    [Id] uniqueidentifier NOT NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [StartTime] datetime2 NOT NULL,
    [EndTime] datetime2 NULL,
    [DowntimeHours] float NULL,
    [Reason] nvarchar(50) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [EstimatedCostImpact] decimal(18,2) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AssetDowntimes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetDowntimes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AssetInspections] (
    [Id] uniqueidentifier NOT NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [InspectionTemplateId] uniqueidentifier NOT NULL,
    [InspectorId] uniqueidentifier NOT NULL,
    [InspectionDate] datetime2 NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [OverallResult] nvarchar(20) NULL,
    [InspectionData] nvarchar(max) NOT NULL,
    [Notes] nvarchar(2000) NULL,
    [RecommendedActions] nvarchar(2000) NULL,
    [NextInspectionDue] datetime2 NULL,
    [IsRegulatoryRequired] bit NOT NULL,
    [RegulatoryStandard] nvarchar(100) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AssetInspections] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetInspections_InspectionTemplates_InspectionTemplateId] FOREIGN KEY ([InspectionTemplateId]) REFERENCES [InspectionTemplates] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AssetInspections_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [InspectionDocuments] (
    [Id] uniqueidentifier NOT NULL,
    [InspectionId] uniqueidentifier NOT NULL,
    [FileName] nvarchar(200) NOT NULL,
    [FilePath] nvarchar(500) NOT NULL,
    [FileType] nvarchar(50) NULL,
    [FileSize] bigint NOT NULL,
    [DocumentType] nvarchar(50) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InspectionDocuments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InspectionDocuments_AssetInspections_InspectionId] FOREIGN KEY ([InspectionId]) REFERENCES [AssetInspections] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InspectionDocuments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AssetMaintenanceDowntimes] (
    [Id] uniqueidentifier NOT NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [AdmissionId] uniqueidentifier NOT NULL,
    [DischargeId] uniqueidentifier NULL,
    [JobCardId] uniqueidentifier NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [DowntimeStart] datetime2 NOT NULL,
    [DowntimeEnd] datetime2 NULL,
    [DowntimeMinutes] int NOT NULL,
    [DowntimeType] nvarchar(50) NOT NULL,
    [Priority] nvarchar(20) NOT NULL,
    [EstimatedCostImpact] decimal(18,2) NOT NULL,
    [ActualCostImpact] decimal(18,2) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AssetMaintenanceDowntimes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetMaintenanceDowntimes_AssetAdmissions_AdmissionId] FOREIGN KEY ([AdmissionId]) REFERENCES [AssetAdmissions] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AssetMaintenanceDowntimes_AssetDischarges_DischargeId] FOREIGN KEY ([DischargeId]) REFERENCES [AssetDischarges] ([Id]),
    CONSTRAINT [FK_AssetMaintenanceDowntimes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AssetTaskTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [MaintenanceTypeId] uniqueidentifier NOT NULL,
    [TaskName] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Sequence] int NOT NULL,
    [EstimatedHours] float NOT NULL,
    [IsRequired] bit NOT NULL,
    [AssignedTechnicianId] uniqueidentifier NULL,
    [Instructions] nvarchar(1000) NULL,
    [SafetyRequirements] nvarchar(1000) NULL,
    [RequiredTools] nvarchar(1000) NULL,
    [RequiredParts] nvarchar(1000) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AssetTaskTemplates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetTaskTemplates_MaintenanceTypes_MaintenanceTypeId] FOREIGN KEY ([MaintenanceTypeId]) REFERENCES [MaintenanceTypes] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AssetTaskTemplates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AssetTypeTaskTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [AssetTypeId] uniqueidentifier NOT NULL,
    [MaintenanceTypeId] uniqueidentifier NOT NULL,
    [TaskName] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Sequence] int NOT NULL,
    [EstimatedHours] float NOT NULL,
    [IsRequired] bit NOT NULL,
    [AssignedTechnicianId] uniqueidentifier NULL,
    [Instructions] nvarchar(1000) NULL,
    [SafetyRequirements] nvarchar(1000) NULL,
    [RequiredTools] nvarchar(1000) NULL,
    [RequiredParts] nvarchar(1000) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AssetTypeTaskTemplates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetTypeTaskTemplates_AssetTypes_AssetTypeId] FOREIGN KEY ([AssetTypeId]) REFERENCES [AssetTypes] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AssetTypeTaskTemplates_MaintenanceTypes_MaintenanceTypeId] FOREIGN KEY ([MaintenanceTypeId]) REFERENCES [MaintenanceTypes] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AssetTypeTaskTemplates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AssetUsageTrackings] (
    [Id] uniqueidentifier NOT NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [RecordedAt] datetime2 NOT NULL,
    [Mileage] decimal(18,4) NULL,
    [MileageUnit] nvarchar(10) NULL,
    [OperatingHours] decimal(18,4) NULL,
    [Cycles] int NULL,
    [FuelConsumed] decimal(18,4) NULL,
    [FuelUnit] nvarchar(10) NULL,
    [DataSource] nvarchar(50) NOT NULL,
    [ExternalReferenceId] nvarchar(100) NULL,
    [AdditionalMetrics] nvarchar(max) NULL,
    [Notes] nvarchar(500) NULL,
    [RecordedById] uniqueidentifier NULL,
    [TriggeredMaintenance] bit NOT NULL,
    [IsValidated] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AssetUsageTrackings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetUsageTrackings_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [AttendanceRecords] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Date] date NOT NULL,
    [CheckInTime] time NULL,
    [CheckOutTime] time NULL,
    [WorkedHours] float NULL,
    [OvertimeHours] float NULL,
    [Status] nvarchar(50) NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AttendanceRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AttendanceRecords_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ContractorWorkOrder] (
    [Id] uniqueidentifier NOT NULL,
    [ContractorId] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [AssignedDate] datetime2 NOT NULL,
    [StartedDate] datetime2 NULL,
    [CompletedDate] datetime2 NULL,
    [Status] nvarchar(20) NOT NULL,
    [EstimatedCost] decimal(18,2) NULL,
    [ActualCost] decimal(18,2) NULL,
    [WorkPerformed] nvarchar(2000) NULL,
    [PartsUsed] nvarchar(max) NULL,
    [QualityRating] int NULL,
    [Notes] nvarchar(1000) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ContractorWorkOrder] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ContractorWorkOrder_MaintenanceContractor_ContractorId] FOREIGN KEY ([ContractorId]) REFERENCES [MaintenanceContractor] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Departments] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(20) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [DepartmentType] int NOT NULL,
    [ParentDepartmentId] uniqueidentifier NULL,
    [DepartmentHeadId] uniqueidentifier NULL,
    [Budget] decimal(18,4) NULL,
    [IsActive] bit NOT NULL,
    [Color] nvarchar(7) NULL,
    [Icon] nvarchar(50) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Departments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Departments_Departments_ParentDepartmentId] FOREIGN KEY ([ParentDepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Departments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EmployeePositions] (
    [Id] uniqueidentifier NOT NULL,
    [Title] nvarchar(100) NOT NULL,
    [Code] nvarchar(20) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [DepartmentId] uniqueidentifier NOT NULL,
    [Level] int NOT NULL,
    [MinSalary] decimal(18,2) NULL,
    [MaxSalary] decimal(18,2) NULL,
    [RequiresCertification] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [Responsibilities] nvarchar(2000) NULL,
    [Requirements] nvarchar(2000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeePositions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeePositions_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EmployeePositions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [WorkStations] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(20) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Location] nvarchar(500) NULL,
    [DepartmentId] uniqueidentifier NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkStations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkStations_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]),
    CONSTRAINT [FK_WorkStations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [PositionSkillRequirement] (
    [Id] uniqueidentifier NOT NULL,
    [PositionId] uniqueidentifier NOT NULL,
    [SkillId] uniqueidentifier NOT NULL,
    [RequiredLevel] int NOT NULL,
    [IsRequired] bit NOT NULL,
    [Priority] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PositionSkillRequirement] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PositionSkillRequirement_EmployeePositions_PositionId] FOREIGN KEY ([PositionId]) REFERENCES [EmployeePositions] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PositionSkillRequirement_Skills_SkillId] FOREIGN KEY ([SkillId]) REFERENCES [Skills] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PositionSkillRequirement_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EmployeeBiometrics] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [BiometricType] nvarchar(50) NOT NULL,
    [BiometricData] nvarchar(500) NOT NULL,
    [DeviceId] nvarchar(100) NULL,
    [EnrolledDate] datetime2 NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeBiometrics] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeBiometrics_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EmployeeContractDetails] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [ContractNumber] nvarchar(50) NOT NULL,
    [ContractType] int NOT NULL,
    [StartDate] date NOT NULL,
    [EndDate] date NULL,
    [Salary] decimal(18,2) NOT NULL,
    [PayFrequency] nvarchar(50) NOT NULL,
    [WorkingHoursPerWeek] int NOT NULL,
    [VacationDaysPerYear] int NOT NULL,
    [SickDaysPerYear] int NOT NULL,
    [Terms] nvarchar(1000) NULL,
    [IsActive] bit NOT NULL,
    [ContractPath] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeContractDetails] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeContractDetails_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EmployeeDependents] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [LastName] nvarchar(100) NULL,
    [Relationship] nvarchar(50) NOT NULL,
    [DateOfBirth] date NULL,
    [Gender] int NULL,
    [Occupation] nvarchar(100) NULL,
    [IsStudentDependent] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeDependents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeDependents_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EmployeeEmergencyContacts] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [LastName] nvarchar(100) NULL,
    [Relationship] nvarchar(50) NOT NULL,
    [PhoneNumber] nvarchar(50) NOT NULL,
    [AlternatePhoneNumber] nvarchar(50) NULL,
    [EmailAddress] nvarchar(200) NULL,
    [Address] nvarchar(500) NULL,
    [IsPrimary] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeEmergencyContacts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeEmergencyContacts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EmployeeIdentificationCards] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [DocumentType] nvarchar(100) NOT NULL,
    [DocumentNumber] nvarchar(100) NOT NULL,
    [IssueDate] date NULL,
    [ExpiryDate] date NULL,
    [IssuingAuthority] nvarchar(200) NULL,
    [DocumentPath] nvarchar(500) NULL,
    [IsVerified] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeIdentificationCards] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeIdentificationCards_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EmployeeQualifications] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [QualificationName] nvarchar(200) NOT NULL,
    [Institution] nvarchar(200) NOT NULL,
    [FieldOfStudy] nvarchar(100) NULL,
    [StartDate] date NULL,
    [CompletionDate] date NULL,
    [Grade] nvarchar(50) NULL,
    [Description] nvarchar(500) NULL,
    [IsVerified] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeQualifications] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeQualifications_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Employees] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeNumber] nvarchar(50) NOT NULL,
    [CorporateEmployeeID] nvarchar(50) NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [MiddleName] nvarchar(100) NULL,
    [LastName] nvarchar(100) NOT NULL,
    [Title] nvarchar(100) NULL,
    [Gender] int NULL,
    [DateOfBirth] date NULL,
    [MaritalStatus] int NULL,
    [Religion] nvarchar(50) NULL,
    [IsFullTime] bit NOT NULL,
    [DateEmployed] date NULL,
    [Address] nvarchar(500) NULL,
    [City] nvarchar(100) NULL,
    [State] nvarchar(50) NULL,
    [PostalCode] nvarchar(20) NULL,
    [CountryId] uniqueidentifier NULL,
    [EmailAddress] nvarchar(200) NOT NULL,
    [TelephoneNumber] nvarchar(50) NULL,
    [BusinessNumber] nvarchar(50) NULL,
    [MobileNumber] nvarchar(50) NULL,
    [Extension] nvarchar(20) NULL,
    [ContractType] int NOT NULL,
    [ProbationPeriodDays] int NOT NULL,
    [ConfirmationDate] date NULL,
    [RetirementDate] date NULL,
    [PicturePath] nvarchar(500) NULL,
    [DepartmentId] uniqueidentifier NOT NULL,
    [SectionId] uniqueidentifier NULL,
    [PositionId] uniqueidentifier NOT NULL,
    [StaffStatus] int NOT NULL,
    [StationId] uniqueidentifier NULL,
    [TaxNumber] nvarchar(50) NULL,
    [BloodType] int NULL,
    [IsActive] bit NOT NULL,
    [ShiftId] uniqueidentifier NULL,
    [ManagerId] uniqueidentifier NULL,
    [Salary] decimal(18,2) NULL,
    [BadgeNumber] nvarchar(50) NULL,
    [LastPromotionDate] datetime2 NULL,
    [LastReviewDate] datetime2 NULL,
    [NextReviewDate] datetime2 NULL,
    [Specialization] nvarchar(100) NULL,
    [CertificationLevel] nvarchar(50) NULL,
    [ExperienceLevel] nvarchar(50) NULL,
    [CurrentWorkload] decimal(18,4) NOT NULL,
    [MaxWorkload] decimal(18,4) NOT NULL,
    [LastSyncDate] datetime2 NULL,
    [Notes] nvarchar(2000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Employees] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Employees_Countries_CountryId] FOREIGN KEY ([CountryId]) REFERENCES [Countries] ([Id]),
    CONSTRAINT [FK_Employees_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Employees_EmployeePositions_PositionId] FOREIGN KEY ([PositionId]) REFERENCES [EmployeePositions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Employees_Employees_ManagerId] FOREIGN KEY ([ManagerId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Employees_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]),
    CONSTRAINT [FK_Employees_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Employees_WorkStations_StationId] FOREIGN KEY ([StationId]) REFERENCES [WorkStations] ([Id])
);
GO

CREATE TABLE [EmployeeShiftPreferences] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [ShiftId] uniqueidentifier NOT NULL,
    [PreferenceLevel] int NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeShiftPreferences] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeShiftPreferences_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EmployeeShiftPreferences_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EmployeeShiftPreferences_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EmployeeSkills] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [SkillId] uniqueidentifier NOT NULL,
    [SkillLevel] int NOT NULL,
    [AcquiredDate] date NULL,
    [CertificationDate] date NULL,
    [CertificationExpiryDate] date NULL,
    [CertificationNumber] nvarchar(200) NULL,
    [CertifyingBody] nvarchar(200) NULL,
    [Notes] nvarchar(1000) NULL,
    [IsVerified] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeSkills] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeSkills_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EmployeeSkills_Skills_SkillId] FOREIGN KEY ([SkillId]) REFERENCES [Skills] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EmployeeSkills_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EmployeeWorkHistories] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [CompanyName] nvarchar(200) NOT NULL,
    [JobTitle] nvarchar(100) NOT NULL,
    [JobDescription] nvarchar(1000) NULL,
    [StartDate] date NOT NULL,
    [EndDate] date NULL,
    [Salary] decimal(18,2) NULL,
    [ReasonForLeaving] nvarchar(1000) NULL,
    [SupervisorName] nvarchar(200) NULL,
    [SupervisorPhone] nvarchar(50) NULL,
    [CanContact] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeWorkHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeWorkHistories_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EmployeeWorkHistories_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [InspectionApprovals] (
    [Id] uniqueidentifier NOT NULL,
    [InspectionId] uniqueidentifier NOT NULL,
    [ApprovalLevel] int NOT NULL,
    [ApproverId] uniqueidentifier NOT NULL,
    [ApproverRole] nvarchar(100) NULL,
    [Status] nvarchar(20) NOT NULL,
    [RequestedDate] datetime2 NOT NULL,
    [DueDate] datetime2 NULL,
    [ApprovedDate] datetime2 NULL,
    [Comments] nvarchar(2000) NULL,
    [Conditions] nvarchar(1000) NULL,
    [Priority] int NOT NULL,
    [CanDelegate] bit NOT NULL,
    [DelegatedToId] uniqueidentifier NULL,
    [DelegatedDate] datetime2 NULL,
    [DelegationReason] nvarchar(500) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InspectionApprovals] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InspectionApprovals_AssetInspections_InspectionId] FOREIGN KEY ([InspectionId]) REFERENCES [AssetInspections] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InspectionApprovals_Employees_ApproverId] FOREIGN KEY ([ApproverId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_InspectionApprovals_Employees_DelegatedToId] FOREIGN KEY ([DelegatedToId]) REFERENCES [Employees] ([Id])
);
GO

CREATE TABLE [MaintenanceAssets] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [AssetNumber] nvarchar(50) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [AssetCategoryId] uniqueidentifier NOT NULL,
    [Manufacturer] nvarchar(100) NULL,
    [Model] nvarchar(100) NULL,
    [EmployeeId] uniqueidentifier NULL,
    [SerialNumber] nvarchar(50) NULL,
    [PurchaseDate] datetime2 NULL,
    [PurchasePrice] decimal(18,2) NULL,
    [CurrentValue] decimal(18,4) NULL,
    [Location] nvarchar(500) NULL,
    [Building] nvarchar(100) NULL,
    [Floor] nvarchar(100) NULL,
    [Room] nvarchar(100) NULL,
    [Status] int NOT NULL,
    [Criticality] int NOT NULL,
    [WarrantyStartDate] datetime2 NULL,
    [WarrantyEndDate] datetime2 NULL,
    [WarrantyProvider] nvarchar(200) NULL,
    [ParentAssetId] uniqueidentifier NULL,
    [Specifications] nvarchar(max) NULL,
    [DocumentLinks] nvarchar(max) NULL,
    [Images] nvarchar(max) NULL,
    [OperatingHours] float NULL,
    [LastOperatingHoursUpdate] datetime2 NULL,
    [Mileage] float NULL,
    [LastMileageUpdate] datetime2 NULL,
    [LicensePlate] nvarchar(50) NULL,
    [VIN] nvarchar(50) NULL,
    [LastServiceDate] datetime2 NULL,
    [NextServiceDue] datetime2 NULL,
    [FloorArea] float NULL,
    [EnergyRating] nvarchar(50) NULL,
    [Capacity] float NULL,
    [CapacityUnit] nvarchar(50) NULL,
    [PowerRating] float NULL,
    [PowerUnit] nvarchar(20) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MaintenanceAssets] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceAssets_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_MaintenanceAssets_MaintenanceAssetCategories_AssetCategoryId] FOREIGN KEY ([AssetCategoryId]) REFERENCES [MaintenanceAssetCategories] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_MaintenanceAssets_MaintenanceAssets_ParentAssetId] FOREIGN KEY ([ParentAssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_MaintenanceAssets_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [MaintenanceAttachments] (
    [Id] uniqueidentifier NOT NULL,
    [FileName] nvarchar(100) NOT NULL,
    [FilePath] nvarchar(500) NOT NULL,
    [ContentType] nvarchar(100) NOT NULL,
    [FileSizeBytes] bigint NOT NULL,
    [Description] nvarchar(200) NULL,
    [AttachmentType] int NOT NULL,
    [EntityType] int NOT NULL,
    [EntityId] uniqueidentifier NOT NULL,
    [UploadedDate] datetime2 NOT NULL,
    [UploadedByUserId] uniqueidentifier NOT NULL,
    [IsMainImage] bit NOT NULL,
    [ImageWidth] int NULL,
    [ImageHeight] int NULL,
    [ThumbnailPath] nvarchar(500) NULL,
    [Latitude] float NULL,
    [Longitude] float NULL,
    [LocationDescription] nvarchar(200) NULL,
    [DocumentVersion] nvarchar(50) NULL,
    [IsArchived] bit NOT NULL,
    [ArchivedDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_MaintenanceAttachments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceAttachments_Employees_UploadedByUserId] FOREIGN KEY ([UploadedByUserId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [MaintenanceTaskTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [MaintenanceTypeId] uniqueidentifier NOT NULL,
    [TaskName] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Sequence] int NOT NULL,
    [EstimatedHours] float NOT NULL,
    [IsRequired] bit NOT NULL,
    [AssignedTechnicianId] uniqueidentifier NULL,
    [Instructions] nvarchar(1000) NULL,
    [SafetyRequirements] nvarchar(1000) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MaintenanceTaskTemplates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceTaskTemplates_Employees_AssignedTechnicianId] FOREIGN KEY ([AssignedTechnicianId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_MaintenanceTaskTemplates_MaintenanceTypes_MaintenanceTypeId] FOREIGN KEY ([MaintenanceTypeId]) REFERENCES [MaintenanceTypes] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_MaintenanceTaskTemplates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Sections] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(20) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [DepartmentId] uniqueidentifier NOT NULL,
    [SectionHeadId] uniqueidentifier NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Sections] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Sections_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Sections_Employees_SectionHeadId] FOREIGN KEY ([SectionHeadId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_Sections_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ShiftAssignments] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [ShiftId] uniqueidentifier NOT NULL,
    [StartDate] date NOT NULL,
    [EndDate] date NULL,
    [IsActive] bit NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ShiftAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ShiftAssignments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ShiftAssignments_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ShiftAssignments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TechnicianCertifications] (
    [Id] uniqueidentifier NOT NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [CertificationName] nvarchar(200) NOT NULL,
    [CertificationNumber] nvarchar(100) NOT NULL,
    [IssuingOrganization] nvarchar(200) NOT NULL,
    [IssueDate] datetime2 NOT NULL,
    [ExpirationDate] datetime2 NULL,
    [Status] nvarchar(20) NOT NULL,
    [CertificationLevel] nvarchar(50) NOT NULL,
    [Category] nvarchar(100) NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    [Requirements] nvarchar(500) NOT NULL,
    [RenewalRequirements] nvarchar(500) NOT NULL,
    [Cost] decimal(18,2) NULL,
    [IsMandatory] bit NOT NULL,
    [IsVerified] bit NOT NULL,
    [VerifiedBy] nvarchar(100) NOT NULL,
    [VerificationDate] datetime2 NULL,
    [Notes] nvarchar(1000) NOT NULL,
    [DocumentPath] nvarchar(500) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TechnicianCertifications] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TechnicianCertifications_Employees_TechnicianId] FOREIGN KEY ([TechnicianId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TechnicianCertifications_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Technicians] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [EmployeeNumber] nvarchar(50) NOT NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [LastName] nvarchar(100) NOT NULL,
    [Email] nvarchar(200) NULL,
    [Phone] nvarchar(20) NULL,
    [Department] nvarchar(100) NOT NULL,
    [Position] nvarchar(100) NOT NULL,
    [Specialization] nvarchar(100) NOT NULL,
    [CertificationLevel] nvarchar(50) NOT NULL,
    [ExperienceLevel] nvarchar(50) NOT NULL,
    [HireDate] datetime2 NOT NULL,
    [IsActive] bit NOT NULL,
    [CurrentWorkload] decimal(18,4) NOT NULL,
    [MaxWorkload] decimal(18,4) NOT NULL,
    [AverageRating] decimal(18,4) NOT NULL,
    [CompletedWorkOrders] int NOT NULL,
    [LastSyncDate] datetime2 NULL,
    [Notes] nvarchar(2000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Technicians] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Technicians_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Technicians_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [UserTechnicianSkills] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [SkillId] uniqueidentifier NOT NULL,
    [ProficiencyLevel] int NOT NULL,
    [CertificationDate] datetime2 NULL,
    [CertificationExpiry] datetime2 NULL,
    [CertifyingBody] nvarchar(200) NULL,
    [CertificationNumber] nvarchar(100) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_UserTechnicianSkills] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserTechnicianSkills_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserTechnicianSkills_TechnicianSkills_SkillId] FOREIGN KEY ([SkillId]) REFERENCES [TechnicianSkills] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserTechnicianSkills_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [MaintenanceCertificate] (
    [Id] uniqueidentifier NOT NULL,
    [CertificateNumber] nvarchar(50) NOT NULL,
    [DischargeId] uniqueidentifier NOT NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [CertificateType] nvarchar(100) NOT NULL,
    [IssuedDate] datetime2 NOT NULL,
    [ValidUntil] datetime2 NULL,
    [IssuedById] uniqueidentifier NOT NULL,
    [FilePath] nvarchar(500) NULL,
    [FileFormat] nvarchar(100) NOT NULL,
    [Description] nvarchar(2000) NULL,
    [CertificateData] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    [AssetDischargeId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MaintenanceCertificate] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceCertificate_AssetDischarges_AssetDischargeId] FOREIGN KEY ([AssetDischargeId]) REFERENCES [AssetDischarges] ([Id]),
    CONSTRAINT [FK_MaintenanceCertificate_AssetDischarges_DischargeId] FOREIGN KEY ([DischargeId]) REFERENCES [AssetDischarges] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_MaintenanceCertificate_MaintenanceAssets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [MaintenanceAssets] ([Id]),
    CONSTRAINT [FK_MaintenanceCertificate_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_MaintenanceCertificate_Users_IssuedById] FOREIGN KEY ([IssuedById]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [MaintenanceAttachmentTag] (
    [Id] uniqueidentifier NOT NULL,
    [AttachmentId] uniqueidentifier NOT NULL,
    [TagName] nvarchar(50) NOT NULL,
    [TagValue] nvarchar(100) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_MaintenanceAttachmentTag] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceAttachmentTag_MaintenanceAttachments_AttachmentId] FOREIGN KEY ([AttachmentId]) REFERENCES [MaintenanceAttachments] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [TechnicianAvailabilities] (
    [Id] uniqueidentifier NOT NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [AvailabilityType] nvarchar(20) NOT NULL,
    [Reason] nvarchar(50) NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [IsRecurring] bit NOT NULL,
    [RecurrencePattern] nvarchar(100) NULL,
    [AvailableHours] float NULL,
    [CapacityPercentage] float NULL,
    [EmployeeId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TechnicianAvailabilities] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TechnicianAvailabilities_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_TechnicianAvailabilities_Technicians_TechnicianId] FOREIGN KEY ([TechnicianId]) REFERENCES [Technicians] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TechnicianAvailabilities_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TechnicianSkillAssignments] (
    [Id] uniqueidentifier NOT NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [SkillId] uniqueidentifier NOT NULL,
    [ProficiencyLevel] int NOT NULL,
    [ProficiencyDescription] nvarchar(100) NOT NULL,
    [AcquiredDate] datetime2 NOT NULL,
    [ExpirationDate] datetime2 NULL,
    [IsVerified] bit NOT NULL,
    [VerifiedBy] nvarchar(100) NOT NULL,
    [LastAssessmentDate] datetime2 NULL,
    [Notes] nvarchar(500) NOT NULL,
    [TechnicianId1] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TechnicianSkillAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TechnicianSkillAssignments_Employees_TechnicianId] FOREIGN KEY ([TechnicianId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TechnicianSkillAssignments_TechnicalSkills_SkillId] FOREIGN KEY ([SkillId]) REFERENCES [TechnicalSkills] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TechnicianSkillAssignments_Technicians_TechnicianId1] FOREIGN KEY ([TechnicianId1]) REFERENCES [Technicians] ([Id]),
    CONSTRAINT [FK_TechnicianSkillAssignments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TechnicianTeams] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [TeamLeaderId] uniqueidentifier NULL,
    [Status] nvarchar(20) NOT NULL,
    [TechnicianId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TechnicianTeams] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TechnicianTeams_Employees_TeamLeaderId] FOREIGN KEY ([TeamLeaderId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_TechnicianTeams_Technicians_TechnicianId] FOREIGN KEY ([TechnicianId]) REFERENCES [Technicians] ([Id]),
    CONSTRAINT [FK_TechnicianTeams_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [MaintenanceSchedules] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(20) NOT NULL,
    [Description] nvarchar(500) NOT NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [MaintenanceTypeId] uniqueidentifier NOT NULL,
    [Frequency] nvarchar(20) NOT NULL,
    [FrequencyValue] int NOT NULL,
    [FrequencyUnit] nvarchar(20) NOT NULL,
    [PrimaryTriggerType] nvarchar(20) NOT NULL,
    [SecondaryTriggerType] nvarchar(20) NULL,
    [TriggerLogic] nvarchar(10) NULL,
    [MileageTrigger] decimal(18,4) NULL,
    [OperatingHoursTrigger] decimal(18,4) NULL,
    [CycleTrigger] decimal(18,4) NULL,
    [UsageUnit] nvarchar(20) NULL,
    [LastUsageValue] decimal(18,4) NULL,
    [ConditionCriteria] nvarchar(max) NULL,
    [ConditionDataSources] nvarchar(max) NULL,
    [LastConditionCheckResult] bit NULL,
    [LastConditionCheckDate] datetime2 NULL,
    [StartDate] datetime2 NOT NULL,
    [NextDueDate] datetime2 NOT NULL,
    [LastCompletedDate] datetime2 NULL,
    [Priority] nvarchar(20) NOT NULL,
    [EstimatedHours] decimal(18,4) NOT NULL,
    [EstimatedCost] decimal(18,2) NOT NULL,
    [AssignedTechnicianId] uniqueidentifier NULL,
    [AssignedTeamId] uniqueidentifier NULL,
    [Instructions] nvarchar(2000) NOT NULL,
    [SafetyNotes] nvarchar(1000) NOT NULL,
    [RequiredSkills] nvarchar(max) NOT NULL,
    [RequiredTools] nvarchar(max) NOT NULL,
    [RequiredParts] nvarchar(max) NOT NULL,
    [AutoGenerateWorkOrders] bit NOT NULL,
    [AdvanceNotificationDays] int NULL,
    [NotificationRecipients] nvarchar(500) NULL,
    [IsActive] bit NOT NULL,
    [ScheduleType] nvarchar(50) NOT NULL,
    [LastGeneratedDate] datetime2 NULL,
    [LastProcessedDate] datetime2 NULL,
    [LastReminderSentDate] datetime2 NULL,
    [LastUsageCheckDate] datetime2 NULL,
    [DefaultTechnicianId] uniqueidentifier NULL,
    [DefaultTeamId] uniqueidentifier NULL,
    [PriorityLevelId] uniqueidentifier NULL,
    [MaintenanceTypeId1] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MaintenanceSchedules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceSchedules_Employees_DefaultTechnicianId] FOREIGN KEY ([DefaultTechnicianId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_MaintenanceSchedules_MaintenanceAssets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_MaintenanceSchedules_MaintenanceTypes_MaintenanceTypeId] FOREIGN KEY ([MaintenanceTypeId]) REFERENCES [MaintenanceTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_MaintenanceSchedules_MaintenanceTypes_MaintenanceTypeId1] FOREIGN KEY ([MaintenanceTypeId1]) REFERENCES [MaintenanceTypes] ([Id]),
    CONSTRAINT [FK_MaintenanceSchedules_PriorityLevels_PriorityLevelId] FOREIGN KEY ([PriorityLevelId]) REFERENCES [PriorityLevels] ([Id]),
    CONSTRAINT [FK_MaintenanceSchedules_TechnicianTeams_DefaultTeamId] FOREIGN KEY ([DefaultTeamId]) REFERENCES [TechnicianTeams] ([Id]),
    CONSTRAINT [FK_MaintenanceSchedules_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TechnicianTeamMembers] (
    [Id] uniqueidentifier NOT NULL,
    [TeamId] uniqueidentifier NOT NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [Role] nvarchar(50) NOT NULL,
    [JoinedDate] datetime2 NOT NULL,
    [LeftDate] datetime2 NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TechnicianTeamMembers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TechnicianTeamMembers_TechnicianTeams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [TechnicianTeams] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TechnicianTeamMembers_Technicians_TechnicianId] FOREIGN KEY ([TechnicianId]) REFERENCES [Technicians] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TechnicianTeamMembers_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [MaintenanceScheduleHistories] (
    [Id] uniqueidentifier NOT NULL,
    [ScheduleId] uniqueidentifier NOT NULL,
    [ChangeType] nvarchar(50) NOT NULL,
    [PreviousValues] nvarchar(max) NULL,
    [NewValues] nvarchar(max) NULL,
    [ChangeReason] nvarchar(500) NULL,
    [ChangedById] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MaintenanceScheduleHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceScheduleHistories_MaintenanceSchedules_ScheduleId] FOREIGN KEY ([ScheduleId]) REFERENCES [MaintenanceSchedules] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_MaintenanceScheduleHistories_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [MaintenanceScheduleNotificationHistories] (
    [Id] uniqueidentifier NOT NULL,
    [ScheduleId] uniqueidentifier NOT NULL,
    [NotificationType] nvarchar(50) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [ScheduledFor] datetime2 NOT NULL,
    [SentAt] datetime2 NULL,
    [Recipients] nvarchar(max) NOT NULL,
    [Subject] nvarchar(200) NULL,
    [Message] nvarchar(max) NULL,
    [ErrorMessage] nvarchar(1000) NULL,
    [RetryCount] int NOT NULL,
    [AdditionalData] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MaintenanceScheduleNotificationHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceScheduleNotificationHistories_MaintenanceSchedules_ScheduleId] FOREIGN KEY ([ScheduleId]) REFERENCES [MaintenanceSchedules] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_MaintenanceScheduleNotificationHistories_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ScheduledWorkOrder] (
    [Id] uniqueidentifier NOT NULL,
    [ScheduleId] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [GeneratedDate] datetime2 NOT NULL,
    [ScheduledCompletionDate] datetime2 NOT NULL,
    [ActualCompletionDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_ScheduledWorkOrder] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ScheduledWorkOrder_MaintenanceSchedules_ScheduleId] FOREIGN KEY ([ScheduleId]) REFERENCES [MaintenanceSchedules] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [FiscalPeriods] (
    [Id] uniqueidentifier NOT NULL,
    [FiscalYearId] uniqueidentifier NOT NULL,
    [PeriodName] nvarchar(50) NOT NULL,
    [PeriodCode] nvarchar(20) NOT NULL,
    [PeriodNumber] int NOT NULL,
    [PeriodType] nvarchar(20) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [PeriodDays] int NOT NULL,
    [PeriodStatus] nvarchar(20) NOT NULL,
    [IsOpen] bit NOT NULL,
    [IsLocked] bit NOT NULL,
    [LockedDate] datetime2 NULL,
    [LockedByUserId] uniqueidentifier NULL,
    [LockReason] nvarchar(500) NULL,
    [IsCloseInitiated] bit NOT NULL,
    [CloseInitiatedDate] datetime2 NULL,
    [CloseInitiatedByUserId] uniqueidentifier NULL,
    [IsClosed] bit NOT NULL,
    [ClosedDate] datetime2 NULL,
    [ClosedByUserId] uniqueidentifier NULL,
    [TrialBalanceValidated] bit NOT NULL,
    [TrialBalanceValidatedDate] datetime2 NULL,
    [BankReconciliationComplete] bit NOT NULL,
    [BankReconciliationCompletedDate] datetime2 NULL,
    [CurrencyRevaluationComplete] bit NOT NULL,
    [CurrencyRevaluationDate] datetime2 NULL,
    [DepreciationComplete] bit NOT NULL,
    [DepreciationCompletedDate] datetime2 NULL,
    [InventoryValuationComplete] bit NOT NULL,
    [InventoryValuationDate] datetime2 NULL,
    [AccrualsComplete] bit NOT NULL,
    [AccrualsCompletedDate] datetime2 NULL,
    [HasBeenReopened] bit NOT NULL,
    [ReopenCount] int NOT NULL,
    [LastReopenedDate] datetime2 NULL,
    [LastReopenedByUserId] uniqueidentifier NULL,
    [ReopenReason] nvarchar(1000) NULL,
    [IsYearEnd] bit NOT NULL,
    [YearEndCloseComplete] bit NOT NULL,
    [YearEndCloseDate] datetime2 NULL,
    [YearEndClosedByUserId] uniqueidentifier NULL,
    [TotalJournalEntries] int NOT NULL,
    [TotalTransactionLines] int NOT NULL,
    [TotalDebits] decimal(18,2) NOT NULL,
    [TotalCredits] decimal(18,2) NOT NULL,
    [BalanceDifference] decimal(18,4) NOT NULL,
    [ClosingNotes] nvarchar(2000) NULL,
    [AllowBackdating] bit NOT NULL,
    [AllowFutureDating] bit NOT NULL,
    [MaxTransactionAmount] decimal(18,2) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [ReferenceNumber] nvarchar(50) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [EffectiveDate] datetime2 NULL,
    [ExpirationDate] datetime2 NULL,
    [Metadata] nvarchar(max) NULL,
    [Tags] nvarchar(500) NULL,
    [Priority] int NOT NULL,
    CONSTRAINT [PK_FiscalPeriods] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FiscalPeriods_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [JournalEntries] (
    [Id] uniqueidentifier NOT NULL,
    [JournalEntryNumber] nvarchar(50) NOT NULL,
    [JournalType] nvarchar(50) NOT NULL,
    [EntryDate] datetime2 NOT NULL,
    [Description] nvarchar(500) NOT NULL,
    [ReferenceNumber] nvarchar(100) NULL,
    [SourceModule] nvarchar(50) NULL,
    [SourceDocumentId] uniqueidentifier NULL,
    [SourceDocumentType] nvarchar(100) NULL,
    [TotalDebitAmount] decimal(18,2) NOT NULL,
    [TotalCreditAmount] decimal(18,2) NOT NULL,
    [BalanceDifference] decimal(18,4) NOT NULL,
    [IsBalanced] bit NOT NULL,
    [IsMultiCurrency] bit NOT NULL,
    [PrimaryCurrency] nvarchar(3) NULL,
    [BookClassification] nvarchar(20) NOT NULL,
    [FiscalPeriodId] uniqueidentifier NOT NULL,
    [PostingDate] datetime2 NULL,
    [PostedByUserId] uniqueidentifier NULL,
    [PostingStatus] nvarchar(50) NOT NULL,
    [RequiresApproval] bit NOT NULL,
    [ApprovalStatus] nvarchar(50) NULL,
    [ApprovalWorkflowId] nvarchar(100) NULL,
    [ApprovedByUserId] uniqueidentifier NULL,
    [ApprovedDate] datetime2 NULL,
    [RejectionReason] nvarchar(1000) NULL,
    [IsReversed] bit NOT NULL,
    [ReversalDate] datetime2 NULL,
    [ReversalJournalEntryId] uniqueidentifier NULL,
    [OriginalJournalEntryId] uniqueidentifier NULL,
    [ReversalType] nvarchar(20) NULL,
    [ReversalReason] nvarchar(500) NULL,
    [IsRecurring] bit NOT NULL,
    [RecurringTemplateId] uniqueidentifier NULL,
    [RecurrenceFrequency] nvarchar(20) NULL,
    [NextRecurrenceDate] datetime2 NULL,
    [IsRevaluationEntry] bit NOT NULL,
    [RevaluationBatchNumber] nvarchar(50) NULL,
    [RevaluationType] nvarchar(20) NULL,
    [IsAutoReversalEntry] bit NOT NULL,
    [IsImported] bit NOT NULL,
    [ImportBatchReference] nvarchar(100) NULL,
    [Notes] nvarchar(2000) NULL,
    [EntryTag] nvarchar(100) NULL,
    [Priority] int NOT NULL,
    [HasAttachments] bit NOT NULL,
    [AttachmentCount] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [EffectiveDate] datetime2 NULL,
    [ExpirationDate] datetime2 NULL,
    [Metadata] nvarchar(max) NULL,
    [Tags] nvarchar(500) NULL,
    CONSTRAINT [PK_JournalEntries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_JournalEntries_FiscalPeriods_FiscalPeriodId] FOREIGN KEY ([FiscalPeriodId]) REFERENCES [FiscalPeriods] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_JournalEntries_JournalEntries_OriginalJournalEntryId] FOREIGN KEY ([OriginalJournalEntryId]) REFERENCES [JournalEntries] ([Id]),
    CONSTRAINT [FK_JournalEntries_JournalEntries_ReversalJournalEntryId] FOREIGN KEY ([ReversalJournalEntryId]) REFERENCES [JournalEntries] ([Id]),
    CONSTRAINT [FK_JournalEntries_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [FiscalYears] (
    [Id] uniqueidentifier NOT NULL,
    [FiscalYearName] nvarchar(100) NOT NULL,
    [FiscalYearCode] nvarchar(20) NOT NULL,
    [Year] int NOT NULL,
    [FiscalYearType] nvarchar(20) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [TotalDays] int NOT NULL,
    [NumberOfPeriods] int NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [IsActive] bit NOT NULL,
    [IsLocked] bit NOT NULL,
    [LockedDate] datetime2 NULL,
    [LockedByUserId] uniqueidentifier NULL,
    [LockReason] nvarchar(500) NULL,
    [IsCloseInitiated] bit NOT NULL,
    [CloseInitiatedDate] datetime2 NULL,
    [CloseInitiatedByUserId] uniqueidentifier NULL,
    [IsClosed] bit NOT NULL,
    [ClosedDate] datetime2 NULL,
    [ClosedByUserId] uniqueidentifier NULL,
    [AllPeriodsClosedValidated] bit NOT NULL,
    [AllPeriodsClosedValidatedDate] datetime2 NULL,
    [FinalDepreciationComplete] bit NOT NULL,
    [FinalDepreciationDate] datetime2 NULL,
    [YearEndRevaluationComplete] bit NOT NULL,
    [YearEndRevaluationDate] datetime2 NULL,
    [YearEndInventoryComplete] bit NOT NULL,
    [YearEndInventoryDate] datetime2 NULL,
    [YearEndAccrualsComplete] bit NOT NULL,
    [YearEndAccrualsDate] datetime2 NULL,
    [YearEndTrialBalanceValidated] bit NOT NULL,
    [YearEndTrialBalanceDate] datetime2 NULL,
    [RetainedEarningsTransferComplete] bit NOT NULL,
    [RetainedEarningsTransferDate] datetime2 NULL,
    [ClosingJournalEntryId] uniqueidentifier NULL,
    [NetIncomeTransferred] decimal(18,4) NULL,
    [OpeningBalancesGenerated] bit NOT NULL,
    [OpeningBalancesGeneratedDate] datetime2 NULL,
    [NextFiscalYearId] uniqueidentifier NULL,
    [OpeningBalanceJournalEntryId] uniqueidentifier NULL,
    [HasBeenReopened] bit NOT NULL,
    [ReopenCount] int NOT NULL,
    [LastReopenedDate] datetime2 NULL,
    [LastReopenedByUserId] uniqueidentifier NULL,
    [ReopenReason] nvarchar(1000) NULL,
    [ReportingFramework] nvarchar(50) NOT NULL,
    [BaseCurrency] nvarchar(3) NOT NULL,
    [TotalJournalEntries] int NOT NULL,
    [TotalTransactionLines] int NOT NULL,
    [TotalDebits] decimal(18,2) NOT NULL,
    [TotalCredits] decimal(18,2) NOT NULL,
    [BalanceDifference] decimal(18,4) NOT NULL,
    [TotalRevenue] decimal(18,2) NOT NULL,
    [TotalExpenses] decimal(18,2) NOT NULL,
    [NetIncome] decimal(18,4) NOT NULL,
    [IsBudgetApproved] bit NOT NULL,
    [BudgetApprovedDate] datetime2 NULL,
    [BudgetApprovedByUserId] uniqueidentifier NULL,
    [BudgetedRevenue] decimal(18,4) NULL,
    [BudgetedExpenses] decimal(18,4) NULL,
    [BudgetedNetIncome] decimal(18,4) NULL,
    [IsAuditComplete] bit NOT NULL,
    [AuditCompletedDate] datetime2 NULL,
    [AuditFirm] nvarchar(200) NULL,
    [AuditOpinion] nvarchar(50) NULL,
    [AuditReportReference] nvarchar(100) NULL,
    [Notes] nvarchar(2000) NULL,
    [YearEndClosingNotes] nvarchar(2000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [ReferenceNumber] nvarchar(50) NOT NULL,
    [EffectiveDate] datetime2 NULL,
    [ExpirationDate] datetime2 NULL,
    [Metadata] nvarchar(max) NULL,
    [Tags] nvarchar(500) NULL,
    [Priority] int NOT NULL,
    CONSTRAINT [PK_FiscalYears] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FiscalYears_FiscalYears_NextFiscalYearId] FOREIGN KEY ([NextFiscalYearId]) REFERENCES [FiscalYears] ([Id]),
    CONSTRAINT [FK_FiscalYears_JournalEntries_ClosingJournalEntryId] FOREIGN KEY ([ClosingJournalEntryId]) REFERENCES [JournalEntries] ([Id]),
    CONSTRAINT [FK_FiscalYears_JournalEntries_OpeningBalanceJournalEntryId] FOREIGN KEY ([OpeningBalanceJournalEntryId]) REFERENCES [JournalEntries] ([Id]),
    CONSTRAINT [FK_FiscalYears_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [UnitAccountBalances] (
    [Id] uniqueidentifier NOT NULL,
    [UnitAccountId] uniqueidentifier NOT NULL,
    [FiscalYearId] uniqueidentifier NOT NULL,
    [FiscalPeriodId] uniqueidentifier NOT NULL,
    [OpeningBalance] decimal(18,4) NOT NULL,
    [PeriodActivity] decimal(18,4) NOT NULL,
    [ClosingBalance] decimal(18,4) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_UnitAccountBalances] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UnitAccountBalances_FiscalPeriods_FiscalPeriodId] FOREIGN KEY ([FiscalPeriodId]) REFERENCES [FiscalPeriods] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UnitAccountBalances_FiscalYears_FiscalYearId] FOREIGN KEY ([FiscalYearId]) REFERENCES [FiscalYears] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UnitAccountBalances_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UnitAccountBalances_UnitAccounts_UnitAccountId] FOREIGN KEY ([UnitAccountId]) REFERENCES [UnitAccounts] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [UnitAccountBudgets] (
    [Id] uniqueidentifier NOT NULL,
    [UnitAccountId] uniqueidentifier NOT NULL,
    [FiscalYearId] uniqueidentifier NOT NULL,
    [FiscalPeriodId] uniqueidentifier NOT NULL,
    [BudgetQuantity] decimal(18,4) NOT NULL,
    [Notes] nvarchar(500) NULL,
    [BudgetVersion] nvarchar(50) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_UnitAccountBudgets] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UnitAccountBudgets_FiscalPeriods_FiscalPeriodId] FOREIGN KEY ([FiscalPeriodId]) REFERENCES [FiscalPeriods] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UnitAccountBudgets_FiscalYears_FiscalYearId] FOREIGN KEY ([FiscalYearId]) REFERENCES [FiscalYears] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UnitAccountBudgets_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UnitAccountBudgets_UnitAccounts_UnitAccountId] FOREIGN KEY ([UnitAccountId]) REFERENCES [UnitAccounts] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [UnitJournalEntries] (
    [Id] uniqueidentifier NOT NULL,
    [EntryNumber] nvarchar(50) NOT NULL,
    [EntryDate] datetime2 NOT NULL,
    [Description] nvarchar(500) NULL,
    [FiscalYearId] uniqueidentifier NOT NULL,
    [FiscalPeriodId] uniqueidentifier NOT NULL,
    [Status] int NOT NULL,
    [SourceDocument] nvarchar(200) NULL,
    [ApprovedAt] datetime2 NULL,
    [ApprovedBy] uniqueidentifier NULL,
    [ApprovedByName] nvarchar(100) NULL,
    [RejectionReason] nvarchar(500) NULL,
    [PostedAt] datetime2 NULL,
    [PostedBy] uniqueidentifier NULL,
    [PostedByName] nvarchar(100) NULL,
    [IsReversal] bit NOT NULL,
    [ReversedEntryId] uniqueidentifier NULL,
    [ReversalEntryId] uniqueidentifier NULL,
    [ReversalReason] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_UnitJournalEntries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UnitJournalEntries_FiscalPeriods_FiscalPeriodId] FOREIGN KEY ([FiscalPeriodId]) REFERENCES [FiscalPeriods] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UnitJournalEntries_FiscalYears_FiscalYearId] FOREIGN KEY ([FiscalYearId]) REFERENCES [FiscalYears] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UnitJournalEntries_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [UnitJournalEntryLines] (
    [Id] uniqueidentifier NOT NULL,
    [UnitJournalEntryId] uniqueidentifier NOT NULL,
    [LineNumber] int NOT NULL,
    [UnitAccountId] uniqueidentifier NOT NULL,
    [Quantity] decimal(18,4) NOT NULL,
    [Description] nvarchar(300) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_UnitJournalEntryLines] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UnitJournalEntryLines_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UnitJournalEntryLines_UnitAccounts_UnitAccountId] FOREIGN KEY ([UnitAccountId]) REFERENCES [UnitAccounts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UnitJournalEntryLines_UnitJournalEntries_UnitJournalEntryId] FOREIGN KEY ([UnitJournalEntryId]) REFERENCES [UnitJournalEntries] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [JobCard] (
    [Id] uniqueidentifier NOT NULL,
    [JobCardNumber] nvarchar(50) NOT NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [MaintenanceTypeId] uniqueidentifier NOT NULL,
    [PriorityLevelId] uniqueidentifier NOT NULL,
    [Title] nvarchar(500) NOT NULL,
    [Description] nvarchar(2000) NULL,
    [ProblemDescription] nvarchar(2000) NULL,
    [MaintenanceLocation] nvarchar(20) NOT NULL,
    [RequestedById] uniqueidentifier NOT NULL,
    [RequestedDate] datetime2 NOT NULL,
    [RequiredCompletionDate] datetime2 NULL,
    [EstimatedHours] float NOT NULL,
    [EstimatedCost] decimal(18,2) NOT NULL,
    [PreferredTechnicianId] uniqueidentifier NULL,
    [PreferredTeamId] uniqueidentifier NULL,
    [ContractorId] uniqueidentifier NULL,
    [RequiresSpecialTools] bit NOT NULL,
    [RequiresShutdown] bit NOT NULL,
    [RequiresSafetyPermit] bit NOT NULL,
    [SpecialInstructions] nvarchar(1000) NULL,
    [SafetyRequirements] nvarchar(1000) NULL,
    [JobCardStatus] nvarchar(20) NOT NULL,
    [ApprovalStatus] nvarchar(50) NOT NULL,
    [SubmittedById] uniqueidentifier NULL,
    [SubmittedDate] datetime2 NULL,
    [ApprovedById] uniqueidentifier NULL,
    [ApprovedDate] datetime2 NULL,
    [ApprovalComments] nvarchar(1000) NULL,
    [WorkflowInstanceId] uniqueidentifier NULL,
    [PlannedStartDate] datetime2 NULL,
    [PlannedEndDate] datetime2 NULL,
    [AssignedTechnicianId] uniqueidentifier NULL,
    [AssignedTeamId] uniqueidentifier NULL,
    [GeneratedWorkOrderId] uniqueidentifier NULL,
    [WorkOrderGeneratedAt] datetime2 NULL,
    [AssetConditionOnAdmission] nvarchar(20) NULL,
    [MileageReadingOnAdmission] decimal(18,4) NULL,
    [HoursReadingOnAdmission] decimal(18,4) NULL,
    [FuelLevelOnAdmission] decimal(18,4) NULL,
    [AdmissionNotes] nvarchar(2000) NULL,
    [BayOrStation] nvarchar(100) NULL,
    [CompletedDate] datetime2 NULL,
    [CompletionNotes] nvarchar(2000) NULL,
    [AssetConditionOnCompletion] nvarchar(20) NULL,
    [MileageReadingOnCompletion] decimal(18,4) NULL,
    [HoursReadingOnCompletion] decimal(18,4) NULL,
    [FuelLevelOnCompletion] decimal(18,4) NULL,
    [WorkCompletedSummary] nvarchar(2000) NULL,
    [RemainingIssues] nvarchar(2000) NULL,
    [QualityCheckPassed] bit NOT NULL,
    [QualityCheckedById] uniqueidentifier NULL,
    [QualityCheckDate] datetime2 NULL,
    [QualityCheckNotes] nvarchar(2000) NULL,
    [CertificateGenerated] bit NOT NULL,
    [CertificateGeneratedDate] datetime2 NULL,
    [CustomerAcceptance] bit NOT NULL,
    [AcceptedById] uniqueidentifier NULL,
    [AcceptedDate] datetime2 NULL,
    [AcceptanceNotes] nvarchar(1000) NULL,
    [RequiresFollowUp] bit NOT NULL,
    [FollowUpDate] datetime2 NULL,
    [FollowUpInstructions] nvarchar(1000) NULL,
    [WarrantyDays] int NOT NULL,
    [WarrantyExpiration] datetime2 NULL,
    [WarrantyTerms] nvarchar(1000) NULL,
    [AttachmentPaths] nvarchar(max) NULL,
    [CustomFieldValues] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_JobCard] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_JobCard_Employees_AcceptedById] FOREIGN KEY ([AcceptedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_JobCard_Employees_AssignedTechnicianId] FOREIGN KEY ([AssignedTechnicianId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_JobCard_Employees_PreferredTechnicianId] FOREIGN KEY ([PreferredTechnicianId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_JobCard_Employees_QualityCheckedById] FOREIGN KEY ([QualityCheckedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_JobCard_Employees_RequestedById] FOREIGN KEY ([RequestedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_JobCard_MaintenanceAssets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_JobCard_MaintenanceContractor_ContractorId] FOREIGN KEY ([ContractorId]) REFERENCES [MaintenanceContractor] ([Id]),
    CONSTRAINT [FK_JobCard_MaintenanceTypes_MaintenanceTypeId] FOREIGN KEY ([MaintenanceTypeId]) REFERENCES [MaintenanceTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_JobCard_PriorityLevels_PriorityLevelId] FOREIGN KEY ([PriorityLevelId]) REFERENCES [PriorityLevels] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_JobCard_TechnicianTeams_AssignedTeamId] FOREIGN KEY ([AssignedTeamId]) REFERENCES [TechnicianTeams] ([Id]),
    CONSTRAINT [FK_JobCard_TechnicianTeams_PreferredTeamId] FOREIGN KEY ([PreferredTeamId]) REFERENCES [TechnicianTeams] ([Id]),
    CONSTRAINT [FK_JobCard_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [JobCardApprovalStep] (
    [Id] uniqueidentifier NOT NULL,
    [JobCardId] uniqueidentifier NOT NULL,
    [StepOrder] int NOT NULL,
    [StepName] nvarchar(100) NOT NULL,
    [ApproverId] uniqueidentifier NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [ActionDate] datetime2 NULL,
    [Comments] nvarchar(1000) NULL,
    [IsRequired] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_JobCardApprovalStep] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_JobCardApprovalStep_Employees_ApproverId] FOREIGN KEY ([ApproverId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_JobCardApprovalStep_JobCard_JobCardId] FOREIGN KEY ([JobCardId]) REFERENCES [JobCard] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_JobCardApprovalStep_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [JobCardCertificates] (
    [Id] uniqueidentifier NOT NULL,
    [JobCardId] uniqueidentifier NOT NULL,
    [CertificateNumber] nvarchar(50) NOT NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [CertificateType] nvarchar(100) NOT NULL,
    [IssuedDate] datetime2 NOT NULL,
    [ValidUntil] datetime2 NULL,
    [IssuedById] uniqueidentifier NOT NULL,
    [FilePath] nvarchar(500) NULL,
    [FileFormat] nvarchar(100) NOT NULL,
    [Description] nvarchar(2000) NULL,
    [CertificateData] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_JobCardCertificates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_JobCardCertificates_Employees_IssuedById] FOREIGN KEY ([IssuedById]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_JobCardCertificates_JobCard_JobCardId] FOREIGN KEY ([JobCardId]) REFERENCES [JobCard] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_JobCardCertificates_MaintenanceAssets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_JobCardCertificates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [JobCardComment] (
    [Id] uniqueidentifier NOT NULL,
    [JobCardId] uniqueidentifier NOT NULL,
    [CommentById] uniqueidentifier NOT NULL,
    [Comment] nvarchar(2000) NOT NULL,
    [CommentType] nvarchar(50) NOT NULL,
    [IsInternal] bit NOT NULL,
    [CommentDate] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_JobCardComment] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_JobCardComment_Employees_CommentById] FOREIGN KEY ([CommentById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_JobCardComment_JobCard_JobCardId] FOREIGN KEY ([JobCardId]) REFERENCES [JobCard] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_JobCardComment_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [JobCardDocument] (
    [Id] uniqueidentifier NOT NULL,
    [JobCardId] uniqueidentifier NOT NULL,
    [FileName] nvarchar(255) NOT NULL,
    [FilePath] nvarchar(500) NOT NULL,
    [ContentType] nvarchar(100) NULL,
    [FileSize] bigint NOT NULL,
    [DocumentType] nvarchar(50) NOT NULL,
    [Description] nvarchar(500) NULL,
    [UploadedById] uniqueidentifier NOT NULL,
    [UploadedDate] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_JobCardDocument] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_JobCardDocument_Employees_UploadedById] FOREIGN KEY ([UploadedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_JobCardDocument_JobCard_JobCardId] FOREIGN KEY ([JobCardId]) REFERENCES [JobCard] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_JobCardDocument_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [WorkOrders] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderNumber] nvarchar(50) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(2000) NULL,
    [JobCardId] uniqueidentifier NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [WorkOrderTypeId] uniqueidentifier NOT NULL,
    [MaintenanceTypeId] uniqueidentifier NOT NULL,
    [PriorityLevelId] uniqueidentifier NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [MaintenanceLocation] nvarchar(20) NOT NULL,
    [AssignedTechnicianId] uniqueidentifier NULL,
    [AssignedTeamId] uniqueidentifier NULL,
    [RequestedStartDate] datetime2 NULL,
    [RequestedCompletionDate] datetime2 NULL,
    [ActualStartDate] datetime2 NULL,
    [ActualCompletionDate] datetime2 NULL,
    [EstimatedCost] decimal(18,2) NOT NULL,
    [ActualCost] decimal(18,2) NOT NULL,
    [EstimatedHours] float NOT NULL,
    [ActualHours] float NOT NULL,
    [RequestedById] uniqueidentifier NULL,
    [ApprovedById] uniqueidentifier NULL,
    [ApprovedAt] datetime2 NULL,
    [SupervisorId] uniqueidentifier NULL,
    [CompletedById] uniqueidentifier NULL,
    [QualityCheckedById] uniqueidentifier NULL,
    [ContractorId] uniqueidentifier NULL,
    [CompletionNotes] nvarchar(1000) NULL,
    [FailureCode] nvarchar(50) NULL,
    [CauseCode] nvarchar(50) NULL,
    [ActionCode] nvarchar(50) NULL,
    [SafetyRequirements] nvarchar(500) NULL,
    [RequiresPermit] bit NOT NULL,
    [RequiresLockout] bit NOT NULL,
    [RequiresConfinedSpaceEntry] bit NOT NULL,
    [ParentWorkOrderId] uniqueidentifier NULL,
    [MaintenanceScheduleId] uniqueidentifier NULL,
    [IsRecurring] bit NOT NULL,
    [CustomFields] nvarchar(max) NULL,
    [CustomFieldValues] nvarchar(max) NULL,
    [TechnicianId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrders] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrders_Employees_ApprovedById] FOREIGN KEY ([ApprovedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrders_Employees_AssignedTechnicianId] FOREIGN KEY ([AssignedTechnicianId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrders_Employees_CompletedById] FOREIGN KEY ([CompletedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrders_Employees_QualityCheckedById] FOREIGN KEY ([QualityCheckedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrders_Employees_RequestedById] FOREIGN KEY ([RequestedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrders_Employees_SupervisorId] FOREIGN KEY ([SupervisorId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrders_JobCard_JobCardId] FOREIGN KEY ([JobCardId]) REFERENCES [JobCard] ([Id]),
    CONSTRAINT [FK_WorkOrders_MaintenanceAssets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrders_MaintenanceContractor_ContractorId] FOREIGN KEY ([ContractorId]) REFERENCES [MaintenanceContractor] ([Id]),
    CONSTRAINT [FK_WorkOrders_MaintenanceSchedules_MaintenanceScheduleId] FOREIGN KEY ([MaintenanceScheduleId]) REFERENCES [MaintenanceSchedules] ([Id]),
    CONSTRAINT [FK_WorkOrders_MaintenanceTypes_MaintenanceTypeId] FOREIGN KEY ([MaintenanceTypeId]) REFERENCES [MaintenanceTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrders_PriorityLevels_PriorityLevelId] FOREIGN KEY ([PriorityLevelId]) REFERENCES [PriorityLevels] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrders_TechnicianTeams_AssignedTeamId] FOREIGN KEY ([AssignedTeamId]) REFERENCES [TechnicianTeams] ([Id]),
    CONSTRAINT [FK_WorkOrders_Technicians_TechnicianId] FOREIGN KEY ([TechnicianId]) REFERENCES [Technicians] ([Id]),
    CONSTRAINT [FK_WorkOrders_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrders_WorkOrderTypes_WorkOrderTypeId] FOREIGN KEY ([WorkOrderTypeId]) REFERENCES [WorkOrderTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrders_WorkOrders_ParentWorkOrderId] FOREIGN KEY ([ParentWorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [MaintenanceStaffSchedules] (
    [Id] uniqueidentifier NOT NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [StartDateTime] datetime2 NOT NULL,
    [EndDateTime] datetime2 NOT NULL,
    [ScheduleType] nvarchar(50) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [JobCardId] uniqueidentifier NULL,
    [TeamId] uniqueidentifier NULL,
    [WorkLocation] nvarchar(200) NULL,
    [Address] nvarchar(200) NULL,
    [Latitude] decimal(18,4) NULL,
    [Longitude] decimal(18,4) NULL,
    [RequiresTravel] bit NOT NULL,
    [DepartureTime] datetime2 NULL,
    [ArrivalTime] datetime2 NULL,
    [EstimatedTravelMinutes] int NULL,
    [ActualTravelMinutes] int NULL,
    [AssignedVehicleId] uniqueidentifier NULL,
    [TransportationType] nvarchar(100) NULL,
    [Notes] nvarchar(1000) NULL,
    [ActualStartTime] datetime2 NULL,
    [ActualEndTime] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MaintenanceStaffSchedules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceStaffSchedules_JobCard_JobCardId] FOREIGN KEY ([JobCardId]) REFERENCES [JobCard] ([Id]),
    CONSTRAINT [FK_MaintenanceStaffSchedules_MaintenanceAssets_AssignedVehicleId] FOREIGN KEY ([AssignedVehicleId]) REFERENCES [MaintenanceAssets] ([Id]),
    CONSTRAINT [FK_MaintenanceStaffSchedules_TechnicianTeams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [TechnicianTeams] ([Id]),
    CONSTRAINT [FK_MaintenanceStaffSchedules_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_MaintenanceStaffSchedules_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id])
);
GO

CREATE TABLE [SafetyComplianceRecords] (
    [Id] uniqueidentifier NOT NULL,
    [SafetyProtocolId] uniqueidentifier NOT NULL,
    [ProtocolId] uniqueidentifier NOT NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [ComplianceDate] datetime2 NOT NULL,
    [CheckDate] datetime2 NOT NULL,
    [ComplianceStatus] nvarchar(20) NOT NULL,
    [Notes] nvarchar(2000) NULL,
    [IsCompliant] bit NOT NULL,
    [ViolationType] nvarchar(50) NULL,
    [CorrectiveActions] nvarchar(2000) NULL,
    [CorrectiveActionDueDate] datetime2 NULL,
    [ChecklistItems] nvarchar(max) NULL,
    [Violations] nvarchar(max) NULL,
    [InspectorId] uniqueidentifier NULL,
    [InspectorNotes] nvarchar(2000) NULL,
    [VerifiedById] uniqueidentifier NULL,
    [VerifiedDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SafetyComplianceRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SafetyComplianceRecords_Employees_InspectorId] FOREIGN KEY ([InspectorId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_SafetyComplianceRecords_Employees_TechnicianId] FOREIGN KEY ([TechnicianId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SafetyComplianceRecords_Employees_VerifiedById] FOREIGN KEY ([VerifiedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_SafetyComplianceRecords_SafetyProtocols_SafetyProtocolId] FOREIGN KEY ([SafetyProtocolId]) REFERENCES [SafetyProtocols] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SafetyComplianceRecords_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SafetyComplianceRecords_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id])
);
GO

CREATE TABLE [TechnicianSchedules] (
    [Id] uniqueidentifier NOT NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [ScheduleType] nvarchar(50) NOT NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [ShiftId] uniqueidentifier NULL,
    [Title] nvarchar(200) NULL,
    [Description] nvarchar(1000) NULL,
    [Location] nvarchar(500) NULL,
    [Status] nvarchar(20) NOT NULL,
    [Priority] nvarchar(20) NOT NULL,
    [IsAllDay] bit NOT NULL,
    [IsRecurring] bit NOT NULL,
    [RecurrencePattern] nvarchar(100) NULL,
    [Notes] nvarchar(1000) NULL,
    [EstimatedHours] float NOT NULL,
    [ActualHours] float NOT NULL,
    [EmployeeId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TechnicianSchedules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TechnicianSchedules_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_TechnicianSchedules_TechnicianShifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [TechnicianShifts] ([Id]),
    CONSTRAINT [FK_TechnicianSchedules_Technicians_TechnicianId] FOREIGN KEY ([TechnicianId]) REFERENCES [Technicians] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TechnicianSchedules_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TechnicianSchedules_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id])
);
GO

CREATE TABLE [ToolCheckouts] (
    [Id] uniqueidentifier NOT NULL,
    [ToolId] uniqueidentifier NOT NULL,
    [CheckedOutById] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [JobCardId] uniqueidentifier NULL,
    [CheckoutDate] datetime2 NOT NULL,
    [ExpectedReturnDate] datetime2 NULL,
    [ActualReturnDate] datetime2 NULL,
    [CheckedInById] uniqueidentifier NULL,
    [Status] nvarchar(20) NOT NULL,
    [CheckoutNotes] nvarchar(1000) NULL,
    [ReturnNotes] nvarchar(1000) NULL,
    [ConditionOnCheckout] nvarchar(20) NULL,
    [ConditionOnReturn] nvarchar(20) NULL,
    [DamageReported] bit NOT NULL,
    [DamageDescription] nvarchar(2000) NULL,
    [DamageCost] decimal(18,2) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ToolCheckouts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ToolCheckouts_InventoryItems_ToolId] FOREIGN KEY ([ToolId]) REFERENCES [InventoryItems] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ToolCheckouts_JobCard_JobCardId] FOREIGN KEY ([JobCardId]) REFERENCES [JobCard] ([Id]),
    CONSTRAINT [FK_ToolCheckouts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ToolCheckouts_Users_CheckedInById] FOREIGN KEY ([CheckedInById]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_ToolCheckouts_Users_CheckedOutById] FOREIGN KEY ([CheckedOutById]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ToolCheckouts_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id])
);
GO

CREATE TABLE [WorkOrderComments] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Comment] nvarchar(2000) NOT NULL,
    [CommentType] nvarchar(20) NOT NULL,
    [IsInternal] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderComments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderComments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrderComments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrderComments_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorkOrderDocuments] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [FileName] nvarchar(200) NOT NULL,
    [FilePath] nvarchar(500) NOT NULL,
    [FileType] nvarchar(50) NULL,
    [FileSize] bigint NOT NULL,
    [Description] nvarchar(1000) NULL,
    [DocumentType] nvarchar(50) NOT NULL,
    [UploadedById] uniqueidentifier NOT NULL,
    [UploadedAt] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderDocuments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderDocuments_Employees_UploadedById] FOREIGN KEY ([UploadedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrderDocuments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrderDocuments_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorkOrderLabor] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [TechnicianId] uniqueidentifier NOT NULL,
    [StartTime] datetime2 NOT NULL,
    [EndTime] datetime2 NULL,
    [Hours] float NOT NULL,
    [HourlyRate] decimal(18,4) NOT NULL,
    [TotalCost] decimal(18,2) NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [LaborType] nvarchar(50) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderLabor] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderLabor_Employees_TechnicianId] FOREIGN KEY ([TechnicianId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrderLabor_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrderLabor_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorkOrderParts] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [InventoryItemId] uniqueidentifier NOT NULL,
    [ItemCode] nvarchar(100) NOT NULL,
    [ItemName] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [QuantityRequired] decimal(18,4) NOT NULL,
    [QuantityAllocated] decimal(18,4) NOT NULL,
    [QuantityUsed] decimal(18,4) NOT NULL,
    [QuantityReturned] decimal(18,4) NOT NULL,
    [UnitCost] decimal(18,2) NOT NULL,
    [TotalCost] decimal(18,2) NOT NULL,
    [WarehouseLocationId] uniqueidentifier NULL,
    [SerialNumber] nvarchar(100) NULL,
    [LotNumber] nvarchar(100) NULL,
    [Status] nvarchar(20) NOT NULL,
    [AllocationId] uniqueidentifier NULL,
    [AllocatedAt] datetime2 NULL,
    [PickedAt] datetime2 NULL,
    [UsedAt] datetime2 NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderParts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderParts_InventoryAllocations_AllocationId] FOREIGN KEY ([AllocationId]) REFERENCES [InventoryAllocations] ([Id]),
    CONSTRAINT [FK_WorkOrderParts_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_WorkOrderParts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrderParts_WarehouseLocations_WarehouseLocationId] FOREIGN KEY ([WarehouseLocationId]) REFERENCES [WarehouseLocations] ([Id]),
    CONSTRAINT [FK_WorkOrderParts_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorkOrderQualityChecks] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [ChecklistId] uniqueidentifier NOT NULL,
    [InspectorId] uniqueidentifier NOT NULL,
    [InspectionDate] datetime2 NOT NULL,
    [OverallResult] nvarchar(20) NOT NULL,
    [Score] int NOT NULL,
    [CheckResults] nvarchar(max) NOT NULL,
    [Notes] nvarchar(2000) NULL,
    [CorrectiveActions] nvarchar(2000) NULL,
    [RequiresFollowUp] bit NOT NULL,
    [FollowUpDueDate] datetime2 NULL,
    [AttachmentPaths] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderQualityChecks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderQualityChecks_QualityControlChecklists_ChecklistId] FOREIGN KEY ([ChecklistId]) REFERENCES [QualityControlChecklists] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrderQualityChecks_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorkOrderQualitySignOffs] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [SignOffLevel] int NOT NULL,
    [SignOffRole] nvarchar(100) NOT NULL,
    [SignOffById] uniqueidentifier NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [SignOffDate] datetime2 NULL,
    [Comments] nvarchar(2000) NULL,
    [Conditions] nvarchar(1000) NULL,
    [RejectionReason] nvarchar(1000) NULL,
    [QualityRating] int NULL,
    [SafetyCompliant] bit NOT NULL,
    [WorkmanshipSatisfactory] bit NOT NULL,
    [MaterialsAcceptable] bit NOT NULL,
    [TestingComplete] bit NOT NULL,
    [DocumentationComplete] bit NOT NULL,
    [DelegatedToId] uniqueidentifier NULL,
    [DelegatedDate] datetime2 NULL,
    [DelegationReason] nvarchar(500) NULL,
    [PhotoPaths] nvarchar(max) NULL,
    [DocumentPaths] nvarchar(max) NULL,
    [RequiresFollowUp] bit NOT NULL,
    [FollowUpDate] datetime2 NULL,
    [FollowUpInstructions] nvarchar(1000) NULL,
    [IsRequired] bit NOT NULL,
    [SortOrder] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderQualitySignOffs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderQualitySignOffs_Employees_DelegatedToId] FOREIGN KEY ([DelegatedToId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrderQualitySignOffs_Employees_SignOffById] FOREIGN KEY ([SignOffById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrderQualitySignOffs_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrderQualitySignOffs_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorkOrderRejections] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [RejectedById] uniqueidentifier NOT NULL,
    [RejectedDate] datetime2 NOT NULL,
    [RejectionType] nvarchar(50) NOT NULL,
    [RejectionReason] nvarchar(2000) NOT NULL,
    [Severity] nvarchar(20) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [ReworkAssignedToId] uniqueidentifier NULL,
    [ReworkAssignedDate] datetime2 NULL,
    [ReworkDueDate] datetime2 NULL,
    [ReworkCompletedDate] datetime2 NULL,
    [EstimatedReworkCost] decimal(18,2) NOT NULL,
    [ActualReworkCost] decimal(18,2) NOT NULL,
    [EstimatedReworkHours] float NOT NULL,
    [ActualReworkHours] float NOT NULL,
    [CustomerNotified] bit NOT NULL,
    [CustomerNotifiedDate] datetime2 NULL,
    [AffectsDelivery] bit NOT NULL,
    [RevisedDeliveryDate] datetime2 NULL,
    [ResolutionNotes] nvarchar(2000) NULL,
    [ResolvedById] uniqueidentifier NULL,
    [ResolvedDate] datetime2 NULL,
    [RequiresReinspection] bit NOT NULL,
    [ReinspectedById] uniqueidentifier NULL,
    [ReinspectedDate] datetime2 NULL,
    [ReinspectionResult] nvarchar(20) NULL,
    [PhotoPaths] nvarchar(max) NULL,
    [DocumentPaths] nvarchar(max) NULL,
    [IsEscalated] bit NOT NULL,
    [EscalatedToId] uniqueidentifier NULL,
    [EscalatedDate] datetime2 NULL,
    [EscalationReason] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderRejections] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderRejections_Employees_EscalatedToId] FOREIGN KEY ([EscalatedToId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrderRejections_Employees_ReinspectedById] FOREIGN KEY ([ReinspectedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrderRejections_Employees_RejectedById] FOREIGN KEY ([RejectedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrderRejections_Employees_ResolvedById] FOREIGN KEY ([ResolvedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrderRejections_Employees_ReworkAssignedToId] FOREIGN KEY ([ReworkAssignedToId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrderRejections_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrderRejections_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorkOrderTasks] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [TaskName] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Sequence] int NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [EstimatedHours] float NOT NULL,
    [ActualHours] float NOT NULL,
    [AssignedTechnicianId] uniqueidentifier NULL,
    [StartedAt] datetime2 NULL,
    [CompletedAt] datetime2 NULL,
    [CompletionNotes] nvarchar(1000) NULL,
    [IsRequired] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderTasks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderTasks_Employees_AssignedTechnicianId] FOREIGN KEY ([AssignedTechnicianId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_WorkOrderTasks_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrderTasks_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [MaintenanceExpenses] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [ScheduleId] uniqueidentifier NULL,
    [TechnicianId] uniqueidentifier NULL,
    [ExpenseType] nvarchar(50) NOT NULL,
    [Description] nvarchar(200) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [ExpenseDate] datetime2 NOT NULL,
    [MileageDriven] decimal(18,4) NULL,
    [MileageRate] decimal(18,4) NULL,
    [FuelQuantity] decimal(18,4) NULL,
    [FuelPricePerUnit] decimal(18,2) NULL,
    [ReceiptPath] nvarchar(500) NULL,
    [VendorName] nvarchar(100) NULL,
    [ReferenceNumber] nvarchar(50) NULL,
    [Status] nvarchar(20) NOT NULL,
    [ApprovedById] uniqueidentifier NULL,
    [ApprovedDate] datetime2 NULL,
    [ApprovalNotes] nvarchar(1000) NULL,
    [IsReimbursable] bit NOT NULL,
    [IsReimbursed] bit NOT NULL,
    [ReimbursedDate] datetime2 NULL,
    [VehicleId] uniqueidentifier NULL,
    [Location] nvarchar(200) NULL,
    [Latitude] decimal(18,4) NULL,
    [Longitude] decimal(18,4) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MaintenanceExpenses] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceExpenses_Employees_ApprovedById] FOREIGN KEY ([ApprovedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_MaintenanceExpenses_Employees_TechnicianId] FOREIGN KEY ([TechnicianId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_MaintenanceExpenses_MaintenanceAssets_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [MaintenanceAssets] ([Id]),
    CONSTRAINT [FK_MaintenanceExpenses_MaintenanceStaffSchedules_ScheduleId] FOREIGN KEY ([ScheduleId]) REFERENCES [MaintenanceStaffSchedules] ([Id]),
    CONSTRAINT [FK_MaintenanceExpenses_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_MaintenanceExpenses_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorkOrderTools] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [ToolId] uniqueidentifier NOT NULL,
    [IsRequired] bit NOT NULL,
    [IsAllocated] bit NOT NULL,
    [AllocationDate] datetime2 NULL,
    [CheckoutId] uniqueidentifier NULL,
    [AllocationId] uniqueidentifier NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderTools] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderTools_InventoryAllocations_AllocationId] FOREIGN KEY ([AllocationId]) REFERENCES [InventoryAllocations] ([Id]),
    CONSTRAINT [FK_WorkOrderTools_InventoryItems_ToolId] FOREIGN KEY ([ToolId]) REFERENCES [InventoryItems] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_WorkOrderTools_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrderTools_ToolCheckouts_CheckoutId] FOREIGN KEY ([CheckoutId]) REFERENCES [ToolCheckouts] ([Id]),
    CONSTRAINT [FK_WorkOrderTools_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorkOrderReworks] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [InspectorId] uniqueidentifier NOT NULL,
    [ReworkReason] nvarchar(50) NOT NULL,
    [Description] nvarchar(2000) NOT NULL,
    [Severity] nvarchar(20) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [AssignedTechnicianId] uniqueidentifier NULL,
    [IdentifiedDate] datetime2 NOT NULL,
    [TargetCompletionDate] datetime2 NULL,
    [StartedDate] datetime2 NULL,
    [CompletedDate] datetime2 NULL,
    [EstimatedHours] float NULL,
    [ActualHours] float NULL,
    [AdditionalCost] decimal(18,2) NULL,
    [Notes] nvarchar(2000) NULL,
    [AttachmentPaths] nvarchar(max) NULL,
    [RequiresCustomerNotification] bit NOT NULL,
    [AffectsWarranty] bit NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [WorkOrderQualityCheckId] uniqueidentifier NULL,
    CONSTRAINT [PK_WorkOrderReworks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderReworks_WorkOrderQualityChecks_WorkOrderQualityCheckId] FOREIGN KEY ([WorkOrderQualityCheckId]) REFERENCES [WorkOrderQualityChecks] ([Id]),
    CONSTRAINT [FK_WorkOrderReworks_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [QualitySignOffChecklists] (
    [Id] uniqueidentifier NOT NULL,
    [SignOffId] uniqueidentifier NOT NULL,
    [CheckItem] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Category] nvarchar(50) NOT NULL,
    [CheckType] nvarchar(20) NOT NULL,
    [IsRequired] bit NOT NULL,
    [SortOrder] int NOT NULL,
    [BooleanResult] bit NULL,
    [RatingResult] int NULL,
    [MeasurementResult] decimal(18,4) NULL,
    [MeasurementUnit] nvarchar(50) NULL,
    [TextResult] nvarchar(1000) NULL,
    [Notes] nvarchar(1000) NULL,
    [PhotoPaths] nvarchar(max) NULL,
    [CheckedDate] datetime2 NULL,
    [CheckedById] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_QualitySignOffChecklists] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_QualitySignOffChecklists_Employees_CheckedById] FOREIGN KEY ([CheckedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_QualitySignOffChecklists_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_QualitySignOffChecklists_WorkOrderQualitySignOffs_SignOffId] FOREIGN KEY ([SignOffId]) REFERENCES [WorkOrderQualitySignOffs] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [RejectionFollowUps] (
    [Id] uniqueidentifier NOT NULL,
    [RejectionId] uniqueidentifier NOT NULL,
    [FollowUpAction] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [AssignedToId] uniqueidentifier NOT NULL,
    [DueDate] datetime2 NULL,
    [Status] nvarchar(20) NOT NULL,
    [CompletedDate] datetime2 NULL,
    [CompletedById] uniqueidentifier NULL,
    [CompletionNotes] nvarchar(1000) NULL,
    [Priority] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_RejectionFollowUps] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RejectionFollowUps_Employees_AssignedToId] FOREIGN KEY ([AssignedToId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_RejectionFollowUps_Employees_CompletedById] FOREIGN KEY ([CompletedById]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_RejectionFollowUps_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RejectionFollowUps_WorkOrderRejections_RejectionId] FOREIGN KEY ([RejectionId]) REFERENCES [WorkOrderRejections] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorkOrderReworkTasks] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderReworkId] uniqueidentifier NOT NULL,
    [TaskDescription] nvarchar(200) NOT NULL,
    [Sequence] int NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [EstimatedHours] float NULL,
    [ActualHours] float NULL,
    [CompletedDate] datetime2 NULL,
    [CompletedById] uniqueidentifier NULL,
    [Notes] nvarchar(1000) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkOrderReworkTasks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderReworkTasks_WorkOrderReworks_WorkOrderReworkId] FOREIGN KEY ([WorkOrderReworkId]) REFERENCES [WorkOrderReworks] ([Id]) ON DELETE CASCADE
);
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'CreatedAt', N'CreatedBy', N'Description', N'IsSystemRole', N'Name', N'NormalizedName', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[AspNetRoles]'))
    SET IDENTITY_INSERT [AspNetRoles] ON;
INSERT INTO [AspNetRoles] ([Id], [ConcurrencyStamp], [CreatedAt], [CreatedBy], [Description], [IsSystemRole], [Name], [NormalizedName], [UpdatedAt], [UpdatedBy])
VALUES ('00000000-0000-0000-0000-000000000001', NULL, '2025-12-26T07:09:49.6158512Z', NULL, NULL, CAST(1 AS bit), N'SuperAdmin', N'SUPERADMIN', NULL, NULL),
('00000000-0000-0000-0000-000000000002', NULL, '2025-12-26T07:09:49.6158569Z', NULL, NULL, CAST(1 AS bit), N'TenantAdmin', N'TENANTADMIN', NULL, NULL),
('00000000-0000-0000-0000-000000000003', NULL, '2025-12-26T07:09:49.6158572Z', NULL, NULL, CAST(1 AS bit), N'Manager', N'MANAGER', NULL, NULL),
('00000000-0000-0000-0000-000000000004', NULL, '2025-12-26T07:09:49.6158574Z', NULL, NULL, CAST(1 AS bit), N'Employee', N'EMPLOYEE', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'CreatedAt', N'CreatedBy', N'Description', N'IsSystemRole', N'Name', N'NormalizedName', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[AspNetRoles]'))
    SET IDENTITY_INSERT [AspNetRoles] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Category', N'CreatedAt', N'CreatedBy', N'CreatedById', N'DeletedAt', N'DeletedBy', N'Description', N'DisplayName', N'IsDeleted', N'IsSystemPermission', N'LastModifiedById', N'Name', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Permissions]'))
    SET IDENTITY_INSERT [Permissions] ON;
INSERT INTO [Permissions] ([Id], [Category], [CreatedAt], [CreatedBy], [CreatedById], [DeletedAt], [DeletedBy], [Description], [DisplayName], [IsDeleted], [IsSystemPermission], [LastModifiedById], [Name], [UpdatedAt], [UpdatedBy])
VALUES ('00000000-0000-0000-0000-000000000001', N'User Management', '2025-12-26T07:09:49.6158921Z', NULL, NULL, NULL, NULL, N'View user accounts and details', N'View Users', CAST(0 AS bit), CAST(1 AS bit), NULL, N'users.read', NULL, NULL),
('00000000-0000-0000-0000-000000000002', N'User Management', '2025-12-26T07:09:49.6158933Z', NULL, NULL, NULL, NULL, N'Create new user accounts', N'Create Users', CAST(0 AS bit), CAST(1 AS bit), NULL, N'users.create', NULL, NULL),
('00000000-0000-0000-0000-000000000003', N'User Management', '2025-12-26T07:09:49.6158942Z', NULL, NULL, NULL, NULL, N'Edit existing user accounts', N'Update Users', CAST(0 AS bit), CAST(1 AS bit), NULL, N'users.update', NULL, NULL),
('00000000-0000-0000-0000-000000000004', N'User Management', '2025-12-26T07:09:49.6158951Z', NULL, NULL, NULL, NULL, N'Delete user accounts', N'Delete Users', CAST(0 AS bit), CAST(1 AS bit), NULL, N'users.delete', NULL, NULL),
('00000000-0000-0000-0000-000000000005', N'Role Management', '2025-12-26T07:09:49.6159014Z', NULL, NULL, NULL, NULL, N'View role definitions', N'View Roles', CAST(0 AS bit), CAST(1 AS bit), NULL, N'roles.read', NULL, NULL),
('00000000-0000-0000-0000-000000000006', N'Role Management', '2025-12-26T07:09:49.6159023Z', NULL, NULL, NULL, NULL, N'Create new roles', N'Create Roles', CAST(0 AS bit), CAST(1 AS bit), NULL, N'roles.create', NULL, NULL),
('00000000-0000-0000-0000-000000000007', N'Role Management', '2025-12-26T07:09:49.6159042Z', NULL, NULL, NULL, NULL, N'Edit existing roles', N'Update Roles', CAST(0 AS bit), CAST(1 AS bit), NULL, N'roles.update', NULL, NULL),
('00000000-0000-0000-0000-000000000008', N'Role Management', '2025-12-26T07:09:49.6159050Z', NULL, NULL, NULL, NULL, N'Delete roles', N'Delete Roles', CAST(0 AS bit), CAST(1 AS bit), NULL, N'roles.delete', NULL, NULL),
('00000000-0000-0000-0000-000000000009', N'Dashboard & Reports', '2025-12-26T07:09:49.6159070Z', NULL, NULL, NULL, NULL, N'Access main dashboard', N'View Dashboard', CAST(0 AS bit), CAST(1 AS bit), NULL, N'dashboard.read', NULL, NULL),
('00000000-0000-0000-0000-000000000010', N'Dashboard & Reports', '2025-12-26T07:09:49.6159083Z', NULL, NULL, NULL, NULL, N'Access reporting features', N'View Reports', CAST(0 AS bit), CAST(1 AS bit), NULL, N'reports.read', NULL, NULL),
('00000000-0000-0000-0000-000000000011', N'Dashboard & Reports', '2025-12-26T07:09:49.6159098Z', NULL, NULL, NULL, NULL, N'Generate custom reports', N'Create Reports', CAST(0 AS bit), CAST(1 AS bit), NULL, N'reports.create', NULL, NULL),
('00000000-0000-0000-0000-000000000012', N'Dashboard & Reports', '2025-12-26T07:09:49.6159116Z', NULL, NULL, NULL, NULL, N'Access analytics data', N'View Analytics', CAST(0 AS bit), CAST(1 AS bit), NULL, N'analytics.read', NULL, NULL),
('00000000-0000-0000-0000-000000000013', N'System Administration', '2025-12-26T07:09:49.6159133Z', NULL, NULL, NULL, NULL, N'Access admin interface', N'View Admin', CAST(0 AS bit), CAST(1 AS bit), NULL, N'admin.read', NULL, NULL),
('00000000-0000-0000-0000-000000000014', N'System Administration', '2025-12-26T07:09:49.6159142Z', NULL, NULL, NULL, NULL, N'View system settings', N'View Settings', CAST(0 AS bit), CAST(1 AS bit), NULL, N'settings.read', NULL, NULL),
('00000000-0000-0000-0000-000000000015', N'System Administration', '2025-12-26T07:09:49.6159149Z', NULL, NULL, NULL, NULL, N'Modify system settings', N'Update Settings', CAST(0 AS bit), CAST(1 AS bit), NULL, N'settings.update', NULL, NULL),
('00000000-0000-0000-0000-000000000016', N'System Administration', '2025-12-26T07:09:49.6159156Z', NULL, NULL, NULL, NULL, N'Access audit trail', N'View Audit Logs', CAST(0 AS bit), CAST(1 AS bit), NULL, N'audit.read', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Category', N'CreatedAt', N'CreatedBy', N'CreatedById', N'DeletedAt', N'DeletedBy', N'Description', N'DisplayName', N'IsDeleted', N'IsSystemPermission', N'LastModifiedById', N'Name', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Permissions]'))
    SET IDENTITY_INSERT [Permissions] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Address', N'AllowSelfRegistration', N'BaseCurrency', N'BaseCurrencyName', N'Code', N'ContactEmail', N'ContactPhone', N'CoverImageUrl', N'CreatedAt', N'CreatedBy', N'CreatedById', N'CurrencyDecimalPlaces', N'CurrencySymbol', N'DefaultPriority', N'DeletedAt', N'DeletedBy', N'Description', N'Domain', N'EnableAutoSelection', N'FaviconUrl', N'IsDefaultForInternalUsers', N'IsDefaultForPublicUsers', N'IsDeleted', N'LastModifiedById', N'LdapBaseDn', N'LdapBindDn', N'LdapBindPassword', N'LdapEnabled', N'LdapPort', N'LdapServer', N'LogoUrl', N'Name', N'PrimaryColor', N'PublicRegistrationDomains', N'RequireEmailVerification', N'SecondaryColor', N'Status', N'SubscriptionEndDate', N'SubscriptionStartDate', N'UpdatedAt', N'UpdatedBy', N'UserAudience', N'WelcomeMessage') AND [object_id] = OBJECT_ID(N'[Tenants]'))
    SET IDENTITY_INSERT [Tenants] ON;
INSERT INTO [Tenants] ([Id], [Address], [AllowSelfRegistration], [BaseCurrency], [BaseCurrencyName], [Code], [ContactEmail], [ContactPhone], [CoverImageUrl], [CreatedAt], [CreatedBy], [CreatedById], [CurrencyDecimalPlaces], [CurrencySymbol], [DefaultPriority], [DeletedAt], [DeletedBy], [Description], [Domain], [EnableAutoSelection], [FaviconUrl], [IsDefaultForInternalUsers], [IsDefaultForPublicUsers], [IsDeleted], [LastModifiedById], [LdapBaseDn], [LdapBindDn], [LdapBindPassword], [LdapEnabled], [LdapPort], [LdapServer], [LogoUrl], [Name], [PrimaryColor], [PublicRegistrationDomains], [RequireEmailVerification], [SecondaryColor], [Status], [SubscriptionEndDate], [SubscriptionStartDate], [UpdatedAt], [UpdatedBy], [UserAudience], [WelcomeMessage])
VALUES ('00000000-0000-0000-0000-000000000001', NULL, CAST(0 AS bit), N'GHS', NULL, N'DEFAULT', NULL, NULL, NULL, '2025-12-26T07:09:49.6158317Z', NULL, NULL, 2, NULL, 10, NULL, NULL, N'Default system tenant', NULL, CAST(0 AS bit), NULL, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, NULL, NULL, CAST(0 AS bit), NULL, NULL, NULL, N'Default Tenant', NULL, NULL, CAST(0 AS bit), NULL, 1, NULL, NULL, NULL, NULL, 2, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Address', N'AllowSelfRegistration', N'BaseCurrency', N'BaseCurrencyName', N'Code', N'ContactEmail', N'ContactPhone', N'CoverImageUrl', N'CreatedAt', N'CreatedBy', N'CreatedById', N'CurrencyDecimalPlaces', N'CurrencySymbol', N'DefaultPriority', N'DeletedAt', N'DeletedBy', N'Description', N'Domain', N'EnableAutoSelection', N'FaviconUrl', N'IsDefaultForInternalUsers', N'IsDefaultForPublicUsers', N'IsDeleted', N'LastModifiedById', N'LdapBaseDn', N'LdapBindDn', N'LdapBindPassword', N'LdapEnabled', N'LdapPort', N'LdapServer', N'LogoUrl', N'Name', N'PrimaryColor', N'PublicRegistrationDomains', N'RequireEmailVerification', N'SecondaryColor', N'Status', N'SubscriptionEndDate', N'SubscriptionStartDate', N'UpdatedAt', N'UpdatedBy', N'UserAudience', N'WelcomeMessage') AND [object_id] = OBJECT_ID(N'[Tenants]'))
    SET IDENTITY_INSERT [Tenants] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PermissionId', N'RoleId', N'GrantedAt', N'GrantedBy') AND [object_id] = OBJECT_ID(N'[RolePermissions]'))
    SET IDENTITY_INSERT [RolePermissions] ON;
INSERT INTO [RolePermissions] ([PermissionId], [RoleId], [GrantedAt], [GrantedBy])
VALUES ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001', '2025-12-26T07:09:49.6159215Z', N'System'),
('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', '2025-12-26T07:09:49.6159218Z', N'System'),
('00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001', '2025-12-26T07:09:49.6159219Z', N'System'),
('00000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000001', '2025-12-26T07:09:49.6159220Z', N'System'),
('00000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000001', '2025-12-26T07:09:49.6159221Z', N'System'),
('00000000-0000-0000-0000-000000000006', '00000000-0000-0000-0000-000000000001', '2025-12-26T07:09:49.6159223Z', N'System'),
('00000000-0000-0000-0000-000000000007', '00000000-0000-0000-0000-000000000001', '2025-12-26T07:09:49.6159224Z', N'System'),
('00000000-0000-0000-0000-000000000008', '00000000-0000-0000-0000-000000000001', '2025-12-26T07:09:49.6159225Z', N'System'),
('00000000-0000-0000-0000-000000000009', '00000000-0000-0000-0000-000000000001', '2025-12-26T07:09:49.6159227Z', N'System'),
('00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000001', '2025-12-26T07:09:49.6159228Z', N'System'),
('00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000001', '2025-12-26T07:09:49.6159229Z', N'System'),
('00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000001', '2025-12-26T07:09:49.6159230Z', N'System'),
('00000000-0000-0000-0000-000000000013', '00000000-0000-0000-0000-000000000001', '2025-12-26T07:09:49.6159231Z', N'System'),
('00000000-0000-0000-0000-000000000014', '00000000-0000-0000-0000-000000000001', '2025-12-26T07:09:49.6159232Z', N'System'),
('00000000-0000-0000-0000-000000000015', '00000000-0000-0000-0000-000000000001', '2025-12-26T07:09:49.6159233Z', N'System'),
('00000000-0000-0000-0000-000000000016', '00000000-0000-0000-0000-000000000001', '2025-12-26T07:09:49.6159233Z', N'System'),
('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000002', '2025-12-26T07:09:49.6159284Z', N'System'),
('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000002', '2025-12-26T07:09:49.6159287Z', N'System'),
('00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000002', '2025-12-26T07:09:49.6159288Z', N'System'),
('00000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000002', '2025-12-26T07:09:49.6159289Z', N'System'),
('00000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000002', '2025-12-26T07:09:49.6159289Z', N'System'),
('00000000-0000-0000-0000-000000000006', '00000000-0000-0000-0000-000000000002', '2025-12-26T07:09:49.6159290Z', N'System'),
('00000000-0000-0000-0000-000000000007', '00000000-0000-0000-0000-000000000002', '2025-12-26T07:09:49.6159291Z', N'System'),
('00000000-0000-0000-0000-000000000008', '00000000-0000-0000-0000-000000000002', '2025-12-26T07:09:49.6159292Z', N'System'),
('00000000-0000-0000-0000-000000000009', '00000000-0000-0000-0000-000000000002', '2025-12-26T07:09:49.6159293Z', N'System'),
('00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000002', '2025-12-26T07:09:49.6159294Z', N'System'),
('00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000002', '2025-12-26T07:09:49.6159295Z', N'System'),
('00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000002', '2025-12-26T07:09:49.6159296Z', N'System'),
('00000000-0000-0000-0000-000000000014', '00000000-0000-0000-0000-000000000002', '2025-12-26T07:09:49.6159296Z', N'System'),
('00000000-0000-0000-0000-000000000015', '00000000-0000-0000-0000-000000000002', '2025-12-26T07:09:49.6159297Z', N'System'),
('00000000-0000-0000-0000-000000000016', '00000000-0000-0000-0000-000000000002', '2025-12-26T07:09:49.6159298Z', N'System'),
('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000003', '2025-12-26T07:09:49.6159378Z', N'System'),
('00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000003', '2025-12-26T07:09:49.6159386Z', N'System'),
('00000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000003', '2025-12-26T07:09:49.6159388Z', N'System'),
('00000000-0000-0000-0000-000000000009', '00000000-0000-0000-0000-000000000003', '2025-12-26T07:09:49.6159389Z', N'System'),
('00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000003', '2025-12-26T07:09:49.6159390Z', N'System'),
('00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000003', '2025-12-26T07:09:49.6159391Z', N'System'),
('00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000003', '2025-12-26T07:09:49.6159392Z', N'System'),
('00000000-0000-0000-0000-000000000013', '00000000-0000-0000-0000-000000000003', '2025-12-26T07:09:49.6159393Z', N'System'),
('00000000-0000-0000-0000-000000000014', '00000000-0000-0000-0000-000000000003', '2025-12-26T07:09:49.6159394Z', N'System'),
('00000000-0000-0000-0000-000000000016', '00000000-0000-0000-0000-000000000003', '2025-12-26T07:09:49.6159394Z', N'System'),
('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000004', '2025-12-26T07:09:49.6159409Z', N'System');
INSERT INTO [RolePermissions] ([PermissionId], [RoleId], [GrantedAt], [GrantedBy])
VALUES ('00000000-0000-0000-0000-000000000009', '00000000-0000-0000-0000-000000000004', '2025-12-26T07:09:49.6159411Z', N'System'),
('00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000004', '2025-12-26T07:09:49.6159412Z', N'System');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PermissionId', N'RoleId', N'GrantedAt', N'GrantedBy') AND [object_id] = OBJECT_ID(N'[RolePermissions]'))
    SET IDENTITY_INSERT [RolePermissions] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'CreatedById', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'LastModifiedById', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] ON;
INSERT INTO [TenantModules] ([Id], [Configuration], [CreatedAt], [CreatedBy], [CreatedById], [DeletedAt], [DeletedBy], [Description], [DisabledDate], [EnabledDate], [IsDeleted], [LastModifiedById], [ModuleName], [Status], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('32f274dc-d0ed-4fe1-bd84-5c6f6621b72c', NULL, '2025-12-26T07:09:49.6158651Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-12-26T07:09:49.6158645Z', CAST(0 AS bit), NULL, N'Finance', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('38136db3-da01-47e6-a3cf-5b251284efe9', NULL, '2025-12-26T07:09:49.6158701Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-12-26T07:09:49.6158701Z', CAST(0 AS bit), NULL, N'Procurement', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('6e3484ac-56ba-4e71-964d-f9e73876b20b', NULL, '2025-12-26T07:09:49.6158745Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-12-26T07:09:49.6158745Z', CAST(0 AS bit), NULL, N'WorkflowEngine', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('77bad3f9-418e-4f1c-a17c-ada05161848e', NULL, '2025-12-26T07:09:49.6158715Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-12-26T07:09:49.6158715Z', CAST(0 AS bit), NULL, N'Inventory', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('c49d7a52-1f73-44e0-80ba-c9a746945c6a', NULL, '2025-12-26T07:09:49.6158687Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-12-26T07:09:49.6158686Z', CAST(0 AS bit), NULL, N'Sales', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('c8184437-32b9-4b5d-8242-8dfc160f3445', NULL, '2025-12-26T07:09:49.6158672Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-12-26T07:09:49.6158671Z', CAST(0 AS bit), NULL, N'HR', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL),
('c84729f9-3dbf-4637-a972-72b3e1be7c1e', NULL, '2025-12-26T07:09:49.6158731Z', NULL, NULL, NULL, NULL, NULL, NULL, '2025-12-26T07:09:49.6158730Z', CAST(0 AS bit), NULL, N'Marketing', 1, '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Configuration', N'CreatedAt', N'CreatedBy', N'CreatedById', N'DeletedAt', N'DeletedBy', N'Description', N'DisabledDate', N'EnabledDate', N'IsDeleted', N'LastModifiedById', N'ModuleName', N'Status', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[TenantModules]'))
    SET IDENTITY_INSERT [TenantModules] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAt', N'CreatedBy', N'CreatedById', N'DecimalPlaces', N'DeletedAt', N'DeletedBy', N'Description', N'IsActive', N'IsDeleted', N'LastModifiedById', N'Name', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[UnitTypes]'))
    SET IDENTITY_INSERT [UnitTypes] ON;
INSERT INTO [UnitTypes] ([Id], [Code], [CreatedAt], [CreatedBy], [CreatedById], [DecimalPlaces], [DeletedAt], [DeletedBy], [Description], [IsActive], [IsDeleted], [LastModifiedById], [Name], [TenantId], [UpdatedAt], [UpdatedBy])
VALUES ('00000000-0000-0000-1001-000000000001', N'EMP', '2025-12-26T07:09:49.6158789Z', NULL, NULL, 0, NULL, NULL, N'Headcount/Full-Time Equivalents (FTE)', CAST(1 AS bit), CAST(0 AS bit), NULL, N'Employees', '00000000-0000-0000-0000-000000000001', NULL, NULL),
('00000000-0000-0000-1001-000000000002', N'SQFT', '2025-12-26T07:09:49.6158802Z', NULL, NULL, 2, NULL, NULL, N'Floor space area measurement', CAST(1 AS bit), CAST(0 AS bit), NULL, N'Square Feet', '00000000-0000-0000-0000-000000000001', NULL, NULL),
('00000000-0000-0000-1001-000000000003', N'HRS', '2025-12-26T07:09:49.6158809Z', NULL, NULL, 2, NULL, NULL, N'Time measurement in hours', CAST(1 AS bit), CAST(0 AS bit), NULL, N'Hours', '00000000-0000-0000-0000-000000000001', NULL, NULL),
('00000000-0000-0000-1001-000000000004', N'UNITS', '2025-12-26T07:09:49.6158812Z', NULL, NULL, 0, NULL, NULL, N'Generic unit count (production, sales, etc.)', CAST(1 AS bit), CAST(0 AS bit), NULL, N'Units', '00000000-0000-0000-0000-000000000001', NULL, NULL),
('00000000-0000-0000-1001-000000000005', N'PCT', '2025-12-26T07:09:49.6158816Z', NULL, NULL, 2, NULL, NULL, N'Percentage values (0-100)', CAST(1 AS bit), CAST(0 AS bit), NULL, N'Percentage', '00000000-0000-0000-0000-000000000001', NULL, NULL),
('00000000-0000-0000-1001-000000000006', N'KWH', '2025-12-26T07:09:49.6158820Z', NULL, NULL, 2, NULL, NULL, N'Energy consumption measurement', CAST(1 AS bit), CAST(0 AS bit), NULL, N'Kilowatt Hours', '00000000-0000-0000-0000-000000000001', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAt', N'CreatedBy', N'CreatedById', N'DecimalPlaces', N'DeletedAt', N'DeletedBy', N'Description', N'IsActive', N'IsDeleted', N'LastModifiedById', N'Name', N'TenantId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[UnitTypes]'))
    SET IDENTITY_INSERT [UnitTypes] OFF;
GO

CREATE INDEX [IX_AccountBalances_AccountId] ON [AccountBalances] ([AccountId]);
GO

CREATE INDEX [IX_AccountBalances_FiscalPeriodId] ON [AccountBalances] ([FiscalPeriodId]);
GO

CREATE INDEX [IX_AccountBalances_TenantId] ON [AccountBalances] ([TenantId]);
GO

CREATE INDEX [IX_AccountCurrencyLinks_AccountId] ON [AccountCurrencyLinks] ([AccountId]);
GO

CREATE INDEX [IX_AccountCurrencyLinks_TenantId] ON [AccountCurrencyLinks] ([TenantId]);
GO

CREATE INDEX [IX_Accounts_ParentAccountId] ON [Accounts] ([ParentAccountId]);
GO

CREATE INDEX [IX_Accounts_TenantId] ON [Accounts] ([TenantId]);
GO

CREATE INDEX [IX_AccountSegmentStructures_TenantId] ON [AccountSegmentStructures] ([TenantId]);
GO

CREATE INDEX [IX_AccountSegmentValues_AccountId] ON [AccountSegmentValues] ([AccountId]);
GO

CREATE INDEX [IX_AccountSegmentValues_SegmentLookupValueId] ON [AccountSegmentValues] ([SegmentLookupValueId]);
GO

CREATE INDEX [IX_AccountSegmentValues_SegmentStructureId] ON [AccountSegmentValues] ([SegmentStructureId]);
GO

CREATE INDEX [IX_AccountSegmentValues_TenantId] ON [AccountSegmentValues] ([TenantId]);
GO

CREATE INDEX [IX_AccountTransactions_AccountId] ON [AccountTransactions] ([AccountId]);
GO

CREATE INDEX [IX_AccountTransactions_FiscalPeriodId] ON [AccountTransactions] ([FiscalPeriodId]);
GO

CREATE INDEX [IX_AccountTransactions_JournalEntryId] ON [AccountTransactions] ([JournalEntryId]);
GO

CREATE INDEX [IX_AccountTransactions_OriginalTransactionId] ON [AccountTransactions] ([OriginalTransactionId]);
GO

CREATE INDEX [IX_AccountTransactions_ReversalTransactionId] ON [AccountTransactions] ([ReversalTransactionId]);
GO

CREATE INDEX [IX_AccountTransactions_TenantId] ON [AccountTransactions] ([TenantId]);
GO

CREATE INDEX [IX_AllocationRules_DriverUnitAccountId] ON [AllocationRules] ([DriverUnitAccountId]);
GO

CREATE INDEX [IX_AllocationRules_IsActive] ON [AllocationRules] ([IsActive]);
GO

CREATE INDEX [IX_AllocationRules_SourceAccountId] ON [AllocationRules] ([SourceAccountId]);
GO

CREATE UNIQUE INDEX [IX_AllocationRules_TenantId_Code] ON [AllocationRules] ([TenantId], [Code]);
GO

CREATE INDEX [IX_AllocationTargets_AllocationRuleId] ON [AllocationTargets] ([AllocationRuleId]);
GO

CREATE INDEX [IX_AllocationTargets_TargetAccountId] ON [AllocationTargets] ([TargetAccountId]);
GO

CREATE INDEX [IX_AllocationTargets_TargetDriverUnitAccountId] ON [AllocationTargets] ([TargetDriverUnitAccountId]);
GO

CREATE INDEX [IX_AllocationTargets_TenantId] ON [AllocationTargets] ([TenantId]);
GO

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
GO

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
GO

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
GO

CREATE INDEX [IX_AssetAdmissions_AdmissionDate] ON [AssetAdmissions] ([AdmissionDate]);
GO

CREATE UNIQUE INDEX [IX_AssetAdmissions_AdmissionNumber] ON [AssetAdmissions] ([AdmissionNumber]);
GO

CREATE INDEX [IX_AssetAdmissions_AdmissionType] ON [AssetAdmissions] ([AdmissionType]);
GO

CREATE INDEX [IX_AssetAdmissions_AdmittedById] ON [AssetAdmissions] ([AdmittedById]);
GO

CREATE INDEX [IX_AssetAdmissions_AssetId] ON [AssetAdmissions] ([AssetId]);
GO

CREATE INDEX [IX_AssetAdmissions_JobCardId] ON [AssetAdmissions] ([JobCardId]);
GO

CREATE INDEX [IX_AssetAdmissions_Status] ON [AssetAdmissions] ([Status]);
GO

CREATE INDEX [IX_AssetAdmissions_TenantId] ON [AssetAdmissions] ([TenantId]);
GO

CREATE INDEX [IX_AssetAdmissions_WorkOrderId] ON [AssetAdmissions] ([WorkOrderId]);
GO

CREATE INDEX [IX_AssetDischarges_AcceptedById] ON [AssetDischarges] ([AcceptedById]);
GO

CREATE UNIQUE INDEX [IX_AssetDischarges_AdmissionId] ON [AssetDischarges] ([AdmissionId]);
GO

CREATE INDEX [IX_AssetDischarges_AssetId] ON [AssetDischarges] ([AssetId]);
GO

CREATE INDEX [IX_AssetDischarges_CustomerAcceptance] ON [AssetDischarges] ([CustomerAcceptance]);
GO

CREATE INDEX [IX_AssetDischarges_DischargeDate] ON [AssetDischarges] ([DischargeDate]);
GO

CREATE INDEX [IX_AssetDischarges_DischargedById] ON [AssetDischarges] ([DischargedById]);
GO

CREATE UNIQUE INDEX [IX_AssetDischarges_DischargeNumber] ON [AssetDischarges] ([DischargeNumber]);
GO

CREATE INDEX [IX_AssetDischarges_JobCardId] ON [AssetDischarges] ([JobCardId]);
GO

CREATE INDEX [IX_AssetDischarges_QualityCheckedById] ON [AssetDischarges] ([QualityCheckedById]);
GO

CREATE INDEX [IX_AssetDischarges_TenantId] ON [AssetDischarges] ([TenantId]);
GO

CREATE INDEX [IX_AssetDischarges_WorkOrderId] ON [AssetDischarges] ([WorkOrderId]);
GO

CREATE INDEX [IX_AssetDowntimes_AssetId] ON [AssetDowntimes] ([AssetId]);
GO

CREATE INDEX [IX_AssetDowntimes_Reason] ON [AssetDowntimes] ([Reason]);
GO

CREATE INDEX [IX_AssetDowntimes_StartTime] ON [AssetDowntimes] ([StartTime]);
GO

CREATE INDEX [IX_AssetDowntimes_Status] ON [AssetDowntimes] ([Status]);
GO

CREATE INDEX [IX_AssetDowntimes_TenantId] ON [AssetDowntimes] ([TenantId]);
GO

CREATE INDEX [IX_AssetDowntimes_WorkOrderId] ON [AssetDowntimes] ([WorkOrderId]);
GO

CREATE INDEX [IX_AssetInspections_AssetId] ON [AssetInspections] ([AssetId]);
GO

CREATE INDEX [IX_AssetInspections_InspectionDate] ON [AssetInspections] ([InspectionDate]);
GO

CREATE INDEX [IX_AssetInspections_InspectionTemplateId] ON [AssetInspections] ([InspectionTemplateId]);
GO

CREATE INDEX [IX_AssetInspections_InspectorId] ON [AssetInspections] ([InspectorId]);
GO

CREATE INDEX [IX_AssetInspections_IsRegulatoryRequired] ON [AssetInspections] ([IsRegulatoryRequired]);
GO

CREATE INDEX [IX_AssetInspections_NextInspectionDue] ON [AssetInspections] ([NextInspectionDue]);
GO

CREATE INDEX [IX_AssetInspections_OverallResult] ON [AssetInspections] ([OverallResult]);
GO

CREATE INDEX [IX_AssetInspections_Status] ON [AssetInspections] ([Status]);
GO

CREATE INDEX [IX_AssetInspections_TenantId] ON [AssetInspections] ([TenantId]);
GO

CREATE INDEX [IX_AssetMaintenanceDowntimes_AdmissionId] ON [AssetMaintenanceDowntimes] ([AdmissionId]);
GO

CREATE INDEX [IX_AssetMaintenanceDowntimes_AssetId] ON [AssetMaintenanceDowntimes] ([AssetId]);
GO

CREATE INDEX [IX_AssetMaintenanceDowntimes_DischargeId] ON [AssetMaintenanceDowntimes] ([DischargeId]);
GO

CREATE INDEX [IX_AssetMaintenanceDowntimes_DowntimeStart] ON [AssetMaintenanceDowntimes] ([DowntimeStart]);
GO

CREATE INDEX [IX_AssetMaintenanceDowntimes_JobCardId] ON [AssetMaintenanceDowntimes] ([JobCardId]);
GO

CREATE INDEX [IX_AssetMaintenanceDowntimes_Status] ON [AssetMaintenanceDowntimes] ([Status]);
GO

CREATE INDEX [IX_AssetMaintenanceDowntimes_TenantId] ON [AssetMaintenanceDowntimes] ([TenantId]);
GO

CREATE INDEX [IX_AssetMaintenanceDowntimes_WorkOrderId] ON [AssetMaintenanceDowntimes] ([WorkOrderId]);
GO

CREATE INDEX [IX_AssetTaskTemplates_AssetId] ON [AssetTaskTemplates] ([AssetId]);
GO

CREATE INDEX [IX_AssetTaskTemplates_AssignedTechnicianId] ON [AssetTaskTemplates] ([AssignedTechnicianId]);
GO

CREATE INDEX [IX_AssetTaskTemplates_MaintenanceTypeId] ON [AssetTaskTemplates] ([MaintenanceTypeId]);
GO

CREATE INDEX [IX_AssetTaskTemplates_TenantId] ON [AssetTaskTemplates] ([TenantId]);
GO

CREATE INDEX [IX_AssetTypeFields_AssetTypeId] ON [AssetTypeFields] ([AssetTypeId]);
GO

CREATE UNIQUE INDEX [IX_AssetTypeFields_AssetTypeId_FieldName] ON [AssetTypeFields] ([AssetTypeId], [FieldName]);
GO

CREATE INDEX [IX_AssetTypeFields_DisplayOrder] ON [AssetTypeFields] ([DisplayOrder]);
GO

CREATE INDEX [IX_AssetTypeFields_FieldName] ON [AssetTypeFields] ([FieldName]);
GO

CREATE INDEX [IX_AssetTypeFields_IsActive] ON [AssetTypeFields] ([IsActive]);
GO

CREATE INDEX [IX_AssetTypeFields_TenantId] ON [AssetTypeFields] ([TenantId]);
GO

CREATE UNIQUE INDEX [IX_AssetTypes_Code] ON [AssetTypes] ([Code]);
GO

CREATE INDEX [IX_AssetTypes_IsActive] ON [AssetTypes] ([IsActive]);
GO

CREATE UNIQUE INDEX [IX_AssetTypes_TenantId_Name] ON [AssetTypes] ([TenantId], [Name]);
GO

CREATE INDEX [IX_AssetTypeTaskTemplates_AssetTypeId] ON [AssetTypeTaskTemplates] ([AssetTypeId]);
GO

CREATE INDEX [IX_AssetTypeTaskTemplates_AssignedTechnicianId] ON [AssetTypeTaskTemplates] ([AssignedTechnicianId]);
GO

CREATE INDEX [IX_AssetTypeTaskTemplates_MaintenanceTypeId] ON [AssetTypeTaskTemplates] ([MaintenanceTypeId]);
GO

CREATE INDEX [IX_AssetTypeTaskTemplates_TenantId] ON [AssetTypeTaskTemplates] ([TenantId]);
GO

CREATE INDEX [IX_AssetUsageTrackings_AssetId] ON [AssetUsageTrackings] ([AssetId]);
GO

CREATE INDEX [IX_AssetUsageTrackings_RecordedById] ON [AssetUsageTrackings] ([RecordedById]);
GO

CREATE INDEX [IX_AssetUsageTrackings_TenantId] ON [AssetUsageTrackings] ([TenantId]);
GO

CREATE INDEX [IX_AttendanceRecords_EmployeeId] ON [AttendanceRecords] ([EmployeeId]);
GO

CREATE INDEX [IX_AttendanceRecords_TenantId] ON [AttendanceRecords] ([TenantId]);
GO

CREATE INDEX [IX_AuditLogs_Resource] ON [AuditLogs] ([Resource]);
GO

CREATE INDEX [IX_AuditLogs_TenantId] ON [AuditLogs] ([TenantId]);
GO

CREATE INDEX [IX_AuditLogs_Timestamp] ON [AuditLogs] ([Timestamp]);
GO

CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs] ([UserId]);
GO

CREATE INDEX [IX_BlacklistedTokens_ExpiresAt] ON [BlacklistedTokens] ([ExpiresAt]);
GO

CREATE UNIQUE INDEX [IX_BlacklistedTokens_Jti] ON [BlacklistedTokens] ([Jti]);
GO

CREATE INDEX [IX_BlacklistedTokens_UserId] ON [BlacklistedTokens] ([UserId]);
GO

CREATE UNIQUE INDEX [IX_BusinessPartnerCategories_BusinessPartnerId_CategoryId] ON [BusinessPartnerCategories] ([BusinessPartnerId], [CategoryId]);
GO

CREATE INDEX [IX_BusinessPartnerCategories_CategoryId] ON [BusinessPartnerCategories] ([CategoryId]);
GO

CREATE INDEX [IX_BusinessPartnerContacts_BusinessPartnerId] ON [BusinessPartnerContacts] ([BusinessPartnerId]);
GO

CREATE INDEX [IX_BusinessPartnerContacts_Email] ON [BusinessPartnerContacts] ([Email]);
GO

CREATE INDEX [IX_BusinessPartnerContacts_TenantId] ON [BusinessPartnerContacts] ([TenantId]);
GO

CREATE INDEX [IX_BusinessPartnerDocuments_BusinessPartnerId] ON [BusinessPartnerDocuments] ([BusinessPartnerId]);
GO

CREATE INDEX [IX_BusinessPartnerDocuments_DocumentType] ON [BusinessPartnerDocuments] ([DocumentType]);
GO

CREATE INDEX [IX_BusinessPartnerDocuments_ExpiryDate] ON [BusinessPartnerDocuments] ([ExpiryDate]);
GO

CREATE INDEX [IX_BusinessPartnerDocuments_IsVerified] ON [BusinessPartnerDocuments] ([IsVerified]);
GO

CREATE INDEX [IX_BusinessPartnerDocuments_TenantId] ON [BusinessPartnerDocuments] ([TenantId]);
GO

CREATE INDEX [IX_BusinessPartnerDocuments_UploadedById] ON [BusinessPartnerDocuments] ([UploadedById]);
GO

CREATE INDEX [IX_BusinessPartnerDocuments_VerifiedById] ON [BusinessPartnerDocuments] ([VerifiedById]);
GO

CREATE INDEX [IX_BusinessPartnerFinancials_BusinessPartnerId] ON [BusinessPartnerFinancials] ([BusinessPartnerId]);
GO

CREATE UNIQUE INDEX [IX_BusinessPartnerFinancials_BusinessPartnerId_FiscalYear] ON [BusinessPartnerFinancials] ([BusinessPartnerId], [FiscalYear]);
GO

CREATE INDEX [IX_BusinessPartnerFinancials_FiscalYear] ON [BusinessPartnerFinancials] ([FiscalYear]);
GO

CREATE INDEX [IX_BusinessPartnerFinancials_TenantId] ON [BusinessPartnerFinancials] ([TenantId]);
GO

CREATE INDEX [IX_BusinessPartnerLicenses_BusinessPartnerId] ON [BusinessPartnerLicenses] ([BusinessPartnerId]);
GO

CREATE INDEX [IX_BusinessPartnerLicenses_ExpiryDate] ON [BusinessPartnerLicenses] ([ExpiryDate]);
GO

CREATE INDEX [IX_BusinessPartnerLicenses_LicenseNumber] ON [BusinessPartnerLicenses] ([LicenseNumber]);
GO

CREATE INDEX [IX_BusinessPartnerLicenses_LicenseTypeId] ON [BusinessPartnerLicenses] ([LicenseTypeId]);
GO

CREATE INDEX [IX_BusinessPartnerLicenses_Status] ON [BusinessPartnerLicenses] ([Status]);
GO

CREATE INDEX [IX_BusinessPartnerLicenses_TenantId] ON [BusinessPartnerLicenses] ([TenantId]);
GO

CREATE INDEX [IX_BusinessPartnerRegistrationDocuments_DocumentType] ON [BusinessPartnerRegistrationDocuments] ([DocumentType]);
GO

CREATE INDEX [IX_BusinessPartnerRegistrationDocuments_IsVerified] ON [BusinessPartnerRegistrationDocuments] ([IsVerified]);
GO

CREATE INDEX [IX_BusinessPartnerRegistrationDocuments_RegistrationId] ON [BusinessPartnerRegistrationDocuments] ([RegistrationId]);
GO

CREATE INDEX [IX_BusinessPartnerRegistrationDocuments_TenantId] ON [BusinessPartnerRegistrationDocuments] ([TenantId]);
GO

CREATE INDEX [IX_BusinessPartnerRegistrationDocuments_VerifiedById] ON [BusinessPartnerRegistrationDocuments] ([VerifiedById]);
GO

CREATE INDEX [IX_BusinessPartnerRegistrations_ApplicantEmail] ON [BusinessPartnerRegistrations] ([ApplicantEmail]);
GO

CREATE INDEX [IX_BusinessPartnerRegistrations_ApprovedById] ON [BusinessPartnerRegistrations] ([ApprovedById]);
GO

CREATE INDEX [IX_BusinessPartnerRegistrations_BusinessPartnerId] ON [BusinessPartnerRegistrations] ([BusinessPartnerId]);
GO

CREATE UNIQUE INDEX [IX_BusinessPartnerRegistrations_RegistrationNumber] ON [BusinessPartnerRegistrations] ([RegistrationNumber]);
GO

CREATE INDEX [IX_BusinessPartnerRegistrations_ReviewedById] ON [BusinessPartnerRegistrations] ([ReviewedById]);
GO

CREATE INDEX [IX_BusinessPartnerRegistrations_Status] ON [BusinessPartnerRegistrations] ([Status]);
GO

CREATE INDEX [IX_BusinessPartnerRegistrations_SubmittedDate] ON [BusinessPartnerRegistrations] ([SubmittedDate]);
GO

CREATE INDEX [IX_BusinessPartnerRegistrations_TenantId] ON [BusinessPartnerRegistrations] ([TenantId]);
GO

CREATE INDEX [IX_BusinessPartnerRegistrationStatusHistories_ChangedAt] ON [BusinessPartnerRegistrationStatusHistories] ([ChangedAt]);
GO

CREATE INDEX [IX_BusinessPartnerRegistrationStatusHistories_ChangedById] ON [BusinessPartnerRegistrationStatusHistories] ([ChangedById]);
GO

CREATE INDEX [IX_BusinessPartnerRegistrationStatusHistories_RegistrationId] ON [BusinessPartnerRegistrationStatusHistories] ([RegistrationId]);
GO

CREATE INDEX [IX_BusinessPartners_ApprovedById] ON [BusinessPartners] ([ApprovedById]);
GO

CREATE INDEX [IX_BusinessPartners_IsActive] ON [BusinessPartners] ([IsActive]);
GO

CREATE INDEX [IX_BusinessPartners_IsBlacklisted] ON [BusinessPartners] ([IsBlacklisted]);
GO

CREATE INDEX [IX_BusinessPartners_IsPreferred] ON [BusinessPartners] ([IsPreferred]);
GO

CREATE UNIQUE INDEX [IX_BusinessPartners_PartnerCode] ON [BusinessPartners] ([PartnerCode]);
GO

CREATE INDEX [IX_BusinessPartners_PartnerName] ON [BusinessPartners] ([PartnerName]);
GO

CREATE INDEX [IX_BusinessPartners_PartnerType] ON [BusinessPartners] ([PartnerType]);
GO

CREATE INDEX [IX_BusinessPartners_PrimaryEmail] ON [BusinessPartners] ([PrimaryEmail]);
GO

CREATE INDEX [IX_BusinessPartners_RegistrationStatus] ON [BusinessPartners] ([RegistrationStatus]);
GO

CREATE INDEX [IX_BusinessPartners_TenantId] ON [BusinessPartners] ([TenantId]);
GO

CREATE UNIQUE INDEX [IX_BusinessPartnerSpecializations_BusinessPartnerId_SpecializationId] ON [BusinessPartnerSpecializations] ([BusinessPartnerId], [SpecializationId]);
GO

CREATE INDEX [IX_BusinessPartnerSpecializations_SpecializationId] ON [BusinessPartnerSpecializations] ([SpecializationId]);
GO

CREATE INDEX [IX_ContractorInvoice_MaintenanceContractorId] ON [ContractorInvoice] ([MaintenanceContractorId]);
GO

CREATE INDEX [IX_ContractorPerformanceReview_ContractorId] ON [ContractorPerformanceReview] ([ContractorId]);
GO

CREATE INDEX [IX_ContractorSpecializations_IsActive] ON [ContractorSpecializations] ([IsActive]);
GO

CREATE UNIQUE INDEX [IX_ContractorSpecializations_SpecializationCode] ON [ContractorSpecializations] ([SpecializationCode]);
GO

CREATE INDEX [IX_ContractorSpecializations_SpecializationName] ON [ContractorSpecializations] ([SpecializationName]);
GO

CREATE INDEX [IX_ContractorSpecializations_TenantId] ON [ContractorSpecializations] ([TenantId]);
GO

CREATE INDEX [IX_ContractorWorkOrder_ContractorId] ON [ContractorWorkOrder] ([ContractorId]);
GO

CREATE UNIQUE INDEX [IX_ContractorWorkOrder_WorkOrderId] ON [ContractorWorkOrder] ([WorkOrderId]);
GO

CREATE INDEX [IX_Countries_TenantId] ON [Countries] ([TenantId]);
GO

CREATE UNIQUE INDEX [IX_Departments_Code] ON [Departments] ([Code]);
GO

CREATE INDEX [IX_Departments_DepartmentHeadId] ON [Departments] ([DepartmentHeadId]);
GO

CREATE INDEX [IX_Departments_DepartmentType] ON [Departments] ([DepartmentType]);
GO

CREATE INDEX [IX_Departments_IsActive] ON [Departments] ([IsActive]);
GO

CREATE INDEX [IX_Departments_Name] ON [Departments] ([Name]);
GO

CREATE INDEX [IX_Departments_ParentDepartmentId] ON [Departments] ([ParentDepartmentId]);
GO

CREATE INDEX [IX_Departments_TenantId] ON [Departments] ([TenantId]);
GO

CREATE INDEX [IX_EmailSettings_TenantId] ON [EmailSettings] ([TenantId]);
GO

CREATE INDEX [IX_EmailTemplates_Category] ON [EmailTemplates] ([Category]);
GO

CREATE INDEX [IX_EmailTemplates_Module] ON [EmailTemplates] ([Module]);
GO

CREATE UNIQUE INDEX [IX_EmailTemplates_TenantId_Name] ON [EmailTemplates] ([TenantId], [Name]);
GO

CREATE INDEX [IX_EmployeeBiometrics_EmployeeId] ON [EmployeeBiometrics] ([EmployeeId]);
GO

CREATE INDEX [IX_EmployeeBiometrics_TenantId] ON [EmployeeBiometrics] ([TenantId]);
GO

CREATE INDEX [IX_EmployeeContractDetails_EmployeeId] ON [EmployeeContractDetails] ([EmployeeId]);
GO

CREATE INDEX [IX_EmployeeContractDetails_TenantId] ON [EmployeeContractDetails] ([TenantId]);
GO

CREATE INDEX [IX_EmployeeDependents_EmployeeId] ON [EmployeeDependents] ([EmployeeId]);
GO

CREATE INDEX [IX_EmployeeDependents_TenantId] ON [EmployeeDependents] ([TenantId]);
GO

CREATE INDEX [IX_EmployeeEmergencyContacts_EmployeeId] ON [EmployeeEmergencyContacts] ([EmployeeId]);
GO

CREATE INDEX [IX_EmployeeEmergencyContacts_TenantId] ON [EmployeeEmergencyContacts] ([TenantId]);
GO

CREATE INDEX [IX_EmployeeIdentificationCards_EmployeeId] ON [EmployeeIdentificationCards] ([EmployeeId]);
GO

CREATE INDEX [IX_EmployeeIdentificationCards_TenantId] ON [EmployeeIdentificationCards] ([TenantId]);
GO

CREATE INDEX [IX_EmployeePositions_DepartmentId] ON [EmployeePositions] ([DepartmentId]);
GO

CREATE INDEX [IX_EmployeePositions_TenantId] ON [EmployeePositions] ([TenantId]);
GO

CREATE INDEX [IX_EmployeeQualifications_EmployeeId] ON [EmployeeQualifications] ([EmployeeId]);
GO

CREATE INDEX [IX_EmployeeQualifications_TenantId] ON [EmployeeQualifications] ([TenantId]);
GO

CREATE INDEX [IX_Employees_CorporateEmployeeID] ON [Employees] ([CorporateEmployeeID]);
GO

CREATE INDEX [IX_Employees_CountryId] ON [Employees] ([CountryId]);
GO

CREATE INDEX [IX_Employees_DateEmployed] ON [Employees] ([DateEmployed]);
GO

CREATE INDEX [IX_Employees_DepartmentId] ON [Employees] ([DepartmentId]);
GO

CREATE UNIQUE INDEX [IX_Employees_EmailAddress] ON [Employees] ([EmailAddress]);
GO

CREATE UNIQUE INDEX [IX_Employees_EmployeeNumber] ON [Employees] ([EmployeeNumber]);
GO

CREATE INDEX [IX_Employees_IsActive] ON [Employees] ([IsActive]);
GO

CREATE INDEX [IX_Employees_ManagerId] ON [Employees] ([ManagerId]);
GO

CREATE INDEX [IX_Employees_PositionId] ON [Employees] ([PositionId]);
GO

CREATE INDEX [IX_Employees_SectionId] ON [Employees] ([SectionId]);
GO

CREATE INDEX [IX_Employees_ShiftId] ON [Employees] ([ShiftId]);
GO

CREATE INDEX [IX_Employees_StaffStatus] ON [Employees] ([StaffStatus]);
GO

CREATE INDEX [IX_Employees_StationId] ON [Employees] ([StationId]);
GO

CREATE INDEX [IX_Employees_TenantId] ON [Employees] ([TenantId]);
GO

CREATE INDEX [IX_EmployeeShiftPreferences_EmployeeId] ON [EmployeeShiftPreferences] ([EmployeeId]);
GO

CREATE INDEX [IX_EmployeeShiftPreferences_ShiftId] ON [EmployeeShiftPreferences] ([ShiftId]);
GO

CREATE INDEX [IX_EmployeeShiftPreferences_TenantId] ON [EmployeeShiftPreferences] ([TenantId]);
GO

CREATE INDEX [IX_EmployeeSkills_EmployeeId] ON [EmployeeSkills] ([EmployeeId]);
GO

CREATE INDEX [IX_EmployeeSkills_SkillId] ON [EmployeeSkills] ([SkillId]);
GO

CREATE INDEX [IX_EmployeeSkills_TenantId] ON [EmployeeSkills] ([TenantId]);
GO

CREATE INDEX [IX_EmployeeWorkHistories_EmployeeId] ON [EmployeeWorkHistories] ([EmployeeId]);
GO

CREATE INDEX [IX_EmployeeWorkHistories_TenantId] ON [EmployeeWorkHistories] ([TenantId]);
GO

CREATE UNIQUE INDEX [IX_ExchangeRates_PreviousRateId] ON [ExchangeRates] ([PreviousRateId]) WHERE [PreviousRateId] IS NOT NULL;
GO

CREATE INDEX [IX_ExchangeRates_TenantId] ON [ExchangeRates] ([TenantId]);
GO

CREATE INDEX [IX_FinanceSettings_RealizedGainLossAccountId] ON [FinanceSettings] ([RealizedGainLossAccountId]);
GO

CREATE INDEX [IX_FinanceSettings_RetainedEarningsAccountId] ON [FinanceSettings] ([RetainedEarningsAccountId]);
GO

CREATE INDEX [IX_FinanceSettings_SuspenseAccountId] ON [FinanceSettings] ([SuspenseAccountId]);
GO

CREATE INDEX [IX_FinanceSettings_TenantId] ON [FinanceSettings] ([TenantId]);
GO

CREATE INDEX [IX_FinanceSettings_UnrealizedGainLossAccountId] ON [FinanceSettings] ([UnrealizedGainLossAccountId]);
GO

CREATE INDEX [IX_FiscalPeriods_FiscalYearId] ON [FiscalPeriods] ([FiscalYearId]);
GO

CREATE INDEX [IX_FiscalPeriods_TenantId] ON [FiscalPeriods] ([TenantId]);
GO

CREATE INDEX [IX_FiscalYears_ClosingJournalEntryId] ON [FiscalYears] ([ClosingJournalEntryId]);
GO

CREATE UNIQUE INDEX [IX_FiscalYears_NextFiscalYearId] ON [FiscalYears] ([NextFiscalYearId]) WHERE [NextFiscalYearId] IS NOT NULL;
GO

CREATE INDEX [IX_FiscalYears_OpeningBalanceJournalEntryId] ON [FiscalYears] ([OpeningBalanceJournalEntryId]);
GO

CREATE INDEX [IX_FiscalYears_TenantId] ON [FiscalYears] ([TenantId]);
GO

CREATE INDEX [IX_InspectionApprovals_ApprovalLevel] ON [InspectionApprovals] ([ApprovalLevel]);
GO

CREATE INDEX [IX_InspectionApprovals_ApprovedDate] ON [InspectionApprovals] ([ApprovedDate]);
GO

CREATE INDEX [IX_InspectionApprovals_ApproverId] ON [InspectionApprovals] ([ApproverId]);
GO

CREATE INDEX [IX_InspectionApprovals_DelegatedToId] ON [InspectionApprovals] ([DelegatedToId]);
GO

CREATE INDEX [IX_InspectionApprovals_DueDate] ON [InspectionApprovals] ([DueDate]);
GO

CREATE INDEX [IX_InspectionApprovals_InspectionId] ON [InspectionApprovals] ([InspectionId]);
GO

CREATE INDEX [IX_InspectionApprovals_Priority] ON [InspectionApprovals] ([Priority]);
GO

CREATE INDEX [IX_InspectionApprovals_RequestedDate] ON [InspectionApprovals] ([RequestedDate]);
GO

CREATE INDEX [IX_InspectionApprovals_Status] ON [InspectionApprovals] ([Status]);
GO

CREATE INDEX [IX_InspectionChecklistItems_Category] ON [InspectionChecklistItems] ([Category]);
GO

CREATE INDEX [IX_InspectionChecklistItems_IsCritical] ON [InspectionChecklistItems] ([IsCritical]);
GO

CREATE INDEX [IX_InspectionChecklistItems_IsRequired] ON [InspectionChecklistItems] ([IsRequired]);
GO

CREATE INDEX [IX_InspectionChecklistItems_ItemType] ON [InspectionChecklistItems] ([ItemType]);
GO

CREATE INDEX [IX_InspectionChecklistItems_RequiresPhoto] ON [InspectionChecklistItems] ([RequiresPhoto]);
GO

CREATE INDEX [IX_InspectionChecklistItems_SortOrder] ON [InspectionChecklistItems] ([SortOrder]);
GO

CREATE INDEX [IX_InspectionChecklistItems_TemplateId] ON [InspectionChecklistItems] ([TemplateId]);
GO

CREATE INDEX [IX_InspectionChecklistItems_TenantId] ON [InspectionChecklistItems] ([TenantId]);
GO

CREATE INDEX [IX_InspectionChecklistTemplates_Category] ON [InspectionChecklistTemplates] ([Category]);
GO

CREATE INDEX [IX_InspectionChecklistTemplates_IsActive] ON [InspectionChecklistTemplates] ([IsActive]);
GO

CREATE INDEX [IX_InspectionChecklistTemplates_IsDefault] ON [InspectionChecklistTemplates] ([IsDefault]);
GO

CREATE INDEX [IX_InspectionChecklistTemplates_Name] ON [InspectionChecklistTemplates] ([Name]);
GO

CREATE INDEX [IX_InspectionChecklistTemplates_SortOrder] ON [InspectionChecklistTemplates] ([SortOrder]);
GO

CREATE INDEX [IX_InspectionChecklistTemplates_TenantId] ON [InspectionChecklistTemplates] ([TenantId]);
GO

CREATE INDEX [IX_InspectionChecklistTemplates_Version] ON [InspectionChecklistTemplates] ([Version]);
GO

CREATE INDEX [IX_InspectionDocuments_DocumentType] ON [InspectionDocuments] ([DocumentType]);
GO

CREATE INDEX [IX_InspectionDocuments_InspectionId] ON [InspectionDocuments] ([InspectionId]);
GO

CREATE INDEX [IX_InspectionDocuments_TenantId] ON [InspectionDocuments] ([TenantId]);
GO

CREATE INDEX [IX_InspectionTemplates_Category] ON [InspectionTemplates] ([Category]);
GO

CREATE INDEX [IX_InspectionTemplates_InspectionType] ON [InspectionTemplates] ([InspectionType]);
GO

CREATE INDEX [IX_InspectionTemplates_IsActive] ON [InspectionTemplates] ([IsActive]);
GO

CREATE INDEX [IX_InspectionTemplates_TenantId] ON [InspectionTemplates] ([TenantId]);
GO

CREATE INDEX [IX_InventoryAllocations_AllocatedById] ON [InventoryAllocations] ([AllocatedById]);
GO

CREATE INDEX [IX_InventoryAllocations_InventoryItemId] ON [InventoryAllocations] ([InventoryItemId]);
GO

CREATE INDEX [IX_InventoryAllocations_LocationId] ON [InventoryAllocations] ([LocationId]);
GO

CREATE INDEX [IX_InventoryAllocations_TenantId] ON [InventoryAllocations] ([TenantId]);
GO

CREATE INDEX [IX_InventoryAllocations_WarehouseId] ON [InventoryAllocations] ([WarehouseId]);
GO

CREATE INDEX [IX_InventoryCategories_Code] ON [InventoryCategories] ([Code]);
GO

CREATE INDEX [IX_InventoryCategories_IsActive] ON [InventoryCategories] ([IsActive]);
GO

CREATE INDEX [IX_InventoryCategories_ParentCategoryId] ON [InventoryCategories] ([ParentCategoryId]);
GO

CREATE INDEX [IX_InventoryCategories_TenantId] ON [InventoryCategories] ([TenantId]);
GO

CREATE INDEX [IX_InventoryItems_ABCClass] ON [InventoryItems] ([ABCClass]);
GO

CREATE INDEX [IX_InventoryItems_CategoryId] ON [InventoryItems] ([CategoryId]);
GO

CREATE INDEX [IX_InventoryItems_IsLotTracked] ON [InventoryItems] ([IsLotTracked]);
GO

CREATE INDEX [IX_InventoryItems_IsSerialTracked] ON [InventoryItems] ([IsSerialTracked]);
GO

CREATE UNIQUE INDEX [IX_InventoryItems_ItemCode] ON [InventoryItems] ([ItemCode]);
GO

CREATE INDEX [IX_InventoryItems_Status] ON [InventoryItems] ([Status]);
GO

CREATE INDEX [IX_InventoryItems_TenantId] ON [InventoryItems] ([TenantId]);
GO

CREATE INDEX [IX_InventoryLocations_InventoryItemId] ON [InventoryLocations] ([InventoryItemId]);
GO

CREATE INDEX [IX_InventoryLocations_LocationId] ON [InventoryLocations] ([LocationId]);
GO

CREATE INDEX [IX_InventoryLocations_TenantId] ON [InventoryLocations] ([TenantId]);
GO

CREATE INDEX [IX_JobCard_AcceptedById] ON [JobCard] ([AcceptedById]);
GO

CREATE INDEX [IX_JobCard_ApprovalStatus] ON [JobCard] ([ApprovalStatus]);
GO

CREATE INDEX [IX_JobCard_AssetId] ON [JobCard] ([AssetId]);
GO

CREATE INDEX [IX_JobCard_AssignedTeamId] ON [JobCard] ([AssignedTeamId]);
GO

CREATE INDEX [IX_JobCard_AssignedTechnicianId] ON [JobCard] ([AssignedTechnicianId]);
GO

CREATE INDEX [IX_JobCard_ContractorId] ON [JobCard] ([ContractorId]);
GO

CREATE INDEX [IX_JobCard_GeneratedWorkOrderId] ON [JobCard] ([GeneratedWorkOrderId]);
GO

CREATE UNIQUE INDEX [IX_JobCard_JobCardNumber] ON [JobCard] ([JobCardNumber]);
GO

CREATE INDEX [IX_JobCard_JobCardStatus] ON [JobCard] ([JobCardStatus]);
GO

CREATE INDEX [IX_JobCard_MaintenanceTypeId] ON [JobCard] ([MaintenanceTypeId]);
GO

CREATE INDEX [IX_JobCard_PreferredTeamId] ON [JobCard] ([PreferredTeamId]);
GO

CREATE INDEX [IX_JobCard_PreferredTechnicianId] ON [JobCard] ([PreferredTechnicianId]);
GO

CREATE INDEX [IX_JobCard_PriorityLevelId] ON [JobCard] ([PriorityLevelId]);
GO

CREATE INDEX [IX_JobCard_QualityCheckedById] ON [JobCard] ([QualityCheckedById]);
GO

CREATE INDEX [IX_JobCard_RequestedById] ON [JobCard] ([RequestedById]);
GO

CREATE INDEX [IX_JobCard_RequestedDate] ON [JobCard] ([RequestedDate]);
GO

CREATE INDEX [IX_JobCard_RequiredCompletionDate] ON [JobCard] ([RequiredCompletionDate]);
GO

CREATE INDEX [IX_JobCard_TenantId] ON [JobCard] ([TenantId]);
GO

CREATE INDEX [IX_JobCardApprovalStep_ApproverId] ON [JobCardApprovalStep] ([ApproverId]);
GO

CREATE INDEX [IX_JobCardApprovalStep_JobCardId] ON [JobCardApprovalStep] ([JobCardId]);
GO

CREATE INDEX [IX_JobCardApprovalStep_Status] ON [JobCardApprovalStep] ([Status]);
GO

CREATE INDEX [IX_JobCardApprovalStep_StepOrder] ON [JobCardApprovalStep] ([StepOrder]);
GO

CREATE INDEX [IX_JobCardApprovalStep_TenantId] ON [JobCardApprovalStep] ([TenantId]);
GO

CREATE INDEX [IX_JobCardCertificates_AssetId] ON [JobCardCertificates] ([AssetId]);
GO

CREATE INDEX [IX_JobCardCertificates_IssuedById] ON [JobCardCertificates] ([IssuedById]);
GO

CREATE INDEX [IX_JobCardCertificates_JobCardId] ON [JobCardCertificates] ([JobCardId]);
GO

CREATE INDEX [IX_JobCardCertificates_TenantId] ON [JobCardCertificates] ([TenantId]);
GO

CREATE INDEX [IX_JobCardComment_CommentById] ON [JobCardComment] ([CommentById]);
GO

CREATE INDEX [IX_JobCardComment_CommentDate] ON [JobCardComment] ([CommentDate]);
GO

CREATE INDEX [IX_JobCardComment_CommentType] ON [JobCardComment] ([CommentType]);
GO

CREATE INDEX [IX_JobCardComment_JobCardId] ON [JobCardComment] ([JobCardId]);
GO

CREATE INDEX [IX_JobCardComment_TenantId] ON [JobCardComment] ([TenantId]);
GO

CREATE INDEX [IX_JobCardDocument_DocumentType] ON [JobCardDocument] ([DocumentType]);
GO

CREATE INDEX [IX_JobCardDocument_JobCardId] ON [JobCardDocument] ([JobCardId]);
GO

CREATE INDEX [IX_JobCardDocument_TenantId] ON [JobCardDocument] ([TenantId]);
GO

CREATE INDEX [IX_JobCardDocument_UploadedById] ON [JobCardDocument] ([UploadedById]);
GO

CREATE INDEX [IX_JobCardDocument_UploadedDate] ON [JobCardDocument] ([UploadedDate]);
GO

CREATE INDEX [IX_JournalEntries_FiscalPeriodId] ON [JournalEntries] ([FiscalPeriodId]);
GO

CREATE INDEX [IX_JournalEntries_OriginalJournalEntryId] ON [JournalEntries] ([OriginalJournalEntryId]);
GO

CREATE INDEX [IX_JournalEntries_ReversalJournalEntryId] ON [JournalEntries] ([ReversalJournalEntryId]);
GO

CREATE INDEX [IX_JournalEntries_TenantId] ON [JournalEntries] ([TenantId]);
GO

CREATE INDEX [IX_LicenseTypes_IsActive] ON [LicenseTypes] ([IsActive]);
GO

CREATE UNIQUE INDEX [IX_LicenseTypes_LicenseCode] ON [LicenseTypes] ([LicenseCode]);
GO

CREATE INDEX [IX_LicenseTypes_LicenseName] ON [LicenseTypes] ([LicenseName]);
GO

CREATE INDEX [IX_LicenseTypes_TenantId] ON [LicenseTypes] ([TenantId]);
GO

CREATE INDEX [IX_MaintenanceAssetCategories_Code] ON [MaintenanceAssetCategories] ([Code]);
GO

CREATE INDEX [IX_MaintenanceAssetCategories_IsActive] ON [MaintenanceAssetCategories] ([IsActive]);
GO

CREATE INDEX [IX_MaintenanceAssetCategories_ParentCategoryId] ON [MaintenanceAssetCategories] ([ParentCategoryId]);
GO

CREATE INDEX [IX_MaintenanceAssetCategories_TenantId] ON [MaintenanceAssetCategories] ([TenantId]);
GO

CREATE INDEX [IX_MaintenanceAssets_AssetCategoryId] ON [MaintenanceAssets] ([AssetCategoryId]);
GO

CREATE INDEX [IX_MaintenanceAssets_AssetNumber] ON [MaintenanceAssets] ([AssetNumber]);
GO

CREATE INDEX [IX_MaintenanceAssets_Criticality] ON [MaintenanceAssets] ([Criticality]);
GO

CREATE INDEX [IX_MaintenanceAssets_EmployeeId] ON [MaintenanceAssets] ([EmployeeId]);
GO

CREATE INDEX [IX_MaintenanceAssets_ParentAssetId] ON [MaintenanceAssets] ([ParentAssetId]);
GO

CREATE INDEX [IX_MaintenanceAssets_SerialNumber] ON [MaintenanceAssets] ([SerialNumber]);
GO

CREATE INDEX [IX_MaintenanceAssets_Status] ON [MaintenanceAssets] ([Status]);
GO

CREATE INDEX [IX_MaintenanceAssets_TenantId] ON [MaintenanceAssets] ([TenantId]);
GO

CREATE INDEX [IX_MaintenanceAttachments_UploadedByUserId] ON [MaintenanceAttachments] ([UploadedByUserId]);
GO

CREATE INDEX [IX_MaintenanceAttachmentTag_AttachmentId] ON [MaintenanceAttachmentTag] ([AttachmentId]);
GO

CREATE INDEX [IX_MaintenanceCertificate_AssetDischargeId] ON [MaintenanceCertificate] ([AssetDischargeId]);
GO

CREATE INDEX [IX_MaintenanceCertificate_AssetId] ON [MaintenanceCertificate] ([AssetId]);
GO

CREATE UNIQUE INDEX [IX_MaintenanceCertificate_CertificateNumber] ON [MaintenanceCertificate] ([CertificateNumber]);
GO

CREATE INDEX [IX_MaintenanceCertificate_DischargeId] ON [MaintenanceCertificate] ([DischargeId]);
GO

CREATE INDEX [IX_MaintenanceCertificate_IssuedById] ON [MaintenanceCertificate] ([IssuedById]);
GO

CREATE INDEX [IX_MaintenanceCertificate_IssuedDate] ON [MaintenanceCertificate] ([IssuedDate]);
GO

CREATE INDEX [IX_MaintenanceCertificate_TenantId] ON [MaintenanceCertificate] ([TenantId]);
GO

CREATE INDEX [IX_MaintenanceEscalationRules_NotificationTemplateId] ON [MaintenanceEscalationRules] ([NotificationTemplateId]);
GO

CREATE INDEX [IX_MaintenanceEscalationRules_TenantId] ON [MaintenanceEscalationRules] ([TenantId]);
GO

CREATE INDEX [IX_MaintenanceExpenses_ApprovedById] ON [MaintenanceExpenses] ([ApprovedById]);
GO

CREATE INDEX [IX_MaintenanceExpenses_ScheduleId] ON [MaintenanceExpenses] ([ScheduleId]);
GO

CREATE INDEX [IX_MaintenanceExpenses_TechnicianId] ON [MaintenanceExpenses] ([TechnicianId]);
GO

CREATE INDEX [IX_MaintenanceExpenses_TenantId] ON [MaintenanceExpenses] ([TenantId]);
GO

CREATE INDEX [IX_MaintenanceExpenses_VehicleId] ON [MaintenanceExpenses] ([VehicleId]);
GO

CREATE INDEX [IX_MaintenanceExpenses_WorkOrderId] ON [MaintenanceExpenses] ([WorkOrderId]);
GO

CREATE INDEX [IX_MaintenanceNotifications_TenantId] ON [MaintenanceNotifications] ([TenantId]);
GO

CREATE INDEX [IX_MaintenanceNotificationTemplates_TenantId] ON [MaintenanceNotificationTemplates] ([TenantId]);
GO

CREATE INDEX [IX_MaintenanceScheduleHistories_ScheduleId] ON [MaintenanceScheduleHistories] ([ScheduleId]);
GO

CREATE INDEX [IX_MaintenanceScheduleHistories_TenantId] ON [MaintenanceScheduleHistories] ([TenantId]);
GO

CREATE INDEX [IX_MaintenanceScheduleNotificationHistories_ScheduleId] ON [MaintenanceScheduleNotificationHistories] ([ScheduleId]);
GO

CREATE INDEX [IX_MaintenanceScheduleNotificationHistories_TenantId] ON [MaintenanceScheduleNotificationHistories] ([TenantId]);
GO

CREATE INDEX [IX_MaintenanceSchedules_AssetId] ON [MaintenanceSchedules] ([AssetId]);
GO

CREATE INDEX [IX_MaintenanceSchedules_DefaultTeamId] ON [MaintenanceSchedules] ([DefaultTeamId]);
GO

CREATE INDEX [IX_MaintenanceSchedules_DefaultTechnicianId] ON [MaintenanceSchedules] ([DefaultTechnicianId]);
GO

CREATE INDEX [IX_MaintenanceSchedules_IsActive] ON [MaintenanceSchedules] ([IsActive]);
GO

CREATE INDEX [IX_MaintenanceSchedules_LastGeneratedDate] ON [MaintenanceSchedules] ([LastGeneratedDate]);
GO

CREATE INDEX [IX_MaintenanceSchedules_MaintenanceTypeId] ON [MaintenanceSchedules] ([MaintenanceTypeId]);
GO

CREATE INDEX [IX_MaintenanceSchedules_MaintenanceTypeId1] ON [MaintenanceSchedules] ([MaintenanceTypeId1]);
GO

CREATE INDEX [IX_MaintenanceSchedules_NextDueDate] ON [MaintenanceSchedules] ([NextDueDate]);
GO

CREATE INDEX [IX_MaintenanceSchedules_PriorityLevelId] ON [MaintenanceSchedules] ([PriorityLevelId]);
GO

CREATE INDEX [IX_MaintenanceSchedules_ScheduleType] ON [MaintenanceSchedules] ([ScheduleType]);
GO

CREATE INDEX [IX_MaintenanceSchedules_TenantId] ON [MaintenanceSchedules] ([TenantId]);
GO

CREATE INDEX [IX_MaintenanceStaffSchedules_AssignedVehicleId] ON [MaintenanceStaffSchedules] ([AssignedVehicleId]);
GO

CREATE INDEX [IX_MaintenanceStaffSchedules_JobCardId] ON [MaintenanceStaffSchedules] ([JobCardId]);
GO

CREATE INDEX [IX_MaintenanceStaffSchedules_TeamId] ON [MaintenanceStaffSchedules] ([TeamId]);
GO

CREATE INDEX [IX_MaintenanceStaffSchedules_TenantId] ON [MaintenanceStaffSchedules] ([TenantId]);
GO

CREATE INDEX [IX_MaintenanceStaffSchedules_WorkOrderId] ON [MaintenanceStaffSchedules] ([WorkOrderId]);
GO

CREATE INDEX [IX_MaintenanceTaskTemplates_AssignedTechnicianId] ON [MaintenanceTaskTemplates] ([AssignedTechnicianId]);
GO

CREATE INDEX [IX_MaintenanceTaskTemplates_MaintenanceTypeId] ON [MaintenanceTaskTemplates] ([MaintenanceTypeId]);
GO

CREATE INDEX [IX_MaintenanceTaskTemplates_TenantId] ON [MaintenanceTaskTemplates] ([TenantId]);
GO

CREATE INDEX [IX_MaintenanceTools_TenantId] ON [MaintenanceTools] ([TenantId]);
GO

CREATE INDEX [IX_MaintenanceTypes_Category] ON [MaintenanceTypes] ([Category]);
GO

CREATE INDEX [IX_MaintenanceTypes_Code] ON [MaintenanceTypes] ([Code]);
GO

CREATE INDEX [IX_MaintenanceTypes_IsActive] ON [MaintenanceTypes] ([IsActive]);
GO

CREATE INDEX [IX_MaintenanceTypes_TenantId] ON [MaintenanceTypes] ([TenantId]);
GO

CREATE INDEX [IX_Notifications_TenantId] ON [Notifications] ([TenantId]);
GO

CREATE UNIQUE INDEX [IX_PartnerCategories_CategoryCode] ON [PartnerCategories] ([CategoryCode]);
GO

CREATE INDEX [IX_PartnerCategories_CategoryName] ON [PartnerCategories] ([CategoryName]);
GO

CREATE INDEX [IX_PartnerCategories_CategoryType] ON [PartnerCategories] ([CategoryType]);
GO

CREATE INDEX [IX_PartnerCategories_IsActive] ON [PartnerCategories] ([IsActive]);
GO

CREATE INDEX [IX_PartnerCategories_ParentCategoryId] ON [PartnerCategories] ([ParentCategoryId]);
GO

CREATE INDEX [IX_PartnerCategories_TenantId] ON [PartnerCategories] ([TenantId]);
GO

CREATE INDEX [IX_PasswordResetTokens_TenantId] ON [PasswordResetTokens] ([TenantId]);
GO

CREATE INDEX [IX_PasswordResetTokens_UserId] ON [PasswordResetTokens] ([UserId]);
GO

CREATE INDEX [IX_Permissions_Category] ON [Permissions] ([Category]);
GO

CREATE UNIQUE INDEX [IX_Permissions_Name] ON [Permissions] ([Name]);
GO

CREATE INDEX [IX_PositionSkillRequirement_PositionId] ON [PositionSkillRequirement] ([PositionId]);
GO

CREATE INDEX [IX_PositionSkillRequirement_SkillId] ON [PositionSkillRequirement] ([SkillId]);
GO

CREATE INDEX [IX_PositionSkillRequirement_TenantId] ON [PositionSkillRequirement] ([TenantId]);
GO

CREATE INDEX [IX_PriorityLevels_IsActive] ON [PriorityLevels] ([IsActive]);
GO

CREATE UNIQUE INDEX [IX_PriorityLevels_Level] ON [PriorityLevels] ([Level]);
GO

CREATE INDEX [IX_PriorityLevels_TenantId] ON [PriorityLevels] ([TenantId]);
GO

CREATE INDEX [IX_ProtocolAdherences_ProtocolId] ON [ProtocolAdherences] ([ProtocolId]);
GO

CREATE INDEX [IX_ProtocolAdherences_TenantId] ON [ProtocolAdherences] ([TenantId]);
GO

CREATE INDEX [IX_ProtocolAuditDetails_AuditId] ON [ProtocolAuditDetails] ([AuditId]);
GO

CREATE INDEX [IX_ProtocolAuditDetails_ProtocolId] ON [ProtocolAuditDetails] ([ProtocolId]);
GO

CREATE INDEX [IX_ProtocolTrainings_ProtocolId] ON [ProtocolTrainings] ([ProtocolId]);
GO

CREATE INDEX [IX_ProtocolTrainings_TenantId] ON [ProtocolTrainings] ([TenantId]);
GO

CREATE INDEX [IX_ProtocolViolations_ProtocolId] ON [ProtocolViolations] ([ProtocolId]);
GO

CREATE INDEX [IX_ProtocolViolations_TenantId] ON [ProtocolViolations] ([TenantId]);
GO

CREATE INDEX [IX_PurchaseOrderItems_InventoryItemId] ON [PurchaseOrderItems] ([InventoryItemId]);
GO

CREATE INDEX [IX_PurchaseOrderItems_PurchaseOrderId] ON [PurchaseOrderItems] ([PurchaseOrderId]);
GO

CREATE INDEX [IX_PurchaseOrderItems_TenantId] ON [PurchaseOrderItems] ([TenantId]);
GO

CREATE INDEX [IX_PurchaseOrderReceiptItems_LocationId] ON [PurchaseOrderReceiptItems] ([LocationId]);
GO

CREATE INDEX [IX_PurchaseOrderReceiptItems_PurchaseOrderItemId] ON [PurchaseOrderReceiptItems] ([PurchaseOrderItemId]);
GO

CREATE INDEX [IX_PurchaseOrderReceiptItems_ReceiptId] ON [PurchaseOrderReceiptItems] ([ReceiptId]);
GO

CREATE INDEX [IX_PurchaseOrderReceiptItems_TenantId] ON [PurchaseOrderReceiptItems] ([TenantId]);
GO

CREATE INDEX [IX_PurchaseOrderReceipts_InspectedById] ON [PurchaseOrderReceipts] ([InspectedById]);
GO

CREATE INDEX [IX_PurchaseOrderReceipts_PurchaseOrderId] ON [PurchaseOrderReceipts] ([PurchaseOrderId]);
GO

CREATE INDEX [IX_PurchaseOrderReceipts_ReceiptDate] ON [PurchaseOrderReceipts] ([ReceiptDate]);
GO

CREATE UNIQUE INDEX [IX_PurchaseOrderReceipts_ReceiptNumber] ON [PurchaseOrderReceipts] ([ReceiptNumber]);
GO

CREATE INDEX [IX_PurchaseOrderReceipts_ReceivedById] ON [PurchaseOrderReceipts] ([ReceivedById]);
GO

CREATE INDEX [IX_PurchaseOrderReceipts_Status] ON [PurchaseOrderReceipts] ([Status]);
GO

CREATE INDEX [IX_PurchaseOrderReceipts_TenantId] ON [PurchaseOrderReceipts] ([TenantId]);
GO

CREATE INDEX [IX_PurchaseOrders_ApprovedById] ON [PurchaseOrders] ([ApprovedById]);
GO

CREATE INDEX [IX_PurchaseOrders_OrderDate] ON [PurchaseOrders] ([OrderDate]);
GO

CREATE UNIQUE INDEX [IX_PurchaseOrders_OrderNumber] ON [PurchaseOrders] ([OrderNumber]);
GO

CREATE INDEX [IX_PurchaseOrders_RequestedById] ON [PurchaseOrders] ([RequestedById]);
GO

CREATE INDEX [IX_PurchaseOrders_Status] ON [PurchaseOrders] ([Status]);
GO

CREATE INDEX [IX_PurchaseOrders_SupplierId] ON [PurchaseOrders] ([SupplierId]);
GO

CREATE INDEX [IX_PurchaseOrders_TenantId] ON [PurchaseOrders] ([TenantId]);
GO

CREATE INDEX [IX_PurchaseRequisitionItems_InventoryItemId] ON [PurchaseRequisitionItems] ([InventoryItemId]);
GO

CREATE INDEX [IX_PurchaseRequisitionItems_PreferredSupplierId] ON [PurchaseRequisitionItems] ([PreferredSupplierId]);
GO

CREATE INDEX [IX_PurchaseRequisitionItems_PurchaseOrderId] ON [PurchaseRequisitionItems] ([PurchaseOrderId]);
GO

CREATE INDEX [IX_PurchaseRequisitionItems_RequisitionId] ON [PurchaseRequisitionItems] ([RequisitionId]);
GO

CREATE INDEX [IX_PurchaseRequisitionItems_TenantId] ON [PurchaseRequisitionItems] ([TenantId]);
GO

CREATE INDEX [IX_PurchaseRequisitions_ApprovedById] ON [PurchaseRequisitions] ([ApprovedById]);
GO

CREATE INDEX [IX_PurchaseRequisitions_RequestedById] ON [PurchaseRequisitions] ([RequestedById]);
GO

CREATE INDEX [IX_PurchaseRequisitions_TenantId] ON [PurchaseRequisitions] ([TenantId]);
GO

CREATE INDEX [IX_QualityControlChecklists_AssetCategory] ON [QualityControlChecklists] ([AssetCategory]);
GO

CREATE INDEX [IX_QualityControlChecklists_IsActive] ON [QualityControlChecklists] ([IsActive]);
GO

CREATE INDEX [IX_QualityControlChecklists_IsMandatory] ON [QualityControlChecklists] ([IsMandatory]);
GO

CREATE INDEX [IX_QualityControlChecklists_MaintenanceType] ON [QualityControlChecklists] ([MaintenanceType]);
GO

CREATE INDEX [IX_QualityControlChecklists_Name] ON [QualityControlChecklists] ([Name]);
GO

CREATE INDEX [IX_QualityControlChecklists_Version] ON [QualityControlChecklists] ([Version]);
GO

CREATE INDEX [IX_QualityControlChecklists_WorkOrderType] ON [QualityControlChecklists] ([WorkOrderType]);
GO

CREATE INDEX [IX_QualityMetrics_AssetCategory] ON [QualityMetrics] ([AssetCategory]);
GO

CREATE INDEX [IX_QualityMetrics_CalculatedDate] ON [QualityMetrics] ([CalculatedDate]);
GO

CREATE INDEX [IX_QualityMetrics_MetricsDate] ON [QualityMetrics] ([MetricsDate]);
GO

CREATE INDEX [IX_QualityMetrics_TeamId] ON [QualityMetrics] ([TeamId]);
GO

CREATE INDEX [IX_QualityMetrics_TechnicianId] ON [QualityMetrics] ([TechnicianId]);
GO

CREATE INDEX [IX_QualitySignOffChecklists_Category] ON [QualitySignOffChecklists] ([Category]);
GO

CREATE INDEX [IX_QualitySignOffChecklists_CheckedById] ON [QualitySignOffChecklists] ([CheckedById]);
GO

CREATE INDEX [IX_QualitySignOffChecklists_CheckedDate] ON [QualitySignOffChecklists] ([CheckedDate]);
GO

CREATE INDEX [IX_QualitySignOffChecklists_CheckType] ON [QualitySignOffChecklists] ([CheckType]);
GO

CREATE INDEX [IX_QualitySignOffChecklists_IsRequired] ON [QualitySignOffChecklists] ([IsRequired]);
GO

CREATE INDEX [IX_QualitySignOffChecklists_SignOffId] ON [QualitySignOffChecklists] ([SignOffId]);
GO

CREATE INDEX [IX_QualitySignOffChecklists_SortOrder] ON [QualitySignOffChecklists] ([SortOrder]);
GO

CREATE INDEX [IX_QualitySignOffChecklists_TenantId] ON [QualitySignOffChecklists] ([TenantId]);
GO

CREATE INDEX [IX_RatioDefinitions_IsActive] ON [RatioDefinitions] ([IsActive]);
GO

CREATE UNIQUE INDEX [IX_RatioDefinitions_TenantId_Code] ON [RatioDefinitions] ([TenantId], [Code]);
GO

CREATE INDEX [IX_RefreshTokens_ExpiresAt] ON [RefreshTokens] ([ExpiresAt]);
GO

CREATE INDEX [IX_RefreshTokens_TenantId] ON [RefreshTokens] ([TenantId]);
GO

CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
GO

CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
GO

CREATE INDEX [IX_RejectionFollowUps_AssignedToId] ON [RejectionFollowUps] ([AssignedToId]);
GO

CREATE INDEX [IX_RejectionFollowUps_CompletedById] ON [RejectionFollowUps] ([CompletedById]);
GO

CREATE INDEX [IX_RejectionFollowUps_CompletedDate] ON [RejectionFollowUps] ([CompletedDate]);
GO

CREATE INDEX [IX_RejectionFollowUps_DueDate] ON [RejectionFollowUps] ([DueDate]);
GO

CREATE INDEX [IX_RejectionFollowUps_Priority] ON [RejectionFollowUps] ([Priority]);
GO

CREATE INDEX [IX_RejectionFollowUps_RejectionId] ON [RejectionFollowUps] ([RejectionId]);
GO

CREATE INDEX [IX_RejectionFollowUps_Status] ON [RejectionFollowUps] ([Status]);
GO

CREATE INDEX [IX_RejectionFollowUps_TenantId] ON [RejectionFollowUps] ([TenantId]);
GO

CREATE INDEX [IX_ReportDataSources_CreatedAt] ON [ReportDataSources] ([CreatedAt]);
GO

CREATE INDEX [IX_ReportDataSources_CreatedByUserId] ON [ReportDataSources] ([CreatedByUserId]);
GO

CREATE INDEX [IX_ReportDataSources_IsActive] ON [ReportDataSources] ([IsActive]);
GO

CREATE INDEX [IX_ReportDataSources_LastUsed] ON [ReportDataSources] ([LastUsed]);
GO

CREATE INDEX [IX_ReportDataSources_Name] ON [ReportDataSources] ([Name]);
GO

CREATE INDEX [IX_ReportDataSources_TenantId] ON [ReportDataSources] ([TenantId]);
GO

CREATE INDEX [IX_ReportDataSources_Type] ON [ReportDataSources] ([Type]);
GO

CREATE INDEX [IX_ReportDataSourceUsageLogs_AccessedAt] ON [ReportDataSourceUsageLogs] ([AccessedAt]);
GO

CREATE INDEX [IX_ReportDataSourceUsageLogs_DataSourceId] ON [ReportDataSourceUsageLogs] ([DataSourceId]);
GO

CREATE INDEX [IX_ReportDataSourceUsageLogs_OperationType] ON [ReportDataSourceUsageLogs] ([OperationType]);
GO

CREATE INDEX [IX_ReportDataSourceUsageLogs_Success] ON [ReportDataSourceUsageLogs] ([Success]);
GO

CREATE INDEX [IX_ReportDataSourceUsageLogs_TenantId] ON [ReportDataSourceUsageLogs] ([TenantId]);
GO

CREATE INDEX [IX_ReportDataSourceUsageLogs_UserId] ON [ReportDataSourceUsageLogs] ([UserId]);
GO

CREATE INDEX [IX_ReportExecutions_ExecutedAt] ON [ReportExecutions] ([ExecutedAt]);
GO

CREATE INDEX [IX_ReportExecutions_ReportId] ON [ReportExecutions] ([ReportId]);
GO

CREATE INDEX [IX_ReportExecutions_Status] ON [ReportExecutions] ([Status]);
GO

CREATE INDEX [IX_ReportExecutions_TenantId] ON [ReportExecutions] ([TenantId]);
GO

CREATE INDEX [IX_ReportExecutions_UserId] ON [ReportExecutions] ([UserId]);
GO

CREATE INDEX [IX_ReportExports_ExportedAt] ON [ReportExports] ([ExportedAt]);
GO

CREATE INDEX [IX_ReportExports_Format] ON [ReportExports] ([Format]);
GO

CREATE INDEX [IX_ReportExports_ReportId] ON [ReportExports] ([ReportId]);
GO

CREATE INDEX [IX_ReportExports_TenantId] ON [ReportExports] ([TenantId]);
GO

CREATE INDEX [IX_ReportExports_UserId] ON [ReportExports] ([UserId]);
GO

CREATE INDEX [IX_ReportRoleAssignments_AssignedAt] ON [ReportRoleAssignments] ([AssignedAt]);
GO

CREATE INDEX [IX_ReportRoleAssignments_ReportId] ON [ReportRoleAssignments] ([ReportId]);
GO

CREATE UNIQUE INDEX [IX_ReportRoleAssignments_ReportId_RoleId] ON [ReportRoleAssignments] ([ReportId], [RoleId]);
GO

CREATE INDEX [IX_ReportRoleAssignments_RoleId] ON [ReportRoleAssignments] ([RoleId]);
GO

CREATE INDEX [IX_ReportRoleAssignments_TenantId] ON [ReportRoleAssignments] ([TenantId]);
GO

CREATE INDEX [IX_Reports_IsScheduled] ON [Reports] ([IsScheduled]);
GO

CREATE INDEX [IX_Reports_LastRun] ON [Reports] ([LastRun]);
GO

CREATE INDEX [IX_Reports_ModuleId] ON [Reports] ([ModuleId]);
GO

CREATE INDEX [IX_Reports_Status] ON [Reports] ([Status]);
GO

CREATE INDEX [IX_Reports_TenantId] ON [Reports] ([TenantId]);
GO

CREATE INDEX [IX_Reports_Type] ON [Reports] ([Type]);
GO

CREATE INDEX [IX_ReportSchedules_Frequency] ON [ReportSchedules] ([Frequency]);
GO

CREATE INDEX [IX_ReportSchedules_IsActive] ON [ReportSchedules] ([IsActive]);
GO

CREATE INDEX [IX_ReportSchedules_NextExecutionDate] ON [ReportSchedules] ([NextExecutionDate]);
GO

CREATE INDEX [IX_ReportSchedules_ReportId] ON [ReportSchedules] ([ReportId]);
GO

CREATE INDEX [IX_ReportSchedules_TenantId] ON [ReportSchedules] ([TenantId]);
GO

CREATE INDEX [IX_ReportTemplates_Category] ON [ReportTemplates] ([Category]);
GO

CREATE INDEX [IX_ReportTemplates_TenantId] ON [ReportTemplates] ([TenantId]);
GO

CREATE INDEX [IX_ReportTemplates_Type] ON [ReportTemplates] ([Type]);
GO

CREATE INDEX [IX_ReportTemplates_UsageCount] ON [ReportTemplates] ([UsageCount]);
GO

CREATE INDEX [IX_RolePermissions_PermissionId] ON [RolePermissions] ([PermissionId]);
GO

CREATE INDEX [IX_SafetyComplianceRecords_ComplianceDate] ON [SafetyComplianceRecords] ([ComplianceDate]);
GO

CREATE INDEX [IX_SafetyComplianceRecords_ComplianceStatus] ON [SafetyComplianceRecords] ([ComplianceStatus]);
GO

CREATE INDEX [IX_SafetyComplianceRecords_InspectorId] ON [SafetyComplianceRecords] ([InspectorId]);
GO

CREATE INDEX [IX_SafetyComplianceRecords_SafetyProtocolId] ON [SafetyComplianceRecords] ([SafetyProtocolId]);
GO

CREATE INDEX [IX_SafetyComplianceRecords_TechnicianId] ON [SafetyComplianceRecords] ([TechnicianId]);
GO

CREATE INDEX [IX_SafetyComplianceRecords_TenantId] ON [SafetyComplianceRecords] ([TenantId]);
GO

CREATE INDEX [IX_SafetyComplianceRecords_VerifiedById] ON [SafetyComplianceRecords] ([VerifiedById]);
GO

CREATE INDEX [IX_SafetyComplianceRecords_WorkOrderId] ON [SafetyComplianceRecords] ([WorkOrderId]);
GO

CREATE INDEX [IX_SafetyProtocols_Category] ON [SafetyProtocols] ([Category]);
GO

CREATE UNIQUE INDEX [IX_SafetyProtocols_Code] ON [SafetyProtocols] ([Code]);
GO

CREATE INDEX [IX_SafetyProtocols_EffectiveDate] ON [SafetyProtocols] ([EffectiveDate]);
GO

CREATE INDEX [IX_SafetyProtocols_IsActive] ON [SafetyProtocols] ([IsActive]);
GO

CREATE INDEX [IX_SafetyProtocols_IsMandatory] ON [SafetyProtocols] ([IsMandatory]);
GO

CREATE INDEX [IX_SafetyProtocols_Name] ON [SafetyProtocols] ([Name]);
GO

CREATE INDEX [IX_SafetyProtocols_NextReviewDate] ON [SafetyProtocols] ([NextReviewDate]);
GO

CREATE INDEX [IX_SafetyProtocols_Severity] ON [SafetyProtocols] ([Severity]);
GO

CREATE INDEX [IX_SafetyProtocols_TenantId] ON [SafetyProtocols] ([TenantId]);
GO

CREATE INDEX [IX_ScheduledWorkOrder_ScheduleId] ON [ScheduledWorkOrder] ([ScheduleId]);
GO

CREATE INDEX [IX_Sections_Code] ON [Sections] ([Code]);
GO

CREATE INDEX [IX_Sections_DepartmentId] ON [Sections] ([DepartmentId]);
GO

CREATE INDEX [IX_Sections_IsActive] ON [Sections] ([IsActive]);
GO

CREATE INDEX [IX_Sections_SectionHeadId] ON [Sections] ([SectionHeadId]);
GO

CREATE INDEX [IX_Sections_TenantId] ON [Sections] ([TenantId]);
GO

CREATE INDEX [IX_Securities_TenantId] ON [Securities] ([TenantId]);
GO

CREATE INDEX [IX_SecurityAlerts_AffectedUserId] ON [SecurityAlerts] ([AffectedUserId]);
GO

CREATE INDEX [IX_SecurityAlerts_Category] ON [SecurityAlerts] ([Category]);
GO

CREATE INDEX [IX_SecurityAlerts_Dismissed] ON [SecurityAlerts] ([Dismissed]);
GO

CREATE INDEX [IX_SecurityAlerts_TenantId] ON [SecurityAlerts] ([TenantId]);
GO

CREATE INDEX [IX_SecurityAlerts_Timestamp] ON [SecurityAlerts] ([Timestamp]);
GO

CREATE INDEX [IX_SecurityAlerts_Type] ON [SecurityAlerts] ([Type]);
GO

CREATE INDEX [IX_SecurityLogs_Action] ON [SecurityLogs] ([Action]);
GO

CREATE INDEX [IX_SecurityLogs_IpAddress] ON [SecurityLogs] ([IpAddress]);
GO

CREATE INDEX [IX_SecurityLogs_TenantId] ON [SecurityLogs] ([TenantId]);
GO

CREATE INDEX [IX_SecurityLogs_Timestamp] ON [SecurityLogs] ([Timestamp]);
GO

CREATE INDEX [IX_SecurityLogs_UserId] ON [SecurityLogs] ([UserId]);
GO

CREATE INDEX [IX_SecurityMetricsSet_MetricDate] ON [SecurityMetricsSet] ([MetricDate]);
GO

CREATE UNIQUE INDEX [IX_SecurityMetricsSet_TenantId_MetricDate] ON [SecurityMetricsSet] ([TenantId], [MetricDate]);
GO

CREATE INDEX [IX_SecurityPolicies_IsActive] ON [SecurityPolicies] ([IsActive]);
GO

CREATE INDEX [IX_SecurityPolicies_Name] ON [SecurityPolicies] ([Name]);
GO

CREATE INDEX [IX_SecurityPolicies_Priority] ON [SecurityPolicies] ([Priority]);
GO

CREATE INDEX [IX_SecurityPolicies_TenantId] ON [SecurityPolicies] ([TenantId]);
GO

CREATE INDEX [IX_SecurityPolicies_Type] ON [SecurityPolicies] ([Type]);
GO

CREATE INDEX [IX_SecurityPolicyViolations_DetectedAt] ON [SecurityPolicyViolations] ([DetectedAt]);
GO

CREATE INDEX [IX_SecurityPolicyViolations_IsResolved] ON [SecurityPolicyViolations] ([IsResolved]);
GO

CREATE INDEX [IX_SecurityPolicyViolations_SecurityPolicyId] ON [SecurityPolicyViolations] ([SecurityPolicyId]);
GO

CREATE INDEX [IX_SecurityPolicyViolations_Severity] ON [SecurityPolicyViolations] ([Severity]);
GO

CREATE INDEX [IX_SecurityPolicyViolations_TenantId] ON [SecurityPolicyViolations] ([TenantId]);
GO

CREATE INDEX [IX_SecurityPolicyViolations_UserId] ON [SecurityPolicyViolations] ([UserId]);
GO

CREATE INDEX [IX_SecurityPolicyViolations_ViolationType] ON [SecurityPolicyViolations] ([ViolationType]);
GO

CREATE INDEX [IX_SegmentLookupValues_ParentValueId] ON [SegmentLookupValues] ([ParentValueId]);
GO

CREATE INDEX [IX_SegmentLookupValues_SegmentStructureId] ON [SegmentLookupValues] ([SegmentStructureId]);
GO

CREATE INDEX [IX_SegmentLookupValues_TenantId] ON [SegmentLookupValues] ([TenantId]);
GO

CREATE INDEX [IX_ShiftAssignments_EmployeeId] ON [ShiftAssignments] ([EmployeeId]);
GO

CREATE INDEX [IX_ShiftAssignments_ShiftId] ON [ShiftAssignments] ([ShiftId]);
GO

CREATE INDEX [IX_ShiftAssignments_TenantId] ON [ShiftAssignments] ([TenantId]);
GO

CREATE INDEX [IX_Shifts_TenantId] ON [Shifts] ([TenantId]);
GO

CREATE INDEX [IX_Skills_TenantId] ON [Skills] ([TenantId]);
GO

CREATE INDEX [IX_StockAdjustmentItems_AdjustmentId] ON [StockAdjustmentItems] ([AdjustmentId]);
GO

CREATE INDEX [IX_StockAdjustmentItems_InventoryItemId] ON [StockAdjustmentItems] ([InventoryItemId]);
GO

CREATE INDEX [IX_StockAdjustmentItems_LocationId] ON [StockAdjustmentItems] ([LocationId]);
GO

CREATE INDEX [IX_StockAdjustmentItems_TenantId] ON [StockAdjustmentItems] ([TenantId]);
GO

CREATE INDEX [IX_StockAdjustments_AdjustmentDate] ON [StockAdjustments] ([AdjustmentDate]);
GO

CREATE UNIQUE INDEX [IX_StockAdjustments_AdjustmentNumber] ON [StockAdjustments] ([AdjustmentNumber]);
GO

CREATE INDEX [IX_StockAdjustments_ApprovedById] ON [StockAdjustments] ([ApprovedById]);
GO

CREATE INDEX [IX_StockAdjustments_InventoryItemId] ON [StockAdjustments] ([InventoryItemId]);
GO

CREATE INDEX [IX_StockAdjustments_ReasonCode] ON [StockAdjustments] ([ReasonCode]);
GO

CREATE INDEX [IX_StockAdjustments_Status] ON [StockAdjustments] ([Status]);
GO

CREATE INDEX [IX_StockAdjustments_TenantId] ON [StockAdjustments] ([TenantId]);
GO

CREATE INDEX [IX_StockMovements_ApprovedById] ON [StockMovements] ([ApprovedById]);
GO

CREATE INDEX [IX_StockMovements_InventoryItemId] ON [StockMovements] ([InventoryItemId]);
GO

CREATE INDEX [IX_StockMovements_LocationId] ON [StockMovements] ([LocationId]);
GO

CREATE INDEX [IX_StockMovements_MovementDate] ON [StockMovements] ([MovementDate]);
GO

CREATE INDEX [IX_StockMovements_MovementType] ON [StockMovements] ([MovementType]);
GO

CREATE INDEX [IX_StockMovements_ProcessedById] ON [StockMovements] ([ProcessedById]);
GO

CREATE INDEX [IX_StockMovements_ReferenceNumber] ON [StockMovements] ([ReferenceNumber]);
GO

CREATE INDEX [IX_StockMovements_ReferenceType] ON [StockMovements] ([ReferenceType]);
GO

CREATE INDEX [IX_StockMovements_TenantId] ON [StockMovements] ([TenantId]);
GO

CREATE INDEX [IX_SupplierContacts_SupplierId] ON [SupplierContacts] ([SupplierId]);
GO

CREATE INDEX [IX_SupplierContacts_TenantId] ON [SupplierContacts] ([TenantId]);
GO

CREATE INDEX [IX_SupplierItemCatalogs_InventoryItemId] ON [SupplierItemCatalogs] ([InventoryItemId]);
GO

CREATE INDEX [IX_SupplierItemCatalogs_SupplierId] ON [SupplierItemCatalogs] ([SupplierId]);
GO

CREATE INDEX [IX_SupplierItemCatalogs_TenantId] ON [SupplierItemCatalogs] ([TenantId]);
GO

CREATE INDEX [IX_Suppliers_TenantId] ON [Suppliers] ([TenantId]);
GO

CREATE UNIQUE INDEX [IX_SystemSettings_TenantId_Key] ON [SystemSettings] ([TenantId], [Key]);
GO

CREATE INDEX [IX_TaxCalculations_TaxTypeId] ON [TaxCalculations] ([TaxTypeId]);
GO

CREATE INDEX [IX_TaxCalculations_TenantId] ON [TaxCalculations] ([TenantId]);
GO

CREATE INDEX [IX_TaxRates_TaxTypeId] ON [TaxRates] ([TaxTypeId]);
GO

CREATE INDEX [IX_TaxRates_TenantId] ON [TaxRates] ([TenantId]);
GO

CREATE INDEX [IX_TaxRules_TaxTypeId] ON [TaxRules] ([TaxTypeId]);
GO

CREATE INDEX [IX_TaxRules_TenantId] ON [TaxRules] ([TenantId]);
GO

CREATE INDEX [IX_TaxThresholds_TaxTypeId] ON [TaxThresholds] ([TaxTypeId]);
GO

CREATE INDEX [IX_TaxThresholds_TenantId] ON [TaxThresholds] ([TenantId]);
GO

CREATE INDEX [IX_TaxTypes_TenantId] ON [TaxTypes] ([TenantId]);
GO

CREATE INDEX [IX_TechnicalSkills_Category] ON [TechnicalSkills] ([Category]);
GO

CREATE UNIQUE INDEX [IX_TechnicalSkills_Code] ON [TechnicalSkills] ([Code]);
GO

CREATE INDEX [IX_TechnicalSkills_Complexity] ON [TechnicalSkills] ([Complexity]);
GO

CREATE INDEX [IX_TechnicalSkills_IsActive] ON [TechnicalSkills] ([IsActive]);
GO

CREATE INDEX [IX_TechnicalSkills_IsFromHRModule] ON [TechnicalSkills] ([IsFromHRModule]);
GO

CREATE INDEX [IX_TechnicalSkills_LastSyncDate] ON [TechnicalSkills] ([LastSyncDate]);
GO

CREATE INDEX [IX_TechnicalSkills_Name] ON [TechnicalSkills] ([Name]);
GO

CREATE INDEX [IX_TechnicalSkills_RiskLevel] ON [TechnicalSkills] ([RiskLevel]);
GO

CREATE INDEX [IX_TechnicalSkills_SkillLevel] ON [TechnicalSkills] ([SkillLevel]);
GO

CREATE INDEX [IX_TechnicalSkills_TenantId] ON [TechnicalSkills] ([TenantId]);
GO

CREATE INDEX [IX_TechnicianAvailabilities_AvailabilityType] ON [TechnicianAvailabilities] ([AvailabilityType]);
GO

CREATE INDEX [IX_TechnicianAvailabilities_EmployeeId] ON [TechnicianAvailabilities] ([EmployeeId]);
GO

CREATE INDEX [IX_TechnicianAvailabilities_Reason] ON [TechnicianAvailabilities] ([Reason]);
GO

CREATE INDEX [IX_TechnicianAvailabilities_StartDate] ON [TechnicianAvailabilities] ([StartDate]);
GO

CREATE INDEX [IX_TechnicianAvailabilities_TechnicianId] ON [TechnicianAvailabilities] ([TechnicianId]);
GO

CREATE INDEX [IX_TechnicianAvailabilities_TenantId] ON [TechnicianAvailabilities] ([TenantId]);
GO

CREATE INDEX [IX_TechnicianCertifications_TechnicianId] ON [TechnicianCertifications] ([TechnicianId]);
GO

CREATE INDEX [IX_TechnicianCertifications_TenantId] ON [TechnicianCertifications] ([TenantId]);
GO

CREATE INDEX [IX_Technicians_EmployeeId] ON [Technicians] ([EmployeeId]);
GO

CREATE INDEX [IX_Technicians_TenantId] ON [Technicians] ([TenantId]);
GO

CREATE INDEX [IX_TechnicianSchedules_EmployeeId] ON [TechnicianSchedules] ([EmployeeId]);
GO

CREATE INDEX [IX_TechnicianSchedules_ScheduleType] ON [TechnicianSchedules] ([ScheduleType]);
GO

CREATE INDEX [IX_TechnicianSchedules_ShiftId] ON [TechnicianSchedules] ([ShiftId]);
GO

CREATE INDEX [IX_TechnicianSchedules_StartDate] ON [TechnicianSchedules] ([StartDate]);
GO

CREATE INDEX [IX_TechnicianSchedules_Status] ON [TechnicianSchedules] ([Status]);
GO

CREATE INDEX [IX_TechnicianSchedules_TechnicianId] ON [TechnicianSchedules] ([TechnicianId]);
GO

CREATE INDEX [IX_TechnicianSchedules_TenantId] ON [TechnicianSchedules] ([TenantId]);
GO

CREATE INDEX [IX_TechnicianSchedules_WorkOrderId] ON [TechnicianSchedules] ([WorkOrderId]);
GO

CREATE INDEX [IX_TechnicianShifts_IsActive] ON [TechnicianShifts] ([IsActive]);
GO

CREATE INDEX [IX_TechnicianShifts_StartTime] ON [TechnicianShifts] ([StartTime]);
GO

CREATE INDEX [IX_TechnicianShifts_TenantId] ON [TechnicianShifts] ([TenantId]);
GO

CREATE INDEX [IX_TechnicianSkillAssignments_ExpirationDate] ON [TechnicianSkillAssignments] ([ExpirationDate]);
GO

CREATE INDEX [IX_TechnicianSkillAssignments_IsVerified] ON [TechnicianSkillAssignments] ([IsVerified]);
GO

CREATE INDEX [IX_TechnicianSkillAssignments_LastAssessmentDate] ON [TechnicianSkillAssignments] ([LastAssessmentDate]);
GO

CREATE INDEX [IX_TechnicianSkillAssignments_ProficiencyLevel] ON [TechnicianSkillAssignments] ([ProficiencyLevel]);
GO

CREATE INDEX [IX_TechnicianSkillAssignments_SkillId] ON [TechnicianSkillAssignments] ([SkillId]);
GO

CREATE INDEX [IX_TechnicianSkillAssignments_TechnicianId] ON [TechnicianSkillAssignments] ([TechnicianId]);
GO

CREATE UNIQUE INDEX [IX_TechnicianSkillAssignments_TechnicianId_SkillId] ON [TechnicianSkillAssignments] ([TechnicianId], [SkillId]);
GO

CREATE INDEX [IX_TechnicianSkillAssignments_TechnicianId1] ON [TechnicianSkillAssignments] ([TechnicianId1]);
GO

CREATE INDEX [IX_TechnicianSkillAssignments_TenantId] ON [TechnicianSkillAssignments] ([TenantId]);
GO

CREATE INDEX [IX_TechnicianSkills_Category] ON [TechnicianSkills] ([Category]);
GO

CREATE INDEX [IX_TechnicianSkills_IsActive] ON [TechnicianSkills] ([IsActive]);
GO

CREATE INDEX [IX_TechnicianSkills_TenantId] ON [TechnicianSkills] ([TenantId]);
GO

CREATE INDEX [IX_TechnicianTeamMembers_IsActive] ON [TechnicianTeamMembers] ([IsActive]);
GO

CREATE INDEX [IX_TechnicianTeamMembers_TeamId] ON [TechnicianTeamMembers] ([TeamId]);
GO

CREATE INDEX [IX_TechnicianTeamMembers_TeamId_TechnicianId] ON [TechnicianTeamMembers] ([TeamId], [TechnicianId]);
GO

CREATE INDEX [IX_TechnicianTeamMembers_TechnicianId] ON [TechnicianTeamMembers] ([TechnicianId]);
GO

CREATE INDEX [IX_TechnicianTeamMembers_TenantId] ON [TechnicianTeamMembers] ([TenantId]);
GO

CREATE INDEX [IX_TechnicianTeams_Status] ON [TechnicianTeams] ([Status]);
GO

CREATE INDEX [IX_TechnicianTeams_TeamLeaderId] ON [TechnicianTeams] ([TeamLeaderId]);
GO

CREATE INDEX [IX_TechnicianTeams_TechnicianId] ON [TechnicianTeams] ([TechnicianId]);
GO

CREATE INDEX [IX_TechnicianTeams_TenantId] ON [TechnicianTeams] ([TenantId]);
GO

CREATE UNIQUE INDEX [IX_TenantModules_TenantId_ModuleName] ON [TenantModules] ([TenantId], [ModuleName]);
GO

CREATE UNIQUE INDEX [IX_Tenants_Code] ON [Tenants] ([Code]);
GO

CREATE UNIQUE INDEX [IX_Tenants_Domain] ON [Tenants] ([Domain]) WHERE [Domain] IS NOT NULL;
GO

CREATE INDEX [IX_ThreatDetections_AffectedUserId] ON [ThreatDetections] ([AffectedUserId]);
GO

CREATE INDEX [IX_ThreatDetections_DetectedAt] ON [ThreatDetections] ([DetectedAt]);
GO

CREATE INDEX [IX_ThreatDetections_IpAddress] ON [ThreatDetections] ([IpAddress]);
GO

CREATE INDEX [IX_ThreatDetections_Severity] ON [ThreatDetections] ([Severity]);
GO

CREATE INDEX [IX_ThreatDetections_Status] ON [ThreatDetections] ([Status]);
GO

CREATE INDEX [IX_ThreatDetections_TenantId] ON [ThreatDetections] ([TenantId]);
GO

CREATE INDEX [IX_ThreatDetections_ThreatType] ON [ThreatDetections] ([ThreatType]);
GO

CREATE INDEX [IX_ThreatIndicators_IndicatorType] ON [ThreatIndicators] ([IndicatorType]);
GO

CREATE INDEX [IX_ThreatIndicators_IsActive] ON [ThreatIndicators] ([IsActive]);
GO

CREATE INDEX [IX_ThreatIndicators_TenantId] ON [ThreatIndicators] ([TenantId]);
GO

CREATE INDEX [IX_ThreatIndicators_ThreatDetectionId] ON [ThreatIndicators] ([ThreatDetectionId]);
GO

CREATE INDEX [IX_ThreatIndicators_Value] ON [ThreatIndicators] ([Value]);
GO

CREATE INDEX [IX_ToolCheckouts_CheckedInById] ON [ToolCheckouts] ([CheckedInById]);
GO

CREATE INDEX [IX_ToolCheckouts_CheckedOutById] ON [ToolCheckouts] ([CheckedOutById]);
GO

CREATE INDEX [IX_ToolCheckouts_JobCardId] ON [ToolCheckouts] ([JobCardId]);
GO

CREATE INDEX [IX_ToolCheckouts_TenantId] ON [ToolCheckouts] ([TenantId]);
GO

CREATE INDEX [IX_ToolCheckouts_ToolId] ON [ToolCheckouts] ([ToolId]);
GO

CREATE INDEX [IX_ToolCheckouts_WorkOrderId] ON [ToolCheckouts] ([WorkOrderId]);
GO

CREATE INDEX [IX_UnitAccountBalances_FiscalPeriodId] ON [UnitAccountBalances] ([FiscalPeriodId]);
GO

CREATE INDEX [IX_UnitAccountBalances_FiscalYearId] ON [UnitAccountBalances] ([FiscalYearId]);
GO

CREATE INDEX [IX_UnitAccountBalances_TenantId] ON [UnitAccountBalances] ([TenantId]);
GO

CREATE UNIQUE INDEX [IX_UnitAccountBalances_UnitAccountId_FiscalPeriodId] ON [UnitAccountBalances] ([UnitAccountId], [FiscalPeriodId]);
GO

CREATE INDEX [IX_UnitAccountBudgets_FiscalPeriodId] ON [UnitAccountBudgets] ([FiscalPeriodId]);
GO

CREATE INDEX [IX_UnitAccountBudgets_FiscalYearId] ON [UnitAccountBudgets] ([FiscalYearId]);
GO

CREATE INDEX [IX_UnitAccountBudgets_IsActive] ON [UnitAccountBudgets] ([IsActive]);
GO

CREATE INDEX [IX_UnitAccountBudgets_TenantId] ON [UnitAccountBudgets] ([TenantId]);
GO

CREATE UNIQUE INDEX [IX_UnitAccountBudgets_UnitAccountId_FiscalPeriodId_BudgetVersion] ON [UnitAccountBudgets] ([UnitAccountId], [FiscalPeriodId], [BudgetVersion]);
GO

CREATE INDEX [IX_UnitAccounts_IsActive] ON [UnitAccounts] ([IsActive]);
GO

CREATE INDEX [IX_UnitAccounts_IsPostingAccount] ON [UnitAccounts] ([IsPostingAccount]);
GO

CREATE INDEX [IX_UnitAccounts_ParentAccountId] ON [UnitAccounts] ([ParentAccountId]);
GO

CREATE UNIQUE INDEX [IX_UnitAccounts_TenantId_AccountNumber] ON [UnitAccounts] ([TenantId], [AccountNumber]);
GO

CREATE INDEX [IX_UnitAccounts_UnitTypeId] ON [UnitAccounts] ([UnitTypeId]);
GO

CREATE INDEX [IX_UnitJournalEntries_EntryDate] ON [UnitJournalEntries] ([EntryDate]);
GO

CREATE INDEX [IX_UnitJournalEntries_FiscalPeriodId] ON [UnitJournalEntries] ([FiscalPeriodId]);
GO

CREATE INDEX [IX_UnitJournalEntries_FiscalYearId] ON [UnitJournalEntries] ([FiscalYearId]);
GO

CREATE INDEX [IX_UnitJournalEntries_Status] ON [UnitJournalEntries] ([Status]);
GO

CREATE UNIQUE INDEX [IX_UnitJournalEntries_TenantId_EntryNumber] ON [UnitJournalEntries] ([TenantId], [EntryNumber]);
GO

CREATE INDEX [IX_UnitJournalEntryLines_TenantId] ON [UnitJournalEntryLines] ([TenantId]);
GO

CREATE INDEX [IX_UnitJournalEntryLines_UnitAccountId] ON [UnitJournalEntryLines] ([UnitAccountId]);
GO

CREATE INDEX [IX_UnitJournalEntryLines_UnitJournalEntryId] ON [UnitJournalEntryLines] ([UnitJournalEntryId]);
GO

CREATE INDEX [IX_UnitTypes_IsActive] ON [UnitTypes] ([IsActive]);
GO

CREATE UNIQUE INDEX [IX_UnitTypes_TenantId_Code] ON [UnitTypes] ([TenantId], [Code]);
GO

CREATE INDEX [IX_UserReportFavorites_FavoritedAt] ON [UserReportFavorites] ([FavoritedAt]);
GO

CREATE INDEX [IX_UserReportFavorites_TenantId] ON [UserReportFavorites] ([TenantId]);
GO

CREATE INDEX [IX_UserReportFavorites_UserId] ON [UserReportFavorites] ([UserId]);
GO

CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
GO

CREATE INDEX [EmailIndex] ON [Users] ([NormalizedEmail]);
GO

CREATE UNIQUE INDEX [IX_ApplicationUser_Email_Unique] ON [Users] ([Email]) WHERE [Email] IS NOT NULL;
GO

CREATE UNIQUE INDEX [IX_ApplicationUser_UserName_Unique] ON [Users] ([UserName]) WHERE [UserName] IS NOT NULL;
GO

CREATE INDEX [IX_Users_TenantId] ON [Users] ([TenantId]);
GO

CREATE UNIQUE INDEX [UserNameIndex] ON [Users] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO

CREATE INDEX [IX_UserSessions_TenantId] ON [UserSessions] ([TenantId]);
GO

CREATE INDEX [IX_UserSessions_UserId] ON [UserSessions] ([UserId]);
GO

CREATE INDEX [IX_UserTechnicianSkills_CertificationExpiry] ON [UserTechnicianSkills] ([CertificationExpiry]);
GO

CREATE INDEX [IX_UserTechnicianSkills_EmployeeId] ON [UserTechnicianSkills] ([EmployeeId]);
GO

CREATE UNIQUE INDEX [IX_UserTechnicianSkills_EmployeeId_SkillId] ON [UserTechnicianSkills] ([EmployeeId], [SkillId]);
GO

CREATE INDEX [IX_UserTechnicianSkills_ProficiencyLevel] ON [UserTechnicianSkills] ([ProficiencyLevel]);
GO

CREATE INDEX [IX_UserTechnicianSkills_SkillId] ON [UserTechnicianSkills] ([SkillId]);
GO

CREATE INDEX [IX_UserTechnicianSkills_TenantId] ON [UserTechnicianSkills] ([TenantId]);
GO

CREATE INDEX [IX_UserTenants_IsDefault] ON [UserTenants] ([IsDefault]);
GO

CREATE INDEX [IX_UserTenants_TenantId_Status] ON [UserTenants] ([TenantId], [Status]);
GO

CREATE UNIQUE INDEX [IX_UserTenants_UserId_TenantId] ON [UserTenants] ([UserId], [TenantId]);
GO

CREATE INDEX [IX_WarehouseLocations_IsActive] ON [WarehouseLocations] ([IsActive]);
GO

CREATE INDEX [IX_WarehouseLocations_LocationCode] ON [WarehouseLocations] ([LocationCode]);
GO

CREATE INDEX [IX_WarehouseLocations_ParentLocationId] ON [WarehouseLocations] ([ParentLocationId]);
GO

CREATE INDEX [IX_WarehouseLocations_TenantId] ON [WarehouseLocations] ([TenantId]);
GO

CREATE INDEX [IX_WarehouseLocations_WarehouseId] ON [WarehouseLocations] ([WarehouseId]);
GO

CREATE INDEX [IX_WarehouseQuantities_InventoryItemId] ON [WarehouseQuantities] ([InventoryItemId]);
GO

CREATE INDEX [IX_WarehouseQuantities_TenantId] ON [WarehouseQuantities] ([TenantId]);
GO

CREATE INDEX [IX_WarehouseQuantities_WarehouseId] ON [WarehouseQuantities] ([WarehouseId]);
GO

CREATE UNIQUE INDEX [IX_Warehouses_Code] ON [Warehouses] ([Code]);
GO

CREATE INDEX [IX_Warehouses_IsActive] ON [Warehouses] ([IsActive]);
GO

CREATE INDEX [IX_Warehouses_TenantId] ON [Warehouses] ([TenantId]);
GO

CREATE INDEX [IX_Warehouses_WarehouseType] ON [Warehouses] ([WarehouseType]);
GO

CREATE INDEX [IX_WorkflowActivityLogs_ActivityDate] ON [WorkflowActivityLogs] ([ActivityDate]);
GO

CREATE INDEX [IX_WorkflowActivityLogs_ActivityType] ON [WorkflowActivityLogs] ([ActivityType]);
GO

CREATE INDEX [IX_WorkflowActivityLogs_PerformedById] ON [WorkflowActivityLogs] ([PerformedById]);
GO

CREATE INDEX [IX_WorkflowActivityLogs_StepInstanceId] ON [WorkflowActivityLogs] ([StepInstanceId]);
GO

CREATE INDEX [IX_WorkflowActivityLogs_TenantId] ON [WorkflowActivityLogs] ([TenantId]);
GO

CREATE INDEX [IX_WorkflowActivityLogs_WorkflowInstanceId] ON [WorkflowActivityLogs] ([WorkflowInstanceId]);
GO

CREATE INDEX [IX_WorkflowApprovals_ApproverId] ON [WorkflowApprovals] ([ApproverId]);
GO

CREATE INDEX [IX_WorkflowApprovals_DueDate] ON [WorkflowApprovals] ([DueDate]);
GO

CREATE INDEX [IX_WorkflowApprovals_ProcessedById] ON [WorkflowApprovals] ([ProcessedById]);
GO

CREATE INDEX [IX_WorkflowApprovals_RequestedDate] ON [WorkflowApprovals] ([RequestedDate]);
GO

CREATE INDEX [IX_WorkflowApprovals_Status] ON [WorkflowApprovals] ([Status]);
GO

CREATE INDEX [IX_WorkflowApprovals_StepInstanceId] ON [WorkflowApprovals] ([StepInstanceId]);
GO

CREATE INDEX [IX_WorkflowApprovals_TenantId] ON [WorkflowApprovals] ([TenantId]);
GO

CREATE INDEX [IX_WorkflowDefinitions_EntityTypeId] ON [WorkflowDefinitions] ([EntityTypeId]);
GO

CREATE INDEX [IX_WorkflowDefinitions_IsActive] ON [WorkflowDefinitions] ([IsActive]);
GO

CREATE INDEX [IX_WorkflowDefinitions_Name] ON [WorkflowDefinitions] ([Name]);
GO

CREATE UNIQUE INDEX [IX_WorkflowDefinitions_TenantId_Name] ON [WorkflowDefinitions] ([TenantId], [Name]);
GO

CREATE INDEX [IX_WorkflowEntityTypes_IsActive] ON [WorkflowEntityTypes] ([IsActive]);
GO

CREATE INDEX [IX_WorkflowEntityTypes_Name] ON [WorkflowEntityTypes] ([Name]);
GO

CREATE UNIQUE INDEX [IX_WorkflowEntityTypes_TenantId_Name] ON [WorkflowEntityTypes] ([TenantId], [Name]);
GO

CREATE INDEX [IX_WorkflowInstances_CurrentStepId] ON [WorkflowInstances] ([CurrentStepId]);
GO

CREATE INDEX [IX_WorkflowInstances_EntityId] ON [WorkflowInstances] ([EntityId]);
GO

CREATE INDEX [IX_WorkflowInstances_EntityTypeId] ON [WorkflowInstances] ([EntityTypeId]);
GO

CREATE INDEX [IX_WorkflowInstances_EntityTypeId_EntityId] ON [WorkflowInstances] ([EntityTypeId], [EntityId]);
GO

CREATE INDEX [IX_WorkflowInstances_InitiatedById] ON [WorkflowInstances] ([InitiatedById]);
GO

CREATE INDEX [IX_WorkflowInstances_Priority] ON [WorkflowInstances] ([Priority]);
GO

CREATE INDEX [IX_WorkflowInstances_StartedById] ON [WorkflowInstances] ([StartedById]);
GO

CREATE INDEX [IX_WorkflowInstances_StartedDate] ON [WorkflowInstances] ([StartedDate]);
GO

CREATE INDEX [IX_WorkflowInstances_Status] ON [WorkflowInstances] ([Status]);
GO

CREATE INDEX [IX_WorkflowInstances_TenantId] ON [WorkflowInstances] ([TenantId]);
GO

CREATE INDEX [IX_WorkflowInstances_WorkflowDefinitionId] ON [WorkflowInstances] ([WorkflowDefinitionId]);
GO

CREATE INDEX [IX_WorkflowStepInstances_AssignedToId] ON [WorkflowStepInstances] ([AssignedToId]);
GO

CREATE INDEX [IX_WorkflowStepInstances_DueDate] ON [WorkflowStepInstances] ([DueDate]);
GO

CREATE INDEX [IX_WorkflowStepInstances_StartedDate] ON [WorkflowStepInstances] ([StartedDate]);
GO

CREATE INDEX [IX_WorkflowStepInstances_Status] ON [WorkflowStepInstances] ([Status]);
GO

CREATE INDEX [IX_WorkflowStepInstances_TenantId] ON [WorkflowStepInstances] ([TenantId]);
GO

CREATE INDEX [IX_WorkflowStepInstances_WorkflowInstanceId] ON [WorkflowStepInstances] ([WorkflowInstanceId]);
GO

CREATE INDEX [IX_WorkflowStepInstances_WorkflowStepId] ON [WorkflowStepInstances] ([WorkflowStepId]);
GO

CREATE INDEX [IX_WorkflowSteps_IsEndStep] ON [WorkflowSteps] ([IsEndStep]);
GO

CREATE INDEX [IX_WorkflowSteps_IsStartStep] ON [WorkflowSteps] ([IsStartStep]);
GO

CREATE INDEX [IX_WorkflowSteps_Name] ON [WorkflowSteps] ([Name]);
GO

CREATE INDEX [IX_WorkflowSteps_Order] ON [WorkflowSteps] ([Order]);
GO

CREATE INDEX [IX_WorkflowSteps_StepType] ON [WorkflowSteps] ([StepType]);
GO

CREATE INDEX [IX_WorkflowSteps_TenantId] ON [WorkflowSteps] ([TenantId]);
GO

CREATE INDEX [IX_WorkflowSteps_WorkflowDefinitionId] ON [WorkflowSteps] ([WorkflowDefinitionId]);
GO

CREATE INDEX [IX_WorkflowTransitions_FromStepId] ON [WorkflowTransitions] ([FromStepId]);
GO

CREATE INDEX [IX_WorkflowTransitions_FromStepId_ToStepId] ON [WorkflowTransitions] ([FromStepId], [ToStepId]);
GO

CREATE INDEX [IX_WorkflowTransitions_IsDefault] ON [WorkflowTransitions] ([IsDefault]);
GO

CREATE INDEX [IX_WorkflowTransitions_Priority] ON [WorkflowTransitions] ([Priority]);
GO

CREATE INDEX [IX_WorkflowTransitions_TenantId] ON [WorkflowTransitions] ([TenantId]);
GO

CREATE INDEX [IX_WorkflowTransitions_ToStepId] ON [WorkflowTransitions] ([ToStepId]);
GO

CREATE INDEX [IX_WorkflowTransitions_WorkflowDefinitionId] ON [WorkflowTransitions] ([WorkflowDefinitionId]);
GO

CREATE INDEX [IX_WorkOrderComments_CommentType] ON [WorkOrderComments] ([CommentType]);
GO

CREATE INDEX [IX_WorkOrderComments_CreatedAt] ON [WorkOrderComments] ([CreatedAt]);
GO

CREATE INDEX [IX_WorkOrderComments_EmployeeId] ON [WorkOrderComments] ([EmployeeId]);
GO

CREATE INDEX [IX_WorkOrderComments_TenantId] ON [WorkOrderComments] ([TenantId]);
GO

CREATE INDEX [IX_WorkOrderComments_WorkOrderId] ON [WorkOrderComments] ([WorkOrderId]);
GO

CREATE INDEX [IX_WorkOrderDocuments_DocumentType] ON [WorkOrderDocuments] ([DocumentType]);
GO

CREATE INDEX [IX_WorkOrderDocuments_TenantId] ON [WorkOrderDocuments] ([TenantId]);
GO

CREATE INDEX [IX_WorkOrderDocuments_UploadedAt] ON [WorkOrderDocuments] ([UploadedAt]);
GO

CREATE INDEX [IX_WorkOrderDocuments_UploadedById] ON [WorkOrderDocuments] ([UploadedById]);
GO

CREATE INDEX [IX_WorkOrderDocuments_WorkOrderId] ON [WorkOrderDocuments] ([WorkOrderId]);
GO

CREATE INDEX [IX_WorkOrderLabor_LaborType] ON [WorkOrderLabor] ([LaborType]);
GO

CREATE INDEX [IX_WorkOrderLabor_StartTime] ON [WorkOrderLabor] ([StartTime]);
GO

CREATE INDEX [IX_WorkOrderLabor_TechnicianId] ON [WorkOrderLabor] ([TechnicianId]);
GO

CREATE INDEX [IX_WorkOrderLabor_TenantId] ON [WorkOrderLabor] ([TenantId]);
GO

CREATE INDEX [IX_WorkOrderLabor_WorkOrderId] ON [WorkOrderLabor] ([WorkOrderId]);
GO

CREATE INDEX [IX_WorkOrderParts_AllocationId] ON [WorkOrderParts] ([AllocationId]);
GO

CREATE INDEX [IX_WorkOrderParts_InventoryItemId] ON [WorkOrderParts] ([InventoryItemId]);
GO

CREATE INDEX [IX_WorkOrderParts_ItemCode] ON [WorkOrderParts] ([ItemCode]);
GO

CREATE INDEX [IX_WorkOrderParts_Status] ON [WorkOrderParts] ([Status]);
GO

CREATE INDEX [IX_WorkOrderParts_TenantId] ON [WorkOrderParts] ([TenantId]);
GO

CREATE INDEX [IX_WorkOrderParts_WarehouseLocationId] ON [WorkOrderParts] ([WarehouseLocationId]);
GO

CREATE INDEX [IX_WorkOrderParts_WorkOrderId] ON [WorkOrderParts] ([WorkOrderId]);
GO

CREATE INDEX [IX_WorkOrderQualityChecks_ChecklistId] ON [WorkOrderQualityChecks] ([ChecklistId]);
GO

CREATE INDEX [IX_WorkOrderQualityChecks_InspectionDate] ON [WorkOrderQualityChecks] ([InspectionDate]);
GO

CREATE INDEX [IX_WorkOrderQualityChecks_InspectorId] ON [WorkOrderQualityChecks] ([InspectorId]);
GO

CREATE INDEX [IX_WorkOrderQualityChecks_OverallResult] ON [WorkOrderQualityChecks] ([OverallResult]);
GO

CREATE INDEX [IX_WorkOrderQualityChecks_RequiresFollowUp] ON [WorkOrderQualityChecks] ([RequiresFollowUp]);
GO

CREATE INDEX [IX_WorkOrderQualityChecks_Score] ON [WorkOrderQualityChecks] ([Score]);
GO

CREATE INDEX [IX_WorkOrderQualityChecks_WorkOrderId] ON [WorkOrderQualityChecks] ([WorkOrderId]);
GO

CREATE INDEX [IX_WorkOrderQualitySignOffs_DelegatedToId] ON [WorkOrderQualitySignOffs] ([DelegatedToId]);
GO

CREATE INDEX [IX_WorkOrderQualitySignOffs_IsRequired] ON [WorkOrderQualitySignOffs] ([IsRequired]);
GO

CREATE INDEX [IX_WorkOrderQualitySignOffs_SignOffById] ON [WorkOrderQualitySignOffs] ([SignOffById]);
GO

CREATE INDEX [IX_WorkOrderQualitySignOffs_SignOffDate] ON [WorkOrderQualitySignOffs] ([SignOffDate]);
GO

CREATE INDEX [IX_WorkOrderQualitySignOffs_SignOffLevel] ON [WorkOrderQualitySignOffs] ([SignOffLevel]);
GO

CREATE INDEX [IX_WorkOrderQualitySignOffs_SignOffRole] ON [WorkOrderQualitySignOffs] ([SignOffRole]);
GO

CREATE INDEX [IX_WorkOrderQualitySignOffs_SortOrder] ON [WorkOrderQualitySignOffs] ([SortOrder]);
GO

CREATE INDEX [IX_WorkOrderQualitySignOffs_Status] ON [WorkOrderQualitySignOffs] ([Status]);
GO

CREATE INDEX [IX_WorkOrderQualitySignOffs_TenantId] ON [WorkOrderQualitySignOffs] ([TenantId]);
GO

CREATE INDEX [IX_WorkOrderQualitySignOffs_WorkOrderId] ON [WorkOrderQualitySignOffs] ([WorkOrderId]);
GO

CREATE INDEX [IX_WorkOrderRejections_EscalatedToId] ON [WorkOrderRejections] ([EscalatedToId]);
GO

CREATE INDEX [IX_WorkOrderRejections_IsEscalated] ON [WorkOrderRejections] ([IsEscalated]);
GO

CREATE INDEX [IX_WorkOrderRejections_ReinspectedById] ON [WorkOrderRejections] ([ReinspectedById]);
GO

CREATE INDEX [IX_WorkOrderRejections_RejectedById] ON [WorkOrderRejections] ([RejectedById]);
GO

CREATE INDEX [IX_WorkOrderRejections_RejectedDate] ON [WorkOrderRejections] ([RejectedDate]);
GO

CREATE INDEX [IX_WorkOrderRejections_RejectionType] ON [WorkOrderRejections] ([RejectionType]);
GO

CREATE INDEX [IX_WorkOrderRejections_RequiresReinspection] ON [WorkOrderRejections] ([RequiresReinspection]);
GO

CREATE INDEX [IX_WorkOrderRejections_ResolvedById] ON [WorkOrderRejections] ([ResolvedById]);
GO

CREATE INDEX [IX_WorkOrderRejections_ReworkAssignedToId] ON [WorkOrderRejections] ([ReworkAssignedToId]);
GO

CREATE INDEX [IX_WorkOrderRejections_Severity] ON [WorkOrderRejections] ([Severity]);
GO

CREATE INDEX [IX_WorkOrderRejections_Status] ON [WorkOrderRejections] ([Status]);
GO

CREATE INDEX [IX_WorkOrderRejections_TenantId] ON [WorkOrderRejections] ([TenantId]);
GO

CREATE INDEX [IX_WorkOrderRejections_WorkOrderId] ON [WorkOrderRejections] ([WorkOrderId]);
GO

CREATE INDEX [IX_WorkOrderReworks_AssignedTechnicianId] ON [WorkOrderReworks] ([AssignedTechnicianId]);
GO

CREATE INDEX [IX_WorkOrderReworks_IdentifiedDate] ON [WorkOrderReworks] ([IdentifiedDate]);
GO

CREATE INDEX [IX_WorkOrderReworks_InspectorId] ON [WorkOrderReworks] ([InspectorId]);
GO

CREATE INDEX [IX_WorkOrderReworks_ReworkReason] ON [WorkOrderReworks] ([ReworkReason]);
GO

CREATE INDEX [IX_WorkOrderReworks_Severity] ON [WorkOrderReworks] ([Severity]);
GO

CREATE INDEX [IX_WorkOrderReworks_Status] ON [WorkOrderReworks] ([Status]);
GO

CREATE INDEX [IX_WorkOrderReworks_TargetCompletionDate] ON [WorkOrderReworks] ([TargetCompletionDate]);
GO

CREATE INDEX [IX_WorkOrderReworks_WorkOrderId] ON [WorkOrderReworks] ([WorkOrderId]);
GO

CREATE INDEX [IX_WorkOrderReworks_WorkOrderQualityCheckId] ON [WorkOrderReworks] ([WorkOrderQualityCheckId]);
GO

CREATE INDEX [IX_WorkOrderReworkTasks_CompletedById] ON [WorkOrderReworkTasks] ([CompletedById]);
GO

CREATE INDEX [IX_WorkOrderReworkTasks_CompletedDate] ON [WorkOrderReworkTasks] ([CompletedDate]);
GO

CREATE INDEX [IX_WorkOrderReworkTasks_Sequence] ON [WorkOrderReworkTasks] ([Sequence]);
GO

CREATE INDEX [IX_WorkOrderReworkTasks_Status] ON [WorkOrderReworkTasks] ([Status]);
GO

CREATE INDEX [IX_WorkOrderReworkTasks_WorkOrderReworkId] ON [WorkOrderReworkTasks] ([WorkOrderReworkId]);
GO

CREATE INDEX [IX_WorkOrders_ApprovedById] ON [WorkOrders] ([ApprovedById]);
GO

CREATE INDEX [IX_WorkOrders_AssetId] ON [WorkOrders] ([AssetId]);
GO

CREATE INDEX [IX_WorkOrders_AssignedTeamId] ON [WorkOrders] ([AssignedTeamId]);
GO

CREATE INDEX [IX_WorkOrders_AssignedTechnicianId] ON [WorkOrders] ([AssignedTechnicianId]);
GO

CREATE INDEX [IX_WorkOrders_CompletedById] ON [WorkOrders] ([CompletedById]);
GO

CREATE INDEX [IX_WorkOrders_ContractorId] ON [WorkOrders] ([ContractorId]);
GO

CREATE INDEX [IX_WorkOrders_JobCardId] ON [WorkOrders] ([JobCardId]);
GO

CREATE INDEX [IX_WorkOrders_MaintenanceScheduleId] ON [WorkOrders] ([MaintenanceScheduleId]);
GO

CREATE INDEX [IX_WorkOrders_MaintenanceTypeId] ON [WorkOrders] ([MaintenanceTypeId]);
GO

CREATE INDEX [IX_WorkOrders_ParentWorkOrderId] ON [WorkOrders] ([ParentWorkOrderId]);
GO

CREATE INDEX [IX_WorkOrders_PriorityLevelId] ON [WorkOrders] ([PriorityLevelId]);
GO

CREATE INDEX [IX_WorkOrders_QualityCheckedById] ON [WorkOrders] ([QualityCheckedById]);
GO

CREATE INDEX [IX_WorkOrders_RequestedById] ON [WorkOrders] ([RequestedById]);
GO

CREATE INDEX [IX_WorkOrders_RequestedCompletionDate] ON [WorkOrders] ([RequestedCompletionDate]);
GO

CREATE INDEX [IX_WorkOrders_RequestedStartDate] ON [WorkOrders] ([RequestedStartDate]);
GO

CREATE INDEX [IX_WorkOrders_Status] ON [WorkOrders] ([Status]);
GO

CREATE INDEX [IX_WorkOrders_SupervisorId] ON [WorkOrders] ([SupervisorId]);
GO

CREATE INDEX [IX_WorkOrders_TechnicianId] ON [WorkOrders] ([TechnicianId]);
GO

CREATE INDEX [IX_WorkOrders_TenantId] ON [WorkOrders] ([TenantId]);
GO

CREATE UNIQUE INDEX [IX_WorkOrders_WorkOrderNumber] ON [WorkOrders] ([WorkOrderNumber]);
GO

CREATE INDEX [IX_WorkOrders_WorkOrderTypeId] ON [WorkOrders] ([WorkOrderTypeId]);
GO

CREATE INDEX [IX_WorkOrderTasks_AssignedTechnicianId] ON [WorkOrderTasks] ([AssignedTechnicianId]);
GO

CREATE INDEX [IX_WorkOrderTasks_Sequence] ON [WorkOrderTasks] ([Sequence]);
GO

CREATE INDEX [IX_WorkOrderTasks_Status] ON [WorkOrderTasks] ([Status]);
GO

CREATE INDEX [IX_WorkOrderTasks_TenantId] ON [WorkOrderTasks] ([TenantId]);
GO

CREATE INDEX [IX_WorkOrderTasks_WorkOrderId] ON [WorkOrderTasks] ([WorkOrderId]);
GO

CREATE INDEX [IX_WorkOrderTools_AllocationId] ON [WorkOrderTools] ([AllocationId]);
GO

CREATE INDEX [IX_WorkOrderTools_CheckoutId] ON [WorkOrderTools] ([CheckoutId]);
GO

CREATE INDEX [IX_WorkOrderTools_TenantId] ON [WorkOrderTools] ([TenantId]);
GO

CREATE INDEX [IX_WorkOrderTools_ToolId] ON [WorkOrderTools] ([ToolId]);
GO

CREATE INDEX [IX_WorkOrderTools_WorkOrderId] ON [WorkOrderTools] ([WorkOrderId]);
GO

CREATE INDEX [IX_WorkOrderTypes_Code] ON [WorkOrderTypes] ([Code]);
GO

CREATE INDEX [IX_WorkOrderTypes_IsActive] ON [WorkOrderTypes] ([IsActive]);
GO

CREATE INDEX [IX_WorkOrderTypes_TenantId] ON [WorkOrderTypes] ([TenantId]);
GO

CREATE INDEX [IX_WorkStations_DepartmentId] ON [WorkStations] ([DepartmentId]);
GO

CREATE INDEX [IX_WorkStations_TenantId] ON [WorkStations] ([TenantId]);
GO

ALTER TABLE [AccountBalances] ADD CONSTRAINT [FK_AccountBalances_FiscalPeriods_FiscalPeriodId] FOREIGN KEY ([FiscalPeriodId]) REFERENCES [FiscalPeriods] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [AccountTransactions] ADD CONSTRAINT [FK_AccountTransactions_FiscalPeriods_FiscalPeriodId] FOREIGN KEY ([FiscalPeriodId]) REFERENCES [FiscalPeriods] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [AccountTransactions] ADD CONSTRAINT [FK_AccountTransactions_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [JournalEntries] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [AssetAdmissions] ADD CONSTRAINT [FK_AssetAdmissions_JobCard_JobCardId] FOREIGN KEY ([JobCardId]) REFERENCES [JobCard] ([Id]);
GO

ALTER TABLE [AssetAdmissions] ADD CONSTRAINT [FK_AssetAdmissions_MaintenanceAssets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [AssetAdmissions] ADD CONSTRAINT [FK_AssetAdmissions_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]);
GO

ALTER TABLE [AssetDischarges] ADD CONSTRAINT [FK_AssetDischarges_JobCard_JobCardId] FOREIGN KEY ([JobCardId]) REFERENCES [JobCard] ([Id]);
GO

ALTER TABLE [AssetDischarges] ADD CONSTRAINT [FK_AssetDischarges_MaintenanceAssets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [AssetDischarges] ADD CONSTRAINT [FK_AssetDischarges_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]);
GO

ALTER TABLE [AssetDowntimes] ADD CONSTRAINT [FK_AssetDowntimes_MaintenanceAssets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [AssetDowntimes] ADD CONSTRAINT [FK_AssetDowntimes_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]);
GO

ALTER TABLE [AssetInspections] ADD CONSTRAINT [FK_AssetInspections_Employees_InspectorId] FOREIGN KEY ([InspectorId]) REFERENCES [Employees] ([Id]);
GO

ALTER TABLE [AssetInspections] ADD CONSTRAINT [FK_AssetInspections_MaintenanceAssets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [AssetMaintenanceDowntimes] ADD CONSTRAINT [FK_AssetMaintenanceDowntimes_JobCard_JobCardId] FOREIGN KEY ([JobCardId]) REFERENCES [JobCard] ([Id]);
GO

ALTER TABLE [AssetMaintenanceDowntimes] ADD CONSTRAINT [FK_AssetMaintenanceDowntimes_MaintenanceAssets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [MaintenanceAssets] ([Id]);
GO

ALTER TABLE [AssetMaintenanceDowntimes] ADD CONSTRAINT [FK_AssetMaintenanceDowntimes_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]);
GO

ALTER TABLE [AssetTaskTemplates] ADD CONSTRAINT [FK_AssetTaskTemplates_Employees_AssignedTechnicianId] FOREIGN KEY ([AssignedTechnicianId]) REFERENCES [Employees] ([Id]);
GO

ALTER TABLE [AssetTaskTemplates] ADD CONSTRAINT [FK_AssetTaskTemplates_MaintenanceAssets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [AssetTypeTaskTemplates] ADD CONSTRAINT [FK_AssetTypeTaskTemplates_Employees_AssignedTechnicianId] FOREIGN KEY ([AssignedTechnicianId]) REFERENCES [Employees] ([Id]);
GO

ALTER TABLE [AssetUsageTrackings] ADD CONSTRAINT [FK_AssetUsageTrackings_Employees_RecordedById] FOREIGN KEY ([RecordedById]) REFERENCES [Employees] ([Id]);
GO

ALTER TABLE [AssetUsageTrackings] ADD CONSTRAINT [FK_AssetUsageTrackings_MaintenanceAssets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [MaintenanceAssets] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [AttendanceRecords] ADD CONSTRAINT [FK_AttendanceRecords_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [ContractorWorkOrder] ADD CONSTRAINT [FK_ContractorWorkOrder_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [Departments] ADD CONSTRAINT [FK_Departments_Employees_DepartmentHeadId] FOREIGN KEY ([DepartmentHeadId]) REFERENCES [Employees] ([Id]);
GO

ALTER TABLE [EmployeeBiometrics] ADD CONSTRAINT [FK_EmployeeBiometrics_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [EmployeeContractDetails] ADD CONSTRAINT [FK_EmployeeContractDetails_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [EmployeeDependents] ADD CONSTRAINT [FK_EmployeeDependents_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [EmployeeEmergencyContacts] ADD CONSTRAINT [FK_EmployeeEmergencyContacts_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [EmployeeIdentificationCards] ADD CONSTRAINT [FK_EmployeeIdentificationCards_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [EmployeeQualifications] ADD CONSTRAINT [FK_EmployeeQualifications_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [Employees] ADD CONSTRAINT [FK_Employees_Sections_SectionId] FOREIGN KEY ([SectionId]) REFERENCES [Sections] ([Id]);
GO

ALTER TABLE [FiscalPeriods] ADD CONSTRAINT [FK_FiscalPeriods_FiscalYears_FiscalYearId] FOREIGN KEY ([FiscalYearId]) REFERENCES [FiscalYears] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [JobCard] ADD CONSTRAINT [FK_JobCard_WorkOrders_GeneratedWorkOrderId] FOREIGN KEY ([GeneratedWorkOrderId]) REFERENCES [WorkOrders] ([Id]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251226070950_InitialCreate', N'8.0.0');
GO

COMMIT;
GO

