'use client';

import React from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { AlertTriangle, CreditCard, FileText, Loader2, PlayCircle, RefreshCw, Search, Settings2 } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Input } from '@/components/ui/input';
import { Pagination } from '@/components/ui/pagination';
import { usePaginatedItems } from '@/hooks/use-paginated-items';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  estateLandManagementService,
  EstateManagedAssetStatus,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';
import { estateGroundRentService, type GroundRentAccount } from '@/services/estate-ground-rent.service';
import { estatePropertyManagementService, type EstateRentPenaltyStatus } from '@/services/estate-property-management.service';
import {
  assetMatchesWorkspacePrefill,
  buildPropertyWorkspaceHref,
  estateAssetStatusLabels,
  formatEstateDate,
  formatEstateMoney,
  isLandAsset,
  occupantName,
  propertyReference,
  sourceReference,
} from './property-workspace-utils';

function isRentalBillingReady(asset: EstateManagedAsset) {
  const hasActiveLease = [
    EstateManagedAssetStatus.Leased,
    EstateManagedAssetStatus.Occupied,
  ].includes(asset.status);
  const hasBillingStartDate = Boolean(
    asset.rightOfEntryDate || asset.dateOfTenancy
  );
  return (
    hasActiveLease &&
    hasBillingStartDate &&
    Boolean(asset.propertyFileReference)
  );
}

function rentalCharge(asset: EstateManagedAsset) {
  const amount = asset.externalMonthlyRent ?? asset.externalListingPrice;
  return amount != null
    ? `Rent ${formatEstateMoney(
        amount,
        asset.externalListingCurrency || asset.currency || 'GHS'
      )} / month`
    : 'Rent / service charge';
}

export function BillingServiceChargeWorkspace() {
  const searchParams = useSearchParams();
  const prefillAssetId = searchParams.get('assetId');
  const prefillReference =
    searchParams.get('field_propertyUnit') || searchParams.get('referenceNumber');
  const initialSearch = prefillReference || '';
  const [assets, setAssets] = React.useState<EstateManagedAsset[]>([]);
  const [accounts, setAccounts] = React.useState<GroundRentAccount[]>([]);
  const [penaltyStatuses, setPenaltyStatuses] = React.useState<EstateRentPenaltyStatus[]>([]);
  const [searchDraft, setSearchDraft] = React.useState(initialSearch);
  const [search, setSearch] = React.useState(initialSearch);
  const [isLoading, setIsLoading] = React.useState(true);
  const [activatingAssetId, setActivatingAssetId] = React.useState<string | null>(null);
  const [pendingBillingAsset, setPendingBillingAsset] = React.useState<EstateManagedAsset | null>(null);
  const [penaltyAsset, setPenaltyAsset] = React.useState<EstateManagedAsset | null>(null);
  const [penaltyMethod, setPenaltyMethod] = React.useState('None');
  const [gracePeriodDays, setGracePeriodDays] = React.useState('0');
  const [penaltyValue, setPenaltyValue] = React.useState('0');
  const [penaltyCapAmount, setPenaltyCapAmount] = React.useState('');
  const [savingPenaltyTerms, setSavingPenaltyTerms] = React.useState(false);
  const [assessingPenaltyAssetId, setAssessingPenaltyAssetId] = React.useState<string | null>(null);
  const [loadError, setLoadError] = React.useState<string | null>(null);

  const load = React.useCallback(async () => {
    setIsLoading(true);
    setLoadError(null);
    try {
      const [managedAssets, groundRentAccounts, rentPenaltyStatuses] = await Promise.all([
        estateLandManagementService.getManagedAssets({ search: search || undefined, take: 500 }),
        estateGroundRentService.getAccounts().catch(() => []),
        estatePropertyManagementService.getRentPenaltyStatuses().catch(() => []),
      ]);
      setAssets(managedAssets);
      setAccounts(groundRentAccounts);
      setPenaltyStatuses(rentPenaltyStatuses);
    } catch {
      setAssets([]);
      setAccounts([]);
      setPenaltyStatuses([]);
      setLoadError('Unable to load billing and service charge records.');
    } finally {
      setIsLoading(false);
    }
  }, [search]);

  React.useEffect(() => {
    void load();
  }, [load]);

  const accountByAssetId = React.useMemo(
    () => new Map(accounts.map((account) => [account.estateManagedAssetId, account])),
    [accounts]
  );

  const billableAssets = React.useMemo(
    () =>
      assets.filter((asset) =>
        Boolean(asset.customerBusinessPartnerId || asset.lesseeName) ||
        [EstateManagedAssetStatus.Reserved, EstateManagedAssetStatus.Leased, EstateManagedAssetStatus.Occupied].includes(asset.status)
      ),
    [assets]
  );

  const penaltyStatusByAssetId = React.useMemo(
    () => new Map(penaltyStatuses.map((status) => [status.assetId, status])),
    [penaltyStatuses]
  );

  const displayedBillableAssets = React.useMemo(() => {
    if (!prefillAssetId && !prefillReference) return billableAssets;
    return billableAssets.filter((asset) =>
      assetMatchesWorkspacePrefill(asset, prefillAssetId, prefillReference)
    );
  }, [billableAssets, prefillAssetId, prefillReference]);
  const billingPages = usePaginatedItems(displayedBillableAssets, 10);

  const readyCount = displayedBillableAssets.filter((asset) => {
    const account = accountByAssetId.get(asset.id);
    if (isLandAsset(asset)) return Boolean(account?.canGenerateInvoice);
    return isRentalBillingReady(asset) && !asset.rentBillingActivatedAt;
  }).length;

  const activateRentBilling = async (asset: EstateManagedAsset): Promise<boolean> => {
    setActivatingAssetId(asset.id);
    try {
      const result = await estatePropertyManagementService.activateRentBilling(asset.id);
      toast.success(result.message);
      await load();
      setPendingBillingAsset(null);
      return true;
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Unable to activate rent billing.');
      return false;
    } finally {
      setActivatingAssetId(null);
    }
  };

  const openPenaltyTerms = (asset: EstateManagedAsset) => {
    setPenaltyAsset(asset);
    setPenaltyMethod(asset.rentPenaltyMethod || 'None');
    setGracePeriodDays(String(asset.rentGracePeriodDays || 0));
    setPenaltyValue(String(asset.rentPenaltyValue || 0));
    setPenaltyCapAmount(asset.rentPenaltyCapAmount == null ? '' : String(asset.rentPenaltyCapAmount));
  };

  const savePenaltyTerms = async () => {
    if (!penaltyAsset) return;
    const graceDays = Number(gracePeriodDays);
    const value = Number(penaltyValue);
    const cap = penaltyCapAmount.trim() === '' ? null : Number(penaltyCapAmount);
    if (!Number.isInteger(graceDays) || graceDays < 0 || graceDays > 365) {
      toast.error('Grace period must be a whole number between 0 and 365 days.');
      return;
    }
    if (value < 0 || (cap != null && cap < 0)) {
      toast.error('Penalty values cannot be negative.');
      return;
    }
    setSavingPenaltyTerms(true);
    try {
      const result = await estatePropertyManagementService.updateRentPenaltyTerms(penaltyAsset.id, {
        gracePeriodDays: graceDays,
        penaltyMethod,
        penaltyValue: penaltyMethod === 'None' ? 0 : value,
        penaltyCapAmount: penaltyMethod === 'None' ? null : cap,
      });
      toast.success(result.message);
      setPenaltyAsset(null);
      await load();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Unable to save rental penalty terms.');
    } finally {
      setSavingPenaltyTerms(false);
    }
  };

  const assessRentPenalty = async (asset: EstateManagedAsset) => {
    setAssessingPenaltyAssetId(asset.id);
    try {
      const result = await estatePropertyManagementService.assessRentPenalty(asset.id);
      toast.success(result.message);
      await load();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Unable to assess the rental penalty.');
    } finally {
      setAssessingPenaltyAssetId(null);
    }
  };

  return (
    <div className="space-y-6">
      <div className="grid gap-3 sm:grid-cols-3">
        <Card><CardHeader className="pb-3"><CardDescription>Billing records</CardDescription><CardTitle className="text-2xl">{displayedBillableAssets.length}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-3"><CardDescription>Ready for billing</CardDescription><CardTitle className="text-2xl">{readyCount}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-3"><CardDescription>Ground-rent accounts</CardDescription><CardTitle className="text-2xl">{accounts.length}</CardTitle></CardHeader></Card>
      </div>

      <Card>
        <CardHeader className="gap-4">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
            <div>
              <CardTitle className="flex items-center gap-2"><CreditCard className="h-5 w-5 text-primary" /> Billing / Service Charge Operations</CardTitle>
              <CardDescription className="mt-2 max-w-3xl">
                Billing readiness board for rent, ground rent, service charge, deposits, arrears, and Finance AR handoff references. This screen does not duplicate Finance AR ledgers.
              </CardDescription>
            </div>
            <Button type="button" variant="outline" size="icon" disabled={isLoading} onClick={() => void load()}><RefreshCw className="h-4 w-4" /></Button>
          </div>
          <form className="grid gap-2 lg:grid-cols-[minmax(14rem,1fr)_auto]" onSubmit={(event) => { event.preventDefault(); setSearch(searchDraft.trim()); }}>
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input value={searchDraft} onChange={(event) => setSearchDraft(event.target.value)} className="pl-9" placeholder="Search property, tenant, or billing reference" />
            </div>
            <Button type="submit">Search</Button>
          </form>
        </CardHeader>
        <CardContent>
          {loadError ? <div className="rounded-md border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive">{loadError}</div> : null}
          {isLoading ? <div className="flex justify-center gap-2 py-10 text-sm text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin" /> Loading billing records</div> : null}
          {!isLoading && displayedBillableAssets.length > 0 ? (
            <div className="overflow-x-auto rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Property / unit</TableHead>
                    <TableHead>Customer</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Charge</TableHead>
                    <TableHead>Billing readiness</TableHead>
                    <TableHead>Next date</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {billingPages.items.map((asset) => {
                    const account = accountByAssetId.get(asset.id);
                    const land = isLandAsset(asset);
                    const ready = land
                      ? Boolean(account?.canGenerateInvoice)
                      : isRentalBillingReady(asset);
                    const rentBillingActive = !land && Boolean(asset.rentBillingActivatedAt);
                    const penaltyStatus = penaltyStatusByAssetId.get(asset.id);
                    return (
                      <TableRow key={asset.id}>
                        <TableCell><div className="font-medium">{asset.name}</div><div className="text-xs text-muted-foreground">{propertyReference(asset)}</div></TableCell>
                        <TableCell>{occupantName(asset)}</TableCell>
                        <TableCell><Badge variant="secondary">{estateAssetStatusLabels[asset.status]}</Badge></TableCell>
                        <TableCell>{land ? `Ground rent ${formatEstateMoney(account?.amountPerPeriod ?? asset.groundRentPayable, account?.currencyCode || asset.currency || 'GHS')}` : <div><div>{rentalCharge(asset)}</div><div className="mt-1 text-xs text-muted-foreground">{asset.rentPenaltyMethod && asset.rentPenaltyMethod !== 'None' ? `${asset.rentPenaltyMethod} penalty after ${asset.rentGracePeriodDays} day${asset.rentGracePeriodDays === 1 ? '' : 's'}` : 'No late-payment penalty configured'}</div></div>}</TableCell>
                        <TableCell>{rentBillingActive ? <Badge variant="secondary">Active</Badge> : ready ? <Badge>Ready</Badge> : <Badge variant="outline">{account?.invoiceHoldReason || 'Needs setup / hold'}</Badge>}</TableCell>
                        <TableCell>{formatEstateDate(account?.nextDueDate || asset.nextRentBillingDate || asset.rightOfEntryDate || asset.dateOfTenancy)}</TableCell>
                        <TableCell className="text-right">
                          <div className="flex justify-end gap-2">
                            {land ? (
                              <Button asChild size="sm" variant="outline"><Link href={`/estate/property-management/EstatePropertyManagementGroundRent?assetId=${encodeURIComponent(asset.id)}`}>Ground rent</Link></Button>
                            ) : null}
                            {!land && ready && !rentBillingActive ? (
                              <Button
                                type="button"
                                size="sm"
                                disabled={activatingAssetId === asset.id}
                                onClick={() => setPendingBillingAsset(asset)}
                              >
                                {activatingAssetId === asset.id ? <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" /> : <PlayCircle className="mr-1 h-3.5 w-3.5" />}
                                Activate billing
                              </Button>
                            ) : null}
                            {rentBillingActive && asset.lastRentInvoiceId ? (
                              <Button asChild size="sm" variant="outline">
                                <Link href={`/finance/ar/invoices/${encodeURIComponent(asset.lastRentInvoiceId)}`}>{asset.lastRentInvoiceNumber || 'View invoice'}</Link>
                              </Button>
                            ) : null}
                            {!land ? (
                              <Button type="button" size="sm" variant="outline" onClick={() => openPenaltyTerms(asset)}>
                                <Settings2 className="mr-1 h-3.5 w-3.5" /> Penalty terms
                              </Button>
                            ) : null}
                            {!land && rentBillingActive && penaltyStatus?.canAssessPenalty ? (
                              <Button type="button" size="sm" variant="outline" disabled={assessingPenaltyAssetId === asset.id} onClick={() => void assessRentPenalty(asset)}>
                                {assessingPenaltyAssetId === asset.id ? <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" /> : <AlertTriangle className="mr-1 h-3.5 w-3.5" />} Apply penalty
                              </Button>
                            ) : null}
                            {asset.lastRentPenaltyInvoiceId ? (
                              <Button asChild size="sm" variant="outline"><Link href={`/finance/ar/invoices/${encodeURIComponent(asset.lastRentPenaltyInvoiceId)}`}>{asset.lastRentPenaltyInvoiceNumber || 'Penalty invoice'}</Link></Button>
                            ) : null}
                            <Button asChild size="sm" variant="ghost">
                              <Link href={buildPropertyWorkspaceHref('/estate/property-management/EstatePropertyManagementDocumentRecordIndex', asset, 'Billing record', { documentCategory: 'Billing / finance', billingReference: sourceReference(asset) })}>
                                <FileText className="mr-1 h-3.5 w-3.5" /> Records
                              </Link>
                            </Button>
                          </div>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
          ) : null}
          {displayedBillableAssets.length > billingPages.pageSize ? <Pagination currentPage={billingPages.currentPage} totalPages={billingPages.totalPages} totalItems={billingPages.totalItems} pageSize={billingPages.pageSize} onPageChange={billingPages.setCurrentPage} /> : null}
          {!isLoading && !loadError && displayedBillableAssets.length === 0 ? <div className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">No billing records found.</div> : null}
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={pendingBillingAsset !== null}
        onOpenChange={(open) => !open && setPendingBillingAsset(null)}
        title="Activate monthly rent billing?"
        description="Create the first rent invoice in Finance AR and activate the monthly billing schedule."
        confirmText="Activate billing"
        isLoading={pendingBillingAsset ? activatingAssetId === pendingBillingAsset.id : false}
        onConfirm={async () => pendingBillingAsset ? activateRentBilling(pendingBillingAsset) : false}
        maxWidth="520px"
      >
        {pendingBillingAsset ? (
          <dl className="grid grid-cols-[minmax(0,1fr)_auto] gap-x-6 gap-y-3 rounded-md border bg-muted/30 p-4 text-sm">
            <dt className="text-muted-foreground">Property / unit</dt>
            <dd className="text-right font-medium">{pendingBillingAsset.name}</dd>
            <dt className="text-muted-foreground">Customer</dt>
            <dd className="text-right font-medium">{occupantName(pendingBillingAsset)}</dd>
            <dt className="text-muted-foreground">Monthly rent</dt>
            <dd className="text-right font-medium">{formatEstateMoney(pendingBillingAsset.externalMonthlyRent ?? pendingBillingAsset.externalListingPrice, pendingBillingAsset.externalListingCurrency || pendingBillingAsset.currency || 'GHS')}</dd>
            <dt className="text-muted-foreground">Billing starts</dt>
            <dd className="text-right font-medium">{formatEstateDate(pendingBillingAsset.rightOfEntryDate || pendingBillingAsset.dateOfTenancy)}</dd>
            <dt className="text-muted-foreground">Agreement</dt>
            <dd className="max-w-64 break-words text-right font-medium">{pendingBillingAsset.propertyFileReference || 'Not recorded'}</dd>
          </dl>
        ) : null}
      </ConfirmationDialog>

      <Dialog open={penaltyAsset !== null} onOpenChange={(open) => !open && setPenaltyAsset(null)}>
        <DialogContent className="sm:max-w-xl">
          <DialogHeader>
            <DialogTitle>Rental penalty terms</DialogTitle>
            <DialogDescription>Set the late-payment grace period and penalty rule for {penaltyAsset?.name}. Only Estate or Property managers can save these terms.</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2 sm:grid-cols-2">
            <div className="space-y-2"><Label htmlFor="rent-grace-days">Grace period (days)</Label><Input id="rent-grace-days" type="number" min={0} max={365} step={1} value={gracePeriodDays} onChange={(event) => setGracePeriodDays(event.target.value)} /></div>
            <div className="space-y-2"><Label>Penalty method</Label><Select value={penaltyMethod} onValueChange={setPenaltyMethod}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="None">No penalty</SelectItem><SelectItem value="Fixed">Fixed amount</SelectItem><SelectItem value="Percentage">Percentage of unpaid balance</SelectItem></SelectContent></Select></div>
            <div className="space-y-2"><Label htmlFor="rent-penalty-value">{penaltyMethod === 'Percentage' ? 'Penalty percentage' : 'Penalty amount'}</Label><Input id="rent-penalty-value" type="number" min={0} max={penaltyMethod === 'Percentage' ? 100 : undefined} step="0.01" disabled={penaltyMethod === 'None'} value={penaltyValue} onChange={(event) => setPenaltyValue(event.target.value)} /></div>
            <div className="space-y-2"><Label htmlFor="rent-penalty-cap">Maximum penalty amount (optional)</Label><Input id="rent-penalty-cap" type="number" min={0} step="0.01" disabled={penaltyMethod === 'None'} value={penaltyCapAmount} onChange={(event) => setPenaltyCapAmount(event.target.value)} /></div>
          </div>
          <DialogFooter><Button type="button" variant="outline" onClick={() => setPenaltyAsset(null)}>Cancel</Button><Button type="button" disabled={savingPenaltyTerms} onClick={() => void savePenaltyTerms()}>{savingPenaltyTerms ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}Save penalty terms</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
