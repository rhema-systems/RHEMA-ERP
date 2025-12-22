'use client';

import React, { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Layers, Plus, Edit, Trash2, List, Settings2, Upload, FileSpreadsheet, Download } from 'lucide-react';
import type { SegmentStructure, SegmentLookupValue } from '@/types/finance';

// MOCK DATA
const MOCK_SEGMENTS: SegmentStructure[] = [
    {
        id: 'seg-1',
        segmentName: 'Natural Account',
        segmentCode: 'ACCT',
        segmentPosition: 1,
        segmentLength: 4,
        dataType: 'Numeric',
        separatorCharacter: '-',
        lookupTableRequired: true,
        isMandatory: true,
        isReportingDimension: true,
        isNaturalAccount: true,
        isActive: true,
        description: 'Main GL account segment',
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'seg-2',
        segmentName: 'Department',
        segmentCode: 'DEPT',
        segmentPosition: 2,
        segmentLength: 3,
        dataType: 'Numeric',
        separatorCharacter: '-',
        lookupTableRequired: true,
        isMandatory: true,
        isReportingDimension: true,
        isNaturalAccount: false,
        isActive: true,
        description: 'Cost center or department',
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'seg-3',
        segmentName: 'Project',
        segmentCode: 'PROJ',
        segmentPosition: 3,
        segmentLength: 4,
        dataType: 'Alphanumeric',
        separatorCharacter: '',
        lookupTableRequired: true,
        isMandatory: false,
        isReportingDimension: true,
        isNaturalAccount: false,
        isActive: true,
        description: 'Project code',
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
];

const MOCK_LOOKUP_VALUES: Record<string, SegmentLookupValue[]> = {
    'seg-2': [
        {
            id: 'val-1',
            segmentStructureId: 'seg-2',
            segmentValue: '100',
            description: 'Administration',
            effectiveDate: '2024-01-01T00:00:00Z',
            isActive: true,
            displayOrder: 1,
            createdAt: '2024-01-01T00:00:00Z',
            updatedAt: '2024-01-01T00:00:00Z',
        },
        {
            id: 'val-2',
            segmentStructureId: 'seg-2',
            segmentValue: '200',
            description: 'Sales & Marketing',
            effectiveDate: '2024-01-01T00:00:00Z',
            isActive: true,
            displayOrder: 2,
            createdAt: '2024-01-01T00:00:00Z',
            updatedAt: '2024-01-01T00:00:00Z',
        },
        {
            id: 'val-3',
            segmentStructureId: 'seg-2',
            segmentValue: '300',
            description: 'Engineering',
            effectiveDate: '2024-01-01T00:00:00Z',
            isActive: true,
            displayOrder: 3,
            createdAt: '2024-01-01T00:00:00Z',
            updatedAt: '2024-01-01T00:00:00Z',
        },
    ],
    'seg-3': [
        {
            id: 'val-4',
            segmentStructureId: 'seg-3',
            segmentValue: 'P001',
            description: 'Website Redesign',
            effectiveDate: '2024-01-01T00:00:00Z',
            isActive: true,
            displayOrder: 1,
            createdAt: '2024-01-01T00:00:00Z',
            updatedAt: '2024-01-01T00:00:00Z',
        },
    ],
};

export default function SegmentConfigurationPage() {
    const [segments, setSegments] = useState<SegmentStructure[]>(MOCK_SEGMENTS);
    const [selectedSegmentId, setSelectedSegmentId] = useState<string>('seg-2');
    const [lookupValues, setLookupValues] = useState(MOCK_LOOKUP_VALUES);

    // Dialog states
    const [isSegmentDialogOpen, setIsSegmentDialogOpen] = useState(false);
    const [isValueDialogOpen, setIsValueDialogOpen] = useState(false);
    const [isUploadDialogOpen, setIsUploadDialogOpen] = useState(false);
    const [editingSegment, setEditingSegment] = useState<SegmentStructure | null>(null);

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

    const handleCreateSegment = () => {
        const newSegment: SegmentStructure = {
            id: `seg-${Date.now()}`,
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
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
        };
        setSegments([...segments, newSegment]);
        setIsSegmentDialogOpen(false);
        resetSegmentForm();
    };

    const handleCreateValue = () => {
        const newValue: SegmentLookupValue = {
            id: `val-${Date.now()}`,
            segmentStructureId: selectedSegmentId,
            segmentValue: valueForm.segmentValue,
            description: valueForm.description,
            effectiveDate: new Date().toISOString(),
            isActive: valueForm.isActive,
            displayOrder: (lookupValues[selectedSegmentId]?.length || 0) + 1,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
        };

        setLookupValues({
            ...lookupValues,
            [selectedSegmentId]: [...(lookupValues[selectedSegmentId] || []), newValue],
        });
        setIsValueDialogOpen(false);
        resetValueForm();
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

    const resetValueForm = () => {
        setValueForm({
            segmentValue: '',
            description: '',
            isActive: true,
        });
    };

    const getDataTypeBadge = (type: string) => {
        return <Badge variant="outline">{type}</Badge>;
    };

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
                    <p className="text-sm text-orange-600 mt-1">
                        ⚠️ DEMO FRONTEND UI
                    </p>
                </div>
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

            <Tabs defaultValue="structure" className="space-y-4">
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
                                    <DialogTitle>Add New Segment</DialogTitle>
                                    <DialogDescription>
                                        Define a new segment for your Chart of Accounts
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
                                            />
                                        </div>
                                        <div className="space-y-2">
                                            <Label htmlFor="dataType">Data Type</Label>
                                            <Select
                                                value={segmentForm.dataType}
                                                onValueChange={(value) => setSegmentForm({ ...segmentForm, dataType: value })}
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
                                    <Button onClick={handleCreateSegment}>Create Segment</Button>
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
                                        <Button variant="ghost" size="sm">
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
                                                <DialogTitle>Add Lookup Value</DialogTitle>
                                                <DialogDescription>
                                                    Add a valid value for {segments.find(s => s.id === selectedSegmentId)?.segmentName}
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
                                            </div>
                                            <DialogFooter>
                                                <Button variant="outline" onClick={() => setIsValueDialogOpen(false)}>Cancel</Button>
                                                <Button onClick={handleCreateValue}>Add Value</Button>
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
                                                        <Button variant="ghost" size="sm">
                                                            <Edit className="h-4 w-4" />
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
        </div>
    );
}
