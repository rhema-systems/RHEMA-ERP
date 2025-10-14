'use client';

import React, { useState, useEffect } from 'react';
import ReactFlow, {
  Node,
  Edge,
  Background,
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

import {
  Play, Pause, Clock, User, CheckCircle, AlertCircle, 
  XCircle, Timer, RefreshCw, Eye, Filter, Search
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

interface WorkflowInstance {
  id: string;
  workflowDefinitionId: string;
  workflowName: string;
  entityType: string;
  entityId: string;
  status: 'running' | 'completed' | 'failed' | 'paused' | 'cancelled';
  currentStepId: string;
  currentStepName: string;
  startedAt: string;
  completedAt?: string;
  assignedTo?: string;
  priority: 'low' | 'medium' | 'high';
  progress: number;
}

interface WorkflowInstanceMonitorProps {
  isOpen: boolean;
  onClose: () => void;
}

const mockInstances: WorkflowInstance[] = [
  {
    id: '1',
    workflowDefinitionId: 'wf-maintenance-001',
    workflowName: 'Equipment Maintenance Request',
    entityType: 'WorkOrder',
    entityId: 'WO-2024-001',
    status: 'running',
    currentStepId: 'approval-manager',
    currentStepName: 'Manager Approval',
    startedAt: '2024-01-15T10:30:00Z',
    assignedTo: 'John Smith',
    priority: 'high',
    progress: 60,
  },
  {
    id: '2',
    workflowDefinitionId: 'wf-procurement-001',
    workflowName: 'Purchase Order Approval',
    entityType: 'PurchaseOrder',
    entityId: 'PO-2024-045',
    status: 'completed',
    currentStepId: 'end',
    currentStepName: 'Completed',
    startedAt: '2024-01-14T08:00:00Z',
    completedAt: '2024-01-15T12:00:00Z',
    assignedTo: 'Alice Johnson',
    priority: 'medium',
    progress: 100,
  },
  {
    id: '3',
    workflowDefinitionId: 'wf-hr-001',
    workflowName: 'Employee Onboarding',
    entityType: 'Employee',
    entityId: 'EMP-2024-012',
    status: 'paused',
    currentStepId: 'document-collection',
    currentStepName: 'Document Collection',
    startedAt: '2024-01-12T09:00:00Z',
    assignedTo: 'HR Department',
    priority: 'low',
    progress: 30,
  },
];

export function WorkflowInstanceMonitor({ isOpen, onClose }: WorkflowInstanceMonitorProps) {
  const [instances, setInstances] = useState<WorkflowInstance[]>(mockInstances);
  const [selectedInstance, setSelectedInstance] = useState<WorkflowInstance | null>(null);
  const [nodes, setNodes, onNodesChange] = useNodesState([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState([]);
  const [filterStatus, setFilterStatus] = useState<string>('all');
  const [searchTerm, setSearchTerm] = useState('');
  const [activeTab, setActiveTab] = useState('list');

  useEffect(() => {
    if (selectedInstance) {
      loadWorkflowVisualization(selectedInstance);
    }
  }, [selectedInstance]);

  const loadWorkflowVisualization = async (instance: WorkflowInstance) => {
    // Mock workflow visualization data - in real app, this would come from API
    const mockNodes: Node[] = [
      {
        id: 'start',
        type: 'start',
        position: { x: 250, y: 50 },
        data: { label: 'Start' },
      },
      {
        id: 'task-1',
        type: 'task',
        position: { x: 250, y: 150 },
        data: { 
          label: 'Create Request',
          assignee: 'System',
          priority: 'medium',
          status: 'completed'
        },
        className: 'completed-node',
      },
      {
        id: 'approval-manager',
        type: 'approval',
        position: { x: 250, y: 250 },
        data: { 
          label: 'Manager Approval',
          approvers: ['John Smith'],
          approvalType: 'any',
          status: instance.currentStepId === 'approval-manager' ? 'active' : 'pending'
        },
        className: instance.currentStepId === 'approval-manager' ? 'active-node' : 'pending-node',
      },
      {
        id: 'condition-1',
        type: 'condition',
        position: { x: 250, y: 350 },
        data: { 
          label: 'Approved?',
          operator: 'AND',
          status: 'pending'
        },
        className: 'pending-node',
      },
      {
        id: 'task-2',
        type: 'task',
        position: { x: 100, y: 450 },
        data: { 
          label: 'Schedule Work',
          assignee: 'Maintenance Team',
          priority: 'high',
          status: 'pending'
        },
        className: 'pending-node',
      },
      {
        id: 'task-3',
        type: 'task',
        position: { x: 400, y: 450 },
        data: { 
          label: 'Send Rejection',
          assignee: 'System',
          priority: 'low',
          status: 'pending'
        },
        className: 'pending-node',
      },
      {
        id: 'end',
        type: 'end',
        position: { x: 250, y: 550 },
        data: { label: 'End' },
        className: 'pending-node',
      },
    ];

    const mockEdges: Edge[] = [
      { id: 'e1', source: 'start', target: 'task-1' },
      { id: 'e2', source: 'task-1', target: 'approval-manager' },
      { id: 'e3', source: 'approval-manager', target: 'condition-1' },
      { id: 'e4', source: 'condition-1', target: 'task-2', sourceHandle: 'true', label: 'Approved' },
      { id: 'e5', source: 'condition-1', target: 'task-3', sourceHandle: 'false', label: 'Rejected' },
      { id: 'e6', source: 'task-2', target: 'end' },
      { id: 'e7', source: 'task-3', target: 'end' },
    ];

    setNodes(mockNodes);
    setEdges(mockEdges);
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

  const filteredInstances = instances.filter(instance => {
    const matchesStatus = filterStatus === 'all' || instance.status === filterStatus;
    const matchesSearch = searchTerm === '' || 
      instance.workflowName.toLowerCase().includes(searchTerm.toLowerCase()) ||
      instance.entityId.toLowerCase().includes(searchTerm.toLowerCase()) ||
      instance.currentStepName.toLowerCase().includes(searchTerm.toLowerCase());
    return matchesStatus && matchesSearch;
  });

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 bg-black bg-opacity-50 flex items-center justify-center">
      <div className="bg-white rounded-lg shadow-lg w-[95vw] h-[95vh] flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between p-4 border-b">
          <h2 className="text-xl font-semibold">Workflow Instance Monitor</h2>
          <div className="flex items-center space-x-2">
            <Button variant="outline" size="sm">
              <RefreshCw className="h-4 w-4 mr-2" />
              Refresh
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
                {filteredInstances.map((instance) => (
                  <Card 
                    key={instance.id}
                    className={`cursor-pointer transition-colors ${
                      selectedInstance?.id === instance.id ? 'bg-blue-50 border-blue-200' : 'hover:bg-gray-50'
                    }`}
                    onClick={() => setSelectedInstance(instance)}
                  >
                    <CardContent className="p-3">
                      <div className="flex items-start justify-between mb-2">
                        <div className="flex-1">
                          <h4 className="font-medium text-sm">{instance.workflowName}</h4>
                          <p className="text-xs text-gray-600">{instance.entityId}</p>
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
                ))}
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
                      {getStatusIcon(selectedInstance.status)}
                      <Badge className={`${getStatusColor(selectedInstance.status)}`}>
                        {selectedInstance.status.toUpperCase()}
                      </Badge>
                    </div>
                  </div>
                  
                  <div className="grid grid-cols-4 gap-4 text-sm">
                    <div>
                      <span className="text-gray-600">Entity ID:</span>
                      <p className="font-medium">{selectedInstance.entityId}</p>
                    </div>
                    <div>
                      <span className="text-gray-600">Current Step:</span>
                      <p className="font-medium">{selectedInstance.currentStepName}</p>
                    </div>
                    <div>
                      <span className="text-gray-600">Assigned To:</span>
                      <p className="font-medium">{selectedInstance.assignedTo}</p>
                    </div>
                    <div>
                      <span className="text-gray-600">Started:</span>
                      <p className="font-medium">{new Date(selectedInstance.startedAt).toLocaleString()}</p>
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
                          <Background variant="dots" gap={12} size={1} />
                        </ReactFlow>
                      </ReactFlowProvider>
                    </div>
                  </TabsContent>
                  
                  <TabsContent value="history" className="flex-1 p-4">
                    <div className="space-y-3">
                      <div className="flex items-center p-3 bg-green-50 border border-green-200 rounded">
                        <CheckCircle className="h-5 w-5 text-green-600 mr-3" />
                        <div className="flex-1">
                          <h4 className="font-medium">Create Request</h4>
                          <p className="text-sm text-gray-600">Completed by System</p>
                        </div>
                        <div className="text-sm text-gray-500">
                          Jan 15, 10:30 AM
                        </div>
                      </div>
                      
                      <div className="flex items-center p-3 bg-blue-50 border border-blue-200 rounded">
                        <Clock className="h-5 w-5 text-blue-600 mr-3" />
                        <div className="flex-1">
                          <h4 className="font-medium">Manager Approval</h4>
                          <p className="text-sm text-gray-600">Waiting for John Smith</p>
                        </div>
                        <div className="text-sm text-gray-500">
                          In Progress
                        </div>
                      </div>
                      
                      <div className="flex items-center p-3 bg-gray-50 border border-gray-200 rounded opacity-60">
                        <Timer className="h-5 w-5 text-gray-400 mr-3" />
                        <div className="flex-1">
                          <h4 className="font-medium">Schedule Work</h4>
                          <p className="text-sm text-gray-600">Pending</p>
                        </div>
                        <div className="text-sm text-gray-500">
                          Pending
                        </div>
                      </div>
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
                            <span>{new Date(selectedInstance.startedAt).toLocaleString()}</span>
                          </div>
                          {selectedInstance.completedAt && (
                            <div className="flex justify-between">
                              <span className="text-gray-600">Completed At:</span>
                              <span>{new Date(selectedInstance.completedAt).toLocaleString()}</span>
                            </div>
                          )}
                          <div className="flex justify-between">
                            <span className="text-gray-600">Duration:</span>
                            <span>
                              {selectedInstance.completedAt 
                                ? `${Math.floor((new Date(selectedInstance.completedAt).getTime() - new Date(selectedInstance.startedAt).getTime()) / (1000 * 60 * 60))}h`
                                : `${Math.floor((new Date().getTime() - new Date(selectedInstance.startedAt).getTime()) / (1000 * 60 * 60))}h (ongoing)`
                              }
                            </span>
                          </div>
                        </div>
                      </div>
                    </div>
                  </TabsContent>
                </Tabs>
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