'use client';

import React, { useCallback, useEffect, useMemo, useState } from 'react';
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
  DEFAULT_PROJECT_CURRENCY,
  formatProjectMoney,
  loadProjectCurrencyContext,
  type ProjectCurrencyReference,
} from '@/lib/project-currency';
import {
  CreateProjectExpenseDto,
  CreateProjectTimesheetEntryDto,
  ProjectCatalogEntryDto,
  ProjectMobileSummaryDto,
  UpdateProjectWorkItemProgressDto,
  projectService,
} from '@/services/projectService';
import { civilEngineeringDirectTaskService } from '@/services/civil-engineering-direct-task.service';
import type {
  CivilEngineeringDirectTaskFeedback,
  CivilEngineeringDirectTaskFeedbackAction,
  CivilEngineeringDirectTaskFeedbackLookups,
  ProcessCivilEngineeringDirectTaskFeedbackRequest,
} from '@/types/civil-engineering-direct-task';

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

type CivilOfflineActor = {
  tenantId: string;
  userId: string;
};

function getCurrentCivilOfflineActor(): CivilOfflineActor | null {
  if (typeof window === 'undefined') return null;
  const token = localStorage.getItem('authToken') || localStorage.getItem('token');
  if (!token) return null;

  try {
    const rawUser = localStorage.getItem('user');
    if (!rawUser) return null;
    const user = JSON.parse(rawUser);
    const rawTenant = localStorage.getItem('currentTenant');
    const tenant = rawTenant ? JSON.parse(rawTenant) : null;
    const userId = String(user?.id || user?.userId || '').trim();
    const tenantId = String(
      user?.currentTenantId || user?.tenantId || tenant?.id || tenant?.tenantId || ''
    ).trim();
    return userId && tenantId ? { tenantId, userId } : null;
  } catch {
    return null;
  }
}

export const buildCivilFeedbackStorageKey = (
  tenantId: string,
  userId: string,
  taskId: string
) =>
  `civil-mobile-field-feedback:v2:${encodeURIComponent(tenantId)}:${encodeURIComponent(userId)}:${encodeURIComponent(taskId)}`;

const civilNone = '__civil_select__';

type QueuedCivilFieldFeedback = {
  tenantId: string;
  userId: string;
  projectId: string;
  taskId: string;
  assignmentRowVersion: string;
  request: ProcessCivilEngineeringDirectTaskFeedbackRequest;
  queuedAt: string;
};

const queuedFeedbackMatches = (
  queuedFeedback: QueuedCivilFieldFeedback,
  actor: CivilOfflineActor,
  projectId: string,
  taskId: string
) =>
  queuedFeedback.tenantId === actor.tenantId &&
  queuedFeedback.userId === actor.userId &&
  queuedFeedback.projectId === projectId &&
  queuedFeedback.taskId === taskId;

function CivilMobileFieldFeedback({
  projectId,
  taskId,
  title,
  rowVersion,
  onSaved,
}: {
  projectId: string;
  taskId: string;
  title: string;
  rowVersion: string;
  onSaved: () => Promise<void>;
}) {
  const offlineActor = useMemo(() => getCurrentCivilOfflineActor(), []);
  const storageKey = offlineActor
    ? buildCivilFeedbackStorageKey(
        offlineActor.tenantId,
        offlineActor.userId,
        taskId
      )
    : null;
  const [lookups, setLookups] =
    useState<CivilEngineeringDirectTaskFeedbackLookups>();
  const [history, setHistory] = useState<CivilEngineeringDirectTaskFeedback[]>([]);
  const [action, setAction] =
    useState<CivilEngineeringDirectTaskFeedbackAction | ''>('');
  const [message, setMessage] = useState('');
  const [progress, setProgress] = useState('');
  const [measurementValue, setMeasurementValue] = useState('');
  const [measurementUnitId, setMeasurementUnitId] = useState(civilNone);
  const [documentVersionId, setDocumentVersionId] = useState(civilNone);
  const [currentRowVersion, setCurrentRowVersion] = useState(rowVersion);
  const [queued, setQueued] = useState<QueuedCivilFieldFeedback>();
  const [saving, setSaving] = useState(false);
  const [isOnline, setIsOnline] = useState(
    () => typeof navigator === 'undefined' || navigator.onLine
  );
  const selectedDocument = useMemo(
    () =>
      lookups?.documents.find(
        item => item.centralDocumentVersionId === documentVersionId
      ),
    [documentVersionId, lookups]
  );
  const availableActions = useMemo(
    () =>
      (lookups?.availableActions || []).filter(
        item => item !== 'Accept' && item !== 'Return'
      ),
    [lookups]
  );

  const load = useCallback(async () => {
    const [feedbackLookups, feedbackHistory] = await Promise.all([
      civilEngineeringDirectTaskService.feedbackLookups(projectId, taskId),
      civilEngineeringDirectTaskService.feedback(projectId, taskId),
    ]);
    setLookups(feedbackLookups);
    setHistory(feedbackHistory);
  }, [projectId, taskId]);

  const applySaved = useCallback(
    async (saved: { rowVersion: string }) => {
      setCurrentRowVersion(saved.rowVersion);
      if (storageKey) localStorage.removeItem(storageKey);
      setQueued(undefined);
      setAction('');
      setMessage('');
      setProgress('');
      setMeasurementValue('');
      setMeasurementUnitId(civilNone);
      setDocumentVersionId(civilNone);
      await Promise.all([load(), onSaved()]);
    },
    [load, onSaved, storageKey]
  );

  const flushQueued = useCallback(async () => {
    if (
      typeof window === 'undefined' ||
      !navigator.onLine ||
      !storageKey ||
      !offlineActor
    ) return;
    const currentActor = getCurrentCivilOfflineActor();
    if (
      !currentActor ||
      currentActor.tenantId !== offlineActor.tenantId ||
      currentActor.userId !== offlineActor.userId
    ) {
      setQueued(undefined);
      return;
    }
    const stored = localStorage.getItem(storageKey);
    if (!stored) return;
    try {
      const pending = JSON.parse(stored) as QueuedCivilFieldFeedback;
      if (!queuedFeedbackMatches(pending, currentActor, projectId, taskId)) {
        localStorage.removeItem(storageKey);
        setQueued(undefined);
        return;
      }
      const mobileSummary = await projectService.getMobileSummary();
      const currentAssignment = mobileSummary.assignments.find(
        item =>
          item.projectId === projectId &&
          item.civilDirectTaskId === taskId &&
          item.civilDirectTaskRowVersion === pending.assignmentRowVersion
      );
      if (!currentAssignment) {
        toast.error(
          'Queued Civil feedback was not synchronized because the task is no longer assigned to this user or has changed.'
        );
        return;
      }
      const saved =
        await civilEngineeringDirectTaskService.submitAssigneeFeedback(
          projectId,
          taskId,
          pending.request
        );
      await applySaved(saved);
      toast.success('Queued Civil field feedback synchronized');
    } catch (error: any) {
      toast.error(
        error.message ||
          'Queued Civil field feedback needs attention before it can be synchronized'
      );
    }
  }, [applySaved, offlineActor, projectId, storageKey, taskId]);

  useEffect(() => {
    setCurrentRowVersion(rowVersion);
  }, [rowVersion]);

  useEffect(() => {
    try {
      const stored = storageKey ? localStorage.getItem(storageKey) : null;
      if (stored && offlineActor && storageKey) {
        const pending = JSON.parse(stored) as QueuedCivilFieldFeedback;
        if (queuedFeedbackMatches(pending, offlineActor, projectId, taskId)) {
          setQueued(pending);
        } else {
          localStorage.removeItem(storageKey);
        }
      }
    } catch {
      if (storageKey) localStorage.removeItem(storageKey);
    }
    void load().catch((error: any) =>
      toast.error(error.message || 'Failed to load governed Civil task controls')
    );
    void flushQueued();
    const handleOnline = () => {
      setIsOnline(true);
      void flushQueued();
    };
    const handleOffline = () => setIsOnline(false);
    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);
    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, [flushQueued, load, storageKey]);

  const submit = async () => {
    if (!action) {
      toast.error('Select a governed Civil task action');
      return;
    }
    if (
      (action === 'UpdateProgress' && (!progress || Number(progress) >= 100)) ||
      ((action === 'UpdateProgress' || action === 'Complete') && !message.trim())
    ) {
      toast.error(
        action === 'UpdateProgress'
          ? 'Enter a field progress value from 0 to 99.99 and a site note.'
          : 'Enter a field completion note.'
      );
      return;
    }
    if (measurementValue && measurementUnitId === civilNone) {
      toast.error('Select an active measurement unit.');
      return;
    }
    if (
      action === 'Complete' &&
      lookups?.requireFeedbackEvidence &&
      !selectedDocument
    ) {
      toast.error('Select the required current Published central-DMS evidence.');
      return;
    }

    const request: ProcessCivilEngineeringDirectTaskFeedbackRequest = {
      clientRequestId: crypto.randomUUID(),
      action,
      message: message.trim() || undefined,
      progressPercent: action === 'UpdateProgress' ? Number(progress) : undefined,
      measurementValue: measurementValue ? Number(measurementValue) : undefined,
      measurementUnitId:
        measurementUnitId === civilNone ? undefined : measurementUnitId,
      centralDocumentRecordId: selectedDocument?.centralDocumentRecordId,
      centralDocumentVersionId: selectedDocument?.centralDocumentVersionId,
      rowVersion: currentRowVersion,
    };

    if (!isOnline) {
      const currentActor = getCurrentCivilOfflineActor();
      if (
        !storageKey ||
        !offlineActor ||
        !currentActor ||
        currentActor.tenantId !== offlineActor.tenantId ||
        currentActor.userId !== offlineActor.userId
      ) {
        toast.error(
          'Sign in to the current tenant again before queuing Civil field feedback.'
        );
        return;
      }
      const pending: QueuedCivilFieldFeedback = {
        tenantId: currentActor.tenantId,
        userId: currentActor.userId,
        projectId,
        taskId,
        assignmentRowVersion: currentRowVersion,
        request: {
          ...request,
          capturedOfflineAtUtc: new Date().toISOString(),
        },
        queuedAt: new Date().toISOString(),
      };
      localStorage.setItem(storageKey, JSON.stringify(pending));
      setQueued(pending);
      toast.success(
        'Field update queued locally and will synchronize once this device is online.'
      );
      return;
    }

    setSaving(true);
    try {
      const saved =
        await civilEngineeringDirectTaskService.submitAssigneeFeedback(
          projectId,
          taskId,
          request
        );
      await applySaved(saved);
      toast.success('Governed Civil field feedback saved');
    } catch (error: any) {
      toast.error(error.message || 'Failed to save governed Civil field feedback');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-4 rounded-lg border border-primary/30 bg-primary/[0.03] p-4">
      <div>
        <div className="font-medium">Governed Civil field feedback</div>
        <p className="mt-1 text-sm text-muted-foreground">
          This task uses the Civil task lifecycle. Site notes, progress,
          measurements and current Published central-DMS photo/evidence are
          recorded in the immutable task feedback history.
        </p>
      </div>
      {queued ? (
        <div className="rounded border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:bg-amber-950/30 dark:text-amber-100">
          One offline field update is queued from{' '}
          {format(new Date(queued.queuedAt), 'PP p')}. Reconnect and use Sync
          queued update; subsequent entries stay blocked until this one is
          accepted.
        </div>
      ) : null}
      <div className="grid gap-4 md:grid-cols-3">
        <div className="grid gap-2">
          <Label>Controlled action</Label>
          <Select
            value={action || civilNone}
            onValueChange={value =>
              setAction(
                value === civilNone
                  ? ''
                  : (value as CivilEngineeringDirectTaskFeedbackAction)
              )
            }
            disabled={Boolean(queued)}
          >
            <SelectTrigger><SelectValue placeholder="Select action" /></SelectTrigger>
            <SelectContent>
              <SelectItem value={civilNone} disabled>Select action</SelectItem>
              {availableActions.map(item => (
                <SelectItem key={item} value={item}>
                  {item === 'UpdateProgress' ? 'Update progress' : item}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        {action === 'UpdateProgress' ? (
          <div className="grid gap-2">
            <Label>Progress (%)</Label>
            <Input
              type="number"
              min={0}
              max={99.99}
              step={0.01}
              value={progress}
              onChange={event => setProgress(event.target.value)}
              disabled={Boolean(queued)}
            />
          </div>
        ) : null}
        <div className="grid gap-2">
          <Label>Measurement value (optional)</Label>
          <Input
            type="number"
            min={0}
            step={0.0001}
            value={measurementValue}
            onChange={event => {
              const value = event.target.value;
              setMeasurementValue(value);
              if (!value) setMeasurementUnitId(civilNone);
            }}
            disabled={Boolean(queued)}
          />
        </div>
        <div className="grid gap-2">
          <Label>Measurement unit</Label>
          <Select
            value={measurementUnitId}
            onValueChange={setMeasurementUnitId}
            disabled={Boolean(queued)}
          >
            <SelectTrigger><SelectValue placeholder="No measurement" /></SelectTrigger>
            <SelectContent>
              <SelectItem value={civilNone}>No measurement</SelectItem>
              {lookups?.measurementUnits.map(unit => (
                <SelectItem key={unit.id} value={unit.id}>
                  {unit.symbol || unit.code} · {unit.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="grid gap-2 md:col-span-2">
          <Label>
            Central-DMS photo / evidence
            {action === 'Complete' && lookups?.requireFeedbackEvidence
              ? ''
              : ' (optional)'}
          </Label>
          <Select
            value={documentVersionId}
            onValueChange={setDocumentVersionId}
            disabled={Boolean(queued)}
          >
            <SelectTrigger>
              <SelectValue placeholder="Select current Published central-DMS file" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={civilNone}>No evidence file</SelectItem>
              {lookups?.documents.map(document => (
                <SelectItem
                  key={document.centralDocumentVersionId}
                  value={document.centralDocumentVersionId}
                >
                  {document.documentReference} · {document.title}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>
      <div className="grid gap-2">
        <Label>
          {action === 'Complete'
            ? 'Completion note'
            : action === 'UpdateProgress'
              ? 'Site progress note'
              : 'Site note (optional)'}
        </Label>
        <Textarea
          rows={3}
          maxLength={2000}
          value={message}
          onChange={event => setMessage(event.target.value)}
          disabled={Boolean(queued)}
          placeholder="Record site conditions, measurements, observations and constraints accurately."
        />
      </div>
      <div className="flex flex-wrap gap-2">
        <Button
          disabled={!action || saving || Boolean(queued)}
          onClick={() => void submit()}
        >
          {saving ? 'Saving...' : isOnline ? 'Save field feedback' : 'Queue field feedback'}
        </Button>
        {queued ? (
          <Button
            variant="outline"
            disabled={!isOnline || saving}
            onClick={() => void flushQueued()}
          >
            Sync queued update
          </Button>
        ) : null}
      </div>
      <div className="space-y-2">
        <div className="text-sm font-medium">Civil feedback history · {title}</div>
        {history.length ? (
          history.map(entry => (
            <div key={entry.id} className="rounded border bg-background p-3 text-sm">
              <div className="flex flex-wrap gap-2">
                <Badge variant="outline">{entry.action}</Badge>
                {entry.progressPercent != null ? <span>{entry.progressPercent}%</span> : null}
                {entry.measurementValue != null ? (
                  <span>{entry.measurementValue} {entry.measurementUnitLabel || ''}</span>
                ) : null}
                <span className="text-muted-foreground">
                  {format(new Date(entry.createdAt), 'PP p')}
                </span>
              </div>
              {entry.message ? (
                <p className="mt-2 whitespace-pre-wrap break-words text-muted-foreground">
                  {entry.message}
                </p>
              ) : null}
              {entry.documentReference ? (
                <p className="mt-1 text-xs text-muted-foreground">
                  Central-DMS evidence: {entry.documentReference}
                </p>
              ) : null}
              {entry.capturedOfflineAtUtc ? (
                <p className="mt-1 text-xs text-muted-foreground">
                  Captured offline: {format(new Date(entry.capturedOfflineAtUtc), 'PP p')}
                </p>
              ) : null}
            </div>
          ))
        ) : (
          <p className="text-sm text-muted-foreground">
            No governed Civil field feedback has been recorded.
          </p>
        )}
      </div>
    </div>
  );
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
  const [baseCurrency, setBaseCurrency] = useState<ProjectCurrencyReference>(DEFAULT_PROJECT_CURRENCY);

  const load = async () => {
    try {
      setLoading(true);
      const [summaryResult, catalogResults, currencyContext] = await Promise.all([
        projectService.getMobileSummary(),
        Promise.allSettled([
          projectService.getCatalogEntries('task-statuses'),
          projectService.getCatalogEntries('timesheet-work-types'),
          projectService.getCatalogEntries('expense-categories'),
          projectService.getCatalogEntries('document-categories'),
          projectService.getCatalogEntries('document-types'),
        ]),
        loadProjectCurrencyContext(),
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
      setBaseCurrency(currencyContext.baseCurrency);

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
          currency: currencyContext.baseCurrency.code,
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

  const formatMoney = (value: number, currency?: string | null) =>
    formatProjectMoney(value, currency, baseCurrency.code);

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
        currency: baseCurrency.code,
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
          currency: baseCurrency.code,
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
          <div><div className="text-sm text-muted-foreground">Pending Expenses</div><div className="text-2xl font-semibold">{formatMoney(summary.pendingExpenses)}</div></div>
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
            {item.civilDirectTaskId && item.civilDirectTaskRowVersion ? (
              <CivilMobileFieldFeedback
                projectId={item.projectId}
                taskId={item.civilDirectTaskId}
                title={item.workItemTitle}
                rowVersion={item.civilDirectTaskRowVersion}
                onSaved={load}
              />
            ) : (
              <>
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
                      ...prev[item.workItemId],
                      userId,
                      entryDate: today(),
                      hours: Number(e.target.value || '0'),
                      isBillable: false,
                      hourlyRate: 0,
                      workType: pickPreferredOption(workTypeOptions, ['Field', 'Standard']),
                      notes: '',
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
                      ...prev[item.workItemId],
                      userId,
                      entryDate: today(),
                      hours: 8,
                      isBillable: false,
                      hourlyRate: 0,
                      workType: value,
                      notes: '',
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
                <Label>Expense Amount ({baseCurrency.code})</Label>
                <Input
                  type="number"
                  value={expenseDrafts[item.workItemId]?.amount ?? 0}
                  onChange={(e) => setExpenseDrafts((prev) => ({
                    ...prev,
                    [item.workItemId]: {
                      ...prev[item.workItemId],
                      userId,
                      expenseDate: today(),
                      category: pickPreferredOption(expenseCategoryOptions, ['Travel', 'Supplies']),
                      currency: baseCurrency.code,
                      amount: Number(e.target.value || '0'),
                      taxAmount: 0,
                      isBillable: false,
                      notes: '',
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
                      ...prev[item.workItemId],
                      userId,
                      expenseDate: today(),
                      category: value,
                      currency: baseCurrency.code,
                      amount: 0,
                      taxAmount: 0,
                      isBillable: false,
                      notes: '',
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
              </>
            )}
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
