-- Recreate InventoryAllocations
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
    CONSTRAINT [PK_InventoryAllocations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InventoryAllocations_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [InventoryItems]([Id]),
    CONSTRAINT [FK_InventoryAllocations_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses]([Id]),
    CONSTRAINT [FK_InventoryAllocations_WarehouseLocations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [WarehouseLocations]([Id]),
    CONSTRAINT [FK_InventoryAllocations_Users_AllocatedById] FOREIGN KEY ([AllocatedById]) REFERENCES [Users]([Id]),
    CONSTRAINT [FK_InventoryAllocations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_InventoryAllocations_AllocatedById] ON [InventoryAllocations] ([AllocatedById]);
CREATE INDEX [IX_InventoryAllocations_InventoryItemId] ON [InventoryAllocations] ([InventoryItemId]);
CREATE INDEX [IX_InventoryAllocations_LocationId] ON [InventoryAllocations] ([LocationId]);
CREATE INDEX [IX_InventoryAllocations_TenantId] ON [InventoryAllocations] ([TenantId]);
CREATE INDEX [IX_InventoryAllocations_WarehouseId] ON [InventoryAllocations] ([WarehouseId]);

-- Recreate WorkOrderParts
CREATE TABLE [WorkOrderParts] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [PartId] uniqueidentifier NOT NULL,
    [AllocationId] uniqueidentifier NULL,
    [QuantityRequired] decimal(18,4) NOT NULL,
    [QuantityUsed] decimal(18,4) NOT NULL,
    [QuantityWasted] decimal(18,4) NOT NULL,
    [EstimatedCost] decimal(18,2) NULL,
    [ActualCost] decimal(18,2) NULL,
    [Notes] nvarchar(1000) NULL,
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
    CONSTRAINT [PK_WorkOrderParts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderParts_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders]([Id]),
    CONSTRAINT [FK_WorkOrderParts_InventoryItems_PartId] FOREIGN KEY ([PartId]) REFERENCES [InventoryItems]([Id]),
    CONSTRAINT [FK_WorkOrderParts_InventoryAllocations_AllocationId] FOREIGN KEY ([AllocationId]) REFERENCES [InventoryAllocations]([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_WorkOrderParts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_WorkOrderParts_AllocationId] ON [WorkOrderParts] ([AllocationId]);
CREATE INDEX [IX_WorkOrderParts_PartId] ON [WorkOrderParts] ([PartId]);
CREATE INDEX [IX_WorkOrderParts_TenantId] ON [WorkOrderParts] ([TenantId]);
CREATE INDEX [IX_WorkOrderParts_WorkOrderId] ON [WorkOrderParts] ([WorkOrderId]);

-- Recreate ToolCheckouts
CREATE TABLE [ToolCheckouts] (
    [Id] uniqueidentifier NOT NULL,
    [ToolId] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NULL,
    [JobCardId] uniqueidentifier NULL,
    [CheckedOutById] uniqueidentifier NOT NULL,
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
    CONSTRAINT [PK_ToolCheckouts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ToolCheckouts_InventoryItems_ToolId] FOREIGN KEY ([ToolId]) REFERENCES [InventoryItems]([Id]),
    CONSTRAINT [FK_ToolCheckouts_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders]([Id]),
    CONSTRAINT [FK_ToolCheckouts_JobCard_JobCardId] FOREIGN KEY ([JobCardId]) REFERENCES [JobCard]([Id]),
    CONSTRAINT [FK_ToolCheckouts_Users_CheckedOutById] FOREIGN KEY ([CheckedOutById]) REFERENCES [Users]([Id]),
    CONSTRAINT [FK_ToolCheckouts_Users_CheckedInById] FOREIGN KEY ([CheckedInById]) REFERENCES [Users]([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_ToolCheckouts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_ToolCheckouts_CheckedInById] ON [ToolCheckouts] ([CheckedInById]);
CREATE INDEX [IX_ToolCheckouts_CheckedOutById] ON [ToolCheckouts] ([CheckedOutById]);
CREATE INDEX [IX_ToolCheckouts_JobCardId] ON [ToolCheckouts] ([JobCardId]);
CREATE INDEX [IX_ToolCheckouts_TenantId] ON [ToolCheckouts] ([TenantId]);
CREATE INDEX [IX_ToolCheckouts_ToolId] ON [ToolCheckouts] ([ToolId]);
CREATE INDEX [IX_ToolCheckouts_WorkOrderId] ON [ToolCheckouts] ([WorkOrderId]);

-- Recreate WorkOrderTools
CREATE TABLE [WorkOrderTools] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [ToolId] uniqueidentifier NOT NULL,
    [CheckoutId] uniqueidentifier NULL,
    [AllocationId] uniqueidentifier NULL,
    [WarehouseId] uniqueidentifier NULL,
    [Quantity] int NOT NULL,
    [Cost] decimal(18,2) NULL,
    [IsAllocated] bit NOT NULL,
    [IsReturned] bit NOT NULL,
    [Notes] nvarchar(max) NULL,
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
    CONSTRAINT [PK_WorkOrderTools] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderTools_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders]([Id]),
    CONSTRAINT [FK_WorkOrderTools_InventoryItems_ToolId] FOREIGN KEY ([ToolId]) REFERENCES [InventoryItems]([Id]),
    CONSTRAINT [FK_WorkOrderTools_ToolCheckouts_CheckoutId] FOREIGN KEY ([CheckoutId]) REFERENCES [ToolCheckouts]([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_WorkOrderTools_InventoryAllocations_AllocationId] FOREIGN KEY ([AllocationId]) REFERENCES [InventoryAllocations]([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_WorkOrderTools_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrderTools_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_WorkOrderTools_AllocationId] ON [WorkOrderTools] ([AllocationId]);
CREATE INDEX [IX_WorkOrderTools_CheckoutId] ON [WorkOrderTools] ([CheckoutId]);
CREATE INDEX [IX_WorkOrderTools_TenantId] ON [WorkOrderTools] ([TenantId]);
CREATE INDEX [IX_WorkOrderTools_ToolId] ON [WorkOrderTools] ([ToolId]);
CREATE INDEX [IX_WorkOrderTools_WarehouseId] ON [WorkOrderTools] ([WarehouseId]);
CREATE INDEX [IX_WorkOrderTools_WorkOrderId] ON [WorkOrderTools] ([WorkOrderId]);
