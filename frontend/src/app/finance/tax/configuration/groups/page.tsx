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
import { TaxGroup, TaxApplicability, CreateTaxGroupDto } from '@/types/tax';
import { Plus, Edit, Trash2, CheckCircle, XCircle, Layers } from 'lucide-react';
import { useToast } from '@/hooks/use-toast';
import Link from 'next/link';

export default function TaxGroupsPage() {
    const [groups, setGroups] = useState<TaxGroup[]>([]);
    const [loading, setLoading] = useState(true);
    const [dialogOpen, setDialogOpen] = useState(false);
    const [editingGroup, setEditingGroup] = useState<TaxGroup | null>(null);
    const [formData, setFormData] = useState<CreateTaxGroupDto>({
        code: '',
        name: '',
        description: '',
        applicability: TaxApplicability.Sales,
        isDefault: false,
        isActive: true,
    });
    const { toast } = useToast();

    useEffect(() => {
        loadGroups();
    }, []);

    const loadGroups = async () => {
        try {
            const data = await taxDataService.getTaxGroups();
            setGroups(data);
        } catch (error) {
            console.error('Failed to load tax groups:', error);
            toast({
                title: 'Error',
                description: 'Failed to load tax groups',
                variant: 'destructive',
            });
        } finally {
            setLoading(false);
        }
    };

    const handleOpenDialog = (group?: TaxGroup) => {
        if (group) {
            setEditingGroup(group);
            setFormData({
                code: group.code,
                name: group.name,
                description: group.description || '',
                applicability: group.applicability,
                isDefault: group.isDefault,
                isActive: group.isActive,
            });
        } else {
            setEditingGroup(null);
            setFormData({
                code: '',
                name: '',
                description: '',
                applicability: TaxApplicability.Sales,
                isDefault: false,
                isActive: true,
            });
        }
        setDialogOpen(true);
    };

    const handleSave = async () => {
        try {
            if (editingGroup) {
                await taxDataService.updateTaxGroup(editingGroup.id, formData);
                toast({
                    title: 'Success',
                    description: 'Tax group updated successfully',
                });
            } else {
                await taxDataService.createTaxGroup(formData);
                toast({
                    title: 'Success',
                    description: 'Tax group created successfully',
                });
            }
            setDialogOpen(false);
            loadGroups();
        } catch (error) {
            console.error('Failed to save tax group:', error);
            toast({
                title: 'Error',
                description: 'Failed to save tax group',
                variant: 'destructive',
            });
        }
    };

    const handleDelete = async (id: string) => {
        if (!confirm('Are you sure you want to delete this tax group?')) return;

        try {
            await taxDataService.deleteTaxGroup(id);
            toast({
                title: 'Success',
                description: 'Tax group deleted successfully',
            });
            loadGroups();
        } catch (error) {
            console.error('Failed to delete tax group:', error);
            toast({
                title: 'Error',
                description: 'Failed to delete tax group',
                variant: 'destructive',
            });
        }
    };

    return (
        <div className="p-6 space-y-6">
            {/* Header */}
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-3xl font-bold">Tax Groups</h1>
                    <p className="text-muted-foreground">Manage tax bundles and compound calculations (e.g., VAT Scheme)</p>
                </div>
                <Button onClick={() => handleOpenDialog()}>
                    <Plus className="mr-2 h-4 w-4" />
                    Add Tax Group
                </Button>
            </div>

            {/* Groups List */}
            <Card>
                <CardHeader>
                    <CardTitle>All Tax Groups</CardTitle>
                    <CardDescription>{groups.length} groups configured</CardDescription>
                </CardHeader>
                <CardContent>
                    {loading ? (
                        <div className="text-center py-8 text-muted-foreground">Loading...</div>
                    ) : groups.length === 0 ? (
                        <div className="text-center py-8 text-muted-foreground">
                            No tax groups found. Click "Add Tax Group" to create one.
                        </div>
                    ) : (
                        <div className="space-y-3">
                            {groups.map(group => (
                                <div
                                    key={group.id}
                                    className="flex items-center justify-between p-4 border rounded-lg hover:bg-accent transition-colors"
                                >
                                    <div className="flex-1">
                                        <div className="flex items-center gap-3 mb-2">
                                            <Layers className="h-5 w-5 text-muted-foreground" />
                                            <h3 className="font-semibold text-lg">{group.name}</h3>
                                            <Badge variant="outline">{group.code}</Badge>
                                            {group.isDefault && (
                                                <Badge variant="secondary">Default</Badge>
                                            )}
                                            {group.isActive ? (
                                                <Badge variant="outline" className="bg-green-50 text-green-700 border-green-200">
                                                    Active
                                                </Badge>
                                            ) : (
                                                <Badge variant="outline" className="bg-gray-50 text-gray-700 border-gray-200">
                                                    Inactive
                                                </Badge>
                                            )}
                                        </div>
                                        <p className="text-sm text-muted-foreground mb-2">{group.description}</p>
                                        <div className="flex gap-2 items-center">
                                            <Badge variant="outline" className="bg-blue-50 text-blue-700 border-blue-200">
                                                {group.applicability}
                                            </Badge>
                                            <span className="text-xs text-muted-foreground">
                                                {group.components?.length || 0} Components
                                            </span>
                                        </div>
                                    </div>
                                    <div className="flex items-center gap-2">
                                        <Link href={`/finance/tax/configuration/groups/${group.id}`}>
                                            <Button variant="outline" size="sm">
                                                Manage Components
                                            </Button>
                                        </Link>
                                        <Button
                                            variant="ghost"
                                            size="sm"
                                            onClick={() => handleOpenDialog(group)}
                                        >
                                            <Edit className="h-4 w-4" />
                                        </Button>
                                        <Button
                                            variant="ghost"
                                            size="sm"
                                            onClick={() => handleDelete(group.id)}
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
                        <DialogTitle>{editingGroup ? 'Edit Tax Group' : 'Add Tax Group'}</DialogTitle>
                        <DialogDescription>
                            {editingGroup ? 'Update the tax group details' : 'Create a new tax group'}
                        </DialogDescription>
                    </DialogHeader>
                    <div className="grid gap-4 py-4">
                        <div className="grid grid-cols-2 gap-4">
                            <div>
                                <Label htmlFor="code">Group Code *</Label>
                                <Input
                                    id="code"
                                    value={formData.code}
                                    onChange={(e) => setFormData({ ...formData, code: e.target.value.toUpperCase() })}
                                    placeholder="VAT-STD-SCHEME"
                                    className="mt-1"
                                />
                            </div>
                            <div>
                                <Label htmlFor="name">Group Name *</Label>
                                <Input
                                    id="name"
                                    value={formData.name}
                                    onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                                    placeholder="Standard VAT Scheme"
                                    className="mt-1"
                                />
                            </div>
                        </div>
                        <div>
                            <Label htmlFor="description">Description</Label>
                            <Input
                                id="description"
                                value={formData.description}
                                onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                                placeholder="Includes NHIL, GETFund, COVID, and VAT"
                                className="mt-1"
                            />
                        </div>
                        <div className="grid grid-cols-2 gap-4">
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
                                id="isDefault"
                                checked={formData.isDefault}
                                onCheckedChange={(checked) => setFormData({ ...formData, isDefault: checked })}
                            />
                            <Label htmlFor="isDefault">Set as Default Group</Label>
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
                            {editingGroup ? 'Update' : 'Create'}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}
