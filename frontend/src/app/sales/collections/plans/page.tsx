'use client';

import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { CalendarClock, Search, RefreshCw, Plus, CheckCircle, DollarSign } from 'lucide-react';
import { toast } from 'sonner';
import { collectionService, type PaymentPlanSummaryDto } from '@/services/collectionService';
import { format } from 'date-fns';

const STATUS_CONFIG: Record<string, { className: string }> = {
  Draft: { className: 'bg-gray-100 text-gray-800' },
  Active: { className: 'bg-blue-100 text-blue-800' },
  Completed: { className: 'bg-green-100 text-green-800' },
  Cancelled: { className: 'bg-red-100 text-red-800' },
  Defaulted: { className: 'bg-orange-100 text-orange-800' },
};

export default function PaymentPlansPage() {
  const [plans, setPlans] = useState<PaymentPlanSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  useEffect(() => { loadPlans(); }, [page, statusFilter]);

  const loadPlans = async () => {
    try {
      setLoading(true);
      const data = await collectionService.getPlans(page, pageSize, searchTerm || undefined, statusFilter || undefined);
      setPlans(data.items); setTotalCount(data.totalCount);
    } catch (error) { console.error(error); toast.error('Failed to load plans'); }
    finally { setLoading(false); }
  };

  const handleSearch = () => { setPage(1); loadPlans(); };
  const handleApprove = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await collectionService.approvePlan(id); toast.success('Plan approved'); loadPlans(); } catch (err: any) { toast.error(err.message); }
  };

  const formatDate = (d?: string) => { if (!d) return '-'; try { return format(new Date(d), 'dd MMM yyyy'); } catch { return d; } };
  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2"><CalendarClock className="h-8 w-8 text-sky-600" />Payment Plans</h1>
          <p className="text-gray-500">Manage customer payment plans and installments</p>
        </div>
        <Button className="bg-sky-600 hover:bg-sky-700"><Plus className="h-4 w-4 mr-2" />New Plan</Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Plans</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold">{totalCount}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Active</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-blue-600">{plans.filter(p => p.planStatus === 'Active').length}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Debt</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-amber-600"><DollarSign className="h-5 w-5 inline" />{plans.reduce((s, p) => s + p.totalDebt, 0).toLocaleString()}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Collected</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-green-600"><DollarSign className="h-5 w-5 inline" />{plans.reduce((s, p) => s + p.totalPaid, 0).toLocaleString()}</p></CardContent></Card>
      </div>

      <Card>
        <CardHeader><CardTitle>Filter</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="flex gap-2">
              <Input placeholder="Search..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleSearch()} />
              <Button onClick={handleSearch}><Search className="h-4 w-4" /></Button>
            </div>
            <Select value={statusFilter || 'all'} onValueChange={(v) => setStatusFilter(v === 'all' ? '' : v)}>
              <SelectTrigger><SelectValue placeholder="All Statuses" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All</SelectItem>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="Active">Active</SelectItem>
                <SelectItem value="Completed">Completed</SelectItem>
                <SelectItem value="Cancelled">Cancelled</SelectItem>
              </SelectContent>
            </Select>
            <Button variant="outline" onClick={loadPlans}><RefreshCw className="h-4 w-4 mr-2" />Refresh</Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Payment Plans</CardTitle><CardDescription>Showing {plans.length} of {totalCount}</CardDescription></CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8"><CalendarClock className="h-12 w-12 animate-pulse mx-auto mb-4 text-sky-500" /><p className="text-gray-500">Loading...</p></div>
          ) : plans.length === 0 ? (
            <div className="text-center py-8"><CalendarClock className="h-12 w-12 mx-auto mb-4 text-gray-400" /><p className="text-gray-500">No payment plans found</p></div>
          ) : (
            <>
              <Table>
                <TableHeader><TableRow>
                  <TableHead>Plan Name</TableHead><TableHead>Customer</TableHead><TableHead>Frequency</TableHead>
                  <TableHead>Installments</TableHead><TableHead>Debt</TableHead><TableHead>Paid</TableHead><TableHead>Progress</TableHead><TableHead>Status</TableHead><TableHead>Actions</TableHead>
                </TableRow></TableHeader>
                <TableBody>
                  {plans.map((p) => {
                    const progress = p.totalDebt > 0 ? Math.round((p.totalPaid / p.totalDebt) * 100) : 0;
                    return (
                      <TableRow key={p.id} className="cursor-pointer hover:bg-gray-50">
                        <TableCell className="font-medium">{p.planName}</TableCell>
                        <TableCell>{p.customerName || '-'}</TableCell>
                        <TableCell><Badge variant="outline">{p.frequency}</Badge></TableCell>
                        <TableCell>{p.installmentsPaid}/{p.numberOfInstallments}</TableCell>
                        <TableCell className="font-semibold">${p.totalDebt.toLocaleString()}</TableCell>
                        <TableCell className="text-green-600">${p.totalPaid.toLocaleString()}</TableCell>
                        <TableCell>
                          <div className="flex items-center gap-2">
                            <div className="w-16 bg-gray-200 rounded-full h-2">
                              <div className="bg-sky-500 h-2 rounded-full transition-all" style={{ width: `${progress}%` }} />
                            </div>
                            <span className="text-xs">{progress}%</span>
                          </div>
                        </TableCell>
                        <TableCell><Badge className={STATUS_CONFIG[p.planStatus]?.className || ''}>{p.planStatus}</Badge></TableCell>
                        <TableCell>
                          {p.planStatus === 'Draft' && <Button variant="outline" size="sm" onClick={(e) => handleApprove(p.id, e)}><CheckCircle className="h-3 w-3 mr-1" />Approve</Button>}
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
