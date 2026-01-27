# Tender Management Flow Redesign

## Overview
This document outlines the redesigned tender management flow based on the Ghana Electronic Procurement System (GHANEPS) model, where business partners can manage sub-users within their organization and follow a structured bidding process.

---

## Current Implementation Analysis

### ✅ What We Have Already

#### 1. **User-BusinessPartner Relationship**
- `BusinessPartner` entity has `UserId` field linking to the main user account
- External users (AuthenticationProvider='Local') are separated from internal users (LDAP)
- External portal layout validates Local authentication
- One-to-one relationship: One User → One BusinessPartner

#### 2. **Tender Viewing & Fees**
- External portal shows available tenders (`/external-portal/tenders`)
- Tender details page shows fees, items, documents, clarifications
- `TenderFee` entity exists with payment tracking
- `TenderPayment` entity tracks payment status (Pending, Completed, Failed, Refunded)

#### 3. **Bid Submission Flow**
- Current flow: `/external-portal/tenders/[id]/submit-bid`
- 3-step wizard: Items → Proposals → Documents
- Auto-save draft functionality
- Document upload for technical/commercial proposals and required documents
- Submit confirmation dialog

#### 4. **Payment Tracking**
- Payment status shown in bids grid (Paid/Pending/Failed/Not Paid badges)
- Payment tab in bid details showing payment information
- Backend service `RecordPaymentAsync` for recording payments

---

## 🔄 Required Changes

### 1. **Multi-User Management per Business Partner**

#### Database Changes Needed:
```csharp
// New entity: BusinessPartnerUser
public class BusinessPartnerUser : TenantEntity
{
    public Guid BusinessPartnerId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } // "Admin", "User", "Viewer"
    public bool IsActive { get; set; }
    public DateTime GrantedAt { get; set; }
    public Guid? GrantedBy { get; set; } // Admin who created this user
    
    // Navigation
    public virtual BusinessPartner BusinessPartner { get; set; }
    public virtual ApplicationUser User { get; set; }
    public virtual ApplicationUser? GrantedByUser { get; set; }
}
```

#### Frontend Changes Needed:
- **New Page**: `/external-portal/user-management` (similar to GHANEPS "Supplier Administration > User Management")
- **Features**:
  - List all users under the business partner
  - Add new user (create ApplicationUser + BusinessPartnerUser link)
  - Edit user details
  - Activate/deactivate users
  - Assign organizational roles (Supplier Admin, Supplier User)

---

### 2. **Tender Association & Task Management**

#### Database Changes Needed:
```csharp
// New entity: TenderAssignment
public class TenderAssignment : TenantEntity
{
    public Guid TenderId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public Guid? AssignedToUserId { get; set; } // null = all users
    public string AssignmentType { get; set; } // "AllUsers", "Self", "SelectedUsers"
    public DateTime AssignedAt { get; set; }
    public Guid AssignedBy { get; set; }
    
    // Navigation
    public virtual Tender Tender { get; set; }
    public virtual BusinessPartner BusinessPartner { get; set; }
    public virtual ApplicationUser? AssignedToUser { get; set; }
    public virtual ApplicationUser AssignedByUser { get; set; }
}
```

#### Frontend Changes Needed:
- **New Page**: `/external-portal/task-list` (similar to GHANEPS "Task List")
- Shows tenders assigned to the current user
- Admin can assign tenders to specific users or all users

---

### 3. **Revised Bid Submission Flow**

#### New Flow (Based on GHANEPS):
1. **View Available Tenders** → `/external-portal/tenders`
2. **View Tender Details** → `/external-portal/tenders/[id]`
3. **Click "Bid" Button** → Start bidding process
4. **Step 1: Type of Association** (NEW)
   - Radio options:
     - "Associate all users of this Supplier with this Tender"
     - "Associate only myself with this Tender"
     - "Pick users from list to associate with this Tender"
5. **Step 2: Accept Agreement** (NEW - Optional per tender)
   - View supplier declaration document
   - Checkbox to accept terms
   - "Accept and Proceed" button
6. **Step 3: Payment** (MODIFIED)
   - Show payment details
   - Payment status check
   - "Proceed to Payment" button (if not paid)
   - Block submission if mandatory fees not paid
7. **Step 4-6: Bid Submission** (EXISTING - Keep current 3-step wizard)
   - Items
   - Proposals
   - Documents

---

### 4. **Payment Flow Integration**

#### Changes Needed:
- Move payment check BEFORE bid submission wizard
- Show payment page similar to GHANEPS screenshot
- Display:
  - Payment Kind: "Tender Submission"
  - Payer: Business Partner Name
  - Target: Tender Title
  - Payment Terms and Method
  - ID, Amount, Currency
  - Created At, Expires At, Remaining Days
  - Status badge
- "Proceed to Payment" button redirects to payment gateway or shows payment instructions
- After payment verification, unlock bid submission

---

## 📋 Implementation Plan

### Phase 1: Multi-User Management (Week 1)
- [ ] Create `BusinessPartnerUser` entity and migration
- [ ] Create backend service for user management
- [ ] Create API endpoints for CRUD operations
- [ ] Create `/external-portal/user-management` page
- [ ] Add "User Management" to external portal sidebar

### Phase 2: Tender Assignment (Week 2)
- [ ] Create `TenderAssignment` entity and migration
- [ ] Create backend service for tender assignments
- [ ] Create API endpoints
- [ ] Create `/external-portal/task-list` page
- [ ] Modify tender list to show assignment options

### Phase 3: Revised Bid Flow - Part 1 (Week 3)
- [ ] Add `AcceptanceDeclarationDocument` field to `Tender` entity
- [ ] Add `RequiresAcceptanceDeclaration` boolean to `Tender`
- [ ] Create new bid initiation page with Type of Association
- [ ] Create acceptance declaration step
- [ ] Create payment verification step

### Phase 4: Revised Bid Flow - Part 2 (Week 4)
- [ ] Integrate payment check before bid submission
- [ ] Block bid submission if fees not paid
- [ ] Update bid submission wizard to be steps 4-6
- [ ] Test end-to-end flow
- [ ] Update documentation

---

## 🎯 Key Differences from Current Implementation

| Aspect | Current | New (GHANEPS Model) |
|--------|---------|---------------------|
| User Management | Single user per business partner | Multiple users per business partner with roles |
| Tender Access | All tenders visible to all users | Task-based assignment system |
| Bid Initiation | Direct to bid form | Association selection first |
| Agreement | No acceptance step | Optional declaration document |
| Payment | Checked after submission | Checked and required BEFORE submission |
| Payment UI | Tab in bid details | Dedicated payment page before bid form |

---

## Next Steps

1. Review and approve this design
2. Prioritize phases based on business needs
3. Create detailed technical specifications for each phase
4. Begin implementation starting with Phase 1

