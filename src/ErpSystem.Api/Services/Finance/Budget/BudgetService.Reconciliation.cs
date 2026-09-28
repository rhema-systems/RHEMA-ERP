using System.Text.Json;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Budget;

public partial class BudgetService
{
    public async Task<FinanceBudgetReconciliationReportDto> GetBudgetReconciliationAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var issues = new List<FinanceBudgetReconciliationIssueDto>();
        BudgetPrimaryBook? primaryBook = null;
        try
        {
            primaryBook = await BudgetPrimaryBookResolver.ResolveAsync(_context, tenantId, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            issues.Add(Issue(
                "BUDGET_PRIMARY_BOOK_CONFIGURATION",
                exception.Message,
                "Configure exactly one default Primary Full accounting book before using budget control."));
        }

        var reservations = await _context.FinanceBudgetReservations.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .OrderBy(item => item.ReservedAt)
            .ToListAsync(cancellationToken);
        var operations = await _context.FinanceBudgetReservationOperations.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        var vendorInvoiceIds = reservations
            .Where(item => item.SourceDocumentType == "VendorInvoice")
            .Select(item => item.SourceDocumentId)
            .Distinct()
            .ToArray();
        var vendorInvoiceStates = await _context.VendorInvoices.AsNoTracking()
            .Where(item => item.TenantId == tenantId && vendorInvoiceIds.Contains(item.Id) && !item.IsDeleted)
            .Select(item => new { item.Id, item.Status, item.JournalEntryId })
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var journalIds = reservations.Where(item => item.JournalEntryId.HasValue)
            .Select(item => item.JournalEntryId!.Value).Distinct().ToArray();
        var postingEventIds = reservations.Where(item => item.PostingEventId.HasValue)
            .Select(item => item.PostingEventId!.Value).Distinct().ToArray();
        var journals = await _context.JournalEntries.AsNoTracking()
            .Where(item => item.TenantId == tenantId && journalIds.Contains(item.Id) && !item.IsDeleted)
            .Select(item => new { item.Id, item.IsReversed })
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var postingEvents = await _context.FinancePostingEvents.AsNoTracking()
            .Where(item => item.TenantId == tenantId && postingEventIds.Contains(item.Id) && !item.IsDeleted)
            .Select(item => new { item.Id, item.JournalEntryId })
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var primaryActualKeys = new HashSet<(Guid JournalEntryId, Guid AccountId, Guid FiscalPeriodId)>();
        if (primaryBook is not null && journalIds.Length > 0)
        {
            var primaryActuals = await _context.AccountTransactions.AsNoTracking()
                .Where(transaction => transaction.TenantId == tenantId
                    && !transaction.IsDeleted
                    && transaction.AccountingBookId == primaryBook.Id
                    && journalIds.Contains(transaction.JournalEntryId))
                .Select(transaction => new
                {
                    transaction.JournalEntryId,
                    transaction.AccountId,
                    transaction.FiscalPeriodId
                })
                .Distinct()
                .ToListAsync(cancellationToken);
            primaryActualKeys = primaryActuals
                .Select(item => (item.JournalEntryId, item.AccountId, item.FiscalPeriodId))
                .ToHashSet();
        }

        foreach (var reservation in reservations)
        {
            if (reservation.Status == "Reserved"
                && reservation.SourceDocumentType == "VendorInvoice")
            {
                if (!vendorInvoiceStates.TryGetValue(reservation.SourceDocumentId, out var invoice))
                {
                    issues.Add(Issue(
                        "BUDGET_ORPHAN_RESERVATION_SOURCE_MISSING",
                        "An active AP budget reservation has no live vendor invoice.",
                        "Review the deleted or missing source and release the reservation through a controlled Finance correction.",
                        reservation));
                }
                else if (invoice.Status is VendorInvoiceStatus.Rejected or VendorInvoiceStatus.Voided)
                {
                    issues.Add(Issue(
                        "BUDGET_ORPHAN_RESERVATION_TERMINAL_SOURCE",
                        $"An active AP budget reservation remains against a {invoice.Status} invoice.",
                        "Replay the governed rejection/void outcome to release the reservation idempotently.",
                        reservation));
                }
            }

            if (reservation.Status != "Consumed")
                continue;

            if (!reservation.JournalEntryId.HasValue || !reservation.PostingEventId.HasValue)
            {
                issues.Add(Issue(
                    "BUDGET_CONSUMED_POSTING_EVIDENCE_MISSING",
                    "A consumed budget reservation has no complete journal and posting-event identity.",
                    "Investigate the source posting and restore immutable posting evidence before relying on availability.",
                    reservation));
                continue;
            }

            var journalExists = journals.ContainsKey(reservation.JournalEntryId.Value);
            var eventMatches = postingEvents.TryGetValue(reservation.PostingEventId.Value, out var postingEvent)
                && postingEvent.JournalEntryId == reservation.JournalEntryId;
            if (!journalExists || !eventMatches)
                issues.Add(Issue(
                    "BUDGET_CONSUMED_POSTING_LINEAGE_INVALID",
                    "A consumed budget reservation points to missing or mismatched posting evidence.",
                    "Run Finance posting-lineage diagnostics and correct the durable back-reference under change control.",
                    reservation));

            var consumeOperationExists = operations.Any(operation =>
                operation.OperationType == "ConsumeForPosting"
                && OperationContainsReservation(operation, reservation.Id)
                && operation.JournalEntryId == reservation.JournalEntryId
                && operation.PostingEventId == reservation.PostingEventId);
            if (!consumeOperationExists)
                issues.Add(Issue(
                    "BUDGET_CONSUME_OPERATION_MISSING",
                    "A consumed reservation has no matching immutable ConsumeForPosting operation.",
                    "Investigate the posting transaction; do not synthesize evidence without approved migration controls.",
                    reservation));

            if (primaryBook is not null && journalExists)
            {
                var primaryActualExists = primaryActualKeys.Contains((
                    reservation.JournalEntryId.Value,
                    reservation.AccountId,
                    reservation.FiscalPeriodId));
                if (!primaryActualExists)
                    issues.Add(Issue(
                        "BUDGET_PRIMARY_GL_ACTUAL_MISSING",
                        "A consumed reservation has no matching account-period actual in the primary accounting book.",
                        "Review the source posting classification and primary-book journal before reconciling budget availability.",
                        reservation));
            }

            if (journals.TryGetValue(reservation.JournalEntryId.Value, out var originalJournal)
                && originalJournal.IsReversed)
            {
                var reversalEvidenceExists = operations.Any(operation =>
                    operation.OperationType == "RecordActualReversal"
                    && OperationContainsReservation(operation, reservation.Id));
                if (!reversalEvidenceExists)
                    issues.Add(Issue(
                        "BUDGET_ACTUAL_REVERSAL_OPERATION_MISSING",
                        "A budget-controlled posting was reversed without immutable budget-reversal evidence.",
                        "Replay the governed source reversal or execute an approved evidence backfill after validating journal lineage.",
                        reservation));
            }
        }

        var postedControlledInvoices = await _context.VendorInvoices.AsNoTracking()
            .Where(invoice => invoice.TenantId == tenantId
                && !invoice.IsDeleted
                && invoice.JournalEntryId.HasValue
                && invoice.LineItems.Any(line => !line.IsDeleted && line.BudgetEntryId.HasValue))
            .Select(invoice => new { invoice.Id, invoice.InvoiceNumber, invoice.JournalEntryId })
            .ToListAsync(cancellationToken);
        foreach (var invoice in postedControlledInvoices)
        {
            if (reservations.Any(item => item.SourceDocumentType == "VendorInvoice"
                    && item.SourceDocumentId == invoice.Id
                    && item.Status == "Consumed"
                    && item.JournalEntryId == invoice.JournalEntryId))
                continue;
            issues.Add(new FinanceBudgetReconciliationIssueDto
            {
                Code = "BUDGET_POSTED_SOURCE_CONSUMPTION_MISSING",
                Severity = "Error",
                SourceDocumentType = "VendorInvoice",
                SourceDocumentId = invoice.Id,
                JournalEntryId = invoice.JournalEntryId,
                Message = $"Posted budget-controlled invoice {invoice.InvoiceNumber} has no consumed reservation evidence.",
                RecommendedAction = "Investigate the source submission and posting transaction before accepting the budget position."
            });
        }

        return new FinanceBudgetReconciliationReportDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            TenantId = tenantId,
            PrimaryAccountingBookId = primaryBook?.Id,
            PrimaryAccountingBookCode = primaryBook?.Code,
            ReservationCount = reservations.Count,
            ActiveReservationCount = reservations.Count(item => item.Status == "Reserved"),
            ConsumedReservationCount = reservations.Count(item => item.Status == "Consumed"),
            ReleasedReservationCount = reservations.Count(item => item.Status == "Released"),
            ErrorCount = issues.Count(item => item.Severity == "Error"),
            WarningCount = issues.Count(item => item.Severity == "Warning"),
            Issues = issues
        };
    }

    private static bool OperationContainsReservation(
        FinanceBudgetReservationOperation operation,
        Guid reservationId)
    {
        if (operation.FinanceBudgetReservationId == reservationId)
            return true;
        if (string.IsNullOrWhiteSpace(operation.ReservationIdsJson))
            return false;
        try
        {
            return JsonSerializer.Deserialize<Guid[]>(operation.ReservationIdsJson)?.Contains(reservationId) == true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static FinanceBudgetReconciliationIssueDto Issue(
        string code,
        string message,
        string action,
        FinanceBudgetReservation? reservation = null) => new()
        {
            Code = code,
            Severity = "Error",
            ReservationId = reservation?.Id,
            SourceDocumentType = reservation?.SourceDocumentType,
            SourceDocumentId = reservation?.SourceDocumentId,
            JournalEntryId = reservation?.JournalEntryId,
            PostingEventId = reservation?.PostingEventId,
            Message = message,
            RecommendedAction = action
        };
}
