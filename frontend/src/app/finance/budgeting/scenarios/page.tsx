'use client';

import React, { useState, useEffect } from 'react';
import Link from 'next/link';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Calendar, Plus, ChevronRight, Calculator, RefreshCw } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { budgetDataService } from '@/services/finance/budget-data.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { BudgetScenario, CreateBudgetScenarioDto } from '@/types/budget';
import type { FiscalYear } from '@/types/finance';

export default function BudgetScenariosPage() {
    const { toast } = useToast();
    const { hasPermission } = useAuth();
    const canCreateScenario = hasPermission('Finance.Budgeting.Write');
    const [scenarios, setScenarios] = useState<BudgetScenario[]>([]);
    const [fiscalYears, setFiscalYears] = useState<FiscalYear[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
    const [formData, setFormData] = useState<CreateBudgetScenarioDto>({
        name: '',
        description: '',
        fiscalYearId: '',
        baseCurrencyCode: 'GHS',
    });

    useEffect(() => {
        loadData();
    }, []);

    const loadData = async () => {
        try {
            setIsLoading(true);
            const fiscalYearsData = await financeDataService.getFiscalYears();
            setFiscalYears(fiscalYearsData);

            const scenariosData = await budgetDataService.getScenarios(fiscalYearsData);
            setScenarios(scenariosData);
        } catch (error) {
            console.error('Failed to load budget data:', error);
            toast({
                title: 'Error',
                description: 'Failed to load budget scenarios.',
                variant: 'destructive',
            });
        } finally {
            setIsLoading(false);
        }
    };

    const handleCreate = async () => {
        if (!canCreateScenario) {
            toast({
                title: 'Permission Required',
                description: 'You do not have permission to create budget scenarios.',
                variant: 'destructive',
            });
            return;
        }

        if (!formData.name || !formData.fiscalYearId) {
            toast({
                title: 'Validation Error',
                description: 'Please fill in all required fields.',
                variant: 'destructive',
            });
            return;
        }

        try {
            await budgetDataService.createScenario(formData);
            toast({
                title: 'Success',
                description: 'Budget scenario created successfully.',
            });
            setIsCreateDialogOpen(false);
            setFormData({
                name: '',
                description: '',
                fiscalYearId: '',
                baseCurrencyCode: 'GHS',
            });
            loadData();
        } catch (error) {
            console.error('Failed to create scenario:', error);
            toast({
                title: 'Error',
                description: 'Failed to create budget scenario.',
                variant: 'destructive',
            });
        }
    };

    const getStatusBadge = (status: string) => {
        const variants: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
            Collecting: 'default',
            InReview: 'default',
            Approved: 'secondary',
            Superseded: 'outline',
            Archived: 'outline',
            Draft: 'outline',
        };
        return <Badge variant={variants[status] || 'default'}>{status}</Badge>;
    };

    const formatDate = (dateString?: string) => {
        if (!dateString) return '-';
        return new Date(dateString).toLocaleDateString('en-US', {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
        });
    };

    const getFiscalYearCode = (id: string) => {
        return fiscalYears.find(fy => fy.id === id)?.fiscalYearCode || id;
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <Calculator className="h-8 w-8" />
                        Budget Scenarios
                    </h1>
                    <p className="text-muted-foreground">
                        Manage budget versions and planning scenarios
                    </p>
                </div>
                <div className="flex gap-2">
                    <Button variant="outline" size="icon" onClick={loadData} disabled={isLoading}>
                        <RefreshCw className={`h-4 w-4 ${isLoading ? 'animate-spin' : ''}`} />
                    </Button>
                    {canCreateScenario && (
                        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
                            <DialogTrigger asChild>
                                <Button>
                                    <Plus className="mr-2 h-4 w-4" />
                                    New Scenario
                                </Button>
                            </DialogTrigger>
                            <DialogContent>
                                <DialogHeader>
                                    <DialogTitle>Create Budget Scenario</DialogTitle>
                                    <DialogDescription>
                                        Create a new budget version for a specific fiscal year.
                                    </DialogDescription>
                                </DialogHeader>
                                <div className="space-y-4 py-4">
                                    <div className="space-y-2">
                                        <Label htmlFor="name">Scenario Name <span className="text-red-500">*</span></Label>
                                        <Input
                                            id="name"
                                            placeholder="e.g. FY2026 Original Budget"
                                            value={formData.name}
                                            onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                                        />
                                    </div>
                                    <div className="space-y-2">
                                        <Label htmlFor="fiscalYear">Fiscal Year <span className="text-red-500">*</span></Label>
                                        <Select
                                            value={formData.fiscalYearId}
                                            onValueChange={(value) => setFormData({ ...formData, fiscalYearId: value })}
                                        >
                                            <SelectTrigger id="fiscalYear">
                                                <SelectValue placeholder="Select Fiscal Year" />
                                            </SelectTrigger>
                                            <SelectContent>
                                                {fiscalYears.map((fy) => (
                                                    <SelectItem key={fy.id} value={fy.id}>
                                                        {fy.fiscalYearName} ({fy.fiscalYearCode})
                                                    </SelectItem>
                                                ))}
                                            </SelectContent>
                                        </Select>
                                    </div>
                                    <div className="space-y-2">
                                        <Label htmlFor="description">Description</Label>
                                        <Input
                                            id="description"
                                            placeholder="Optional description"
                                            value={formData.description || ''}
                                            onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                                        />
                                    </div>
                                    <div className="space-y-2">
                                        <Label htmlFor="currency">Base Currency</Label>
                                        <Input
                                            id="currency"
                                            value={formData.baseCurrencyCode}
                                            disabled
                                            className="bg-muted"
                                        />
                                        <p className="text-xs text-muted-foreground">Budgeting always uses the system base currency for consolidation.</p>
                                    </div>
                                </div>
                                <DialogFooter>
                                    <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                                        Cancel
                                    </Button>
                                    <Button onClick={handleCreate}>Create Scenario</Button>
                                </DialogFooter>
                            </DialogContent>
                        </Dialog>
                    )}
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
                        <BreadcrumbPage>Budgeting</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Scenarios List */}
            <Card>
                <CardHeader>
                    <CardTitle>Budget Scenarios ({scenarios.length})</CardTitle>
                    <CardDescription>
                        All budget versions across fiscal years
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    {scenarios.length === 0 ? (
                        <div className="text-center py-8 text-muted-foreground">
                            {canCreateScenario
                                ? 'No budget scenarios found. Create one to get started.'
                                : 'No budget scenarios found.'}
                        </div>
                    ) : (
                        <div className="space-y-4">
                            {scenarios.map((scenario) => (
                                <div key={scenario.id} className="border rounded-lg p-4 flex items-center justify-between hover:bg-muted/50 transition-colors">
                                    <div className="flex items-start gap-4">
                                        <div className="p-2 bg-primary/10 rounded-full text-primary mt-1">
                                            <Calculator className="h-5 w-5" />
                                        </div>
                                        <div>
                                            <div className="flex items-center gap-2">
                                                <h3 className="font-semibold text-lg">{scenario.name}</h3>
                                                {getStatusBadge(scenario.status)}
                                                {scenario.isActive && <Badge variant="default" className="bg-green-600 hover:bg-green-700">Official</Badge>}
                                            </div>
                                            <p className="text-sm text-muted-foreground mt-1">
                                                {getFiscalYearCode(scenario.fiscalYearId)} • {scenario.baseCurrencyCode} • Updated {formatDate(scenario.updatedAt || scenario.createdAt)}
                                            </p>
                                            {scenario.description && (
                                                <p className="text-sm text-muted-foreground mt-1 italic">
                                                    {scenario.description}
                                                </p>
                                            )}
                                        </div>
                                    </div>
                                    <div>
                                        <Link href={`/finance/budgeting/scenarios/${scenario.id}`}>
                                            <Button variant="outline">
                                                Manage
                                                <ChevronRight className="ml-2 h-4 w-4" />
                                            </Button>
                                        </Link>
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
