-- Read-only checks shared by fresh installation and populated-copy upgrade.
IF NOT EXISTS(SELECT 1 FROM sys.columns
 WHERE object_id=OBJECT_ID(N'dbo.InventoryIssueVouchers') AND name=N'ApprovedById' AND is_nullable=1)
 THROW 52080,'Optional issue approval column is missing.',1;
DECLARE @guard nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_InventoryIssueVouchers_ControlledLifecycle'));
IF @guard IS NULL OR NOT EXISTS(SELECT 1 FROM sys.triggers
 WHERE object_id=OBJECT_ID(N'dbo.TR_InventoryIssueVouchers_ControlledLifecycle') AND is_disabled=0)
 OR CHARINDEX(N'INV_ISSUE_OPTIONAL_APPROVAL_START',@guard)=0
 OR CHARINDEX(N'INV_ISSUE_APPROVAL_REQUIRED',@guard)=0
 OR CHARINDEX(N'ISNULL(i.ApprovedById',@guard)=0
 OR CHARINDEX(N'ISNULL(r.ApprovedById',@guard)=0
 OR CHARINDEX(N'INV_RECEIPT_INITIAL_SEQUENCE_INVALID',@guard)=0
 OR CHARINDEX(N'FinancePostingEvents',@guard)=0
 THROW 52081,'Issue approval, receipt or Finance lifecycle guard is missing.',1;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints
 WHERE parent_object_id=OBJECT_ID(N'dbo.InventoryIssueVouchers')
 AND name=N'CK_InventoryIssueVouchers_Sod' AND is_disabled=0 AND is_not_trusted=0)
 THROW 52082,'Independent issue actor constraint is missing.',1;
