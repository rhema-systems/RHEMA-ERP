'use client';

import { useQuery } from '@tanstack/react-query';
import {
    BarChart3,
    TrendingDown,
    Building2,
    Wallet,
    AlertCircle,
    Clock,
    CheckCircle2,
    DollarSign,
    Calendar,
    ArrowUpRight,
    ArrowDownRight,
    Tag
} from 'lucide-react';
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
} from '@/components/ui/card';
import { accountsPayableService } from '@/services/accountsPayableService';
import { formatCurrency } from '@/lib/utils';
import { Skeleton } from '@/components/ui/skeleton';
import {
    BarChart,
    Bar,
    XAxis,
    YAxis,
    CartesianGrid,
    Tooltip,
    ResponsiveContainer,
    Cell
} from 'recharts';

export default function ApDashboardPage() {
    const { data: apSummary, isLoading: summaryLoading } = useQuery({
        queryKey: ['ap-summary'],
        queryFn: () => accountsPayableService.getApSummary(),
    });

    const { data: agingReport, isLoading: agingLoading } = useQuery({
        queryKey: ['ap-aging-summary'],
        queryFn: () => accountsPayableService.getAgingReport(),
    });

    // Prepare chart data from aging report
    const agingChartData = [
        { name: 'Current', amount: agingReport?.current || 0 },
        { name: '1-30 Days', amount: agingReport?.thirtyDays || 0 },
        { name: '31-60 Days', amount: agingReport?.sixtyDays || 0 },
        { name: '60+ Days', amount: agingReport?.ninetyPlusDays || 0 }
    ];

    const COLORS = ['#10b981', '#3b82f6', '#f59e0b', '#ef4444'];
    const dashboardCurrencyCode = agingReport?.currencyCode || 'GHS';

    if (summaryLoading || agingLoading) {
        return <DashboardSkeleton />;
    }

    return (
        <div className="space-y-8 p-8 max-w-[1600px] mx-auto">
            <div>
                <h1 className="text-3xl font-bold tracking-tight">Accounts Payable</h1>
                <p className="text-muted-foreground mt-2">
                    Overview of your payables, supplier performance, and cash requirements.
                </p>
            </div>

            {/* KPI Cards */}
            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                        <CardTitle className="text-sm font-medium">Total Outstanding</CardTitle>
                        <DollarSign className="h-4 w-4 text-muted-foreground" />
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{formatCurrency(apSummary?.totalOutstanding || 0, dashboardCurrencyCode)}</div>
                        <p className="text-xs text-muted-foreground mt-1">
                            {apSummary?.outstandingInvoiceCount || 0} open invoices
                        </p>
                    </CardContent>
                </Card>

                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                        <CardTitle className="text-sm font-medium">Overdue Amount</CardTitle>
                        <AlertCircle className="h-4 w-4 text-red-500" />
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold text-red-600">
                            {formatCurrency(apSummary?.totalOverdue || 0, dashboardCurrencyCode)}
                        </div>
                        <p className="text-xs flex items-center mt-1 text-red-600/80">
                            {apSummary?.overdueInvoiceCount || 0} overdue invoices
                        </p>
                    </CardContent>
                </Card>

                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                        <CardTitle className="text-sm font-medium">Paid This Month</CardTitle>
                        <CheckCircle2 className="h-4 w-4 text-green-500" />
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold text-green-600">
                            {formatCurrency(apSummary?.totalPaidThisMonth || 0, dashboardCurrencyCode)}
                        </div>
                        <p className="text-xs text-muted-foreground mt-1">
                            Discounts taken: {formatCurrency(apSummary?.discountsTaken || 0, dashboardCurrencyCode)}
                        </p>
                    </CardContent>
                </Card>

                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                        <CardTitle className="text-sm font-medium">Avg Days to Payment</CardTitle>
                        <Clock className="h-4 w-4 text-blue-500" />
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold text-blue-600">
                            {apSummary?.averageDaysToPayment?.toFixed(0) || 0} days
                        </div>
                        <p className="text-xs text-muted-foreground mt-1">
                            Payment efficiency
                        </p>
                    </CardContent>
                </Card>
            </div>

            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-7">
                {/* Aging Chart */}
                <Card className="col-span-4">
                    <CardHeader>
                        <CardTitle>Payables Aging Analysis</CardTitle>
                        <CardDescription>
                            Breakdown of outstanding balances by age buckets
                        </CardDescription>
                    </CardHeader>
                    <CardContent className="pl-2">
                        <div className="h-[350px]">
                            <ResponsiveContainer width="100%" height="100%">
                                <BarChart data={agingChartData}>
                                    <CartesianGrid strokeDasharray="3 3" className="stroke-muted" />
                                    <XAxis
                                        dataKey="name"
                                        stroke="#888888"
                                        fontSize={12}
                                        tickLine={false}
                                        axisLine={false}
                                    />
                                    <YAxis
                                        stroke="#888888"
                                        fontSize={12}
                                        tickLine={false}
                                        axisLine={false}
                                        tickFormatter={(value) => formatCurrency(Number(value), dashboardCurrencyCode)}
                                    />
                                    <Tooltip
                                        cursor={{ fill: 'transparent' }}
                                        contentStyle={{ borderRadius: '8px', border: 'none', boxShadow: '0 4px 6px -1px rgb(0 0 0 / 0.1)' }}
                                    />
                                    <Bar dataKey="amount" radius={[4, 4, 0, 0]}>
                                        {agingChartData.map((entry, index) => (
                                            <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                                        ))}
                                    </Bar>
                                </BarChart>
                            </ResponsiveContainer>
                        </div>
                    </CardContent>
                </Card>

                {/* Quick Stats */}
                <Card className="col-span-3">
                    <CardHeader>
                        <CardTitle>Payables Health</CardTitle>
                        <CardDescription>
                            Key indicators of payables performance
                        </CardDescription>
                    </CardHeader>
                    <CardContent>
                        <div className="space-y-8">
                            <div className="flex items-center">
                                <div className="flex items-center justify-center w-9 h-9 rounded-full bg-blue-100 dark:bg-blue-900/20">
                                    <Building2 className="h-5 w-5 text-blue-600" />
                                </div>
                                <div className="ml-4 space-y-1">
                                    <p className="text-sm font-medium leading-none">Total Suppliers</p>
                                    <p className="text-sm text-muted-foreground">
                                        Active suppliers with balance
                                    </p>
                                </div>
                                <div className="ml-auto font-medium">
                                    {agingReport?.totalSuppliers || 0}
                                </div>
                            </div>

                            <div className="flex items-center">
                                <div className="flex items-center justify-center w-9 h-9 rounded-full bg-orange-100 dark:bg-orange-900/20">
                                    <Clock className="h-5 w-5 text-orange-600" />
                                </div>
                                <div className="ml-4 space-y-1">
                                    <p className="text-sm font-medium leading-none">Pending Approvals</p>
                                    <p className="text-sm text-muted-foreground">
                                        Invoices awaiting review
                                    </p>
                                </div>
                                <div className="ml-auto font-medium text-orange-600">
                                    {apSummary?.pendingApprovalCount || 0}
                                </div>
                            </div>

                            <div className="flex items-center">
                                <div className="flex items-center justify-center w-9 h-9 rounded-full bg-purple-100 dark:bg-purple-900/20">
                                    <Tag className="h-5 w-5 text-purple-600" />
                                </div>
                                <div className="ml-4 space-y-1">
                                    <p className="text-sm font-medium leading-none">Missed Discounts</p>
                                    <p className="text-sm text-muted-foreground">
                                        Lost early payment discounts
                                    </p>
                                </div>
                                <div className="ml-auto font-medium text-purple-600">
                                    {formatCurrency(apSummary?.discountsMissed || 0, dashboardCurrencyCode)}
                                </div>
                            </div>
                        </div>

                        <div className="mt-8">
                            <h4 className="mb-4 text-sm font-medium leading-none">Payables Distribution</h4>
                            <div className="space-y-4">
                                <div className="space-y-1">
                                    <div className="flex items-center justify-between text-sm">
                                        <span className="text-muted-foreground">Current (Not Due)</span>
                                        <span>{((agingReport?.current || 0) / (apSummary?.totalOutstanding || 1) * 100).toFixed(0)}%</span>
                                    </div>
                                    <div className="h-2 w-full rounded-full bg-secondary">
                                        <div
                                            className="h-full rounded-full bg-green-500"
                                            style={{ width: `${((agingReport?.current || 0) / (apSummary?.totalOutstanding || 1) * 100)}%` }}
                                        />
                                    </div>
                                </div>
                                <div className="space-y-1">
                                    <div className="flex items-center justify-between text-sm">
                                        <span className="text-muted-foreground">Overdue</span>
                                        <span>{(((apSummary?.totalOverdue || 0)) / (apSummary?.totalOutstanding || 1) * 100).toFixed(0)}%</span>
                                    </div>
                                    <div className="h-2 w-full rounded-full bg-secondary">
                                        <div
                                            className="h-full rounded-full bg-red-500"
                                            style={{ width: `${(((apSummary?.totalOverdue || 0)) / (apSummary?.totalOutstanding || 1) * 100)}%` }}
                                        />
                                    </div>
                                </div>
                            </div>
                        </div>
                    </CardContent>
                </Card>
            </div>
        </div>
    );
}

function DashboardSkeleton() {
    return (
        <div className="space-y-8 p-8 max-w-[1600px] mx-auto">
            <div>
                <Skeleton className="h-10 w-48" />
                <Skeleton className="h-4 w-96 mt-2" />
            </div>
            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
                {[1, 2, 3, 4].map((i) => (
                    <Skeleton key={i} className="h-32 w-full" />
                ))}
            </div>
            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-7">
                <Skeleton className="col-span-4 h-[400px]" />
                <Skeleton className="col-span-3 h-[400px]" />
            </div>
        </div>
    );
}
