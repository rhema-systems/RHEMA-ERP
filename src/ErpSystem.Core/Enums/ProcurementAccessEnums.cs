namespace ErpSystem.Core.Enums;

public enum ProcurementWarehouseScopeMode
{
    None = 0,
    All = 1,
    Restricted = 2
}

public enum ProcurementLocationScopeMode
{
    None = 0,
    All = 1,
    Restricted = 2
}

public enum ProcurementCommitteeType
{
    EntityTenderCommittee = 0,
    CentralTenderReviewCommittee = 1,
    EvaluationCommittee = 2,
    DisposalCommittee = 3
}

public enum ProcurementCommitteeStatus
{
    Draft = 0,
    Active = 1,
    Retired = 2
}

public enum ProcurementCommitteeMemberKind
{
    Chair = 0,
    VotingMember = 1,
    NonVotingMember = 2,
    Observer = 3,
    Secretary = 4
}
