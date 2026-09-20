'use client';

import React from 'react';
import { useSearchParams } from 'next/navigation';
import { FileText, Loader2, RefreshCw, Search } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Pagination } from '@/components/ui/pagination';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  estateLandManagementService,
  type EstateManagedAsset,
  type EstateManagedAssetDocument,
} from '@/services/estate-land-management.service';
import {
  assetMatchesWorkspacePrefill,
  formatEstateDate,
  occupantName,
  propertyReference,
  sourceReference,
} from './property-workspace-utils';
import { useManagedAssetsPage } from './use-managed-assets-page';

interface DocumentRow extends EstateManagedAssetDocument {
  asset: EstateManagedAsset;
}

export function RecordsIndexWorkspace() {
  const searchParams = useSearchParams();
  const prefillAssetId = searchParams.get('assetId');
  const prefillReference =
    searchParams.get('field_propertyUnit') || searchParams.get('referenceNumber');
  const initialSearch = prefillReference || '';
  const [assets, setAssets] = React.useState<EstateManagedAsset[]>([]);
  const [documents, setDocuments] = React.useState<DocumentRow[]>([]);
  const [searchDraft, setSearchDraft] = React.useState(initialSearch);
  const [search, setSearch] = React.useState(initialSearch);
  const [selectedAssetId, setSelectedAssetId] = React.useState('all');
  const {
    assets: pagedAssets,
    page,
    setPage,
    isLoading: isAssetsLoading,
    loadError: assetLoadError,
    loadAssets,
    pageSize,
    totalPages,
    totalItems,
  } = useManagedAssetsPage({
    search: search || undefined,
    errorMessage: 'Unable to load property records index.',
  });
  const [isDocumentsLoading, setIsDocumentsLoading] = React.useState(false);
  const [loadError, setLoadError] = React.useState<string | null>(null);

  const load = React.useCallback(async () => {
    setIsDocumentsLoading(true);
    setLoadError(null);
    try {
      const targetAssets = selectedAssetId === 'all'
        ? pagedAssets
        : assets.filter((asset) => asset.id === selectedAssetId);
      const documentGroups = await Promise.all(
        targetAssets.map(async (asset) => {
          const records = await estateLandManagementService.getDocuments(asset.id).catch(() => []);
          return records.map((record) => ({ ...record, asset }));
        })
      );
      setDocuments(documentGroups.flat());
    } catch {
      setDocuments([]);
      setLoadError('Unable to load property records index.');
    } finally {
      setIsDocumentsLoading(false);
    }
  }, [assets, pagedAssets, selectedAssetId]);

  React.useEffect(() => {
    setAssets(pagedAssets);
  }, [pagedAssets]);

  React.useEffect(() => {
    void load();
  }, [load]);

  React.useEffect(() => {
    if (selectedAssetId !== 'all' || (!prefillAssetId && !prefillReference)) return;
    const matchedAsset = assets.find((asset) =>
      assetMatchesWorkspacePrefill(asset, prefillAssetId, prefillReference)
    );
    if (matchedAsset) {
      setSelectedAssetId(matchedAsset.id);
    }
  }, [assets, prefillAssetId, prefillReference, selectedAssetId]);

  const isLoading = isAssetsLoading || isDocumentsLoading;
  const effectiveLoadError = assetLoadError || loadError;

  return (
    <div className="space-y-6">
      <div className="grid gap-3 sm:grid-cols-3">
        <Card><CardHeader className="pb-3"><CardDescription>Indexed assets</CardDescription><CardTitle className="text-2xl">{totalItems}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-3"><CardDescription>Loaded documents</CardDescription><CardTitle className="text-2xl">{documents.length}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-3"><CardDescription>DMS-linked records</CardDescription><CardTitle className="text-2xl">{documents.filter((item) => item.centralDocumentRecordId).length}</CardTitle></CardHeader></Card>
      </div>

      <Card>
        <CardHeader className="gap-4">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
            <div>
              <CardTitle className="flex items-center gap-2"><FileText className="h-5 w-5 text-primary" /> Property Documents / Records Index</CardTitle>
              <CardDescription className="mt-2 max-w-3xl">
                Search property-linked document references, DMS references, listing images, lease files, handover evidence, and operational record metadata. Central DMS remains the file/version owner.
              </CardDescription>
            </div>
            <Button type="button" variant="outline" size="icon" disabled={isLoading} onClick={() => void loadAssets(page)}><RefreshCw className="h-4 w-4" /></Button>
          </div>
          <form className="grid gap-2 lg:grid-cols-[minmax(14rem,1fr)_18rem_auto]" onSubmit={(event) => { event.preventDefault(); setPage(1); setSearch(searchDraft.trim()); setSelectedAssetId('all'); }}>
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input value={searchDraft} onChange={(event) => setSearchDraft(event.target.value)} className="pl-9" placeholder="Search property, document, tenant, or reference" />
            </div>
            <Select value={selectedAssetId} onValueChange={setSelectedAssetId}>
              <SelectTrigger><SelectValue placeholder="Select property" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Recent matching properties</SelectItem>
                {assets.map((asset) => (
                  <SelectItem key={asset.id} value={asset.id}>{propertyReference(asset)} · {asset.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button type="submit">Search</Button>
          </form>
        </CardHeader>
        <CardContent>
          {effectiveLoadError ? <div className="rounded-md border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive">{effectiveLoadError}</div> : null}
          {isLoading ? <div className="flex justify-center gap-2 py-10 text-sm text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin" /> Loading records index</div> : null}
          {!isLoading && documents.length > 0 ? (
            <div className="overflow-x-auto rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Document</TableHead>
                    <TableHead>Property / unit</TableHead>
                    <TableHead>Tenant / occupant</TableHead>
                    <TableHead>Category</TableHead>
                    <TableHead>DMS reference</TableHead>
                    <TableHead>Uploaded</TableHead>
                    <TableHead>Status</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {documents.map((document) => (
                    <TableRow key={document.id}>
                      <TableCell><div className="font-medium">{document.documentName || document.fileName}</div><div className="text-xs text-muted-foreground">{document.fileName}</div></TableCell>
                      <TableCell><div>{document.asset.name}</div><div className="text-xs text-muted-foreground">{propertyReference(document.asset)} · {sourceReference(document.asset)}</div></TableCell>
                      <TableCell>{occupantName(document.asset)}</TableCell>
                      <TableCell>{document.documentType}</TableCell>
                      <TableCell>{document.centralDocumentReference || 'Not linked'}</TableCell>
                      <TableCell>{formatEstateDate(document.uploadedAt)}</TableCell>
                      <TableCell><Badge variant={document.centralDocumentRecordId ? 'secondary' : 'outline'}>{document.centralDocumentRecordId ? 'DMS linked' : 'Asset file'}</Badge></TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          ) : null}
          {selectedAssetId === 'all' && totalPages > 1 ? <Pagination currentPage={page} totalPages={totalPages} totalItems={totalItems} pageSize={pageSize} onPageChange={setPage} /> : null}
          {!isLoading && !effectiveLoadError && documents.length === 0 ? <div className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">No documents found for the current selection.</div> : null}
        </CardContent>
      </Card>
    </div>
  );
}
