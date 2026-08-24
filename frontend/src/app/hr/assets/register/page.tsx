'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Boxes, Loader2, Plus, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useDebounce } from '@/hooks/use-debounce';
import { assetRegisterService } from '@/services/hr/asset-register.service';
import { COMPANY_ASSET_STATUSES } from '@/types/hr/assets';
import type { CompanyAssetStatus } from '@/types/hr/assets';

const PAGE_SIZE = 20;
const ANY = '__any__';

/**
 * ⚠ Deliberately NOT a currency format. `purchaseCost` is what the asset cost to acquire and the
 * register holds no currency code for it — the column is headed "Purchase cost" for the same
 * reason. It was called `currentValue` until slice 11 (defect D-jj), a name that promised a
 * depreciated figure over a number that never was one. HR has no valuation to give; Finance does.
 */
const fmtCost = (v: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { maximumFractionDigits: 2 });

/**
 * The register list — every company asset, filtered the way the API filters it.
 *
 * ⚠ The status filter sends the enum **NAME** (`'Available'`), while the create and edit forms send
 * the **number**. Both are measured; neither is guessable from the other.
 */
export default function AssetRegisterPage() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<string>(ANY);
  const [assetTypeId, setAssetTypeId] = useState<string>(ANY);
  const debouncedSearch = useDebounce(search, 300);

  const { data: types = [] } = useQuery({
    queryKey: ['hr', 'assets', 'types'],
    queryFn: () => assetRegisterService.getTypes(),
  });

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'assets', 'register', page, debouncedSearch, status, assetTypeId],
    queryFn: () =>
      assetRegisterService.getAssetsPaged({
        pageNumber: page,
        pageSize: PAGE_SIZE,
        searchTerm: debouncedSearch || undefined,
        status: status === ANY ? undefined : (status as CompanyAssetStatus),
        assetTypeId: assetTypeId === ANY ? undefined : assetTypeId,
      }),
  });

  const rows = data?.items ?? [];
  const resetToFirstPage = <T,>(setter: (v: T) => void) => (v: T) => { setter(v); setPage(1); };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Asset register"
        description="Every asset the organisation holds, and who has it."
        backHref="/hr/assets"
        actions={
          <Button asChild>
            <Link href="/hr/assets/register/new">
              <Plus className="mr-2 h-4 w-4" /> Register an asset
            </Link>
          </Button>
        }
      />

      <Card>
        <CardContent className="flex flex-wrap gap-3 p-4">
          <div className="relative min-w-[240px] flex-1">
            <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input
              className="pl-8"
              placeholder="Asset number, name, serial number…"
              value={search}
              onChange={(e) => resetToFirstPage(setSearch)(e.target.value)}
            />
          </div>
          <Select value={status} onValueChange={resetToFirstPage(setStatus)}>
            <SelectTrigger className="w-[190px]">
              <SelectValue placeholder="Any status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ANY}>Any status</SelectItem>
              {COMPANY_ASSET_STATUSES.map((s) => (
                <SelectItem key={s.label} value={s.label}>{s.text}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select value={assetTypeId} onValueChange={resetToFirstPage(setAssetTypeId)}>
            <SelectTrigger className="w-[220px]">
              <SelectValue placeholder="Any type" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ANY}>Any type</SelectItem>
              {types.map((t) => (
                <SelectItem key={t.id} value={t.id}>{t.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Boxes}
              title="No assets match"
              description="Nothing on the register answers to those filters."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Asset</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Condition</TableHead>
                  <TableHead>Held by</TableHead>
                  <TableHead>Unit</TableHead>
                  <TableHead>Location</TableHead>
                  <TableHead className="text-right">Purchase cost</TableHead>
                  <TableHead>Source</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((a) => (
                  <TableRow key={a.id} className="cursor-pointer">
                    <TableCell>
                      <Link href={`/hr/assets/register/${a.id}`} className="hover:underline">
                        <div className="font-medium">{a.assetName}</div>
                        <div className="text-xs text-muted-foreground">{a.assetNumber}</div>
                      </Link>
                    </TableCell>
                    <TableCell>{a.assetTypeName}</TableCell>
                    <TableCell><StatusBadge status={a.statusName} /></TableCell>
                    <TableCell>{a.conditionName}</TableCell>
                    <TableCell>
                      {a.isCurrentlyAssigned
                        ? a.currentAssignedToName ?? 'Assigned'
                        : <span className="text-muted-foreground">—</span>}
                    </TableCell>
                    {/* An asset with no unit or location is exactly what a register should surface. */}
                    <TableCell>{a.unitName ?? <span className="text-muted-foreground">Not set</span>}</TableCell>
                    <TableCell>{a.locationName ?? <span className="text-muted-foreground">Not set</span>}</TableCell>
                    <TableCell className="text-right">{fmtCost(a.purchaseCost)}</TableCell>
                    <TableCell>
                      {a.source === 'FixedAssetsModule'
                        ? <span title="Finance owns its purchase figures and disposal">Fixed assets</span>
                        : 'HR'}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {data && data.totalPages > 1 && (
        <div className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground">
            Page {data.page} of {data.totalPages} · {data.totalCount} asset
            {data.totalCount === 1 ? '' : 's'}
          </p>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={!data.hasPrevious}
              onClick={() => setPage((p) => p - 1)}>Previous</Button>
            <Button variant="outline" size="sm" disabled={!data.hasNext}
              onClick={() => setPage((p) => p + 1)}>Next</Button>
          </div>
        </div>
      )}
    </div>
  );
}
