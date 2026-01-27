# Tender Management System - Complete Workflow Guide

## End-to-End Tender Evaluation & Award Process

### Step 1: Tender Creation & Publication
**User:** Internal Team (Manager/Admin)
**Location:** `/procurement/tenders`

1. Create tender with details (title, type, estimated value, etc.)
2. Add tender items/lots
3. Upload required documents
4. Set submission deadline
5. Invite suppliers
6. Publish tender

**Result:** Tender status changes to "Published", suppliers receive notifications

---

### Step 2: Supplier Bidding
**User:** External Suppliers
**Location:** `/external-portal/tenders`

1. Login to external portal
2. View published tender
3. Review tender documents and requirements
4. Prepare bid with:
   - Bid amount and currency
   - Technical proposal
   - Commercial proposal
   - Required documents
5. Submit bid before deadline

**Result:** Bid status = "Submitted", internal team receives notification

---

### Step 3: Bid Opening
**User:** Internal Team (Manager/Admin)
**Location:** `/procurement/tenders/{id}` → Bids Tab

1. Navigate to tender detail page
2. Go to Bids tab
3. Select bids to open (individual or bulk)
4. Click "Open Bid" button
5. Confirm opening

**Result:** Bid status changes to "Opened", bids ready for evaluation

---

### Step 4: Assign Evaluators
**User:** Internal Team (Manager/Admin)
**Location:** `/procurement/tenders/{id}` → Evaluators Tab

1. Go to tender detail page
2. Click "Evaluators" tab
3. Click "Assign Evaluators" button
4. In dialog:
   - Select evaluator from user list
   - Choose role (Evaluator, ChairPerson, Secretary, Observer)
   - Set weightage percentage (0-100)
   - Click "Add Evaluator"
5. Repeat for all evaluators
6. Click "Assign" to save

**Result:** Evaluators assigned, notifications sent to evaluators

---

### Step 5: Evaluators Score Bids
**User:** Assigned Evaluators
**Location:** `/procurement/evaluations`

1. Login to internal portal
2. Go to "My Evaluations" page
3. Click on evaluation to open
4. Score each criterion (0-100):
   - Price Score
   - Quality Score
   - Delivery Score
   - Experience Score
   - Technical Score
   - Compliance Score
5. Add comments:
   - Technical Comments
   - Commercial Comments
   - Overall Comments
6. Check "Recommended" if bid should be awarded
7. Click "Save Draft" to save without submitting
8. Click "Submit Evaluation" when ready

**Result:** Evaluation status = "Submitted", scores consolidated

---

### Step 6: Review Evaluation Results
**User:** Internal Team (Manager/Admin)
**Location:** `/procurement/evaluations`

1. Go to Evaluations page
2. View all evaluations grouped by status
3. Click on evaluation to view details
4. See consolidated scores from all evaluators
5. Review comments and recommendations

**Result:** All evaluations reviewed and consolidated

---

### Step 7: Generate Award Recommendation
**User:** Internal Team (Manager/Admin)
**Location:** `/procurement/tenders/{id}` → Award Tab

1. Go to tender detail page
2. Click "Award" tab
3. System automatically generates recommendation:
   - Ranks bids by average score
   - Filters by compliance
   - Shows top-ranked bid as recommendation
4. Review recommendation details:
   - Recommended bid number
   - Supplier name
   - Bid amount
   - Average score
   - Evaluation count

**Result:** Award recommendation displayed

---

### Step 8: Create Award
**User:** Internal Team (Manager/Admin)
**Location:** `/procurement/tenders/{id}` → Award Tab

1. In Award tab, review recommendation
2. Click "Award Bid" button
3. In dialog:
   - Confirm bid selection (pre-filled with recommended bid)
   - Enter award amount (pre-filled with bid amount)
   - Add award justification (optional)
   - Add notes (optional)
4. Click "Create Award"

**Result:** Award created, status = "Awarded"

**Automatic Actions:**
- Tender status changes to "Awarded"
- Winning bid status changes to "Awarded"
- Award notification sent to winning supplier
- Rejection notifications sent to unsuccessful suppliers

---

### Step 9: Supplier Receives Award Notification
**User:** Winning Supplier
**Location:** `/external-portal/my-bids`

1. Login to external portal
2. Go to "My Bids" page
3. See bid with status "Accepted"
4. Click on bid to view details
5. See award information:
   - Award amount and currency
   - Award date
   - Evaluation scores from all evaluators
   - Comments from evaluators
6. Receive in-app notification about award
7. Receive email notification with award details

**Result:** Supplier informed of award, can proceed with contract

---

### Step 10: View Evaluation Report
**User:** Internal Team (Manager/Admin)
**Location:** `/procurement/evaluations` → Report

1. Go to Evaluations page
2. Click "View Report" for tender
3. Report shows:
   - Tender details
   - All bid scorecards
   - Evaluation criteria and weightages
   - Bid rankings
   - Recommended bid
   - Evaluation statistics

**Result:** Comprehensive evaluation report available

---

## Key Features by Role

### Internal Team (Manager/Admin)
- ✅ Create and publish tenders
- ✅ Assign evaluators
- ✅ View all evaluations
- ✅ Generate award recommendations
- ✅ Create and manage awards
- ✅ View evaluation reports
- ✅ Send notifications

### Evaluators
- ✅ View assigned evaluations
- ✅ Score bids
- ✅ Add comments and recommendations
- ✅ Submit evaluations
- ✅ View consolidated scores

### Suppliers (External Portal)
- ✅ View published tenders
- ✅ Submit bids
- ✅ View bid status
- ✅ View evaluation results (after evaluation)
- ✅ View award status (if awarded)
- ✅ Receive notifications

---

## Status Transitions

### Tender Status
```
Draft → Published → Closed → Evaluated → Awarded
```

### Bid Status
```
Draft → Submitted → Opened → UnderEvaluation → Accepted/Rejected
```

### Evaluation Status
```
Draft → Submitted → Approved
```

### Award Status
```
Pending → Awarded → Cancelled
```

---

## Notifications Sent

| Event | Recipient | Type | Message |
|-------|-----------|------|---------|
| Tender Published | Invited Suppliers | Email + In-App | Tender invitation with deadline |
| Bid Submitted | Internal Team | In-App | Bid received notification |
| Evaluator Assigned | Evaluators | In-App | Evaluation assignment |
| Award Created | Winning Supplier | Email + In-App | Congratulations, bid awarded |
| Bid Rejected | Unsuccessful Suppliers | In-App | Bid not successful |

---

## Common Tasks

### How to Reassign Evaluators
1. Go to tender → Evaluators tab
2. Click remove (X) on evaluator to remove
3. Click "Assign Evaluators" to add new ones

### How to Cancel an Award
1. Go to award detail page
2. Click "Cancel Award" button
3. Enter cancellation reason
4. Confirm cancellation

### How to View Bid Evaluation Results
**As Internal Team:**
1. Go to Evaluations page
2. Click on evaluation to view scores

**As Supplier:**
1. Go to My Bids page
2. Click on bid
3. Go to "Evaluation" tab

### How to Generate Evaluation Report
1. Go to Evaluations page
2. Click "View Report" for tender
3. Report shows all bid scorecards and rankings

---

## Troubleshooting

**Problem:** Evaluators not receiving notifications
- **Solution:** Check evaluator email in system, verify notification service is running

**Problem:** Evaluation scores not consolidating
- **Solution:** Ensure all evaluators have submitted evaluations, check bid status is "Opened"

**Problem:** Award recommendation not showing
- **Solution:** Verify bids have status "Evaluated", check evaluations are submitted

**Problem:** Supplier not seeing evaluation results
- **Solution:** Verify bid status is "Opened" or later, check evaluation is submitted

---

## Best Practices

1. **Assign Multiple Evaluators** - Use 3-5 evaluators for better objectivity
2. **Set Clear Weightages** - Ensure weightages add up to 100%
3. **Document Justifications** - Always add award justification for audit trail
4. **Review Before Award** - Review all evaluations before creating award
5. **Communicate Clearly** - Send detailed notifications to suppliers
6. **Archive Reports** - Keep evaluation reports for compliance
7. **Test Workflow** - Test with sample tender before production use

