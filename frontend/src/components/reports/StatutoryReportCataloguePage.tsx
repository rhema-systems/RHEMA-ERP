'use client';

import React, { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import {
  AlertTriangle,
  BarChart3,
  BookOpenCheck,
  Boxes,
  Building2,
  CalendarClock,
  ChevronLeft,
  ChevronRight,
  ClipboardCheck,
  Download,
  FileCheck2,
  FileSpreadsheet,
  History,
  Loader2,
  PackageSearch,
  RefreshCw,
  Scale,
  Send,
  ShieldCheck,
  Trash2,
  Warehouse,
  type LucideIcon,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { businessPartnerService } from '@/services/businessPartnerService';
import { financeDataService } from '@/services/finance/finance-data.service';
import { inventoryManagementService } from '@/services/inventoryManagementService';
import { ReportDefinition, ReportResult, reportsService } from '@/services/reports';

type CatalogueMode = 'procurement' | 'inventory';
type ExportFormat = 'pdf' | 'xlsx' | 'csv';

interface CatalogueItem {
  code: string;
  title: string;
  icon: LucideIcon;
}

const procurementCatalogue: CatalogueItem[] = [
  { code: 'app-vs-actual', title: 'APP vs Actual', icon: BarChart3 },
  { code: 'tender-register', title: 'Tender Register', icon: FileSpreadsheet },
  { code: 'contract-register', title: 'Contract Register', icon: FileCheck2 },
  { code: 'supplier-performance', title: 'Supplier Performance', icon: Building2 },
  { code: 'award-notification', title: 'Award Notifications', icon: Send },
  { code: 'savings-register', title: 'Savings Register', icon: Scale },
  { code: 'etc-minutes-register', title: 'ETC Minutes', icon: BookOpenCheck },
];

const inventoryCatalogue: CatalogueItem[] = [
  { code: 'balance-register', title: 'Balance Register', icon: Warehouse },
  { code: 'movement-register', title: 'Movement Register', icon: History },
  { code: 'ageing-register', title: 'Ageing Register', icon: CalendarClock },
  { code: 'reorder-register', title: 'Reorder Register', icon: PackageSearch },
  { code: 'count-variance-register', title: 'Count Variances', icon: ClipboardCheck },
  { code: 'valuation-gl-register', title: 'Valuation to GL', icon: Scale },
  { code: 'slow-non-moving-register', title: 'Slow / Non-moving', icon: Boxes },
  { code: 'expiry-register', title: 'Expiry Register', icon: AlertTriangle },
  { code: 'disposal-register', title: 'Disposal Register', icon: Trash2 },
];

const supplierFilterReports = new Set(['contract-register', 'supplier-performance', 'award-notification']);

function displayValue(value: unknown, dataType?: string, format?: string) {
  if (value === null || value === undefined || value === '') return '\u2014';
  if (dataType === 'Boolean') return value ? 'Yes' : 'No';
  if (dataType === 'DateTime') {
    const date = new Date(String(value));
    return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleString();
  }
  if (dataType === 'Decimal' || format === 'N2') {
    const number = Number(value);
    return Number.isFinite(number)
      ? number.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
      : String(value);
  }
  return String(value);
}

export function StatutoryReportCataloguePage({ mode }: { mode: CatalogueMode }) {
  const { hasAnyRole, hasPermission } = useAuth();
  const { toast } = useToast();
  const isInventory = mode === 'inventory';
  const catalogue = isInventory ? inventoryCatalogue : procurementCatalogue;
  const taskCode = isInventory ? 'TDC-0702' : 'TDC-0701';
  const isAdministrator = hasAnyRole(['SuperAdmin', 'TenantAdmin']);
  const canRead = isAdministrator || hasPermission('procurement.reports.read');
  const canExport = isAdministrator || hasPermission('procurement.reports.export');
  const [mounted, setMounted] = useState(false);
  const [selectedReportId, setSelectedReportId] = useState('');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [fiscalYear, setFiscalYear] = useState('');
  const [status, setStatus] = useState('');
  const [supplierId, setSupplierId] = useState('all');
  const [warehouseId, setWarehouseId] = useState('all');
  const [categoryId, setCategoryId] = useState('all');
  const [movementType, setMovementType] = useState('all');
  const [fiscalPeriodId, setFiscalPeriodId] = useState('all');
  const [slowMovingDays, setSlowMovingDays] = useState('90');
  const [nonMovingDays, setNonMovingDays] = useState('180');
  const [expiryWarningDays, setExpiryWarningDays] = useState('90');
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<ReportResult | null>(null);
  const [exporting, setExporting] = useState<ExportFormat | null>(null);

  const reportsQuery = useQuery({
    queryKey: ['reports', mode, taskCode],
    queryFn: () => reportsService.getReports(mode, 'published'),
    enabled: canRead,
    refetchOnWindowFocus: false,
  });
  const reports = useMemo(
    () => (reportsQuery.data ?? []).filter(report => report.tags?.includes(taskCode)),
    [reportsQuery.data, taskCode],
  );
  const suppliersQuery = useQuery({
    queryKey: ['business-partners', 'report-filter'],
    queryFn: () => businessPartnerService.getAllPartnersForDropdown(),
    enabled: canRead && !isInventory,
    staleTime: 5 * 60 * 1000,
  });
  const warehousesQuery = useQuery({
    queryKey: ['inventory-warehouses', 'statutory-report-filter'],
    queryFn: () => inventoryManagementService.getWarehouses(true),
    enabled: canRead && isInventory,
    staleTime: 5 * 60 * 1000,
  });
  const categoriesQuery = useQuery({
    queryKey: ['inventory-categories', 'statutory-report-filter'],
    queryFn: () => inventoryManagementService.getInventoryCategories(true),
    enabled: canRead && isInventory,
    staleTime: 5 * 60 * 1000,
  });
  const movementTypesQuery = useQuery({
    queryKey: ['inventory-movement-types', 'statutory-report-filter'],
    queryFn: () => inventoryManagementService.getStockMovementTypes(),
    enabled: canRead && isInventory,
    staleTime: 5 * 60 * 1000,
  });
  const fiscalPeriodsQuery = useQuery({
    queryKey: ['fiscal-periods', 'inventory-statutory-report-filter'],
    queryFn: () => financeDataService.getFiscalPeriods(),
    enabled: canRead && isInventory,
    staleTime: 5 * 60 * 1000,
  });

  useEffect(() => {
    setMounted(true);
  }, []);

  useEffect(() => {
    if (!selectedReportId && reports.length > 0) setSelectedReportId(reports[0].id);
  }, [reports, selectedReportId]);

  const selectedReport = reports.find(report => report.id === selectedReportId);
  const selectedCode = selectedReport?.tags?.find(tag => catalogue.some(item => item.code === tag));
  const parameters = useMemo(() => ({
    ...(startDate ? { startDate } : {}),
    ...(endDate ? { endDate } : {}),
    ...(!isInventory && fiscalYear ? { fiscalYear: Number(fiscalYear) } : {}),
    ...(status.trim() ? { status: status.trim() } : {}),
    ...(!isInventory && supplierId !== 'all' ? { supplierId } : {}),
    ...(isInventory && warehouseId !== 'all' ? { warehouseId } : {}),
    ...(isInventory && categoryId !== 'all' ? { categoryId } : {}),
    ...(isInventory && movementType !== 'all' ? { movementType } : {}),
    ...(isInventory && fiscalPeriodId !== 'all' ? { fiscalPeriodId } : {}),
    ...(isInventory && slowMovingDays ? { slowMovingDays: Number(slowMovingDays) } : {}),
    ...(isInventory && nonMovingDays ? { nonMovingDays: Number(nonMovingDays) } : {}),
    ...(isInventory && expiryWarningDays ? { expiryWarningDays: Number(expiryWarningDays) } : {}),
  }), [
    categoryId, endDate, expiryWarningDays, fiscalPeriodId, fiscalYear, isInventory, movementType,
    nonMovingDays, slowMovingDays, startDate, status, supplierId, warehouseId,
  ]);

  const executeMutation = useMutation({
    mutationFn: ({ reportId, targetPage }: { reportId: string; targetPage: number }) =>
      reportsService.executeReport(reportId, { parameters, includeMetadata: true, page: targetPage, pageSize: 100 }),
    onSuccess: data => {
      setResult(data);
      setPage(data.currentPage);
    },
    onError: (error: Error) => toast({ title: 'Report generation failed', description: error.message, variant: 'destructive' }),
  });

  const runReport = (targetPage = 1) => {
    if (selectedReportId) executeMutation.mutate({ reportId: selectedReportId, targetPage });
  };

  const selectReport = (reportId: string) => {
    setSelectedReportId(reportId);
    setResult(null);
    setPage(1);
    setStatus('');
  };

  const exportReport = async (format: ExportFormat) => {
    if (!selectedReportId) return;
    setExporting(format);
    try {
      const { blob, fileName } = await reportsService.exportReport(selectedReportId, { format, parameters });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = fileName;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);
      toast({ title: 'Report exported', description: fileName });
    } catch (error) {
      toast({
        title: 'Export failed',
        description: error instanceof Error ? error.message : 'The report could not be exported.',
        variant: 'destructive',
      });
    } finally {
      setExporting(null);
    }
  };

  if (!mounted) {
    return <Card><CardContent className="flex h-32 items-center justify-center"><Loader2 className="h-5 w-5 animate-spin" /></CardContent></Card>;
  }

  if (!canRead) {
    return (
      <Card className="border-amber-200 bg-amber-50">
        <CardHeader>
          <CardTitle className="flex items-center gap-2"><ShieldCheck className="h-5 w-5" /> {isInventory ? 'Inventory' : 'Procurement'} reports</CardTitle>
          <CardDescription>An active tenant responsibility granting procurement.reports.read is required.</CardDescription>
        </CardHeader>
      </Card>
    );
  }

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-2xl font-semibold tracking-tight">{isInventory ? 'Inventory' : 'Procurement'} statutory reports</h1>
            <Badge variant="outline">{taskCode}</Badge>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            Shared definitions, tenant and warehouse/location scope controls, execution history, and governed exports.
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={() => reportsQuery.refetch()} disabled={reportsQuery.isFetching}>
          <RefreshCw className={`mr-2 h-4 w-4 ${reportsQuery.isFetching ? 'animate-spin' : ''}`} /> Refresh catalogue
        </Button>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {catalogue.map(item => {
          const report = reports.find(candidate => candidate.tags?.includes(item.code));
          const Icon = item.icon;
          const active = report?.id === selectedReportId;
          return (
            <button
              type="button"
              key={item.code}
              disabled={!report}
              onClick={() => report && selectReport(report.id)}
              className={`rounded-lg border p-3 text-left transition ${active ? 'border-blue-500 bg-blue-50 shadow-sm' : 'bg-card hover:border-slate-400'} disabled:cursor-not-allowed disabled:opacity-50`}
            >
              <div className="flex items-center gap-2">
                <span className={`rounded-md p-2 ${active ? 'bg-blue-600 text-white' : 'bg-slate-100 text-slate-700'}`}><Icon className="h-4 w-4" /></span>
                <div><div className="text-sm font-medium">{item.title}</div><div className="text-xs text-muted-foreground">{report ? 'Published' : 'Not installed'}</div></div>
              </div>
            </button>
          );
        })}
      </div>

      {reportsQuery.isLoading ? (
        <Card><CardContent className="flex h-32 items-center justify-center"><Loader2 className="h-5 w-5 animate-spin" /></CardContent></Card>
      ) : reportsQuery.isError ? (
        <Card className="border-red-200"><CardContent className="py-6 text-sm text-red-700">The {mode} report catalogue could not be loaded.</CardContent></Card>
      ) : reports.length !== catalogue.length ? (
        <Card className="border-amber-200 bg-amber-50"><CardContent className="py-4 text-sm text-amber-900">The database catalogue is incomplete. Run the current migration and statutory report seeder.</CardContent></Card>
      ) : null}

      {selectedReport && (
        <Card>
          <CardHeader className="pb-3"><CardTitle className="text-lg">{selectedReport.name}</CardTitle><CardDescription>{selectedReport.description}</CardDescription></CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
              <div className="space-y-1.5"><Label htmlFor={`${mode}-report-start`}>Start date</Label><Input id={`${mode}-report-start`} type="date" value={startDate} onChange={event => setStartDate(event.target.value)} /></div>
              <div className="space-y-1.5"><Label htmlFor={`${mode}-report-end`}>End date</Label><Input id={`${mode}-report-end`} type="date" value={endDate} onChange={event => setEndDate(event.target.value)} /></div>
              <div className="space-y-1.5"><Label htmlFor={`${mode}-report-status`}>Status / classification</Label><Input id={`${mode}-report-status`} placeholder="All statuses" value={status} onChange={event => setStatus(event.target.value)} /></div>
              {!isInventory ? (
                <>
                  <div className="space-y-1.5"><Label htmlFor="procurement-report-year">Fiscal year</Label><Input id="procurement-report-year" type="number" min="2000" max="2200" placeholder="All years" value={fiscalYear} onChange={event => setFiscalYear(event.target.value)} disabled={selectedCode !== 'app-vs-actual'} /></div>
                  <div className="space-y-1.5"><Label>Supplier</Label><Select value={supplierId} onValueChange={setSupplierId} disabled={!supplierFilterReports.has(selectedCode ?? '')}><SelectTrigger><SelectValue placeholder="All suppliers" /></SelectTrigger><SelectContent><SelectItem value="all">All suppliers</SelectItem>{(suppliersQuery.data ?? []).map(partner => <SelectItem key={partner.id} value={partner.id}>{partner.partnerCode} · {partner.partnerName}</SelectItem>)}</SelectContent></Select></div>
                </>
              ) : (
                <>
                  <div className="space-y-1.5"><Label>Warehouse</Label><Select value={warehouseId} onValueChange={setWarehouseId}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">All permitted warehouses</SelectItem>{(warehousesQuery.data ?? []).map(item => <SelectItem key={item.id} value={item.id}>{item.code} · {item.name}</SelectItem>)}</SelectContent></Select></div>
                  <div className="space-y-1.5"><Label>Category</Label><Select value={categoryId} onValueChange={setCategoryId}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">All categories</SelectItem>{(categoriesQuery.data ?? []).map(item => <SelectItem key={item.id} value={item.id}>{item.code} · {item.name}</SelectItem>)}</SelectContent></Select></div>
                  <div className="space-y-1.5"><Label>Movement type</Label><Select value={movementType} onValueChange={setMovementType} disabled={selectedCode !== 'movement-register'}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">All movement types</SelectItem>{(movementTypesQuery.data ?? []).map(item => <SelectItem key={item} value={item}>{item}</SelectItem>)}</SelectContent></Select></div>
                  <div className="space-y-1.5"><Label>Fiscal period</Label><Select value={fiscalPeriodId} onValueChange={setFiscalPeriodId} disabled={selectedCode !== 'valuation-gl-register'}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">All fiscal periods</SelectItem>{(fiscalPeriodsQuery.data ?? []).map(item => <SelectItem key={item.id} value={item.id}>{item.periodCode} · {item.periodName}</SelectItem>)}</SelectContent></Select></div>
                  <div className="space-y-1.5"><Label htmlFor="slow-moving-days">Slow-moving days</Label><Input id="slow-moving-days" type="number" min="1" value={slowMovingDays} onChange={event => setSlowMovingDays(event.target.value)} disabled={selectedCode !== 'slow-non-moving-register'} /></div>
                  <div className="space-y-1.5"><Label htmlFor="non-moving-days">Non-moving days</Label><Input id="non-moving-days" type="number" min="1" value={nonMovingDays} onChange={event => setNonMovingDays(event.target.value)} disabled={selectedCode !== 'slow-non-moving-register'} /></div>
                  <div className="space-y-1.5"><Label htmlFor="expiry-warning-days">Expiry warning days</Label><Input id="expiry-warning-days" type="number" min="1" value={expiryWarningDays} onChange={event => setExpiryWarningDays(event.target.value)} disabled={selectedCode !== 'expiry-register'} /></div>
                </>
              )}
            </div>
            <div className="flex flex-wrap items-center justify-between gap-2 border-t pt-4">
              <div className="text-xs text-muted-foreground">Filters and generation metadata are retained in the shared report execution/export records.</div>
              <div className="flex flex-wrap gap-2">
                {canExport && (['xlsx', 'pdf', 'csv'] as ExportFormat[]).map(format => (
                  <Button key={format} variant="outline" size="sm" onClick={() => exportReport(format)} disabled={!!exporting || executeMutation.isPending}>
                    {exporting === format ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Download className="mr-2 h-4 w-4" />}{format.toUpperCase()}
                  </Button>
                ))}
                <Button size="sm" onClick={() => runReport(1)} disabled={executeMutation.isPending}>
                  {executeMutation.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <FileSpreadsheet className="mr-2 h-4 w-4" />} Run report
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {result && (
        <Card>
          <CardHeader className="pb-3"><div className="flex flex-wrap items-center justify-between gap-2"><div><CardTitle className="text-lg">Results</CardTitle><CardDescription>{result.totalRows.toLocaleString()} source row(s) · generated {new Date(result.executedAt).toLocaleString()}</CardDescription></div><Badge variant="secondary">Page {result.currentPage} of {Math.max(result.totalPages, 1)}</Badge></div></CardHeader>
          <CardContent className="space-y-3">
            <div className="overflow-x-auto rounded-md border">
              <Table>
                <TableHeader><TableRow>{result.columns.filter(column => column.isVisible).map(column => <TableHead key={column.name}>{column.displayName ?? column.name}</TableHead>)}</TableRow></TableHeader>
                <TableBody>{result.data.length === 0 ? <TableRow><TableCell colSpan={result.columns.length} className="h-24 text-center text-muted-foreground">No source transactions match the selected filters.</TableCell></TableRow> : result.data.map((row, rowIndex) => <TableRow key={`${result.currentPage}-${rowIndex}`}>{result.columns.filter(column => column.isVisible).map(column => <TableCell key={column.name} className="whitespace-nowrap">{displayValue(row[column.name], column.dataType, column.format)}</TableCell>)}</TableRow>)}</TableBody>
              </Table>
            </div>
            <div className="flex items-center justify-between"><span className="text-xs text-muted-foreground">Source: {result.metadata?.dataSource ?? `ERP ${mode} transactions`}</span><div className="flex gap-2"><Button variant="outline" size="sm" disabled={!result.hasPreviousPage || executeMutation.isPending} onClick={() => runReport(page - 1)}><ChevronLeft className="mr-1 h-4 w-4" /> Previous</Button><Button variant="outline" size="sm" disabled={!result.hasNextPage || executeMutation.isPending} onClick={() => runReport(page + 1)}>Next <ChevronRight className="ml-1 h-4 w-4" /></Button></div></div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
