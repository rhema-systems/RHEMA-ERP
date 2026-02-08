# Purchasing Module Implementation Plan

## Executive Summary
This document outlines the complete implementation plan for the Purchasing Module, including Purchase Requisitions, Purchase Orders, and Purchase Receipts (GRN). The analysis shows that backend infrastructure exists but needs migration from legacy Supplier entity to BusinessPartner entity, and frontend implementation is incomplete.

---

## Critical Architecture Decision

### 🔄 **MIGRATION REQUIRED: Supplier → BusinessPartner**

The system currently has TWO separate supplier management systems:

1. **Legacy Supplier Entity** (ProcurementEntities.cs)
   - Used by: PurchaseOrder, PurchaseRequisition
   - Status: Legacy/Deprecated
   - Action: **MIGRATE TO BusinessPartner**

2. **BusinessPartner Entity** (BusinessPartnerEntities.cs) ✅
   - Used by: Tenders, Contracts, Performance Tracking
   - Status: **Current/Active** (95% complete)
   - Features: Comprehensive supplier/contractor management
   - Frontend: Fully implemented at `/procurement/business-partners`
   - Service: `businessPartnerService.ts` (400+ lines)

**Decision:** Scrap Supplier entity and migrate all purchasing to use BusinessPartner

---

## Current Implementation Status

### ✅ **COMPLETED - BusinessPartner System**

#### 1. **Database Entities** (100% Complete)
- ✅ `BusinessPartner` - Unified supplier/contractor entity
- ✅ `PartnerCategory` - Hierarchical categories
- ✅ `ContractorSpecialization` - Specializations
- ✅ `LicenseType` - License configuration
- ✅ `BusinessPartnerLicense` - License tracking
- ✅ `BusinessPartnerContact` - Multiple contacts
- ✅ `BusinessPartnerDocument` - Document management
- ✅ `BusinessPartnerFinancial` - Financial history
- ✅ `BusinessPartnerRegistration` - External registration workflow
- ✅ `BusinessPartnerUser` - Multi-user management

#### 2. **Frontend Implementation** (100% Complete)
- ✅ `/procurement/business-partners` - List & management
- ✅ `/procurement/business-partners/[id]` - Detail view
- ✅ `/procurement/business-partners/[id]/edit` - Edit page
- ✅ `/procurement/business-partners/new` - Create page
- ✅ `/register/business-partner` - External registration portal
- ✅ `/administration/procurement/registrations` - Registration review
- ✅ `businessPartnerService.ts` - Complete API integration

#### 3. **Admin Configuration** (100% Complete)
- ✅ Partner Categories management
- ✅ Contractor Specializations management
- ✅ License Types management
- ✅ All CRUD operations functional

---

### ⚠️ **NEEDS MIGRATION - Purchase Requisitions & Orders**

#### 1. **Database Entities** (Needs Update)
- ✅ `PurchaseRequisition` - Exists but uses legacy Supplier
- ✅ `PurchaseRequisitionItem` - Exists
- ✅ `PurchaseOrder` - Exists but uses `SupplierId` (needs migration to `BusinessPartnerId`)
- ✅ `PurchaseOrderItem` - Exists
- ✅ `PurchaseOrderReceipt` - Exists
- ✅ `PurchaseOrderReceiptItem` - Exists

#### 2. **Backend APIs** (Needs Update)
- ✅ `PurchaseRequisitionsController` - Exists but references Supplier
- ✅ `PurchaseOrdersController` - Exists but references Supplier
- ⚠️ Need to update to use BusinessPartner instead

#### 3. **Frontend** (Needs Complete Implementation)
- ❌ No Purchase Requisition pages
- ❌ No Purchase Order pages
- ⚠️ `/procurement/purchase-receipts/page.tsx` - Exists but basic (uses inventory service)

---

## Implementation Plan

### **Phase 0: Backend Migration** (Priority: CRITICAL - 2-3 days)

#### 0.1 Update Database Entities
**Files to Modify:**
- `src/ErpSystem.Core/Entities/Procurement/ProcurementEntities.cs`

**Changes:**
```csharp
// PurchaseOrder entity
- Remove: public Guid SupplierId { get; set; }
- Remove: public virtual Supplier Supplier { get; set; }
+ Add: public Guid BusinessPartnerId { get; set; }
+ Add: public virtual BusinessPartner BusinessPartner { get; set; }

// PurchaseRequisition entity  
- Remove: public Guid? PreferredSupplierId { get; set; }
+ Add: public Guid? PreferredBusinessPartnerId { get; set; }

// PurchaseRequisitionItem entity
- Remove: public Guid? PreferredSupplierId { get; set; }
- Remove: public virtual Supplier? PreferredSupplier { get; set; }
+ Add: public Guid? PreferredBusinessPartnerId { get; set; }
+ Add: public virtual BusinessPartner? PreferredBusinessPartner { get; set; }
```

#### 0.2 Create Database Migration
**Command:** `dotnet ef migrations add MigrateSupplierToBusinessPartner`

**Migration Tasks:**
1. Add `BusinessPartnerId` column to `PurchaseOrders`
2. Migrate existing Supplier data to BusinessPartner (if any)
3. Update foreign key constraints
4. Remove `SupplierId` column (after data migration)
5. Update indexes

#### 0.3 Update DTOs
**Files to Modify:**
- `src/ErpSystem.Core/DTOs/Procurement/PurchaseOrderDTOs.cs`
- `src/ErpSystem.Core/DTOs/Procurement/PurchaseRequisitionDTOs.cs`

**Changes:**
```csharp
// Replace all Supplier references with BusinessPartner
- SupplierId → BusinessPartnerId
- SupplierName → BusinessPartnerName
- PreferredSupplierId → PreferredBusinessPartnerId
```

#### 0.4 Update Controllers
**Files to Modify:**
- `src/ErpSystem.Api/Controllers/Procurement/PurchaseOrdersController.cs`
- `src/ErpSystem.Api/Controllers/Procurement/PurchaseRequisitionsController.cs`

**Changes:**
- Replace `ISupplierRepository` with `IBusinessPartnerRepository`
- Update all supplier references to business partner
- Update blacklist check to use BusinessPartner.IsBlacklisted

#### 0.5 Update Repositories
**Files to Modify:**
- `src/ErpSystem.Data/Repositories/Procurement/PurchaseOrderRepositories.cs`
- `src/ErpSystem.Data/Repositories/Procurement/PurchaseRequisitionRepositories.cs`

**Changes:**
- Update Include statements: `.Include(po => po.BusinessPartner)`
- Update queries to use BusinessPartner navigation

#### 0.6 Remove Legacy Supplier Entity (Optional - After Migration)
**Files to Remove/Deprecate:**
- Supplier entity from `ProcurementEntities.cs`
- SupplierContact entity
- SupplierItemCatalog entity
- ISupplierRepository interface
- SupplierRepository implementation
- SuppliersController

---

### **Phase 1: Frontend Services & DTOs** (Priority: HIGH - 2-3 days)

#### 1.1 Create Purchasing Service
**File:** `frontend/src/services/purchasingService.ts`

**Required Interfaces:**
```typescript
// Purchase Requisition DTOs
interface PurchaseRequisitionSummaryDto {
  id: string;
  requisitionNumber: string;
  requisitionDate: string;
  requestedByName: string;
  requiredDate?: string;
  status: string;
  priority: string;
  department?: string;
  totalAmount: number;
  itemCount: number;
}

interface PurchaseRequisitionDetailDto extends PurchaseRequisitionSummaryDto {
  costCenter?: string;
  justification?: string;
  notes?: string;
  approvedByName?: string;
  approvedAt?: string;
  rejectionReason?: string;
  items: PurchaseRequisitionItemDto[];
}

interface CreatePurchaseRequisitionDto {
  requestedById: string;
  requiredDate?: string;
  priority: string;
  department?: string;
  costCenter?: string;
  justification?: string;
  notes?: string;
  items: CreatePurchaseRequisitionItemDto[];
}

interface PurchaseRequisitionItemDto {
  id: string;
  requisitionId: string;
  inventoryItemId?: string;
  itemCode?: string;
  itemName?: string;
  itemDescription: string;
  quantity: number;
  unitOfMeasure: string;
  estimatedUnitPrice: number;
  lineTotal: number;
  requiredDate?: string;
  preferredBusinessPartnerId?: string;
  preferredBusinessPartnerName?: string;
  notes?: string;
  specifications?: string;
  status: string;
  purchaseOrderId?: string;
  purchaseOrderNumber?: string;
}

// Purchase Order DTOs
interface PurchaseOrderSummaryDto {
  id: string;
  orderNumber: string;
  businessPartnerId: string;
  businessPartnerName: string;
  orderDate: string;
  requiredDate?: string;
  promisedDate?: string;
  status: string;
  totalAmount: number;
  itemCount: number;
  requestedByName?: string;
}

interface PurchaseOrderDetailDto extends PurchaseOrderSummaryDto {
  receivedDate?: string;
  approvedByName?: string;
  approvedAt?: string;
  subTotal: number;
  taxAmount: number;
  shippingCost: number;
  discountAmount: number;
  paymentTerms?: string;
  shippingTerms?: string;
  terms?: string;
  notes?: string;
  deliveryWarehouseId?: string;
  deliveryAddress?: string;
  deliveryInstructions?: string;
  supplierOrderNumber?: string;
  referenceNumber?: string;
  businessPartnerPhone?: string;
  businessPartnerEmail?: string;
  businessPartnerAddress?: string;
  items: PurchaseOrderItemDto[];
  receipts: PurchaseOrderReceiptDto[];
}

interface CreatePurchaseOrderDto {
  businessPartnerId: string;
  requiredDate?: string;
  promisedDate?: string;
  paymentTerms?: string;
  shippingTerms?: string;
  terms?: string;
  notes?: string;
  deliveryWarehouseId?: string;
  deliveryAddress?: string;
  deliveryInstructions?: string;
  referenceNumber?: string;
  requestedById?: string;
  items: CreatePurchaseOrderItemDto[];
}

interface PurchaseOrderItemDto {
  id: string;
  purchaseOrderId: string;
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  supplierItemCode?: string;
  itemDescription?: string;
  orderedQuantity: number;
  receivedQuantity: number;
  remainingQuantity: number;
  unitPrice: number;
  lineTotal: number;
  expectedDeliveryDate?: string;
  notes?: string;
}

interface PurchaseOrderReceiptDto {
  id: string;
  purchaseOrderId: string;
  receiptNumber: string;
  receiptDate: string;
  deliveryNote?: string;
  carrierName?: string;
  trackingNumber?: string;
  status: string;
  receivedByName?: string;
  inspectedByName?: string;
  notes?: string;
  requiresInspection: boolean;
  inspectionDate?: string;
  inspectionResult?: string;
  inspectionNotes?: string;
  purchaseOrderNumber: string;
  businessPartnerName: string;
}
```

**Required API Methods:**
```typescript
// Purchase Requisitions
- getPurchaseRequisitions(page, pageSize, filters)
- getPurchaseRequisitionById(id)
- createPurchaseRequisition(data)
- updatePurchaseRequisitionStatus(id, status)
- approvePurchaseRequisition(id, approval)
- submitPurchaseRequisition(id)
- getPurchaseRequisitionsByStatus(status)
- getPurchaseRequisitionsByPriority(priority)
- getPurchaseRequisitionsByDepartment(department)
- getPendingApprovalRequisitions()
- convertToPurchaseOrder(id)

// Purchase Orders
- getPurchaseOrders(page, pageSize, filters)
- getPurchaseOrderById(id)
- createPurchaseOrder(data)
- updatePurchaseOrderStatus(id, status)
- approvePurchaseOrder(id, approval)
- submitPurchaseOrder(id)
- getPurchaseOrdersByBusinessPartner(businessPartnerId)
- getPurchaseOrdersByStatus(status)
- receivePurchaseOrder(id, receiptData)
```

---

### **Phase 2: Purchase Requisitions Module** (Priority: HIGH - 5-8 days)

#### 2.1 Purchase Requisition List Page
**File:** `frontend/src/app/procurement/purchase-requisitions/page.tsx`

**Features:**
- List all requisitions with pagination
- Filter by status (Draft, Submitted, Pending Approval, Approved, Rejected, Ordered)
- Filter by priority (Low, Normal, High, Urgent)
- Filter by department
- Search by requisition number, requester
- Date range filter
- Stats cards (Total, Pending Approval, Approved, Rejected)
- Quick actions (View, Edit, Submit, Approve/Reject, Convert to PO)
- Status badges with colors
- Create new requisition button

**UI Layout:**
```
┌─────────────────────────────────────────────────────┐
│ Purchase Requisitions              [+ New Requisition]│
│ Manage internal purchase requests                    │
├─────────────────────────────────────────────────────┤
│ Dashboard > Procurement > Purchase Requisitions      │
├─────────────────────────────────────────────────────┤
│ [Total: 45] [Pending: 12] [Approved: 8] [Rejected: 2]│
├─────────────────────────────────────────────────────┤
│ [Search...] [Status ▼] [Priority ▼] [Department ▼]  │
├─────────────────────────────────────────────────────┤
│ PR-2024-001 | Normal | Draft | IT Dept | $5,200     │
│ PR-2024-002 | High | Pending | HR | $12,500         │
│ ...                                                   │
└─────────────────────────────────────────────────────┘
```

#### 2.2 Create Purchase Requisition Page
**File:** `frontend/src/app/procurement/purchase-requisitions/new/page.tsx`

**Form Sections:**
1. **Basic Information**
   - Requisition date (auto-filled, read-only)
   - Required date (date picker)
   - Priority (dropdown: Low, Normal, High, Urgent)
   - Department (text input or dropdown)
   - Cost center (text input)
   - Justification (textarea)

2. **Items Section**
   - Add Item button
   - Item table with columns:
     - Item (searchable dropdown from inventory)
     - Description (auto-filled or manual)
     - Quantity
     - Unit of Measure
     - Estimated Unit Price
     - Line Total (calculated)
     - Preferred Business Partner (dropdown)
     - Required Date
     - Specifications/Notes
     - Actions (Edit, Delete)
   - Total Amount (calculated)

3. **Actions**
   - Save as Draft
   - Submit for Approval
   - Cancel

#### 2.3 View/Edit Purchase Requisition Page
**File:** `frontend/src/app/procurement/purchase-requisitions/[id]/page.tsx`

**Features:**
- View mode (default)
- Edit mode (if status = Draft)
- Approval section (if user has approval rights)
- Status timeline/history
- Convert to PO button (if approved)
- Print/Export button
- Audit trail section

---

### **Phase 3: Purchase Orders Module** (Priority: HIGH - 7-11 days)

#### 3.1 Purchase Order List Page
**File:** `frontend/src/app/procurement/purchase-orders/page.tsx`

**Features:**
- List all purchase orders with pagination
- Filter by status (Draft, Pending Approval, Approved, Sent, Acknowledged, Partially Received, Received, Cancelled)
- Filter by business partner (dropdown)
- Date range filter
- Search by PO number, business partner
- Stats cards (Total, Pending, Active, Received, Overdue)
- Quick actions (View, Edit, Approve, Send, Receive, Cancel)
- Status badges
- Create new PO button
- Link to source requisition (if applicable)

#### 3.2 Create Purchase Order Page
**File:** `frontend/src/app/procurement/purchase-orders/new/page.tsx`

**Creation Modes:**
1. **From Scratch**
2. **From Approved Requisition** (pre-fill data)

**Form Sections:**
1. **Business Partner Selection**
   - Business Partner dropdown (filtered by PartnerType = 'Supplier' or 'Both')
   - Display: Partner info, payment terms, blacklist status
   - Blacklist warning if applicable

2. **Order Details**
   - Order date (auto, read-only)
   - Required date
   - Promised date
   - Payment terms (from business partner or custom)
   - Shipping terms
   - Delivery warehouse (dropdown)
   - Delivery address
   - Terms & conditions (textarea)
   - Reference number
   - Notes

3. **Items Section**
   - Add Item button
   - Item table with columns:
     - Item (from inventory)
     - Business Partner Item Code
     - Description
     - Ordered Quantity
     - Unit Price
     - Line Total
     - Expected Delivery Date
     - Notes
     - Actions

4. **Financial Summary**
   - Subtotal (calculated)
   - Tax Amount
   - Shipping Cost
   - Discount Amount
   - **Total Amount** (bold)

5. **Actions**
   - Save as Draft
   - Submit for Approval
   - Cancel

#### 3.3 View/Edit Purchase Order Page
**File:** `frontend/src/app/procurement/purchase-orders/[id]/page.tsx`

**Tabs:**
1. **Overview**
   - PO header information
   - Business partner details
   - Status and dates
   - Financial summary

2. **Items**
   - Items table with received quantities
   - Progress bars for receipt status

3. **Receipts**
   - List of all receipts/GRNs
   - Receipt details
   - Link to receipt detail page

4. **Approval History**
   - Approval timeline
   - Approver names and dates
   - Comments

5. **Audit Trail**
   - All changes to the PO
   - User and timestamp

**Actions (Status-dependent):**
- Edit (if Draft)
- Submit for Approval (if Draft)
- Approve/Reject (if Pending Approval)
- Send to Business Partner (if Approved)
- Receive Goods (if Approved/Sent)
- Cancel PO
- Print PO
- Export PDF

#### 3.4 Receive Purchase Order Dialog
**Component:** Dialog or separate page

**Features:**
- PO summary (number, business partner, total)
- Items to receive table:
  - Item name/code
  - Ordered quantity
  - Previously received
  - Remaining quantity
  - **Quantity to receive now** (input)
  - Accepted quantity (input)
  - Rejected quantity (input)
  - Rejection reason (if rejected > 0)
  - Quality status (dropdown)
  - Serial/Lot number (if tracked)
  - Expiration date (if tracked)
  - Location (dropdown)
  - Notes
- Receipt information:
  - Receipt date (date picker)
  - Delivery note number
  - Carrier name
  - Tracking number
  - Received by (current user, read-only)
  - Requires inspection (checkbox)
  - Notes
- Actions:
  - Create Receipt
  - Cancel

---

### **Phase 4: Enhanced Purchase Receipts (GRN)** (Priority: MEDIUM - 4-6 days)

#### 4.1 Redesign Purchase Receipts Page
**File:** `frontend/src/app/procurement/purchase-receipts/page.tsx` (Complete Refactor)

**Changes:**
- Use `purchasingService` instead of `inventoryManagementService`
- Link to source Purchase Order
- Show business partner information
- Enhanced filters (by PO, business partner, date range, status)
- Quality inspection workflow
- Approval workflow
- Print GRN
- Create purchase return button

#### 4.2 GRN Detail Page
**File:** `frontend/src/app/procurement/purchase-receipts/[id]/page.tsx` (New)

**Tabs:**
1. **Receipt Details**
   - Receipt header
   - PO reference
   - Business partner info
   - Receipt date, carrier, tracking

2. **Items Received**
   - Items table with quality status
   - Serial/Lot numbers
   - Locations
   - Acceptance/rejection details

3. **Quality Inspection**
   - Inspection checklist
   - Inspector name
   - Inspection date
   - Results
   - Photos/attachments

4. **Actions History**
   - All actions on this receipt
   - Approvals, rejections
   - Status changes

**Actions:**
- Approve Receipt (if pending)
- Reject Receipt (if pending)
- Complete Receipt (if approved)
- Print GRN
- Create Purchase Return
- Export PDF

---

### **Phase 5: Integration & Enhancement** (Priority: MEDIUM - 3-5 days)

#### 5.1 Business Partner Integration
**No new pages needed** - Already exists at `/procurement/business-partners`

**Enhancements:**
- Add "Purchase Orders" tab to business partner detail page
- Add "Purchase History" section
- Add "Item Catalog/Pricing" tab
- Link from PO to business partner detail

#### 5.2 Inventory Integration
- Link from PO items to inventory item detail
- Show available stock when creating PO
- Update stock on receipt
- Update item costs (Last Purchase Cost, Last Purchase Date)

#### 5.3 Budget Integration
- Budget validation on PR creation
- Budget commitment on PO approval
- Budget utilization on receipt completion
- Budget alerts and warnings

---

### **Phase 6: Reports & Analytics** (Priority: LOW - 3-5 days)

#### 6.1 Purchasing Reports
**Location:** `frontend/src/app/reports/purchasing/`

**Reports:**
1. Purchase Requisition Report
2. Purchase Order Report
3. Business Partner Performance Report
4. Spend Analysis by Business Partner
5. Receipt/GRN Report
6. Outstanding POs Report
7. Budget vs Actual Report
8. Purchase Price Variance Report

---

## Estimated Implementation Timeline

| Phase | Component | Estimated Time | Priority |
|-------|-----------|----------------|----------|
| **0** | **Backend Migration** | **2-3 days** | **CRITICAL** |
| 0.1 | Update Entities | 0.5 days | CRITICAL |
| 0.2 | Create Migration | 0.5 days | CRITICAL |
| 0.3 | Update DTOs | 0.5 days | CRITICAL |
| 0.4 | Update Controllers | 0.5 days | CRITICAL |
| 0.5 | Update Repositories | 0.5 days | CRITICAL |
| 0.6 | Testing | 0.5 days | CRITICAL |
| **1** | **Frontend Service** | **2-3 days** | **HIGH** |
| 1.1 | Create purchasingService.ts | 2-3 days | HIGH |
| **2** | **Purchase Requisitions** | **5-8 days** | **HIGH** |
| 2.1 | PR List Page | 1-2 days | HIGH |
| 2.2 | Create PR Page | 2-3 days | HIGH |
| 2.3 | View/Edit PR Page | 2-3 days | HIGH |
| **3** | **Purchase Orders** | **7-11 days** | **HIGH** |
| 3.1 | PO List Page | 1-2 days | HIGH |
| 3.2 | Create PO Page | 3-4 days | HIGH |
| 3.3 | View/Edit PO Page | 3-4 days | HIGH |
| 3.4 | Receive PO Dialog | 2-3 days | HIGH |
| **4** | **Enhanced GRN** | **4-6 days** | **MEDIUM** |
| 4.1 | Redesign GRN Page | 2-3 days | MEDIUM |
| 4.2 | GRN Detail Page | 2-3 days | MEDIUM |
| **5** | **Integration** | **3-5 days** | **MEDIUM** |
| 5.1 | Business Partner Integration | 1-2 days | MEDIUM |
| 5.2 | Inventory Integration | 1-2 days | MEDIUM |
| 5.3 | Budget Integration | 1-2 days | MEDIUM |
| **6** | **Reports** | **3-5 days** | **LOW** |
| 6.1 | Purchasing Reports | 3-5 days | LOW |

**Total Estimated Time: 26-41 days**

---

## Technical Implementation Details

### **Workflow Integration**

#### Purchase Requisition Workflow
```
Draft → Submit → Pending Approval → Approved/Rejected
                                   ↓
                            Convert to PO
```

#### Purchase Order Workflow
```
Draft → Submit → Pending Approval → Approved → Sent → Acknowledged
                                                      ↓
                                            Partially Received → Received
```

#### Receipt/GRN Workflow
```
Received → Pending Inspection → Inspection In Progress
                              ↓
                    Approved/Partially Approved/Rejected → Completed
```

### **Key Integrations**

1. **BusinessPartner Module** ✅
   - Business partner selection (filtered by type = 'Supplier' or 'Both')
   - Blacklist enforcement
   - Performance tracking
   - Contact information
   - Payment terms

2. **Inventory Module**
   - Item selection
   - Stock updates on receipt
   - Valuation updates
   - Last purchase cost/date tracking

3. **Budget Module**
   - Budget validation on PR/PO creation
   - Budget commitment on PO approval
   - Budget utilization on receipt

4. **Approval Module**
   - Multi-level approvals
   - Approval routing
   - Notification triggers

---

## Dependencies & Prerequisites

### ✅ Already Complete
1. ✅ BusinessPartner entity and full management system
2. ✅ BusinessPartner frontend pages and service
3. ✅ Authentication & Authorization
4. ✅ Inventory module (for item selection)
5. ✅ Budget module (for budget tracking)

### ⚠️ Needs Migration
1. ⚠️ PurchaseOrder entity (Supplier → BusinessPartner)
2. ⚠️ PurchaseRequisition entity (Supplier → BusinessPartner)
3. ⚠️ Controllers and repositories
4. ⚠️ DTOs

### 📋 Needs Implementation
1. ❌ Frontend purchasing service
2. ❌ Purchase Requisition pages
3. ❌ Purchase Order pages
4. ❌ Enhanced GRN pages

---

## Migration Strategy

### Data Migration Plan
```sql
-- Step 1: Add BusinessPartnerId column to PurchaseOrders
ALTER TABLE PurchaseOrders ADD BusinessPartnerId UNIQUEIDENTIFIER NULL;

-- Step 2: Migrate existing Supplier data to BusinessPartner
-- (If there are existing Suppliers, create corresponding BusinessPartners)
INSERT INTO BusinessPartners (Id, PartnerCode, PartnerName, PartnerType, ...)
SELECT 
    NEWID(),
    SupplierCode,
    Name,
    'Supplier',
    ...
FROM Suppliers
WHERE NOT EXISTS (SELECT 1 FROM BusinessPartners WHERE PartnerCode = Suppliers.SupplierCode);

-- Step 3: Update PurchaseOrders to reference BusinessPartner
UPDATE po
SET po.BusinessPartnerId = bp.Id
FROM PurchaseOrders po
INNER JOIN Suppliers s ON po.SupplierId = s.Id
INNER JOIN BusinessPartners bp ON s.SupplierCode = bp.PartnerCode;

-- Step 4: Make BusinessPartnerId NOT NULL
ALTER TABLE PurchaseOrders ALTER COLUMN BusinessPartnerId UNIQUEIDENTIFIER NOT NULL;

-- Step 5: Add foreign key constraint
ALTER TABLE PurchaseOrders 
ADD CONSTRAINT FK_PurchaseOrders_BusinessPartner 
FOREIGN KEY (BusinessPartnerId) REFERENCES BusinessPartners(Id);

-- Step 6: Drop old Supplier foreign key and column
ALTER TABLE PurchaseOrders DROP CONSTRAINT FK_PurchaseOrders_Supplier;
ALTER TABLE PurchaseOrders DROP COLUMN SupplierId;

-- Repeat similar steps for PurchaseRequisitions
```

---

## Testing Strategy

### Unit Tests
- Service methods
- DTO validation
- Calculation logic
- Business partner filtering

### Integration Tests
- API endpoints
- Database operations
- Workflow transitions
- Business partner blacklist enforcement

### E2E Tests
- Complete PR → PO → Receipt flow
- Approval workflows
- Budget integration
- Business partner selection and validation

---

## Next Steps

1. **Review & Confirm** this implementation plan
2. **Confirm migration strategy** for Supplier → BusinessPartner
3. **Phase 0:** Execute backend migration (2-3 days)
4. **Phase 1:** Create purchasing service (2-3 days)
5. **Phase 2:** Implement Purchase Requisitions (5-8 days)
6. **Phase 3:** Implement Purchase Orders (7-11 days)
7. **Phase 4:** Enhance GRN pages (4-6 days)
8. **Phase 5:** Integration work (3-5 days)
9. **Phase 6:** Reports (3-5 days)

---

## Important Notes

### ✅ Advantages of Using BusinessPartner
- **Already implemented** with comprehensive features
- **Unified system** - no duplication
- **Rich functionality** - categories, licenses, documents, performance tracking
- **External portal** - suppliers can self-register
- **Approval workflow** - built-in registration approval
- **Blacklist management** - already functional
- **Frontend complete** - full UI already exists

### ⚠️ Migration Considerations
- Existing PurchaseOrders (if any) need data migration
- Update all references from Supplier to BusinessPartner
- Test blacklist enforcement
- Verify performance tracking integration
- Update any reports or analytics

### 🎯 Business Partner Filtering
When selecting business partners for purchasing:
```typescript
// Filter to show only suppliers
businessPartners.filter(bp => 
  bp.partnerType === 'Supplier' || bp.partnerType === 'Both'
)
```

---

**Document Version:** 2.0  
**Last Updated:** 2026-01-26  
**Status:** Ready for Review & Confirmation
