'use client';

import React from 'react';
import Link from 'next/link';
import { Download, ExternalLink, RefreshCw, Search, UserRoundPen } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  EstateManagedAssetType,
  EstateManagedAssetStatus,
  estateLandManagementService,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';
import { estateAssetStatusLabels, formatEstateDate } from '../../property-management/[entityType]/property-workspace-utils';
import { facilitiesOperatingRows, type FacilitiesOperatingRow } from './facilities-operating-register';
import { FacilitiesDutyLookup } from './FacilitiesDutyLookup';
import { estateFacilitiesService, type FacilitiesStaffOption } from '@/services/estate-facilities.service';

const PAGE_SIZE = 50;
const FETCH_SIZE = 250;
const MAX_ASSETS = 5000;

async function downloadReport(rows: FacilitiesOperatingRow[], format: 'xlsx' | 'pdf' | 'csv') {
  const stamp = new Date().toISOString().slice(0, 10);
  const data = rows.map(({ asset, site, unit, termEnd, nextBilling, attention }) => ({
    Property: asset.name,
    Reference: asset.assetCode,
    Site: site,
    Block: asset.blockName || '',
    Floor: asset.floorLabel || '',
    Unit: unit,
    Status: estateAssetStatusLabels[asset.status],
    Occupier: asset.lesseeName || '',
    'Responsible officer': asset.responsibleOfficerName || '',
    'Term ends': termEnd || '',
    'Next rent billing': nextBilling || '',
    Attention: attention.join('; '),
  }));
  if (format === 'xlsx' || format === 'csv') {
    const XLSX = await import('xlsx');
    const sheet = XLSX.utils.json_to_sheet(data);
    sheet['!cols'] = [28, 20, 28, 16, 12, 20, 18, 26, 24, 16, 20, 36].map((wch) => ({ wch }));
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, sheet, 'Facilities operating');
    XLSX.writeFile(book, `facilities-operating-${stamp}.${format}`, { bookType: format });
    return;
  }
  const { jsPDF } = await import('jspdf');
  const pdf = new jsPDF();
  pdf.setFontSize(15);
  pdf.text('Facilities operating register', 14, 18);
  pdf.setFontSize(9);
  pdf.text(`${stamp}  |  ${rows.length} records`, 14, 26);
  let y = 36;
  for (const row of data) {
    const lines = pdf.splitTextToSize(
      `${row.Reference}  |  ${row.Property}  |  ${row.Status}\n${row.Site} / ${row.Unit}  |  Occupier: ${row.Occupier || '-'}\nTerm: ${row['Term ends'] || '-'}  |  Next billing: ${row['Next rent billing'] || '-'}${row.Attention ? `  |  ${row.Attention}` : ''}`,
      180
    ) as string[];
    if (y + lines.length * 5 + 5 > 280) {
      pdf.addPage();
      y = 18;
    }
    pdf.text(lines, 14, y);
    y += lines.length * 5 + 5;
  }
  pdf.save(`facilities-operating-${stamp}.pdf`);
}

export function FacilitiesOperatingRegister({ mode }: { mode: 'site' | 'lease' }) {
  const [assets, setAssets] = React.useState<EstateManagedAsset[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);
  const [truncated, setTruncated] = React.useState(false);
  const [search, setSearch] = React.useState('');
  const [status, setStatus] = React.useState('all');
  const [attentionOnly, setAttentionOnly] = React.useState(false);
  const [page, setPage] = React.useState(0);
  const [officerAsset, setOfficerAsset] = React.useState<EstateManagedAsset | null>(null);
  const [officerError, setOfficerError] = React.useState<string | null>(null);
  const searchStaff = React.useCallback((query: string) => estateFacilitiesService.searchDutyStaff(query), []);

  const load = React.useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const all: EstateManagedAsset[] = [];
      let hasMore = false;
      for (const assetType of [EstateManagedAssetType.Property, EstateManagedAssetType.Facility]) {
        let count = 0;
        let moreForType = true;
        while (count < MAX_ASSETS && moreForType) {
          const batch = await estateLandManagementService.getManagedAssets({ assetType, skip: count, take: FETCH_SIZE });
          all.push(...batch);
          count += batch.length;
          moreForType = batch.length === FETCH_SIZE;
        }
        hasMore ||= moreForType;
      }
      setTruncated(hasMore);
      setAssets(all);
      setPage(0);
    } catch {
      setError('Unable to load the Estate property register.');
    } finally {
      setLoading(false);
    }
  }, []);

  React.useEffect(() => { void load(); }, [load]);

  const rows = React.useMemo(() => facilitiesOperatingRows(assets, mode), [assets, mode]);
  const filtered = React.useMemo(() => {
    const query = search.trim().toLowerCase();
    return rows.filter(({ asset, site, unit, attention }) =>
      (status === 'all' || asset.status === Number(status)) &&
      (!attentionOnly || attention.length > 0) &&
      (!query || [asset.name, asset.assetCode, site, unit, asset.blockName,
        asset.floorLabel, asset.lesseeName].some((value) => value?.toLowerCase().includes(query)))
    );
  }, [rows, search, status, attentionOnly]);
  const visible = filtered.slice(page * PAGE_SIZE, (page + 1) * PAGE_SIZE);
  const alertCount = rows.filter((row) => row.attention.length > 0).length;
  const ownerPath = mode === 'lease'
    ? '/estate/property-management/EstatePropertyManagementLease'
    : '/estate/property-management/EstatePropertyManagementPropertyUnit';

  return (
    <section className="space-y-3" aria-label={mode === 'lease' ? 'Lease monitoring' : 'Property and site register'}>
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="mr-auto text-lg font-semibold">{mode === 'lease' ? 'Lease monitoring' : 'Property and site register'}</h2>
        <Badge variant="outline">{rows.length} records</Badge>
        {alertCount > 0 ? <Badge variant="destructive">{alertCount} need attention</Badge> : null}
        <Button size="icon" variant="outline" title="Refresh register" aria-label="Refresh register" onClick={() => void load()} disabled={loading}>
          <RefreshCw className="h-4 w-4" />
        </Button>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button size="icon" variant="outline" title="Export register" aria-label="Export register" disabled={filtered.length === 0}>
              <Download className="h-4 w-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onSelect={() => void downloadReport(filtered, 'xlsx').catch(() => setError('Unable to export Excel report.'))}>Excel</DropdownMenuItem>
            <DropdownMenuItem onSelect={() => void downloadReport(filtered, 'csv').catch(() => setError('Unable to export CSV report.'))}>CSV</DropdownMenuItem>
            <DropdownMenuItem onSelect={() => void downloadReport(filtered, 'pdf').catch(() => setError('Unable to export PDF report.'))}>PDF</DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
        <Button asChild size="sm" variant="outline"><Link href={ownerPath}>Open Estate <ExternalLink className="ml-2 h-4 w-4" /></Link></Button>
      </div>
      <div className="flex flex-wrap items-center gap-2">
        <div className="relative min-w-[220px] flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input aria-label="Search properties" placeholder="Search property, site, unit or occupier" className="pl-9" value={search}
            onChange={(event) => { setSearch(event.target.value); setPage(0); }} />
        </div>
        <Select value={status} onValueChange={(value) => { setStatus(value); setPage(0); }}>
          <SelectTrigger className="w-[180px]" aria-label="Filter status"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            {Object.entries(estateAssetStatusLabels).map(([value, label]) =>
              <SelectItem key={value} value={value}>{label}</SelectItem>)}
          </SelectContent>
        </Select>
        <Button size="sm" variant={attentionOnly ? 'default' : 'outline'} aria-pressed={attentionOnly}
          onClick={() => { setAttentionOnly(!attentionOnly); setPage(0); }}>Needs attention</Button>
      </div>
      {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
      {truncated ? <p role="status" className="text-sm text-muted-foreground">More than {MAX_ASSETS} records exist for a property type. Use the Estate register to review additional records.</p> : null}
      <div className="overflow-x-auto border-y">
        <Table className="min-w-[980px]">
          <TableHeader><TableRow>
            <TableHead>Property</TableHead><TableHead>Site / unit</TableHead><TableHead>Status</TableHead>
            <TableHead>Occupier</TableHead>{mode === 'site' ? <TableHead>Responsible officer</TableHead> : null}
            <TableHead>Term ends</TableHead><TableHead>Next rent billing</TableHead><TableHead>Attention</TableHead>
          </TableRow></TableHeader>
          <TableBody>
            {loading ? <TableRow><TableCell colSpan={mode === 'site' ? 8 : 7}>Loading Estate records...</TableCell></TableRow>
              : visible.length === 0 ? <TableRow><TableCell colSpan={mode === 'site' ? 8 : 7}>No properties match the current filters.</TableCell></TableRow>
              : visible.map(({ asset, site, unit, termEnd, nextBilling, attention }) =>
                <TableRow key={asset.id}>
                  <TableCell><div className="font-medium">{asset.name}</div><div className="text-xs text-muted-foreground">{asset.assetCode}</div></TableCell>
                  <TableCell><div>{site}</div><div className="text-xs text-muted-foreground">{[asset.blockName, asset.floorLabel, unit].filter(Boolean).join(' / ')}</div></TableCell>
                  <TableCell>{estateAssetStatusLabels[asset.status]}</TableCell>
                  <TableCell>{asset.lesseeName || '-'}</TableCell>
                  {mode === 'site' ? <TableCell><div className="flex items-center gap-1">
                    <span>{asset.responsibleOfficerName || '-'}</span>
                    <Button size="icon" variant="ghost" title={`Assign responsible officer for ${asset.name}`}
                      aria-label={`Assign responsible officer for ${asset.name}`}
                      onClick={() => { setOfficerError(null); setOfficerAsset(asset); }}>
                      <UserRoundPen className="h-4 w-4" />
                    </Button>
                  </div></TableCell> : null}
                  <TableCell>{formatEstateDate(termEnd)}</TableCell>
                  <TableCell>{formatEstateDate(nextBilling)}</TableCell>
                  <TableCell>{attention.length ? attention.map((item) => <Badge key={item} variant="destructive" className="mr-1 mb-1">{item}</Badge>) : '-'}</TableCell>
                </TableRow>)}
          </TableBody>
        </Table>
      </div>
      <div className="flex items-center justify-between text-sm text-muted-foreground">
        <span>{filtered.length ? `${page * PAGE_SIZE + 1}-${Math.min((page + 1) * PAGE_SIZE, filtered.length)} of ${filtered.length}` : '0 records'}</span>
        <div className="flex gap-2">
          <Button size="sm" variant="outline" disabled={page === 0} onClick={() => setPage(page - 1)}>Previous</Button>
          <Button size="sm" variant="outline" disabled={(page + 1) * PAGE_SIZE >= filtered.length} onClick={() => setPage(page + 1)}>Next</Button>
        </div>
      </div>
      <Dialog open={officerAsset !== null} onOpenChange={(open) => { if (!open) setOfficerAsset(null); }}>
        <DialogContent aria-describedby={undefined}>
          <DialogHeader><DialogTitle>Responsible officer: {officerAsset?.name}</DialogTitle></DialogHeader>
          {officerError ? <p role="alert" className="text-sm text-destructive">{officerError}</p> : null}
          <FacilitiesDutyLookup<FacilitiesStaffOption>
            label="HR employee"
            selectedLabel={officerAsset?.responsibleOfficerName || undefined}
            search={searchStaff}
            describe={(person) => ({ title: person.staffName, detail: [person.employeeNumber, person.department].filter(Boolean).join(' / ') })}
            onSelect={(person) => {
              if (!officerAsset) return;
              void estateFacilitiesService.assignSiteOfficer(officerAsset.id, person.id).then((assignment) => {
                setAssets((current) => current.map((asset) => asset.id === officerAsset.id
                  ? { ...asset, ...assignment } : asset));
                setOfficerAsset(null);
              }).catch(() => setOfficerError('Unable to assign this HR employee.'));
            }}
          />
        </DialogContent>
      </Dialog>
    </section>
  );
}
