'use client';

import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Textarea } from '@/components/ui/textarea';
import { Target, Search, RefreshCw, Plus, TrendingUp, DollarSign, CheckCircle, XCircle } from 'lucide-react';
import { toast } from 'sonner';
import { crmService, type OpportunitySummaryDto, type CreateOpportunityDto } from '@/services/crmService';
import { format } from 'date-fns';

const STAGE_CONFIG: Record<string, { className: string }> = {
  Prospecting: { className: 'bg-blue-100 text-blue-800' },
  Qualification: { className: 'bg-cyan-100 text-cyan-800' },
  Proposal: { className: 'bg-yellow-100 text-yellow-800' },
  Negotiation: { className: 'bg-orange-100 text-orange-800' },
  ClosedWon: { className: 'bg-green-100 text-green-800' },
  ClosedLost: { className: 'bg-red-100 text-red-800' },
};

// Backend requires name, amount, expectedCloseDate
const EMPTY_OPP: CreateOpportunityDto = {
  name: '',
  amount: 0,
  expectedCloseDate: new Date(Date.now() + 30 * 86400000).toISOString().split('T')[0], // default 30 days out
};

export default function OpportunitiesPage() {
  const [opportunities, setOpportunities] = useState<OpportunitySummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [stageFilter, setStageFilter] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;
  const [showCreate, setShowCreate] = useState(false);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<CreateOpportunityDto>({ ...EMPTY_OPP });

  useEffect(() => { loadOpportunities(); }, [page, stageFilter]);

  const loadOpportunities = async () => {
    try {
      setLoading(true);
      const data = await crmService.getOpportunities(page, pageSize, searchTerm || undefined, stageFilter || undefined);
      setOpportunities(data.items);
      setTotalCount(data.totalCount);
    } catch (error) { console.error(error); toast.error('Failed to load opportunities'); }
    finally { setLoading(false); }
  };

  const handleSearch = () => { setPage(1); loadOpportunities(); };
  const handleAdvance = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await crmService.advanceStage(id); toast.success('Stage advanced'); loadOpportunities(); } catch (error: any) { toast.error(error.message); }
  };
  const handleWin = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await crmService.winOpportunity(id); toast.success('Opportunity won!'); loadOpportunities(); } catch (error: any) { toast.error(error.message); }
  };
  const handleLose = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await crmService.loseOpportunity(id); toast.success('Marked as lost'); loadOpportunities(); } catch (error: any) { toast.error(error.message); }
  };

  const handleCreate = async () => {
    if (!form.name.trim()) { toast.error('Opportunity name is required'); return; }
    try {
      setCreating(true);
      await crmService.createOpportunity(form);
      toast.success('Opportunity created successfully');
      setShowCreate(false);
      setForm({ ...EMPTY_OPP });
      loadOpportunities();
    } catch (error: any) { toast.error(error.message || 'Failed to create opportunity'); }
    finally { setCreating(false); }
  };

  const formatDate = (d?: string) => { if (!d) return '-'; try { return format(new Date(d), 'dd MMM yyyy'); } catch { return d; } };
  const totalPages = Math.ceil(totalCount / pageSize);
  // amount is the correct backend field (not estimatedValue)
  const pipelineValue = opportunities.filter(o => !['ClosedWon', 'ClosedLost'].includes(o.stage)).reduce((s, o) => s + o.amount, 0);
  const weightedValue = opportunities.filter(o => !['ClosedWon', 'ClosedLost'].includes(o.stage)).reduce((s, o) => s + (o.amount * o.probability / 100), 0);

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2"><Target className="h-8 w-8 text-purple-600" />Opportunities</h1>
          <p className="text-gray-500">Track and manage sales opportunities</p>
        </div>
        <Button className="bg-purple-600 hover:bg-purple-700" onClick={() => setShowCreate(true)}>
          <Plus className="h-4 w-4 mr-2" />New Opportunity
        </Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold">{totalCount}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Pipeline Value</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-blue-600"><DollarSign className="h-5 w-5 inline" />{pipelineValue.toLocaleString()}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Weighted Value</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-green-600"><DollarSign className="h-5 w-5 inline" />{weightedValue.toLocaleString()}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Won</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-emerald-600">{opportunities.filter(o => o.stage === 'ClosedWon').length}</p></CardContent></Card>
      </div>

      <Card>
        <CardHeader><CardTitle>Filter</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="flex gap-2">
              <Input placeholder="Search..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleSearch()} />
              <Button onClick={handleSearch}><Search className="h-4 w-4" /></Button>
            </div>
            <Select value={stageFilter || 'all'} onValueChange={(v) => setStageFilter(v === 'all' ? '' : v)}>
              <SelectTrigger><SelectValue placeholder="All Stages" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Stages</SelectItem>
                <SelectItem value="Prospecting">Prospecting</SelectItem><SelectItem value="Qualification">Qualification</SelectItem>
                <SelectItem value="Proposal">Proposal</SelectItem><SelectItem value="Negotiation">Negotiation</SelectItem>
                <SelectItem value="ClosedWon">Closed Won</SelectItem><SelectItem value="ClosedLost">Closed Lost</SelectItem>
              </SelectContent>
            </Select>
            <Button variant="outline" onClick={loadOpportunities}><RefreshCw className="h-4 w-4 mr-2" />Refresh</Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Opportunities</CardTitle><CardDescription>Showing {opportunities.length} of {totalCount}</CardDescription></CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8"><Target className="h-12 w-12 animate-pulse mx-auto mb-4 text-purple-500" /><p className="text-gray-500">Loading...</p></div>
          ) : opportunities.length === 0 ? (
            <div className="text-center py-8"><Target className="h-12 w-12 mx-auto mb-4 text-gray-400" /><p className="text-gray-500">No opportunities found</p></div>
          ) : (
            <>
              <Table>
                <TableHeader><TableRow>
                  <TableHead>Name</TableHead><TableHead>Customer</TableHead><TableHead>Stage</TableHead>
                  <TableHead>Probability</TableHead><TableHead>Amount</TableHead><TableHead>Close Date</TableHead><TableHead>Actions</TableHead>
                </TableRow></TableHeader>
                <TableBody>
                  {opportunities.map((opp) => (
                    <TableRow key={opp.id} className="cursor-pointer hover:bg-gray-50">
                      <TableCell className="font-medium">{opp.name}</TableCell>
                      <TableCell>{opp.customerName || '-'}</TableCell>
                      <TableCell><Badge className={STAGE_CONFIG[opp.stage]?.className || ''}>{opp.stage.replace(/([A-Z])/g, ' $1').trim()}</Badge></TableCell>
                      <TableCell>
                        <div className="flex items-center gap-1">
                          <div className="w-12 bg-gray-200 rounded-full h-2"><div className="bg-purple-500 h-2 rounded-full" style={{ width: `${opp.probability}%` }} /></div>
                          <span className="text-xs">{opp.probability}%</span>
                        </div>
                      </TableCell>
                      <TableCell className="font-semibold">${opp.amount.toLocaleString()}</TableCell>
                      <TableCell>{formatDate(opp.expectedCloseDate)}</TableCell>
                      <TableCell>
                        <div className="flex gap-1">
                          {!['ClosedWon', 'ClosedLost'].includes(opp.stage) && (
                            <>
                              <Button variant="outline" size="sm" onClick={(e) => handleAdvance(opp.id, e)}><TrendingUp className="h-3 w-3" /></Button>
                              <Button variant="outline" size="sm" className="text-green-600" onClick={(e) => handleWin(opp.id, e)}><CheckCircle className="h-3 w-3" /></Button>
                              <Button variant="outline" size="sm" className="text-red-600" onClick={(e) => handleLose(opp.id, e)}><XCircle className="h-3 w-3" /></Button>
                            </>
                          )}
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

      {/* Create Opportunity Dialog — fields match backend CreateOpportunityDto */}
      <Dialog open={showCreate} onOpenChange={setShowCreate}>
        <DialogContent className="sm:max-w-[550px]">
          <DialogHeader>
            <DialogTitle>Create New Opportunity</DialogTitle>
            <DialogDescription>Enter opportunity details to add to your pipeline.</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="space-y-2"><Label>Opportunity Name *</Label>
              <Input value={form.name} onChange={(e) => setForm(f => ({ ...f, name: e.target.value }))} placeholder="Enterprise deal — Acme Corp" /></div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>Stage</Label>
                <Select value={form.stage || 'Prospecting'} onValueChange={(v) => setForm(f => ({ ...f, stage: v }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Prospecting">Prospecting</SelectItem><SelectItem value="Qualification">Qualification</SelectItem>
                    <SelectItem value="Proposal">Proposal</SelectItem><SelectItem value="Negotiation">Negotiation</SelectItem>
                  </SelectContent>
                </Select></div>
              <div className="space-y-2"><Label>Probability (%)</Label>
                <Input type="number" min={0} max={100} value={form.probability ?? 10} onChange={(e) => setForm(f => ({ ...f, probability: Number(e.target.value) || 0 }))} placeholder="50" /></div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>Amount *</Label>
                <Input type="number" value={form.amount || ''} onChange={(e) => setForm(f => ({ ...f, amount: Number(e.target.value) || 0 }))} placeholder="50000" /></div>
              <div className="space-y-2"><Label>Expected Close Date *</Label>
                <Input type="date" value={form.expectedCloseDate ? form.expectedCloseDate.split('T')[0] : ''} onChange={(e) => setForm(f => ({ ...f, expectedCloseDate: e.target.value }))} /></div>
            </div>
            <div className="space-y-2"><Label>Description</Label>
              <Textarea rows={3} value={form.description || ''} onChange={(e) => setForm(f => ({ ...f, description: e.target.value }))} placeholder="Deal details and notes..." /></div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setShowCreate(false); setForm({ ...EMPTY_OPP }); }}>Cancel</Button>
            <Button className="bg-purple-600 hover:bg-purple-700" onClick={handleCreate} disabled={creating || !form.name.trim()}>
              {creating ? 'Creating...' : 'Create Opportunity'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
