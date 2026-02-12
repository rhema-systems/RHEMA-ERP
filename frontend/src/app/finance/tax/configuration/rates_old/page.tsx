'use client';

import { useEffect, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { taxDataService } from '@/services/finance/tax-data.service';
import type { TaxRate, TaxType, CreateTaxRateDto } from '@/types/tax';
import { Plus, Edit, History, CheckCircle } from 'lucide-react';
import { useToast } from '@/hooks/use-toast';

export default function TaxRatesPage() {
    const [taxRates, setTaxRates] = useState<TaxRate[]>([]);
    const [taxTypes, setTaxTypes] = useState<TaxType[]>([]);
    const [loading, setLoading] = useState(true);
    const [dialogOpen, setDialogOpen] = useState(false);
    const [editingRate, setEditingRate] = useState<TaxRate | null>(null);
    const [selectedTaxType, setSelectedTaxType] = useState<string>('all');
    const [formData, setFormData] = useState<CreateTaxRateDto>({
        taxTypeId: '',
        rate: 0,
        effectiveFrom: new Date().toISOString().split('T')[0],
        effectiveTo: undefined,
    });
    const { toast } = useToast();

    useEffect(() => {
        loadData();
    }, []);

    const loadData = async () => {
        try {
            const [rates, types] = await Promise.all([
                taxDataService.getTaxRates(),
                taxDataService.getTaxTypes(),
            ]);
            setTaxRates(rates);
            setTaxTypes(types.filter(t => t.isActive));
        } catch (error) {
            console.error('Failed to load data:', error);
            toast({
                title: 'Error',
                description: 'Failed to load tax rates',
                variant: 'destructive',
            });
        } finally {
            setLoading(false);
        }
    };

    const handleOpenDialog = (rate?: TaxRate) => {
        if (rate) {
            setEditingRate(rate);
            setFormData({
                taxTypeId: rate.taxTypeId,
                rate: rate.rate,
                effectiveFrom: rate.effectiveFrom,
                effectiveTo: rate.effectiveTo,
            });
        } else {
            setEditingRate(null);
            setFormData({
                taxTypeId: taxTypes[0]?.id || '',
                rate: 0,
                effectiveFrom: new Date().toISOString().split('T')[0],
                effectiveTo: undefined,
            });
        }
        setDialogOpen(true);
    };

    const handleSave = async () => {
        try {
            if (editingRate) {
                await taxDataService.updateTaxRate(editingRate.id, formData);
                toast({
                    title: 'Success',
                    description: 'Tax rate updated successfully',
                });
            } else {
                await taxDataService.createTaxRate(formData);
                toast({
                    title: 'Success',
                    description: 'Tax rate created successfully',
                });
            }
            setDialogOpen(false);
            loadData();
        } catch (error) {
            console.error('Failed to save tax rate:', error);
            toast({
                title: 'Error',
                description: 'Failed to save tax rate',
                variant: 'destructive',
            });
        }
    };

    const getTaxTypeName = (taxTypeId: string) => {
        return taxTypes.find(t => t.id === taxTypeId)?.name || 'Unknown';
    };

    const getTaxTypeCode = (taxTypeId: string) => {
        return taxTypes.find(t => t.id === taxTypeId)?.code || '';
    };

    const filteredRates = selectedTaxType === 'all'
        ? taxRates
        : taxRates.filter(r => r.taxTypeId === selectedTaxType);

    const groupedRates = filteredRates.reduce((acc, rate) => {
        const taxTypeId = rate.taxTypeId;
        if (!acc[taxTypeId]) {
            acc[taxTypeId] = [];
        }
        acc[taxTypeId].push(rate);
        return acc;
    }, {} as Record<string, TaxRate[]>);

    return (
        <div className="p-6 space-y-6">
            {/* Header */}
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-3xl font-bold">Tax Rates</h1>
                    <p className="text-muted-foreground">Manage tax rates and their effective periods</p>
                </div>
                <Button onClick={() => handleOpenDialog()}>
                    <Plus className="mr-2 h-4 w-4" />
                    Add Tax Rate
                </Button>
            </div>

            {/* Filter */}
            <Card>
                <CardContent className="pt-6">
                    <div className="flex items-center gap-4">
                        <Label htmlFor="taxTypeFilter">Filter by Tax Type:</Label>
                        <Select value={selectedTaxType} onValueChange={setSelectedTaxType}>
                            <SelectTrigger className="w-64">
                                <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                                <SelectItem value="all">All Tax Types</SelectItem>
                                {taxTypes.map(type => (
                                    <SelectItem key={type.id} value={type.id}>
                                        {type.name} ({type.code})
                                    </SelectItem>
                                ))}
                            </SelectContent>
                        </Select>
                    </div>
                </CardContent>
            </Card>

            {/* Tax Rates by Type */}
            {loading ? (
                <Card>
                    <CardContent className="py-8">
                        <div className="text-center text-muted-foreground">Loading...</div>
                    </CardContent>
                </Card>
            ) : Object.keys(groupedRates).length === 0 ? (
                <Card>
                    <CardContent className="py-8">
                        <div className="text-center text-muted-foreground">
                            No tax rates found. Click "Add Tax Rate" to create one.
                        </div>
                    </CardContent>
                </Card>
            ) : (
                Object.entries(groupedRates).map(([taxTypeId, rates]) => (
                    <Card key={taxTypeId}>
                        <CardHeader>
                            <div className="flex items-center justify-between">
                                <div>
                                    <CardTitle className="flex items-center gap-2">
                                        {getTaxTypeName(taxTypeId)}
                                        <Badge variant="outline">{getTaxTypeCode(taxTypeId)}</Badge>
                                    </CardTitle>
                                    <CardDescription>
                                        {rates.length} rate{rates.length !== 1 ? 's' : ''} configured
                                    </CardDescription>
                                </div>
                                <Button
                                    variant="outline"
                                    size="sm"
                                    onClick={() => {
                                        setFormData({
                                            ...formData,
                                            taxTypeId: taxTypeId,
                                        });
                                        handleOpenDialog();
                                    }}
                                >
                                    <Plus className="mr-2 h-4 w-4" />
                                    Add Rate
                                </Button>
                            </div>
                        </CardHeader>
                        <CardContent>
                            <div className="space-y-2">
                                {rates
                                    .sort((a, b) => new Date(b.effectiveFrom).getTime() - new Date(a.effectiveFrom).getTime())
                                    .map(rate => {
                                        const isActive = !rate.effectiveTo || new Date(rate.effectiveTo) > new Date();
                                        return (
                                            <div
                                                key={rate.id}
                                                className="flex items-center justify-between p-3 border rounded-lg hover:bg-accent transition-colors"
                                            >
                                                <div className="flex items-center gap-4">
                                                    <div className="text-2xl font-bold text-blue-600">
                                                        {rate.rate}%
                                                    </div>
                                                    <div>
                                                        <div className="flex items-center gap-2">
                                                            <span className="text-sm font-medium">
                                                                Effective from: {rate.effectiveFrom}
                                                            </span>
                                                            {isActive && (
                                                                <Badge variant="outline" className="bg-green-50 text-green-700 border-green-200">
                                                                    <CheckCircle className="mr-1 h-3 w-3" />
                                                                    Current
                                                                </Badge>
                                                            )}
                                                        </div>
                                                        {rate.effectiveTo && (
                                                            <span className="text-sm text-muted-foreground">
                                                                Until: {rate.effectiveTo}
                                                            </span>
                                                        )}
                                                    </div>
                                                </div>
                                                <div className="flex items-center gap-2">
                                                    <Button
                                                        variant="ghost"
                                                        size="sm"
                                                        onClick={() => handleOpenDialog(rate)}
                                                    >
                                                        <Edit className="h-4 w-4" />
                                                    </Button>
                                                </div>
                                            </div>
                                        );
                                    })}
                            </div>
                        </CardContent>
                    </Card>
                ))
            )}

            {/* Add/Edit Dialog */}
            <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>{editingRate ? 'Edit Tax Rate' : 'Add Tax Rate'}</DialogTitle>
                        <DialogDescription>
                            {editingRate ? 'Update the tax rate details' : 'Create a new tax rate'}
                        </DialogDescription>
                    </DialogHeader>
                    <div className="grid gap-4 py-4">
                        <div>
                            <Label htmlFor="taxType">Tax Type *</Label>
                            <Select
                                value={formData.taxTypeId}
                                onValueChange={(value) => setFormData({ ...formData, taxTypeId: value })}
                                disabled={!!editingRate}
                            >
                                <SelectTrigger className="mt-1">
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    {taxTypes.map(type => (
                                        <SelectItem key={type.id} value={type.id}>
                                            {type.name} ({type.code})
                                        </SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                        <div>
                            <Label htmlFor="rate">Rate (%) *</Label>
                            <Input
                                id="rate"
                                type="number"
                                step="0.01"
                                value={formData.rate}
                                onChange={(e) => setFormData({ ...formData, rate: parseFloat(e.target.value) })}
                                placeholder="15.00"
                                className="mt-1"
                            />
                        </div>
                        <div className="grid grid-cols-2 gap-4">
                            <div>
                                <Label htmlFor="effectiveFrom">Effective From *</Label>
                                <Input
                                    id="effectiveFrom"
                                    type="date"
                                    value={formData.effectiveFrom}
                                    onChange={(e) => setFormData({ ...formData, effectiveFrom: e.target.value })}
                                    className="mt-1"
                                />
                            </div>
                            <div>
                                <Label htmlFor="effectiveTo">Effective To</Label>
                                <Input
                                    id="effectiveTo"
                                    type="date"
                                    value={formData.effectiveTo || ''}
                                    onChange={(e) => setFormData({ ...formData, effectiveTo: e.target.value || undefined })}
                                    className="mt-1"
                                />
                            </div>
                        </div>
                        <p className="text-xs text-muted-foreground">
                            Leave "Effective To" empty if this rate is ongoing
                        </p>
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setDialogOpen(false)}>
                            Cancel
                        </Button>
                        <Button onClick={handleSave}>
                            {editingRate ? 'Update' : 'Create'}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}
