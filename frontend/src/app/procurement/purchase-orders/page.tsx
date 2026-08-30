'use client';

import React, { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Pagination } from '@/components/ui/pagination';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { formatPendingApprovers, useWorkflowEntitySummaries } from '@/hooks/useWorkflowEntitySummaries';
import {
  Plus, Search, Eye, FileText, Clock, CheckCircle, XCircle,
  Send, Package, TruckIcon, Filter, AlertTriangle, Loader2, Calendar
} from 'lucide-react';
import {
  purchasingService,
  PurchaseOrderSummaryDto
} from '@/services/purchasingService';
import { businessPartnerService, BusinessPartnerDto } from '@/services/businessPartnerService';
import { format } from 'date-fns';
import Link from 'next/link';
import { toast } from 'sonner';
import { formatProcurementMoney } from '@/lib/procurement-currency';
import {
  getPurchaseOrderStatusPresentation,
  isPurchaseOrderStatus,
  PURCHASE_ORDER_STATUS_OPTIONS,
  PurchaseOrderStatusKey,
} from '@/lib/purchase-order-status';

const POStatusIcons: Partial<
  Record<PurchaseOrderStatusKey, React.ComponentType<{ className?: string }>>
> = {
  Draft: FileText,
  Submitted: Clock,
  'Pending Approval': Clock,
  Approved: CheckCircle,
  Rejected: XCircle,
  Sent,
  Acknowledged: CheckCircle,
  'Partially Received': Package,
  Received: TruckIcon,
  Cancelled: XCircle,
  Closed: CheckCircle,
};

export default function PurchaseOrdersPage() {
  const router = useRouter();
  const [orders, setOrders] = useState<PurchaseOrderSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const { summariesById: workflowSummariesById } = useWorkflowEntitySummaries(
    'PurchaseOrder',
    orders.map((o) => o.id),
    orders.length > 0
  );
  
  // Pagination
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [totalCount, setTotalCount] = useState(0);
  
  // Filters
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [supplierFilter, setSupplierFilter] = useState('all');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  
  // Reference data
  const [suppliers, setSuppliers] = useState<BusinessPartnerDto[]>([]);

  const fetchOrders = async () => {
    try {
      setLoading(true);
      setError(null);
      const result = await purchasingService.getPurchaseOrders({
        page: currentPage,
        pageSize: pageSize,
        search: searchTerm || undefined,
        status: statusFilter !== 'all' ? statusFilter : undefined,
        supplierId: supplierFilter !== 'all' ? supplierFilter : undefined,
        startDate: startDate || undefined,
        endDate: endDate || undefined
      });
      setOrders(result.items);
      setTotalCount(result.totalCount);
    } catch (err: any) {
      console.error('Error fetching purchase orders:', err);
      setError('Failed to load purchase orders');
      toast.error('Failed to load purchase orders');
    } finally {
      setLoading(false);
    }
  };

  const fetchSuppliers = async () => {
    try {
      const data = await businessPartnerService.getActivePartners();
      // Filter to only show Supplier or Both types
      setSuppliers((data || []).filter(bp => 
        bp.partnerType === 'Supplier' || bp.partnerType === 'Both'
      ));
    } catch (err) {
      console.error('Error fetching suppliers:', err);
    }
  };

  useEffect(() => {
    fetchSuppliers();
  }, []);

  useEffect(() => {
    fetchOrders();
  }, [currentPage, pageSize, searchTerm, statusFilter, supplierFilter, startDate, endDate]);

  const handlePageChange = (page: number) => {
    setCurrentPage(page);
  };

  const handlePageSizeChange = (newPageSize: number) => {
    setPageSize(newPageSize);
    setCurrentPage(1);
  };

  const handleFilterChange = () => {
    setCurrentPage(1);
  };

  const getStatusBadge = (status: string) => {
    const statusConfig = getPurchaseOrderStatusPresentation(status);
    const Icon = POStatusIcons[statusConfig.key] || FileText;
    return (
      <Badge variant="outline" className={statusConfig.badgeClass}>
        <Icon className="h-3 w-3 mr-1" />
        {statusConfig.label}
      </Badge>
    );
  };

  // Calculate stats (these would ideally come from a separate API endpoint)
  const pendingCount = orders.filter(po =>
    isPurchaseOrderStatus(po.status, 'Pending Approval', 'Submitted')
  ).length;
  const activeCount = orders.filter(po =>
    isPurchaseOrderStatus(po.status, 'Approved', 'Sent', 'Acknowledged', 'Partially Received')
  ).length;
  const receivedCount = orders.filter(po => isPurchaseOrderStatus(po.status, 'Received')).length;
  const overdueCount = orders.filter(po => {
    if (!po.requiredDate) return false;
    const required = new Date(po.requiredDate);
    const today = new Date();
    return required < today && isPurchaseOrderStatus(po.status, 'Approved', 'Sent', 'Acknowledged');
  }).length;

  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Purchase Orders</h1>
          <p className="text-muted-foreground">Manage purchase orders and supplier deliveries</p>
        </div>
        <Link href="/procurement/purchase-orders/new">
          <Button>
            <Plus className="mr-2 h-4 w-4" />
            New Purchase Order
          </Button>
        </Link>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/procurement">Procurement</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Purchase Orders</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-5 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{totalCount}</p>
                <p className="text-sm text-muted-foreground">Total Orders</p>
              </div>
              <FileText className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold text-yellow-600">{pendingCount}</p>
                <p className="text-sm text-muted-foreground">Pending</p>
              </div>
              <Clock className="h-8 w-8 text-yellow-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold text-blue-600">{activeCount}</p>
                <p className="text-sm text-muted-foreground">Active</p>
              </div>
              <Send className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold text-green-600">{receivedCount}</p>
                <p className="text-sm text-muted-foreground">Received</p>
              </div>
              <CheckCircle className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold text-red-600">{overdueCount}</p>
                <p className="text-sm text-muted-foreground">Overdue</p>
              </div>
              <AlertTriangle className="h-8 w-8 text-red-500" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center">
            <Filter className="h-4 w-4 mr-2" />
            Filters
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div className="relative">
                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Search orders..."
                  className="pl-8"
                  value={searchTerm}
                  onChange={(e) => {
                    setSearchTerm(e.target.value);
                    handleFilterChange();
                  }}
                />
              </div>
              
              <Select value={statusFilter} onValueChange={(value) => {
                setStatusFilter(value);
                handleFilterChange();
              }}>
                <SelectTrigger>
                  <SelectValue placeholder="Status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Status</SelectItem>
                  {PURCHASE_ORDER_STATUS_OPTIONS.map(s => (
                    <SelectItem key={s.key} value={s.key}>{s.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              
              <Select value={supplierFilter} onValueChange={(value) => {
                setSupplierFilter(value);
                handleFilterChange();
              }}>
                <SelectTrigger>
                  <SelectValue placeholder="Supplier" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Suppliers</SelectItem>
                  {suppliers.map(supplier => (
                    <SelectItem key={supplier.id} value={supplier.id}>
                      {supplier.partnerCode} - {supplier.partnerName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            
            {/* Date Range Filter */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="startDate" className="flex items-center gap-2">
                  <Calendar className="h-4 w-4" />
                  Start Date
                </Label>
                <Input
                  id="startDate"
                  type="date"
                  value={startDate}
                  onChange={(e) => {
                    setStartDate(e.target.value);
                    handleFilterChange();
                  }}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="endDate" className="flex items-center gap-2">
                  <Calendar className="h-4 w-4" />
                  End Date
                </Label>
                <Input
                  id="endDate"
                  type="date"
                  value={endDate}
                  onChange={(e) => {
                    setEndDate(e.target.value);
                    handleFilterChange();
                  }}
                />
              </div>
            </div>
            
            {/* Clear Filters Button */}
            {(searchTerm || statusFilter !== 'all' || supplierFilter !== 'all' || startDate || endDate) && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => {
                  setSearchTerm('');
                  setStatusFilter('all');
                  setSupplierFilter('all');
                  setStartDate('');
                  setEndDate('');
                  handleFilterChange();
                }}
              >
                Clear Filters
              </Button>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Orders List */}
      <Card>
        <CardHeader>
          <CardTitle>Purchase Orders</CardTitle>
          <CardDescription>
            {loading ? 'Loading...' : `${totalCount} order(s) found`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading && (
            <div className="text-center py-8">
              <Loader2 className="h-8 w-8 animate-spin text-muted-foreground mx-auto" />
            </div>
          )}
          
          {error && (
            <div className="text-center py-8 text-red-600">{error}</div>
          )}
          
          {!loading && !error && (
            <div className="space-y-3">
              {orders.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">
                  No purchase orders found.
                </div>
              ) : (
                orders.map((po) => {
                  const isOverdue = po.requiredDate && new Date(po.requiredDate) < new Date() &&
                    isPurchaseOrderStatus(po.status, 'Approved', 'Sent', 'Acknowledged');

                  const summary = workflowSummariesById[po.id];
                  const stepName = summary?.currentStepName || po.currentWorkflowStepName;
                  const pending = formatPendingApprovers(summary?.pendingApprovers || []);
                  const showWorkflowBadges = isPurchaseOrderStatus(po.status, 'Pending Approval', 'Submitted');
                  
                  return (
                    <div
                      key={po.id}
                      className={`border rounded-lg p-4 hover:bg-muted/50 transition-colors ${
                        isOverdue ? 'border-red-300 bg-red-50/50' : ''
                      }`}
                    >
                      <div className="flex items-center justify-between">
                        <div className="flex items-center space-x-4">
                          <div className={`w-12 h-12 rounded-lg flex items-center justify-center ${
                            isOverdue ? 'bg-red-100' : 'bg-blue-100'
                          }`}>
                            {isOverdue ? (
                              <AlertTriangle className="h-6 w-6 text-red-600" />
                            ) : (
                              <FileText className="h-6 w-6 text-blue-600" />
                            )}
                          </div>
                          <div>
                            <div className="flex items-center space-x-2">
                              <h3 className="font-semibold">{po.orderNumber}</h3>
                              {getStatusBadge(po.status)}
                              {showWorkflowBadges && stepName && (
                                <Badge variant="outline" className="text-xs">
                                  Step: {stepName}
                                </Badge>
                              )}
                              {showWorkflowBadges && pending.short && (
                                <Badge variant="outline" className="text-xs" title={pending.full}>
                                  Pending with: {pending.short}
                                </Badge>
                              )}
                              {isOverdue && (
                                <Badge className="bg-red-100 text-red-800">
                                  <AlertTriangle className="h-3 w-3 mr-1" />
                                  Overdue
                                </Badge>
                              )}
                            </div>
                            <p className="text-sm text-muted-foreground">
                              Supplier: {po.supplierName} •
                              Date: {format(new Date(po.orderDate), 'MMM dd, yyyy')} •
                              Items: {po.itemCount}
                              {po.requiredDate && ` • Required: ${format(new Date(po.requiredDate), 'MMM dd, yyyy')}`}
                            </p>
                            <p className="text-sm font-medium text-primary">
                              Total: {formatProcurementMoney(po.totalAmount, po.currency)}
                            </p>
                          </div>
                        </div>
                        
                        <div className="flex items-center space-x-2">
                          <Link href={`/procurement/purchase-orders/${po.id}`}>
                            <Button size="sm" variant="outline">
                              <Eye className="h-4 w-4 mr-1" />
                              View
                            </Button>
                          </Link>
                          
                          <WorkflowApprovalActions
                            entityType="PurchaseOrder"
                            entityId={po.id}
                            entityLabel="Purchase Order"
                            entityNumber={po.orderNumber}
                            status={po.status}
                            currentStepName={stepName}
                            workflowSummary={summary}
                            canSubmit={isPurchaseOrderStatus(po.status, 'Draft')}
                            canApproveReject={isPurchaseOrderStatus(po.status, 'Pending Approval', 'Submitted')}
                            onSubmit={async () => {
                              await purchasingService.submitPurchaseOrder(po.id);
                            }}
                            onApprove={async (comments) => {
                              await purchasingService.approvePurchaseOrder(po.id, {
                                approved: true,
                                comments: comments || undefined,
                              });
                            }}
                            onReject={async (comments) => {
                              await purchasingService.approvePurchaseOrder(po.id, {
                                approved: false,
                                comments: comments || undefined,
                                rejectionReason: comments || undefined,
                              });
                            }}
                            onAfterAction={fetchOrders}
                            onOpenWorkflows={() => router.push('/administration/workflow')}
                          />
                          
                          {isPurchaseOrderStatus(po.status, 'Approved', 'Sent', 'Acknowledged', 'Partially Received') && (
                            <Link href={`/procurement/purchase-orders/${po.id}/receive`}>
                              <Button size="sm">
                                <Package className="h-4 w-4 mr-1" />
                                Receive
                              </Button>
                            </Link>
                          )}
                        </div>
                      </div>
                    </div>
                  );
                })
              )}
            </div>
          )}
          
          {/* Pagination */}
          {!loading && !error && totalCount > 0 && (
            <Pagination
              currentPage={currentPage}
              totalPages={totalPages}
              totalItems={totalCount}
              pageSize={pageSize}
              onPageChange={handlePageChange}
              onPageSizeChange={handlePageSizeChange}
            />
          )}
        </CardContent>
      </Card>
    </div>
  );
}
