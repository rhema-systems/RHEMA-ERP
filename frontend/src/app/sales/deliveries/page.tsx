'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Truck, Search, Eye, RefreshCw, Download, Plus, Package, PackageCheck } from 'lucide-react';
import { toast } from 'sonner';
import { salesOrderService, type DeliveryNoteSummaryDto } from '@/services/salesOrderService';
import { format } from 'date-fns';
import * as XLSX from 'xlsx';

const STATUS_CONFIG: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline'; className: string }> = {
  Draft: { variant: 'outline', className: 'bg-gray-100 text-gray-800' },
  Packed: { variant: 'secondary', className: 'bg-blue-100 text-blue-800' },
  Shipped: { variant: 'default', className: 'bg-indigo-100 text-indigo-800' },
  Delivered: { variant: 'default', className: 'bg-green-100 text-green-800' },
  Cancelled: { variant: 'destructive', className: 'bg-red-100 text-red-800' },
};

export default function DeliveryNotesPage() {
  const router = useRouter();
  const [deliveries, setDeliveries] = useState<DeliveryNoteSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  useEffect(() => {
    loadDeliveries();
  }, [page, statusFilter]);

  const loadDeliveries = async () => {
    try {
      setLoading(true);
      const data = await salesOrderService.getDeliveryNotes(
        page, pageSize, searchTerm, statusFilter || undefined
      );
      setDeliveries(data.items);
      setTotalCount(data.totalCount);
    } catch (error) {
      console.error('Error loading delivery notes:', error);
      toast.error('Failed to load delivery notes');
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = () => {
    setPage(1);
    loadDeliveries();
  };

  const handleExport = () => {
    if (deliveries.length === 0) { toast.error('No data to export'); return; }
    const exportData = deliveries.map(d => ({
      'Delivery #': d.deliveryNumber,
      'Sales Order #': d.salesOrderNumber,
      'Customer': d.customerName,
      'Status': d.status,
      'Carrier': d.carrierName || '',
      'Tracking #': d.trackingNumber || '',
      'Items': d.totalItems,
      'Delivery Date': d.deliveryDate ? format(new Date(d.deliveryDate), 'yyyy-MM-dd') : '',
      'Created': d.createdAt ? format(new Date(d.createdAt), 'yyyy-MM-dd') : '',
    }));
    const ws = XLSX.utils.json_to_sheet(exportData);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Delivery Notes');
    XLSX.writeFile(wb, `delivery_notes_${new Date().toISOString().split('T')[0]}.xlsx`);
    toast.success('Exported to Excel');
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return '-';
    try { return format(new Date(dateString), 'dd MMM yyyy'); } catch { return dateString; }
  };

  const getStatusBadge = (status: string) => {
    const c = STATUS_CONFIG[status] || { variant: 'outline' as const, className: '' };
    return <Badge variant={c.variant} className={c.className}>{status}</Badge>;
  };

  const totalPages = Math.ceil(totalCount / pageSize);

  const stats = {
    total: totalCount,
    shipped: deliveries.filter(d => d.status === 'Shipped').length,
    delivered: deliveries.filter(d => d.status === 'Delivered').length,
    pending: deliveries.filter(d => ['Draft', 'Packed'].includes(d.status)).length,
  };

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2">
            <Truck className="h-8 w-8 text-green-600" />
            Delivery Notes
          </h1>
          <p className="text-gray-500">Manage delivery notes and shipments</p>
        </div>
        <Button onClick={() => router.push('/sales/deliveries/create')}>
          <Plus className="h-4 w-4 mr-2" />New Delivery Note
        </Button>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Deliveries</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold">{stats.total}</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">In Transit</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-blue-600">{stats.shipped}</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Delivered</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-green-600">{stats.delivered}</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Pending</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-yellow-600">{stats.pending}</p></CardContent>
        </Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader><CardTitle>Filter Deliveries</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="flex gap-2">
              <Input
                placeholder="Search by delivery #, customer..."
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
                <SelectItem value="Packed">Packed</SelectItem>
                <SelectItem value="Shipped">Shipped</SelectItem>
                <SelectItem value="Delivered">Delivered</SelectItem>
                <SelectItem value="Cancelled">Cancelled</SelectItem>
              </SelectContent>
            </Select>
            <div className="flex gap-2">
              <Button variant="outline" onClick={handleExport}><Download className="h-4 w-4 mr-2" />Export</Button>
              <Button variant="outline" onClick={loadDeliveries}><RefreshCw className="h-4 w-4 mr-2" />Refresh</Button>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Deliveries Table */}
      <Card>
        <CardHeader>
          <CardTitle>Delivery Notes</CardTitle>
          <CardDescription>Showing {deliveries.length} of {totalCount} delivery notes</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8">
              <Truck className="h-12 w-12 animate-pulse mx-auto mb-4 text-green-500" />
              <p className="text-gray-500">Loading delivery notes...</p>
            </div>
          ) : deliveries.length === 0 ? (
            <div className="text-center py-8">
              <Package className="h-12 w-12 mx-auto mb-4 text-gray-400" />
              <p className="text-gray-500">No delivery notes found</p>
            </div>
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Delivery #</TableHead>
                    <TableHead>Sales Order</TableHead>
                    <TableHead>Customer</TableHead>
                    <TableHead>Carrier</TableHead>
                    <TableHead>Items</TableHead>
                    <TableHead>Delivery Date</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {deliveries.map((delivery) => (
                    <TableRow key={delivery.id} className="cursor-pointer hover:bg-gray-50"
                      onClick={() => router.push(`/sales/deliveries/${delivery.id}`)}>
                      <TableCell className="font-mono text-sm text-green-600">{delivery.deliveryNumber}</TableCell>
                      <TableCell className="font-mono text-sm text-blue-600">{delivery.salesOrderNumber}</TableCell>
                      <TableCell>{delivery.customerName}</TableCell>
                      <TableCell>
                        <div>
                          <p className="text-sm">{delivery.carrierName || '-'}</p>
                          {delivery.trackingNumber && (
                            <p className="text-xs text-gray-500">#{delivery.trackingNumber}</p>
                          )}
                        </div>
                      </TableCell>
                      <TableCell>{delivery.totalItems}</TableCell>
                      <TableCell>{formatDate(delivery.deliveryDate)}</TableCell>
                      <TableCell>{getStatusBadge(delivery.status)}</TableCell>
                      <TableCell>
                        <Button variant="outline" size="sm" onClick={(e) => { e.stopPropagation(); router.push(`/sales/deliveries/${delivery.id}`); }}>
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
