'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Search, Eye, Edit, Plus, Download, RefreshCw, Filter, Trash2, TrendingUp, TrendingDown, Minus } from 'lucide-react';
import { toast } from 'sonner';
import { marketAnalysisService, type MarketAnalysisDto } from '@/services/procurementPlanningService';
import { format } from 'date-fns';
import * as XLSX from 'xlsx';
import { saveAs } from 'file-saver';

export default function MarketAnalysisPage() {
  const router = useRouter();
  const [analyses, setAnalyses] = useState<MarketAnalysisDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [deleteTarget, setDeleteTarget] = useState<MarketAnalysisDto>();
  const [deleting, setDeleting] = useState(false);

  useEffect(() => { loadAnalyses(); }, [page, statusFilter]);

  const loadAnalyses = async () => {
    try {
      setLoading(true);
      const result = await marketAnalysisService.getAnalyses({
        page, pageSize: 25,
        search: searchTerm || undefined,
        status: statusFilter !== 'all' ? statusFilter : undefined,
      });
      setAnalyses(result.items);
      setTotalPages(result.totalPages);
    } catch (error) {
      console.error('Error loading analyses:', error);
      toast.error('Failed to load market analyses', {
        description: error instanceof Error ? error.message : undefined,
      });
    } finally { setLoading(false); }
  };

  const handleSearch = () => { setPage(1); loadAnalyses(); };
  const handleViewDetails = (id: string) => router.push(`/procurement/planning/market-analysis/${id}`);
  const handleEdit = (id: string) => router.push(`/procurement/planning/market-analysis/${id}/edit`);
  const handleCreateNew = () => router.push('/procurement/planning/market-analysis/new');
  const getAnalysisDate = (analysis: MarketAnalysisDto) => analysis.analysisDate || analysis.preparedDate || analysis.createdAt;

  const handleDelete = async () => {
    if (!deleteTarget) return false;
    try {
      setDeleting(true);
      await marketAnalysisService.deleteAnalysis(deleteTarget.id);
      toast.success('Analysis deleted successfully');
      setDeleteTarget(undefined);
      await loadAnalyses();
      return true;
    } catch (error) {
      console.error('Error deleting analysis:', error);
      toast.error('Failed to delete analysis', {
        description: error instanceof Error ? error.message : undefined,
      });
      return false;
    } finally {
      setDeleting(false);
    }
  };

  const handleExportToExcel = () => {
    try {
      if (analyses.length === 0) { toast.error('No data to export'); return; }
      const exportData = analyses.map(a => ({
        'Analysis #': a.analysisCode, 'Title': a.title, 'Item': a.itemDescription, 'Category': a.itemCategory,
        'Status': a.status, 'Current Price': a.currentMarketPrice, 'Avg Price': a.averagePrice ?? a.historicalAveragePrice,
        'Min Price': a.minimumPrice ?? a.currentMarketPrice, 'Max Price': a.maximumPrice ?? a.forecastedPrice, 'Trend': a.priceTrend,
        'Variance %': a.priceVariancePercent ?? a.priceChangePercent, 'Forecast': a.forecastedPrice, 'Currency': a.currency,
        'Analysis Date': getAnalysisDate(a) ? format(new Date(getAnalysisDate(a) || ''), 'yyyy-MM-dd') : '',
      }));
      const ws = XLSX.utils.json_to_sheet(exportData);
      const wb = XLSX.utils.book_new();
      XLSX.utils.book_append_sheet(wb, ws, 'Market Analysis');
      const excelBuffer = XLSX.write(wb, { bookType: 'xlsx', type: 'array' });
      const data = new Blob([excelBuffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
      saveAs(data, `market_analysis_${format(new Date(), 'yyyyMMdd')}.xlsx`);
      toast.success('Analysis exported successfully');
    } catch (error) { console.error('Error exporting:', error); toast.error('Failed to export'); }
  };

  const getStatusBadge = (status: string) => {
    const config: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', className: string }> = {
      'Draft': { variant: 'secondary', className: 'bg-gray-100 text-gray-800' },
      'Published': { variant: 'default', className: 'bg-green-100 text-green-800' },
      'Archived': { variant: 'outline', className: 'bg-gray-100 text-gray-800' },
    };
    const c = config[status] || { variant: 'outline' as const, className: '' };
    return <Badge variant={c.variant} className={c.className}>{status}</Badge>;
  };

  const getTrendIcon = (trend: string) => {
    if (trend === 'Increasing') return <TrendingUp className="h-4 w-4 text-red-500" />;
    if (trend === 'Decreasing') return <TrendingDown className="h-4 w-4 text-green-500" />;
    return <Minus className="h-4 w-4 text-gray-500" />;
  };

  const formatCurrency = (amount: number, currency: string) => {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency: currency || 'USD', minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(amount);
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Market Analysis</h1>
          <p className="text-muted-foreground">Analyze market prices and forecast trends</p>
        </div>
        <Button onClick={handleCreateNew} className="gap-2"><Plus className="h-4 w-4" />New Analysis</Button>
      </div>

      <Card>
        <CardHeader><CardTitle className="flex items-center gap-2"><Filter className="h-5 w-5" />Filters</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="flex gap-2">
              <Input placeholder="Search analyses..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleSearch()} className="flex-1" />
              <Button onClick={handleSearch} size="icon" variant="secondary"><Search className="h-4 w-4" /></Button>
            </div>
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger><SelectValue placeholder="All Statuses" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="Published">Published</SelectItem>
                <SelectItem value="Archived">Archived</SelectItem>
              </SelectContent>
            </Select>
            <div className="flex gap-2">
              <Button onClick={loadAnalyses} variant="outline" className="gap-2 flex-1"><RefreshCw className="h-4 w-4" />Refresh</Button>
              <Button onClick={handleExportToExcel} variant="outline" className="gap-2 flex-1"><Download className="h-4 w-4" />Export</Button>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Market Analyses</CardTitle><CardDescription>{loading ? 'Loading...' : `${analyses.length} analysis(es) found`}</CardDescription></CardHeader>
        <CardContent>
          {loading ? (<div className="text-center py-8">Loading analyses...</div>) : analyses.length === 0 ? (<div className="text-center py-8 text-gray-500">No analyses found</div>) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader><TableRow><TableHead>Analysis #</TableHead><TableHead>Item</TableHead><TableHead>Category</TableHead><TableHead>Status</TableHead><TableHead>Current Price</TableHead><TableHead>Variance</TableHead><TableHead>Trend</TableHead><TableHead>Forecast</TableHead><TableHead>Actions</TableHead></TableRow></TableHeader>
                <TableBody>
                  {analyses.map((a) => (
                    <TableRow key={a.id}>
                      <TableCell className="font-medium">{a.analysisCode}</TableCell>
                      <TableCell><div><div className="font-medium">{a.title}</div><div className="text-sm text-gray-500">{a.itemDescription?.substring(0, 50) || 'No item description'}</div></div></TableCell>
                      <TableCell>{a.itemCategory}</TableCell>
                      <TableCell>{getStatusBadge(a.status)}</TableCell>
                      <TableCell>{formatCurrency(a.currentMarketPrice, a.currency)}</TableCell>
                      <TableCell>{((a.priceVariancePercent ?? a.priceChangePercent) || 0).toFixed(1)}%</TableCell>
                      <TableCell><div className="flex items-center gap-2">{getTrendIcon(a.priceTrend)}<span>{a.priceTrend}</span></div></TableCell>
                      <TableCell>{formatCurrency(a.forecastedPrice, a.currency)}</TableCell>
                      <TableCell>
                        <div className="flex gap-2">
                          <Button variant="ghost" size="sm" onClick={() => handleViewDetails(a.id)} title="View"><Eye className="h-4 w-4" /></Button>
                          {a.status === 'Draft' && (<><Button variant="ghost" size="sm" onClick={() => handleEdit(a.id)} title="Edit"><Edit className="h-4 w-4" /></Button><Button variant="ghost" size="sm" onClick={() => setDeleteTarget(a)} title="Delete"><Trash2 className="h-4 w-4" /></Button></>)}
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

      <ConfirmationDialog
        open={Boolean(deleteTarget)}
        onOpenChange={(open) => { if (!open && !deleting) setDeleteTarget(undefined); }}
        title="Delete Market Analysis?"
        description={deleteTarget ? `Delete ${deleteTarget.analysisCode} - ${deleteTarget.title}? This action cannot be undone.` : ''}
        confirmText={deleting ? 'Deleting...' : 'Delete'}
        cancelText="Cancel"
        onConfirm={handleDelete}
        isLoading={deleting}
        variant="destructive"
      />
    </div>
  );
}
