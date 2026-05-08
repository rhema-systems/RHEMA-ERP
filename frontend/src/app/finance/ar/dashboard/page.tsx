'use client';

import { useQuery } from '@tanstack/react-query';
import {
    BarChart3,
    TrendingUp,
    Users,
    Wallet,
    AlertCircle,
    Clock,
    CheckCircle2,
    DollarSign,
    ArrowUpRight,
    ArrowDownRight
} from 'lucide-react';
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
} from '@/components/ui/card';
import { arService } from '@/services/ar-service';
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

export default function ArDashboardPage() {
    const { data: dashboardData, isLoading: dashboardLoading } = useQuery({
        queryKey: ['ar-dashboard'],
        queryFn: () => arService.getCollectionsDashboard(),
    });

    const { data: arSummary, isLoading: summaryLoading } = useQuery({
        queryKey: ['ar-summary'],
        queryFn: () => arService.getArSummary(),
    });

    const { data: agingReport, isLoading: agingLoading } = useQuery({
        queryKey: ['ar-aging-summary'],
        queryFn: () => arService.getAgingReport(),
    });

    // Prepare chart data from aging report
    const agingChartData = agingReport?.buckets.map(bucket => ({
        name: bucket.bucket,
        amount: bucket.amount,
        count: bucket.customerCount
    })) || [];

    const COLORS = ['#10b981', '#3b82f6', '#f59e0b', '#ef4444', '#7f1d1d'];

    if (dashboardLoading || summaryLoading || agingLoading) {
        return <DashboardSkeleton />;
    }

    return (
        <div className="space-y-8 p-8 max-w-[1600px] mx-auto">
            <div>
                <h1 className="text-3xl font-bold tracking-tight">Accounts Receivable</h1>
                <p className="text-muted-foreground mt-2">
                    Overview of your receivables, collections, and customer performance.
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
                        <div className="text-2xl font-bold">{formatCurrency(dashboardData?.totalOutstanding || 0)}</div>
                        <p className="text-xs text-muted-foreground mt-1">
                            Current total receivables
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
                            {formatCurrency(dashboardData?.overdueAmount || 0)}
                        </div>
                        <p className="text-xs text-red-600/80 mt-1">
                            Requires immediate attention
                        </p>
                    </CardContent>
                </Card>

                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                        <CardTitle className="text-sm font-medium">Collected This Month</CardTitle>
                        <CheckCircle2 className="h-4 w-4 text-green-500" />
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold text-green-600">
                            {formatCurrency(dashboardData?.collectedThisMonth || 0)}
                        </div>
                        <p className="text-xs text-muted-foreground mt-1">
                            Inflows for current period
                        </p>
                    </CardContent>
                </Card>

                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                        <CardTitle className="text-sm font-medium">DSO (Days Sales Outstanding)</CardTitle>
                        <Clock className="h-4 w-4 text-muted-foreground" />
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold text-blue-600">
                            {dashboardData?.dso?.toFixed(0) || 0} days
                        </div>
                        <p className="text-xs text-muted-foreground mt-1">
                            Average collection period
                        </p>
                    </CardContent>
                </Card>
            </div>

            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-7">
                {/* Aging Chart */}
                <Card className="col-span-4">
                    <CardHeader>
                        <CardTitle>Aging Analysis</CardTitle>
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
                                        tickFormatter={(value) => `$${value}`}
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

                {/* Quick Stats / Recent Activity Placeholder */}
                <Card className="col-span-3">
                    <CardHeader>
                        <CardTitle>Portfolio Health</CardTitle>
                        <CardDescription>
                            Key indicators of credit portfolio performance
                        </CardDescription>
                    </CardHeader>
                    <CardContent>
                        <div className="space-y-8">
                            <div className="flex items-center">
                                <div className="flex items-center justify-center w-9 h-9 rounded-full bg-blue-100 dark:bg-blue-900/20">
                                    <Users className="h-5 w-5 text-blue-600" />
                                </div>
                                <div className="ml-4 space-y-1">
                                    <p className="text-sm font-medium leading-none">Active Customers</p>
                                    <p className="text-sm text-muted-foreground">
                                        {arSummary?.activeCustomersCount || 0} customers with balance
                                    </p>
                                </div>
                                <div className="ml-auto font-medium">
                                    {arSummary?.activeCustomersCount || 0}
                                </div>
                            </div>

                            <div className="flex items-center">
                                <div className="flex items-center justify-center w-9 h-9 rounded-full bg-red-100 dark:bg-red-900/20">
                                    <AlertCircle className="h-5 w-5 text-red-600" />
                                </div>
                                <div className="ml-4 space-y-1">
                                    <p className="text-sm font-medium leading-none">Customers Over Limit</p>
                                    <p className="text-sm text-muted-foreground">
                                        Exceeding credit limit
                                    </p>
                                </div>
                                <div className="ml-auto font-medium text-red-600">
                                    {arSummary?.customersOverLimitCount || 0}
                                </div>
                            </div>

                            <div className="flex items-center">
                                <div className="flex items-center justify-center w-9 h-9 rounded-full bg-green-100 dark:bg-green-900/20">
                                    <Wallet className="h-5 w-5 text-green-600" />
                                </div>
                                <div className="ml-4 space-y-1">
                                    <p className="text-sm font-medium leading-none">Collection Efficiency</p>
                                    <p className="text-sm text-muted-foreground">
                                        Payments vs Due
                                    </p>
                                </div>
                                <div className="ml-auto font-medium text-green-600">
                                    {arSummary?.collectionEfficiency?.toFixed(1) || 0}%
                                </div>
                            </div>
                        </div>

                        <div className="mt-8">
                            <h4 className="mb-4 text-sm font-medium leading-none">Distribution by Status</h4>
                            {/* Simple distribution bars could go here */}
                            <div className="space-y-4">
                                <div className="space-y-1">
                                    <div className="flex items-center justify-between text-sm">
                                        <span className="text-muted-foreground">Current</span>
                                        <span>{((agingReport?.buckets[0]?.amount || 0) / (dashboardData?.totalOutstanding || 1) * 100).toFixed(0)}%</span>
                                    </div>
                                    <div className="h-2 w-full rounded-full bg-secondary">
                                        <div
                                            className="h-full rounded-full bg-green-500"
                                            style={{ width: `${((agingReport?.buckets[0]?.amount || 0) / (dashboardData?.totalOutstanding || 1) * 100)}%` }}
                                        />
                                    </div>
                                </div>
                                <div className="space-y-1">
                                    <div className="flex items-center justify-between text-sm">
                                        <span className="text-muted-foreground">1-30 Days</span>
                                        <span>{((agingReport?.buckets[1]?.amount || 0) / (dashboardData?.totalOutstanding || 1) * 100).toFixed(0)}%</span>
                                    </div>
                                    <div className="h-2 w-full rounded-full bg-secondary">
                                        <div
                                            className="h-full rounded-full bg-blue-500"
                                            style={{ width: `${((agingReport?.buckets[1]?.amount || 0) / (dashboardData?.totalOutstanding || 1) * 100)}%` }}
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
