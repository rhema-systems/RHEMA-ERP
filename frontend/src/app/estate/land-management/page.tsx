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
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  estateLandManagementService,
  EstateManagedAssetSourceType,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';
import ExistingLandDialog from './ExistingLandDialog';
import LandDocumentsPanel from './LandDocumentsPanel';

const LandBankMap = dynamic(() => import('./LandBankMap'), { ssr: false });

function formatArea(value?: number) {
  if (value == null) return 'Not recorded';
  return `${value.toLocaleString(undefined, { maximumFractionDigits: 2 })} sqm`;
}

function formatMoney(value?: number, currency = 'GHS') {
  if (value == null) return 'Not recorded';
  return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(value);
}

function sourceLabel(sourceType: EstateManagedAssetSourceType) {
  if (sourceType === EstateManagedAssetSourceType.LandAcquisition) return 'Land acquisition';
  if (sourceType === EstateManagedAssetSourceType.ProjectUnit) return 'Project unit';
  return 'Manual';
}

function hasBoundary(asset?: EstateManagedAsset) {
  return Boolean(asset?.boundaryCoordinates?.trim());
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

function DetailRow({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="rounded-md border bg-background p-3">
      <p className="text-xs font-medium uppercase tracking-normal text-muted-foreground">{label}</p>
      <div className="mt-1 text-sm font-medium text-foreground">{value || 'Not recorded'}</div>
    </div>
  );
}

export default function EstateLandManagementPage() {
  const [assets, setAssets] = React.useState<EstateManagedAsset[]>([]);
  const [selectedId, setSelectedId] = React.useState<string | null>(null);
  const [search, setSearch] = React.useState('');
  const [isLoading, setIsLoading] = React.useState(true);
  const [existingLandOpen, setExistingLandOpen] = React.useState(false);
  const [markingReady, setMarkingReady] = React.useState(false);

  const selected = React.useMemo(
    () => assets.find((asset) => asset.id === selectedId) || assets[0],
    [assets, selectedId]
  );

  const loadLandBank = React.useCallback(async (query?: string) => {
    setIsLoading(true);
    try {
      const data = await estateLandManagementService.getLandBank(query);
      setAssets(data);
      setSelectedId((current) => (current && data.some((asset) => asset.id === current) ? current : data[0]?.id ?? null));
    } catch (error) {
      console.error('Failed to load land bank', error);
      toast.error('Unable to load land bank records');
    } finally {
      setIsLoading(false);
    }
  }, []);

  React.useEffect(() => {
    void loadLandBank();
  }, [loadLandBank]);

  const acquisitionCount = assets.filter((asset) => asset.sourceType === EstateManagedAssetSourceType.LandAcquisition).length;
  const verifiedCount = assets.filter((asset) => asset.boundaryVerified || hasBoundary(asset)).length;

  return (
    <div className="min-h-screen bg-background text-foreground">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6 px-4 py-6 sm:px-6 lg:px-8">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div className="space-y-2">
            <Badge variant="outline" className="w-fit">
              Estate operations
            </Badge>
            <div>
              <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">Land Management</h1>
              <p className="mt-2 max-w-3xl text-sm text-muted-foreground">
                Land bank records made available after cadastral demarcation and acquisition completion, ready for project management planning.
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
          <StatCard title="Land bank records" value={assets.length} icon={Landmark} />
          <StatCard title="From acquisition" value={acquisitionCount} icon={ArrowRight} />
          <StatCard title="Boundary verified" value={verifiedCount} icon={BadgeCheck} />
        </div>

        <div className="grid gap-6 lg:grid-cols-[380px_minmax(0,1fr)]">
          <Card className="border-border bg-card text-card-foreground">
            <CardHeader>
              <CardTitle className="text-base">Land Bank</CardTitle>
              <CardDescription>Demarcated lands available for project planning.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <form
                className="flex gap-2"
                onSubmit={(event) => {
                  event.preventDefault();
                  void loadLandBank(search);
                }}
              >
                <div className="relative flex-1">
                  <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    value={search}
                    onChange={(event) => setSearch(event.target.value)}
                    placeholder="Search land bank"
                    className="pl-9"
                  />
                </div>
                <Button type="submit" variant="outline" size="icon" aria-label="Search land bank">
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

                {!isLoading && assets.length === 0 ? (
                  <div className="rounded-md border py-12 text-center text-sm text-muted-foreground">
                    No demarcated land has been pushed to the land bank yet.
                  </div>
                ) : null}

                {!isLoading &&
                  assets.map((asset) => {
                    const active = selected?.id === asset.id;
                    return (
                      <button
                        key={asset.id}
                        type="button"
                        onClick={() => setSelectedId(asset.id)}
                        className={`w-full rounded-md border p-3 text-left transition-colors ${
                          active ? 'border-teal-600 bg-teal-50 text-teal-950 dark:bg-teal-950/30 dark:text-teal-100' : 'bg-background hover:bg-muted'
                        }`}
                      >
                        <div className="flex items-start justify-between gap-3">
                          <div className="min-w-0">
                            <p className="truncate text-sm font-semibold">{asset.name}</p>
                            <p className="mt-1 truncate text-xs text-muted-foreground">{asset.assetCode}</p>
                          </div>
                          {asset.boundaryVerified || hasBoundary(asset) ? (
                            <Badge variant="secondary" className="shrink-0">
                              Verified
                            </Badge>
                          ) : null}
                        </div>
                        <div className="mt-3 flex items-center gap-2 text-xs text-muted-foreground">
                          <MapPin className="h-3.5 w-3.5 shrink-0" />
                          <span className="truncate">{asset.location || 'Location not recorded'}</span>
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
                  <CardTitle className="text-lg">{selected?.name || 'Select a land bank record'}</CardTitle>
                  <CardDescription className="mt-1">
                    {selected ? `${selected.assetCode} - ${sourceLabel(selected.sourceType)}` : 'Project management can pull from records shown here.'}
                  </CardDescription>
                </div>
                {selected?.isReadyForProjectManagement ? (
                  <Button asChild variant="outline"><Link href="/development/projects"><ExternalLink className="mr-2 h-4 w-4" />Project Pull Ready</Link></Button>
                ) : selected ? (
                  <Button variant="outline" disabled={markingReady} onClick={async () => {
                    try { setMarkingReady(true); await estateLandManagementService.markReadyForProjectManagement(selected.id); toast.success('Land is ready for project management.'); await loadLandBank(search); }
                    catch (error: any) { toast.error(error?.message || 'Unable to make land ready.'); } finally { setMarkingReady(false); }
                  }}>{markingReady ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Building2 className="mr-2 h-4 w-4" />}Mark Project Ready</Button>
                ) : null}
              </div>
            </CardHeader>
            <CardContent className="space-y-5">
              {selected ? (
                <>
                  <LandBankMap boundaryCoordinates={selected.boundaryCoordinates} />

                  <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                    <DetailRow label="Location" value={selected.location} />
                    <DetailRow label="Area" value={formatArea(selected.areaSquareMeters)} />
                    <DetailRow label="Valuation" value={formatMoney(selected.valuationAmount, selected.currency)} />
                    <DetailRow label="Purpose" value={selected.purpose} />
                    <DetailRow label="Zoning" value={selected.zoningClassification} />
                    <DetailRow label="Planning status" value={selected.planningComplianceStatus} />
                    <DetailRow label="GIS layer" value={selected.gisLayerReference} />
                    <DetailRow label="Survey plan" value={selected.surveyPlanNumber} />
                    <DetailRow label="Map sheet" value={selected.mapSheetNumber} />
                    <DetailRow label="Surveyor" value={selected.surveyorName} />
                    <DetailRow label="Survey date" value={selected.surveyDate ? new Date(selected.surveyDate).toLocaleDateString() : undefined} />
                    <DetailRow label="Region / District" value={[selected.region, selected.district].filter(Boolean).join(' / ')} />
                    <DetailRow label="Beacon count" value={selected.beaconCount?.toString()} />
                  </div>

                  {selected.ownershipHistory?.length ? (
                    <div className="overflow-hidden rounded-md border bg-background">
                      <div className="border-b px-4 py-3"><p className="text-sm font-semibold">Ownership History</p></div>
                      <div className="divide-y">{selected.ownershipHistory.map((owner, index) => <div key={`${owner.ownerName}-${index}`} className="grid gap-2 px-4 py-3 text-sm md:grid-cols-4"><span className="font-medium">{owner.ownerName}</span><span>{owner.ownershipType}</span><span>{owner.interestHeld}</span><span>{owner.isCurrentOwner ? 'Current owner' : `${owner.ownershipStartDate} - ${owner.ownershipEndDate || 'Not recorded'}`}</span></div>)}</div>
                    </div>
                  ) : null}

                  <LandDocumentsPanel assetId={selected.id} />

                  {selected.notes ? (
                    <div className="rounded-md border bg-background p-4">
                      <p className="text-sm font-semibold">Notes</p>
                      <p className="mt-2 text-sm leading-6 text-muted-foreground">{selected.notes}</p>
                    </div>
                  ) : null}
                </>
              ) : (
                <div className="rounded-md border py-16 text-center text-sm text-muted-foreground">
                  Select a land bank record to view its demarcation and planning details.
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      </div>
      <ExistingLandDialog open={existingLandOpen} onOpenChange={setExistingLandOpen} onCreated={async (asset) => { await loadLandBank(search); setSelectedId(asset.id); }} />
    </div>
  );
}
