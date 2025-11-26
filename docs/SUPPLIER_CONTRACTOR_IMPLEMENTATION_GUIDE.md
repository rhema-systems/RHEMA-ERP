# Supplier & Contractor Management - Implementation Guide

## Quick Start Guide

This guide provides step-by-step instructions for implementing the unified Supplier & Contractor Management System.

---

## Prerequisites

1. Review `SUPPLIER_CONTRACTOR_UNIFIED_SYSTEM.md` for complete system design
2. Ensure database access and migration tools are ready
3. Backend (.NET) and Frontend (Next.js/React) development environments set up

---

## Implementation Checklist

### ✅ Phase 1: Database & Backend Core (Week 1-2)

#### Step 1.1: Create Database Migration

```bash
# Navigate to the project root
cd src/ErpSystem.Data

# Create migration
dotnet ef migrations add AddBusinessPartnerManagement --project ErpSystem.Data.csproj --startup-project ../ErpSystem.Api/ErpSystem.Api.csproj

# Apply migration
dotnet ef database update --project ErpSystem.Data.csproj --startup-project ../ErpSystem.Api/ErpSystem.Api.csproj
```

**Tables to Create:**
- BusinessPartners
- PartnerCategories
- BusinessPartnerCategories
- ContractorSpecializations
- BusinessPartnerSpecializations
- LicenseTypes
- BusinessPartnerLicenses
- BusinessPartnerContacts
- BusinessPartnerDocuments
- BusinessPartnerFinancials

#### Step 1.2: Create Entity Models

**Location:** `src/ErpSystem.Core/Entities/Procurement/`

**Files to Create:**
1. `BusinessPartner.cs` - Main entity
2. `PartnerCategory.cs` - Category configuration
3. `ContractorSpecialization.cs` - Specialization configuration
4. `LicenseType.cs` - License type configuration
5. `BusinessPartnerLicense.cs` - License records
6. `BusinessPartnerContact.cs` - Contact records
7. `BusinessPartnerDocument.cs` - Document records
8. `BusinessPartnerFinancial.cs` - Financial records

#### Step 1.3: Create DTOs

**Location:** `src/ErpSystem.Core/DTOs/Procurement/`

**Files to Create:**
1. `BusinessPartnerDto.cs` - Main DTO
2. `CreateBusinessPartnerDto.cs` - Create request
3. `UpdateBusinessPartnerDto.cs` - Update request
4. `BusinessPartnerDetailDto.cs` - Detailed view
5. `PartnerCategoryDto.cs`
6. `ContractorSpecializationDto.cs`
7. `LicenseTypeDto.cs`
8. `BusinessPartnerLicenseDto.cs`
9. `BusinessPartnerDocumentDto.cs`
10. `BusinessPartnerFinancialDto.cs`

#### Step 1.4: Create Repositories

**Location:** `src/ErpSystem.Data/Repositories/Procurement/`

**Files to Create:**
1. `BusinessPartnerRepository.cs`
2. `PartnerCategoryRepository.cs`
3. `ContractorSpecializationRepository.cs`
4. `LicenseTypeRepository.cs`

**Interface Location:** `src/ErpSystem.Core/Interfaces/Procurement/`

#### Step 1.5: Create Services

**Location:** `src/ErpSystem.Core/Services/Procurement/`

**Files to Create:**
1. `BusinessPartnerService.cs` - Main business logic
2. `PartnerVerificationService.cs` - Verification workflows
3. `PartnerPerformanceService.cs` - Performance tracking
4. `PartnerBlacklistService.cs` - Blacklist management

#### Step 1.6: Create API Controllers

**Location:** `src/ErpSystem.Api/Controllers/Procurement/`

**Files to Create:**
1. `BusinessPartnersController.cs` - Main CRUD
2. `PartnerCategoriesController.cs` - Admin config
3. `ContractorSpecializationsController.cs` - Admin config
4. `LicenseTypesController.cs` - Admin config
5. `PartnerRegistrationController.cs` - External portal
6. `PartnerVerificationController.cs` - Internal verification

---

### ✅ Phase 2: Admin Configuration UI (Week 3)

#### Step 2.1: Create Admin Pages

**Location:** `frontend/src/app/administration/procurement/`

**Pages to Create:**
1. `partner-categories/page.tsx` - Category management
2. `contractor-specializations/page.tsx` - Specialization management
3. `license-types/page.tsx` - License type management
4. `approval-workflows/page.tsx` - Workflow configuration

#### Step 2.2: Create Admin Components

**Location:** `frontend/src/components/procurement/admin/`

**Components to Create:**
1. `PartnerCategoryManagement.tsx`
2. `ContractorSpecializationManagement.tsx`
3. `LicenseTypeManagement.tsx`
4. `ApprovalWorkflowManagement.tsx`

#### Step 2.3: Create Services

**Location:** `frontend/src/services/`

**Files to Create:**
1. `businessPartnerService.ts` - Main API service
2. `partnerConfigService.ts` - Admin configuration service

---

### ✅ Phase 3: External Registration Portal (Week 4-5)

#### Step 3.1: Create Public Registration Pages

**Location:** `frontend/src/app/register/partner/`

**Pages to Create:**
1. `page.tsx` - Main registration wizard
2. `status/[id]/page.tsx` - Registration status tracking

#### Step 3.2: Create Registration Wizard Components

**Location:** `frontend/src/components/procurement/registration/`

**Components to Create:**
1. `RegistrationWizard.tsx` - Main wizard container
2. `Step1TypeSelection.tsx`
3. `Step2BasicInfo.tsx`
4. `Step3Address.tsx`
5. `Step4Banking.tsx`
6. `Step5Categories.tsx`
7. `Step6Licenses.tsx`
8. `Step7Financials.tsx`
9. `Step8Documents.tsx`
10. `Step9Review.tsx`

#### Step 3.3: Create Document Upload Component

**Location:** `frontend/src/components/procurement/registration/`

**Component:** `DocumentUpload.tsx`

---

### ✅ Phase 4: Internal Management UI (Week 6-7)

#### Step 4.1: Create Management Pages

**Location:** `frontend/src/app/procurement/`

**Pages to Create:**
1. `business-partners/page.tsx` - Main listing
2. `business-partners/[id]/page.tsx` - Detail view
3. `business-partners/pending-approval/page.tsx` - Approval queue

#### Step 4.2: Create Management Components

**Location:** `frontend/src/components/procurement/`

**Components to Create:**
1. `BusinessPartnerManagement.tsx` - Main listing
2. `BusinessPartnerDetail.tsx` - Detail view
3. `PartnerVerification.tsx` - Verification interface
4. `PartnerApproval.tsx` - Approval interface
5. `DocumentVerification.tsx` - Document verification

---

## Configuration Steps

### 1. Update Sidebar Navigation

**File:** `frontend/src/components/layout/sidebar.tsx`

Add under Procurement section:
```typescript
{
  title: 'Business Partners',
  href: '/procurement/business-partners',
  icon: Users,
},
{
  title: 'Pending Approvals',
  href: '/procurement/business-partners/pending-approval',
  icon: CheckCircle,
},
```

Add under Administration → Procurement:
```typescript
{
  title: 'Partner Categories',
  href: '/administration/procurement/partner-categories',
  icon: FolderTree,
},
{
  title: 'Contractor Specializations',
  href: '/administration/procurement/contractor-specializations',
  icon: Wrench,
},
{
  title: 'License Types',
  href: '/administration/procurement/license-types',
  icon: FileCheck,
},
```

### 2. Configure Dependency Injection

**File:** `src/ErpSystem.Api/Program.cs`

Add service registrations:
```csharp
// Business Partner Services
builder.Services.AddScoped<IBusinessPartnerService, BusinessPartnerService>();
builder.Services.AddScoped<IPartnerVerificationService, PartnerVerificationService>();
builder.Services.AddScoped<IPartnerPerformanceService, PartnerPerformanceService>();
builder.Services.AddScoped<IPartnerBlacklistService, PartnerBlacklistService>();

// Repositories
builder.Services.AddScoped<IBusinessPartnerRepository, BusinessPartnerRepository>();
builder.Services.AddScoped<IPartnerCategoryRepository, PartnerCategoryRepository>();
builder.Services.AddScoped<IContractorSpecializationRepository, ContractorSpecializationRepository>();
builder.Services.AddScoped<ILicenseTypeRepository, LicenseTypeRepository>();
```


