'use client';

import React, { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { FolderTree, Plus, Search, Eye, Edit, Filter } from 'lucide-react';
import Link from 'next/link';
import type { Account, AccountType, AccountStatus } from '@/types/finance';

// MOCK DATA
const MOCK_ACCOUNTS: Account[] = [
    {
        id: 'acc-1',
        tenantId: 'tenant-1',
        accountCode: '1000',
        accountNumber: '1000',
        accountName: 'Cash and Cash Equivalents',
        accountType: 'Asset',
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
    },
    {
        id: 'acc-2',
        tenantId: 'tenant-1',
        accountCode: '1100',
        accountNumber: '1100',
        accountName: 'Accounts Receivable',
        accountType: 'Asset',
        currencyCode: 'GHS',
        isMultiCurrency: true,
        isSegmented: false,
        isIFRSClassified: true,
        isBaseClassified: true,
        isLocalClassified: true,
        allowDirectPosting: true,
        isControlAccount: true,
        budgetTrackingEnabled: false,
        status: 'Active',
        currentBalance: 85000,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'acc-3',
        tenantId: 'tenant-1',
        accountCode: '2000',
        accountNumber: '2000',
        accountName: 'Accounts Payable',
        accountType: 'Liability',
        currencyCode: 'GHS',
        isMultiCurrency: true,
        isSegmented: false,
        isIFRSClassified: true,
        isBaseClassified: true,
        isLocalClassified: true,
        allowDirectPosting: true,
        isControlAccount: true,
        budgetTrackingEnabled: false,
        status: 'Active',
        currentBalance: -45000,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'acc-4',
        tenantId: 'tenant-1',
        accountCode: '3000',
        accountNumber: '3000',
        accountName: 'Retained Earnings',
        accountType: 'Equity',
        currencyCode: 'GHS',
        isMultiCurrency: false,
        isSegmented: false,
        isIFRSClassified: true,
        isBaseClassified: true,
        isLocalClassified: true,
        allowDirectPosting: false,
        isControlAccount: false,
        budgetTrackingEnabled: false,
        status: 'Active',
        currentBalance: -190000,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'acc-5',
        tenantId: 'tenant-1',
        accountCode: '4000',
        accountNumber: '4000',
        accountName: 'Sales Revenue',
        accountType: 'Revenue',
        currencyCode: 'GHS',
        isMultiCurrency: true,
        isSegmented: false,
        isIFRSClassified: true,
        isBaseClassified: true,
        isLocalClassified: true,
        allowDirectPosting: true,
        isControlAccount: false,
        budgetTrackingEnabled: true,
        status: 'Active',
        currentBalance: -250000,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'acc-6',
        tenantId: 'tenant-1',
        accountCode: '5000',
        accountNumber: '5000',
        accountName: 'Cost of Goods Sold',
        accountType: 'Expense',
        currencyCode: 'GHS',
        isMultiCurrency: false,
        isSegmented: false,
        isIFRSClassified: true,
        isBaseClassified: true,
        isLocalClassified: true,
        allowDirectPosting: true,
        isControlAccount: false,
        budgetTrackingEnabled: true,
        status: 'Active',
        currentBalance: 120000,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'acc-7',
        tenantId: 'tenant-1',
        accountCode: '5100',
        accountNumber: '5100',
        accountName: 'Salaries and Wages',
        accountType: 'Expense',
        currencyCode: 'GHS',
        isMultiCurrency: false,
        isSegmented: false,
        isIFRSClassified: true,
        isBaseClassified: true,
        isLocalClassified: true,
        allowDirectPosting: true,
        isControlAccount: false,
        budgetTrackingEnabled: true,
        status: 'Active',
        currentBalance: 80000,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
];

export default function AccountsPage() {
    const [accounts, setAccounts] = useState<Account[]>(MOCK_ACCOUNTS);
    const [searchTerm, setSearchTerm] = useState('');
    const [filters, setFilters] = useState({
        accountType: 'all',
        status: 'all',
        isMultiCurrency: 'all',
    });

    const filteredAccounts = accounts.filter((account) => {
        // Search filter
        const matchesSearch =
            searchTerm === '' ||
            account.accountCode.toLowerCase().includes(searchTerm.toLowerCase()) ||
            account.accountName.toLowerCase().includes(searchTerm.toLowerCase()) ||
            account.accountNumber.toLowerCase().includes(searchTerm.toLowerCase());

        // Type filter
        const matchesType = filters.accountType === 'all' || account.accountType === filters.accountType;

        // Status filter
        const matchesStatus = filters.status === 'all' || account.status === filters.status;

        // Multi-currency filter
        const matchesMultiCurrency =
            filters.isMultiCurrency === 'all' ||
            (filters.isMultiCurrency === 'yes' && account.isMultiCurrency) ||
            (filters.isMultiCurrency === 'no' && !account.isMultiCurrency);

        return matchesSearch && matchesType && matchesStatus && matchesMultiCurrency;
    });

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
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <FolderTree className="h-8 w-8" />
                        Chart of Accounts
                    </h1>
                    <p className="text-muted-foreground">
                        Manage your organization's chart of accounts
                    </p>
                    <p className="text-sm text-orange-600 mt-1">
                        ⚠️ DEMO MODE - Using mock data (backend not connected)
                    </p>
                </div>
                <Link href="/finance/accounts/new">
                    <Button>
                        <Plus className="mr-2 h-4 w-4" />
                        New Account
                    </Button>
                </Link>
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
                        <BreadcrumbPage>Chart of Accounts</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Search and Filters */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {/* Search */}
                <Card>
                    <CardHeader>
                        <CardTitle className="text-base">Search</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="relative">
                            <Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
                            <Input
                                placeholder="Search by code, number, or name..."
                                value={searchTerm}
                                onChange={(e) => setSearchTerm(e.target.value)}
                                className="pl-10"
                            />
                        </div>
                    </CardContent>
                </Card>

                {/* Filters */}
                <Card>
                    <CardHeader>
                        <CardTitle className="text-base flex items-center gap-2">
                            <Filter className="h-4 w-4" />
                            Filters
                        </CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="grid grid-cols-3 gap-2">
                            <Select
                                value={filters.accountType}
                                onValueChange={(value) => setFilters({ ...filters, accountType: value })}
                            >
                                <SelectTrigger>
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="all">All Types</SelectItem>
                                    <SelectItem value="Asset">Asset</SelectItem>
                                    <SelectItem value="Liability">Liability</SelectItem>
                                    <SelectItem value="Equity">Equity</SelectItem>
                                    <SelectItem value="Revenue">Revenue</SelectItem>
                                    <SelectItem value="Expense">Expense</SelectItem>
                                </SelectContent>
                            </Select>
                            <Select
                                value={filters.status}
                                onValueChange={(value) => setFilters({ ...filters, status: value })}
                            >
                                <SelectTrigger>
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="all">All Status</SelectItem>
                                    <SelectItem value="Active">Active</SelectItem>
                                    <SelectItem value="Inactive">Inactive</SelectItem>
                                    <SelectItem value="Closed">Closed</SelectItem>
                                </SelectContent>
                            </Select>
                            <Select
                                value={filters.isMultiCurrency}
                                onValueChange={(value) => setFilters({ ...filters, isMultiCurrency: value })}
                            >
                                <SelectTrigger>
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="all">All Currencies</SelectItem>
                                    <SelectItem value="yes">Multi-Currency</SelectItem>
                                    <SelectItem value="no">Single Currency</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                    </CardContent>
                </Card>
            </div>

            {/* Accounts Table */}
            <Card>
                <CardHeader>
                    <CardTitle>Accounts ({filteredAccounts.length})</CardTitle>
                    <CardDescription>
                        View and manage GL accounts
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <div className="rounded-md border">
                        <table className="w-full">
                            <thead>
                                <tr className="border-b bg-muted/50">
                                    <th className="p-4 text-left font-medium">Code</th>
                                    <th className="p-4 text-left font-medium">Account Name</th>
                                    <th className="p-4 text-left font-medium">Type</th>
                                    <th className="p-4 text-right font-medium">Balance</th>
                                    <th className="p-4 text-left font-medium">Currency</th>
                                    <th className="p-4 text-left font-medium">Status</th>
                                    <th className="p-4 text-left font-medium">Features</th>
                                    <th className="p-4 text-right font-medium">Actions</th>
                                </tr>
                            </thead>
                            <tbody>
                                {filteredAccounts.map((account) => (
                                    <tr key={account.id} className="border-b hover:bg-muted/50">
                                        <td className="p-4 font-mono font-semibold">{account.accountCode}</td>
                                        <td className="p-4">
                                            <div>
                                                <div className="font-medium">{account.accountName}</div>
                                                <div className="text-sm text-muted-foreground">{account.accountNumber}</div>
                                            </div>
                                        </td>
                                        <td className="p-4">{getAccountTypeBadge(account.accountType)}</td>
                                        <td className="p-4 text-right font-mono">
                                            {account.currentBalance !== undefined ? formatCurrency(account.currentBalance) : '-'}
                                        </td>
                                        <td className="p-4">
                                            <div className="flex items-center gap-1">
                                                <span className="font-mono text-sm">{account.currencyCode}</span>
                                                {account.isMultiCurrency && (
                                                    <Badge variant="outline" className="text-xs">
                                                        Multi
                                                    </Badge>
                                                )}
                                            </div>
                                        </td>
                                        <td className="p-4">{getStatusBadge(account.status)}</td>
                                        <td className="p-4">
                                            <div className="flex flex-wrap gap-1">
                                                {account.isControlAccount && (
                                                    <Badge variant="outline" className="text-xs">
                                                        Control
                                                    </Badge>
                                                )}
                                                {account.budgetTrackingEnabled && (
                                                    <Badge variant="outline" className="text-xs">
                                                        Budget
                                                    </Badge>
                                                )}
                                                {!account.allowDirectPosting && (
                                                    <Badge variant="outline" className="text-xs">
                                                        No Posting
                                                    </Badge>
                                                )}
                                            </div>
                                        </td>
                                        <td className="p-4 text-right space-x-2">
                                            <Link href={`/finance/accounts/${account.id}`}>
                                                <Button variant="ghost" size="sm">
                                                    <Eye className="h-4 w-4" />
                                                </Button>
                                            </Link>
                                            <Link href={`/finance/accounts/${account.id}/edit`}>
                                                <Button variant="ghost" size="sm">
                                                    <Edit className="h-4 w-4" />
                                                </Button>
                                            </Link>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}
