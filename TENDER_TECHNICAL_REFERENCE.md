# Tender Management System - Technical Reference

## Backend Architecture

### Service Layer

#### TenderEvaluationService
**Location:** `src/ErpSystem.Core/Services/Procurement/TenderEvaluationService.cs`

**Key Methods:**
- `CreateEvaluationAsync(Guid bidId, CreateEvaluationDto dto)` - Create new evaluation
- `UpdateEvaluationAsync(Guid id, UpdateEvaluationDto dto)` - Update draft evaluation
- `SubmitEvaluationAsync(Guid id, SubmitEvaluationDto dto)` - Submit evaluation
- `GetBidEvaluationsAsync(Guid bidId)` - Get all evaluations for a bid
- `GetMyEvaluationsAsync(Guid evaluatorId)` - Get evaluations assigned to user
- `GetBidScorecardAsync(Guid bidId)` - Get consolidated scores for a bid
- `GetConsolidatedEvaluationsAsync(Guid tenderId)` - Get all bid scorecards for tender
- `GetTenderEvaluationReportAsync(Guid tenderId)` - Generate evaluation report

**Score Calculation:**
- Individual evaluator scores: 0-100 per criterion
- Consolidated score: Average of all evaluator scores
- Bid ranking: Ordered by consolidated total score (descending)

#### TenderAwardService
**Location:** `src/ErpSystem.Core/Services/Procurement/TenderAwardService.cs`

**Key Methods:**
- `GenerateAwardRecommendationAsync(Guid tenderId)` - Generate award recommendation
- `CreateAwardAsync(Guid tenderId, CreateAwardDto dto)` - Create award
- `UpdateAwardAsync(Guid id, CreateAwardDto dto)` - Update award
- `CancelAwardAsync(Guid id, CancelAwardDto dto)` - Cancel award
- `GetAwardByTenderIdAsync(Guid tenderId)` - Get award for tender
- `SendAwardNotificationsAsync(Guid tenderId, AwardNotificationDto dto)` - Send notifications

**Award Recommendation Logic:**
1. Filter bids with status "Evaluated"
2. Calculate average score from all evaluations
3. Rank bids by average score (highest first)
4. Apply compliance filtering
5. Return top-ranked compliant bid as recommendation

#### TenderNotificationService
**Location:** `src/ErpSystem.Core/Services/Procurement/TenderNotificationService.cs`

**Key Methods:**
- `SendAwardNotificationAsync(Guid awardId)` - Notify winning supplier
- `SendRejectionNotificationsAsync(Guid tenderId, List<Guid> rejectedBidIds)` - Notify unsuccessful suppliers
- `SendEvaluationAssignedNotificationAsync(Guid tenderId, List<Guid> evaluatorIds)` - Notify evaluators
- `SendTenderPublishedNotificationAsync(Guid tenderId, List<Guid> businessPartnerIds)` - Notify invited suppliers

**Notification Types:**
- In-App notifications (created in database)
- Email notifications (sent via email service)
- Both types sent for critical events (awards, rejections)

### API Controllers

#### TenderEvaluationsController
**Location:** `src/ErpSystem.Api/Controllers/Procurement/TenderEvaluationsController.cs`

**Endpoints:**
- `GET /api/procurement/TenderEvaluations/{id}` - Get evaluation by ID
- `GET /api/procurement/TenderEvaluations/by-bid/{bidId}` - Get evaluations for bid
- `GET /api/procurement/TenderEvaluations/my-evaluations/{evaluatorId}` - Get my evaluations
- `POST /api/procurement/TenderEvaluations` - Create evaluation
- `PUT /api/procurement/TenderEvaluations/{id}` - Update evaluation
- `POST /api/procurement/TenderEvaluations/{id}/submit` - Submit evaluation
- `GET /api/procurement/TenderEvaluations/report/{tenderId}` - Get evaluation report

#### TenderAwardsController
**Location:** `src/ErpSystem.Api/Controllers/Procurement/TenderAwardsController.cs`

**Endpoints:**
- `GET /api/procurement/TenderAwards` - Get all awards (paginated)
- `GET /api/procurement/TenderAwards/{id}` - Get award by ID
- `GET /api/procurement/TenderAwards/by-tender/{tenderId}` - Get award for tender
- `GET /api/procurement/TenderAwards/recommendation/{tenderId}` - Generate recommendation
- `POST /api/procurement/TenderAwards` - Create award
- `PUT /api/procurement/TenderAwards/{id}` - Update award
- `POST /api/procurement/TenderAwards/{id}/cancel` - Cancel award

#### TendersController
**Location:** `src/ErpSystem.Api/Controllers/Procurement/TendersController.cs`

**New Endpoints:**
- `GET /api/procurement/Tenders/{id}/evaluators` - Get tender evaluators
- `DELETE /api/procurement/Tenders/{tenderId}/evaluators/{evaluatorId}` - Remove evaluator

---

## Frontend Architecture

### Services

#### tenderEvaluationService.ts
**Location:** `frontend/src/services/tenderEvaluationService.ts`

**Key Functions:**
- `getEvaluationById(id)` - Fetch evaluation
- `getMyEvaluations(evaluatorId)` - Fetch my evaluations
- `createEvaluation(data)` - Create evaluation
- `updateEvaluation(id, data)` - Update evaluation
- `submitEvaluation(id, data)` - Submit evaluation
- `getEvaluationReport(tenderId)` - Get evaluation report

#### tenderAwardService.ts
**Location:** `frontend/src/services/tenderAwardService.ts`

**Key Functions:**
- `generateAwardRecommendation(tenderId)` - Generate recommendation
- `createAward(data)` - Create award
- `getAwardByTenderId(tenderId)` - Get award for tender
- `cancelAward(id, data)` - Cancel award
- `sendAwardNotifications(tenderId, data)` - Send notifications

### Components

#### Internal Portal
- **TenderEvaluators.tsx** - Display and manage evaluators
- **AssignEvaluatorsDialog.tsx** - Assign new evaluators
- **TenderAward.tsx** - Award recommendation and creation
- **Evaluation Detail Page** - `/procurement/evaluations/[id]`
- **Awards Page** - `/procurement/awards`
- **Award Detail Page** - `/procurement/awards/[id]`

#### External Portal
- **My Bids Page** - `/external-portal/my-bids`
- **Bid Detail Page** - `/external-portal/my-bids/[id]`
  - Evaluation Tab - Shows evaluation results
  - Award Status - Shows if bid was awarded

---

## Data Models

### DTOs

**TenderEvaluationDto**
- id, tenderBidId, tenderEvaluatorId
- evaluatorName, evaluatorRole
- priceScore, qualityScore, deliveryScore, experienceScore, technicalScore, complianceScore
- totalScore, status
- technicalComments, commercialComments, overallComments
- isRecommended, recommendation

**AwardRecommendationDto**
- tenderId, tenderNumber, tenderTitle
- totalBids, evaluatedBids
- recommendedBidId, recommendedBidNumber, recommendedBusinessPartner
- recommendedAmount, recommendedScore
- bidRecommendations (array of BidRecommendationDto)

**TenderAwardDto**
- id, tenderId, tenderBidId
- businessPartnerId, businessPartnerName
- awardDate, awardedAmount, currency
- status, awardJustification
- awardedById, awardedByName

---

## Database Schema

### Key Tables
- `TenderEvaluators` - Evaluator assignments
- `TenderEvaluations` - Evaluation scores and comments
- `TenderAwards` - Award records
- `Notifications` - In-app notifications
- `NotificationLogs` - Email notification logs

### Relationships
- Tender → TenderEvaluators (1:N)
- Tender → TenderAwards (1:1)
- TenderBid → TenderEvaluations (1:N)
- TenderAward → TenderBid (1:1)

---

## Authorization & Security

**Required Roles:**
- SuperAdmin, TenantAdmin, Manager - Full access to evaluations and awards
- Evaluator - Can only view and edit own evaluations
- Supplier - Can view own bid evaluations and award status

**Tenant Isolation:**
- All queries filtered by TenantId
- Users can only access their tenant's data

---

## Error Handling

All services implement:
- Try-catch blocks with logging
- Meaningful error messages
- Proper HTTP status codes
- Validation of input data

---

## Performance Considerations

1. **Evaluation Queries** - Indexed by BidId and TenderId
2. **Award Queries** - Indexed by TenderId
3. **Notification Queries** - Indexed by RecipientId
4. **Pagination** - Used for large result sets
5. **Caching** - Consider caching evaluation reports

---

## Testing Checklist

- [ ] Evaluator assignment and removal
- [ ] Evaluation creation and submission
- [ ] Score consolidation and ranking
- [ ] Award recommendation generation
- [ ] Award creation and cancellation
- [ ] Notification sending
- [ ] External portal bid evaluation display
- [ ] Award status visibility to suppliers
- [ ] Authorization and role-based access
- [ ] Tenant isolation

