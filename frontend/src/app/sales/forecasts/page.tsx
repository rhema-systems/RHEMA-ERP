'use client';

import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { TrendingUp, Search, RefreshCw, Plus, ArrowUpRight, ArrowDownRight } from 'lucide-react';
import { toast } from 'sonner';
import { forecastService, type ForecastSummary } from '@/services/forecastService';
import { format } from 'date-fns';

const STATUS_CONFIG: Record<string, { className: string }> = {
  Draft: { className: 'bg-gray-100 text-gray-800' },
  Submitted: { className: 'bg-blue-100 text-blue-800' },
  Approved: { className: 'bg-green-100 text-green-800' },
  Locked: { className: 'bg-purple-100 text-purple-800' },
};

export default function ForecastsPage() {
  const [forecasts, setForecasts] = useState<ForecastSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  useEffect(() => { loadForecasts(); }, [page, statusFilter]);

  const loadForecasts = async () => {
    try {
      setLoading(true);
      const data = await forecastService.getForecasts(page, pageSize, searchTerm || undefined, statusFilter || undefined);
      setForecasts(data.data.items); setTotalCount(data.data.totalCount);
    } catch { toast.error('Failed to load forecasts'); }
    finally { setLoading(false); }
  };

  const handleSubmit = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await forecastService.submitForecast(id); toast.success('Forecast submitted'); loadForecasts(); } catch (err: any) { toast.error(err.message); }
  };
  const handleApprove = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await forecastService.approveForecast(id); toast.success('Forecast approved'); loadForecasts(); } catch (err: any) { toast.error(err.message); }
  };
  const handleRefreshActuals = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await forecastService.refreshActuals(id); toast.success('Actuals refreshed'); loadForecasts(); } catch (err: any) { toast.error(err.message); }
  };

  const formatDate = (d?: string) => { if (!d) return '-'; try { return format(new Date(d), 'dd MMM yyyy'); } catch { return d; } };
  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2"><TrendingUp className="h-8 w-8 text-blue-600" />Sales Forecasts</h1>
          <p className="text-gray-500">Revenue forecasting and variance analysis</p>
        </div>
        <Button className="bg-blue-600 hover:bg-blue-700"><Plus className="h-4 w-4 mr-2" />New Forecast</Button>
      </div>

      <Card>
        <CardHeader><CardTitle>Filter</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="flex gap-2">
              <Input placeholder="Search..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && loadForecasts()} />
              <Button onClick={loadForecasts}><Search className="h-4 w-4" /></Button>
            </div>
            <Select value={statusFilter || 'all'} onValueChange={(v) => setStatusFilter(v === 'all' ? '' : v)}>
              <SelectTrigger><SelectValue placeholder="All Statuses" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All</SelectItem>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="Submitted">Submitted</SelectItem>
                <SelectItem value="Approved">Approved</SelectItem>
                <SelectItem value="Locked">Locked</SelectItem>
              </SelectContent>
            </Select>
            <Button variant="outline" onClick={loadForecasts}><RefreshCw className="h-4 w-4 mr-2" />Refresh</Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Forecasts</CardTitle><CardDescription>Showing {forecasts.length} of {totalCount}</CardDescription></CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8"><TrendingUp className="h-12 w-12 animate-pulse mx-auto mb-4 text-blue-500" /><p className="text-gray-500">Loading...</p></div>
          ) : forecasts.length === 0 ? (
            <div className="text-center py-8"><TrendingUp className="h-12 w-12 mx-auto mb-4 text-gray-400" /><p className="text-gray-500">No forecasts found</p></div>
          ) : (
            <>
              <Table>
                <TableHeader><TableRow>
                  <TableHead>Name</TableHead><TableHead>Method</TableHead><TableHead>Period</TableHead>
                  <TableHead>Forecast</TableHead><TableHead>Actual</TableHead><TableHead>Variance</TableHead>
                  <TableHead>Status</TableHead><TableHead>Actions</TableHead>
                </TableRow></TableHeader>
                <TableBody>
                  {forecasts.map((f) => (
                    <TableRow key={f.id} className="cursor-pointer hover:bg-gray-50">
                      <TableCell className="font-medium">{f.name}</TableCell>
                      <TableCell><Badge variant="outline">{f.method}</Badge></TableCell>
                      <TableCell className="text-sm">{formatDate(f.periodStart)} – {formatDate(f.periodEnd)}</TableCell>
                      <TableCell>${f.totalForecastAmount.toLocaleString()}</TableCell>
                      <TableCell>${f.totalActualAmount.toLocaleString()}</TableCell>
                      <TableCell>
                        <span className={`flex items-center gap-1 font-semibold ${f.variance >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                          {f.variance >= 0 ? <ArrowUpRight className="h-4 w-4" /> : <ArrowDownRight className="h-4 w-4" />}
                          {f.variancePercentage}%
                        </span>
                      </TableCell>
                      <TableCell><Badge className={STATUS_CONFIG[f.status]?.className || ''}>{f.status}</Badge></TableCell>
                      <TableCell>
                        <div className="flex gap-1">
                          {f.status === 'Draft' && <Button variant="outline" size="sm" onClick={(e) => handleSubmit(f.id, e)}>Submit</Button>}
                          {f.status === 'Submitted' && <Button variant="outline" size="sm" onClick={(e) => handleApprove(f.id, e)}>Approve</Button>}
                          <Button variant="outline" size="sm" onClick={(e) => handleRefreshActuals(f.id, e)}><RefreshCw className="h-3 w-3" /></Button>
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
