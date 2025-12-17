# TENDER MANAGEMENT - FILES TO CREATE

**Date:** 2025-11-30

This document lists all files that need to be created for the Tender Management System implementation.

---

## PHASE 1: BACKEND IMPLEMENTATION

### 1. Additional Entities (if needed)

**Location:** `src/ErpSystem.Core/Entities/Procurement/`

- [ ] `TenderEntities.cs` - Add missing entities:
  - `TenderFee`
  - `TenderPayment`
  - `TenderEvaluator`
  - `TenderEvaluation`
  - `TenderInterview`
  - `TenderClarification`
  - `TenderRevision`
  - `TenderTemplate`
  - `TenderViewLog`

---

### 2. DTOs

**Location:** `src/ErpSystem.Core/DTOs/Procurement/`

- [ ] `TenderDTOs.cs` (New file, ~500-700 lines)
  - `TenderDto`
  - `TenderDetailDto`
  - `TenderSummaryDto`
  - `CreateTenderDto`
  - `UpdateTenderDto`
  - `PublishTenderDto`
  - `TenderItemDto`
  - `CreateTenderItemDto`
  - `TenderDocumentDto`
  - `UploadTenderDocumentDto`
  - `TenderInvitationDto`
  - `InviteTenderersDto`

- [ ] `TenderBidDTOs.cs` (New file, ~400-600 lines)
  - `TenderBidDto`
  - `TenderBidDetailDto`
  - `TenderBidSummaryDto`
  - `CreateTenderBidDto`
  - `UpdateTenderBidDto`
  - `SubmitTenderBidDto`
  - `TenderBidItemDto`
  - `CreateTenderBidItemDto`
  - `TenderBidDocumentDto`
  - `UploadBidDocumentDto`

- [ ] `TenderEvaluationDTOs.cs` (New file, ~300-400 lines)
  - `TenderEvaluationDto`
  - `CreateEvaluationDto`
  - `EvaluationScorecardDto`
  - `ConsolidatedEvaluationDto`
  - `EvaluationReportDto`

- [ ] `TenderAwardDTOs.cs` (New file, ~200-300 lines)
  - `TenderAwardDto`
  - `CreateAwardDto`
  - `AwardRecommendationDto`
  - `AwardNotificationDto`

---

### 3. Repository Interfaces

**Location:** `src/ErpSystem.Core/Interfaces/Procurement/`

- [ ] `ITenderRepositories.cs` (New file, ~200-300 lines)
  - `ITenderRepository`
  - `ITenderItemRepository`
  - `ITenderDocumentRepository`
  - `ITenderInvitationRepository`
  - `ITenderBidRepository`
  - `ITenderBidItemRepository`
  - `ITenderBidDocumentRepository`
  - `ITenderEvaluationRepository`
  - `ITenderAwardRepository`

---

### 4. Repository Implementations

**Location:** `src/ErpSystem.Data/Repositories/Procurement/`

- [ ] `TenderRepositories.cs` (New file, ~600-800 lines)
  - `TenderRepository`
  - `TenderItemRepository`
  - `TenderDocumentRepository`
  - `TenderInvitationRepository`
  - `TenderBidRepository`
  - `TenderBidItemRepository`
  - `TenderBidDocumentRepository`
  - `TenderEvaluationRepository`
  - `TenderAwardRepository`

---

### 5. Service Interfaces

**Location:** `src/ErpSystem.Core/Interfaces/Procurement/`

- [ ] `ITenderServices.cs` (New file, ~150-200 lines)
  - `ITenderService`
  - `ITenderBidService`
  - `ITenderEvaluationService`
  - `ITenderAwardService`
  - `ITenderNotificationService`
  - `ITenderTemplateService`

---

### 6. Service Implementations

**Location:** `src/ErpSystem.Core/Services/Procurement/`

- [ ] `TenderService.cs` (New file, ~500-700 lines)
  - Create, update, delete tenders
  - Publish/unpublish tenders
  - Manage tender items
  - Manage tender documents
  - Invite tenderers
  - Generate tender numbers

- [ ] `TenderBidService.cs` (New file, ~400-600 lines)
  - Create, update bids
  - Submit bids
  - Validate bid completeness
  - Enforce submission deadlines
  - Manage bid documents
  - Generate bid numbers
  - Withdraw bids

- [ ] `TenderEvaluationService.cs` (New file, ~400-500 lines)
  - Assign evaluators
  - Create evaluations
  - Calculate scores
  - Consolidate evaluations
  - Generate evaluation reports
  - Rank bids

- [ ] `TenderAwardService.cs` (New file, ~300-400 lines)
  - Create award recommendations
  - Process awards
  - Generate award letters
  - Notify winners/losers
  - Link to purchase orders

- [ ] `TenderNotificationService.cs` (New file, ~300-400 lines)
  - Tender publication notifications
  - Bid submission confirmations
  - Award notifications
  - Deadline reminders
  - Clarification notifications

- [ ] `TenderTemplateService.cs` (New file, ~200-300 lines)
  - Create/manage templates
  - Apply templates
  - Template categories

---

### 7. Controllers

**Location:** `src/ErpSystem.Api/Controllers/Procurement/`

- [ ] `TendersController.cs` (New file, ~500-700 lines)
  - GET /api/procurement/tenders
  - GET /api/procurement/tenders/{id}
  - POST /api/procurement/tenders
  - PUT /api/procurement/tenders/{id}
  - DELETE /api/procurement/tenders/{id}
  - POST /api/procurement/tenders/{id}/publish
  - POST /api/procurement/tenders/{id}/close
  - POST /api/procurement/tenders/{id}/invite
  - GET /api/procurement/tenders/{id}/bids
  - GET /api/procurement/tenders/published (for external portal)

- [ ] `TenderBidsController.cs` (New file, ~400-500 lines)
  - GET /api/procurement/tender-bids
  - GET /api/procurement/tender-bids/{id}
  - POST /api/procurement/tender-bids
  - PUT /api/procurement/tender-bids/{id}
  - POST /api/procurement/tender-bids/{id}/submit
  - POST /api/procurement/tender-bids/{id}/withdraw
  - GET /api/procurement/tender-bids/my-bids (for external portal)

- [ ] `TenderEvaluationsController.cs` (New file, ~300-400 lines)
  - GET /api/procurement/tender-evaluations
  - POST /api/procurement/tender-evaluations
  - PUT /api/procurement/tender-evaluations/{id}
  - GET /api/procurement/tenders/{id}/evaluations
  - POST /api/procurement/tenders/{id}/consolidate
  - GET /api/procurement/tenders/{id}/evaluation-report

- [ ] `TenderAwardsController.cs` (New file, ~200-300 lines)
  - GET /api/procurement/tender-awards
  - POST /api/procurement/tender-awards
  - POST /api/procurement/tender-awards/{id}/approve
  - POST /api/procurement/tender-awards/{id}/notify

- [ ] `TenderTemplatesController.cs` (New file, ~200-300 lines)
  - GET /api/procurement/tender-templates
  - POST /api/procurement/tender-templates
  - PUT /api/procurement/tender-templates/{id}
  - DELETE /api/procurement/tender-templates/{id}

---

### 8. Dependency Injection Registration

**Location:** `src/ErpSystem.Api/Extensions/`

- [ ] Update `ServiceCollectionExtensions.cs`
  - Register all tender repositories
  - Register all tender services

---

## PHASE 2: FRONTEND - INTERNAL

### Location: `frontend/src/app/procurement/tenders/`

- [ ] `page.tsx` - Tender list/dashboard (~300-400 lines)
- [ ] `create/page.tsx` - Tender creation wizard (~500-700 lines)
- [ ] `[id]/page.tsx` - Tender detail view (~400-600 lines)
- [ ] `[id]/edit/page.tsx` - Tender edit form (~400-500 lines)
- [ ] `[id]/evaluate/page.tsx` - Bid evaluation interface (~500-700 lines)
- [ ] `[id]/award/page.tsx` - Award management (~300-400 lines)

### Location: `frontend/src/app/procurement/bids/`

- [ ] `page.tsx` - All bids list (~300-400 lines)
- [ ] `[id]/page.tsx` - Bid detail view (~300-400 lines)

### Location: `frontend/src/app/administration/procurement/`

- [ ] `tender-templates/page.tsx` - Template management (~300-400 lines)
- [ ] `tender-templates/[id]/page.tsx` - Template editor (~400-500 lines)
- [ ] `evaluation-criteria/page.tsx` - Criteria configuration (~300-400 lines)
- [ ] `tender-settings/page.tsx` - General tender settings (~200-300 lines)

### Location: `frontend/src/components/procurement/tenders/`

- [ ] `TenderCreationWizard.tsx` (~400-500 lines)
- [ ] `TenderItemsForm.tsx` (~200-300 lines)
- [ ] `TenderDocumentsUpload.tsx` (~200-300 lines)
- [ ] `TenderInvitationDialog.tsx` (~200-300 lines)
- [ ] `BidEvaluationScorecard.tsx` (~300-400 lines)
- [ ] `BidComparisonTable.tsx` (~300-400 lines)
- [ ] `AwardRecommendationForm.tsx` (~200-300 lines)
- [ ] `TenderStatusBadge.tsx` (~50-100 lines)
- [ ] `TenderTimeline.tsx` (~200-300 lines)

### Location: `frontend/src/services/`

- [ ] `tenderService.ts` (~400-600 lines)
- [ ] `tenderBidService.ts` (~300-400 lines)
- [ ] `tenderEvaluationService.ts` (~200-300 lines)

---

## PHASE 3: FRONTEND - EXTERNAL PORTAL

### Location: `frontend/src/app/external-portal/tenders/`

- [ ] `page.tsx` - Published tender listing (~300-400 lines)
- [ ] `[id]/page.tsx` - Tender detail view (~400-500 lines)
- [ ] `[id]/submit/page.tsx` - Bid submission wizard (~500-700 lines)
- [ ] `[id]/clarifications/page.tsx` - Q&A interface (~300-400 lines)

### Location: `frontend/src/app/external-portal/my-bids/`

- [ ] `page.tsx` - My bids dashboard (~300-400 lines)
- [ ] `[id]/page.tsx` - Bid detail view (~300-400 lines)

### Location: `frontend/src/components/external-portal/tenders/`

- [ ] `TenderCard.tsx` (~100-150 lines)
- [ ] `TenderDetailView.tsx` (~300-400 lines)
- [ ] `BidSubmissionWizard.tsx` (~500-700 lines)
- [ ] `BidItemsForm.tsx` (~200-300 lines)
- [ ] `BidDocumentsUpload.tsx` (~200-300 lines)
- [ ] `ClarificationForm.tsx` (~150-200 lines)
- [ ] `MyBidsTable.tsx` (~200-300 lines)

### Update Existing Files - External Portal

- [ ] `frontend/src/components/external-portal/external-sidebar.tsx`
  - Add "Tenders" menu item
  - Add "My Bids" menu item

- [ ] `frontend/src/app/external-portal/page.tsx`
  - Add "Browse Tenders" quick action card
  - Add "My Bids" quick action card
  - Add tender-related dashboard widgets

### Update Existing Files - Procurement Module

- [ ] `frontend/src/app/procurement/layout.tsx`
  - Add "Tenders" menu section with submenu
  - Add "Bid Management" menu section with submenu

- [ ] `frontend/src/app/procurement/page.tsx`
  - Add tender-related dashboard widgets
  - Add active tenders count
  - Add pending evaluation count
  - Add total bids count
  - Add awarded this month count

### Update Existing Files - Administration

- [ ] `frontend/src/app/administration/layout.tsx`
  - Add "Tender Templates" menu item under Procurement
  - Add "Evaluation Criteria" menu item under Procurement
  - Add "Tender Settings" menu item under Procurement

---

## TOTAL FILE COUNT

| Category | New Files | Updated Files | Total |
|----------|-----------|---------------|-------|
| Backend Entities | 1 (update) | 0 | 1 |
| Backend DTOs | 4 | 0 | 4 |
| Backend Repositories | 2 | 0 | 2 |
| Backend Services | 6 | 0 | 6 |
| Backend Controllers | 5 | 0 | 5 |
| Backend Config | 0 | 1 | 1 |
| Frontend Internal Pages | 11 | 0 | 11 |
| Frontend Internal Components | 9 | 0 | 9 |
| Frontend External Pages | 6 | 0 | 6 |
| Frontend External Components | 7 | 0 | 7 |
| Frontend Services | 3 | 0 | 3 |
| Frontend Layouts/Menus | 0 | 5 | 5 |
| **TOTAL** | **54** | **6** | **60** |

---

## ESTIMATED LINES OF CODE

| Layer | Estimated LOC |
|-------|---------------|
| Backend | ~8,000-12,000 |
| Frontend Internal | ~5,000-7,000 |
| Frontend External | ~4,000-6,000 |
| **TOTAL** | **~17,000-25,000** |


