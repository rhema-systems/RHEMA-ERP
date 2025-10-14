'use client';

import { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';

import {
  Plus, Settings, Play, Eye, Users, FileText, GitBranch, 
  Bell, Upload, AlertTriangle, Database
} from 'lucide-react';

import { WorkflowDesigner } from '@/components/workflow/WorkflowDesigner';
import { WorkflowInstanceMonitor } from '@/components/workflow/WorkflowInstanceMonitor';
import { WorkflowCreationWizard } from '@/components/workflow/WorkflowCreationWizard';
import { DashboardLayout } from '@/components/layout/dashboard-layout';

export default function WorkflowDemoPage() {
  const [activeTab, setActiveTab] = useState('overview');
  const [isDesignerOpen, setIsDesignerOpen] = useState(false);
  const [isInstanceMonitorOpen, setIsInstanceMonitorOpen] = useState(false);
  const [isCreationWizardOpen, setIsCreationWizardOpen] = useState(false);
  const [selectedWorkflowId, setSelectedWorkflowId] = useState<string | null>(null);

  const handleCreateWorkflow = () => {
    setSelectedWorkflowId(null);
    setIsCreationWizardOpen(true);
  };

  const handleDesignWorkflow = (workflowId: string) => {
    setSelectedWorkflowId(workflowId);
    setIsDesignerOpen(true);
  };

  const handleViewInstances = () => {
    setIsInstanceMonitorOpen(true);
  };

  const handleCompleteWorkflowCreation = (workflowData: any) => {
    console.log('Creating new workflow:', workflowData);
    setIsCreationWizardOpen(false);
    // Open the designer for further editing
    setSelectedWorkflowId('new-workflow');
    setIsDesignerOpen(true);
  };

  const handleSaveWorkflow = (workflowData: any) => {
    console.log('Saving workflow:', workflowData);
    setIsDesignerOpen(false);
    setSelectedWorkflowId(null);
  };

  const workflowFeatures = [
    {
      icon: FileText,
      title: "Task Nodes",
      description: "Assign tasks to users, roles, or departments with priorities and deadlines"
    },
    {
      icon: Users,
      title: "Approval Nodes", 
      description: "Multi-level approvals with any/all/sequential approval types"
    },
    {
      icon: GitBranch,
      title: "Condition Nodes",
      description: "Branch workflows based on data conditions and business rules"
    },
    {
      icon: Bell,
      title: "Notification Nodes",
      description: "Send notifications via email, SMS, push notifications, or in-app"
    },
    {
      icon: Upload,
      title: "Document Nodes",
      description: "Require document uploads with approval workflows"
    },
    {
      icon: AlertTriangle,
      title: "Escalation Nodes",
      description: "Automatic escalation with timeout rules and escalation paths"
    },
    {
      icon: Database,
      title: "Integration Nodes",
      description: "Connect to any ERP module or external system"
    },
    {
      icon: Settings,
      title: "Visual Designer",
      description: "Drag-and-drop workflow designer with real-time preview"
    }
  ];

  return (
    <DashboardLayout>
      <div className="space-y-6">
      {/* Header */}
      <div className="text-center space-y-4">
        <h1 className="text-4xl font-bold tracking-tight">
          Advanced Workflow Management System
        </h1>
        <p className="text-xl text-muted-foreground max-w-3xl mx-auto">
          Create, design, and monitor complex business process workflows with visual tools, 
          conditional logic, checklists, approvals, and full module integration.
        </p>
        <div className="flex justify-center space-x-4">
          <Button size="lg" onClick={handleCreateWorkflow}>
            <Plus className="h-5 w-5 mr-2" />
            Create New Workflow
          </Button>
          <Button size="lg" variant="outline" onClick={handleViewInstances}>
            <Eye className="h-5 w-5 mr-2" />
            Monitor Instances
          </Button>
          <Button size="lg" variant="outline" onClick={() => handleDesignWorkflow('demo-workflow')}>
            <Settings className="h-5 w-5 mr-2" />
            Open Designer
          </Button>
        </div>
      </div>

      {/* Main Content */}
      <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-6">
        <TabsList className="grid w-full grid-cols-4">
          <TabsTrigger value="overview">System Overview</TabsTrigger>
          <TabsTrigger value="features">Key Features</TabsTrigger>
          <TabsTrigger value="examples">Sample Workflows</TabsTrigger>
          <TabsTrigger value="integration">Module Integration</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="space-y-6">
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <Card>
              <CardHeader>
                <CardTitle className="text-lg">Visual Designer</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground">
                  Drag-and-drop interface for creating complex workflows with nodes, 
                  conditions, and integrations.
                </p>
              </CardContent>
            </Card>
            
            <Card>
              <CardHeader>
                <CardTitle className="text-lg">Creation Wizard</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground">
                  Step-by-step wizard to create workflows with module selection, 
                  steps, conditions, and checklists.
                </p>
              </CardContent>
            </Card>
            
            <Card>
              <CardHeader>
                <CardTitle className="text-lg">Instance Monitor</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground">
                  Real-time monitoring of active workflow instances with 
                  visual progress tracking.
                </p>
              </CardContent>
            </Card>
            
            <Card>
              <CardHeader>
                <CardTitle className="text-lg">Module Integration</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground">
                  Seamlessly integrate with all ERP modules including 
                  maintenance, procurement, HR, and more.
                </p>
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardHeader>
              <CardTitle>Workflow Creation Process</CardTitle>
              <CardDescription>
                Our three-step process makes it easy to create powerful workflows
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                <div className="text-center space-y-2">
                  <div className="w-12 h-12 bg-blue-100 rounded-full flex items-center justify-center mx-auto">
                    <span className="text-blue-600 font-bold">1</span>
                  </div>
                  <h3 className="font-semibold">Setup & Module Selection</h3>
                  <p className="text-sm text-muted-foreground">
                    Choose your workflow name, description, target module, and entity type
                  </p>
                </div>
                
                <div className="text-center space-y-2">
                  <div className="w-12 h-12 bg-green-100 rounded-full flex items-center justify-center mx-auto">
                    <span className="text-green-600 font-bold">2</span>
                  </div>
                  <h3 className="font-semibold">Add Steps & Logic</h3>
                  <p className="text-sm text-muted-foreground">
                    Add workflow steps, conditions, checklists, and configure assignees
                  </p>
                </div>
                
                <div className="text-center space-y-2">
                  <div className="w-12 h-12 bg-purple-100 rounded-full flex items-center justify-center mx-auto">
                    <span className="text-purple-600 font-bold">3</span>
                  </div>
                  <h3 className="font-semibold">Design & Deploy</h3>
                  <p className="text-sm text-muted-foreground">
                    Use the visual designer to refine your workflow and deploy it live
                  </p>
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="features" className="space-y-6">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {workflowFeatures.map((feature, index) => {
              const IconComponent = feature.icon;
              return (
                <Card key={index}>
                  <CardHeader>
                    <div className="flex items-center space-x-3">
                      <div className="p-2 bg-blue-100 rounded">
                        <IconComponent className="h-5 w-5 text-blue-600" />
                      </div>
                      <CardTitle className="text-lg">{feature.title}</CardTitle>
                    </div>
                  </CardHeader>
                  <CardContent>
                    <p className="text-sm text-muted-foreground">
                      {feature.description}
                    </p>
                  </CardContent>
                </Card>
              );
            })}
          </div>
        </TabsContent>

        <TabsContent value="examples" className="space-y-6">
          <div className="space-y-4">
            <Card>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <div>
                    <CardTitle>Equipment Maintenance Request</CardTitle>
                    <CardDescription>
                      Complete maintenance workflow with approvals, scheduling, and completion tracking
                    </CardDescription>
                  </div>
                  <div className="flex space-x-2">
                    <Badge variant="default">Maintenance</Badge>
                    <Badge variant="outline">8 Steps</Badge>
                  </div>
                </div>
              </CardHeader>
              <CardContent>
                <div className="flex items-center justify-between">
                  <div className="text-sm text-muted-foreground">
                    Features: Task assignment, manager approval, condition checks, technician scheduling, completion checklist
                  </div>
                  <Button variant="outline" onClick={() => handleDesignWorkflow('maintenance-001')}>
                    <Settings className="h-4 w-4 mr-1" />
                    View Design
                  </Button>
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <div>
                    <CardTitle>Purchase Order Approval</CardTitle>
                    <CardDescription>
                      Multi-level approval workflow with budget conditions and escalations
                    </CardDescription>
                  </div>
                  <div className="flex space-x-2">
                    <Badge variant="default">Procurement</Badge>
                    <Badge variant="outline">12 Steps</Badge>
                  </div>
                </div>
              </CardHeader>
              <CardContent>
                <div className="flex items-center justify-between">
                  <div className="text-sm text-muted-foreground">
                    Features: Budget conditions, sequential approvals, automatic escalation, vendor notifications
                  </div>
                  <Button variant="outline" onClick={() => handleDesignWorkflow('procurement-001')}>
                    <Settings className="h-4 w-4 mr-1" />
                    View Design
                  </Button>
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <div>
                    <CardTitle>Employee Onboarding</CardTitle>
                    <CardDescription>
                      Comprehensive onboarding process with document management and training checklists
                    </CardDescription>
                  </div>
                  <div className="flex space-x-2">
                    <Badge variant="default">HR</Badge>
                    <Badge variant="outline">15 Steps</Badge>
                  </div>
                </div>
              </CardHeader>
              <CardContent>
                <div className="flex items-center justify-between">
                  <div className="text-sm text-muted-foreground">
                    Features: Document upload, training checklists, department notifications, IT setup tasks
                  </div>
                  <Button variant="outline" onClick={() => handleDesignWorkflow('hr-001')}>
                    <Settings className="h-4 w-4 mr-1" />
                    View Design
                  </Button>
                </div>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <TabsContent value="integration" className="space-y-6">
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <Card>
              <CardHeader>
                <div className="flex items-center space-x-2">
                  <Settings className="h-5 w-5 text-blue-600" />
                  <CardTitle className="text-base">Maintenance</CardTitle>
                </div>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground">
                  Work orders, equipment tracking, maintenance scheduling
                </p>
              </CardContent>
            </Card>
            
            <Card>
              <CardHeader>
                <div className="flex items-center space-x-2">
                  <Database className="h-5 w-5 text-green-600" />
                  <CardTitle className="text-base">Inventory</CardTitle>
                </div>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground">
                  Stock management, reorder workflows, inventory audits
                </p>
              </CardContent>
            </Card>
            
            <Card>
              <CardHeader>
                <div className="flex items-center space-x-2">
                  <Users className="h-5 w-5 text-purple-600" />
                  <CardTitle className="text-base">Human Resources</CardTitle>
                </div>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground">
                  Employee onboarding, leave requests, performance reviews
                </p>
              </CardContent>
            </Card>
            
            <Card>
              <CardHeader>
                <div className="flex items-center space-x-2">
                  <FileText className="h-5 w-5 text-orange-600" />
                  <CardTitle className="text-base">Procurement</CardTitle>
                </div>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground">
                  Purchase orders, vendor management, approval chains
                </p>
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardHeader>
              <CardTitle>Integration Capabilities</CardTitle>
              <CardDescription>
                Workflows can seamlessly integrate with any module or external system
              </CardDescription>
            </CardHeader>
            <CardContent>
              <ul className="space-y-2 text-sm">
                <li>✓ Direct database operations (create, read, update, delete)</li>
                <li>✓ API calls to external systems</li>
                <li>✓ Email and notification systems</li>
                <li>✓ Document management systems</li>
                <li>✓ Reporting and analytics platforms</li>
                <li>✓ Custom business logic execution</li>
              </ul>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Workflow Designer Modal */}
      <WorkflowDesigner
        workflowId={selectedWorkflowId}
        isOpen={isDesignerOpen}
        onClose={() => {
          setIsDesignerOpen(false);
          setSelectedWorkflowId(null);
        }}
        onSave={handleSaveWorkflow}
      />

      {/* Workflow Instance Monitor Modal */}
      <WorkflowInstanceMonitor
        isOpen={isInstanceMonitorOpen}
        onClose={() => setIsInstanceMonitorOpen(false)}
      />

      {/* Workflow Creation Wizard Modal */}
      <WorkflowCreationWizard
        isOpen={isCreationWizardOpen}
        onClose={() => setIsCreationWizardOpen(false)}
        onComplete={handleCompleteWorkflowCreation}
      />
      </div>
    </DashboardLayout>
  );
}
