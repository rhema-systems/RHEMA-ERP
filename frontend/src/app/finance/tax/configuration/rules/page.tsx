'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import {
    Plus,
    Search,
    Edit2,
    Trash2,
    ArrowUp,
    ArrowDown,
    CheckCircle,
    XCircle,
    Power,
    Layers
} from 'lucide-react';
import { taxDataService } from '@/services/finance/tax-data.service';
import { TaxRule, TaxGroup, CreateTaxRuleDto, UpdateTaxRuleDto } from '@/types/tax';
import { useToast } from '@/components/ui/use-toast';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from "@/components/ui/dialog";
import { Label } from '@/components/ui/label';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from "@/components/ui/select";
import { Checkbox } from "@/components/ui/checkbox";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from "@/components/ui/table";

export default function TaxRulesPage() {
    const router = useRouter();
    const { toast } = useToast();
    const [rules, setRules] = useState<TaxRule[]>([]);
    const [taxGroups, setTaxGroups] = useState<TaxGroup[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState('');

    // Dialog State
    const [isDialogOpen, setIsDialogOpen] = useState(false);
    const [editingRule, setEditingRule] = useState<TaxRule | null>(null);
    const [isSaving, setIsSaving] = useState(false);

    // Form State
    const [formData, setFormData] = useState<Partial<CreateTaxRuleDto>>({
        name: '',
        description: '',
        priority: 0,
        taxGroupId: '',
        transactionType: '',
        productCategoryId: '', // Placeholder
        customerType: '',
        serviceType: '',
        isActive: true
    });

    useEffect(() => {
        loadData();
    }, []);

    const loadData = async () => {
        try {
            setIsLoading(true);
            const [rulesData, groupsData] = await Promise.all([
                taxDataService.getTaxRules(),
                taxDataService.getTaxGroups({ isActive: true })
            ]);
            setRules(rulesData);
            setTaxGroups(groupsData);
        } catch (error) {
            console.error('Failed to load tax rules:', error);
            toast({
                variant: "destructive",
                title: "Error",
                description: "Failed to load tax rules."
            });
        } finally {
            setIsLoading(false);
        }
    };

    const handleCreate = () => {
        setEditingRule(null);
        setFormData({
            name: '',
            description: '',
            priority: rules.length + 1, // Default to end
            taxGroupId: '',
            transactionType: '',
            customerType: '',
            serviceType: '',
            isActive: true
        });
        setIsDialogOpen(true);
    };

    const handleEdit = (rule: TaxRule) => {
        setEditingRule(rule);
        setFormData({
            name: rule.name,
            description: rule.description || '',
            priority: rule.priority,
            taxGroupId: rule.taxGroupId,
            transactionType: rule.transactionType || '',
            productCategoryId: rule.productCategoryId || '',
            customerType: rule.customerType || '',
            serviceType: rule.serviceType || '',
            isActive: rule.isActive
        });
        setIsDialogOpen(true);
    };

    const handleSave = async () => {
        if (!formData.name || !formData.taxGroupId) {
            toast({
                variant: "destructive",
                title: "Validation Error",
                description: "Name and Tax Group are required."
            });
            return;
        }

        try {
            setIsSaving(true);
            const payload: CreateTaxRuleDto = {
                name: formData.name,
                description: formData.description,
                priority: Number(formData.priority) || 0,
                taxGroupId: formData.taxGroupId,
                transactionType: formData.transactionType || null as any,
                productCategoryId: formData.productCategoryId || null as any,
                customerType: formData.customerType || null as any,
                serviceType: formData.serviceType || null as any,
                isActive: formData.isActive ?? true
            };

            // Clean up empty strings to nulls for optional fields if API expects null
            if (!payload.transactionType) delete payload.transactionType;
            if (!payload.productCategoryId) delete payload.productCategoryId;
            if (!payload.customerType) delete payload.customerType;
            if (!payload.serviceType) delete payload.serviceType;

            if (editingRule) {
                await taxDataService.updateTaxRule(editingRule.id, payload);
                toast({ title: "Success", description: "Tax rule updated successfully." });
            } else {
                await taxDataService.createTaxRule(payload);
                toast({ title: "Success", description: "Tax rule created successfully." });
            }
            setIsDialogOpen(false);
            loadData();
        } catch (error) {
            console.error('Failed to save tax rule:', error);
            toast({
                variant: "destructive",
                title: "Error",
                description: "Failed to save tax rule."
            });
        } finally {
            setIsSaving(false);
        }
    };

    const handleDelete = async (id: string) => {
        if (!confirm('Are you sure you want to delete this rule?')) return;

        try {
            await taxDataService.deleteTaxRule(id);
            toast({ title: "Success", description: "Tax rule deleted." });
            loadData();
        } catch (error) {
            toast({
                variant: "destructive",
                title: "Error",
                description: "Failed to delete tax rule."
            });
        }
    };

    const filteredRules = rules.filter(rule =>
        rule.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        rule.taxGroupName.toLowerCase().includes(searchTerm.toLowerCase())
    );

    return (
        <div className="container mx-auto py-6 space-y-6">
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Tax Rules</h1>
                    <p className="text-muted-foreground mt-1">
                        Configure rules to automatically apply tax groups based on transaction context.
                    </p>
                </div>
                <div className="flex gap-2">
                    <Button variant="outline" onClick={async () => {
                        if (confirm('Seed default tax rules? This will create standard rules for sales and services.')) {
                            try {
                                setIsLoading(true);
                                await taxDataService.seedTaxRules();
                                await loadData();
                                toast({ title: "Success", description: "Tax rules seeded." });
                            } catch (e) {
                                console.error(e);
                                toast({ variant: "destructive", title: "Error", description: "Failed to seed rules." });
                            } finally {
                                setIsLoading(false);
                            }
                        }
                    }}>
                        <Layers className="mr-2 h-4 w-4" /> Seed Default Rules
                    </Button>
                    <Button onClick={handleCreate}>
                        <Plus className="mr-2 h-4 w-4" /> New Rule
                    </Button>
                </div>
            </div>

            <Card>
                <CardHeader className="pb-3">
                    <CardTitle>Rules List</CardTitle>
                    <CardDescription>
                        Rules are evaluated in priority order (lowest number first). The first matching rule determines the tax group.
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <div className="flex items-center space-x-2 mb-4">
                        <div className="relative flex-1 max-w-sm">
                            <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
                            <Input
                                placeholder="Search rules..."
                                className="pl-8"
                                value={searchTerm}
                                onChange={(e) => setSearchTerm(e.target.value)}
                            />
                        </div>
                    </div>

                    <div className="rounded-md border">
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead className="w-[80px]">Priority</TableHead>
                                    <TableHead>Rule Name</TableHead>
                                    <TableHead>Tax Group</TableHead>
                                    <TableHead>Conditions</TableHead>
                                    <TableHead className="w-[100px]">Status</TableHead>
                                    <TableHead className="text-right">Actions</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {isLoading ? (
                                    <TableRow>
                                        <TableCell colSpan={6} className="h-24 text-center">
                                            Loading rules...
                                        </TableCell>
                                    </TableRow>
                                ) : filteredRules.length === 0 ? (
                                    <TableRow>
                                        <TableCell colSpan={6} className="h-24 text-center text-muted-foreground">
                                            No tax rules found. Create one to get started.
                                        </TableCell>
                                    </TableRow>
                                ) : (
                                    filteredRules.map((rule) => (
                                        <TableRow key={rule.id}>
                                            <TableCell className="font-medium">
                                                <Badge variant="outline" className="bg-slate-50">
                                                    {rule.priority}
                                                </Badge>
                                            </TableCell>
                                            <TableCell>
                                                <div className="font-medium">{rule.name}</div>
                                                <div className="text-sm text-muted-foreground truncate max-w-[200px]">
                                                    {rule.description}
                                                </div>
                                            </TableCell>
                                            <TableCell>
                                                <Badge variant="secondary">{rule.taxGroupName}</Badge>
                                            </TableCell>
                                            <TableCell>
                                                <div className="flex flex-wrap gap-1">
                                                    {rule.transactionType && (
                                                        <Badge variant="outline" className="text-xs">Tx: {rule.transactionType}</Badge>
                                                    )}
                                                    {rule.customerType && (
                                                        <Badge variant="outline" className="text-xs">Cust: {rule.customerType}</Badge>
                                                    )}
                                                    {rule.serviceType && (
                                                        <Badge variant="outline" className="text-xs">Svc: {rule.serviceType}</Badge>
                                                    )}
                                                    {!rule.transactionType && !rule.customerType && !rule.serviceType && (
                                                        <span className="text-xs text-muted-foreground">No specific conditions (Match All)</span>
                                                    )}
                                                </div>
                                            </TableCell>
                                            <TableCell>
                                                {rule.isActive ? (
                                                    <Badge className="bg-green-100 text-green-800 hover:bg-green-100">Active</Badge>
                                                ) : (
                                                    <Badge variant="destructive" className="bg-red-100 text-red-800 hover:bg-red-100">Inactive</Badge>
                                                )}
                                            </TableCell>
                                            <TableCell className="text-right">
                                                <div className="flex justify-end gap-2">
                                                    <Button variant="ghost" size="icon" onClick={() => handleEdit(rule)}>
                                                        <Edit2 className="h-4 w-4" />
                                                    </Button>
                                                    <Button variant="ghost" size="icon" className="text-red-500" onClick={() => handleDelete(rule.id)}>
                                                        <Trash2 className="h-4 w-4" />
                                                    </Button>
                                                </div>
                                            </TableCell>
                                        </TableRow>
                                    ))
                                )}
                            </TableBody>
                        </Table>
                    </div>
                </CardContent>
            </Card>

            <Dialog open={isDialogOpen} onOpenChange={setIsDialogOpen}>
                <DialogContent className="max-w-2xl">
                    <DialogHeader>
                        <DialogTitle>{editingRule ? 'Edit Tax Rule' : 'Create Tax Rule'}</DialogTitle>
                        <DialogDescription>
                            Define conditions under which a specific Tax Group should be applied.
                        </DialogDescription>
                    </DialogHeader>

                    <div className="grid gap-4 py-4">
                        <div className="grid grid-cols-2 gap-4">
                            <div className="space-y-2">
                                <Label htmlFor="name">Rule Name <span className="text-red-500">*</span></Label>
                                <Input
                                    id="name"
                                    value={formData.name}
                                    onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                                    placeholder="e.g., Default Sales Tax"
                                />
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="priority">Priority <span className="text-red-500">*</span></Label>
                                <Input
                                    id="priority"
                                    type="number"
                                    value={formData.priority}
                                    onChange={(e) => setFormData({ ...formData, priority: parseInt(e.target.value) })}
                                    placeholder="1"
                                />
                                <p className="text-xs text-muted-foreground">Lower numbers run first.</p>
                            </div>
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="description">Description (Optional)</Label>
                            <Input
                                id="description"
                                value={formData.description || ''}
                                onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                                placeholder="Explain when this rule applies"
                            />
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="taxGroup">Tax Group to Apply <span className="text-red-500">*</span></Label>
                            <Select
                                value={formData.taxGroupId}
                                onValueChange={(val) => setFormData({ ...formData, taxGroupId: val })}
                            >
                                <SelectTrigger>
                                    <SelectValue placeholder="Select a tax group" />
                                </SelectTrigger>
                                <SelectContent>
                                    {taxGroups.map(group => (
                                        <SelectItem key={group.id} value={group.id}>
                                            {group.name} ({group.code}) - {group.applicability}
                                        </SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>

                        <div className="border-t pt-4 mt-2">
                            <Label className="mb-2 block font-semibold">Conditions (Leave empty to match any)</Label>
                            <div className="grid grid-cols-2 gap-4">
                                <div className="space-y-2">
                                    <Label htmlFor="transactionType">Transaction Type</Label>
                                    <Select
                                        value={formData.transactionType || "Any"}
                                        onValueChange={(val) => setFormData({ ...formData, transactionType: val === "Any" ? "" : val })}
                                    >
                                        <SelectTrigger>
                                            <SelectValue placeholder="Any" />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="Any">Any</SelectItem>
                                            <SelectItem value="SaleOfGoods">SaleOfGoods</SelectItem>
                                            <SelectItem value="SaleOfServices">SaleOfServices</SelectItem>
                                            <SelectItem value="PurchaseOfGoods">PurchaseOfGoods</SelectItem>
                                            <SelectItem value="PurchaseOfServices">PurchaseOfServices</SelectItem>
                                        </SelectContent>
                                    </Select>
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="customerType">Customer Type</Label>
                                    <Select
                                        value={formData.customerType || "Any"}
                                        onValueChange={(val) => setFormData({ ...formData, customerType: val === "Any" ? "" : val })}
                                    >
                                        <SelectTrigger>
                                            <SelectValue placeholder="Any" />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="Any">Any</SelectItem>
                                            <SelectItem value="Individual">Individual</SelectItem>
                                            <SelectItem value="Corporate">Corporate</SelectItem>
                                            <SelectItem value="Foreign">Foreign</SelectItem>
                                            <SelectItem value="Government">Government</SelectItem>
                                        </SelectContent>
                                    </Select>
                                </div>
                                {/* Service Type and Product Category can be text inputs or sophisticated selectors later */}
                                <div className="space-y-2">
                                    <Label htmlFor="serviceType">Service Type (Optional)</Label>
                                    <Input
                                        id="serviceType"
                                        value={formData.serviceType || ''}
                                        onChange={(e) => setFormData({ ...formData, serviceType: e.target.value })}
                                        placeholder="Specific service code"
                                    />
                                </div>
                            </div>
                        </div>

                        <div className="flex items-center space-x-2 pt-2">
                            <Checkbox
                                id="isActive"
                                checked={formData.isActive}
                                onCheckedChange={(checked) => setFormData({ ...formData, isActive: checked as boolean })}
                            />
                            <Label htmlFor="isActive">Rule is Active</Label>
                        </div>
                    </div>

                    <DialogFooter>
                        <Button variant="outline" onClick={() => setIsDialogOpen(false)} disabled={isSaving}>Cancel</Button>
                        <Button onClick={handleSave} disabled={isSaving}>
                            {isSaving ? 'Saving...' : (editingRule ? 'Update Rule' : 'Create Rule')}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}
