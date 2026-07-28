'use client';

import React from 'react';
import dynamic from 'next/dynamic';
import Link from 'next/link';
import {
  ArrowRight,
  BadgeCheck,
  Building2,
  ExternalLink,
  Landmark,
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
import {
  estateLandManagementService,
  EstateManagedAssetSourceType,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';
import {
  estateAcquisitionService,
  type LandAcquisitionItem,
} from '@/services/estate-acquisition.service';
import ExistingLandDialog from './ExistingLandDialog';
import DemarcateLandDialog from './DemarcateLandDialog';
import LandDocumentsPanel from './LandDocumentsPanel';

const LandBankMap = dynamic(() => import('./LandBankMap'), { ssr: false });

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

function hasBoundary(asset?: EstateManagedAsset) {
  return Boolean(asset?.boundaryCoordinates?.trim());
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
  const [assets, setAssets] = React.useState<EstateManagedAsset[]>([]);
  const [acquisitions, setAcquisitions] = React.useState<LandAcquisitionItem[]>(
    []
  );
  const [selectedKey, setSelectedKey] = React.useState<string | null>(null);
  const [search, setSearch] = React.useState('');
  const [isLoading, setIsLoading] = React.useState(true);
  const [existingLandOpen, setExistingLandOpen] = React.useState(false);
  const [demarcationAsset, setDemarcationAsset] =
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
        ? record.asset.boundaryVerified || hasBoundary(record.asset)
        : acquisitionHasDemarcation(record.acquisition)
  ).length;

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

      <div className="grid gap-6 lg:grid-cols-[380px_minmax(0,1fr)]">
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <CardTitle className="text-base">Land Bank</CardTitle>
            <CardDescription>
              Demarcated lands available for project planning.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <form
              className="flex gap-2"
              onSubmit={(event) => {
                event.preventDefault();
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

            <div className="space-y-2">
              {isLoading ? (
                <div className="flex items-center justify-center gap-2 rounded-md border py-12 text-sm text-muted-foreground">
                  <Loader2 className="h-4 w-4 animate-spin" />
                  Loading land bank
                </div>
              ) : null}

              {!isLoading && records.length === 0 ? (
                <div className="rounded-md border py-12 text-center text-sm text-muted-foreground">
                  No land bank or acquisition record matched your search.
                </div>
              ) : null}

              {!isLoading &&
                records.map((record) => {
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
                      ? record.asset.boundaryVerified ||
                        hasBoundary(record.asset)
                      : acquisitionHasDemarcation(record.acquisition);
                  return (
                    <button
                      key={record.key}
                      type="button"
                      onClick={() => setSelectedKey(record.key)}
                      className={`w-full rounded-md border p-3 text-left transition-colors ${
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
                      <div className="mt-3 flex items-center gap-2 text-xs text-muted-foreground">
                        <MapPin className="h-3.5 w-3.5 shrink-0" />
                        <span className="truncate">
                          {location || 'Location not recorded'}
                        </span>
                      </div>
                    </button>
                  );
                })}
            </div>
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
                      !acquisitionHasDemarcation(selected.acquisition) ||
                      markingReadyKey === `acquisition:${selected.acquisition.id}`
                    }
                    onClick={() =>
                      void publishAcquisitionToLandBank(selected.acquisition)
                    }
                    title={
                      acquisitionHasDemarcation(selected.acquisition)
                        ? undefined
                        : 'Complete cadastral demarcation before publishing this land to Estate Land Bank.'
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
                  <Button
                    variant="outline"
                    onClick={() => setDemarcationAsset(selected.asset)}
                  >
                    <MapPin className="mr-2 h-4 w-4" />
                    {hasBoundary(selected.asset) ? 'Re-demarcate' : 'Demarcate'}
                  </Button>
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
                        !(selected.asset.boundaryVerified || hasBoundary(selected.asset)) ||
                        markingReadyKey === `asset:${selected.asset.id}`
                      }
                      onClick={() => void markAssetProjectReady(selected.asset)}
                      title={
                        selected.asset.boundaryVerified || hasBoundary(selected.asset)
                          ? undefined
                          : 'Record and verify the whole land boundary before Project Management can access it.'
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
                  boundaryCoordinates={selected.asset.boundaryCoordinates}
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
                          !acquisitionHasDemarcation(selected.acquisition) ||
                          markingReadyKey ===
                            `acquisition:${selected.acquisition.id}`
                        }
                        onClick={() =>
                          void publishAcquisitionToLandBank(
                            selected.acquisition
                          )
                        }
                        title={
                          acquisitionHasDemarcation(selected.acquisition)
                            ? undefined
                            : 'Complete cadastral demarcation before publishing this land to Estate Land Bank.'
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
        onSaved={async (asset) => {
          await loadLandRecords(search);
          setSelectedKey(`asset:${asset.id}`);
        }}
      />
    </div>
  );
}
