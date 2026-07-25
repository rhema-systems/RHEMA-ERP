'use client';

import React, { useState, useEffect, useMemo } from 'react';
import ReactFlow, {
  Node,
  Edge,
  Background,
  BackgroundVariant,
  Controls,
  MiniMap,
  useNodesState,
  useEdgesState,
  ReactFlowProvider,
  NodeTypes
} from 'reactflow';
import 'reactflow/dist/style.css';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { workflowApiService } from '@/services/workflow-api.service';
import { toast } from 'sonner';
import Link from 'next/link';
import type {
  WorkflowStatusDto,
  WorkflowStepStatusDto,
  WorkflowApprovalStatusDto,
  WorkflowDirectoryUser
} from '@/types/workflow';
import {
  WorkflowInstanceStatus,
  WorkflowStepInstanceStatus
} from '@/types/workflow';

import {
  Play, Pause, Clock, CheckCircle, AlertCircle,
  XCircle, Timer, RefreshCw, Eye, Filter, Search, UserRoundPlus, UserMinus
} from 'lucide-react';

// Import custom node types
import { StartNode } from './nodes/StartNode';
import { EndNode } from './nodes/EndNode';
import { TaskNode } from './nodes/TaskNode';
import { ApprovalNode } from './nodes/ApprovalNode';
import { ConditionNode } from './nodes/ConditionNode';
import { NotificationNode } from './nodes/NotificationNode';
import { DocumentNode } from './nodes/DocumentNode';
import { EscalationNode } from './nodes/EscalationNode';
import { IntegrationNode } from './nodes/IntegrationNode';
import { WorkflowReasonDialog } from './WorkflowReasonDialog';

const nodeTypes: NodeTypes = {
  start: StartNode,
  end: EndNode,
  task: TaskNode,
  approval: ApprovalNode,
  condition: ConditionNode,
  notification: NotificationNode,
  document: DocumentNode,
  escalation: EscalationNode,
  integration: IntegrationNode,
};

type UiWorkflowStatus = 'running' | 'completed' | 'failed' | 'paused' | 'cancelled';

interface WorkflowInstanceView {
  id: string;
  workflowDefinitionId: string;
  workflowName: string;
  entityType: string;
  entityId: string;
  status: UiWorkflowStatus;
  currentStepId: string;
  currentStepName: string;
  startedAt: string;
  completedAt?: string;
  assignedTo?: string;
  priority: 'low' | 'medium' | 'high';
  progress: number;
  steps: WorkflowStepStatusDto[];
  pendingApprovals: WorkflowApprovalStatusDto[];
  entityLink?: string | null;
}

interface WorkflowInstanceMonitorProps {
  isOpen: boolean;
  onClose: () => void;
  workflowDefinitionId?: string;
  workflowName?: string;
  onLiveInstancesCountChanged?: (count: number) => void;
}

export function WorkflowInstanceMonitor({
  isOpen,
  onClose,
  workflowDefinitionId,
  workflowName,
  onLiveInstancesCountChanged,
}: WorkflowInstanceMonitorProps) {
  const [instances, setInstances] = useState<WorkflowStatusDto[]>([]);
  const [selectedInstanceId, setSelectedInstanceId] = useState<string | null>(null);
  const [nodes, setNodes, onNodesChange] = useNodesState([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState([]);
  const [filterStatus, setFilterStatus] = useState<string>('all');
  const [searchTerm, setSearchTerm] = useState('');
  const [activeTab, setActiveTab] = useState('list');
  const [isLoading, setIsLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  // Instance cancellation and ad-hoc approver management are independent governance actions.
  const [cancellingInstanceId, setCancellingInstanceId] = useState<string | null>(null);
  const [approverDialogOpen, setApproverDialogOpen] = useState(false);
  const [directoryUsers, setDirectoryUsers] = useState<WorkflowDirectoryUser[]>([]);
  const [adHocUserId, setAdHocUserId] = useState('');
  const [adHocRole, setAdHocRole] = useState('');
  const [adHocReason, setAdHocReason] = useState('');
  const [adHocGroup, setAdHocGroup] = useState(1);
  const [approverSaving, setApproverSaving] = useState(false);
  const [removeApproverTarget, setRemoveApproverTarget] = useState<WorkflowApprovalStatusDto | null>(null);

  const instanceStatusName = (status: WorkflowInstanceStatus | string | number | undefined) => {
    if (typeof status === 'number') {
      return WorkflowInstanceStatus[status] || String(status);
    }
    return String(status || '');
  };

  const stepStatusName = (status: WorkflowStepInstanceStatus | string | number | undefined) => {
    if (typeof status === 'number') {
      return WorkflowStepInstanceStatus[status] || String(status);
    }
    return String(status || '');
  };

  const isInstanceStatus = (status: WorkflowInstanceStatus | string | number | undefined, expected: keyof typeof WorkflowInstanceStatus) =>
    instanceStatusName(status).toLowerCase() === expected.toLowerCase();

  const isStepStatus = (status: WorkflowStepInstanceStatus | string | number | undefined, expected: keyof typeof WorkflowStepInstanceStatus) =>
    stepStatusName(status).toLowerCase() === expected.toLowerCase();

  const formatApprovers = (approvals: WorkflowApprovalStatusDto[], maxNames = 2) => {
    const names = (approvals || [])
      .map(approval => approval.approverName?.trim())
      .filter(Boolean);
    const full = names.join(', ');
    if (names.length <= maxNames) {
      return { short: full, full };
    }

    return {
      short: `${names.slice(0, maxNames).join(', ')} +${names.length - maxNames}`,
      full,
    };
  };

  const toUiStatus = (status: WorkflowInstanceStatus | string | number): UiWorkflowStatus => {
    if (isInstanceStatus(status, 'Completed')) {
      return 'completed';
    }
    if (isInstanceStatus(status, 'Cancelled')) {
      return 'cancelled';
    }
    if (isInstanceStatus(status, 'Failed')) {
      return 'failed';
    }
    if (isInstanceStatus(status, 'Suspended') || isInstanceStatus(status, 'Waiting')) {
      return 'paused';
    }

    return 'running';
  };

  const getCurrentStep = (steps: WorkflowStepStatusDto[]): WorkflowStepStatusDto | null => {
    if (!steps || steps.length === 0) return null;
    const inProgress = steps.find(step => isStepStatus(step.status, 'InProgress'));
    if (inProgress) return inProgress;
    const pending = steps.find(step => isStepStatus(step.status, 'Pending'));
    if (pending) return pending;
    const completedSteps = steps
      .filter(step => isStepStatus(step.status, 'Completed'))
      .sort((a, b) => {
        const aDate = a.completedDate ? new Date(a.completedDate).getTime() : 0;
        const bDate = b.completedDate ? new Date(b.completedDate).getTime() : 0;
        return bDate - aDate;
      });
    return completedSteps[0] ?? steps[0];
  };

  const calculateProgress = (status: WorkflowStatusDto) => {
    if (status.progress && typeof status.progress.percentComplete === 'number') {
      return Math.max(0, Math.min(100, status.progress.percentComplete));
    }
    if (!status.steps || status.steps.length === 0) return 0;
    const completed = status.steps.filter(step => isStepStatus(step.status, 'Completed')).length;
    return Math.round((completed / status.steps.length) * 100);
  };

  const determinePriority = (
    status: WorkflowStatusDto,
    steps: WorkflowStepStatusDto[],
    approvals: WorkflowApprovalStatusDto[]
  ): 'low' | 'medium' | 'high' => {
    const hasOverdueStep = steps.some(step => step.isOverdue);
    const hasOverdueApproval = approvals.some(approval => approval.isOverdue);

    if (hasOverdueStep || hasOverdueApproval || isInstanceStatus(status.status, 'Failed')) {
      return 'high';
    }

    if (isInstanceStatus(status.status, 'Completed') || isInstanceStatus(status.status, 'Cancelled')) {
      return 'low';
    }

    return 'medium';
  };

  const normalizeEntityType = (value: string) =>
    value.replace(/[\s_-]+/g, '').toLowerCase();

  const entityRouteMap: Record<string, (id: string) => string> = {
    journalentry: (id) => `/finance/journal-entries/${id}`,
    journalentries: (id) => `/finance/journal-entries/${id}`,
    jobcard: (id) => `/maintenance/job-cards?id=${id}`,
    workorder: (id) => `/maintenance/work-orders?id=${id}`,
    asset: (id) => `/maintenance/assets?id=${id}`,
    purchaseorder: (id) => `/procurement/purchase-orders/${id}`,
    purchaseorders: (id) => `/procurement/purchase-orders/${id}`,
    purchaserequisition: (id) => `/procurement/purchase-requisitions/${id}`,
    purchaserequisitions: (id) => `/procurement/purchase-requisitions/${id}`,
    purchasereceipt: (id) => `/procurement/purchase-receipts/${id}`,
    purchasereceipts: (id) => `/procurement/purchase-receipts/${id}`,
    tender: (id) => `/procurement/tenders/${id}`,
    tenderaward: (id) => `/procurement/awards/${id}`,
    contract: (id) => `/procurement/contracts/${id}`,
    businesspartner: (id) => `/procurement/business-partners/${id}`,
  };

  const getEntityLink = (entityType: string, entityId: string) => {
    if (!entityType || !entityId) return null;
    const key = normalizeEntityType(entityType);
    const resolver = entityRouteMap[key];
    return resolver ? resolver(entityId) : null;
  };

  const formatDate = (value?: string | Date) => {
    if (!value) return 'N/A';
    const date = typeof value === 'string' ? new Date(value) : value;
    if (Number.isNaN(date.getTime())) return 'N/A';
    return date.toLocaleString();
  };

  const formatDuration = (startedAt?: string, completedAt?: string) => {
    if (!startedAt) return 'N/A';
    const startDate = new Date(startedAt);
    if (Number.isNaN(startDate.getTime())) return 'N/A';
    const endDate = completedAt ? new Date(completedAt) : new Date();
    if (Number.isNaN(endDate.getTime())) return 'N/A';
    const hours = Math.floor((endDate.getTime() - startDate.getTime()) / (1000 * 60 * 60));
    return completedAt ? `${hours}h` : `${hours}h (ongoing)`;
  };

  const instanceViews = useMemo<WorkflowInstanceView[]>(() => {
    return instances.map(instance => {
      const steps = instance.steps || [];
      const status = toUiStatus(instance.status);
      const rawApprovals = instance.pendingApprovals || [];
      const approvals = status === 'completed' || status === 'cancelled' || status === 'failed'
        ? []
        : rawApprovals;
      const currentStep = getCurrentStep(steps);
      const startedAt = instance.startedDate ? new Date(instance.startedDate).toISOString() : '';
      const completedAt = instance.completedDate ? new Date(instance.completedDate).toISOString() : undefined;
      const entityLink = getEntityLink(instance.entityType, instance.entityId);
      return {
        id: instance.workflowInstanceId,
        workflowDefinitionId: 'N/A',
        workflowName: instance.workflowName,
        entityType: instance.entityType || 'Workflow',
        entityId: instance.entityId || instance.workflowInstanceId,
        status,
        currentStepId: instance.currentStepInstanceId || currentStep?.stepInstanceId || '',
        currentStepName: instance.currentStepName || currentStep?.stepName || 'No current step',
        startedAt,
        completedAt,
        assignedTo: currentStep?.assignedToName || formatApprovers(approvals).short || 'Unassigned',
        priority: determinePriority(instance, steps, approvals),
        progress: calculateProgress(instance),
        steps,
        pendingApprovals: approvals,
        entityLink,
      };
    });
  }, [instances]);

  const selectedInstance = useMemo(
    () => instanceViews.find(instance => instance.id === selectedInstanceId) ?? null,
    [instanceViews, selectedInstanceId]
  );

  const loadInstances = async () => {
    if (!isOpen) return;
    setIsLoading(true);
    setLoadError(null);
    try {
      const baseFilter = {
        page: 1,
        pageSize: 100,
        sortBy: 'StartedDate',
        sortDescending: true,
        workflowDefinitionId,
      };

      const result = workflowDefinitionId
        ? await Promise.all(['Created', 'InProgress', 'Waiting', 'Suspended'].map((status) =>
            workflowApiService.getWorkflowInstances({
              ...baseFilter,
              status,
            })
          )).then((responses) => ({
            data: Array.from(
              new Map(responses.flatMap((response) => response.data).map((instance) => [instance.workflowInstanceId, instance])).values()
            ),
          }))
        : await workflowApiService.getWorkflowInstances(baseFilter);

      setInstances(result.data);
      if (workflowDefinitionId) {
        onLiveInstancesCountChanged?.(result.data.length);
      }
      if (selectedInstanceId && !result.data.some(item => item.workflowInstanceId === selectedInstanceId)) {
        setSelectedInstanceId(null);
      }
    } catch (error) {
      console.error('Failed to load workflow instances:', error);
      setLoadError('Failed to load workflow instances.');
    } finally {
      setIsLoading(false);
    }
  };

  const openApproverDialog = async () => {
    setApproverDialogOpen(true);
    if (directoryUsers.length === 0) {
      try { setDirectoryUsers(await workflowApiService.getWorkflowDirectoryUsers()); }
      catch (error: any) { toast.error(error?.message || 'Unable to load users'); }
    }
  };

  const addAdHocApprover = async () => {
    if (!selectedInstance?.currentStepId || (!adHocUserId && !adHocRole.trim()) || !adHocReason.trim()) {
      toast.error('Select a user or enter a role, and provide a reason.'); return;
    }
    try {
      setApproverSaving(true);
      await workflowApiService.addAdHocApprover(selectedInstance.currentStepId, {
        userId: adHocUserId || undefined, role: adHocUserId ? undefined : adHocRole.trim(),
        approvalGroup: adHocGroup, reason: adHocReason.trim(),
      });
      setAdHocUserId(''); setAdHocRole(''); setAdHocReason(''); await loadInstances();
      toast.success('Ad hoc approver added');
    } catch (error: any) { toast.error(error?.message || 'Failed to add approver'); }
    finally { setApproverSaving(false); }
  };

  const removeAdHocApprover = async (reason: string) => {
    if (!removeApproverTarget) return;
    try {
      setApproverSaving(true);
      await workflowApiService.removeAdHocApprover(removeApproverTarget.approvalId, reason);
      setRemoveApproverTarget(null);
      await loadInstances();
      toast.success('Ad hoc approver removed');
    }
    catch (error: any) { toast.error(error?.message || 'Failed to remove approver'); }
    finally { setApproverSaving(false); }
  };

  useEffect(() => {
    if (isOpen) {
      loadInstances();
    }
  }, [isOpen, workflowDefinitionId]);

  useEffect(() => {
    if (selectedInstance) {
      loadWorkflowVisualization(selectedInstance);
    } else {
      setNodes([]);
      setEdges([]);
    }
  }, [selectedInstance, setEdges, setNodes]);

  const loadWorkflowVisualization = (instance: WorkflowInstanceView) => {
    if (!instance.steps || instance.steps.length === 0) {
      setNodes([]);
      setEdges([]);
      return;
    }

    const approvalStepNames = new Set(
      instance.pendingApprovals.map(approval => approval.stepName)
    );

    const stepsSorted = [...instance.steps].sort((a, b) => {
      const aDate = a.startedDate ?? a.completedDate ?? a.dueDate;
      const bDate = b.startedDate ?? b.completedDate ?? b.dueDate;
      if (aDate && bDate) {
        return new Date(aDate).getTime() - new Date(bDate).getTime();
      }
      if (aDate) return -1;
      if (bDate) return 1;
      return a.stepName.localeCompare(b.stepName);
    });

    const getNodeClassName = (status: WorkflowStepInstanceStatus | string | number) => {
      if (isStepStatus(status, 'Completed')) {
        return 'completed-node';
      }
      if (isStepStatus(status, 'InProgress')) {
        return 'active-node';
      }
      return 'pending-node';
    };

    const nodes: Node[] = [
      {
        id: 'start',
        type: 'start',
        position: { x: 250, y: 50 },
        data: { label: 'Start' },
      },
    ];

    stepsSorted.forEach((step, index) => {
      const yOffset = 150 + index * 120;
      const approvers = instance.pendingApprovals
        .filter(approval => approval.stepName === step.stepName)
        .map(approval => approval.approverName);
      const isApproval = approvalStepNames.has(step.stepName);
      nodes.push({
        id: step.stepInstanceId,
        type: isApproval ? 'approval' : 'task',
        position: { x: 250, y: yOffset },
        data: isApproval
          ? {
              label: step.stepName,
              approvers,
              approvalType: 'any',
            }
          : {
              label: step.stepName,
              assignee: step.assignedToName ?? 'Unassigned',
              priority: instance.priority,
              dueDate: step.dueDate ? new Date(step.dueDate).toLocaleDateString() : undefined,
            },
        className: getNodeClassName(step.status),
      });
    });

    const endOffset = 150 + stepsSorted.length * 120;
    nodes.push({
      id: 'end',
      type: 'end',
      position: { x: 250, y: endOffset },
      data: { label: 'End' },
      className:
        instance.status === 'completed'
          ? 'completed-node'
          : 'pending-node',
    });

    const edges: Edge[] = [];
    if (stepsSorted.length > 0) {
      edges.push({ id: 'e-start', source: 'start', target: stepsSorted[0].stepInstanceId });
    }
    stepsSorted.forEach((step, index) => {
      if (index === stepsSorted.length - 1) return;
      edges.push({
        id: `e-${step.stepInstanceId}-${stepsSorted[index + 1].stepInstanceId}`,
        source: step.stepInstanceId,
        target: stepsSorted[index + 1].stepInstanceId,
      });
    });
    if (stepsSorted.length > 0) {
      edges.push({
        id: `e-${stepsSorted[stepsSorted.length - 1].stepInstanceId}-end`,
        source: stepsSorted[stepsSorted.length - 1].stepInstanceId,
        target: 'end',
      });
    }

    setNodes(nodes);
    setEdges(edges);
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'running': return <Play className="h-4 w-4 text-blue-600" />;
      case 'completed': return <CheckCircle className="h-4 w-4 text-green-600" />;
      case 'failed': return <XCircle className="h-4 w-4 text-red-600" />;
      case 'paused': return <Pause className="h-4 w-4 text-yellow-600" />;
      case 'cancelled': return <XCircle className="h-4 w-4 text-gray-600" />;
      default: return <AlertCircle className="h-4 w-4 text-gray-600" />;
    }
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'running': return 'bg-blue-100 text-blue-800';
      case 'completed': return 'bg-green-100 text-green-800';
      case 'failed': return 'bg-red-100 text-red-800';
      case 'paused': return 'bg-yellow-100 text-yellow-800';
      case 'cancelled': return 'bg-gray-100 text-gray-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  const getPriorityColor = (priority: string) => {
    switch (priority) {
      case 'high': return 'bg-red-100 text-red-800';
      case 'medium': return 'bg-yellow-100 text-yellow-800';
      case 'low': return 'bg-green-100 text-green-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  const filteredInstances = instanceViews.filter(instance => {
    const matchesStatus = filterStatus === 'all' || instance.status === filterStatus;
    const matchesSearch = searchTerm === '' || 
      instance.workflowName.toLowerCase().includes(searchTerm.toLowerCase()) ||
      instance.entityId.toLowerCase().includes(searchTerm.toLowerCase()) ||
      instance.entityType.toLowerCase().includes(searchTerm.toLowerCase()) ||
      instance.currentStepName.toLowerCase().includes(searchTerm.toLowerCase());
    return matchesStatus && matchesSearch;
  });

  const canCancelInstance = (instance: WorkflowInstanceView | null) =>
    !!instance && (instance.status === 'running' || instance.status === 'paused');

  const handleCancelInstance = async (instance: WorkflowInstanceView) => {
    const confirmed = window.confirm(
      `Cancel workflow instance for ${instance.entityType} ${instance.entityId}? This will remove it from the live workflow pipeline.`
    );
    if (!confirmed) return;

    try {
      setCancellingInstanceId(instance.id);
      await workflowApiService.cancelWorkflow(instance.id, {
        reason: 'Cancelled by workflow administrator to unblock workflow configuration.',
      });
      await loadInstances();
    } catch (error) {
      console.error('Failed to cancel workflow instance:', error);
      setLoadError('Failed to cancel workflow instance.');
    } finally {
      setCancellingInstanceId(null);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[70] bg-black bg-opacity-50 flex items-center justify-center">
      <div className="bg-white rounded-lg shadow-lg w-[95vw] h-[95vh] flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between p-4 border-b">
          <div>
            <h2 className="text-xl font-semibold">Workflow Instance Monitor</h2>
            {workflowName && (
              <p className="text-sm text-muted-foreground">Showing live instances for {workflowName}</p>
            )}
          </div>
          <div className="flex items-center space-x-2">
            <Button variant="outline" size="sm" onClick={loadInstances} disabled={isLoading}>
              <RefreshCw className="h-4 w-4 mr-2" />
              {isLoading ? 'Refreshing...' : 'Refresh'}
            </Button>
            <Button variant="outline" onClick={onClose}>
              <XCircle className="h-4 w-4" />
            </Button>
          </div>
        </div>

        <div className="flex flex-1 overflow-hidden">
          {/* Left Sidebar - Instance List */}
          <div className="w-1/3 border-r flex flex-col">
            {/* Filters */}
            <div className="p-4 border-b space-y-3">
              <div className="flex space-x-2">
                <div className="flex-1">
                  <Input
                    placeholder="Search instances..."
                    value={searchTerm}
                    onChange={(e) => setSearchTerm(e.target.value)}
                    className="w-full"
                  />
                </div>
                <Button variant="outline" size="sm">
                  <Filter className="h-4 w-4" />
                </Button>
              </div>
              
              <Select value={filterStatus} onValueChange={setFilterStatus}>
                <SelectTrigger>
                  <SelectValue placeholder="Filter by status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Status</SelectItem>
                  <SelectItem value="running">Running</SelectItem>
                  <SelectItem value="completed">Completed</SelectItem>
                  <SelectItem value="paused">Paused</SelectItem>
                  <SelectItem value="failed">Failed</SelectItem>
                  <SelectItem value="cancelled">Cancelled</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {/* Instance List */}
            <ScrollArea className="flex-1">
              <div className="space-y-2 p-4">
                {isLoading && (
                  <div className="text-sm text-muted-foreground">Loading workflow instances...</div>
                )}
                {!isLoading && loadError && (
                  <div className="text-sm text-red-600">{loadError}</div>
                )}
                {!isLoading && !loadError && filteredInstances.length === 0 && (
                  <div className="text-sm text-muted-foreground">No workflow instances found.</div>
                )}
                {filteredInstances.map((instance) => {
                  const pending = formatApprovers(instance.pendingApprovals);

                  return (
                  <Card 
                    key={instance.id}
                    className={`cursor-pointer transition-colors ${
                      selectedInstance?.id === instance.id ? 'bg-blue-50 border-blue-200' : 'hover:bg-gray-50'
                    }`}
                    onClick={() => setSelectedInstanceId(instance.id)}
                  >
                    <CardContent className="p-3">
                      <div className="flex items-start justify-between mb-2">
                        <div className="flex-1">
                          <h4 className="font-medium text-sm">{instance.workflowName}</h4>
                          <p className="text-xs text-gray-600">{instance.entityId}</p>
                          <div className="flex items-center gap-2 mt-1">
                            <Badge variant="outline" className="text-[10px] px-2">
                              {instance.entityType}
                            </Badge>
                            {instance.entityLink && (
                              <Button variant="link" size="sm" className="h-auto p-0 text-xs" asChild>
                                <Link href={instance.entityLink} target="_blank" rel="noreferrer">
                                  Open
                                </Link>
                              </Button>
                            )}
                          </div>
                        </div>
                        <div className="flex items-center space-x-1">
                          {getStatusIcon(instance.status)}
                          <Badge variant="outline" className={`text-xs ${getStatusColor(instance.status)}`}>
                            {instance.status}
                          </Badge>
                        </div>
                      </div>
                      
                      <div className="space-y-1">
                        <div className="flex items-center justify-between text-xs">
                          <span className="text-gray-600">Current Step:</span>
                          <span className="font-medium">{instance.currentStepName}</span>
                        </div>
                        
                        <div className="flex items-center justify-between text-xs">
                          <span className="text-gray-600">Assigned To:</span>
                          <span>{instance.assignedTo}</span>
                        </div>

                        {pending.short && (
                          <div className="flex items-center justify-between text-xs">
                            <span className="text-gray-600">Pending With:</span>
                            <span className="font-medium" title={pending.full}>{pending.short}</span>
                          </div>
                        )}
                        
                        <div className="flex items-center justify-between text-xs">
                          <span className="text-gray-600">Priority:</span>
                          <Badge variant="outline" className={`text-xs ${getPriorityColor(instance.priority)}`}>
                            {instance.priority}
                          </Badge>
                        </div>
                        
                        <div className="mt-2">
                          <div className="flex justify-between text-xs mb-1">
                            <span>Progress</span>
                          <span>{instance.progress}%</span>
                        </div>
                        <div className="w-full bg-gray-200 rounded-full h-1.5">
                          <div 
                            className="bg-blue-600 h-1.5 rounded-full" 
                              style={{ width: `${instance.progress}%` }}
                            />
                          </div>
                        </div>
                      </div>
                    </CardContent>
                  </Card>
                  );
                })}
              </div>
            </ScrollArea>
          </div>

          {/* Right Panel - Instance Details & Visualization */}
          <div className="flex-1 flex flex-col">
            {selectedInstance ? (
              <>
                {/* Instance Header */}
                <div className="p-4 border-b bg-gray-50">
                  <div className="flex items-center justify-between mb-2">
                    <h3 className="text-lg font-semibold">{selectedInstance.workflowName}</h3>
                    <div className="flex items-center space-x-2">
                      {/* Keep both governance actions: cancellation for bad runs, approver editing for live approvals. */}
                      {canCancelInstance(selectedInstance) && (
                        <Button
                          variant="destructive"
                          size="sm"
                          onClick={() => handleCancelInstance(selectedInstance)}
                          disabled={cancellingInstanceId === selectedInstance.id}
                        >
                          <XCircle className="h-4 w-4 mr-2" />
                          {cancellingInstanceId === selectedInstance.id ? 'Cancelling...' : 'Cancel Instance'}
                        </Button>
                      )}
                      {selectedInstance.currentStepId && selectedInstance.pendingApprovals.length > 0 && (
                        <Button size="sm" variant="outline" onClick={() => void openApproverDialog()}><UserRoundPlus className="mr-2 h-4 w-4" />Approvers</Button>
                      )}
                      {getStatusIcon(selectedInstance.status)}
                      <Badge className={`${getStatusColor(selectedInstance.status)}`}>
                        {selectedInstance.status.toUpperCase()}
                      </Badge>
                    </div>
                  </div>
                  
                  <div className="grid grid-cols-1 gap-4 text-sm md:grid-cols-2 xl:grid-cols-5">
                    <div className="min-w-0">
                      <span className="text-gray-600">Entity ID:</span>
                      <div className="flex items-center gap-2">
                        <p className="break-all font-medium">{selectedInstance.entityId}</p>
                        {selectedInstance.entityLink && (
                          <Button variant="link" size="sm" className="h-auto p-0 text-xs" asChild>
                            <Link href={selectedInstance.entityLink} target="_blank" rel="noreferrer">
                              Open
                            </Link>
                          </Button>
                        )}
                      </div>
                    </div>
                    <div className="min-w-0">
                      <span className="text-gray-600">Current Step:</span>
                      <p className="break-words font-medium">{selectedInstance.currentStepName}</p>
                    </div>
                    <div className="min-w-0">
                      <span className="text-gray-600">Assigned To:</span>
                      <p className="break-words font-medium">{selectedInstance.assignedTo}</p>
                    </div>
                    <div className="min-w-0">
                      <span className="text-gray-600">Pending With:</span>
                      <p className="break-words font-medium" title={formatApprovers(selectedInstance.pendingApprovals).full}>
                        {formatApprovers(selectedInstance.pendingApprovals).short || 'None'}
                      </p>
                    </div>
                    <div>
                      <span className="text-gray-600">Started:</span>
                      <p className="font-medium">{formatDate(selectedInstance.startedAt)}</p>
                    </div>
                  </div>
                </div>

                {/* Tabs */}
                <Tabs value={activeTab} onValueChange={setActiveTab} className="flex-1 flex flex-col">
                  <TabsList className="mx-4 mt-4 w-fit">
                    <TabsTrigger value="visualization">Workflow Visualization</TabsTrigger>
                    <TabsTrigger value="history">Step History</TabsTrigger>
                    <TabsTrigger value="details">Details</TabsTrigger>
                  </TabsList>
                  
                  <TabsContent value="visualization" className="flex-1 m-0">
                    <div className="h-full">
                      <ReactFlowProvider>
                        <ReactFlow
                          nodes={nodes}
                          edges={edges}
                          onNodesChange={onNodesChange}
                          onEdgesChange={onEdgesChange}
                          nodeTypes={nodeTypes}
                          fitView
                          className="workflow-visualization"
                        >
                          <Controls />
                          <MiniMap />
                          <Background variant={BackgroundVariant.Dots} gap={12} size={1} />
                        </ReactFlow>
                      </ReactFlowProvider>
                    </div>
                  </TabsContent>
                  
                  <TabsContent value="history" className="flex-1 p-4">
                    <div className="space-y-3">
                      {selectedInstance.steps.length === 0 && (
                        <div className="text-sm text-muted-foreground">No step history available.</div>
                      )}
                      {selectedInstance.steps.map((step) => {
                        const isCompleted = isStepStatus(step.status, 'Completed');
                        const isInProgress = isStepStatus(step.status, 'InProgress');
                        const statusIcon = isCompleted ? (
                          <CheckCircle className="h-5 w-5 text-green-600 mr-3" />
                        ) : isInProgress ? (
                          <Clock className="h-5 w-5 text-blue-600 mr-3" />
                        ) : (
                          <Timer className="h-5 w-5 text-gray-400 mr-3" />
                        );

                        const statusText = isCompleted
                          ? 'Completed'
                          : isInProgress
                            ? 'In Progress'
                            : 'Pending';

                        const panelClass = isCompleted
                          ? 'bg-green-50 border-green-200'
                          : isInProgress
                            ? 'bg-blue-50 border-blue-200'
                            : 'bg-gray-50 border-gray-200 opacity-70';

                        return (
                          <div
                            key={step.stepInstanceId}
                            className={`flex items-center p-3 border rounded ${panelClass}`}
                          >
                            {statusIcon}
                            <div className="flex-1">
                              <h4 className="font-medium">{step.stepName}</h4>
                              <p className="text-sm text-gray-600">
                                {step.assignedToName ? `Assigned to ${step.assignedToName}` : 'Unassigned'}
                              </p>
                            </div>
                            <div className="text-sm text-gray-500">
                              {isCompleted
                                ? formatDate(step.completedDate)
                                : step.startedDate
                                  ? formatDate(step.startedDate)
                                  : statusText}
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </TabsContent>
                  
                  <TabsContent value="details" className="flex-1 p-4">
                    <div className="grid grid-cols-2 gap-6">
                      <div>
                        <h4 className="font-medium mb-3">Instance Information</h4>
                        <div className="space-y-2 text-sm">
                          <div className="flex justify-between">
                            <span className="text-gray-600">Instance ID:</span>
                            <span>{selectedInstance.id}</span>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-gray-600">Workflow Definition:</span>
                            <span>{selectedInstance.workflowDefinitionId}</span>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-gray-600">Entity Type:</span>
                            <span>{selectedInstance.entityType}</span>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-gray-600">Priority:</span>
                            <Badge className={`${getPriorityColor(selectedInstance.priority)}`}>
                              {selectedInstance.priority}
                            </Badge>
                          </div>
                        </div>
                      </div>
                      
                      <div>
                        <h4 className="font-medium mb-3">Timing Information</h4>
                        <div className="space-y-2 text-sm">
                          <div className="flex justify-between">
                            <span className="text-gray-600">Started At:</span>
                            <span>{formatDate(selectedInstance.startedAt)}</span>
                          </div>
                          {selectedInstance.completedAt && (
                            <div className="flex justify-between">
                              <span className="text-gray-600">Completed At:</span>
                              <span>{formatDate(selectedInstance.completedAt)}</span>
                            </div>
                          )}
                          <div className="flex justify-between">
                            <span className="text-gray-600">Duration:</span>
                            <span>
                              {formatDuration(selectedInstance.startedAt, selectedInstance.completedAt)}
                            </span>
                          </div>
                        </div>
                      </div>
                    </div>

                    <div className="mt-6">
                      <h4 className="font-medium mb-3">Pending Approvals</h4>
                      {selectedInstance.pendingApprovals.length === 0 && (
                        <div className="text-sm text-muted-foreground">No pending approvals.</div>
                      )}
                      {selectedInstance.pendingApprovals.length > 0 && (
                        <div className="space-y-2">
                          {selectedInstance.pendingApprovals.map((approval) => (
                            <div
                              key={approval.approvalId}
                              className="flex items-center justify-between p-3 border rounded bg-amber-50"
                            >
                              <div>
                                <div className="text-sm font-medium">{approval.stepName}</div>
                                <div className="text-xs text-muted-foreground">
                                  {approval.approverName} • {formatDate(approval.requestedDate)}
                                </div>
                              </div>
                              <Badge variant="outline" className="text-xs">
                                Pending
                              </Badge>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  </TabsContent>
                </Tabs>

                <Dialog open={approverDialogOpen} onOpenChange={setApproverDialogOpen}>
                  <DialogContent className="sm:max-w-xl"><DialogHeader><DialogTitle>Manage step approvers</DialogTitle></DialogHeader>
                    <div className="space-y-4">
                      <div className="space-y-2">{selectedInstance.pendingApprovals.map(approval => <div key={approval.approvalId} className="flex items-center justify-between rounded-md border px-3 py-2 text-sm">
                        <div><span className="font-medium">{approval.approverName || approval.approverRole}</span>{approval.isAdHoc && <Badge variant="outline" className="ml-2">Ad hoc</Badge>}</div>
                        {approval.isAdHoc && <Button size="icon" variant="ghost" title="Remove approver" onClick={() => setRemoveApproverTarget(approval)}><UserMinus className="h-4 w-4" /></Button>}
                      </div>)}</div>
                      <div className="grid gap-3 sm:grid-cols-2"><div><Label>User</Label><select className="h-10 w-full rounded-md border bg-background px-3 text-sm" value={adHocUserId} onChange={event => setAdHocUserId(event.target.value)}><option value="">Use role instead</option>{directoryUsers.map(user => <option key={user.id} value={user.id}>{user.firstName} {user.lastName}</option>)}</select></div>
                        <div><Label>Role</Label><Input value={adHocRole} onChange={event => setAdHocRole(event.target.value)} disabled={!!adHocUserId} /></div>
                        <div><Label>Approval group</Label><Input type="number" min="1" value={adHocGroup} onChange={event => setAdHocGroup(Math.max(1, Number(event.target.value)))} /></div>
                        <div className="sm:col-span-2"><Label>Reason</Label><Textarea value={adHocReason} onChange={event => setAdHocReason(event.target.value)} /></div></div>
                    </div><DialogFooter><Button variant="outline" onClick={() => setApproverDialogOpen(false)}>Close</Button><Button onClick={() => void addAdHocApprover()} disabled={approverSaving}>Add approver</Button></DialogFooter>
                  </DialogContent>
                </Dialog>
                <WorkflowReasonDialog
                  open={!!removeApproverTarget}
                  onOpenChange={open => !open && setRemoveApproverTarget(null)}
                  title="Remove ad hoc approver"
                  description={removeApproverTarget ? (
                    <div className="space-y-2">
                      <p className="text-sm text-muted-foreground">This removes a pending ad hoc approver from the current workflow step.</p>
                      <div className="rounded-md border bg-muted/30 p-3 text-sm">
                        <div className="font-medium text-foreground">
                          {removeApproverTarget.approverName || removeApproverTarget.approverRole || 'Approver'}
                        </div>
                        <div className="text-muted-foreground">{removeApproverTarget.stepName}</div>
                      </div>
                    </div>
                  ) : undefined}
                  reasonLabel="Removal reason"
                  reasonPlaceholder="Explain why this approver is being removed"
                  confirmText="Remove approver"
                  variant="destructive"
                  isLoading={approverSaving}
                  onConfirm={removeAdHocApprover}
                />
              </>
            ) : (
              <div className="flex-1 flex items-center justify-center text-gray-500">
                <div className="text-center">
                  <Eye className="h-12 w-12 mx-auto mb-4 text-gray-400" />
                  <p>Select a workflow instance to view details</p>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
      
      <style jsx>{`
        .workflow-visualization .completed-node {
          border-color: #10b981 !important;
          background-color: #d1fae5 !important;
        }
        .workflow-visualization .active-node {
          border-color: #3b82f6 !important;
          background-color: #dbeafe !important;
          box-shadow: 0 0 10px rgba(59, 130, 246, 0.5) !important;
        }
        .workflow-visualization .pending-node {
          border-color: #d1d5db !important;
          background-color: #f9fafb !important;
          opacity: 0.7;
        }
      `}</style>
    </div>
  );
}
