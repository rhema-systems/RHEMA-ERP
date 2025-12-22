'use client';

import React, { useState, useMemo } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
    ChevronRight,
    Home,
    Search,
    Plus,
    Play,
    Settings,
    MoreHorizontal,
    AlertTriangle,
    ArrowRightLeft,
    Calculator,
    Percent,
    Equal,
} from 'lucide-react';
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
} from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from '@/components/ui/table';
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { AllocationRule, AllocationType } from '@/types/unit-accounts';

// ============================================
// DEMO MODE BANNER
// ============================================
const DemoModeBanner = () => (
    <Alert className="mb-4 border-yellow-500 bg-yellow-50">
        <AlertTriangle className="h-4 w-4 text-yellow-600" />
        <AlertDescription className="text-yellow-700">
            <strong>⚠️ DEMO FRONTEND UI</strong>
        </AlertDescription>
    </Alert>
);

// ============================================
// MOCK DATA
// ============================================
const MOCK_ALLOCATION_RULES: AllocationRule[] = [
    {
        id: '1',
        code: 'RENT-ALLOC',
        name: 'Rent Allocation by Square Feet',
        description: 'Allocate rent expense across departments based on square footage',
        sourceAccountId: 's1',
        sourceAccountNumber: '6100-000',
        sourceAccountName: 'Rent Expense',
        allocationType: 'UnitAccountBased',
        driverUnitAccountId: 'd1',
        driverUnitAccountNumber: 'U-2000',
        driverUnitAccountName: 'Office Space Total',
        isActive: true,
        autoReverse: false,
        lastRunDate: '2024-03-31',
        targets: [
            { id: 't1', allocationRuleId: '1', targetAccountId: 'a1', targetAccountNumber: '6100-100', targetAccountName: 'Rent - Sales', targetDriverUnitAccountNumber: 'U-2100' },
            { id: 't2', allocationRuleId: '1', targetAccountId: 'a2', targetAccountNumber: '6100-200', targetAccountName: 'Rent - Engineering', targetDriverUnitAccountNumber: 'U-2200' },
            { id: 't3', allocationRuleId: '1', targetAccountId: 'a3', targetAccountNumber: '6100-300', targetAccountName: 'Rent - Admin', targetDriverUnitAccountNumber: 'U-2300' },
        ],
        createdAt: '2024-01-15',
        createdBy: 'admin',
    },
    {
        id: '2',
        code: 'IT-OVERHEAD',
        name: 'IT Overhead by Headcount',
        description: 'Distribute IT department costs based on employee count',
        sourceAccountId: 's2',
        sourceAccountNumber: '7200-000',
        sourceAccountName: 'IT Department Overhead',
        allocationType: 'UnitAccountBased',
        driverUnitAccountId: 'd2',
        driverUnitAccountNumber: 'U-1000',
        driverUnitAccountName: 'Total Employees',
        isActive: true,
        autoReverse: true,
        lastRunDate: '2024-03-31',
        targets: [
            { id: 't4', allocationRuleId: '2', targetAccountId: 'a4', targetAccountNumber: '7200-100', targetAccountName: 'IT Alloc - Sales', targetDriverUnitAccountNumber: 'U-1100' },
            { id: 't5', allocationRuleId: '2', targetAccountId: 'a5', targetAccountNumber: '7200-200', targetAccountName: 'IT Alloc - Engineering', targetDriverUnitAccountNumber: 'U-1200' },
        ],
        createdAt: '2024-01-20',
        createdBy: 'admin',
    },
    {
        id: '3',
        code: 'UTIL-FIXED',
        name: 'Utilities Fixed Split',
        description: 'Fixed percentage allocation of utility costs',
        sourceAccountId: 's3',
        sourceAccountNumber: '6200-000',
        sourceAccountName: 'Utilities Expense',
        allocationType: 'FixedPercentage',
        isActive: true,
        autoReverse: false,
        lastRunDate: '2024-03-31',
        targets: [
            { id: 't6', allocationRuleId: '3', targetAccountId: 'a6', targetAccountNumber: '6200-100', targetAccountName: 'Utilities - Production', fixedPercentage: 60 },
            { id: 't7', allocationRuleId: '3', targetAccountId: 'a7', targetAccountNumber: '6200-200', targetAccountName: 'Utilities - Admin', fixedPercentage: 25 },
            { id: 't8', allocationRuleId: '3', targetAccountId: 'a8', targetAccountNumber: '6200-300', targetAccountName: 'Utilities - Warehouse', fixedPercentage: 15 },
        ],
        createdAt: '2024-02-01',
        createdBy: 'admin',
    },
    {
        id: '4',
        code: 'MGMT-EQUAL',
        name: 'Management Fee Equal Split',
        description: 'Equally distribute management fees across all divisions',
        sourceAccountId: 's4',
        sourceAccountNumber: '7300-000',
        sourceAccountName: 'Corporate Management Fee',
        allocationType: 'EqualDistribution',
        isActive: false,
        autoReverse: false,
        targets: [
            { id: 't9', allocationRuleId: '4', targetAccountId: 'a9', targetAccountNumber: '7300-100', targetAccountName: 'Mgmt Fee - Division A' },
            { id: 't10', allocationRuleId: '4', targetAccountId: 'a10', targetAccountNumber: '7300-200', targetAccountName: 'Mgmt Fee - Division B' },
            { id: 't11', allocationRuleId: '4', targetAccountId: 'a11', targetAccountNumber: '7300-300', targetAccountName: 'Mgmt Fee - Division C' },
        ],
        createdAt: '2024-02-15',
        createdBy: 'admin',
    },
];

// ============================================
// HELPER COMPONENTS
// ============================================
const getAllocationTypeIcon = (type: AllocationType) => {
    switch (type) {
        case 'UnitAccountBased':
            return <Calculator className="h-4 w-4" />;
        case 'FixedPercentage':
            return <Percent className="h-4 w-4" />;
        case 'EqualDistribution':
            return <Equal className="h-4 w-4" />;
    }
};

const getAllocationTypeBadge = (type: AllocationType) => {
    switch (type) {
        case 'UnitAccountBased':
            return <Badge variant="outline" className="bg-blue-50">Unit Based</Badge>;
        case 'FixedPercentage':
            return <Badge variant="outline" className="bg-purple-50">Fixed %</Badge>;
        case 'EqualDistribution':
            return <Badge variant="outline" className="bg-green-50">Equal Split</Badge>;
    }
};

// ============================================
// MAIN COMPONENT
// ============================================
export default function AllocationsPage() {
    const router = useRouter();
    const [searchQuery, setSearchQuery] = useState('');
    const [filterActive, setFilterActive] = useState<'all' | 'active' | 'inactive'>('all');

    // Filter rules
    const filteredRules = useMemo(() => {
        let result = [...MOCK_ALLOCATION_RULES];

        if (searchQuery) {
            const query = searchQuery.toLowerCase();
            result = result.filter(
                (r) =>
                    r.code.toLowerCase().includes(query) ||
                    r.name.toLowerCase().includes(query) ||
                    r.sourceAccountNumber?.toLowerCase().includes(query)
            );
        }

        if (filterActive === 'active') {
            result = result.filter((r) => r.isActive);
        } else if (filterActive === 'inactive') {
            result = result.filter((r) => !r.isActive);
        }

        return result;
    }, [searchQuery, filterActive]);

    const handleRunAllocation = (rule: AllocationRule) => {
        console.log('Running allocation:', rule.code);
        alert(`Simulating allocation run for: ${rule.name}\n\nThis would create a journal entry allocating the source account balance to target accounts.`);
    };

    return (
        <div className="space-y-6">
            {/* Breadcrumbs */}
            <nav className="flex items-center space-x-2 text-sm text-muted-foreground">
                <Link href="/" className="flex items-center hover:text-foreground">
                    <Home className="h-4 w-4" />
                </Link>
                <ChevronRight className="h-4 w-4" />
                <Link href="/finance" className="hover:text-foreground">
                    Finance
                </Link>
                <ChevronRight className="h-4 w-4" />
                <span className="text-foreground">Allocation Rules</span>
            </nav>

            <DemoModeBanner />

            {/* Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Allocation Rules</h1>
                    <p className="text-muted-foreground">
                        Define rules to distribute financial amounts using unit account drivers
                    </p>
                </div>
                <Button asChild>
                    <Link href="/finance/allocations/new">
                        <Plus className="mr-2 h-4 w-4" />
                        New Allocation Rule
                    </Link>
                </Button>
            </div>

            {/* Summary Stats */}
            <div className="grid gap-4 md:grid-cols-3">
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                        <CardTitle className="text-sm font-medium">Total Rules</CardTitle>
                        <ArrowRightLeft className="h-4 w-4 text-muted-foreground" />
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{MOCK_ALLOCATION_RULES.length}</div>
                        <p className="text-xs text-muted-foreground">Allocation rules defined</p>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                        <CardTitle className="text-sm font-medium">Active Rules</CardTitle>
                        <Play className="h-4 w-4 text-green-500" />
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold text-green-600">
                            {MOCK_ALLOCATION_RULES.filter((r) => r.isActive).length}
                        </div>
                        <p className="text-xs text-muted-foreground">Ready to run</p>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                        <CardTitle className="text-sm font-medium">Unit-Based</CardTitle>
                        <Calculator className="h-4 w-4 text-blue-500" />
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold text-blue-600">
                            {MOCK_ALLOCATION_RULES.filter((r) => r.allocationType === 'UnitAccountBased').length}
                        </div>
                        <p className="text-xs text-muted-foreground">Using unit account drivers</p>
                    </CardContent>
                </Card>
            </div>

            {/* Rules List */}
            <Card>
                <CardHeader>
                    <CardTitle>Allocation Rules</CardTitle>
                    <CardDescription>
                        Rules for distributing financial amounts based on unit quantities or percentages
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <div className="flex flex-wrap items-center gap-4 mb-4">
                        <div className="relative flex-1 min-w-[200px]">
                            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                            <Input
                                placeholder="Search rules..."
                                value={searchQuery}
                                onChange={(e) => setSearchQuery(e.target.value)}
                                className="pl-9"
                            />
                        </div>
                        <div className="flex gap-2">
                            <Button
                                variant={filterActive === 'all' ? 'default' : 'outline'}
                                size="sm"
                                onClick={() => setFilterActive('all')}
                            >
                                All
                            </Button>
                            <Button
                                variant={filterActive === 'active' ? 'default' : 'outline'}
                                size="sm"
                                onClick={() => setFilterActive('active')}
                            >
                                Active
                            </Button>
                            <Button
                                variant={filterActive === 'inactive' ? 'default' : 'outline'}
                                size="sm"
                                onClick={() => setFilterActive('inactive')}
                            >
                                Inactive
                            </Button>
                        </div>
                    </div>

                    {/* Rules Table */}
                    <div className="rounded-md border">
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead>Rule</TableHead>
                                    <TableHead>Source Account</TableHead>
                                    <TableHead>Type</TableHead>
                                    <TableHead>Driver</TableHead>
                                    <TableHead>Targets</TableHead>
                                    <TableHead>Last Run</TableHead>
                                    <TableHead>Status</TableHead>
                                    <TableHead className="w-[100px]">Actions</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {filteredRules.map((rule) => (
                                    <TableRow key={rule.id}>
                                        <TableCell>
                                            <div>
                                                <div className="font-medium">{rule.code}</div>
                                                <div className="text-sm text-muted-foreground">{rule.name}</div>
                                            </div>
                                        </TableCell>
                                        <TableCell>
                                            <div>
                                                <div className="font-mono text-sm">{rule.sourceAccountNumber}</div>
                                                <div className="text-sm text-muted-foreground">{rule.sourceAccountName}</div>
                                            </div>
                                        </TableCell>
                                        <TableCell>{getAllocationTypeBadge(rule.allocationType)}</TableCell>
                                        <TableCell>
                                            {rule.driverUnitAccountNumber ? (
                                                <div>
                                                    <div className="font-mono text-sm">{rule.driverUnitAccountNumber}</div>
                                                    <div className="text-sm text-muted-foreground">{rule.driverUnitAccountName}</div>
                                                </div>
                                            ) : (
                                                <span className="text-muted-foreground">—</span>
                                            )}
                                        </TableCell>
                                        <TableCell>
                                            <Badge variant="secondary">{rule.targets.length} targets</Badge>
                                        </TableCell>
                                        <TableCell>
                                            {rule.lastRunDate ? (
                                                <span className="text-sm">{new Date(rule.lastRunDate).toLocaleDateString()}</span>
                                            ) : (
                                                <span className="text-muted-foreground">Never</span>
                                            )}
                                        </TableCell>
                                        <TableCell>
                                            {rule.isActive ? (
                                                <Badge className="bg-green-100 text-green-800">Active</Badge>
                                            ) : (
                                                <Badge variant="secondary">Inactive</Badge>
                                            )}
                                        </TableCell>
                                        <TableCell>
                                            <DropdownMenu>
                                                <DropdownMenuTrigger asChild>
                                                    <Button variant="ghost" size="icon">
                                                        <MoreHorizontal className="h-4 w-4" />
                                                    </Button>
                                                </DropdownMenuTrigger>
                                                <DropdownMenuContent align="end">
                                                    <DropdownMenuItem onClick={() => router.push(`/finance/allocations/${rule.id}`)}>
                                                        <Settings className="mr-2 h-4 w-4" />
                                                        Edit Rule
                                                    </DropdownMenuItem>
                                                    <DropdownMenuItem
                                                        onClick={() => handleRunAllocation(rule)}
                                                        disabled={!rule.isActive}
                                                    >
                                                        <Play className="mr-2 h-4 w-4" />
                                                        Run Allocation
                                                    </DropdownMenuItem>
                                                    <DropdownMenuSeparator />
                                                    <DropdownMenuItem onClick={() => {
                                                        alert(`⚠️ DEMO MODE: Allocation History for ${rule.code}\n\nLast 3 Runs:\n• ${rule.lastRunDate ? new Date(rule.lastRunDate).toLocaleDateString() : 'N/A'} - $${(Math.random() * 10000 + 1000).toFixed(2)} allocated\n• ${rule.lastRunDate ? new Date(new Date(rule.lastRunDate).getTime() - 30 * 24 * 60 * 60 * 1000).toLocaleDateString() : 'N/A'} - $${(Math.random() * 10000 + 1000).toFixed(2)} allocated\n• ${rule.lastRunDate ? new Date(new Date(rule.lastRunDate).getTime() - 60 * 24 * 60 * 60 * 1000).toLocaleDateString() : 'N/A'} - $${(Math.random() * 10000 + 1000).toFixed(2)} allocated\n\nFull history available when connected to API.`);
                                                    }}>
                                                        View History
                                                    </DropdownMenuItem>
                                                </DropdownMenuContent>
                                            </DropdownMenu>
                                        </TableCell>
                                    </TableRow>
                                ))}
                                {filteredRules.length === 0 && (
                                    <TableRow>
                                        <TableCell colSpan={8} className="text-center py-8 text-muted-foreground">
                                            No allocation rules found.
                                        </TableCell>
                                    </TableRow>
                                )}
                            </TableBody>
                        </Table>
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}
