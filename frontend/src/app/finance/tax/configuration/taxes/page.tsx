'use client';

import { useEffect, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { taxDataService } from '@/services/finance/tax-data.service';
import { Tax, TaxCategory, TaxApplicability, CreateTaxDto } from '@/types/tax';
import { Plus, Edit, Trash2, CheckCircle, XCircle } from 'lucide-react';
import { useToast } from '@/hooks/use-toast';
import Link from 'next/link';

export default function TaxesPage() {
    const [taxes, setTaxes] = useState<Tax[]>([]);
    const [loading, setLoading] = useState(true);
    const [dialogOpen, setDialogOpen] = useState(false);
    const [editingTax, setEditingTax] = useState<Tax | null>(null);
    const [formData, setFormData] = useState<CreateTaxDto>({
        code: '',
        name: '',
        rate: 0,
        category: TaxCategory.Standard,
        applicability: TaxApplicability.Both,
        isInputTaxDeductible: false,
        isActive: true,
        thresholdAmount: null,
    });
    const { toast } = useToast();

    useEffect(() => {
        loadTaxes();
    }, []);

    const loadTaxes = async () => {
        try {
            const data = await taxDataService.getTaxes();
            setTaxes(data);
        } catch (error) {
            console.error('Failed to load taxes:', error);
            toast({
                title: 'Error',
                description: 'Failed to load taxes',
                variant: 'destructive',
            });
        } finally {
            setLoading(false);
        }
    };

    const handleOpenDialog = (tax?: Tax) => {
        if (tax) {
            setEditingTax(tax);
            setFormData({
                code: tax.code,
                name: tax.name,
                rate: tax.rate,
                category: tax.category,
                applicability: tax.applicability,
                isInputTaxDeductible: tax.isInputTaxDeductible,
                isActive: tax.isActive,
                thresholdAmount: tax.thresholdAmount,
            });
        } else {
            setEditingTax(null);
            setFormData({
                code: '',
                name: '',
                rate: 0,
                category: TaxCategory.Standard,
                applicability: TaxApplicability.Both,
                isInputTaxDeductible: false,
                isActive: true,
                thresholdAmount: null,
            });
        }
        setDialogOpen(true);
    };

    const handleSave = async () => {
        try {
            if (editingTax) {
                await taxDataService.updateTax(editingTax.id, formData);
                toast({
                    title: 'Success',
                    description: 'Tax updated successfully',
                });
            } else {
                await taxDataService.createTax(formData);
                toast({
                    title: 'Success',
                    description: 'Tax created successfully',
                });
            }
            setDialogOpen(false);
            loadTaxes();
        } catch (error) {
            console.error('Failed to save tax:', error);
            toast({
                title: 'Error',
                description: 'Failed to save tax',
                variant: 'destructive',
            });
        }
    };

    const handleDelete = async (id: string) => {
        if (!confirm('Are you sure you want to delete this tax?')) return;

        try {
            await taxDataService.deleteTax(id);
            toast({
                title: 'Success',
                description: 'Tax deleted successfully',
            });
            loadTaxes();
        } catch (error) {
            console.error('Failed to delete tax:', error);
            toast({
                title: 'Error',
                description: 'Failed to delete tax',
                variant: 'destructive',
            });
        }
    };

    return (
        <div className="p-6 space-y-6">
            {/* Header */}
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-3xl font-bold">Taxes</h1>
                    <p className="text-muted-foreground">Manage individual tax definitions (VAT, NHIL, WHT, etc.)</p>
                </div>
                <Button onClick={() => handleOpenDialog()}>
                    <Plus className="mr-2 h-4 w-4" />
                    Add Tax
                </Button>
            </div>

            {/* Taxes List */}
            <Card>
                <CardHeader>
                    <CardTitle>All Taxes</CardTitle>
                    <CardDescription>{taxes.length} taxes configured</CardDescription>
                </CardHeader>
                <CardContent>
                    {loading ? (
                        <div className="text-center py-8 text-muted-foreground">Loading...</div>
                    ) : taxes.length === 0 ? (
                        <div className="text-center py-8 text-muted-foreground">
                            No taxes found. Click "Add Tax" to create one.
                        </div>
                    ) : (
                        <div className="space-y-3">
                            {taxes.map(tax => (
                                <div
                                    key={tax.id}
                                    className="flex items-center justify-between p-4 border rounded-lg hover:bg-accent transition-colors"
                                >
                                    <div className="flex-1">
                                        <div className="flex items-center gap-3 mb-2">
                                            <h3 className="font-semibold text-lg">{tax.name}</h3>
                                            <Badge variant="outline">{tax.code}</Badge>
                                            <Badge variant="secondary">{tax.rate}%</Badge>
                                            {tax.isActive ? (
                                                <Badge variant="outline" className="bg-green-50 text-green-700 border-green-200">
                                                    <CheckCircle className="mr-1 h-3 w-3" />
                                                    Active
                                                </Badge>
                                            ) : (
                                                <Badge variant="outline" className="bg-gray-50 text-gray-700 border-gray-200">
                                                    <XCircle className="mr-1 h-3 w-3" />
                                                    Inactive
                                                </Badge>
                                            )}
                                        </div>
                                        <div className="flex gap-2">
                                            <Badge variant="outline" className="bg-blue-50 text-blue-700 border-blue-200">
                                                {tax.category}
                                            </Badge>
                                            <Badge variant="outline" className="bg-green-50 text-green-700 border-green-200">
                                                {tax.applicability}
                                            </Badge>
                                            {tax.isInputTaxDeductible && (
                                                <Badge variant="outline" className="bg-purple-50 text-purple-700 border-purple-200">
                                                    Deductible
                                                </Badge>
                                            )}
                                            {tax.thresholdAmount && (
                                                <Badge variant="outline">
                                                    Threshold: {tax.thresholdAmount.toLocaleString()}
                                                </Badge>
                                            )}
                                        </div>
                                    </div>
                                    <div className="flex items-center gap-2">
                                        <Button
                                            variant="ghost"
                                            size="sm"
                                            onClick={() => handleOpenDialog(tax)}
                                        >
                                            <Edit className="h-4 w-4" />
                                        </Button>
                                        <Button
                                            variant="ghost"
                                            size="sm"
                                            onClick={() => handleDelete(tax.id)}
                                        >
                                            <Trash2 className="h-4 w-4 text-destructive" />
                                        </Button>
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}
                </CardContent>
            </Card>

            {/* Add/Edit Dialog */}
            <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
                <DialogContent className="max-w-2xl">
                    <DialogHeader>
                        <DialogTitle>{editingTax ? 'Edit Tax' : 'Add Tax'}</DialogTitle>
                        <DialogDescription>
                            {editingTax ? 'Update the tax details' : 'Create a new tax'}
                        </DialogDescription>
                    </DialogHeader>
                    <div className="grid gap-4 py-4">
                        <div className="grid grid-cols-2 gap-4">
                            <div>
                                <Label htmlFor="code">Tax Code *</Label>
                                <Input
                                    id="code"
                                    value={formData.code}
                                    onChange={(e) => setFormData({ ...formData, code: e.target.value.toUpperCase() })}
                                    placeholder="VAT, NHIL, etc."
                                    className="mt-1"
                                />
                            </div>
                            <div>
                                <Label htmlFor="name">Tax Name *</Label>
                                <Input
                                    id="name"
                                    value={formData.name}
                                    onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                                    placeholder="Value Added Tax"
                                    className="mt-1"
                                />
                            </div>
                        </div>
                        <div className="grid grid-cols-2 gap-4">
                            <div>
                                <Label htmlFor="rate">Tax Rate (%) *</Label>
                                <Input
                                    id="rate"
                                    type="number"
                                    step="0.01"
                                    value={formData.rate}
                                    onChange={(e) => setFormData({ ...formData, rate: parseFloat(e.target.value) })}
                                    className="mt-1"
                                />
                            </div>
                            <div>
                                <Label htmlFor="threshold">Threshold Check (Optional)</Label>
                                <Input
                                    id="threshold"
                                    type="number"
                                    value={formData.thresholdAmount || ''}
                                    onChange={(e) => setFormData({ ...formData, thresholdAmount: e.target.value ? parseFloat(e.target.value) : null })}
                                    placeholder="Min amount to apply"
                                    className="mt-1"
                                />
                            </div>
                        </div>
                        <div className="grid grid-cols-2 gap-4">
                            <div>
                                <Label htmlFor="category">Category *</Label>
                                <Select
                                    value={formData.category}
                                    onValueChange={(value: any) => setFormData({ ...formData, category: value })}
                                >
                                    <SelectTrigger className="mt-1">
                                        <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="Standard">Standard</SelectItem>
                                        <SelectItem value="Withholding">Withholding</SelectItem>
                                        <SelectItem value="Levy">Levy</SelectItem>
                                        <SelectItem value="Excise">Excise</SelectItem>
                                    </SelectContent>
                                </Select>
                            </div>
                            <div>
                                <Label htmlFor="applicability">Applicability *</Label>
                                <Select
                                    value={formData.applicability}
                                    onValueChange={(value: any) => setFormData({ ...formData, applicability: value })}
                                >
                                    <SelectTrigger className="mt-1">
                                        <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="Sales">Sales Only</SelectItem>
                                        <SelectItem value="Purchases">Purchases Only</SelectItem>
                                        <SelectItem value="Both">Both</SelectItem>
                                    </SelectContent>
                                </Select>
                            </div>
                        </div>
                        <div className="flex items-center space-x-2">
                            <Switch
                                id="isDeductible"
                                checked={formData.isInputTaxDeductible}
                                onCheckedChange={(checked) => setFormData({ ...formData, isInputTaxDeductible: checked })}
                            />
                            <Label htmlFor="isDeductible">Is Input Tax Deductible? (Recoverable)</Label>
                        </div>
                        <div className="flex items-center space-x-2">
                            <Switch
                                id="isActive"
                                checked={formData.isActive}
                                onCheckedChange={(checked) => setFormData({ ...formData, isActive: checked })}
                            />
                            <Label htmlFor="isActive">Active</Label>
                        </div>
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setDialogOpen(false)}>
                            Cancel
                        </Button>
                        <Button onClick={handleSave}>
                            {editingTax ? 'Update' : 'Create'}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}
