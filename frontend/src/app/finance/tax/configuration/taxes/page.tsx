'use client';

import { useEffect, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { taxDataService } from '@/services/finance/tax-data.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import { Tax } from '@/types/tax';
import type { Account } from '@/types/finance';
import { Plus, Edit, CheckCircle, Eye, LockKeyhole } from 'lucide-react';
import { useToast } from '@/hooks/use-toast';
import { TaxFormDialog } from '@/components/finance/tax/TaxFormDialog';
import Link from 'next/link';

export default function TaxesPage() {
    const [taxes, setTaxes] = useState<Tax[]>([]);
    const [accounts, setAccounts] = useState<Account[]>([]);
    const [loading, setLoading] = useState(true);
    const [accountsLoading, setAccountsLoading] = useState(true);
    const [dialogOpen, setDialogOpen] = useState(false);
    const [editingTax, setEditingTax] = useState<Tax | null>(null);
    const { toast } = useToast();

    useEffect(() => {
        loadTaxes();
        loadAccounts();
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

    const loadAccounts = async () => {
        try {
            setAccountsLoading(true);
            const data = await financeDataService.getAccounts({ status: 'Active', pageSize: 1000 });
            setAccounts(data);
        } catch (error) {
            console.error('Failed to load accounts:', error);
            toast({
                title: 'Error',
                description: 'Failed to load GL accounts',
                variant: 'destructive',
            });
        } finally {
            setAccountsLoading(false);
        }
    };

    const handleOpenDialog = (tax?: Tax) => {
        setEditingTax(tax ?? null);
        setDialogOpen(true);
    };

    const handleDelete = async (id: string) => {
        if (!confirm('Retire this tax configuration? It will remain visible and become permanently locked.')) return;

        try {
            await taxDataService.deleteTax(id);
            toast({
                title: 'Success',
                description: 'Tax retired and locked successfully',
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
                                                    <LockKeyhole className="mr-1 h-3 w-3" />
                                                    Inactive · Locked
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
                                        <Button variant="ghost" size="sm" asChild>
                                            <Link href={`/finance/tax/configuration/taxes/${tax.id}`} aria-label={`View ${tax.name}`}>
                                                <Eye className="h-4 w-4" />
                                            </Link>
                                        </Button>
                                        {tax.isActive && (
                                            <>
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
                                                    aria-label={`Retire and lock ${tax.name}`}
                                                >
                                                    <LockKeyhole className="h-4 w-4 text-destructive" />
                                                </Button>
                                            </>
                                        )}
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}
                </CardContent>
            </Card>

            <TaxFormDialog
                open={dialogOpen}
                tax={editingTax}
                accounts={accounts}
                accountsLoading={accountsLoading}
                onOpenChange={setDialogOpen}
                onSaved={() => {
                    toast({
                        title: 'Success',
                        description: editingTax ? 'Tax updated successfully' : 'Tax created successfully',
                    });
                    loadTaxes();
                }}
            />
        </div>
    );
}
