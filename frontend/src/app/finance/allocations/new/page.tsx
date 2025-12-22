'use client';

import React, { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
    ChevronRight,
    Home,
    Save,
    X,
    Plus,
    Trash2,
    AlertTriangle,
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

// Mock data for dropdowns
const MOCK_GL_ACCOUNTS = [
    { id: 'gl1', number: '6100-000', name: 'Rent Expense' },
    { id: 'gl2', number: '6200-000', name: 'Utilities Expense' },
    { id: 'gl3', number: '7200-000', name: 'IT Overhead' },
    { id: 'gl4', number: '6100-100', name: 'Rent - Sales' },
    { id: 'gl5', number: '6100-200', name: 'Rent - Engineering' },
    { id: 'gl6', number: '6100-300', name: 'Rent - Admin' },
];

const MOCK_UNIT_ACCOUNTS = [
    { id: 'u1', number: 'U-1000', name: 'Total Employees' },
    { id: 'u2', number: 'U-1100', name: 'Sales Employees' },
    { id: 'u3', number: 'U-1200', name: 'Engineering Employees' },
    { id: 'u4', number: 'U-2000', name: 'Total Office Space' },
    { id: 'u5', number: 'U-2100', name: 'Sales Office Space' },
    { id: 'u6', number: 'U-2200', name: 'Engineering Office Space' },
];

interface AllocationTargetRow {
    id: string;
    targetAccountId: string;
    fixedPercentage: number;
    targetDriverUnitAccountId: string;
}

export default function NewAllocationRulePage() {
    const router = useRouter();
    const [formData, setFormData] = useState({
        code: '',
        name: '',
        description: '',
        sourceAccountId: '',
        allocationType: 'UnitAccountBased' as AllocationType,
        driverUnitAccountId: '',
        autoReverse: false,
    });
    const [targets, setTargets] = useState<AllocationTargetRow[]>([
        { id: '1', targetAccountId: '', fixedPercentage: 0, targetDriverUnitAccountId: '' },
    ]);
    const [errors, setErrors] = useState<Record<string, string>>({});

    const handleSubmit = (e: React.FormEvent) => {
        e.preventDefault();
        const newErrors: Record<string, string> = {};

        if (!formData.code) newErrors.code = 'Code is required';
        if (!formData.name) newErrors.name = 'Name is required';
        if (!formData.sourceAccountId) newErrors.sourceAccountId = 'Source account is required';
        if (formData.allocationType === 'UnitAccountBased' && !formData.driverUnitAccountId) {
            newErrors.driverUnitAccountId = 'Driver account is required for unit-based allocation';
        }
        if (targets.length === 0) newErrors.targets = 'At least one target is required';

        if (Object.keys(newErrors).length > 0) {
            setErrors(newErrors);
            return;
        }

        console.log('Creating allocation rule:', { ...formData, targets });
        alert('⚠️ DEMO FRONTEND UI: Allocation rule would be created.');
        router.push('/finance/allocations');
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

    const updateTarget = (id: string, field: keyof AllocationTargetRow, value: string | number) => {
        setTargets(targets.map((t) => (t.id === id ? { ...t, [field]: value } : t)));
    };

    const totalPercentage = targets.reduce((sum, t) => sum + (t.fixedPercentage || 0), 0);

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
                <span className="text-foreground">New Rule</span>
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
                        <h1 className="text-3xl font-bold tracking-tight">New Allocation Rule</h1>
                        <p className="text-muted-foreground">Define a rule for distributing expenses</p>
                    </div>
                    <div className="flex gap-2">
                        <Button type="button" variant="outline" onClick={() => router.back()}>
                            <X className="mr-2 h-4 w-4" />
                            Cancel
                        </Button>
                        <Button type="submit">
                            <Save className="mr-2 h-4 w-4" />
                            Create Rule
                        </Button>
                    </div>
                </div>

                <div className="grid gap-6 md:grid-cols-2">
                    {/* Basic Info */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Rule Details</CardTitle>
                            <CardDescription>Basic allocation rule information</CardDescription>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            <div className="grid gap-4 md:grid-cols-2">
                                <div className="space-y-2">
                                    <Label htmlFor="code">Code *</Label>
                                    <Input
                                        id="code"
                                        value={formData.code}
                                        onChange={(e) => setFormData({ ...formData, code: e.target.value.toUpperCase() })}
                                        placeholder="RENT-ALLOC"
                                        className={errors.code ? 'border-red-500' : ''}
                                    />
                                    {errors.code && <p className="text-sm text-red-500">{errors.code}</p>}
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="name">Name *</Label>
                                    <Input
                                        id="name"
                                        value={formData.name}
                                        onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                                        placeholder="Rent Allocation by Square Feet"
                                        className={errors.name ? 'border-red-500' : ''}
                                    />
                                    {errors.name && <p className="text-sm text-red-500">{errors.name}</p>}
                                </div>
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="description">Description</Label>
                                <Textarea
                                    id="description"
                                    value={formData.description}
                                    onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                                    placeholder="Describe how this allocation works..."
                                    rows={3}
                                />
                            </div>
                            <div className="flex items-center space-x-2">
                                <Switch
                                    id="autoReverse"
                                    checked={formData.autoReverse}
                                    onCheckedChange={(checked) => setFormData({ ...formData, autoReverse: checked })}
                                />
                                <Label htmlFor="autoReverse">Auto-reverse at period end</Label>
                            </div>
                        </CardContent>
                    </Card>

                    {/* Source & Driver */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Source & Driver</CardTitle>
                            <CardDescription>Configure allocation source and method</CardDescription>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            <div className="space-y-2">
                                <Label>Source GL Account *</Label>
                                <Select
                                    value={formData.sourceAccountId}
                                    onValueChange={(v) => setFormData({ ...formData, sourceAccountId: v })}
                                >
                                    <SelectTrigger className={errors.sourceAccountId ? 'border-red-500' : ''}>
                                        <SelectValue placeholder="Select source account" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {MOCK_GL_ACCOUNTS.map((acc) => (
                                            <SelectItem key={acc.id} value={acc.id}>
                                                {acc.number} - {acc.name}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                {errors.sourceAccountId && <p className="text-sm text-red-500">{errors.sourceAccountId}</p>}
                            </div>
                            <div className="space-y-2">
                                <Label>Allocation Type *</Label>
                                <Select
                                    value={formData.allocationType}
                                    onValueChange={(v) => setFormData({ ...formData, allocationType: v as AllocationType })}
                                >
                                    <SelectTrigger>
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
                                    <Label>Driver Unit Account *</Label>
                                    <Select
                                        value={formData.driverUnitAccountId}
                                        onValueChange={(v) => setFormData({ ...formData, driverUnitAccountId: v })}
                                    >
                                        <SelectTrigger className={errors.driverUnitAccountId ? 'border-red-500' : ''}>
                                            <SelectValue placeholder="Select driver account" />
                                        </SelectTrigger>
                                        <SelectContent>
                                            {MOCK_UNIT_ACCOUNTS.map((acc) => (
                                                <SelectItem key={acc.id} value={acc.id}>
                                                    {acc.number} - {acc.name}
                                                </SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                    {errors.driverUnitAccountId && <p className="text-sm text-red-500">{errors.driverUnitAccountId}</p>}
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
                                <CardDescription>Define where to distribute the amounts</CardDescription>
                            </div>
                            <Button type="button" variant="outline" onClick={addTarget}>
                                <Plus className="mr-2 h-4 w-4" />
                                Add Target
                            </Button>
                        </div>
                    </CardHeader>
                    <CardContent>
                        {formData.allocationType === 'FixedPercentage' && (
                            <div className={`mb-4 p-2 rounded ${totalPercentage === 100 ? 'bg-green-50' : 'bg-yellow-50'}`}>
                                <p className={`text-sm ${totalPercentage === 100 ? 'text-green-700' : 'text-yellow-700'}`}>
                                    Total Percentage: {totalPercentage}% {totalPercentage !== 100 && '(must equal 100%)'}
                                </p>
                            </div>
                        )}
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead>Target GL Account</TableHead>
                                    {formData.allocationType === 'FixedPercentage' && <TableHead>Percentage</TableHead>}
                                    {formData.allocationType === 'UnitAccountBased' && <TableHead>Target Driver</TableHead>}
                                    <TableHead className="w-[80px]">Actions</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {targets.map((target) => (
                                    <TableRow key={target.id}>
                                        <TableCell>
                                            <Select
                                                value={target.targetAccountId}
                                                onValueChange={(v) => updateTarget(target.id, 'targetAccountId', v)}
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
                                        {formData.allocationType === 'FixedPercentage' && (
                                            <TableCell>
                                                <Input
                                                    type="number"
                                                    value={target.fixedPercentage}
                                                    onChange={(e) => updateTarget(target.id, 'fixedPercentage', parseFloat(e.target.value) || 0)}
                                                    min={0}
                                                    max={100}
                                                    className="w-24"
                                                />
                                            </TableCell>
                                        )}
                                        {formData.allocationType === 'UnitAccountBased' && (
                                            <TableCell>
                                                <Select
                                                    value={target.targetDriverUnitAccountId}
                                                    onValueChange={(v) => updateTarget(target.id, 'targetDriverUnitAccountId', v)}
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
                                        )}
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
                        {errors.targets && <p className="text-sm text-red-500 mt-2">{errors.targets}</p>}
                    </CardContent>
                </Card>
            </form>
        </div>
    );
}
