'use client';

import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { format } from 'date-fns';
import { BarChart3, Download, Loader2, Package, RefreshCw, Search, Wrench } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useToast } from '@/hooks/use-toast';
import { useMaintenanceCurrency } from '@/hooks/useMaintenanceCurrency';
import maintenanceReportsService, {
  type AssetCostReportRow,
  type AssetMovementReportRow,
  type InspectionServiceReportRow,
  type MaintenanceOperationalReports,
  type PartIssuedReportRow,
  type WorkOrderCostReportRow,
  type WorkOrderStatusReportRow,
} from '@/services/maintenanceReportsService';

type ReportTab = 'movements' | 'inspection-service' | 'work-orders' | 'parts' | 'costs';

const dateInput = (value: Date) => format(value, 'yyyy-MM-dd');

const displayDateTime = (value?: string | null) => {
  if (!value) return '-';
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? '-' : format(parsed, 'MMM dd, yyyy HH:mm');
};

const csvValue = (value: unknown) => {
  const text = value == null ? '' : String(value);
  return `"${text.replace(/"/g, '""')}"`;
};

export default function MaintenanceReportsPage() {
  const { toast } = useToast();
  const { formatMoney } = useMaintenanceCurrency();
  const [fromDate, setFromDate] = useState(() => {
    const from = new Date();
    from.setFullYear(from.getFullYear() - 1);
    return dateInput(from);
  });
  const [toDate, setToDate] = useState(() => dateInput(new Date()));
  const [search, setSearch] = useState('');
  const [assetFilter, setAssetFilter] = useState('all');
  const [columnFilter, setColumnFilter] = useState('all');
  const [activeTab, setActiveTab] = useState<ReportTab>('movements');
  const [data, setData] = useState<MaintenanceOperationalReports | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadReports = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await maintenanceReportsService.getOperationalReports({
        fromUtc: new Date(`${fromDate}T00:00:00`).toISOString(),
        toUtc: new Date(`${toDate}T23:59:59.999`).toISOString(),
      });
      setData(result);
    } catch (loadError) {
      const message = loadError instanceof Error ? loadError.message : 'Maintenance reports could not be loaded.';
      setError(message);
      toast({ title: 'Reports unavailable', description: message, variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  }, [fromDate, toDate, toast]);

  useEffect(() => {
    void loadReports();
  }, [loadReports]);

  const matches = useCallback((...values: unknown[]) => {
    const needle = search.trim().toLowerCase();
    if (!needle) return true;
    return values.some((value) => String(value ?? '').toLowerCase().includes(needle));
  }, [search]);

  const passesFilters = useCallback((assetId: string, ...columnValues: unknown[]) => {
    const matchesAsset = assetFilter === 'all' || assetId === assetFilter;
    const matchesColumn = columnFilter === 'all' || columnValues.some((value) => String(value ?? '') === columnFilter);
    return matchesAsset && matchesColumn;
  }, [assetFilter, columnFilter]);

  const movements = useMemo(() => (data?.assetMovements ?? []).filter((row) => passesFilters(row.assetId, row.movementType) && matches(
    row.assetNumber, row.assetName, row.fromProject, row.fromSite, row.toProject, row.toSite, row.reason,
  )), [data, matches, passesFilters]);

  const inspectionService = useMemo(() => (data?.inspectionServiceHistory ?? []).filter((row) => passesFilters(row.assetId, row.recordType, row.sheetType, row.status, row.result) && matches(
    row.assetNumber, row.assetName, row.recordType, row.sheetType, row.templateName, row.status, row.result, row.inspectorName,
  )), [data, matches, passesFilters]);

  const workOrders = useMemo(() => (data?.workOrderStatus ?? []).filter((row) => passesFilters(row.assetId, row.status, row.priority, row.workOrderType, row.maintenanceType) && matches(
    row.workOrderNumber, row.assetNumber, row.assetName, row.title, row.status, row.workOrderType, row.maintenanceType,
  )), [data, matches, passesFilters]);

  const parts = useMemo(() => (data?.partsIssued ?? []).filter((row) => passesFilters(row.assetId, row.status, row.itemCode, row.itemName) && matches(
    row.workOrderNumber, row.assetNumber, row.assetName, row.itemCode, row.itemName, row.status,
  )), [data, matches, passesFilters]);

  const workOrderCosts = useMemo(() => (data?.costsByWorkOrder ?? []).filter((row) => passesFilters(row.assetId, row.status) && matches(
    row.workOrderNumber, row.assetNumber, row.assetName, row.title, row.status,
  )), [data, matches, passesFilters]);

  const assetCosts = useMemo(() => Array.from(workOrderCosts.reduce((groups, row) => {
    const existing = groups.get(row.assetId) ?? {
      assetId: row.assetId,
      assetNumber: row.assetNumber,
      assetName: row.assetName,
      workOrderCount: 0,
      partsCost: 0,
      laborCost: 0,
      otherCost: 0,
      totalCost: 0,
    };
    existing.workOrderCount += 1;
    existing.partsCost += row.partsCost;
    existing.laborCost += row.laborCost;
    existing.otherCost += row.otherCost;
    existing.totalCost += row.totalCost;
    groups.set(row.assetId, existing);
    return groups;
  }, new Map<string, AssetCostReportRow>()).values()).sort((a, b) => b.totalCost - a.totalCost), [workOrderCosts]);

  const assetOptions = useMemo(() => {
    const rows = [
      ...(data?.assetMovements ?? []),
      ...(data?.inspectionServiceHistory ?? []),
      ...(data?.workOrderStatus ?? []),
      ...(data?.partsIssued ?? []),
      ...(data?.costsByWorkOrder ?? []),
    ];
    return Array.from(new Map(rows.map((row) => [row.assetId, { id: row.assetId, label: `${row.assetNumber} · ${row.assetName}` }])).values())
      .sort((a, b) => a.label.localeCompare(b.label));
  }, [data]);

  const columnOptions = useMemo(() => {
    let values: Array<string | null | undefined> = [];
    if (activeTab === 'movements') values = (data?.assetMovements ?? []).map((row) => row.movementType);
    if (activeTab === 'inspection-service') values = (data?.inspectionServiceHistory ?? []).flatMap((row) => [row.recordType, row.sheetType, row.status, row.result]);
    if (activeTab === 'work-orders') values = (data?.workOrderStatus ?? []).flatMap((row) => [row.status, row.priority, row.workOrderType, row.maintenanceType]);
    if (activeTab === 'parts') values = (data?.partsIssued ?? []).flatMap((row) => [row.status, row.itemCode]);
    if (activeTab === 'costs') values = (data?.costsByWorkOrder ?? []).map((row) => row.status);
    return Array.from(new Set(values.filter((value): value is string => !!value))).sort();
  }, [activeTab, data]);

  const totalCost = (data?.costsByAsset ?? []).reduce((sum, row) => sum + row.totalCost, 0);

  const exportCsv = () => {
    let rows: Array<Record<string, unknown>> = [];
    let name: string = activeTab;

    if (activeTab === 'movements') rows = movements as unknown as Array<Record<string, unknown>>;
    if (activeTab === 'inspection-service') rows = inspectionService as unknown as Array<Record<string, unknown>>;
    if (activeTab === 'work-orders') rows = workOrders as unknown as Array<Record<string, unknown>>;
    if (activeTab === 'parts') rows = parts as unknown as Array<Record<string, unknown>>;
    if (activeTab === 'costs') {
      rows = workOrderCosts as unknown as Array<Record<string, unknown>>;
      name = 'cost-per-work-order';
    }

    if (rows.length === 0) {
      toast({ title: 'Nothing to export', description: 'The selected report has no rows for these filters.' });
      return;
    }

    const headers = Object.keys(rows[0]);
    const csv = [
      headers.map(csvValue).join(','),
      ...rows.map((row) => headers.map((header) => csvValue(row[header])).join(',')),
    ].join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `maintenance-${name}-${fromDate}-to-${toDate}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  };

  return (
        <div className="space-y-6 p-6">
          <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-end">
            <div>
              <div className="flex items-center gap-2">
                <Wrench className="h-7 w-7 text-primary" />
                <h1 className="text-3xl font-bold tracking-tight">Maintenance Reports</h1>
              </div>
              <p className="mt-1 text-muted-foreground">Live asset, inspection, work-order, parts and cost reporting.</p>
            </div>
            <Button onClick={exportCsv} disabled={loading || !data}><Download className="mr-2 h-4 w-4" />Export Current Report</Button>
          </div>

          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
            <ReportCountCard title="Asset movements" value={data?.assetMovements.length ?? 0} />
            <ReportCountCard title="Inspections / service" value={data?.inspectionServiceHistory.length ?? 0} />
            <ReportCountCard title="Work orders" value={data?.workOrderStatus.length ?? 0} />
            <ReportCountCard title="Parts issued" value={data?.partsIssued.length ?? 0} />
            <Card><CardHeader className="pb-2"><CardDescription>Maintenance cost</CardDescription><CardTitle>{formatMoney(totalCost)}</CardTitle></CardHeader></Card>
          </div>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">Report Filters</CardTitle>
              <CardDescription>Date range is applied at the server; asset, status/type, and search filters apply to the selected report.</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-[160px_160px_minmax(220px,1fr)_220px_minmax(260px,1fr)_auto] xl:items-end">
                <div className="space-y-1"><Label htmlFor="report-from">From date</Label><Input id="report-from" type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)} /></div>
                <div className="space-y-1"><Label htmlFor="report-to">To date</Label><Input id="report-to" type="date" value={toDate} onChange={(event) => setToDate(event.target.value)} /></div>
                <div className="space-y-1">
                  <Label>Asset</Label>
                  <Select value={assetFilter} onValueChange={setAssetFilter}>
                    <SelectTrigger><SelectValue placeholder="All assets" /></SelectTrigger>
                    <SelectContent><SelectItem value="all">All assets</SelectItem>{assetOptions.map((asset) => <SelectItem key={asset.id} value={asset.id}>{asset.label}</SelectItem>)}</SelectContent>
                  </Select>
                </div>
                <div className="space-y-1">
                  <Label>Status / type</Label>
                  <Select value={columnFilter} onValueChange={setColumnFilter}>
                    <SelectTrigger><SelectValue placeholder="All values" /></SelectTrigger>
                    <SelectContent><SelectItem value="all">All values</SelectItem>{columnOptions.map((value) => <SelectItem key={value} value={value}>{value}</SelectItem>)}</SelectContent>
                  </Select>
                </div>
                <div className="space-y-1">
                  <Label htmlFor="report-search">Search</Label>
                  <div className="relative"><Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" /><Input id="report-search" className="pl-9" placeholder="Asset, work order, part, technician..." value={search} onChange={(event) => setSearch(event.target.value)} /></div>
                </div>
                <div className="flex gap-2">
                  <Button variant="outline" onClick={() => void loadReports()} disabled={loading}><RefreshCw className={`mr-2 h-4 w-4 ${loading ? 'animate-spin' : ''}`} />Apply</Button>
                  <Button variant="ghost" onClick={() => { setSearch(''); setAssetFilter('all'); setColumnFilter('all'); }}>Clear</Button>
                </div>
              </div>
            </CardContent>
          </Card>

          {error ? (
            <Card className="border-destructive/40"><CardContent className="py-10 text-center"><p className="font-medium text-destructive">{error}</p><Button className="mt-4" variant="outline" onClick={() => void loadReports()}>Try again</Button></CardContent></Card>
          ) : loading && !data ? (
            <div className="flex items-center justify-center py-20 text-muted-foreground"><Loader2 className="mr-2 h-5 w-5 animate-spin" />Loading maintenance reports...</div>
          ) : (
            <Tabs value={activeTab} onValueChange={(value) => { setActiveTab(value as ReportTab); setColumnFilter('all'); }}>
              <TabsList className="h-auto flex-wrap justify-start">
                <TabsTrigger value="movements">Asset Movement</TabsTrigger>
                <TabsTrigger value="inspection-service">Inspection / Service</TabsTrigger>
                <TabsTrigger value="work-orders">Work Order Status</TabsTrigger>
                <TabsTrigger value="parts">Parts Issued</TabsTrigger>
                <TabsTrigger value="costs">Cost Analysis</TabsTrigger>
              </TabsList>

              <TabsContent value="movements"><MovementTable rows={movements} /></TabsContent>
              <TabsContent value="inspection-service"><InspectionServiceTable rows={inspectionService} /></TabsContent>
              <TabsContent value="work-orders"><WorkOrderTable rows={workOrders} /></TabsContent>
              <TabsContent value="parts"><PartsTable rows={parts} formatMoney={formatMoney} /></TabsContent>
              <TabsContent value="costs"><CostsReport assetRows={assetCosts} workOrderRows={workOrderCosts} formatMoney={formatMoney} /></TabsContent>
            </Tabs>
          )}
        </div>
  );
}

function ReportCountCard({ title, value }: { title: string; value: number }) {
  return <Card><CardHeader className="pb-2"><CardDescription>{title}</CardDescription><CardTitle>{value.toLocaleString()}</CardTitle></CardHeader></Card>;
}

function EmptyRow({ columns }: { columns: number }) {
  return <TableRow><TableCell colSpan={columns} className="h-28 text-center text-muted-foreground">No records match the selected period and search.</TableCell></TableRow>;
}

function ReportTable({ children }: { children: ReactNode }) {
  return <Card><CardContent className="overflow-x-auto p-0"><Table>{children}</Table></CardContent></Card>;
}

function MovementTable({ rows }: { rows: AssetMovementReportRow[] }) {
  return <ReportTable><TableHeader><TableRow><TableHead>Date / Time</TableHead><TableHead>Asset</TableHead><TableHead>From</TableHead><TableHead>To</TableHead><TableHead>Reason</TableHead></TableRow></TableHeader><TableBody>{rows.length ? rows.map((row) => <TableRow key={row.movementId}><TableCell className="whitespace-nowrap">{displayDateTime(row.effectiveAtUtc)}</TableCell><TableCell><strong>{row.assetNumber}</strong><div className="text-xs text-muted-foreground">{row.assetName}</div></TableCell><TableCell>{row.fromProject || 'Unassigned'}<div className="text-xs text-muted-foreground">{row.fromSite || 'Unassigned'}</div></TableCell><TableCell>{row.toProject || 'Unassigned'}<div className="text-xs text-muted-foreground">{row.toSite || 'Unassigned'}</div></TableCell><TableCell>{row.reason}<div className="text-xs text-muted-foreground">{row.notes}</div></TableCell></TableRow>) : <EmptyRow columns={5} />}</TableBody></ReportTable>;
}

function InspectionServiceTable({ rows }: { rows: InspectionServiceReportRow[] }) {
  return <ReportTable><TableHeader><TableRow><TableHead>Date / Time</TableHead><TableHead>Asset</TableHead><TableHead>Record</TableHead><TableHead>Checklist</TableHead><TableHead>Status / Result</TableHead><TableHead>Inspector</TableHead></TableRow></TableHeader><TableBody>{rows.length ? rows.map((row) => <TableRow key={`${row.source}-${row.recordId}`}><TableCell className="whitespace-nowrap">{displayDateTime(row.performedAtUtc)}</TableCell><TableCell><strong>{row.assetNumber}</strong><div className="text-xs text-muted-foreground">{row.assetName}</div></TableCell><TableCell><Badge variant="outline">{row.recordType}</Badge><div className="mt-1 text-xs text-muted-foreground">{row.source}</div></TableCell><TableCell>{row.templateName}<div className="text-xs text-muted-foreground">{row.sheetType} · {row.inspectionKind}</div></TableCell><TableCell><Badge variant={row.result === 'Fail' ? 'destructive' : 'outline'}>{row.result || row.status}</Badge><div className="mt-1 text-xs text-muted-foreground">{row.status}</div></TableCell><TableCell>{row.inspectorName || 'Not recorded'}</TableCell></TableRow>) : <EmptyRow columns={6} />}</TableBody></ReportTable>;
}

function WorkOrderTable({ rows }: { rows: WorkOrderStatusReportRow[] }) {
  return <ReportTable><TableHeader><TableRow><TableHead>Work Order</TableHead><TableHead>Asset</TableHead><TableHead>Type</TableHead><TableHead>Status</TableHead><TableHead>Technician</TableHead><TableHead>Created</TableHead><TableHead>Due / Completed</TableHead></TableRow></TableHeader><TableBody>{rows.length ? rows.map((row) => <TableRow key={row.workOrderId}><TableCell><strong>{row.workOrderNumber}</strong><div className="max-w-64 truncate text-xs text-muted-foreground">{row.title}</div></TableCell><TableCell>{row.assetNumber}<div className="text-xs text-muted-foreground">{row.assetName}</div></TableCell><TableCell>{row.workOrderType}<div className="text-xs text-muted-foreground">{row.maintenanceType} · {row.priority}</div></TableCell><TableCell><Badge variant={row.isOverdue ? 'destructive' : 'outline'}>{row.isOverdue ? 'Overdue' : row.status}</Badge></TableCell><TableCell>{row.assignedTechnician || 'Unassigned'}</TableCell><TableCell className="whitespace-nowrap">{displayDateTime(row.createdAtUtc)}</TableCell><TableCell className="whitespace-nowrap">{displayDateTime(row.completedAtUtc || row.requestedCompletionAtUtc)}</TableCell></TableRow>) : <EmptyRow columns={7} />}</TableBody></ReportTable>;
}

function PartsTable({ rows, formatMoney }: { rows: PartIssuedReportRow[]; formatMoney: (value: number) => string }) {
  return <ReportTable><TableHeader><TableRow><TableHead>Issued</TableHead><TableHead>Work Order</TableHead><TableHead>Asset</TableHead><TableHead>Part</TableHead><TableHead className="text-right">Issued / Used / Returned</TableHead><TableHead className="text-right">Unit Cost</TableHead><TableHead className="text-right">Total</TableHead></TableRow></TableHeader><TableBody>{rows.length ? rows.map((row) => <TableRow key={row.workOrderPartId}><TableCell className="whitespace-nowrap">{displayDateTime(row.issuedAtUtc)}</TableCell><TableCell>{row.workOrderNumber}</TableCell><TableCell>{row.assetNumber}<div className="text-xs text-muted-foreground">{row.assetName}</div></TableCell><TableCell><strong>{row.itemCode}</strong><div className="text-xs text-muted-foreground">{row.itemName}</div></TableCell><TableCell className="text-right">{row.quantityIssued} / {row.quantityUsed} / {row.quantityReturned}</TableCell><TableCell className="text-right">{formatMoney(row.unitCost)}</TableCell><TableCell className="text-right font-medium">{formatMoney(row.totalCost)}</TableCell></TableRow>) : <EmptyRow columns={7} />}</TableBody></ReportTable>;
}

function CostsReport({ assetRows, workOrderRows, formatMoney }: { assetRows: AssetCostReportRow[]; workOrderRows: WorkOrderCostReportRow[]; formatMoney: (value: number) => string }) {
  return <div className="space-y-4"><Card><CardHeader><CardTitle className="flex items-center gap-2 text-lg"><BarChart3 className="h-5 w-5" />Cost per Asset</CardTitle></CardHeader><CardContent className="overflow-x-auto p-0"><Table><TableHeader><TableRow><TableHead>Asset</TableHead><TableHead className="text-right">Work Orders</TableHead><TableHead className="text-right">Parts</TableHead><TableHead className="text-right">Labor</TableHead><TableHead className="text-right">Other</TableHead><TableHead className="text-right">Total</TableHead></TableRow></TableHeader><TableBody>{assetRows.length ? assetRows.map((row) => <TableRow key={row.assetId}><TableCell><strong>{row.assetNumber}</strong><div className="text-xs text-muted-foreground">{row.assetName}</div></TableCell><TableCell className="text-right">{row.workOrderCount}</TableCell><TableCell className="text-right">{formatMoney(row.partsCost)}</TableCell><TableCell className="text-right">{formatMoney(row.laborCost)}</TableCell><TableCell className="text-right">{formatMoney(row.otherCost)}</TableCell><TableCell className="text-right font-semibold">{formatMoney(row.totalCost)}</TableCell></TableRow>) : <EmptyRow columns={6} />}</TableBody></Table></CardContent></Card><Card><CardHeader><CardTitle className="flex items-center gap-2 text-lg"><Package className="h-5 w-5" />Cost per Work Order</CardTitle></CardHeader><CardContent className="overflow-x-auto p-0"><Table><TableHeader><TableRow><TableHead>Work Order</TableHead><TableHead>Asset</TableHead><TableHead>Status</TableHead><TableHead className="text-right">Parts</TableHead><TableHead className="text-right">Labor</TableHead><TableHead className="text-right">Other</TableHead><TableHead className="text-right">Total</TableHead></TableRow></TableHeader><TableBody>{workOrderRows.length ? workOrderRows.map((row) => <TableRow key={row.workOrderId}><TableCell><strong>{row.workOrderNumber}</strong><div className="max-w-64 truncate text-xs text-muted-foreground">{row.title}</div></TableCell><TableCell>{row.assetNumber}<div className="text-xs text-muted-foreground">{row.assetName}</div></TableCell><TableCell><Badge variant="outline">{row.status}</Badge></TableCell><TableCell className="text-right">{formatMoney(row.partsCost)}</TableCell><TableCell className="text-right">{formatMoney(row.laborCost)}</TableCell><TableCell className="text-right">{formatMoney(row.otherCost)}</TableCell><TableCell className="text-right font-semibold">{formatMoney(row.totalCost)}</TableCell></TableRow>) : <EmptyRow columns={7} />}</TableBody></Table></CardContent></Card></div>;
}
