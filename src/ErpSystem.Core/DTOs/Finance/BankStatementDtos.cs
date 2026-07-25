using System;
using System.Collections.Generic;

namespace ErpSystem.Core.DTOs.Finance;

public class BankStatementDto
{
    public Guid Id { get; set; }
    public Guid BankAccountId { get; set; }
    public DateTime StatementDate { get; set; }
    public string? StatementNumber { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public DateTime ImportedAt { get; set; }
    public Guid? ImportedBy { get; set; }
    public string? Notes { get; set; }
}

public class BankStatementLineDto
{
    public Guid Id { get; set; }
    public Guid BankStatementId { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime? ValueDate { get; set; }
    public string? Description { get; set; }
    public string? ReferenceNumber { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal Balance { get; set; }
    public bool IsMatched { get; set; }
    public Guid? MatchedTransactionId { get; set; }
    public Guid? ReconciliationMatchId { get; set; }
}
