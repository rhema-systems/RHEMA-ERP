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
  WorkflowVariableInfo,
  WorkflowEntityTypeInfo
} from '@/types/workflow';
import { buildFallbackEntityTypes, filterEntityTypesByModule, isEntityTypeInList, moduleEntityTypeMap } from './entityTypeMapping';
import {
  WorkflowStepType,
  WorkflowApprovalType,
  WorkflowAssignmentType,
  WorkflowRejectionHandling,
  WorkflowConditionType,
  WorkflowLogicalOperator
} from '@/types/workflow';

import {
  Save, X, Play, Pause, Settings, Users, Clock, AlertTriangle,
  FileText, Mail, Phone, MessageSquare, Database, Code,
  GitBranch, CheckCircle, XCircle, AlertCircle, Timer,
  User as UserIcon, UserCheck, Building, Zap, Bell, Upload, Download,
  ChevronsUpDown, Check
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
  const [conditionVariables, setConditionVariables] = useState<WorkflowVariableInfo[]>([]);
  const [variablesLoading, setVariablesLoading] = useState(false);
  const [availableEntityTypes, setAvailableEntityTypes] = useState<WorkflowEntityTypeInfo[]>([]);
  const [entityTypesLoading, setEntityTypesLoading] = useState(false);
  const [entityTypesError, setEntityTypesError] = useState<string | null>(null);
  const [isCustomEntityType, setIsCustomEntityType] = useState(false);

  const customEntityTypeValue = '__custom__';

  const isGuid = (value: string) => /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);

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

  const onConnect = useCallback(
    (params: Connection) => setEdges((eds) => addEdge(params, eds)),
    [setEdges]
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
    event.dataTransfer.dropEffect = 'move';
  }, []);

  const onDrop = useCallback(
    (event: React.DragEvent) => {
      event.preventDefault();

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
    [reactFlowInstance, nodes.length, setNodes]
  );

  const getDefaultNodeData = (type: string) => {
    switch (type) {
      case 'task':
        return {
          assignee: '',
          dueDate: '',
          priority: 'medium',
          instructions: '',
          requiredFields: []
        };
      case 'approval':
        return {
          approvers: [],
          approverRoles: [],
          approverUsers: [],
          approvalType: 'any', // any, all, sequence
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
    event.dataTransfer.setData('application/reactflow', nodeType);
    event.dataTransfer.effectAllowed = 'move';
    setDraggedType(nodeType);
  };

  const handleSave = async () => {
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
          isActive: true,
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
          isActive: true,
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
          name: savedDefinition.name,
          description: savedDefinition.description,
          entityType: savedDefinition.entityType,
          version: savedDefinition.version,
          isActive: savedDefinition.isActive,
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
      return {
        id: stepId,
        name: stepName,
        description: node.data?.instructions || undefined,
        stepType: mapNodeTypeToStepType(node.type),
        order: index + 1,
        isRequired: true,
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
    const baseData: Record<string, any> = { label: step.name };
    if (normalizeStepType(step.stepType) === WorkflowStepType.Approval) {
      const approvalConfig = step.configuration?.approvalConfig;
      const approverRoles = approvalConfig?.approverRules
        ?.filter(rule => rule.assignmentType === WorkflowAssignmentType.Role && rule.role)
        .map(rule => rule.role ?? '')
        .filter(Boolean) ?? [];
      const approverUsers = approvalConfig?.approverRules
        ?.filter(rule => rule.assignmentType === WorkflowAssignmentType.User && rule.userId)
        .map(rule => rule.userId ?? '')
        .filter(Boolean) ?? [];
      return {
        ...baseData,
        approvers: approverRoles,
        approverRoles,
        approverUsers,
        approvalType: approvalConfig ? mapApprovalTypeToLabel(approvalConfig.approvalType) : 'any',
      };
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

  const buildStepConfiguration = (node: Node): WorkflowStepConfigurationDto | undefined => {
    if (node.type === 'approval') {
      const approverRoles = (node.data?.approverRoles || node.data?.approvers || []).filter((role: string) => role);
      const approverUsers = (node.data?.approverUsers || []).filter((userId: string) => userId);
      const approverRules: WorkflowAssignmentRuleDto[] = [];

      approverRoles.forEach((role: string, index: number) => {
        approverRules.push({
          assignmentType: WorkflowAssignmentType.Role,
          role,
          priority: approverRoles.length - index,
        });
      });

      approverUsers.forEach((userId: string, index: number) => {
        approverRules.push({
          assignmentType: WorkflowAssignmentType.User,
          userId,
          priority: approverUsers.length - index,
        });
      });

      return {
        approvalConfig: {
          approvalType: mapApprovalTypeToEnum(node.data?.approvalType),
          approverRules,
          minApprovalsRequired: 1,
          rejectionHandling: WorkflowRejectionHandling.StopWorkflow,
        },
      };
    }

    if (node.type === 'task' && node.data?.assignee) {
      return {
        assignmentRules: [{
          assignmentType: WorkflowAssignmentType.Role,
          role: node.data.assignee,
          priority: 1,
        }],
      };
    }

    return undefined;
  };

  const mapApprovalTypeToEnum = (value?: string): WorkflowApprovalType => {
    switch ((value || '').toLowerCase()) {
      case 'all':
        return WorkflowApprovalType.Consensus;
      case 'sequence':
        return WorkflowApprovalType.Multiple;
      case 'any':
      default:
        return WorkflowApprovalType.Single;
    }
  };

  const mapApprovalTypeToLabel = (value?: WorkflowApprovalType): string => {
    switch (value) {
      case WorkflowApprovalType.Consensus:
        return 'all';
      case WorkflowApprovalType.Multiple:
        return 'sequence';
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

  const handleValidate = () => {
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
      const roles = (node.data?.approverRoles || node.data?.approvers || []) as string[];
      const users = (node.data?.approverUsers || []) as string[];
      if (roles.length === 0 && users.length === 0) {
        errors.push(`Approval step "${node.data?.label || 'Approval'}" must have at least one approver role or user`);
      }
    });
    
    if (errors.length > 0) {
      toast.error(errors.length === 1 ? errors[0] : `Validation errors: ${errors.join(' • ')}`);
      return false;
    }
    
    toast.success('Workflow validation successful');
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

  const updateSelectedEdge = (updates: Record<string, any>) => {
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

  const selectedWorkflowOption =
    activeWorkflowId ? workflowOptions.find((item) => item.id === activeWorkflowId) : undefined;

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

  if (!isOpen) return null;

  return (
      <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-[95vw] max-h-[95vh] p-0">
        <div className="flex flex-col h-[95vh]">
          {/* Header */}
          <div className="p-4 border-b space-y-3">
            {/* Leave space for DialogContent's built-in top-right close button */}
            <div className="flex items-start justify-between gap-3 pr-12">
              <div>
                <DialogTitle className="text-xl font-semibold">
                  {workflowId ? 'Edit Workflow' : 'Create New Workflow'}
                </DialogTitle>
                <div className="text-xs text-muted-foreground mt-1">
                  Pick an existing workflow to load it, or start a new one.
                </div>
              </div>
              <div className="flex items-center gap-2">
                <Button variant="outline" size="sm" onClick={handleValidate}>
                  Validate
                </Button>
                <Button variant="outline" size="sm" onClick={handleTest}>
                  <Play className="h-4 w-4 mr-2" />
                  Test
                </Button>
                <Button onClick={handleSave} disabled={isSaving}>
                  <Save className="h-4 w-4 mr-2" />
                  {isSaving ? 'Saving...' : 'Save'}
                </Button>
              </div>
            </div>

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
                  className="w-full"
                />
              </div>

              <div className="md:col-span-2 flex flex-col space-y-1">
                <Label className="text-xs text-muted-foreground">Module</Label>
                <Select value={entityTypeModuleFilter} onValueChange={setEntityTypeModuleFilter}>
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
                            className={`flex items-center space-x-2 p-2 rounded cursor-pointer hover:bg-gray-100 ${item.color} text-white`}
                            draggable
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
                            className="flex items-center space-x-2 p-2 rounded cursor-pointer hover:bg-gray-100 border"
                            draggable
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
                  onNodesChange={onNodesChange}
                  onEdgesChange={onEdgesChange}
                  onConnect={onConnect}
                  onInit={setReactFlowInstance}
                  onDrop={onDrop}
                  onDragOver={onDragOver}
                  onNodeClick={onNodeClick}
                  onEdgeClick={onEdgeClick}
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
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setIsPropertiesOpen(false)}
                    >
                      <X className="h-4 w-4" />
                    </Button>
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
                                <SelectItem value="sequence">Sequential</SelectItem>
                              </SelectContent>
                            </Select>
                          </div>
                          
                          <div>
                            <Label>Escalation Timeout (hours)</Label>
                            <Input type="number" placeholder="24" />
                          </div>
                          
                          <div>
                            <Label>Approver Roles</Label>
                            <Input
                              placeholder="Filter roles"
                              value={roleSearch}
                              onChange={(e) => setRoleSearch(e.target.value)}
                              className="mb-2"
                            />
                            <div className="max-h-32 overflow-y-auto rounded border bg-white p-2 space-y-2">
                              {availableRoles.length === 0 && (
                                <div className="text-xs text-muted-foreground">No roles available</div>
                              )}
                              {availableRoles
                                .filter(role => role.name.toLowerCase().includes(roleSearch.toLowerCase()))
                                .map(role => {
                                  const selectedRoles: string[] = selectedNode.data?.approverRoles || selectedNode.data?.approvers || [];
                                  const isChecked = selectedRoles.includes(role.name);
                                  return (
                                    <div key={role.id} className="flex items-center space-x-2">
                                      <Checkbox
                                        checked={isChecked}
                                        onCheckedChange={(checked) => {
                                          const currentRoles: string[] = selectedNode.data?.approverRoles || selectedNode.data?.approvers || [];
                                          const nextRoles = checked
                                            ? Array.from(new Set([...currentRoles, role.name]))
                                            : currentRoles.filter((r: string) => r !== role.name);
                                          updateSelectedNode({ approverRoles: nextRoles, approvers: nextRoles });
                                        }}
                                      />
                                      <span className="text-xs">{role.name}</span>
                                    </div>
                                  );
                                })}
                            </div>
                          </div>

                          <div>
                            <Label>Approver Users</Label>
                            <Input
                              placeholder="Filter users"
                              value={userSearch}
                              onChange={(e) => setUserSearch(e.target.value)}
                              className="mb-2"
                            />
                            <div className="max-h-32 overflow-y-auto rounded border bg-white p-2 space-y-2">
                              {availableUsers.length === 0 && (
                                <div className="text-xs text-muted-foreground">No users available</div>
                              )}
                              {availableUsers
                                .filter(user => formatUserLabel(user).toLowerCase().includes(userSearch.toLowerCase()))
                                .map(user => {
                                  const selectedUsers: string[] = selectedNode.data?.approverUsers || [];
                                  const isChecked = selectedUsers.includes(user.id);
                                  return (
                                    <div key={user.id} className="flex items-center space-x-2">
                                      <Checkbox
                                        checked={isChecked}
                                        onCheckedChange={(checked) => {
                                          const currentUsers: string[] = selectedNode.data?.approverUsers || [];
                                          const nextUsers = checked
                                            ? Array.from(new Set([...currentUsers, user.id]))
                                            : currentUsers.filter((u: string) => u !== user.id);
                                          updateSelectedNode({ approverUsers: nextUsers });
                                        }}
                                      />
                                      <span className="text-xs">{formatUserLabel(user)}</span>
                                    </div>
                                  );
                                })}
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
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setIsPropertiesOpen(false)}
                    >
                      <X className="h-4 w-4" />
                    </Button>
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
  );
}
