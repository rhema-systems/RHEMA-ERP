import type { OutboxSummary } from "@/src/offline/sync-runtime";
import type {
  MobilePosBootstrap,
  MobilePosDevice,
  MobilePosOfflineGrant,
  ServerProfile,
} from "@/src/types/api";
import { createSupportReference } from "./support-reference";

interface SupportBundleInput {
  profile: ServerProfile;
  appVersion?: string;
  bootstrap?: MobilePosBootstrap | null;
  pendingDevice?: MobilePosDevice | null;
  offlineGrant?: MobilePosOfflineGrant | null;
  outbox?: OutboxSummary | null;
  generatedAt?: Date;
  reference?: string;
}

export interface MobilePosSupportBundle {
  schemaVersion: 1;
  supportReference: string;
  generatedAtUtc: string;
  environment: string;
  apiOrigin: string;
  appVersion?: string;
  device?: {
    id: string;
    name: string;
    status: string;
    manufacturer?: string;
    model?: string;
    operatingSystemVersion?: string;
    recordedAppVersion?: string;
    printerAdapterKey?: string;
    scannerAdapterKey?: string;
    lastSeenAtUtc?: string;
    revocationEpoch: number;
  };
  assignment?: {
    storeId?: string;
    storeCode?: string;
    storeName?: string;
    tillId?: string;
    tillNumber?: string;
    currentTillSessionId?: string;
  };
  offlineAuthorization?: {
    grantId: string;
    policyId: string;
    issuedAtUtc: string;
    expiresAtUtc: string;
  };
  queue?: {
    visibleUnresolvedTotal: number;
    queryLimit: 500;
    pending: number;
    rejected: number;
    conflict: number;
    manualReview: number;
  };
  privacyNotice: string;
}

export function buildMobilePosSupportBundle(input: SupportBundleInput): MobilePosSupportBundle {
  const device = input.bootstrap?.device ?? input.pendingDevice ?? undefined;
  const store = input.bootstrap?.store;
  const till = input.bootstrap?.till;
  const generatedAt = input.generatedAt ?? new Date();
  if (!Number.isFinite(generatedAt.getTime())) throw new Error("The support-bundle timestamp is invalid.");

  return {
    schemaVersion: 1,
    supportReference: input.reference ?? createSupportReference(generatedAt.getTime()),
    generatedAtUtc: generatedAt.toISOString(),
    environment: input.profile.environment,
    apiOrigin: input.profile.apiBaseUrl,
    appVersion: input.appVersion,
    device: device ? {
      id: device.id,
      name: device.deviceName,
      status: String(device.status),
      manufacturer: device.manufacturer,
      model: device.model,
      operatingSystemVersion: device.operatingSystemVersion,
      recordedAppVersion: device.appVersion,
      printerAdapterKey: device.printerAdapterKey,
      scannerAdapterKey: device.scannerAdapterKey,
      lastSeenAtUtc: device.lastSeenAtUtc,
      revocationEpoch: device.revocationEpoch,
    } : undefined,
    assignment: device || store || till ? {
      storeId: store?.id ?? device?.mobilePosStoreId,
      storeCode: store?.code,
      storeName: store?.name ?? device?.storeName,
      tillId: till?.id ?? device?.mobilePosTillId,
      tillNumber: till?.tillNumber ?? device?.tillNumber,
      currentTillSessionId: input.bootstrap?.currentTillSessionId,
    } : undefined,
    offlineAuthorization: input.offlineGrant ? {
      grantId: input.offlineGrant.id,
      policyId: input.offlineGrant.mobilePosOfflinePolicyId,
      issuedAtUtc: input.offlineGrant.issuedAtUtc,
      expiresAtUtc: input.offlineGrant.expiresAtUtc,
    } : undefined,
    queue: input.outbox ? {
      visibleUnresolvedTotal: input.outbox.total,
      queryLimit: 500,
      pending: input.outbox.pending,
      rejected: input.outbox.rejected,
      conflict: input.outbox.conflict,
      manualReview: input.outbox.manualReview,
    } : undefined,
    privacyNotice: "This bundle excludes credentials, signed grant tokens, customer data, sale payloads, and payment details.",
  };
}
