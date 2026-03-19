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
import { FileText, Search, RefreshCw, Plus, Send, CheckCircle, XCircle, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { crmService, type QuoteSummaryDto, type CreateQuoteDto, type CreateQuoteLineItemDto } from '@/services/crmService';
import { format } from 'date-fns';

const STATUS_CONFIG: Record<string, { className: string }> = {
  Draft: { className: 'bg-gray-100 text-gray-800' },
  Sent: { className: 'bg-blue-100 text-blue-800' },
  Accepted: { className: 'bg-green-100 text-green-800' },
  Rejected: { className: 'bg-red-100 text-red-800' },
  Expired: { className: 'bg-amber-100 text-amber-800' },
  Revised: { className: 'bg-yellow-100 text-yellow-800' },
};

const EMPTY_LINE: CreateQuoteLineItemDto = { description: '', quantity: 1, unitPrice: 0 };

// Backend requires opportunityId, quoteName, validUntil, lineItems
const makeEmptyQuote = (): CreateQuoteDto => ({
  opportunityId: '',
  quoteName: '',
  validUntil: new Date(Date.now() + 30 * 86400000).toISOString().split('T')[0],
  lineItems: [{ ...EMPTY_LINE }],
});

export default function QuotesPage() {
  const [quotes, setQuotes] = useState<QuoteSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;
  const [showCreate, setShowCreate] = useState(false);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<CreateQuoteDto>(makeEmptyQuote());

  useEffect(() => { loadQuotes(); }, [page, statusFilter]);

  const loadQuotes = async () => {
    try {
      setLoading(true);
      const data = await crmService.getQuotes(page, pageSize, searchTerm || undefined, statusFilter || undefined);
      setQuotes(data.items);
      setTotalCount(data.totalCount);
    } catch (error) { console.error(error); toast.error('Failed to load quotes'); }
    finally { setLoading(false); }
  };

  const handleSearch = () => { setPage(1); loadQuotes(); };
  const handleSend = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await crmService.sendQuote(id); toast.success('Quote sent'); loadQuotes(); } catch (error: any) { toast.error(error.message); }
  };
  const handleAccept = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await crmService.acceptQuote(id); toast.success('Quote accepted'); loadQuotes(); } catch (error: any) { toast.error(error.message); }
  };

  const handleCreate = async () => {
    if (!form.quoteName.trim()) { toast.error('Quote name is required'); return; }
    const validLines = form.lineItems.filter(l => l.description.trim() && l.quantity > 0);
    if (validLines.length === 0) { toast.error('Add at least one line item'); return; }
    try {
      setCreating(true);
      await crmService.createQuote({ ...form, lineItems: validLines });
      toast.success('Quote created successfully');
      setShowCreate(false);
      setForm(makeEmptyQuote());
      loadQuotes();
    } catch (error: any) { toast.error(error.message || 'Failed to create quote'); }
    finally { setCreating(false); }
  };

  const updateLine = (idx: number, field: keyof CreateQuoteLineItemDto, value: any) => {
    setForm(f => {
      const lineItems = [...f.lineItems];
      lineItems[idx] = { ...lineItems[idx], [field]: value };
      return { ...f, lineItems };
    });
  };
  const addLine = () => setForm(f => ({ ...f, lineItems: [...f.lineItems, { ...EMPTY_LINE }] }));
  const removeLine = (idx: number) => setForm(f => ({ ...f, lineItems: f.lineItems.filter((_, i) => i !== idx) }));

  const formatDate = (d?: string) => { if (!d) return '-'; try { return format(new Date(d), 'dd MMM yyyy'); } catch { return d; } };
  const totalPages = Math.ceil(totalCount / pageSize);
  const quoteTotal = form.lineItems.reduce((s, l) => s + (l.quantity * l.unitPrice), 0);

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2"><FileText className="h-8 w-8 text-teal-600" />Quotes</h1>
          <p className="text-gray-500">Manage sales quotes and proposals</p>
        </div>
        <Button className="bg-teal-600 hover:bg-teal-700" onClick={() => setShowCreate(true)}>
          <Plus className="h-4 w-4 mr-2" />New Quote
        </Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Quotes</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold">{totalCount}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Sent</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-blue-600">{quotes.filter(q => q.quoteStatus === 'Sent').length}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Accepted</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-green-600">{quotes.filter(q => q.quoteStatus === 'Accepted').length}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Value</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-teal-600">${quotes.reduce((s, q) => s + q.totalAmount, 0).toLocaleString()}</p></CardContent></Card>
      </div>

      <Card>
        <CardHeader><CardTitle>Filter</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="flex gap-2">
              <Input placeholder="Search by quote #..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleSearch()} />
              <Button onClick={handleSearch}><Search className="h-4 w-4" /></Button>
            </div>
            <Select value={statusFilter || 'all'} onValueChange={(v) => setStatusFilter(v === 'all' ? '' : v)}>
              <SelectTrigger><SelectValue placeholder="All Statuses" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All</SelectItem><SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="Sent">Sent</SelectItem><SelectItem value="Accepted">Accepted</SelectItem>
                <SelectItem value="Rejected">Rejected</SelectItem><SelectItem value="Expired">Expired</SelectItem>
              </SelectContent>
            </Select>
            <Button variant="outline" onClick={loadQuotes}><RefreshCw className="h-4 w-4 mr-2" />Refresh</Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Quotes</CardTitle><CardDescription>Showing {quotes.length} of {totalCount}</CardDescription></CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8"><FileText className="h-12 w-12 animate-pulse mx-auto mb-4 text-teal-500" /><p className="text-gray-500">Loading...</p></div>
          ) : quotes.length === 0 ? (
            <div className="text-center py-8"><FileText className="h-12 w-12 mx-auto mb-4 text-gray-400" /><p className="text-gray-500">No quotes found</p></div>
          ) : (
            <>
              <Table>
                <TableHeader><TableRow>
                  <TableHead>Quote #</TableHead><TableHead>Name</TableHead><TableHead>Customer</TableHead>
                  <TableHead>Lines</TableHead><TableHead>Total</TableHead><TableHead>Valid Until</TableHead><TableHead>Status</TableHead><TableHead>Actions</TableHead>
                </TableRow></TableHeader>
                <TableBody>
                  {quotes.map((q) => (
                    <TableRow key={q.id} className="cursor-pointer hover:bg-gray-50">
                      <TableCell className="font-mono text-sm text-teal-600">{q.documentNumber}</TableCell>
                      <TableCell className="font-medium">{q.quoteName}</TableCell>
                      <TableCell>{q.customerName || '-'}</TableCell>
                      <TableCell>{q.lineCount}</TableCell>
                      <TableCell className="font-semibold">${q.totalAmount.toLocaleString()}</TableCell>
                      <TableCell>{formatDate(q.validUntil)}</TableCell>
                      <TableCell><Badge className={STATUS_CONFIG[q.quoteStatus]?.className || ''}>{q.quoteStatus}</Badge></TableCell>
                      <TableCell>
                        <div className="flex gap-1">
                          {q.quoteStatus === 'Draft' && <Button variant="outline" size="sm" onClick={(e) => handleSend(q.id, e)}><Send className="h-3 w-3 mr-1" />Send</Button>}
                          {q.quoteStatus === 'Sent' && (
                            <>
                              <Button variant="outline" size="sm" className="text-green-600" onClick={(e) => handleAccept(q.id, e)}><CheckCircle className="h-3 w-3" /></Button>
                              <Button variant="outline" size="sm" className="text-red-600" onClick={(e) => { e.stopPropagation(); crmService.rejectQuote(q.id).then(() => { toast.success('Rejected'); loadQuotes(); }); }}><XCircle className="h-3 w-3" /></Button>
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

      {/* Create Quote Dialog — fields match backend CreateQuoteDto */}
      <Dialog open={showCreate} onOpenChange={setShowCreate}>
        <DialogContent className="sm:max-w-[650px] max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Create New Quote</DialogTitle>
            <DialogDescription>Build a quote with line items for your customer.</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="space-y-2"><Label>Quote Name *</Label>
              <Input value={form.quoteName} onChange={(e) => setForm(f => ({ ...f, quoteName: e.target.value }))} placeholder="Q1 Enterprise License" /></div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>Opportunity ID</Label>
                <Input value={form.opportunityId} onChange={(e) => setForm(f => ({ ...f, opportunityId: e.target.value }))} placeholder="Paste opportunity GUID" /></div>
              <div className="space-y-2"><Label>Valid Until *</Label>
                <Input type="date" value={form.validUntil.split('T')[0]} onChange={(e) => setForm(f => ({ ...f, validUntil: e.target.value }))} /></div>
            </div>
            <div className="space-y-2"><Label>Proposal / Notes</Label>
              <Textarea rows={2} value={form.proposal || ''} onChange={(e) => setForm(f => ({ ...f, proposal: e.target.value }))} placeholder="Quote terms and proposal details..." /></div>

            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <Label className="text-base font-semibold">Line Items</Label>
                <Button variant="outline" size="sm" onClick={addLine}><Plus className="h-3 w-3 mr-1" />Add Line</Button>
              </div>
              {form.lineItems.map((line, idx) => (
                <div key={idx} className="grid grid-cols-12 gap-2 items-end border rounded-lg p-3 bg-gray-50">
                  <div className="col-span-5 space-y-1"><Label className="text-xs">Description *</Label>
                    <Input value={line.description} onChange={(e) => updateLine(idx, 'description', e.target.value)} placeholder="Product or service" /></div>
                  <div className="col-span-2 space-y-1"><Label className="text-xs">Qty</Label>
                    <Input type="number" min={1} value={line.quantity} onChange={(e) => updateLine(idx, 'quantity', Number(e.target.value) || 1)} /></div>
                  <div className="col-span-2 space-y-1"><Label className="text-xs">Unit Price</Label>
                    <Input type="number" min={0} value={line.unitPrice} onChange={(e) => updateLine(idx, 'unitPrice', Number(e.target.value) || 0)} /></div>
                  <div className="col-span-2 text-right font-semibold text-sm pt-5">${(line.quantity * line.unitPrice).toLocaleString()}</div>
                  <div className="col-span-1 pt-5">
                    {form.lineItems.length > 1 && <Button variant="ghost" size="sm" className="text-red-500 h-8 w-8 p-0" onClick={() => removeLine(idx)}><Trash2 className="h-3 w-3" /></Button>}
                  </div>
                </div>
              ))}
              <div className="text-right font-bold text-lg border-t pt-2">Total: ${quoteTotal.toLocaleString()}</div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setShowCreate(false); setForm(makeEmptyQuote()); }}>Cancel</Button>
            <Button className="bg-teal-600 hover:bg-teal-700" onClick={handleCreate} disabled={creating || !form.quoteName.trim()}>
              {creating ? 'Creating...' : 'Create Quote'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
