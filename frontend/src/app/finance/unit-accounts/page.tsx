'use client';

import React, { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuTrigger
} from '@/components/ui/dropdown-menu';
import {
    Calculator,
    Plus,
    Search,
    MoreHorizontal,
    Pencil,
    Trash2,
    ToggleLeft,
    ToggleRight,
    ChevronRight,
    ChevronDown,
    FolderTree
} from 'lucide-react';
import Link from 'next/link';
import type { UnitAccount, UnitType } from '@/types/unit-accounts';

// MOCK DATA
const MOCK_UNIT_TYPES: UnitType[] = [
    { id: 'ut-1', code: 'EMP', name: 'Employees', decimalPlaces: 0, isActive: true, createdAt: '', createdBy: '' },
    { id: 'ut-2', code: 'SQFT', name: 'Square Footage', decimalPlaces: 2, isActive: true, createdAt: '', createdBy: '' },
    { id: 'ut-3', code: 'HRS', name: 'Hours', decimalPlaces: 2, isActive: true, createdAt: '', createdBy: '' },
];

const MOCK_UNIT_ACCOUNTS: UnitAccount[] = [
    {
        id: 'ua-1',
        accountNumber: 'U-1000',
        name: 'Total Employees',
        description: 'Aggregate employee count',
        unitTypeId: 'ut-1',
        unitType: MOCK_UNIT_TYPES[0],
        accountLevel: 1,
        isPostingAccount: false,
        isActive: true,
        currentBalance: 125,
        createdAt: '2024-01-01T00:00:00Z',
        createdBy: 'admin',
        children: [
            {
                id: 'ua-2',
                accountNumber: 'U-1100',
                name: 'Operations Department',
                unitTypeId: 'ut-1',
                unitType: MOCK_UNIT_TYPES[0],
                parentAccountId: 'ua-1',
                accountLevel: 2,
                isPostingAccount: true,
                isActive: true,
                currentBalance: 45,
                createdAt: '2024-01-01T00:00:00Z',
                createdBy: 'admin',
            },
            {
                id: 'ua-3',
                accountNumber: 'U-1200',
                name: 'Sales Department',
                unitTypeId: 'ut-1',
                unitType: MOCK_UNIT_TYPES[0],
                parentAccountId: 'ua-1',
                accountLevel: 2,
                isPostingAccount: true,
                isActive: true,
                currentBalance: 30,
                createdAt: '2024-01-01T00:00:00Z',
                createdBy: 'admin',
            },
            {
                id: 'ua-4',
                accountNumber: 'U-1300',
                name: 'Admin Department',
                unitTypeId: 'ut-1',
                unitType: MOCK_UNIT_TYPES[0],
                parentAccountId: 'ua-1',
                accountLevel: 2,
                isPostingAccount: true,
                isActive: true,
                currentBalance: 50,
                createdAt: '2024-01-01T00:00:00Z',
                createdBy: 'admin',
            },
        ],
    },
    {
        id: 'ua-5',
        accountNumber: 'U-2000',
        name: 'Office Space',
        description: 'Total office area',
        unitTypeId: 'ut-2',
        unitType: MOCK_UNIT_TYPES[1],
        accountLevel: 1,
        isPostingAccount: false,
        isActive: true,
        currentBalance: 15000,
        createdAt: '2024-01-01T00:00:00Z',
        createdBy: 'admin',
        children: [
            {
                id: 'ua-6',
                accountNumber: 'U-2100',
                name: 'Head Office',
                unitTypeId: 'ut-2',
                unitType: MOCK_UNIT_TYPES[1],
                parentAccountId: 'ua-5',
                accountLevel: 2,
                isPostingAccount: true,
                isActive: true,
                currentBalance: 10000,
                createdAt: '2024-01-01T00:00:00Z',
                createdBy: 'admin',
            },
            {
                id: 'ua-7',
                accountNumber: 'U-2200',
                name: 'Branch Offices',
                unitTypeId: 'ut-2',
                unitType: MOCK_UNIT_TYPES[1],
                parentAccountId: 'ua-5',
                accountLevel: 2,
                isPostingAccount: true,
                isActive: true,
                currentBalance: 5000,
                createdAt: '2024-01-01T00:00:00Z',
                createdBy: 'admin',
            },
        ],
    },
    {
        id: 'ua-8',
        accountNumber: 'U-3000',
        name: 'Machine Hours',
        unitTypeId: 'ut-3',
        unitType: MOCK_UNIT_TYPES[2],
        accountLevel: 1,
        isPostingAccount: true,
        isActive: true,
        currentBalance: 2500.50,
        createdAt: '2024-01-01T00:00:00Z',
        createdBy: 'admin',
    },
];

interface AccountRowProps {
    account: UnitAccount;
    level: number;
    expandedIds: Set<string>;
    onToggle: (id: string) => void;
}

function AccountRow({ account, level, expandedIds, onToggle }: AccountRowProps) {
    const hasChildren = account.children && account.children.length > 0;
    const isExpanded = expandedIds.has(account.id);

    return (
        <>
            <tr className="border-b hover:bg-muted/50">
                <td className="p-4">
                    <div className="flex items-center" style={{ paddingLeft: `${(level - 1) * 24}px` }}>
                        {hasChildren ? (
                            <button
                                onClick={() => onToggle(account.id)}
                                className="mr-2 p-1 hover:bg-muted rounded"
                            >
                                {isExpanded ? (
                                    <ChevronDown className="h-4 w-4" />
                                ) : (
                                    <ChevronRight className="h-4 w-4" />
                                )}
                            </button>
                        ) : (
                            <span className="mr-2 w-6" />
                        )}
                        <span className="font-mono font-semibold text-blue-600">
                            {account.accountNumber}
                        </span>
                    </div>
                </td>
                <td className="p-4">
                    <div className="flex items-center gap-2">
                        <span className="font-medium">{account.name}</span>
                        {!account.isPostingAccount && (
                            <Badge variant="outline" className="text-xs">Summary</Badge>
                        )}
                    </div>
                </td>
                <td className="p-4">
                    <Badge variant="secondary">{account.unitType?.name || '-'}</Badge>
                </td>
                <td className="p-4 text-right font-mono">
                    {account.currentBalance?.toLocaleString(undefined, {
                        minimumFractionDigits: account.unitType?.decimalPlaces || 0,
                        maximumFractionDigits: account.unitType?.decimalPlaces || 0,
                    })}
                </td>
                <td className="p-4 text-center">
                    <Badge variant={account.isActive ? 'default' : 'secondary'}>
                        {account.isActive ? 'Active' : 'Inactive'}
                    </Badge>
                </td>
                <td className="p-4 text-right">
                    <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="sm">
                                <MoreHorizontal className="h-4 w-4" />
                            </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                            <DropdownMenuItem asChild>
                                <Link href={`/finance/unit-accounts/${account.id}`}>
                                    <Pencil className="mr-2 h-4 w-4" />
                                    Edit / View Balance
                                </Link>
                            </DropdownMenuItem>
                            <DropdownMenuItem>
                                {account.isActive ? (
                                    <>
                                        <ToggleLeft className="mr-2 h-4 w-4" />
                                        Deactivate
                                    </>
                                ) : (
                                    <>
                                        <ToggleRight className="mr-2 h-4 w-4" />
                                        Activate
                                    </>
                                )}
                            </DropdownMenuItem>
                            <DropdownMenuItem className="text-destructive">
                                <Trash2 className="mr-2 h-4 w-4" />
                                Delete
                            </DropdownMenuItem>
                        </DropdownMenuContent>
                    </DropdownMenu>
                </td>
            </tr>
            {hasChildren && isExpanded && account.children?.map((child) => (
                <AccountRow
                    key={child.id}
                    account={child}
                    level={level + 1}
                    expandedIds={expandedIds}
                    onToggle={onToggle}
                />
            ))}
        </>
    );
}

export default function UnitAccountsPage() {
    const [searchTerm, setSearchTerm] = useState('');
    const [expandedIds, setExpandedIds] = useState<Set<string>>(new Set(['ua-1', 'ua-5']));

    const handleToggle = (id: string) => {
        setExpandedIds((prev) => {
            const next = new Set(prev);
            if (next.has(id)) {
                next.delete(id);
            } else {
                next.add(id);
            }
            return next;
        });
    };

    const expandAll = () => {
        const allIds = new Set<string>();
        const addIds = (accounts: UnitAccount[]) => {
            accounts.forEach((acc) => {
                if (acc.children && acc.children.length > 0) {
                    allIds.add(acc.id);
                    addIds(acc.children);
                }
            });
        };
        addIds(MOCK_UNIT_ACCOUNTS);
        setExpandedIds(allIds);
    };

    const collapseAll = () => {
        setExpandedIds(new Set());
    };

    // Filter top-level accounts by search (simplified - real implementation would search children too)
    const filteredAccounts = MOCK_UNIT_ACCOUNTS.filter((account) => {
        if (searchTerm === '') return true;
        const searchLower = searchTerm.toLowerCase();
        return (
            account.accountNumber.toLowerCase().includes(searchLower) ||
            account.name.toLowerCase().includes(searchLower)
        );
    });

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <Calculator className="h-8 w-8" />
                        Unit Accounts
                    </h1>
                    <p className="text-muted-foreground">
                        Manage unit accounts for tracking non-financial quantities
                    </p>

                </div>
                <Link href="/finance/unit-accounts/new">
                    <Button>
                        <Plus className="mr-2 h-4 w-4" />
                        New Unit Account
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
                        <BreadcrumbPage>Unit Accounts</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Search and Actions */}
            <Card>
                <CardHeader className="pb-3">
                    <CardTitle className="text-base">Search & View Options</CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="flex flex-col md:flex-row gap-4 items-start md:items-center">
                        <div className="relative flex-1">
                            <Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
                            <Input
                                placeholder="Search by account number or name..."
                                value={searchTerm}
                                onChange={(e) => setSearchTerm(e.target.value)}
                                className="pl-10"
                            />
                        </div>
                        <div className="flex gap-2">
                            <Button variant="outline" size="sm" onClick={expandAll}>
                                <FolderTree className="mr-2 h-4 w-4" />
                                Expand All
                            </Button>
                            <Button variant="outline" size="sm" onClick={collapseAll}>
                                Collapse All
                            </Button>
                        </div>
                    </div>
                </CardContent>
            </Card>

            {/* Unit Accounts Tree/Table */}
            <Card>
                <CardHeader>
                    <CardTitle>Account Hierarchy</CardTitle>
                    <CardDescription>
                        Unit accounts organized in a tree structure. Click arrows to expand/collapse.
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <div className="rounded-md border">
                        <table className="w-full">
                            <thead>
                                <tr className="border-b bg-muted/50">
                                    <th className="p-4 text-left font-medium w-[200px]">Account Number</th>
                                    <th className="p-4 text-left font-medium">Name</th>
                                    <th className="p-4 text-left font-medium w-[150px]">Unit Type</th>
                                    <th className="p-4 text-right font-medium w-[150px]">Balance</th>
                                    <th className="p-4 text-center font-medium w-[100px]">Status</th>
                                    <th className="p-4 text-right font-medium w-[80px]">Actions</th>
                                </tr>
                            </thead>
                            <tbody>
                                {filteredAccounts.map((account) => (
                                    <AccountRow
                                        key={account.id}
                                        account={account}
                                        level={1}
                                        expandedIds={expandedIds}
                                        onToggle={handleToggle}
                                    />
                                ))}
                                {filteredAccounts.length === 0 && (
                                    <tr>
                                        <td colSpan={6} className="p-8 text-center text-muted-foreground">
                                            No unit accounts found matching your search.
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
