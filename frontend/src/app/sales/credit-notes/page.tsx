'use client';

import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { CreditCard, Search, RefreshCw, XCircle } from 'lucide-react';
import { toast } from 'sonner';
import { returnOrderService, type CreditNoteSummaryDto } from '@/services/returnOrderService';
import { format } from 'date-fns';

const STATUS_CONFIG: Record<string, { className: string }> = {
  Draft: { className: 'bg-gray-100 text-gray-800' },
  PendingApproval: { className: 'bg-blue-100 text-blue-800' },
  Approved: { className: 'bg-blue-100 text-blue-800' },
  Applied: { className: 'bg-green-100 text-green-800' },
  Voided: { className: 'bg-red-100 text-red-800' },
};

const formatStatus = (value: string) => value.replace(/([a-z])([A-Z])/g, '$1 $2');

export default function CreditNotesPage() {
  const [notes, setNotes] = useState<CreditNoteSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  useEffect(() => { loadNotes(); }, [page, statusFilter]);

  const loadNotes = async () => {
    try {
      setLoading(true);
      const data = await returnOrderService.getCreditNotes(page, pageSize, searchTerm || undefined, statusFilter || undefined);
      setNotes(data.items); setTotalCount(data.totalCount);
    } catch (error) { console.error(error); toast.error('Failed to load credit notes'); }
    finally { setLoading(false); }
  };

  const handleSearch = () => { setPage(1); loadNotes(); };
  const handleSubmitForApproval = async (id: string) => {
    try { await returnOrderService.submitCreditNoteForApproval(id); toast.success('Submitted for approval'); await loadNotes(); } catch (err: any) { toast.error(err.message); }
  };
  const handleWorkflowApprove = async (id: string, comments: string) => {
    try { await returnOrderService.processCreditNoteApproval(id, { isApproved: true, comments }); toast.success('Approved'); await loadNotes(); } catch (err: any) { toast.error(err.message); }
  };
  const handleWorkflowReject = async (id: string, comments: string) => {
    try {
      await returnOrderService.processCreditNoteApproval(id, { isApproved: false, comments, rejectionReason: comments });
      toast.success('Returned to draft');
      await loadNotes();
    } catch (err: any) { toast.error(err.message); }
  };
  const handleApply = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await returnOrderService.applyCreditNote(id); toast.success('Applied'); loadNotes(); } catch (err: any) { toast.error(err.message); }
  };
  const handleVoid = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await returnOrderService.voidCreditNote(id); toast.success('Voided'); loadNotes(); } catch (err: any) { toast.error(err.message); }
  };

  const formatDate = (d?: string) => { if (!d) return '-'; try { return format(new Date(d), 'dd MMM yyyy'); } catch { return d; } };
  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2"><CreditCard className="h-8 w-8 text-emerald-600" />Credit Notes</h1>
          <p className="text-gray-500">Manage customer credit notes</p>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold">{totalCount}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Pending Approval</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-blue-600">{notes.filter(n => ['Draft', 'PendingApproval'].includes(n.creditNoteStatus)).length}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Applied</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-green-600">{notes.filter(n => n.creditNoteStatus === 'Applied').length}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Value</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-emerald-600">${notes.reduce((s, n) => s + n.totalAmount, 0).toLocaleString()}</p></CardContent></Card>
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
                <SelectItem value="PendingApproval">Pending Approval</SelectItem>
                <SelectItem value="Approved">Approved</SelectItem>
                <SelectItem value="Applied">Applied</SelectItem>
                <SelectItem value="Voided">Voided</SelectItem>
              </SelectContent>
            </Select>
            <Button variant="outline" onClick={loadNotes}><RefreshCw className="h-4 w-4 mr-2" />Refresh</Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Credit Notes</CardTitle><CardDescription>Showing {notes.length} of {totalCount}</CardDescription></CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8"><CreditCard className="h-12 w-12 animate-pulse mx-auto mb-4 text-emerald-500" /><p className="text-gray-500">Loading...</p></div>
          ) : notes.length === 0 ? (
            <div className="text-center py-8"><CreditCard className="h-12 w-12 mx-auto mb-4 text-gray-400" /><p className="text-gray-500">No credit notes found</p></div>
          ) : (
            <>
              <Table>
                <TableHeader><TableRow>
                  <TableHead>CN #</TableHead><TableHead>Customer</TableHead><TableHead>Reason</TableHead>
                  <TableHead>Lines</TableHead><TableHead>Amount</TableHead><TableHead>Applied</TableHead><TableHead>Status</TableHead><TableHead>Actions</TableHead>
                </TableRow></TableHeader>
                <TableBody>
                  {notes.map((n) => (
                    <TableRow key={n.id} className="cursor-pointer hover:bg-gray-50">
                      <TableCell className="font-mono text-sm text-emerald-600">{n.documentNumber}</TableCell>
                      <TableCell>{n.customerName || '-'}</TableCell>
                      <TableCell>{n.reason || '-'}</TableCell>
                      <TableCell>{n.lineCount}</TableCell>
                      <TableCell className="font-semibold">${n.totalAmount.toLocaleString()}</TableCell>
                      <TableCell>{formatDate(n.appliedDate)}</TableCell>
                      <TableCell><Badge className={STATUS_CONFIG[n.creditNoteStatus]?.className || ''}>{formatStatus(n.creditNoteStatus)}</Badge></TableCell>
                      <TableCell>
                        <div className="flex gap-1">
                          <WorkflowApprovalActions
                            entityType="CreditNote"
                            entityId={n.id}
                            entityLabel="Credit Note"
                            entityNumber={n.documentNumber}
                            status={n.creditNoteStatus}
                            loadWorkflowSummary
                            canSubmit={n.creditNoteStatus === 'Draft'}
                            canApproveReject={n.creditNoteStatus === 'PendingApproval'}
                            onSubmit={() => handleSubmitForApproval(n.id)}
                            onApprove={(comments) => handleWorkflowApprove(n.id, comments)}
                            onReject={(comments) => handleWorkflowReject(n.id, comments)}
                            onAfterAction={loadNotes}
                            onOpenWorkflows={() => { window.location.href = '/administration/workflow'; }}
                          />
                          {n.creditNoteStatus === 'Approved' && <Button variant="outline" size="sm" className="text-green-600" onClick={(e) => handleApply(n.id, e)}>Apply</Button>}
                          {n.creditNoteStatus !== 'Voided' && n.creditNoteStatus !== 'Applied' && <Button variant="outline" size="sm" className="text-red-600" onClick={(e) => handleVoid(n.id, e)}><XCircle className="h-3 w-3" /></Button>}
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
