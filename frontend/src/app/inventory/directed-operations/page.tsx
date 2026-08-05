'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { AxiosError } from 'axios';
import { ArrowRight, CheckCircle2, History, Loader2, PackageCheck, Play, RefreshCw, Route, XCircle } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { inventoryManagementService, type WarehouseDto } from '@/services/inventoryManagementService';
import {
  inventoryDirectedOperationService, type DirectedAssignee, type DirectedSuggestion, type DirectedTask, type DirectedTaskType,
} from '@/services/inventoryDirectedOperationService';

type Problem = { detail?: string; message?: string; title?: string };
const messageFrom = (error: unknown, fallback: string) => {
  const value = (error as AxiosError<Problem>)?.response?.data;
  return value?.detail || value?.message || value?.title || (error instanceof Error ? error.message : fallback);
};
const terminal = (task: DirectedTask) => task.status === 'Completed' || task.status === 'Cancelled';

export default function DirectedWarehouseOperationsPage() {
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [users, setUsers] = useState<DirectedAssignee[]>([]);
  const [warehouseId, setWarehouseId] = useState('');
  const [type, setType] = useState<'all' | DirectedTaskType>('all');
  const [assigneeId, setAssigneeId] = useState('self');
  const [suggestions, setSuggestions] = useState<DirectedSuggestion[]>([]);
  const [tasks, setTasks] = useState<DirectedTask[]>([]);
  const [comment, setComment] = useState('Warehouse quantity and bin placement physically verified.');
  const [lotNumber, setLotNumber] = useState('');
  const [batchNumber, setBatchNumber] = useState('');
  const [serialNumber, setSerialNumber] = useState('');
  const [loading, setLoading] = useState(true);
  const [workingId, setWorkingId] = useState('');

  const refresh = useCallback(async (selectedWarehouse = warehouseId) => {
    if (!selectedWarehouse) return;
    setLoading(true);
    try {
      const [nextSuggestions, nextTasks, nextUsers] = await Promise.all([
        inventoryDirectedOperationService.suggestions(selectedWarehouse, type === 'all' ? undefined : type),
        inventoryDirectedOperationService.tasks(selectedWarehouse),
        inventoryDirectedOperationService.assignees(selectedWarehouse, type === 'all' ? undefined : type),
      ]);
      setSuggestions(nextSuggestions);
      setTasks(nextTasks);
      setUsers(nextUsers);
      setAssigneeId(current => current !== 'self' && !nextUsers.some(value => value.userId === current) ? 'self' : current);
    } catch (error) { toast.error(messageFrom(error, 'Unable to load directed warehouse work.')); }
    finally { setLoading(false); }
  }, [type, warehouseId]);

  useEffect(() => {
    void inventoryManagementService.getWarehouses(true)
      .then(nextWarehouses => {
        setWarehouses(nextWarehouses);
        const selected = nextWarehouses.find(value => value.isDefault)?.id || nextWarehouses[0]?.id || '';
        setWarehouseId(selected);
        if (!selected) setLoading(false);
      }).catch(error => { setLoading(false); toast.error(messageFrom(error, 'Unable to load warehouse controls.')); });
  }, []);
  useEffect(() => { if (warehouseId) void refresh(); }, [type, warehouseId, refresh]);

  const execute = async (id: string, operation: () => Promise<unknown>, success: string) => {
    setWorkingId(id);
    try { await operation(); toast.success(success); await refresh(); }
    catch (error) { toast.error(messageFrom(error, 'The directed operation could not be completed.')); }
    finally { setWorkingId(''); }
  };

  const activeTasks = useMemo(() => tasks.filter(value => !terminal(value)), [tasks]);
  const history = useMemo(() => tasks.filter(terminal), [tasks]);
  const statusVariant = (status: DirectedTask['status']) => status === 'Completed' ? 'secondary' : status === 'Cancelled' ? 'destructive' : 'outline';

  return <div className="space-y-6 p-6">
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div><h1 className="flex items-center gap-2 text-2xl font-semibold"><Route className="h-6 w-6" />Directed Warehouse Operations</h1><p className="text-sm text-muted-foreground">One assignment and history register over existing receipt, requisition, transfer, capacity, tracking, and location controls.</p></div>
      <Button variant="outline" onClick={() => void refresh()} disabled={loading || !warehouseId}>{loading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-2 h-4 w-4" />}Refresh</Button>
    </div>

    <Card><CardContent className="grid gap-4 pt-6 md:grid-cols-4">
      <div className="space-y-2"><Label>Warehouse scope</Label><Select value={warehouseId} onValueChange={setWarehouseId}><SelectTrigger><SelectValue placeholder="Select warehouse" /></SelectTrigger><SelectContent>{warehouses.map(value => <SelectItem key={value.id} value={value.id}>{value.code} · {value.name}</SelectItem>)}</SelectContent></Select></div>
      <div className="space-y-2"><Label>Work type</Label><Select value={type} onValueChange={value => setType(value as typeof type)}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">All work types</SelectItem><SelectItem value="PutAway">Put-away</SelectItem><SelectItem value="Picking">Picking</SelectItem><SelectItem value="Replenishment">Replenishment</SelectItem></SelectContent></Select></div>
      <div className="space-y-2"><Label>Assign new work to</Label><Select value={assigneeId} onValueChange={setAssigneeId}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="self">Myself</SelectItem>{users.map(value => <SelectItem key={value.userId} value={value.userId}>{value.displayName || value.username}</SelectItem>)}</SelectContent></Select></div>
      <div className="flex items-end gap-2"><Badge variant="outline">{suggestions.length} suggestions</Badge><Badge variant="outline">{activeTasks.length} active</Badge><Badge variant="outline">{history.length} terminal</Badge></div>
    </CardContent></Card>

    <Tabs defaultValue="suggestions">
      <TabsList><TabsTrigger value="suggestions">Suggested work</TabsTrigger><TabsTrigger value="active">Assigned work</TabsTrigger><TabsTrigger value="history">History</TabsTrigger></TabsList>
      <TabsContent value="suggestions"><Card><CardHeader><CardTitle>Server-derived work queue</CardTitle><CardDescription>Only currently eligible, capacity-safe suggestions within your exact warehouse and bin authority are shown.</CardDescription></CardHeader><CardContent><WorkTable empty="No eligible directed work in this scope." rows={suggestions.map(value => ({
        key: value.suggestionKey,
        type: value.taskType,
        reference: value.sourceReference,
        item: `${value.itemCode} · ${value.itemName}`,
        route: `${value.sourceLocationCode || 'Receipt'} → ${value.destinationLocationCode || 'Issue destination'}`,
        quantity: value.quantity,
        badge: value.isQuarantine ? 'Quarantine' : value.destinationCapacity?.hasCapacity ? 'Capacity passed' : 'Ready',
        detail: value.explanation,
        action: <Button size="sm" disabled={workingId === value.suggestionKey} onClick={() => void execute(value.suggestionKey,
          () => inventoryDirectedOperationService.create(value, assigneeId === 'self' ? undefined : assigneeId), 'Directed task assigned.')}>
          {workingId === value.suggestionKey ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <PackageCheck className="mr-2 h-4 w-4" />}Assign</Button>,
      }))} /></CardContent></Card></TabsContent>

      <TabsContent value="active" className="space-y-4">
        <Card><CardHeader><CardTitle>Work confirmation</CardTitle><CardDescription>Tracking values are passed to the existing requisition or transfer owner; its effective category rules remain authoritative.</CardDescription></CardHeader><CardContent className="grid gap-3 md:grid-cols-4"><div className="space-y-2 md:col-span-4"><Label>Operator comment</Label><Input value={comment} onChange={event => setComment(event.target.value)} /></div><div className="space-y-2"><Label>Lot</Label><Input value={lotNumber} onChange={event => setLotNumber(event.target.value)} /></div><div className="space-y-2"><Label>Batch</Label><Input value={batchNumber} onChange={event => setBatchNumber(event.target.value)} /></div><div className="space-y-2"><Label>Serial</Label><Input value={serialNumber} onChange={event => setSerialNumber(event.target.value)} /></div></CardContent></Card>
        <Card><CardContent className="pt-6"><div className="overflow-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>Task</TableHead><TableHead>Type / status</TableHead><TableHead>Item</TableHead><TableHead>Route</TableHead><TableHead>Assigned</TableHead><TableHead className="text-right">Actions</TableHead></TableRow></TableHeader><TableBody>{activeTasks.length === 0 ? <TableRow><TableCell colSpan={6} className="h-28 text-center text-muted-foreground">No active directed tasks.</TableCell></TableRow> : activeTasks.map(task => <TableRow key={task.id}><TableCell><div className="font-medium">{task.taskNumber}</div><div className="text-xs text-muted-foreground">{task.sourceReference}</div></TableCell><TableCell><div>{task.taskType}</div><Badge variant="outline">{task.status}</Badge></TableCell><TableCell>{task.itemCode} · {task.itemName}<div className="text-xs text-muted-foreground">Qty {task.quantity}</div></TableCell><TableCell className="whitespace-nowrap">{task.sourceLocationCode || 'Receipt'} <ArrowRight className="inline h-3 w-3" /> {task.destinationLocationCode || 'Issue'}</TableCell><TableCell>{task.assignedToName}</TableCell><TableCell><div className="flex justify-end gap-2">{task.status === 'Assigned' && <Button size="sm" variant="outline" onClick={() => void execute(task.id, () => inventoryDirectedOperationService.start(task, comment), 'Task started.')} disabled={workingId === task.id}><Play className="mr-1 h-3 w-3" />Start</Button>}{task.status !== 'AwaitingStockMove' && <Button size="sm" onClick={() => void execute(task.id, () => inventoryDirectedOperationService.confirm(task, { comment, lotNumber: lotNumber || undefined, batchNumber: batchNumber || undefined, serialNumber: serialNumber || undefined }), task.taskType === 'Replenishment' || task.taskType === 'PutAway' ? 'Warehouse transfer created.' : 'Directed work confirmed.')} disabled={workingId === task.id}><CheckCircle2 className="mr-1 h-3 w-3" />Confirm</Button>}{task.status === 'AwaitingStockMove' && <Button size="sm" onClick={() => void execute(task.id, () => inventoryDirectedOperationService.reconcile(task), 'Transfer outcome reconciled.')} disabled={workingId === task.id}><RefreshCw className="mr-1 h-3 w-3" />Reconcile</Button>}{task.status !== 'AwaitingStockMove' && <Button size="sm" variant="ghost" onClick={() => void execute(task.id, () => inventoryDirectedOperationService.cancel(task, comment), 'Task cancelled.')} disabled={workingId === task.id}><XCircle className="h-3 w-3" /></Button>}</div></TableCell></TableRow>)}</TableBody></Table></div></CardContent></Card>
      </TabsContent>

      <TabsContent value="history"><Card><CardHeader><CardTitle><History className="mr-2 inline h-5 w-5" />Terminal task and action history</CardTitle><CardDescription>Every lifecycle step retains actor, time, correlation, and integrity lineage.</CardDescription></CardHeader><CardContent><div className="space-y-3">{history.length === 0 ? <div className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">No terminal directed tasks in this scope.</div> : history.map(task => <div key={task.id} className="rounded-md border p-4"><div className="flex flex-wrap items-center justify-between gap-2"><div><span className="font-medium">{task.taskNumber}</span> · {task.itemCode} · Qty {task.quantity}</div><Badge variant={statusVariant(task.status)}>{task.status}</Badge></div><div className="mt-1 text-sm text-muted-foreground">{task.sourceReference} · {task.sourceLocationCode || 'Receipt'} → {task.destinationLocationCode || 'Issue'}{task.linkedInventoryTransferId ? ` · Transfer ${task.linkedInventoryTransferId}` : ''}</div><div className="mt-3 grid gap-2 md:grid-cols-2">{task.actions.map(action => <div key={action.id} className="rounded bg-muted/40 p-2 text-xs"><div className="font-medium">{action.sequence}. {action.actionType} · {action.actorName}</div><div>{new Date(action.occurredAtUtc).toLocaleString()} · {action.comment}</div><div className="truncate font-mono text-muted-foreground">{action.integrityHash}</div></div>)}</div></div>)}</div></CardContent></Card></TabsContent>
    </Tabs>
  </div>;
}

type WorkRow = { key: string; type: string; reference: string; item: string; route: string; quantity: number; badge: string; detail: string; action: React.ReactNode };
function WorkTable({ rows, empty }: { rows: WorkRow[]; empty: string }) {
  return <div className="overflow-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>Type / source</TableHead><TableHead>Item</TableHead><TableHead>Directed route</TableHead><TableHead>Quantity</TableHead><TableHead>Control</TableHead><TableHead className="text-right">Action</TableHead></TableRow></TableHeader><TableBody>{rows.length === 0 ? <TableRow><TableCell colSpan={6} className="h-28 text-center text-muted-foreground">{empty}</TableCell></TableRow> : rows.map(row => <TableRow key={row.key}><TableCell><div className="font-medium">{row.type}</div><div className="text-xs text-muted-foreground">{row.reference}</div></TableCell><TableCell>{row.item}</TableCell><TableCell><div>{row.route}</div><div className="max-w-md text-xs text-muted-foreground">{row.detail}</div></TableCell><TableCell>{row.quantity}</TableCell><TableCell><Badge variant={row.badge === 'Quarantine' ? 'destructive' : 'secondary'}>{row.badge}</Badge></TableCell><TableCell className="text-right">{row.action}</TableCell></TableRow>)}</TableBody></Table></div>;
}
