// Permission alone is not banking approval authority. Fail closed until both identities are known,
// and never expose a decision to the user who submitted the deposit workflow.
export function isIndependentBankDepositReviewer(
    actorId?: string | null,
    submittedById?: string | null,
) {
    return Boolean(
        actorId &&
        submittedById &&
        actorId.toLowerCase() !== submittedById.toLowerCase(),
    );
}

// Bank acknowledgement is a separate permissioned action, but it retains the same
// maker/checker boundary enforced by BankingSettlementService. Fail closed until the
// API supplies the submitter identity.
export function canRecordBankDepositAcknowledgement(
    actorId: string | null | undefined,
    submittedById: string | null | undefined,
    hasConfirmationPermission: boolean,
) {
    return hasConfirmationPermission &&
        isIndependentBankDepositReviewer(actorId, submittedById);
}
