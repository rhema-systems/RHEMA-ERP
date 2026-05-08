'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import {
  ShoppingCart, Search, Eye, RefreshCw, Download, Plus, Building2
} from 'lucide-react';
import { toast } from 'sonner';
import { salesOrderService, type SalesOrderSummaryDto } from '@/services/salesOrderService';
import { format } from 'date-fns';
import * as XLSX from 'xlsx';

const STATUS_CONFIG: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline'; className: string }> = {
  Draft: { variant: 'outline', className: 'bg-gray-100 text-gray-800' },
  PendingApproval: { variant: 'secondary', className: 'bg-yellow-100 text-yellow-800' },
  Approved: { variant: 'default', className: 'bg-blue-100 text-blue-800' },
  Confirmed: { variant: 'default', className: 'bg-indigo-100 text-indigo-800' },
  InProgress: { variant: 'default', className: 'bg-cyan-100 text-cyan-800' },
  PartiallyDelivered: { variant: 'secondary', className: 'bg-orange-100 text-orange-800' },
  FullyDelivered: { variant: 'default', className: 'bg-green-100 text-green-800' },
  Invoiced: { variant: 'default', className: 'bg-emerald-100 text-emerald-800' },
  OnHold: { variant: 'secondary', className: 'bg-amber-100 text-amber-800' },
  Cancelled: { variant: 'destructive', className: 'bg-red-100 text-red-800' },
  Closed: { variant: 'outline', className: 'bg-slate-100 text-slate-700' },
  Rejected: { variant: 'destructive', className: 'bg-red-50 text-red-700' },
};

export default function SalesOrdersPage() {
  const router = useRouter();
  const [orders, setOrders] = useState<SalesOrderSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [salesScope, setSalesScope] = useState<'all' | 'projectLinked' | 'releasedUnits'>('all');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  useEffect(() => {
    loadOrders();
  }, [page, statusFilter, salesScope]);

  const loadOrders = async () => {
    try {
      setLoading(true);
      const data = await salesOrderService.getSalesOrders(
        page,
        pageSize,
        searchTerm,
        statusFilter || undefined,
        undefined,
        undefined,
        undefined,
        undefined,
        undefined,
        salesScope === 'projectLinked' || salesScope === 'releasedUnits',
        salesScope === 'releasedUnits'
      );
      setOrders(data.items);
      setTotalCount(data.totalCount);
    } catch (error) {
      console.error('Error loading sales orders:', error);
      toast.error('Failed to load sales orders');
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = () => {
    setPage(1);
    loadOrders();
  };

  const handleExport = () => {
    if (orders.length === 0) { toast.error('No data to export'); return; }
    const exportData = orders.map(o => ({
      'Order #': o.orderNumber,
      'Customer': o.customerName,
      'Type': o.orderType,
      'Status': o.status,
      'Priority': o.priority,
      'Total': o.totalAmount,
      'Currency': o.currency,
      'Sales Rep': o.salesRepName || '',
      'Order Date': o.orderDate ? format(new Date(o.orderDate), 'yyyy-MM-dd') : '',
      'Expected Delivery': o.expectedDeliveryDate ? format(new Date(o.expectedDeliveryDate), 'yyyy-MM-dd') : '',
      'Delivery %': o.deliveryProgress,
      'Project': o.projectUnitContext?.projectCode || '',
      'Project Unit': o.projectUnitContext?.projectUnitCode || o.projectUnitContext?.projectUnitName || '',
    }));
    const ws = XLSX.utils.json_to_sheet(exportData);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Sales Orders');
    XLSX.writeFile(wb, `sales_orders_${new Date().toISOString().split('T')[0]}.xlsx`);
    toast.success('Exported to Excel');
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return '-';
    try { return format(new Date(dateString), 'dd MMM yyyy'); } catch { return dateString; }
  };

  const formatCurrency = (amount: number, currency: string) =>
    `${currency} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}`;
  const formatLabel = (value?: string) => value ? value.replace(/([A-Z])/g, ' $1').trim() : '-';

  const getStatusBadge = (status: string) => {
    const c = STATUS_CONFIG[status] || { variant: 'outline' as const, className: '' };
    return <Badge variant={c.variant} className={c.className}>{status.replace(/([A-Z])/g, ' $1').trim()}</Badge>;
  };

  const totalPages = Math.ceil(totalCount / pageSize);

  const stats = {
    total: totalCount,
    confirmed: orders.filter(o => ['Confirmed', 'InProgress', 'PartiallyDelivered'].includes(o.status)).length,
    pendingApproval: orders.filter(o => o.status === 'PendingApproval').length,
    totalValue: orders.reduce((sum, o) => sum + o.totalAmount, 0),
  };

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2">
            <ShoppingCart className="h-8 w-8 text-blue-600" />
            Sales Orders
          </h1>
          <p className="text-gray-500">Manage customer sales orders</p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" onClick={() => router.push('/sales/orders/create?source=released-unit')}>
            <Building2 className="h-4 w-4 mr-2" />From Released Unit
          </Button>
          <Button onClick={() => router.push('/sales/orders/create')}>
            <Plus className="h-4 w-4 mr-2" />New Sales Order
          </Button>
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Orders</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold">{stats.total}</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Active / In Progress</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-green-600">{stats.confirmed}</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Pending Approval</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-yellow-600">{stats.pendingApproval}</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Value</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-blue-600">GHS {stats.totalValue.toLocaleString()}</p></CardContent>
        </Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader><CardTitle>Filter Orders</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="flex gap-2">
              <Input
                placeholder="Search by order #, customer..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
              />
              <Button onClick={handleSearch}><Search className="h-4 w-4" /></Button>
            </div>
            <Select value={statusFilter || 'all'} onValueChange={(v) => setStatusFilter(v === 'all' ? '' : v)}>
              <SelectTrigger><SelectValue placeholder="All Statuses" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="PendingApproval">Pending Approval</SelectItem>
                <SelectItem value="Approved">Approved</SelectItem>
                <SelectItem value="Confirmed">Confirmed</SelectItem>
                <SelectItem value="InProgress">In Progress</SelectItem>
                <SelectItem value="PartiallyDelivered">Partially Delivered</SelectItem>
                <SelectItem value="FullyDelivered">Fully Delivered</SelectItem>
                <SelectItem value="OnHold">On Hold</SelectItem>
                <SelectItem value="Cancelled">Cancelled</SelectItem>
                <SelectItem value="Closed">Closed</SelectItem>
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
              <Button variant="outline" onClick={loadOrders}><RefreshCw className="h-4 w-4 mr-2" />Refresh</Button>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Orders Table */}
      <Card>
        <CardHeader>
          <CardTitle>Sales Orders</CardTitle>
          <CardDescription>Showing {orders.length} of {totalCount} orders</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8">
              <ShoppingCart className="h-12 w-12 animate-pulse mx-auto mb-4 text-blue-500" />
              <p className="text-gray-500">Loading sales orders...</p>
            </div>
          ) : orders.length === 0 ? (
            <div className="text-center py-8">
              <ShoppingCart className="h-12 w-12 mx-auto mb-4 text-gray-400" />
              <p className="text-gray-500">No sales orders found</p>
              <Button variant="outline" className="mt-4" onClick={() => router.push('/sales/orders/create')}>
                <Plus className="h-4 w-4 mr-2" />Create First Order
              </Button>
            </div>
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Order #</TableHead>
                    <TableHead>Customer</TableHead>
                    <TableHead>Order Date</TableHead>
                    <TableHead>Total</TableHead>
                    <TableHead>Priority</TableHead>
                    <TableHead>Delivery</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead className="hidden xl:table-cell">Project Context</TableHead>
                    <TableHead>Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {orders.map((order) => (
                    <TableRow key={order.id} className="cursor-pointer hover:bg-gray-50"
                      onClick={() => router.push(`/sales/orders/${order.id}`)}>
                      <TableCell className="font-mono text-sm text-blue-600">{order.orderNumber}</TableCell>
                      <TableCell>
                        <div>
                          <p className="font-medium">{order.customerName}</p>
                          {order.propertyReference && (
                            <p className="text-xs text-gray-500">Property: {order.propertyReference}</p>
                          )}
                          {order.projectUnitContext && (
                            <div className="mt-1 flex items-center gap-1 text-xs text-emerald-600">
                              <Building2 className="h-3 w-3" />
                              <span>
                                {order.projectUnitContext.projectCode}
                                {order.projectUnitContext.projectUnitCode
                                  ? ` • ${order.projectUnitContext.projectUnitCode}`
                                  : order.projectUnitContext.projectUnitName
                                    ? ` • ${order.projectUnitContext.projectUnitName}`
                                    : ''}
                              </span>
                            </div>
                          )}
                        </div>
                      </TableCell>
                      <TableCell>{formatDate(order.orderDate)}</TableCell>
                      <TableCell className="font-semibold">{formatCurrency(order.totalAmount, order.currency)}</TableCell>
                      <TableCell>
                        <Badge variant={order.priority === 'High' || order.priority === 'Urgent' ? 'destructive' : 'outline'}>
                          {order.priority}
                        </Badge>
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          <div className="w-16 bg-gray-200 rounded-full h-2">
                            <div
                              className="bg-green-500 h-2 rounded-full transition-all"
                              style={{ width: `${Math.min(order.deliveryProgress, 100)}%` }}
                            />
                          </div>
                          <span className="text-xs text-gray-500">{order.deliveryProgress}%</span>
                        </div>
                      </TableCell>
                      <TableCell>{getStatusBadge(order.status)}</TableCell>
                      <TableCell className="hidden xl:table-cell text-xs text-gray-500">
                        {order.projectUnitContext
                          ? `${formatLabel(order.projectUnitContext.projectUnitCommercialStatus)} / ${formatLabel(order.projectUnitContext.projectUnitHandoverStatus)}`
                          : '-'}
                      </TableCell>
                      <TableCell>
                        <Button variant="outline" size="sm" onClick={(e) => { e.stopPropagation(); router.push(`/sales/orders/${order.id}`); }}>
                          <Eye className="h-4 w-4 mr-1" />View
                        </Button>
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
