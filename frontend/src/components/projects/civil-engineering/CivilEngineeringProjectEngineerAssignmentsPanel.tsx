'use client';

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { CalendarClock, History, RefreshCw, UserCheck, UserRoundPlus, XCircle } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringSupervisionService } from '@/services/civil-engineering-supervision.service';
import type {
  CivilEngineeringProjectEngineerAssignment,
  CivilEngineeringProjectEngineerAssignmentLookups,
  CivilEngineeringProjectEngineerAssignmentRevision,
  CivilEngineeringProjectEngineerAuthority,
} from '@/types/civil-engineering-supervision';

const today = () => new Date().toISOString().slice(0, 10);

const authorityLabels: Record<CivilEngineeringProjectEngineerAuthority, string> = {
  SiteSupervision: 'Site supervision',
  SiteSupervisionAndInstructions: 'Site supervision and instructions',
  FullProjectEngineer: 'Full Project Engineer authority',
};

const formatDate = (value?: string | null) =>
  value
    ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(new Date(value))
    : 'Open-ended';

const toUtcDate = (value: string) => new Date(`${value}T00:00:00.000Z`).toISOString();

const errorMessage = (error: unknown, fallback: string) => {
  const value = error as {
    response?: { detail?: string; correlationId?: string };
    message?: string;
  };
  const detail = value.response?.detail || value.message || fallback;
  return `${detail}${value.response?.correlationId ? ` Reference: ${value.response.correlationId}` : ''}`;
};

type Props = { projectId: string };

export function CivilEngineeringProjectEngineerAssignmentsPanel({ projectId }: Props) {
  const { toast } = useToast();
  const [assignments, setAssignments] = useState<CivilEngineeringProjectEngineerAssignment[]>([]);
  const [lookups, setLookups] = useState<CivilEngineeringProjectEngineerAssignmentLookups>();
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [selectedUserId, setSelectedUserId] = useState('');
  const [authority, setAuthority] = useState<CivilEngineeringProjectEngineerAuthority>('SiteSupervisionAndInstructions');
  const [effectiveFrom, setEffectiveFrom] = useState(today());
  const [replacementReason, setReplacementReason] = useState('');
  const [endTarget, setEndTarget] = useState<CivilEngineeringProjectEngineerAssignment>();
  const [endDate, setEndDate] = useState(today());
  const [endReason, setEndReason] = useState('');
  const [history, setHistory] = useState<CivilEngineeringProjectEngineerAssignmentRevision[]>([]);
  const [historyTarget, setHistoryTarget] = useState<string>();

  const activeAssignment = useMemo(
    () => assignments.find((item) => item.isActive),
    [assignments]
  );

  const load = useCallback(async () => {
    setIsLoading(true);
    const [assignmentResult, lookupResult] = await Promise.allSettled([
      civilEngineeringSupervisionService.listProjectEngineerAssignments(projectId),
      civilEngineeringSupervisionService.projectEngineerAssignmentLookups(projectId),
    ]);

    if (assignmentResult.status === 'fulfilled') {
      setAssignments(assignmentResult.value);
    } else {
      toast({
        variant: 'destructive',
        title: 'Project Engineer appointments could not be loaded',
        description: errorMessage(assignmentResult.reason, 'Refresh the project workspace and try again.'),
      });
    }

    if (lookupResult.status === 'fulfilled') {
      setLookups(lookupResult.value);
      setAuthority(lookupResult.value.defaultAuthority);
      setSelectedUserId((current) => current || lookupResult.value.candidates[0]?.userId || '');
    } else {
      // Workspace readers may review appointments but cannot obtain the management-only selectors.
      setLookups(undefined);
    }
    setIsLoading(false);
  }, [projectId, toast]);

  useEffect(() => { void load(); }, [load]);

  const assign = async () => {
    if (!lookups || !selectedUserId || !effectiveFrom) return;
    if (activeAssignment && replacementReason.trim().length < 5) {
      toast({ variant: 'destructive', title: 'Reason required', description: 'Provide at least five characters for a Project Engineer replacement.' });
      return;
    }
    setIsSaving(true);
    try {
      await civilEngineeringSupervisionService.assignProjectEngineer(projectId, {
        clientRequestId: crypto.randomUUID(),
        assignedUserId: selectedUserId,
        authority,
        effectiveFrom: toUtcDate(effectiveFrom),
        reason: replacementReason.trim() || undefined,
      });
      setReplacementReason('');
      toast({ title: activeAssignment ? 'Project Engineer replaced' : 'Project Engineer appointed', description: 'The appointment and its central audit record were saved.' });
      await load();
    } catch (error) {
      toast({ variant: 'destructive', title: 'Appointment was not saved', description: errorMessage(error, 'The Project Engineer appointment could not be completed.') });
    } finally {
      setIsSaving(false);
    }
  };

  const end = async () => {
    if (!endTarget || !endDate || endReason.trim().length < 5) {
      toast({ variant: 'destructive', title: 'End details required', description: 'Choose an end date and provide a reason of at least five characters.' });
      return;
    }
    setIsSaving(true);
    try {
      await civilEngineeringSupervisionService.endProjectEngineerAssignment(endTarget.id, {
        clientRequestId: crypto.randomUUID(),
        rowVersion: endTarget.rowVersion,
        effectiveTo: toUtcDate(endDate),
        reason: endReason.trim(),
      });
      setEndTarget(undefined);
      setEndReason('');
      toast({ title: 'Project Engineer appointment ended', description: 'The appointment remains in the immutable history.' });
      await load();
    } catch (error) {
      toast({ variant: 'destructive', title: 'Appointment was not ended', description: errorMessage(error, 'The appointment could not be ended.') });
    } finally {
      setIsSaving(false);
    }
  };

  const showHistory = async (assignmentId: string) => {
    try {
      const values = await civilEngineeringSupervisionService.projectEngineerAssignmentHistory(assignmentId);
      setHistory(values);
      setHistoryTarget(assignmentId);
    } catch (error) {
      toast({ variant: 'destructive', title: 'Appointment history could not be loaded', description: errorMessage(error, 'The audit history is unavailable.') });
    }
  };

  return (
    <Card className="border-slate-200/70 shadow-sm">
      <CardHeader className="gap-3 pb-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <CardTitle className="flex items-center gap-2 text-base"><UserCheck className="h-4 w-4 text-blue-600" />Project Engineer appointment</CardTitle>
          <CardDescription>Assign an eligible Civil Engineer or Supervising Civil Engineer to supervise this project. Appointment authority and dates are governed by the effective Civil supervision policy.</CardDescription>
        </div>
        <Button variant="outline" size="sm" onClick={() => void load()} disabled={isLoading || isSaving}>
          <RefreshCw className="mr-2 h-4 w-4" />Refresh
        </Button>
      </CardHeader>
      <CardContent className="space-y-4">
        {isLoading ? <div className="text-sm text-slate-500">Loading Project Engineer appointments…</div> : null}
        {!isLoading && assignments.length === 0 ? <div className="rounded-lg border border-dashed border-slate-300 bg-slate-50 px-4 py-3 text-sm text-slate-600">No Project Engineer is appointed yet.</div> : null}
        {assignments.map((assignment) => (
          <div key={assignment.id} className="rounded-lg border border-slate-200 bg-white p-4">
            <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
              <div className="space-y-1">
                <div className="flex flex-wrap items-center gap-2"><span className="font-medium text-slate-900">{assignment.assignedUserName}</span><Badge variant={assignment.isActive ? 'default' : 'secondary'}>{assignment.isActive ? 'Active' : 'Ended'}</Badge><Badge variant="outline">{authorityLabels[assignment.authority]}</Badge></div>
                <div className="text-sm text-slate-600">{assignment.sourceCivilRole} · {formatDate(assignment.effectiveFrom)} to {formatDate(assignment.effectiveTo)}</div>
                {assignment.reason ? <p className="text-xs text-slate-500">Reason: {assignment.reason}</p> : null}
              </div>
              <div className="flex flex-wrap gap-2">
                <Button variant="outline" size="sm" onClick={() => void showHistory(assignment.id)}><History className="mr-2 h-4 w-4" />History</Button>
                {assignment.isActive && lookups ? <Button variant="outline" size="sm" onClick={() => { setEndTarget(assignment); setEndDate(today()); setEndReason(''); }}><XCircle className="mr-2 h-4 w-4" />End</Button> : null}
              </div>
            </div>
            {historyTarget === assignment.id ? <div className="mt-3 space-y-2 border-t border-slate-100 pt-3 text-xs text-slate-600">{history.length === 0 ? 'No appointment revisions were recorded.' : history.map((revision) => <div key={revision.id} className="rounded bg-slate-50 px-3 py-2"><span className="font-medium text-slate-800">{revision.action}</span> · {revision.actorName} · {formatDate(revision.timestamp)}{revision.reason ? ` · ${revision.reason}` : ''}</div>)}</div> : null}
          </div>
        ))}

        {lookups ? <div className="rounded-lg border border-blue-100 bg-blue-50/40 p-4">
          <div className="mb-3 flex items-center gap-2 text-sm font-medium text-slate-900"><UserRoundPlus className="h-4 w-4 text-blue-600" />{activeAssignment ? 'Replace active Project Engineer' : 'Appoint Project Engineer'}</div>
          <div className="grid gap-4 md:grid-cols-3">
            <div className="space-y-2"><Label>Eligible engineer</Label><Select value={selectedUserId} onValueChange={setSelectedUserId}><SelectTrigger><SelectValue placeholder="Select an eligible project member" /></SelectTrigger><SelectContent>{lookups.candidates.map((candidate) => <SelectItem key={candidate.userId} value={candidate.userId}>{candidate.displayName} · {candidate.sourceCivilRole}</SelectItem>)}</SelectContent></Select></div>
            <div className="space-y-2"><Label>Appointment authority</Label><Select value={authority} onValueChange={(value) => setAuthority(value as CivilEngineeringProjectEngineerAuthority)}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{lookups.authorities.map((value) => <SelectItem key={value} value={value}>{authorityLabels[value]}</SelectItem>)}</SelectContent></Select></div>
            <div className="space-y-2"><Label>Effective from</Label><Input type="date" value={effectiveFrom} onChange={(event) => setEffectiveFrom(event.target.value)} /></div>
          </div>
          {activeAssignment ? <div className="mt-4 space-y-2"><Label>Replacement reason</Label><Textarea value={replacementReason} onChange={(event) => setReplacementReason(event.target.value)} rows={2} placeholder="Explain why the active Project Engineer is being replaced." /></div> : null}
          <div className="mt-4 flex justify-end"><Button onClick={() => void assign()} disabled={isSaving || !selectedUserId || !effectiveFrom}><CalendarClock className="mr-2 h-4 w-4" />{activeAssignment ? 'Replace Project Engineer' : 'Appoint Project Engineer'}</Button></div>
        </div> : <div className="rounded-lg border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-600">You can view appointment history for this project. Appointment controls require the assigned Head of Civil Engineering or configured Projects Coordinator role.</div>}

        {endTarget ? <div className="rounded-lg border border-amber-200 bg-amber-50 p-4"><div className="mb-3 text-sm font-medium text-slate-900">End {endTarget.assignedUserName}'s appointment</div><div className="grid gap-4 md:grid-cols-[180px_1fr]"><div className="space-y-2"><Label>Effective to</Label><Input type="date" min={endTarget.effectiveFrom.slice(0, 10)} value={endDate} onChange={(event) => setEndDate(event.target.value)} /></div><div className="space-y-2"><Label>Reason</Label><Textarea value={endReason} onChange={(event) => setEndReason(event.target.value)} rows={2} placeholder="Explain why this appointment is ending." /></div></div><div className="mt-4 flex justify-end gap-2"><Button variant="outline" onClick={() => setEndTarget(undefined)} disabled={isSaving}>Cancel</Button><Button variant="destructive" onClick={() => void end()} disabled={isSaving}>End appointment</Button></div></div> : null}
      </CardContent>
    </Card>
  );
}
