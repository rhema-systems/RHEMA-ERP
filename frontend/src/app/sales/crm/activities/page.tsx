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
import { Activity, Search, RefreshCw, Plus, CheckCircle, Phone, Mail, Calendar } from 'lucide-react';
import { toast } from 'sonner';
import { crmService, type ActivitySummaryDto, type CreateActivityDto } from '@/services/crmService';
import { format } from 'date-fns';

const TYPE_ICONS: Record<string, React.ReactNode> = {
  Call: <Phone className="h-4 w-4 text-blue-500" />,
  Email: <Mail className="h-4 w-4 text-green-500" />,
  Meeting: <Calendar className="h-4 w-4 text-purple-500" />,
  Task: <CheckCircle className="h-4 w-4 text-amber-500" />,
};

// Backend uses string for activityStatus
const STATUS_CONFIG: Record<string, { className: string }> = {
  Planned: { className: 'bg-blue-100 text-blue-800' },
  InProgress: { className: 'bg-yellow-100 text-yellow-800' },
  Completed: { className: 'bg-green-100 text-green-800' },
  Cancelled: { className: 'bg-red-100 text-red-800' },
};

// Backend uses numeric priority (1-4): 1=Low, 2=Normal, 3=High, 4=Urgent
const PRIORITY_LABELS: Record<number, string> = { 1: 'Low', 2: 'Normal', 3: 'High', 4: 'Urgent' };

const EMPTY_ACTIVITY: CreateActivityDto = { subject: '' };

export default function ActivitiesPage() {
  const [activities, setActivities] = useState<ActivitySummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [typeFilter, setTypeFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;
  const [showCreate, setShowCreate] = useState(false);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<CreateActivityDto>(EMPTY_ACTIVITY);

  useEffect(() => { loadActivities(); }, [page, typeFilter, statusFilter]);

  const loadActivities = async () => {
    try {
      setLoading(true);
      const data = await crmService.getActivities(page, pageSize, searchTerm || undefined, typeFilter || undefined, statusFilter || undefined);
      setActivities(data.items);
      setTotalCount(data.totalCount);
    } catch (error) { console.error(error); toast.error('Failed to load activities'); }
    finally { setLoading(false); }
  };

  const handleSearch = () => { setPage(1); loadActivities(); };
  const handleComplete = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await crmService.completeActivity(id); toast.success('Activity completed'); loadActivities(); } catch (error: any) { toast.error(error.message); }
  };

  const handleCreate = async () => {
    if (!form.subject.trim()) { toast.error('Subject is required'); return; }
    try {
      setCreating(true);
      await crmService.createActivity(form);
      toast.success('Activity created successfully');
      setShowCreate(false);
      setForm(EMPTY_ACTIVITY);
      loadActivities();
    } catch (error: any) { toast.error(error.message || 'Failed to create activity'); }
    finally { setCreating(false); }
  };

  const formatDate = (d?: string) => { if (!d) return '-'; try { return format(new Date(d), 'dd MMM yyyy HH:mm'); } catch { return d; } };
  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2"><Activity className="h-8 w-8 text-orange-600" />CRM Activities</h1>
          <p className="text-gray-500">Calls, emails, meetings, and tasks</p>
        </div>
        <Button className="bg-orange-600 hover:bg-orange-700" onClick={() => setShowCreate(true)}>
          <Plus className="h-4 w-4 mr-2" />New Activity
        </Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold">{totalCount}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Planned</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-blue-600">{activities.filter(a => a.activityStatus === 'Planned').length}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">In Progress</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-yellow-600">{activities.filter(a => a.activityStatus === 'InProgress').length}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Completed</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-green-600">{activities.filter(a => a.activityStatus === 'Completed').length}</p></CardContent></Card>
      </div>

      <Card>
        <CardHeader><CardTitle>Filter</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="flex gap-2">
              <Input placeholder="Search..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleSearch()} />
              <Button onClick={handleSearch}><Search className="h-4 w-4" /></Button>
            </div>
            <Select value={typeFilter || 'all'} onValueChange={(v) => setTypeFilter(v === 'all' ? '' : v)}>
              <SelectTrigger><SelectValue placeholder="All Types" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                <SelectItem value="Call">Call</SelectItem><SelectItem value="Email">Email</SelectItem>
                <SelectItem value="Meeting">Meeting</SelectItem><SelectItem value="Task">Task</SelectItem>
              </SelectContent>
            </Select>
            <Select value={statusFilter || 'all'} onValueChange={(v) => setStatusFilter(v === 'all' ? '' : v)}>
              <SelectTrigger><SelectValue placeholder="All Statuses" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All</SelectItem><SelectItem value="Planned">Planned</SelectItem>
                <SelectItem value="InProgress">In Progress</SelectItem><SelectItem value="Completed">Completed</SelectItem>
              </SelectContent>
            </Select>
            <Button variant="outline" onClick={loadActivities}><RefreshCw className="h-4 w-4 mr-2" />Refresh</Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Activities</CardTitle><CardDescription>Showing {activities.length} of {totalCount}</CardDescription></CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8"><Activity className="h-12 w-12 animate-pulse mx-auto mb-4 text-orange-500" /><p className="text-gray-500">Loading...</p></div>
          ) : activities.length === 0 ? (
            <div className="text-center py-8"><Activity className="h-12 w-12 mx-auto mb-4 text-gray-400" /><p className="text-gray-500">No activities found</p></div>
          ) : (
            <>
              <Table>
                <TableHeader><TableRow>
                  <TableHead>Type</TableHead><TableHead>Subject</TableHead><TableHead>Priority</TableHead>
                  <TableHead>Due Date</TableHead><TableHead>Assigned To</TableHead><TableHead>Status</TableHead><TableHead>Actions</TableHead>
                </TableRow></TableHeader>
                <TableBody>
                  {activities.map((a) => (
                    <TableRow key={a.id} className="cursor-pointer hover:bg-gray-50">
                      <TableCell><div className="flex items-center gap-2">{TYPE_ICONS[a.activityType] || <Activity className="h-4 w-4" />}{a.activityType}</div></TableCell>
                      <TableCell className="font-medium">{a.subject}</TableCell>
                      <TableCell>
                        {/* priority is numeric: 1=Low, 2=Normal, 3=High, 4=Urgent */}
                        <Badge variant={a.priority >= 3 ? 'destructive' : 'outline'}>
                          {PRIORITY_LABELS[a.priority] || `P${a.priority}`}
                        </Badge>
                      </TableCell>
                      <TableCell>{formatDate(a.dueDate)}</TableCell>
                      <TableCell>{a.assignedToName || '-'}</TableCell>
                      <TableCell><Badge className={STATUS_CONFIG[a.activityStatus]?.className || ''}>{a.activityStatus.replace(/([A-Z])/g, ' $1').trim()}</Badge></TableCell>
                      <TableCell>
                        {a.activityStatus !== 'Completed' && (
                          <Button variant="outline" size="sm" onClick={(e) => handleComplete(a.id, e)}><CheckCircle className="h-3 w-3 mr-1" />Complete</Button>
                        )}
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

      {/* Create Activity Dialog — fields match backend CreateActivityDto */}
      <Dialog open={showCreate} onOpenChange={setShowCreate}>
        <DialogContent className="sm:max-w-[500px]">
          <DialogHeader>
            <DialogTitle>Create New Activity</DialogTitle>
            <DialogDescription>Log a call, email, meeting, or task.</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="space-y-2"><Label>Subject *</Label>
              <Input value={form.subject} onChange={(e) => setForm(f => ({ ...f, subject: e.target.value }))} placeholder="Follow-up call with client" /></div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>Activity Type</Label>
                <Select value={form.activityType || 'Call'} onValueChange={(v) => setForm(f => ({ ...f, activityType: v }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Call">📞 Call</SelectItem><SelectItem value="Email">📧 Email</SelectItem>
                    <SelectItem value="Meeting">📅 Meeting</SelectItem><SelectItem value="Task">✅ Task</SelectItem>
                  </SelectContent>
                </Select></div>
              <div className="space-y-2"><Label>Priority</Label>
                <Select value={String(form.priority || 2)} onValueChange={(v) => setForm(f => ({ ...f, priority: Number(v) }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="1">Low</SelectItem><SelectItem value="2">Normal</SelectItem>
                    <SelectItem value="3">High</SelectItem><SelectItem value="4">Urgent</SelectItem>
                  </SelectContent>
                </Select></div>
            </div>
            <div className="space-y-2"><Label>Due Date</Label>
              <Input type="datetime-local" value={form.dueDate ? form.dueDate.slice(0, 16) : ''} onChange={(e) => setForm(f => ({ ...f, dueDate: e.target.value || undefined }))} /></div>
            <div className="space-y-2"><Label>Description</Label>
              <Textarea rows={3} value={form.description || ''} onChange={(e) => setForm(f => ({ ...f, description: e.target.value }))} placeholder="Activity details and notes..." /></div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setShowCreate(false); setForm(EMPTY_ACTIVITY); }}>Cancel</Button>
            <Button className="bg-orange-600 hover:bg-orange-700" onClick={handleCreate} disabled={creating || !form.subject.trim()}>
              {creating ? 'Creating...' : 'Create Activity'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
