'use client';

import { useEffect, useMemo, useState } from 'react';
import { CalendarClock, RefreshCw, ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import {
  inventoryManagementService,
  InventoryCycleCountScheduleDto,
  SaveInventoryCycleCountScheduleRequest,
  WarehouseDto,
  WarehouseLocationDto,
} from '@/services/inventoryManagementService';
import { procurementCalendarService } from '@/services/procurement-calendar.service';
import type { ProcurementCalendarOccurrence } from '@/types/procurement-calendar';

type Props = { warehouses: WarehouseDto[]; onCountsChanged: () => void };

const emptyForm: SaveInventoryCycleCountScheduleRequest = {
  warehouseId: '',
  locationId: '',
  abcClass: 'A',
  frequencyDays: 30,
  nextDueAtUtc: '',
  calendarOccurrenceId: '',
  cutoffOccurrenceId: '',
  recountQuantityThreshold: 1,
  recountValueThreshold: 0,
  isActive: true,
  notes: '',
};

export function PhysicalCountControlPanel({ warehouses, onCountsChanged }: Props) {
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [schedules, setSchedules] = useState<InventoryCycleCountScheduleDto[]>([]);
  const [locations, setLocations] = useState<WarehouseLocationDto[]>([]);
  const [cycleOccurrences, setCycleOccurrences] = useState<ProcurementCalendarOccurrence[]>([]);
  const [cutoffOccurrences, setCutoffOccurrences] = useState<ProcurementCalendarOccurrence[]>([]);
  const [form, setForm] = useState<SaveInventoryCycleCountScheduleRequest>(emptyForm);

  const warehouseNames = useMemo(() => new Map(warehouses.map(item => [item.id, item.name])), [warehouses]);
  const locationNames = useMemo(() => new Map(locations.map(item => [item.id, item.locationCode])), [locations]);

  const load = async () => {
    setLoading(true);
    try {
      const [scheduleRows, cyclePage, cutoffPage] = await Promise.all([
        inventoryManagementService.getCycleCountSchedules(),
        procurementCalendarService.occurrences({ page: 1, pageSize: 200, eventType: 'CycleCount' }),
        procurementCalendarService.occurrences({ page: 1, pageSize: 200, eventType: 'YearEndClose' }),
      ]);
      setSchedules(scheduleRows);
      setCycleOccurrences(cyclePage.items.filter(item => !['Completed', 'Cancelled', 'Failed'].includes(item.status)));
      setCutoffOccurrences(cutoffPage.items.filter(item => !['Completed', 'Cancelled', 'Failed'].includes(item.status)));
    } catch {
      toast.error('Cycle-count controls could not be loaded for the current assignment.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { if (open) void load(); }, [open]);
  useEffect(() => {
    if (!form.warehouseId) { setLocations([]); return; }
    void inventoryManagementService.getWarehouseLocations(form.warehouseId).then(setLocations).catch(() => setLocations([]));
  }, [form.warehouseId]);

  const save = async () => {
    if (!form.warehouseId || !form.locationId || !form.nextDueAtUtc || !form.calendarOccurrenceId || !form.cutoffOccurrenceId) {
      toast.error('Warehouse, exact location, due date, Cycle Count occurrence and Year End Close occurrence are required.');
      return;
    }
    setLoading(true);
    try {
      await inventoryManagementService.saveCycleCountSchedule({
        ...form,
        nextDueAtUtc: new Date(form.nextDueAtUtc).toISOString(),
      });
      toast.success('Governed ABC cycle-count schedule saved.');
      setForm(emptyForm);
      await load();
    } catch {
      toast.error('The schedule was rejected by location, calendar, cut-off, role, or concurrency controls.');
      setLoading(false);
    }
  };

  const generate = async () => {
    setLoading(true);
    try {
      const result = await inventoryManagementService.generateDueCycleCounts();
      toast.success(`${result.countsCreated} due governed count(s) generated.`);
      await load();
      onCountsChanged();
    } catch {
      toast.error('Due counts could not be generated for the current assignment.');
      setLoading(false);
    }
  };

  return (
    <>
      <Card className="border-blue-200 bg-blue-50/60 dark:border-blue-900 dark:bg-blue-950/20">
        <CardContent className="flex flex-col gap-3 p-4 md:flex-row md:items-center md:justify-between">
          <div className="flex gap-3">
            <ShieldCheck className="mt-0.5 h-5 w-5 text-blue-700" />
            <div>
              <p className="font-semibold">Controlled cycle-count lifecycle</p>
              <p className="text-sm text-muted-foreground">Schedule counts, review variances, and post the completed count. Configured approvals apply before posting.</p>
            </div>
          </div>
          <Button variant="outline" onClick={() => setOpen(true)}><CalendarClock className="mr-2 h-4 w-4" />ABC schedules</Button>
        </CardContent>
      </Card>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-h-[92vh] max-w-6xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>ABC cycle-count schedules</DialogTitle>
            <DialogDescription>Schedules compose the shared procurement calendar. Freeze and blind-count controls are mandatory and cannot be disabled here.</DialogDescription>
          </DialogHeader>
          <div className="grid gap-5 lg:grid-cols-[1.1fr_1.4fr]">
            <div className="grid gap-3 rounded-lg border p-4">
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1"><Label>Warehouse</Label><Select value={form.warehouseId} onValueChange={value => setForm(previous => ({ ...previous, warehouseId: value, locationId: '' }))}><SelectTrigger><SelectValue placeholder="Warehouse" /></SelectTrigger><SelectContent>{warehouses.map(item => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}</SelectContent></Select></div>
                <div className="space-y-1"><Label>Exact location</Label><Select value={form.locationId} onValueChange={value => setForm(previous => ({ ...previous, locationId: value }))}><SelectTrigger><SelectValue placeholder="Location" /></SelectTrigger><SelectContent>{locations.filter(item => item.isActive).map(item => <SelectItem key={item.id} value={item.id}>{item.locationCode}</SelectItem>)}</SelectContent></Select></div>
                <div className="space-y-1"><Label>ABC class</Label><Select value={form.abcClass} onValueChange={value => setForm(previous => ({ ...previous, abcClass: value as 'A' | 'B' | 'C' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{['A', 'B', 'C'].map(value => <SelectItem key={value} value={value}>{value}</SelectItem>)}</SelectContent></Select></div>
                <div className="space-y-1"><Label>Frequency (days)</Label><Input type="number" min={1} max={366} value={form.frequencyDays} onChange={event => setForm(previous => ({ ...previous, frequencyDays: Number(event.target.value) }))} /></div>
                <div className="col-span-2 space-y-1"><Label>Next due</Label><Input type="datetime-local" value={form.nextDueAtUtc} onChange={event => setForm(previous => ({ ...previous, nextDueAtUtc: event.target.value }))} /></div>
                <div className="col-span-2 space-y-1"><Label>Cycle Count occurrence</Label><Select value={form.calendarOccurrenceId} onValueChange={value => setForm(previous => ({ ...previous, calendarOccurrenceId: value }))}><SelectTrigger><SelectValue placeholder="Shared calendar occurrence" /></SelectTrigger><SelectContent>{cycleOccurrences.map(item => <SelectItem key={item.id} value={item.id}>{item.title} · {new Date(item.dueAtUtc).toLocaleDateString()}</SelectItem>)}</SelectContent></Select></div>
                <div className="col-span-2 space-y-1"><Label>Year End Close cut-off</Label><Select value={form.cutoffOccurrenceId} onValueChange={value => setForm(previous => ({ ...previous, cutoffOccurrenceId: value }))}><SelectTrigger><SelectValue placeholder="Shared cut-off occurrence" /></SelectTrigger><SelectContent>{cutoffOccurrences.map(item => <SelectItem key={item.id} value={item.id}>{item.title} · {new Date(item.dueAtUtc).toLocaleDateString()}</SelectItem>)}</SelectContent></Select></div>
              </div>
              <Textarea placeholder="Schedule notes" value={form.notes} onChange={event => setForm(previous => ({ ...previous, notes: event.target.value }))} />
              <div className="flex items-center justify-between"><div className="flex gap-2"><Badge>Freeze required</Badge><Badge>Blind required</Badge></div><Button onClick={save} disabled={loading}>Save schedule</Button></div>
            </div>
            <div className="rounded-lg border">
              <Table>
                <TableHeader><TableRow><TableHead>Scope</TableHead><TableHead>Class</TableHead><TableHead>Next due</TableHead><TableHead>Cut-off</TableHead><TableHead>Status</TableHead></TableRow></TableHeader>
                <TableBody>{schedules.map(item => <TableRow key={item.id}><TableCell>{warehouseNames.get(item.warehouseId) ?? item.warehouseId}<div className="text-xs text-muted-foreground">{locationNames.get(item.locationId) ?? item.locationId}</div></TableCell><TableCell><Badge variant="secondary">{item.abcClass}</Badge></TableCell><TableCell>{new Date(item.nextDueAtUtc).toLocaleString()}</TableCell><TableCell>{new Date(item.cutoffAtUtc).toLocaleString()}</TableCell><TableCell>{item.isActive ? 'Active' : 'Closed'}</TableCell></TableRow>)}</TableBody>
              </Table>
              {!loading && schedules.length === 0 && <p className="p-6 text-center text-sm text-muted-foreground">No governed schedules are visible for the current location assignment.</p>}
            </div>
          </div>
          <DialogFooter className="justify-between"><Button variant="outline" onClick={generate} disabled={loading}><RefreshCw className={`mr-2 h-4 w-4 ${loading ? 'animate-spin' : ''}`} />Generate due counts</Button><Button variant="outline" onClick={() => setOpen(false)}>Close</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
