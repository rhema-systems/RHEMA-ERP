# Tender Management - Component Breakdown

## 1. EVALUATOR ASSIGNMENT COMPONENT

### Backend Files to Modify
- `TenderService.cs`
  - Add `AssignEvaluatorsAsync(Guid tenderId, AssignEvaluatorsDto dto)`
  - Add `GetTenderEvaluatorsAsync(Guid tenderId)`
  - Add `RemoveEvaluatorAsync(Guid tenderId, Guid evaluatorId)`

- `TendersController.cs`
  - Add `POST /api/procurement/tenders/{tenderId}/assign-evaluators`
  - Add `GET /api/procurement/tenders/{tenderId}/evaluators`
  - Add `DELETE /api/procurement/tenders/{tenderId}/evaluators/{evaluatorId}`

### Frontend Files to Create/Modify
- `frontend/src/app/procurement/tenders/[id]/page.tsx`
  - Add "Evaluators" tab to TabsList
  - Add TabsContent for evaluators

- `frontend/src/components/procurement/tenders/TenderEvaluators.tsx` (NEW)
  - List assigned evaluators
  - Add evaluator dialog
  - Remove evaluator button

- `frontend/src/services/tenderService.ts`
  - Add `assignEvaluators()` method
  - Add `getTenderEvaluators()` method
  - Add `removeEvaluator()` method

---

## 2. EVALUATION DETAIL COMPONENT

### Backend Files to Verify
- `TenderEvaluationService.cs`
  - Verify `CreateEvaluationAsync()` works
  - Verify `UpdateEvaluationAsync()` works
  - Verify `SubmitEvaluationAsync()` works

- `TenderEvaluationsController.cs`
  - All endpoints should work

### Frontend Files to Create
- `frontend/src/app/procurement/evaluations/[id]/page.tsx` (NEW)
  - Display bid details
  - Score input form (6 fields)
  - Comments section
  - Recommendation checkbox
  - Submit button

- `frontend/src/components/procurement/evaluations/EvaluationForm.tsx` (NEW)
  - Score input fields with validation
  - Comments textarea
  - Recommendation checkbox
  - Submit/Save buttons

- `frontend/src/services/tenderEvaluationService.ts`
  - Verify all methods exist
  - Add missing methods if needed

---

## 3. AWARD RECOMMENDATION COMPONENT

### Backend Files to Modify
- `TenderAwardService.cs`
  - Implement `GenerateAwardRecommendationAsync(Guid tenderId)`
  - Implement ranking algorithm
  - Implement compliance filtering

- `TenderAwardsController.cs`
  - Fix `GenerateAwardRecommendation()` endpoint (currently returns 501)

### Frontend Files to Create
- `frontend/src/app/procurement/awards/recommendation/page.tsx` (NEW)
  - Display tender details
  - Show ranked bids table
  - Highlight recommended bid
  - Show justification
  - Approve/reject buttons

- `frontend/src/components/procurement/awards/AwardRecommendation.tsx` (NEW)
  - Ranked bids display
  - Recommendation details
  - Action buttons

- `frontend/src/services/tenderAwardService.ts`
  - Add `getAwardRecommendation()` method

---

## 4. AWARD APPROVAL COMPONENT

### Backend Files to Modify
- `TenderAwardService.cs`
  - Implement `ApproveAwardAsync(Guid awardId, ApproveAwardDto dto)`
  - Implement `RejectAwardAsync(Guid awardId, RejectAwardDto dto)`
  - Update tender status to "Awarded"
  - Update bid status to "Accepted"

- `TenderAwardsController.cs`
  - Fix `ApproveAward()` endpoint (currently returns 501)
  - Fix `RejectAward()` endpoint (currently returns 501)

### Frontend Files to Create/Modify
- `frontend/src/app/procurement/awards/[id]/page.tsx` (NEW)
  - Display award details
  - Show linked bid information
  - Show evaluation scores
  - Approve/reject buttons
  - Award history timeline

- `frontend/src/components/procurement/awards/AwardApprovalDialog.tsx` (NEW)
  - Award amount input
  - Justification textarea
  - Approve/reject buttons

- `frontend/src/services/tenderAwardService.ts`
  - Add `approveAward()` method
  - Add `rejectAward()` method

---

## 5. NOTIFICATION COMPONENT

### Backend Files to Create/Modify
- `TenderAwardService.cs`
  - Implement `SendAwardNotificationAsync(Guid awardId)`
  - Implement `SendRejectionNotificationAsync(Guid bidId)`

- `NotificationService.cs` (if exists)
  - Add award notification template
  - Add rejection notification template

### Frontend Files
- No frontend changes needed (notifications handled by backend)

---

## 6. REPORTS COMPONENT

### Backend Files to Create/Modify
- `TenderEvaluationService.cs`
  - Enhance `GetTenderEvaluationReportAsync()` method
  - Add report formatting

- `TenderAwardService.cs`
  - Add `GenerateAwardReportAsync()` method
  - Add `GenerateTenderOutcomeSummaryAsync()` method

### Frontend Files to Create
- `frontend/src/app/procurement/reports/page.tsx` (NEW)
  - List available reports
  - Filter by tender
  - Download buttons

- `frontend/src/components/procurement/reports/ReportViewer.tsx` (NEW)
  - Display report content
  - PDF export button

---

## File Summary

### New Backend Files: 0
(All infrastructure exists, just need to implement methods)

### Modified Backend Files: 3
- TenderService.cs
- TenderAwardService.cs
- TenderEvaluationService.cs
- TendersController.cs
- TenderAwardsController.cs

### New Frontend Files: 8
- `evaluations/[id]/page.tsx`
- `awards/[id]/page.tsx`
- `awards/recommendation/page.tsx`
- `reports/page.tsx`
- `components/evaluations/EvaluationForm.tsx`
- `components/awards/AwardRecommendation.tsx`
- `components/awards/AwardApprovalDialog.tsx`
- `components/reports/ReportViewer.tsx`

### Modified Frontend Files: 3
- `tenders/[id]/page.tsx`
- `evaluations/page.tsx`
- `awards/page.tsx`
- `services/tenderEvaluationService.ts`
- `services/tenderAwardService.ts`

---

## Implementation Order

1. **Evaluator Assignment** (2-3 days)
   - Simplest, unblocks evaluation
   - Start here

2. **Evaluation Detail Page** (3-4 days)
   - Core functionality
   - Most complex form

3. **Award Recommendation** (2-3 days)
   - Decision making
   - Relatively straightforward

4. **Award Approval** (2 days)
   - Finalization
   - Depends on recommendation

5. **Notifications** (2 days)
   - Communication
   - Can be done in parallel

6. **Reports** (2-3 days)
   - Documentation
   - Can be done last

**Total Estimated Effort: 13-17 days (2-3 weeks)**

