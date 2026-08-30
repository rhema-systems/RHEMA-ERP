'use client';

import { useCallback, useEffect, useState } from 'react';
import { MessageSquare, RefreshCw, ShieldX } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringComplaintResolutionService } from '@/services/civil-engineering-complaint-resolution.service';
import type { CivilEngineeringComplaintResolution } from '@/types/civil-engineering-complaint-resolution';

const readPermission = 'civil-engineering.workspace.read';
const date = (value: string) => new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
const errorText = (error: unknown) => {
  const value = error as { response?: { detail?: string; correlationId?: string }; message?: string };
  return `${value.response?.detail || value.message || 'Refresh and try again.'}${value.response?.correlationId ? ` Reference: ${value.response.correlationId}` : ''}`;
};

export default function CivilEngineeringComplaintResolutionsPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canRead = hasPermission(readPermission);
  const [items, setItems] = useState<CivilEngineeringComplaintResolution[]>([]);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    if (!canRead) { setLoading(false); return; }
    setLoading(true);
    try {
      setItems(await civilEngineeringComplaintResolutionService.list());
    } catch (error) {
      toast({ variant: 'destructive', title: 'Complaint resolution register could not be loaded', description: errorText(error) });
    } finally {
      setLoading(false);
    }
  }, [canRead, toast]);

  useEffect(() => { void load(); }, [load]);

  if (!canRead) return <Alert><ShieldX className="h-4 w-4" /><AlertTitle>Civil workspace access required</AlertTitle><AlertDescription>You do not have access to Civil complaint-resolution records.</AlertDescription></Alert>;

  return <div className="space-y-5">
    <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><h1 className="flex items-center gap-2 text-2xl font-semibold"><MessageSquare className="h-6 w-6" />Company-building complaint resolution</h1><p className="mt-1 text-sm text-muted-foreground">A read-only Civil view of each Helpdesk complaint’s linked assessment, remedy, execution, inspection and closure progress.</p></div><Button variant="outline" size="sm" onClick={() => void load()} disabled={loading}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button></div>
    <Alert><AlertTitle>Shared owner lifecycle</AlertTitle><AlertDescription>Helpdesk remains responsible for the complaint and requester closure. Maintenance owns job cards/work orders, Finance owns posting, and this view only projects their governed Civil links.</AlertDescription></Alert>
    <Card><CardHeader><CardTitle className="text-base">Complaint resolution register</CardTitle><CardDescription>Only Civil-linked company-asset complaints inside your assigned scope are shown.</CardDescription></CardHeader><CardContent className="space-y-3">{loading ? <p className="text-sm text-muted-foreground">Loading complaint resolution records…</p> : null}{!loading && !items.length ? <p className="rounded border border-dashed p-3 text-sm text-muted-foreground">No company-building complaint is linked to your Civil scope.</p> : null}{items.map((item) => <article key={item.helpdeskTicketId} className="rounded-lg border p-4"><div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between"><div className="min-w-0"><div className="flex flex-wrap items-center gap-2"><span className="font-medium">{item.complaintTicketNumber}</span><Badge variant="outline">Helpdesk: {item.helpdeskStatus}</Badge><Badge variant={item.readyForHelpdeskResolution ? 'default' : 'secondary'}>{item.resolutionStage}</Badge></div><p className="mt-2 font-medium break-words">{item.complaintSubject || 'Company-building complaint'}</p><p className="mt-1 text-sm text-muted-foreground">{item.resolutionLabel}</p></div><p className="shrink-0 text-xs text-muted-foreground">Updated {date(item.lastActivityAt)}</p></div><div className="mt-4 grid gap-2 border-t pt-3 text-sm sm:grid-cols-2 xl:grid-cols-4"><p><span className="text-muted-foreground">Civil intake:</span> {item.intakeNumber} · {item.intakeStatus}</p><p><span className="text-muted-foreground">Assessment:</span> {item.assessmentStage || 'Awaiting assignment'}</p><p><span className="text-muted-foreground">Costing / award:</span> {item.costingStage || 'Not started'}</p><p><span className="text-muted-foreground">Execution:</span> {item.workOrderNumber ? `${item.workOrderNumber} · ${item.workOrderStatus || 'Unknown'}` : item.executionStage || 'Not started'}</p><p><span className="text-muted-foreground">Completion:</span> {item.completionStage || 'Not submitted'}</p><p><span className="text-muted-foreground">Inspection:</span> {item.inspectionStatus || 'Not directed'}</p><p><span className="text-muted-foreground">Finance direction:</span> {item.paymentDirectionStatus || 'Not directed'}</p><p><span className="text-muted-foreground">Helpdesk action:</span> {item.readyForHelpdeskResolution ? 'Civil outcome ready for independent Helpdesk resolution' : 'Await Civil lifecycle outcome'}</p></div></article>)}</CardContent></Card>
  </div>;
}
