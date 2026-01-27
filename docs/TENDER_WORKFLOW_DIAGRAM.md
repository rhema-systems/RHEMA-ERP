# Tender Management Workflow Diagram

## Complete Tender Lifecycle

```
┌─────────────────────────────────────────────────────────────────────┐
│                    TENDER MANAGEMENT WORKFLOW                        │
└─────────────────────────────────────────────────────────────────────┘

PHASE 1: TENDER CREATION & PUBLICATION
═══════════════════════════════════════
  1. Create Tender (Draft)
     ├─ Set title, description, type
     ├─ Define evaluation criteria & weightages
     ├─ Add tender items
     └─ Upload tender documents

  2. Publish Tender
     ├─ Set submission deadline
     ├─ Set opening date
     ├─ Invite business partners
     └─ Send notifications
     Status: Published

PHASE 2: BIDDING (EXTERNAL PORTAL)
═══════════════════════════════════
  3. Suppliers View Tender
     ├─ Download tender documents
     ├─ View requirements
     └─ Check deadline

  4. Suppliers Submit Bids
     ├─ Create bid (Draft)
     ├─ Add bid items with prices
     ├─ Upload bid documents
     ├─ Accept declaration
     └─ Submit bid
     Status: Submitted

PHASE 3: BID OPENING (INTERNAL)
════════════════════════════════
  5. Open Bids
     ├─ Individual bid opening
     ├─ OR Bulk open all bids
     └─ Bids become visible
     Status: Opened

PHASE 4: EVALUATION (NEW - TO IMPLEMENT)
═════════════════════════════════════════
  6. Assign Evaluators
     ├─ Select evaluation team
     ├─ Set weightage per evaluator
     └─ Notify evaluators
     Status: UnderEvaluation

  7. Evaluate Bids
     ├─ Each evaluator scores bid
     │  ├─ Price score
     │  ├─ Quality score
     │  ├─ Delivery score
     │  ├─ Experience score
     │  ├─ Technical score
     │  └─ Compliance score
     ├─ Add comments
     ├─ Make recommendation
     └─ Submit evaluation

  8. Consolidate Evaluations
     ├─ Calculate average scores
     ├─ Rank bids
     └─ Generate evaluation report

PHASE 5: AWARD (NEW - TO IMPLEMENT)
════════════════════════════════════
  9. Generate Award Recommendation
     ├─ Rank bids by total score
     ├─ Recommend winning bid
     └─ Show justification

  10. Approve Award
      ├─ Review recommendation
      ├─ Approve or reject
      └─ Add justification notes

  11. Create Award
      ├─ Record award decision
      ├─ Set awarded amount
      └─ Link to bid
      Status: Awarded

PHASE 6: POST-AWARD
════════════════════
  12. Send Notifications
      ├─ Award letter to winner
      └─ Rejection letters to others

  13. Generate Reports
      ├─ Evaluation report
      ├─ Award recommendation report
      └─ Tender outcome summary

  14. Contract Management
      ├─ Generate PO (if needed)
      └─ Track contract status
```

## Status Transitions

```
Tender Status Flow:
Draft → Published → Closed → Evaluated → Awarded → Completed

Bid Status Flow:
Draft → Submitted → Opened → UnderEvaluation → Accepted/Rejected

Evaluation Status Flow:
Draft → Submitted → Approved

Award Status Flow:
Pending → Awarded → ContractSigned
```

## Key Actors

- **Tender Creator:** Creates and publishes tenders
- **Supplier:** Views tender, submits bid
- **Bid Opener:** Opens submitted bids
- **Evaluator:** Scores and evaluates bids
- **Award Manager:** Reviews and approves awards
- **System:** Sends notifications, generates reports

## Data Flow

```
Tender → Items → Documents
  ↓
Invitations → Suppliers
  ↓
Bids → Items → Documents
  ↓
Open Bids
  ↓
Evaluators → Evaluations → Scores
  ↓
Consolidate Scores
  ↓
Award Recommendation
  ↓
Award Approval
  ↓
Award → Notifications → Reports
```

