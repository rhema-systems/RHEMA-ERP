'use client';

import React, { useState, useCallback, useEffect, useRef } from 'react';
import ReactFlow, {
  Node,
  Edge,
  addEdge,
  Background,
  BackgroundVariant,
  Controls,
  MiniMap,
  useNodesState,
  useEdgesState,
  Connection,
  EdgeChange,
  NodeChange,
  ReactFlowProvider,
  NodeTypes
} from 'reactflow';
import 'reactflow/dist/style.css';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Badge } from '@/components/ui/badge';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Separator } from '@/components/ui/separator';
import { Checkbox } from '@/components/ui/checkbox';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList, CommandSeparator } from '@/components/ui/command';
import { workflowApiService } from '@/services/workflow-api.service';
import { adminApiService, Role, type User } from '@/services/admin-api.service';
import { cn } from '@/lib/utils';
import { toast } from 'sonner';
import type {
  WorkflowDefinitionDto,
  WorkflowDefinitionAdminDto,
  CreateWorkflowDefinitionAdminDto,
  UpdateWorkflowDefinitionAdminDto,
  CreateWorkflowStepDto,
  CreateWorkflowTransitionDto,
  WorkflowStepConfigurationDto,
  WorkflowAssignmentRuleDto,
  WorkflowApprovalConflictRuleDto,
  WorkflowQualityCheckDto,
  WorkflowVariableInfo,
  WorkflowEntityTypeInfo
} from '@/types/workflow';
import { buildFallbackEntityTypes, filterEntityTypesByModule, isEntityTypeInList, moduleEntityTypeMap } from './entityTypeMapping';
import {
  WorkflowStepType,
  WorkflowDefinitionLifecycleStatus,
  WorkflowApprovalType,
  WorkflowApprovalActivationMode,
  WorkflowApprovalActorSource,
  WorkflowAssignmentType,
  WorkflowRejectionHandling,
  WorkflowConditionType,
  WorkflowLogicalOperator,
  WorkflowSignatureMethod
} from '@/types/workflow';

import {
  Save, X, Play, Pause, Settings, Users, Clock, AlertTriangle,
  FileText, Mail, Phone, MessageSquare, Database, Code,
  GitBranch, CheckCircle, XCircle, AlertCircle, Timer,
  User as UserIcon, UserCheck, Building, Zap, Bell, Upload, Download,
  ChevronsUpDown, Check, Plus, Trash2
} from 'lucide-react';

import { StartNode } from './nodes/StartNode';
import { EndNode } from './nodes/EndNode';
import { TaskNode } from './nodes/TaskNode';
import { ApprovalNode } from './nodes/ApprovalNode';
import { ConditionNode } from './nodes/ConditionNode';
import { NotificationNode } from './nodes/NotificationNode';
import { DocumentNode } from './nodes/DocumentNode';
import { EscalationNode } from './nodes/EscalationNode';
import { IntegrationNode } from './nodes/IntegrationNode';

interface WorkflowDesignerProps {
  workflowId: string | null;
  isOpen: boolean;
  onClose: () => void;
  onSave: (workflow: any) => void;
}

// Custom node types for different workflow elements
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

// Initial nodes for a new workflow
const initialNodes: Node[] = [
  {
    id: '1',
    type: 'start',
    position: { x: 250, y: 25 },
    data: { label: 'Start' },
  },
];

const initialEdges: Edge[] = [];

const normalizeNodeType = (type?: string): string => {
  const normalized = (type || '').trim().toLowerCase();
  switch (normalized) {
    case 'start':
      return 'start';
    case 'end':
      return 'end';
    case 'approval':
    case 'approvalnode':
      return 'approval';
    case 'condition':
    case 'decision':
    case 'conditionnode':
      return 'condition';
    case 'notification':
    case 'notificationnode':
      return 'notification';
    case 'document':
    case 'documentnode':
      return 'document';
    case 'escalation':
    case 'escalationnode':
      return 'escalation';
    case 'integration':
    case 'integrationnode':
      return 'integration';
    case 'task':
    case 'manual':
    case 'tasknode':
    default:
      return 'task';
  }
};

const normalizeStepType = (stepType: unknown): WorkflowStepType => {
  if (typeof stepType === 'number') {
    return stepType as WorkflowStepType;
  }

  if (typeof stepType === 'string') {
    const normalized = stepType.trim().toLowerCase();
    switch (normalized) {
      case 'approval':
      case '2':
        return WorkflowStepType.Approval;
      case 'notification':
      case '5':
        return WorkflowStepType.Notification;
      case 'decision':
      case 'condition':
      case '3':
        return WorkflowStepType.Decision;
      case 'script':
      case 'integration':
      case '4':
        return WorkflowStepType.Script;
      case 'validation':
      case 'document':
      case '7':
        return WorkflowStepType.Validation;
      case 'automatic':
      case 'escalation':
      case '1':
        return WorkflowStepType.Automatic;
      case 'manual':
      case 'task':
      case '0':
      default:
        return WorkflowStepType.Manual;
    }
  }

  return WorkflowStepType.Manual;
};

const normalizeAssignmentType = (assignmentType: unknown): WorkflowAssignmentType | undefined => {
  if (typeof assignmentType === 'number') {
    return assignmentType as WorkflowAssignmentType;
  }

  if (typeof assignmentType === 'string') {
    const normalized = assignmentType.trim().toLowerCase();
    switch (normalized) {
      case 'user':
      case '0':
        return WorkflowAssignmentType.User;
      case 'role':
      case '1':
        return WorkflowAssignmentType.Role;
      case 'dynamic':
      case '2':
        return WorkflowAssignmentType.Dynamic;
      case 'requestormanager':
      case 'requestor_manager':
      case 'requestor-manager':
      case '3':
        return WorkflowAssignmentType.RequestorManager;
      case 'previousstepuser':
      case 'previous_step_user':
      case 'previous-step-user':
      case '4':
        return WorkflowAssignmentType.PreviousStepUser;
      default:
        return undefined;
    }
  }

  return undefined;
};

const normalizeApprovalType = (approvalType: unknown): WorkflowApprovalType | undefined => {
  if (typeof approvalType === 'number') {
    return approvalType as WorkflowApprovalType;
  }

  if (typeof approvalType === 'string') {
    const normalized = approvalType.trim().toLowerCase();
    switch (normalized) {
      case 'single':
      case '0':
        return WorkflowApprovalType.Single;
      case 'multiple':
      case '1':
        return WorkflowApprovalType.Multiple;
      case 'consensus':
      case '2':
        return WorkflowApprovalType.Consensus;
      case 'majority':
      case '3':
        return WorkflowApprovalType.Majority;
      default:
        return undefined;
    }
  }

  return undefined;
};

const normalizeStringList = (values: unknown): string[] => {
  if (!Array.isArray(values)) {
    return [];
  }

  return values
    .map((value) => (typeof value === 'string' ? value.trim() : ''))
    .filter(Boolean);
};

// Node palette for drag and drop
const nodePalette = [
  { type: 'start', label: 'Start', icon: Play, color: 'bg-green-500' },
  { type: 'end', label: 'End', icon: CheckCircle, color: 'bg-red-500' },
  { type: 'task', label: 'Task', icon: FileText, color: 'bg-blue-500' },
  { type: 'approval', label: 'Approval', icon: UserCheck, color: 'bg-purple-500' },
  { type: 'condition', label: 'Condition', icon: GitBranch, color: 'bg-yellow-500' },
  { type: 'notification', label: 'Notification', icon: Bell, color: 'bg-orange-500' },
  { type: 'document', label: 'Document', icon: Upload, color: 'bg-teal-500' },
  { type: 'escalation', label: 'Escalation', icon: AlertTriangle, color: 'bg-red-600' },
  { type: 'integration', label: 'Integration', icon: Database, color: 'bg-indigo-500' },
];

// Module integrations available
const moduleIntegrations = [
  { id: 'maintenance', name: 'Maintenance Management', icon: Settings },
  { id: 'inventory', name: 'Inventory Management', icon: Database },
  { id: 'hr', name: 'Human Resources', icon: Users },
  { id: 'finance', name: 'Finance & Accounting', icon: Building },
  { id: 'procurement', name: 'Procurement', icon: FileText },
  { id: 'helpdesk', name: 'Helpdesk (EHC)', icon: MessageSquare },
  { id: 'projects', name: 'Project Management', icon: CheckCircle },
  { id: 'sales', name: 'Sales & CRM', icon: UserIcon },
  { id: 'quality', name: 'Quality Management', icon: CheckCircle },
];

export function WorkflowDesigner({ workflowId, isOpen, onClose, onSave }: WorkflowDesignerProps) {
  const [nodes, setNodes, onNodesChange] = useNodesState(initialNodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState(initialEdges);
  const [selectedNode, setSelectedNode] = useState<Node | null>(null);
  const [selectedEdge, setSelectedEdge] = useState<Edge | null>(null);
  const [workflowName, setWorkflowName] = useState('');
  const [workflowDescription, setWorkflowDescription] = useState('');
  const [entityType, setEntityType] = useState('WorkOrder');
  const [entityTypeModuleFilter, setEntityTypeModuleFilter] = useState('all');
  const [activeTab, setActiveTab] = useState('design');
  const [isPropertiesOpen, setIsPropertiesOpen] = useState(false);
  const [draggedType, setDraggedType] = useState<string | null>(null);
  const reactFlowWrapper = useRef<HTMLDivElement>(null);
  const [reactFlowInstance, setReactFlowInstance] = useState<any>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [activeWorkflowId, setActiveWorkflowId] = useState<string | null>(null);
  const [workflowOptions, setWorkflowOptions] = useState<WorkflowDefinitionAdminDto[]>([]);
  const [workflowOptionsLoading, setWorkflowOptionsLoading] = useState(false);
  const [workflowOptionsError, setWorkflowOptionsError] = useState<string | null>(null);
  const [workflowPickerOpen, setWorkflowPickerOpen] = useState(false);
  const [workflowPickerQuery, setWorkflowPickerQuery] = useState('');
  const [availableRoles, setAvailableRoles] = useState<Role[]>([]);
  const [availableUsers, setAvailableUsers] = useState<User[]>([]);
  const [roleSearch, setRoleSearch] = useState('');
  const [userSearch, setUserSearch] = useState('');
  const [approverDialogOpen, setApproverDialogOpen] = useState(false);
  const [checklistDialogOpen, setChecklistDialogOpen] = useState(false);
  const [conditionVariables, setConditionVariables] = useState<WorkflowVariableInfo[]>([]);
  const [variablesLoading, setVariablesLoading] = useState(false);
  const [availableEntityTypes, setAvailableEntityTypes] = useState<WorkflowEntityTypeInfo[]>([]);
  const [entityTypesLoading, setEntityTypesLoading] = useState(false);
  const [entityTypesError, setEntityTypesError] = useState<string | null>(null);
  const [isCustomEntityType, setIsCustomEntityType] = useState(false);

  const customEntityTypeValue = '__custom__';

  const isGuid = (value: string) => /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);

  const selectedWorkflowOption =
    activeWorkflowId ? workflowOptions.find((item) => item.id === activeWorkflowId) : undefined;
  const selectedWorkflowLiveInstanceCount = selectedWorkflowOption?.activeInstancesCount ?? 0;
  const selectedWorkflowIsImmutable = selectedWorkflowOption != null &&
    selectedWorkflowOption.lifecycleStatus !== WorkflowDefinitionLifecycleStatus.Draft;
  const selectedWorkflowHasLiveInstances = selectedWorkflowLiveInstanceCount > 0 || selectedWorkflowIsImmutable;
  const workflowLiveInstanceLockMessage =
    selectedWorkflowIsImmutable
      ? `Version ${selectedWorkflowOption?.version ?? ''} is ${selectedWorkflowOption?.lifecycleStatus === WorkflowDefinitionLifecycleStatus.Published ? 'published' : 'retired'} and is read-only. Clone it as a new draft to make changes.`
      : selectedWorkflowLiveInstanceCount === 1
      ? 'This workflow has 1 live instance and cannot be edited. Complete or cancel that instance first, or create a separate workflow for future records.'
      : `This workflow has ${selectedWorkflowLiveInstanceCount} live instances and cannot be edited. Complete or cancel those instances first, or create a separate workflow for future records.`;

  const inferModuleForEntityType = (entityTypeName?: string | null) => {
    const key = (entityTypeName || '').replace(/[^a-z0-9]/gi, '').toLowerCase();
    if (!key) return 'all';

    for (const [moduleId, entityTypes] of Object.entries(moduleEntityTypeMap)) {
      const match = entityTypes.some((name) => name.replace(/[^a-z0-9]/gi, '').toLowerCase() === key);
      if (match) return moduleId;
    }

    return 'all';
  };

  const resetToNewWorkflow = (name?: string) => {
    setActiveWorkflowId(null);
    setWorkflowName(name ?? '');
    setWorkflowDescription('');
    setNodes(initialNodes);
    setEdges(initialEdges);
  };

  // Load workflow data if editing
  useEffect(() => {
    if (workflowId && isOpen) {
      if (isGuid(workflowId)) {
        loadWorkflowData(workflowId);
      } else {
        setWorkflowName('');
        setWorkflowDescription('');
        setEntityType('WorkOrder');
        setNodes(initialNodes);
        setEdges(initialEdges);
        setActiveWorkflowId(null);
      }
    }
    if (!workflowId && isOpen) {
      setWorkflowName('');
      setWorkflowDescription('');
      setEntityType('WorkOrder');
      setNodes(initialNodes);
      setEdges(initialEdges);
      setActiveWorkflowId(null);
    }
  }, [workflowId, isOpen]);

  useEffect(() => {
    if (!isOpen) return;

    const loadApproverSources = async () => {
      try {
        const [roles, users] = await Promise.all([
          adminApiService.getRoles(),
          adminApiService.getUsers(),
        ]);
        setAvailableRoles(roles);
        setAvailableUsers(users);
      } catch (error) {
        console.error('Failed to load roles or users for approvals:', error);
      }
    };

    loadApproverSources();
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen) return;

    const loadWorkflowOptions = async () => {
      setWorkflowOptionsLoading(true);
      setWorkflowOptionsError(null);
      try {
        const pageSize = 100; // API caps at 100
        let page = 1;
        let all: WorkflowDefinitionAdminDto[] = [];
        let totalCount = 0;

        for (let safety = 0; safety < 50; safety++) {
          const result = await workflowApiService.getWorkflowDefinitions({
            page,
            pageSize,
            sortBy: 'Name',
            sortDescending: false,
            // IMPORTANT: include both active and inactive (admin view)
            isActive: undefined,
          } as any);

          totalCount = result.totalCount ?? totalCount;
          all = [...all, ...(result.data || [])];

          if ((result.data || []).length === 0) break;
          if (totalCount > 0 && all.length >= totalCount) break;
          if (page >= 50) break;

          page++;
        }

        // De-dupe in case API returns overlaps
        const deduped = Array.from(new Map(all.map((d) => [d.id, d])).values());
        setWorkflowOptions(deduped);
      } catch (error) {
        console.error('Failed to load workflow definitions:', error);
        setWorkflowOptions([]);
        setWorkflowOptionsError('Unable to load workflow list.');
      } finally {
        setWorkflowOptionsLoading(false);
      }
    };

    loadWorkflowOptions();
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen) return;

    const loadEntityTypes = async () => {
      setEntityTypesLoading(true);
      setEntityTypesError(null);
      try {
        const types = await workflowApiService.ensureWorkflowEntityTypes();
        if (types.length > 0) {
          setAvailableEntityTypes(types);
          return;
        }

        const fallbackTypes = buildFallbackEntityTypes();
        setAvailableEntityTypes(fallbackTypes);
        if (fallbackTypes.length === 0) {
          setEntityTypesError('Unable to load workflow entity types.');
        }
      } catch (error) {
        console.error('Failed to load workflow entity types:', error);
        const fallbackTypes = buildFallbackEntityTypes();
        setAvailableEntityTypes(fallbackTypes);
        if (fallbackTypes.length === 0) {
          setEntityTypesError('Unable to load workflow entity types.');
        }
      } finally {
        setEntityTypesLoading(false);
      }
    };

    loadEntityTypes();
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen) return;
    if (!entityType) {
      setIsCustomEntityType(availableEntityTypes.length === 0);
      return;
    }

    if (availableEntityTypes.length === 0) {
      setIsCustomEntityType(true);
      return;
    }

    const isKnown = availableEntityTypes.some(
      (item) =>
        item.name.toLowerCase() === entityType.toLowerCase() ||
        item.code.toLowerCase() === entityType.toLowerCase()
    );
    setIsCustomEntityType(!isKnown);
  }, [entityType, availableEntityTypes, isOpen]);

  useEffect(() => {
    if (!isOpen || !entityType || isCustomEntityType) return;

    const { items: filteredItems } = filterEntityTypesByModule(
      availableEntityTypes,
      entityTypeModuleFilter
    );

    if (!isEntityTypeInList(entityType, filteredItems))
    {
      setEntityType('');
    }
  }, [entityTypeModuleFilter, availableEntityTypes, entityType, isCustomEntityType, isOpen]);

  useEffect(() => {
    if (!isOpen) return;

    const loadVariables = async () => {
      if (!entityType) {
        setConditionVariables([]);
        return;
      }

      setVariablesLoading(true);
      try {
        const variables = await workflowApiService.getWorkflowVariables(entityType);
        setConditionVariables(variables);
      } catch (error) {
        console.error('Failed to load workflow variables:', error);
        setConditionVariables([]);
      } finally {
        setVariablesLoading(false);
      }
    };

    loadVariables();
  }, [entityType, isOpen]);

  const loadWorkflowData = async (id: string) => {
    try {
      setLoadError(null);
      const definition = await workflowApiService.getWorkflowDefinition(id);
      applyDefinition(definition);
      setWorkflowPickerOpen(false);
      setWorkflowPickerQuery('');
    } catch (error) {
      console.error('Failed to load workflow:', error);
      setLoadError('Failed to load workflow definition.');
    }
  };

  function upgradeEdgeBasedBranchingToDecisionNodes(stepNodes: Node[], stepEdges: Edge[]) {
    // The "condition" node type maps to WorkflowStepType.Decision on save.
    // This helper detects a common SQL-seeded pattern:
    // - A step has exactly 2 outgoing transitions
    // - At least 1 transition has a condition expression
    // Then we insert a Decision node between the step and its outgoing targets.

    const nodeById = new Map<string, Node>();
    stepNodes.forEach(node => nodeById.set(node.id, node));

    const outgoingBySource = new Map<string, Edge[]>();
    stepEdges.forEach(edge => {
      const existing = outgoingBySource.get(edge.source) ?? [];
      existing.push(edge);
      outgoingBySource.set(edge.source, existing);
    });

    const upgradedNodes: Node[] = [...stepNodes];
    let upgradedEdges: Edge[] = [...stepEdges];

    outgoingBySource.forEach((outgoingEdges, sourceId) => {
      if (outgoingEdges.length !== 2) {
        return;
      }

      const sourceNode = nodeById.get(sourceId);
      if (!sourceNode) {
        return;
      }

      // Don't upgrade existing Decision nodes (or non-step nodes).
      if (sourceNode.type === 'condition' || sourceNode.type === 'start' || sourceNode.type === 'end') {
        return;
      }

      const edgesWithCondition = outgoingEdges.filter(edge => {
        const expr = edge.data?.conditionExpression?.toString().trim();
        return !!expr;
      });

      if (edgesWithCondition.length === 0) {
        return;
      }

      // Avoid double-upgrading if a Decision node already exists directly after this step.
      const alreadyUpgraded = outgoingEdges.some(edge => nodeById.get(edge.target)?.type === 'condition');
      if (alreadyUpgraded) {
        return;
      }

      const conditionalEdge = edgesWithCondition[0];
      const otherEdge = outgoingEdges.find(edge => edge.id !== conditionalEdge.id) ?? outgoingEdges[1];

      const conditionalExpression = conditionalEdge.data?.conditionExpression?.toString().trim() || '';
      const otherExpression = otherEdge.data?.conditionExpression?.toString().trim() || '';
      const canPromoteToNodeExpression = !!conditionalExpression && !otherExpression && edgesWithCondition.length === 1;
      const baseExpression = canPromoteToNodeExpression ? conditionalExpression : '';

      const decisionNodeId = crypto.randomUUID();
      const decisionNode: Node = {
        id: decisionNodeId,
        type: 'condition',
        position: { x: sourceNode.position.x, y: sourceNode.position.y + 90 },
        data: {
          label: baseExpression ? 'Condition' : 'Condition',
          conditionExpression: baseExpression,
          operator: 'AND',
        },
      };

      upgradedNodes.push(decisionNode);
      nodeById.set(decisionNodeId, decisionNode);

      // Remove the original outgoing edges from the source step
      const outgoingEdgeIds = new Set(outgoingEdges.map(edge => edge.id));
      upgradedEdges = upgradedEdges.filter(edge => !(edge.source === sourceId && outgoingEdgeIds.has(edge.id)));

      // Add: source step -> decision node
      upgradedEdges.push({
        id: `edge-${sourceId}-${decisionNodeId}`,
        source: sourceId,
        target: decisionNodeId,
      });

      // Add: decision -> targets (reuse existing edge IDs/labels where possible)
      upgradedEdges.push({
        ...conditionalEdge,
        source: decisionNodeId,
        sourceHandle: 'true',
        // If we promoted the expression into the Decision node, clear it off the edge.
        data: canPromoteToNodeExpression ? undefined : conditionalEdge.data,
      });

      upgradedEdges.push({
        ...otherEdge,
        source: decisionNodeId,
        sourceHandle: 'false',
        data: otherEdge.data,
      });
    });

    return { stepNodes: upgradedNodes, stepEdges: upgradedEdges };
  }

  const applyDefinition = (definition: WorkflowDefinitionDto) => {
    setWorkflowName(definition.name);
    setWorkflowDescription(definition.description || '');
    const effectiveEntityType = definition.entityType || 'WorkOrder';
    setEntityTypeModuleFilter(inferModuleForEntityType(effectiveEntityType));
    setEntityType(effectiveEntityType);
    setActiveWorkflowId(definition.id);

    const designerConfig = parseDesignerConfig(definition.configuration);
    if (designerConfig) {
      const stepTypeById = new Map(
        (definition.steps || []).map((step) => [step.id, normalizeNodeType(mapStepTypeToNodeType(step.stepType))])
      );
      const stepById = new Map((definition.steps || []).map((step) => [step.id, step]));

      const reconciledNodes = designerConfig.nodes.map((node) => {
        const forcedType = stepTypeById.get(node.id);
        if (!forcedType) {
          return node;
        }

        const step = stepById.get(node.id);
        const enrichedData = step ? buildNodeDataFromStep(step) : {};
        return {
          ...node,
          type: forcedType,
          data: {
            ...(node.data || {}),
            ...enrichedData,
            label: node.data?.label || enrichedData.label || node.data?.name || 'Step',
          },
        };
      });

      setNodes(reconciledNodes);
      setEdges(designerConfig.edges);
      return;
    }

    const steps = [...definition.steps].sort((a, b) => a.order - b.order);
    if (steps.length === 0) {
      setNodes(initialNodes);
      setEdges(initialEdges);
      return;
    }

    const stepNodes: Node[] = steps.map((step, index) => ({
      id: step.id,
      type: normalizeNodeType(mapStepTypeToNodeType(step.stepType)),
      position: { x: 260, y: 150 + index * 120 },
      data: buildNodeDataFromStep(step),
    }));

    const startNode: Node = {
      id: 'start',
      type: 'start',
      position: { x: 260, y: 50 },
      data: { label: 'Start' },
    };

    const transitions = definition.transitions || [];
    const stepEdges: Edge[] = transitions.length > 0
      ? transitions.map((transition) => ({
          id: transition.id,
          source: transition.fromStepId,
          target: transition.toStepId,
          label: transition.name,
          data: transition.condition?.expression
            ? { conditionExpression: transition.condition.expression }
            : undefined,
        }))
      : steps.slice(0, -1).map((step, index) => ({
          id: `edge-${step.id}-${steps[index + 1].id}`,
          source: step.id,
          target: steps[index + 1].id,
        }));

    // Upgrade edge-based conditional branching into explicit Decision (Condition) nodes for consistency.
    const upgraded = upgradeEdgeBasedBranchingToDecisionNodes(stepNodes, stepEdges);
    const upgradedStepNodes = upgraded.stepNodes;
    const upgradedStepEdges = upgraded.stepEdges;

    const maxY = upgradedStepNodes.reduce((max, node) => Math.max(max, node.position?.y ?? 0), 150);
    const endNode: Node = {
      id: 'end',
      type: 'end',
      position: { x: 260, y: maxY + 140 },
      data: { label: 'End' },
    };

    const edgesWithEnds: Edge[] = [
      { id: 'edge-start', source: startNode.id, target: steps[0].id },
      ...upgradedStepEdges,
      { id: 'edge-end', source: steps[steps.length - 1].id, target: endNode.id },
    ];

    setNodes([startNode, ...upgradedStepNodes, endNode]);
    setEdges(edgesWithEnds);
  };

  const parseDesignerConfig = (config?: string | null) => {
    if (!config) return null;
    try {
      const parsed = JSON.parse(config);
      if (parsed?.designer?.nodes && parsed?.designer?.edges) {
        const normalizedNodes = (parsed.designer.nodes as Node[]).map((node) => ({
          ...node,
          type: normalizeNodeType(node.type),
        }));
        return {
          nodes: normalizedNodes,
          edges: parsed.designer.edges as Edge[],
        };
      }
    } catch (error) {
      console.warn('Failed to parse designer configuration:', error);
    }
    return null;
  };

  const handleNodesChange = useCallback(
    (changes: NodeChange[]) => {
      if (selectedWorkflowHasLiveInstances) {
        const selectionChanges = changes.filter((change) => change.type === 'select');
        if (selectionChanges.length > 0) {
          onNodesChange(selectionChanges);
        }
        return;
      }

      onNodesChange(changes);
    },
    [onNodesChange, selectedWorkflowHasLiveInstances]
  );

  const handleEdgesChange = useCallback(
    (changes: EdgeChange[]) => {
      if (selectedWorkflowHasLiveInstances) {
        const selectionChanges = changes.filter((change) => change.type === 'select');
        if (selectionChanges.length > 0) {
          onEdgesChange(selectionChanges);
        }
        return;
      }

      onEdgesChange(changes);
    },
    [onEdgesChange, selectedWorkflowHasLiveInstances]
  );

  const onConnect = useCallback(
    (params: Connection) => {
      if (selectedWorkflowHasLiveInstances) return;
      setEdges((eds) => addEdge(params, eds));
    },
    [selectedWorkflowHasLiveInstances, setEdges]
  );

  const onNodeClick = useCallback((event: React.MouseEvent, node: Node) => {
    setSelectedNode(node);
    setSelectedEdge(null);
    setIsPropertiesOpen(true);
  }, []);

  const onEdgeClick = useCallback((event: React.MouseEvent, edge: Edge) => {
    setSelectedEdge(edge);
    setSelectedNode(null);
    setIsPropertiesOpen(true);
  }, []);

  const onDragOver = useCallback((event: React.DragEvent) => {
    event.preventDefault();
    event.dataTransfer.dropEffect = selectedWorkflowHasLiveInstances ? 'none' : 'move';
  }, [selectedWorkflowHasLiveInstances]);

  const onDrop = useCallback(
    (event: React.DragEvent) => {
      event.preventDefault();
      if (selectedWorkflowHasLiveInstances) return;

      const reactFlowBounds = reactFlowWrapper.current?.getBoundingClientRect();
      const type = event.dataTransfer.getData('application/reactflow');

      if (typeof type === 'undefined' || !type) {
        return;
      }

      const position = reactFlowInstance.project({
        x: event.clientX - (reactFlowBounds?.left ?? 0),
        y: event.clientY - (reactFlowBounds?.top ?? 0),
      });

      const newNode: Node = {
        id: `${type}-${nodes.length + 1}`,
        type,
        position,
        data: { 
          label: `${type.charAt(0).toUpperCase() + type.slice(1)}`,
          ...getDefaultNodeData(type)
        },
      };

      setNodes((nds) => nds.concat(newNode));
    },
    [reactFlowInstance, nodes.length, selectedWorkflowHasLiveInstances, setNodes]
  );

  const deleteSelectedNode = useCallback(() => {
    if (selectedWorkflowHasLiveInstances) {
      toast.error('Workflow is read-only', {
        description: workflowLiveInstanceLockMessage,
      });
      return;
    }

    if (!selectedNode) return;

    if (selectedNode.type === 'start') {
      toast.error('The start node cannot be deleted.');
      return;
    }

    const nodeLabel = selectedNode.data?.label || selectedNode.type || 'this node';
    const confirmed = window.confirm(`Delete "${nodeLabel}" and all connected transitions?`);
    if (!confirmed) return;

    setNodes((currentNodes) => currentNodes.filter((node) => node.id !== selectedNode.id));
    setEdges((currentEdges) =>
      currentEdges.filter((edge) => edge.source !== selectedNode.id && edge.target !== selectedNode.id)
    );
    setSelectedNode(null);
    setSelectedEdge(null);
    setIsPropertiesOpen(false);
    setApproverDialogOpen(false);
    setChecklistDialogOpen(false);
    toast.success('Workflow step deleted');
  }, [
    selectedNode,
    selectedWorkflowHasLiveInstances,
    setEdges,
    setNodes,
    workflowLiveInstanceLockMessage,
  ]);

  const deleteSelectedEdge = useCallback(() => {
    if (selectedWorkflowHasLiveInstances) {
      toast.error('Workflow is read-only', {
        description: workflowLiveInstanceLockMessage,
      });
      return;
    }

    if (!selectedEdge) return;

    const confirmed = window.confirm('Delete this transition?');
    if (!confirmed) return;

    setEdges((currentEdges) => currentEdges.filter((edge) => edge.id !== selectedEdge.id));
    setSelectedEdge(null);
    setIsPropertiesOpen(false);
    toast.success('Workflow transition deleted');
  }, [
    selectedEdge,
    selectedWorkflowHasLiveInstances,
    setEdges,
    workflowLiveInstanceLockMessage,
  ]);

  useEffect(() => {
    if (!isOpen || approverDialogOpen || checklistDialogOpen) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Delete' && event.key !== 'Backspace') return;
      if (event.altKey || event.ctrlKey || event.metaKey) return;

      const target = event.target as HTMLElement | null;
      const targetTag = target?.tagName?.toLowerCase();
      if (
        target?.isContentEditable ||
        targetTag === 'input' ||
        targetTag === 'textarea' ||
        targetTag === 'select'
      ) {
        return;
      }

      if (!selectedNode && !selectedEdge) return;

      event.preventDefault();
      if (selectedNode) {
        deleteSelectedNode();
        return;
      }

      deleteSelectedEdge();
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [
    approverDialogOpen,
    checklistDialogOpen,
    deleteSelectedEdge,
    deleteSelectedNode,
    isOpen,
    selectedEdge,
    selectedNode,
  ]);

  const getDefaultNodeData = (type: string) => {
    switch (type) {
      case 'task':
        return {
          assignee: '',
          taskActionType: 'general',
          taskAssigneeType: 'currentActor',
          taskAssigneeUserId: '',
          taskAssigneeRole: '',
          taskDynamicExpression: '',
          documentName: '',
          dueDate: '',
          estimatedHours: '',
          priority: 'medium',
          instructions: '',
          requiredFields: []
        };
      case 'approval':
        return {
          approvers: [],
          approverRoles: [],
          approverUsers: [],
          approvalType: 'any',
          approvalActivationMode: 'parallel',
          minApprovalsRequired: 1,
          preventInitiatorApproval: true,
          requireDistinctApprovers: true,
          approvalConflictRules: [],
          escalationTimeout: 24,
          escalationTo: '',
          conditions: []
        };
      case 'condition':
        return {
          conditionExpression: '',
          conditions: [],
          operator: 'AND',
          branches: []
        };
      case 'notification':
        return {
          recipients: [],
          template: '',
          channels: ['email'],
          priority: 'normal'
        };
      case 'document':
        return {
          documentType: '',
          required: true,
          templates: [],
          approvalRequired: false
        };
      case 'escalation':
        return {
          escalationRules: [],
          timeout: 24,
          escalationLevels: []
        };
      case 'integration':
        return {
          module: '',
          action: '',
          parameters: {},
          responseMapping: {}
        };
      default:
        return {};
    }
  };

  const onDragStart = (event: React.DragEvent, nodeType: string) => {
    if (selectedWorkflowHasLiveInstances) {
      event.preventDefault();
      event.dataTransfer.effectAllowed = 'none';
      return;
    }

    event.dataTransfer.setData('application/reactflow', nodeType);
    event.dataTransfer.effectAllowed = 'move';
    setDraggedType(nodeType);
  };

  const handleSave = async () => {
    if (selectedWorkflowHasLiveInstances) {
      toast.error('Workflow is read-only', {
        description: workflowLiveInstanceLockMessage,
      });
      return;
    }

    if (!handleValidate(false)) {
      return;
    }

    setIsSaving(true);
    try {
      const payload = buildDefinitionPayload();
      let savedDefinition: WorkflowDefinitionDto;
      const effectiveWorkflowId = workflowId && isGuid(workflowId) ? workflowId : activeWorkflowId;
      const isExisting = effectiveWorkflowId ? isGuid(effectiveWorkflowId) : false;

      if (isExisting && effectiveWorkflowId) {
        const updateDto: UpdateWorkflowDefinitionAdminDto = {
          name: payload.name,
          description: payload.description,
          entityType: payload.entityType,
          isActive: false,
          configuration: payload.configuration,
          steps: payload.steps,
          transitions: payload.transitions,
        };
        savedDefinition = await workflowApiService.updateWorkflowDefinition(effectiveWorkflowId, updateDto);
      } else {
        const createDto: CreateWorkflowDefinitionAdminDto = {
          name: payload.name,
          description: payload.description,
          entityType: payload.entityType,
          isActive: false,
          configuration: payload.configuration,
          steps: payload.steps,
          transitions: payload.transitions,
        };
        savedDefinition = await workflowApiService.createWorkflowDefinition(createDto);
      }

      setActiveWorkflowId(savedDefinition.id);
      setWorkflowOptions((prev) => {
        const existing = prev.find((item) => item.id === savedDefinition.id);
        if (existing) {
          return prev.map((item) =>
            item.id === savedDefinition.id
              ? { ...item, name: savedDefinition.name, entityType: savedDefinition.entityType }
              : item
          );
        }
        return [...prev, {
          id: savedDefinition.id,
          definitionKey: savedDefinition.definitionKey,
          name: savedDefinition.name,
          description: savedDefinition.description,
          entityType: savedDefinition.entityType,
          version: savedDefinition.version,
          isActive: savedDefinition.isActive,
          lifecycleStatus: savedDefinition.lifecycleStatus,
          changeSummary: savedDefinition.changeSummary,
          supersedesDefinitionId: savedDefinition.supersedesDefinitionId,
          publishedAt: savedDefinition.publishedAt,
          retiredAt: savedDefinition.retiredAt,
          configuration: savedDefinition.configuration,
          createdDate: savedDefinition.createdDate,
          lastModifiedDate: savedDefinition.lastModifiedDate,
          createdByName: savedDefinition.createdByName,
          lastModifiedByName: savedDefinition.lastModifiedByName,
          stepCount: savedDefinition.steps?.length || 0,
          activeInstancesCount: 0,
          lastUsedDate: undefined
        }];
      });
      onSave(savedDefinition);
    } catch (error) {
      console.error('Failed to save workflow:', error);
      const message = error instanceof Error ? error.message : 'Unable to save workflow.';
      toast.error('Failed to save workflow', {
        description: message.includes('live instances') || message.includes('active instances')
          ? 'This workflow has live instances and cannot be edited. Complete or cancel those instances first, or create a separate workflow for future records.'
          : message,
      });
    } finally {
      setIsSaving(false);
    }
  };

  const buildDefinitionPayload = () => {
    const normalizedNodes = nodes.map((node) => ({ ...node, type: normalizeNodeType(node.type) }));
    const normalizedEdges = edges.map((edge) => ({ ...edge, source: edge.source, target: edge.target }));

    const startNodeIds = new Set(
      normalizedNodes.filter((node) => node.type === 'start').map((node) => node.id)
    );
    const adjacency = new Map<string, string[]>();
    normalizedEdges.forEach((edge) => {
      const existing = adjacency.get(edge.source) ?? [];
      existing.push(edge.target);
      adjacency.set(edge.source, existing);
    });

    const reachable = new Set<string>();
    const queue: string[] = Array.from(startNodeIds);
    while (queue.length > 0) {
      const current = queue.shift();
      if (!current) {
        continue;
      }
      if (reachable.has(current)) continue;
      reachable.add(current);
      const next = adjacency.get(current) ?? [];
      next.forEach((targetId) => {
        if (!reachable.has(targetId)) {
          queue.push(targetId);
        }
      });
    }

    const allStepNodes = normalizedNodes.filter(node => node.type !== 'start' && node.type !== 'end');
    const stepNodes = reachable.size > 0
      ? allStepNodes.filter(node => reachable.has(node.id))
      : allStepNodes;

    const sortedNodes = [...stepNodes].sort((a, b) => {
      if (a.position.y !== b.position.y) return a.position.y - b.position.y;
      return a.position.x - b.position.x;
    });

    const nodeIdToStepId = new Map<string, string>();
    sortedNodes.forEach((node) => {
      const id = isGuid(node.id) ? node.id : crypto.randomUUID();
      nodeIdToStepId.set(node.id, id);
    });

    const usedNames = new Map<string, number>();
    const steps: CreateWorkflowStepDto[] = sortedNodes.map((node, index) => {
      const stepId = nodeIdToStepId.get(node.id) ?? node.id;
      const baseName = (node.data?.label || `Step ${index + 1}`).toString().trim() || `Step ${index + 1}`;
      const normalizedName = baseName.toLowerCase();
      const duplicateCount = usedNames.get(normalizedName) ?? 0;
      usedNames.set(normalizedName, duplicateCount + 1);
      const stepName = duplicateCount === 0 ? baseName : `${baseName} (${duplicateCount + 1})`;
      const estimatedHoursValue = Number(node.data?.estimatedHours);
      const estimatedHours = Number.isFinite(estimatedHoursValue) && estimatedHoursValue > 0
        ? estimatedHoursValue
        : undefined;
      const approverRoles = normalizeStringList(node.data?.approverRoles || node.data?.approvers);
      const requiredRole =
        node.type === 'approval' && approverRoles.length > 0
          ? approverRoles[0]
          : node.type === 'task' && node.data?.taskAssigneeType === 'role' && node.data?.taskAssigneeRole
            ? node.data.taskAssigneeRole
            : undefined;
      return {
        id: stepId,
        name: stepName,
        description: node.data?.instructions || undefined,
        stepType: mapNodeTypeToStepType(node.type),
        order: index + 1,
        isRequired: true,
        requiredRole,
        estimatedHours,
        configuration: buildStepConfiguration(node),
      };
    });

    const transitions = buildTransitions(sortedNodes, nodeIdToStepId);

    const designerConfig = buildDesignerConfig(nodeIdToStepId);

    return {
      name: workflowName || 'Untitled Workflow',
      description: workflowDescription || undefined,
      entityType: entityType || 'WorkOrder',
      configuration: designerConfig,
      steps,
      transitions,
    };
  };

  const buildTransitions = (stepNodes: Node[], nodeIdToStepId: Map<string, string>): CreateWorkflowTransitionDto[] => {
    const stepNodeIds = new Set(stepNodes.map(node => node.id));
    const edgesForTransitions = edges.filter(edge => stepNodeIds.has(edge.source) && stepNodeIds.has(edge.target));
    if (edgesForTransitions.length === 0) {
      if (stepNodes.length <= 1) {
        return [];
      }

      return stepNodes.slice(0, -1).map((node, index) => {
        const nextNode = stepNodes[index + 1];
        return {
          fromStepId: nodeIdToStepId.get(node.id) ?? node.id,
          toStepId: nodeIdToStepId.get(nextNode.id) ?? nextNode.id,
          name: `${node.data?.label ?? `Step ${index + 1}`} to ${nextNode.data?.label ?? `Step ${index + 2}`}`,
          description: undefined,
          condition: undefined,
          isDefault: true,
          priority: 1,
        };
      });
    }

    const stepNodeMap = new Map<string, Node>();
    stepNodes.forEach(node => stepNodeMap.set(node.id, node));

    const resolveConditionExpression = (edge: Edge): string | undefined => {
      const explicit = edge.data?.conditionExpression?.toString().trim();
      if (explicit) {
        return explicit;
      }

      const sourceNode = stepNodeMap.get(edge.source);
      if (sourceNode?.type === 'condition') {
        const baseExpression = sourceNode.data?.conditionExpression?.toString().trim();
        if (!baseExpression) {
          return undefined;
        }

        if (edge.sourceHandle === 'false') {
          return invertSimpleCondition(baseExpression) ?? undefined;
        }

        return baseExpression;
      }

      return undefined;
    };

    const conditionExpressions = new Map<string, string | undefined>();
    edgesForTransitions.forEach(edge => {
      conditionExpressions.set(edge.id, resolveConditionExpression(edge));
    });

    const edgesBySource = new Map<string, Edge[]>();
    edgesForTransitions.forEach(edge => {
      const existing = edgesBySource.get(edge.source) ?? [];
      existing.push(edge);
      edgesBySource.set(edge.source, existing);
    });

    const defaultEdgeBySource = new Map<string, string>();
    edgesBySource.forEach((sourceEdges, source) => {
      const edgesWithCondition = sourceEdges.filter(edge => conditionExpressions.get(edge.id));
      const edgesWithoutCondition = sourceEdges.filter(edge => !conditionExpressions.get(edge.id));
      const defaultEdge = edgesWithoutCondition[0] ?? sourceEdges[0];
      if (defaultEdge) {
        defaultEdgeBySource.set(source, defaultEdge.id);
      }

      if (edgesWithCondition.length > 0 && edgesWithoutCondition.length === 0) {
        defaultEdgeBySource.set(source, sourceEdges[0].id);
      }
    });

    const outgoingCount: Record<string, number> = {};
    edgesForTransitions.forEach(edge => {
      outgoingCount[edge.source] = (outgoingCount[edge.source] || 0) + 1;
    });

    const outgoingIndex: Record<string, number> = {};
    return edgesForTransitions.map((edge) => {
      const fromStepId = nodeIdToStepId.get(edge.source) ?? edge.source;
      const toStepId = nodeIdToStepId.get(edge.target) ?? edge.target;
      const sourceNode = stepNodes.find(node => node.id === edge.source);
      const targetNode = stepNodes.find(node => node.id === edge.target);
      const name = edge.label?.toString() || `${sourceNode?.data?.label ?? 'Step'} to ${targetNode?.data?.label ?? 'Step'}`;
      const conditionExpression = conditionExpressions.get(edge.id);
      outgoingIndex[edge.source] = (outgoingIndex[edge.source] || 0) + 1;
      const isDefault = defaultEdgeBySource.get(edge.source) === edge.id;
      const priority = outgoingCount[edge.source] - outgoingIndex[edge.source];

      return {
        fromStepId,
        toStepId,
        name,
        description: undefined,
        condition: conditionExpression ? {
          conditionType: WorkflowConditionType.Expression,
          expression: conditionExpression,
          logicalOperator: WorkflowLogicalOperator.And,
        } : undefined,
        isDefault,
        priority,
      };
    });
  };

  const buildDesignerConfig = (nodeIdToStepId: Map<string, string>) => {
    const normalizedNodes = nodes.map((node) => {
      if (node.type === 'start' || node.type === 'end') {
        return node;
      }
      return { ...node, id: nodeIdToStepId.get(node.id) ?? node.id };
    });

    const normalizedEdges = edges.map((edge) => {
      const source = nodeIdToStepId.get(edge.source) ?? edge.source;
      const target = nodeIdToStepId.get(edge.target) ?? edge.target;
      return { ...edge, source, target };
    });

    return JSON.stringify({ designer: { nodes: normalizedNodes, edges: normalizedEdges } });
  };

  const buildNodeDataFromStep = (step: WorkflowDefinitionDto['steps'][number]) => {
    const stepChecklist = step.configuration?.qualityConfig?.qualityChecks?.map((item, index) => ({
      ...item,
      id: item.id || `check-${index + 1}`,
    })) ?? [];
    const baseData: Record<string, any> = {
      label: step.name,
      instructions: step.description || '',
      estimatedHours: step.estimatedHours?.toString() || '',
      dueDate: step.estimatedHours ? `${step.estimatedHours}h` : '',
      stepChecklist,
    };
    if (normalizeStepType(step.stepType) === WorkflowStepType.Approval) {
      const approvalConfig = step.configuration?.approvalConfig;
      const configuredApproverRoles = approvalConfig?.approverRules
        ?.filter(rule => normalizeAssignmentType(rule.assignmentType) === WorkflowAssignmentType.Role && rule.role)
        .map(rule => rule.role ?? '')
        .filter(Boolean) ?? [];
      const approverRoles = configuredApproverRoles.length > 0
        ? configuredApproverRoles
        : step.requiredRole
          ? [step.requiredRole]
          : [];
      const approverUsers = approvalConfig?.approverRules
        ?.filter(rule => normalizeAssignmentType(rule.assignmentType) === WorkflowAssignmentType.User && rule.userId)
        .map(rule => rule.userId ?? '')
        .filter(Boolean) ?? [];
      return {
        ...baseData,
        approvers: approverRoles,
        approverRoles,
        approverUsers,
        approvalType: approvalConfig
          ? normalizeApprovalType(approvalConfig.approvalType) === WorkflowApprovalType.Single && approvalConfig.minApprovalsRequired > 1
            ? 'minimum'
            : mapApprovalTypeToLabel(approvalConfig.approvalType)
          : 'any',
        approvalActivationMode:
          approvalConfig?.activationMode === WorkflowApprovalActivationMode.Sequential ||
          String(approvalConfig?.activationMode).toLowerCase() === 'sequential'
            ? 'sequential'
            : 'parallel',
        minApprovalsRequired: approvalConfig?.minApprovalsRequired || 1,
        preventInitiatorApproval: approvalConfig?.preventInitiatorApproval === true,
        requireDistinctApprovers: approvalConfig?.requireDistinctApprovers === true,
        approvalConflictRules: approvalConfig?.conflictRules || [],
        requireElectronicSignature: approvalConfig?.signaturePolicy?.isRequired === true,
        signatureMethod: approvalConfig?.signaturePolicy?.method ?? WorkflowSignatureMethod.Attestation,
        requiredSigningRole: approvalConfig?.signaturePolicy?.requiredSigningRole || '',
        requireValidCertificateChain: approvalConfig?.signaturePolicy?.requireValidCertificateChain === true,
        signatureAttestation: approvalConfig?.signaturePolicy?.attestationText || 'I confirm that I reviewed and approve this transaction.',
        approvalChecklist: stepChecklist,
      };
    }

    if (normalizeStepType(step.stepType) === WorkflowStepType.Manual) {
      const assignmentRule = step.configuration?.assignmentRules?.[0];
      if (!assignmentRule) {
        return {
          ...baseData,
          taskActionType: step.configuration?.taskConfig?.taskActionType || 'general',
          taskAssigneeType: 'currentActor',
          documentName: step.configuration?.taskConfig?.documentName || '',
          priority: 'medium',
        };
      }

      if (normalizeAssignmentType(assignmentRule.assignmentType) === WorkflowAssignmentType.Role && assignmentRule.role) {
        return {
          ...baseData,
          taskActionType: step.configuration?.taskConfig?.taskActionType || 'general',
          taskAssigneeType: 'role',
          taskAssigneeRole: assignmentRule.role,
          assignee: assignmentRule.role,
          documentName: step.configuration?.taskConfig?.documentName || '',
          priority: 'medium',
        };
      }

      if (normalizeAssignmentType(assignmentRule.assignmentType) === WorkflowAssignmentType.User && assignmentRule.userId) {
        return {
          ...baseData,
          taskActionType: step.configuration?.taskConfig?.taskActionType || 'general',
          taskAssigneeType: 'user',
          taskAssigneeUserId: assignmentRule.userId,
          assignee: assignmentRule.userId,
          documentName: step.configuration?.taskConfig?.documentName || '',
          priority: 'medium',
        };
      }

      if (normalizeAssignmentType(assignmentRule.assignmentType) === WorkflowAssignmentType.Dynamic && assignmentRule.dynamicExpression) {
        return {
          ...baseData,
          taskActionType: step.configuration?.taskConfig?.taskActionType || 'general',
          taskAssigneeType: 'dynamic',
          taskDynamicExpression: assignmentRule.dynamicExpression,
          assignee: assignmentRule.dynamicExpression,
          documentName: step.configuration?.taskConfig?.documentName || '',
          priority: 'medium',
        };
      }

      if (normalizeAssignmentType(assignmentRule.assignmentType) === WorkflowAssignmentType.RequestorManager) {
        return {
          ...baseData,
          taskActionType: step.configuration?.taskConfig?.taskActionType || 'general',
          taskAssigneeType: 'requestorManager',
          assignee: 'Requestor Manager',
          documentName: step.configuration?.taskConfig?.documentName || '',
          priority: 'medium',
        };
      }

      if (normalizeAssignmentType(assignmentRule.assignmentType) === WorkflowAssignmentType.PreviousStepUser) {
        return {
          ...baseData,
          taskActionType: step.configuration?.taskConfig?.taskActionType || 'general',
          taskAssigneeType: 'previousStepUser',
          assignee: 'Previous Step User',
          documentName: step.configuration?.taskConfig?.documentName || '',
          priority: 'medium',
        };
      }
    }

    const assignmentRule = step.configuration?.assignmentRules?.find(rule => rule.role);
    if (assignmentRule?.role) {
      return {
        ...baseData,
        assignee: assignmentRule.role,
      };
    }

    return baseData;
  };

  const buildRequirementKey = (value?: string) => {
    const normalized = (value || 'task-document')
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/(^-|-$)/g, '');

    return normalized || 'task-document';
  };

  const readStepChecklist = (data?: any): WorkflowQualityCheckDto[] =>
    ((data?.stepChecklist || data?.approvalChecklist || []) as WorkflowQualityCheckDto[]);

  const buildStepChecklist = (data?: any): WorkflowQualityCheckDto[] =>
    readStepChecklist(data)
      .filter(item => item?.name?.trim())
      .map((item, index) => ({
        id: item.id || `check-${index + 1}`,
        name: item.name.trim(),
        description: item.description?.trim() || '',
        isRequired: item.isRequired !== false,
        requiresDocument: item.requiresDocument === true,
        documentType: item.documentType?.trim() || undefined,
        documentName: item.documentName?.trim() || undefined,
        applicabilityCondition: item.applicabilityCondition,
        expectedValue: item.expectedValue,
        validationExpression: item.validationExpression,
      }));

  const buildStepConfiguration = (node: Node): WorkflowStepConfigurationDto | undefined => {
    const stepChecklist = buildStepChecklist(node.data);
    const qualityConfig = stepChecklist.length > 0
      ? {
          qualityConfig: {
            qualityChecks: stepChecklist,
          },
        }
      : {};

    if (node.type === 'approval') {
      const approverRoles = normalizeStringList(node.data?.approverRoles || node.data?.approvers);
      const approverUsers = normalizeStringList(node.data?.approverUsers);
      const approverRules: WorkflowAssignmentRuleDto[] = [];
      const sequentialApprovals = node.data?.approvalActivationMode === 'sequential';

      approverRoles.forEach((role: string, index: number) => {
        approverRules.push({
          assignmentType: WorkflowAssignmentType.Role,
          role,
          approvalGroup: sequentialApprovals ? index + 1 : 1,
          priority: approverRoles.length - index,
        });
      });

      approverUsers.forEach((userId: string, index: number) => {
        approverRules.push({
          assignmentType: WorkflowAssignmentType.User,
          userId,
          approvalGroup: sequentialApprovals ? approverRoles.length + index + 1 : 1,
          priority: approverUsers.length - index,
        });
      });

      return {
        approvalConfig: {
          approvalType: mapApprovalTypeToEnum(node.data?.approvalType),
          activationMode: sequentialApprovals
            ? WorkflowApprovalActivationMode.Sequential
            : WorkflowApprovalActivationMode.Parallel,
          approverRules,
          minApprovalsRequired: Math.max(Number(node.data?.minApprovalsRequired) || 1, 1),
          rejectionHandling: WorkflowRejectionHandling.StopWorkflow,
          preventInitiatorApproval: node.data?.preventInitiatorApproval === true,
          requireDistinctApprovers: node.data?.requireDistinctApprovers === true,
          conflictRules: ((node.data?.approvalConflictRules || []) as WorkflowApprovalConflictRuleDto[])
            .filter(rule => rule?.name?.trim())
            .map(rule => ({
              id: rule.id,
              name: rule.name.trim(),
              isEnabled: rule.isEnabled !== false,
              actorSource: rule.actorSource,
              sourceStepName: rule.sourceStepName?.trim() || undefined,
              contextField: rule.contextField?.trim() || undefined,
              message: rule.message?.trim() || undefined,
            })),
          signaturePolicy: node.data?.requireElectronicSignature === true ? {
            isRequired: true,
            method: Number(node.data?.signatureMethod ?? WorkflowSignatureMethod.Attestation) as WorkflowSignatureMethod,
            requiredSigningRole: node.data?.requiredSigningRole?.trim() || undefined,
            requireValidCertificateChain: node.data?.requireValidCertificateChain === true,
            attestationText: node.data?.signatureAttestation?.trim() || 'I confirm that I reviewed and approve this transaction.',
          } : undefined,
        },
        ...qualityConfig,
      };
    }

    if (node.type === 'task') {
      const taskAssigneeType = node.data?.taskAssigneeType || 'currentActor';
      const taskActionType = node.data?.taskActionType || 'general';
      const documentName = node.data?.documentName?.trim() || '';
      const taskConfig = {
        taskActionType,
        documentName: documentName || undefined,
        requiresDocument: taskActionType === 'document',
        documentRequirementKey: taskActionType === 'document'
          ? buildRequirementKey(documentName || node.data?.label || node.id)
          : undefined,
        instructions: node.data?.instructions?.trim() || undefined,
      };
      const configuration: WorkflowStepConfigurationDto = {
        taskConfig,
        ...qualityConfig,
      };

      if (taskAssigneeType === 'user' && node.data?.taskAssigneeUserId) {
        return {
          ...configuration,
          assignmentRules: [{
            assignmentType: WorkflowAssignmentType.User,
            userId: node.data.taskAssigneeUserId,
            priority: 1,
          }],
        };
      }

      if (taskAssigneeType === 'dynamic' && node.data?.taskDynamicExpression) {
        return {
          ...configuration,
          assignmentRules: [{
            assignmentType: WorkflowAssignmentType.Dynamic,
            dynamicExpression: node.data.taskDynamicExpression,
            priority: 1,
          }],
        };
      }

      if (taskAssigneeType === 'requestorManager') {
        return {
          ...configuration,
          assignmentRules: [{
            assignmentType: WorkflowAssignmentType.RequestorManager,
            priority: 1,
          }],
        };
      }

      if (taskAssigneeType === 'previousStepUser') {
        return {
          ...configuration,
          assignmentRules: [{
            assignmentType: WorkflowAssignmentType.PreviousStepUser,
            priority: 1,
          }],
        };
      }

      if (taskAssigneeType === 'role' && node.data?.taskAssigneeRole) {
        return {
          ...configuration,
          assignmentRules: [{
            assignmentType: WorkflowAssignmentType.Role,
            role: node.data.taskAssigneeRole,
            priority: 1,
          }],
        };
      }

      if (node.data?.assignee) {
        return {
          ...configuration,
          assignmentRules: [{
            assignmentType: WorkflowAssignmentType.Role,
            role: node.data.assignee,
            priority: 1,
          }],
        };
      }

      return configuration;
    }

    return undefined;
  };

  const mapApprovalTypeToEnum = (value?: unknown): WorkflowApprovalType => {
    if (typeof value === 'number') {
      return value as WorkflowApprovalType;
    }

    switch ((typeof value === 'string' ? value : '').toLowerCase()) {
      case 'all':
        return WorkflowApprovalType.Consensus;
      case 'majority':
        return WorkflowApprovalType.Majority;
      case 'minimum':
        return WorkflowApprovalType.Single;
      case 'any':
      default:
        return WorkflowApprovalType.Single;
    }
  };

  const mapApprovalTypeToLabel = (value?: unknown): string => {
    switch (normalizeApprovalType(value)) {
      case WorkflowApprovalType.Consensus:
        return 'all';
      case WorkflowApprovalType.Multiple:
        return 'all';
      case WorkflowApprovalType.Majority:
        return 'majority';
      case WorkflowApprovalType.Single:
      default:
        return 'any';
    }
  };

  const mapNodeTypeToStepType = (type?: string): WorkflowStepType => {
    switch (type) {
      case 'approval':
        return WorkflowStepType.Approval;
      case 'notification':
        return WorkflowStepType.Notification;
      case 'condition':
        return WorkflowStepType.Decision;
      case 'integration':
        return WorkflowStepType.Script;
      case 'document':
        return WorkflowStepType.Validation;
      case 'escalation':
        return WorkflowStepType.Automatic;
      case 'task':
      default:
        return WorkflowStepType.Manual;
    }
  };

  const mapStepTypeToNodeType = (stepType: WorkflowStepType | string | number): string => {
    switch (normalizeStepType(stepType)) {
      case WorkflowStepType.Approval:
        return 'approval';
      case WorkflowStepType.Notification:
        return 'notification';
      case WorkflowStepType.Decision:
        return 'condition';
      case WorkflowStepType.Script:
        return 'integration';
      case WorkflowStepType.Validation:
        return 'document';
      case WorkflowStepType.Automatic:
        return 'escalation';
      case WorkflowStepType.Manual:
      default:
        return 'task';
    }
  };

  const handleTest = () => {
    // Test workflow functionality
    console.log('Testing workflow...');
  };

  const handleValidate = (showSuccess = true) => {
    // Validate workflow structure
    const errors = [];
    
    // Check if workflow has start node
    const startNodes = nodes.filter(node => node.type === 'start');
    if (startNodes.length === 0) {
      errors.push('Workflow must have a start node');
    }
    
    // Check if workflow has end node
    const endNodes = nodes.filter(node => node.type === 'end');
    if (endNodes.length === 0) {
      errors.push('Workflow must have an end node');
    }

    // More validation logic...

    const approvalNodes = nodes.filter(node => node.type === 'approval');
    approvalNodes.forEach(node => {
      const roles = normalizeStringList(node.data?.approverRoles || node.data?.approvers);
      const users = normalizeStringList(node.data?.approverUsers);
      if (roles.length === 0 && users.length === 0) {
        errors.push(`Approval step "${node.data?.label || 'Approval'}" must have at least one approver role or user`);
      }

      const minimumApprovals = Math.max(Number(node.data?.minApprovalsRequired) || 1, 1);
      if (minimumApprovals > roles.length + users.length) {
        errors.push(`Approval step "${node.data?.label || 'Approval'}" requires ${minimumApprovals} approvals but only ${roles.length + users.length} approval slots are configured`);
      }

      const conflictRules = (node.data?.approvalConflictRules || []) as WorkflowApprovalConflictRuleDto[];
      conflictRules.forEach((rule, index) => {
        if (!rule.name?.trim()) {
          errors.push(`Approval step "${node.data?.label || 'Approval'}" has an unnamed SOD rule at row ${index + 1}`);
        }
        if (rule.actorSource === WorkflowApprovalActorSource.SpecificStepActor && !rule.sourceStepName?.trim()) {
          errors.push(`SOD rule "${rule.name || index + 1}" must select a source workflow step`);
        }
        if (rule.actorSource === WorkflowApprovalActorSource.ContextUser && !rule.contextField?.trim()) {
          errors.push(`SOD rule "${rule.name || index + 1}" must define a workflow context user field`);
        }
      });

    });

    const taskNodes = nodes.filter(node => node.type === 'task');
    taskNodes.forEach(node => {
      const assignmentType = node.data?.taskAssigneeType || 'currentActor';
      if (assignmentType === 'user' && !node.data?.taskAssigneeUserId) {
        errors.push(`Task step "${node.data?.label || 'Task'}" must have a user selected`);
      }

      if (assignmentType === 'role' && !node.data?.taskAssigneeRole) {
        errors.push(`Task step "${node.data?.label || 'Task'}" must have a role selected`);
      }

      if (assignmentType === 'dynamic' && !node.data?.taskDynamicExpression?.trim()) {
        errors.push(`Task step "${node.data?.label || 'Task'}" must have a dynamic user field`);
      }

      if (node.data?.taskActionType === 'document' && !node.data?.documentName?.trim()) {
        errors.push(`Task step "${node.data?.label || 'Task'}" must name the required document`);
      }
    });

    nodes
      .filter(node => !['start', 'end'].includes(node.type || ''))
      .forEach(node => {
        const checklist = readStepChecklist(node.data);
        checklist.forEach((item, index) => {
          if (!item.name?.trim()) {
            errors.push(`Step "${node.data?.label || 'Workflow step'}" has a blank checklist item at row ${index + 1}`);
          }
          if (item.requiresDocument && !item.documentType?.trim()) {
            errors.push(`Checklist item "${item.name || index + 1}" in step "${node.data?.label || 'Workflow step'}" must define a document type`);
          }
          if (item.requiresDocument && !item.documentName?.trim()) {
            errors.push(`Checklist item "${item.name || index + 1}" in step "${node.data?.label || 'Workflow step'}" must define a document name`);
          }
        });
      });
    
    if (errors.length > 0) {
      toast.error(errors.length === 1 ? errors[0] : `Validation errors: ${errors.join(' • ')}`);
      return false;
    }
    
    if (showSuccess) {
      toast.success('Workflow validation successful');
    }
    return true;
  };

  const baseVariableSuggestions = [
    { label: 'Entity ID', value: 'entityId', description: 'The ID of the entity being processed', group: 'Core' },
    { label: 'Initiated By', value: 'initiatedById', description: 'The ID of the user who initiated the workflow', group: 'Core' },
  ];

  const conditionVariableSuggestions = () => {
    const apiVariables = conditionVariables.map(variable => ({
      label: variable.displayName || variable.name,
      value: variable.name,
      description: variable.description || '',
      group: getVariableGroup(variable)
    }));

    const merged = [...baseVariableSuggestions];
    apiVariables.forEach(variable => {
      if (!merged.some(existing => existing.value === variable.value)) {
        merged.push(variable);
      }
    });

    return merged;
  };

  const getVariableGroup = (variable: WorkflowVariableInfo) => {
    const name = variable.name.toLowerCase();
    const dataType = (variable.dataType || '').toLowerCase();

    if (name.includes('date') || name.endsWith('at')) return 'Dates';
    if (name.includes('amount') || name.includes('total') || name.includes('cost') || name.includes('price') || name.includes('budget') || dataType === 'decimal') {
      return 'Financial';
    }
    if (name.includes('status') || name.includes('approval') || name.includes('priority') || name.includes('level')) return 'Status';
    if (name.includes('item') || name.includes('quantity') || name.includes('count')) return 'Items';
    if (name.includes('warehouse') || name.includes('delivery') || name.includes('address')) return 'Logistics';
    if (name.includes('project') || name.includes('department') || name.includes('costcenter') || name.includes('cost center') || name.includes('request') || name.includes('initiated')) {
      return 'People & Org';
    }
    if (name.includes('contract') || name.includes('tender') || name.includes('requisition') || name.includes('workorder') || name.includes('jobcard')) {
      return 'References';
    }

    return 'General';
  };

  const groupedVariables = (variables: { label: string; value: string; description?: string; group?: string }[]) => {
    const grouped = new Map<string, typeof variables>();
    variables.forEach(variable => {
      const group = variable.group || 'General';
      const list = grouped.get(group) ?? [];
      list.push(variable);
      grouped.set(group, list);
    });

    return Array.from(grouped.entries())
      .sort((a, b) => a[0].localeCompare(b[0]))
      .map(([group, items]) => ({
        group,
        items: items.sort((a, b) => a.label.localeCompare(b.label))
      }));
  };

  const conditionOperatorSuggestions = [
    { label: '==', value: '==' },
    { label: '!=', value: '!=' },
    { label: '>', value: '>' },
    { label: '<', value: '<' },
    { label: '>=', value: '>=' },
    { label: '<=', value: '<=' },
    { label: '&&', value: '&&' },
    { label: '||', value: '||' },
  ];

  const appendExpression = (current: string, snippet: string) => {
    if (!current) return snippet;
    const needsSpace = !current.endsWith(' ') && !snippet.startsWith(' ');
    return `${current}${needsSpace ? ' ' : ''}${snippet}`;
  };

  const invertSimpleCondition = (expression: string): string | null => {
    if (expression.includes('&&') || expression.includes('||')) {
      return null;
    }

    const operators = ['>=', '<=', '!=', '==', '>', '<'];
    for (const op of operators) {
      const index = indexOfOperator(expression, op);
      if (index > -1) {
        const left = expression.slice(0, index).trim();
        const right = expression.slice(index + op.length).trim();
        const inverted = invertOperator(op);
        return `${left} ${inverted} ${right}`;
      }
    }

    return null;
  };

  const invertOperator = (op: string): string => {
    switch (op) {
      case '>=':
        return '<';
      case '<=':
        return '>';
      case '>':
        return '<=';
      case '<':
        return '>=';
      case '==':
        return '!=';
      case '!=':
        return '==';
      default:
        return op;
    }
  };

  const indexOfOperator = (expression: string, op: string): number => {
    let inQuotes = false;
    for (let i = 0; i <= expression.length - op.length; i++) {
      const ch = expression[i];
      if (ch === '"' || ch === '\'') {
        inQuotes = !inQuotes;
      }

      if (inQuotes) {
        continue;
      }

      if (expression.slice(i, i + op.length) === op) {
        return i;
      }
    }
    return -1;
  };

  const updateSelectedNode = (updates: Record<string, any>) => {
    if (selectedWorkflowHasLiveInstances) return;
    if (!selectedNode) return;
    const updatedNode = {
      ...selectedNode,
      data: { ...selectedNode.data, ...updates }
    };
    setNodes((nds) =>
      nds.map((node) => (node.id === selectedNode.id ? updatedNode : node))
    );
    setSelectedNode(updatedNode);
  };

  const updateApprovalChecklist = (nextChecklist: WorkflowQualityCheckDto[]) => {
    updateSelectedNode({
      stepChecklist: nextChecklist,
      ...(selectedNode?.type === 'approval' ? { approvalChecklist: nextChecklist } : {}),
    });
  };

  const addApprovalChecklistItem = () => {
    const currentChecklist = readStepChecklist(selectedNode?.data);
    updateApprovalChecklist([
      ...currentChecklist,
      {
        id: `check-${Date.now()}`,
        name: '',
        description: '',
        isRequired: true,
        requiresDocument: false,
      },
    ]);
  };

  const updateApprovalChecklistItem = (itemId: string, updates: Partial<WorkflowQualityCheckDto>) => {
    const currentChecklist = readStepChecklist(selectedNode?.data);
    updateApprovalChecklist(
      currentChecklist.map((item) =>
        item.id === itemId
          ? { ...item, ...updates }
          : item
      )
    );
  };

  const removeApprovalChecklistItem = (itemId: string) => {
    const currentChecklist = readStepChecklist(selectedNode?.data);
    updateApprovalChecklist(currentChecklist.filter((item) => item.id !== itemId));
  };

  const updateApprovalConflictRules = (rules: WorkflowApprovalConflictRuleDto[]) => {
    updateSelectedNode({ approvalConflictRules: rules });
  };

  const addApprovalConflictRule = () => {
    const currentRules = (selectedNode?.data?.approvalConflictRules || []) as WorkflowApprovalConflictRuleDto[];
    updateApprovalConflictRules([
      ...currentRules,
      {
        id: `sod-${Date.now()}`,
        name: '',
        isEnabled: true,
        actorSource: WorkflowApprovalActorSource.PreviousStepActor,
      },
    ]);
  };

  const applyEnterpriseSodPreset = () => {
    const fields = [
      ['Requester cannot approve', 'requestedById'],
      ['Document creator cannot approve', 'createdById'],
      ['Evaluator cannot approve award', 'evaluatorUserId'],
      ['PO creator cannot confirm receipt', 'purchaseOrderCreatedById'],
      ['Invoice processor cannot approve payment', 'invoiceProcessedById'],
      ['Inventory issuer cannot approve adjustment', 'inventoryIssuedById'],
    ];
    const existing = (selectedNode?.data?.approvalConflictRules || []) as WorkflowApprovalConflictRuleDto[];
    const existingFields = new Set(existing.map(rule => rule.contextField).filter(Boolean));
    updateApprovalConflictRules([
      ...existing,
      ...fields.filter(([, contextField]) => !existingFields.has(contextField)).map(([name, contextField]) => ({
        id: crypto.randomUUID(), name, isEnabled: true,
        actorSource: WorkflowApprovalActorSource.ContextUser, contextField,
        message: `${name}.`,
      })),
    ]);
  };

  const updateApprovalConflictRule = (ruleId: string, updates: Partial<WorkflowApprovalConflictRuleDto>) => {
    const currentRules = (selectedNode?.data?.approvalConflictRules || []) as WorkflowApprovalConflictRuleDto[];
    updateApprovalConflictRules(currentRules.map(rule => rule.id === ruleId ? { ...rule, ...updates } : rule));
  };

  const removeApprovalConflictRule = (ruleId: string) => {
    const currentRules = (selectedNode?.data?.approvalConflictRules || []) as WorkflowApprovalConflictRuleDto[];
    updateApprovalConflictRules(currentRules.filter(rule => rule.id !== ruleId));
  };

  const updateSelectedEdge = (updates: Record<string, any>) => {
    if (selectedWorkflowHasLiveInstances) return;
    if (!selectedEdge) return;
    const updatedEdge = {
      ...selectedEdge,
      ...updates,
      data: { ...selectedEdge.data, ...(updates.data || {}) }
    };
    setEdges((eds) =>
      eds.map((edge) => (edge.id === selectedEdge.id ? updatedEdge : edge))
    );
    setSelectedEdge(updatedEdge);
  };

  const formatUserLabel = (user: User) => {
    const fullName = [user.firstName, user.lastName].filter(Boolean).join(' ').trim();
    return fullName ? `${fullName} (${user.username})` : user.username;
  };

  const workflowPickerLabel =
    selectedWorkflowOption?.name ||
    workflowName ||
    (workflowOptionsLoading ? 'Loading workflows...' : 'Select workflow...');

  const handlePickWorkflow = async (definitionId: string) => {
    setWorkflowPickerOpen(false);
    setWorkflowPickerQuery('');
    await loadWorkflowData(definitionId);
  };

  const handleCreateNewWorkflowFromPicker = () => {
    const name = workflowPickerQuery.trim();
    resetToNewWorkflow(name || '');
    setWorkflowPickerOpen(false);
    setWorkflowPickerQuery('');
  };

  const handleEntityTypeSelect = (value: string) => {
    if (value === customEntityTypeValue) {
      setIsCustomEntityType(true);
      setEntityType('');
      return;
    }

    setIsCustomEntityType(false);
    setEntityType(value);
  };

  const { items: entityTypeOptions, fallback: entityTypeFallback } = filterEntityTypesByModule(
    availableEntityTypes,
    entityTypeModuleFilter
  );

  const selectedApproverRoles =
    selectedNode?.type === 'approval'
      ? normalizeStringList(selectedNode.data?.approverRoles || selectedNode.data?.approvers)
      : [];
  const selectedApproverUsers =
    selectedNode?.type === 'approval'
      ? normalizeStringList(selectedNode.data?.approverUsers)
      : [];
  const selectedApprovalChecklist =
    selectedNode && !['start', 'end'].includes(selectedNode.type || '')
      ? readStepChecklist(selectedNode.data)
      : [];
  const selectedApprovalConflictRules =
    selectedNode?.type === 'approval'
      ? ((selectedNode.data?.approvalConflictRules || []) as WorkflowApprovalConflictRuleDto[])
      : [];
  const availableSodSourceSteps = nodes
    .filter(node => node.id !== selectedNode?.id && !['start', 'end'].includes(node.type || ''))
    .map(node => String(node.data?.label || '').trim())
    .filter(Boolean);
  const requiredApprovalChecklistCount = selectedApprovalChecklist.filter((item) => item.isRequired !== false).length;
  const filteredAvailableRoles = availableRoles.filter((role) =>
    role.name.toLowerCase().includes(roleSearch.toLowerCase())
  );
  const filteredAvailableUsers = availableUsers.filter((user) =>
    formatUserLabel(user).toLowerCase().includes(userSearch.toLowerCase())
  );
  const selectedTaskUser = selectedNode?.type === 'task' && selectedNode.data?.taskAssigneeUserId
    ? availableUsers.find((user) => user.id === selectedNode.data.taskAssigneeUserId)
    : undefined;

  if (!isOpen) return null;

  return (
    <>
      <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-[95vw] max-h-[95vh] p-0">
        <div className="flex flex-col h-[95vh]">
          {/* Header */}
          <div className="p-4 border-b space-y-3">
            {/* Leave space for DialogContent's built-in top-right close button */}
            <div className="flex items-start justify-between gap-3 pr-12">
              <div>
                <DialogTitle className="text-xl font-semibold">
                  {selectedWorkflowIsImmutable ? 'View Workflow' : workflowId ? 'Edit Workflow' : 'Create New Workflow'}
                </DialogTitle>
                <div className="text-xs text-muted-foreground mt-1">
                  Pick an existing workflow to load it, or start a new one.
                </div>
              </div>
              <div className="flex items-center gap-2">
                <Button variant="outline" size="sm" onClick={() => handleValidate()}>
                  Validate
                </Button>
                <Button variant="outline" size="sm" onClick={handleTest}>
                  <Play className="h-4 w-4 mr-2" />
                  Test
                </Button>
                <Button
                  onClick={handleSave}
                  disabled={isSaving || selectedWorkflowHasLiveInstances}
                  title={selectedWorkflowHasLiveInstances
                    ? workflowLiveInstanceLockMessage
                    : undefined}
                >
                  <Save className="h-4 w-4 mr-2" />
                  {isSaving ? 'Saving...' : 'Save'}
                </Button>
              </div>
            </div>

            {selectedWorkflowHasLiveInstances && (
              <div className="flex items-start gap-2 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                <span>{workflowLiveInstanceLockMessage}</span>
              </div>
            )}

            <div className="grid grid-cols-1 md:grid-cols-12 gap-3 items-end">
              <div className="md:col-span-4 flex flex-col space-y-1">
                <Label className="text-xs text-muted-foreground">Workflow</Label>
                <Popover open={workflowPickerOpen} onOpenChange={setWorkflowPickerOpen}>
                  <PopoverTrigger asChild>
                    <Button
                      variant="outline"
                      role="combobox"
                      aria-expanded={workflowPickerOpen}
                      className="w-full justify-between"
                      disabled={workflowOptionsLoading}
                    >
                      <span className="truncate">{workflowPickerLabel}</span>
                      <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                    </Button>
                  </PopoverTrigger>
                  <PopoverContent className="w-[520px] p-0" align="start">
                    <Command>
                      <CommandInput
                        placeholder="Search workflows..."
                        value={workflowPickerQuery}
                        onValueChange={setWorkflowPickerQuery}
                      />
                      <CommandList>
                        <CommandEmpty>No workflows found.</CommandEmpty>
                        <CommandGroup heading="Workflows (Active + Inactive)">
                          {workflowOptions.map((workflow) => (
                            <CommandItem
                              key={workflow.id}
                              value={`${workflow.name} ${workflow.entityType} ${workflow.isActive ? 'active' : 'inactive'}`}
                              onSelect={() => handlePickWorkflow(workflow.id)}
                            >
                              <Check
                                className={cn(
                                  "mr-2 h-4 w-4",
                                  activeWorkflowId === workflow.id ? "opacity-100" : "opacity-0"
                                )}
                              />
                              <div className="flex flex-1 items-center justify-between gap-3 min-w-0">
                                <div className="min-w-0">
                                  <div className="truncate">{workflow.name}</div>
                                  <div className="text-xs text-muted-foreground truncate">{workflow.entityType}</div>
                                </div>
                                <Badge
                                  className="shrink-0"
                                  variant={workflow.isActive ? 'default' : 'secondary'}
                                >
                                  {workflow.isActive ? 'Active' : 'Inactive'}
                                </Badge>
                              </div>
                            </CommandItem>
                          ))}
                        </CommandGroup>
                        <CommandSeparator />
                        <CommandGroup heading="New">
                          <CommandItem
                            value={`__new__ ${workflowPickerQuery}`}
                            onSelect={handleCreateNewWorkflowFromPicker}
                          >
                            Start new workflow{workflowPickerQuery.trim() ? `: "${workflowPickerQuery.trim()}"` : ''}
                          </CommandItem>
                        </CommandGroup>
                      </CommandList>
                    </Command>
                  </PopoverContent>
                </Popover>
                {workflowOptionsError && (
                  <span className="text-xs text-amber-600">{workflowOptionsError}</span>
                )}
              </div>

              <div className="md:col-span-4 flex flex-col space-y-1">
                <Label className="text-xs text-muted-foreground">Description</Label>
                <Input
                  placeholder="Optional description"
                  value={workflowDescription}
                  onChange={(e) => setWorkflowDescription(e.target.value)}
                  disabled={selectedWorkflowHasLiveInstances}
                  className="w-full"
                />
              </div>

              <div className="md:col-span-2 flex flex-col space-y-1">
                <Label className="text-xs text-muted-foreground">Module</Label>
                <Select
                  value={entityTypeModuleFilter}
                  onValueChange={setEntityTypeModuleFilter}
                  disabled={selectedWorkflowHasLiveInstances}
                >
                  <SelectTrigger className="w-full">
                    <SelectValue placeholder="All Modules" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All Modules</SelectItem>
                    {moduleIntegrations.map((module) => (
                      <SelectItem key={module.id} value={module.id}>
                        {module.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className={cn("md:col-span-2 flex flex-col space-y-1", isCustomEntityType ? "md:col-span-3" : "")}>
                <Label className="text-xs text-muted-foreground">Entity Type</Label>
                <Select
                  value={isCustomEntityType ? customEntityTypeValue : entityType}
                  onValueChange={handleEntityTypeSelect}
                  disabled={selectedWorkflowHasLiveInstances}
                >
                  <SelectTrigger className="w-full">
                    <SelectValue placeholder={entityTypesLoading ? 'Loading...' : 'Entity Type'} />
                  </SelectTrigger>
                  <SelectContent>
                    {entityTypeOptions.map((item) => (
                      <SelectItem key={item.id} value={item.name}>
                        {item.name}
                      </SelectItem>
                    ))}
                    <SelectItem value={customEntityTypeValue}>Custom...</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              {isCustomEntityType && (
                <div className="md:col-span-3 flex flex-col space-y-1">
                  <Label className="text-xs text-muted-foreground">Custom Entity Type</Label>
                  <Input
                    placeholder="e.g. PurchaseRequisition"
                    value={entityType}
                    onChange={(e) => setEntityType(e.target.value)}
                    disabled={selectedWorkflowHasLiveInstances}
                    className="w-full"
                  />
                </div>
              )}

              {entityTypeFallback && (
                <div className="md:col-span-12">
                  <span className="text-xs text-amber-600">
                    No matching entity types for the selected module. Showing all.
                  </span>
                </div>
              )}
            </div>
          </div>
          {(loadError || entityTypesError) && (
            <div className="px-4 py-2 text-sm text-red-600 border-b">
              {[loadError, entityTypesError].filter(Boolean).join(' ')}
            </div>
          )}

          <div className="flex flex-1 overflow-hidden">
            {/* Left Sidebar - Node Palette */}
            <div className="w-64 border-r bg-gray-50 p-4">
              <Tabs value={activeTab} onValueChange={setActiveTab}>
                <TabsList className="grid w-full grid-cols-2">
                  <TabsTrigger value="design">Design</TabsTrigger>
                  <TabsTrigger value="modules">Modules</TabsTrigger>
                </TabsList>
                
                <TabsContent value="design" className="mt-4">
                  <div className="space-y-2">
                    <Label className="text-sm font-medium">Workflow Elements</Label>
                    <div className="grid gap-2">
                      {nodePalette.map((item) => {
                        const IconComponent = item.icon;
                        return (
                          <div
                            key={item.type}
                            className={cn(
                              'flex items-center space-x-2 p-2 rounded text-white',
                              selectedWorkflowHasLiveInstances
                                ? 'cursor-not-allowed opacity-60'
                                : 'cursor-pointer hover:bg-gray-100',
                              item.color
                            )}
                            draggable={!selectedWorkflowHasLiveInstances}
                            onDragStart={(event) => onDragStart(event, item.type)}
                          >
                            <IconComponent className="h-4 w-4" />
                            <span className="text-sm font-medium">{item.label}</span>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                </TabsContent>
                
                <TabsContent value="modules" className="mt-4">
                  <div className="space-y-2">
                    <Label className="text-sm font-medium">Module Integrations</Label>
                    <div className="space-y-1">
                      {moduleIntegrations.map((module) => {
                        const IconComponent = module.icon;
                        return (
                          <div
                            key={module.id}
                            className={cn(
                              'flex items-center space-x-2 p-2 rounded border',
                              selectedWorkflowHasLiveInstances
                                ? 'cursor-not-allowed opacity-60'
                                : 'cursor-pointer hover:bg-gray-100'
                            )}
                            draggable={!selectedWorkflowHasLiveInstances}
                            onDragStart={(event) => {
                              onDragStart(event, 'integration');
                              // Set module-specific data
                            }}
                          >
                            <IconComponent className="h-4 w-4" />
                            <span className="text-xs">{module.name}</span>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                </TabsContent>
              </Tabs>
            </div>

            {/* Main Canvas */}
            <div className="flex-1" ref={reactFlowWrapper}>
              <ReactFlowProvider>
                <ReactFlow
                  nodes={nodes}
                  edges={edges}
                  onNodesChange={handleNodesChange}
                  onEdgesChange={handleEdgesChange}
                  onConnect={onConnect}
                  onInit={setReactFlowInstance}
                  onDrop={onDrop}
                  onDragOver={onDragOver}
                  onNodeClick={onNodeClick}
                  onEdgeClick={onEdgeClick}
                  nodesDraggable={!selectedWorkflowHasLiveInstances}
                  nodesConnectable={!selectedWorkflowHasLiveInstances}
                  deleteKeyCode={null}
                  elementsSelectable
                  nodeTypes={nodeTypes}
                  fitView
                >
                  <Controls />
                  <MiniMap />
                  <Background variant={BackgroundVariant.Dots} gap={12} size={1} />
                </ReactFlow>
              </ReactFlowProvider>
            </div>

            {/* Right Sidebar - Properties */}
            {isPropertiesOpen && selectedNode && (
              <div className="w-80 border-l bg-gray-50">
                <div className="p-4">
                  <div className="flex items-center justify-between mb-4">
                    <h3 className="font-medium">Node Properties</h3>
                    <div className="flex items-center gap-1">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={deleteSelectedNode}
                        disabled={selectedWorkflowHasLiveInstances || selectedNode.type === 'start'}
                        title={
                          selectedNode.type === 'start'
                            ? 'The start node cannot be deleted'
                            : 'Delete this workflow step'
                        }
                        className="text-red-600 hover:text-red-700"
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => setIsPropertiesOpen(false)}
                        title="Close properties"
                      >
                        <X className="h-4 w-4" />
                      </Button>
                    </div>
                  </div>
                  
                  <ScrollArea className="h-[calc(100vh-200px)]">
                    <div className="space-y-4">
                      {/* Node-specific properties would go here */}
                      <div>
                        <Label>Node Type</Label>
                        <Badge variant="outline">{selectedNode.type}</Badge>
                      </div>
                      
                      <div>
                        <Label>Label</Label>
                        <Input
                          value={selectedNode.data?.label || ''}
                          onChange={(e) => {
                            updateSelectedNode({ label: e.target.value });
                          }}
                        />
                      </div>

                      {/* Conditional properties based on node type */}
                      {selectedNode.type === 'task' && (
                        <>
                          <Separator />
                          <div>
                            <Label>Task Action</Label>
                            <Select
                              value={selectedNode.data?.taskActionType || 'general'}
                              onValueChange={(value) => updateSelectedNode({ taskActionType: value })}
                              disabled={selectedWorkflowHasLiveInstances}
                            >
                              <SelectTrigger>
                                <SelectValue placeholder="Select task action" />
                              </SelectTrigger>
                              <SelectContent>
                                <SelectItem value="general">General Task</SelectItem>
                                <SelectItem value="document">Attach Document</SelectItem>
                                <SelectItem value="review">Review / Validate</SelectItem>
                                <SelectItem value="update-record">Update Record</SelectItem>
                              </SelectContent>
                            </Select>
                          </div>

                          {selectedNode.data?.taskActionType === 'document' && (
                            <div>
                              <Label>Document Requirement</Label>
                              <Input
                                value={selectedNode.data?.documentName || ''}
                                placeholder="e.g. Signed customer approval"
                                disabled={selectedWorkflowHasLiveInstances}
                                onChange={(e) => updateSelectedNode({ documentName: e.target.value })}
                              />
                            </div>
                          )}

                          <div>
                            <Label>Assign To</Label>
                            <Select
                              value={selectedNode.data?.taskAssigneeType || 'currentActor'}
                              disabled={selectedWorkflowHasLiveInstances}
                              onValueChange={(value) => {
                                const assigneeLabel =
                                  value === 'requestorManager'
                                    ? 'Requestor Manager'
                                    : value === 'previousStepUser'
                                      ? 'Previous Step User'
                                      : value === 'dynamic'
                                        ? selectedNode.data?.taskDynamicExpression || 'Dynamic User'
                                        : '';

                                updateSelectedNode({
                                  taskAssigneeType: value,
                                  taskAssigneeUserId: '',
                                  taskAssigneeRole: value === 'role' ? selectedNode.data?.taskAssigneeRole || '' : '',
                                  taskDynamicExpression: value === 'dynamic' ? selectedNode.data?.taskDynamicExpression || '' : '',
                                  assignee: assigneeLabel,
                                });
                              }}
                            >
                              <SelectTrigger>
                                <SelectValue placeholder="Select assignee mode" />
                              </SelectTrigger>
                              <SelectContent>
                                <SelectItem value="currentActor">Current / Previous Actor</SelectItem>
                                <SelectItem value="role">Role</SelectItem>
                                <SelectItem value="user">Specific User</SelectItem>
                                <SelectItem value="requestorManager">Requestor Manager</SelectItem>
                                <SelectItem value="previousStepUser">Previous Step User</SelectItem>
                                <SelectItem value="dynamic">Dynamic User From Context</SelectItem>
                              </SelectContent>
                            </Select>
                          </div>

                          {(selectedNode.data?.taskAssigneeType || 'currentActor') === 'role' && (
                            <div>
                              <Label>Role</Label>
                              <Select
                                value={selectedNode.data?.taskAssigneeRole || ''}
                                disabled={selectedWorkflowHasLiveInstances}
                                onValueChange={(value) =>
                                  updateSelectedNode({
                                    taskAssigneeType: 'role',
                                    taskAssigneeRole: value,
                                    assignee: value,
                                  })
                                }
                              >
                                <SelectTrigger>
                                  <SelectValue placeholder="Select role" />
                                </SelectTrigger>
                                <SelectContent>
                                  {availableRoles.map((role) => (
                                    <SelectItem key={role.id} value={role.name}>
                                      {role.name}
                                    </SelectItem>
                                  ))}
                                </SelectContent>
                              </Select>
                            </div>
                          )}

                          {(selectedNode.data?.taskAssigneeType || 'currentActor') === 'user' && (
                            <div>
                              <Label>User</Label>
                              <Popover>
                                <PopoverTrigger asChild>
                                  <Button
                                    type="button"
                                    variant="outline"
                                    className="w-full justify-between"
                                    disabled={selectedWorkflowHasLiveInstances}
                                  >
                                    <span className="truncate">
                                      {selectedTaskUser
                                        ? formatUserLabel(selectedTaskUser)
                                        : 'Select user'}
                                    </span>
                                    <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                  </Button>
                                </PopoverTrigger>
                                <PopoverContent className="w-[280px] p-0" align="start">
                                  <Command>
                                    <CommandInput placeholder="Search users..." />
                                    <CommandList>
                                      <CommandEmpty>No users found.</CommandEmpty>
                                      <CommandGroup>
                                        {availableUsers.map((user) => (
                                          <CommandItem
                                            key={user.id}
                                            value={`${formatUserLabel(user)} ${user.email || ''}`}
                                            onSelect={() =>
                                              updateSelectedNode({
                                                taskAssigneeType: 'user',
                                                taskAssigneeUserId: user.id,
                                                assignee: formatUserLabel(user),
                                              })
                                            }
                                          >
                                            <Check
                                              className={cn(
                                                'mr-2 h-4 w-4',
                                                selectedNode.data?.taskAssigneeUserId === user.id
                                                  ? 'opacity-100'
                                                  : 'opacity-0'
                                              )}
                                            />
                                            <span className="truncate">{formatUserLabel(user)}</span>
                                          </CommandItem>
                                        ))}
                                      </CommandGroup>
                                    </CommandList>
                                  </Command>
                                </PopoverContent>
                              </Popover>
                            </div>
                          )}

                          {(selectedNode.data?.taskAssigneeType || 'currentActor') === 'dynamic' && (
                            <div>
                              <Label>Dynamic User Field</Label>
                              <Input
                                value={selectedNode.data?.taskDynamicExpression || ''}
                                placeholder="e.g. technicianId"
                                disabled={selectedWorkflowHasLiveInstances}
                                onChange={(e) =>
                                  updateSelectedNode({
                                    taskDynamicExpression: e.target.value,
                                    assignee: e.target.value || 'Dynamic User',
                                  })
                                }
                              />
                            </div>
                          )}

                          <div className="grid grid-cols-2 gap-3">
                            <div>
                              <Label>Priority</Label>
                              <Select
                                value={selectedNode.data?.priority || 'medium'}
                                onValueChange={(value) => updateSelectedNode({ priority: value })}
                                disabled={selectedWorkflowHasLiveInstances}
                              >
                                <SelectTrigger>
                                  <SelectValue placeholder="Priority" />
                                </SelectTrigger>
                                <SelectContent>
                                  <SelectItem value="low">Low</SelectItem>
                                  <SelectItem value="medium">Medium</SelectItem>
                                  <SelectItem value="high">High</SelectItem>
                                </SelectContent>
                              </Select>
                            </div>
                            <div>
                              <Label>Due Hours</Label>
                              <Input
                                type="number"
                                min="0"
                                value={selectedNode.data?.estimatedHours || ''}
                                placeholder="24"
                                disabled={selectedWorkflowHasLiveInstances}
                                onChange={(e) =>
                                  updateSelectedNode({
                                    estimatedHours: e.target.value,
                                    dueDate: e.target.value ? `${e.target.value}h` : '',
                                  })
                                }
                              />
                            </div>
                          </div>

                          <div>
                            <Label>Instructions</Label>
                            <Textarea
                              value={selectedNode.data?.instructions || ''}
                              placeholder="What should this person do?"
                              rows={4}
                              disabled={selectedWorkflowHasLiveInstances}
                              onChange={(e) => updateSelectedNode({ instructions: e.target.value })}
                            />
                          </div>

                          <div className="rounded-md border bg-white p-3 space-y-3">
                            <div className="flex items-start justify-between gap-3">
                              <div>
                                <Label>Step Checklist</Label>
                                <p className="text-xs text-muted-foreground">
                                  The task owner must complete required checks before submitting this step.
                                </p>
                              </div>
                              <Button
                                type="button"
                                variant="outline"
                                size="sm"
                                onClick={() => setChecklistDialogOpen(true)}
                              >
                                <CheckCircle className="h-3.5 w-3.5 mr-1" />
                                Configure
                              </Button>
                            </div>
                            <div className="flex flex-wrap gap-2">
                              <Badge variant="secondary">{selectedApprovalChecklist.length} items</Badge>
                              <Badge variant="secondary">{requiredApprovalChecklistCount} required</Badge>
                            </div>
                          </div>
                        </>
                      )}

                      {selectedNode.type === 'approval' && (
                        <>
                          <Separator />
                          <div>
                            <Label>Approval Type</Label>
                            <Select
                              value={selectedNode.data?.approvalType || 'any'}
                              onValueChange={(value) => updateSelectedNode({ approvalType: value })}
                            >
                              <SelectTrigger>
                                <SelectValue placeholder="Select approval type" />
                              </SelectTrigger>
                              <SelectContent>
                                <SelectItem value="any">Any Approver</SelectItem>
                                <SelectItem value="all">All Approvers</SelectItem>
                                <SelectItem value="majority">Majority</SelectItem>
                                <SelectItem value="minimum">Minimum Number</SelectItem>
                              </SelectContent>
                            </Select>
                          </div>

                          <div>
                            <Label>Approval Sequence</Label>
                            <Select
                              value={selectedNode.data?.approvalActivationMode || 'parallel'}
                              disabled={selectedWorkflowHasLiveInstances}
                              onValueChange={(value) => updateSelectedNode({ approvalActivationMode: value })}
                            >
                              <SelectTrigger>
                                <SelectValue placeholder="Select approval sequence" />
                              </SelectTrigger>
                              <SelectContent>
                                <SelectItem value="parallel">Parallel</SelectItem>
                                <SelectItem value="sequential">Sequential</SelectItem>
                              </SelectContent>
                            </Select>
                            <p className="mt-1 text-xs text-muted-foreground">
                              Sequential mode activates approvers one at a time in the configured order.
                            </p>
                          </div>

                          {(selectedNode.data?.approvalType === 'minimum' || selectedNode.data?.approvalType === 'majority') && (
                            <div>
                              <Label>Minimum Approvals</Label>
                              <Input
                                type="number"
                                min="1"
                                value={selectedNode.data?.minApprovalsRequired || 1}
                                disabled={selectedWorkflowHasLiveInstances}
                                onChange={(event) =>
                                  updateSelectedNode({ minApprovalsRequired: Math.max(Number(event.target.value) || 1, 1) })
                                }
                              />
                            </div>
                          )}

                          <div className="space-y-2 rounded-md border bg-white p-3">
                            <Label>Approval Controls</Label>
                            <label className="flex items-start gap-2 text-sm">
                              <Checkbox
                                checked={selectedNode.data?.preventInitiatorApproval === true}
                                disabled={selectedWorkflowHasLiveInstances}
                                onCheckedChange={(checked) =>
                                  updateSelectedNode({ preventInitiatorApproval: checked === true })
                                }
                              />
                              <span>Prevent the workflow initiator from approving this step</span>
                            </label>
                            <label className="flex items-start gap-2 text-sm">
                              <Checkbox
                                checked={selectedNode.data?.requireDistinctApprovers === true}
                                disabled={selectedWorkflowHasLiveInstances}
                                onCheckedChange={(checked) =>
                                  updateSelectedNode({ requireDistinctApprovers: checked === true })
                                }
                              />
                              <span>Require a different user for each approval slot</span>
                            </label>
                          </div>

                          <div className="space-y-3 rounded-md border bg-white p-3">
                            <label className="flex items-center gap-2 text-sm"><Checkbox checked={selectedNode.data?.requireElectronicSignature === true} disabled={selectedWorkflowHasLiveInstances} onCheckedChange={checked => updateSelectedNode({ requireElectronicSignature: checked === true })} />Require electronic signature</label>
                            {selectedNode.data?.requireElectronicSignature === true && <>
                              <Select value={String(selectedNode.data?.signatureMethod ?? WorkflowSignatureMethod.Attestation)} disabled={selectedWorkflowHasLiveInstances} onValueChange={value => updateSelectedNode({ signatureMethod: Number(value) })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>
                                <SelectItem value={String(WorkflowSignatureMethod.Attestation)}>Attestation</SelectItem><SelectItem value={String(WorkflowSignatureMethod.DigitalCertificate)}>Digital certificate</SelectItem><SelectItem value={String(WorkflowSignatureMethod.ExternalProvider)}>External provider</SelectItem>
                              </SelectContent></Select>
                              <Input value={selectedNode.data?.requiredSigningRole || ''} placeholder="Required signing role (optional)" disabled={selectedWorkflowHasLiveInstances} onChange={event => updateSelectedNode({ requiredSigningRole: event.target.value })} />
                              <Textarea value={selectedNode.data?.signatureAttestation || 'I confirm that I reviewed and approve this transaction.'} disabled={selectedWorkflowHasLiveInstances} onChange={event => updateSelectedNode({ signatureAttestation: event.target.value })} />
                              {Number(selectedNode.data?.signatureMethod) === WorkflowSignatureMethod.DigitalCertificate && <label className="flex items-center gap-2 text-sm"><Checkbox checked={selectedNode.data?.requireValidCertificateChain === true} disabled={selectedWorkflowHasLiveInstances} onCheckedChange={checked => updateSelectedNode({ requireValidCertificateChain: checked === true })} />Require valid certificate chain</label>}
                            </>}
                          </div>

                          <div className="space-y-3 rounded-md border bg-white p-3">
                            <div className="flex items-center justify-between gap-2">
                              <div>
                                <Label>Cross-Step SOD Rules</Label>
                                <p className="text-xs text-muted-foreground">Block conflicting actors from approving this step.</p>
                              </div>
                              <Button
                                type="button"
                                variant="outline"
                                size="sm"
                                disabled={selectedWorkflowHasLiveInstances}
                                onClick={addApprovalConflictRule}
                              >
                                <Plus className="mr-1 h-3.5 w-3.5" />
                                Add
                              </Button>
                              <Button type="button" variant="outline" size="sm" disabled={selectedWorkflowHasLiveInstances} onClick={applyEnterpriseSodPreset}>
                                ERP preset
                              </Button>
                            </div>

                            {selectedApprovalConflictRules.length === 0 ? (
                              <p className="text-xs text-muted-foreground">No cross-step conflicts configured.</p>
                            ) : (
                              <div className="space-y-3">
                                {selectedApprovalConflictRules.map((rule) => (
                                  <div key={rule.id} className="space-y-2 border-t pt-3 first:border-t-0 first:pt-0">
                                    <div className="flex items-center gap-2">
                                      <Input
                                        value={rule.name || ''}
                                        placeholder="Rule name"
                                        disabled={selectedWorkflowHasLiveInstances}
                                        onChange={(event) => updateApprovalConflictRule(rule.id, { name: event.target.value })}
                                      />
                                      <Button
                                        type="button"
                                        variant="ghost"
                                        size="icon"
                                        className="shrink-0 text-red-600"
                                        disabled={selectedWorkflowHasLiveInstances}
                                        onClick={() => removeApprovalConflictRule(rule.id)}
                                        aria-label="Remove SOD rule"
                                      >
                                        <Trash2 className="h-4 w-4" />
                                      </Button>
                                    </div>
                                    <Select
                                      value={String(rule.actorSource)}
                                      disabled={selectedWorkflowHasLiveInstances}
                                      onValueChange={(value) => updateApprovalConflictRule(rule.id, {
                                        actorSource: Number(value) as WorkflowApprovalActorSource,
                                        sourceStepName: undefined,
                                        contextField: undefined,
                                      })}
                                    >
                                      <SelectTrigger>
                                        <SelectValue placeholder="Conflicting actor" />
                                      </SelectTrigger>
                                      <SelectContent>
                                        <SelectItem value={String(WorkflowApprovalActorSource.PreviousStepActor)}>Previous step actor</SelectItem>
                                        <SelectItem value={String(WorkflowApprovalActorSource.AnyPreviousApprover)}>Any prior approver</SelectItem>
                                        <SelectItem value={String(WorkflowApprovalActorSource.SpecificStepActor)}>Actor from named step</SelectItem>
                                        <SelectItem value={String(WorkflowApprovalActorSource.ContextUser)}>User from workflow field</SelectItem>
                                      </SelectContent>
                                    </Select>

                                    {rule.actorSource === WorkflowApprovalActorSource.SpecificStepActor && (
                                      <Select
                                        value={rule.sourceStepName || ''}
                                        disabled={selectedWorkflowHasLiveInstances}
                                        onValueChange={(value) => updateApprovalConflictRule(rule.id, { sourceStepName: value })}
                                      >
                                        <SelectTrigger>
                                          <SelectValue placeholder="Select source step" />
                                        </SelectTrigger>
                                        <SelectContent>
                                          {availableSodSourceSteps.map((stepName) => (
                                            <SelectItem key={stepName} value={stepName}>{stepName}</SelectItem>
                                          ))}
                                        </SelectContent>
                                      </Select>
                                    )}

                                    {rule.actorSource === WorkflowApprovalActorSource.ContextUser && (
                                      <Input
                                        value={rule.contextField || ''}
                                        placeholder="Context user field, e.g. requestedById"
                                        disabled={selectedWorkflowHasLiveInstances}
                                        onChange={(event) => updateApprovalConflictRule(rule.id, { contextField: event.target.value })}
                                      />
                                    )}

                                    <Input
                                      value={rule.message || ''}
                                      placeholder="Optional blocking message"
                                      disabled={selectedWorkflowHasLiveInstances}
                                      onChange={(event) => updateApprovalConflictRule(rule.id, { message: event.target.value })}
                                    />
                                    <label className="flex items-center gap-2 text-xs">
                                      <Checkbox
                                        checked={rule.isEnabled !== false}
                                        disabled={selectedWorkflowHasLiveInstances}
                                        onCheckedChange={(checked) => updateApprovalConflictRule(rule.id, { isEnabled: checked === true })}
                                      />
                                      Enabled
                                    </label>
                                  </div>
                                ))}
                              </div>
                            )}
                          </div>
                          
                          <div>
                            <Label>Escalation Timeout (hours)</Label>
                            <Input type="number" placeholder="24" />
                          </div>
                          
                          <div className="rounded-md border bg-white p-3 space-y-3">
                            <div className="flex items-start justify-between gap-3">
                              <div>
                                <Label>Approvers</Label>
                                <p className="text-xs text-muted-foreground">
                                  Choose roles or named users allowed to complete this step.
                                </p>
                              </div>
                              <Button
                                type="button"
                                variant="outline"
                                size="sm"
                                onClick={() => setApproverDialogOpen(true)}
                              >
                                <Users className="h-3.5 w-3.5 mr-1" />
                                Configure
                              </Button>
                            </div>
                            <div className="flex flex-wrap gap-2">
                              <Badge variant="secondary">{selectedApproverRoles.length} roles</Badge>
                              <Badge variant="secondary">{selectedApproverUsers.length} users</Badge>
                            </div>
                            {(selectedApproverRoles.length > 0 || selectedApproverUsers.length > 0) && (
                              <p className="text-xs text-muted-foreground line-clamp-2">
                                {[
                                  ...selectedApproverRoles,
                                  ...selectedApproverUsers
                                    .map((userId) => availableUsers.find((user) => user.id === userId))
                                    .filter((user): user is User => Boolean(user))
                                    .map(formatUserLabel),
                                ].slice(0, 6).join(', ')}
                              </p>
                            )}
                          </div>

                          <div className="rounded-md border bg-white p-3 space-y-3">
                            <div className="flex items-start justify-between gap-3">
                              <div>
                                <Label>Step Checklist</Label>
                                <p className="text-xs text-muted-foreground">
                                  Required checks must be satisfied before this approval can be completed.
                                </p>
                              </div>
                              <Button
                                type="button"
                                variant="outline"
                                size="sm"
                                onClick={() => setChecklistDialogOpen(true)}
                              >
                                <CheckCircle className="h-3.5 w-3.5 mr-1" />
                                Configure
                              </Button>
                            </div>
                            <div className="flex flex-wrap gap-2">
                              <Badge variant="secondary">{selectedApprovalChecklist.length} items</Badge>
                              <Badge variant="secondary">{requiredApprovalChecklistCount} required</Badge>
                            </div>
                          </div>
                        </>
                      )}

                      {selectedNode.type === 'condition' && (
                        <>
                          <Separator />
                          <div>
                            <Label>Condition Logic</Label>
                            <Textarea
                              placeholder="Define your condition logic here..."
                              rows={4}
                              value={selectedNode.data?.conditionExpression || ''}
                              onChange={(e) => updateSelectedNode({ conditionExpression: e.target.value })}
                            />
                            <div className="mt-2 space-y-2">
                              <div>
                                <Label className="text-xs text-muted-foreground">Variables</Label>
                                <div className="mt-1 space-y-3">
                                  {groupedVariables(conditionVariableSuggestions()).map(group => (
                                    <div key={group.group}>
                                      <div className="text-[11px] uppercase tracking-wide text-muted-foreground mb-1">
                                        {group.group}
                                      </div>
                                      <div className="flex flex-wrap gap-2">
                                        {group.items.map(item => (
                                          <Button
                                            key={item.value}
                                            type="button"
                                            variant="outline"
                                            size="sm"
                                            title={item.description || item.value}
                                            onClick={() =>
                                              updateSelectedNode({
                                                conditionExpression: appendExpression(
                                                  selectedNode.data?.conditionExpression || '',
                                                  item.value
                                                )
                                              })
                                            }
                                          >
                                            {item.label}
                                          </Button>
                                        ))}
                                      </div>
                                    </div>
                                  ))}
                                  {variablesLoading && (
                                    <span className="text-xs text-muted-foreground">Loading...</span>
                                  )}
                                </div>
                              </div>

                              <div>
                                <Label className="text-xs text-muted-foreground">Operators</Label>
                                <div className="flex flex-wrap gap-2 mt-1">
                                  {conditionOperatorSuggestions.map((item) => (
                                    <Button
                                      key={item.value}
                                      type="button"
                                      variant="outline"
                                      size="sm"
                                      onClick={() =>
                                        updateSelectedNode({
                                          conditionExpression: appendExpression(
                                            selectedNode.data?.conditionExpression || '',
                                            item.value
                                          )
                                        })
                                      }
                                    >
                                      {item.label}
                                    </Button>
                                  ))}
                                </div>
                              </div>
                              <p className="text-xs text-muted-foreground">
                                Variables come from the workflow data context (plus `entityId`, `initiatedById`).
                              </p>
                            </div>
                          </div>
                          
                          <div>
                            <Label>Operator</Label>
                            <Select
                              value={selectedNode.data?.operator || 'AND'}
                              onValueChange={(value) => updateSelectedNode({ operator: value })}
                            >
                              <SelectTrigger>
                                <SelectValue placeholder="Select operator" />
                              </SelectTrigger>
                              <SelectContent>
                                <SelectItem value="AND">AND</SelectItem>
                                <SelectItem value="OR">OR</SelectItem>
                              </SelectContent>
                            </Select>
                          </div>
                        </>
                      )}

                      {selectedNode.type === 'notification' && (
                        <>
                          <Separator />
                          <div>
                            <Label>Notification Template</Label>
                            <Select>
                              <SelectTrigger>
                                <SelectValue placeholder="Select template" />
                              </SelectTrigger>
                              <SelectContent>
                                <SelectItem value="approval-request">Approval Request</SelectItem>
                                <SelectItem value="task-assignment">Task Assignment</SelectItem>
                                <SelectItem value="escalation">Escalation</SelectItem>
                                <SelectItem value="completion">Completion Notice</SelectItem>
                              </SelectContent>
                            </Select>
                          </div>
                          
                          <div>
                            <Label>Channels</Label>
                            <div className="space-y-2">
                              <div className="flex items-center space-x-2">
                                <input type="checkbox" id="email" defaultChecked />
                                <Label htmlFor="email">Email</Label>
                              </div>
                              <div className="flex items-center space-x-2">
                                <input type="checkbox" id="sms" />
                                <Label htmlFor="sms">SMS</Label>
                              </div>
                              <div className="flex items-center space-x-2">
                                <input type="checkbox" id="push" />
                                <Label htmlFor="push">Push Notification</Label>
                              </div>
                              <div className="flex items-center space-x-2">
                                <input type="checkbox" id="inapp" defaultChecked />
                                <Label htmlFor="inapp">In-App</Label>
                              </div>
                            </div>
                          </div>
                        </>
                      )}

                      {selectedNode.type === 'document' && (
                        <>
                          <Separator />
                          <div>
                            <Label>Document Type</Label>
                            <Select>
                              <SelectTrigger>
                                <SelectValue placeholder="Select document type" />
                              </SelectTrigger>
                              <SelectContent>
                                <SelectItem value="contract">Contract</SelectItem>
                                <SelectItem value="invoice">Invoice</SelectItem>
                                <SelectItem value="receipt">Receipt</SelectItem>
                                <SelectItem value="report">Report</SelectItem>
                                <SelectItem value="other">Other</SelectItem>
                              </SelectContent>
                            </Select>
                          </div>
                          
                          <div>
                            <Label>Required</Label>
                            <div className="flex items-center space-x-2">
                              <input type="checkbox" id="required" defaultChecked />
                              <Label htmlFor="required">Document is required</Label>
                            </div>
                          </div>
                          
                          <div>
                            <Label>Approval Required</Label>
                            <div className="flex items-center space-x-2">
                              <input type="checkbox" id="doc-approval" />
                              <Label htmlFor="doc-approval">Requires approval</Label>
                            </div>
                          </div>
                        </>
                      )}

                      {selectedNode.type === 'integration' && (
                        <>
                          <Separator />
                          <div>
                            <Label>Target Module</Label>
                            <Select>
                              <SelectTrigger>
                                <SelectValue placeholder="Select module" />
                              </SelectTrigger>
                              <SelectContent>
                                {moduleIntegrations.map((module) => (
                                  <SelectItem key={module.id} value={module.id}>
                                    {module.name}
                                  </SelectItem>
                                ))}
                              </SelectContent>
                            </Select>
                          </div>
                          
                          <div>
                            <Label>Action</Label>
                            <Select>
                              <SelectTrigger>
                                <SelectValue placeholder="Select action" />
                              </SelectTrigger>
                              <SelectContent>
                                <SelectItem value="create">Create Record</SelectItem>
                                <SelectItem value="update">Update Record</SelectItem>
                                <SelectItem value="delete">Delete Record</SelectItem>
                                <SelectItem value="notify">Send Notification</SelectItem>
                                <SelectItem value="validate">Validate Data</SelectItem>
                              </SelectContent>
                            </Select>
                          </div>
                          
                          <div>
                            <Label>Parameters (JSON)</Label>
                            <Textarea
                              placeholder='{ "field": "value" }'
                              rows={3}
                            />
                          </div>
                        </>
                      )}
                    </div>
                  </ScrollArea>
                </div>
              </div>
            )}

            {isPropertiesOpen && selectedEdge && (
              <div className="w-80 border-l bg-gray-50">
                <div className="p-4">
                  <div className="flex items-center justify-between mb-4">
                    <h3 className="font-medium">Transition Properties</h3>
                    <div className="flex items-center gap-1">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={deleteSelectedEdge}
                        disabled={selectedWorkflowHasLiveInstances}
                        title="Delete this transition"
                        className="text-red-600 hover:text-red-700"
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => setIsPropertiesOpen(false)}
                        title="Close properties"
                      >
                        <X className="h-4 w-4" />
                      </Button>
                    </div>
                  </div>

                  <ScrollArea className="h-[calc(100vh-200px)]">
                    <div className="space-y-4">
                      <div>
                        <Label>Transition Name</Label>
                        <Input
                          value={selectedEdge.label?.toString() || ''}
                          onChange={(e) => updateSelectedEdge({ label: e.target.value })}
                          placeholder="e.g. Approved path"
                        />
                      </div>

                      <div>
                        <Label>Condition Expression</Label>
                        <Textarea
                          placeholder="e.g. totalAmount > 1000"
                          rows={4}
                          value={selectedEdge.data?.conditionExpression || ''}
                          onChange={(e) =>
                            updateSelectedEdge({ data: { conditionExpression: e.target.value } })
                          }
                        />
                        <div className="mt-2 space-y-2">
                          <div>
                            <Label className="text-xs text-muted-foreground">Variables</Label>
                            <div className="mt-1 space-y-3">
                              {groupedVariables(conditionVariableSuggestions()).map(group => (
                                <div key={group.group}>
                                  <div className="text-[11px] uppercase tracking-wide text-muted-foreground mb-1">
                                    {group.group}
                                  </div>
                                  <div className="flex flex-wrap gap-2">
                                    {group.items.map(item => (
                                      <Button
                                        key={item.value}
                                        type="button"
                                        variant="outline"
                                        size="sm"
                                        title={item.description || item.value}
                                        onClick={() =>
                                          updateSelectedEdge({
                                            data: {
                                              conditionExpression: appendExpression(
                                                selectedEdge.data?.conditionExpression || '',
                                                item.value
                                              )
                                            }
                                          })
                                        }
                                      >
                                        {item.label}
                                      </Button>
                                    ))}
                                  </div>
                                </div>
                              ))}
                              {variablesLoading && (
                                <span className="text-xs text-muted-foreground">Loading...</span>
                              )}
                            </div>
                          </div>

                          <div>
                            <Label className="text-xs text-muted-foreground">Operators</Label>
                            <div className="flex flex-wrap gap-2 mt-1">
                              {conditionOperatorSuggestions.map((item) => (
                                <Button
                                  key={item.value}
                                  type="button"
                                  variant="outline"
                                  size="sm"
                                  onClick={() =>
                                    updateSelectedEdge({
                                      data: {
                                        conditionExpression: appendExpression(
                                          selectedEdge.data?.conditionExpression || '',
                                          item.value
                                        )
                                      }
                                    })
                                  }
                                >
                                  {item.label}
                                </Button>
                              ))}
                            </div>
                          </div>
                          <p className="text-xs text-muted-foreground">
                            Variables come from the workflow data context (plus `entityId`, `initiatedById`).
                          </p>
                        </div>
                        <p className="text-xs text-muted-foreground mt-1">
                          Leave empty to make this transition the default fallback when no conditions match.
                        </p>
                      </div>
                    </div>
                  </ScrollArea>
                </div>
              </div>
            )}
          </div>
        </div>
      </DialogContent>
    </Dialog>

      <Dialog open={approverDialogOpen} onOpenChange={setApproverDialogOpen}>
        <DialogContent className="max-w-3xl max-h-[85vh] overflow-hidden">
          <DialogHeader>
            <DialogTitle>Configure Approvers</DialogTitle>
            <DialogDescription>
              Select the roles or users that can approve the selected workflow step.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-3">
              <div className="flex items-center justify-between gap-2">
                <Label>Approver Roles</Label>
                <Badge variant="secondary">{selectedApproverRoles.length} selected</Badge>
              </div>
              <Input
                placeholder="Filter roles"
                value={roleSearch}
                onChange={(e) => setRoleSearch(e.target.value)}
              />
              <ScrollArea className="h-[360px] rounded-md border bg-white">
                <div className="p-3 space-y-2">
                  {availableRoles.length === 0 && (
                    <div className="text-xs text-muted-foreground">No roles available</div>
                  )}
                  {availableRoles.length > 0 && filteredAvailableRoles.length === 0 && (
                    <div className="text-xs text-muted-foreground">No matching roles</div>
                  )}
                  {filteredAvailableRoles.map((role) => {
                    const isChecked = selectedApproverRoles.includes(role.name);
                    return (
                      <div key={role.id} className="flex items-center space-x-2">
                        <Checkbox
                          checked={isChecked}
                          disabled={selectedWorkflowHasLiveInstances}
                          onCheckedChange={(checked) => {
                            const currentRoles: string[] =
                              normalizeStringList(selectedNode?.data?.approverRoles || selectedNode?.data?.approvers);
                            const nextRoles = checked
                              ? Array.from(new Set([...currentRoles, role.name]))
                              : currentRoles.filter((r: string) => r !== role.name);
                            updateSelectedNode({ approverRoles: nextRoles, approvers: nextRoles });
                          }}
                        />
                        <span className="text-sm">{role.name}</span>
                      </div>
                    );
                  })}
                </div>
              </ScrollArea>
            </div>

            <div className="space-y-3">
              <div className="flex items-center justify-between gap-2">
                <Label>Approver Users</Label>
                <Badge variant="secondary">{selectedApproverUsers.length} selected</Badge>
              </div>
              <Input
                placeholder="Filter users"
                value={userSearch}
                onChange={(e) => setUserSearch(e.target.value)}
              />
              <ScrollArea className="h-[360px] rounded-md border bg-white">
                <div className="p-3 space-y-2">
                  {availableUsers.length === 0 && (
                    <div className="text-xs text-muted-foreground">No users available</div>
                  )}
                  {availableUsers.length > 0 && filteredAvailableUsers.length === 0 && (
                    <div className="text-xs text-muted-foreground">No matching users</div>
                  )}
                  {filteredAvailableUsers.map((user) => {
                    const isChecked = selectedApproverUsers.includes(user.id);
                    return (
                      <div key={user.id} className="flex items-center space-x-2">
                        <Checkbox
                          checked={isChecked}
                          disabled={selectedWorkflowHasLiveInstances}
                          onCheckedChange={(checked) => {
                            const currentUsers: string[] = normalizeStringList(selectedNode?.data?.approverUsers);
                            const nextUsers = checked
                              ? Array.from(new Set([...currentUsers, user.id]))
                              : currentUsers.filter((u: string) => u !== user.id);
                            updateSelectedNode({ approverUsers: nextUsers });
                          }}
                        />
                        <span className="text-sm">{formatUserLabel(user)}</span>
                      </div>
                    );
                  })}
                </div>
              </ScrollArea>
            </div>
          </div>

          <DialogFooter>
            <Button type="button" onClick={() => setApproverDialogOpen(false)}>
              Done
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={checklistDialogOpen} onOpenChange={setChecklistDialogOpen}>
        <DialogContent className="max-w-3xl max-h-[85vh] overflow-hidden">
          <DialogHeader>
            <DialogTitle>Configure Step Checklist</DialogTitle>
            <DialogDescription>
              Define the checks and evidence required before this workflow step can continue.
            </DialogDescription>
          </DialogHeader>

          <div className="flex items-center justify-between gap-3">
            <div className="flex flex-wrap gap-2">
              <Badge variant="secondary">{selectedApprovalChecklist.length} items</Badge>
              <Badge variant="secondary">{requiredApprovalChecklistCount} required</Badge>
            </div>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={addApprovalChecklistItem}
              disabled={selectedWorkflowHasLiveInstances}
            >
              <Plus className="h-3.5 w-3.5 mr-1" />
              Add Item
            </Button>
          </div>

          <ScrollArea className="h-[430px] pr-4">
            {selectedApprovalChecklist.length === 0 ? (
              <div className="rounded-md border border-dashed bg-white p-4 text-sm text-muted-foreground">
                No checklist items for this step.
              </div>
            ) : (
              <div className="space-y-3">
                {selectedApprovalChecklist.map((item, index) => {
                  const itemId = item.id || `check-${index + 1}`;
                  return (
                    <div key={itemId} className="rounded-md border bg-white p-3 space-y-3">
                      <div className="flex items-start justify-between gap-3">
                        <div className="flex-1 space-y-2">
                          <Input
                            value={item.name || ''}
                            placeholder="Checklist item"
                            disabled={selectedWorkflowHasLiveInstances}
                            onChange={(e) => updateApprovalChecklistItem(itemId, { name: e.target.value })}
                          />
                          <Textarea
                            value={item.description || ''}
                            placeholder="Optional description"
                            rows={2}
                            disabled={selectedWorkflowHasLiveInstances}
                            onChange={(e) => updateApprovalChecklistItem(itemId, { description: e.target.value })}
                          />
                        </div>
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          className="text-red-600 hover:text-red-700"
                          onClick={() => removeApprovalChecklistItem(itemId)}
                          aria-label="Remove checklist item"
                          disabled={selectedWorkflowHasLiveInstances}
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </div>
                      <div className="flex flex-wrap items-center gap-x-5 gap-y-2">
                        <label className="flex items-center gap-2 text-sm">
                          <Checkbox
                            checked={item.isRequired !== false}
                            disabled={selectedWorkflowHasLiveInstances}
                            onCheckedChange={(checked) =>
                              updateApprovalChecklistItem(itemId, { isRequired: checked === true })
                            }
                          />
                          Required
                        </label>
                        <label className="flex items-center gap-2 text-sm">
                          <Checkbox
                            checked={item.requiresDocument === true}
                            disabled={selectedWorkflowHasLiveInstances}
                            onCheckedChange={(checked) =>
                              updateApprovalChecklistItem(itemId, {
                                requiresDocument: checked === true,
                                ...(
                                  checked === true
                                    ? {}
                                    : { documentType: undefined, documentName: undefined }
                                ),
                              })
                            }
                          />
                          Requires document evidence
                        </label>
                      </div>
                      {item.requiresDocument === true && (
                        <div className="grid gap-3 rounded-md border bg-muted/20 p-3 sm:grid-cols-2">
                          <div className="space-y-1.5">
                            <Label htmlFor={`check-document-type-${itemId}`}>Document type</Label>
                            <Input
                              id={`check-document-type-${itemId}`}
                              value={item.documentType || ''}
                              placeholder="e.g. Tax clearance certificate"
                              disabled={selectedWorkflowHasLiveInstances}
                              onChange={(event) =>
                                updateApprovalChecklistItem(itemId, { documentType: event.target.value })
                              }
                            />
                          </div>
                          <div className="space-y-1.5">
                            <Label htmlFor={`check-document-name-${itemId}`}>Document name</Label>
                            <Input
                              id={`check-document-name-${itemId}`}
                              value={item.documentName || ''}
                              placeholder="e.g. Current GRA clearance"
                              disabled={selectedWorkflowHasLiveInstances}
                              onChange={(event) =>
                                updateApprovalChecklistItem(itemId, { documentName: event.target.value })
                              }
                            />
                          </div>
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            )}
          </ScrollArea>

          <DialogFooter>
            <Button type="button" onClick={() => setChecklistDialogOpen(false)}>
              Done
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
