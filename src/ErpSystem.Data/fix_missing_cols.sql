-- Add BankAccountId column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[CustomerPayments]') AND name = 'BankAccountId')
BEGIN
    ALTER TABLE [CustomerPayments] ADD [BankAccountId] uniqueidentifier NULL;
    CREATE INDEX [IX_CustomerPayments_BankAccountId] ON [CustomerPayments] ([BankAccountId]);
    ALTER TABLE [CustomerPayments] ADD CONSTRAINT [FK_CustomerPayments_BankAccounts_BankAccountId] FOREIGN KEY ([BankAccountId]) REFERENCES [BankAccounts] ([Id]);
END

-- Add CheckNumber column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[CustomerPayments]') AND name = 'CheckNumber')
BEGIN
    ALTER TABLE [CustomerPayments] ADD [CheckNumber] nvarchar(100) NULL;
END

-- Add CreditNoteId column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[CustomerPayments]') AND name = 'CreditNoteId')
BEGIN
    ALTER TABLE [CustomerPayments] ADD [CreditNoteId] uniqueidentifier NULL;
END

-- Add TransactionReference column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[CustomerPayments]') AND name = 'TransactionReference')
BEGIN
    ALTER TABLE [CustomerPayments] ADD [TransactionReference] nvarchar(100) NULL;
END

-- Add EffectiveDate column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[CustomerPayments]') AND name = 'EffectiveDate')
BEGIN
    ALTER TABLE [CustomerPayments] ADD [EffectiveDate] datetime2 NULL;
END

-- Add ExpirationDate column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[CustomerPayments]') AND name = 'ExpirationDate')
BEGIN
    ALTER TABLE [CustomerPayments] ADD [ExpirationDate] datetime2 NULL;
END

-- Add Metadata column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[CustomerPayments]') AND name = 'Metadata')
BEGIN
    ALTER TABLE [CustomerPayments] ADD [Metadata] nvarchar(max) NULL;
END

-- Add Priority column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[CustomerPayments]') AND name = 'Priority')
BEGIN
    ALTER TABLE [CustomerPayments] ADD [Priority] int NOT NULL DEFAULT 5;
END

-- Add Tags column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[CustomerPayments]') AND name = 'Tags')
BEGIN
    ALTER TABLE [CustomerPayments] ADD [Tags] nvarchar(500) NULL;
END
