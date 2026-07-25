'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { FolderTree, Plus, Search, Eye, Edit, Filter, Loader2 } from 'lucide-react';
import Link from 'next/link';
import type { Account, AccountType, AccountStatus } from '@/types/finance';
import { financeDataService } from '@/services/finance/finance-data.service';
import { useAuth } from '@/hooks/use-auth';
import { usePathname } from 'next/navigation';

export default function AccountsPage() {
    const pathname = usePathname() ?? '';
    const administrationMode = pathname.startsWith('/administration/finance');
    const { hasAnyPermission } = useAuth();
    const canManage = hasAnyPermission(['Finance.Admin', 'Finance.ChartOfAccounts.Manage']);
    const [accounts, setAccounts] = useState<Account[]>([]);
    const [coaType, setCoaType] = useState<'Standard' | 'Segmented'>('Standard');
    const [loading, setLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState('');
    const [filters, setFilters] = useState({
        accountType: 'all',
        status: 'all',
        isMultiCurrency: 'all',
    });

    // Load settings and accounts
    useEffect(() => {
        const loadData = async () => {
            try {
                setLoading(true);

                // First load settings to get COA type
                const settings = await financeDataService.getFinanceSettings();
                setCoaType(settings.coaType);

                // Then load accounts with the correct COA type
                const accountsData = await financeDataService.getAccounts({ coaType: settings.coaType });
                setAccounts(accountsData);
            } catch (error) {
                console.error('Error loading accounts:', error);
            } finally {
                setLoading(false);
            }
        };

        loadData();
    }, []);

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

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-[400px]">
                <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
        );
    }

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <FolderTree className="h-8 w-8" />
                        Chart of Accounts
                        <Badge variant="outline" className="ml-2">
                            {coaType === 'Segmented' ? 'Segmented COA' : 'Standard COA'}
                        </Badge>
                    </h1>
                    <p className="text-muted-foreground">
                        {administrationMode
                            ? "Configure and maintain the organization's chart of accounts"
                            : "Review the organization's chart of accounts and balances"}
                    </p>
                </div>
                {canManage && (
                <Link href="/finance/accounts/new">
                    <Button>
                        <Plus className="mr-2 h-4 w-4" />
                        New Account
                    </Button>
                </Link>
                )}
            </div>

            {/* Breadcrumbs */}
            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbLink href={administrationMode ? '/administration' : '/finance'}>
                            {administrationMode ? 'Administration' : 'Finance'}
                        </BreadcrumbLink>
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
                                                {account.isSegmented && (
                                                    <Badge variant="outline" className="mt-1 text-xs">Segmented</Badge>
                                                )}
                                            </div>
                                        </td>
                                        <td className="p-4">{getAccountTypeBadge(account.accountType)}</td>
                                        <td className="p-4 text-right font-mono">
                                            <span className={(account.currentBalance ?? 0) < 0 ? 'text-red-600' : ''}>
                                                {(account.currentBalance ?? 0) < 0 ? '-' : ''}{formatCurrency(account.currentBalance ?? 0)}
                                            </span>
                                        </td>
                                        <td className="p-4">{account.currencyCode}</td>
                                        <td className="p-4">{getStatusBadge(account.status)}</td>
                                        <td className="p-4">
                                            <div className="flex flex-wrap gap-1">
                                                {account.isMultiCurrency && (
                                                    <Badge variant="outline" className="text-xs">Multi-Currency</Badge>
                                                )}
                                                {account.isControlAccount && (
                                                    <Badge variant="outline" className="text-xs">Control</Badge>
                                                )}
                                                {account.budgetTrackingEnabled && (
                                                    <Badge variant="outline" className="text-xs">Budget</Badge>
                                                )}
                                            </div>
                                        </td>
                                        <td className="p-4 text-right">
                                            <div className="flex justify-end gap-2">
                                                <Link href={`/finance/accounts/${account.id}`}>
                                                    <Button variant="ghost" size="sm">
                                                        <Eye className="h-4 w-4" />
                                                    </Button>
                                                </Link>
                                                {canManage && (
                                                <Link href={`/finance/accounts/${account.id}/edit`}>
                                                    <Button variant="ghost" size="sm">
                                                        <Edit className="h-4 w-4" />
                                                    </Button>
                                                </Link>
                                                )}
                                            </div>
                                        </td>
                                    </tr>
                                ))}
                                {filteredAccounts.length === 0 && (
                                    <tr>
                                        <td colSpan={8} className="p-8 text-center text-muted-foreground">
                                            No accounts found matching your criteria
                                        </td>
                                    </tr>
                                )}
                            </tbody>
                        </table>
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}
