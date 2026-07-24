namespace ErpSystem.Core.Enums;

public enum ProcurementTenderDocumentTemplateStatus
{
    Draft = 0,
    PendingApproval = 1,
    Published = 2,
    Retired = 3
}

public enum ProcurementTenderDocumentSourceType
{
    Tender = 0,
    RequestForQuotation = 1
}

public enum ProcurementTenderDocumentFeeMode
{
    Free = 0,
    Paid = 1
}

public enum ProcurementTenderDocumentChangeType
{
    Addendum = 0,
    SubmissionDeadlineExtension = 1,
    BidValidityExtension = 2
}

public enum ProcurementTenderDocumentChangeStatus
{
    PendingApproval = 0,
    Approved = 1,
    Rejected = 2
}

public enum ProcurementTenderDocumentRecipientSourceType
{
    Issuance = 0,
    TenderBid = 1,
    RequestForQuotationQuote = 2
}

public enum ProcurementTenderDocumentAcknowledgementOutcome
{
    Acknowledged = 0,
    Declined = 1
}
