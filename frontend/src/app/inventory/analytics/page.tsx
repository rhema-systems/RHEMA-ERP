'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { AlertTriangle, BarChart3, Clock3, Loader2, PackageSearch, RefreshCw, ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { inventoryAnalyticsService, type InventoryActivityClassification,
  type InventoryAnalytics } from '@/services/inventoryAnalyticsService';

const classification: Record<InventoryActivityClassification, string> = {
  0: 'Active', 1: 'Slow-moving', 2: 'Non-moving', 3: 'Stockout',
};
const money = (value: number) => new Intl.NumberFormat('en-GH', {
  style: 'currency', currency: 'GHS', maximumFractionDigits: 2,
}).format(value || 0);
const quantity = (value: number) => new Intl.NumberFormat('en-GH', { maximumFractionDigits: 4 }).format(value || 0);

export default function InventoryAnalyticsPage() {
  const [data, setData] = useState<InventoryAnalytics | null>(null);
  const [loading, setLoading] = useState(true);
  const [slowDays, setSlowDays] = useState(90);
  const [nonMovingDays, setNonMovingDays] = useState(180);
  const [expiryDays, setExpiryDays] = useState(90);
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState('all');

  const refresh = useCallback(async () => {
    setLoading(true);
    try {
      setData(await inventoryAnalyticsService.get({
        slowMovingDays: slowDays, nonMovingDays, expiryWarningDays: expiryDays, take: 1000,
      }));
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Unable to load inventory analytics.');
    } finally { setLoading(false); }
  }, [expiryDays, nonMovingDays, slowDays]);

  useEffect(() => { void refresh(); }, [refresh]);
  const rows = useMemo(() => (data?.items || []).filter(value => {
    const query = search.trim().toLowerCase();
    const matchesSearch = !query || [value.itemCode, value.itemName, value.warehouseCode,
      value.warehouseName, value.locationCode, value.locationName]
      .some(text => text?.toLowerCase().includes(query));
    const matchesFilter = filter === 'all' ||
      (filter === 'disposal' && value.disposalCandidate) ||
      (filter === 'replenishment' && value.replenishmentCandidate) ||
      (filter === String(value.activityClassification));
    return matchesSearch && matchesFilter;
  }), [data, filter, search]);
  const summary = data?.summary;

  return <div className="space-y-6 p-6">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div><h1 className="text-2xl font-semibold">Inventory Ageing & Action Analytics</h1>
        <p className="text-sm text-muted-foreground">Ageing, slow/non-moving, current stockout and expiry exposure by the exact item, warehouse and location.</p></div>
      <Button variant="outline" onClick={() => void refresh()} disabled={loading}>
        {loading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-2 h-4 w-4" />}Refresh
      </Button>
    </div>

    <Card><CardHeader><CardTitle><ShieldCheck className="mr-2 inline h-5 w-5" />Shared-owner boundary</CardTitle>
      <CardDescription>Posted movements remain the activity and demand source, active valuation layers remain the receipt-age and expiry source, and current balances remain the item/location cache. Replenishment actions link to the governed TDC-0612 lifecycle. Disposal candidates are read-only recommendations for TDC-0615; this page does not mutate stock or create a parallel replenishment/disposal workflow.</CardDescription>
    </CardHeader></Card>

    <Card><CardHeader><CardTitle>Classification thresholds</CardTitle><CardDescription>Change the analytic boundary and refresh; the source stock records remain unchanged.</CardDescription></CardHeader>
      <CardContent className="flex flex-wrap items-end gap-3">
        <div className="w-44 space-y-2"><Label>Slow-moving days</Label><Input type="number" min={1} value={slowDays} onChange={event => setSlowDays(Number(event.target.value))} /></div>
        <div className="w-44 space-y-2"><Label>Non-moving days</Label><Input type="number" min={2} value={nonMovingDays} onChange={event => setNonMovingDays(Number(event.target.value))} /></div>
        <div className="w-44 space-y-2"><Label>Expiry warning days</Label><Input type="number" min={1} value={expiryDays} onChange={event => setExpiryDays(Number(event.target.value))} /></div>
        <Button onClick={() => void refresh()} disabled={loading || nonMovingDays <= slowDays}><BarChart3 className="mr-2 h-4 w-4" />Apply thresholds</Button>
      </CardContent></Card>

    <div className="grid gap-4 md:grid-cols-4">
      <Card><CardHeader className="pb-2"><CardDescription>Inventory value</CardDescription><CardTitle>{money(summary?.inventoryValue || 0)}</CardTitle></CardHeader></Card>
      <Card><CardHeader className="pb-2"><CardDescription>Slow / non-moving</CardDescription><CardTitle>{summary?.slowMovingCount || 0} / {summary?.nonMovingCount || 0}</CardTitle></CardHeader></Card>
      <Card><CardHeader className="pb-2"><CardDescription>Current stockouts</CardDescription><CardTitle className={summary?.stockoutCount ? 'text-destructive' : ''}>{summary?.stockoutCount || 0}</CardTitle></CardHeader></Card>
      <Card><CardHeader className="pb-2"><CardDescription>Expired / expiring value</CardDescription><CardTitle>{money((summary?.expiredValue || 0) + (summary?.expiringValue || 0))}</CardTitle></CardHeader></Card>
    </div>

    <Card><CardHeader><CardTitle><Clock3 className="mr-2 inline h-5 w-5" />Inventory ageing bands</CardTitle>
      <CardDescription>Value is reconciled back to the current location balance; FIFO layers supply exact receipt age where available.</CardDescription></CardHeader>
      <CardContent><div className="overflow-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>Band</TableHead><TableHead>Item locations</TableHead><TableHead>Quantity</TableHead><TableHead>Value</TableHead></TableRow></TableHeader>
        <TableBody>{(data?.ageingBands || []).map(value => <TableRow key={value.key}><TableCell className="font-medium">{value.label}</TableCell><TableCell>{value.itemLocationCount}</TableCell><TableCell>{quantity(value.quantity)}</TableCell><TableCell>{money(value.value)}</TableCell></TableRow>)}</TableBody>
      </Table></div></CardContent></Card>

    <Card><CardHeader><CardTitle><PackageSearch className="mr-2 inline h-5 w-5" />Item/location action register</CardTitle>
      <CardDescription>Prioritized exceptions link to the existing governed owner; no recommendation button writes inventory data.</CardDescription></CardHeader>
      <CardContent className="space-y-4"><div className="flex flex-wrap gap-3"><Input className="max-w-sm" placeholder="Search item, warehouse or location" value={search} onChange={event => setSearch(event.target.value)} />
        <Select value={filter} onValueChange={setFilter}><SelectTrigger className="w-52"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">All classifications</SelectItem><SelectItem value="1">Slow-moving</SelectItem><SelectItem value="2">Non-moving</SelectItem><SelectItem value="3">Stockout</SelectItem><SelectItem value="disposal">Disposal candidates</SelectItem><SelectItem value="replenishment">Replenishment actions</SelectItem></SelectContent></Select></div>
        <div className="overflow-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>Item / location</TableHead><TableHead>Classification</TableHead><TableHead>Stock / value</TableHead><TableHead>Age / activity</TableHead><TableHead>Expiry</TableHead><TableHead>Action</TableHead></TableRow></TableHeader>
          <TableBody>{rows.length === 0 ? <TableRow><TableCell colSpan={6} className="h-24 text-center text-muted-foreground">No item/location analytics match the current scope.</TableCell></TableRow> : rows.map(value => <TableRow key={`${value.inventoryItemId}-${value.warehouseId}-${value.locationId || 'warehouse'}`}>
            <TableCell><div className="font-medium">{value.itemCode} · {value.itemName}</div><div className="text-xs text-muted-foreground">{value.warehouseCode} / {value.locationCode || 'Warehouse level'} · {value.categoryName}</div></TableCell>
            <TableCell><Badge variant={value.activityClassification === 3 ? 'destructive' : value.activityClassification === 0 ? 'secondary' : 'outline'}>{classification[value.activityClassification]}</Badge>{value.disposalCandidate && <Badge variant="destructive" className="ml-2">Disposal review</Badge>}</TableCell>
            <TableCell><div>{quantity(value.quantityAvailable)} available</div><div className="text-xs text-muted-foreground">{money(value.inventoryValue)}</div></TableCell>
            <TableCell><div>{value.oldestAgeingBand}</div><div className="text-xs text-muted-foreground">{value.daysSinceActivity} days since activity</div></TableCell>
            <TableCell><div>{quantity(value.expiredQuantity)} expired</div><div className="text-xs text-muted-foreground">{quantity(value.expiringQuantity)} expiring</div></TableCell>
            <TableCell className="max-w-sm"><div className="text-sm"><AlertTriangle className="mr-1 inline h-4 w-4" />{value.recommendedAction}</div>{value.replenishmentCandidate && <Button asChild size="sm" variant="outline" className="mt-2"><Link href={value.replenishmentPurchaseRequisitionId ? `/procurement/purchase-requisitions/${value.replenishmentPurchaseRequisitionId}` : '/inventory/replenishment'}>{value.replenishmentPurchaseRequisitionId ? `Open ${value.replenishmentPurchaseRequisitionNumber || 'draft PR'}` : value.replenishmentRecommendationNumber ? 'Open recommendation' : 'Open replenishment'}</Link></Button>}</TableCell>
          </TableRow>)}</TableBody></Table></div>
      </CardContent></Card>
    <p className="text-xs text-muted-foreground">As of {data?.asOfUtc ? new Date(data.asOfUtc).toLocaleString() : '—'} · {summary?.itemLocationCount || 0} authorized item/location balances.</p>
  </div>;
}
