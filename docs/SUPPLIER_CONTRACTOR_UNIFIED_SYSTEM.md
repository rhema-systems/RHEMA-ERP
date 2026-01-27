# Unified Supplier & Contractor Management System

## Overview
Comprehensive business partner management system under the Procurement module that handles both suppliers and contractors through a unified interface with specialized extensions.

---

## System Architecture

### Core Concept: Business Partner Entity
A unified entity that represents all external business relationships (suppliers, contractors, or both), with specialized fields and workflows based on partner type.

```
BusinessPartner (Base Entity)
├── Core Information (shared by all)
├── Type Classification (Supplier/Contractor/Both)
├── Specialized Data (type-specific fields)
├── Financial Information
├── Performance Metrics
├── Approval Workflow
└── Risk & Compliance
```

---

## Database Schema

### 1. BusinessPartners (Main Table)

```sql
CREATE TABLE BusinessPartners (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    
    -- Core Identification
    PartnerCode NVARCHAR(50) NOT NULL UNIQUE,
    PartnerName NVARCHAR(200) NOT NULL,
    PartnerType NVARCHAR(20) NOT NULL, -- 'Supplier', 'Contractor', 'Both'
    
    -- Legal & Registration
    LegalName NVARCHAR(200),
    BusinessRegistrationNumber NVARCHAR(100),
    TaxIdentificationNumber NVARCHAR(100),
    VATNumber NVARCHAR(100),
    RegistrationDate DATE,
    IncorporationDate DATE,
    
    -- Contact Information
    PrimaryContactName NVARCHAR(100),
    PrimaryContactTitle NVARCHAR(100),
    PrimaryEmail NVARCHAR(100),
    PrimaryPhone NVARCHAR(50),
    SecondaryPhone NVARCHAR(50),
    Website NVARCHAR(200),
    
    -- Address Information
    PhysicalAddress NVARCHAR(500),
    PhysicalCity NVARCHAR(100),
    PhysicalState NVARCHAR(100),
    PhysicalCountry NVARCHAR(100),
    PhysicalPostalCode NVARCHAR(20),
    
    MailingAddress NVARCHAR(500),
    MailingCity NVARCHAR(100),
    MailingState NVARCHAR(100),
    MailingCountry NVARCHAR(100),
    MailingPostalCode NVARCHAR(20),
    
    -- Banking Information
    BankName NVARCHAR(200),
    BankAccountNumber NVARCHAR(100),
    BankAccountName NVARCHAR(200),
    BankBranch NVARCHAR(200),
    BankSwiftCode NVARCHAR(50),
    BankIBAN NVARCHAR(100),
    
    -- Classification
    IndustryClassification NVARCHAR(100),
    CompanySize NVARCHAR(50), -- 'Small', 'Medium', 'Large', 'Enterprise'
    GeographicCoverage NVARCHAR(200),
    
    -- Status & Approval
    RegistrationStatus NVARCHAR(50) NOT NULL DEFAULT 'Pending', 
    -- 'Pending', 'UnderReview', 'Approved', 'Rejected', 'Suspended', 'Blacklisted'
    ApprovalStatus NVARCHAR(50),
    ApprovedBy NVARCHAR(450), -- User ID
    ApprovedDate DATETIME,
    RejectionReason NVARCHAR(1000),
    
    -- Performance & Risk
    PerformanceRating DECIMAL(3,2), -- 0.00 to 5.00
    RiskLevel NVARCHAR(20), -- 'Low', 'Medium', 'High', 'Critical'
    IsPreferred BIT DEFAULT 0,
    IsActive BIT DEFAULT 1,
    IsBlacklisted BIT DEFAULT 0,
    BlacklistReason NVARCHAR(1000),
    BlacklistDate DATETIME,
    BlacklistExpiryDate DATETIME,
    
    -- Financial Health
    AnnualTurnover DECIMAL(18,2),
    CreditRating NVARCHAR(20),
    InsuranceCoverage DECIMAL(18,2),
    
    -- Metadata
    Notes NVARCHAR(MAX),
    CreatedBy NVARCHAR(450),
    CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy NVARCHAR(450),
    UpdatedAt DATETIME,
    IsDeleted BIT DEFAULT 0,
    DeletedAt DATETIME,
    DeletedBy NVARCHAR(450),
    
    CONSTRAINT FK_BusinessPartners_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
    CONSTRAINT FK_BusinessPartners_ApprovedBy FOREIGN KEY (ApprovedBy) REFERENCES AspNetUsers(Id),
    INDEX IX_BusinessPartners_TenantId (TenantId),
    INDEX IX_BusinessPartners_PartnerType (PartnerType),
    INDEX IX_BusinessPartners_RegistrationStatus (RegistrationStatus),
    INDEX IX_BusinessPartners_IsActive (IsActive)
);
```

### 2. PartnerCategories (Configuration Table)

```sql
CREATE TABLE PartnerCategories (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    
    CategoryCode NVARCHAR(50) NOT NULL,
    CategoryName NVARCHAR(200) NOT NULL,
    CategoryType NVARCHAR(20) NOT NULL, -- 'Supplier', 'Contractor', 'Both'
    Description NVARCHAR(1000),
    ParentCategoryId UNIQUEIDENTIFIER, -- For hierarchical categories
    
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_PartnerCategories_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
    CONSTRAINT FK_PartnerCategories_Parent FOREIGN KEY (ParentCategoryId) REFERENCES PartnerCategories(Id)
);
```

### 3. BusinessPartnerCategories (Many-to-Many)

```sql
CREATE TABLE BusinessPartnerCategories (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    BusinessPartnerId UNIQUEIDENTIFIER NOT NULL,
    CategoryId UNIQUEIDENTIFIER NOT NULL,
    IsPrimary BIT DEFAULT 0,
    
    CONSTRAINT FK_BPCategories_Partner FOREIGN KEY (BusinessPartnerId) REFERENCES BusinessPartners(Id) ON DELETE CASCADE,
    CONSTRAINT FK_BPCategories_Category FOREIGN KEY (CategoryId) REFERENCES PartnerCategories(Id)
);
```

### 4. ContractorSpecializations (Contractor-Specific)

```sql
CREATE TABLE ContractorSpecializations (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL,

    SpecializationCode NVARCHAR(50) NOT NULL,
    SpecializationName NVARCHAR(200) NOT NULL,
    Description NVARCHAR(1000),
    RequiresLicense BIT DEFAULT 0,

    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_ContractorSpec_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
```

### 5. BusinessPartnerSpecializations (Many-to-Many for Contractors)

```sql
CREATE TABLE BusinessPartnerSpecializations (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    BusinessPartnerId UNIQUEIDENTIFIER NOT NULL,
    SpecializationId UNIQUEIDENTIFIER NOT NULL,
    YearsOfExperience INT,
    IsPrimary BIT DEFAULT 0,

    CONSTRAINT FK_BPSpec_Partner FOREIGN KEY (BusinessPartnerId) REFERENCES BusinessPartners(Id) ON DELETE CASCADE,
    CONSTRAINT FK_BPSpec_Specialization FOREIGN KEY (SpecializationId) REFERENCES ContractorSpecializations(Id)
);
```

### 6. LicenseTypes (Configuration for Contractor Licenses)

```sql
CREATE TABLE LicenseTypes (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL,

    LicenseCode NVARCHAR(50) NOT NULL,
    LicenseName NVARCHAR(200) NOT NULL,
    Description NVARCHAR(1000),
    IssuingAuthority NVARCHAR(200),
    ValidityPeriodMonths INT,
    IsMandatory BIT DEFAULT 0,

    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_LicenseTypes_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
```

### 7. BusinessPartnerLicenses (Contractor Licenses & Certifications)

```sql
CREATE TABLE BusinessPartnerLicenses (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    BusinessPartnerId UNIQUEIDENTIFIER NOT NULL,
    LicenseTypeId UNIQUEIDENTIFIER NOT NULL,

    LicenseNumber NVARCHAR(100) NOT NULL,
    IssuingAuthority NVARCHAR(200),
    IssueDate DATE,
    ExpiryDate DATE,
    Status NVARCHAR(50) DEFAULT 'Active', -- 'Active', 'Expired', 'Suspended', 'Revoked'

    DocumentPath NVARCHAR(500),
    Notes NVARCHAR(1000),

    CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME,

    CONSTRAINT FK_BPLicenses_Partner FOREIGN KEY (BusinessPartnerId) REFERENCES BusinessPartners(Id) ON DELETE CASCADE,
    CONSTRAINT FK_BPLicenses_LicenseType FOREIGN KEY (LicenseTypeId) REFERENCES LicenseTypes(Id)
);
```

### 8. BusinessPartnerContacts (Multiple Contacts per Partner)

```sql
CREATE TABLE BusinessPartnerContacts (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    BusinessPartnerId UNIQUEIDENTIFIER NOT NULL,

    ContactName NVARCHAR(100) NOT NULL,
    ContactTitle NVARCHAR(100),
    Department NVARCHAR(100),
    Email NVARCHAR(100),
    Phone NVARCHAR(50),
    Mobile NVARCHAR(50),
    IsPrimary BIT DEFAULT 0,

    CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_BPContacts_Partner FOREIGN KEY (BusinessPartnerId) REFERENCES BusinessPartners(Id) ON DELETE CASCADE
);
```

### 9. BusinessPartnerDocuments (Document Management)

```sql
CREATE TABLE BusinessPartnerDocuments (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    BusinessPartnerId UNIQUEIDENTIFIER NOT NULL,

    DocumentType NVARCHAR(100) NOT NULL,
    -- 'BusinessRegistration', 'TaxCertificate', 'Insurance', 'BankStatement',
    -- 'License', 'Certification', 'FinancialStatement', 'Other'
    DocumentName NVARCHAR(200) NOT NULL,
    DocumentPath NVARCHAR(500) NOT NULL,
    FileSize BIGINT,
    MimeType NVARCHAR(100),

    IssueDate DATE,
    ExpiryDate DATE,
    IsVerified BIT DEFAULT 0,
    VerifiedBy NVARCHAR(450),
    VerifiedDate DATETIME,

    UploadedBy NVARCHAR(450),
    UploadedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_BPDocuments_Partner FOREIGN KEY (BusinessPartnerId) REFERENCES BusinessPartners(Id) ON DELETE CASCADE,
    CONSTRAINT FK_BPDocuments_VerifiedBy FOREIGN KEY (VerifiedBy) REFERENCES AspNetUsers(Id)
);
```

### 10. BusinessPartnerFinancials (Financial History)

```sql
CREATE TABLE BusinessPartnerFinancials (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    BusinessPartnerId UNIQUEIDENTIFIER NOT NULL,

    FiscalYear INT NOT NULL,
    AnnualRevenue DECIMAL(18,2),
    NetProfit DECIMAL(18,2),
    TotalAssets DECIMAL(18,2),
    TotalLiabilities DECIMAL(18,2),
    CreditRating NVARCHAR(20),

    FinancialStatementPath NVARCHAR(500),
    AuditorName NVARCHAR(200),
    AuditDate DATE,

    CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_BPFinancials_Partner FOREIGN KEY (BusinessPartnerId) REFERENCES BusinessPartners(Id) ON DELETE CASCADE
);
```

---

## External Registration Portal

### Portal Architecture - Extensible UI Design

The external portal UI is designed to support multiple registration modules:
- ✅ **Business Partner Registration** (Supplier/Contractor) - Current Implementation
- 🔜 **Land Management Registration** - Future (separate tables/entities)
- 🔜 **Rent Management Registration** - Future (separate tables/entities)
- 🔜 **Permit Application** - Future (separate tables/entities)
- 🔜 **Other Services** - Future (separate tables/entities)

**Note:** Each registration type will have its own dedicated database tables, entities, services, and business logic. The portal UI provides a unified entry point, but backend systems remain independent.

### Portal Landing Page Structure

```
External Portal Home (/external)
├── Business Partner Registration → /external/register/business-partner
├── Land Management (Future) → /external/register/land-management
├── Rent Management (Future) → /external/register/rent-management
├── Permit Application (Future) → /external/register/permit
└── Track Application Status → /external/status
```

### Business Partner Registration Wizard Flow

**Step 1: Partner Type Selection**
- Select: Supplier / Contractor / Both
- Determines which forms to show

**Step 2: Basic Information**
- Company name, legal name
- Business registration number
- Tax ID, VAT number
- Contact information

**Step 3: Address & Location**
- Physical address
- Mailing address
- Geographic coverage

**Step 4: Banking Information**
- Bank details
- Account information

**Step 5: Categories & Specializations**
- For Suppliers: Product/service categories
- For Contractors: Specializations, capacity

**Step 6: Licenses & Certifications** (Contractors)
- Upload licenses
- Certifications
- Insurance documents

**Step 7: Financial Information**
- Annual turnover
- Financial statements (last 3 years)
- Bank references

**Step 8: Document Upload**
- Business registration certificate
- Tax certificates
- Insurance certificates
- Other supporting documents

**Step 9: Review & Submit**
- Review all entered information
- Accept terms & conditions
- Submit for approval

---

## Internal Admin Configuration

### Admin Setup Pages (Under Administration → Procurement)

1. **Partner Categories**
   - Manage supplier/contractor categories
   - Hierarchical structure support
   - Active/inactive status

2. **Contractor Specializations**
   - Define specialization areas
   - Link to required licenses
   - Set experience requirements

3. **License Types**
   - Define license types
   - Set issuing authorities
   - Configure validity periods
   - Mark mandatory licenses

4. **Certification Types**
   - Quality certifications (ISO, etc.)
   - Safety certifications
   - Industry-specific certifications

5. **Approval Workflows**
   - Define approval stages
   - Set approval authorities
   - Configure notification rules

6. **Risk Assessment Criteria**
   - Define risk factors
   - Set scoring rules
   - Configure risk levels

---

## Internal Management Features

### 1. Partner Verification & Approval

**Verification Checklist:**
- ✅ Business registration verified
- ✅ Tax documents verified
- ✅ Banking information verified
- ✅ Licenses verified (contractors)
- ✅ Financial statements reviewed
- ✅ References checked
- ✅ Site visit completed (if required)

**Approval Workflow:**
1. Initial Review (Procurement Officer)
2. Document Verification (Compliance Officer)
3. Financial Assessment (Finance Manager)
4. Final Approval (Procurement Manager)

### 2. Performance Tracking

**Metrics Tracked:**
- On-time delivery rate (suppliers)
- Quality rating (defect rate)
- Cost competitiveness
- Responsiveness
- Contract compliance
- Innovation contributions

**For Contractors:**
- Schedule adherence
- Quality of work
- Safety compliance
- Cost management
- Change order frequency
- Client satisfaction

### 3. Financial Assessment

**Financial Health Indicators:**
- Annual turnover trend (3-year)
- Profit margins
- Debt-to-equity ratio
- Credit rating
- Payment history
- Insurance coverage adequacy

**Risk Flags:**
- Declining revenue
- Negative cash flow
- High debt levels
- Poor credit rating
- Expired insurance

### 4. Blacklist Management

**Blacklist Reasons:**
- Poor performance
- Contract breach
- Fraud/corruption
- Safety violations
- Quality failures
- Financial insolvency

**Blacklist Process:**
1. Incident report
2. Investigation
3. Show-cause notice
4. Review committee decision
5. Blacklist entry with duration
6. Appeal process

**System Controls:**
- Prevent PO creation with blacklisted partners
- Block project assignment
- Alert on blacklist expiry
- Audit trail of all actions

---

## Integration Points

### 1. Procurement Module
- Supplier selection for purchase orders
- Supplier performance tracking
- Supplier payment terms
- Supplier catalog integration

### 2. Project Management
- Contractor assignment to projects
- Contractor capacity checking
- Contractor performance on projects
- Contractor billing integration

### 3. Finance Module
- Accounts payable integration
- Payment processing
- Financial reporting
- Budget tracking

### 4. Inventory Module
- Supplier item catalog
- Supplier lead times
- Supplier pricing
- Reorder point integration

---

## API Endpoints Structure

### External Portal APIs (Public/Anonymous)

```
# Business Partner Registration
POST   /api/external/business-partner/register/initiate
POST   /api/external/business-partner/register/submit
POST   /api/external/business-partner/register/upload-document
GET    /api/external/business-partner/register/status/{registrationNumber}
GET    /api/external/business-partner/categories
GET    /api/external/business-partner/specializations
GET    /api/external/business-partner/license-types

# Future modules will have their own endpoints:
# /api/external/land-management/...
# /api/external/rent-management/...
# /api/external/permit/...
```

### Internal Management APIs (Authenticated)

```
# Business Partners
GET    /api/procurement/business-partners
GET    /api/procurement/business-partners/{id}
POST   /api/procurement/business-partners
PUT    /api/procurement/business-partners/{id}
DELETE /api/procurement/business-partners/{id}

# Approval & Verification
POST   /api/procurement/business-partners/{id}/verify
POST   /api/procurement/business-partners/{id}/approve
POST   /api/procurement/business-partners/{id}/reject
GET    /api/procurement/business-partners/pending-approval

# Performance
GET    /api/procurement/business-partners/{id}/performance
POST   /api/procurement/business-partners/{id}/performance-rating
GET    /api/procurement/business-partners/{id}/performance-history

# Blacklist
POST   /api/procurement/business-partners/{id}/blacklist
POST   /api/procurement/business-partners/{id}/remove-blacklist
GET    /api/procurement/business-partners/blacklisted

# Documents
GET    /api/procurement/business-partners/{id}/documents
POST   /api/procurement/business-partners/{id}/documents/upload
POST   /api/procurement/business-partners/{id}/documents/{docId}/verify

# Admin Configuration
GET    /api/procurement/admin/partner-categories
POST   /api/procurement/admin/partner-categories
GET    /api/procurement/admin/contractor-specializations
POST   /api/procurement/admin/contractor-specializations
GET    /api/procurement/admin/license-types
POST   /api/procurement/admin/license-types
```

---

## Implementation Phases

### Phase 1: Core Infrastructure (Week 1-2)
- ✅ Database schema creation
- ✅ Entity models and DTOs
- ✅ Repository layer
- ✅ Service layer
- ✅ Basic CRUD APIs

### Phase 2: Admin Configuration (Week 3)
- ✅ Partner categories management
- ✅ Contractor specializations
- ✅ License types
- ✅ Admin UI pages

### Phase 3: External Registration Portal (Week 4-5)
- ✅ Registration wizard UI
- ✅ Document upload functionality
- ✅ Form validation
- ✅ Status tracking

### Phase 4: Internal Management (Week 6-7)
- ✅ Verification workflows
- ✅ Approval processes
- ✅ Document verification
- ✅ Partner listing and search

### Phase 5: Performance & Risk (Week 8)
- ✅ Performance tracking
- ✅ Financial assessment
- ✅ Risk scoring
- ✅ Blacklist management

### Phase 6: Integration (Week 9-10)
- ✅ Procurement integration
- ✅ Project management integration
- ✅ Finance integration
- ✅ Reporting and analytics

---

## Key Benefits of Unified System

1. **Single Source of Truth** - One database for all business partners
2. **Reduced Duplication** - No need to maintain separate systems
3. **Flexible Classification** - Partners can be both suppliers and contractors
4. **Shared Workflows** - Approval, verification, blacklisting work the same way
5. **Comprehensive View** - See all relationships with a partner in one place
6. **Easier Maintenance** - One codebase to maintain and enhance
7. **Better Reporting** - Unified analytics across all partners
8. **Cost Effective** - Lower development and maintenance costs

---

## Next Steps

1. **Review & Approve** this design document
2. **Create database migration** for all tables
3. **Implement backend entities and services**
4. **Build admin configuration pages**
5. **Develop external registration portal**
6. **Implement internal management features**
7. **Integrate with existing modules**
8. **Test and deploy**


