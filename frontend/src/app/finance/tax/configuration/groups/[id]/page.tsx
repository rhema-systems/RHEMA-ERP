'use client';

import { useEffect, useState, use } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { taxDataService } from '@/services/finance/tax-data.service';
import { TaxGroup, Tax, TaxGroupComponent, TaxCategory, CompoundBasis, CreateTaxGroupComponentDto } from '@/types/tax';
import { Plus, Trash2, ArrowLeft, Save, Edit } from 'lucide-react';
import { useToast } from '@/hooks/use-toast';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

interface PageProps {
    params: Promise<{ id: string }>;
}

export default function TaxGroupDetailsPage({ params }: PageProps) {
    const resolvedParams = use(params);
    const router = useRouter();
    const { toast } = useToast();

    const [group, setGroup] = useState<TaxGroup | null>(null);
    const [availableTaxes, setAvailableTaxes] = useState<Tax[]>([]);
    const [loading, setLoading] = useState(true);
    const [dialogOpen, setDialogOpen] = useState(false);

    const [formData, setFormData] = useState<CreateTaxGroupComponentDto>({
        taxId: '',
        calculationOrder: 1,
        compoundBasis: CompoundBasis.BaseOnly,
    });

    useEffect(() => {
        loadData();
    }, [resolvedParams.id]);

    const loadData = async () => {
        try {
            const [groupData, taxesData] = await Promise.all([
                taxDataService.getTaxGroupById(resolvedParams.id),
                taxDataService.getTaxes({ isActive: true })
            ]);
            setGroup(groupData);
            setAvailableTaxes(taxesData);
        } catch (error) {
            console.error('Failed to load tax group details:', error);
            toast({
                title: 'Error',
                description: 'Failed to load tax group details',
                variant: 'destructive',
            });
        } finally {
            setLoading(false);
        }
    };

    const [editingComponentId, setEditingComponentId] = useState<string | null>(null);

    const handleSaveComponent = async () => {
        if (!group) return;
        try {
            if (editingComponentId) {
                await taxDataService.updateComponent(group.id, editingComponentId, {
                    calculationOrder: formData.calculationOrder,
                    compoundBasis: formData.compoundBasis,
                });
                toast({ title: 'Success', description: 'Component updated' });
            } else {
                await taxDataService.addComponent(group.id, formData);
                toast({ title: 'Success', description: 'Tax added to group' });
            }
            setDialogOpen(false);
            loadData();
        } catch (error) {
            console.error('Failed to save component:', error);
            toast({
                title: 'Error',
                description: 'Failed to save component',
                variant: 'destructive',
            });
        }
    };

    const handleEditComponent = (component: TaxGroupComponent) => {
        setEditingComponentId(component.id);
        setFormData({
            taxId: component.taxId,
            calculationOrder: component.calculationOrder,
            compoundBasis: component.compoundBasis,
        });
        setDialogOpen(true);
    };

    const handleRemoveComponent = async (componentId: string) => {
        if (!group || !confirm('Remove this tax from the group?')) return;
        try {
            await taxDataService.removeComponent(group.id, componentId);
            toast({
                title: 'Success',
                description: 'Component removed',
            });
            loadData();
        } catch (error) {
            console.error('Failed to remove component:', error);
            toast({
                title: 'Error',
                description: 'Failed to remove component',
                variant: 'destructive',
            });
        }
    };

    if (loading) {
        return <div className="p-6">Loading...</div>;
    }

    if (!group) {
        return <div className="p-6">Tax group not found</div>;
    }

    // Filter out taxes already in the group
    const taxesToAdd = availableTaxes.filter(
        t => !group.components.some(c => c.taxId === t.id)
    );

    return (
        <div className="p-6 space-y-6">
            <div className="flex items-center gap-4">
                <Link href="/finance/tax/configuration/groups">
                    <Button variant="ghost" size="icon">
                        <ArrowLeft className="h-4 w-4" />
                    </Button>
                </Link>
                <div>
                    <h1 className="text-3xl font-bold">{group.name}</h1>
                    <div className="flex items-center gap-2 mt-1">
                        <Badge variant="outline">{group.code}</Badge>
                        <Badge variant="secondary">{group.applicability}</Badge>
                    </div>
                </div>
            </div>

            <Card>
                <CardHeader className="flex flex-row items-center justify-between">
                    <div>
                        <CardTitle>Components</CardTitle>
                        <CardDescription>Taxes included in this group and their calculation order</CardDescription>
                    </div>
                    <Button onClick={() => setDialogOpen(true)}>
                        <Plus className="mr-2 h-4 w-4" />
                        Add Tax
                    </Button>
                </CardHeader>
                <CardContent>
                    {group.components.length === 0 ? (
                        <div className="text-center py-8 text-muted-foreground">
                            No taxes in this group yet. Add one to start.
                        </div>
                    ) : (
                        <div className="space-y-4">
                            {/* Sort by calculation order */}
                            {group.components
                                .sort((a, b) => a.calculationOrder - b.calculationOrder)
                                .map((component) => (
                                    <div
                                        key={component.id}
                                        className="flex items-center justify-between p-4 border rounded-lg"
                                    >
                                        <div className="flex items-center gap-4">
                                            <div className="flex flex-col items-center justify-center h-10 w-10 bg-slate-100 rounded-full font-bold text-slate-600">
                                                {component.calculationOrder}
                                            </div>
                                            <div>
                                                <h4 className="font-semibold">{component.taxName}</h4>
                                                <div className="flex items-center gap-2 text-sm text-muted-foreground">
                                                    <Badge variant="outline">{component.taxCode}</Badge>
                                                    <span>{component.taxRate}%</span>
                                                    <Badge variant="secondary" className="text-xs">
                                                        {component.compoundBasis}
                                                    </Badge>
                                                </div>
                                            </div>
                                        </div>
                                        <div className="flex gap-1">
                                            <Button
                                                variant="ghost"
                                                size="icon"
                                                onClick={() => handleEditComponent(component)}
                                            >
                                                <Edit className="h-4 w-4" />
                                            </Button>
                                            <Button
                                                variant="ghost"
                                                size="icon"
                                                onClick={() => handleRemoveComponent(component.id)}
                                                className="text-destructive"
                                            >
                                                <Trash2 className="h-4 w-4" />
                                            </Button>
                                        </div>
                                    </div>
                                ))}
                        </div>
                    )}
                </CardContent>
            </Card>

            <Dialog open={dialogOpen} onOpenChange={(open) => {
                setDialogOpen(open);
                if (!open) {
                    setEditingComponentId(null);
                    setFormData({
                        taxId: '',
                        calculationOrder: (group?.components.length || 0) + 1,
                        compoundBasis: CompoundBasis.BaseOnly,
                    });
                }
            }}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>{editingComponentId ? 'Edit Tax Component' : 'Add Tax to Group'}</DialogTitle>
                        <DialogDescription>
                            {editingComponentId ? 'Modify tax calculation settings' : 'Select a tax and define how it\'s calculated'}
                        </DialogDescription>
                    </DialogHeader>
                    <div className="grid gap-4 py-4">
                        <div className="grid gap-2">
                            <Label htmlFor="tax">Tax *</Label>
                            <Select
                                value={formData.taxId}
                                onValueChange={(val) => setFormData({ ...formData, taxId: val })}
                                disabled={!!editingComponentId}
                            >
                                <SelectTrigger>
                                    <SelectValue placeholder="Select a tax..." />
                                </SelectTrigger>
                                <SelectContent>
                                    {taxesToAdd.concat(
                                        editingComponentId
                                            ? availableTaxes.filter(t => t.id === formData.taxId)
                                            : []
                                    ).map(t => (
                                        <SelectItem key={t.id} value={t.id}>
                                            {t.name} ({t.rate}%)
                                        </SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="grid grid-cols-2 gap-4">
                            <div className="grid gap-2">
                                <Label htmlFor="order">Calculation Order</Label>
                                <Input
                                    id="order"
                                    type="number"
                                    value={formData.calculationOrder}
                                    onChange={(e) => setFormData({ ...formData, calculationOrder: parseInt(e.target.value) })}
                                />
                            </div>
                            <div className="grid gap-2">
                                <Label htmlFor="basis">Basis</Label>
                                <Select
                                    value={formData.compoundBasis}
                                    onValueChange={(val: any) => setFormData({ ...formData, compoundBasis: val })}
                                >
                                    <SelectTrigger>
                                        <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="BaseOnly">Base Amount Only</SelectItem>
                                        <SelectItem value="Cumulative">Cumulative (Compound)</SelectItem>
                                    </SelectContent>
                                </Select>
                            </div>
                        </div>
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setDialogOpen(false)}>
                            Cancel
                        </Button>
                        <Button onClick={handleSaveComponent} disabled={!formData.taxId}>
                            {editingComponentId ? 'Update' : 'Add Tax'}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}

