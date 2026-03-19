'use client';

import React, { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow
} from '@/components/ui/table';
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuTrigger
} from '@/components/ui/dropdown-menu';
import {
    Divide,
    Plus,
    Search,
    MoreHorizontal,
    Pencil,
    Trash2,
    ToggleLeft,
    ToggleRight,
    Calculator
} from 'lucide-react';
import Link from 'next/link';
import type { RatioDefinition } from '@/types/unit-accounts';

// MOCK DATA
const MOCK_RATIOS: RatioDefinition[] = [
    {
        id: 'rd-1',
        code: 'REV-EMP',
        name: 'Revenue Per Employee',
        description: 'Total revenue divided by employee headcount',
        numeratorType: 'FinancialAccount',
        numeratorAccountId: 'acc-revenue',
        denominatorType: 'UnitAccount',
        denominatorAccountId: 'ua-1',
        resultFormat: 'Currency',
        decimalPlaces: 2,
        isActive: true,
        createdAt: '2024-01-01T00:00:00Z',
        createdBy: 'admin',
    },
    {
        id: 'rd-2',
        code: 'COST-SQFT',
        name: 'Cost Per Square Foot',
        description: 'Operating costs per unit of floor space',
        numeratorType: 'FinancialAccount',
        numeratorAccountId: 'acc-opex',
        denominatorType: 'UnitAccount',
        denominatorAccountId: 'ua-5',
        resultFormat: 'Currency',
        decimalPlaces: 2,
        isActive: true,
        createdAt: '2024-01-01T00:00:00Z',
        createdBy: 'admin',
    },
    {
        id: 'rd-3',
        code: 'UNITS-HR',
        name: 'Units Per Machine Hour',
        description: 'Production efficiency ratio',
        numeratorType: 'UnitAccount',
        numeratorAccountId: 'ua-prod',
        denominatorType: 'UnitAccount',
        denominatorAccountId: 'ua-8',
        resultFormat: 'Decimal',
        decimalPlaces: 2,
        isActive: true,
        createdAt: '2024-01-01T00:00:00Z',
        createdBy: 'admin',
    },
    {
        id: 'rd-4',
        code: 'GP-PCT',
        name: 'Gross Profit Margin',
        description: 'Gross profit as percentage of revenue',
        numeratorType: 'FinancialAccount',
        numeratorAccountId: 'acc-gp',
        denominatorType: 'FinancialAccount',
        denominatorAccountId: 'acc-revenue',
        resultFormat: 'Percentage',
        decimalPlaces: 1,
        isActive: true,
        createdAt: '2024-01-01T00:00:00Z',
        createdBy: 'admin',
    },
];

export default function RatioDefinitionsPage() {
    const [ratios] = useState<RatioDefinition[]>(MOCK_RATIOS);
    const [searchTerm, setSearchTerm] = useState('');

    const filteredRatios = ratios.filter((ratio) => {
        if (searchTerm === '') return true;
        const searchLower = searchTerm.toLowerCase();
        return (
            ratio.code.toLowerCase().includes(searchLower) ||
            ratio.name.toLowerCase().includes(searchLower)
        );
    });

    const getFormatBadge = (format: RatioDefinition['resultFormat']) => {
        const colors = {
            Currency: 'bg-green-500',
            Percentage: 'bg-blue-500',
            Decimal: 'bg-gray-500',
        };
        return <Badge className={colors[format]}>{format}</Badge>;
    };

    const getTypeBadge = (type: RatioDefinition['numeratorType']) => {
        const labels = {
            FinancialAccount: 'Financial',
            UnitAccount: 'Unit',
            Constant: 'Constant',
        };
        return <Badge variant="outline">{labels[type]}</Badge>;
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <Divide className="h-8 w-8" />
                        Ratio Definitions
                    </h1>
                    <p className="text-muted-foreground">
                        Define KPI ratios combining financial and unit data
                    </p>

                </div>
                <div className="flex gap-2">
                    <Link href="/finance/ratio-definitions/calculator">
                        <Button variant="outline">
                            <Calculator className="mr-2 h-4 w-4" />
                            Calculator
                        </Button>
                    </Link>
                    <Link href="/finance/ratio-definitions/new">
                        <Button>
                            <Plus className="mr-2 h-4 w-4" />
                            New Ratio
                        </Button>
                    </Link>
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
                        <BreadcrumbPage>Ratio Definitions</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Search */}
            <Card>
                <CardHeader className="pb-3">
                    <CardTitle className="text-base">Search</CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="relative">
                        <Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
                        <Input
                            placeholder="Search by code or name..."
                            value={searchTerm}
                            onChange={(e) => setSearchTerm(e.target.value)}
                            className="pl-10"
                        />
                    </div>
                </CardContent>
            </Card>

            {/* Ratios Table */}
            <Card>
                <CardHeader>
                    <CardTitle>Ratios ({filteredRatios.length})</CardTitle>
                    <CardDescription>
                        Custom KPI formulas for financial and operational analysis
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHead className="w-[120px]">Code</TableHead>
                                <TableHead>Name</TableHead>
                                <TableHead className="text-center">Numerator</TableHead>
                                <TableHead className="text-center">Denominator</TableHead>
                                <TableHead className="text-center">Format</TableHead>
                                <TableHead className="text-center">Status</TableHead>
                                <TableHead className="text-right">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {filteredRatios.map((ratio) => (
                                <TableRow key={ratio.id}>
                                    <TableCell className="font-mono font-semibold text-blue-600">
                                        {ratio.code}
                                    </TableCell>
                                    <TableCell>
                                        <div>
                                            <p className="font-medium">{ratio.name}</p>
                                            <p className="text-sm text-muted-foreground truncate max-w-xs">
                                                {ratio.description}
                                            </p>
                                        </div>
                                    </TableCell>
                                    <TableCell className="text-center">
                                        {getTypeBadge(ratio.numeratorType)}
                                    </TableCell>
                                    <TableCell className="text-center">
                                        {getTypeBadge(ratio.denominatorType)}
                                    </TableCell>
                                    <TableCell className="text-center">
                                        {getFormatBadge(ratio.resultFormat)}
                                    </TableCell>
                                    <TableCell className="text-center">
                                        <Badge variant={ratio.isActive ? 'default' : 'secondary'}>
                                            {ratio.isActive ? 'Active' : 'Inactive'}
                                        </Badge>
                                    </TableCell>
                                    <TableCell className="text-right">
                                        <DropdownMenu>
                                            <DropdownMenuTrigger asChild>
                                                <Button variant="ghost" size="sm">
                                                    <MoreHorizontal className="h-4 w-4" />
                                                </Button>
                                            </DropdownMenuTrigger>
                                            <DropdownMenuContent align="end">
                                                <DropdownMenuItem asChild>
                                                    <Link href={`/finance/ratio-definitions/${ratio.id}`}>
                                                        <Pencil className="mr-2 h-4 w-4" />
                                                        Edit
                                                    </Link>
                                                </DropdownMenuItem>
                                                <DropdownMenuItem asChild>
                                                    <Link href={`/finance/ratio-definitions/calculator?id=${ratio.id}`}>
                                                        <Calculator className="mr-2 h-4 w-4" />
                                                        Calculate
                                                    </Link>
                                                </DropdownMenuItem>
                                                <DropdownMenuItem>
                                                    {ratio.isActive ? (
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
                                    </TableCell>
                                </TableRow>
                            ))}
                            {filteredRatios.length === 0 && (
                                <TableRow>
                                    <TableCell colSpan={7} className="text-center py-8 text-muted-foreground">
                                        No ratio definitions found.
                                    </TableCell>
                                </TableRow>
                            )}
                        </TableBody>
                    </Table>
                </CardContent>
            </Card>
        </div>
    );
}
