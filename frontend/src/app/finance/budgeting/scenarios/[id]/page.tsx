'use client';

import React, { useState, useEffect, use } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { ChevronRight, ArrowLeft, Lock, Unlock, FileText, CheckCircle, XCircle, Clock, Plus } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { budgetDataService } from '@/services/finance/budget-data.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { BudgetScenario, BudgetReturn, CreateBudgetReturnDto } from '@/types/budget';
import type { SegmentStructure, SegmentLookupValue } from '@/types/finance';

interface PageProps {
    params: Promise<{
        id: string;
    }>;
}

export default function ScenarioDetailsPage({ params }: PageProps) {
    const { id } = use(params);
    const { toast } = useToast();
    const router = useRouter();
    const [scenario, setScenario] = useState<BudgetScenario | null>(null);
    const [returns, setReturns] = useState<BudgetReturn[]>([]);
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

    useEffect(() => {
        loadData();
        loadSegments();
    }, [id]);

    useEffect(() => {
        if (selectedSegmentId) {
            loadSegmentValues(selectedSegmentId);
        }
    }, [selectedSegmentId]);

    const loadData = async () => {
        try {
            setIsLoading(true);
            const [scenarioData, returnsData] = await Promise.all([
                budgetDataService.getScenarioById(id),
                budgetDataService.getReturns(id)
            ]);
            setScenario(scenarioData);
            setReturns(returnsData);
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
            setNewReturnData(prev => ({ ...prev, segmentValueId: '', notes: '' }));
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

    const handleLockScenario = async () => {
        if (!confirm('Are you sure you want to lock this budget? This action cannot be easily undone.')) return;
        try {
            await budgetDataService.lockScenario(id);
            toast({ title: 'Success', description: 'Budget scenario locked.' });
            loadData();
        } catch (error) {
            toast({ title: 'Error', description: 'Failed to lock scenario.', variant: 'destructive' });
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
                        <Badge variant={scenario.status === 'Locked' ? 'destructive' : 'default'} className="ml-2">
                            {scenario.status === 'Locked' && <Lock className="w-3 h-3 mr-1" />}
                            {scenario.status}
                        </Badge>
                    </div>
                </div>
                <div className="flex gap-2">
                    {scenario.status !== 'Locked' && (
                        <div className="flex gap-2">
                            <Button variant="outline" className="text-destructive hover:bg-destructive/10" onClick={handleLockScenario}>
                                <Lock className="mr-2 h-4 w-4" />
                                Lock Budget
                            </Button>
                            <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
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
                            </Dialog>
                        </div>
                    )}
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
                                        <TableCell>{ret.assignedToUserName || 'Unassigned'}</TableCell>
                                        <TableCell>{getStatusBadge(ret.status)}</TableCell>
                                        <TableCell>{new Date(ret.updatedAt || ret.createdAt).toLocaleDateString()}</TableCell>
                                        <TableCell className="text-right">
                                            <Link href={`/finance/budgeting/returns/${ret.id}`}>
                                                <Button size="sm" variant="outline">
                                                    Open Worksheet
                                                    <ChevronRight className="ml-2 h-4 w-4" />
                                                </Button>
                                            </Link>
                                        </TableCell>
                                    </TableRow>
                                ))
                            )}
                        </TableBody>
                    </Table>
                </CardContent>
            </Card>
        </div>
    );
}
