'use client';

import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Megaphone, Search, RefreshCw, Plus, Rocket, CheckCircle, DollarSign, Users } from 'lucide-react';
import { toast } from 'sonner';
import { campaignService, type CampaignSummaryDto } from '@/services/campaignService';
import { format } from 'date-fns';

const STATUS_CONFIG: Record<string, { className: string }> = {
  Draft: { className: 'bg-gray-100 text-gray-800' },
  Active: { className: 'bg-green-100 text-green-800' },
  Paused: { className: 'bg-yellow-100 text-yellow-800' },
  Completed: { className: 'bg-blue-100 text-blue-800' },
  Cancelled: { className: 'bg-red-100 text-red-800' },
};

export default function CampaignsPage() {
  const [campaigns, setCampaigns] = useState<CampaignSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  useEffect(() => { loadCampaigns(); }, [page, statusFilter]);

  const loadCampaigns = async () => {
    try {
      setLoading(true);
      const data = await campaignService.getCampaigns(page, pageSize, searchTerm || undefined, statusFilter || undefined);
      setCampaigns(data.items); setTotalCount(data.totalCount);
    } catch (error) { console.error(error); toast.error('Failed to load campaigns'); }
    finally { setLoading(false); }
  };

  const handleSearch = () => { setPage(1); loadCampaigns(); };
  const handleLaunch = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await campaignService.launchCampaign(id); toast.success('Campaign launched!'); loadCampaigns(); } catch (err: any) { toast.error(err.message); }
  };
  const handleComplete = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await campaignService.completeCampaign(id); toast.success('Campaign completed'); loadCampaigns(); } catch (err: any) { toast.error(err.message); }
  };

  const formatDate = (d?: string) => { if (!d) return '-'; try { return format(new Date(d), 'dd MMM yyyy'); } catch { return d; } };
  const totalPages = Math.ceil(totalCount / pageSize);
  const totalBudget = campaigns.reduce((s, c) => s + c.budget, 0);
  const totalSpent = campaigns.reduce((s, c) => s + c.actualCost, 0);

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2"><Megaphone className="h-8 w-8 text-pink-600" />Campaigns</h1>
          <p className="text-gray-500">Marketing campaign management</p>
        </div>
        <Button className="bg-pink-600 hover:bg-pink-700"><Plus className="h-4 w-4 mr-2" />New Campaign</Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold">{totalCount}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Budget</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-blue-600"><DollarSign className="h-5 w-5 inline" />{totalBudget.toLocaleString()}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Spent</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-amber-600"><DollarSign className="h-5 w-5 inline" />{totalSpent.toLocaleString()}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Leads Generated</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-green-600"><Users className="h-5 w-5 inline" />{campaigns.reduce((s, c) => s + c.leadsGenerated, 0)}</p></CardContent></Card>
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
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="Active">Active</SelectItem>
                <SelectItem value="Completed">Completed</SelectItem>
                <SelectItem value="Cancelled">Cancelled</SelectItem>
              </SelectContent>
            </Select>
            <Button variant="outline" onClick={loadCampaigns}><RefreshCw className="h-4 w-4 mr-2" />Refresh</Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Campaigns</CardTitle><CardDescription>Showing {campaigns.length} of {totalCount}</CardDescription></CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8"><Megaphone className="h-12 w-12 animate-pulse mx-auto mb-4 text-pink-500" /><p className="text-gray-500">Loading...</p></div>
          ) : campaigns.length === 0 ? (
            <div className="text-center py-8"><Megaphone className="h-12 w-12 mx-auto mb-4 text-gray-400" /><p className="text-gray-500">No campaigns found</p></div>
          ) : (
            <>
              <Table>
                <TableHeader><TableRow>
                  <TableHead>Name</TableHead><TableHead>Type</TableHead><TableHead>Budget</TableHead>
                  <TableHead>Spent</TableHead><TableHead>Leads</TableHead><TableHead>Response</TableHead>
                  <TableHead>Dates</TableHead><TableHead>Status</TableHead><TableHead>Actions</TableHead>
                </TableRow></TableHeader>
                <TableBody>
                  {campaigns.map((c) => (
                    <TableRow key={c.id} className="cursor-pointer hover:bg-gray-50">
                      <TableCell className="font-medium">{c.name}</TableCell>
                      <TableCell><Badge variant="outline">{c.campaignType}</Badge></TableCell>
                      <TableCell>${c.budget.toLocaleString()}</TableCell>
                      <TableCell className={c.actualCost > c.budget ? 'text-red-600 font-semibold' : ''}>${c.actualCost.toLocaleString()}</TableCell>
                      <TableCell className="font-semibold">{c.leadsGenerated}</TableCell>
                      <TableCell>{c.responseRate.toFixed(1)}%</TableCell>
                      <TableCell className="text-sm">{formatDate(c.startDate)}{c.endDate ? ` – ${formatDate(c.endDate)}` : ''}</TableCell>
                      <TableCell><Badge className={STATUS_CONFIG[c.campaignStatus]?.className || ''}>{c.campaignStatus}</Badge></TableCell>
                      <TableCell>
                        <div className="flex gap-1">
                          {c.campaignStatus === 'Draft' && <Button variant="outline" size="sm" onClick={(e) => handleLaunch(c.id, e)}><Rocket className="h-3 w-3 mr-1" />Launch</Button>}
                          {c.campaignStatus === 'Active' && <Button variant="outline" size="sm" onClick={(e) => handleComplete(c.id, e)}><CheckCircle className="h-3 w-3 mr-1" />Complete</Button>}
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
