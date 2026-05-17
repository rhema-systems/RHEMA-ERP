 
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
  AlertTriangle, Filter, ShoppingCart, Calendar
} from 'lucide-react';
import {
  purchasingService,
  PurchaseRequisitionSummaryDto
} from '@/services/purchasingService';
import { format } from 'date-fns';
import Link from 'next/link';
import { toast } from 'sonner';

const PRStatuses = [
  { value: 'Draft', label: 'Draft', color: 'bg-gray-100 text-gray-800', icon: FileText },
  { value: 'Submitted', label: 'Submitted', color: 'bg-blue-100 text-blue-800', icon: Clock },
  { value: 'Pending Approval', label: 'Pending Approval', color: 'bg-yellow-100 text-yellow-800', icon: Clock },
  { value: 'Approved', label: 'Approved', color: 'bg-green-100 text-green-800', icon: CheckCircle },
  { value: 'Rejected', label: 'Rejected', color: 'bg-red-100 text-red-800', icon: XCircle },
  { value: 'Ordered', label: 'Ordered', color: 'bg-purple-100 text-purple-800', icon: ShoppingCart }
];

const PRPriorities = [
  { value: 'Low', label: 'Low', color: 'bg-gray-100 text-gray-600' },
  { value: 'Normal', label: 'Normal', color: 'bg-blue-100 text-blue-600' },
  { value: 'High', label: 'High', color: 'bg-orange-100 text-orange-600' },
  { value: 'Urgent', label: 'Urgent', color: 'bg-red-100 text-red-600' }
];

export default function PurchaseRequisitionsPage() {
  const router = useRouter();
  const [requisitions, setRequisitions] = useState<PurchaseRequisitionSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Submit/approve/reject UX is centralized in <WorkflowApprovalActions /> (no browser prompt/confirm).
  const { summariesById: workflowSummariesById } = useWorkflowEntitySummaries(
    'PurchaseRequisition',
    requisitions.map((r) => r.id),
    requisitions.length > 0
  );
  
  // Pagination
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [totalCount, setTotalCount] = useState(0);
  
  // Filters
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [priorityFilter, setPriorityFilter] = useState('all');
  const [departmentFilter, setDepartmentFilter] = useState('all');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');

  const fetchRequisitions = async () => {
    try {
      setLoading(true);
      setError(null);
      const result = await purchasingService.getPurchaseRequisitions({
        page: currentPage,
        pageSize: pageSize,
        search: searchTerm || undefined,
        status: statusFilter !== 'all' ? statusFilter : undefined,
        priority: priorityFilter !== 'all' ? priorityFilter : undefined,
        department: departmentFilter !== 'all' ? departmentFilter : undefined,
        startDate: startDate || undefined,
        endDate: endDate || undefined
      });
      setRequisitions(result.items);
      setTotalCount(result.totalCount);
    } catch (err: any) {
      console.error('Error fetching purchase requisitions:', err);
      setError('Failed to load purchase requisitions');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchRequisitions();
  }, [currentPage, pageSize, searchTerm, statusFilter, priorityFilter, departmentFilter, startDate, endDate]);

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

  // Submit/approve/reject UX is centralized in <WorkflowApprovalActions /> (no browser prompt/confirm).

  const getStatusBadge = (status: string) => {
    const statusConfig = PRStatuses.find(s => s.value === status);
    const Icon = statusConfig?.icon || FileText;
    return (
      <Badge className={statusConfig?.color || 'bg-gray-100'}>
        <Icon className="h-3 w-3 mr-1" />
        {statusConfig?.label || status}
      </Badge>
    );
  };

  const getPriorityBadge = (priority: string) => {
    const priorityConfig = PRPriorities.find(p => p.value === priority);
    return (
      <Badge variant="outline" className={priorityConfig?.color || 'bg-gray-100'}>
        {priorityConfig?.label || priority}
      </Badge>
    );
  };

  // Calculate stats (these would ideally come from a separate API endpoint)
  const pendingCount = requisitions.filter(pr => pr.status === 'Pending Approval' || pr.status === 'Submitted').length;
  const approvedCount = requisitions.filter(pr => pr.status === 'Approved').length;
  const rejectedCount = requisitions.filter(pr => pr.status === 'Rejected').length;

  // Get unique departments for filter
  const departments = Array.from(
    new Set(requisitions.map(pr => pr.department).filter((dept): dept is string => Boolean(dept)))
  );
  
  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Purchase Requisitions</h1>
          <p className="text-muted-foreground">Manage internal purchase requests and approvals</p>
        </div>
        <Link href="/procurement/purchase-requisitions/new">
          <Button>
            <Plus className="mr-2 h-4 w-4" />
            New Requisition
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
            <BreadcrumbPage>Purchase Requisitions</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{totalCount}</p>
                <p className="text-sm text-muted-foreground">Total Requisitions</p>
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
                <p className="text-sm text-muted-foreground">Pending Approval</p>
              </div>
              <Clock className="h-8 w-8 text-yellow-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold text-green-600">{approvedCount}</p>
                <p className="text-sm text-muted-foreground">Approved</p>
              </div>
              <CheckCircle className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold text-red-600">{rejectedCount}</p>
                <p className="text-sm text-muted-foreground">Rejected</p>
              </div>
              <XCircle className="h-8 w-8 text-red-500" />
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
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
              <div className="relative">
                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Search requisitions..."
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
                  {PRStatuses.map(s => (
                    <SelectItem key={s.value} value={s.value}>{s.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              
              <Select value={priorityFilter} onValueChange={(value) => {
                setPriorityFilter(value);
                handleFilterChange();
              }}>
                <SelectTrigger>
                  <SelectValue placeholder="Priority" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Priorities</SelectItem>
                  {PRPriorities.map(p => (
                    <SelectItem key={p.value} value={p.value}>{p.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              
              <Select value={departmentFilter} onValueChange={(value) => {
                setDepartmentFilter(value);
                handleFilterChange();
              }}>
                <SelectTrigger>
                  <SelectValue placeholder="Department" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Departments</SelectItem>
                  {departments.map(dept => (
                    <SelectItem key={dept} value={dept}>{dept}</SelectItem>
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
            {(searchTerm || statusFilter !== 'all' || priorityFilter !== 'all' || departmentFilter !== 'all' || startDate || endDate) && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => {
                  setSearchTerm('');
                  setStatusFilter('all');
                  setPriorityFilter('all');
                  setDepartmentFilter('all');
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

      {/* Requisitions List */}
      <Card>
        <CardHeader>
          <CardTitle>Purchase Requisitions</CardTitle>
          <CardDescription>
            {loading ? 'Loading...' : `${totalCount} requisition(s) found`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading && (
            <div className="text-center py-8 text-muted-foreground">Loading...</div>
          )}
          
          {error && (
            <div className="text-center py-8 text-red-600">{error}</div>
          )}
          
          {!loading && !error && (
            <div className="space-y-3">
              {requisitions.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">
                  No purchase requisitions found.
                </div>
              ) : (
                requisitions.map((pr) => {
                  const summary = workflowSummariesById[pr.id];
                  const stepName = summary?.currentStepName || pr.currentWorkflowStepName;
                  const pending = formatPendingApprovers(summary?.pendingApprovers || []);
                  const showWorkflowBadges = pr.status === 'Pending Approval' || pr.status === 'Submitted';

                  return (
                    <div
                      key={pr.id}
                      className="border rounded-lg p-4 hover:bg-muted/50 transition-colors"
                    >
                    <div className="flex items-center justify-between">
                      <div className="flex items-center space-x-4">
                        <div className="w-12 h-12 rounded-lg bg-blue-100 flex items-center justify-center">
                          <FileText className="h-6 w-6 text-blue-600" />
                        </div>
                        <div>
                          <div className="flex items-center space-x-2">
                            <h3 className="font-semibold">{pr.requisitionNumber}</h3>
                            {getStatusBadge(pr.status)}
                            {getPriorityBadge(pr.priority)}
                            {showWorkflowBadges && stepName && (
                              <Badge variant="outline" className="text-muted-foreground">
                                Step: {stepName}
                              </Badge>
                            )}
                            {showWorkflowBadges && pending.short && (
                              <Badge variant="outline" className="text-muted-foreground" title={pending.full}>
                                Pending with: {pending.short}
                              </Badge>
                            )}
                          </div>
                          <p className="text-sm text-muted-foreground">
                            Requested by: {pr.requestedByName} •
                            Date: {format(new Date(pr.requisitionDate), 'MMM dd, yyyy')} •
                            {pr.department && `Department: ${pr.department} •`}
                            Items: {pr.itemCount}
                          </p>
                          <p className="text-sm font-medium text-primary">
                            Total: ${pr.totalAmount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                          </p>
                        </div>
                      </div>
                      
                      <div className="flex items-center space-x-2">
                        <Link href={`/procurement/purchase-requisitions/${pr.id}`}>
                          <Button size="sm" variant="outline">
                            <Eye className="h-4 w-4 mr-1" />
                            View
                          </Button>
                        </Link>
                        
                        <WorkflowApprovalActions
                          entityType="PurchaseRequisition"
                          entityId={pr.id}
                          entityLabel="Purchase Requisition"
                          entityNumber={pr.requisitionNumber}
                          status={pr.status}
                          currentStepName={stepName}
                          workflowSummary={summary}
                          canSubmit={pr.status === 'Draft'}
                          canApproveReject={pr.status === 'Pending Approval' || pr.status === 'Submitted'}
                          onSubmit={async () => {
                            await purchasingService.submitPurchaseRequisition(pr.id);
                          }}
                          onApprove={async (comments) => {
                            await purchasingService.approvePurchaseRequisition(pr.id, {
                              approved: true,
                              comments: comments || undefined,
                            });
                          }}
                          onReject={async (comments) => {
                            await purchasingService.approvePurchaseRequisition(pr.id, {
                              approved: false,
                              comments: comments || undefined,
                              rejectionReason: comments || undefined,
                            });
                          }}
                          onAfterAction={fetchRequisitions}
                          onOpenWorkflows={() => router.push('/administration/workflow')}
                        />
                        
                        {/* After approval, users should open the PR to create PO/RFQ from the detail view. */}
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
