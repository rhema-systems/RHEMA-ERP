'use client';

import React, { useEffect, useState } from 'react';
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
import { workflowApiService } from '@/services/workflow-api.service';
import type {
  CreateWorkflowDefinitionAdminDto,
  CreateWorkflowStepDto,
  CreateWorkflowTransitionDto,
  WorkflowEntityTypeInfo
} from '@/types/workflow';
import { WorkflowStepType } from '@/types/workflow';
import { buildFallbackEntityTypes, filterEntityTypesByModule, isEntityTypeInList } from './entityTypeMapping';
import { toast } from 'sonner';

import {
  ArrowRight, ArrowLeft, Check, Plus, X, Settings, Database, Users, 
  Building, FileText, CheckCircle, User, Play, GitBranch, Bell, Landmark,
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
  requiresDocument?: boolean;
  documentName?: string;
  documentRequirementKey?: string;
  documentRequirements?: DocumentRequirement[];
}

interface ChecklistItem {
  id: string;
  text: string;
  isRequired: boolean;
  isCompleted?: boolean;
}

interface DocumentRequirement {
  id: string;
  documentName: string;
  documentType?: string;
  requirementKey?: string;
  isRequired: boolean;
}

const moduleOptions = [
  { id: 'maintenance', name: 'Maintenance Management', icon: Settings, description: 'Equipment maintenance and work orders' },
  { id: 'inventory', name: 'Inventory Management', icon: Database, description: 'Stock control and warehouse operations' },
  { id: 'hr', name: 'Human Resources', icon: Users, description: 'Employee management and HR processes' },
  { id: 'finance', name: 'Finance & Accounting', icon: Building, description: 'Financial transactions and accounting' },
  { id: 'procurement', name: 'Procurement', icon: FileText, description: 'Purchase orders and supplier management' },
  { id: 'projects', name: 'Project Management', icon: CheckCircle, description: 'Project planning and execution' },
  { id: 'planning', name: 'Development Planning', icon: FileText, description: 'Town planning SOPs, land use reviews, and site plan workflows' },
  { id: 'estate', name: 'Estate Management', icon: Landmark, description: 'Properties, facilities, leases, and land acquisition' },
  { id: 'legal', name: 'Legal', icon: FileText, description: 'Legal procedures, instruments, court processes, and approvals' },
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
  const [availableEntityTypes, setAvailableEntityTypes] = useState<WorkflowEntityTypeInfo[]>([]);
  const [entityTypesLoading, setEntityTypesLoading] = useState(false);
  const [entityTypesError, setEntityTypesError] = useState<string | null>(null);
  const [isCustomEntityType, setIsCustomEntityType] = useState(false);
  const [workflowSteps, setWorkflowSteps] = useState<WorkflowStep[]>([]);
  const [editingStep, setEditingStep] = useState<WorkflowStep | null>(null);
  const [isStepModalOpen, setIsStepModalOpen] = useState(false);
  const [isCreating, setIsCreating] = useState(false);

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
  const [requiresDocument, setRequiresDocument] = useState(false);
  const [documentRequirements, setDocumentRequirements] = useState<DocumentRequirement[]>([]);

  const customEntityTypeValue = '__custom__';
  const { items: entityTypeOptions, fallback: entityTypeFallback } = filterEntityTypesByModule(
    availableEntityTypes,
    selectedModule
  );
  const entityTypeNotice = entityTypesError
    ? entityTypesError
    : entityTypeFallback
      ? 'No matching entity types for the selected module. Showing all.'
      : null;

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
      selectedModule
    );

    if (!isEntityTypeInList(entityType, filteredItems))
    {
      setEntityType('');
    }
  }, [selectedModule, availableEntityTypes, entityType, isCustomEntityType, isOpen]);

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
    setRequiresDocument(false);
    setDocumentRequirements([]);
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
    setRequiresDocument(step.requiresDocument || step.type === 'document');
    setDocumentRequirements(
      step.documentRequirements && step.documentRequirements.length > 0
        ? step.documentRequirements
        : step.documentName
          ? [{
              id: step.documentRequirementKey || crypto.randomUUID(),
              documentName: step.documentName,
              requirementKey: step.documentRequirementKey,
              isRequired: true,
            }]
          : []
    );
    setEditingStep(step);
    setIsStepModalOpen(true);
  };

  const handleSaveStep = () => {
    const step: WorkflowStep = {
      id: editingStep?.id || crypto.randomUUID(),
      type: stepType as any,
      name: stepName,
      description: stepDescription,
      assigneeType: assigneeType as any,
      assignee,
      isRequired,
      timeoutHours,
      conditions: conditions.length > 0 ? conditions : undefined,
      checklist: checklist.length > 0 ? checklist : undefined,
      requiresDocument: requiresDocument || stepType === 'document',
      documentRequirements: documentRequirements
        .filter(item => item.documentName.trim())
        .map((item, index) => ({
          ...item,
          id: item.id || `document-${index + 1}`,
          documentName: item.documentName.trim(),
          documentType: item.documentType?.trim() || undefined,
          requirementKey: item.requirementKey?.trim() || buildRequirementKey(item.documentName, index),
          isRequired: item.isRequired !== false,
        })),
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

  const buildRequirementKey = (value: string, index: number) => {
    const normalized = (value || `document-${index + 1}`)
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/(^-|-$)/g, '');

    return normalized || `document-${index + 1}`;
  };

  const addDocumentRequirement = () => {
    setDocumentRequirements(prev => [
      ...prev,
      {
        id: crypto.randomUUID(),
        documentName: '',
        documentType: '',
        requirementKey: '',
        isRequired: true,
      },
    ]);
  };

  const updateDocumentRequirement = (
    index: number,
    field: keyof DocumentRequirement,
    value: string | boolean
  ) => {
    setDocumentRequirements(prev => prev.map((item, i) => i === index ? { ...item, [field]: value } : item));
  };

  const removeDocumentRequirement = (index: number) => {
    setDocumentRequirements(prev => prev.filter((_, i) => i !== index));
  };

  const handleComplete = () => {
    void (async () => {
      try {
        setIsCreating(true);

        // Create a real workflow definition record so the Designer can load/save against a real ID.
        // Keep it inactive until the workflow is fully designed (approvers, conditions, transitions, etc.).
        const steps: CreateWorkflowStepDto[] = workflowSteps.map((s, idx) => {
          const qualityChecks = (s.checklist || [])
            .filter(item => item.text.trim())
            .map((item, index) => ({
              id: item.id || `check-${idx + 1}-${index + 1}`,
              name: item.text.trim(),
              description: '',
              isRequired: item.isRequired !== false,
              requiresDocument: false,
            }));

          // Stage document uploads are configured separately from checklist items.
          const documentRequirements = (s.documentRequirements || [])
            .filter(item => item.documentName.trim())
            .map((item, requirementIndex) => ({
              id: item.id || `document-${idx + 1}-${requirementIndex + 1}`,
              requirementKey: item.requirementKey?.trim() || buildRequirementKey(item.documentName, requirementIndex),
              documentName: item.documentName.trim(),
              documentType: item.documentType?.trim() || undefined,
              isRequired: item.isRequired !== false,
            }));
          const fallbackDocumentName = s.documentName?.trim();
          const hasStageDocument = Boolean(s.requiresDocument || s.type === 'document' || documentRequirements.length > 0 || fallbackDocumentName);
          const configuration = hasStageDocument || qualityChecks.length > 0
            ? {
                ...(hasStageDocument
                  ? {
                      taskConfig: {
                        taskActionType: s.type === 'document' ? 'document' : 'general',
                        requiresDocument: Boolean(s.requiresDocument || s.type === 'document'),
                        documentName: documentRequirements[0]?.documentName || fallbackDocumentName || undefined,
                        documentRequirementKey: documentRequirements[0]?.requirementKey || s.documentRequirementKey?.trim() || s.id,
                        documentRequirements: documentRequirements.length > 0
                          ? documentRequirements
                          : fallbackDocumentName
                            ? [{
                                id: s.documentRequirementKey?.trim() || s.id,
                                requirementKey: s.documentRequirementKey?.trim() || s.id,
                                documentName: fallbackDocumentName,
                                isRequired: true,
                              }]
                            : [],
                        instructions: s.description?.trim() || undefined,
                      },
                    }
                  : {}),
                ...(qualityChecks.length > 0
                  ? {
                      qualityConfig: {
                        qualityChecks,
                      },
                    }
                  : {}),
              }
            : undefined;

          return {
            id: s.id,
            name: s.name,
            description: s.description,
            // The wizard is a "starter"; real step behavior is configured in the Designer.
            // Defaulting to Manual avoids getting stuck on Approval steps without approval config.
            stepType: WorkflowStepType.Manual,
            order: idx + 1,
            isRequired: s.isRequired,
            requiredRole: s.assigneeType === 'role' ? (s.assignee || undefined) : undefined,
            estimatedHours: s.timeoutHours ? s.timeoutHours : undefined,
            configuration,
          };
        });

        const transitions: CreateWorkflowTransitionDto[] = steps.length >= 2
          ? steps.slice(0, steps.length - 1).map((from, i) => ({
              fromStepId: from.id || '',
              toStepId: steps[i + 1].id || '',
              name: `${from.name} → ${steps[i + 1].name}`,
              description: undefined,
              condition: undefined,
              isDefault: true,
              priority: 0,
            }))
          : [];

        const configuration = JSON.stringify({
          createdVia: 'wizard',
          module: selectedModule,
          wizardVersion: 1,
        });

        const createDto: CreateWorkflowDefinitionAdminDto = {
          name: workflowName.trim(),
          description: workflowDescription.trim() ? workflowDescription.trim() : undefined,
          entityType: entityType.trim(),
          isActive: false,
          configuration,
          steps,
          transitions,
        };

        const created = await workflowApiService.createWorkflowDefinition(createDto);
        toast.success('Workflow created', { description: 'Opening designer...' });

        onComplete(created);
        onClose();
      } catch (error: any) {
        console.error('Failed to create workflow definition from wizard:', error);
        const status = error?.status;
        const existingId = error?.response?.existingDefinitionId;
        const message = error?.message || 'Please try again.';

        if (status === 409 && existingId) {
          toast.error('Workflow Already Exists', {
            description: message,
            action: {
              label: 'Open Existing',
              onClick: () => {
                onComplete({ id: existingId });
                onClose();
              }
            }
          });
        } else {
          toast.error('Failed to create workflow', {
            description: message,
          });
        }
      } finally {
        setIsCreating(false);
      }
    })();
  };

  const getStepIcon = (type: string) => {
    const stepType = stepTypes.find(st => st.id === type);
    return stepType ? stepType.icon : FileText;
  };

  const canProceed = (step: number) => {
    switch (step) {
      case 1: return workflowName && workflowDescription && selectedModule && entityType;
      // Steps can be defined in the Designer; allow proceeding even with no steps.
      case 2: return true;
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
                        step === currentStep ? 'bg-primary text-primary-foreground' :
                        step < currentStep ? 'bg-emerald-600 text-white' : 'bg-muted text-muted-foreground'
                      }`}>
                        {step < currentStep ? <Check className="h-4 w-4" /> : step}
                      </div>
                      {step < 3 && <ArrowRight className="h-4 w-4 mx-2 text-muted-foreground" />}
                    </div>
                  ))}
                </div>
              </div>
            </div>
        </DialogHeader>

          {/* Content */}
          <div className="flex-1 overflow-y-auto">
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
                      {availableEntityTypes.length > 0 || entityTypesLoading ? (
                        <div className="space-y-2">
                          <Select
                            value={isCustomEntityType ? customEntityTypeValue : entityType}
                            onValueChange={(value) => {
                              if (value === customEntityTypeValue) {
                                setIsCustomEntityType(true);
                                setEntityType('');
                                return;
                              }
                              setIsCustomEntityType(false);
                              setEntityType(value);
                            }}
                          >
                            <SelectTrigger>
                              <SelectValue placeholder={entityTypesLoading ? 'Loading...' : 'Select entity type'} />
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
                          {isCustomEntityType && (
                            <Input
                              placeholder="Custom Entity Type"
                              value={entityType}
                              onChange={(e) => setEntityType(e.target.value)}
                            />
                          )}
                        </div>
                      ) : (
                        <Input
                          id="entityType"
                          placeholder="Enter entity type"
                          value={entityType}
                          onChange={(e) => setEntityType(e.target.value)}
                        />
                      )}
                      {entityTypeNotice && (
                        <p className={entityTypesError ? 'text-xs text-red-600' : 'text-xs text-amber-600'}>
                          {entityTypeNotice}
                        </p>
                      )}
                    </div>
                  </div>
                </div>

                <Separator />

                <div>
                  <h3 className="text-lg font-semibold mb-4">Select Module</h3>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                    {moduleOptions.map((module) => {
                      const IconComponent = module.icon;
                      return (
                        <Card
                          key={module.id}
                          className={`cursor-pointer transition-colors ${
                            selectedModule === module.id
                              ? 'border-primary bg-primary/10'
                              : 'hover:bg-muted/60'
                          }`}
                          onClick={() => setSelectedModule(module.id)}
                        >
                          <CardContent className="p-3">
                            <div className="flex items-start space-x-3">
                              <IconComponent className="h-5 w-5 text-primary mt-0.5" />
                              <div className="flex-1">
                                <h4 className="font-medium text-sm">{module.name}</h4>
                                <p className="text-xs text-muted-foreground mt-1">{module.description}</p>
                              </div>
                              {selectedModule === module.id && (
                                <Check className="h-5 w-5 text-primary" />
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
                    <div className="text-center py-12 text-muted-foreground">
                      <FileText className="h-12 w-12 mx-auto mb-4 text-muted-foreground" />
                      <p>No steps added yet</p>
                      <p className="text-sm">Click "Add Step" to create your workflow</p>
                    </div>
                  ) : (
                    <div className="space-y-4">
                      {workflowSteps.map((step, index) => {
                        const IconComponent = getStepIcon(step.type);
                        const stageDocumentCount = step.documentRequirements?.filter(item => item.documentName.trim()).length || 0;
                        return (
                          <Card key={step.id}>
                            <CardContent className="p-4">
                              <div className="flex items-start justify-between">
                                <div className="flex items-start space-x-3 flex-1">
                                  <div className="bg-primary/10 p-2 rounded">
                                    <IconComponent className="h-4 w-4 text-primary" />
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
                                      {(step.requiresDocument || step.type === 'document' || stageDocumentCount > 0) && (
                                        <Badge variant="secondary" className="text-xs">
                                          {stageDocumentCount > 1 ? `${stageDocumentCount} Documents` : 'Document'}
                                        </Badge>
                                      )}
                                    </div>
                                    <p className="text-sm text-muted-foreground mb-2">{step.description}</p>
                                    <div className="flex items-center space-x-4 text-xs text-muted-foreground">
                                      <span>Assignee: {step.assignee}</span>
                                      {step.timeoutHours && (
                                        <span>Timeout: {step.timeoutHours}h</span>
                                      )}
                                      {stageDocumentCount > 0 && (
                                        <span>Documents: {stageDocumentCount}</span>
                                      )}
                                      {step.checklist && step.checklist.length > 0 && (
                                        <span>Checklist: {step.checklist.length} items</span>
                                      )}
                                    </div>
                                  </div>
                                </div>
                                <div className="flex items-center space-x-2">
                                  <span className="text-sm text-muted-foreground">#{index + 1}</span>
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
                        <span className="text-muted-foreground">Name:</span>
                        <span>{workflowName}</span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-muted-foreground">Module:</span>
                        <span>{moduleOptions.find(m => m.id === selectedModule)?.name}</span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-muted-foreground">Entity Type:</span>
                        <span>{entityType}</span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-muted-foreground">Total Steps:</span>
                        <span>{workflowSteps.length}</span>
                      </div>
                    </div>
                  </div>
                  
                  <div>
                    <h4 className="font-medium mb-3">Description</h4>
                    <p className="text-sm text-muted-foreground">{workflowDescription}</p>
                  </div>
                </div>

                <Separator />

                <div>
                  <h4 className="font-medium mb-3">Workflow Steps</h4>
                  <div className="space-y-3">
                    {workflowSteps.map((step, index) => {
                      const IconComponent = getStepIcon(step.type);
                      const stageDocumentCount = step.documentRequirements?.filter(item => item.documentName.trim()).length || 0;
                      return (
                        <div key={step.id} className="flex items-center space-x-3 p-3 bg-muted/60 rounded">
                          <div className="bg-primary/10 p-1.5 rounded">
                            <IconComponent className="h-4 w-4 text-primary" />
                          </div>
                          <div className="flex-1">
                            <div className="flex items-center space-x-2">
                              <span className="font-medium">{index + 1}. {step.name}</span>
                              <Badge variant="outline" className="text-xs">
                                {step.type}
                              </Badge>
                              {(step.requiresDocument || step.type === 'document' || stageDocumentCount > 0) && (
                                <Badge variant="secondary" className="text-xs">
                                  {stageDocumentCount > 1 ? `${stageDocumentCount} Documents` : 'Document'}
                                </Badge>
                              )}
                            </div>
                            <p className="text-sm text-muted-foreground">
                              {step.assignee}
                              {stageDocumentCount > 0 && (
                                <span> - {stageDocumentCount} document upload{stageDocumentCount === 1 ? '' : 's'}</span>
                              )}
                            </p>
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
                  disabled={isCreating}
                >
                  <ArrowLeft className="h-4 w-4 mr-2" />
                  Back
                </Button>
              )}
            </div>
            <div className="flex items-center space-x-3">
              <Button variant="outline" onClick={onClose} disabled={isCreating}>
                Cancel
              </Button>
              {currentStep < 3 ? (
                <Button 
                  onClick={() => setCurrentStep(prev => prev + 1)}
                  disabled={!canProceed(currentStep) || isCreating}
                >
                  Next
                  <ArrowRight className="h-4 w-4 ml-2" />
                </Button>
              ) : (
                <Button onClick={handleComplete} disabled={isCreating}>
                  <Check className="h-4 w-4 mr-2" />
                  {isCreating ? 'Creating...' : 'Create Workflow'}
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
                    <Select
                      value={stepType}
                      onValueChange={(value) => {
                        setStepType(value);
                        if (value === 'document' && documentRequirements.length === 0) {
                          addDocumentRequirement();
                        }
                      }}
                    >
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

                <Separator />
                <div className="space-y-3 rounded-md border border-border p-3">
                  <div className="flex items-center space-x-2">
                    <Checkbox
                      id="requiresDocument"
                      checked={requiresDocument || stepType === 'document'}
                      onCheckedChange={(checked) => {
                        const enabled = checked === true;
                        setRequiresDocument(enabled);
                        if (enabled && documentRequirements.length === 0) {
                          addDocumentRequirement();
                        }
                      }}
                      disabled={stepType === 'document'}
                    />
                    <Label htmlFor="requiresDocument">Require document upload for this stage</Label>
                  </div>
                  {(requiresDocument || stepType === 'document') && (
                    <div className="space-y-3">
                      <div className="flex items-center justify-between">
                        <Label>Required Documents</Label>
                        <Button variant="outline" size="sm" onClick={addDocumentRequirement}>
                          <Plus className="h-4 w-4 mr-1" />
                          Add Document
                        </Button>
                      </div>
                      {documentRequirements.length === 0 && (
                        <div className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
                          Add each document that must be uploaded before this stage can be completed.
                        </div>
                      )}
                      <div className="space-y-3">
                        {documentRequirements.map((item, index) => (
                          <div key={item.id || index} className="rounded-md border border-border p-3">
                            <div className="grid gap-3 md:grid-cols-[1fr_1fr_auto]">
                              <div>
                                <Label className="text-xs">Document Name</Label>
                                <Input
                                  value={item.documentName}
                                  onChange={(e) => updateDocumentRequirement(index, 'documentName', e.target.value)}
                                  placeholder="e.g. Cadastral plan"
                                />
                              </div>
                              <div>
                                <Label className="text-xs">Document Type</Label>
                                <Input
                                  value={item.documentType || ''}
                                  onChange={(e) => updateDocumentRequirement(index, 'documentType', e.target.value)}
                                  placeholder="e.g. Survey Plan"
                                />
                              </div>
                              <div className="flex items-end">
                                <Button
                                  variant="outline"
                                  size="sm"
                                  onClick={() => removeDocumentRequirement(index)}
                                  aria-label="Remove document requirement"
                                >
                                  <X className="h-4 w-4" />
                                </Button>
                              </div>
                            </div>
                            <div className="mt-3 grid gap-3 md:grid-cols-[1fr_auto]">
                              <div>
                                <Label className="text-xs">Requirement Key</Label>
                                <Input
                                  value={item.requirementKey || ''}
                                  onChange={(e) => updateDocumentRequirement(index, 'requirementKey', e.target.value)}
                                  placeholder="Auto-generated if blank"
                                />
                              </div>
                              <div className="flex items-end space-x-2 pb-2">
                                <Checkbox
                                  checked={item.isRequired !== false}
                                  onCheckedChange={(checked) => updateDocumentRequirement(index, 'isRequired', checked as boolean)}
                                />
                                <Label className="text-xs">Required</Label>
                              </div>
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
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
                      <div key={item.id || index} className="rounded-md border border-border p-3">
                        <div className="flex items-center gap-2">
                          <Input
                            value={item.text}
                            onChange={(e) => updateChecklistItem(index, 'text', e.target.value)}
                            placeholder="Checklist item"
                          />
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => removeChecklistItem(index)}
                            aria-label="Remove checklist item"
                          >
                            <X className="h-4 w-4" />
                          </Button>
                        </div>
                        <div className="mt-3 grid gap-3 md:grid-cols-2">
                          <div className="flex items-center space-x-2">
                            <Checkbox
                              checked={item.isRequired}
                              onCheckedChange={(checked) => updateChecklistItem(index, 'isRequired', checked as boolean)}
                            />
                            <Label className="text-xs">Required</Label>
                          </div>
                        </div>
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
