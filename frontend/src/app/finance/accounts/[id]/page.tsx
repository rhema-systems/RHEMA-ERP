'use client';

import React from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { ArrowLeft, Edit, Trash2, DollarSign } from 'lucide-react';
import { useRouter } from 'next/navigation';
import type { Account, AccountType, AccountStatus } from '@/types/finance';

// MOCK DATA - In real app, this would come from API based on params.id
const MOCK_ACCOUNT: Account = {
    id: 'acc-1',
    tenantId: 'tenant-1',
    accountCode: '1000',
    accountNumber: '1000',
    accountName: 'Cash and Cash Equivalents',
    accountType: 'Asset',
    description: 'Bank accounts, petty cash, and other liquid assets',
    currencyCode: 'GHS',
    isMultiCurrency: false,
    isSegmented: false,
    isIFRSClassified: true,
    isBaseClassified: true,
    isLocalClassified: true,
    allowDirectPosting: true,
    isControlAccount: false,
    budgetTrackingEnabled: false,
    status: 'Active',
    currentBalance: 150000,
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
};

export default function AccountDetailPage({ params }: { params: { id: string } }) {
    const router = useRouter();
    const account = MOCK_ACCOUNT; // In real app: fetch based on params.id

    const getAccountTypeBadge = (type: AccountType) => {
        const colors: Record<AccountType, string> = {
            Asset: 'bg-blue-500',
            Liability: 'bg-red-500',
            Equity: 'bg-purple-500',
            Revenue: 'bg-green-500',
            Expense: 'bg-orange-500',
        };
        return <Badge className={colors[type]}>{type}</Badge>;
    };

    const getStatusBadge = (status: AccountStatus) => {
        const variants: Record<AccountStatus, 'default' | 'secondary' | 'destructive'> = {
            Active: 'default',
            Inactive: 'secondary',
            Closed: 'destructive',
        };
        return <Badge variant={variants[status]}>{status}</Badge>;
    };

    const formatCurrency = (amount: number) => {
        return new Intl.NumberFormat('en-GH', {
            style: 'currency',
            currency: 'GHS',
        }).format(Math.abs(amount));
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">
                        {account.accountCode} - {account.accountName}
                    </h1>
                    <p className="text-muted-foreground">
                        Account details and transaction history
                    </p>
                    <p className="text-sm text-orange-600 mt-1">
                        ⚠️ DEMO MODE - Using mock data (backend not connected)
                    </p>
                </div>
                <div className="flex gap-2">
                    <Button variant="outline" onClick={() => router.back()}>
                        <ArrowLeft className="mr-2 h-4 w-4" />
                        Back
                    </Button>
                    <Button onClick={() => router.push(`/finance/accounts/${params.id}/edit`)}>
                        <Edit className="mr-2 h-4 w-4" />
                        Edit
                    </Button>
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
                        <BreadcrumbLink href="/finance/accounts">Chart of Accounts</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>{account.accountCode}</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                {/* Main Content */}
                <div className="lg:col-span-2 space-y-6">
                    {/* Account Summary */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Account Information</CardTitle>
                        </CardHeader>
                        <CardContent>
                            <div className="grid grid-cols-2 gap-4">
                                <div>
                                    <p className="text-sm text-muted-foreground">Account Code</p>
                                    <p className="font-mono font-semibold">{account.accountCode}</p>
                                </div>
                                <div>
                                    <p className="text-sm text-muted-foreground">Account Number</p>
                                    <p className="font-mono font-semibold">{account.accountNumber}</p>
                                </div>
                                <div>
                                    <p className="text-sm text-muted-foreground">Account Name</p>
                                    <p className="font-semibold">{account.accountName}</p>
                                </div>
                                <div>
                                    <p className="text-sm text-muted-foreground">Account Type</p>
                                    <div className="mt-1">{getAccountTypeBadge(account.accountType)}</div>
                                </div>
                                {account.description && (
                                    <div className="col-span-2">
                                        <p className="text-sm text-muted-foreground">Description</p>
                                        <p>{account.description}</p>
                                    </div>
                                )}
                            </div>
                        </CardContent>
                    </Card>

                    {/* Balance */}
                    <Card>
                        <CardHeader>
                            <CardTitle className="flex items-center gap-2">
                                <DollarSign className="h-5 w-5" />
                                Current Balance
                            </CardTitle>
                        </CardHeader>
                        <CardContent>
                            <div className="text-3xl font-bold font-mono">
                                {account.currentBalance !== undefined ? formatCurrency(account.currentBalance) : 'N/A'}
                            </div>
                            <p className="text-sm text-muted-foreground mt-1">
                                As of {new Date().toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' })}
                            </p>
                        </CardContent>
                    </Card>

                    {/* Transaction History Placeholder */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Recent Transactions</CardTitle>
                            <CardDescription>Last 10 transactions for this account</CardDescription>
                        </CardHeader>
                        <CardContent>
                            <div className="text-center py-8 text-muted-foreground">
                                <p>No transactions to display</p>
                                <p className="text-sm mt-2">Transaction history will appear here once the backend is connected</p>
                            </div>
                        </CardContent>
                    </Card>
                </div>

                {/* Sidebar */}
                <div className="space-y-6">
                    {/* Status */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Status</CardTitle>
                        </CardHeader>
                        <CardContent>
                            {getStatusBadge(account.status)}
                        </CardContent>
                    </Card>

                    {/* Currency Settings */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Currency</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-2">
                            <div>
                                <p className="text-sm text-muted-foreground">Primary Currency</p>
                                <p className="font-mono font-semibold">{account.currencyCode}</p>
                            </div>
                            <div>
                                <p className="text-sm text-muted-foreground">Multi-Currency</p>
                                <p>{account.isMultiCurrency ? 'Enabled' : 'Disabled'}</p>
                            </div>
                        </CardContent>
                    </Card>

                    {/* Features */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Features</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-2">
                            <div className="flex items-center justify-between">
                                <span className="text-sm">Direct Posting</span>
                                <Badge variant={account.allowDirectPosting ? 'default' : 'secondary'}>
                                    {account.allowDirectPosting ? 'Allowed' : 'Not Allowed'}
                                </Badge>
                            </div>
                            <div className="flex items-center justify-between">
                                <span className="text-sm">Control Account</span>
                                <Badge variant={account.isControlAccount ? 'default' : 'secondary'}>
                                    {account.isControlAccount ? 'Yes' : 'No'}
                                </Badge>
                            </div>
                            <div className="flex items-center justify-between">
                                <span className="text-sm">Budget Tracking</span>
                                <Badge variant={account.budgetTrackingEnabled ? 'default' : 'secondary'}>
                                    {account.budgetTrackingEnabled ? 'Enabled' : 'Disabled'}
                                </Badge>
                            </div>
                        </CardContent>
                    </Card>

                    {/* Classification */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Classification</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-2">
                            <div className="flex items-center justify-between">
                                <span className="text-sm">IFRS</span>
                                <Badge variant={account.isIFRSClassified ? 'default' : 'secondary'}>
                                    {account.isIFRSClassified ? 'Yes' : 'No'}
                                </Badge>
                            </div>
                            <div className="flex items-center justify-between">
                                <span className="text-sm">Base</span>
                                <Badge variant={account.isBaseClassified ? 'default' : 'secondary'}>
                                    {account.isBaseClassified ? 'Yes' : 'No'}
                                </Badge>
                            </div>
                            <div className="flex items-center justify-between">
                                <span className="text-sm">Local</span>
                                <Badge variant={account.isLocalClassified ? 'default' : 'secondary'}>
                                    {account.isLocalClassified ? 'Yes' : 'No'}
                                </Badge>
                            </div>
                        </CardContent>
                    </Card>

                    {/* Actions */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Actions</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-2">
                            <Button variant="outline" className="w-full" disabled>
                                <Trash2 className="mr-2 h-4 w-4" />
                                Delete Account
                            </Button>
                            <p className="text-xs text-muted-foreground">
                                Accounts with transactions cannot be deleted
                            </p>
                        </CardContent>
                    </Card>
                </div>
            </div>
        </div>
    );
}
