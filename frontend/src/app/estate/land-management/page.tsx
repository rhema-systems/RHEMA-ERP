'use client';

import React from 'react';
import dynamic from 'next/dynamic';
import Link from 'next/link';
import {
  ArrowRight,
  BadgeCheck,
  Building2,
  ChevronLeft,
  ChevronRight,
  ExternalLink,
  Globe2,
  Landmark,
  Link2,
  Loader2,
  MapPin,
  Plus,
  Search,
} from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { useAuth } from '@/hooks/use-auth';
import {
  estateLandManagementService,
  EstateManagedAssetSourceType,
  EstateManagedAssetStatus,
  type EstateLandDemarcation,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';
import {
  estateAcquisitionService,
  type LandAcquisitionItem,
} from '@/services/estate-acquisition.service';
import ExistingLandDialog from './ExistingLandDialog';
import DemarcateLandDialog from './DemarcateLandDialog';
import GisAssetLinkDialog from './GisAssetLinkDialog';
import LandDocumentsPanel from './LandDocumentsPanel';

const LandBankMap = dynamic(() => import('./LandBankMap'), { ssr: false });
const LAND_CARD_PAGE_SIZE = 9;
const GIS_LINK_ROLES = [
  'admin',
  'Admin',
  'SystemAdmin',
  'SuperAdmin',
  'TenantAdmin',
  'Estate Manager',
  'Land Registry Officer',
  'Survey Officer',
];
type LandManagementRecord =
  | { key: string; type: 'asset'; asset: EstateManagedAsset }
  | { key: string; type: 'acquisition'; acquisition: LandAcquisitionItem };

function formatArea(value?: number) {
  if (value == null) return 'Not recorded';
  return `${value.toLocaleString(undefined, { maximumFractionDigits: 2 })} sqm`;
}

function formatMoney(value?: number, currency = 'GHS') {
  if (value == null) return 'Not recorded';
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency,
  }).format(value);
}

function sourceLabel(sourceType: EstateManagedAssetSourceType) {
  if (sourceType === EstateManagedAssetSourceType.LandAcquisition)
    return 'Land acquisition';
  if (sourceType === EstateManagedAssetSourceType.ProjectUnit)
    return 'Project unit';
  return 'Manual';
}

function acquisitionMatches(item: LandAcquisitionItem, query?: string) {
  const normalized = query?.trim().toLowerCase();
  if (!normalized) return true;
  return [
    item.projectReference,
    item.location,
    item.currentStage,
    item.status,
    item.intendedUse,
    item.ownerName,
    item.acquisitionType,
  ]
    .filter(Boolean)
    .some((value) => `${value}`.toLowerCase().includes(normalized));
}

function acquisitionDemarcationHref(item: LandAcquisitionItem) {
  const stage = item.stageOrder >= 2 ? 2 : item.stageOrder;
  return `/estate/land-acquisition?acquisitionId=${encodeURIComponent(item.id)}&stage=${stage}`;
}

function acquisitionHasDemarcation(item: LandAcquisitionItem) {
  return item.stageOrder >= 2;
}

function acquisitionBoundaryVerified(item: LandAcquisitionItem) {
  return item.stageOrder > 3;
}

function acquisitionReadyForLandBank(item: LandAcquisitionItem) {
  return item.stageOrder >= 15 && item.stageInputsComplete;
}

function StatCard({
  title,
  value,
  icon: Icon,
}: {
  title: string;
  value: string | number;
  icon: React.ComponentType<{ className?: string }>;
}) {
  return (
    <Card className="border-border bg-card text-card-foreground">
      <CardContent className="flex items-center justify-between gap-4 p-5">
        <div>
          <p className="text-sm text-muted-foreground">{title}</p>
          <p className="mt-1 text-2xl font-semibold">{value}</p>
        </div>
        <div className="flex h-11 w-11 items-center justify-center rounded-md border bg-muted">
          <Icon className="h-5 w-5 text-teal-700 dark:text-teal-300" />
        </div>
      </CardContent>
    </Card>
  );
}

function DetailRow({
  label,
  value,
}: {
  label: string;
  value?: React.ReactNode;
}) {
  return (
    <div className="rounded-md border bg-background p-3">
      <p className="text-xs font-medium uppercase tracking-normal text-muted-foreground">
        {label}
      </p>
      <div className="mt-1 text-sm font-medium text-foreground">
        {value || 'Not recorded'}
      </div>
    </div>
  );
}

export default function EstateLandManagementPage() {
  const { hasAnyRole, hasPermission } = useAuth();
  const canLinkGis = hasAnyRole(GIS_LINK_ROLES);
  const canMarkProjectReady = hasPermission(
    'estate.land.project-readiness'
  );
  const [assets, setAssets] = React.useState<EstateManagedAsset[]>([]);
  const [acquisitions, setAcquisitions] = React.useState<LandAcquisitionItem[]>(
    []
  );
  const [selectedKey, setSelectedKey] = React.useState<string | null>(null);
  const [search, setSearch] = React.useState('');
  const [recordPage, setRecordPage] = React.useState(1);
  const [isLoading, setIsLoading] = React.useState(true);
  const [selectedDemarcations, setSelectedDemarcations] = React.useState<
    EstateLandDemarcation[]
  >([]);
  const [existingLandOpen, setExistingLandOpen] = React.useState(false);
  const [demarcationAsset, setDemarcationAsset] =
    React.useState<EstateManagedAsset | null>(null);
  const [gisLinkAsset, setGisLinkAsset] =
    React.useState<EstateManagedAsset | null>(null);
  const [markingReadyKey, setMarkingReadyKey] = React.useState<string | null>(
    null
  );

  const records = React.useMemo<LandManagementRecord[]>(() => {
    const publishedAcquisitionIds = new Set(
      assets
        .map((asset) => asset.landAcquisitionId)
        .filter((id): id is string => Boolean(id))
    );
    return [
      ...assets.map((asset) => ({
        key: `asset:${asset.id}`,
        type: 'asset' as const,
        asset,
      })),
      ...acquisitions
        .filter((item) => !publishedAcquisitionIds.has(item.id))
        .map((acquisition) => ({
          key: `acquisition:${acquisition.id}`,
          type: 'acquisition' as const,
          acquisition,
        })),
    ];
  }, [acquisitions, assets]);

  const selected = React.useMemo(
    () => records.find((record) => record.key === selectedKey) || records[0],
    [records, selectedKey]
  );
  const recordPageCount = Math.max(
    1,
    Math.ceil(records.length / LAND_CARD_PAGE_SIZE)
  );
  const pagedRecords = React.useMemo(
    () =>
      records.slice(
        (recordPage - 1) * LAND_CARD_PAGE_SIZE,
        recordPage * LAND_CARD_PAGE_SIZE
      ),
    [recordPage, records]
  );

  React.useEffect(() => {
    if (
      pagedRecords.length &&
      !pagedRecords.some((record) => record.key === selectedKey)
    ) {
      setSelectedKey(pagedRecords[0].key);
    }
  }, [pagedRecords, selectedKey]);

  React.useEffect(() => {
    setRecordPage((current) => Math.min(current, recordPageCount));
  }, [recordPageCount]);

  React.useEffect(() => {
    let active = true;
    if (selected?.type !== 'asset') {
      setSelectedDemarcations([]);
      return;
    }

    setSelectedDemarcations([]);
    void estateLandManagementService
      .getLandDemarcations(selected.asset.id)
      .then((items) => {
        if (active) setSelectedDemarcations(items);
      })
      .catch(() => {
        if (active) setSelectedDemarcations([]);
      });

    return () => {
      active = false;
    };
  }, [selected]);

  const loadLandRecords = React.useCallback(async (query?: string) => {
    setIsLoading(true);
    try {
      const [data, acquisitionBoard] = await Promise.all([
        estateLandManagementService.getLandBank(query),
        estateAcquisitionService.getAcquisitionWorkflowBoard().catch(() => ({
          stages: [],
        })),
      ]);
      const acquisitionItems = acquisitionBoard.stages
        .flatMap((stage) => stage.items)
        .filter((item) => acquisitionMatches(item, query));
      setAssets(data);
      setAcquisitions(acquisitionItems);
      const nextKeys = [
        ...data.map((asset) => `asset:${asset.id}`),
        ...acquisitionItems.map((item) => `acquisition:${item.id}`),
      ];
      setSelectedKey((current) =>
        current && nextKeys.includes(current)
          ? current
          : (nextKeys[0] ?? null)
      );
    } catch (error) {
      console.error('Failed to load land records', error);
      toast.error('Unable to load land management records');
    } finally {
      setIsLoading(false);
    }
  }, []);

  React.useEffect(() => {
    void loadLandRecords();
  }, [loadLandRecords]);

  const acquisitionCount = records.filter(
    (record) =>
      record.type === 'acquisition' ||
      record.asset.sourceType === EstateManagedAssetSourceType.LandAcquisition
  ).length;
  const verifiedCount = records.filter(
    (record) =>
      record.type === 'asset'
        ? record.asset.boundaryVerified
        : acquisitionBoundaryVerified(record.acquisition)
  ).length;
  const selectedAsset = selected?.type === 'asset' ? selected.asset : null;
  const saleListingDisabledReason = !selectedAsset
    ? undefined
    : selectedAsset.status !== EstateManagedAssetStatus.LandBank
      ? 'Land already assigned to a project cannot be listed for sale.'
      : !selectedAsset.boundaryVerified
        ? 'Verify the main cadastral boundary first.'
        : selectedAsset.demarcationCount === 0
          ? 'Add at least one demarcation first.'
          : selectedAsset.verifiedDemarcationCount !==
              selectedAsset.demarcationCount
            ? 'Verify every demarcation first.'
            : undefined;
  const saleListingLabel =
    selectedAsset?.isPublishedToExternalPortal &&
    (selectedAsset.externalListingType === 'Sale' ||
      selectedAsset.externalListingType === 'SaleAndRent')
      ? 'View Sale Listing'
      : 'List Land for Sale';

  const markAssetProjectReady = async (asset: EstateManagedAsset) => {
    const key = `asset:${asset.id}`;
    try {
      setMarkingReadyKey(key);
      await estateLandManagementService.markReadyForProjectManagement(asset.id);
      toast.success('Whole land is demarcated and ready for project management.');
      await loadLandRecords(search);
    } catch (error: any) {
      toast.error(error?.message || 'Unable to make land ready.');
    } finally {
      setMarkingReadyKey(null);
    }
  };

  const publishAcquisitionToLandBank = async (item: LandAcquisitionItem) => {
    const key = `acquisition:${item.id}`;
    try {
      setMarkingReadyKey(key);
      const result = await estateAcquisitionService.publishToLandBank(item.id);
      if (!result.success) {
        throw new Error(result.message || 'Unable to publish land.');
      }
      toast.success(
        result.message || 'Land asset has been published to Estate Land Bank.'
      );
      await loadLandRecords(search);
      if (result.asset?.id) {
        setSelectedKey(`asset:${result.asset.id}`);
      }
    } catch (error: any) {
      toast.error(
        error?.message ||
          'Unable to publish this acquisition into Estate Land Bank.'
      );
    } finally {
      setMarkingReadyKey(null);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="space-y-2">
          <Badge variant="outline" className="w-fit">
            Estate operations
          </Badge>
          <div>
            <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
              Land Management
            </h1>
            <p className="mt-2 max-w-3xl text-sm text-muted-foreground">
              Land bank records made available after cadastral demarcation and
              acquisition completion, ready for project management planning.
            </p>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/estate/gis">
              <Globe2 className="mr-2 h-4 w-4" />
              GIS Integration
            </Link>
          </Button>
          <Button variant="outline" onClick={() => setExistingLandOpen(true)}>
            <Plus className="mr-2 h-4 w-4" />
            Add Existing Land
          </Button>
          <Button asChild variant="outline">
            <Link href="/estate/land-acquisition">
              <Landmark className="mr-2 h-4 w-4" />
              Land Acquisition
            </Link>
          </Button>
          <Button asChild>
            <Link href="/development/projects">
              <Building2 className="mr-2 h-4 w-4" />
              Project Management
            </Link>
          </Button>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <StatCard
          title="Land bank records"
          value={assets.length}
          icon={Landmark}
        />
        <StatCard
          title="From acquisition"
          value={acquisitionCount}
          icon={ArrowRight}
        />
        <StatCard
          title="Boundary verified"
          value={verifiedCount}
          icon={BadgeCheck}
        />
      </div>

      <div className="space-y-6">
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader className="flex flex-row items-start justify-between gap-4">
            <div>
              <CardTitle className="text-base">Land Bank</CardTitle>
              <CardDescription>
                Demarcated lands available for project planning.
              </CardDescription>
            </div>
            <Badge variant="outline" className="shrink-0">
              {records.length} records
            </Badge>
          </CardHeader>
          <CardContent className="space-y-4">
            <form
              className="flex gap-2"
              onSubmit={(event) => {
                event.preventDefault();
                setRecordPage(1);
                void loadLandRecords(search);
              }}
            >
              <div className="relative flex-1">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Search land bank or acquisition"
                  className="pl-9"
                />
              </div>
              <Button
                type="submit"
                variant="outline"
                size="icon"
                aria-label="Search land bank"
              >
                <Search className="h-4 w-4" />
              </Button>
            </form>

            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              {isLoading ? (
                <div className="col-span-full flex items-center justify-center gap-2 rounded-md border py-12 text-sm text-muted-foreground">
                  <Loader2 className="h-4 w-4 animate-spin" />
                  Loading land bank
                </div>
              ) : null}

              {!isLoading && records.length === 0 ? (
                <div className="col-span-full rounded-md border py-12 text-center text-sm text-muted-foreground">
                  No land bank or acquisition record matched your search.
                </div>
              ) : null}

              {/* Keep several records visible while selection drives the workspace below. */}
              {!isLoading &&
                pagedRecords.map((record) => {
                  const active = selected?.key === record.key;
                  const title =
                    record.type === 'asset'
                      ? record.asset.name
                      : record.acquisition.projectReference;
                  const subtitle =
                    record.type === 'asset'
                      ? record.asset.assetCode
                      : record.acquisition.currentStage;
                  const location =
                    record.type === 'asset'
                      ? record.asset.location
                      : record.acquisition.location;
                  const verified =
                    record.type === 'asset'
                      ? record.asset.boundaryVerified
                      : acquisitionBoundaryVerified(record.acquisition);
                  const area =
                    record.type === 'asset'
                      ? formatArea(record.asset.areaSquareMeters)
                      : record.acquisition.estimatedSize || 'Area not recorded';
                  return (
                    <button
                      key={record.key}
                      type="button"
                      onClick={() => setSelectedKey(record.key)}
                      className={`flex min-h-36 w-full flex-col rounded-md border p-4 text-left transition-colors ${
                        active
                          ? 'border-teal-600 bg-teal-50 text-teal-950 dark:bg-teal-950/30 dark:text-teal-100'
                          : 'bg-background hover:bg-muted'
                      }`}
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0">
                          <p className="truncate text-sm font-semibold">
                            {title}
                          </p>
                          <p className="mt-1 truncate text-xs text-muted-foreground">
                            {subtitle}
                          </p>
                        </div>
                        {verified ? (
                          <Badge variant="secondary" className="shrink-0">
                            Verified
                          </Badge>
                        ) : record.type === 'acquisition' ? (
                          <Badge variant="outline" className="shrink-0">
                            Acquisition
                          </Badge>
                        ) : null}
                      </div>
                      <div className="mt-auto flex items-center gap-2 pt-4 text-xs text-muted-foreground">
                        <MapPin className="h-3.5 w-3.5 shrink-0" />
                        <span className="truncate">
                          {location || 'Location not recorded'}
                        </span>
                      </div>
                      <div className="mt-2 flex items-center justify-between gap-3 text-xs text-muted-foreground">
                        <span className="truncate">{area}</span>
                        <span className="shrink-0">
                          {record.type === 'asset' &&
                          record.asset.demarcationCount > 0
                            ? `${record.asset.demarcationCount} demarcation${record.asset.demarcationCount === 1 ? '' : 's'}`
                            : record.type === 'asset'
                              ? sourceLabel(record.asset.sourceType)
                              : 'Acquisition'}
                        </span>
                      </div>
                    </button>
                  );
                })}
            </div>
            {recordPageCount > 1 ? (
              <div className="flex items-center justify-between border-t pt-4">
                <p className="text-sm text-muted-foreground">
                  Page {recordPage} of {recordPageCount}
                </p>
                <div className="flex gap-1">
                  <Button
                    type="button"
                    size="icon"
                    variant="outline"
                    aria-label="Previous land records"
                    disabled={recordPage === 1}
                    onClick={() =>
                      setRecordPage((current) => Math.max(1, current - 1))
                    }
                  >
                    <ChevronLeft className="h-4 w-4" />
                  </Button>
                  <Button
                    type="button"
                    size="icon"
                    variant="outline"
                    aria-label="Next land records"
                    disabled={recordPage === recordPageCount}
                    onClick={() =>
                      setRecordPage((current) =>
                        Math.min(recordPageCount, current + 1)
                      )
                    }
                  >
                    <ChevronRight className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            ) : null}
          </CardContent>
        </Card>

        <Card className="border-border bg-card text-card-foreground">
          <CardHeader className="space-y-3">
            <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
              <div>
                <CardTitle className="text-lg">
                  {selected?.type === 'asset'
                    ? selected.asset.name
                    : selected?.acquisition.projectReference ||
                      'Select a land record'}
                </CardTitle>
                <CardDescription className="mt-1">
                  {selected?.type === 'asset'
                    ? `${selected.asset.assetCode} - ${sourceLabel(selected.asset.sourceType)}`
                    : selected?.type === 'acquisition'
                      ? `${selected.acquisition.currentStage} - ${selected.acquisition.status}`
                    : 'Project management can pull from records shown here.'}
                </CardDescription>
              </div>
              {selected?.type === 'acquisition' ? (
                <div className="flex flex-wrap gap-2">
                  <Button asChild variant="outline">
                    <Link
                      href={acquisitionDemarcationHref(selected.acquisition)}
                    >
                      <ExternalLink className="mr-2 h-4 w-4" />
                      {acquisitionHasDemarcation(selected.acquisition)
                        ? 'Review Demarcation'
                        : 'Demarcate'}
                    </Link>
                  </Button>
                  <Button
                    variant="outline"
                    disabled={
                      !acquisitionReadyForLandBank(selected.acquisition) ||
                      markingReadyKey === `acquisition:${selected.acquisition.id}`
                    }
                    onClick={() =>
                      void publishAcquisitionToLandBank(selected.acquisition)
                    }
                    title={
                      acquisitionReadyForLandBank(selected.acquisition)
                        ? undefined
                        : 'Complete the acquisition workflow through Asset Creation before publishing this land to Estate Land Bank.'
                    }
                  >
                    {markingReadyKey ===
                    `acquisition:${selected.acquisition.id}` ? (
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    ) : (
                      <Building2 className="mr-2 h-4 w-4" />
                    )}
                    Publish to Land Bank
                  </Button>
                </div>
              ) : selected?.type === 'asset' ? (
                <div className="flex flex-wrap gap-2">
                  {canLinkGis ? (
                    <Button
                      variant="outline"
                      onClick={() => setGisLinkAsset(selected.asset)}
                    >
                      <Link2 className="mr-2 h-4 w-4" />
                      {selected.asset.gisFeatureId ? 'GIS Link' : 'Link GIS'}
                    </Button>
                  ) : null}
                  <Button
                    variant="outline"
                    onClick={() => setDemarcationAsset(selected.asset)}
                  >
                    <MapPin className="mr-2 h-4 w-4" />
                    {selected.asset.demarcationCount
                      ? 'Manage Demarcations'
                      : 'Add Demarcation'}
                  </Button>
                  {saleListingDisabledReason ? (
                    <Button
                      variant="outline"
                      disabled
                      title={saleListingDisabledReason}
                    >
                      <Globe2 className="mr-2 h-4 w-4" />
                      {saleListingLabel}
                    </Button>
                  ) : (
                    <Button asChild variant="outline">
                      <Link
                        href={`/estate/property-management/listings?assetId=${encodeURIComponent(selected.asset.id)}&listingType=Sale`}
                      >
                        <Globe2 className="mr-2 h-4 w-4" />
                        {saleListingLabel}
                      </Link>
                    </Button>
                  )}
                  {selected.asset.isReadyForProjectManagement ? (
                    <Button asChild variant="outline">
                      <Link href="/development/projects">
                        <ExternalLink className="mr-2 h-4 w-4" />
                        Project Pull Ready
                      </Link>
                    </Button>
                  ) : (
                    <Button
                      variant="outline"
                      disabled={
                        !canMarkProjectReady ||
                        selected.asset.isPublishedToExternalPortal ||
                        !selected.asset.boundaryVerified ||
                        selected.asset.demarcationCount === 0 ||
                        selected.asset.verifiedDemarcationCount !==
                          selected.asset.demarcationCount ||
                        markingReadyKey === `asset:${selected.asset.id}`
                      }
                      onClick={() => void markAssetProjectReady(selected.asset)}
                      title={
                        !canMarkProjectReady
                          ? 'Requires the Mark Land Project Ready permission assigned in Administration.'
                          : selected.asset.isPublishedToExternalPortal
                            ? 'Withdraw the active external land listing before marking this land ready for a project.'
                            : !selected.asset.boundaryVerified
                              ? 'Verify the main cadastral boundary first.'
                              : selected.asset.demarcationCount === 0
                                ? 'Add at least one demarcation first.'
                                : selected.asset.verifiedDemarcationCount !==
                                    selected.asset.demarcationCount
                                  ? 'Verify every demarcation first.'
                                  : undefined
                      }
                    >
                      {markingReadyKey === `asset:${selected.asset.id}` ? (
                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      ) : (
                        <Building2 className="mr-2 h-4 w-4" />
                      )}
                      Mark Demarcated & Ready
                    </Button>
                  )}
                </div>
              ) : selected ? (
                null
              ) : null}
            </div>
          </CardHeader>
          <CardContent className="space-y-5">
            {selected?.type === 'asset' ? (
              <>
                <LandBankMap
                  assetId={selected.asset.id}
                  boundaryCoordinates={selected.asset.boundaryCoordinates}
                  demarcations={selectedDemarcations.map((item) => ({
                    id: item.id,
                    description: `Parcel ${item.demarcationNumber}: ${item.description}`,
                    boundaryCoordinates: item.boundaryCoordinates,
                  }))}
                  gisFeatureId={selected.asset.gisFeatureId}
                  gisLayerReference={selected.asset.gisLayerReference}
                  gisProvider={selected.asset.gisProvider}
                />

                <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                  <DetailRow label="Location" value={selected.asset.location} />
                  <DetailRow
                    label="Area"
                    value={formatArea(selected.asset.areaSquareMeters)}
                  />
                  <DetailRow
                    label="Valuation"
                    value={formatMoney(
                      selected.asset.valuationAmount,
                      selected.asset.currency
                    )}
                  />
                  <DetailRow label="Purpose" value={selected.asset.purpose} />
                  <DetailRow
                    label="Zoning"
                    value={selected.asset.zoningClassification}
                  />
                  <DetailRow
                    label="Planning status"
                    value={selected.asset.planningComplianceStatus}
                  />
                  <DetailRow
                    label="GIS layer"
                    value={selected.asset.gisLayerReference}
                  />
                  <DetailRow
                    label="GIS provider"
                    value={selected.asset.gisProvider}
                  />
                  <DetailRow
                    label="GIS feature"
                    value={selected.asset.gisFeatureId}
                  />
                  <DetailRow
                    label="GIS sync"
                    value={selected.asset.gisSyncStatus}
                  />
                  <DetailRow
                    label="Survey plan"
                    value={selected.asset.surveyPlanNumber}
                  />
                  <DetailRow
                    label="Map sheet"
                    value={selected.asset.mapSheetNumber}
                  />
                  <DetailRow
                    label="Surveyor"
                    value={selected.asset.surveyorName}
                  />
                  <DetailRow
                    label="Survey date"
                    value={
                      selected.asset.surveyDate
                        ? new Date(
                            selected.asset.surveyDate
                          ).toLocaleDateString()
                        : undefined
                    }
                  />
                  <DetailRow
                    label="Region / District"
                    value={[selected.asset.region, selected.asset.district]
                      .filter(Boolean)
                      .join(' / ')}
                  />
                  <DetailRow
                    label="Beacon count"
                    value={selected.asset.beaconCount?.toString()}
                  />
                  <DetailRow
                    label="Demarcations"
                    value={selected.asset.demarcationCount.toString()}
                  />
                </div>

                {selected.asset.ownershipHistory?.length ? (
                  <div className="overflow-hidden rounded-md border bg-background">
                    <div className="border-b px-4 py-3">
                      <p className="text-sm font-semibold">Ownership History</p>
                    </div>
                    <div className="divide-y">
                      {selected.asset.ownershipHistory.map((owner, index) => (
                        <div
                          key={`${owner.ownerName}-${index}`}
                          className="grid gap-2 px-4 py-3 text-sm md:grid-cols-4"
                        >
                          <span className="font-medium">{owner.ownerName}</span>
                          <span>{owner.ownershipType}</span>
                          <span>{owner.interestHeld}</span>
                          <span>
                            {owner.isCurrentOwner
                              ? 'Current owner'
                              : `${owner.ownershipStartDate} - ${owner.ownershipEndDate || 'Not recorded'}`}
                          </span>
                        </div>
                      ))}
                    </div>
                  </div>
                ) : null}

                <LandDocumentsPanel assetId={selected.asset.id} />

                {selected.asset.notes ? (
                  <div className="rounded-md border bg-background p-4">
                    <p className="text-sm font-semibold">Notes</p>
                    <p className="mt-2 text-sm leading-6 text-muted-foreground">
                      {selected.asset.notes}
                    </p>
                  </div>
                ) : null}
              </>
            ) : selected?.type === 'acquisition' ? (
              <div className="space-y-5">
                <div className="rounded-md border bg-background p-4">
                  <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                    <div>
                      <p className="text-sm font-semibold">
                        Acquisition land record
                      </p>
                      <p className="mt-1 text-sm text-muted-foreground">
                        Open this record to complete or review cadastral
                        demarcation before it is pushed to the land bank.
                      </p>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <Button asChild variant="outline">
                        <Link
                          href={acquisitionDemarcationHref(
                            selected.acquisition
                          )}
                        >
                          <ExternalLink className="mr-2 h-4 w-4" />
                          {acquisitionHasDemarcation(selected.acquisition)
                            ? 'Review Demarcation'
                            : 'Demarcate'}
                        </Link>
                      </Button>
                      <Button
                        disabled={
                          !acquisitionReadyForLandBank(selected.acquisition) ||
                          markingReadyKey ===
                            `acquisition:${selected.acquisition.id}`
                        }
                        onClick={() =>
                          void publishAcquisitionToLandBank(
                            selected.acquisition
                          )
                        }
                        title={
                          acquisitionReadyForLandBank(selected.acquisition)
                            ? undefined
                            : 'Complete the acquisition workflow through Asset Creation before publishing this land to Estate Land Bank.'
                        }
                      >
                        {markingReadyKey ===
                        `acquisition:${selected.acquisition.id}` ? (
                          <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                        ) : (
                          <Building2 className="mr-2 h-4 w-4" />
                        )}
                        Publish to Land Bank
                      </Button>
                    </div>
                  </div>
                </div>

                <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                  <DetailRow
                    label="Project reference"
                    value={selected.acquisition.projectReference}
                  />
                  <DetailRow
                    label="Location"
                    value={selected.acquisition.location}
                  />
                  <DetailRow
                    label="Current stage"
                    value={selected.acquisition.currentStage}
                  />
                  <DetailRow
                    label="Status"
                    value={selected.acquisition.status}
                  />
                  <DetailRow
                    label="Intended use"
                    value={selected.acquisition.intendedUse}
                  />
                  <DetailRow
                    label="Estimated size"
                    value={selected.acquisition.estimatedSize}
                  />
                  <DetailRow
                    label="Documents"
                    value={selected.acquisition.documents.toString()}
                  />
                  <DetailRow
                    label="Stage inputs"
                    value={
                      selected.acquisition.stageInputsComplete
                        ? 'Complete'
                        : `${selected.acquisition.missingInputs.length} missing`
                    }
                  />
                </div>
              </div>
            ) : (
              <div className="rounded-md border py-16 text-center text-sm text-muted-foreground">
                Select a land bank record to view its demarcation and planning
                details.
              </div>
            )}
          </CardContent>
        </Card>
      </div>
      <ExistingLandDialog
        open={existingLandOpen}
        onOpenChange={setExistingLandOpen}
        onCreated={async (asset) => {
          await loadLandRecords(search);
          setSelectedKey(`asset:${asset.id}`);
        }}
      />
      <DemarcateLandDialog
        asset={demarcationAsset}
        open={Boolean(demarcationAsset)}
        onOpenChange={(open) => {
          if (!open) setDemarcationAsset(null);
        }}
        onSaved={async () => {
          const assetId = demarcationAsset?.id;
          await loadLandRecords(search);
          if (assetId) {
            setSelectedKey(`asset:${assetId}`);
            setSelectedDemarcations(
              await estateLandManagementService.getLandDemarcations(assetId)
            );
          }
        }}
      />
      <GisAssetLinkDialog
        asset={gisLinkAsset}
        open={Boolean(gisLinkAsset)}
        onOpenChange={(open) => {
          if (!open) setGisLinkAsset(null);
        }}
        onLinked={async () => {
          const assetId = gisLinkAsset?.id;
          await loadLandRecords(search);
          if (assetId) setSelectedKey(`asset:${assetId}`);
        }}
      />
    </div>
  );
}
