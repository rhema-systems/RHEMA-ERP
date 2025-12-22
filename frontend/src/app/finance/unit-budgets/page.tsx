'use client';

import React, { useState, useMemo } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
    ChevronRight,
    Home,
    Search,
    Plus,
    TrendingUp,
    TrendingDown,
    Minus,
    Calendar,
    Settings,
    AlertTriangle,
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
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from '@/components/ui/table';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { BudgetVariance } from '@/types/unit-accounts';

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
const MOCK_FISCAL_PERIODS = [
    { id: 'p1', name: 'January 2024' },
    { id: 'p2', name: 'February 2024' },
    { id: 'p3', name: 'March 2024' },
    { id: 'p4', name: 'Q1 2024' },
    { id: 'p5', name: 'April 2024' },
];

const MOCK_BUDGET_VARIANCES: BudgetVariance[] = [
    {
        unitAccountId: '1',
        accountNumber: 'U-1100',
        accountName: 'Sales Department Employees',
        unitTypeCode: 'EMP',
        periodName: 'March 2024',
        budgetQuantity: 25,
        actualQuantity: 23,
        variance: -2,
        variancePercent: -8.0,
        isFavorable: true, // Under budget for cost accounts
    },
    {
        unitAccountId: '2',
        accountNumber: 'U-1200',
        accountName: 'Engineering Department Employees',
        unitTypeCode: 'EMP',
        periodName: 'March 2024',
        budgetQuantity: 50,
        actualQuantity: 52,
        variance: 2,
        variancePercent: 4.0,
        isFavorable: false,
    },
    {
        unitAccountId: '3',
        accountNumber: 'U-2100',
        accountName: 'Office Space - HQ',
        unitTypeCode: 'SQFT',
        periodName: 'March 2024',
        budgetQuantity: 10000,
        actualQuantity: 10000,
        variance: 0,
        variancePercent: 0,
        isFavorable: true,
    },
    {
        unitAccountId: '4',
        accountNumber: 'U-3100',
        accountName: 'Production Hours',
        unitTypeCode: 'HRS',
        periodName: 'March 2024',
        budgetQuantity: 2400,
        actualQuantity: 2650,
        variance: 250,
        variancePercent: 10.4,
        isFavorable: true, // Over budget for production is favorable
    },
    {
        unitAccountId: '5',
        accountNumber: 'U-4100',
        accountName: 'Units Produced',
        unitTypeCode: 'UNITS',
        periodName: 'March 2024',
        budgetQuantity: 1500,
        actualQuantity: 1380,
        variance: -120,
        variancePercent: -8.0,
        isFavorable: false,
    },
    {
        unitAccountId: '6',
        accountNumber: 'U-5100',
        accountName: 'Energy Consumption',
        unitTypeCode: 'KWH',
        periodName: 'March 2024',
        budgetQuantity: 50000,
        actualQuantity: 47500,
        variance: -2500,
        variancePercent: -5.0,
        isFavorable: true,
    },
];

// ============================================
// SUMMARY CARDS
// ============================================
interface SummaryCardsProps {
    variances: BudgetVariance[];
}

const SummaryCards: React.FC<SummaryCardsProps> = ({ variances }) => {
    const favorable = variances.filter((v) => v.isFavorable);
    const unfavorable = variances.filter((v) => !v.isFavorable);
    const onBudget = variances.filter((v) => v.variance === 0);

    return (
        <div className="grid gap-4 md:grid-cols-4">
            <Card>
                <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                    <CardTitle className="text-sm font-medium">Total Accounts</CardTitle>
                    <Calendar className="h-4 w-4 text-muted-foreground" />
                </CardHeader>
                <CardContent>
                    <div className="text-2xl font-bold">{variances.length}</div>
                    <p className="text-xs text-muted-foreground">Accounts with budget</p>
                </CardContent>
            </Card>
            <Card>
                <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                    <CardTitle className="text-sm font-medium">Favorable</CardTitle>
                    <TrendingUp className="h-4 w-4 text-green-500" />
                </CardHeader>
                <CardContent>
                    <div className="text-2xl font-bold text-green-600">{favorable.length}</div>
                    <p className="text-xs text-muted-foreground">Within or under budget</p>
                </CardContent>
            </Card>
            <Card>
                <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                    <CardTitle className="text-sm font-medium">Unfavorable</CardTitle>
                    <TrendingDown className="h-4 w-4 text-red-500" />
                </CardHeader>
                <CardContent>
                    <div className="text-2xl font-bold text-red-600">{unfavorable.length}</div>
                    <p className="text-xs text-muted-foreground">Over budget</p>
                </CardContent>
            </Card>
            <Card>
                <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                    <CardTitle className="text-sm font-medium">On Budget</CardTitle>
                    <Minus className="h-4 w-4 text-blue-500" />
                </CardHeader>
                <CardContent>
                    <div className="text-2xl font-bold text-blue-600">{onBudget.length}</div>
                    <p className="text-xs text-muted-foreground">Exactly on target</p>
                </CardContent>
            </Card>
        </div>
    );
};

// ============================================
// MAIN COMPONENT
// ============================================
export default function UnitBudgetsPage() {
    const router = useRouter();
    const [searchQuery, setSearchQuery] = useState('');
    const [selectedPeriod, setSelectedPeriod] = useState('p3');
    const [filterStatus, setFilterStatus] = useState<'all' | 'favorable' | 'unfavorable'>('all');

    // Filter variances
    const filteredVariances = useMemo(() => {
        let result = [...MOCK_BUDGET_VARIANCES];

        if (searchQuery) {
            const query = searchQuery.toLowerCase();
            result = result.filter(
                (v) =>
                    v.accountNumber.toLowerCase().includes(query) ||
                    v.accountName.toLowerCase().includes(query)
            );
        }

        if (filterStatus === 'favorable') {
            result = result.filter((v) => v.isFavorable);
        } else if (filterStatus === 'unfavorable') {
            result = result.filter((v) => !v.isFavorable);
        }

        return result;
    }, [searchQuery, filterStatus]);

    const formatNumber = (value: number, decimals: number = 0) => {
        return new Intl.NumberFormat('en-US', {
            minimumFractionDigits: decimals,
            maximumFractionDigits: decimals,
        }).format(value);
    };

    const getVarianceColor = (variance: BudgetVariance) => {
        if (variance.variance === 0) return 'text-blue-600';
        return variance.isFavorable ? 'text-green-600' : 'text-red-600';
    };

    const getVarianceBadge = (variance: BudgetVariance) => {
        if (variance.variance === 0) {
            return <Badge variant="outline" className="bg-blue-50">On Budget</Badge>;
        }
        return variance.isFavorable ? (
            <Badge variant="outline" className="bg-green-50 text-green-700">Favorable</Badge>
        ) : (
            <Badge variant="outline" className="bg-red-50 text-red-700">Unfavorable</Badge>
        );
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
                <span className="text-foreground">Unit Budgets</span>
            </nav>

            <DemoModeBanner />

            {/* Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Unit Budgets</h1>
                    <p className="text-muted-foreground">
                        Budget vs Actual variance analysis for unit accounts
                    </p>
                </div>
                <div className="flex items-center gap-2">
                    <Button variant="outline" asChild>
                        <Link href="/finance/unit-budgets/manage">
                            <Settings className="mr-2 h-4 w-4" />
                            Manage Budgets
                        </Link>
                    </Button>
                    <Button asChild>
                        <Link href="/finance/unit-budgets/new">
                            <Plus className="mr-2 h-4 w-4" />
                            New Budget Entry
                        </Link>
                    </Button>
                </div>
            </div>

            {/* Summary Cards */}
            <SummaryCards variances={filteredVariances} />

            {/* Filters */}
            <Card>
                <CardHeader>
                    <CardTitle>Variance Analysis</CardTitle>
                    <CardDescription>
                        Compare budgeted quantities against actual balances
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <div className="flex flex-wrap items-center gap-4 mb-4">
                        <div className="relative flex-1 min-w-[200px]">
                            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                            <Input
                                placeholder="Search accounts..."
                                value={searchQuery}
                                onChange={(e) => setSearchQuery(e.target.value)}
                                className="pl-9"
                            />
                        </div>
                        <Select value={selectedPeriod} onValueChange={setSelectedPeriod}>
                            <SelectTrigger className="w-[180px]">
                                <SelectValue placeholder="Select period" />
                            </SelectTrigger>
                            <SelectContent>
                                {MOCK_FISCAL_PERIODS.map((period) => (
                                    <SelectItem key={period.id} value={period.id}>
                                        {period.name}
                                    </SelectItem>
                                ))}
                            </SelectContent>
                        </Select>
                        <Select
                            value={filterStatus}
                            onValueChange={(v) => setFilterStatus(v as 'all' | 'favorable' | 'unfavorable')}
                        >
                            <SelectTrigger className="w-[150px]">
                                <SelectValue placeholder="Filter status" />
                            </SelectTrigger>
                            <SelectContent>
                                <SelectItem value="all">All Variances</SelectItem>
                                <SelectItem value="favorable">Favorable Only</SelectItem>
                                <SelectItem value="unfavorable">Unfavorable Only</SelectItem>
                            </SelectContent>
                        </Select>
                    </div>

                    {/* Variance Table */}
                    <div className="rounded-md border">
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead>Account</TableHead>
                                    <TableHead>Type</TableHead>
                                    <TableHead className="text-right">Budget</TableHead>
                                    <TableHead className="text-right">Actual</TableHead>
                                    <TableHead className="text-right">Variance</TableHead>
                                    <TableHead className="text-right">Variance %</TableHead>
                                    <TableHead>Status</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {filteredVariances.map((variance) => (
                                    <TableRow
                                        key={variance.unitAccountId}
                                        className="cursor-pointer hover:bg-muted/50"
                                        onClick={() => router.push(`/finance/unit-accounts/${variance.unitAccountId}`)}
                                    >
                                        <TableCell>
                                            <div>
                                                <div className="font-medium">{variance.accountNumber}</div>
                                                <div className="text-sm text-muted-foreground">{variance.accountName}</div>
                                            </div>
                                        </TableCell>
                                        <TableCell>
                                            <Badge variant="outline">{variance.unitTypeCode}</Badge>
                                        </TableCell>
                                        <TableCell className="text-right font-mono">
                                            {formatNumber(variance.budgetQuantity)}
                                        </TableCell>
                                        <TableCell className="text-right font-mono">
                                            {formatNumber(variance.actualQuantity)}
                                        </TableCell>
                                        <TableCell className={`text-right font-mono ${getVarianceColor(variance)}`}>
                                            {variance.variance > 0 ? '+' : ''}
                                            {formatNumber(variance.variance)}
                                        </TableCell>
                                        <TableCell className={`text-right font-mono ${getVarianceColor(variance)}`}>
                                            {variance.variancePercent > 0 ? '+' : ''}
                                            {variance.variancePercent.toFixed(1)}%
                                        </TableCell>
                                        <TableCell>{getVarianceBadge(variance)}</TableCell>
                                    </TableRow>
                                ))}
                                {filteredVariances.length === 0 && (
                                    <TableRow>
                                        <TableCell colSpan={7} className="text-center py-8 text-muted-foreground">
                                            No budget variances found for the selected criteria.
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
