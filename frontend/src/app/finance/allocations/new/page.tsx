'use client';

import React, { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { AlertTriangle, ChevronRight, Home, Plus, Save, Trash2, X } from 'lucide-react';
import { toast } from 'sonner';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
} from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { financeDataService } from '@/services/finance/finance-data.service';
import { unitAccountsDataService } from '@/services/finance/unit-accounts-data.service';
import type { Account } from '@/types/finance';
import type { AllocationType, UnitAccount } from '@/types/unit-accounts';

interface AllocationTargetRow {
    id: string;
    targetAccountId: string;
    fixedPercentage: string;
    targetDriverUnitAccountId: string;
}

function isEligibleGlAccount(account: Account) {
    return account.status === 'Active' && account.allowDirectPosting && !account.isControlAccount;
}

function accountLabel(account: Account) {
    return `${account.accountNumber || account.accountCode} - ${account.accountName}`;
}

function unitAccountLabel(account: UnitAccount) {
    return `${account.accountNumber} - ${account.name}`;
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
        { id: '1', targetAccountId: '', fixedPercentage: '', targetDriverUnitAccountId: '' },
    ]);
    const [glAccounts, setGlAccounts] = useState<Account[]>([]);
    const [unitAccounts, setUnitAccounts] = useState<UnitAccount[]>([]);
    const [errors, setErrors] = useState<Record<string, string>>({});
    const [isLoadingLookups, setIsLoadingLookups] = useState(true);
    const [isSaving, setIsSaving] = useState(false);

    useEffect(() => {
        let isMounted = true;

        const loadLookups = async () => {
            try {
                setIsLoadingLookups(true);
                const [accounts, unitPostingAccounts] = await Promise.all([
                    financeDataService.getAccounts({ status: 'Active', take: 1000 }),
                    unitAccountsDataService.getUnitAccounts({ isActive: true }),
                ]);

                if (!isMounted) return;

                setGlAccounts(accounts.filter(isEligibleGlAccount));
                setUnitAccounts(unitPostingAccounts);
            } catch (error: any) {
                toast.error(error?.message || 'Failed to load allocation rule lookups.');
            } finally {
                if (isMounted) {
                    setIsLoadingLookups(false);
                }
            }
        };

        loadLookups();

        return () => {
            isMounted = false;
        };
    }, []);

    const selectedSourceAccount = glAccounts.find((account) => account.id === formData.sourceAccountId);
    const targetAccountOptions = useMemo(() => {
        return glAccounts.filter((account) =>
            account.id !== formData.sourceAccountId &&
            (!selectedSourceAccount || account.accountType === selectedSourceAccount.accountType)
        );
    }, [glAccounts, formData.sourceAccountId, selectedSourceAccount]);

    const totalPercentage = targets.reduce((sum, target) => sum + (Number.parseFloat(target.fixedPercentage) || 0), 0);

    const validateForm = () => {
        const newErrors: Record<string, string> = {};
        const trimmedCode = formData.code.trim();

        if (!trimmedCode) newErrors.code = 'Code is required';
        if (!formData.name.trim()) newErrors.name = 'Name is required';
        if (!formData.sourceAccountId) newErrors.sourceAccountId = 'Source account is required';
        if (formData.allocationType === 'UnitAccountBased' && !formData.driverUnitAccountId) {
            newErrors.driverUnitAccountId = 'Driver account is required for unit-based allocation';
        }

        const populatedTargets = targets.filter((target) => target.targetAccountId);
        if (populatedTargets.length === 0) {
            newErrors.targets = 'At least one target account is required';
        }

        if (new Set(populatedTargets.map((target) => target.targetAccountId)).size !== populatedTargets.length) {
            newErrors.targets = 'Target accounts must be unique';
        }

        if (populatedTargets.some((target) => target.targetAccountId === formData.sourceAccountId)) {
            newErrors.targets = 'Target accounts cannot include the source account';
        }

        if (formData.allocationType === 'FixedPercentage') {
            if (populatedTargets.some((target) => (Number.parseFloat(target.fixedPercentage) || 0) <= 0)) {
                newErrors.targets = 'Fixed-percentage targets must have positive percentages';
            } else if (Math.abs(totalPercentage - 100) > 0.01) {
                newErrors.targets = 'Fixed percentages must total 100%';
            }
        }

        if (formData.allocationType === 'UnitAccountBased' && populatedTargets.some((target) => !target.targetDriverUnitAccountId)) {
            newErrors.targets = 'Each unit-based target requires a target driver unit account';
        }

        setErrors(newErrors);
        return Object.keys(newErrors).length === 0;
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!validateForm()) return;

        try {
            setIsSaving(true);
            const created = await unitAccountsDataService.createAllocationRule({
                code: formData.code.trim().toUpperCase(),
                name: formData.name.trim(),
                description: formData.description.trim() || undefined,
                sourceAccountId: formData.sourceAccountId,
                allocationType: formData.allocationType,
                driverUnitAccountId: formData.allocationType === 'UnitAccountBased'
                    ? formData.driverUnitAccountId
                    : undefined,
                autoReverse: formData.autoReverse,
                targets: targets
                    .filter((target) => target.targetAccountId)
                    .map((target) => ({
                        targetAccountId: target.targetAccountId,
                        fixedPercentage: formData.allocationType === 'FixedPercentage'
                            ? Number.parseFloat(target.fixedPercentage)
                            : undefined,
                        targetDriverUnitAccountId: formData.allocationType === 'UnitAccountBased'
                            ? target.targetDriverUnitAccountId
                            : undefined,
                    })),
            });

            toast.success(`Allocation rule ${created.code} created.`);
            router.push('/finance/allocations');
        } catch (error: any) {
            toast.error(error?.message || 'Failed to create allocation rule.');
        } finally {
            setIsSaving(false);
        }
    };

    const addTarget = () => {
        setTargets((current) => [
            ...current,
            { id: Date.now().toString(), targetAccountId: '', fixedPercentage: '', targetDriverUnitAccountId: '' },
        ]);
    };

    const removeTarget = (id: string) => {
        setTargets((current) => current.length > 1 ? current.filter((target) => target.id !== id) : current);
    };

    const updateTarget = (id: string, field: keyof AllocationTargetRow, value: string) => {
        setTargets((current) => current.map((target) => target.id === id ? { ...target, [field]: value } : target));
    };

    const handleSourceChange = (sourceAccountId: string) => {
        const sourceAccount = glAccounts.find((account) => account.id === sourceAccountId);
        setFormData((current) => ({ ...current, sourceAccountId }));
        setTargets((current) => current.map((target) => {
            const targetAccount = glAccounts.find((account) => account.id === target.targetAccountId);
            return targetAccount && sourceAccount && targetAccount.accountType !== sourceAccount.accountType
                ? { ...target, targetAccountId: '' }
                : target;
        }));
    };

    const handleAllocationTypeChange = (allocationType: AllocationType) => {
        setFormData((current) => ({
            ...current,
            allocationType,
            driverUnitAccountId: allocationType === 'UnitAccountBased' ? current.driverUnitAccountId : '',
        }));
        setTargets((current) => current.map((target) => ({
            ...target,
            fixedPercentage: allocationType === 'FixedPercentage' ? target.fixedPercentage : '',
            targetDriverUnitAccountId: allocationType === 'UnitAccountBased' ? target.targetDriverUnitAccountId : '',
        })));
    };

    return (
        <div className="space-y-6">
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
                        <Button type="submit" disabled={isSaving || isLoadingLookups}>
                            <Save className="mr-2 h-4 w-4" />
                            Create Rule
                        </Button>
                    </div>
                </div>

                {Object.values(errors).length > 0 && (
                    <Alert variant="destructive" className="mb-6">
                        <AlertTriangle className="h-4 w-4" />
                        <AlertDescription>{Object.values(errors)[0]}</AlertDescription>
                    </Alert>
                )}

                <div className="grid gap-6 md:grid-cols-2">
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

                    <Card>
                        <CardHeader>
                            <CardTitle>Source & Driver</CardTitle>
                            <CardDescription>Configure allocation source and method</CardDescription>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            <div className="space-y-2">
                                <Label>Source GL Account *</Label>
                                <Select value={formData.sourceAccountId} onValueChange={handleSourceChange} disabled={isLoadingLookups}>
                                    <SelectTrigger className={errors.sourceAccountId ? 'border-red-500' : ''}>
                                        <SelectValue placeholder="Select source account" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {glAccounts.map((account) => (
                                            <SelectItem key={account.id} value={account.id}>
                                                {accountLabel(account)}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                            </div>
                            <div className="space-y-2">
                                <Label>Allocation Type *</Label>
                                <Select value={formData.allocationType} onValueChange={(value) => handleAllocationTypeChange(value as AllocationType)}>
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
                                        onValueChange={(value) => setFormData({ ...formData, driverUnitAccountId: value })}
                                        disabled={isLoadingLookups}
                                    >
                                        <SelectTrigger className={errors.driverUnitAccountId ? 'border-red-500' : ''}>
                                            <SelectValue placeholder="Select driver account" />
                                        </SelectTrigger>
                                        <SelectContent>
                                            {unitAccounts.map((account) => (
                                                <SelectItem key={account.id} value={account.id}>
                                                    {unitAccountLabel(account)}
                                                </SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                </div>
                            )}
                        </CardContent>
                    </Card>
                </div>

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
                            <div className={`mb-4 p-2 rounded ${Math.abs(totalPercentage - 100) <= 0.01 ? 'bg-green-50' : 'bg-yellow-50'}`}>
                                <p className={`text-sm ${Math.abs(totalPercentage - 100) <= 0.01 ? 'text-green-700' : 'text-yellow-700'}`}>
                                    Total Percentage: {totalPercentage.toFixed(2)}%
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
                                                onValueChange={(value) => updateTarget(target.id, 'targetAccountId', value)}
                                                disabled={!formData.sourceAccountId || isLoadingLookups}
                                            >
                                                <SelectTrigger>
                                                    <SelectValue placeholder="Select account" />
                                                </SelectTrigger>
                                                <SelectContent>
                                                    {targetAccountOptions.map((account) => (
                                                        <SelectItem key={account.id} value={account.id}>
                                                            {accountLabel(account)}
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
                                                    onChange={(e) => updateTarget(target.id, 'fixedPercentage', e.target.value)}
                                                    min={0}
                                                    max={100}
                                                    step="0.01"
                                                    className="w-28"
                                                />
                                            </TableCell>
                                        )}
                                        {formData.allocationType === 'UnitAccountBased' && (
                                            <TableCell>
                                                <Select
                                                    value={target.targetDriverUnitAccountId}
                                                    onValueChange={(value) => updateTarget(target.id, 'targetDriverUnitAccountId', value)}
                                                    disabled={isLoadingLookups}
                                                >
                                                    <SelectTrigger>
                                                        <SelectValue placeholder="Select driver" />
                                                    </SelectTrigger>
                                                    <SelectContent>
                                                        {unitAccounts.map((account) => (
                                                            <SelectItem key={account.id} value={account.id}>
                                                                {unitAccountLabel(account)}
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
                    </CardContent>
                </Card>
            </form>
        </div>
    );
}
