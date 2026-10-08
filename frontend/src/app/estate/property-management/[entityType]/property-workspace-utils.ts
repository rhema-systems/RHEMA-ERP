import {
  EstateManagedAssetStatus,
  EstateManagedAssetType,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';

export const estateAssetStatusLabels: Record<EstateManagedAssetStatus, string> = {
  [EstateManagedAssetStatus.LandBank]: 'Land bank',
  [EstateManagedAssetStatus.UnderDevelopment]: 'Under development',
  [EstateManagedAssetStatus.Available]: 'Available',
  [EstateManagedAssetStatus.Reserved]: 'Reserved',
  [EstateManagedAssetStatus.Leased]: 'Leased',
  [EstateManagedAssetStatus.Occupied]: 'Occupied',
  [EstateManagedAssetStatus.Sold]: 'Sold',
  [EstateManagedAssetStatus.Retired]: 'Retired',
  [EstateManagedAssetStatus.UnderMaintenance]: 'Under maintenance',
  [EstateManagedAssetStatus.Blocked]: 'Blocked',
};

export function formatEstateDate(value?: string | null) {
  if (!value) return 'Not recorded';
  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  }).format(new Date(value));
}

export function leaseExpiryDate(asset: EstateManagedAsset): Date | null {
  const start = asset.dateOfTenancy || asset.rightOfEntryDate;
  const months = asset.externalLeaseTermMonths ||
    (asset.leaseTermYears ? asset.leaseTermYears * 12 : 0);
  if (!start || months <= 0) return null;

  const date = new Date(start);
  if (Number.isNaN(date.getTime())) return null;
  const year = date.getUTCFullYear();
  const month = date.getUTCMonth() + months;
  const day = Math.min(
    date.getUTCDate(),
    new Date(Date.UTC(year, month + 1, 0)).getUTCDate()
  );
  return new Date(Date.UTC(year, month, day));
}

export function isFullTermLease(asset: EstateManagedAsset): boolean {
  return asset.externalListingType?.toLowerCase().includes('lease') === true;
}

export function leaseExpiryAlert(asset: EstateManagedAsset, asOf = new Date()) {
  if (!isOccupiedLike(asset)) return null;
  const expiry = leaseExpiryDate(asset);
  if (!expiry) return null;
  const today = Date.UTC(asOf.getUTCFullYear(), asOf.getUTCMonth(), asOf.getUTCDate());
  const days = Math.round((expiry.getTime() - today) / 86_400_000);
  if (days < 0) return `Expired ${-days} day${days === -1 ? '' : 's'} ago`;
  if (days === 0) return 'Expires today';
  if (days <= 90) return `Expires in ${days} day${days === 1 ? '' : 's'}`;
  return null;
}

export function formatEstateMoney(value?: number | null, currency = 'GHS') {
  if (value == null) return 'Not recorded';
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency,
  }).format(value);
}

export function propertyListingCompletionRequirements(stageName?: string | null) {
  const normalized = stageName?.trim().toLowerCase() || '';
  const isDecisionStage =
    normalized === 'management decision' ||
    normalized === 'estate decision and agreement';
  const isAgreementHandoffStage =
    normalized === 'approved transaction handoff' ||
    normalized === 'legal agreement review';

  return {
    requiresApprovedRentTerms: isDecisionStage,
    requiresGeneratedAgreement:
      normalized === 'management decision' ||
      normalized === 'estate decision and agreement' ||
      isAgreementHandoffStage,
    requiresLegalAgreementReview:
      normalized === 'management decision' || isAgreementHandoffStage,
  };
}

export function isLegalAgreementReviewSigned(status?: string | null) {
  const normalized = status?.trim().toLowerCase() || '';
  return (
    (normalized.includes('head of legal') && normalized.includes('signed')) ||
    (normalized.includes('approved by legal') &&
      normalized.includes('customer signature')) ||
    normalized.includes('fully signed')
  );
}

export function isLegalAgreementReviewCompleteForSigningLocation(
  status?: string | null,
  signingLocation?: string | null
) {
  const normalizedStatus = status?.trim().toLowerCase() || '';
  const signedByHeadOfLegal =
    (normalizedStatus.includes('head of legal') &&
      normalizedStatus.includes('signed')) ||
    normalizedStatus.includes('fully signed');

  if (!signingLocation?.toLowerCase().includes('estate')) {
    return signedByHeadOfLegal;
  }

  return isLegalAgreementReviewSigned(status);
}

export function propertyReference(asset: EstateManagedAsset) {
  return asset.projectUnitCode || asset.assetCode;
}

export function sourceReference(asset: EstateManagedAsset) {
  return asset.propertyFileReference || propertyReference(asset);
}

export function assetMatchesWorkspacePrefill(
  asset: EstateManagedAsset,
  assetId?: string | null,
  reference?: string | null
) {
  if (assetId && asset.id === assetId) return true;
  const normalizedReference = reference?.trim().toLowerCase();
  if (!normalizedReference) return false;
  return [asset.assetCode, asset.projectUnitCode, asset.propertyFileReference]
    .filter((value): value is string => Boolean(value))
    .some((value) => value.toLowerCase() === normalizedReference);
}

export function occupantName(asset: EstateManagedAsset) {
  return asset.lesseeName || 'Not linked';
}

export function isLandAsset(asset: EstateManagedAsset) {
  return asset.assetType === EstateManagedAssetType.Land;
}

export function isOccupiedLike(asset: EstateManagedAsset) {
  return (
    asset.status === EstateManagedAssetStatus.Occupied ||
    asset.status === EstateManagedAssetStatus.Leased
  );
}

export function isActiveTenantAsset(asset: EstateManagedAsset) {
  return Boolean(asset.customerBusinessPartnerId || asset.lesseeName);
}

export function buildPropertyWorkspaceHref(
  basePath: string,
  asset: EstateManagedAsset,
  titlePrefix: string,
  fields: Record<string, string | null | undefined> = {}
) {
  const reference = sourceReference(asset);
  const params = new URLSearchParams({
    assetId: asset.id,
    title: `${titlePrefix} - ${asset.name}`,
    referenceNumber: reference,
    applicantName: asset.lesseeName || '',
    sourceDepartment: 'Estate / Property Management',
    description: `${titlePrefix} action for ${propertyReference(asset)}.`,
    field_sourceWorkspace: 'Estate / Property Management',
    field_sourceReference: reference,
    field_propertyUnit: propertyReference(asset),
    field_propertyReference: propertyReference(asset),
    field_leaseReference: reference,
    field_occupantReference: asset.lesseeName || '',
    field_customerReference: asset.customerBusinessPartnerId || '',
  });

  Object.entries(fields).forEach(([key, value]) => {
    if (value != null && value !== '') {
      params.set(key.startsWith('field_') ? key : `field_${key}`, value);
    }
  });

  return `${basePath}?${params.toString()}`;
}
