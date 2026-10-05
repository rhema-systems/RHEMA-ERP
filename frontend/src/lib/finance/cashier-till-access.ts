// A visible review permission is not maker-checker authority. Fail closed until both identities
// are known, and require the reviewer to be different from the cashier who held and counted cash.
export function isIndependentTillReviewer(actorId?: string | null, cashierUserId?: string | null) {
    return Boolean(
        actorId &&
        cashierUserId &&
        actorId.toLowerCase() !== cashierUserId.toLowerCase(),
    );
}
