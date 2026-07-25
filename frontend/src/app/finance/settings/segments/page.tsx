'use client';

import React, { useState, useEffect, Suspense } from 'react';
import { useSearchParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Layers, Plus, Edit, Trash2, List, Settings2, Upload, FileSpreadsheet, Download, ArrowUp, ArrowDown } from 'lucide-react';
import type { SegmentStructure, SegmentLookupValue } from '@/types/finance';
import { financeDataService } from '@/services/finance/finance-data.service';
import { toast } from 'sonner';



function SegmentConfigurationContent() {
    const [segments, setSegments] = useState<SegmentStructure[]>([]);
    const [selectedSegmentId, setSelectedSegmentId] = useState<string>('');
    const [lookupValues, setLookupValues] = useState<Record<string, SegmentLookupValue[]>>({});
    const [isLoading, setIsLoading] = useState(true);

    // Dialog states
    const [isSegmentDialogOpen, setIsSegmentDialogOpen] = useState(false);
    const [isValueDialogOpen, setIsValueDialogOpen] = useState(false);
    const [isUploadDialogOpen, setIsUploadDialogOpen] = useState(false);
    const [editingSegment, setEditingSegment] = useState<SegmentStructure | null>(null);
    const [lookupValueToDelete, setLookupValueToDelete] = useState<SegmentLookupValue | null>(null);
    const [isDeletingLookupValue, setIsDeletingLookupValue] = useState(false);
    const [valueFormError, setValueFormError] = useState<string | null>(null);

    // Reorder confirmation dialog state
    const [reorderConfirmOpen, setReorderConfirmOpen] = useState(false);
    const [pendingReorder, setPendingReorder] = useState<{ segmentId: string; direction: 'up' | 'down' } | null>(null);

    React.useEffect(() => {
        loadData();
    }, []);

    const loadData = async () => {
        try {
            setIsLoading(true);
            const data = await financeDataService.getSegmentStructures();
            // Sort by position
            data.sort((a, b) => a.segmentPosition - b.segmentPosition);
            setSegments(data);
            if (data.length > 0 && !selectedSegmentId) {
                const firstId = data[0].id;
                setSelectedSegmentId(firstId);
                // Trigger fetch for first item
                // The useEffect below will handle it if we set state here? 
                // Careful with strict mode double trigger, but it's fine.
            }
        } catch (error) {
            console.error('Failed to load segments:', error);
            toast.error('Failed to load segments');
        } finally {
            setIsLoading(false);
        }
    };

    const fetchLookupValues = async (segmentId: string) => {
        if (!segmentId) return;
        // Check if we already have it? Maybe force refresh is better.
        try {
            const values = await financeDataService.getSegmentLookupValues(segmentId);
            setLookupValues(prev => ({
                ...prev,
                [segmentId]: values
            }));
        } catch (error) {
            console.error(`Failed to load values for segment ${segmentId}:`, error);
        }
    };

    React.useEffect(() => {
        if (selectedSegmentId) {
            fetchLookupValues(selectedSegmentId);
        }
    }, [selectedSegmentId]);

    const handleRegenerate = async () => {
        try {
            setIsLoading(true);
            await financeDataService.regenerateAccountNumbers();
            toast.success('Account numbers regenerated');
        } catch (error) {
            console.error('Regeneration failed:', error);
            toast.error('Failed to regenerate account numbers');
        } finally {
            setIsLoading(false);
        }
    };

    // Show confirmation dialog before reordering
    const handleReorderClick = (segmentId: string, direction: 'up' | 'down') => {
        const currentIndex = segments.findIndex(s => s.id === segmentId);
        if (currentIndex === -1) return;

        const newIndex = direction === 'up' ? currentIndex - 1 : currentIndex + 1;
        if (newIndex < 0 || newIndex >= segments.length) {
            console.log('Reorder blocked: Index out of bounds');
            return;
        }

        // Store pending reorder and show confirmation
        setPendingReorder({ segmentId, direction });
        setReorderConfirmOpen(true);
    };

    // Execute the actual reorder after confirmation
    const handleReorderConfirm = async () => {
        if (!pendingReorder) return;

        const { segmentId, direction } = pendingReorder;
        console.log(`Reordering segment ${segmentId} ${direction}`);

        const currentIndex = segments.findIndex(s => s.id === segmentId);
        if (currentIndex === -1) return;

        const newIndex = direction === 'up' ? currentIndex - 1 : currentIndex + 1;

        // Clone and swap
        const updatedSegments = segments.map(s => ({ ...s }));

        // Swap
        const temp = updatedSegments[currentIndex];
        updatedSegments[currentIndex] = updatedSegments[newIndex];
        updatedSegments[newIndex] = temp;

        // Recalculate positions
        updatedSegments.forEach((seg, index) => {
            seg.segmentPosition = index + 1;
        });

        // Ensure array is sorted by position
        updatedSegments.sort((a, b) => a.segmentPosition - b.segmentPosition);

        // Create full payload
        const reorderList = updatedSegments.map(s => ({
            segmentId: s.id,
            newPosition: s.segmentPosition
        }));

        console.log('Sending Reorder Payload:', JSON.stringify(reorderList, null, 2));

        try {
            // Optimistic update
            setSegments(updatedSegments);

            await financeDataService.reorderSegmentStructures(reorderList);
            toast.success('Segments reordered and account codes updated');

            // Reload to ensure sync
            await loadData();
        } catch (error) {
            console.error('Reorder failed:', error);
            toast.error('Failed to reorder segments');
            loadData(); // Revert on error
        } finally {
            setReorderConfirmOpen(false);
            setPendingReorder(null);
        }
    };


    // Form states
    const [segmentForm, setSegmentForm] = useState({
        segmentName: '',
        segmentCode: '',
        segmentPosition: 1,
        segmentLength: 3,
        dataType: 'Numeric',
        separatorCharacter: '-',
        isMandatory: true,
        isReportingDimension: true,
        description: '',
    });

    const [valueForm, setValueForm] = useState({
        segmentValue: '',
        description: '',
        isActive: true,
    });

    const handleEditSegment = (segment: SegmentStructure) => {
        setEditingSegment(segment);
        setSegmentForm({
            segmentName: segment.segmentName,
            segmentCode: segment.segmentCode,
            segmentPosition: segment.segmentPosition,
            segmentLength: segment.segmentLength,
            dataType: segment.dataType,
            separatorCharacter: segment.separatorCharacter || '-',
            isMandatory: segment.isMandatory || false,
            isReportingDimension: segment.isReportingDimension || false,
            description: segment.description || '',
        });
        setIsSegmentDialogOpen(true);
    };

    const handleSaveSegment = async () => {
        try {
            if (editingSegment) {
                // Update
                const updateDto = {
                    id: editingSegment.id,
                    segmentName: segmentForm.segmentName,
                    description: segmentForm.description,
                    isReportingDimension: segmentForm.isReportingDimension,
                    isMandatory: segmentForm.isMandatory,
                    isActive: editingSegment.isActive,
                    // Other fields might be read-only on backend for updates, but sending what we can
                };

                await financeDataService.updateSegmentStructure(editingSegment.id, updateDto);
                toast.success('Segment updated');
            } else {
                // Create
                const newSegmentDto = {
                    segmentName: segmentForm.segmentName,
                    segmentCode: segmentForm.segmentCode,
                    segmentPosition: segments.length + 1,
                    segmentLength: segmentForm.segmentLength,
                    dataType: segmentForm.dataType,
                    separatorCharacter: segmentForm.separatorCharacter,
                    lookupTableRequired: true,
                    isMandatory: segmentForm.isMandatory,
                    isReportingDimension: segmentForm.isReportingDimension,
                    isNaturalAccount: false,
                    isActive: true,
                    description: segmentForm.description,
                };
                await financeDataService.createSegmentStructure(newSegmentDto);
                toast.success('Segment created');
            }

            setIsSegmentDialogOpen(false);
            resetSegmentForm();
            loadData();
        } catch (error) {
            console.error('Failed to save:', error);
            toast.error(editingSegment ? 'Failed to update segment' : 'Failed to create segment');
        }
    };

    // Keep handleCreateSegment for compatibility if invoked elsewhere, or remove if unused. 
    // Promoting handleSaveSegment as the main handler.

    const [editingValue, setEditingValue] = useState<SegmentLookupValue | null>(null);

    const handleEditValue = (value: SegmentLookupValue) => {
        setValueFormError(null);
        setEditingValue(value);
        setValueForm({
            segmentValue: value.segmentValue,
            description: value.description,
            isActive: value.isActive,
        });
        setIsValueDialogOpen(true);
    };

    const handleSaveValue = async () => {
        if (!selectedSegmentId) return;

        try {
            setValueFormError(null);
            const payload = {
                segmentStructureId: selectedSegmentId,
                segmentValue: valueForm.segmentValue,
                description: valueForm.description,
                isActive: valueForm.isActive,
                displayOrder: editingValue ? editingValue.displayOrder : (lookupValues[selectedSegmentId]?.length || 0) + 1,
                effectiveDate: editingValue?.effectiveDate ?? new Date().toISOString(),
            };

            if (editingValue) {
                await financeDataService.updateSegmentLookupValue(selectedSegmentId, editingValue.id, {
                    ...payload,
                    id: editingValue.id,
                });
                toast.success('Value updated');
            } else {
                await financeDataService.createSegmentLookupValue(selectedSegmentId, payload);
                toast.success('Value created');
            }

            await fetchLookupValues(selectedSegmentId);
            setIsValueDialogOpen(false);
            resetValueForm();
        } catch (error) {
            console.error('Failed to save lookup value:', error);
            const message = error instanceof Error ? error.message : editingValue ? 'Failed to update value' : 'Failed to create value';
            setValueFormError(message);
            toast.error(message);
        }
    };

    const handleDeleteValue = async () => {
        if (!selectedSegmentId || !lookupValueToDelete) return;

        try {
            setIsDeletingLookupValue(true);
            await financeDataService.deleteSegmentLookupValue(selectedSegmentId, lookupValueToDelete.id);
            toast.success('Value deleted');
            await fetchLookupValues(selectedSegmentId);
            setLookupValueToDelete(null);
        } catch (error) {
            console.error('Failed to delete lookup value:', error);
            toast.error(error instanceof Error ? error.message : 'Failed to delete value');
        } finally {
            setIsDeletingLookupValue(false);
        }
    };

    const resetValueForm = () => {
        setValueForm({
            segmentValue: '',
            description: '',
            isActive: true,
        });
        setValueFormError(null);
        setEditingValue(null);
    };



    const handleBulkUpload = () => {
        // Simulate upload
        setTimeout(() => {
            setIsUploadDialogOpen(false);
            alert('Values imported successfully! (Mock)');
        }, 1000);
    };

    const resetSegmentForm = () => {
        setSegmentForm({
            segmentName: '',
            segmentCode: '',
            segmentPosition: segments.length + 1,
            segmentLength: 3,
            dataType: 'Numeric',
            separatorCharacter: '-',
            isMandatory: true,
            isReportingDimension: true,
            description: '',
        });
        setEditingSegment(null);
    };



    const getDataTypeBadge = (type: string) => {
        return <Badge variant="outline">{type}</Badge>;
    };

    const searchParams = useSearchParams();
    const tabParam = searchParams.get('tab');
    const [activeTab, setActiveTab] = useState(tabParam === 'values' ? 'values' : 'structure');

    useEffect(() => {
        if (tabParam) {
            setActiveTab(tabParam === 'values' ? 'values' : 'structure');
        }
    }, [tabParam]);

    // ... (rest of local state)

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <Layers className="h-8 w-8" />
                        Segment Configuration
                    </h1>
                    <p className="text-muted-foreground">
                        Define Chart of Accounts structure and segments
                    </p>
                </div>
                <Button variant="outline" onClick={handleRegenerate} disabled={isLoading}>
                    <Settings2 className="mr-2 h-4 w-4" />
                    Regenerate COA
                </Button>
            </div>

            {/* Breadcrumbs */}
            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/finance">Finance</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/finance/settings">Settings</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>Segments</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-4">
                <TabsList>
                    <TabsTrigger value="structure" className="flex items-center gap-2">
                        <Settings2 className="h-4 w-4" />
                        Segment Structure
                    </TabsTrigger>
                    <TabsTrigger value="values" className="flex items-center gap-2">
                        <List className="h-4 w-4" />
                        Lookup Values
                    </TabsTrigger>
                </TabsList>

                {/* Structure Tab */}
                <TabsContent value="structure" className="space-y-4">
                    <div className="flex justify-end">
                        <Dialog open={isSegmentDialogOpen} onOpenChange={setIsSegmentDialogOpen}>
                            <DialogTrigger asChild>
                                <Button onClick={resetSegmentForm}>
                                    <Plus className="mr-2 h-4 w-4" />
                                    Add Segment
                                </Button>
                            </DialogTrigger>
                            <DialogContent className="max-w-lg">
                                <DialogHeader>
                                    <DialogTitle>{editingSegment ? 'Edit Segment' : 'Add New Segment'}</DialogTitle>
                                    <DialogDescription>
                                        {editingSegment ? 'Modify existing segment configuration' : 'Define a new segment for your Chart of Accounts'}
                                    </DialogDescription>
                                </DialogHeader>
                                <div className="grid gap-4 py-4">
                                    <div className="grid grid-cols-2 gap-4">
                                        <div className="space-y-2">
                                            <Label htmlFor="segmentName">Segment Name</Label>
                                            <Input
                                                id="segmentName"
                                                value={segmentForm.segmentName}
                                                onChange={(e) => setSegmentForm({ ...segmentForm, segmentName: e.target.value })}
                                                placeholder="e.g., Department"
                                            />
                                        </div>
                                        <div className="space-y-2">
                                            <Label htmlFor="segmentCode">Code</Label>
                                            <Input
                                                id="segmentCode"
                                                value={segmentForm.segmentCode}
                                                onChange={(e) => setSegmentForm({ ...segmentForm, segmentCode: e.target.value })}
                                                placeholder="e.g., DEPT"
                                                disabled={!!editingSegment}
                                            />
                                        </div>
                                    </div>
                                    <div className="grid grid-cols-2 gap-4">
                                        <div className="space-y-2">
                                            <Label htmlFor="segmentLength">Length</Label>
                                            <Input
                                                id="segmentLength"
                                                type="number"
                                                value={segmentForm.segmentLength}
                                                onChange={(e) => setSegmentForm({ ...segmentForm, segmentLength: parseInt(e.target.value) })}
                                                disabled={!!editingSegment}
                                            />
                                        </div>
                                        <div className="space-y-2">
                                            <Label htmlFor="dataType">Data Type</Label>
                                            <Select
                                                value={segmentForm.dataType}
                                                onValueChange={(value) => setSegmentForm({ ...segmentForm, dataType: value })}
                                                disabled={!!editingSegment}
                                            >
                                                <SelectTrigger id="dataType">
                                                    <SelectValue />
                                                </SelectTrigger>
                                                <SelectContent>
                                                    <SelectItem value="Numeric">Numeric</SelectItem>
                                                    <SelectItem value="Alphanumeric">Alphanumeric</SelectItem>
                                                </SelectContent>
                                            </Select>
                                        </div>
                                    </div>
                                    <div className="space-y-2">
                                        <Label htmlFor="description">Description</Label>
                                        <Input
                                            id="description"
                                            value={segmentForm.description}
                                            onChange={(e) => setSegmentForm({ ...segmentForm, description: e.target.value })}
                                        />
                                    </div>
                                    <div className="flex flex-col gap-2">
                                        <div className="flex items-center space-x-2">
                                            <Checkbox
                                                id="isMandatory"
                                                checked={segmentForm.isMandatory}
                                                onCheckedChange={(checked) =>
                                                    setSegmentForm({ ...segmentForm, isMandatory: checked as boolean })
                                                }
                                            />
                                            <Label htmlFor="isMandatory">Mandatory Segment</Label>
                                        </div>
                                        <div className="flex items-center space-x-2">
                                            <Checkbox
                                                id="isReportingDimension"
                                                checked={segmentForm.isReportingDimension}
                                                onCheckedChange={(checked) =>
                                                    setSegmentForm({ ...segmentForm, isReportingDimension: checked as boolean })
                                                }
                                            />
                                            <Label htmlFor="isReportingDimension">Use for Reporting</Label>
                                        </div>
                                    </div>
                                </div>
                                <DialogFooter>
                                    <Button variant="outline" onClick={() => setIsSegmentDialogOpen(false)}>Cancel</Button>
                                    <Button onClick={handleSaveSegment}>{editingSegment ? 'Save Changes' : 'Create Segment'}</Button>
                                </DialogFooter>
                            </DialogContent>
                        </Dialog>
                    </div>

                    <div className="grid gap-4">
                        {segments.map((segment) => (
                            <Card key={segment.id}>
                                <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                                    <div className="flex items-center gap-2">
                                        <Badge variant="secondary" className="h-6 w-6 rounded-full flex items-center justify-center p-0">
                                            {segment.segmentPosition}
                                        </Badge>
                                        <CardTitle className="text-base font-semibold">
                                            {segment.segmentName} ({segment.segmentCode})
                                        </CardTitle>
                                        {segment.isNaturalAccount && (
                                            <Badge variant="default" className="ml-2">Natural Account</Badge>
                                        )}
                                    </div>
                                    <div className="flex items-center gap-2">
                                        <div className="flex flex-col gap-1 mr-2">
                                            <Button
                                                variant="ghost"
                                                size="sm"
                                                className="h-6 w-6 p-0"
                                                onClick={() => handleReorderClick(segment.id, 'up')}
                                                disabled={segment.segmentPosition === 1}
                                            >
                                                <ArrowUp className="h-4 w-4" />
                                            </Button>
                                            <Button
                                                variant="ghost"
                                                size="sm"
                                                className="h-6 w-6 p-0"
                                                onClick={() => handleReorderClick(segment.id, 'down')}
                                                disabled={segment.segmentPosition === segments.length}
                                            >
                                                <ArrowDown className="h-4 w-4" />
                                            </Button>
                                        </div>
                                        <Button variant="ghost" size="sm" onClick={() => handleEditSegment(segment)}>
                                            <Edit className="h-4 w-4" />
                                        </Button>
                                    </div>
                                </CardHeader>
                                <CardContent>
                                    <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm mt-2">
                                        <div>
                                            <p className="text-muted-foreground">Length</p>
                                            <p className="font-medium">{segment.segmentLength} chars</p>
                                        </div>
                                        <div>
                                            <p className="text-muted-foreground">Type</p>
                                            <p className="font-medium">{segment.dataType}</p>
                                        </div>
                                        <div>
                                            <p className="text-muted-foreground">Separator</p>
                                            <p className="font-medium">"{segment.separatorCharacter}"</p>
                                        </div>
                                        <div>
                                            <p className="text-muted-foreground">Status</p>
                                            <Badge variant={segment.isActive ? 'outline' : 'secondary'}>
                                                {segment.isActive ? 'Active' : 'Inactive'}
                                            </Badge>
                                        </div>
                                    </div>
                                    <div className="flex gap-2 mt-4">
                                        {segment.isMandatory && <Badge variant="secondary">Mandatory</Badge>}
                                        {segment.isReportingDimension && <Badge variant="secondary">Reporting Dimension</Badge>}
                                    </div>
                                </CardContent>
                            </Card>
                        ))}
                    </div>

                    <Card className="bg-muted/50">
                        <CardContent className="py-4">
                            <p className="text-sm font-medium text-muted-foreground mb-2">Account Structure Preview:</p>
                            <div className="flex items-center gap-1 text-lg font-mono font-bold">
                                {segments.map((seg, index) => (
                                    <React.Fragment key={seg.id}>
                                        <span className="bg-background border rounded px-2 py-1">
                                            {seg.segmentCode}
                                        </span>
                                        {index < segments.length - 1 && (
                                            <span className="text-muted-foreground">{seg.separatorCharacter}</span>
                                        )}
                                    </React.Fragment>
                                ))}
                            </div>
                        </CardContent>
                    </Card>
                </TabsContent>

                {/* Values Tab */}
                <TabsContent value="values" className="space-y-4">
                    <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
                        {/* Sidebar - Segment Selection */}
                        <Card className="md:col-span-1">
                            <CardHeader>
                                <CardTitle className="text-base">Segments</CardTitle>
                            </CardHeader>
                            <CardContent className="p-0">
                                <div className="flex flex-col">
                                    {segments
                                        .filter(s => !s.isNaturalAccount) // Exclude natural account as it uses main account list
                                        .map((segment) => (
                                            <button
                                                key={segment.id}
                                                onClick={() => setSelectedSegmentId(segment.id)}
                                                className={`text-left px-4 py-3 text-sm font-medium transition-colors hover:bg-muted/50 ${selectedSegmentId === segment.id
                                                    ? 'bg-muted text-primary border-l-2 border-primary'
                                                    : 'text-muted-foreground'
                                                    }`}
                                            >
                                                {segment.segmentName}
                                            </button>
                                        ))}
                                </div>
                            </CardContent>
                        </Card>

                        {/* Main Content - Values List */}
                        <Card className="md:col-span-3">
                            <CardHeader className="flex flex-row items-center justify-between">
                                <div>
                                    <CardTitle>
                                        {segments.find(s => s.id === selectedSegmentId)?.segmentName} Values
                                    </CardTitle>
                                    <CardDescription>
                                        Manage valid values for this segment
                                    </CardDescription>
                                </div>
                                <div className="flex gap-2">
                                    <Dialog open={isUploadDialogOpen} onOpenChange={setIsUploadDialogOpen}>
                                        <DialogTrigger asChild>
                                            <Button variant="outline">
                                                <Upload className="mr-2 h-4 w-4" />
                                                Import
                                            </Button>
                                        </DialogTrigger>
                                        <DialogContent>
                                            <DialogHeader>
                                                <DialogTitle>Import Segment Values</DialogTitle>
                                                <DialogDescription>
                                                    Upload a CSV or Excel file to bulk import values for {segments.find(s => s.id === selectedSegmentId)?.segmentName}
                                                </DialogDescription>
                                            </DialogHeader>
                                            <div className="grid gap-4 py-4">
                                                <div className="border-2 border-dashed rounded-lg p-8 text-center hover:bg-muted/50 transition-colors cursor-pointer">
                                                    <FileSpreadsheet className="h-10 w-10 mx-auto text-muted-foreground mb-2" />
                                                    <p className="text-sm font-medium">Click to select file or drag and drop</p>
                                                    <p className="text-xs text-muted-foreground mt-1">CSV, XLS, XLSX (Max 5MB)</p>
                                                </div>
                                                <div className="flex items-center justify-between p-3 bg-muted rounded-md">
                                                    <div className="flex items-center gap-2">
                                                        <FileSpreadsheet className="h-4 w-4" />
                                                        <span className="text-sm">Template.xlsx</span>
                                                    </div>
                                                    <Button variant="ghost" size="sm" className="h-8">
                                                        <Download className="h-4 w-4 mr-2" />
                                                        Download Template
                                                    </Button>
                                                </div>
                                            </div>
                                            <DialogFooter>
                                                <Button variant="outline" onClick={() => setIsUploadDialogOpen(false)}>Cancel</Button>
                                                <Button onClick={handleBulkUpload}>Import Values</Button>
                                            </DialogFooter>
                                        </DialogContent>
                                    </Dialog>

                                    <Dialog open={isValueDialogOpen} onOpenChange={setIsValueDialogOpen}>
                                        <DialogTrigger asChild>
                                            <Button onClick={resetValueForm}>
                                                <Plus className="mr-2 h-4 w-4" />
                                                Add Value
                                            </Button>
                                        </DialogTrigger>
                                        <DialogContent>
                                            <DialogHeader>
                                                <DialogTitle>{editingValue ? 'Edit Lookup Value' : 'Add Lookup Value'}</DialogTitle>
                                                <DialogDescription>
                                                    {editingValue ? 'Modify existing value' : `Add a valid value for ${segments.find(s => s.id === selectedSegmentId)?.segmentName}`}
                                                </DialogDescription>
                                            </DialogHeader>
                                            <div className="grid gap-4 py-4">
                                                <div className="space-y-2">
                                                    <Label htmlFor="segmentValue">Value</Label>
                                                    <Input
                                                        id="segmentValue"
                                                        value={valueForm.segmentValue}
                                                        onChange={(e) => setValueForm({ ...valueForm, segmentValue: e.target.value })}
                                                        placeholder="e.g., 100"
                                                        maxLength={segments.find(s => s.id === selectedSegmentId)?.segmentLength}
                                                        disabled={!!editingValue}
                                                    />
                                                    <p className="text-xs text-muted-foreground">
                                                        Max length: {segments.find(s => s.id === selectedSegmentId)?.segmentLength} characters
                                                    </p>
                                                </div>
                                                <div className="space-y-2">
                                                    <Label htmlFor="valueDescription">Description</Label>
                                                    <Input
                                                        id="valueDescription"
                                                        value={valueForm.description}
                                                        onChange={(e) => setValueForm({ ...valueForm, description: e.target.value })}
                                                        placeholder="e.g., Administration"
                                                    />
                                                </div>
                                                <div className="flex items-center space-x-2">
                                                    <Checkbox
                                                        id="valueActive"
                                                        checked={valueForm.isActive}
                                                        onCheckedChange={(checked) =>
                                                            setValueForm({ ...valueForm, isActive: checked as boolean })
                                                        }
                                                    />
                                                    <Label htmlFor="valueActive">Active</Label>
                                                </div>
                                                {valueFormError && (
                                                    <div className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
                                                        {valueFormError}
                                                    </div>
                                                )}
                                            </div>
                                            <DialogFooter>
                                                <Button variant="outline" onClick={() => setIsValueDialogOpen(false)}>Cancel</Button>
                                                <Button onClick={handleSaveValue}>{editingValue ? 'Save Changes' : 'Add Value'}</Button>
                                            </DialogFooter>
                                        </DialogContent>
                                    </Dialog>
                                </div>
                            </CardHeader>
                            <CardContent>
                                <div className="rounded-md border">
                                    <table className="w-full">
                                        <thead>
                                            <tr className="border-b bg-muted/50">
                                                <th className="p-3 text-left font-medium">Value</th>
                                                <th className="p-3 text-left font-medium">Description</th>
                                                <th className="p-3 text-left font-medium">Status</th>
                                                <th className="p-3 text-right font-medium">Actions</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {lookupValues[selectedSegmentId]?.map((value) => (
                                                <tr key={value.id} className="border-b hover:bg-muted/50">
                                                    <td className="p-3 font-mono font-semibold">{value.segmentValue}</td>
                                                    <td className="p-3">{value.description}</td>
                                                    <td className="p-3">
                                                        <Badge variant={value.isActive ? 'outline' : 'secondary'}>
                                                            {value.isActive ? 'Active' : 'Inactive'}
                                                        </Badge>
                                                    </td>
                                                    <td className="p-3 text-right">
                                                        <Button variant="ghost" size="sm" onClick={() => handleEditValue(value)} title="Edit value">
                                                            <Edit className="h-4 w-4" />
                                                        </Button>
                                                        <Button
                                                            variant="ghost"
                                                            size="sm"
                                                            className="text-destructive hover:text-destructive"
                                                            onClick={() => setLookupValueToDelete(value)}
                                                            title="Delete value"
                                                        >
                                                            <Trash2 className="h-4 w-4" />
                                                        </Button>
                                                    </td>
                                                </tr>
                                            )) || (
                                                    <tr>
                                                        <td colSpan={4} className="p-8 text-center text-muted-foreground">
                                                            No values defined for this segment
                                                        </td>
                                                    </tr>
                                                )}
                                        </tbody>
                                    </table>
                                </div>
                            </CardContent>
                        </Card>
                    </div>
                </TabsContent>
            </Tabs>

            {/* Reorder Confirmation Dialog */}
            <AlertDialog open={reorderConfirmOpen} onOpenChange={setReorderConfirmOpen}>
                <AlertDialogContent>
                    <AlertDialogHeader>
                        <AlertDialogTitle>Reorder Segments?</AlertDialogTitle>
                        <AlertDialogDescription>
                            This will change the order of segments in your Chart of Accounts structure.
                            <br /><br />
                            <strong>Important:</strong> All existing GL account codes will be regenerated
                            to reflect the new segment order. This affects how account codes are displayed
                            throughout the system.
                        </AlertDialogDescription>
                    </AlertDialogHeader>
                    <AlertDialogFooter>
                        <AlertDialogCancel onClick={() => setPendingReorder(null)}>Cancel</AlertDialogCancel>
                        <AlertDialogAction onClick={handleReorderConfirm}>
                            Yes, Reorder Segments
                        </AlertDialogAction>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>

            {/* Lookup Value Delete Confirmation Dialog */}
            <AlertDialog open={!!lookupValueToDelete} onOpenChange={(open) => !open && setLookupValueToDelete(null)}>
                <AlertDialogContent>
                    <AlertDialogHeader>
                        <AlertDialogTitle>Delete Lookup Value?</AlertDialogTitle>
                        <AlertDialogDescription>
                            This will delete lookup value <strong>{lookupValueToDelete?.segmentValue}</strong>
                            {lookupValueToDelete?.description ? ` (${lookupValueToDelete.description})` : ''}.
                            If the value is already used by accounts, the system will deactivate it instead.
                        </AlertDialogDescription>
                    </AlertDialogHeader>
                    <AlertDialogFooter>
                        <AlertDialogCancel disabled={isDeletingLookupValue}>Cancel</AlertDialogCancel>
                        <AlertDialogAction
                            disabled={isDeletingLookupValue}
                            onClick={(event) => {
                                event.preventDefault();
                                handleDeleteValue();
                            }}
                        >
                            {isDeletingLookupValue ? 'Deleting...' : 'Delete Value'}
                        </AlertDialogAction>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>
        </div>
    );
}

export default function SegmentConfigurationPage() {
    return (
        <Suspense fallback={<div>Loading Configuration...</div>}>
            <SegmentConfigurationContent />
        </Suspense>
    );
}
