'use client';

import React, { useState } from 'react';
import Link from 'next/link';
import { useRouter, useParams } from 'next/navigation';
import {
    ChevronRight,
    Home,
    Save,
    Trash2,
    Play,
    AlertTriangle,
    Plus,
} from 'lucide-react';
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
} from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Badge } from '@/components/ui/badge';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from '@/components/ui/table';
import { AllocationType } from '@/types/unit-accounts';

// Mock data
const MOCK_RULE = {
    id: '1',
    code: 'RENT-ALLOC',
    name: 'Rent Allocation by Square Feet',
    description: 'Allocate rent expense across departments based on square footage',
    sourceAccountId: 'gl1',
    allocationType: 'UnitAccountBased' as AllocationType,
    driverUnitAccountId: 'u4',
    isActive: true,
    autoReverse: false,
    lastRunDate: '2024-03-31',
    targets: [
        { id: 't1', targetAccountId: 'gl4', fixedPercentage: 0, targetDriverUnitAccountId: 'u5' },
        { id: 't2', targetAccountId: 'gl5', fixedPercentage: 0, targetDriverUnitAccountId: 'u6' },
    ],
};

const MOCK_GL_ACCOUNTS = [
    { id: 'gl1', number: '6100-000', name: 'Rent Expense' },
    { id: 'gl4', number: '6100-100', name: 'Rent - Sales' },
    { id: 'gl5', number: '6100-200', name: 'Rent - Engineering' },
    { id: 'gl6', number: '6100-300', name: 'Rent - Admin' },
];

const MOCK_UNIT_ACCOUNTS = [
    { id: 'u4', number: 'U-2000', name: 'Total Office Space' },
    { id: 'u5', number: 'U-2100', name: 'Sales Office Space' },
    { id: 'u6', number: 'U-2200', name: 'Engineering Office Space' },
];

export default function EditAllocationRulePage() {
    const router = useRouter();
    const params = useParams();
    const ruleId = params.id as string;

    const [formData, setFormData] = useState({
        code: MOCK_RULE.code,
        name: MOCK_RULE.name,
        description: MOCK_RULE.description,
        sourceAccountId: MOCK_RULE.sourceAccountId,
        allocationType: MOCK_RULE.allocationType,
        driverUnitAccountId: MOCK_RULE.driverUnitAccountId,
        isActive: MOCK_RULE.isActive,
        autoReverse: MOCK_RULE.autoReverse,
    });
    const [targets, setTargets] = useState(MOCK_RULE.targets);

    const handleSubmit = (e: React.FormEvent) => {
        e.preventDefault();
        console.log('Updating allocation rule:', { ...formData, targets });
        alert('⚠️ DEMO FRONTEND UI: Allocation rule would be updated.');
        router.push('/finance/allocations');
    };

    const handleDelete = () => {
        if (confirm('Are you sure you want to delete this allocation rule?')) {
            alert('⚠️ DEMO FRONTEND UI: Allocation rule would be deleted.');
            router.push('/finance/allocations');
        }
    };

    const handleRun = () => {
        alert(`⚠️ DEMO FRONTEND UI: Running allocation "${formData.name}"...`);
    };

    const addTarget = () => {
        setTargets([
            ...targets,
            { id: Date.now().toString(), targetAccountId: '', fixedPercentage: 0, targetDriverUnitAccountId: '' },
        ]);
    };

    const removeTarget = (id: string) => {
        if (targets.length > 1) {
            setTargets(targets.filter((t) => t.id !== id));
        }
    };

    return (
        <div className="space-y-6">
            {/* Breadcrumbs */}
            <nav className="flex items-center space-x-2 text-sm text-muted-foreground">
                <Link href="/" className="flex items-center hover:text-foreground">
                    <Home className="h-4 w-4" />
                </Link>
                <ChevronRight className="h-4 w-4" />
                <Link href="/finance" className="hover:text-foreground">Finance</Link>
                <ChevronRight className="h-4 w-4" />
                <Link href="/finance/allocations" className="hover:text-foreground">Allocations</Link>
                <ChevronRight className="h-4 w-4" />
                <span className="text-foreground">{formData.code}</span>
            </nav>

            <Alert className="border-yellow-500 bg-yellow-50">
                <AlertTriangle className="h-4 w-4 text-yellow-600" />
                <AlertDescription className="text-yellow-700">
                    <strong>⚠️ DEMO FRONTEND UI</strong>
                </AlertDescription>
            </Alert>

            <form onSubmit={handleSubmit}>
                <div className="flex items-center justify-between mb-6">
                    <div>
                        <div className="flex items-center gap-3">
                            <h1 className="text-3xl font-bold tracking-tight">{formData.code}</h1>
                            <Badge className={formData.isActive ? 'bg-green-100 text-green-800' : ''}>
                                {formData.isActive ? 'Active' : 'Inactive'}
                            </Badge>
                        </div>
                        <p className="text-muted-foreground">{formData.name}</p>
                    </div>
                    <div className="flex gap-2">
                        <Button type="button" variant="outline" onClick={handleRun} disabled={!formData.isActive}>
                            <Play className="mr-2 h-4 w-4" />
                            Run Allocation
                        </Button>
                        <Button type="button" variant="destructive" onClick={handleDelete}>
                            <Trash2 className="mr-2 h-4 w-4" />
                            Delete
                        </Button>
                        <Button type="submit">
                            <Save className="mr-2 h-4 w-4" />
                            Save Changes
                        </Button>
                    </div>
                </div>

                <div className="grid gap-6 md:grid-cols-2">
                    {/* Basic Info */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Rule Details</CardTitle>
                            <CardDescription>
                                Last run: {MOCK_RULE.lastRunDate ? new Date(MOCK_RULE.lastRunDate).toLocaleDateString() : 'Never'}
                            </CardDescription>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            <div className="grid gap-4 md:grid-cols-2">
                                <div className="space-y-2">
                                    <Label htmlFor="code">Code</Label>
                                    <Input id="code" value={formData.code} disabled className="bg-muted" />
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="name">Name</Label>
                                    <Input
                                        id="name"
                                        value={formData.name}
                                        onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                                    />
                                </div>
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="description">Description</Label>
                                <Textarea
                                    id="description"
                                    value={formData.description}
                                    onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                                    rows={3}
                                />
                            </div>
                            <div className="flex items-center justify-between">
                                <div className="flex items-center space-x-2">
                                    <Switch
                                        id="isActive"
                                        checked={formData.isActive}
                                        onCheckedChange={(checked) => setFormData({ ...formData, isActive: checked })}
                                    />
                                    <Label htmlFor="isActive">Active</Label>
                                </div>
                                <div className="flex items-center space-x-2">
                                    <Switch
                                        id="autoReverse"
                                        checked={formData.autoReverse}
                                        onCheckedChange={(checked) => setFormData({ ...formData, autoReverse: checked })}
                                    />
                                    <Label htmlFor="autoReverse">Auto-reverse</Label>
                                </div>
                            </div>
                        </CardContent>
                    </Card>

                    {/* Source & Driver */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Source & Driver</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            <div className="space-y-2">
                                <Label>Source GL Account</Label>
                                <Select value={formData.sourceAccountId} disabled>
                                    <SelectTrigger className="bg-muted">
                                        <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {MOCK_GL_ACCOUNTS.map((acc) => (
                                            <SelectItem key={acc.id} value={acc.id}>
                                                {acc.number} - {acc.name}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                <p className="text-xs text-muted-foreground">Source account cannot be changed after creation</p>
                            </div>
                            <div className="space-y-2">
                                <Label>Allocation Type</Label>
                                <Select value={formData.allocationType} disabled>
                                    <SelectTrigger className="bg-muted">
                                        <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="UnitAccountBased">Unit Account Based</SelectItem>
                                        <SelectItem value="FixedPercentage">Fixed Percentage</SelectItem>
                                        <SelectItem value="EqualDistribution">Equal Distribution</SelectItem>
                                    </SelectContent>
                                </Select>
                            </div>
                            {formData.allocationType === 'UnitAccountBased' && (
                                <div className="space-y-2">
                                    <Label>Driver Unit Account</Label>
                                    <Select
                                        value={formData.driverUnitAccountId}
                                        onValueChange={(v) => setFormData({ ...formData, driverUnitAccountId: v })}
                                    >
                                        <SelectTrigger>
                                            <SelectValue />
                                        </SelectTrigger>
                                        <SelectContent>
                                            {MOCK_UNIT_ACCOUNTS.map((acc) => (
                                                <SelectItem key={acc.id} value={acc.id}>
                                                    {acc.number} - {acc.name}
                                                </SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                </div>
                            )}
                        </CardContent>
                    </Card>
                </div>

                {/* Targets */}
                <Card className="mt-6">
                    <CardHeader>
                        <div className="flex items-center justify-between">
                            <div>
                                <CardTitle>Allocation Targets</CardTitle>
                                <CardDescription>{targets.length} target(s) configured</CardDescription>
                            </div>
                            <Button type="button" variant="outline" onClick={addTarget}>
                                <Plus className="mr-2 h-4 w-4" />
                                Add Target
                            </Button>
                        </div>
                    </CardHeader>
                    <CardContent>
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead>Target GL Account</TableHead>
                                    <TableHead>Target Driver</TableHead>
                                    <TableHead className="w-[80px]">Actions</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {targets.map((target) => (
                                    <TableRow key={target.id}>
                                        <TableCell>
                                            <Select
                                                value={target.targetAccountId}
                                                onValueChange={(v) =>
                                                    setTargets(targets.map((t) => (t.id === target.id ? { ...t, targetAccountId: v } : t)))
                                                }
                                            >
                                                <SelectTrigger>
                                                    <SelectValue placeholder="Select account" />
                                                </SelectTrigger>
                                                <SelectContent>
                                                    {MOCK_GL_ACCOUNTS.map((acc) => (
                                                        <SelectItem key={acc.id} value={acc.id}>
                                                            {acc.number} - {acc.name}
                                                        </SelectItem>
                                                    ))}
                                                </SelectContent>
                                            </Select>
                                        </TableCell>
                                        <TableCell>
                                            <Select
                                                value={target.targetDriverUnitAccountId}
                                                onValueChange={(v) =>
                                                    setTargets(targets.map((t) => (t.id === target.id ? { ...t, targetDriverUnitAccountId: v } : t)))
                                                }
                                            >
                                                <SelectTrigger>
                                                    <SelectValue placeholder="Select driver" />
                                                </SelectTrigger>
                                                <SelectContent>
                                                    {MOCK_UNIT_ACCOUNTS.map((acc) => (
                                                        <SelectItem key={acc.id} value={acc.id}>
                                                            {acc.number} - {acc.name}
                                                        </SelectItem>
                                                    ))}
                                                </SelectContent>
                                            </Select>
                                        </TableCell>
                                        <TableCell>
                                            <Button
                                                type="button"
                                                variant="ghost"
                                                size="icon"
                                                onClick={() => removeTarget(target.id)}
                                                disabled={targets.length === 1}
                                            >
                                                <Trash2 className="h-4 w-4 text-red-500" />
                                            </Button>
                                        </TableCell>
                                    </TableRow>
                                ))}
                            </TableBody>
                        </Table>
                    </CardContent>
                </Card>
            </form>
        </div>
    );
}
