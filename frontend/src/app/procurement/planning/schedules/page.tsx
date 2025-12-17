'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Search, Eye, Edit, Plus, Download, RefreshCw, Filter, Trash2, Calendar, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { procurementScheduleService, type ProcurementScheduleDto } from '@/services/procurementPlanningService';
import { format } from 'date-fns';
import * as XLSX from 'xlsx';
import { saveAs } from 'file-saver';

export default function ProcurementSchedulesPage() {
  const router = useRouter();
  const [schedules, setSchedules] = useState<ProcurementScheduleDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [deleteDialog, setDeleteDialog] = useState<{ open: boolean; id: string | null; deleting: boolean }>({ open: false, id: null, deleting: false });

  useEffect(() => { loadSchedules(); }, [page, statusFilter]);

  const loadSchedules = async () => {
    try {
      setLoading(true);
      const result = await procurementScheduleService.getSchedules({
        page, pageSize: 25,
        search: searchTerm || undefined,
        status: statusFilter !== 'all' ? statusFilter : undefined,
      });
      setSchedules(result.items);
      setTotalPages(result.totalPages);
    } catch (error) {
      console.error('Error loading schedules:', error);
      toast.error('Failed to load procurement schedules');
    } finally { setLoading(false); }
  };

  const handleSearch = () => { setPage(1); loadSchedules(); };
  const handleViewDetails = (id: string) => router.push(`/procurement/planning/schedules/${id}`);
  const handleEdit = (id: string) => router.push(`/procurement/planning/schedules/${id}/edit`);
  const handleCreateNew = () => router.push('/procurement/planning/schedules/new');

  const handleConfirmDelete = async () => {
    if (!deleteDialog.id) return;
    try {
      setDeleteDialog(prev => ({ ...prev, deleting: true }));
      await procurementScheduleService.deleteSchedule(deleteDialog.id);
      toast.success('Schedule deleted successfully');
      setDeleteDialog({ open: false, id: null, deleting: false });
      loadSchedules();
    } catch (error) {
      console.error('Error deleting schedule:', error);
      toast.error('Failed to delete schedule');
      setDeleteDialog(prev => ({ ...prev, deleting: false }));
    }
  };

  const handleExportToExcel = () => {
    try {
      if (schedules.length === 0) { toast.error('No data to export'); return; }
      const exportData = schedules.map(s => ({
        'Schedule #': s.scheduleCode, 'Title': s.title, 'Department': s.departmentName || '',
        'Status': s.status, 'Type': s.scheduleType,
        'Start Date': format(new Date(s.plannedStartDate), 'yyyy-MM-dd'),
        'End Date': format(new Date(s.plannedEndDate), 'yyyy-MM-dd'),
      }));
      const ws = XLSX.utils.json_to_sheet(exportData);
      const wb = XLSX.utils.book_new();
      XLSX.utils.book_append_sheet(wb, ws, 'Schedules');
      const excelBuffer = XLSX.write(wb, { bookType: 'xlsx', type: 'array' });
      const data = new Blob([excelBuffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
      saveAs(data, `procurement_schedules_${format(new Date(), 'yyyyMMdd')}.xlsx`);
      toast.success('Schedules exported successfully');
    } catch (error) { console.error('Error exporting:', error); toast.error('Failed to export'); }
  };

  const getStatusBadge = (status: string) => {
    const config: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', className: string }> = {
      'Planned': { variant: 'secondary', className: 'bg-gray-100 text-gray-800' },
      'InProgress': { variant: 'default', className: 'bg-blue-100 text-blue-800' },
      'Completed': { variant: 'default', className: 'bg-green-100 text-green-800' },
      'Delayed': { variant: 'destructive', className: 'bg-red-100 text-red-800' },
      'Cancelled': { variant: 'outline', className: 'bg-gray-100 text-gray-800' },
    };
    const c = config[status] || { variant: 'outline' as const, className: '' };
    return <Badge variant={c.variant} className={c.className}>{status}</Badge>;
  };



  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Procurement Schedules</h1>
          <p className="text-muted-foreground">Manage procurement timing and scheduling</p>
        </div>
        <Button onClick={handleCreateNew} className="gap-2"><Plus className="h-4 w-4" />Create Schedule</Button>
      </div>

      <Card>
        <CardHeader><CardTitle className="flex items-center gap-2"><Filter className="h-5 w-5" />Filters</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="flex gap-2">
              <Input placeholder="Search schedules..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleSearch()} className="flex-1" />
              <Button onClick={handleSearch} size="icon" variant="secondary"><Search className="h-4 w-4" /></Button>
            </div>
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger><SelectValue placeholder="All Statuses" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                <SelectItem value="Planned">Planned</SelectItem>
                <SelectItem value="InProgress">In Progress</SelectItem>
                <SelectItem value="Completed">Completed</SelectItem>
                <SelectItem value="Delayed">Delayed</SelectItem>
                <SelectItem value="Cancelled">Cancelled</SelectItem>
              </SelectContent>
            </Select>
            <div className="flex gap-2">
              <Button onClick={loadSchedules} variant="outline" className="gap-2 flex-1"><RefreshCw className="h-4 w-4" />Refresh</Button>
              <Button onClick={handleExportToExcel} variant="outline" className="gap-2 flex-1"><Download className="h-4 w-4" />Export</Button>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Schedules</CardTitle><CardDescription>{loading ? 'Loading...' : `${schedules.length} schedule(s) found`}</CardDescription></CardHeader>
        <CardContent>
          {loading ? (<div className="text-center py-8">Loading schedules...</div>) : schedules.length === 0 ? (<div className="text-center py-8 text-gray-500">No schedules found</div>) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader><TableRow><TableHead>Schedule #</TableHead><TableHead>Title</TableHead><TableHead>Plan #</TableHead><TableHead>Department</TableHead><TableHead>Status</TableHead><TableHead>Timeline</TableHead><TableHead>Actions</TableHead></TableRow></TableHeader>
                <TableBody>
                  {schedules.map((s) => (
                    <TableRow key={s.id}>
                      <TableCell className="font-medium">{s.scheduleCode}</TableCell>
                      <TableCell><div className="font-medium">{s.title}</div></TableCell>
                      <TableCell>{s.procurementPlanNumber || 'N/A'}</TableCell>
                      <TableCell>{s.departmentName || 'N/A'}</TableCell>
                      <TableCell>{getStatusBadge(s.status)}</TableCell>
                      <TableCell><div className="text-sm"><div>{format(new Date(s.plannedStartDate), 'MMM dd, yyyy')}</div><div className="text-gray-500">to {format(new Date(s.plannedEndDate), 'MMM dd, yyyy')}</div></div></TableCell>
                      <TableCell>
                        <div className="flex gap-2">
                          <Button variant="ghost" size="sm" onClick={() => handleViewDetails(s.id)} title="View"><Eye className="h-4 w-4" /></Button>
                          {s.status === 'Planned' && (<><Button variant="ghost" size="sm" onClick={() => handleEdit(s.id)} title="Edit"><Edit className="h-4 w-4" /></Button><Button variant="ghost" size="sm" onClick={() => setDeleteDialog({ open: true, id: s.id, deleting: false })} title="Delete"><Trash2 className="h-4 w-4" /></Button></>)}
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

      {/* Delete Confirmation Dialog */}
      <Dialog open={deleteDialog.open} onOpenChange={(open) => !deleteDialog.deleting && setDeleteDialog({ open, id: open ? deleteDialog.id : null, deleting: false })}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete Schedule</DialogTitle>
            <DialogDescription>Are you sure you want to delete this schedule? This action cannot be undone.</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteDialog({ open: false, id: null, deleting: false })} disabled={deleteDialog.deleting}>Cancel</Button>
            <Button variant="destructive" onClick={handleConfirmDelete} disabled={deleteDialog.deleting}>
              {deleteDialog.deleting && <Loader2 className="h-4 w-4 animate-spin mr-2" />}Delete
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

