'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { ArrowLeft, History, Loader2, LockKeyhole } from 'lucide-react';
import { toast } from '@/components/ui/use-toast';
import { useRouter } from 'next/navigation';
import { taxDataService } from '@/services/finance/tax-data.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import { TaxFormDialog } from '@/components/finance/tax/TaxFormDialog';
import type { Tax, TaxConfigurationVersion } from '@/types/tax';
import type { Account } from '@/types/finance';

interface PageProps {
    params: Promise<{ id: string }>;
}

export default function TaxDetailPage({ params }: PageProps) {
    const { id } = React.use(params);
    const router = useRouter();
    const [isLoading, setIsLoading] = useState(true);
    const [accountsLoading, setAccountsLoading] = useState(true);
    const [editOpen, setEditOpen] = useState(false);
    const [tax, setTax] = useState<Tax | null>(null);
    const [versions, setVersions] = useState<TaxConfigurationVersion[]>([]);
    const [accounts, setAccounts] = useState<Account[]>([]);

    useEffect(() => {
        loadTax();
        loadAccounts();
    }, [id]);

    const loadTax = async () => {
        try {
            setIsLoading(true);
            const [data, versionData] = await Promise.all([
                taxDataService.getTaxById(id),
                taxDataService.getTaxConfigurationVersions(id),
            ]);
            setTax(data);
            setVersions(versionData);
        } catch (error) {
            console.error('Failed to load tax:', error);
            toast({
                title: 'Error',
                description: 'Failed to load tax details.',
                variant: 'destructive',
            });
            // If 404, redirect to list? Or just show error state
        } finally {
            setIsLoading(false);
        }
    };

    const loadAccounts = async () => {
        try {
            setAccountsLoading(true);
            const data = await financeDataService.getAccounts({ pageSize: 1000 });
            setAccounts(data);
        } catch (error) {
            console.error('Failed to load accounts:', error);
            toast({
                title: 'Error',
                description: 'Failed to load GL accounts.',
                variant: 'destructive',
            });
        } finally {
            setAccountsLoading(false);
        }
    };

    const formatAccount = (accountId?: string | null) => {
        if (!accountId) return 'Not Set';
        const account = accounts.find(item => item.id === accountId);
        return account
            ? `${account.accountNumber || account.accountCode} - ${account.accountName}`
            : accountId;
    };

    const formatDateTime = (value?: string | null) =>
        value ? new Date(value).toLocaleString() : 'Current';

    if (isLoading) {
        return (
            <div className="flex h-[400px] items-center justify-center">
                <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
        );
    }

    if (!tax) {
        return (
            <div className="flex flex-col items-center justify-center h-[400px] gap-4">
                <p className="text-muted-foreground">Tax not found</p>
                <Button variant="outline" onClick={() => router.push('/finance/tax/configuration/taxes')}>
                    Back to Taxes
                </Button>
            </div>
        );
    }

    return (
        <div className="space-y-6">
            {/* Header */}
            <div className="flex items-center justify-between">
                <div className="flex items-center gap-4">
                    <Button variant="ghost" size="icon" onClick={() => router.back()}>
                        <ArrowLeft className="h-4 w-4" />
                    </Button>
                    <div>
                        <h1 className="text-3xl font-bold tracking-tight">{tax.name}</h1>
                        <p className="text-muted-foreground">
                            {tax.code} • {tax.category}
                        </p>
                    </div>
                </div>
                <div className="flex items-center gap-2">
                    <Button variant="outline" onClick={() => loadTax()}>
                        Refresh
                    </Button>
                    {tax.isActive ? (
                        <Button variant="default" onClick={() => setEditOpen(true)}>
                            Edit
                        </Button>
                    ) : (
                        <Badge variant="outline" className="gap-1.5 py-2">
                            <LockKeyhole className="h-3.5 w-3.5" />
                            Locked
                        </Badge>
                    )}
                </div>
            </div>

            {/* Breadcrumbs */}
            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/finance">Finance</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/finance/tax/configuration/taxes">Taxes</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>{tax.code}</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {!tax.isActive && (
                <Alert>
                    <LockKeyhole className="h-4 w-4" />
                    <AlertTitle>Inactive tax configuration is locked</AlertTitle>
                    <AlertDescription>
                        This record remains visible for audit and historical reporting. Its rate,
                        applicability, thresholds, and GL mappings cannot be edited or deleted.
                    </AlertDescription>
                </Alert>
            )}

            {/* Content */}
            <div className="grid gap-6 md:grid-cols-2">
                <Card>
                    <CardHeader>
                        <CardTitle>General Information</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <div className="grid grid-cols-2 gap-4">
                            <div>
                                <Label className="text-muted-foreground">Tax Code</Label>
                                <p className="font-medium">{tax.code}</p>
                            </div>
                            <div>
                                <Label className="text-muted-foreground">Rate</Label>
                                <p className="font-medium">{tax.rate}%</p>
                            </div>
                            <div>
                                <Label className="text-muted-foreground">Category</Label>
                                <p className="font-medium">{tax.category}</p>
                            </div>
                            <div>
                                <Label className="text-muted-foreground">Applicability</Label>
                                <p className="font-medium">{tax.applicability}</p>
                            </div>
                            <div>
                                <Label className="text-muted-foreground">Recoverable</Label>
                                <p className="font-medium">{tax.isInputTaxDeductible ? 'Yes' : 'No'}</p>
                            </div>
                            <div>
                                <Label className="text-muted-foreground">Status</Label>
                                <Badge variant={tax.isActive ? 'default' : 'secondary'}>
                                    {tax.isActive ? 'Active' : 'Inactive'}
                                </Badge>
                            </div>
                        </div>
                        <div>
                            <Label className="text-muted-foreground">Description</Label>
                            <p className="mt-1 text-sm">{tax.description || '-'}</p>
                        </div>
                    </CardContent>
                </Card>

                <Card>
                    <CardHeader>
                        <CardTitle>Configuration</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <div>
                            <Label className="text-muted-foreground">GL Accounts</Label>
                            <div className="mt-2 space-y-2 text-sm">
                                <div className="flex justify-between border-b pb-2">
                                    <span>Payable Account</span>
                                    <span className="text-right font-mono text-muted-foreground">{formatAccount(tax.taxPayableAccountId)}</span>
                                </div>
                                <div className="flex justify-between border-b pb-2">
                                    <span>Receivable Account</span>
                                    <span className="text-right font-mono text-muted-foreground">{formatAccount(tax.taxReceivableAccountId)}</span>
                                </div>
                            </div>
                        </div>
                        {tax.thresholdAmount && (
                            <div>
                                <Label className="text-muted-foreground">Threshold Amount</Label>
                                <p className="font-medium">{tax.thresholdAmount}</p>
                            </div>
                        )}
                    </CardContent>
                </Card>
            </div>

            <Card>
                <CardHeader>
                    <CardTitle className="flex items-center gap-2">
                        <History className="h-5 w-5" />
                        Complete Configuration History
                    </CardTitle>
                </CardHeader>
                <CardContent className="space-y-4">
                    {versions.map(version => (
                        <div key={`${version.id}-${version.versionNumber}`} className="rounded-lg border p-4">
                            <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
                                <div className="flex items-center gap-2">
                                    <Badge variant={version.isCurrent ? 'default' : 'secondary'}>
                                        Version {version.versionNumber}
                                    </Badge>
                                    <Badge variant="outline">
                                        {version.isCurrent ? 'Current' : 'Superseded'}
                                    </Badge>
                                    {version.isLocked && (
                                        <Badge variant="outline" className="gap-1">
                                            <LockKeyhole className="h-3 w-3" />
                                            Read only
                                        </Badge>
                                    )}
                                </div>
                                <span className="text-xs text-muted-foreground">
                                    {formatDateTime(version.validFrom)} – {formatDateTime(version.validTo)}
                                </span>
                            </div>
                            <div className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
                                <div><span className="text-muted-foreground">Name</span><p className="font-medium">{version.name}</p></div>
                                <div><span className="text-muted-foreground">Rate</span><p className="font-medium">{version.rate}%</p></div>
                                <div><span className="text-muted-foreground">Effective from</span><p className="font-medium">{new Date(version.effectiveFrom).toLocaleDateString()}</p></div>
                                <div><span className="text-muted-foreground">Status</span><p className="font-medium">{version.isActive ? 'Active' : 'Inactive'}</p></div>
                                <div><span className="text-muted-foreground">Category</span><p className="font-medium">{version.category}</p></div>
                                <div><span className="text-muted-foreground">Applicability</span><p className="font-medium">{version.applicability}</p></div>
                                <div><span className="text-muted-foreground">Payable account</span><p className="font-mono text-xs">{formatAccount(version.taxPayableAccountId)}</p></div>
                                <div><span className="text-muted-foreground">Receivable account</span><p className="font-mono text-xs">{formatAccount(version.taxReceivableAccountId)}</p></div>
                            </div>
                            {(version.changeReason || version.changedBy) && (
                                <p className="mt-3 text-xs text-muted-foreground">
                                    {version.changeReason || 'Configuration updated'}
                                    {version.changedBy ? ` · by ${version.changedBy}` : ''}
                                </p>
                            )}
                        </div>
                    ))}
                </CardContent>
            </Card>

            {tax.isActive && (
                <TaxFormDialog
                    open={editOpen}
                    tax={tax}
                    accounts={accounts}
                    accountsLoading={accountsLoading}
                    onOpenChange={setEditOpen}
                    onSaved={(saved) => {
                        setTax(saved);
                        void loadTax();
                        toast({
                            title: 'Success',
                            description: 'Tax updated successfully.',
                        });
                    }}
                />
            )}
        </div>
    );
}
