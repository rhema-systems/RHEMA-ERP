'use client';

import React, { useState, useCallback, useEffect, useRef } from 'react';
import ReactFlow, {
  Node,
  Edge,
  addEdge,
  Background,
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

import {
  Save, X, Play, Pause, Settings, Users, Clock, AlertTriangle,
  FileText, Mail, Phone, MessageSquare, Database, Code,
  GitBranch, CheckCircle, XCircle, AlertCircle, Timer,
  User, UserCheck, Building, Zap, Bell, Upload, Download
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
  { id: 'projects', name: 'Project Management', icon: CheckCircle },
  { id: 'sales', name: 'Sales & CRM', icon: User },
  { id: 'quality', name: 'Quality Management', icon: CheckCircle },
];

export function WorkflowDesigner({ workflowId, isOpen, onClose, onSave }: WorkflowDesignerProps) {
  const [nodes, setNodes, onNodesChange] = useNodesState(initialNodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState(initialEdges);
  const [selectedNode, setSelectedNode] = useState<Node | null>(null);
  const [workflowName, setWorkflowName] = useState('');
  const [workflowDescription, setWorkflowDescription] = useState('');
  const [activeTab, setActiveTab] = useState('design');
  const [isPropertiesOpen, setIsPropertiesOpen] = useState(false);
  const [draggedType, setDraggedType] = useState<string | null>(null);
  const reactFlowWrapper = useRef<HTMLDivElement>(null);
  const [reactFlowInstance, setReactFlowInstance] = useState<any>(null);

  // Load workflow data if editing
  useEffect(() => {
    if (workflowId && isOpen) {
      loadWorkflowData(workflowId);
    }
  }, [workflowId, isOpen]);

  const loadWorkflowData = async (id: string) => {
    try {
      // API call to load workflow data
      // setNodes(loadedNodes);
      // setEdges(loadedEdges);
      // setWorkflowName(loadedName);
      // setWorkflowDescription(loadedDescription);
    } catch (error) {
      console.error('Failed to load workflow:', error);
    }
  };

  const onConnect = useCallback(
    (params: Connection) => setEdges((eds) => addEdge(params, eds)),
    [setEdges]
  );

  const onNodeClick = useCallback((event: React.MouseEvent, node: Node) => {
    setSelectedNode(node);
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
          approvalType: 'any', // any, all, sequence
          escalationTimeout: 24,
          escalationTo: '',
          conditions: []
        };
      case 'condition':
        return {
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
    const workflowData = {
      id: workflowId || undefined,
      name: workflowName,
      description: workflowDescription,
      entityType: 'WorkOrder', // This would be dynamic based on context
      nodes: nodes,
      edges: edges,
      version: 1,
      isActive: true
    };

    try {
      // API call to save workflow
      onSave(workflowData);
    } catch (error) {
      console.error('Failed to save workflow:', error);
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
    
    if (errors.length > 0) {
      alert('Validation errors: ' + errors.join(', '));
      return false;
    }
    
    alert('Workflow validation successful!');
    return true;
  };

  if (!isOpen) return null;

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-[95vw] max-h-[95vh] p-0">
        <div className="flex flex-col h-[95vh]">
          {/* Header */}
          <div className="flex items-center justify-between p-4 border-b">
            <div className="flex items-center space-x-4">
              <DialogTitle className="text-xl font-semibold">
                {workflowId ? 'Edit Workflow' : 'Create New Workflow'}
              </DialogTitle>
              <div className="flex space-x-2">
                <Input
                  placeholder="Workflow Name"
                  value={workflowName}
                  onChange={(e) => setWorkflowName(e.target.value)}
                  className="w-64"
                />
                <Button variant="outline" size="sm" onClick={handleValidate}>
                  Validate
                </Button>
                <Button variant="outline" size="sm" onClick={handleTest}>
                  <Play className="h-4 w-4 mr-2" />
                  Test
                </Button>
              </div>
            </div>
            <div className="flex items-center space-x-2">
              <Button onClick={handleSave}>
                <Save className="h-4 w-4 mr-2" />
                Save
              </Button>
              <Button variant="outline" onClick={onClose}>
                <X className="h-4 w-4" />
              </Button>
            </div>
          </div>

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
                  nodeTypes={nodeTypes}
                  fitView
                >
                  <Controls />
                  <MiniMap />
                  <Background variant="dots" gap={12} size={1} />
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
                            const updatedNode = {
                              ...selectedNode,
                              data: { ...selectedNode.data, label: e.target.value }
                            };
                            setNodes((nds) =>
                              nds.map((node) => (node.id === selectedNode.id ? updatedNode : node))
                            );
                            setSelectedNode(updatedNode);
                          }}
                        />
                      </div>

                      {/* Conditional properties based on node type */}
                      {selectedNode.type === 'approval' && (
                        <>
                          <Separator />
                          <div>
                            <Label>Approval Type</Label>
                            <Select>
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
                            <Label>Approvers</Label>
                            <Button variant="outline" size="sm">
                              <Users className="h-4 w-4 mr-2" />
                              Add Approvers
                            </Button>
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
                            />
                          </div>
                          
                          <div>
                            <Label>Operator</Label>
                            <Select>
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
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}