SET NOCOUNT ON;
SET XACT_ABORT ON;

CREATE TABLE [dbo].[Tenants]
(
    [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_Tenants] PRIMARY KEY
);

CREATE TABLE [dbo].[Users]
(
    [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_Users] PRIMARY KEY
);

CREATE TABLE [dbo].[AuditLogs]
(
    [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_AuditLogs] PRIMARY KEY,
    [TenantId] uniqueidentifier NOT NULL,
    [Action] nvarchar(100) NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    CONSTRAINT [FK_AuditLogs_Tenants_TenantId]
        FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [dbo].[ProcurementControlEvents]
(
    [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_ProcurementControlEvents] PRIMARY KEY,
    [TenantId] uniqueidentifier NOT NULL,
    [Action] nvarchar(200) NOT NULL,
    [OccurredAtUtc] datetime2 NOT NULL
);
GO

CREATE TRIGGER [dbo].[TR_ProcurementControlEvents_AppendOnly]
ON [dbo].[ProcurementControlEvents]
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, 'Procurement control events are append-only and cannot be updated or deleted.', 1;
END;
GO

CREATE TABLE [dbo].[DataRetentionPolicies]
(
    [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_DataRetentionPolicies] PRIMARY KEY,
    [AuditLogRetentionDays] int NOT NULL,
    [WorkflowAuditRetentionDays] int NOT NULL
);

CREATE TABLE [dbo].[__EFMigrationsHistory]
(
    [MigrationId] nvarchar(150) NOT NULL CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY,
    [ProductVersion] nvarchar(32) NOT NULL
);

DECLARE @tenantId uniqueidentifier = '11111111-1111-1111-1111-111111111111';
DECLARE @userId uniqueidentifier = '22222222-2222-2222-2222-222222222222';

INSERT INTO [dbo].[Tenants] ([Id]) VALUES (@tenantId);
INSERT INTO [dbo].[Users] ([Id]) VALUES (@userId);
INSERT INTO [dbo].[AuditLogs] ([Id], [TenantId], [Action], [Timestamp])
VALUES ('33333333-3333-3333-3333-333333333333', @tenantId, N'Created', '2026-08-06T00:00:00Z');
INSERT INTO [dbo].[DataRetentionPolicies]
    ([Id], [AuditLogRetentionDays], [WorkflowAuditRetentionDays])
VALUES ('44444444-4444-4444-4444-444444444444', 365, 365);

INSERT INTO [dbo].[ProcurementControlEvents] ([Id], [TenantId], [Action], [OccurredAtUtc])
VALUES
    ('50000000-0000-0000-0000-000000000001', @tenantId, N'Created', '2026-08-06T00:00:00Z'),
    ('50000000-0000-0000-0000-000000000002', @tenantId, N'Updated', '2026-08-06T00:00:01Z'),
    ('50000000-0000-0000-0000-000000000003', @tenantId, N'InvoiceMatchExceptionApproved', '2026-08-06T00:00:02Z'),
    ('50000000-0000-0000-0000-000000000004', @tenantId, N'InvoiceMatchExceptionRejected', '2026-08-06T00:00:03Z'),
    ('50000000-0000-0000-0000-000000000005', @tenantId, N'OverrideSourcingMethod', '2026-08-06T00:00:04Z'),
    ('50000000-0000-0000-0000-000000000006', @tenantId, N'PostStockAdjustment', '2026-08-06T00:00:05Z'),
    ('50000000-0000-0000-0000-000000000007', @tenantId, N'ReverseStockAdjustment', '2026-08-06T00:00:06Z'),
    ('50000000-0000-0000-0000-000000000008', @tenantId, N'Dispatch', '2026-08-06T00:00:07Z'),
    ('50000000-0000-0000-0000-000000000009', @tenantId, N'Receive', '2026-08-06T00:00:08Z');

INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260805122000_FixSupplierApplicantPaymentAttribution', N'8.0.0');

PRINT 'TDC0706_BASELINE_READY';
