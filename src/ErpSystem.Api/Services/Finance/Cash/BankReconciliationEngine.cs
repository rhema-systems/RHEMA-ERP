using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Api.Services.Finance.Cash;

/// <summary>
/// Engine for auto-matching cash transactions with bank statement lines
/// </summary>
public class BankReconciliationEngine
{
    public List<MatchResult> AutoMatch(
        List<CashTransaction> transactions,
        List<BankStatementLine> statementLines)
    {
        var matches = new List<MatchResult>();

        foreach (var transaction in transactions)
        {
            var bestMatch = FindBestMatch(transaction, statementLines);
            if (bestMatch != null && bestMatch.Confidence >= 80) // 80% confidence threshold
            {
                matches.Add(bestMatch);
                statementLines.Remove(statementLines.First(l => l.Id == bestMatch.StatementLineId));
            }
        }

        return matches;
    }

    private MatchResult? FindBestMatch(CashTransaction transaction, List<BankStatementLine> statementLines)
    {
        MatchResult? bestMatch = null;
        int highestConfidence = 0;

        foreach (var line in statementLines)
        {
            var confidence = CalculateMatchConfidence(transaction, line);
            if (confidence > highestConfidence)
            {
                highestConfidence = confidence;
                bestMatch = new MatchResult
                {
                    TransactionId = transaction.Id,
                    StatementLineId = line.Id,
                    Confidence = confidence
                };
            }
        }

        return bestMatch;
    }

    private int CalculateMatchConfidence(CashTransaction transaction, BankStatementLine line)
    {
        int confidence = 0;

        // 1. Amount match (40 points)
        var transactionAmount = transaction.Amount;
        var lineAmount = line.CreditAmount > 0 ? line.CreditAmount : line.DebitAmount;

        if (transactionAmount == lineAmount)
        {
            confidence += 40;
        }
        else if (Math.Abs(transactionAmount - lineAmount) < 1) // Within 1 unit
        {
            confidence += 30;
        }
        else if (Math.Abs(transactionAmount - lineAmount) < 10) // Within 10 units
        {
            confidence += 20;
        }

        // 2. Date match (30 points)
        var dateDiff = Math.Abs((transaction.TransactionDate - line.TransactionDate).TotalDays);
        if (dateDiff == 0)
        {
            confidence += 30;
        }
        else if (dateDiff <= 1)
        {
            confidence += 25;
        }
        else if (dateDiff <= 3)
        {
            confidence += 20;
        }
        else if (dateDiff <= 7)
        {
            confidence += 10;
        }

        // 3. Reference number match (20 points)
        if (!string.IsNullOrEmpty(transaction.ReferenceNumber) && 
            !string.IsNullOrEmpty(line.ReferenceNumber))
        {
            if (transaction.ReferenceNumber.Equals(line.ReferenceNumber, StringComparison.OrdinalIgnoreCase))
            {
                confidence += 20;
            }
            else if (line.ReferenceNumber.Contains(transaction.ReferenceNumber, StringComparison.OrdinalIgnoreCase) ||
                     transaction.ReferenceNumber.Contains(line.ReferenceNumber, StringComparison.OrdinalIgnoreCase))
            {
                confidence += 15;
            }
        }

        // 4. Description similarity (10 points)
        if (!string.IsNullOrEmpty(transaction.Description) && 
            !string.IsNullOrEmpty(line.Description))
        {
            var similarity = CalculateStringSimilarity(transaction.Description, line.Description);
            confidence += (int)(similarity * 10);
        }

        return confidence;
    }

    private double CalculateStringSimilarity(string str1, string str2)
    {
        str1 = str1.ToLower();
        str2 = str2.ToLower();

        // Simple word overlap calculation
        var words1 = str1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words2 = str2.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var commonWords = words1.Intersect(words2).Count();
        var totalWords = Math.Max(words1.Length, words2.Length);

        return totalWords > 0 ? (double)commonWords / totalWords : 0;
    }
}

public class MatchResult
{
    public Guid TransactionId { get; set; }
    public Guid StatementLineId { get; set; }
    public int Confidence { get; set; } // 0-100
}
