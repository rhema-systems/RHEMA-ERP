'use client';

import { useEffect, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { taxDataService } from '@/services/finance/tax-data.service';
import type { TaxRule, TaxType, CreateTaxRuleDto } from '@/types/tax';
import { Plus, Edit, Trash2, CheckCircle, XCircle } from 'lucide-react';
import { useToast } from '@/hooks/use-toast';

export default function TaxRulesPage() {
    const [taxRules, setTaxRules] = useState<TaxRule[]>([]);
    const [taxTypes, setTaxTypes] = useState<TaxType[]>([]);
    const [loading, setLoading] = useState(true);
    const [dialogOpen, setDialogOpen] = useState(false);
    const [editingRule, setEditingRule] = useState<TaxRule | null>(null);
    const [formData, setFormData] = useState<CreateTaxRuleDto>({
        taxTypeId: '',
        ruleName: '',
        description: '',
        applicabilityCondition: '',
        priority: 1,
        isActive: true,
    });
    const { toast } = useToast();

    useEffect(() => {
        loadData();
    }, []);

    const loadData = async () => {
        try {
            const [rules, types] = await Promise.all([
                taxDataService.getTaxRules(),
                taxDataService.getTaxTypes(),
            ]);
            setTaxRules(rules);
            setTaxTypes(types);
        } catch (error) {
            console.error('Failed to load data:', error);
            toast({
                title: 'Error',
                description: 'Failed to load tax rules',
                variant: 'destructive',
            });
        } finally {
            setLoading(false);
        }
    };

    const handleOpenDialog = (rule?: TaxRule) => {
        if (rule) {
            setEditingRule(rule);
            setFormData({
                taxTypeId: rule.taxTypeId,
                ruleName: rule.ruleName,
                description: rule.description || '',
                applicabilityCondition: rule.applicabilityCondition || '',
                priority: rule.priority,
                isActive: rule.isActive,
            });
        } else {
            setEditingRule(null);
            setFormData({
                taxTypeId: taxTypes[0]?.id || '',
                ruleName: '',
                description: '',
                applicabilityCondition: '',
                priority: 1,
                isActive: true,
            });
        }
        setDialogOpen(true);
    };

    const handleSave = async () => {
        try {
            if (editingRule) {
                await taxDataService.updateTaxRule(editingRule.id, formData);
                toast({
                    title: 'Success',
                    description: 'Tax rule updated successfully',
                });
            } else {
                await taxDataService.createTaxRule(formData);
                toast({
                    title: 'Success',
                    description: 'Tax rule created successfully',
                });
            }
            setDialogOpen(false);
            loadData();
        } catch (error) {
            console.error('Failed to save tax rule:', error);
            toast({
                title: 'Error',
                description: 'Failed to save tax rule',
                variant: 'destructive',
            });
        }
    };

    const handleDelete = async (id: string) => {
        if (!confirm('Are you sure you want to delete this tax rule?')) return;

        try {
            await taxDataService.deleteTaxRule(id);
            toast({
                title: 'Success',
                description: 'Tax rule deleted successfully',
            });
            loadData();
        } catch (error) {
            console.error('Failed to delete tax rule:', error);
            toast({
                title: 'Error',
                description: 'Failed to delete tax rule',
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

    return (
        <div className="p-6 space-y-6">
            {/* Header */}
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-3xl font-bold">Tax Rules</h1>
                    <p className="text-muted-foreground">Manage tax applicability rules and conditions</p>
                </div>
                <Button onClick={() => handleOpenDialog()}>
                    <Plus className="mr-2 h-4 w-4" />
                    Add Tax Rule
                </Button>
            </div>

            {/* Tax Rules List */}
            <Card>
                <CardHeader>
                    <CardTitle>All Tax Rules</CardTitle>
                    <CardDescription>{taxRules.length} rules configured</CardDescription>
                </CardHeader>
                <CardContent>
                    {loading ? (
                        <div className="text-center py-8 text-muted-foreground">Loading...</div>
                    ) : taxRules.length === 0 ? (
                        <div className="text-center py-8 text-muted-foreground">
                            No tax rules found. Click "Add Tax Rule" to create one.
                        </div>
                    ) : (
                        <div className="space-y-3">
                            {taxRules
                                .sort((a, b) => a.priority - b.priority)
                                .map(rule => (
                                    <div
                                        key={rule.id}
                                        className="flex items-start justify-between p-4 border rounded-lg hover:bg-accent transition-colors"
                                    >
                                        <div className="flex-1">
                                            <div className="flex items-center gap-3 mb-2">
                                                <Badge variant="outline" className="text-lg px-3 py-1">
                                                    #{rule.priority}
                                                </Badge>
                                                <h3 className="font-semibold text-lg">{rule.ruleName}</h3>
                                                {rule.isActive ? (
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
                                            <div className="flex items-center gap-2 mb-2">
                                                <span className="text-sm font-medium">Tax Type:</span>
                                                <Badge variant="outline" className="bg-blue-50 text-blue-700 border-blue-200">
                                                    {getTaxTypeName(rule.taxTypeId)} ({getTaxTypeCode(rule.taxTypeId)})
                                                </Badge>
                                            </div>
                                            {rule.description && (
                                                <p className="text-sm text-muted-foreground mb-2">{rule.description}</p>
                                            )}
                                            {rule.applicabilityCondition && (
                                                <div className="bg-muted p-2 rounded text-sm font-mono">
                                                    {rule.applicabilityCondition}
                                                </div>
                                            )}
                                        </div>
                                        <div className="flex items-center gap-2 ml-4">
                                            <Button
                                                variant="ghost"
                                                size="sm"
                                                onClick={() => handleOpenDialog(rule)}
                                            >
                                                <Edit className="h-4 w-4" />
                                            </Button>
                                            <Button
                                                variant="ghost"
                                                size="sm"
                                                onClick={() => handleDelete(rule.id)}
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

            {/* Information Card */}
            <Card className="bg-blue-50 border-blue-200">
                <CardHeader>
                    <CardTitle className="text-blue-900">About Tax Rules</CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="text-sm space-y-2 text-blue-900">
                        <p>
                            <strong>Priority:</strong> Lower numbers are evaluated first. Use priority to control the order of rule evaluation.
                        </p>
                        <p>
                            <strong>Applicability Condition:</strong> Define conditions when this tax should apply (e.g., "Amount &gt; 2000" for WHT threshold).
                        </p>
                        <p>
                            <strong>Examples:</strong>
                        </p>
                        <ul className="list-disc list-inside ml-4 space-y-1">
                            <li>WHT on Services: "TransactionType = 'Service' AND Amount &gt; 2000"</li>
                            <li>VAT Exempt: "ProductCategory = 'Medical Supplies'"</li>
                            <li>Reduced Rate: "CustomerType = 'Charity'"</li>
                        </ul>
                    </div>
                </CardContent>
            </Card>

            {/* Add/Edit Dialog */}
            <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
                <DialogContent className="max-w-2xl">
                    <DialogHeader>
                        <DialogTitle>{editingRule ? 'Edit Tax Rule' : 'Add Tax Rule'}</DialogTitle>
                        <DialogDescription>
                            {editingRule ? 'Update the tax rule details' : 'Create a new tax rule'}
                        </DialogDescription>
                    </DialogHeader>
                    <div className="grid gap-4 py-4">
                        <div>
                            <Label htmlFor="taxType">Tax Type *</Label>
                            <Select
                                value={formData.taxTypeId}
                                onValueChange={(value) => setFormData({ ...formData, taxTypeId: value })}
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
                            <Label htmlFor="ruleName">Rule Name *</Label>
                            <Input
                                id="ruleName"
                                value={formData.ruleName}
                                onChange={(e) => setFormData({ ...formData, ruleName: e.target.value })}
                                placeholder="WHT Threshold Rule"
                                className="mt-1"
                            />
                        </div>
                        <div>
                            <Label htmlFor="description">Description</Label>
                            <Textarea
                                id="description"
                                value={formData.description}
                                onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                                placeholder="Brief description of when this rule applies"
                                className="mt-1"
                                rows={2}
                            />
                        </div>
                        <div>
                            <Label htmlFor="applicabilityCondition">Applicability Condition</Label>
                            <Textarea
                                id="applicabilityCondition"
                                value={formData.applicabilityCondition}
                                onChange={(e) => setFormData({ ...formData, applicabilityCondition: e.target.value })}
                                placeholder="Amount > 2000 AND TransactionType = 'Service'"
                                className="mt-1 font-mono text-sm"
                                rows={3}
                            />
                            <p className="text-xs text-muted-foreground mt-1">
                                Define the condition when this tax rule should apply
                            </p>
                        </div>
                        <div>
                            <Label htmlFor="priority">Priority *</Label>
                            <Input
                                id="priority"
                                type="number"
                                value={formData.priority}
                                onChange={(e) => setFormData({ ...formData, priority: parseInt(e.target.value) })}
                                placeholder="1"
                                className="mt-1"
                                min="1"
                            />
                            <p className="text-xs text-muted-foreground mt-1">
                                Lower numbers are evaluated first
                            </p>
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
                            {editingRule ? 'Update' : 'Create'}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}
