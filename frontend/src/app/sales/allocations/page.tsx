'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { format } from 'date-fns';
import * as XLSX from 'xlsx';
import { ArrowUpRight, Download, Eye, MapPinned, RefreshCw, Search } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { salesAllocationService, type SalesAllocationDto } from '@/services/salesAllocationService';
import { salesSetupService, type SalesSaleableSourceDto } from '@/services/salesSetupService';

const STATUS_OPTIONS = ['Reserved', 'PendingApproval', 'Allocated', 'Sold', 'Leased', 'Released', 'Cancelled', 'Expired', 'Rejected'];

const STATUS_CONFIG: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline'; className: string }> = {
  Reserved: { variant: 'secondary', className: 'bg-amber-100 text-amber-800' },
  PendingApproval: { variant: 'secondary', className: 'bg-yellow-100 text-yellow-800' },
  Approved: { variant: 'default', className: 'bg-blue-100 text-blue-800' },
  Allocated: { variant: 'default', className: 'bg-green-100 text-green-800' },
  Sold: { variant: 'default', className: 'bg-emerald-100 text-emerald-800' },
  Leased: { variant: 'default', className: 'bg-cyan-100 text-cyan-800' },
  Released: { variant: 'outline', className: 'bg-slate-100 text-slate-700' },
  Cancelled: { variant: 'destructive', className: 'bg-red-100 text-red-800' },
  Expired: { variant: 'outline', className: 'bg-zinc-100 text-zinc-700' },
  Rejected: { variant: 'destructive', className: 'bg-red-50 text-red-700' },
};

const ACTIVE_STATUSES = new Set(['Reserved', 'PendingApproval', 'Approved', 'Allocated', 'Sold', 'Leased']);

const formatDate = (value?: string) => {
  if (!value) return '-';
  try { return format(new Date(value), 'dd MMM yyyy'); } catch { return value; }
};

const formatAmount = (amount?: number, currency?: string) => {
  if (amount === undefined || amount === null) return '-';
  return `${currency || 'GHS'} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}`;
};

const formatLabel = (value?: string) => value ? value.replace(/([A-Z])/g, ' $1').trim() : '-';

export default function SalesAllocationsPage() {
  const router = useRouter();
  const [allocations, setAllocations] = useState<SalesAllocationDto[]>([]);
  const [sources, setSources] = useState<SalesSaleableSourceDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [sourceFilter, setSourceFilter] = useState('all');
  const [statusFilter, setStatusFilter] = useState('all');
  const [activityFilter, setActivityFilter] = useState<'active' | 'all'>('active');

  useEffect(() => {
    void loadSources();
  }, []);

  useEffect(() => {
    void loadAllocations();
  }, [sourceFilter, statusFilter, activityFilter]);

  const loadSources = async () => {
    try {
      const data = await salesSetupService.getSaleableSources(true);
      setSources(data);
    } catch {
      setSources([]);
    }
  };

  const loadAllocations = async () => {
    try {
      setLoading(true);
      const data = await salesAllocationService.getAllocations({
        saleableSourceId: sourceFilter === 'all' ? undefined : sourceFilter,
        status: statusFilter === 'all' ? undefined : statusFilter,
        activeOnly: activityFilter === 'active',
      });
      setAllocations(data);
    } catch (error: any) {
      toast.error(error.message || 'Failed to load sales allocations');
    } finally {
      setLoading(false);
    }
  };

  const filteredAllocations = useMemo(() => {
    const term = searchTerm.trim().toLowerCase();
    if (!term) return allocations;

    return allocations.filter((allocation) =>
      [
        allocation.sourceItemName,
        allocation.sourceItemCode,
        allocation.sourceCode,
        allocation.sourceType,
        allocation.customerName,
        allocation.salesOrderNumber,
        allocation.salesAgreementTitle,
        allocation.allocationType,
        allocation.status,
      ]
        .filter(Boolean)
        .some((value) => String(value).toLowerCase().includes(term)));
  }, [allocations, searchTerm]);

  const stats = useMemo(() => ({
    total: filteredAllocations.length,
    active: filteredAllocations.filter((allocation) => ACTIVE_STATUSES.has(allocation.status)).length,
    pending: filteredAllocations.filter((allocation) => allocation.status === 'PendingApproval').length,
    value: filteredAllocations.reduce((sum, allocation) => sum + (allocation.agreedValue ?? allocation.estimatedValue ?? 0), 0),
  }), [filteredAllocations]);

  const getStatusBadge = (status: string) => {
    const config = STATUS_CONFIG[status] || { variant: 'outline' as const, className: '' };
    return <Badge variant={config.variant} className={config.className}>{formatLabel(status)}</Badge>;
  };

  const handleExport = () => {
    if (filteredAllocations.length === 0) {
      toast.error('No allocations to export');
      return;
    }

    const rows = filteredAllocations.map((allocation) => ({
      Source: allocation.sourceCode,
      'Source Type': allocation.sourceType,
      Item: allocation.sourceItemName,
      'Item Code': allocation.sourceItemCode || '',
      Customer: allocation.customerName || '',
      Type: allocation.allocationType,
      Status: allocation.status,
      'Reserved Until': allocation.reservedUntil ? formatDate(allocation.reservedUntil) : '',
      'Effective Date': allocation.effectiveDate ? formatDate(allocation.effectiveDate) : '',
      'Released Date': allocation.releasedDate ? formatDate(allocation.releasedDate) : '',
      'Estimated Value': allocation.estimatedValue ?? '',
      'Agreed Value': allocation.agreedValue ?? '',
      Currency: allocation.currency || '',
      'Sales Order': allocation.salesOrderNumber || '',
      'Sales Agreement': allocation.salesAgreementTitle || '',
    }));

    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, XLSX.utils.json_to_sheet(rows), 'Sales Allocations');
    XLSX.writeFile(workbook, `sales_allocations_${new Date().toISOString().split('T')[0]}.xlsx`);
    toast.success('Allocation ledger exported');
  };

  return (
    <div className="container mx-auto space-y-6 py-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h1 className="flex items-center gap-2 text-3xl font-bold">
            <MapPinned className="h-8 w-8 text-emerald-600" />
            Sales Allocations
          </h1>
          <p className="text-gray-500">Track reservations, allocations, releases, and source-linked sales activity.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={handleExport}>
            <Download className="mr-2 h-4 w-4" />
            Export
          </Button>
          <Button variant="outline" onClick={() => void loadAllocations()}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Ledger Records</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold">{stats.total}</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Active</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-emerald-600">{stats.active}</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Pending Approval</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-amber-600">{stats.pending}</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Tracked Value</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-blue-600">{formatAmount(stats.value, filteredAllocations[0]?.currency)}</p></CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader><CardTitle>Filters</CardTitle></CardHeader>
        <CardContent>
          <div className="grid gap-4 lg:grid-cols-[minmax(0,1.5fr)_minmax(180px,1fr)_minmax(180px,1fr)_minmax(180px,1fr)]">
            <div className="flex gap-2">
              <Input
                placeholder="Search item, source, customer, order..."
                value={searchTerm}
                onChange={(event) => setSearchTerm(event.target.value)}
              />
              <Button variant="outline" onClick={() => setSearchTerm('')}>
                <Search className="h-4 w-4" />
              </Button>
            </div>
            <Select value={sourceFilter} onValueChange={setSourceFilter}>
              <SelectTrigger><SelectValue placeholder="Saleable source" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Sources</SelectItem>
                {sources.map((source) => (
                  <SelectItem key={source.id} value={source.id}>{source.displayName}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger><SelectValue placeholder="Status" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                {STATUS_OPTIONS.map((status) => (
                  <SelectItem key={status} value={status}>{formatLabel(status)}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select value={activityFilter} onValueChange={(value: 'active' | 'all') => setActivityFilter(value)}>
              <SelectTrigger><SelectValue placeholder="Activity" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="active">Active Only</SelectItem>
                <SelectItem value="all">All Records</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Allocation Ledger</CardTitle>
          <CardDescription>Showing {filteredAllocations.length} allocation record(s)</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="py-12 text-center text-gray-500">
              <MapPinned className="mx-auto mb-4 h-12 w-12 animate-pulse text-emerald-500" />
              Loading allocations...
            </div>
          ) : filteredAllocations.length === 0 ? (
            <div className="py-12 text-center text-gray-500">
              <MapPinned className="mx-auto mb-4 h-12 w-12 text-gray-400" />
              No allocations matched the current filters.
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Source Item</TableHead>
                  <TableHead>Customer</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Reserved Until</TableHead>
                  <TableHead className="text-right">Value</TableHead>
                  <TableHead>Linked Document</TableHead>
                  <TableHead className="w-24">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredAllocations.map((allocation) => (
                  <TableRow
                    key={allocation.id}
                    className="cursor-pointer hover:bg-gray-50"
                    onClick={() => router.push(`/sales/allocations/${allocation.id}`)}
                  >
                    <TableCell>
                      <div>
                        <p className="font-medium">{allocation.sourceItemName}</p>
                        <p className="text-xs text-gray-500">
                          {[allocation.sourceItemCode, allocation.sourceCode, formatLabel(allocation.sourceType)].filter(Boolean).join(' - ')}
                        </p>
                      </div>
                    </TableCell>
                    <TableCell>{allocation.customerName || '-'}</TableCell>
                    <TableCell><Badge variant="outline">{formatLabel(allocation.allocationType)}</Badge></TableCell>
                    <TableCell>{getStatusBadge(allocation.status)}</TableCell>
                    <TableCell>{formatDate(allocation.reservedUntil)}</TableCell>
                    <TableCell className="text-right font-semibold">{formatAmount(allocation.agreedValue ?? allocation.estimatedValue, allocation.currency)}</TableCell>
                    <TableCell>
                      {allocation.salesOrderId ? (
                        <Button
                          variant="link"
                          className="h-auto p-0 text-blue-600"
                          onClick={(event) => {
                            event.stopPropagation();
                            router.push(`/sales/orders/${allocation.salesOrderId}`);
                          }}
                        >
                          {allocation.salesOrderNumber || 'Sales Order'}
                          <ArrowUpRight className="ml-1 h-3 w-3" />
                        </Button>
                      ) : allocation.salesAgreementId ? (
                        <Button
                          variant="link"
                          className="h-auto p-0 text-blue-600"
                          onClick={(event) => {
                            event.stopPropagation();
                            router.push(`/sales/agreements/${allocation.salesAgreementId}`);
                          }}
                        >
                          {allocation.salesAgreementTitle || 'Sales Agreement'}
                          <ArrowUpRight className="ml-1 h-3 w-3" />
                        </Button>
                      ) : '-'}
                    </TableCell>
                    <TableCell>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={(event) => {
                          event.stopPropagation();
                          router.push(`/sales/allocations/${allocation.id}`);
                        }}
                      >
                        <Eye className="mr-1 h-4 w-4" />
                        View
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
