'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { FileText, Search, Eye, RefreshCw, Download, Plus, Building2 } from 'lucide-react';
import { toast } from 'sonner';
import { salesAgreementService, type SalesAgreementSummaryDto } from '@/services/salesAgreementService';
import { format } from 'date-fns';
import * as XLSX from 'xlsx';

const STATUS_CONFIG: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline'; className: string }> = {
  Draft: { variant: 'outline', className: 'bg-gray-100 text-gray-800' },
  PendingApproval: { variant: 'secondary', className: 'bg-yellow-100 text-yellow-800' },
  Active: { variant: 'default', className: 'bg-green-100 text-green-800' },
  Expiring: { variant: 'secondary', className: 'bg-orange-100 text-orange-800' },
  Renewed: { variant: 'default', className: 'bg-blue-100 text-blue-800' },
  Expired: { variant: 'outline', className: 'bg-slate-100 text-slate-700' },
  Terminated: { variant: 'destructive', className: 'bg-red-100 text-red-800' },
  Suspended: { variant: 'secondary', className: 'bg-amber-100 text-amber-800' },
};

export default function SalesAgreementsPage() {
  const router = useRouter();
  const [agreements, setAgreements] = useState<SalesAgreementSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [typeFilter, setTypeFilter] = useState('');
  const [salesScope, setSalesScope] = useState<'all' | 'projectLinked' | 'releasedUnits'>('all');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  useEffect(() => { loadAgreements(); }, [page, statusFilter, typeFilter, salesScope]);

  const loadAgreements = async () => {
    try {
      setLoading(true);
      const data = await salesAgreementService.getAgreements(
        page,
        pageSize,
        searchTerm,
        statusFilter || undefined,
        typeFilter || undefined,
        undefined,
        salesScope === 'projectLinked' || salesScope === 'releasedUnits',
        salesScope === 'releasedUnits'
      );
      setAgreements(data.items);
      setTotalCount(data.totalCount);
    } catch (error) {
      console.error('Error:', error);
      toast.error('Failed to load agreements');
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = () => { setPage(1); loadAgreements(); };

  const handleExport = () => {
    if (!agreements.length) { toast.error('No data to export'); return; }
    const ws = XLSX.utils.json_to_sheet(agreements.map(a => ({
      'Agreement #': a.documentNumber, Title: a.agreementTitle, Customer: a.customerName,
      Type: a.agreementType, Status: a.agreementStatus,
      'Start Date': a.startDate ? format(new Date(a.startDate), 'yyyy-MM-dd') : '',
      'End Date': a.endDate ? format(new Date(a.endDate), 'yyyy-MM-dd') : '',
      'Agreed Value': a.agreedValue, 'Utilized Value': a.utilizedValue, Currency: a.currency,
      Property: a.propertyReference || '', 'Auto-Renew': a.autoRenew ? 'Yes' : 'No',
      Project: a.projectUnitContext?.projectCode || '',
      'Project Unit': a.projectUnitContext?.projectUnitCode || a.projectUnitContext?.projectUnitName || '',
    })));
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Agreements');
    XLSX.writeFile(wb, `sales_agreements_${new Date().toISOString().split('T')[0]}.xlsx`);
    toast.success('Exported to Excel');
  };

  const formatDate = (d?: string) => { if (!d) return '-'; try { return format(new Date(d), 'dd MMM yyyy'); } catch { return d; } };
  const formatCurrency = (n: number) => `GHS ${n.toLocaleString(undefined, { minimumFractionDigits: 2 })}`;
  const formatLabel = (value?: string) => value ? value.replace(/([A-Z])/g, ' $1').trim() : '-';
  const getStatusBadge = (s: string) => {
    const c = STATUS_CONFIG[s] || { variant: 'outline' as const, className: '' };
    return <Badge variant={c.variant} className={c.className}>{s.replace(/([A-Z])/g, ' $1').trim()}</Badge>;
  };

  const totalPages = Math.ceil(totalCount / pageSize);
  const stats = {
    total: totalCount,
    active: agreements.filter(a => a.agreementStatus === 'Active').length,
    expiring: agreements.filter(a => a.agreementStatus === 'Expiring').length,
    totalValue: agreements.reduce((s, a) => s + a.agreedValue, 0),
  };

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2">
            <FileText className="h-8 w-8 text-purple-600" />
            Sales Agreements
          </h1>
          <p className="text-gray-500">Manage customer agreements, leases, and contracts</p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" onClick={() => router.push('/sales/agreements/create?source=released-unit')}>
            <Building2 className="h-4 w-4 mr-2" />From Released Unit
          </Button>
          <Button onClick={() => router.push('/sales/agreements/create')}>
            <Plus className="h-4 w-4 mr-2" />New Agreement
          </Button>
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Agreements</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold">{stats.total}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Active</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-green-600">{stats.active}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Expiring Soon</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-orange-600">{stats.expiring}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Value</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-purple-600">{formatCurrency(stats.totalValue)}</p></CardContent></Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader><CardTitle>Filter Agreements</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-5 gap-4">
            <div className="flex gap-2 md:col-span-2">
              <Input placeholder="Search by number, title, customer..." value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleSearch()} />
              <Button onClick={handleSearch}><Search className="h-4 w-4" /></Button>
            </div>
            <Select value={statusFilter || 'all'} onValueChange={(v) => setStatusFilter(v === 'all' ? '' : v)}>
              <SelectTrigger><SelectValue placeholder="All Statuses" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="PendingApproval">Pending Approval</SelectItem>
                <SelectItem value="Active">Active</SelectItem>
                <SelectItem value="Expiring">Expiring</SelectItem>
                <SelectItem value="Expired">Expired</SelectItem>
                <SelectItem value="Suspended">Suspended</SelectItem>
                <SelectItem value="Terminated">Terminated</SelectItem>
              </SelectContent>
            </Select>
            <Select value={typeFilter || 'all'} onValueChange={(v) => setTypeFilter(v === 'all' ? '' : v)}>
              <SelectTrigger><SelectValue placeholder="All Types" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                <SelectItem value="General">General</SelectItem>
                <SelectItem value="VolumeBased">Volume Based</SelectItem>
                <SelectItem value="PriceLock">Price Lock</SelectItem>
                <SelectItem value="LeaseAgreement">Lease Agreement</SelectItem>
                <SelectItem value="TenancyAgreement">Tenancy Agreement</SelectItem>
                <SelectItem value="PlotAllocation">Plot Allocation</SelectItem>
                <SelectItem value="ServiceLevel">Service Level</SelectItem>
              </SelectContent>
            </Select>
            <Select value={salesScope} onValueChange={(v: 'all' | 'projectLinked' | 'releasedUnits') => setSalesScope(v)}>
              <SelectTrigger><SelectValue placeholder="Sales Scope" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Records</SelectItem>
                <SelectItem value="projectLinked">Project-Linked Only</SelectItem>
                <SelectItem value="releasedUnits">Released Units Only</SelectItem>
              </SelectContent>
            </Select>
            <div className="flex gap-2">
              <Button variant="outline" onClick={handleExport}><Download className="h-4 w-4 mr-2" />Export</Button>
              <Button variant="outline" onClick={loadAgreements}><RefreshCw className="h-4 w-4 mr-2" />Refresh</Button>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Table */}
      <Card>
        <CardHeader>
          <CardTitle>Agreements</CardTitle>
          <CardDescription>Showing {agreements.length} of {totalCount} agreements</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8"><FileText className="h-12 w-12 animate-pulse mx-auto mb-4 text-purple-500" /><p className="text-gray-500">Loading...</p></div>
          ) : agreements.length === 0 ? (
            <div className="text-center py-8"><FileText className="h-12 w-12 mx-auto mb-4 text-gray-400" /><p className="text-gray-500">No agreements found</p></div>
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Agreement #</TableHead>
                    <TableHead>Title</TableHead>
                    <TableHead>Customer</TableHead>
                    <TableHead>Type</TableHead>
                    <TableHead>Period</TableHead>
                    <TableHead className="text-right">Value</TableHead>
                    <TableHead>Utilization</TableHead>
                    <TableHead>Milestones</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {agreements.map((a) => {
                    const utilPct = a.agreedValue > 0 ? Math.round((a.utilizedValue / a.agreedValue) * 100) : 0;
                    return (
                      <TableRow key={a.id} className="cursor-pointer hover:bg-gray-50"
                        onClick={() => router.push(`/sales/agreements/${a.id}`)}>
                        <TableCell className="font-mono text-sm text-purple-600">{a.documentNumber}</TableCell>
                        <TableCell>
                          <div>
                            <p className="font-medium">{a.agreementTitle}</p>
                            {a.propertyReference && <p className="text-xs text-gray-500">Property: {a.propertyReference}</p>}
                            {a.projectUnitContext && (
                              <div className="mt-1 flex items-center gap-1 text-xs text-blue-600">
                                <Building2 className="h-3 w-3" />
                                <span>
                                  {a.projectUnitContext.projectCode}
                                  {a.projectUnitContext.projectUnitCode
                                    ? ` • ${a.projectUnitContext.projectUnitCode}`
                                    : a.projectUnitContext.projectUnitName
                                      ? ` • ${a.projectUnitContext.projectUnitName}`
                                      : ''}
                                </span>
                              </div>
                            )}
                          </div>
                        </TableCell>
                        <TableCell>
                          <div>
                            <p>{a.customerName}</p>
                            {a.projectUnitContext && (
                              <p className="text-xs text-gray-500">
                                {formatLabel(a.projectUnitContext.projectUnitCommercialStatus)} • {formatLabel(a.projectUnitContext.projectUnitHandoverStatus)}
                              </p>
                            )}
                          </div>
                        </TableCell>
                        <TableCell><Badge variant="outline">{a.agreementType.replace(/([A-Z])/g, ' $1').trim()}</Badge></TableCell>
                        <TableCell><div className="text-sm"><p>{formatDate(a.startDate)}</p><p className="text-gray-400">→ {formatDate(a.endDate)}</p></div></TableCell>
                        <TableCell className="text-right font-semibold">{formatCurrency(a.agreedValue)}</TableCell>
                        <TableCell>
                          <div className="flex items-center gap-2">
                            <div className="flex-1 bg-gray-200 rounded-full h-2 w-16">
                              <div className="bg-purple-500 h-2 rounded-full" style={{ width: `${Math.min(utilPct, 100)}%` }} />
                            </div>
                            <span className="text-sm">{utilPct}%</span>
                          </div>
                        </TableCell>
                        <TableCell>
                          {a.totalMilestones > 0 && <span className="text-sm">{a.completedMilestones}/{a.totalMilestones}</span>}
                        </TableCell>
                        <TableCell>{getStatusBadge(a.agreementStatus)}</TableCell>
                        <TableCell>
                          <Button variant="outline" size="sm" onClick={(e) => { e.stopPropagation(); router.push(`/sales/agreements/${a.id}`); }}>
                            <Eye className="h-4 w-4 mr-1" />View
                          </Button>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
              {totalPages > 1 && (
                <div className="flex items-center justify-between mt-4">
                  <p className="text-sm text-gray-600">Page {page} of {totalPages}</p>
                  <div className="flex gap-2">
                    <Button variant="outline" size="sm" onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1}>Previous</Button>
                    <Button variant="outline" size="sm" onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages}>Next</Button>
                  </div>
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
