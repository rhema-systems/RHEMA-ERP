import type { MobilePosBootstrap, MobilePosOfflineGrant } from "@/src/types/api";

export function isOfflineGrantUsable(
  grant: MobilePosOfflineGrant,
  bootstrap: MobilePosBootstrap,
  expectedTenantId?: string,
  now = new Date(),
): boolean {
  if (!grant || !grant.policy || typeof grant.token !== "string" || grant.token.length === 0) return false;
  const expiresAt = Date.parse(grant.expiresAtUtc);
  const issuedAt = Date.parse(grant.issuedAtUtc);
  if (!Number.isFinite(expiresAt) || !Number.isFinite(issuedAt)) return false;
  if (issuedAt > now.getTime() + 60_000 || expiresAt <= now.getTime()) return false;
  if (expectedTenantId && grant.tenantId !== expectedTenantId) return false;

  return grant.userId === bootstrap.userId
    && grant.mobilePosDeviceId === bootstrap.device.id
    && grant.mobilePosStoreId === bootstrap.store.id
    && grant.mobilePosTillId === bootstrap.till.id
    && grant.cashierTillSessionId === bootstrap.currentTillSessionId
    && grant.mobilePosOfflinePolicyId === bootstrap.offlinePolicy?.id
    && grant.revocationEpoch === bootstrap.device.revocationEpoch
    && grant.policy.policyId === bootstrap.offlinePolicy?.id
    && grant.policy.defaultWalkInBusinessPartnerId === bootstrap.store.defaultWalkInBusinessPartnerId
    && grant.policy.defaultWalkInBusinessPartnerRoleId === bootstrap.store.defaultWalkInBusinessPartnerRoleId;
}

export function offlineGrantMinutesRemaining(grant: MobilePosOfflineGrant, now = new Date()): number {
  const expiresAt = Date.parse(grant.expiresAtUtc);
  if (!Number.isFinite(expiresAt)) return 0;
  return Math.max(0, Math.ceil((expiresAt - now.getTime()) / 60_000));
}
