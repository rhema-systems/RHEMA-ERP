using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Services.Procurement;

// Allow-list receipt metadata. Merely passing the deadline never opens an envelope.
internal static class ProcurementBidDisclosure
{
    internal static TenderBidSummaryDto Seal(TenderBidSummaryDto bid) => new()
    {
        Id = bid.Id, TenderId = bid.TenderId, TenderNumber = bid.TenderNumber,
        TenderTitle = bid.TenderTitle, BidNumber = bid.BidNumber,
        BusinessPartnerId = bid.BusinessPartnerId, SubmittedDate = bid.SubmittedDate,
        Status = bid.Status, IsSealed = true, IsFinancialProposalSealed = true
    };

    internal static TenderBidDetailDto Seal(TenderBidDetailDto bid) => new()
    {
        Id = bid.Id, TenderId = bid.TenderId, TenderNumber = bid.TenderNumber,
        TenderTitle = bid.TenderTitle, BidNumber = bid.BidNumber,
        BusinessPartnerId = bid.BusinessPartnerId, SubmittedDate = bid.SubmittedDate,
        Status = bid.Status, CreatedAt = bid.CreatedAt, UpdatedAt = bid.UpdatedAt,
        IsSealed = true, IsFinancialProposalSealed = true
    };

    internal static TenderBidLotDto HideFinancials(TenderBidLotDto lot)
    {
        lot.TotalLotAmount = 0m;
        lot.Currency = null;
        lot.PaymentTerms = null;
        lot.CommercialProposal = null;
        lot.PriceScore = null;
        lot.TotalScore = null;
        lot.EvaluationNotes = null;
        lot.Notes = null;
        foreach (var item in lot.Items)
        {
            item.UnitPrice = 0m;
            item.TotalPrice = 0m;
        }
        return lot;
    }
}
