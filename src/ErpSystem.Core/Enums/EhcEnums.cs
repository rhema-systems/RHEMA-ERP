namespace ErpSystem.Core.Enums;

public enum EhcTicketType
{
    Enquiry = 1,
    Complaint = 2,
    Helpdesk = 3
}

public enum EhcTicketPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum EhcTicketSource
{
    Web = 1,
    Mobile = 2,
    Email = 3,
    Internal = 4,
    PhoneCall = 5,
    Sms = 6,
    WhatsApp = 7
}

public enum EhcTicketStatus
{
    New = 1,
    Acknowledged = 2,
    InProgress = 3,
    PendingUser = 4,
    PendingThirdParty = 5,
    Resolved = 6,
    Closed = 7,
    Reopened = 8
}

public enum EhcTicketLinkType
{
    Related = 1,
    ParentOf = 2,
    DuplicateOf = 3
}

public enum EhcProblemStatus
{
    Open = 1,
    InProgress = 2,
    Resolved = 3,
    Closed = 4
}

public enum EhcCapaTaskStatus
{
    Open = 1,
    InProgress = 2,
    Done = 3,
    Cancelled = 4
}
