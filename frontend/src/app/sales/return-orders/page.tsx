'use client';

import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { RotateCcw, Search, RefreshCw, Plus, Eye, CheckCircle, Package, ClipboardCheck } from 'lucide-react';
import { toast } from 'sonner';
import { returnOrderService, type ReturnOrderSummaryDto } from '@/services/returnOrderService';
import { format } from 'date-fns';

const STATUS_CONFIG: Record<string, { className: string }> = {
  Requested: { className: 'bg-blue-100 text-blue-800' },
  Approved: { className: 'bg-cyan-100 text-cyan-800' },
  Received: { className: 'bg-yellow-100 text-yellow-800' },
  Inspected: { className: 'bg-orange-100 text-orange-800' },
  CreditIssued: { className: 'bg-green-100 text-green-800' },
  Rejected: { className: 'bg-red-100 text-red-800' },
  Closed: { className: 'bg-gray-100 text-gray-800' },
};

export default function ReturnOrdersPage() {
  const [orders, setOrders] = useState<ReturnOrderSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  useEffect(() => { loadOrders(); }, [page, statusFilter]);

  const loadOrders = async () => {
    try {
      setLoading(true);
      const data = await returnOrderService.getReturnOrders(page, pageSize, searchTerm || undefined, statusFilter || undefined);
      setOrders(data.items);
      setTotalCount(data.totalCount);
    } catch (error) { console.error(error); toast.error('Failed to load return orders'); }
    finally { setLoading(false); }
  };

  const handleSearch = () => { setPage(1); loadOrders(); };

  const handleApprove = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await returnOrderService.approveReturnOrder(id); toast.success('Return approved'); loadOrders(); } catch (error: any) { toast.error(error.message); }
  };
  const handleReceive = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await returnOrderService.receiveReturnOrder(id); toast.success('Return received'); loadOrders(); } catch (error: any) { toast.error(error.message); }
  };
  const handleInspect = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await returnOrderService.inspectReturnOrder(id); toast.success('Inspection complete'); loadOrders(); } catch (error: any) { toast.error(error.message); }
  };
  const handleIssueCredit = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await returnOrderService.issueCreditNote(id); toast.success('Credit note issued'); loadOrders(); } catch (error: any) { toast.error(error.message); }
  };

  const formatDate = (d?: string) => { if (!d) return '-'; try { return format(new Date(d), 'dd MMM yyyy'); } catch { return d; } };
  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2"><RotateCcw className="h-8 w-8 text-amber-600" />Return Orders</h1>
          <p className="text-gray-500">Manage product returns and inspections</p>
        </div>
        <Button className="bg-amber-600 hover:bg-amber-700"><Plus className="h-4 w-4 mr-2" />New Return</Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Returns</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold">{totalCount}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Pending Inspection</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-yellow-600">{orders.filter(o => o.returnStatus === 'Received').length}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Credit Issued</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-green-600">{orders.filter(o => o.returnStatus === 'CreditIssued').length}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Value</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-amber-600">${orders.reduce((s, o) => s + o.totalAmount, 0).toLocaleString()}</p></CardContent></Card>
      </div>

      <Card>
        <CardHeader><CardTitle>Filter</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="flex gap-2">
              <Input placeholder="Search by return #, customer..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleSearch()} />
              <Button onClick={handleSearch}><Search className="h-4 w-4" /></Button>
            </div>
            <Select value={statusFilter || 'all'} onValueChange={(v) => setStatusFilter(v === 'all' ? '' : v)}>
              <SelectTrigger><SelectValue placeholder="All Statuses" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All</SelectItem>
                <SelectItem value="Requested">Requested</SelectItem>
                <SelectItem value="Approved">Approved</SelectItem>
                <SelectItem value="Received">Received</SelectItem>
                <SelectItem value="Inspected">Inspected</SelectItem>
                <SelectItem value="CreditIssued">Credit Issued</SelectItem>
                <SelectItem value="Rejected">Rejected</SelectItem>
              </SelectContent>
            </Select>
            <Button variant="outline" onClick={loadOrders}><RefreshCw className="h-4 w-4 mr-2" />Refresh</Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Return Orders</CardTitle><CardDescription>Showing {orders.length} of {totalCount}</CardDescription></CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8"><RotateCcw className="h-12 w-12 animate-pulse mx-auto mb-4 text-amber-500" /><p className="text-gray-500">Loading...</p></div>
          ) : orders.length === 0 ? (
            <div className="text-center py-8"><RotateCcw className="h-12 w-12 mx-auto mb-4 text-gray-400" /><p className="text-gray-500">No return orders found</p></div>
          ) : (
            <>
              <Table>
                <TableHeader><TableRow>
                  <TableHead>Return #</TableHead><TableHead>Customer</TableHead><TableHead>Sales Order</TableHead>
                  <TableHead>Reason</TableHead><TableHead>Lines</TableHead><TableHead>Total</TableHead><TableHead>Status</TableHead><TableHead>Actions</TableHead>
                </TableRow></TableHeader>
                <TableBody>
                  {orders.map((o) => (
                    <TableRow key={o.id} className="cursor-pointer hover:bg-gray-50">
                      <TableCell className="font-mono text-sm text-amber-600">{o.documentNumber}</TableCell>
                      <TableCell>{o.customerName || '-'}</TableCell>
                      <TableCell className="text-sm">{o.salesOrderNumber || '-'}</TableCell>
                      <TableCell><Badge variant="outline">{o.reasonCode}</Badge></TableCell>
                      <TableCell>{o.lineCount}</TableCell>
                      <TableCell className="font-semibold">${o.totalAmount.toLocaleString()}</TableCell>
                      <TableCell><Badge className={STATUS_CONFIG[o.returnStatus]?.className || ''}>{o.returnStatus.replace(/([A-Z])/g, ' $1').trim()}</Badge></TableCell>
                      <TableCell>
                        <div className="flex gap-1">
                          {o.returnStatus === 'Requested' && <Button variant="outline" size="sm" onClick={(e) => handleApprove(o.id, e)}><CheckCircle className="h-3 w-3 mr-1" />Approve</Button>}
                          {o.returnStatus === 'Approved' && <Button variant="outline" size="sm" onClick={(e) => handleReceive(o.id, e)}><Package className="h-3 w-3 mr-1" />Receive</Button>}
                          {o.returnStatus === 'Received' && <Button variant="outline" size="sm" onClick={(e) => handleInspect(o.id, e)}><ClipboardCheck className="h-3 w-3 mr-1" />Inspect</Button>}
                          {o.returnStatus === 'Inspected' && <Button variant="outline" size="sm" className="text-green-600" onClick={(e) => handleIssueCredit(o.id, e)}>Issue Credit</Button>}
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
