'use client';

import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { CheckCircle2, ClipboardList, Eye, Loader2, Plus, RefreshCw, Save, ShieldCheck, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { financeDataService } from '@/services/finance/finance-data.service';
import type {
    FinanceCloseTemplate,
    FinanceCloseTemplateTask,
    FinanceCloseType,
    SaveFinanceCloseTemplateVersion,
} from '@/types/finance';

const CLOSE_TYPES: Array<{ value: FinanceCloseType; label: string }> = [
    { value: 'MonthEnd', label: 'Period-end' },
    { value: 'QuarterEnd', label: 'Quarter-end' },
    { value: 'YearEnd', label: 'Year-end' },
];

const displayTemplateName = (template: Pick<FinanceCloseTemplate, 'closeType' | 'name'>) =>
    template.closeType === 'MonthEnd'
        ? template.name.replace(/month-end/gi, 'period-end')
        : template.name;

const NON_WAIVABLE_CHECKS = new Set([
    'POSTING_INTEGRITY',
    'TRIAL_BALANCE',
    'AP_CONTROL_RECONCILIATION',
    'AR_CONTROL_RECONCILIATION',
    'RECURRING_JOURNAL_EXCEPTIONS',
    'FIXED_ASSET_DEPRECIATION',
]);

const withoutId = (task: FinanceCloseTemplateTask): Omit<FinanceCloseTemplateTask, 'id'> => {
    const { id: _id, ...definition } = task;
    return definition;
};

/**
 * Administration for the same approved templates consumed by FiscalPeriodService. Approved rows
 * are never edited: an administrator creates a draft version and a different administrator signs
 * the activation declaration, preserving TDC's maker-checker evidence.
 */
export default function FinanceCloseTemplatesPage() {
    const { toast } = useToast();
    const { hasPermission } = useAuth();
    const canAdminister = hasPermission('Finance.Admin');
    const [templates, setTemplates] = useState<FinanceCloseTemplate[]>([]);
    const [draftId, setDraftId] = useState<string | null>(null);
    const [draft, setDraft] = useState<SaveFinanceCloseTemplateVersion | null>(null);
    const [viewingTemplate, setViewingTemplate] = useState<FinanceCloseTemplate | null>(null);
    const [approvalDeclaration, setApprovalDeclaration] = useState('');
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [draftRevealRequest, setDraftRevealRequest] = useState(0);
    const draftEditorRef = useRef<HTMLDivElement>(null);

    const loadTemplates = useCallback(async () => {
        try {
            setLoading(true);
            setTemplates(await financeDataService.getFinanceCloseTemplates());
        } catch (error: any) {
            toast({ title: 'Unable to load close templates', description: error?.message || 'Template configuration could not be loaded.', variant: 'destructive' });
        } finally {
            setLoading(false);
        }
    }, [toast]);

    useEffect(() => { void loadTemplates(); }, [loadTemplates]);

    useEffect(() => {
        if (draftRevealRequest === 0) return;

        const frame = window.requestAnimationFrame(() => {
            draftEditorRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
            draftEditorRef.current?.focus({ preventScroll: true });
        });

        return () => window.cancelAnimationFrame(frame);
    }, [draftRevealRequest]);

    const activeByType = useMemo(() => new Map(
        CLOSE_TYPES.map(type => [type.value, templates.find(template => template.closeType === type.value && template.isActive)])
    ), [templates]);

    const openDraft = (template: FinanceCloseTemplate, existingDraft: boolean) => {
        setDraftId(existingDraft ? template.id : null);
        setDraft({
            templateCode: template.templateCode,
            name: template.name,
            closeType: template.closeType,
            description: template.description,
            tasks: template.tasks.map(withoutId),
        });
        setApprovalDeclaration('');
        setDraftRevealRequest(current => current + 1);
    };

    const updateTask = (
        index: number,
        patch: Partial<Omit<FinanceCloseTemplateTask, 'id'>>
    ) => {
        setDraft(current => current ? {
            ...current,
            tasks: current.tasks.map((task, taskIndex) => taskIndex === index ? { ...task, ...patch } : task),
        } : current);
    };

    const addManualTask = () => {
        setDraft(current => {
            if (!current) return current;
            const sequence = Math.max(0, ...current.tasks.map(task => task.sequence)) + 10;
            return {
                ...current,
                tasks: [...current.tasks, {
                    taskCode: `MANUAL_${sequence}`,
                    title: 'Manual close task',
                    category: 'Finance Review',
                    sequence,
                    isMandatory: true,
                    isAutomated: false,
                    dueDaysAfterPeriodEnd: current.closeType === 'YearEnd' ? 15 : current.closeType === 'QuarterEnd' ? 10 : 5,
                }],
            };
        });
    };

    const removeManualTask = (index: number) => {
        setDraft(current => current ? {
            ...current,
            tasks: current.tasks.filter((_, taskIndex) => taskIndex !== index),
        } : current);
    };

    const saveDraft = async () => {
        if (!draft) return;
        try {
            setSaving(true);
            const saved = draftId
                ? await financeDataService.updateFinanceCloseTemplateDraft(draftId, draft)
                : await financeDataService.createFinanceCloseTemplateVersion(draft);
            await loadTemplates();
            openDraft(saved, true);
            toast({ title: 'Draft version saved', description: `${saved.templateCode} v${saved.version} is awaiting independent approval.` });
        } catch (error: any) {
            toast({ title: 'Draft could not be saved', description: error?.message || 'Review the template fields and dependencies.', variant: 'destructive' });
        } finally {
            setSaving(false);
        }
    };

    const approveDraft = async () => {
        if (!draftId || approvalDeclaration.trim().length < 20) {
            toast({ title: 'Approval declaration required', description: 'Enter an independent review declaration of at least 20 characters.', variant: 'destructive' });
            return;
        }

        try {
            setSaving(true);
            const approved = await financeDataService.approveFinanceCloseTemplate(draftId, approvalDeclaration.trim());
            setDraft(null);
            setDraftId(null);
            setApprovalDeclaration('');
            await loadTemplates();
            toast({ title: 'Close template activated', description: `${approved.name} v${approved.version} now governs new ${approved.closeType} cycles.` });
        } catch (error: any) {
            toast({ title: 'Approval failed', description: error?.message || 'A different Finance administrator must approve this version.', variant: 'destructive' });
        } finally {
            setSaving(false);
        }
    };

    const selectedTemplate = draftId ? templates.find(template => template.id === draftId) : undefined;

    return (
        <div className="space-y-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                    <h1 className="flex items-center gap-2 text-3xl font-bold tracking-tight">
                        <ClipboardList className="h-8 w-8" /> Finance Close Templates
                    </h1>
                    <p className="text-muted-foreground">Versioned period-, quarter-, and year-end controls for new close cycles.</p>
                </div>
                <Button variant="outline" onClick={() => void loadTemplates()} disabled={loading}>
                    <RefreshCw className={`mr-2 h-4 w-4 ${loading ? 'animate-spin' : ''}`} /> Refresh
                </Button>
            </div>

            <div className="grid gap-4 lg:grid-cols-3">
                {CLOSE_TYPES.map(type => {
                    const active = activeByType.get(type.value);
                    return (
                        <Card key={type.value}>
                            <CardHeader>
                                <div className="flex items-center justify-between gap-2">
                                    <CardTitle className="text-lg">{type.label}</CardTitle>
                                    <Badge variant={active ? 'default' : 'destructive'}>{active ? 'Active' : 'Missing'}</Badge>
                                </div>
                                <CardDescription>{active ? displayTemplateName(active) + ' · ' + active.templateCode + ' v' + active.version : 'No approved template'}</CardDescription>
                            </CardHeader>
                            <CardContent>
                                {active && canAdminister ? (
                                    <Button className="w-full" variant="outline" onClick={() => openDraft(active, false)}>
                                        <Plus className="mr-2 h-4 w-4" /> Create next version
                                    </Button>
                                ) : null}
                            </CardContent>
                        </Card>
                    );
                })}
            </div>

            <Card>
                <CardHeader>
                    <CardTitle>Version history</CardTitle>
                    <CardDescription>Superseded versions remain available for audit and historical cycle references.</CardDescription>
                </CardHeader>
                <CardContent className="space-y-2">
                    {loading ? <div className="flex justify-center py-8"><Loader2 className="h-5 w-5 animate-spin" /></div> : templates.map(template => (
                        <div key={template.id} className="flex flex-wrap items-center gap-2 rounded-md border p-3 text-sm">
                            <span className="font-medium">{displayTemplateName(template)}</span>
                            <Badge variant="outline">{template.closeType}</Badge>
                            <Badge variant="outline">v{template.version}</Badge>
                            <Badge variant={template.isActive ? 'default' : 'secondary'}>{template.status}</Badge>
                            <span className="text-muted-foreground">{template.tasks.length} tasks</span>
                            <div className="ml-auto flex gap-2">
                                <Button size="sm" variant="outline" onClick={() => setViewingTemplate(template)}>
                                    <Eye className="mr-1 h-4 w-4" /> View steps
                                </Button>
                                {template.status === 'Draft' && canAdminister ? (
                                    <Button size="sm" variant="outline" onClick={() => openDraft(template, true)}>Edit draft</Button>
                                ) : null}
                            </div>
                        </div>
                    ))}
                </CardContent>
            </Card>

            <Dialog open={Boolean(viewingTemplate)} onOpenChange={open => { if (!open) setViewingTemplate(null); }}>
                <DialogContent className="max-h-[90vh] max-w-4xl overflow-y-auto">
                    <DialogHeader>
                        <DialogTitle>{viewingTemplate ? displayTemplateName(viewingTemplate) : ''} v{viewingTemplate?.version}</DialogTitle>
                        <DialogDescription>
                            Read-only control steps retained for this {viewingTemplate?.status.toLowerCase()} template version.
                        </DialogDescription>
                    </DialogHeader>
                    <div className="space-y-3">
                        {viewingTemplate?.tasks
                            .slice()
                            .sort((left, right) => left.sequence - right.sequence)
                            .map(task => (
                                <div key={task.id || task.taskCode} className="rounded-md border p-3">
                                    <div className="flex flex-wrap items-start justify-between gap-2">
                                        <div>
                                            <p className="font-medium">{task.sequence}. {task.title}</p>
                                            <p className="text-sm text-muted-foreground">{task.taskCode} · {task.category}</p>
                                        </div>
                                        <div className="flex flex-wrap gap-2">
                                            <Badge variant={task.isMandatory ? 'default' : 'secondary'}>{task.isMandatory ? 'Mandatory' : 'Warning'}</Badge>
                                            <Badge variant="outline">{task.isAutomated ? task.checkCode : 'Manual'}</Badge>
                                            <Badge variant="outline">Due {task.dueDaysAfterPeriodEnd >= 0 ? '+' : ''}{task.dueDaysAfterPeriodEnd} day(s)</Badge>
                                        </div>
                                    </div>
                                    {task.dependsOnTaskCode ? <p className="mt-2 text-sm">Depends on: <span className="font-medium">{task.dependsOnTaskCode}</span></p> : null}
                                    {task.instructions ? <p className="mt-2 text-sm text-muted-foreground">{task.instructions}</p> : null}
                                </div>
                            ))}
                    </div>
                </DialogContent>
            </Dialog>

            {draft ? (
                <Card ref={draftEditorRef} tabIndex={-1} className="scroll-mt-6 outline-none">
                    <CardHeader>
                        <CardTitle>{draftId ? 'Edit draft version' : 'Create next draft version'}</CardTitle>
                        <CardDescription>Automated check codes remain tied to the existing Finance providers; manual tasks are completed with retained evidence in the close workspace.</CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-5">
                        <div className="rounded-md border border-blue-200 bg-blue-50/50 p-3" role="status">
                            <p className="font-medium">{draftId ? 'Editing saved draft' : 'Unsaved next version'}</p>
                            <p className="text-sm text-muted-foreground">
                                {draftId
                                    ? 'Changes remain in this draft until you save them. Activation still requires an independent Finance administrator.'
                                    : 'This is a working copy of the active template. No new version is created until you select Save draft.'}
                            </p>
                        </div>
                        <div className="grid gap-4 md:grid-cols-3">
                            <div><Label>Template code</Label><Input value={draft.templateCode} disabled /></div>
                            <div className="md:col-span-2"><Label>Name</Label><Input value={draft.name} onChange={event => setDraft({ ...draft, name: event.target.value })} maxLength={160} /></div>
                            <div className="md:col-span-3"><Label>Description</Label><Textarea value={draft.description || ''} onChange={event => setDraft({ ...draft, description: event.target.value })} maxLength={2000} rows={2} /></div>
                        </div>

                        <div className="space-y-3">
                            <div className="flex items-center justify-between gap-2">
                                <div><h2 className="font-semibold">Task definitions</h2><p className="text-sm text-muted-foreground">Dependencies use task codes and must not form a cycle.</p></div>
                                <Button variant="outline" size="sm" onClick={addManualTask}><Plus className="mr-2 h-4 w-4" /> Add manual task</Button>
                            </div>
                            {draft.tasks.map((task, index) => {
                                const lockedMandatory = Boolean(task.checkCode && NON_WAIVABLE_CHECKS.has(task.checkCode));
                                const certification = task.taskCode === 'PREPARER_CERTIFICATION';
                                return (
                                    <div key={`${task.taskCode}-${index}`} className="grid gap-3 rounded-md border p-3 md:grid-cols-12">
                                        <div className="md:col-span-3"><Label>Task code</Label><Input value={task.taskCode} disabled={task.isAutomated || certification} onChange={event => updateTask(index, { taskCode: event.target.value.toUpperCase() })} /></div>
                                        <div className="md:col-span-5"><Label>Title</Label><Input value={task.title} onChange={event => updateTask(index, { title: event.target.value })} /></div>
                                        <div className="md:col-span-2"><Label>Sequence</Label><Input type="number" value={task.sequence} onChange={event => updateTask(index, { sequence: Number(event.target.value) })} /></div>
                                        <div className="md:col-span-2"><Label>Due offset</Label><Input type="number" value={task.dueDaysAfterPeriodEnd} onChange={event => updateTask(index, { dueDaysAfterPeriodEnd: Number(event.target.value) })} /></div>
                                        <div className="md:col-span-3"><Label>Category</Label><Input value={task.category} onChange={event => updateTask(index, { category: event.target.value })} /></div>
                                        <div className="md:col-span-3"><Label>Depends on</Label><Input value={task.dependsOnTaskCode || ''} onChange={event => updateTask(index, { dependsOnTaskCode: event.target.value.toUpperCase() || undefined })} placeholder="TASK_CODE" /></div>
                                        <div className="md:col-span-3"><Label>Check provider</Label><Input value={task.checkCode || 'Manual'} disabled /></div>
                                        <div className="flex items-end gap-3 md:col-span-3">
                                            <div className="flex items-center gap-2 pb-2"><Switch checked={task.isMandatory} disabled={lockedMandatory || certification} onCheckedChange={checked => updateTask(index, { isMandatory: checked })} /><Label>Mandatory</Label></div>
                                            {!task.isAutomated && !certification ? <Button size="icon" variant="ghost" onClick={() => removeManualTask(index)} aria-label="Remove manual task"><Trash2 className="h-4 w-4 text-red-600" /></Button> : null}
                                        </div>
                                        <div className="md:col-span-12"><Label>Instructions</Label><Textarea value={task.instructions || ''} onChange={event => updateTask(index, { instructions: event.target.value || undefined })} rows={2} maxLength={2000} /></div>
                                    </div>
                                );
                            })}
                        </div>

                        <div className="flex flex-wrap gap-2">
                            <Button onClick={() => void saveDraft()} disabled={saving}>{saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />} Save draft</Button>
                            <Button variant="outline" onClick={() => { setDraft(null); setDraftId(null); }}>Cancel</Button>
                        </div>

                        {draftId ? (
                            <div className="space-y-3 rounded-md border border-blue-200 bg-blue-50/50 p-4">
                                <div className="flex items-start gap-2"><ShieldCheck className="mt-0.5 h-5 w-5 text-blue-700" /><div><h2 className="font-semibold">Independent activation</h2><p className="text-sm text-muted-foreground">The author cannot approve this version. A second Finance administrator must review the task design.</p></div></div>
                                <Label>Approval declaration</Label>
                                <Textarea value={approvalDeclaration} onChange={event => setApprovalDeclaration(event.target.value)} rows={3} maxLength={2000} placeholder="I independently reviewed this template version and approve it for future TDC close cycles…" />
                                <Button onClick={() => void approveDraft()} disabled={saving || !selectedTemplate?.canApprove}>
                                    <CheckCircle2 className="mr-2 h-4 w-4" /> Approve and activate
                                </Button>
                                {!selectedTemplate?.canApprove ? <p className="text-xs text-muted-foreground">Sign in as a different Finance administrator to activate this draft.</p> : null}
                            </div>
                        ) : null}
                    </CardContent>
                </Card>
            ) : null}
        </div>
    );
}
