# Tender Management - Quick Reference Guide

## Current Workflow (What Works)

```
1. Create Tender (Draft)
   ↓
2. Publish Tender (Published)
   ↓
3. Suppliers Submit Bids (Submitted)
   ↓
4. Open Bids (Opened)
   ↓
   ❌ STOPS HERE - Evaluation & Award Missing
```

---

## What Needs to Happen Next

### Step 5: Assign Evaluators
**Status:** ❌ Not Implemented

**What to do:**
- Go to tender detail page
- Click "Evaluators" tab (doesn't exist yet)
- Add evaluation team members
- Set their weightage (e.g., 50%, 50%)
- Tender status changes to "UnderEvaluation"

**Backend needed:**
- `POST /api/procurement/tenders/{tenderId}/assign-evaluators`
- `GET /api/procurement/tenders/{tenderId}/evaluators`

**Frontend needed:**
- Evaluators tab in tender detail
- Evaluator assignment dialog
- Evaluator list with remove button

---

### Step 6: Evaluate Bids
**Status:** ❌ Not Implemented (UI missing)

**What to do:**
- Go to "My Evaluations" page
- Click on pending evaluation
- Score the bid on 6 criteria (0-100):
  - Price Score
  - Quality Score
  - Delivery Score
  - Experience Score
  - Technical Score
  - Compliance Score
- Add comments
- Check "Recommended" if bid is good
- Submit evaluation

**Backend needed:**
- Enhance TenderEvaluationService methods
- Implement score calculation logic

**Frontend needed:**
- Evaluation detail page (`/procurement/evaluations/[id]`)
- Score input form with validation
- Comments section
- Submit button

---

### Step 7: Consolidate & Rank
**Status:** ⚠️ Partially Implemented

**What happens:**
- System calculates average score from all evaluators
- Bids are ranked by total score
- Compliance checks applied
- Evaluation report generated

**Backend needed:**
- Verify score consolidation logic
- Ensure ranking algorithm works

---

### Step 8: Generate Award Recommendation
**Status:** ❌ Not Implemented

**What to do:**
- Go to tender detail page
- Click "Award Recommendation" tab
- System shows ranked bids
- Top-ranked compliant bid is recommended
- Review justification
- Click "Approve Award" or "Recommend Alternative"

**Backend needed:**
- `GenerateAwardRecommendationAsync()` in TenderAwardService
- Ranking algorithm
- Compliance filtering

**Frontend needed:**
- Award recommendation page
- Ranked bids display
- Approve/reject buttons

---

### Step 9: Approve Award
**Status:** ❌ Not Implemented

**What to do:**
- Review award recommendation
- Click "Approve Award"
- Enter award amount (usually bid amount)
- Add justification notes
- Confirm approval
- Tender status changes to "Awarded"

**Backend needed:**
- `ApproveAwardAsync()` in TenderAwardService
- `RejectAwardAsync()` in TenderAwardService
- Update tender status to "Awarded"
- Update bid status to "Accepted"

**Frontend needed:**
- Award approval dialog
- Award detail page
- Approval history/timeline

---

### Step 10: Send Notifications
**Status:** ❌ Not Implemented

**What happens:**
- Award letter sent to winning supplier
- Rejection letters sent to other suppliers
- Notifications appear in system

**Backend needed:**
- `SendAwardNotificationAsync()`
- `SendRejectionNotificationAsync()`
- Notification templates

---

### Step 11: Generate Reports
**Status:** ❌ Not Implemented

**What to do:**
- Go to tender detail page
- Click "Reports" tab
- Download evaluation report
- Download award recommendation report
- Download tender outcome summary

**Backend needed:**
- Report generation methods
- PDF export functionality

---

## Key Entities & Their Relationships

```
Tender
├── TenderEvaluator (Assigned evaluators)
├── TenderBid (Submitted bids)
│   ├── TenderEvaluation (Scores from each evaluator)
│   └── TenderBidDocument (Bid documents)
└── TenderAward (Award decision)
```

---

## Status Values

### Tender Status
- Draft → Published → Closed → **Evaluated** (NEW) → Awarded

### Bid Status
- Draft → Submitted → Opened → **UnderEvaluation** (NEW) → Accepted/Rejected

### Evaluation Status
- Draft → Submitted → Approved

### Award Status
- Pending → Awarded → ContractSigned

---

## Important Notes

1. **Evaluators must be assigned BEFORE evaluation starts**
2. **All evaluators must submit before consolidation**
3. **Bid must be compliant to be awarded**
4. **Award amount should match bid amount (usually)**
5. **Notifications are critical for supplier communication**

---

## Files to Check

**Backend:**
- `src/ErpSystem.Core/Services/Procurement/TenderEvaluationService.cs`
- `src/ErpSystem.Core/Services/Procurement/TenderAwardService.cs`
- `src/ErpSystem.Api/Controllers/Procurement/TenderEvaluationsController.cs`
- `src/ErpSystem.Api/Controllers/Procurement/TenderAwardsController.cs`

**Frontend:**
- `frontend/src/app/procurement/evaluations/page.tsx`
- `frontend/src/app/procurement/awards/page.tsx`
- `frontend/src/services/tenderEvaluationService.ts`
- `frontend/src/services/tenderAwardService.ts`

---

## Next Action

**Start with:** Evaluator Assignment UI
- Easiest to implement
- Unblocks evaluation process
- Good foundation for rest of workflow

**Estimated effort:** 2-3 days

