namespace ErpSystem.Core.Enums;

public enum ProcurementBidderCommunicationAwardFamily
{
    RequestForQuotation = 0,
    FormalTender = 1,
    ExceptionalSourcing = 2,
    LegacyTenderAward = 3
}

public enum ProcurementBidderCommunicationRecipientOutcome
{
    Successful = 0,
    Unsuccessful = 1
}

public enum ProcurementBidderCommunicationDispatchChannel
{
    Email = 0,
    Sms = 1,
    SupplierPortal = 2,
    PhysicalDelivery = 3,
    Other = 4
}

public enum ProcurementBidderCommunicationDeliveryOutcome
{
    Sent = 0,
    Delivered = 1,
    Failed = 2,
    Returned = 3
}

public enum ProcurementBidderCommunicationAcknowledgementOutcome
{
    Received = 0,
    Accepted = 1,
    Disputed = 2
}

public enum ProcurementBidderAppealOutcome
{
    Upheld = 0,
    Dismissed = 1,
    Withdrawn = 2
}

public enum ProcurementTenderSecurityInstrumentType
{
    BidBond = 0,
    BankGuarantee = 1,
    InsuranceBond = 2,
    CashDeposit = 3,
    Other = 4
}

public enum ProcurementTenderSecurityActionType
{
    Released = 0,
    Returned = 1,
    Forfeited = 2
}
