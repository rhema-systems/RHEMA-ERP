# Tender Post-Opening Implementation Checklist

## 1. EVALUATOR ASSIGNMENT WORKFLOW

### Backend
- [ ] Verify `TenderEvaluator` entity exists and has all required fields
- [ ] Verify `ITenderEvaluatorRepository` interface exists
- [ ] Verify `TenderEvaluatorRepository` implementation exists
- [ ] Create/verify `AssignEvaluatorsDto` DTO
- [ ] Implement `AssignEvaluatorsAsync()` in TenderService
- [ ] Implement `GetTenderEvaluatorsAsync()` in TenderService
- [ ] Implement `RemoveEvaluatorAsync()` in TenderService
- [ ] Create API endpoint: POST `/api/procurement/tenders/{tenderId}/assign-evaluators`
- [ ] Create API endpoint: GET `/api/procurement/tenders/{tenderId}/evaluators`
- [ ] Create API endpoint: DELETE `/api/procurement/tenders/{tenderId}/evaluators/{evaluatorId}`

### Frontend
- [ ] Create `TenderEvaluators` component for tender detail page
- [ ] Add "Evaluators" tab to tender detail page
- [ ] Implement evaluator assignment dialog
- [ ] Implement evaluator list with remove functionality
- [ ] Add evaluator service API calls

---

## 2. BID EVALUATION PROCESS

### Backend
- [ ] Verify `TenderEvaluation` entity has all score fields
- [ ] Verify `CreateEvaluationDto` and `UpdateEvaluationDto` exist
- [ ] Verify evaluation service methods exist:
  - `CreateEvaluationAsync()`
  - `UpdateEvaluationAsync()`
  - `SubmitEvaluationAsync()`
  - `GetBidScorecardAsync()`
  - `GetConsolidatedEvaluationsAsync()`
- [ ] Implement score calculation logic (weighted scoring)
- [ ] Implement evaluation consolidation (average multiple evaluators)
- [ ] Create API endpoints (already exist in controller)

### Frontend
- [ ] Create evaluation detail page (`/procurement/evaluations/[id]`)
- [ ] Implement score input form with validation
- [ ] Implement comments section
- [ ] Implement recommendation checkbox
- [ ] Implement submit evaluation button
- [ ] Update evaluations list page with better filtering
- [ ] Add evaluation progress indicators

---

## 3. AWARD RECOMMENDATION & APPROVAL

### Backend
- [ ] Implement `GenerateAwardRecommendationAsync()` in TenderAwardService
- [ ] Implement `ApproveAwardAsync()` in TenderAwardService
- [ ] Implement `RejectAwardAsync()` in TenderAwardService
- [ ] Implement `GenerateAwardNotificationAsync()` in TenderAwardService
- [ ] Create `ApproveAwardDto` and `RejectAwardDto` DTOs
- [ ] Create `AwardRecommendationDto` DTO
- [ ] Update API controller endpoints (currently return 501)

### Frontend
- [ ] Create award recommendation page
- [ ] Show ranked bids with evaluation scores
- [ ] Implement approve/reject award functionality
- [ ] Create award detail page (`/procurement/awards/[id]`)
- [ ] Add award history/timeline
- [ ] Add award justification notes

---

## 4. NOTIFICATIONS & REPORTING

### Backend
- [ ] Implement award notification to winning supplier
- [ ] Implement rejection notification to unsuccessful suppliers
- [ ] Implement evaluation completion notifications
- [ ] Create evaluation report generation
- [ ] Create award recommendation report

### Frontend
- [ ] Create reports page
- [ ] Add evaluation report view/download
- [ ] Add award recommendation report
- [ ] Add tender outcome summary

---

## 5. TENDER STATUS FLOW

Current: Draft → Published → Closed → Awarded
Missing: Closed → Evaluated → Awarded

- [ ] Add "Evaluated" status to tender workflow
- [ ] Update bid status flow: Submitted → Opened → UnderEvaluation → Accepted/Rejected
- [ ] Update tender status transitions in service

---

## 6. TESTING

- [ ] Unit tests for evaluator assignment
- [ ] Unit tests for evaluation scoring
- [ ] Unit tests for award recommendation
- [ ] Integration tests for full workflow
- [ ] Frontend component tests

---

## Implementation Order
1. Evaluator assignment (enables evaluation)
2. Evaluation scoring UI (core functionality)
3. Award recommendation (decision making)
4. Award approval workflow (finalization)
5. Notifications (communication)
6. Reports (documentation)

