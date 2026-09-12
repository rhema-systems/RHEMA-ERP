'use client';

import Link from 'next/link';
import { useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { FileCheck2, Loader2, Plus, RefreshCw, Search, ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { prequalificationStatusLabel, validatePrequalificationDraft } from '@/lib/procurement-prequalification';
import { procurementPrequalificationService as service } from '@/services/procurement-prequalification.service';
import type { CreateProcurementPrequalificationExercise } from '@/types/procurement-prequalification';

const localInput = (value: Date) => {
  const shifted = new Date(value.getTime() - value.getTimezoneOffset() * 60_000);
  return shifted.toISOString().slice(0, 16);
};

const emptyForm = (): CreateProcurementPrequalificationExercise => {
  const opens = new Date(Date.now() + 60 * 60 * 1000);
  const closes = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000);
  return {
    reference: '',
    title: '',
    description: '',
    categoryIds: [],
    opensAtUtc: localInput(opens),
    closesAtUtc: localInput(closes),
    validityMonths: 12,
    passingScore: 70,
    policySetId: '',
    workflowDefinitionId: '',
    criteria: [
      { code: 'LEGAL-COMPLIANCE', name: 'Legal and statutory compliance', weight: 50, minimumScore: 60, isMandatory: true, requiresEvidence: true, sortOrder: 1 },
      { code: 'TECHNICAL-CAPACITY', name: 'Technical and delivery capacity', weight: 50, minimumScore: 60, isMandatory: true, requiresEvidence: true, sortOrder: 2 },
    ],
  };
};

const formatDate = (value?: string) => value ? new Date(value).toLocaleString() : '—';

export default function PrequalificationPage() {
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canManage = hasPermission('procurement.sourcing.manage');
  const [search, setSearch] = useState('');
  const [createOpen, setCreateOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState<CreateProcurementPrequalificationExercise>(emptyForm());

  const exercises = useQuery({ queryKey: ['procurement-prequalification'], queryFn: service.list });
  const readiness = useQuery({ queryKey: ['procurement-prequalification-readiness'], queryFn: service.readiness });
  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase();
    return (exercises.data ?? []).filter((item) =>
      !term || item.reference.toLowerCase().includes(term) || item.title.toLowerCase().includes(term));
  }, [exercises.data, search]);

  const create = async () => {
    if (!readiness.data || readiness.isError || readiness.isFetching) {
      toast.error('Wait for the current sourcing setup to load before saving.');
      return;
    }
    const request = {
      ...form,
      workflowDefinitionId: readiness.data.approvalRequired === false ? null : form.workflowDefinitionId,
      opensAtUtc: new Date(form.opensAtUtc).toISOString(),
      closesAtUtc: new Date(form.closesAtUtc).toISOString(),
    };
    const error = validatePrequalificationDraft(request, readiness.data.approvalRequired !== false);
    if (error) { toast.error(error); return; }
    try {
      setSaving(true);
      const created = await service.create(request);
      toast.success('Prequalification draft created');
      setCreateOpen(false);
      setForm(emptyForm());
      await queryClient.invalidateQueries({ queryKey: ['procurement-prequalification'] });
      window.location.href = `/procurement/prequalification/${created.id}`;
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Unable to create prequalification exercise');
    } finally {
      setSaving(false);
    }
  };

  const updateCriterion = (index: number, field: string, value: string | number | boolean) =>
    setForm((current) => ({
      ...current,
      criteria: current.criteria.map((criterion, criterionIndex) =>
        criterionIndex === index ? { ...criterion, [field]: value } : criterion),
    }));

  return (
    <div className="space-y-6 p-6" data-testid="prequalification-list-page">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Supplier prequalification</h1>
          <p className="text-sm text-muted-foreground">History-first advertisements, evaluations, decisions, and reusable qualified lists.</p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => void Promise.all([exercises.refetch(), readiness.refetch()])}>
            <RefreshCw className="mr-2 h-4 w-4" />Refresh
          </Button>
          {canManage && <Button onClick={() => setCreateOpen(true)}><Plus className="mr-2 h-4 w-4" />New exercise</Button>}
        </div>
      </div>

      <div className="grid gap-3 md:grid-cols-3">
        <Summary label="Exercises" value={String(exercises.data?.length ?? 0)} />
        <Summary label="Open applications" value={String(exercises.data?.filter((item) => item.status === 1).length ?? 0)} />
        <Summary label="Active qualified entries" value={String(exercises.data?.reduce((sum, item) => sum + item.qualifiedCount, 0) ?? 0)} />
      </div>

      <Card>
        <CardHeader className="flex-row items-center justify-between gap-3">
          <CardTitle className="flex items-center gap-2"><ShieldCheck className="h-5 w-5" />Exercise history</CardTitle>
          <div className="relative w-full max-w-sm"><Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" /><Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search reference or title" className="pl-9" /></div>
        </CardHeader>
        <CardContent>
          {exercises.isLoading ? <div className="flex justify-center p-10"><Loader2 className="h-6 w-6 animate-spin" /></div> :
            exercises.isError ? <p className="rounded border border-destructive/30 p-4 text-sm text-destructive">Unable to load prequalification history.</p> :
            filtered.length === 0 ? <div className="p-10 text-center text-sm text-muted-foreground"><FileCheck2 className="mx-auto mb-3 h-8 w-8" />No prequalification exercises match this view.</div> :
            <Table>
              <TableHeader><TableRow><TableHead>Reference</TableHead><TableHead>Title</TableHead><TableHead>Status</TableHead><TableHead>Window</TableHead><TableHead>Applications</TableHead><TableHead>Qualified</TableHead><TableHead /></TableRow></TableHeader>
              <TableBody>{filtered.map((item) => <TableRow key={item.id}>
                <TableCell className="font-medium">{item.reference}</TableCell>
                <TableCell>{item.title}</TableCell>
                <TableCell><Badge variant={item.status === 5 ? 'default' : item.status === 6 || item.status === 7 ? 'destructive' : 'secondary'}>{item.approvalRequired === false && item.status === 5 ? 'Qualification complete' : prequalificationStatusLabel[item.status]}</Badge></TableCell>
                <TableCell className="text-xs">{formatDate(item.opensAtUtc)}<br />to {formatDate(item.closesAtUtc)}</TableCell>
                <TableCell>{item.applicationCount}</TableCell><TableCell>{item.qualifiedCount}</TableCell>
                <TableCell className="text-right"><Button size="sm" variant="outline" asChild><Link href={`/procurement/prequalification/${item.id}`}>Open history</Link></Button></TableCell>
              </TableRow>)}</TableBody>
            </Table>}
        </CardContent>
      </Card>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-h-[90vh] max-w-4xl overflow-y-auto">
          <DialogHeader><DialogTitle>Create prequalification exercise</DialogTitle><DialogDescription>Lock scope, dates, categories, criteria, passing score, validity, and the exact published workflow before advertisement.</DialogDescription></DialogHeader>
          <div className="grid gap-4 md:grid-cols-2">
            <Field label="Reference"><Input value={form.reference} onChange={(event) => setForm({ ...form, reference: event.target.value })} /></Field>
            <Field label="Title"><Input value={form.title} onChange={(event) => setForm({ ...form, title: event.target.value })} /></Field>
            <div className="md:col-span-2"><Field label="Description"><Textarea rows={3} value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} /></Field></div>
            <Field label="Opens at"><Input type="datetime-local" value={form.opensAtUtc} onChange={(event) => setForm({ ...form, opensAtUtc: event.target.value })} /></Field>
            <Field label="Closes at"><Input type="datetime-local" value={form.closesAtUtc} onChange={(event) => setForm({ ...form, closesAtUtc: event.target.value })} /></Field>
            <Field label="Qualified-list validity (months)"><Input type="number" min={1} max={60} value={form.validityMonths} onChange={(event) => setForm({ ...form, validityMonths: Number(event.target.value) })} /></Field>
            <Field label="Overall passing score"><Input type="number" min={1} max={100} value={form.passingScore} onChange={(event) => setForm({ ...form, passingScore: Number(event.target.value) })} /></Field>
            <div className="md:col-span-2"><Field label="Exact current procurement policy"><Select value={form.policySetId} onValueChange={(value) => setForm({ ...form, policySetId: value })}><SelectTrigger><SelectValue placeholder="Select effective policy" /></SelectTrigger><SelectContent>{readiness.data?.policies.map((item) => <SelectItem key={item.id} value={item.id}>{item.code} · {item.name} · v{item.version}</SelectItem>)}</SelectContent></Select></Field></div>
            {readiness.data?.approvalRequired !== false && <div className="md:col-span-2"><Field label="Exact published sourcing workflow"><Select value={form.workflowDefinitionId ?? undefined} onValueChange={(value) => setForm({ ...form, workflowDefinitionId: value })}><SelectTrigger><SelectValue placeholder="Select workflow" /></SelectTrigger><SelectContent>{readiness.data?.workflows.map((item) => <SelectItem key={item.id} value={item.id}>{item.name} · v{item.version}</SelectItem>)}</SelectContent></Select></Field></div>}
            <div className="md:col-span-2 space-y-2"><Label>Advertised categories</Label><div className="grid gap-2 md:grid-cols-2">{readiness.data?.categories.map((item) => <label key={item.id} className="flex items-center gap-2 rounded border p-3 text-sm"><Checkbox checked={form.categoryIds.includes(item.id)} onCheckedChange={(checked) => setForm((current) => ({ ...current, categoryIds: checked ? [...current.categoryIds, item.id] : current.categoryIds.filter((id) => id !== item.id) }))} /><span>{item.code} · {item.name}</span></label>)}</div></div>
          </div>
          <div className="space-y-3">
            <div className="flex items-center justify-between"><Label>Locked evaluation criteria</Label><Button variant="outline" size="sm" onClick={() => setForm((current) => ({ ...current, criteria: [...current.criteria, { code: `CRITERION-${current.criteria.length + 1}`, name: '', weight: 0, minimumScore: 60, isMandatory: false, requiresEvidence: true, sortOrder: current.criteria.length + 1 }] }))}>Add criterion</Button></div>
            {form.criteria.map((criterion, index) => <div key={`${criterion.code}-${index}`} className="grid gap-3 rounded border p-3 md:grid-cols-6">
              <Input value={criterion.code} onChange={(event) => updateCriterion(index, 'code', event.target.value)} placeholder="Code" />
              <Input className="md:col-span-2" value={criterion.name} onChange={(event) => updateCriterion(index, 'name', event.target.value)} placeholder="Criterion name" />
              <Input type="number" value={criterion.weight} onChange={(event) => updateCriterion(index, 'weight', Number(event.target.value))} placeholder="Weight" />
              <Input type="number" value={criterion.minimumScore} onChange={(event) => updateCriterion(index, 'minimumScore', Number(event.target.value))} placeholder="Minimum" />
              <div className="flex flex-col gap-2 text-xs"><label className="flex items-center gap-2"><Checkbox checked={criterion.isMandatory} onCheckedChange={(checked) => updateCriterion(index, 'isMandatory', Boolean(checked))} />Mandatory</label><label className="flex items-center gap-2"><Checkbox checked={criterion.requiresEvidence} onCheckedChange={(checked) => updateCriterion(index, 'requiresEvidence', Boolean(checked))} />Evidence</label></div>
            </div>)}
          </div>
          <DialogFooter><Button variant="outline" onClick={() => setCreateOpen(false)}>Cancel</Button><Button onClick={() => void create()} disabled={saving || readiness.isLoading}>{saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Create controlled draft</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return <div className="space-y-2"><Label>{label}</Label>{children}</div>;
}
function Summary({ label, value }: { label: string; value: string }) {
  return <Card><CardContent className="p-4"><p className="text-xs uppercase text-muted-foreground">{label}</p><p className="mt-1 text-2xl font-semibold">{value}</p></CardContent></Card>;
}
