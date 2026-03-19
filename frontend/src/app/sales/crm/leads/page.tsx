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
import { Users, Search, RefreshCw, Plus, Star, TrendingUp, DollarSign } from 'lucide-react';
import { toast } from 'sonner';
import { crmService, type LeadSummaryDto, type CreateLeadDto } from '@/services/crmService';
import { format } from 'date-fns';

const STATUS_CONFIG: Record<string, { className: string }> = {
  New: { className: 'bg-blue-100 text-blue-800' },
  Contacted: { className: 'bg-cyan-100 text-cyan-800' },
  Qualified: { className: 'bg-green-100 text-green-800' },
  Unqualified: { className: 'bg-gray-100 text-gray-800' },
  Converted: { className: 'bg-emerald-100 text-emerald-800' },
  Lost: { className: 'bg-red-100 text-red-800' },
};

// Initial empty form matching backend CreateLeadDto
const EMPTY_LEAD: CreateLeadDto = { firstName: '', lastName: '' };

export default function LeadsPage() {
  const router = useRouter();
  const [leads, setLeads] = useState<LeadSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;
  const [showCreate, setShowCreate] = useState(false);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<CreateLeadDto>(EMPTY_LEAD);

  useEffect(() => { loadLeads(); }, [page, statusFilter]);

  const loadLeads = async () => {
    try {
      setLoading(true);
      const data = await crmService.getLeads(page, pageSize, searchTerm || undefined, statusFilter || undefined);
      setLeads(data.items);
      setTotalCount(data.totalCount);
    } catch (error) { console.error(error); toast.error('Failed to load leads'); }
    finally { setLoading(false); }
  };

  const handleSearch = () => { setPage(1); loadLeads(); };
  const handleQualify = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await crmService.qualifyLead(id); toast.success('Lead qualified'); loadLeads(); } catch (error: any) { toast.error(error.message); }
  };
  const handleConvert = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await crmService.convertLead(id); toast.success('Lead converted'); loadLeads(); } catch (error: any) { toast.error(error.message); }
  };

  const handleCreate = async () => {
    if (!form.firstName.trim() || !form.lastName.trim()) { toast.error('First name and last name are required'); return; }
    try {
      setCreating(true);
      await crmService.createLead(form);
      toast.success('Lead created successfully');
      setShowCreate(false);
      setForm(EMPTY_LEAD);
      loadLeads();
    } catch (error: any) { toast.error(error.message || 'Failed to create lead'); }
    finally { setCreating(false); }
  };

  const formatDate = (d?: string) => { if (!d) return '-'; try { return format(new Date(d), 'dd MMM yyyy'); } catch { return d; } };
  const totalPages = Math.ceil(totalCount / pageSize);
  const stats = {
    total: totalCount,
    qualified: leads.filter(l => l.leadStatus === 'Qualified').length,
    totalValue: leads.reduce((sum, l) => sum + l.estimatedValue, 0),
    avgScore: leads.length ? Math.round(leads.reduce((sum, l) => sum + l.qualificationScore, 0) / leads.length) : 0,
  };

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2"><Users className="h-8 w-8 text-indigo-600" />Leads</h1>
          <p className="text-gray-500">Manage your sales leads pipeline</p>
        </div>
        <Button className="bg-indigo-600 hover:bg-indigo-700" onClick={() => setShowCreate(true)}>
          <Plus className="h-4 w-4 mr-2" />New Lead
        </Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Leads</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold">{stats.total}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Qualified</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-green-600">{stats.qualified}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Pipeline Value</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-blue-600"><DollarSign className="h-5 w-5 inline" />{stats.totalValue.toLocaleString()}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Avg Score</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-amber-600"><Star className="h-5 w-5 inline" />{stats.avgScore}/100</p></CardContent></Card>
      </div>

      <Card>
        <CardHeader><CardTitle>Filter Leads</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="flex gap-2">
              <Input placeholder="Search by name, company..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleSearch()} />
              <Button onClick={handleSearch}><Search className="h-4 w-4" /></Button>
            </div>
            <Select value={statusFilter || 'all'} onValueChange={(v) => setStatusFilter(v === 'all' ? '' : v)}>
              <SelectTrigger><SelectValue placeholder="All Statuses" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                <SelectItem value="New">New</SelectItem><SelectItem value="Contacted">Contacted</SelectItem>
                <SelectItem value="Qualified">Qualified</SelectItem><SelectItem value="Converted">Converted</SelectItem><SelectItem value="Lost">Lost</SelectItem>
              </SelectContent>
            </Select>
            <Button variant="outline" onClick={loadLeads}><RefreshCw className="h-4 w-4 mr-2" />Refresh</Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Leads</CardTitle><CardDescription>Showing {leads.length} of {totalCount} leads</CardDescription></CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8"><Users className="h-12 w-12 animate-pulse mx-auto mb-4 text-indigo-500" /><p className="text-gray-500">Loading leads...</p></div>
          ) : leads.length === 0 ? (
            <div className="text-center py-8"><Users className="h-12 w-12 mx-auto mb-4 text-gray-400" /><p className="text-gray-500">No leads found</p></div>
          ) : (
            <>
              <Table>
                <TableHeader><TableRow>
                  <TableHead>Name</TableHead><TableHead>Company</TableHead><TableHead>Source</TableHead>
                  <TableHead>Score</TableHead><TableHead>Value</TableHead><TableHead>Status</TableHead><TableHead>Actions</TableHead>
                </TableRow></TableHeader>
                <TableBody>
                  {leads.map((lead) => (
                    <TableRow key={lead.id} className="cursor-pointer hover:bg-gray-50">
                      <TableCell className="font-medium">
                        <div><p>{lead.fullName || `${lead.firstName} ${lead.lastName}`}</p>
                        {lead.email && <p className="text-xs text-gray-500">{lead.email}</p>}</div>
                      </TableCell>
                      <TableCell>{lead.companyName || '-'}</TableCell>
                      <TableCell>{lead.leadSource || '-'}</TableCell>
                      <TableCell>
                        <div className="flex items-center gap-1">
                          <div className="w-12 bg-gray-200 rounded-full h-2"><div className="bg-amber-500 h-2 rounded-full" style={{ width: `${lead.qualificationScore}%` }} /></div>
                          <span className="text-xs">{lead.qualificationScore}</span>
                        </div>
                      </TableCell>
                      <TableCell className="font-semibold">${lead.estimatedValue.toLocaleString()}</TableCell>
                      <TableCell><Badge className={STATUS_CONFIG[lead.leadStatus]?.className || ''}>{lead.leadStatus}</Badge></TableCell>
                      <TableCell>
                        <div className="flex gap-1">
                          {lead.leadStatus === 'New' && <Button variant="outline" size="sm" onClick={(e) => handleQualify(lead.id, e)}><TrendingUp className="h-3 w-3 mr-1" />Qualify</Button>}
                          {lead.leadStatus === 'Qualified' && <Button variant="outline" size="sm" onClick={(e) => handleConvert(lead.id, e)}><Star className="h-3 w-3 mr-1" />Convert</Button>}
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

      {/* Create Lead Dialog — fields match backend CreateLeadDto */}
      <Dialog open={showCreate} onOpenChange={setShowCreate}>
        <DialogContent className="sm:max-w-[550px]">
          <DialogHeader>
            <DialogTitle>Create New Lead</DialogTitle>
            <DialogDescription>Enter lead details to add to your pipeline.</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>First Name *</Label>
                <Input value={form.firstName} onChange={(e) => setForm(f => ({ ...f, firstName: e.target.value }))} placeholder="John" /></div>
              <div className="space-y-2"><Label>Last Name *</Label>
                <Input value={form.lastName} onChange={(e) => setForm(f => ({ ...f, lastName: e.target.value }))} placeholder="Doe" /></div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>Company</Label>
                <Input value={form.companyName || ''} onChange={(e) => setForm(f => ({ ...f, companyName: e.target.value }))} placeholder="Acme Corp" /></div>
              <div className="space-y-2"><Label>Job Title</Label>
                <Input value={form.jobTitle || ''} onChange={(e) => setForm(f => ({ ...f, jobTitle: e.target.value }))} placeholder="Sales Manager" /></div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>Email</Label>
                <Input type="email" value={form.email || ''} onChange={(e) => setForm(f => ({ ...f, email: e.target.value }))} placeholder="john@acme.com" /></div>
              <div className="space-y-2"><Label>Phone</Label>
                <Input value={form.phone || ''} onChange={(e) => setForm(f => ({ ...f, phone: e.target.value }))} placeholder="+1 555-0100" /></div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>Lead Source</Label>
                <Select value={form.leadSource || 'Unknown'} onValueChange={(v) => setForm(f => ({ ...f, leadSource: v }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Unknown">Unknown</SelectItem><SelectItem value="Website">Website</SelectItem>
                    <SelectItem value="Referral">Referral</SelectItem><SelectItem value="ColdCall">Cold Call</SelectItem>
                    <SelectItem value="SocialMedia">Social Media</SelectItem><SelectItem value="TradeShow">Trade Show</SelectItem>
                    <SelectItem value="Email">Email Campaign</SelectItem><SelectItem value="Other">Other</SelectItem>
                  </SelectContent>
                </Select></div>
              <div className="space-y-2"><Label>Estimated Value</Label>
                <Input type="number" value={form.estimatedValue ?? ''} onChange={(e) => setForm(f => ({ ...f, estimatedValue: e.target.value ? Number(e.target.value) : undefined }))} placeholder="10000" /></div>
            </div>
            <div className="space-y-2"><Label>Notes</Label>
              <Textarea rows={3} value={form.notes || ''} onChange={(e) => setForm(f => ({ ...f, notes: e.target.value }))} placeholder="Additional notes about this lead..." /></div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setShowCreate(false); setForm(EMPTY_LEAD); }}>Cancel</Button>
            <Button className="bg-indigo-600 hover:bg-indigo-700" onClick={handleCreate} disabled={creating || !form.firstName.trim() || !form.lastName.trim()}>
              {creating ? 'Creating...' : 'Create Lead'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
