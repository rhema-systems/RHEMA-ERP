'use client';

import { Suspense, useEffect, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { Plus, Search, Download, Upload, Settings, Play, Pause, BarChart3, Eye, Edit3, Trash2, RefreshCw, Info, Copy, Rocket, Archive, History, ShieldCheck, AlertTriangle } from 'lucide-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { WorkflowDesigner } from '@/components/workflow/WorkflowDesigner';
import { WorkflowInstanceMonitor } from '@/components/workflow/WorkflowInstanceMonitor';
import { WorkflowCreationWizard } from '@/components/workflow/WorkflowCreationWizard';
import { WorkflowApprovalPoliciesPanel } from '@/components/workflow/WorkflowApprovalPoliciesPanel';
import { WorkflowGovernancePanel } from '@/components/workflow/WorkflowGovernancePanel';
import { WorkflowTemplateCataloguePanel } from '@/components/workflow/WorkflowTemplateCataloguePanel';
import { WorkflowAnalyticsPanel } from '@/components/workflow/WorkflowAnalyticsPanel';
import { workflowApiService } from '@/services/workflow-api.service';
import type { 
  WorkflowDefinitionAdminDto, 
  WorkflowDefinitionDto,
  WorkflowDefinitionFilterDto,
  WorkflowDefinitionVersionDto,
  WorkflowDefinitionComparisonDto,
  WorkflowModuleConformanceReport,
  WorkflowEntityTypeInfo
} from '@/types/workflow';
import { WorkflowDefinitionLifecycleStatus } from '@/types/workflow';
import { toast } from '@/hooks/use-toast';

function WorkflowAdministrationPageInner() {
  const searchParams = useSearchParams();
  const [activeTab, setActiveTab] = useState('definitions');
  const [selectedWorkflow, setSelectedWorkflow] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [entityTypeFilter, setEntityTypeFilter] = useState<string>('all');
  const [definitionStatusFilter, setDefinitionStatusFilter] = useState<'all' | 'active' | 'inactive'>('all');
  const [isDesignerOpen, setIsDesignerOpen] = useState(false);
  const [isInstanceMonitorOpen, setIsInstanceMonitorOpen] = useState(false);
  const [isCreationWizardOpen, setIsCreationWizardOpen] = useState(false);
  const [isSeedingEntityTypes, setIsSeedingEntityTypes] = useState(false);
  const [entityTypes, setEntityTypes] = useState<WorkflowEntityTypeInfo[]>([]);
  const [initializedFromQuery, setInitializedFromQuery] = useState(false);
  const [conformance, setConformance] = useState<WorkflowModuleConformanceReport | null>(null);

  // Workflow statistics
  const [stats, setStats] = useState({
    totalDefinitions: 0,
    activeInstances: 0,
    completedToday: 0,
    pendingApprovals: 0,
  });

  const [definitions, setDefinitions] = useState<WorkflowDefinitionAdminDto[]>([]);
  const [definitionsTotalCount, setDefinitionsTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);

  // Edit / Delete actions
  const [editDialogOpen, setEditDialogOpen] = useState(false);
  const [editLoading, setEditLoading] = useState(false);
  const [editSaving, setEditSaving] = useState(false);
  const [editDefinition, setEditDefinition] = useState<WorkflowDefinitionDto | null>(null);
  const [editName, setEditName] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [lifecycleSavingId, setLifecycleSavingId] = useState<string | null>(null);
  const [versionsDialogOpen, setVersionsDialogOpen] = useState(false);
  const [versionsLoading, setVersionsLoading] = useState(false);
  const [versions, setVersions] = useState<WorkflowDefinitionVersionDto[]>([]);
  const [versionComparison, setVersionComparison] = useState<WorkflowDefinitionComparisonDto | null>(null);
  const [simulationResult, setSimulationResult] = useState<any>(null);

  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<WorkflowDefinitionAdminDto | null>(null);
  const [deleteSaving, setDeleteSaving] = useState(false);

  const handleSimulate = async (definitionId: string) => {
    try { setSimulationResult(await workflowApiService.simulateWorkflowDefinition(definitionId, {})); }
    catch (error: any) { toast({ title: 'Simulation failed', description: error?.message, variant: 'destructive' }); }
  };

  const handleRollback = async (definitionId: string) => {
    try { await workflowApiService.rollbackWorkflowDefinition(definitionId); setVersionsDialogOpen(false); await fetchDefinitions(); toast({ title: 'Rollback draft created' }); }
    catch (error: any) { toast({ title: 'Rollback failed', description: error?.message, variant: 'destructive' }); }
  };

  useEffect(() => {
    // Load workflow statistics and definitions on mount
    fetchWorkflowStats();
    fetchDefinitions();
    fetchEntityTypes();
    fetchConformance();
  }, []);

  useEffect(() => {
    if (initializedFromQuery) return;

    const q = searchParams?.get('q');
    const et = searchParams?.get('entityType');
    const designId = searchParams?.get('designWorkflowId');

    if (q) setSearchQuery(q);
    if (et) setEntityTypeFilter(et);
    if (designId) handleDesignWorkflow(designId);

    setInitializedFromQuery(true);
  }, [initializedFromQuery, searchParams]);

  // Refetch definitions when search query changes
  useEffect(() => {
    const debounceTimer = setTimeout(() => {
      fetchDefinitions();
    }, 300);
    return () => clearTimeout(debounceTimer);
  }, [searchQuery, definitionStatusFilter, entityTypeFilter]);

  const fetchWorkflowStats = async () => {
    try {
      const now = new Date();
      const startOfTodayUtc = new Date(Date.UTC(
        now.getUTCFullYear(),
        now.getUTCMonth(),
        now.getUTCDate()
      ));
      const endOfTodayUtc = new Date(startOfTodayUtc);
      endOfTodayUtc.setUTCDate(endOfTodayUtc.getUTCDate() + 1);

      const statusRequests = [
        workflowApiService.getWorkflowInstances({
          page: 1,
          pageSize: 1,
          status: 'Created',
          sortBy: 'StartedDate',
          sortDescending: true,
        }),
        workflowApiService.getWorkflowInstances({
          page: 1,
          pageSize: 1,
          status: 'InProgress',
          sortBy: 'StartedDate',
          sortDescending: true,
        }),
        workflowApiService.getWorkflowInstances({
          page: 1,
          pageSize: 1,
          status: 'Waiting',
          sortBy: 'StartedDate',
          sortDescending: true,
        }),
        workflowApiService.getWorkflowInstances({
          page: 1,
          pageSize: 1,
          status: 'Suspended',
          sortBy: 'StartedDate',
          sortDescending: true,
        }),
      ];

      const [
        definitionsResult,
        approvalsResult,
        completedTodayResult,
        ...activeResults
      ] = await Promise.allSettled([
        workflowApiService.getWorkflowDefinitions({
          page: 1,
          pageSize: 1,
          sortBy: 'Name',
          sortDescending: false,
        } as unknown as WorkflowDefinitionFilterDto),
        workflowApiService.getPendingApprovals(),
        workflowApiService.getWorkflowInstances({
          page: 1,
          pageSize: 1,
          status: 'Completed',
          sortBy: 'CompletedDate',
          sortDescending: true,
          completedAfter: startOfTodayUtc,
          completedBefore: endOfTodayUtc,
        }),
        ...statusRequests,
      ]);

      const totalDefinitions =
        definitionsResult.status === 'fulfilled' ? definitionsResult.value.totalCount : undefined;
      const pendingApprovals =
        approvalsResult.status === 'fulfilled' ? approvalsResult.value.length : undefined;
      const completedToday =
        completedTodayResult.status === 'fulfilled' ? completedTodayResult.value.totalCount : undefined;

      const activeInstances = activeResults.reduce((sum, result) => {
        if (result.status === 'fulfilled') {
          return sum + result.value.totalCount;
        }
        return sum;
      }, 0);
      const hasActiveCounts = activeResults.some((result) => result.status === 'fulfilled');

      setStats((prev) => ({
        totalDefinitions: totalDefinitions ?? prev.totalDefinitions,
        activeInstances: hasActiveCounts ? activeInstances : prev.activeInstances,
        completedToday: completedToday ?? prev.completedToday,
        pendingApprovals: pendingApprovals ?? prev.pendingApprovals,
      }));

      if (definitionsResult.status === 'rejected') {
        console.error('Failed to fetch workflow definitions count:', definitionsResult.reason);
      }
      if (approvalsResult.status === 'rejected') {
        console.error('Failed to fetch pending approvals:', approvalsResult.reason);
      }
      if (completedTodayResult.status === 'rejected') {
        console.error('Failed to fetch completed instances count:', completedTodayResult.reason);
      }
      activeResults.forEach((result) => {
        if (result.status === 'rejected') {
          console.error('Failed to fetch active workflow instances:', result.reason);
        }
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
        pageSize: 100,
        searchTerm: searchQuery || undefined,
        entityType: entityTypeFilter && entityTypeFilter !== 'all' ? entityTypeFilter : undefined,
        isActive:
          definitionStatusFilter === 'all'
            ? undefined
            : definitionStatusFilter === 'active',
        createdAfter: undefined,
        createdBefore: undefined,
        sortBy: 'Name',
        sortDescending: false,
      } as unknown as WorkflowDefinitionFilterDto);
      setDefinitions(result.data);
      setDefinitionsTotalCount(result.totalCount);
    } catch (error) {
      console.error('Failed to fetch workflow definitions:', error);
      toast({ title: 'Failed to load definitions', variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  };

  const fetchEntityTypes = async () => {
    try {
      const types = await workflowApiService.getWorkflowEntityTypes();
      const data = (types || [])
        .filter((t) => t && t.code && t.name)
        .map((t) => ({ ...t, code: String(t.code), name: String(t.name) }))
        .sort((a, b) => a.name.localeCompare(b.name));
      setEntityTypes(data);
    } catch (error) {
      console.error('Failed to fetch workflow entity types:', error);
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

  const handleEditWorkflow = async (workflowId: string) => {
    setEditDialogOpen(true);
    setEditLoading(true);
    try {
      const def = await workflowApiService.getWorkflowDefinition(workflowId);
      setEditDefinition(def);
      setEditName(def.name ?? '');
      setEditDescription(def.description ?? '');
    } catch (error: any) {
      console.error('Failed to load workflow definition for edit:', error);
      const status = (error as any)?.status;
      toast({
        title: status === 403 ? 'Not authorized' : 'Failed to load workflow',
        description: status === 403 ? 'You do not have permission to edit workflow definitions.' : undefined,
        variant: 'destructive'
      });
      setEditDialogOpen(false);
    } finally {
      setEditLoading(false);
    }
  };

  const handleSaveWorkflowEdit = async () => {
    if (!editDefinition) return;

    const trimmedName = editName.trim();
    if (!trimmedName) {
      toast({ title: 'Name is required', variant: 'destructive' });
      return;
    }

    setEditSaving(true);
    try {
      await workflowApiService.updateWorkflowDefinition(editDefinition.id, {
        name: trimmedName,
        description: editDescription?.trim() || undefined,
        isActive: false,
        // IMPORTANT: backend overwrites configuration when null/omitted.
        // Keep existing configuration unless the designer is used.
        configuration: editDefinition.configuration ?? undefined,
      });

      toast({ title: 'Workflow updated', variant: 'success' });
      setEditDialogOpen(false);
      setEditDefinition(null);
      await Promise.all([fetchDefinitions(), fetchWorkflowStats()]);
    } catch (error: any) {
      console.error('Failed to update workflow definition:', error);
      const status = (error as any)?.status;
      toast({
        title: status === 403 ? 'Not authorized' : 'Update failed',
        description:
          status === 403
            ? 'You do not have permission to update workflow definitions.'
            : (error as any)?.message?.toString()?.replace(/^Error:\s*/i, ''),
        variant: 'destructive'
      });
    } finally {
      setEditSaving(false);
    }
  };

  const handleCloneDraft = async (definition: WorkflowDefinitionAdminDto) => {
    setLifecycleSavingId(definition.id);
    try {
      const draft = await workflowApiService.cloneWorkflowDefinitionDraft(
        definition.id,
        `Changes after version ${definition.version}`
      );
      toast({ title: `Draft version ${draft.version} created`, variant: 'success' });
      await fetchDefinitions();
      handleDesignWorkflow(draft.id);
    } catch (error: any) {
      toast({ title: 'Unable to create draft', description: error?.message, variant: 'destructive' });
    } finally {
      setLifecycleSavingId(null);
    }
  };

  const handlePublish = async (definition: WorkflowDefinitionAdminDto) => {
    setLifecycleSavingId(definition.id);
    try {
      await workflowApiService.publishWorkflowDefinition(definition.id);
      toast({ title: `Version ${definition.version} published`, variant: 'success' });
      await Promise.all([fetchDefinitions(), fetchWorkflowStats()]);
    } catch (error: any) {
      toast({ title: 'Publish failed', description: error?.message, variant: 'destructive' });
    } finally {
      setLifecycleSavingId(null);
    }
  };

  const handleRetire = async (definition: WorkflowDefinitionAdminDto) => {
    setLifecycleSavingId(definition.id);
    try {
      await workflowApiService.retireWorkflowDefinition(definition.id);
      toast({ title: `Version ${definition.version} retired`, variant: 'success' });
      await Promise.all([fetchDefinitions(), fetchWorkflowStats()]);
    } catch (error: any) {
      toast({ title: 'Retire failed', description: error?.message, variant: 'destructive' });
    } finally {
      setLifecycleSavingId(null);
    }
  };

  const handleViewVersions = async (definition: WorkflowDefinitionAdminDto) => {
    setVersionsDialogOpen(true);
    setVersionsLoading(true);
    setVersions([]);
    setVersionComparison(null);
    try {
      const result = await workflowApiService.getWorkflowDefinitionVersions(definition.id);
      setVersions(result);
      if (result.length >= 2) {
        setVersionComparison(await workflowApiService.compareWorkflowDefinitions(result[1].id, result[0].id));
      }
    } catch (error: any) {
      toast({ title: 'Unable to load version history', description: error?.message, variant: 'destructive' });
      setVersionsDialogOpen(false);
    } finally {
      setVersionsLoading(false);
    }
  };

  const lifecycleLabel = (status: WorkflowDefinitionLifecycleStatus) =>
    status === WorkflowDefinitionLifecycleStatus.Published
      ? 'Published'
      : status === WorkflowDefinitionLifecycleStatus.Retired
        ? 'Retired'
        : 'Draft';

  const handleDeleteWorkflow = (definition: WorkflowDefinitionAdminDto) => {
    setDeleteTarget(definition);
    setDeleteDialogOpen(true);
  };

  const handleConfirmDeleteWorkflow = async () => {
    if (!deleteTarget) return;
    setDeleteSaving(true);
    try {
      await workflowApiService.deleteWorkflowDefinition(deleteTarget.id);
      toast({
        title: 'Workflow deleted',
        description: 'The workflow definition has been archived (hidden from lists).',
        variant: 'success'
      });
      await Promise.all([fetchDefinitions(), fetchWorkflowStats()]);
    } catch (error: any) {
      console.error('Failed to delete workflow definition:', error);
      const status = (error as any)?.status;
      toast({
        title: status === 403 ? 'Not authorized' : 'Delete failed',
        description:
          status === 403
            ? 'You do not have permission to delete workflow definitions.'
            : (error as any)?.message?.toString()?.replace(/^Error:\s*/i, ''),
        variant: 'destructive'
      });
    } finally {
      setDeleteSaving(false);
      setDeleteDialogOpen(false);
      setDeleteTarget(null);
    }
  };

  const handleSeedEntityTypes = async () => {
    try {
      setIsSeedingEntityTypes(true);
      const seeded = await workflowApiService.seedWorkflowEntityTypes();
      await Promise.all([fetchEntityTypes(), fetchConformance()]);
      toast({
        title: 'Entity types seeded',
        description: `${seeded.length} entity types are now available.`
      });
    } catch (error) {
      console.error('Failed to seed workflow entity types:', error);
      toast({
        title: 'Failed to seed entity types',
        variant: 'destructive'
      });
    } finally {
      setIsSeedingEntityTypes(false);
    }
  };

  const fetchConformance = async () => {
    try {
      setConformance(await workflowApiService.getWorkflowModuleConformance());
    } catch (error) {
      console.error('Failed to load workflow module conformance:', error);
    }
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
          <Button variant="outline" size="sm" onClick={handleSeedEntityTypes} disabled={isSeedingEntityTypes}>
            <RefreshCw className="h-4 w-4 mr-2" />
            {isSeedingEntityTypes ? 'Seeding...' : 'Seed Entity Types'}
          </Button>
          <Button onClick={handleCreateWorkflow}>
            <Plus className="h-4 w-4 mr-2" />
            New Workflow (Wizard)
          </Button>
        </div>
      </div>

      {conformance && (
        <div className={`flex flex-col gap-3 border px-4 py-3 md:flex-row md:items-center md:justify-between ${
          conformance.isConformant
            ? 'border-emerald-200 bg-emerald-50 text-emerald-950'
            : 'border-amber-300 bg-amber-50 text-amber-950'
        }`}>
          <div className="flex items-start gap-3">
            {conformance.isConformant
              ? <ShieldCheck className="mt-0.5 h-5 w-5 shrink-0 text-emerald-700" />
              : <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0 text-amber-700" />}
            <div>
              <p className="text-sm font-semibold">
                {conformance.isConformant ? 'Module integrations conformant' : 'Workflow module integration gaps'}
              </p>
              <p className="text-sm opacity-80">
                {conformance.supportedEntityTypes.length} of {conformance.activeEntityTypeCount} active entity types have a status adapter.
              </p>
              {!conformance.isConformant && (
                <p className="mt-1 text-xs">{conformance.missingStatusAdapters.join(', ')}</p>
              )}
            </div>
          </div>
          <Button type="button" variant="outline" size="sm" onClick={fetchConformance}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
        </div>
      )}

      {/* UX Helper: Wizard vs Designer */}
      <Card className="border-blue-200 bg-blue-50">
        <CardContent className="pt-6">
          <div className="flex items-start gap-3">
            <Info className="h-5 w-5 text-blue-600 mt-0.5" />
            <div className="space-y-1">
              <p className="text-sm font-medium text-blue-900">Wizard vs Designer</p>
              <p className="text-sm text-blue-700">
                Use the <strong>Wizard</strong> to create the workflow definition (module + entity type + basic setup). Then use the <strong>Designer</strong> to configure steps, approvers/roles, and conditions before activating it.
              </p>
            </div>
          </div>
        </CardContent>
      </Card>

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
        <TabsList className="grid w-full grid-cols-6">
          <TabsTrigger value="definitions">Definitions</TabsTrigger>
          <TabsTrigger value="instances">Live Instances</TabsTrigger>
          <TabsTrigger value="policies">Approval Policies</TabsTrigger>
          <TabsTrigger value="governance">Governance</TabsTrigger>
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
              <Select value={entityTypeFilter} onValueChange={(value) => setEntityTypeFilter(value as any)}>
                <SelectTrigger className="w-56">
                  <SelectValue placeholder="Entity type" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All entities</SelectItem>
                  {entityTypes.map((t) => (
                    <SelectItem key={t.code} value={t.code}>
                      {t.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Select
                value={definitionStatusFilter}
                onValueChange={(value) => setDefinitionStatusFilter(value as any)}
              >
                <SelectTrigger className="w-44">
                  <SelectValue placeholder="Status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All</SelectItem>
                  <SelectItem value="active">Active</SelectItem>
                  <SelectItem value="inactive">Inactive</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="flex space-x-2">
              <Badge variant="outline">
                Showing {definitions.length} of {definitionsTotalCount}
              </Badge>
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
                      <Badge variant={def.lifecycleStatus === WorkflowDefinitionLifecycleStatus.Published ? 'default' : 'secondary'}>
                        {lifecycleLabel(def.lifecycleStatus)}
                      </Badge>
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
                        {def.lifecycleStatus === WorkflowDefinitionLifecycleStatus.Draft ? 'Design' : 'View'}
                      </Button>
                      <Button variant="outline" size="sm" onClick={() => handleViewVersions(def)}>
                        <History className="h-4 w-4 mr-1" />
                        Versions
                      </Button>
                      <Button variant="outline" size="sm" onClick={() => void handleSimulate(def.id)}><Play className="mr-1 h-4 w-4" />Simulate</Button>
                      {def.lifecycleStatus === WorkflowDefinitionLifecycleStatus.Draft ? (
                        <>
                          <Button variant="outline" size="sm" onClick={() => handleEditWorkflow(def.id)}>
                            <Edit3 className="h-4 w-4 mr-1" />
                            Edit
                          </Button>
                          <Button size="sm" onClick={() => handlePublish(def)} disabled={lifecycleSavingId === def.id}>
                            <Rocket className="h-4 w-4 mr-1" />
                            Publish
                          </Button>
                          <Button variant="outline" size="sm" onClick={() => handleDeleteWorkflow(def)}>
                            <Trash2 className="h-4 w-4 mr-1" />
                            Delete
                          </Button>
                        </>
                      ) : (
                        <>
                          <Button variant="outline" size="sm" onClick={() => handleCloneDraft(def)} disabled={lifecycleSavingId === def.id}>
                            <Copy className="h-4 w-4 mr-1" />
                            Clone Draft
                          </Button>
                          {def.lifecycleStatus === WorkflowDefinitionLifecycleStatus.Published && (
                            <Button variant="outline" size="sm" onClick={() => handleRetire(def)} disabled={lifecycleSavingId === def.id}>
                              <Archive className="h-4 w-4 mr-1" />
                              Retire
                            </Button>
                          )}
                        </>
                      )}
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

        <TabsContent value="policies" className="space-y-4">
          <WorkflowApprovalPoliciesPanel entityTypes={entityTypes} />
        </TabsContent>

        <TabsContent value="governance" className="space-y-4">
          <WorkflowGovernancePanel />
        </TabsContent>

        <TabsContent value="templates" className="space-y-4">
          <WorkflowTemplateCataloguePanel onImported={fetchDefinitions} />
        </TabsContent>

        <TabsContent value="analytics" className="space-y-4">
          <WorkflowAnalyticsPanel />
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

      {/* Edit Workflow Definition */}
      <Dialog
        open={editDialogOpen}
        onOpenChange={(open) => {
          setEditDialogOpen(open);
          if (!open) {
            setEditDefinition(null);
            setEditName('');
            setEditDescription('');
            setEditLoading(false);
            setEditSaving(false);
          }
        }}
      >
        <DialogContent className="sm:max-w-[640px]" style={{ maxWidth: 640 }}>
          <DialogHeader>
            <DialogTitle>Edit Workflow Definition</DialogTitle>
            <DialogDescription>
              Update basic metadata here. Use the Designer for steps, approvers, and conditions.
            </DialogDescription>
          </DialogHeader>

          {editLoading && (
            <div className="text-sm text-muted-foreground">Loading workflow...</div>
          )}

          {!editLoading && editDefinition && (
            <div className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="wf-edit-name">Name</Label>
                  <Input
                    id="wf-edit-name"
                    value={editName}
                    onChange={(e) => setEditName(e.target.value)}
                    disabled={editSaving}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Entity Type</Label>
                  <Input value={editDefinition.entityType} disabled />
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="wf-edit-desc">Description</Label>
                <Textarea
                  id="wf-edit-desc"
                  value={editDescription}
                  onChange={(e) => setEditDescription(e.target.value)}
                  placeholder="Optional description..."
                  disabled={editSaving}
                />
              </div>

              <div className="text-xs text-muted-foreground">
                Draft version {editDefinition.version} • {editDefinition.steps?.length ?? 0} steps • {editDefinition.transitions?.length ?? 0} transitions
              </div>
            </div>
          )}

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setEditDialogOpen(false)}
              disabled={editSaving}
            >
              Cancel
            </Button>
            <Button onClick={handleSaveWorkflowEdit} disabled={editSaving || editLoading || !editDefinition}>
              {editSaving ? 'Saving...' : 'Save Changes'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={versionsDialogOpen} onOpenChange={setVersionsDialogOpen}>
        <DialogContent className="sm:max-w-[720px]" style={{ maxWidth: 720 }}>
          <DialogHeader>
            <DialogTitle>Workflow Version History</DialogTitle>
            <DialogDescription>Published versions remain immutable and running instances keep their original version.</DialogDescription>
          </DialogHeader>
          {versionsLoading ? (
            <div className="py-6 text-sm text-muted-foreground">Loading version history...</div>
          ) : (
            <div className="space-y-4">
              <div className="divide-y rounded-md border">
                {versions.map((version) => (
                  <div key={version.id} className="flex items-center justify-between gap-4 p-3">
                    <div>
                      <div className="font-medium">Version {version.version}</div>
                      <div className="text-xs text-muted-foreground">
                        {version.changeSummary || 'No change summary'}
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      {version.activeInstancesCount > 0 && <Badge variant="outline">{version.activeInstancesCount} live</Badge>}
                      <Badge variant={version.lifecycleStatus === WorkflowDefinitionLifecycleStatus.Published ? 'default' : 'secondary'}>
                        {lifecycleLabel(version.lifecycleStatus)}
                      </Badge>
                      <Button size="sm" variant="outline" onClick={() => void handleRollback(version.id)}>Rollback draft</Button>
                    </div>
                  </div>
                ))}
              </div>
              {versionComparison && (
                <div className="rounded-md border p-3">
                  <div className="mb-2 text-sm font-medium">
                    Version {versionComparison.fromVersion} to {versionComparison.toVersion}
                  </div>
                  {versionComparison.changes.length === 0 ? (
                    <div className="text-sm text-muted-foreground">No structural changes detected.</div>
                  ) : (
                    <ul className="space-y-1 text-sm">
                      {versionComparison.changes.map((change) => <li key={change}>• {change}</li>)}
                    </ul>
                  )}
                  {versionComparison.hasPotentiallyBreakingChanges && (
                    <div className="mt-3 rounded-md bg-amber-50 px-3 py-2 text-xs text-amber-800">
                      Review carefully: this comparison includes removed or materially changed workflow paths.
                    </div>
                  )}
                </div>
              )}
            </div>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={!!simulationResult} onOpenChange={open => { if (!open) setSimulationResult(null); }}><DialogContent className="sm:max-w-xl"><DialogHeader><DialogTitle>Simulation result</DialogTitle></DialogHeader>
        <div className="space-y-2">{(simulationResult?.path || []).map((step:any,index:number)=><div key={step.id} className="flex gap-3 rounded-md border px-3 py-2 text-sm"><span>{index+1}</span><span className="font-medium">{step.name}</span><span className="text-muted-foreground">{String(step.stepType)}</span></div>)}
          {(simulationResult?.warnings || []).map((warning:string)=><div key={warning} className="text-sm text-amber-700">{warning}</div>)}</div>
      </DialogContent></Dialog>

      {/* Delete Workflow Definition */}
      <ConfirmationDialog
        open={deleteDialogOpen}
        onOpenChange={(open) => {
          setDeleteDialogOpen(open);
          if (!open) {
            setDeleteTarget(null);
            setDeleteSaving(false);
          }
        }}
        title="Delete Workflow Definition?"
        description={
          deleteTarget ? (
            <div className="space-y-2">
              <div>
                You are about to delete <strong>{deleteTarget.name}</strong> ({deleteTarget.entityType}).
              </div>
              <div>
                This is a <strong>soft delete</strong> (archive): it will be hidden from the workflow list.
                If the workflow has ever been used (has instances), deletion is blocked to preserve history.
                {deleteTarget.activeInstancesCount > 0 ? (
                  <span> It also has <strong>{deleteTarget.activeInstancesCount}</strong> active instance(s).</span>
                ) : null}
              </div>
            </div>
          ) : undefined
        }
        confirmText={deleteSaving ? 'Deleting...' : 'Delete'}
        variant="destructive"
        onConfirm={handleConfirmDeleteWorkflow}
        isLoading={deleteSaving}
      />
    </div>
  );
}

export default function WorkflowAdministrationPage() {
  return (
    <Suspense fallback={<div className="p-6 text-sm text-muted-foreground">Loading workflow administration…</div>}>
      <WorkflowAdministrationPageInner />
    </Suspense>
  );
}
