'use client';

import React, { useState } from 'react';
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

import {
  ArrowRight, ArrowLeft, Check, Plus, X, Settings, Database, Users, 
  Building, FileText, CheckCircle, User, Play, GitBranch, Bell,
  Upload, AlertTriangle, Zap, ChevronDown, ChevronRight, ListChecks
} from 'lucide-react';

interface WorkflowCreationWizardProps {
  isOpen: boolean;
  onClose: () => void;
  onComplete: (workflow: any) => void;
}

interface WorkflowStep {
  id: string;
  type: 'task' | 'approval' | 'condition' | 'notification' | 'document' | 'escalation' | 'integration';
  name: string;
  description: string;
  assigneeType: 'user' | 'role' | 'department';
  assignee: string;
  conditions?: string[];
  checklist?: ChecklistItem[];
  isRequired: boolean;
  timeoutHours?: number;
}

interface ChecklistItem {
  id: string;
  text: string;
  isRequired: boolean;
  isCompleted?: boolean;
}

const moduleOptions = [
  { id: 'maintenance', name: 'Maintenance Management', icon: Settings, description: 'Equipment maintenance and work orders' },
  { id: 'inventory', name: 'Inventory Management', icon: Database, description: 'Stock control and warehouse operations' },
  { id: 'hr', name: 'Human Resources', icon: Users, description: 'Employee management and HR processes' },
  { id: 'finance', name: 'Finance & Accounting', icon: Building, description: 'Financial transactions and accounting' },
  { id: 'procurement', name: 'Procurement', icon: FileText, description: 'Purchase orders and supplier management' },
  { id: 'projects', name: 'Project Management', icon: CheckCircle, description: 'Project planning and execution' },
  { id: 'sales', name: 'Sales & CRM', icon: User, description: 'Customer relationship management' },
  { id: 'quality', name: 'Quality Management', icon: CheckCircle, description: 'Quality control and assurance' },
];

const stepTypes = [
  { id: 'task', name: 'Task', icon: FileText, description: 'Assign a task to a user or role' },
  { id: 'approval', name: 'Approval', icon: CheckCircle, description: 'Require approval from specific users' },
  { id: 'condition', name: 'Condition', icon: GitBranch, description: 'Branch workflow based on conditions' },
  { id: 'notification', name: 'Notification', icon: Bell, description: 'Send notifications to users' },
  { id: 'document', name: 'Document', icon: Upload, description: 'Require document upload or review' },
  { id: 'escalation', name: 'Escalation', icon: AlertTriangle, description: 'Escalate to higher authority' },
  { id: 'integration', name: 'Integration', icon: Zap, description: 'Integrate with external systems' },
];

const assigneeTypes = [
  { id: 'user', name: 'Specific User', description: 'Assign to a specific person' },
  { id: 'role', name: 'Role', description: 'Assign to anyone with a specific role' },
  { id: 'department', name: 'Department', description: 'Assign to a department' },
];

export function WorkflowCreationWizard({ isOpen, onClose, onComplete }: WorkflowCreationWizardProps) {
  const [currentStep, setCurrentStep] = useState(1);
  const [workflowName, setWorkflowName] = useState('');
  const [workflowDescription, setWorkflowDescription] = useState('');
  const [selectedModule, setSelectedModule] = useState('');
  const [entityType, setEntityType] = useState('');
  const [workflowSteps, setWorkflowSteps] = useState<WorkflowStep[]>([]);
  const [editingStep, setEditingStep] = useState<WorkflowStep | null>(null);
  const [isStepModalOpen, setIsStepModalOpen] = useState(false);

  // Step editing state
  const [stepName, setStepName] = useState('');
  const [stepDescription, setStepDescription] = useState('');
  const [stepType, setStepType] = useState('');
  const [assigneeType, setAssigneeType] = useState('');
  const [assignee, setAssignee] = useState('');
  const [isRequired, setIsRequired] = useState(true);
  const [timeoutHours, setTimeoutHours] = useState(24);
  const [conditions, setConditions] = useState<string[]>([]);
  const [checklist, setChecklist] = useState<ChecklistItem[]>([]);

  const resetStepForm = () => {
    setStepName('');
    setStepDescription('');
    setStepType('');
    setAssigneeType('');
    setAssignee('');
    setIsRequired(true);
    setTimeoutHours(24);
    setConditions([]);
    setChecklist([]);
  };

  const handleAddStep = () => {
    resetStepForm();
    setEditingStep(null);
    setIsStepModalOpen(true);
  };

  const handleEditStep = (step: WorkflowStep) => {
    setStepName(step.name);
    setStepDescription(step.description);
    setStepType(step.type);
    setAssigneeType(step.assigneeType);
    setAssignee(step.assignee);
    setIsRequired(step.isRequired);
    setTimeoutHours(step.timeoutHours || 24);
    setConditions(step.conditions || []);
    setChecklist(step.checklist || []);
    setEditingStep(step);
    setIsStepModalOpen(true);
  };

  const handleSaveStep = () => {
    const step: WorkflowStep = {
      id: editingStep?.id || `step-${Date.now()}`,
      type: stepType as any,
      name: stepName,
      description: stepDescription,
      assigneeType: assigneeType as any,
      assignee,
      isRequired,
      timeoutHours,
      conditions: conditions.length > 0 ? conditions : undefined,
      checklist: checklist.length > 0 ? checklist : undefined,
    };

    if (editingStep) {
      setWorkflowSteps(prev => prev.map(s => s.id === editingStep.id ? step : s));
    } else {
      setWorkflowSteps(prev => [...prev, step]);
    }

    setIsStepModalOpen(false);
    resetStepForm();
  };

  const handleDeleteStep = (stepId: string) => {
    setWorkflowSteps(prev => prev.filter(s => s.id !== stepId));
  };

  const addCondition = () => {
    setConditions(prev => [...prev, '']);
  };

  const updateCondition = (index: number, value: string) => {
    setConditions(prev => prev.map((c, i) => i === index ? value : c));
  };

  const removeCondition = (index: number) => {
    setConditions(prev => prev.filter((_, i) => i !== index));
  };

  const addChecklistItem = () => {
    const newItem: ChecklistItem = {
      id: `checklist-${Date.now()}`,
      text: '',
      isRequired: false,
    };
    setChecklist(prev => [...prev, newItem]);
  };

  const updateChecklistItem = (index: number, field: keyof ChecklistItem, value: string | boolean) => {
    setChecklist(prev => prev.map((item, i) => i === index ? { ...item, [field]: value } : item));
  };

  const removeChecklistItem = (index: number) => {
    setChecklist(prev => prev.filter((_, i) => i !== index));
  };

  const handleComplete = () => {
    const workflow = {
      name: workflowName,
      description: workflowDescription,
      module: selectedModule,
      entityType,
      steps: workflowSteps,
      isActive: true,
      version: 1,
    };
    
    onComplete(workflow);
    onClose();
  };

  const getStepIcon = (type: string) => {
    const stepType = stepTypes.find(st => st.id === type);
    return stepType ? stepType.icon : FileText;
  };

  const canProceed = (step: number) => {
    switch (step) {
      case 1: return workflowName && workflowDescription && selectedModule && entityType;
      case 2: return workflowSteps.length > 0;
      case 3: return true;
      default: return false;
    }
  };

  if (!isOpen) return null;

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-4xl max-h-[90vh] p-0">
        <div className="flex flex-col h-[90vh]">
          {/* Header */}
          <DialogHeader className="p-6 border-b">
            <div className="flex items-center justify-between">
              <div>
                <DialogTitle className="text-xl">Create New Workflow</DialogTitle>
                <DialogDescription>Step {currentStep} of 3</DialogDescription>
              </div>
              <div className="flex items-center space-x-2">
                <div className="flex items-center space-x-1">
                  {[1, 2, 3].map((step) => (
                    <div key={step} className="flex items-center">
                      <div className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium ${
                        step === currentStep ? 'bg-blue-500 text-white' :
                        step < currentStep ? 'bg-green-500 text-white' : 'bg-gray-200 text-gray-600'
                      }`}>
                        {step < currentStep ? <Check className="h-4 w-4" /> : step}
                      </div>
                      {step < 3 && <ArrowRight className="h-4 w-4 mx-2 text-gray-400" />}
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </DialogHeader>

          {/* Content */}
          <div className="flex-1 overflow-hidden">
            {currentStep === 1 && (
              <div className="p-6 space-y-6">
                <div>
                  <h3 className="text-lg font-semibold mb-4">Basic Information</h3>
                  
                  <div className="space-y-4">
                    <div>
                      <Label htmlFor="workflowName">Workflow Name</Label>
                      <Input
                        id="workflowName"
                        placeholder="Enter workflow name"
                        value={workflowName}
                        onChange={(e) => setWorkflowName(e.target.value)}
                      />
                    </div>
                    
                    <div>
                      <Label htmlFor="workflowDescription">Description</Label>
                      <Textarea
                        id="workflowDescription"
                        placeholder="Describe what this workflow does"
                        value={workflowDescription}
                        onChange={(e) => setWorkflowDescription(e.target.value)}
                        rows={3}
                      />
                    </div>
                    
                    <div>
                      <Label htmlFor="entityType">Entity Type</Label>
                      <Select value={entityType} onValueChange={setEntityType}>
                        <SelectTrigger>
                          <SelectValue placeholder="Select entity type" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="WorkOrder">Work Order</SelectItem>
                          <SelectItem value="PurchaseOrder">Purchase Order</SelectItem>
                          <SelectItem value="Employee">Employee</SelectItem>
                          <SelectItem value="Asset">Asset</SelectItem>
                          <SelectItem value="Inventory">Inventory</SelectItem>
                          <SelectItem value="Project">Project</SelectItem>
                          <SelectItem value="Customer">Customer</SelectItem>
                          <SelectItem value="Vendor">Vendor</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>
                  </div>
                </div>

                <Separator />

                <div>
                  <h3 className="text-lg font-semibold mb-4">Select Module</h3>
                  <div className="grid grid-cols-2 gap-4">
                    {moduleOptions.map((module) => {
                      const IconComponent = module.icon;
                      return (
                        <Card
                          key={module.id}
                          className={`cursor-pointer transition-colors ${
                            selectedModule === module.id ? 'bg-blue-50 border-blue-300' : 'hover:bg-gray-50'
                          }`}
                          onClick={() => setSelectedModule(module.id)}
                        >
                          <CardContent className="p-4">
                            <div className="flex items-start space-x-3">
                              <IconComponent className="h-6 w-6 text-blue-600 mt-1" />
                              <div className="flex-1">
                                <h4 className="font-medium">{module.name}</h4>
                                <p className="text-sm text-gray-600 mt-1">{module.description}</p>
                              </div>
                              {selectedModule === module.id && (
                                <Check className="h-5 w-5 text-blue-600" />
                              )}
                            </div>
                          </CardContent>
                        </Card>
                      );
                    })}
                  </div>
                </div>
              </div>
            )}

            {currentStep === 2 && (
              <div className="p-6 space-y-6">
                <div className="flex items-center justify-between">
                  <h3 className="text-lg font-semibold">Workflow Steps</h3>
                  <Button onClick={handleAddStep}>
                    <Plus className="h-4 w-4 mr-2" />
                    Add Step
                  </Button>
                </div>

                <ScrollArea className="h-96">
                  {workflowSteps.length === 0 ? (
                    <div className="text-center py-12 text-gray-500">
                      <FileText className="h-12 w-12 mx-auto mb-4 text-gray-400" />
                      <p>No steps added yet</p>
                      <p className="text-sm">Click "Add Step" to create your workflow</p>
                    </div>
                  ) : (
                    <div className="space-y-4">
                      {workflowSteps.map((step, index) => {
                        const IconComponent = getStepIcon(step.type);
                        return (
                          <Card key={step.id}>
                            <CardContent className="p-4">
                              <div className="flex items-start justify-between">
                                <div className="flex items-start space-x-3 flex-1">
                                  <div className="bg-blue-100 p-2 rounded">
                                    <IconComponent className="h-4 w-4 text-blue-600" />
                                  </div>
                                  <div className="flex-1">
                                    <div className="flex items-center space-x-2 mb-1">
                                      <h4 className="font-medium">{step.name}</h4>
                                      <Badge variant="outline" className="text-xs">
                                        {step.type}
                                      </Badge>
                                      {step.isRequired && (
                                        <Badge variant="secondary" className="text-xs">Required</Badge>
                                      )}
                                    </div>
                                    <p className="text-sm text-gray-600 mb-2">{step.description}</p>
                                    <div className="flex items-center space-x-4 text-xs text-gray-500">
                                      <span>Assignee: {step.assignee}</span>
                                      {step.timeoutHours && (
                                        <span>Timeout: {step.timeoutHours}h</span>
                                      )}
                                      {step.checklist && step.checklist.length > 0 && (
                                        <span>Checklist: {step.checklist.length} items</span>
                                      )}
                                    </div>
                                  </div>
                                </div>
                                <div className="flex items-center space-x-2">
                                  <span className="text-sm text-gray-400">#{index + 1}</span>
                                  <Button
                                    variant="outline"
                                    size="sm"
                                    onClick={() => handleEditStep(step)}
                                  >
                                    Edit
                                  </Button>
                                  <Button
                                    variant="outline"
                                    size="sm"
                                    onClick={() => handleDeleteStep(step.id)}
                                  >
                                    <X className="h-4 w-4" />
                                  </Button>
                                </div>
                              </div>
                            </CardContent>
                          </Card>
                        );
                      })}
                    </div>
                  )}
                </ScrollArea>
              </div>
            )}

            {currentStep === 3 && (
              <div className="p-6 space-y-6">
                <h3 className="text-lg font-semibold">Review & Confirm</h3>
                
                <div className="grid grid-cols-2 gap-6">
                  <div>
                    <h4 className="font-medium mb-3">Workflow Information</h4>
                    <div className="space-y-2 text-sm">
                      <div className="flex justify-between">
                        <span className="text-gray-600">Name:</span>
                        <span>{workflowName}</span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-gray-600">Module:</span>
                        <span>{moduleOptions.find(m => m.id === selectedModule)?.name}</span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-gray-600">Entity Type:</span>
                        <span>{entityType}</span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-gray-600">Total Steps:</span>
                        <span>{workflowSteps.length}</span>
                      </div>
                    </div>
                  </div>
                  
                  <div>
                    <h4 className="font-medium mb-3">Description</h4>
                    <p className="text-sm text-gray-600">{workflowDescription}</p>
                  </div>
                </div>

                <Separator />

                <div>
                  <h4 className="font-medium mb-3">Workflow Steps</h4>
                  <div className="space-y-3">
                    {workflowSteps.map((step, index) => {
                      const IconComponent = getStepIcon(step.type);
                      return (
                        <div key={step.id} className="flex items-center space-x-3 p-3 bg-gray-50 rounded">
                          <div className="bg-blue-100 p-1.5 rounded">
                            <IconComponent className="h-4 w-4 text-blue-600" />
                          </div>
                          <div className="flex-1">
                            <div className="flex items-center space-x-2">
                              <span className="font-medium">{index + 1}. {step.name}</span>
                              <Badge variant="outline" className="text-xs">
                                {step.type}
                              </Badge>
                            </div>
                            <p className="text-sm text-gray-600">{step.assignee}</p>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              </div>
            )}
          </div>

          {/* Footer */}
          <div className="flex items-center justify-between p-6 border-t">
            <div>
              {currentStep > 1 && (
                <Button 
                  variant="outline" 
                  onClick={() => setCurrentStep(prev => prev - 1)}
                >
                  <ArrowLeft className="h-4 w-4 mr-2" />
                  Back
                </Button>
              )}
            </div>
            <div className="flex items-center space-x-3">
              <Button variant="outline" onClick={onClose}>
                Cancel
              </Button>
              {currentStep < 3 ? (
                <Button 
                  onClick={() => setCurrentStep(prev => prev + 1)}
                  disabled={!canProceed(currentStep)}
                >
                  Next
                  <ArrowRight className="h-4 w-4 ml-2" />
                </Button>
              ) : (
                <Button onClick={handleComplete}>
                  <Check className="h-4 w-4 mr-2" />
                  Create Workflow
                </Button>
              )}
            </div>
          </div>
        </div>

        {/* Step Modal */}
        <Dialog open={isStepModalOpen} onOpenChange={setIsStepModalOpen}>
          <DialogContent className="max-w-2xl max-h-[80vh]">
            <DialogHeader>
              <DialogTitle>
                {editingStep ? 'Edit Step' : 'Add New Step'}
              </DialogTitle>
            </DialogHeader>
            
            <ScrollArea className="max-h-96">
              <div className="space-y-4 p-1">
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <Label htmlFor="stepName">Step Name</Label>
                    <Input
                      id="stepName"
                      value={stepName}
                      onChange={(e) => setStepName(e.target.value)}
                      placeholder="Enter step name"
                    />
                  </div>
                  <div>
                    <Label htmlFor="stepType">Step Type</Label>
                    <Select value={stepType} onValueChange={setStepType}>
                      <SelectTrigger>
                        <SelectValue placeholder="Select step type" />
                      </SelectTrigger>
                      <SelectContent>
                        {stepTypes.map((type) => (
                          <SelectItem key={type.id} value={type.id}>
                            {type.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                </div>

                <div>
                  <Label htmlFor="stepDescription">Description</Label>
                  <Textarea
                    id="stepDescription"
                    value={stepDescription}
                    onChange={(e) => setStepDescription(e.target.value)}
                    placeholder="Describe this step"
                    rows={2}
                  />
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <Label htmlFor="assigneeType">Assignee Type</Label>
                    <Select value={assigneeType} onValueChange={setAssigneeType}>
                      <SelectTrigger>
                        <SelectValue placeholder="Select assignee type" />
                      </SelectTrigger>
                      <SelectContent>
                        {assigneeTypes.map((type) => (
                          <SelectItem key={type.id} value={type.id}>
                            {type.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div>
                    <Label htmlFor="assignee">Assignee</Label>
                    <Input
                      id="assignee"
                      value={assignee}
                      onChange={(e) => setAssignee(e.target.value)}
                      placeholder="Enter assignee name/role"
                    />
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div className="flex items-center space-x-2">
                    <Checkbox
                      id="isRequired"
                      checked={isRequired}
                      onCheckedChange={(checked) => setIsRequired(checked as boolean)}
                    />
                    <Label htmlFor="isRequired">Required Step</Label>
                  </div>
                  <div>
                    <Label htmlFor="timeoutHours">Timeout (hours)</Label>
                    <Input
                      id="timeoutHours"
                      type="number"
                      value={timeoutHours}
                      onChange={(e) => setTimeoutHours(parseInt(e.target.value) || 24)}
                      min={1}
                    />
                  </div>
                </div>

                {(stepType === 'condition' || stepType === 'approval') && (
                  <>
                    <Separator />
                    <div>
                      <div className="flex items-center justify-between mb-2">
                        <Label>Conditions</Label>
                        <Button variant="outline" size="sm" onClick={addCondition}>
                          <Plus className="h-4 w-4 mr-1" />
                          Add Condition
                        </Button>
                      </div>
                      <div className="space-y-2">
                        {conditions.map((condition, index) => (
                          <div key={index} className="flex items-center space-x-2">
                            <Input
                              value={condition}
                              onChange={(e) => updateCondition(index, e.target.value)}
                              placeholder="Enter condition"
                            />
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={() => removeCondition(index)}
                            >
                              <X className="h-4 w-4" />
                            </Button>
                          </div>
                        ))}
                      </div>
                    </div>
                  </>
                )}

                <Separator />
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <Label>Checklist Items</Label>
                    <Button variant="outline" size="sm" onClick={addChecklistItem}>
                      <ListChecks className="h-4 w-4 mr-1" />
                      Add Item
                    </Button>
                  </div>
                  <div className="space-y-2">
                    {checklist.map((item, index) => (
                      <div key={index} className="flex items-center space-x-2">
                        <Input
                          value={item.text}
                          onChange={(e) => updateChecklistItem(index, 'text', e.target.value)}
                          placeholder="Checklist item"
                        />
                        <div className="flex items-center space-x-1">
                          <Checkbox
                            checked={item.isRequired}
                            onCheckedChange={(checked) => updateChecklistItem(index, 'isRequired', checked as boolean)}
                          />
                          <Label className="text-xs">Required</Label>
                        </div>
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => removeChecklistItem(index)}
                        >
                          <X className="h-4 w-4" />
                        </Button>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            </ScrollArea>

            <DialogFooter>
              <Button variant="outline" onClick={() => setIsStepModalOpen(false)}>
                Cancel
              </Button>
              <Button
                onClick={handleSaveStep}
                disabled={!stepName || !stepType || !assigneeType || !assignee}
              >
                {editingStep ? 'Update Step' : 'Add Step'}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </DialogContent>
    </Dialog>
  );
}