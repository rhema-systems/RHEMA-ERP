-- Disposable fixture for the real PO commercial-capacity triggers, not live data.
CREATE TABLE PurchaseOrders (Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,
 ProcurementSourceType int,ProcurementSourceId uniqueidentifier,BusinessPartnerId uniqueidentifier,
 Currency nvarchar(10),TotalAmount decimal(18,2),Status nvarchar(40),IsDeleted bit DEFAULT 0);
CREATE TABLE PurchaseOrderItems (Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,
 PurchaseOrderId uniqueidentifier,InventoryItemId uniqueidentifier NULL,SourceRfqItemId uniqueidentifier NULL,
 ItemDescription nvarchar(200),UnitOfMeasure nvarchar(20),UnitPrice decimal(18,4),
 OrderedQuantity decimal(18,4),LineTotal decimal(18,2),IsDeleted bit DEFAULT 0);
CREATE TABLE RequestForQuotations (Id uniqueidentifier,TenantId uniqueidentifier,Currency nvarchar(10),IsDeleted bit);
CREATE TABLE RequestForQuotationItems (Id uniqueidentifier,TenantId uniqueidentifier,RfqId uniqueidentifier,
 InventoryItemId uniqueidentifier NULL,Description nvarchar(200),UnitOfMeasure nvarchar(20),Quantity decimal(18,4),IsDeleted bit);
CREATE TABLE RequestForQuotationAwardLines (TenantId uniqueidentifier,RfqId uniqueidentifier,
 BusinessPartnerId uniqueidentifier,RfqItemId uniqueidentifier,UnitPrice decimal(18,4),LineTotal decimal(18,2),IsDeleted bit);
CREATE TABLE Contracts (Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,TenderAwardId uniqueidentifier,
 Currency nvarchar(10),ContractValue decimal(18,2),IsDeleted bit);
CREATE TABLE TenderAwards (Id uniqueidentifier,TenantId uniqueidentifier,TenderBidId uniqueidentifier,
 BidLotId uniqueidentifier NULL,NegotiationId uniqueidentifier NULL,IsDeleted bit);
CREATE TABLE TenderBidItems (Id uniqueidentifier,TenantId uniqueidentifier,TenderBidId uniqueidentifier,
 TenderItemId uniqueidentifier,BidLotId uniqueidentifier NULL,OfferedQuantity decimal(18,4),UnitPrice decimal(18,4),IsDeleted bit);
CREATE TABLE TenderItems (Id uniqueidentifier,TenantId uniqueidentifier,Description nvarchar(200),UnitOfMeasure nvarchar(20),IsDeleted bit);
CREATE TABLE TenderNegotiationItems (NegotiationId uniqueidentifier,TenderBidItemId uniqueidentifier,
 TenantId uniqueidentifier,Quantity decimal(18,4),NegotiatedUnitPrice decimal(18,4),NegotiatedTotalPrice decimal(18,2),IsDeleted bit);
DECLARE @tenant uniqueidentifier='10000000-0000-0000-0000-000000000001',
 @contract uniqueidentifier='20000000-0000-0000-0000-000000000001',
 @award uniqueidentifier=NEWID(),@bid uniqueidentifier=NEWID(),@supplier uniqueidentifier=NEWID();
INSERT Contracts VALUES(@contract,@tenant,@award,'GHS',52000,0);
INSERT TenderAwards VALUES(@award,@tenant,@bid,NULL,NULL,0);
INSERT TenderItems VALUES(NEWID(),@tenant,'Barcode Device Kit','EA',0),(NEWID(),@tenant,'PVC Pipe 50mm','EACH',0);
INSERT TenderBidItems SELECT NEWID(),@tenant,@bid,Id,NULL,20,CASE WHEN UnitOfMeasure='EACH' THEN 1900 ELSE 700 END,0 FROM TenderItems;
INSERT PurchaseOrders VALUES('30000000-0000-0000-0000-000000000001',@tenant,2,@contract,@supplier,'GHS',52000,'Draft',0);
-- RFQ deliberately has no inventory identity at award time; later mapped stock
-- must continue to use its retained SourceRfqItemId.
DECLARE @rfq uniqueidentifier=NEWID(),@rfqItem uniqueidentifier=NEWID();
INSERT RequestForQuotations VALUES(@rfq,@tenant,'GHS',0);
INSERT RequestForQuotationItems VALUES(@rfqItem,@tenant,@rfq,NULL,'RFQ mapped item','EA',2,0);
INSERT RequestForQuotationAwardLines VALUES(@tenant,@rfq,@supplier,@rfqItem,50,100,0);
INSERT PurchaseOrders VALUES('30000000-0000-0000-0000-000000000002',@tenant,0,@rfq,@supplier,'GHS',100,'Draft',0);
