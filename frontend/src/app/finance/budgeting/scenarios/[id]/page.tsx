'use client';

import React, { useState, useEffect, use } from 'react';
import Link from 'next/link';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { BarChart3, ChevronRight, Landmark, Lock, FileText, CheckCircle, XCircle, Clock, Plus, UserRoundPlus, Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { budgetDataService } from '@/services/finance/budget-data.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import { workflowApiService } from '@/services/workflow-api.service';
import type { BudgetScenario, BudgetReturn, BudgetAssignee, BudgetAuditEvent, CreateBudgetReturnDto } from '@/types/budget';
import type { SegmentStructure, SegmentLookupValue } from '@/types/finance';

interface PageProps {
    params: Promise<{
        id: string;
    }>;
}

export default function ScenarioDetailsPage({ params }: PageProps) {
    const { id } = use(params);
    const { toast } = useToast();
    const { hasPermission } = useAuth();
    const canMaintainBudget = hasPermission('Finance.Budgeting.Write');
    const canAssignReturns = hasPermission('Finance.BudgetReturns.Assign');
    const canLockBudget = hasPermission('Finance.Budgeting.Lock');
    const [scenario, setScenario] = useState<BudgetScenario | null>(null);
    const [returns, setReturns] = useState<BudgetReturn[]>([]);
    const [assignees, setAssignees] = useState<BudgetAssignee[]>([]);
    const [auditHistory, setAuditHistory] = useState<BudgetAuditEvent[]>([]);
    const [isLoading, setIsLoading] = useState(true);

    // Create Return Dialog State
    const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
    const [segments, setSegments] = useState<SegmentStructure[]>([]);
    const [segmentValues, setSegmentValues] = useState<SegmentLookupValue[]>([]);
    const [selectedSegmentId, setSelectedSegmentId] = useState<string>('');
    const [newReturnData, setNewReturnData] = useState<CreateBudgetReturnDto>({
        budgetScenarioId: id,
        segmentValueId: '',
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
        loadSegments();
    }, [id, canAssignReturns]);

    useEffect(() => {
        if (selectedSegmentId) {
            loadSegmentValues(selectedSegmentId);
        }
    }, [selectedSegmentId]);

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
            const [scenarioData, returnsData, assigneeData, auditData] = await Promise.all([
                budgetDataService.getScenarioById(id),
                budgetDataService.getReturns(id),
                assigneePromise,
                budgetDataService.getScenarioAuditHistory(id).catch(() => []),
            ]);
            setScenario(scenarioData);
            setReturns(returnsData);
            setAssignees(assigneeData);
            setAuditHistory(auditData);
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

    const loadSegments = async () => {
        try {
            const data = await financeDataService.getSegmentStructures();
            setSegments(data);
            // Default to the first segment if available (usually Entity or Department)
            if (data.length > 0) {
                // Try to find one named "Department" or "Cost Center", else take the second one (first is usually balancing segment)
                const deptSegment = data.find(s => s.segmentName.includes('Department') || s.segmentName.includes('Cost'));
                setSelectedSegmentId(deptSegment?.id || data[0].id);
            }
        } catch (error) {
            console.error('Failed to load segments:', error);
        }
    };

    const loadSegmentValues = async (segmentId: string) => {
        try {
            const values = await financeDataService.getSegmentLookupValues(segmentId);
            setSegmentValues(values);
        } catch (error) {
            console.error('Failed to load segment values:', error);
        }
    };

    const handleCreateReturn = async () => {
        if (!newReturnData.segmentValueId) {
            toast({
                title: 'Validation Error',
                description: 'Please select a department/segment.',
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
            setNewReturnData(prev => ({ ...prev, segmentValueId: '', assignedToUserId: undefined, notes: '' }));
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
                </div>
                <div className="flex gap-2">
                    <div className="flex gap-2">
                        <Link href={`/finance/budgeting/scenarios/${id}/consolidated`}>
                            <Button variant="outline">
                                <BarChart3 className="mr-2 h-4 w-4" />
                                Consolidated Budget
                            </Button>
                        </Link>
                        {scenario.status === 'Draft' && canMaintainBudget && (
                            <Button onClick={handleOpenScenario}>
                                Open Collection
                            </Button>
                        )}
                        {scenario.status === 'Collecting' && canLockBudget && (
                            <Button variant="outline" onClick={handleSubmitScenario}>
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
                                        <div className="space-y-2">
                                            <Label htmlFor="segment">Segment Type</Label>
                                            <Select value={selectedSegmentId} onValueChange={setSelectedSegmentId}>
                                                <SelectTrigger>
                                                    <SelectValue placeholder="Select Segment Type" />
                                                </SelectTrigger>
                                                <SelectContent>
                                                    {segments.map(s => (
                                                        <SelectItem key={s.id} value={s.id}>{s.segmentName}</SelectItem>
                                                    ))}
                                                </SelectContent>
                                            </Select>
                                        </div>
                                        <div className="space-y-2">
                                            <Label htmlFor="value">Department / Unit <span className="text-red-500">*</span></Label>
                                            <Select
                                                value={newReturnData.segmentValueId}
                                                onValueChange={(val) => setNewReturnData(prev => ({ ...prev, segmentValueId: val }))}
                                            >
                                                <SelectTrigger>
                                                    <SelectValue placeholder="Select Department" />
                                                </SelectTrigger>
                                                <SelectContent>
                                                    {segmentValues.map(v => (
                                                        <SelectItem key={v.id} value={v.id}>{v.segmentValue} - {v.description}</SelectItem>
                                                    ))}
                                                </SelectContent>
                                            </Select>
                                        </div>
                                        {canAssignReturns && (
                                            <div className="space-y-2">
                                                <Label htmlFor="assignee">Assign To</Label>
                                                <Select
                                                    value={newReturnData.assignedToUserId || 'unassigned'}
                                                    onValueChange={(value) => setNewReturnData(prev => ({
                                                        ...prev,
                                                        assignedToUserId: value === 'unassigned' ? undefined : value,
                                                    }))}
                                                >
                                                    <SelectTrigger id="assignee">
                                                        <SelectValue placeholder="Select User" />
                                                    </SelectTrigger>
                                                    <SelectContent>
                                                        <SelectItem value="unassigned">Leave unassigned</SelectItem>
                                                        {assignees.map(user => (
                                                            <SelectItem key={user.id} value={user.id}>
                                                                {user.displayName}{user.email ? ` - ${user.email}` : ''}
                                                            </SelectItem>
                                                        ))}
                                                    </SelectContent>
                                                </Select>
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
                                            {ret.segmentValueName || 'Unknown Segment'}
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
                            Select the user responsible for completing the {returnBeingAssigned?.segmentValueName || 'selected'} worksheet.
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
