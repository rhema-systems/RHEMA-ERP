'use client';

import React, { useState, useEffect, use, useMemo } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Checkbox } from '@/components/ui/checkbox';
import { BarChart3, Check, ChevronRight, ChevronsUpDown, Landmark, Lock, FileText, CheckCircle, XCircle, Clock, Plus, UserRoundPlus, Loader2, ShieldCheck, Pencil, Trash2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { budgetDataService } from '@/services/finance/budget-data.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import { workflowApiService } from '@/services/workflow-api.service';
import { cn } from '@/lib/utils';
import type { BudgetScenario, BudgetReturn, BudgetAssignee, BudgetAuditEvent, CreateBudgetReturnDto, UpdateBudgetScenarioDto } from '@/types/budget';
import type { FinanceDimensionDefinition, FiscalYear, SegmentStructure } from '@/types/finance';

interface PageProps {
    params: Promise<{
        id: string;
    }>;
}

export default function ScenarioDetailsPage({ params }: PageProps) {
    const { id } = use(params);
    const router = useRouter();
    const { toast } = useToast();
    const { hasPermission } = useAuth();
    const canMaintainBudget = hasPermission('Finance.Budgeting.Write');
    const canAssignReturns = hasPermission('Finance.BudgetReturns.Assign');
    const canApproveReturns = hasPermission('Finance.BudgetReturns.Approve');
    const canLockBudget = hasPermission('Finance.Budgeting.Lock');
    const [scenario, setScenario] = useState<BudgetScenario | null>(null);
    const [returns, setReturns] = useState<BudgetReturn[]>([]);
    const [assignees, setAssignees] = useState<BudgetAssignee[]>([]);
    const [auditHistory, setAuditHistory] = useState<BudgetAuditEvent[]>([]);
    const [isLoading, setIsLoading] = useState(true);

    // Create Return Dialog State
    const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
    const [financeDimensions, setFinanceDimensions] = useState<FinanceDimensionDefinition[]>([]);
    const [fiscalYears, setFiscalYears] = useState<FiscalYear[]>([]);
    const [segmentStructures, setSegmentStructures] = useState<SegmentStructure[]>([]);
    const [selectedSegmentStructureId, setSelectedSegmentStructureId] = useState<string>('');
    const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
    const [isSavingScenario, setIsSavingScenario] = useState(false);
    const [isDeletingScenario, setIsDeletingScenario] = useState(false);
    const [editScenarioData, setEditScenarioData] = useState<UpdateBudgetScenarioDto>({
        name: '',
        description: '',
        fiscalYearId: '',
        baseCurrencyCode: 'GHS',
        controlDimensionDefinitionIds: [],
        controlSegmentStructureIds: [],
        rowVersion: '',
    });
    const [selectedDimensionId, setSelectedDimensionId] = useState<string>('');
    const [assigneePickerOpen, setAssigneePickerOpen] = useState(false);
    const [newReturnData, setNewReturnData] = useState<CreateBudgetReturnDto>({
        budgetScenarioId: id,
        distributionDimensionValueId: undefined,
        notes: ''
    });
    const [isAssignDialogOpen, setIsAssignDialogOpen] = useState(false);
    const [returnBeingAssigned, setReturnBeingAssigned] = useState<BudgetReturn | null>(null);
    const [selectedAssigneeId, setSelectedAssigneeId] = useState('');
    const [isAssigning, setIsAssigning] = useState(false);
    const [isAdoptDialogOpen, setIsAdoptDialogOpen] = useState(false);
    const [isAdopting, setIsAdopting] = useState(false);
    const [adoptionReason, setAdoptionReason] = useState('');
    const [adoptionEffectiveDate, setAdoptionEffectiveDate] = useState(
        new Date().toISOString().slice(0, 10)
    );

    useEffect(() => {
        loadData();
    }, [id, canAssignReturns]);

    const loadData = async () => {
        try {
            setIsLoading(true);
            const assigneePromise: Promise<BudgetAssignee[]> = canAssignReturns
                ? workflowApiService.getWorkflowDirectoryUsers().then(users => users.map(user => ({
                    id: user.id,
                    displayName: `${user.firstName || ''} ${user.lastName || ''}`.trim() || user.userName || user.email || 'Unknown user',
                    email: user.email || '',
                })))
                : Promise.resolve([]);
            const [scenarioData, returnsData, assigneeData, auditData, dimensionData, segmentData, fiscalYearData] = await Promise.all([
                budgetDataService.getScenarioById(id),
                budgetDataService.getReturns(id),
                assigneePromise,
                budgetDataService.getScenarioAuditHistory(id).catch(() => []),
                financeDataService.getFinanceDimensions(true),
                financeDataService.getSegmentStructures(),
                financeDataService.getFiscalYears(),
            ]);
            const configuredSegmentValues = await Promise.all(
                scenarioData.controlSegments.map(async control => ({
                    structureId: control.accountSegmentStructureId,
                    values: await financeDataService.getSegmentLookupValues(control.accountSegmentStructureId),
                })),
            );
            const valuesByStructureId = new Map(
                configuredSegmentValues.map(item => [item.structureId, item.values]),
            );
            setScenario(scenarioData);
            setReturns(returnsData);
            setAssignees(assigneeData);
            setAuditHistory(auditData);
            setFinanceDimensions(dimensionData);
            setFiscalYears(fiscalYearData);
            setSegmentStructures(segmentData.map(segment => ({
                ...segment,
                lookupValues: valuesByStructureId.get(segment.id) ?? segment.lookupValues ?? [],
            })));
            setSelectedSegmentStructureId(current => {
                const allowedIds = scenarioData.controlSegments.map(item => item.accountSegmentStructureId);
                return allowedIds.includes(current) ? current : (allowedIds[0] || '');
            });
            setSelectedDimensionId(current => {
                const allowedIds = scenarioData.controlDimensions.map(item => item.financeDimensionDefinitionId);
                return allowedIds.includes(current) ? current : (allowedIds[0] || '');
            });
        } catch (error) {
            console.error('Failed to load scenario details:', error);
            toast({
                title: 'Error',
                description: 'Failed to load scenario details.',
                variant: 'destructive',
            });
        } finally {
            setIsLoading(false);
        }
    };

    const handleCreateReturn = async () => {
        if (scenario?.controlSegments.length && !newReturnData.segmentValueId) {
            toast({
                title: 'Validation Error',
                description: 'Please select a value for the budget-control account segment.',
                variant: 'destructive',
            });
            return;
        }
        if (scenario?.controlDimensions.length && !newReturnData.distributionDimensionValueId) {
            toast({
                title: 'Validation Error',
                description: 'Please select a value for the worksheet distribution dimension.',
                variant: 'destructive',
            });
            return;
        }

        try {
            await budgetDataService.createReturn({
                ...newReturnData,
                budgetScenarioId: id
            });
            toast({
                title: 'Success',
                description: 'Budget return created successfully.',
            });
            setIsCreateDialogOpen(false);
            setNewReturnData(prev => ({ ...prev, segmentValueId: undefined, distributionDimensionValueId: undefined, assignedToUserId: undefined, notes: '' }));
            loadData();
        } catch (error) {
            console.error('Failed to create return:', error);
            toast({
                title: 'Error',
                description: error instanceof Error ? error.message : 'Failed to create budget return.',
                variant: 'destructive',
            });
        }
    };

    const openAssignmentDialog = (budgetReturn: BudgetReturn) => {
        setReturnBeingAssigned(budgetReturn);
        setSelectedAssigneeId(budgetReturn.assignedToUserId || '');
        setIsAssignDialogOpen(true);
    };

    const handleAssignReturn = async () => {
        if (!returnBeingAssigned || !selectedAssigneeId) {
            toast({
                title: 'Validation Error',
                description: 'Please select a user.',
                variant: 'destructive',
            });
            return;
        }

        try {
            setIsAssigning(true);
            await budgetDataService.updateReturn(returnBeingAssigned.id, {
                assignedToUserId: selectedAssigneeId,
                rowVersion: returnBeingAssigned.rowVersion,
            });
            toast({
                title: 'Return assigned',
                description: 'The budget worksheet has been assigned successfully.',
            });
            setIsAssignDialogOpen(false);
            setReturnBeingAssigned(null);
            setSelectedAssigneeId('');
            await loadData();
        } catch (error) {
            toast({
                title: 'Assignment failed',
                description: error instanceof Error ? error.message : 'Failed to assign the budget worksheet.',
                variant: 'destructive',
            });
        } finally {
            setIsAssigning(false);
        }
    };

    const formatReturnDate = (budgetReturn: BudgetReturn) => {
        const value = budgetReturn.updatedAt || budgetReturn.createdAt;
        if (!value) return '—';

        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString();
    };

    const getAssigneeName = (budgetReturn: BudgetReturn) =>
        budgetReturn.assignedToUserName ||
        assignees.find(user => user.id === budgetReturn.assignedToUserId)?.displayName ||
        'Unassigned';

    const scenarioDimensions = useMemo(() => {
        const configured = new Set(
            scenario?.controlDimensions.map(item => item.financeDimensionDefinitionId) ?? [],
        );
        return financeDimensions
            .filter(dimension => configured.has(dimension.id))
            .sort((left, right) => left.displayOrder - right.displayOrder || left.code.localeCompare(right.code));
    }, [financeDimensions, scenario?.controlDimensions]);
    const selectedDimension = scenarioDimensions.find(dimension => dimension.id === selectedDimensionId);
    const distributionValues = (selectedDimension?.values ?? [])
        .filter(value => value.isActive)
        .sort((left, right) => left.displayOrder - right.displayOrder || left.code.localeCompare(right.code));
    const scenarioSegments = useMemo(() => {
        const configured = new Set(
            scenario?.controlSegments.map(item => item.accountSegmentStructureId) ?? [],
        );
        return segmentStructures
            .filter(segment => configured.has(segment.id))
            .sort((left, right) => left.segmentPosition - right.segmentPosition || left.segmentCode.localeCompare(right.segmentCode));
    }, [segmentStructures, scenario?.controlSegments]);
    const selectedSegmentStructure = scenarioSegments.find(segment => segment.id === selectedSegmentStructureId);
    const segmentValues = (selectedSegmentStructure?.lookupValues ?? [])
        .filter(value => value.isActive)
        .sort((left, right) => left.displayOrder - right.displayOrder || left.segmentValue.localeCompare(right.segmentValue));

    const handleOpenScenario = async () => {
        if (!scenario || !confirm('Open this scenario for distributed budget collection?')) return;
        try {
            await budgetDataService.openScenario(id, scenario.rowVersion);
            toast({ title: 'Collection opened', description: 'Returns can now be created and completed.' });
            await loadData();
        } catch (error) {
            toast({ title: 'Unable to open collection', description: error instanceof Error ? error.message : 'Refresh and try again.', variant: 'destructive' });
        }
    };

    const handleSubmitScenario = async () => {
        if (!scenario || !confirm('Submit the completed scenario for approval? Collection will become read-only.')) return;
        try {
            await budgetDataService.submitScenario(id, scenario.rowVersion);
            toast({ title: 'Scenario submitted', description: 'The scenario is now in the Finance approval queue.' });
            await loadData();
        } catch (error) {
            toast({ title: 'Unable to submit scenario', description: error instanceof Error ? error.message : 'Refresh and try again.', variant: 'destructive' });
        }
    };

    const handleArchiveScenario = async () => {
        if (!scenario || !confirm('Archive this approved budget scenario?')) return;
        try {
            await budgetDataService.archiveScenario(id, scenario.rowVersion);
            toast({ title: 'Scenario archived' });
            await loadData();
        } catch (error) {
            toast({ title: 'Unable to archive scenario', description: error instanceof Error ? error.message : 'Refresh and try again.', variant: 'destructive' });
        }
    };

    const openEditScenarioDialog = () => {
        if (!scenario) return;
        setEditScenarioData({
            name: scenario.name,
            description: scenario.description || '',
            fiscalYearId: scenario.fiscalYearId,
            baseCurrencyCode: scenario.baseCurrencyCode,
            controlDimensionDefinitionIds: scenario.controlDimensions.map(item => item.financeDimensionDefinitionId),
            controlSegmentStructureIds: scenario.controlSegments.map(item => item.accountSegmentStructureId),
            rowVersion: scenario.rowVersion,
        });
        setIsEditDialogOpen(true);
    };

    const handleSaveScenario = async () => {
        if (!scenario || !editScenarioData.name.trim() || !editScenarioData.fiscalYearId) {
            toast({ title: 'Validation Error', description: 'Scenario name and fiscal year are required.', variant: 'destructive' });
            return;
        }
        try {
            setIsSavingScenario(true);
            await budgetDataService.updateScenario(id, editScenarioData);
            toast({ title: 'Scenario updated', description: 'The unused Draft scenario setup was saved.' });
            setIsEditDialogOpen(false);
            await loadData();
        } catch (error) {
            toast({ title: 'Unable to update scenario', description: error instanceof Error ? error.message : 'Refresh and try again.', variant: 'destructive' });
        } finally {
            setIsSavingScenario(false);
        }
    };

    const handleDeleteScenario = async () => {
        if (!scenario || !confirm(`Delete unused Draft scenario "${scenario.name}"? This action is audited.`)) return;
        try {
            setIsDeletingScenario(true);
            await budgetDataService.deleteScenario(id, scenario.rowVersion);
            toast({ title: 'Scenario deleted', description: 'The unused Draft scenario was removed from active use.' });
            router.push('/finance/budgeting/scenarios');
        } catch (error) {
            toast({ title: 'Unable to delete scenario', description: error instanceof Error ? error.message : 'Refresh and try again.', variant: 'destructive' });
            setIsDeletingScenario(false);
        }
    };

    const handleAdoptScenario = async () => {
        if (!scenario || !adoptionReason.trim() || !adoptionEffectiveDate) {
            toast({
                title: 'Adoption details required',
                description: 'Enter the effective date and reason for adopting this official budget.',
                variant: 'destructive',
            });
            return;
        }
        try {
            setIsAdopting(true);
            await budgetDataService.adoptScenario(id, {
                rowVersion: scenario.rowVersion,
                effectiveDate: adoptionEffectiveDate,
                reason: adoptionReason.trim(),
            });
            toast({
                title: 'Official budget adopted',
                description: `${scenario.name} is now the reporting baseline for this fiscal year.`,
            });
            setIsAdoptDialogOpen(false);
            setAdoptionReason('');
            await loadData();
        } catch (error) {
            toast({
                title: 'Unable to adopt scenario',
                description: error instanceof Error ? error.message : 'Refresh and try again.',
                variant: 'destructive',
            });
        } finally {
            setIsAdopting(false);
        }
    };

    const getStatusBadge = (status: string) => {
        const variants: Record<string, string> = {
            Draft: 'bg-gray-100 text-gray-800',
            Submitted: 'bg-blue-100 text-blue-800',
            Approved: 'bg-green-100 text-green-800',
            Rejected: 'bg-red-100 text-red-800',
        };
        const icons: Record<string, any> = {
            Draft: FileText,
            Submitted: Clock,
            Approved: CheckCircle,
            Rejected: XCircle,
        };
        const Icon = icons[status] || FileText;
        return (
            <span className={`flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium ${variants[status] || 'bg-gray-100'}`}>
                <Icon className="w-3 h-3" />
                {status}
            </span>
        );
    };

    if (isLoading) return <div className="p-8 text-center">Loading scenario details...</div>;
    if (!scenario) return <div className="p-8 text-center text-red-500">Scenario not found.</div>;
    const approvedReturnCount = returns.filter(item => item.status === 'Approved').length;
    const submittedReturnCount = returns.filter(item => item.status === 'Submitted').length;
    const scenarioReadyForSubmission = returns.length > 0 && approvedReturnCount === returns.length;
    const canManageUnusedDraft = canMaintainBudget && scenario.status === 'Draft' && returns.length === 0;

    return (
        <div className="space-y-6">
            {/* Breadcrumbs */}
            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/finance/budgeting/scenarios">Budget Scenarios</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>{scenario.name}</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Header */}
            <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">{scenario.name}</h1>
                    <div className="flex items-center gap-2 mt-2 text-muted-foreground">
                        <span>{scenario.baseCurrencyCode}</span>
                        <span>•</span>
                        <span>{returns.length} Returns</span>
                        <span>•</span>
                        <Badge variant={scenario.status === 'Archived' ? 'secondary' : 'default'} className="ml-2">
                            {(scenario.status === 'Approved' || scenario.status === 'Superseded' || scenario.status === 'Archived') && <Lock className="w-3 h-3 mr-1" />}
                            {scenario.status}
                        </Badge>
                        {scenario.isActive && (
                            <Badge className="bg-green-600 hover:bg-green-700">Official</Badge>
                        )}
                    </div>
                    <div className="mt-2 flex flex-wrap items-center gap-1">
                        <span className="mr-1 text-xs text-muted-foreground">Budget grain:</span>
                        {scenario.controlSegments.length === 0 && scenario.controlDimensions.length === 0 ? (
                            <Badge variant="outline">Account + fiscal period</Badge>
                        ) : <>
                            {[...scenario.controlSegments]
                                .sort((left, right) => left.displayOrder - right.displayOrder)
                                .map(segment => (
                                    <Badge key={segment.accountSegmentStructureId} variant="outline">
                                        Segment: {segment.segmentCode} — {segment.segmentName}
                                    </Badge>
                                ))}
                            {[...scenario.controlDimensions]
                                .sort((left, right) => left.displayOrder - right.displayOrder)
                                .map(dimension => (
                                <Badge key={dimension.financeDimensionDefinitionId} variant="outline">
                                    Dimension: {dimension.dimensionCode} — {dimension.dimensionName}
                                </Badge>
                                ))}
                        </>}
                    </div>
                </div>
                <div className="flex gap-2">
                    <div className="flex gap-2">
                        <Link href={`/finance/budgeting/scenarios/${id}/consolidated`}>
                            <Button variant="outline">
                                <BarChart3 className="mr-2 h-4 w-4" />
                                Consolidated Budget
                            </Button>
                        </Link>
                        {canManageUnusedDraft && (
                            <Button variant="outline" onClick={openEditScenarioDialog}>
                                <Pencil className="mr-2 h-4 w-4" />
                                Edit Draft
                            </Button>
                        )}
                        {canManageUnusedDraft && (
                            <Button variant="outline" onClick={handleDeleteScenario} disabled={isDeletingScenario} className="text-destructive">
                                {isDeletingScenario ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Trash2 className="mr-2 h-4 w-4" />}
                                Delete Draft
                            </Button>
                        )}
                        {scenario.status === 'Draft' && canMaintainBudget && (
                            <Button onClick={handleOpenScenario}>
                                Open Collection
                            </Button>
                        )}
                        {scenario.status === 'Collecting' && canLockBudget && (
                            <Button
                                variant="outline"
                                onClick={handleSubmitScenario}
                                disabled={!scenarioReadyForSubmission}
                                title={scenarioReadyForSubmission
                                    ? 'Submit the approved scenario to the Finance approval workflow.'
                                    : 'Every budget return must be approved before the scenario can be submitted.'}
                            >
                                <Lock className="mr-2 h-4 w-4" />
                                Submit Scenario
                            </Button>
                        )}
                        {(scenario.status === 'Approved' || scenario.status === 'Superseded') && !scenario.isActive && canLockBudget && (
                            <Button onClick={() => setIsAdoptDialogOpen(true)}>
                                <Landmark className="mr-2 h-4 w-4" />
                                Adopt as Official
                            </Button>
                        )}
                        {(scenario.status === 'Approved' || scenario.status === 'Superseded') && canLockBudget && (
                            <Button variant="outline" onClick={handleArchiveScenario}>
                                Archive Scenario
                            </Button>
                        )}
                        {scenario.status === 'Collecting' && canMaintainBudget && <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
                                <DialogTrigger asChild>
                                    <Button>
                                        <Plus className="mr-2 h-4 w-4" />
                                        Create Return
                                    </Button>
                                </DialogTrigger>
                                <DialogContent>
                                    <DialogHeader>
                                        <DialogTitle>Create Budget Return</DialogTitle>
                                        <DialogDescription>
                                            Assign a budget worksheet to a department or segment.
                                        </DialogDescription>
                                    </DialogHeader>
                                    <div className="space-y-4 py-4">
                                        {scenarioSegments.length > 0 && <>
                                            <div className="space-y-2">
                                                <Label htmlFor="segmentStructure">Account segment type</Label>
                                                <Select
                                                    value={selectedSegmentStructureId}
                                                    onValueChange={(value) => {
                                                        setSelectedSegmentStructureId(value);
                                                        setNewReturnData(current => ({ ...current, segmentValueId: undefined }));
                                                    }}
                                                >
                                                    <SelectTrigger id="segmentStructure">
                                                        <SelectValue placeholder="Select a scenario segment" />
                                                    </SelectTrigger>
                                                    <SelectContent>
                                                        {scenarioSegments.map(segment => (
                                                            <SelectItem key={segment.id} value={segment.id}>
                                                                {segment.segmentCode} — {segment.segmentName}
                                                            </SelectItem>
                                                        ))}
                                                    </SelectContent>
                                                </Select>
                                            </div>
                                            <div className="space-y-2">
                                                <Label htmlFor="segmentValue">
                                                    {selectedSegmentStructure?.segmentName ?? 'Segment value'} <span className="text-red-500">*</span>
                                                </Label>
                                                <Select
                                                    value={newReturnData.segmentValueId}
                                                    onValueChange={(value) => setNewReturnData(current => ({ ...current, segmentValueId: value }))}
                                                    disabled={!selectedSegmentStructure || segmentValues.length === 0}
                                                >
                                                    <SelectTrigger id="segmentValue">
                                                        <SelectValue placeholder={selectedSegmentStructure ? `Select ${selectedSegmentStructure.segmentName}` : 'Select a segment type first'} />
                                                    </SelectTrigger>
                                                    <SelectContent>
                                                        {segmentValues.map(value => (
                                                            <SelectItem key={value.id} value={value.id}>
                                                                {value.segmentValue} — {value.description}
                                                            </SelectItem>
                                                        ))}
                                                    </SelectContent>
                                                </Select>
                                                {selectedSegmentStructure && segmentValues.length === 0 && (
                                                    <p className="text-sm text-destructive">
                                                        No active values are configured for {selectedSegmentStructure.segmentName}.
                                                    </p>
                                                )}
                                            </div>
                                        </>}
                                        <div className="space-y-2">
                                            <Label htmlFor="distributionDimension">Worksheet distribution dimension</Label>
                                            <Select
                                                value={selectedDimensionId}
                                                onValueChange={(value) => {
                                                    setSelectedDimensionId(value);
                                                    setNewReturnData(current => ({ ...current, distributionDimensionValueId: '' }));
                                                }}
                                            >
                                                <SelectTrigger>
                                                    <SelectValue placeholder="Select a scenario dimension" />
                                                </SelectTrigger>
                                                <SelectContent>
                                                    {scenarioDimensions.map(dimension => (
                                                        <SelectItem key={dimension.id} value={dimension.id}>
                                                            {dimension.code} — {dimension.name}
                                                        </SelectItem>
                                                    ))}
                                                </SelectContent>
                                            </Select>
                                        </div>
                                        <div className="space-y-2">
                                            <Label htmlFor="value">
                                                {selectedDimension?.name ?? 'Distribution value'}
                                                {scenarioDimensions.length > 0 && <span className="text-red-500"> *</span>}
                                            </Label>
                                            <Select
                                                value={newReturnData.distributionDimensionValueId}
                                                onValueChange={(val) => setNewReturnData(prev => ({ ...prev, distributionDimensionValueId: val }))}
                                                disabled={!selectedDimension || distributionValues.length === 0}
                                            >
                                                <SelectTrigger>
                                                    <SelectValue placeholder={selectedDimension ? `Select ${selectedDimension.name}` : 'Select a scenario dimension first'} />
                                                </SelectTrigger>
                                                <SelectContent>
                                                    {distributionValues.map(value => (
                                                        <SelectItem key={value.id} value={value.id}>{value.code} — {value.name}</SelectItem>
                                                    ))}
                                                </SelectContent>
                                            </Select>
                                            {selectedDimension && distributionValues.length === 0 && (
                                                <p className="text-sm text-destructive">
                                                    No active values are configured for {selectedDimension.name}.
                                                </p>
                                            )}
                                        </div>
                                        {canAssignReturns && (
                                            <div className="space-y-2">
                                                <Label htmlFor="assignee">Assign To</Label>
                                                <Popover modal open={assigneePickerOpen} onOpenChange={setAssigneePickerOpen}>
                                                    <PopoverTrigger asChild>
                                                        <Button
                                                            id="assignee"
                                                            type="button"
                                                            variant="outline"
                                                            role="combobox"
                                                            aria-expanded={assigneePickerOpen}
                                                            className="w-full justify-between font-normal"
                                                        >
                                                            <span className="truncate text-left">
                                                                {newReturnData.assignedToUserId
                                                                    ? (() => {
                                                                        const assignee = assignees.find(user => user.id === newReturnData.assignedToUserId);
                                                                        return assignee
                                                                            ? `${assignee.displayName}${assignee.email ? ` - ${assignee.email}` : ''}`
                                                                            : 'Select User';
                                                                    })()
                                                                    : 'Leave unassigned'}
                                                            </span>
                                                            <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                                        </Button>
                                                    </PopoverTrigger>
                                                    <PopoverContent className="w-[var(--radix-popover-trigger-width)] p-0" align="start">
                                                        <Command>
                                                            <CommandInput placeholder="Search name or email..." />
                                                            <CommandList>
                                                                <CommandEmpty>No eligible user found.</CommandEmpty>
                                                                <CommandGroup>
                                                                    <CommandItem
                                                                        value="leave unassigned"
                                                                        onSelect={() => {
                                                                            setNewReturnData(prev => ({ ...prev, assignedToUserId: undefined }));
                                                                            setAssigneePickerOpen(false);
                                                                        }}
                                                                    >
                                                                        <Check className={cn('mr-2 h-4 w-4', !newReturnData.assignedToUserId ? 'opacity-100' : 'opacity-0')} />
                                                                        Leave unassigned
                                                                    </CommandItem>
                                                                    {assignees.map(user => (
                                                                        <CommandItem
                                                                            key={user.id}
                                                                            value={`${user.displayName} ${user.email ?? ''}`.trim()}
                                                                            onSelect={() => {
                                                                                setNewReturnData(prev => ({ ...prev, assignedToUserId: user.id }));
                                                                                setAssigneePickerOpen(false);
                                                                            }}
                                                                        >
                                                                            <Check className={cn('mr-2 h-4 w-4', user.id === newReturnData.assignedToUserId ? 'opacity-100' : 'opacity-0')} />
                                                                            <span className="truncate">
                                                                                {user.displayName}{user.email ? ` - ${user.email}` : ''}
                                                                            </span>
                                                                        </CommandItem>
                                                                    ))}
                                                                </CommandGroup>
                                                            </CommandList>
                                                        </Command>
                                                    </PopoverContent>
                                                </Popover>
                                            </div>
                                        )}
                                        <div className="space-y-2">
                                            <Label htmlFor="notes">Notes</Label>
                                            <Input
                                                id="notes"
                                                placeholder="Instructions for the user..."
                                                value={newReturnData.notes || ''}
                                                onChange={(e) => setNewReturnData(prev => ({ ...prev, notes: e.target.value }))}
                                            />
                                        </div>
                                    </div>
                                    <DialogFooter>
                                        <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>Cancel</Button>
                                        <Button onClick={handleCreateReturn}>Create</Button>
                                    </DialogFooter>
                                </DialogContent>
                        </Dialog>}
                    </div>
                </div>
            </div>

            {scenario.status === 'Collecting' && (
                <div className="flex flex-col gap-3 rounded-lg border border-blue-200 bg-blue-50/60 p-4 text-sm text-blue-950 md:flex-row md:items-center md:justify-between">
                    <div>
                        <p className="font-semibold">Review submitted returns before submitting the scenario</p>
                        <p className="mt-1 text-blue-800">
                            {approvedReturnCount} of {returns.length} returns approved
                            {submittedReturnCount > 0 ? ` · ${submittedReturnCount} awaiting approval` : ''}.
                            {' '}Submit Scenario becomes available only after every return is approved.
                        </p>
                    </div>
                    {submittedReturnCount > 0 && canApproveReturns && (
                        <Button variant="outline" asChild className="border-blue-300 bg-white">
                            <Link href="/finance/approvals">
                                <ShieldCheck className="mr-2 h-4 w-4" />
                                Review in Approval Inbox
                            </Link>
                        </Button>
                    )}
                </div>
            )}

            {/* Returns List */}
            <Card>
                <CardHeader>
                    <CardTitle>Budget Returns ({returns.length})</CardTitle>
                    <CardDescription>
                        Distributed budget worksheets for departments
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHead>Department / Unit</TableHead>
                                <TableHead>Assigned To</TableHead>
                                <TableHead>Status</TableHead>
                                <TableHead>Last Updated</TableHead>
                                <TableHead className="text-right">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {returns.length === 0 ? (
                                <TableRow>
                                    <TableCell colSpan={5} className="text-center py-8 text-muted-foreground">
                                        No returns created yet. Click "Create Return" to distribute the budget.
                                    </TableCell>
                                </TableRow>
                            ) : (
                                returns.map((ret) => (
                                    <TableRow key={ret.id}>
                                        <TableCell className="font-medium">
                                            <div>{ret.distributionDimensionName || 'Unassigned distribution'}</div>
                                            {ret.segmentValueName && (
                                                <div className="text-xs font-normal text-muted-foreground">
                                                    {ret.segmentStructureCode || 'Segment'}: {ret.segmentValueName}
                                                </div>
                                            )}
                                        </TableCell>
                                        <TableCell>{getAssigneeName(ret)}</TableCell>
                                        <TableCell>{getStatusBadge(ret.status)}</TableCell>
                                        <TableCell>{formatReturnDate(ret)}</TableCell>
                                        <TableCell className="text-right">
                                            <div className="flex justify-end gap-2">
                                                {canAssignReturns && scenario.status === 'Collecting' && (ret.status === 'Draft' || ret.status === 'Rejected') && (
                                                    <Button size="sm" variant="outline" onClick={() => openAssignmentDialog(ret)}>
                                                        <UserRoundPlus className="mr-2 h-4 w-4" />
                                                        {ret.assignedToUserId ? 'Reassign' : 'Assign'}
                                                    </Button>
                                                )}
                                                {ret.status === 'Submitted' && canApproveReturns && (
                                                    <Button size="sm" asChild>
                                                        <Link href="/finance/approvals">
                                                            <ShieldCheck className="mr-2 h-4 w-4" />
                                                            Review Approval
                                                        </Link>
                                                    </Button>
                                                )}
                                                <Link href={`/finance/budgeting/returns/${ret.id}`}>
                                                    <Button size="sm" variant="outline">
                                                        Open Worksheet
                                                        <ChevronRight className="ml-2 h-4 w-4" />
                                                    </Button>
                                                </Link>
                                            </div>
                                        </TableCell>
                                    </TableRow>
                                ))
                            )}
                        </TableBody>
                    </Table>
                </CardContent>
            </Card>

            {(scenario.isActive || scenario.status === 'Superseded') && (
                <Card>
                    <CardHeader>
                        <CardTitle>{scenario.isActive ? 'Official Budget Baseline' : 'Superseded Baseline'}</CardTitle>
                        <CardDescription>
                            {scenario.isActive
                                ? 'This scenario is used by standard budget-versus-actual reporting.'
                                : 'This scenario remains available for audit and comparison but is no longer the official baseline.'}
                        </CardDescription>
                    </CardHeader>
                    <CardContent className="grid gap-4 md:grid-cols-3 text-sm">
                        <div>
                            <p className="text-muted-foreground">Effective date</p>
                            <p className="font-medium">
                                {scenario.adoptionEffectiveDate
                                    ? new Date(scenario.adoptionEffectiveDate).toLocaleDateString()
                                    : '—'}
                            </p>
                        </div>
                        <div>
                            <p className="text-muted-foreground">Adopted by</p>
                            <p className="font-medium">{scenario.adoptedByUserName || '—'}</p>
                        </div>
                        <div>
                            <p className="text-muted-foreground">Reason</p>
                            <p className="font-medium">{scenario.adoptionReason || '—'}</p>
                        </div>
                    </CardContent>
                </Card>
            )}

            <Card>
                <CardHeader>
                    <CardTitle>Audit History</CardTitle>
                    <CardDescription>Scenario lifecycle and workflow events</CardDescription>
                </CardHeader>
                <CardContent className="space-y-3">
                    {auditHistory.length === 0 ? (
                        <p className="text-sm text-muted-foreground">No audit events recorded yet.</p>
                    ) : auditHistory.map(event => (
                        <div key={event.id} className="flex items-start justify-between gap-4 border-b pb-3 last:border-b-0">
                            <div>
                                <p className="font-medium">{event.action.replace('Finance.', '')}</p>
                                <p className="text-sm text-muted-foreground">{event.username}</p>
                            </div>
                            <time className="text-sm text-muted-foreground">
                                {new Date(event.timestamp).toLocaleString()}
                            </time>
                        </div>
                    ))}
                </CardContent>
            </Card>

            <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
                <DialogContent className="flex max-h-[calc(100vh-2rem)] max-w-2xl flex-col overflow-hidden">
                    <DialogHeader className="shrink-0">
                        <DialogTitle>Edit unused Draft scenario</DialogTitle>
                        <DialogDescription>
                            Structural setup can change only while the Draft has no returns or other dependent evidence.
                        </DialogDescription>
                    </DialogHeader>
                    <div className="min-h-0 flex-1 space-y-4 overflow-y-auto py-4 pr-2">
                        <div className="space-y-2">
                            <Label htmlFor="edit-scenario-name">Scenario name</Label>
                            <Input id="edit-scenario-name" value={editScenarioData.name}
                                onChange={event => setEditScenarioData(current => ({ ...current, name: event.target.value }))} />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="edit-scenario-description">Description</Label>
                            <Textarea id="edit-scenario-description" value={editScenarioData.description || ''}
                                onChange={event => setEditScenarioData(current => ({ ...current, description: event.target.value }))} />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="edit-scenario-year">Fiscal year</Label>
                            <Select value={editScenarioData.fiscalYearId}
                                onValueChange={value => setEditScenarioData(current => ({ ...current, fiscalYearId: value }))}>
                                <SelectTrigger id="edit-scenario-year"><SelectValue placeholder="Select fiscal year" /></SelectTrigger>
                                <SelectContent>
                                    {fiscalYears.map(year => (
                                        <SelectItem key={year.id} value={year.id}>{year.fiscalYearName} ({year.fiscalYearCode})</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="edit-scenario-currency">Base currency</Label>
                            <Input id="edit-scenario-currency" maxLength={3} value={editScenarioData.baseCurrencyCode || ''}
                                onChange={event => setEditScenarioData(current => ({ ...current, baseCurrencyCode: event.target.value.toUpperCase() }))} />
                        </div>
                        <div className="space-y-3 rounded-md border p-3">
                            <div>
                                <Label>Budget-control account segments</Label>
                                <p className="text-xs text-muted-foreground">Return creators choose one value from an enabled structure.</p>
                            </div>
                            {segmentStructures.filter(segment => segment.isActive && segment.lookupTableRequired && !segment.isNaturalAccount).map(segment => {
                                const selected = editScenarioData.controlSegmentStructureIds?.includes(segment.id) ?? false;
                                return (
                                    <label key={segment.id} className="flex items-start gap-3 rounded border p-2">
                                        <Checkbox checked={selected} onCheckedChange={checked => setEditScenarioData(current => ({
                                            ...current,
                                            controlSegmentStructureIds: checked
                                                ? [...(current.controlSegmentStructureIds ?? []), segment.id]
                                                : (current.controlSegmentStructureIds ?? []).filter(item => item !== segment.id),
                                        }))} />
                                        <span className="text-sm"><span className="font-medium">{segment.segmentCode} — {segment.segmentName}</span></span>
                                    </label>
                                );
                            })}
                        </div>
                        <div className="space-y-3 rounded-md border p-3">
                            <div>
                                <Label>Budget-control Finance dimensions</Label>
                                <p className="text-xs text-muted-foreground">Every worksheet cell supplies one value for each enabled dimension.</p>
                            </div>
                            {financeDimensions.filter(dimension => dimension.isActive && dimension.classification !== 'Derived').map(dimension => {
                                const selected = editScenarioData.controlDimensionDefinitionIds?.includes(dimension.id) ?? false;
                                return (
                                    <label key={dimension.id} className="flex items-start gap-3 rounded border p-2">
                                        <Checkbox checked={selected} onCheckedChange={checked => setEditScenarioData(current => ({
                                            ...current,
                                            controlDimensionDefinitionIds: checked
                                                ? [...(current.controlDimensionDefinitionIds ?? []), dimension.id]
                                                : (current.controlDimensionDefinitionIds ?? []).filter(item => item !== dimension.id),
                                        }))} />
                                        <span className="text-sm"><span className="font-medium">{dimension.code} — {dimension.name}</span></span>
                                    </label>
                                );
                            })}
                        </div>
                    </div>
                    <DialogFooter className="shrink-0 border-t pt-4">
                        <Button variant="outline" onClick={() => setIsEditDialogOpen(false)} disabled={isSavingScenario}>Cancel</Button>
                        <Button onClick={handleSaveScenario} disabled={isSavingScenario}>
                            {isSavingScenario && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Save changes
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            <Dialog open={isAssignDialogOpen} onOpenChange={(open) => {
                setIsAssignDialogOpen(open);
                if (!open) {
                    setReturnBeingAssigned(null);
                    setSelectedAssigneeId('');
                }
            }}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Assign Budget Return</DialogTitle>
                        <DialogDescription>
                            Select the user responsible for completing the {returnBeingAssigned?.distributionDimensionName || returnBeingAssigned?.segmentValueName || 'selected'} worksheet.
                        </DialogDescription>
                    </DialogHeader>
                    <div className="space-y-2 py-4">
                        <Label htmlFor="return-assignee">Assigned To</Label>
                        <Select value={selectedAssigneeId} onValueChange={setSelectedAssigneeId}>
                            <SelectTrigger id="return-assignee">
                                <SelectValue placeholder="Select User" />
                            </SelectTrigger>
                            <SelectContent>
                                {assignees.map(user => (
                                    <SelectItem key={user.id} value={user.id}>
                                        {user.displayName}{user.email ? ` - ${user.email}` : ''}
                                    </SelectItem>
                                ))}
                            </SelectContent>
                        </Select>
                        {assignees.length === 0 && (
                            <p className="text-sm text-muted-foreground">No active users are available in this tenant.</p>
                        )}
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setIsAssignDialogOpen(false)} disabled={isAssigning}>
                            Cancel
                        </Button>
                        <Button onClick={handleAssignReturn} disabled={isAssigning || !selectedAssigneeId}>
                            {isAssigning && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Assign Return
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            <Dialog open={isAdoptDialogOpen} onOpenChange={setIsAdoptDialogOpen}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Adopt as Official Budget</DialogTitle>
                        <DialogDescription>
                            This makes {scenario.name} the single reporting baseline for the fiscal year.
                            Any current official budget will be marked Superseded.
                        </DialogDescription>
                    </DialogHeader>
                    <div className="space-y-4 py-4">
                        <div className="space-y-2">
                            <Label htmlFor="adoption-effective-date">Effective date</Label>
                            <Input
                                id="adoption-effective-date"
                                type="date"
                                value={adoptionEffectiveDate}
                                onChange={(event) => setAdoptionEffectiveDate(event.target.value)}
                            />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="adoption-reason">Adoption reason</Label>
                            <Textarea
                                id="adoption-reason"
                                value={adoptionReason}
                                onChange={(event) => setAdoptionReason(event.target.value)}
                                placeholder="Document the approval, board resolution, or revision being adopted."
                                rows={4}
                            />
                        </div>
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setIsAdoptDialogOpen(false)} disabled={isAdopting}>
                            Cancel
                        </Button>
                        <Button
                            onClick={handleAdoptScenario}
                            disabled={isAdopting || !adoptionEffectiveDate || !adoptionReason.trim()}
                        >
                            {isAdopting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Adopt Official Budget
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}
