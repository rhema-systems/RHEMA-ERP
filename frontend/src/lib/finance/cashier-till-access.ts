// A visible review permission is not maker-checker authority. Fail closed until both identities
// are known, and require the reviewer to be different from the cashier who held and counted cash.
export function isIndependentTillReviewer(actorId?: string | null, cashierUserId?: string | null) {
    return Boolean(
        actorId &&
        cashierUserId &&
        actorId.toLowerCase() !== cashierUserId.toLowerCase(),
    );
}

// Editing and cancellation are deliberately narrower than general till-operation permission.
// The server supplies the authoritative unused-session result; the browser also withholds the
// controls unless the authenticated actor is the cashier who opened the custody window.
export function canManageUnusedTill(
    actorId: string | null | undefined,
    cashierUserId: string | null | undefined,
    canOperate: boolean,
    openingDetailsMutable: boolean,
) {
    return Boolean(
        canOperate &&
        openingDetailsMutable &&
        actorId &&
        cashierUserId &&
        actorId.toLowerCase() === cashierUserId.toLowerCase(),
    );
}
