'use client';

import React from 'react';
import Link from 'next/link';
import { FileSignature, Home, Loader2, RefreshCw, Search, Users } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Pagination } from '@/components/ui/pagination';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  EstateManagedAssetStatus,
} from '@/services/estate-land-management.service';
import {
  buildPropertyWorkspaceHref,
  estateAssetStatusLabels,
  formatEstateDate,
  isActiveTenantAsset,
  occupantName,
  propertyReference,
  sourceReference,
} from './property-workspace-utils';
import { useManagedAssetsPage } from './use-managed-assets-page';

export function TenantOccupantWorkspace() {
  const [searchDraft, setSearchDraft] = React.useState('');
  const [search, setSearch] = React.useState('');
  const {
    assets,
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
    ],
    errorMessage: 'Unable to load tenant and occupant records.',
  });

  const occupantAssets = React.useMemo(
    () => assets.filter(isActiveTenantAsset),
    [assets]
  );
  const activeCount = occupantAssets.filter((asset) =>
    [EstateManagedAssetStatus.Leased, EstateManagedAssetStatus.Occupied].includes(asset.status)
  ).length;

  return (
    <div className="space-y-6">
      <div className="grid gap-3 sm:grid-cols-3">
        <Card><CardHeader className="pb-3"><CardDescription>Tenant / occupant links</CardDescription><CardTitle className="text-2xl">{occupantAssets.length}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-3"><CardDescription>Active occupancy</CardDescription><CardTitle className="text-2xl">{activeCount}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-3"><CardDescription>Pending possession</CardDescription><CardTitle className="text-2xl">{occupantAssets.length - activeCount}</CardTitle></CardHeader></Card>
      </div>

      <Card>
        <CardHeader className="gap-4">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
            <div>
              <CardTitle className="flex items-center gap-2"><Users className="h-5 w-5 text-primary" /> Tenant / Occupant Register</CardTitle>
              <CardDescription className="mt-2 max-w-3xl">
                Operational view of customers already linked through Lease Management. Business Partner registration remains owned by the existing Business Partner module.
              </CardDescription>
            </div>
            <Button type="button" variant="outline" size="icon" disabled={isLoading} onClick={() => void loadAssets(page)}><RefreshCw className="h-4 w-4" /></Button>
          </div>
          <form className="grid gap-2 lg:grid-cols-[minmax(14rem,1fr)_auto]" onSubmit={(event) => { event.preventDefault(); setPage(1); setSearch(searchDraft.trim()); }}>
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input value={searchDraft} onChange={(event) => setSearchDraft(event.target.value)} className="pl-9" placeholder="Search tenant, property, or file reference" />
            </div>
            <Button type="submit">Search</Button>
          </form>
        </CardHeader>
        <CardContent>
          {loadError ? <div className="rounded-md border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive">{loadError}</div> : null}
          {isLoading ? <div className="flex justify-center gap-2 py-10 text-sm text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin" /> Loading tenant records</div> : null}
          {!isLoading && occupantAssets.length > 0 ? (
            <div className="overflow-x-auto rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Tenant / occupant</TableHead>
                    <TableHead>Property / unit</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Lease / possession date</TableHead>
                    <TableHead>File reference</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {occupantAssets.map((asset) => (
                    <TableRow key={asset.id}>
                      <TableCell><div className="font-medium">{occupantName(asset)}</div><div className="text-xs text-muted-foreground">{asset.customerBusinessPartnerId || 'Manual occupant reference'}</div></TableCell>
                      <TableCell><div>{asset.name}</div><div className="text-xs text-muted-foreground">{propertyReference(asset)}</div></TableCell>
                      <TableCell><Badge variant="secondary">{estateAssetStatusLabels[asset.status]}</Badge></TableCell>
                      <TableCell>{formatEstateDate(asset.rightOfEntryDate || asset.dateOfTenancy)}</TableCell>
                      <TableCell>{sourceReference(asset)}</TableCell>
                      <TableCell className="text-right">
                        <div className="flex justify-end gap-2">
                          <Button asChild size="sm" variant="ghost"><Link href={buildPropertyWorkspaceHref('/estate/property-management/EstatePropertyManagementLease', asset, 'Lease profile')}><FileSignature className="mr-1 h-3.5 w-3.5" /> Lease</Link></Button>
                          <Button asChild size="sm" variant="ghost"><Link href={buildPropertyWorkspaceHref('/estate/property-management/EstatePropertyManagementOccupancyAvailability', asset, 'Occupancy profile')}><Home className="mr-1 h-3.5 w-3.5" /> Occupancy</Link></Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          ) : null}
          {totalPages > 1 ? <Pagination currentPage={page} totalPages={totalPages} totalItems={totalItems} pageSize={pageSize} onPageChange={setPage} /> : null}
          {!isLoading && !loadError && occupantAssets.length === 0 ? <div className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">No tenant or occupant records have been linked yet.</div> : null}
        </CardContent>
      </Card>
    </div>
  );
}
