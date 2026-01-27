# Quality and Cost Based Selection (QCBS) Evaluation Formula

## Overview

This document describes the tender bid evaluation methodology based on the Quality and Cost Based Selection (QCBS) approach, commonly used in World Bank, government, and institutional procurement processes.

The evaluation process consists of three phases:
1. **Technical Evaluation** - Scoring the technical proposal
2. **Financial Evaluation** - Scoring based on price competitiveness
3. **Combined Evaluation** - Weighted combination of technical and financial scores

---

## 1. Technical Evaluation

### Evaluation Criteria Structure

Technical proposals are evaluated against predefined criteria with sub-criteria. Each criterion has a maximum score, and sub-criteria scores must sum to the parent criterion's maximum.

#### Example Criteria Structure

| Item | Evaluation Criteria | Points |
|------|---------------------|--------|
| 1 | **Relevant Experience** | **0-10** |
|   | • General experience | 0-5 |
|   | • Specific experience in similar projects and environment | 0-5 |
| 2 | **Methodology and Work Plan for Performance of Services** | **10-50** |
|   | • Methodology (including Quality Control/Methods Statement) | 10-30 |
|   | • Work Plan | 0-20 |
| 3 | **Qualification & Experience of Key Staff** | **10-40** |
|   | **Total Technical Points** | **100** |

### Technical Score Calculation

```
St = Σ (Score for each criterion)
```

Where:
- **St** = Total Technical Score (out of 100)

### Minimum Technical Score (Pass Threshold)

Bidders must achieve a **minimum technical score** to qualify for financial evaluation.

```
If St < MinimumTechnicalScore → Bid is DISQUALIFIED
```

**Default Minimum Score: 80 points**

> ⚠️ Bids that fail to meet the minimum technical score are automatically disqualified and do not proceed to financial evaluation.

---

## 2. Financial Evaluation

### Financial Score Formula

The financial score is calculated relative to the lowest bid price among all technically qualified bidders.

```
Sf = 100 × (Fm / F)
```

Where:
- **Sf** = Financial Score of the bid being evaluated
- **Fm** = Lowest (minimum) price among all qualified bids
- **F** = Price of the bid being evaluated

### Key Principles

1. The **lowest price** receives the **maximum financial score of 100**
2. Higher prices receive proportionally lower scores
3. Only **technically qualified bids** (St ≥ minimum) are included in price comparison

### Financial Score Example

| Bidder | Bid Price | Calculation | Financial Score (Sf) |
|--------|-----------|-------------|---------------------|
| A | $100,000 (lowest) | 100 × (100,000 / 100,000) | **100.00** |
| B | $125,000 | 100 × (100,000 / 125,000) | **80.00** |
| C | $150,000 | 100 × (100,000 / 150,000) | **66.67** |
| D | $200,000 | 100 × (100,000 / 200,000) | **50.00** |

---

## 3. Combined Technical and Financial Evaluation

### Combined Score Formula

The final ranking is based on a weighted combination of technical and financial scores.

```
S = (St × T%) + (Sf × P%)
```

Where:
- **S** = Combined/Final Score
- **St** = Technical Score (out of 100)
- **Sf** = Financial Score (calculated using the formula above)
- **T** = Technical Weight (percentage)
- **P** = Price/Financial Weight (percentage)
- **T + P = 100%** (weights must sum to 100)

### Default Weights

| Component | Weight | Description |
|-----------|--------|-------------|
| Technical (T) | **60%** | Weight given to technical proposal quality |
| Financial (P) | **40%** | Weight given to price competitiveness |

> 📝 Weights can be configured per tender based on procurement requirements.

### Combined Score Example

Using T = 60% and P = 40%:

| Bidder | Technical (St) | Financial (Sf) | Combined Score (S) | Rank |
|--------|----------------|----------------|-------------------|------|
| A | 85 | 100.00 | (85 × 0.60) + (100.00 × 0.40) = 51.0 + 40.0 = **91.00** | 🥇 1st |
| B | 92 | 80.00 | (92 × 0.60) + (80.00 × 0.40) = 55.2 + 32.0 = **87.20** | 🥈 2nd |
| C | 88 | 66.67 | (88 × 0.60) + (66.67 × 0.40) = 52.8 + 26.7 = **79.47** | 🥉 3rd |

**Winner: Bidder A** with highest combined score of 91.00

---

## 4. Complete Evaluation Workflow

```
┌─────────────────────────────────────────────────────────────────┐
│                    QCBS EVALUATION PROCESS                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Step 1: TECHNICAL EVALUATION                                   │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  For each bid:                                           │   │
│  │  • Evaluate against technical criteria                   │   │
│  │  • Calculate Technical Score (St)                        │   │
│  │  • Check: St ≥ Minimum Score (default: 80)?             │   │
│  │    - YES → Proceed to financial evaluation               │   │
│  │    - NO  → DISQUALIFIED                                  │   │
│  └─────────────────────────────────────────────────────────┘   │
│                              ↓                                   │
│  Step 2: FINANCIAL EVALUATION                                   │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  • Identify lowest price (Fm) among qualified bids      │   │
│  │  • For each qualified bid:                               │   │
│  │    Calculate: Sf = 100 × (Fm / F)                        │   │
│  └─────────────────────────────────────────────────────────┘   │
│                              ↓                                   │
│  Step 3: COMBINED EVALUATION                                    │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  For each qualified bid:                                 │   │
│  │  • Calculate: S = (St × T%) + (Sf × P%)                  │   │
│  │  • Rank bids by Combined Score (S) descending            │   │
│  │  • Highest S = RECOMMENDED WINNER                        │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 5. Configuration Parameters

| Parameter | Description | Default Value | Configurable |
|-----------|-------------|---------------|--------------|
| `MinimumTechnicalScore` | Minimum St required to qualify | 80 | Per Tender |
| `TechnicalWeight` (T) | Weight for technical score | 60% | Per Tender |
| `FinancialWeight` (P) | Weight for financial score | 40% | Per Tender |
| `MaxTechnicalScore` | Maximum possible technical score | 100 | Fixed |
| `MaxFinancialScore` | Maximum possible financial score | 100 | Fixed |

---

## 6. Edge Cases

### No Qualified Bids
If no bids meet the minimum technical score, the tender evaluation cannot proceed. The tender may need to be re-issued or requirements revised.

### Single Qualified Bid
If only one bid qualifies technically:
- Financial Score = 100 (lowest price = itself)
- Combined Score = (St × T%) + (100 × P%)

### Equal Combined Scores
If two or more bids have identical combined scores, the following tiebreakers apply (in order):
1. Higher Technical Score (St)
2. Lower Price (F)
3. Earlier submission time

---

## 7. References

- World Bank Procurement Guidelines
- FIDIC Procurement Procedures Guide
- Government Procurement Best Practices

---

*Document Version: 1.0*  
*Last Updated: 2024*

