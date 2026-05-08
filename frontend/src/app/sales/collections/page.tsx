'use client';

import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Phone, Search, RefreshCw, Plus, AlertTriangle, Clock } from 'lucide-react';
import { toast } from 'sonner';
import { collectionService, type CollectionActivitySummaryDto } from '@/services/collectionService';
import { format } from 'date-fns';

const STATUS_CONFIG: Record<string, { className: string }> = {
  Pending: { className: 'bg-blue-100 text-blue-800' },
  InProgress: { className: 'bg-yellow-100 text-yellow-800' },
  PromiseToPay: { className: 'bg-cyan-100 text-cyan-800' },
  Resolved: { className: 'bg-green-100 text-green-800' },
  Escalated: { className: 'bg-red-100 text-red-800' },
  WrittenOff: { className: 'bg-gray-100 text-gray-800' },
};

export default function CollectionActivitiesPage() {
  const [activities, setActivities] = useState<CollectionActivitySummaryDto[]>([]);
  const [overdueCount, setOverdueCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  useEffect(() => { loadActivities(); loadOverdueCount(); }, [page, statusFilter]);

  const loadActivities = async () => {
    try {
      setLoading(true);
      const data = await collectionService.getActivities(page, pageSize, searchTerm || undefined, statusFilter || undefined);
      setActivities(data.items); setTotalCount(data.totalCount);
    } catch (error) { console.error(error); toast.error('Failed to load activities'); }
    finally { setLoading(false); }
  };

  const loadOverdueCount = async () => {
    try {
      const overdue = await collectionService.getOverdueFollowUps();
      setOverdueCount(overdue.length);
    } catch { /* ignore */ }
  };

  const handleSearch = () => { setPage(1); loadActivities(); };
  const formatDate = (d?: string) => { if (!d) return '-'; try { return format(new Date(d), 'dd MMM yyyy'); } catch { return d; } };
  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2"><Phone className="h-8 w-8 text-violet-600" />Collections</h1>
          <p className="text-gray-500">Track collection activities and follow-ups</p>
        </div>
        <Button className="bg-violet-600 hover:bg-violet-700"><Plus className="h-4 w-4 mr-2" />Log Activity</Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Activities</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold">{totalCount}</p></CardContent></Card>
        <Card className="border-red-200"><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-red-500 flex items-center gap-1"><AlertTriangle className="h-4 w-4" />Overdue Follow-ups</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-red-600">{overdueCount}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Outstanding</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-amber-600">${activities.reduce((s, a) => s + a.outstandingAmount, 0).toLocaleString()}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Promises</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-cyan-600">${activities.reduce((s, a) => s + a.promisedAmount, 0).toLocaleString()}</p></CardContent></Card>
      </div>

      <Card>
        <CardHeader><CardTitle>Filter</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="flex gap-2">
              <Input placeholder="Search..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleSearch()} />
              <Button onClick={handleSearch}><Search className="h-4 w-4" /></Button>
            </div>
            <Select value={statusFilter || 'all'} onValueChange={(v) => setStatusFilter(v === 'all' ? '' : v)}>
              <SelectTrigger><SelectValue placeholder="All Statuses" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All</SelectItem>
                <SelectItem value="Pending">Pending</SelectItem>
                <SelectItem value="InProgress">In Progress</SelectItem>
                <SelectItem value="PromiseToPay">Promise to Pay</SelectItem>
                <SelectItem value="Resolved">Resolved</SelectItem>
                <SelectItem value="Escalated">Escalated</SelectItem>
              </SelectContent>
            </Select>
            <Button variant="outline" onClick={loadActivities}><RefreshCw className="h-4 w-4 mr-2" />Refresh</Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Collection Activities</CardTitle><CardDescription>Showing {activities.length} of {totalCount}</CardDescription></CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8"><Phone className="h-12 w-12 animate-pulse mx-auto mb-4 text-violet-500" /><p className="text-gray-500">Loading...</p></div>
          ) : activities.length === 0 ? (
            <div className="text-center py-8"><Phone className="h-12 w-12 mx-auto mb-4 text-gray-400" /><p className="text-gray-500">No activities found</p></div>
          ) : (
            <>
              <Table>
                <TableHeader><TableRow>
                  <TableHead>Subject</TableHead><TableHead>Type</TableHead><TableHead>Customer</TableHead>
                  <TableHead>Outstanding</TableHead><TableHead>Promised</TableHead><TableHead>Follow-up</TableHead><TableHead>Status</TableHead>
                </TableRow></TableHeader>
                <TableBody>
                  {activities.map((a) => (
                    <TableRow key={a.id} className="cursor-pointer hover:bg-gray-50">
                      <TableCell className="font-medium">{a.subject}</TableCell>
                      <TableCell><Badge variant="outline">{a.activityType}</Badge></TableCell>
                      <TableCell>{a.customerName || '-'}</TableCell>
                      <TableCell className="font-semibold text-amber-600">${a.outstandingAmount.toLocaleString()}</TableCell>
                      <TableCell className="text-cyan-600">{a.promisedAmount ? `$${a.promisedAmount.toLocaleString()}` : '-'}</TableCell>
                      <TableCell>
                        {a.followUpDate && (
                          <div className="flex items-center gap-1">
                            <Clock className="h-3 w-3 text-gray-500" />
                            <span className={new Date(a.followUpDate) < new Date() ? 'text-red-600 font-medium' : ''}>{formatDate(a.followUpDate)}</span>
                          </div>
                        )}
                      </TableCell>
                      <TableCell><Badge className={STATUS_CONFIG[a.collectionStatus]?.className || ''}>{a.collectionStatus.replace(/([A-Z])/g, ' $1').trim()}</Badge></TableCell>
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
