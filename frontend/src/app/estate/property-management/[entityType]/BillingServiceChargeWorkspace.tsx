'use client';

import React from 'react';
import Link from 'next/link';
import { CreditCard, FileText, Loader2, RefreshCw, Search } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  estateLandManagementService,
  EstateManagedAssetStatus,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';
import { estateGroundRentService, type GroundRentAccount } from '@/services/estate-ground-rent.service';
import {
  buildPropertyWorkspaceHref,
  estateAssetStatusLabels,
  formatEstateDate,
  formatEstateMoney,
  isLandAsset,
  occupantName,
  propertyReference,
  sourceReference,
} from './property-workspace-utils';

export function BillingServiceChargeWorkspace() {
  const [assets, setAssets] = React.useState<EstateManagedAsset[]>([]);
  const [accounts, setAccounts] = React.useState<GroundRentAccount[]>([]);
  const [searchDraft, setSearchDraft] = React.useState('');
  const [search, setSearch] = React.useState('');
  const [isLoading, setIsLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string | null>(null);

  const load = React.useCallback(async () => {
    setIsLoading(true);
    setLoadError(null);
    try {
      const [managedAssets, groundRentAccounts] = await Promise.all([
        estateLandManagementService.getManagedAssets({ search: search || undefined, take: 500 }),
        estateGroundRentService.getAccounts().catch(() => []),
      ]);
      setAssets(managedAssets);
      setAccounts(groundRentAccounts);
    } catch {
      setAssets([]);
      setAccounts([]);
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

  const readyCount = billableAssets.filter((asset) => {
    const account = accountByAssetId.get(asset.id);
    if (isLandAsset(asset)) return Boolean(account?.canGenerateInvoice);
    return asset.status === EstateManagedAssetStatus.Occupied && Boolean(asset.propertyFileReference);
  }).length;

  return (
    <div className="space-y-6">
      <div className="grid gap-3 sm:grid-cols-3">
        <Card><CardHeader className="pb-3"><CardDescription>Billing records</CardDescription><CardTitle className="text-2xl">{billableAssets.length}</CardTitle></CardHeader></Card>
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
          {!isLoading && billableAssets.length > 0 ? (
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
                  {billableAssets.map((asset) => {
                    const account = accountByAssetId.get(asset.id);
                    const land = isLandAsset(asset);
                    const ready = land
                      ? Boolean(account?.canGenerateInvoice)
                      : asset.status === EstateManagedAssetStatus.Occupied && Boolean(asset.propertyFileReference);
                    return (
                      <TableRow key={asset.id}>
                        <TableCell><div className="font-medium">{asset.name}</div><div className="text-xs text-muted-foreground">{propertyReference(asset)}</div></TableCell>
                        <TableCell>{occupantName(asset)}</TableCell>
                        <TableCell><Badge variant="secondary">{estateAssetStatusLabels[asset.status]}</Badge></TableCell>
                        <TableCell>{land ? `Ground rent ${formatEstateMoney(account?.amountPerPeriod ?? asset.groundRentPayable, account?.currencyCode || asset.currency || 'GHS')}` : 'Rent / service charge'}</TableCell>
                        <TableCell>{ready ? <Badge>Ready</Badge> : <Badge variant="outline">{account?.invoiceHoldReason || 'Needs setup / hold'}</Badge>}</TableCell>
                        <TableCell>{formatEstateDate(account?.nextDueDate || asset.rightOfEntryDate || asset.dateOfTenancy)}</TableCell>
                        <TableCell className="text-right">
                          <div className="flex justify-end gap-2">
                            {land ? (
                              <Button asChild size="sm" variant="outline"><Link href={`/estate/property-management/EstatePropertyManagementGroundRent?assetId=${encodeURIComponent(asset.id)}`}>Ground rent</Link></Button>
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
          {!isLoading && !loadError && billableAssets.length === 0 ? <div className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">No billing records found.</div> : null}
        </CardContent>
      </Card>
    </div>
  );
}
