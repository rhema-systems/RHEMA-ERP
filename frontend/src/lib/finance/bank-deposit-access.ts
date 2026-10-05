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
