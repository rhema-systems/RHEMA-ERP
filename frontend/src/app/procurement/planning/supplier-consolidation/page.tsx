'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Search, Eye, Edit, Plus, Download, RefreshCw, Filter, Trash2, Users } from 'lucide-react';
import { toast } from 'sonner';
import {
  procurementPlanService,
  supplierConsolidationService,
  type ProcurementPlanConsolidationOpportunityDto,
  type SupplierConsolidationDto,
} from '@/services/procurementPlanningService';
import { format } from 'date-fns';
import * as XLSX from 'xlsx';
import { saveAs } from 'file-saver';

export default function SupplierConsolidationPage() {
  const router = useRouter();
  const [consolidations, setConsolidations] = useState<SupplierConsolidationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [opportunities, setOpportunities] = useState<ProcurementPlanConsolidationOpportunityDto[]>([]);
  const [creatingOpportunityKey, setCreatingOpportunityKey] = useState<string | null>(null);

  useEffect(() => { loadConsolidations(); loadOpportunities(); }, [page, statusFilter]);

  const loadConsolidations = async () => {
    try {
      setLoading(true);
      const result = await supplierConsolidationService.getConsolidations({
        page, pageSize: 25,
        search: searchTerm || undefined,
        status: statusFilter !== 'all' ? statusFilter : undefined,
      });
      setConsolidations(result.items);
      setTotalPages(result.totalPages);
    } catch (error) {
      console.error('Error loading consolidations:', error);
      toast.error('Failed to load supplier consolidations');
    } finally { setLoading(false); }
  };

  const loadOpportunities = async () => {
    try {
      const data = await procurementPlanService.getConsolidationOpportunities();
      setOpportunities(data);
    } catch (error) {
      console.error('Error loading consolidation opportunities:', error);
      setOpportunities([]);
    }
  };

  const handleSearch = () => { setPage(1); loadConsolidations(); };
  const handleViewDetails = (id: string) => router.push(`/procurement/planning/supplier-consolidation/${id}`);
  const handleEdit = (id: string) => router.push(`/procurement/planning/supplier-consolidation/${id}/edit`);
  const handleCreateNew = () => router.push('/procurement/planning/supplier-consolidation/new');

  const handleCreateFromOpportunity = async (opportunity: ProcurementPlanConsolidationOpportunityDto) => {
    try {
      setCreatingOpportunityKey(opportunity.opportunityKey);
      await supplierConsolidationService.createConsolidation({
        title: `Consolidate ${opportunity.itemDescription}`,
        description: `Generated from ${opportunity.itemCount} approved procurement plan item(s) across ${opportunity.departmentCount} department(s).`,
        itemCategory: opportunity.itemCategory,
        analysisPeriodStart: new Date(new Date().getFullYear(), 0, 1).toISOString(),
        analysisPeriodEnd: new Date(new Date().getFullYear(), 11, 31).toISOString(),
        currentSupplierCount: Math.max(opportunity.departmentCount, 1),
        recommendedSupplierCount: opportunity.departmentCount > 2 ? 2 : 1,
        totalSpend: opportunity.estimatedTotalCost,
        potentialSavings: opportunity.potentialSavings,
        currency: opportunity.currency,
        opportunityLevel: opportunity.opportunityLevel,
        recommendedStrategy: opportunity.recommendedStrategy,
        strategyRationale: `Estimated savings from consolidated sourcing: ${formatCurrency(opportunity.potentialSavings, opportunity.currency)}.`,
        implementationPlan: opportunity.items.map(item => `${item.planNumber} - ${item.departmentName || 'Department'} - ${item.itemDescription}`).join('\n'),
      });
      toast.success('Consolidation strategy created from opportunity');
      await loadConsolidations();
    } catch (error) {
      console.error('Error creating consolidation from opportunity:', error);
      toast.error('Failed to create consolidation from opportunity');
    } finally {
      setCreatingOpportunityKey(null);
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this consolidation?')) return;
    try {
      await supplierConsolidationService.deleteConsolidation(id);
      toast.success('Consolidation deleted successfully');
      loadConsolidations();
    } catch (error) {
      console.error('Error deleting consolidation:', error);
      toast.error('Failed to delete consolidation');
    }
  };

  const handleExportToExcel = () => {
    try {
      if (consolidations.length === 0) { toast.error('No data to export'); return; }
      const exportData = consolidations.map(c => ({
        'Consolidation #': c.consolidationCode, 'Title': c.title, 'Category': c.itemCategory,
        'Status': c.status, 'Strategy': c.recommendedStrategy, 'Current Suppliers': c.currentSupplierCount,
        'Target Suppliers': c.recommendedSupplierCount, 'Current Spend': c.totalSpend,
        'Projected Savings': c.potentialSavings, 'Actual Savings': c.actualSavings, 'Currency': c.currency,
      }));
      const ws = XLSX.utils.json_to_sheet(exportData);
      const wb = XLSX.utils.book_new();
      XLSX.utils.book_append_sheet(wb, ws, 'Consolidations');
      const excelBuffer = XLSX.write(wb, { bookType: 'xlsx', type: 'array' });
      const data = new Blob([excelBuffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
      saveAs(data, `supplier_consolidations_${format(new Date(), 'yyyyMMdd')}.xlsx`);
      toast.success('Consolidations exported successfully');
    } catch (error) { console.error('Error exporting:', error); toast.error('Failed to export'); }
  };

  const getStatusBadge = (status: string) => {
    const config: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', className: string }> = {
      'Draft': { variant: 'secondary', className: 'bg-gray-100 text-gray-800' },
      'UnderReview': { variant: 'default', className: 'bg-blue-100 text-blue-800' },
      'Approved': { variant: 'default', className: 'bg-green-100 text-green-800' },
      'Implementing': { variant: 'outline', className: 'bg-yellow-100 text-yellow-800' },
      'Implemented': { variant: 'default', className: 'bg-purple-100 text-purple-800' },
      'Completed': { variant: 'default', className: 'bg-purple-100 text-purple-800' },
      'Rejected': { variant: 'destructive', className: 'bg-red-100 text-red-800' },
    };
    const c = config[status] || { variant: 'outline' as const, className: '' };
    return <Badge variant={c.variant} className={c.className}>{status}</Badge>;
  };

  const formatCurrency = (amount: number, currency: string) => {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency: currency || 'USD', minimumFractionDigits: 0, maximumFractionDigits: 0 }).format(amount);
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Supplier Consolidation</h1>
          <p className="text-muted-foreground">Manage supplier consolidation strategies and opportunities</p>
        </div>
        <Button onClick={handleCreateNew} className="gap-2"><Plus className="h-4 w-4" />New Consolidation</Button>
      </div>

      <Card>
        <CardHeader><CardTitle className="flex items-center gap-2"><Filter className="h-5 w-5" />Filters</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="flex gap-2">
              <Input placeholder="Search consolidations..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleSearch()} className="flex-1" />
              <Button onClick={handleSearch} size="icon" variant="secondary"><Search className="h-4 w-4" /></Button>
            </div>
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger><SelectValue placeholder="All Statuses" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="UnderReview">Under Review</SelectItem>
                <SelectItem value="Approved">Approved</SelectItem>
                <SelectItem value="Implementing">Implementing</SelectItem>
                <SelectItem value="Implemented">Implemented</SelectItem>
                <SelectItem value="Completed">Completed</SelectItem>
              </SelectContent>
            </Select>
            <div className="flex gap-2">
              <Button onClick={loadConsolidations} variant="outline" className="gap-2 flex-1"><RefreshCw className="h-4 w-4" />Refresh</Button>
              <Button onClick={handleExportToExcel} variant="outline" className="gap-2 flex-1"><Download className="h-4 w-4" />Export</Button>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Plan Consolidation Opportunities</CardTitle>
          <CardDescription>Approved and active plan items grouped by need, category, specification, and supplier signals.</CardDescription>
        </CardHeader>
        <CardContent>
          {opportunities.length === 0 ? (
            <div className="py-6 text-center text-gray-500">No consolidation opportunities found from current approved or active plans.</div>
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Need</TableHead>
                    <TableHead>Departments</TableHead>
                    <TableHead>Plans</TableHead>
                    <TableHead>Quantity</TableHead>
                    <TableHead>Spend</TableHead>
                    <TableHead>Savings</TableHead>
                    <TableHead>Level</TableHead>
                    <TableHead className="w-[130px]">Action</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {opportunities.slice(0, 10).map((opportunity) => (
                    <TableRow key={opportunity.opportunityKey}>
                      <TableCell>
                        <div className="font-medium">{opportunity.itemDescription}</div>
                        <div className="text-xs text-gray-500">{opportunity.itemCategory}</div>
                      </TableCell>
                      <TableCell>{opportunity.departmentCount}</TableCell>
                      <TableCell>{opportunity.planCount}</TableCell>
                      <TableCell>{opportunity.totalQuantity} {opportunity.unitOfMeasure}</TableCell>
                      <TableCell>{formatCurrency(opportunity.estimatedTotalCost, opportunity.currency)}</TableCell>
                      <TableCell className="font-medium text-green-600">{formatCurrency(opportunity.potentialSavings, opportunity.currency)}</TableCell>
                      <TableCell>{getStatusBadge(opportunity.opportunityLevel)}</TableCell>
                      <TableCell>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => handleCreateFromOpportunity(opportunity)}
                          disabled={creatingOpportunityKey === opportunity.opportunityKey}
                        >
                          {creatingOpportunityKey === opportunity.opportunityKey ? 'Creating...' : 'Create'}
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Consolidations</CardTitle><CardDescription>{loading ? 'Loading...' : `${consolidations.length} consolidation(s) found`}</CardDescription></CardHeader>
        <CardContent>
          {loading ? (<div className="text-center py-8">Loading consolidations...</div>) : consolidations.length === 0 ? (<div className="text-center py-8 text-gray-500">No consolidations found</div>) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader><TableRow><TableHead>Consolidation #</TableHead><TableHead>Title</TableHead><TableHead>Category</TableHead><TableHead>Status</TableHead><TableHead>Suppliers</TableHead><TableHead>Current Spend</TableHead><TableHead>Projected Savings</TableHead><TableHead>Actions</TableHead></TableRow></TableHeader>
                <TableBody>
                  {consolidations.map((c) => (
                    <TableRow key={c.id}>
                      <TableCell className="font-medium">{c.consolidationCode}</TableCell>
                      <TableCell><div><div className="font-medium">{c.title}</div><div className="text-sm text-gray-500">{c.recommendedStrategy}</div></div></TableCell>
                      <TableCell>{c.itemCategory}</TableCell>
                      <TableCell>{getStatusBadge(c.status)}</TableCell>
                      <TableCell><div className="flex items-center gap-2"><Users className="h-4 w-4" /><span>{c.currentSupplierCount} → {c.recommendedSupplierCount}</span></div></TableCell>
                      <TableCell>{formatCurrency(c.totalSpend, c.currency)}</TableCell>
                      <TableCell className="text-green-600 font-medium">{formatCurrency(c.potentialSavings, c.currency)}</TableCell>
                      <TableCell>
                        <div className="flex gap-2">
                          <Button variant="ghost" size="sm" onClick={() => handleViewDetails(c.id)} title="View"><Eye className="h-4 w-4" /></Button>
                          {c.status === 'Draft' && (<><Button variant="ghost" size="sm" onClick={() => handleEdit(c.id)} title="Edit"><Edit className="h-4 w-4" /></Button><Button variant="ghost" size="sm" onClick={() => handleDelete(c.id)} title="Delete"><Trash2 className="h-4 w-4" /></Button></>)}
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
