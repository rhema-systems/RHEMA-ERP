'use client';

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  CheckCircle2,
  ClipboardList,
  FileCheck2,
  History,
  Plus,
  RefreshCw,
  Send,
  ShieldCheck,
  ShieldX,
  Undo2,
  UserRoundCheck,
  XCircle,
} from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
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
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { civilEngineeringDesignService } from '@/services/civil-engineering-design.service';
import { CivilEngineeringReconnaissancePanel } from './CivilEngineeringReconnaissancePanel';
import { CivilEngineeringInformationRequestsPanel } from './CivilEngineeringInformationRequestsPanel';
import { CivilEngineeringDocumentRegisterPanel } from './CivilEngineeringDocumentRegisterPanel';
import { CivilEngineeringPlanningGisPanel } from './CivilEngineeringPlanningGisPanel';
import { CivilEngineeringCommercialReadinessPanel } from './CivilEngineeringCommercialReadinessPanel';
import {
  documentManagementService,
  type CentralDocumentRecord,
} from '@/services/document-management.service';
import type {
  CivilEngineeringDesignAction,
  CivilEngineeringDesignCase,
  CivilEngineeringDesignEvidenceType,
  CivilEngineeringDesignLookups,
  CivilEngineeringDesignMemberLookup,
  CivilEngineeringDesignRevision,
  CivilEngineeringDesignStage,
  CivilEngineeringWorkClassification,
  CivilEngineeringWorksInitiationSource,
} from '@/types/civil-engineering-design';

const permissions = {
  read: 'civil-engineering.workspace.read',
  manage: 'civil-engineering.design.manage',
  respond: 'civil-engineering.design-input.respond',
  approve: 'civil-engineering.transactions.approve',
  audit: 'civil-engineering.audit.read',
};

const roles = {
  hod: 'TDC_HEAD_OF_CIVIL_ENGINEERING',
  sce: 'TDC_SUPERVISING_CIVIL_ENGINEER',
  engineer: 'TDC_CIVIL_ENGINEER',
  draftsman: 'TDC_DRAFTSMAN',
};

const stageLabels: Record<CivilEngineeringDesignStage, string> = {
  DraftDirective: 'Directive draft',
  SceInformationGathering: 'Information gathering',
  CivilEngineerDesign: 'Engineering design',
  SceDesignReview: 'SCE design review',
  Drafting: 'Drawing preparation',
  SceDrawingReview: 'SCE drawing review',
  HodFinalReview: 'HOD final review',
  Approved: 'Approved',
  Rejected: 'Rejected',
  Cancelled: 'Cancelled',
};

type ActionOption = {
  action: CivilEngineeringDesignAction;
  label: string;
  evidenceType?: CivilEngineeringDesignEvidenceType;
  assignee?: 'civilEngineer' | 'draftsman';
  reasonRequired?: boolean;
  finalDecision?: boolean;
};

const stageActions: Partial<
  Record<CivilEngineeringDesignStage, ActionOption[]>
> = {
  DraftDirective: [
    {
      action: 'DirectToSce',
      label: 'Direct to SCE',
      evidenceType: 'Directive',
      reasonRequired: true,
    },
    { action: 'Cancel', label: 'Cancel', reasonRequired: true },
  ],
  SceInformationGathering: [
    {
      action: 'AssignCivilEngineer',
      label: 'Assign Civil Engineer',
      evidenceType: 'Reconnaissance',
      assignee: 'civilEngineer',
      reasonRequired: true,
    },
  ],
  CivilEngineerDesign: [
    {
      action: 'SubmitDesign',
      label: 'Submit design',
      evidenceType: 'Design',
    },
  ],
  SceDesignReview: [
    {
      action: 'ReturnDesign',
      label: 'Return design',
      reasonRequired: true,
    },
    {
      action: 'AssignDraftsman',
      label: 'Approve and assign Draftsman',
      evidenceType: 'Design',
      assignee: 'draftsman',
      reasonRequired: true,
    },
  ],
  Drafting: [
    {
      action: 'SubmitDrawings',
      label: 'Submit drawings',
      evidenceType: 'Drawing',
    },
  ],
  SceDrawingReview: [],
  HodFinalReview: [],
};

const emptyLookups: CivilEngineeringDesignLookups = {
  supervisingCivilEngineers: [],
  civilEngineers: [],
  draftsmen: [],
  informationSourceSections: [],
  workClassifications: [],
  engineeringCategories: [],
  estateManagedAssets: [],
  approvedCapitalProjects: [],
  maintenanceEscalations: [],
  propertyDevelopmentNeeds: [],
  planningConditions: [],
  managementDirectives: [],
  defectMonitoringCases: [],
  infrastructureImprovementRequests: [],
};

const initiationSourceLabels: Record<
  CivilEngineeringWorksInitiationSource,
  string
> = {
  ApprovedCapitalProject: 'Approved capital project',
  MaintenanceEscalation: 'Maintenance escalation',
  PropertyDevelopmentNeed: 'Property development need',
  PlanningCondition: 'Planning or development condition',
  ManagementDirective: 'Published management directive',
  DefectMonitoring: 'Defect monitoring case',
  InfrastructureImprovementRequest: 'Infrastructure improvement request',
};

const workClassificationLabels: Record<
  CivilEngineeringWorkClassification,
  string
> = {
  NewProjectDesign: 'New project design',
  ConstructionSupervision: 'Construction supervision',
  ScheduledMaintenance: 'Scheduled maintenance',
  BreakdownMaintenance: 'Breakdown maintenance',
  PermittingReview: 'Permitting review',
  AssetComplaintResolution: 'Asset complaint resolution',
};

const errorMessage = (error: unknown) => {
  const value = error as {
    response?: { detail?: string; correlationId?: string };
    message?: string;
  };
  const detail = value.response?.detail || value.message;
  return detail || 'The Civil design request could not be completed.';
};

const requestId = () => crypto.randomUUID();
const toUtc = (value: string) =>
  value ? new Date(`${value}T23:59:59`).toISOString() : undefined;
const formatDate = (value?: string | null) =>
  value
    ? new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value))
    : 'Not set';

const actorRoleByStage: Partial<Record<CivilEngineeringDesignStage, string>> = {
  DraftDirective: roles.hod,
  SceInformationGathering: roles.sce,
  CivilEngineerDesign: roles.engineer,
  SceDesignReview: roles.sce,
  Drafting: roles.draftsman,
  SceDrawingReview: roles.sce,
  HodFinalReview: roles.hod,
};

export function CivilEngineeringDesignWorkflowPanel({
  projectId,
}: {
  projectId: string;
}) {
  const { hasPermission, hasRole } = useAuth();
  const { toast } = useToast();
  const canRead = hasPermission(permissions.read);
  const canManage = hasPermission(permissions.manage);
  const canRespond = hasPermission(permissions.respond);
  const canApprove = hasPermission(permissions.approve);
  const canAudit = hasPermission(permissions.audit);
  const canCreate = canManage && hasRole(roles.hod);

  const [cases, setCases] = useState<CivilEngineeringDesignCase[]>([]);
  const [lookups, setLookups] = useState(emptyLookups);
  const [documents, setDocuments] = useState<CentralDocumentRecord[]>([]);
  const [selectedId, setSelectedId] = useState('');
  const [history, setHistory] = useState<CivilEngineeringDesignRevision[]>([]);
  const [loading, setLoading] = useState(false);
  const [title, setTitle] = useState('');
  const [directive, setDirective] = useState('');
  const [initiationSource, setInitiationSource] =
    useState<CivilEngineeringWorksInitiationSource | ''>('');
  const [initiationSourceId, setInitiationSourceId] = useState('');
  const [initiationSourceDocumentVersionId, setInitiationSourceDocumentVersionId] =
    useState('');
  const [estateManagedAssetId, setEstateManagedAssetId] = useState('');
  const [engineeringCategoryId, setEngineeringCategoryId] = useState('');
  const [workClassification, setWorkClassification] =
    useState<CivilEngineeringWorkClassification | ''>('');
  const [scopeSummary, setScopeSummary] = useState('');
  const [constraintSummary, setConstraintSummary] = useState('');
  const [riskSummary, setRiskSummary] = useState('');
  const [recommendation, setRecommendation] = useState('');
  const [sceUserId, setSceUserId] = useState('');
  const [createDueDate, setCreateDueDate] = useState('');
  const [selectedAction, setSelectedAction] = useState<ActionOption | null>(
    null
  );
  const [assigneeUserId, setAssigneeUserId] = useState('');
  const [documentRecordId, setDocumentRecordId] = useState('');
  const [reason, setReason] = useState('');
  const [actionDueDate, setActionDueDate] = useState('');

  const selectedCase = useMemo(
    () => cases.find((item) => item.id === selectedId) ?? cases[0],
    [cases, selectedId]
  );
  const eligibleDocuments = useMemo(
    () =>
      documents.filter(
        (item) =>
          item.lifecycleStatus === 'Active' &&
          item.versionStatus === 'Published' &&
          Boolean(item.currentVersion)
      ),
    [documents]
  );
  const selectedInitiationSources = useMemo(() => {
    switch (initiationSource) {
      case 'ApprovedCapitalProject':
        return lookups.approvedCapitalProjects;
      case 'MaintenanceEscalation':
        return lookups.maintenanceEscalations;
      case 'PropertyDevelopmentNeed':
        return lookups.propertyDevelopmentNeeds;
      case 'PlanningCondition':
        return lookups.planningConditions;
      case 'DefectMonitoring':
        return lookups.defectMonitoringCases;
      case 'InfrastructureImprovementRequest':
        return lookups.infrastructureImprovementRequests;
      default:
        return [];
    }
  }, [initiationSource, lookups]);

  const loadHistory = useCallback(
    async (caseId: string) => {
      if (!canAudit || !caseId) {
        setHistory([]);
        return;
      }
      setHistory(await civilEngineeringDesignService.history(caseId));
    },
    [canAudit]
  );

  const load = useCallback(async () => {
    if (!canRead) return;
    setLoading(true);
    try {
      const [caseValues, lookupValues, documentValues] = await Promise.all([
        civilEngineeringDesignService.list(projectId),
        civilEngineeringDesignService.lookups(projectId),
        canManage || canRespond
          ? documentManagementService.getRecords()
          : Promise.resolve([] as CentralDocumentRecord[]),
      ]);
      setCases(caseValues);
      setLookups(lookupValues);
      setDocuments(documentValues);
      const currentId =
        caseValues.find((item) => item.id === selectedId)?.id ||
        caseValues[0]?.id ||
        '';
      setSelectedId(currentId);
      await loadHistory(currentId);
    } catch (error) {
      toast({
        title: 'Unable to load Civil design workflow',
        description: errorMessage(error),
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  }, [
    canManage,
    canRead,
    canRespond,
    loadHistory,
    projectId,
    selectedId,
    toast,
  ]);

  useEffect(() => {
    void load();
  }, [canRead, projectId]);

  const selectCase = async (caseId: string) => {
    setSelectedId(caseId);
    setSelectedAction(null);
    setReason('');
    setAssigneeUserId('');
    setDocumentRecordId('');
    try {
      await loadHistory(caseId);
    } catch (error) {
      toast({
        title: 'Unable to load design history',
        description: errorMessage(error),
        variant: 'destructive',
      });
    }
  };

  const createCase = async () => {
    if (
      title.trim().length < 3 ||
      directive.trim().length < 10 ||
      !sceUserId ||
      !initiationSource ||
      !initiationSourceId ||
      !estateManagedAssetId ||
      !engineeringCategoryId ||
      !workClassification ||
      scopeSummary.trim().length < 3 ||
      constraintSummary.trim().length < 3 ||
      riskSummary.trim().length < 3 ||
      recommendation.trim().length < 3 ||
      (initiationSource === 'ManagementDirective' &&
        !initiationSourceDocumentVersionId)
    ) {
      toast({
        title: 'Complete the governed Engineering Case',
        description:
          'Select the authoritative source, property/site, category, classification, SCE, and complete the case assessment.',
        variant: 'destructive',
      });
      return;
    }
    setLoading(true);
    try {
      const created = await civilEngineeringDesignService.create({
        clientRequestId: requestId(),
        projectId,
        initiationSource,
        initiationSourceId,
        initiationSourceDocumentVersionId:
          initiationSource === 'ManagementDirective'
            ? initiationSourceDocumentVersionId
            : undefined,
        estateManagedAssetId,
        engineeringCategoryId,
        workClassification,
        scopeSummary: scopeSummary.trim(),
        constraintSummary: constraintSummary.trim(),
        riskSummary: riskSummary.trim(),
        recommendation: recommendation.trim(),
        title: title.trim(),
        directive: directive.trim(),
        supervisingCivilEngineerUserId: sceUserId,
        dueAt: toUtc(createDueDate),
      });
      setTitle('');
      setDirective('');
      setInitiationSource('');
      setInitiationSourceId('');
      setInitiationSourceDocumentVersionId('');
      setEstateManagedAssetId('');
      setEngineeringCategoryId('');
      setWorkClassification('');
      setScopeSummary('');
      setConstraintSummary('');
      setRiskSummary('');
      setRecommendation('');
      setSceUserId('');
      setCreateDueDate('');
      setSelectedId(created.id);
      toast({
        title: 'Engineering Case created',
        description: `${created.referenceNumber} is ready for controlled routing.`,
        variant: 'success',
      });
      await load();
      await selectCase(created.id);
    } catch (error) {
      toast({
        title: 'Unable to create design directive',
        description: errorMessage(error),
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  };

  const membersForAction = (action: ActionOption) =>
    action.assignee === 'civilEngineer'
      ? lookups.civilEngineers
      : action.assignee === 'draftsman'
        ? lookups.draftsmen
        : [];

  const runAction = async () => {
    if (!selectedCase || !selectedAction) return;
    if (selectedAction.assignee && !assigneeUserId) {
      toast({
        title: 'Select the assignee',
        description:
          'Choose an eligible current project member before continuing.',
        variant: 'destructive',
      });
      return;
    }
    if (selectedAction.reasonRequired && reason.trim().length < 5) {
      toast({
        title: 'Enter the action reason',
        description: 'The reason must contain at least five characters.',
        variant: 'destructive',
      });
      return;
    }
    if (selectedAction.evidenceType && !documentRecordId) {
      toast({
        title: 'Select controlled evidence',
        description: `Select a current published ${selectedAction.evidenceType} document from central DMS.`,
        variant: 'destructive',
      });
      return;
    }

    setLoading(true);
    try {
      const evidence = [];
      if (selectedAction.evidenceType) {
        const detail =
          await documentManagementService.getRecord(documentRecordId);
        const version = detail?.versions.find(
          (item) =>
            item.status === 'Published' &&
            item.versionNumber === detail.record.currentVersion
        );
        if (!detail || !version) {
          throw new Error(
            'The selected DMS document no longer has a current published version. Refresh and select it again.'
          );
        }
        evidence.push({
          centralDocumentRecordId: detail.record.id,
          centralDocumentVersionId: version.id,
          evidenceType: selectedAction.evidenceType,
        });
      }
      const payload = {
        clientRequestId: requestId(),
        action: selectedAction.action,
        assigneeUserId: assigneeUserId || undefined,
        dueAt: toUtc(actionDueDate),
        reason: reason.trim() || undefined,
        rowVersion: selectedCase.rowVersion,
        evidence,
      };
      const updated = selectedAction.finalDecision
        ? await civilEngineeringDesignService.decide(selectedCase.id, payload)
        : await civilEngineeringDesignService.transition(
            selectedCase.id,
            payload
          );
      setSelectedAction(null);
      setAssigneeUserId('');
      setDocumentRecordId('');
      setReason('');
      setActionDueDate('');
      toast({
        title: 'Design workflow updated',
        description: `${updated.referenceNumber} is now at ${stageLabels[updated.stage]}.`,
        variant: 'success',
      });
      await load();
      await selectCase(updated.id);
    } catch (error) {
      toast({
        title: 'Unable to update design workflow',
        description: errorMessage(error),
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  };

  const runSharedReviewAction = async (
    action: 'SubmitPackage' | 'ReturnDrawings' | 'Approve' | 'Reject',
    comments: string
  ) => {
    if (!selectedCase) return;
    const isPackageSubmission = action === 'SubmitPackage';
    if (isPackageSubmission && (!documentRecordId || !actionDueDate)) {
      throw new Error(
        'Select the current published submission package and the HOD review due date.'
      );
    }

    setLoading(true);
    try {
      const evidence = [];
      if (isPackageSubmission) {
        const detail =
          await documentManagementService.getRecord(documentRecordId);
        const version = detail?.versions.find(
          (item) =>
            item.status === 'Published' &&
            item.versionNumber === detail.record.currentVersion
        );
        if (!detail || !version) {
          throw new Error(
            'The selected DMS submission package no longer has a current published version. Refresh and select it again.'
          );
        }
        evidence.push({
          centralDocumentRecordId: detail.record.id,
          centralDocumentVersionId: version.id,
          evidenceType: 'SubmissionPackage' as const,
        });
      }

      const payload = {
        clientRequestId: requestId(),
        action,
        dueAt: isPackageSubmission ? toUtc(actionDueDate) : undefined,
        reason: comments.trim() || undefined,
        rowVersion: selectedCase.rowVersion,
        evidence,
      };
      if (action === 'Approve' || action === 'Reject') {
        await civilEngineeringDesignService.decide(selectedCase.id, payload);
      } else {
        await civilEngineeringDesignService.transition(
          selectedCase.id,
          payload
        );
      }
      setDocumentRecordId('');
      setActionDueDate('');
    } finally {
      setLoading(false);
    }
  };

  if (!canRead) {
    return (
      <Alert>
        <ShieldX className="h-4 w-4" />
        <AlertTitle>Civil design access required</AlertTitle>
        <AlertDescription>
          You do not have permission to view Civil Engineering design records.
        </AlertDescription>
      </Alert>
    );
  }

  const availableActions = selectedCase
    ? (stageActions[selectedCase.stage] ?? [])
    : [];
  const actorRole = selectedCase
    ? actorRoleByStage[selectedCase.stage]
    : undefined;
  const roleAllowsAction = actorRole ? hasRole(actorRole) : false;
  const actionAllowed =
    selectedCase?.stage === 'HodFinalReview'
      ? canApprove && roleAllowsAction
      : canManage && roleAllowsAction;

  return (
    <div className="space-y-5">
      {canCreate ? (
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="flex items-center gap-2 text-base">
              <Plus className="h-4 w-4" /> New Engineering Case
            </CardTitle>
            <CardDescription>
              Start from one authoritative source, then route the governed case from HOD to SCE.
            </CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 lg:grid-cols-4">
            <div className="space-y-2 lg:col-span-2">
              <Label>Authoritative source</Label>
              <Select
                value={initiationSource || '__none__'}
                onValueChange={(value) => {
                  const selected =
                    value === '__none__'
                      ? ''
                      : (value as CivilEngineeringWorksInitiationSource);
                  setInitiationSource(selected);
                  setInitiationSourceId('');
                  setInitiationSourceDocumentVersionId('');
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select an approved source" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__none__">Select source</SelectItem>
                  {Object.entries(initiationSourceLabels).map(([key, label]) => (
                    <SelectItem key={key} value={key}>
                      {label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2 lg:col-span-2">
              <Label>Source record</Label>
              <Select
                disabled={!initiationSource}
                value={initiationSourceId || '__none__'}
                onValueChange={(value) => {
                  if (value === '__none__') {
                    setInitiationSourceId('');
                    setInitiationSourceDocumentVersionId('');
                    return;
                  }
                  if (initiationSource === 'ManagementDirective') {
                    const directive = lookups.managementDirectives.find(
                      (item) => item.centralDocumentRecordId === value
                    );
                    setInitiationSourceId(value);
                    setInitiationSourceDocumentVersionId(
                      directive?.centralDocumentVersionId ?? ''
                    );
                    return;
                  }
                  setInitiationSourceId(value);
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select source record" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__none__">Select source record</SelectItem>
                  {initiationSource === 'ManagementDirective'
                    ? lookups.managementDirectives.map((item) => (
                        <SelectItem
                          key={item.centralDocumentRecordId}
                          value={item.centralDocumentRecordId}
                        >
                          {item.label}
                        </SelectItem>
                      ))
                    : selectedInitiationSources.map((item) => (
                        <SelectItem key={item.id} value={item.id}>
                          {item.label}
                        </SelectItem>
                      ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2 lg:col-span-2">
              <Label>Property / site</Label>
              <Select
                value={estateManagedAssetId || '__none__'}
                onValueChange={(value) =>
                  setEstateManagedAssetId(value === '__none__' ? '' : value)
                }
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select controlled property/site" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__none__">Select property/site</SelectItem>
                  {lookups.estateManagedAssets.map((item) => (
                    <SelectItem key={item.id} value={item.id}>
                      {item.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Engineering category</Label>
              <Select
                value={engineeringCategoryId || '__none__'}
                onValueChange={(value) =>
                  setEngineeringCategoryId(value === '__none__' ? '' : value)
                }
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select category" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__none__">Select category</SelectItem>
                  {lookups.engineeringCategories.map((item) => (
                    <SelectItem key={item.id} value={item.id}>
                      {item.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Work classification</Label>
              <Select
                value={workClassification || '__none__'}
                onValueChange={(value) =>
                  setWorkClassification(
                    value === '__none__'
                      ? ''
                      : (value as CivilEngineeringWorkClassification)
                  )
                }
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select classification" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__none__">Select classification</SelectItem>
                  {lookups.workClassifications.map((item) => (
                    <SelectItem key={item} value={item}>
                      {workClassificationLabels[item] ?? item}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2 lg:col-span-2">
              <Label htmlFor="civil-design-title">Title</Label>
              <Input
                id="civil-design-title"
                value={title}
                onChange={(event) => setTitle(event.target.value)}
                placeholder="Structural design for Block A"
              />
            </div>
            <div className="space-y-2">
              <Label>Supervising Civil Engineer</Label>
              <Select
                value={sceUserId || '__none__'}
                onValueChange={(value) =>
                  setSceUserId(value === '__none__' ? '' : value)
                }
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select SCE" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__none__">Select SCE</SelectItem>
                  {lookups.supervisingCivilEngineers.map((member) => (
                    <SelectItem key={member.userId} value={member.userId}>
                      {member.displayName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="civil-design-create-due">Due date</Label>
              <Input
                id="civil-design-create-due"
                type="date"
                value={createDueDate}
                onChange={(event) => setCreateDueDate(event.target.value)}
              />
            </div>
            <div className="space-y-2 lg:col-span-2">
              <Label htmlFor="civil-design-scope">Scope summary</Label>
              <Textarea
                id="civil-design-scope"
                rows={3}
                value={scopeSummary}
                onChange={(event) => setScopeSummary(event.target.value)}
                placeholder="State the engineering scope to be assessed and designed."
              />
            </div>
            <div className="space-y-2 lg:col-span-2">
              <Label htmlFor="civil-design-constraints">Constraints</Label>
              <Textarea
                id="civil-design-constraints"
                rows={3}
                value={constraintSummary}
                onChange={(event) => setConstraintSummary(event.target.value)}
                placeholder="Record known site, technical, regulatory, or delivery constraints."
              />
            </div>
            <div className="space-y-2 lg:col-span-2">
              <Label htmlFor="civil-design-risks">Risks</Label>
              <Textarea
                id="civil-design-risks"
                rows={3}
                value={riskSummary}
                onChange={(event) => setRiskSummary(event.target.value)}
                placeholder="Record material delivery, safety, technical, or approval risks."
              />
            </div>
            <div className="space-y-2 lg:col-span-2">
              <Label htmlFor="civil-design-recommendation">Recommendation</Label>
              <Textarea
                id="civil-design-recommendation"
                rows={3}
                value={recommendation}
                onChange={(event) => setRecommendation(event.target.value)}
                placeholder="State the recommended engineering action and expected outcome."
              />
            </div>
            <div className="space-y-2 lg:col-span-3">
              <Label htmlFor="civil-design-directive">HOD directive</Label>
              <Textarea
                id="civil-design-directive"
                rows={3}
                value={directive}
                onChange={(event) => setDirective(event.target.value)}
                placeholder="State the controlled design instruction and required deliverables."
              />
            </div>
            <div className="flex items-end justify-end">
              <Button onClick={() => void createCase()} disabled={loading}>
                <ClipboardList className="mr-2 h-4 w-4" /> Create Engineering Case
              </Button>
            </div>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader className="pb-3">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <CardTitle className="text-base">Civil design workflow</CardTitle>
              <CardDescription>
                Design assignments, review evidence, decisions, and due dates.
              </CardDescription>
            </div>
            <Button
              variant="outline"
              size="sm"
              onClick={() => void load()}
              disabled={loading}
            >
              <RefreshCw className="mr-2 h-4 w-4" /> Refresh
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          {cases.length === 0 ? (
            <div className="rounded-lg border border-dashed p-6 text-sm text-muted-foreground">
              No Civil design directive has been created for this project.
            </div>
          ) : (
            <div className="grid gap-5 xl:grid-cols-[320px,minmax(0,1fr)]">
              <div className="space-y-2">
                {cases.map((item) => (
                  <button
                    type="button"
                    key={item.id}
                    onClick={() => void selectCase(item.id)}
                    className={`w-full rounded-lg border p-3 text-left transition-colors ${
                      item.id === selectedCase?.id
                        ? 'border-primary bg-primary/5'
                        : 'hover:bg-muted/60'
                    }`}
                  >
                    <div className="flex items-center justify-between gap-2">
                      <span className="font-medium">
                        {item.referenceNumber}
                      </span>
                      <Badge variant="outline">{stageLabels[item.stage]}</Badge>
                    </div>
                    <div className="mt-1 line-clamp-2 text-sm">
                      {item.title}
                    </div>
                    <div className="mt-2 text-xs text-muted-foreground">
                      Due {formatDate(item.currentDueAt)}
                    </div>
                  </button>
                ))}
              </div>

              {selectedCase ? (
                <div className="min-w-0 space-y-5">
                  <div className="rounded-lg border p-4">
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div>
                        <div className="flex flex-wrap items-center gap-2">
                          <h3 className="font-semibold">
                            {selectedCase.title}
                          </h3>
                          <Badge>{stageLabels[selectedCase.stage]}</Badge>
                          <Badge variant="secondary">
                            {selectedCase.approvalStatus}
                          </Badge>
                        </div>
                        <p className="mt-2 whitespace-pre-wrap text-sm text-muted-foreground">
                          {selectedCase.directive}
                        </p>
                      </div>
                      <div className="text-right text-xs text-muted-foreground">
                        <div>Due {formatDate(selectedCase.currentDueAt)}</div>
                        <div>
                          {selectedCase.evidence.length} evidence item(s)
                        </div>
                      </div>
                    </div>
                  </div>

                  <CivilEngineeringReconnaissancePanel
                    designCase={selectedCase}
                    lookups={lookups}
                    documents={documents}
                    onWorkflowChanged={load}
                  />

                  <CivilEngineeringPlanningGisPanel
                    designCase={selectedCase}
                    canManage={hasPermission('civil-engineering.permitting.manage')}
                    canApprove={canApprove}
                    onWorkflowChanged={load}
                  />

                  <CivilEngineeringCommercialReadinessPanel
                    designCase={selectedCase}
                  />

                  <CivilEngineeringInformationRequestsPanel
                    designCase={selectedCase}
                    lookups={lookups}
                    documents={documents}
                    canCreate={canManage && hasRole(roles.sce)}
                    canRespond={canRespond}
                    canReview={canApprove && hasRole(roles.sce)}
                    onWorkflowChanged={load}
                  />

                  <CivilEngineeringDocumentRegisterPanel
                    designCaseId={selectedCase.id}
                    canManage={hasPermission(
                      'civil-engineering.documents.manage'
                    )}
                    canApprove={canApprove}
                    onWorkflowChanged={load}
                  />

                  {selectedCase.stage === 'SceDrawingReview' &&
                  canManage &&
                  hasRole(roles.sce) ? (
                    <div className="space-y-4 rounded-lg border p-4">
                      <div>
                        <h4 className="font-medium">SCE design review</h4>
                        <p className="text-sm text-muted-foreground">
                          Select the controlled submission package, then
                          complete the shared design-review checklist before
                          routing to HOD.
                        </p>
                      </div>
                      <div className="grid gap-4 lg:grid-cols-2">
                        <div className="space-y-2">
                          <Label>Submission package</Label>
                          <Select
                            value={documentRecordId || '__none__'}
                            onValueChange={(value) =>
                              setDocumentRecordId(
                                value === '__none__' ? '' : value
                              )
                            }
                          >
                            <SelectTrigger>
                              <SelectValue placeholder="Select published DMS package" />
                            </SelectTrigger>
                            <SelectContent>
                              <SelectItem value="__none__">
                                Select published DMS package
                              </SelectItem>
                              {eligibleDocuments.map((document) => (
                                <SelectItem
                                  key={document.id}
                                  value={document.id}
                                >
                                  {document.documentReference} —{' '}
                                  {document.title}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </div>
                        <div className="space-y-2">
                          <Label htmlFor="civil-design-hod-due">
                            HOD review due date
                          </Label>
                          <Input
                            id="civil-design-hod-due"
                            type="date"
                            value={actionDueDate}
                            onChange={(event) =>
                              setActionDueDate(event.target.value)
                            }
                          />
                        </div>
                      </div>
                      <WorkflowApprovalActions
                        entityType="PROJECT_DESIGN_REVIEW"
                        entityId={selectedCase.id}
                        entityLabel="Civil design package"
                        entityNumber={selectedCase.referenceNumber}
                        status="PendingApproval"
                        loadWorkflowSummary
                        showStepBadge
                        canSubmit={false}
                        canApproveReject
                        approveLabel="Submit package to HOD"
                        rejectLabel="Return drawings"
                        forwardActionsDisabled={
                          !documentRecordId || !actionDueDate || loading
                        }
                        forwardActionsDisabledReason="Select a current published DMS submission package and HOD review due date."
                        onApprove={async (comments) =>
                          runSharedReviewAction('SubmitPackage', comments)
                        }
                        onReject={async (comments) =>
                          runSharedReviewAction('ReturnDrawings', comments)
                        }
                        onAfterAction={async () => {
                          await load();
                          await loadHistory(selectedCase.id);
                        }}
                      />
                    </div>
                  ) : null}

                  {selectedCase.stage === 'HodFinalReview' &&
                  canApprove &&
                  hasRole(roles.hod) ? (
                    <div className="rounded-lg border p-4">
                      <WorkflowApprovalActions
                        entityType="PROJECT_DESIGN_REVIEW"
                        entityId={selectedCase.id}
                        entityLabel="Civil design package"
                        entityNumber={selectedCase.referenceNumber}
                        status="PendingApproval"
                        loadWorkflowSummary
                        showStepBadge
                        canSubmit={false}
                        canApproveReject
                        onApprove={async (comments) =>
                          runSharedReviewAction('Approve', comments)
                        }
                        onReject={async (comments) =>
                          runSharedReviewAction('Reject', comments)
                        }
                        onAfterAction={async () => {
                          await load();
                          await loadHistory(selectedCase.id);
                        }}
                      />
                    </div>
                  ) : null}

                  {availableActions.length > 0 && actionAllowed ? (
                    <div className="rounded-lg border p-4">
                      <div className="mb-3 flex flex-wrap gap-2">
                        {availableActions.map((option) => (
                          <Button
                            key={option.action}
                            type="button"
                            size="sm"
                            variant={
                              option.action === 'Reject' ||
                              option.action === 'Cancel' ||
                              option.action.startsWith('Return')
                                ? 'destructive'
                                : selectedAction?.action === option.action
                                  ? 'default'
                                  : 'outline'
                            }
                            onClick={() => {
                              setSelectedAction(option);
                              setAssigneeUserId('');
                              setDocumentRecordId('');
                              setReason('');
                            }}
                          >
                            {option.action === 'Approve' ? (
                              <ShieldCheck className="mr-2 h-4 w-4" />
                            ) : option.action === 'Reject' ? (
                              <ShieldX className="mr-2 h-4 w-4" />
                            ) : option.action.startsWith('Return') ? (
                              <Undo2 className="mr-2 h-4 w-4" />
                            ) : option.action === 'Cancel' ? (
                              <XCircle className="mr-2 h-4 w-4" />
                            ) : option.assignee ? (
                              <UserRoundCheck className="mr-2 h-4 w-4" />
                            ) : (
                              <Send className="mr-2 h-4 w-4" />
                            )}
                            {option.label}
                          </Button>
                        ))}
                      </div>

                      {selectedAction ? (
                        <div className="grid gap-4 rounded-md bg-muted/30 p-3 lg:grid-cols-2">
                          {selectedAction.assignee ? (
                            <div className="space-y-2">
                              <Label>Assignee</Label>
                              <Select
                                value={assigneeUserId || '__none__'}
                                onValueChange={(value) =>
                                  setAssigneeUserId(
                                    value === '__none__' ? '' : value
                                  )
                                }
                              >
                                <SelectTrigger>
                                  <SelectValue placeholder="Select project member" />
                                </SelectTrigger>
                                <SelectContent>
                                  <SelectItem value="__none__">
                                    Select project member
                                  </SelectItem>
                                  {membersForAction(selectedAction).map(
                                    (
                                      member: CivilEngineeringDesignMemberLookup
                                    ) => (
                                      <SelectItem
                                        key={member.userId}
                                        value={member.userId}
                                      >
                                        {member.displayName}
                                      </SelectItem>
                                    )
                                  )}
                                </SelectContent>
                              </Select>
                            </div>
                          ) : null}
                          {selectedAction.evidenceType ? (
                            <div className="space-y-2">
                              <Label>
                                {selectedAction.evidenceType} evidence
                              </Label>
                              <Select
                                value={documentRecordId || '__none__'}
                                onValueChange={(value) =>
                                  setDocumentRecordId(
                                    value === '__none__' ? '' : value
                                  )
                                }
                              >
                                <SelectTrigger>
                                  <SelectValue placeholder="Select published DMS document" />
                                </SelectTrigger>
                                <SelectContent>
                                  <SelectItem value="__none__">
                                    Select published DMS document
                                  </SelectItem>
                                  {eligibleDocuments.map((document) => (
                                    <SelectItem
                                      key={document.id}
                                      value={document.id}
                                    >
                                      {document.documentReference} —{' '}
                                      {document.title}
                                    </SelectItem>
                                  ))}
                                </SelectContent>
                              </Select>
                            </div>
                          ) : null}
                          <div className="space-y-2">
                            <Label htmlFor="civil-design-action-due">
                              Next due date
                            </Label>
                            <Input
                              id="civil-design-action-due"
                              type="date"
                              value={actionDueDate}
                              onChange={(event) =>
                                setActionDueDate(event.target.value)
                              }
                            />
                          </div>
                          <div className="space-y-2">
                            <Label htmlFor="civil-design-action-reason">
                              Reason{selectedAction.reasonRequired ? ' *' : ''}
                            </Label>
                            <Textarea
                              id="civil-design-action-reason"
                              rows={2}
                              value={reason}
                              onChange={(event) =>
                                setReason(event.target.value)
                              }
                              placeholder="Record the review or assignment reason."
                            />
                          </div>
                          <div className="flex justify-end lg:col-span-2">
                            <Button
                              onClick={() => void runAction()}
                              disabled={loading}
                            >
                              <CheckCircle2 className="mr-2 h-4 w-4" /> Confirm{' '}
                              {selectedAction.label}
                            </Button>
                          </div>
                        </div>
                      ) : null}
                    </div>
                  ) : null}

                  <div className="grid gap-5 lg:grid-cols-2">
                    <div className="rounded-lg border p-4">
                      <h4 className="mb-3 flex items-center gap-2 font-medium">
                        <FileCheck2 className="h-4 w-4" /> Evidence
                      </h4>
                      <div className="space-y-2">
                        {selectedCase.evidence.length === 0 ? (
                          <p className="text-sm text-muted-foreground">
                            No evidence has been linked yet.
                          </p>
                        ) : (
                          selectedCase.evidence.map((item) => (
                            <div
                              key={item.id}
                              className="rounded-md border p-3"
                            >
                              <div className="flex flex-wrap items-center gap-2">
                                <Badge variant="outline">
                                  {item.evidenceType}
                                </Badge>
                                <span className="text-sm font-medium">
                                  {item.documentReference}
                                </span>
                              </div>
                              <div className="mt-1 text-xs text-muted-foreground">
                                {item.documentTitle} · v{item.versionNumber}
                              </div>
                            </div>
                          ))
                        )}
                      </div>
                    </div>

                    <div className="rounded-lg border p-4">
                      <h4 className="mb-3 flex items-center gap-2 font-medium">
                        <History className="h-4 w-4" /> History
                      </h4>
                      {!canAudit ? (
                        <p className="text-sm text-muted-foreground">
                          Civil Engineering audit permission is required.
                        </p>
                      ) : history.length === 0 ? (
                        <p className="text-sm text-muted-foreground">
                          No history is available.
                        </p>
                      ) : (
                        <div className="max-h-72 space-y-2 overflow-y-auto">
                          {history.map((item) => (
                            <div
                              key={item.id}
                              className="rounded-md border p-3"
                            >
                              <div className="text-sm font-medium">
                                {item.fromStage} → {item.toStage}
                              </div>
                              <div className="mt-1 text-xs text-muted-foreground">
                                {item.actorName} · {formatDate(item.timestamp)}
                              </div>
                              {item.reason ? (
                                <p className="mt-1 whitespace-pre-wrap text-xs">
                                  {item.reason}
                                </p>
                              ) : null}
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              ) : null}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
