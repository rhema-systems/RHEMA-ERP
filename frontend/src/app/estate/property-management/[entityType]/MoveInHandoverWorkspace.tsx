'use client';

import React from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { ClipboardCheck, KeyRound, Loader2, RefreshCw, Search } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Pagination } from '@/components/ui/pagination';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import {
  estateLandManagementService,
  EstateManagedAssetStatus,
  type EstateManagedAsset,
  type EstateCompletedTermination,
} from '@/services/estate-land-management.service';
import {
  assetMatchesWorkspacePrefill,
  buildPropertyWorkspaceHref,
  estateAssetStatusLabels,
  formatEstateDate,
  occupantName,
  propertyReference,
  sourceReference,
} from './property-workspace-utils';
import { useManagedAssetsPage } from './use-managed-assets-page';

type HandoverAction = 'move-in' | 'move-out' | 'maintenance' | 'block';

export function MoveInHandoverWorkspace() {
  const searchParams = useSearchParams();
  const prefillAssetId = searchParams.get('assetId');
  const prefillReference =
    searchParams.get('field_propertyUnit') || searchParams.get('referenceNumber');
  const initialSearch = prefillReference || '';
  const [searchDraft, setSearchDraft] = React.useState(initialSearch);
  const [search, setSearch] = React.useState(initialSearch);
  const [selectedAssetId, setSelectedAssetId] = React.useState('');
  const [action, setAction] = React.useState<HandoverAction>('move-in');
  const [actualDate, setActualDate] = React.useState(new Date().toISOString().slice(0, 10));
  const [notes, setNotes] = React.useState('');
  const [isSaving, setIsSaving] = React.useState(false);
  const [terminationCases, setTerminationCases] = React.useState<EstateCompletedTermination[]>([]);
  const [terminationCaseId, setTerminationCaseId] = React.useState('');
  const [terminationError, setTerminationError] = React.useState<string | null>(null);
  const {
    assets,
    setAssets,
    page,
    setPage,
    isLoading,
    loadError,
    loadAssets,
    pageSize,
    totalPages,
    totalItems,
  } = useManagedAssetsPage({
    search: search || undefined,
    statuses: [
      EstateManagedAssetStatus.Reserved,
      EstateManagedAssetStatus.Leased,
      EstateManagedAssetStatus.Occupied,
      EstateManagedAssetStatus.UnderMaintenance,
      EstateManagedAssetStatus.Blocked,
    ],
    errorMessage: 'Unable to load handover records.',
  });

  const selectedAsset = assets.find((asset) => asset.id === selectedAssetId);
  const needsLegalTermination = action === 'move-out' && selectedAsset != null
    && [EstateManagedAssetStatus.Leased, EstateManagedAssetStatus.Occupied].includes(selectedAsset.status);

  React.useEffect(() => {
    if (!needsLegalTermination || !selectedAssetId) {
      setTerminationCases([]);
      setTerminationError(null);
      return;
    }
    let active = true;
    setTerminationError(null);
    void estateLandManagementService.getCompletedTerminations(selectedAssetId)
      .then((items) => { if (active) setTerminationCases(items); })
      .catch(() => { if (active) setTerminationError('Unable to load completed Legal terminations.'); });
    return () => { active = false; };
  }, [needsLegalTermination, selectedAssetId]);

  const selectAsset = (asset: EstateManagedAsset) => {
    setSelectedAssetId(asset.id);
    setAction(asset.status === EstateManagedAssetStatus.Occupied ? 'move-out' : 'move-in');
    setActualDate((asset.rightOfEntryDate || asset.dateOfTenancy || new Date().toISOString()).slice(0, 10));
    setNotes('');
    setTerminationCaseId('');
  };

  React.useEffect(() => {
    if (selectedAssetId || (!prefillAssetId && !prefillReference)) return;
    const matchedAsset = assets.find((asset) =>
      assetMatchesWorkspacePrefill(asset, prefillAssetId, prefillReference)
    );
    if (matchedAsset) {
      selectAsset(matchedAsset);
    }
  }, [assets, prefillAssetId, prefillReference, selectedAssetId]);

  const saveHandover = async () => {
    if (!selectedAsset) {
      toast.error('Select a property or unit first.');
      return;
    }

    if ((action === 'move-in' || action === 'move-out') && !actualDate) {
      toast.error(`Enter the actual ${action === 'move-in' ? 'possession' : 'move-out'} date.`);
      return;
    }
    if (needsLegalTermination && !terminationCaseId) {
      toast.error('Select a completed Legal termination case before move-out.');
      return;
    }

    const status =
      action === 'move-in'
        ? EstateManagedAssetStatus.Occupied
        : action === 'move-out'
          ? EstateManagedAssetStatus.Available
          : action === 'maintenance'
            ? EstateManagedAssetStatus.UnderMaintenance
            : EstateManagedAssetStatus.Blocked;

    setIsSaving(true);
    try {
      const updated = await estateLandManagementService.updateOccupancy(selectedAsset.id, {
        status,
        actualDate: actualDate || null,
        releaseOccupant: action === 'move-out',
        terminationCaseId: needsLegalTermination ? terminationCaseId : null,
        isAvailableForLease: action === 'move-out',
        isAvailableForSale: false,
        isPublishedToExternalPortal: false,
        notes: [
          `Handover action: ${action}`,
          actualDate ? `Actual date: ${actualDate}` : null,
          notes.trim() || null,
        ]
          .filter(Boolean)
          .join(' | '),
      });
      setAssets((current) => current.map((asset) => (asset.id === updated.id ? updated : asset)));
      selectAsset(updated);
      toast.success('Handover update saved.');
    } catch (error: unknown) {
      toast.error(error instanceof Error ? error.message : 'Unable to save handover update.');
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader className="gap-4">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
            <div>
              <CardTitle className="flex items-center gap-2">
                <KeyRound className="h-5 w-5 text-primary" />
                Move-in / Move-out / Handover Board
              </CardTitle>
              <CardDescription className="mt-2 max-w-3xl">
                Record possession actions, key/access handover, move-in, move-out, maintenance blocks, and billing/records handoff context.
              </CardDescription>
            </div>
            <Button type="button" variant="outline" size="icon" disabled={isLoading} onClick={() => void loadAssets(page)}>
              <RefreshCw className="h-4 w-4" />
            </Button>
          </div>
          <form
            className="grid gap-2 lg:grid-cols-[minmax(14rem,1fr)_auto]"
            onSubmit={(event) => {
              event.preventDefault();
              setPage(1);
              setSearch(searchDraft.trim());
            }}
          >
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input value={searchDraft} onChange={(event) => setSearchDraft(event.target.value)} className="pl-9" placeholder="Search property, tenant, or reference" />
            </div>
            <Button type="submit">Search</Button>
          </form>
        </CardHeader>
        <CardContent>
          {loadError ? <div className="rounded-md border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive">{loadError}</div> : null}
          {isLoading ? <div className="flex justify-center gap-2 py-10 text-sm text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin" /> Loading handovers</div> : null}
          {!isLoading && assets.length > 0 ? (
            <div className="overflow-x-auto rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Property / unit</TableHead>
                    <TableHead>Tenant / occupant</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Scheduled / lease date</TableHead>
                    <TableHead>Access/file reference</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {assets.map((asset) => (
                    <TableRow key={asset.id} className={selectedAssetId === asset.id ? 'bg-muted/40' : ''}>
                      <TableCell><div className="font-medium">{asset.name}</div><div className="text-xs text-muted-foreground">{propertyReference(asset)}</div></TableCell>
                      <TableCell>{occupantName(asset)}</TableCell>
                      <TableCell><Badge variant="secondary">{estateAssetStatusLabels[asset.status]}</Badge></TableCell>
                      <TableCell>{formatEstateDate(asset.rightOfEntryDate || asset.dateOfTenancy)}</TableCell>
                      <TableCell>{sourceReference(asset)}</TableCell>
                      <TableCell className="text-right">
                        <div className="flex justify-end gap-2">
                          <Button type="button" size="sm" variant="outline" onClick={() => selectAsset(asset)}>Update</Button>
                          <Button asChild size="sm" variant="ghost">
                            <Link href={buildPropertyWorkspaceHref('/estate/property-management/EstatePropertyManagementDocumentRecordIndex', asset, 'Handover record', { documentCategory: 'Move-in / move-out / handover', handoverReference: `HND-${propertyReference(asset)}` })}>
                              Records
                            </Link>
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          ) : null}
          {totalPages > 1 ? <Pagination currentPage={page} totalPages={totalPages} totalItems={totalItems} pageSize={pageSize} onPageChange={setPage} /> : null}
          {!isLoading && !loadError && assets.length === 0 ? <div className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">No handover-ready records found.</div> : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2"><ClipboardCheck className="h-5 w-5 text-primary" /> Handover action</CardTitle>
          <CardDescription>Apply the possession outcome to Occupancy / Availability. Signed evidence should be indexed in Records.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-4">
          <div className="space-y-2 md:col-span-1">
            <Label>Action</Label>
            <Select value={action} onValueChange={(value) => setAction(value as HandoverAction)} disabled={!selectedAsset}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="move-in">Move-in / possession issued</SelectItem>
                <SelectItem value="move-out">Move-out / release unit</SelectItem>
                <SelectItem value="maintenance">Send to maintenance</SelectItem>
                <SelectItem value="block">Block unit</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Actual date</Label>
            <Input type="date" value={actualDate} onChange={(event) => setActualDate(event.target.value)} disabled={!selectedAsset} />
          </div>
          {needsLegalTermination && selectedAsset ? (
            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="handover-termination-case">Completed Legal termination</Label>
              <Select value={terminationCaseId || undefined} onValueChange={setTerminationCaseId}>
                <SelectTrigger id="handover-termination-case"><SelectValue placeholder="Select Legal case" /></SelectTrigger>
                <SelectContent>
                  {terminationCases.map((item) => (
                    <SelectItem key={item.id} value={item.id}>{item.referenceNumber || item.title}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {terminationError ? <p role="alert" className="text-sm text-destructive">{terminationError}</p> : null}
              {terminationCases.length === 0 ? (
                <Button asChild variant="link" size="sm" className="px-0">
                  <Link href={buildPropertyWorkspaceHref('/legal/LegalTerminationRecognition', selectedAsset, 'Lease or rent termination', {
                    estateManagedAssetId: selectedAsset.id,
                  })}>Open Legal termination</Link>
                </Button>
              ) : null}
            </div>
          ) : null}
          <div className="space-y-2 md:col-span-2">
            <Label>Notes / evidence reference</Label>
            <Textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={2} disabled={!selectedAsset} placeholder="Keys, access cards, inspection note, signed handover reference..." />
          </div>
          <div className="md:col-span-4">
            <Button type="button" disabled={!selectedAsset || isSaving} onClick={() => void saveHandover()}>
              {isSaving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              Save handover update
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
