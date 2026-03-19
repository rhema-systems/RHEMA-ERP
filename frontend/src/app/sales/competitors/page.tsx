'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Textarea } from '@/components/ui/textarea';
import { Swords, Search, RefreshCw, Plus, Trophy, XCircle, Clock } from 'lucide-react';
import { toast } from 'sonner';
import { competitorService, type CompetitorSummary, type CreateCompetitor } from '@/services/competitorService';
import { format } from 'date-fns';

const THREAT_CONFIG: Record<string, { className: string }> = {
  Low: { className: 'bg-green-100 text-green-800' },
  Medium: { className: 'bg-yellow-100 text-yellow-800' },
  High: { className: 'bg-orange-100 text-orange-800' },
  Critical: { className: 'bg-red-100 text-red-800' },
};

const EMPTY_COMPETITOR: CreateCompetitor = {
  name: '', estimatedMarketShare: 0, threatLevel: 'Medium',
};

export default function CompetitorsPage() {
  const router = useRouter();
  const [competitors, setCompetitors] = useState<CompetitorSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [threatFilter, setThreatFilter] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;
  const [showCreate, setShowCreate] = useState(false);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<CreateCompetitor>({ ...EMPTY_COMPETITOR });

  useEffect(() => { loadCompetitors(); }, [page, threatFilter]);

  const loadCompetitors = async () => {
    try {
      setLoading(true);
      // competitorService.getCompetitors returns { data: { items, totalCount, ... } }
      const result = await competitorService.getCompetitors(page, pageSize, searchTerm || undefined, threatFilter || undefined);
      setCompetitors(result.data.items);
      setTotalCount(result.data.totalCount);
    } catch (error) {
      console.error(error);
      toast.error('Failed to load competitors');
    }
    finally { setLoading(false); }
  };

  const handleSearch = () => { setPage(1); loadCompetitors(); };

  const handleCreate = async () => {
    if (!form.name.trim()) { toast.error('Competitor name is required'); return; }
    try {
      setCreating(true);
      await competitorService.createCompetitor(form);
      toast.success('Competitor added successfully');
      setShowCreate(false);
      setForm({ ...EMPTY_COMPETITOR });
      loadCompetitors();
    } catch (error: any) { toast.error(error.message || 'Failed to add competitor'); }
    finally { setCreating(false); }
  };

  const totalPages = Math.ceil(totalCount / pageSize);
  const totalDeals = competitors.reduce((s, c) => s + c.dealCount, 0);
  const totalWins = competitors.reduce((s, c) => s + c.wonDeals, 0);
  const totalLosses = competitors.reduce((s, c) => s + c.lostDeals, 0);

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2"><Swords className="h-8 w-8 text-red-600" />Competitor Intelligence</h1>
          <p className="text-gray-500">Track competitive landscape and deal outcomes</p>
        </div>
        <Button className="bg-red-600 hover:bg-red-700" onClick={() => setShowCreate(true)}>
          <Plus className="h-4 w-4 mr-2" />Add Competitor
        </Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Competitors</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold">{totalCount}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Tracked Deals</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-blue-600"><Clock className="h-5 w-5 inline" />{totalDeals}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Deals Won (vs them)</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-green-600"><Trophy className="h-5 w-5 inline" />{totalWins}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Deals Lost</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-red-600"><XCircle className="h-5 w-5 inline" />{totalLosses}</p></CardContent></Card>
      </div>

      <Card>
        <CardHeader><CardTitle>Filter</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="flex gap-2">
              <Input placeholder="Search..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleSearch()} />
              <Button onClick={handleSearch}><Search className="h-4 w-4" /></Button>
            </div>
            <Select value={threatFilter || 'all'} onValueChange={(v) => setThreatFilter(v === 'all' ? '' : v)}>
              <SelectTrigger><SelectValue placeholder="All Threat Levels" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All</SelectItem>
                <SelectItem value="Low">Low</SelectItem><SelectItem value="Medium">Medium</SelectItem>
                <SelectItem value="High">High</SelectItem><SelectItem value="Critical">Critical</SelectItem>
              </SelectContent>
            </Select>
            <Button variant="outline" onClick={loadCompetitors}><RefreshCw className="h-4 w-4 mr-2" />Refresh</Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Competitors</CardTitle><CardDescription>Showing {competitors.length} of {totalCount}</CardDescription></CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8"><Swords className="h-12 w-12 animate-pulse mx-auto mb-4 text-red-500" /><p className="text-gray-500">Loading...</p></div>
          ) : competitors.length === 0 ? (
            <div className="text-center py-8"><Swords className="h-12 w-12 mx-auto mb-4 text-gray-400" /><p className="text-gray-500">No competitors found</p></div>
          ) : (
            <>
              <Table>
                <TableHeader><TableRow>
                  <TableHead>Name</TableHead><TableHead>Industry</TableHead><TableHead>Threat Level</TableHead>
                  <TableHead>Market Share</TableHead><TableHead>Deals</TableHead><TableHead>Won</TableHead><TableHead>Lost</TableHead><TableHead>Status</TableHead>
                </TableRow></TableHeader>
                <TableBody>
                  {competitors.map((c) => (
                    <TableRow key={c.id} className="cursor-pointer hover:bg-gray-50" onClick={() => router.push(`/sales/competitors/${c.id}`)}>
                      <TableCell className="font-medium">{c.name}</TableCell>
                      <TableCell>{c.industry || '-'}</TableCell>
                      <TableCell><Badge className={THREAT_CONFIG[c.threatLevel]?.className || ''}>{c.threatLevel}</Badge></TableCell>
                      <TableCell>{c.estimatedMarketShare}%</TableCell>
                      <TableCell className="font-semibold">{c.dealCount}</TableCell>
                      <TableCell className="text-green-600 font-semibold">{c.wonDeals}</TableCell>
                      <TableCell className="text-red-600 font-semibold">{c.lostDeals}</TableCell>
                      <TableCell><Badge variant={c.isActive ? 'default' : 'secondary'}>{c.isActive ? 'Active' : 'Inactive'}</Badge></TableCell>
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

      {/* Create Competitor Dialog — fields match backend CreateCompetitorDto */}
      <Dialog open={showCreate} onOpenChange={setShowCreate}>
        <DialogContent className="sm:max-w-[550px]">
          <DialogHeader>
            <DialogTitle>Add Competitor</DialogTitle>
            <DialogDescription>Track a competitor in your competitive intelligence database.</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>Company Name *</Label>
                <Input value={form.name} onChange={(e) => setForm(f => ({ ...f, name: e.target.value }))} placeholder="Competitor Inc." /></div>
              <div className="space-y-2"><Label>Industry</Label>
                <Input value={form.industry || ''} onChange={(e) => setForm(f => ({ ...f, industry: e.target.value }))} placeholder="Technology" /></div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>Threat Level</Label>
                <Select value={form.threatLevel} onValueChange={(v) => setForm(f => ({ ...f, threatLevel: v }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Low">🟢 Low</SelectItem><SelectItem value="Medium">🟡 Medium</SelectItem>
                    <SelectItem value="High">🟠 High</SelectItem><SelectItem value="Critical">🔴 Critical</SelectItem>
                  </SelectContent>
                </Select></div>
              <div className="space-y-2"><Label>Market Share (%)</Label>
                <Input type="number" min={0} max={100} value={form.estimatedMarketShare} onChange={(e) => setForm(f => ({ ...f, estimatedMarketShare: Number(e.target.value) || 0 }))} placeholder="15" /></div>
            </div>
            <div className="space-y-2"><Label>Website</Label>
              <Input value={form.website || ''} onChange={(e) => setForm(f => ({ ...f, website: e.target.value }))} placeholder="https://competitor.com" /></div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>Strengths</Label>
                <Textarea rows={2} value={form.strengths || ''} onChange={(e) => setForm(f => ({ ...f, strengths: e.target.value }))} placeholder="What they do well..." /></div>
              <div className="space-y-2"><Label>Weaknesses</Label>
                <Textarea rows={2} value={form.weaknesses || ''} onChange={(e) => setForm(f => ({ ...f, weaknesses: e.target.value }))} placeholder="Where they fall short..." /></div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>Key Products</Label>
                <Input value={form.keyProducts || ''} onChange={(e) => setForm(f => ({ ...f, keyProducts: e.target.value }))} placeholder="Product A, Product B" /></div>
              <div className="space-y-2"><Label>Pricing Strategy</Label>
                <Input value={form.pricingStrategy || ''} onChange={(e) => setForm(f => ({ ...f, pricingStrategy: e.target.value }))} placeholder="Premium / Budget / Freemium" /></div>
            </div>
            <div className="space-y-2"><Label>Description</Label>
              <Textarea rows={2} value={form.description || ''} onChange={(e) => setForm(f => ({ ...f, description: e.target.value }))} placeholder="General notes about this competitor..." /></div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setShowCreate(false); setForm({ ...EMPTY_COMPETITOR }); }}>Cancel</Button>
            <Button className="bg-red-600 hover:bg-red-700" onClick={handleCreate} disabled={creating || !form.name.trim()}>
              {creating ? 'Adding...' : 'Add Competitor'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
