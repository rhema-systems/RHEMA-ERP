'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Progress } from '@/components/ui/progress';
import {
  Search,
  Eye,
  Edit,
  Plus,
  Download,
  RefreshCw,
  Filter,
  Trash2,
  DollarSign
} from 'lucide-react';
import { toast } from 'sonner';
import { procurementBudgetService, type ProcurementBudgetDto } from '@/services/procurementPlanningService';
import { format } from 'date-fns';
import * as XLSX from 'xlsx';
import { saveAs } from 'file-saver';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';

export default function ProcurementBudgetsPage() {
  const router = useRouter();
  const [budgets, setBudgets] = useState<ProcurementBudgetDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [budgetToDelete, setBudgetToDelete] = useState<ProcurementBudgetDto | null>(null);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    loadBudgets();
  }, [page, statusFilter]);

  const loadBudgets = async () => {
    try {
      setLoading(true);
      const result = await procurementBudgetService.getBudgets({
        page,
        pageSize: 25,
        search: searchTerm || undefined,
        status: statusFilter !== 'all' ? statusFilter : undefined,
      });
      setBudgets(result.items);
      setTotalPages(result.totalPages);
    } catch (error) {
      console.error('Error loading budgets:', error);
      toast.error('Failed to load procurement budgets');
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = () => {
    setPage(1);
    loadBudgets();
  };

  const handleViewDetails = (id: string) => {
    router.push(`/procurement/planning/budgets/${id}`);
  };

  const handleEdit = (id: string) => {
    router.push(`/procurement/planning/budgets/${id}/edit`);
  };

  const handleCreateNew = () => {
    router.push('/procurement/planning/budgets/new');
  };

  const handleDelete = async () => {
    if (!budgetToDelete) return false;
    try {
      setDeleting(true);
      await procurementBudgetService.deleteBudget(budgetToDelete.id);
      toast.success('Budget deleted successfully');
      await loadBudgets();
    } catch (error) {
      console.error('Error deleting budget:', error);
      toast.error(error instanceof Error ? error.message : 'Failed to delete budget');
      return false;
    } finally {
      setDeleting(false);
    }
  };

  const handleExportToExcel = () => {
    try {
      if (budgets.length === 0) {
        toast.error('No data to export');
        return;
      }
      const exportData = budgets.map(budget => ({
        'Budget Code': budget.budgetCode,
        'Title': budget.title,
        'Organization Unit': budget.organizationUnitName || budget.departmentName || '',
        'Fiscal Year': budget.fiscalYear,
        'Status': budget.status,
        'Allocated': budget.allocatedAmount,
        'Utilized': budget.utilizedAmount,
        'Remaining': budget.remainingAmount,
        'Utilization %': budget.utilizationPercent,
        'Currency': budget.currency,
      }));
      const ws = XLSX.utils.json_to_sheet(exportData);
      const wb = XLSX.utils.book_new();
      XLSX.utils.book_append_sheet(wb, ws, 'Budgets');
      const excelBuffer = XLSX.write(wb, { bookType: 'xlsx', type: 'array' });
      const data = new Blob([excelBuffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
      saveAs(data, `procurement_budgets_${format(new Date(), 'yyyyMMdd')}.xlsx`);
      toast.success('Budgets exported successfully');
    } catch (error) {
      console.error('Error exporting budgets:', error);
      toast.error('Failed to export budgets');
    }
  };

  const getStatusBadge = (status: string) => {
    const statusConfig: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', className: string }> = {
      'Draft': { variant: 'secondary', className: 'bg-gray-100 text-gray-800' },
      'Active': { variant: 'default', className: 'bg-green-100 text-green-800' },
      'Frozen': { variant: 'outline', className: 'bg-blue-100 text-blue-800' },
      'Closed': { variant: 'outline', className: 'bg-purple-100 text-purple-800' },
    };
    const config = statusConfig[status] || { variant: 'outline' as const, className: '' };
    return <Badge variant={config.variant} className={config.className}>{status}</Badge>;
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
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Procurement Budgets</h1>
          <p className="text-muted-foreground">Manage organization-unit procurement budgets and allocations</p>
        </div>
        <Button onClick={handleCreateNew} className="gap-2">
          <Plus className="h-4 w-4" />
          Create Budget
        </Button>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2"><Filter className="h-5 w-5" />Filters</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="flex gap-2">
              <Input placeholder="Search budgets..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleSearch()} className="flex-1" />
              <Button onClick={handleSearch} size="icon" variant="secondary"><Search className="h-4 w-4" /></Button>
            </div>
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger><SelectValue placeholder="All Statuses" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="Active">Active</SelectItem>
                <SelectItem value="Frozen">Frozen</SelectItem>
                <SelectItem value="Closed">Closed</SelectItem>
              </SelectContent>
            </Select>
            <div className="flex gap-2">
              <Button onClick={loadBudgets} variant="outline" className="gap-2 flex-1"><RefreshCw className="h-4 w-4" />Refresh</Button>
              <Button onClick={handleExportToExcel} variant="outline" className="gap-2 flex-1"><Download className="h-4 w-4" />Export</Button>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Budgets Table */}
      <Card>
        <CardHeader>
          <CardTitle>Budgets</CardTitle>
          <CardDescription>{loading ? 'Loading...' : `${budgets.length} budget(s) found`}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8">Loading budgets...</div>
          ) : budgets.length === 0 ? (
            <div className="text-center py-8 text-gray-500">No budgets found</div>
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Budget Code</TableHead>
                    <TableHead>Title</TableHead>
                    <TableHead>Organization Unit</TableHead>
                    <TableHead>Fiscal Year</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Allocated</TableHead>
                    <TableHead>Utilization</TableHead>
                    <TableHead>Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {budgets.map((budget) => (
                    <TableRow key={budget.id}>
                      <TableCell className="font-medium">{budget.budgetCode}</TableCell>
                      <TableCell>{budget.title}</TableCell>
                      <TableCell>{budget.organizationUnitName || budget.departmentName || 'N/A'}</TableCell>
                      <TableCell>{budget.fiscalYear}</TableCell>
                      <TableCell>{getStatusBadge(budget.status)}</TableCell>
                      <TableCell>{formatCurrency(budget.allocatedAmount, budget.currency)}</TableCell>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          <Progress value={budget.utilizationPercent} className="w-20" />
                          <span className="text-sm">{budget.utilizationPercent.toFixed(0)}%</span>
                        </div>
                      </TableCell>
                      <TableCell>
                        <div className="flex gap-2">
                          <Button variant="ghost" size="sm" onClick={() => handleViewDetails(budget.id)} title="View"><Eye className="h-4 w-4" /></Button>
                          {budget.status === 'Draft' && (
                            <>
                              <Button variant="ghost" size="sm" onClick={() => handleEdit(budget.id)} title="Edit"><Edit className="h-4 w-4" /></Button>
                              <Button variant="ghost" size="sm" onClick={() => setBudgetToDelete(budget)} title="Delete"><Trash2 className="h-4 w-4" /></Button>
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
          {totalPages > 1 && (
            <div className="flex items-center justify-between mt-4">
              <div className="text-sm text-gray-500">Page {page} of {totalPages}</div>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1}>Previous</Button>
                <Button variant="outline" size="sm" onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages}>Next</Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={budgetToDelete !== null}
        onOpenChange={(open) => { if (!open) setBudgetToDelete(null); }}
        title="Delete procurement budget?"
        description={budgetToDelete
          ? `Delete ${budgetToDelete.budgetCode}? This action is available only while the budget is still a draft.`
          : undefined}
        confirmText="Delete budget"
        variant="destructive"
        onConfirm={handleDelete}
        isLoading={deleting}
      />
    </div>
  );
}

