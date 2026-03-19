'use client';

import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { DollarSign, Search, RefreshCw, Plus, CheckCircle, Banknote } from 'lucide-react';
import { toast } from 'sonner';
import { returnOrderService, type RefundSummaryDto } from '@/services/returnOrderService';
import { format } from 'date-fns';

const STATUS_CONFIG: Record<string, { className: string }> = {
  Requested: { className: 'bg-blue-100 text-blue-800' },
  Approved: { className: 'bg-cyan-100 text-cyan-800' },
  Processed: { className: 'bg-green-100 text-green-800' },
  Rejected: { className: 'bg-red-100 text-red-800' },
};

export default function RefundsPage() {
  const [refunds, setRefunds] = useState<RefundSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  useEffect(() => { loadRefunds(); }, [page, statusFilter]);

  const loadRefunds = async () => {
    try {
      setLoading(true);
      const data = await returnOrderService.getRefunds(page, pageSize, searchTerm || undefined, statusFilter || undefined);
      setRefunds(data.items); setTotalCount(data.totalCount);
    } catch (error) { console.error(error); toast.error('Failed to load refunds'); }
    finally { setLoading(false); }
  };

  const handleSearch = () => { setPage(1); loadRefunds(); };
  const handleApprove = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await returnOrderService.approveRefund(id); toast.success('Approved'); loadRefunds(); } catch (err: any) { toast.error(err.message); }
  };
  const handleProcess = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await returnOrderService.processRefund(id); toast.success('Processed'); loadRefunds(); } catch (err: any) { toast.error(err.message); }
  };

  const formatDate = (d?: string) => { if (!d) return '-'; try { return format(new Date(d), 'dd MMM yyyy'); } catch { return d; } };
  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2"><DollarSign className="h-8 w-8 text-rose-600" />Refunds</h1>
          <p className="text-gray-500">Process and track customer refunds</p>
        </div>
        <Button className="bg-rose-600 hover:bg-rose-700"><Plus className="h-4 w-4 mr-2" />New Refund</Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold">{totalCount}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Pending</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-blue-600">{refunds.filter(r => r.refundStatus === 'Requested' || r.refundStatus === 'Approved').length}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Processed</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-green-600">{refunds.filter(r => r.refundStatus === 'Processed').length}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Value</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-rose-600">${refunds.reduce((s, r) => s + r.refundAmount, 0).toLocaleString()}</p></CardContent></Card>
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
                <SelectItem value="Requested">Requested</SelectItem>
                <SelectItem value="Approved">Approved</SelectItem>
                <SelectItem value="Processed">Processed</SelectItem>
                <SelectItem value="Rejected">Rejected</SelectItem>
              </SelectContent>
            </Select>
            <Button variant="outline" onClick={loadRefunds}><RefreshCw className="h-4 w-4 mr-2" />Refresh</Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Refunds</CardTitle><CardDescription>Showing {refunds.length} of {totalCount}</CardDescription></CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8"><DollarSign className="h-12 w-12 animate-pulse mx-auto mb-4 text-rose-500" /><p className="text-gray-500">Loading...</p></div>
          ) : refunds.length === 0 ? (
            <div className="text-center py-8"><DollarSign className="h-12 w-12 mx-auto mb-4 text-gray-400" /><p className="text-gray-500">No refunds found</p></div>
          ) : (
            <>
              <Table>
                <TableHeader><TableRow>
                  <TableHead>Refund #</TableHead><TableHead>Customer</TableHead><TableHead>Method</TableHead>
                  <TableHead>Amount</TableHead><TableHead>Processed</TableHead><TableHead>Status</TableHead><TableHead>Actions</TableHead>
                </TableRow></TableHeader>
                <TableBody>
                  {refunds.map((r) => (
                    <TableRow key={r.id} className="cursor-pointer hover:bg-gray-50">
                      <TableCell className="font-mono text-sm text-rose-600">{r.documentNumber}</TableCell>
                      <TableCell>{r.customerName || '-'}</TableCell>
                      <TableCell><Badge variant="outline">{r.refundMethod}</Badge></TableCell>
                      <TableCell className="font-semibold">${r.refundAmount.toLocaleString()}</TableCell>
                      <TableCell>{formatDate(r.processedDate)}</TableCell>
                      <TableCell><Badge className={STATUS_CONFIG[r.refundStatus]?.className || ''}>{r.refundStatus}</Badge></TableCell>
                      <TableCell>
                        <div className="flex gap-1">
                          {r.refundStatus === 'Requested' && <Button variant="outline" size="sm" onClick={(e) => handleApprove(r.id, e)}><CheckCircle className="h-3 w-3 mr-1" />Approve</Button>}
                          {r.refundStatus === 'Approved' && <Button variant="outline" size="sm" className="text-green-600" onClick={(e) => handleProcess(r.id, e)}><Banknote className="h-3 w-3 mr-1" />Process</Button>}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
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
