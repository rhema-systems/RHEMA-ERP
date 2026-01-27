'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  Search,
  Eye,
  Edit,
  Plus,
  Download,
  RefreshCw,
  Filter,
  Trash2
} from 'lucide-react';
import { toast } from 'sonner';
import { procurementPlanService, type ProcurementPlanDto } from '@/services/procurementPlanningService';
import { format } from 'date-fns';
import * as XLSX from 'xlsx';
import { saveAs } from 'file-saver';

export default function ProcurementPlansPage() {
  const router = useRouter();
  const [plans, setPlans] = useState<ProcurementPlanDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  useEffect(() => {
    loadPlans();
  }, [page, statusFilter]);

  const loadPlans = async () => {
    try {
      setLoading(true);
      const result = await procurementPlanService.getPlans({
        page,
        pageSize: 25,
        search: searchTerm || undefined,
        status: statusFilter !== 'all' ? statusFilter : undefined,
      });
      setPlans(result.items);
      setTotalPages(result.totalPages);
    } catch (error) {
      console.error('Error loading procurement plans:', error);
      toast.error('Failed to load procurement plans');
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = () => {
    setPage(1);
    loadPlans();
  };

  const handleViewDetails = (id: string) => {
    router.push(`/procurement/planning/plans/${id}`);
  };

  const handleEdit = (id: string) => {
    router.push(`/procurement/planning/plans/${id}/edit`);
  };

  const handleCreateNew = () => {
    router.push('/procurement/planning/plans/new');
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this procurement plan?')) return;

    try {
      await procurementPlanService.deletePlan(id);
      toast.success('Procurement plan deleted successfully');
      loadPlans();
    } catch (error) {
      console.error('Error deleting procurement plan:', error);
      toast.error('Failed to delete procurement plan');
    }
  };

  const handleExportToExcel = () => {
    try {
      if (plans.length === 0) {
        toast.error('No data to export');
        return;
      }

      const exportData = plans.map(plan => ({
        'Plan Number': plan.planNumber,
        'Title': plan.title,
        'Department': plan.departmentName || '',
        'Fiscal Year': plan.fiscalYear,
        'Status': plan.status,
        'Start Date': format(new Date(plan.planStartDate), 'yyyy-MM-dd'),
        'End Date': format(new Date(plan.planEndDate), 'yyyy-MM-dd'),
        'Estimated Budget': plan.totalEstimatedBudget,
        'Approved Budget': plan.approvedBudget,
        'Currency': plan.currency,
        'Items': plan.itemCount,
        'Created At': format(new Date(plan.createdAt), 'yyyy-MM-dd'),
      }));

      const ws = XLSX.utils.json_to_sheet(exportData);
      const wb = XLSX.utils.book_new();
      XLSX.utils.book_append_sheet(wb, ws, 'Procurement Plans');

      const excelBuffer = XLSX.write(wb, { bookType: 'xlsx', type: 'array' });
      const data = new Blob([excelBuffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
      saveAs(data, `procurement_plans_${format(new Date(), 'yyyyMMdd')}.xlsx`);

      toast.success('Procurement plans exported successfully');
    } catch (error) {
      console.error('Error exporting procurement plans:', error);
      toast.error('Failed to export procurement plans');
    }
  };

  const getStatusBadge = (status: string) => {
    const statusConfig: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', className: string }> = {
      'Draft': { variant: 'secondary', className: 'bg-gray-100 text-gray-800' },
      'Submitted': { variant: 'default', className: 'bg-blue-100 text-blue-800' },
      'UnderReview': { variant: 'outline', className: 'bg-yellow-100 text-yellow-800' },
      'Approved': { variant: 'default', className: 'bg-green-100 text-green-800' },
      'Rejected': { variant: 'destructive', className: 'bg-red-100 text-red-800' },
      'Active': { variant: 'default', className: 'bg-emerald-100 text-emerald-800' },
      'Completed': { variant: 'outline', className: 'bg-purple-100 text-purple-800' },
    };

    const config = statusConfig[status] || { variant: 'outline' as const, className: '' };
    return (
      <Badge variant={config.variant} className={config.className}>
        {status}
      </Badge>
    );
  };

  const formatCurrency = (amount: number, currency: string) => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: currency || 'USD',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(amount);
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Procurement Plans</h1>
          <p className="text-muted-foreground">
            Manage departmental procurement plans and requirements
          </p>
        </div>
        <Button onClick={handleCreateNew} className="gap-2">
          <Plus className="h-4 w-4" />
          Create Plan
        </Button>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Filter className="h-5 w-5" />
            Filters
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="flex gap-2">
              <Input
                placeholder="Search plans..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
                className="flex-1"
              />
              <Button onClick={handleSearch} size="icon" variant="secondary">
                <Search className="h-4 w-4" />
              </Button>
            </div>

            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger>
                <SelectValue placeholder="All Statuses" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="Submitted">Submitted</SelectItem>
                <SelectItem value="UnderReview">Under Review</SelectItem>
                <SelectItem value="Approved">Approved</SelectItem>
                <SelectItem value="Rejected">Rejected</SelectItem>
                <SelectItem value="Active">Active</SelectItem>
                <SelectItem value="Completed">Completed</SelectItem>
              </SelectContent>
            </Select>

            <div className="flex gap-2">
              <Button onClick={loadPlans} variant="outline" className="gap-2 flex-1">
                <RefreshCw className="h-4 w-4" />
                Refresh
              </Button>
              <Button onClick={handleExportToExcel} variant="outline" className="gap-2 flex-1">
                <Download className="h-4 w-4" />
                Export
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Plans Table */}
      <Card>
        <CardHeader>
          <CardTitle>Procurement Plans</CardTitle>
          <CardDescription>
            {loading ? 'Loading...' : `${plans.length} plan(s) found`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8">Loading procurement plans...</div>
          ) : plans.length === 0 ? (
            <div className="text-center py-8 text-gray-500">No procurement plans found</div>
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Plan #</TableHead>
                    <TableHead>Title</TableHead>
                    <TableHead>Department</TableHead>
                    <TableHead>Fiscal Year</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Estimated Budget</TableHead>
                    <TableHead>Items</TableHead>
                    <TableHead>Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {plans.map((plan) => (
                    <TableRow key={plan.id}>
                      <TableCell className="font-medium">{plan.planNumber}</TableCell>
                      <TableCell>
                        <div>
                          <div className="font-medium">{plan.title}</div>
                          <div className="text-sm text-gray-500">
                            {format(new Date(plan.planStartDate), 'MMM dd, yyyy')} - {format(new Date(plan.planEndDate), 'MMM dd, yyyy')}
                          </div>
                        </div>
                      </TableCell>
                      <TableCell>{plan.departmentName || 'N/A'}</TableCell>
                      <TableCell>{plan.fiscalYear}</TableCell>
                      <TableCell>{getStatusBadge(plan.status)}</TableCell>
                      <TableCell>{formatCurrency(plan.totalEstimatedBudget, plan.currency)}</TableCell>
                      <TableCell>
                        <Badge variant="secondary">{plan.itemCount} items</Badge>
                      </TableCell>
                      <TableCell>
                        <div className="flex gap-2">
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => handleViewDetails(plan.id)}
                            title="View Details"
                          >
                            <Eye className="h-4 w-4" />
                          </Button>
                          {plan.status === 'Draft' && (
                            <>
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => handleEdit(plan.id)}
                                title="Edit"
                              >
                                <Edit className="h-4 w-4" />
                              </Button>
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => handleDelete(plan.id)}
                                title="Delete"
                              >
                                <Trash2 className="h-4 w-4" />
                              </Button>
                            </>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-between mt-4">
              <div className="text-sm text-gray-500">
                Page {page} of {totalPages}
              </div>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPage(p => Math.max(1, p - 1))}
                  disabled={page === 1}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                  disabled={page === totalPages}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

