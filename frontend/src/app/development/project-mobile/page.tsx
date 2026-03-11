'use client';

import React, { useEffect, useMemo, useState } from 'react';
import { format } from 'date-fns';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Progress } from '@/components/ui/progress';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import {
  CreateProjectExpenseDto,
  CreateProjectTimesheetEntryDto,
  ProjectCatalogEntryDto,
  ProjectMobileSummaryDto,
  UpdateProjectWorkItemProgressDto,
  projectService,
} from '@/services/projectService';

const DEFAULT_TASK_STATUSES = ['Assigned', 'InProgress', 'Blocked', 'PendingReview', 'Completed'];
const DEFAULT_WORK_TYPES = ['Field', 'Standard', 'Travel', 'Support'];
const DEFAULT_EXPENSE_CATEGORIES = ['Travel', 'Supplies', 'Equipment', 'Other'];
const DEFAULT_DOCUMENT_CATEGORIES = ['FieldEvidence', 'Reports', 'General'];
const DEFAULT_DOCUMENT_TYPES = ['MobileEvidence', 'Photo', 'Evidence', 'Attachment'];

const today = () => new Date().toISOString().slice(0, 10);

const formatCatalogLabel = (value: string) =>
  value
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/([A-Z])([A-Z][a-z])/g, '$1 $2')
    .replace(/[-_]/g, ' ');

const resolveCatalogOptions = (entries: ProjectCatalogEntryDto[], fallbackValues: string[], currentValue?: string) => {
  const configured = entries
    .filter((entry) => entry.isActive)
    .map((entry) => entry.name.trim())
    .filter(Boolean);

  const values = configured.length > 0 ? configured : fallbackValues;
  return currentValue && !values.includes(currentValue) ? [currentValue, ...values] : values;
};

const pickPreferredOption = (options: string[], preferences: string[]) =>
  preferences.find((preference) => options.includes(preference)) || options[0] || '';

function getCurrentUserId(): string {
  if (typeof window === 'undefined') return '';
  try {
    const raw = localStorage.getItem('user');
    if (!raw) return '';
    const parsed = JSON.parse(raw);
    return parsed?.id || parsed?.userId || '';
  } catch {
    return '';
  }
}

export default function ProjectMobilePage() {
  const userId = useMemo(() => getCurrentUserId(), []);
  const [summary, setSummary] = useState<ProjectMobileSummaryDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [focusedAssignmentId, setFocusedAssignmentId] = useState<string | null>(null);
  const [progressDrafts, setProgressDrafts] = useState<Record<string, UpdateProjectWorkItemProgressDto>>({});
  const [timesheetDrafts, setTimesheetDrafts] = useState<Record<string, CreateProjectTimesheetEntryDto>>({});
  const [expenseDrafts, setExpenseDrafts] = useState<Record<string, CreateProjectExpenseDto>>({});
  const [evidenceNames, setEvidenceNames] = useState<Record<string, string>>({});
  const [evidenceFiles, setEvidenceFiles] = useState<Record<string, File | null>>({});
  const [uploadingEvidenceFor, setUploadingEvidenceFor] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [showOverdueOnly, setShowOverdueOnly] = useState(false);
  const [taskStatusCatalog, setTaskStatusCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [workTypeCatalog, setWorkTypeCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [expenseCategoryCatalog, setExpenseCategoryCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [documentCategoryCatalog, setDocumentCategoryCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [documentTypeCatalog, setDocumentTypeCatalog] = useState<ProjectCatalogEntryDto[]>([]);

  const load = async () => {
    try {
      setLoading(true);
      const [summaryResult, catalogResults] = await Promise.all([
        projectService.getMobileSummary(),
        Promise.allSettled([
          projectService.getCatalogEntries('task-statuses'),
          projectService.getCatalogEntries('timesheet-work-types'),
          projectService.getCatalogEntries('expense-categories'),
          projectService.getCatalogEntries('document-categories'),
          projectService.getCatalogEntries('document-types'),
        ]),
      ]);

      const taskStatuses = catalogResults[0].status === 'fulfilled' ? catalogResults[0].value : [];
      const workTypes = catalogResults[1].status === 'fulfilled' ? catalogResults[1].value : [];
      const expenseCategories = catalogResults[2].status === 'fulfilled' ? catalogResults[2].value : [];
      const documentCategories = catalogResults[3].status === 'fulfilled' ? catalogResults[3].value : [];
      const documentTypes = catalogResults[4].status === 'fulfilled' ? catalogResults[4].value : [];

      setSummary(summaryResult);
      setTaskStatusCatalog(taskStatuses);
      setWorkTypeCatalog(workTypes);
      setExpenseCategoryCatalog(expenseCategories);
      setDocumentCategoryCatalog(documentCategories);
      setDocumentTypeCatalog(documentTypes);

      const workTypeOptions = resolveCatalogOptions(workTypes, DEFAULT_WORK_TYPES);
      const expenseCategoryOptions = resolveCatalogOptions(expenseCategories, DEFAULT_EXPENSE_CATEGORIES);

      setProgressDrafts(Object.fromEntries(
        summaryResult.assignments.map((item) => [item.workItemId, { status: item.status, percentComplete: item.percentComplete, notes: '' }]),
      ));
      setTimesheetDrafts(Object.fromEntries(
        summaryResult.assignments.map((item) => [item.workItemId, {
          userId,
          entryDate: today(),
          hours: 8,
          isBillable: false,
          hourlyRate: 0,
          workType: pickPreferredOption(workTypeOptions, ['Field', 'Standard']),
          notes: '',
        }]),
      ));
      setExpenseDrafts(Object.fromEntries(
        summaryResult.assignments.map((item) => [item.workItemId, {
          userId,
          expenseDate: today(),
          category: pickPreferredOption(expenseCategoryOptions, ['Travel', 'Supplies', 'Other']),
          currency: 'USD',
          amount: 0,
          taxAmount: 0,
          isBillable: false,
          notes: '',
        }]),
      ));
      setEvidenceNames(Object.fromEntries(summaryResult.assignments.map((item) => [item.workItemId, `${item.workItemTitle} evidence`])));
      setEvidenceFiles({});
    } catch (error: any) {
      toast.error(error.message || 'Failed to load project mobile view');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const assignmentStatuses = useMemo(
    () => Array.from(new Set((summary?.assignments ?? []).map((item) => item.status))),
    [summary],
  );

  const filteredAssignments = useMemo(() => {
    if (!summary) return [];

    return summary.assignments.filter((item) => {
      const matchesSearch = !searchTerm
        || item.workItemTitle.toLowerCase().includes(searchTerm.toLowerCase())
        || item.projectCode.toLowerCase().includes(searchTerm.toLowerCase())
        || item.projectTitle.toLowerCase().includes(searchTerm.toLowerCase());
      const matchesStatus = statusFilter === 'all' || item.status === statusFilter;
      const isOverdue = item.plannedEndDate ? new Date(item.plannedEndDate).getTime() < Date.now() : false;
      return matchesSearch && matchesStatus && (!showOverdueOnly || isOverdue);
    });
  }, [searchTerm, showOverdueOnly, statusFilter, summary]);

  const priorityAssignments = useMemo(
    () => [...filteredAssignments]
      .sort((a, b) => {
        const aOverdue = a.plannedEndDate ? new Date(a.plannedEndDate).getTime() < Date.now() : false;
        const bOverdue = b.plannedEndDate ? new Date(b.plannedEndDate).getTime() < Date.now() : false;
        if (aOverdue !== bOverdue) return aOverdue ? -1 : 1;
        return (a.percentComplete ?? 0) - (b.percentComplete ?? 0);
      })
      .slice(0, 5),
    [filteredAssignments],
  );

  const taskStatusOptionsByWorkItem = useMemo(() => {
    if (!summary) return new Map<string, string[]>();
    return new Map(
      summary.assignments.map((item) => [
        item.workItemId,
        resolveCatalogOptions(taskStatusCatalog, DEFAULT_TASK_STATUSES, progressDrafts[item.workItemId]?.status || item.status),
      ]),
    );
  }, [progressDrafts, summary, taskStatusCatalog]);

  const workTypeOptions = useMemo(
    () => resolveCatalogOptions(workTypeCatalog, DEFAULT_WORK_TYPES),
    [workTypeCatalog],
  );

  const expenseCategoryOptions = useMemo(
    () => resolveCatalogOptions(expenseCategoryCatalog, DEFAULT_EXPENSE_CATEGORIES),
    [expenseCategoryCatalog],
  );

  const evidenceCategory = useMemo(
    () => pickPreferredOption(resolveCatalogOptions(documentCategoryCatalog, DEFAULT_DOCUMENT_CATEGORIES), ['FieldEvidence', 'Reports', 'General']),
    [documentCategoryCatalog],
  );

  const evidenceType = useMemo(
    () => pickPreferredOption(resolveCatalogOptions(documentTypeCatalog, DEFAULT_DOCUMENT_TYPES), ['MobileEvidence', 'Photo', 'Evidence']),
    [documentTypeCatalog],
  );

  const submitTimesheet = async (projectId: string, workItemId: string) => {
    try {
      const draft = timesheetDrafts[workItemId] || {
        userId,
        entryDate: today(),
        hours: 8,
        isBillable: false,
        hourlyRate: 0,
        workType: pickPreferredOption(workTypeOptions, ['Field', 'Standard']),
        notes: '',
      };
      await projectService.submitMobileTimesheet(projectId, { ...draft, userId: draft.userId || userId, workItemId });
      setTimesheetDrafts((prev) => ({
        ...prev,
        [workItemId]: {
          userId,
          entryDate: today(),
          hours: 8,
          isBillable: false,
          hourlyRate: 0,
          workType: draft.workType,
          notes: '',
        },
      }));
      await load();
      toast.success('Mobile timesheet submitted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to submit mobile timesheet');
    }
  };

  const submitExpense = async (projectId: string, workItemId: string) => {
    try {
      const draft = expenseDrafts[workItemId] || {
        userId,
        expenseDate: today(),
        category: pickPreferredOption(expenseCategoryOptions, ['Travel', 'Supplies']),
        currency: 'USD',
        amount: 0,
        taxAmount: 0,
        isBillable: false,
        notes: '',
      };
      await projectService.submitMobileExpense(projectId, { ...draft, userId: draft.userId || userId, workItemId });
      setExpenseDrafts((prev) => ({
        ...prev,
        [workItemId]: {
          userId,
          expenseDate: today(),
          category: draft.category,
          currency: 'USD',
          amount: 0,
          taxAmount: 0,
          isBillable: false,
          notes: '',
        },
      }));
      await load();
      toast.success('Mobile expense submitted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to submit mobile expense');
    }
  };

  const updateProgress = async (projectId: string, workItemId: string) => {
    try {
      await projectService.updateMobileWorkItemProgress(projectId, workItemId, progressDrafts[workItemId]);
      await load();
      toast.success('Mobile progress updated');
    } catch (error: any) {
      toast.error(error.message || 'Failed to update mobile progress');
    }
  };

  const uploadEvidence = async (projectId: string, workItemId: string) => {
    const file = evidenceFiles[workItemId];
    if (!file) {
      toast.error('Choose a file before uploading evidence');
      return;
    }

    try {
      setUploadingEvidenceFor(workItemId);
      await projectService.uploadProjectDocument(projectId, file, {
        documentName: evidenceNames[workItemId] || file.name,
        category: evidenceCategory,
        documentType: evidenceType,
        versionLabel: '1.0',
        status: 'Active',
        isExternalVisible: false,
      });
      setEvidenceFiles((prev) => ({ ...prev, [workItemId]: null }));
      await load();
      toast.success('Field evidence uploaded');
    } catch (error: any) {
      toast.error(error.message || 'Failed to upload field evidence');
    } finally {
      setUploadingEvidenceFor(null);
    }
  };

  if (loading || !summary) {
    return <div className="py-20 text-center text-muted-foreground">Loading project mobile view...</div>;
  }

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <h1 className="text-3xl font-bold tracking-tight">Project Mobile</h1>
        <p className="text-muted-foreground">Field updates, time entry, expense capture, and evidence upload for assigned work.</p>
      </div>

      <Card>
        <CardHeader><CardTitle>My Summary</CardTitle></CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-4">
          <div><div className="text-sm text-muted-foreground">Assignments</div><div className="text-2xl font-semibold">{summary.assignmentCount}</div></div>
          <div><div className="text-sm text-muted-foreground">Overdue</div><div className="text-2xl font-semibold">{summary.overdueCount}</div></div>
          <div><div className="text-sm text-muted-foreground">Pending Hours</div><div className="text-2xl font-semibold">{summary.pendingHours}</div></div>
          <div><div className="text-sm text-muted-foreground">Pending Expenses</div><div className="text-2xl font-semibold">{summary.pendingExpenses}</div></div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Assignment Filters</CardTitle></CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-4">
          <div className="grid gap-2 md:col-span-2">
            <Label>Search</Label>
            <Input value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} placeholder="Search project code, project, or work item" />
          </div>
          <div className="grid gap-2">
            <Label>Status</Label>
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger><SelectValue placeholder="All statuses" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                {assignmentStatuses.map((status) => <SelectItem key={status} value={status}>{formatCatalogLabel(status)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="flex items-end">
            <Button variant={showOverdueOnly ? 'default' : 'outline'} className="w-full" onClick={() => setShowOverdueOnly((prev) => !prev)}>
              {showOverdueOnly ? 'Showing overdue only' : 'Show overdue only'}
            </Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Priority Queue</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          {priorityAssignments.length === 0 ? (
            <div className="text-sm text-muted-foreground">No assignments currently need priority attention.</div>
          ) : (
            priorityAssignments.map((item) => {
              const overdue = item.plannedEndDate ? new Date(item.plannedEndDate).getTime() < Date.now() : false;
              return (
                <div key={item.workItemId} className="flex items-center justify-between rounded-lg border p-4">
                  <div>
                    <div className="font-medium">{item.workItemTitle}</div>
                    <div className="text-sm text-muted-foreground">{item.projectCode} | {formatCatalogLabel(item.status)} | {item.percentComplete}% complete</div>
                  </div>
                  <div className="flex items-center gap-2">
                    {overdue ? <Badge variant="destructive">Overdue</Badge> : <Badge variant="outline">Active</Badge>}
                    <Button variant="outline" size="sm" onClick={() => setFocusedAssignmentId(item.workItemId)}>
                      Open
                    </Button>
                  </div>
                </div>
              );
            })
          )}
        </CardContent>
      </Card>

      {filteredAssignments.length === 0 ? (
        <Card><CardContent className="py-10 text-center text-muted-foreground">No mobile assignments match the current filter.</CardContent></Card>
      ) : null}

      {filteredAssignments.map((item) => (
        <Card key={item.workItemId} className={focusedAssignmentId === item.workItemId ? 'ring-2 ring-blue-500' : undefined}>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <span>{item.workItemTitle}</span>
              <Badge variant="outline">{item.projectCode}</Badge>
              <Badge>{formatCatalogLabel(item.status)}</Badge>
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-5">
            <div className="text-sm text-muted-foreground">
              {item.projectTitle}
              {item.plannedEndDate ? ` | due ${format(new Date(item.plannedEndDate), 'MMM dd, yyyy')}` : ''}
            </div>

            <div className="space-y-2">
              <div className="flex items-center justify-between text-sm"><span>Progress</span><span>{progressDrafts[item.workItemId]?.percentComplete ?? item.percentComplete}%</span></div>
              <Progress value={progressDrafts[item.workItemId]?.percentComplete ?? item.percentComplete} />
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              <div className="grid gap-2">
                <Label>Status</Label>
                <Select
                  value={progressDrafts[item.workItemId]?.status || item.status}
                  onValueChange={(value) => setProgressDrafts((prev) => ({ ...prev, [item.workItemId]: { ...prev[item.workItemId], status: value } }))}
                >
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {(taskStatusOptionsByWorkItem.get(item.workItemId) || DEFAULT_TASK_STATUSES).map((status) => (
                      <SelectItem key={status} value={status}>{formatCatalogLabel(status)}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Percent Complete</Label>
                <Input
                  type="number"
                  min={0}
                  max={100}
                  value={progressDrafts[item.workItemId]?.percentComplete ?? item.percentComplete}
                  onChange={(e) => setProgressDrafts((prev) => ({
                    ...prev,
                    [item.workItemId]: { ...prev[item.workItemId], percentComplete: Math.max(0, Math.min(100, Number(e.target.value || '0'))) },
                  }))}
                />
              </div>
            </div>

            <div className="grid gap-2">
              <Label>Progress Note</Label>
              <Textarea rows={2} value={progressDrafts[item.workItemId]?.notes || ''} onChange={(e) => setProgressDrafts((prev) => ({ ...prev, [item.workItemId]: { ...prev[item.workItemId], notes: e.target.value } }))} />
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              <div className="grid gap-2">
                <Label>Hours</Label>
                <Input
                  type="number"
                  value={timesheetDrafts[item.workItemId]?.hours ?? 8}
                  onChange={(e) => setTimesheetDrafts((prev) => ({
                    ...prev,
                    [item.workItemId]: {
                      userId,
                      entryDate: today(),
                      hours: Number(e.target.value || '0'),
                      isBillable: false,
                      hourlyRate: 0,
                      workType: pickPreferredOption(workTypeOptions, ['Field', 'Standard']),
                      notes: '',
                      ...prev[item.workItemId],
                    },
                  }))}
                />
              </div>
              <div className="grid gap-2">
                <Label>Work Type</Label>
                <Select
                  value={timesheetDrafts[item.workItemId]?.workType || pickPreferredOption(workTypeOptions, ['Field', 'Standard'])}
                  onValueChange={(value) => setTimesheetDrafts((prev) => ({
                    ...prev,
                    [item.workItemId]: {
                      userId,
                      entryDate: today(),
                      hours: 8,
                      isBillable: false,
                      hourlyRate: 0,
                      workType: value,
                      notes: '',
                      ...prev[item.workItemId],
                    },
                  }))}
                >
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {workTypeOptions.map((workType) => <SelectItem key={workType} value={workType}>{formatCatalogLabel(workType)}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              <div className="grid gap-2">
                <Label>Expense Amount</Label>
                <Input
                  type="number"
                  value={expenseDrafts[item.workItemId]?.amount ?? 0}
                  onChange={(e) => setExpenseDrafts((prev) => ({
                    ...prev,
                    [item.workItemId]: {
                      userId,
                      expenseDate: today(),
                      category: pickPreferredOption(expenseCategoryOptions, ['Travel', 'Supplies']),
                      currency: 'USD',
                      amount: Number(e.target.value || '0'),
                      taxAmount: 0,
                      isBillable: false,
                      notes: '',
                      ...prev[item.workItemId],
                    },
                  }))}
                />
              </div>
              <div className="grid gap-2">
                <Label>Expense Category</Label>
                <Select
                  value={expenseDrafts[item.workItemId]?.category || pickPreferredOption(expenseCategoryOptions, ['Travel', 'Supplies'])}
                  onValueChange={(value) => setExpenseDrafts((prev) => ({
                    ...prev,
                    [item.workItemId]: {
                      userId,
                      expenseDate: today(),
                      category: value,
                      currency: 'USD',
                      amount: 0,
                      taxAmount: 0,
                      isBillable: false,
                      notes: '',
                      ...prev[item.workItemId],
                    },
                  }))}
                >
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {expenseCategoryOptions.map((category) => <SelectItem key={category} value={category}>{formatCatalogLabel(category)}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="rounded-lg border p-4">
              <div className="grid gap-4 md:grid-cols-[1.2fr,0.8fr,0.8fr]">
                <div className="grid gap-2">
                  <Label>Evidence Name</Label>
                  <Input value={evidenceNames[item.workItemId] || ''} onChange={(e) => setEvidenceNames((prev) => ({ ...prev, [item.workItemId]: e.target.value }))} />
                </div>
                <div className="grid gap-2">
                  <Label>Evidence Category</Label>
                  <Input value={formatCatalogLabel(evidenceCategory)} readOnly />
                </div>
                <div className="grid gap-2">
                  <Label>Evidence Type</Label>
                  <Input value={formatCatalogLabel(evidenceType)} readOnly />
                </div>
              </div>
              <div className="mt-4 grid gap-4 md:grid-cols-[1fr,auto]">
                <div className="grid gap-2">
                  <Label>Upload Evidence</Label>
                  <Input type="file" onChange={(e) => setEvidenceFiles((prev) => ({ ...prev, [item.workItemId]: e.target.files?.[0] || null }))} />
                </div>
                <div className="flex items-end">
                  <Button variant="outline" disabled={uploadingEvidenceFor === item.workItemId} onClick={() => uploadEvidence(item.projectId, item.workItemId)}>
                    {uploadingEvidenceFor === item.workItemId ? 'Uploading...' : 'Upload Evidence'}
                  </Button>
                </div>
              </div>
              <div className="mt-2 text-xs text-muted-foreground">Evidence is stored in project documents using the configured project document catalogs.</div>
            </div>

            <div className="flex flex-wrap gap-2">
              <Button onClick={() => updateProgress(item.projectId, item.workItemId)}>Update Progress</Button>
              <Button variant="outline" onClick={() => submitTimesheet(item.projectId, item.workItemId)}>Submit Time</Button>
              <Button variant="outline" onClick={() => submitExpense(item.projectId, item.workItemId)}>Submit Expense</Button>
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
