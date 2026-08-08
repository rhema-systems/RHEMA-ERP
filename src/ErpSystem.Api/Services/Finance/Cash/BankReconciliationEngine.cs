using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;

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
        // Matching needs a shrinking candidate set to prevent one statement line from being
        // selected twice. Keep that set private: the service still needs its original tracked
        // rows after this method returns so it can mark each selected line as reconciled.
        var availableStatementLines = statementLines.ToList();

        foreach (var transaction in transactions)
        {
            var bestMatch = FindBestMatch(transaction, availableStatementLines);
            if (bestMatch != null && bestMatch.Confidence >= 80) // 80% confidence threshold
            {
                matches.Add(bestMatch);
                availableStatementLines.Remove(
                    availableStatementLines.First(l => l.Id == bestMatch.StatementLineId));
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
        if (!IsDirectionCompatible(transaction, line))
        {
            return 0;
        }

        int confidence = 0;

        // 1. Amount match (40 points)
        var transactionAmount = transaction.Amount;
        var lineAmount = GetExpectedStatementAmount(transaction, line);

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

    public static bool IsDirectionCompatible(CashTransaction transaction, BankStatementLine line)
    {
        return transaction.TransactionType switch
        {
            CashTransactionType.Receipt => line.CreditAmount > 0m && line.DebitAmount == 0m,
            CashTransactionType.Deposit => line.CreditAmount > 0m && line.DebitAmount == 0m,
            CashTransactionType.Payment => line.DebitAmount > 0m && line.CreditAmount == 0m,
            CashTransactionType.ReturnedCheque => line.DebitAmount > 0m && line.CreditAmount == 0m,
            CashTransactionType.Transfer when IsOutgoingTransferLeg(transaction)
                => line.DebitAmount > 0m && line.CreditAmount == 0m,
            CashTransactionType.Transfer when IsIncomingTransferLeg(transaction)
                => line.CreditAmount > 0m && line.DebitAmount == 0m,
            _ => false
        };
    }

    public static decimal GetExpectedStatementAmount(CashTransaction transaction, BankStatementLine line)
    {
        return transaction.TransactionType switch
        {
            CashTransactionType.Receipt => line.CreditAmount,
            CashTransactionType.Deposit => line.CreditAmount,
            CashTransactionType.Payment => line.DebitAmount,
            CashTransactionType.ReturnedCheque => line.DebitAmount,
            CashTransactionType.Transfer when IsOutgoingTransferLeg(transaction)
                => line.DebitAmount,
            CashTransactionType.Transfer when IsIncomingTransferLeg(transaction)
                => line.CreditAmount,
            _ => 0m
        };
    }

    private static bool IsOutgoingTransferLeg(CashTransaction transaction)
        // Explicit lineage is authoritative for every newly captured transfer. The suffix check
        // is retained only so already-seeded development rows remain visible to reconciliation.
        => transaction.TransferLeg == BankTransferLeg.Outgoing
            || (!transaction.TransferLeg.HasValue
                && transaction.TransactionNumber.EndsWith("-OUT", StringComparison.OrdinalIgnoreCase));

    private static bool IsIncomingTransferLeg(CashTransaction transaction)
        => transaction.TransferLeg == BankTransferLeg.Incoming
            || (!transaction.TransferLeg.HasValue
                && transaction.TransactionNumber.EndsWith("-IN", StringComparison.OrdinalIgnoreCase));

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
