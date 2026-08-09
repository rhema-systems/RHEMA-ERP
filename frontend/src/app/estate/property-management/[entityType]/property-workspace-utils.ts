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

export function formatEstateMoney(value?: number | null, currency = 'GHS') {
  if (value == null) return 'Not recorded';
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency,
  }).format(value);
}

export function propertyReference(asset: EstateManagedAsset) {
  return asset.projectUnitCode || asset.assetCode;
}

export function sourceReference(asset: EstateManagedAsset) {
  return asset.propertyFileReference || propertyReference(asset);
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
