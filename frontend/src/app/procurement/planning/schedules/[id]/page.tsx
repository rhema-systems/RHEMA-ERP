'use client';

import { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { ArrowLeft, Edit, Play, CheckCircle, Loader2, Calendar, FileText, Building } from 'lucide-react';
import { toast } from 'sonner';
import { procurementScheduleService, type ProcurementScheduleDetailDto } from '@/services/procurementPlanningService';
import { format } from 'date-fns';

export default function ProcurementScheduleDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';
  const [schedule, setSchedule] = useState<ProcurementScheduleDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [confirmDialog, setConfirmDialog] = useState<{ open: boolean; type: 'start' | 'complete' | null }>({ open: false, type: null });

  useEffect(() => { if (id) loadSchedule(); }, [id]);

  const loadSchedule = async () => {
    try {
      setLoading(true);
      const data = await procurementScheduleService.getScheduleById(id);
      setSchedule(data);
    } catch (error) {
      console.error('Error loading schedule:', error);
      toast.error('Failed to load schedule details');
    } finally { setLoading(false); }
  };

  const handleConfirmAction = async () => {
    if (!confirmDialog.type) return;
    try {
      setActionLoading(true);
      if (confirmDialog.type === 'start') {
        await procurementScheduleService.startSchedule(id);
        toast.success('Schedule started successfully');
      } else {
        await procurementScheduleService.completeSchedule(id);
        toast.success('Schedule completed successfully');
      }
      setConfirmDialog({ open: false, type: null });
      loadSchedule();
    } catch (error) {
      console.error(`Error ${confirmDialog.type === 'start' ? 'starting' : 'completing'} schedule:`, error);
      toast.error(`Failed to ${confirmDialog.type === 'start' ? 'start' : 'complete'} schedule`);
    } finally { setActionLoading(false); }
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

  if (loading) return <div className="flex items-center justify-center h-96"><Loader2 className="h-8 w-8 animate-spin" /></div>;
  if (!schedule) return <div className="text-center py-8 text-gray-500">Schedule not found</div>;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" onClick={() => router.back()}><ArrowLeft className="h-5 w-5" /></Button>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-3xl font-bold tracking-tight">{schedule.scheduleCode}</h1>
              {getStatusBadge(schedule.status)}
            </div>
            <p className="text-muted-foreground">{schedule.title}</p>
          </div>
        </div>
        <div className="flex gap-2">
          {schedule.status === 'Planned' && (
            <>
              <Button variant="outline" onClick={() => router.push(`/procurement/planning/schedules/${id}/edit`)} className="gap-2">
                <Edit className="h-4 w-4" />Edit
              </Button>
              <Button onClick={() => setConfirmDialog({ open: true, type: 'start' })} disabled={actionLoading} className="gap-2">
                <Play className="h-4 w-4" />Start
              </Button>
            </>
          )}
          {schedule.status === 'InProgress' && (
            <Button onClick={() => setConfirmDialog({ open: true, type: 'complete' })} disabled={actionLoading} className="gap-2 bg-green-600 hover:bg-green-700">
              <CheckCircle className="h-4 w-4" />Complete
            </Button>
          )}
        </div>
      </div>

      {/* Schedule Details */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <Card>
          <CardHeader><CardTitle className="flex items-center gap-2"><Calendar className="h-5 w-5" />Timeline</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div><span className="text-sm text-muted-foreground">Planned Start</span><p className="font-medium">{format(new Date(schedule.plannedStartDate), 'dd MMM yyyy')}</p></div>
              <div><span className="text-sm text-muted-foreground">Planned End</span><p className="font-medium">{format(new Date(schedule.plannedEndDate), 'dd MMM yyyy')}</p></div>
              {schedule.actualStartDate && <div><span className="text-sm text-muted-foreground">Actual Start</span><p className="font-medium">{format(new Date(schedule.actualStartDate), 'dd MMM yyyy')}</p></div>}
              {schedule.actualEndDate && <div><span className="text-sm text-muted-foreground">Actual End</span><p className="font-medium">{format(new Date(schedule.actualEndDate), 'dd MMM yyyy')}</p></div>}
            </div>
            <div><span className="text-sm text-muted-foreground">Schedule Type</span><p className="font-medium"><Badge variant="outline">{schedule.scheduleType}</Badge></p></div>
            {schedule.isOptimalTiming && <div><Badge className="bg-green-100 text-green-800">Optimal Timing</Badge></div>}
            {schedule.timingRationale && <div><span className="text-sm text-muted-foreground">Timing Rationale</span><p className="mt-1">{schedule.timingRationale}</p></div>}
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle className="flex items-center gap-2"><Building className="h-5 w-5" />References</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <div><span className="text-sm text-muted-foreground">Department</span><p className="font-medium">{schedule.departmentName || 'N/A'}</p></div>
            {schedule.procurementPlanNumber && (
              <div>
                <span className="text-sm text-muted-foreground">Procurement Plan</span>
                <p className="font-medium">{schedule.procurementPlanNumber}</p>
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Additional Details */}
      <Card>
        <CardHeader><CardTitle className="flex items-center gap-2"><FileText className="h-5 w-5" />Details</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          {schedule.description && <div><span className="text-sm text-muted-foreground">Description</span><p className="mt-1">{schedule.description}</p></div>}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="flex items-center gap-2"><Badge variant={schedule.considerSeasonalPricing ? 'default' : 'outline'}>{schedule.considerSeasonalPricing ? '✓' : '✗'} Seasonal Pricing</Badge></div>
            <div className="flex items-center gap-2"><Badge variant={schedule.considerCashFlow ? 'default' : 'outline'}>{schedule.considerCashFlow ? '✓' : '✗'} Cash Flow</Badge></div>
            <div className="flex items-center gap-2"><Badge variant={schedule.consolidationOpportunity ? 'default' : 'outline'}>{schedule.consolidationOpportunity ? '✓' : '✗'} Consolidation</Badge></div>
          </div>
          {schedule.seasonalNotes && <div><span className="text-sm text-muted-foreground">Seasonal Notes</span><p className="mt-1">{schedule.seasonalNotes}</p></div>}
          {schedule.cashFlowNotes && <div><span className="text-sm text-muted-foreground">Cash Flow Notes</span><p className="mt-1">{schedule.cashFlowNotes}</p></div>}
          {schedule.storageLimitations && <div><span className="text-sm text-muted-foreground">Storage Limitations</span><p className="mt-1">{schedule.storageLimitations}</p></div>}
          {schedule.consolidationNotes && <div><span className="text-sm text-muted-foreground">Consolidation Notes</span><p className="mt-1">{schedule.consolidationNotes}</p></div>}
          {schedule.notes && <div><span className="text-sm text-muted-foreground">Notes</span><p className="mt-1">{schedule.notes}</p></div>}
        </CardContent>
      </Card>

      {/* Confirmation Dialog */}
      <Dialog open={confirmDialog.open} onOpenChange={(open) => setConfirmDialog({ open, type: open ? confirmDialog.type : null })}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{confirmDialog.type === 'start' ? 'Start Schedule' : 'Complete Schedule'}</DialogTitle>
            <DialogDescription>
              {confirmDialog.type === 'start'
                ? 'Are you sure you want to start this schedule? This will change the status to In Progress.'
                : 'Are you sure you want to mark this schedule as completed?'}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmDialog({ open: false, type: null })} disabled={actionLoading}>Cancel</Button>
            <Button onClick={handleConfirmAction} disabled={actionLoading} className={confirmDialog.type === 'complete' ? 'bg-green-600 hover:bg-green-700' : ''}>
              {actionLoading ? <Loader2 className="h-4 w-4 animate-spin mr-2" /> : null}
              {confirmDialog.type === 'start' ? 'Start' : 'Complete'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
