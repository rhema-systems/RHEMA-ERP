'use client';

import { useState, useEffect } from 'react';
import { Plus, Search, Filter, Download, Upload, Settings, Play, Pause, BarChart3, Eye, Edit3, Trash2 } from 'lucide-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { WorkflowDesigner } from '@/components/workflow/WorkflowDesigner';
import { WorkflowInstanceMonitor } from '@/components/workflow/WorkflowInstanceMonitor';
import { WorkflowCreationWizard } from '@/components/workflow/WorkflowCreationWizard';
import { workflowApiService } from '@/services/workflow-api.service';
import type { 
  WorkflowDefinitionAdminDto, 
  WorkflowDefinitionFilterDto,
  WorkflowSummaryDto 
} from '@/types/workflow';
import { toast } from '@/hooks/use-toast';

export default function WorkflowAdministrationPage() {
  const [activeTab, setActiveTab] = useState('definitions');
  const [selectedWorkflow, setSelectedWorkflow] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [isDesignerOpen, setIsDesignerOpen] = useState(false);
  const [isInstanceMonitorOpen, setIsInstanceMonitorOpen] = useState(false);
  const [isCreationWizardOpen, setIsCreationWizardOpen] = useState(false);

  // Workflow statistics
  const [stats, setStats] = useState({
    totalDefinitions: 0,
    activeInstances: 0,
    completedToday: 0,
    pendingApprovals: 0,
  });

  const [definitions, setDefinitions] = useState<WorkflowDefinitionAdminDto[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    // Load workflow statistics and definitions on mount
    fetchWorkflowStats();
    fetchDefinitions();
  }, []);

  // Refetch definitions when search query changes
  useEffect(() => {
    const debounceTimer = setTimeout(() => {
      fetchDefinitions();
    }, 300);
    return () => clearTimeout(debounceTimer);
  }, [searchQuery]);

  const fetchWorkflowStats = async () => {
    try {
      const summary = await workflowApiService.getWorkflowSummary();
      setStats({
        totalDefinitions: summary.totalWorkflowDefinitions,
        activeInstances: summary.activeWorkflowInstances,
        completedToday: summary.completedWorkflowInstances, // adjust if a dedicated metric exists
        pendingApprovals: 0, // could be derived via getPendingApprovals length if needed
      });
    } catch (error) {
      console.error('Failed to fetch workflow stats:', error);
      toast({ title: 'Failed to load stats', variant: 'destructive' });
    }
  };

  const fetchDefinitions = async () => {
    try {
      setLoading(true);
      const result = await workflowApiService.getWorkflowDefinitions({
        page: 1,
        pageSize: 10,
        searchTerm: searchQuery || undefined,
        entityType: undefined,
        isActive: undefined,
        createdAfter: undefined,
        createdBefore: undefined,
        sortBy: 'Name',
        sortDescending: false,
      } as unknown as WorkflowDefinitionFilterDto);
      setDefinitions(result.data);
    } catch (error) {
      console.error('Failed to fetch workflow definitions:', error);
      toast({ title: 'Failed to load definitions', variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  };

  const handleCreateWorkflow = () => {
    setIsCreationWizardOpen(true);
  };

  const handleViewInstances = () => {
    setIsInstanceMonitorOpen(true);
  };

  const handleDesignWorkflow = (workflowId: string) => {
    setSelectedWorkflow(workflowId);
    setIsDesignerOpen(true);
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex justify-between items-center">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Workflow Administration</h1>
          <p className="text-muted-foreground">
            Design, manage, and monitor business process workflows across all modules
          </p>
        </div>
        <div className="flex space-x-2">
          <Button variant="outline" size="sm">
            <Upload className="h-4 w-4 mr-2" />
            Import
          </Button>
          <Button variant="outline" size="sm">
            <Download className="h-4 w-4 mr-2" />
            Export
          </Button>
          <Button onClick={handleCreateWorkflow}>
            <Plus className="h-4 w-4 mr-2" />
            Create Workflow
          </Button>
        </div>
      </div>

      {/* Statistics Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Definitions</CardTitle>
            <Settings className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{stats.totalDefinitions}</div>
            <p className="text-xs text-muted-foreground">
              +2 from last month
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Active Instances</CardTitle>
            <Play className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{stats.activeInstances}</div>
            <p className="text-xs text-muted-foreground">
              +12% from yesterday
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Completed Today</CardTitle>
            <BarChart3 className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{stats.completedToday}</div>
            <p className="text-xs text-muted-foreground">
              8% above average
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Pending Approvals</CardTitle>
            <Pause className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{stats.pendingApprovals}</div>
            <p className="text-xs text-muted-foreground">
              2 require immediate attention
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Main Content Tabs */}
      <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-4">
        <TabsList className="grid w-full grid-cols-4">
          <TabsTrigger value="definitions">Definitions</TabsTrigger>
          <TabsTrigger value="instances">Live Instances</TabsTrigger>
          <TabsTrigger value="templates">Templates</TabsTrigger>
          <TabsTrigger value="analytics">Analytics</TabsTrigger>
        </TabsList>

        <TabsContent value="definitions" className="space-y-4">
          <div className="flex justify-between items-center">
            <div className="flex space-x-2">
              <div className="relative">
                <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400 h-4 w-4" />
                <Input
                  placeholder="Search workflows..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="pl-10 w-64"
                />
              </div>
              <Button variant="outline" size="sm">
                <Filter className="h-4 w-4 mr-2" />
                Filter
              </Button>
            </div>
            <div className="flex space-x-2">
              <Badge variant="secondary">All Modules Connected</Badge>
              <Badge variant="outline">Real-time Sync</Badge>
            </div>
          </div>
          
          {/* Workflow Definitions from API */}
          <div className="grid gap-4">
            {loading && (
              <div className="text-sm text-muted-foreground">Loading definitions...</div>
            )}
            {!loading && definitions.length === 0 && (
              <div className="text-sm text-muted-foreground">No workflow definitions found.</div>
            )}
            {definitions.map((def) => (
              <Card key={def.id}>
                <CardHeader>
                  <div className="flex items-center justify-between">
                    <div>
                      <CardTitle>{def.name}</CardTitle>
                      <CardDescription>{def.description || 'No description provided'}</CardDescription>
                    </div>
                    <div className="flex space-x-2">
                      <Badge variant={def.isActive ? 'default' : 'secondary'}>{def.isActive ? 'Active' : 'Inactive'}</Badge>
                      <Badge variant="outline">{def.entityType}</Badge>
                    </div>
                  </div>
                </CardHeader>
                <CardContent>
                  <div className="flex items-center justify-between">
                    <div className="text-sm text-muted-foreground">
                      {def.stepCount} steps • {def.activeInstancesCount} active instances • Version {def.version}
                    </div>
                    <div className="flex space-x-2">
                      <Button variant="outline" size="sm" onClick={() => handleDesignWorkflow(def.id)}>
                        <Settings className="h-4 w-4 mr-1" />
                        Design
                      </Button>
                      <Button variant="outline" size="sm">
                        <Edit3 className="h-4 w-4 mr-1" />
                        Edit
                      </Button>
                      <Button variant="outline" size="sm">
                        <Trash2 className="h-4 w-4 mr-1" />
                        Delete
                      </Button>
                    </div>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="instances" className="space-y-4">
          <div className="flex justify-between items-center">
            <div>
              <h3 className="text-lg font-semibold">Live Workflow Instances</h3>
              <p className="text-muted-foreground">
                View and monitor all active workflow instances across your organization.
              </p>
            </div>
            <Button onClick={handleViewInstances}>
              <Eye className="h-4 w-4 mr-2" />
              Monitor Instances
            </Button>
          </div>
          
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            <Card>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <CardTitle className="text-base">Running Instances</CardTitle>
                  <Badge variant="default">{stats.activeInstances}</Badge>
                </div>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground">
                  Currently active workflow instances requiring attention
                </p>
              </CardContent>
            </Card>
            
            <Card>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <CardTitle className="text-base">Pending Approvals</CardTitle>
                  <Badge variant="secondary">{stats.pendingApprovals}</Badge>
                </div>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground">
                  Workflows waiting for approval decisions
                </p>
              </CardContent>
            </Card>
            
            <Card>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <CardTitle className="text-base">Completed Today</CardTitle>
                  <Badge variant="outline">{stats.completedToday}</Badge>
                </div>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground">
                  Workflows completed successfully today
                </p>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <TabsContent value="templates" className="space-y-4">
          <div>
            <h3 className="text-lg font-semibold">Workflow Templates</h3>
            <p className="text-muted-foreground">
              Pre-built workflow templates to get you started quickly with common business processes.
            </p>
          </div>
          
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Card>
              <CardHeader>
                <CardTitle>Standard Approval Process</CardTitle>
                <CardDescription>Basic approval workflow with escalation</CardDescription>
              </CardHeader>
              <CardContent>
                <Button variant="outline" className="w-full" onClick={() => handleDesignWorkflow('template-approval')}>
                  Use Template
                </Button>
              </CardContent>
            </Card>
            
            <Card>
              <CardHeader>
                <CardTitle>Document Review Process</CardTitle>
                <CardDescription>Multi-stage document review with feedback loops</CardDescription>
              </CardHeader>
              <CardContent>
                <Button variant="outline" className="w-full" onClick={() => handleDesignWorkflow('template-document')}>
                  Use Template
                </Button>
              </CardContent>
            </Card>
            
            <Card>
              <CardHeader>
                <CardTitle>Request-to-Fulfillment</CardTitle>
                <CardDescription>Complete request processing from submission to completion</CardDescription>
              </CardHeader>
              <CardContent>
                <Button variant="outline" className="w-full" onClick={() => handleDesignWorkflow('template-request')}>
                  Use Template
                </Button>
              </CardContent>
            </Card>
            
            <Card>
              <CardHeader>
                <CardTitle>Incident Management</CardTitle>
                <CardDescription>Incident reporting, investigation, and resolution workflow</CardDescription>
              </CardHeader>
              <CardContent>
                <Button variant="outline" className="w-full" onClick={() => handleDesignWorkflow('template-incident')}>
                  Use Template
                </Button>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <TabsContent value="analytics" className="space-y-4">
          <div>
            <h3 className="text-lg font-semibold">Workflow Analytics</h3>
            <p className="text-muted-foreground">
              Performance metrics and insights for your workflows to optimize processes.
            </p>
          </div>
          
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Average Duration</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">4.2h</div>
                <p className="text-xs text-muted-foreground">-15% from last week</p>
              </CardContent>
            </Card>
            
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Success Rate</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">94.7%</div>
                <p className="text-xs text-muted-foreground">+2.3% from last week</p>
              </CardContent>
            </Card>
            
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Bottlenecks</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">3</div>
                <p className="text-xs text-muted-foreground">Identified this week</p>
              </CardContent>
            </Card>
            
            <Card>
              <CardHeader>
                <CardTitle className="text-base">SLA Compliance</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">89.2%</div>
                <p className="text-xs text-muted-foreground">+5.1% from last week</p>
              </CardContent>
            </Card>
          </div>
        </TabsContent>
      </Tabs>

      {/* Workflow Designer Modal */}
      <WorkflowDesigner
        workflowId={selectedWorkflow}
        isOpen={isDesignerOpen}
        onClose={() => {
          setIsDesignerOpen(false);
          setSelectedWorkflow(null);
        }}
        onSave={(workflow) => {
          console.log('Workflow saved:', workflow);
          setIsDesignerOpen(false);
          setSelectedWorkflow(null);
        }}
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
        onComplete={(workflow) => {
          console.log('Creating new workflow:', workflow);
          setIsCreationWizardOpen(false);
          // Optionally open the designer for further editing
          setSelectedWorkflow(workflow.id || 'new-workflow');
          setIsDesignerOpen(true);
        }}
      />
    </div>
  );
}
