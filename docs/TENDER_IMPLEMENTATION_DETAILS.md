# Tender Evaluation & Award Implementation Details

## 1. EVALUATOR ASSIGNMENT

### Service Method Signature
```csharp
Task AssignEvaluatorsAsync(Guid tenderId, AssignEvaluatorsDto dto);
Task<IEnumerable<TenderEvaluatorDto>> GetTenderEvaluatorsAsync(Guid tenderId);
Task RemoveEvaluatorAsync(Guid tenderId, Guid evaluatorId);
```

### DTO Structure
```csharp
public class AssignEvaluatorsDto
{
    public List<EvaluatorAssignmentDto> Evaluators { get; set; }
}

public class EvaluatorAssignmentDto
{
    public Guid UserId { get; set; }
    public string Role { get; set; } // Technical, Commercial, Finance
    public decimal WeightagePercentage { get; set; } // 0-100
}
```

### Workflow
1. Tender status must be "Opened" or "Closed"
2. Create TenderEvaluator records for each evaluator
3. Update tender status to "UnderEvaluation"
4. Send notifications to assigned evaluators

---

## 2. EVALUATION SCORING

### Score Calculation
```
Total Score = (Price × PriceWeightage + 
               Quality × QualityWeightage + 
               Delivery × DeliveryWeightage + 
               Experience × ExperienceWeightage) / 100

Weighted Evaluator Score = Evaluator Score × EvaluatorWeightage / 100

Final Bid Score = Average of all evaluator weighted scores
```

### Evaluation Status Flow
- **Draft:** Evaluator creating evaluation
- **Submitted:** Evaluator submitted evaluation
- **Approved:** Manager approved evaluation

### Bid Status During Evaluation
- **Opened:** Bid opened, ready for evaluation
- **UnderEvaluation:** Being evaluated
- **Accepted:** Evaluation complete, bid accepted
- **Rejected:** Evaluation complete, bid rejected

---

## 3. AWARD RECOMMENDATION

### Algorithm
1. Get all bids for tender with status "Opened"
2. For each bid, get consolidated evaluation score
3. Rank bids by total score (highest first)
4. Apply compliance filters:
   - Must have IsCompliant = true
   - Must meet minimum performance rating
5. Recommend top-ranked compliant bid
6. Show alternative recommendations

### Recommendation DTO
```csharp
public class AwardRecommendationDto
{
    public Guid TenderId { get; set; }
    public List<BidRankingDto> RankedBids { get; set; }
    public BidRankingDto RecommendedBid { get; set; }
    public string Justification { get; set; }
}

public class BidRankingDto
{
    public Guid BidId { get; set; }
    public string BidNumber { get; set; }
    public string SupplierName { get; set; }
    public decimal TotalScore { get; set; }
    public decimal BidAmount { get; set; }
    public bool IsCompliant { get; set; }
    public int Rank { get; set; }
}
```

---

## 4. AWARD APPROVAL WORKFLOW

### Approval Process
1. Manager reviews award recommendation
2. Manager approves or rejects award
3. If approved:
   - Create TenderAward record
   - Update tender status to "Awarded"
   - Update bid status to "Accepted"
   - Send award notification to winner
   - Send rejection notifications to others
4. If rejected:
   - Recommend next-ranked bid
   - Return to step 1

### Award Status Values
- **Awarded:** Award approved and active
- **ContractSigned:** Contract signed with supplier
- **Cancelled:** Award cancelled

---

## 5. NOTIFICATION TEMPLATES

### Award Notification
- Tender number and title
- Awarded amount
- Delivery timeline
- Next steps (contract signing)
- Contact information

### Rejection Notification
- Tender number and title
- Bid score and ranking
- Reason for non-selection
- Feedback for improvement
- Option to appeal

---

## 6. REPORT GENERATION

### Evaluation Report
- Tender details
- All bids with scores
- Evaluator comments
- Consolidated scores
- Ranking

### Award Recommendation Report
- Tender details
- Ranked bids
- Recommended bid with justification
- Compliance check results
- Alternative recommendations

### Tender Outcome Summary
- Tender details
- Number of bids received
- Evaluation timeline
- Awarded supplier
- Awarded amount
- Contract status

---

## 7. DATABASE CONSIDERATIONS

### Indexes Needed
- TenderEvaluation: (TenderBidId, TenderEvaluatorId)
- TenderEvaluation: (TenderId, Status)
- TenderAward: (TenderId, Status)
- TenderBid: (TenderId, Status)

### Audit Trail
- Track all evaluation changes
- Track all award decisions
- Log all notifications sent
- Maintain evaluation history

---

## 8. VALIDATION RULES

### Evaluator Assignment
- Tender must be in "Opened" or "Closed" status
- Evaluators must be active users
- Weightage percentages must sum to 100%
- Cannot assign same user twice

### Evaluation Submission
- All score fields must be filled (0-100)
- Comments are optional
- Recommendation must be boolean
- Cannot submit if bid status not "UnderEvaluation"

### Award Approval
- Bid must have completed evaluation
- Bid must be compliant
- Award amount must be positive
- Justification is required

