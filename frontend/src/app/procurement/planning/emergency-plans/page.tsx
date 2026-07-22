'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Search, Eye, Edit, Plus, Download, RefreshCw, Filter, Trash2, AlertTriangle, Shield, PlayCircle, Power, Siren } from 'lucide-react';
import { toast } from 'sonner';
import { emergencyProcurementPlanService, type EmergencyProcurementPlanDto } from '@/services/procurementPlanningService';
import { format } from 'date-fns';
import * as XLSX from 'xlsx';
import { saveAs } from 'file-saver';

export default function EmergencyPlansPage() {
  const router = useRouter();
  const [plans, setPlans] = useState<EmergencyProcurementPlanDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [actionPlanId, setActionPlanId] = useState<string | null>(null);

  useEffect(() => { loadPlans(); }, [page, statusFilter]);

  const loadPlans = async () => {
    try {
      setLoading(true);
      const result = await emergencyProcurementPlanService.getPlans({
        page, pageSize: 25,
        search: searchTerm || undefined,
        status: statusFilter !== 'all' ? statusFilter : undefined,
      });
      setPlans(result.items);
      setTotalPages(result.totalPages);
    } catch (error) {
      console.error('Error loading plans:', error);
      toast.error('Failed to load emergency plans');
    } finally { setLoading(false); }
  };

  const handleSearch = () => { setPage(1); loadPlans(); };
  const handleViewDetails = (id: string) => router.push(`/procurement/planning/emergency-plans/${id}`);
  const handleEdit = (id: string) => router.push(`/procurement/planning/emergency-plans/${id}/edit`);
  const handleCreateNew = () => router.push('/procurement/planning/emergency-plans/new');
  const getPlanValidFrom = (plan: EmergencyProcurementPlanDto) => plan.validFrom || plan.effectiveDate;
  const getPlanValidTo = (plan: EmergencyProcurementPlanDto) => plan.validTo || plan.expiryDate;

  const handleEmergencyAction = async (plan: EmergencyProcurementPlanDto, action: 'activate' | 'trigger' | 'deactivate') => {
    if (action === 'trigger' && !confirm(`Trigger emergency procurement for ${plan.planNumber || plan.planCode}?`)) return;

    try {
      setActionPlanId(plan.id);
      if (action === 'activate') {
        await emergencyProcurementPlanService.activatePlan(plan.id);
        toast.success('Emergency plan activated');
      } else if (action === 'trigger') {
        await emergencyProcurementPlanService.triggerPlan(plan.id);
        toast.success('Emergency procurement triggered');
      } else {
        await emergencyProcurementPlanService.deactivatePlan(plan.id);
        toast.success('Emergency plan deactivated');
      }
      await loadPlans();
    } catch (error) {
      console.error('Error processing emergency plan action:', error);
      toast.error('Failed to update emergency plan');
    } finally {
      setActionPlanId(null);
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this emergency plan?')) return;
    try {
      await emergencyProcurementPlanService.deletePlan(id);
      toast.success('Emergency plan deleted successfully');
      loadPlans();
    } catch (error) {
      console.error('Error deleting plan:', error);
      toast.error('Failed to delete emergency plan');
    }
  };

  const handleExportToExcel = () => {
    try {
      if (plans.length === 0) { toast.error('No data to export'); return; }
      const exportData = plans.map(p => ({
        'Plan #': p.planNumber, 'Title': p.title, 'Department': p.departmentName || '',
        'Status': p.status, 'Emergency Type': p.emergencyType, 'Critical Items': p.criticalItemCount,
        'Emergency Suppliers': p.emergencySupplierCount, 'Budget Reserve': p.emergencyBudgetReserve ?? p.budgetReserve,
        'Currency': p.currency, 'Valid From': getPlanValidFrom(p) ? format(new Date(getPlanValidFrom(p) || ''), 'yyyy-MM-dd') : '',
        'Valid To': getPlanValidTo(p) ? format(new Date(getPlanValidTo(p) || ''), 'yyyy-MM-dd') : '',
      }));
      const ws = XLSX.utils.json_to_sheet(exportData);
      const wb = XLSX.utils.book_new();
      XLSX.utils.book_append_sheet(wb, ws, 'Emergency Plans');
      const excelBuffer = XLSX.write(wb, { bookType: 'xlsx', type: 'array' });
      const data = new Blob([excelBuffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
      saveAs(data, `emergency_plans_${format(new Date(), 'yyyyMMdd')}.xlsx`);
      toast.success('Emergency plans exported successfully');
    } catch (error) { console.error('Error exporting:', error); toast.error('Failed to export'); }
  };

  const getStatusBadge = (status: string) => {
    const config: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', className: string }> = {
      'Draft': { variant: 'secondary', className: 'bg-gray-100 text-gray-800' },
      'Active': { variant: 'default', className: 'bg-green-100 text-green-800' },
      'Activated': { variant: 'destructive', className: 'bg-red-100 text-red-800' },
      'Triggered': { variant: 'destructive', className: 'bg-red-100 text-red-800' },
      'Inactive': { variant: 'outline', className: 'bg-gray-100 text-gray-800' },
      'Expired': { variant: 'outline', className: 'bg-gray-100 text-gray-800' },
      'Archived': { variant: 'outline', className: 'bg-gray-100 text-gray-800' },
    };
    const c = config[status] || { variant: 'outline' as const, className: '' };
    return <Badge variant={c.variant} className={c.className}>{status}</Badge>;
  };

  const getEmergencyTypeBadge = (type: string) => {
    const config: Record<string, string> = {
      'NaturalDisaster': 'bg-red-100 text-red-800', 'SupplyChainDisruption': 'bg-orange-100 text-orange-800',
      'PandemicResponse': 'bg-purple-100 text-purple-800', 'CriticalShortage': 'bg-yellow-100 text-yellow-800',
      'Other': 'bg-gray-100 text-gray-800',
    };
    return <Badge variant="outline" className={config[type] || ''}>{type.replace(/([A-Z])/g, ' $1').trim()}</Badge>;
  };

  const formatCurrency = (amount: number, currency: string) => {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency: currency || 'USD', minimumFractionDigits: 0, maximumFractionDigits: 0 }).format(amount);
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Emergency Procurement Plans</h1>
          <p className="text-muted-foreground">Manage emergency and contingency procurement plans</p>
        </div>
        <Button onClick={handleCreateNew} className="gap-2"><Plus className="h-4 w-4" />New Emergency Plan</Button>
      </div>

      <Card>
        <CardHeader><CardTitle className="flex items-center gap-2"><Filter className="h-5 w-5" />Filters</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="flex gap-2">
              <Input placeholder="Search plans..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleSearch()} className="flex-1" />
              <Button onClick={handleSearch} size="icon" variant="secondary"><Search className="h-4 w-4" /></Button>
            </div>
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger><SelectValue placeholder="All Statuses" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="Active">Active</SelectItem>
                <SelectItem value="Activated">Activated</SelectItem>
                <SelectItem value="Expired">Expired</SelectItem>
                <SelectItem value="Archived">Archived</SelectItem>
              </SelectContent>
            </Select>
            <div className="flex gap-2">
              <Button onClick={loadPlans} variant="outline" className="gap-2 flex-1"><RefreshCw className="h-4 w-4" />Refresh</Button>
              <Button onClick={handleExportToExcel} variant="outline" className="gap-2 flex-1"><Download className="h-4 w-4" />Export</Button>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Emergency Plans</CardTitle><CardDescription>{loading ? 'Loading...' : `${plans.length} plan(s) found`}</CardDescription></CardHeader>
        <CardContent>
          {loading ? (<div className="text-center py-8">Loading emergency plans...</div>) : plans.length === 0 ? (<div className="text-center py-8 text-gray-500">No emergency plans found</div>) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader><TableRow><TableHead>Plan #</TableHead><TableHead>Title</TableHead><TableHead>Department</TableHead><TableHead>Status</TableHead><TableHead>Type</TableHead><TableHead>Resources</TableHead><TableHead>Budget Reserve</TableHead><TableHead>Actions</TableHead></TableRow></TableHeader>
                <TableBody>
                  {plans.map((p) => (
                    <TableRow key={p.id}>
                      <TableCell className="font-medium">{p.planNumber}</TableCell>
                      <TableCell><div><div className="font-medium">{p.title}</div><div className="text-sm text-gray-500">{getPlanValidFrom(p) ? format(new Date(getPlanValidFrom(p) || ''), 'MMM dd, yyyy') : 'N/A'} - {getPlanValidTo(p) ? format(new Date(getPlanValidTo(p) || ''), 'MMM dd, yyyy') : 'N/A'}</div></div></TableCell>
                      <TableCell>{p.departmentName || 'N/A'}</TableCell>
                      <TableCell>{getStatusBadge(p.status)}</TableCell>
                      <TableCell>{getEmergencyTypeBadge(p.emergencyType)}</TableCell>
                      <TableCell><div className="flex items-center gap-4"><div className="flex items-center gap-1"><AlertTriangle className="h-4 w-4 text-orange-500" /><span>{p.criticalItemCount}</span></div><div className="flex items-center gap-1"><Shield className="h-4 w-4 text-blue-500" /><span>{p.emergencySupplierCount}</span></div></div></TableCell>
                      <TableCell>{formatCurrency(p.emergencyBudgetReserve ?? p.budgetReserve, p.currency)}</TableCell>
                      <TableCell>
                        <div className="flex gap-2">
                          <Button variant="ghost" size="sm" onClick={() => handleViewDetails(p.id)} title="View"><Eye className="h-4 w-4" /></Button>
                          {p.status === 'Draft' && (
                            <>
                              <Button variant="ghost" size="sm" onClick={() => handleEdit(p.id)} title="Edit"><Edit className="h-4 w-4" /></Button>
                              <Button variant="ghost" size="sm" onClick={() => handleEmergencyAction(p, 'activate')} disabled={actionPlanId === p.id} title="Activate"><PlayCircle className="h-4 w-4 text-green-600" /></Button>
                              <Button variant="ghost" size="sm" onClick={() => handleDelete(p.id)} title="Delete"><Trash2 className="h-4 w-4" /></Button>
                            </>
                          )}
                          {p.status === 'Active' && (
                            <>
                              <Button variant="ghost" size="sm" onClick={() => handleEmergencyAction(p, 'trigger')} disabled={actionPlanId === p.id} title="Trigger emergency request"><Siren className="h-4 w-4 text-red-600" /></Button>
                              <Button variant="ghost" size="sm" onClick={() => handleEmergencyAction(p, 'deactivate')} disabled={actionPlanId === p.id} title="Deactivate"><Power className="h-4 w-4 text-gray-600" /></Button>
                            </>
                          )}
                          {p.status === 'Triggered' && (
                            <Button variant="ghost" size="sm" onClick={() => handleEmergencyAction(p, 'deactivate')} disabled={actionPlanId === p.id} title="Close emergency trigger"><Power className="h-4 w-4 text-gray-600" /></Button>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
          {totalPages > 1 && (<div className="flex items-center justify-between mt-4"><div className="text-sm text-gray-500">Page {page} of {totalPages}</div><div className="flex gap-2"><Button variant="outline" size="sm" onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1}>Previous</Button><Button variant="outline" size="sm" onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages}>Next</Button></div></div>)}
        </CardContent>
      </Card>
    </div>
  );
}
