'use client';

import React from 'react';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { ArrowUpRight, ArrowDownRight, DollarSign, CreditCard, Wallet, TrendingUp, AlertCircle, CheckCircle2, Clock, ChevronRight } from 'lucide-react';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, PieChart, Pie, Cell, Legend } from 'recharts';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

// MOCK DATA
const KPI_DATA = {
    revenue: { amount: 1250000, change: 12.5, trend: 'up' },
    expenses: { amount: 850000, change: -2.4, trend: 'down' },
    netProfit: { amount: 400000, change: 8.2, trend: 'up' },
    cashOnHand: { amount: 150000, change: 5.1, trend: 'up' },
};

const REVENUE_EXPENSE_DATA = [
    { name: 'Jan', revenue: 150000, expenses: 120000 },
    { name: 'Feb', revenue: 180000, expenses: 130000 },
    { name: 'Mar', revenue: 160000, expenses: 125000 },
    { name: 'Apr', revenue: 210000, expenses: 140000 },
    { name: 'May', revenue: 190000, expenses: 135000 },
    { name: 'Jun', revenue: 240000, expenses: 150000 },
];

const EXPENSE_BREAKDOWN_DATA = [
    { name: 'Salaries', value: 450000 },
    { name: 'Rent', value: 120000 },
    { name: 'Utilities', value: 80000 },
    { name: 'Marketing', value: 150000 },
    { name: 'Other', value: 50000 },
];

const COLORS = ['#0088FE', '#00C49F', '#FFBB28', '#FF8042', '#8884d8'];

const RECENT_TRANSACTIONS = [
    { id: '1', date: '2024-06-15', description: 'Consulting Revenue', amount: 15000, type: 'Credit', status: 'Posted' },
    { id: '2', date: '2024-06-14', description: 'Office Rent Payment', amount: -5000, type: 'Debit', status: 'Posted' },
    { id: '3', date: '2024-06-12', description: 'Utility Bill', amount: -1200, type: 'Debit', status: 'Posted' },
    { id: '4', date: '2024-06-10', description: 'Client Payment', amount: 8500, type: 'Credit', status: 'Posted' },
    { id: '5', date: '2024-06-08', description: 'Software Subscription', amount: -450, type: 'Debit', status: 'Posted' },
];

const PENDING_APPROVALS = [
    { id: '101', date: '2024-06-16', description: 'Travel Reimbursement', amount: 1250, requester: 'John Doe' },
    { id: '102', date: '2024-06-16', description: 'Equipment Purchase', amount: 15000, requester: 'Jane Smith' },
    { id: '103', date: '2024-06-15', description: 'Vendor Payment', amount: 4500, requester: 'Mike Johnson' },
];

export default function FinanceDashboardPage() {
    const router = useRouter();

    const formatMoney = (amount: number) => {
        return new Intl.NumberFormat('en-GH', {
            style: 'currency',
            currency: 'GHS',
            minimumFractionDigits: 0,
            maximumFractionDigits: 0,
        }).format(amount);
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Finance Dashboard</h1>
                    <p className="text-muted-foreground">Overview of financial performance and activities</p>
                </div>
                <div className="flex gap-2">
                    <Button asChild>
                        <Link href="/finance/journal-entries/new">
                            <TrendingUp className="mr-2 h-4 w-4" />
                            New Entry
                        </Link>
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
                        <BreadcrumbPage>Finance</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* KPI Cards */}
            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
                <Link href="/finance/reports/income-statement" className="block transition-transform hover:scale-[1.02]">
                    <Card className="cursor-pointer hover:border-blue-500/50">
                        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                            <CardTitle className="text-sm font-medium">Total Revenue (YTD)</CardTitle>
                            <DollarSign className="h-4 w-4 text-muted-foreground" />
                        </CardHeader>
                        <CardContent>
                            <div className="text-2xl font-bold">{formatMoney(KPI_DATA.revenue.amount)}</div>
                            <p className="text-xs text-muted-foreground flex items-center mt-1">
                                <ArrowUpRight className="h-4 w-4 text-green-500 mr-1" />
                                <span className="text-green-500 font-medium">+{KPI_DATA.revenue.change}%</span>
                                <span className="ml-1">from last year</span>
                            </p>
                        </CardContent>
                    </Card>
                </Link>
                <Link href="/finance/reports/income-statement" className="block transition-transform hover:scale-[1.02]">
                    <Card className="cursor-pointer hover:border-red-500/50">
                        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                            <CardTitle className="text-sm font-medium">Total Expenses (YTD)</CardTitle>
                            <CreditCard className="h-4 w-4 text-muted-foreground" />
                        </CardHeader>
                        <CardContent>
                            <div className="text-2xl font-bold">{formatMoney(KPI_DATA.expenses.amount)}</div>
                            <p className="text-xs text-muted-foreground flex items-center mt-1">
                                <ArrowDownRight className="h-4 w-4 text-green-500 mr-1" />
                                <span className="text-green-500 font-medium">{KPI_DATA.expenses.change}%</span>
                                <span className="ml-1">from last year</span>
                            </p>
                        </CardContent>
                    </Card>
                </Link>
                <Link href="/finance/reports/income-statement" className="block transition-transform hover:scale-[1.02]">
                    <Card className="cursor-pointer hover:border-green-500/50">
                        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                            <CardTitle className="text-sm font-medium">Net Profit Margin</CardTitle>
                            <TrendingUp className="h-4 w-4 text-muted-foreground" />
                        </CardHeader>
                        <CardContent>
                            <div className="text-2xl font-bold">{formatMoney(KPI_DATA.netProfit.amount)}</div>
                            <p className="text-xs text-muted-foreground flex items-center mt-1">
                                <ArrowUpRight className="h-4 w-4 text-green-500 mr-1" />
                                <span className="text-green-500 font-medium">+{KPI_DATA.netProfit.change}%</span>
                                <span className="ml-1">from last year</span>
                            </p>
                        </CardContent>
                    </Card>
                </Link>
                <Link href="/finance/reports/cash-flow" className="block transition-transform hover:scale-[1.02]">
                    <Card className="cursor-pointer hover:border-purple-500/50">
                        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                            <CardTitle className="text-sm font-medium">Cash on Hand</CardTitle>
                            <Wallet className="h-4 w-4 text-muted-foreground" />
                        </CardHeader>
                        <CardContent>
                            <div className="text-2xl font-bold">{formatMoney(KPI_DATA.cashOnHand.amount)}</div>
                            <p className="text-xs text-muted-foreground flex items-center mt-1">
                                <ArrowUpRight className="h-4 w-4 text-green-500 mr-1" />
                                <span className="text-green-500 font-medium">+{KPI_DATA.cashOnHand.change}%</span>
                                <span className="ml-1">from last month</span>
                            </p>
                        </CardContent>
                    </Card>
                </Link>
            </div>

            {/* Charts Section */}
            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-7">
                <Card className="col-span-4">
                    <CardHeader className="flex flex-row items-center justify-between">
                        <div>
                            <CardTitle>Revenue vs Expenses</CardTitle>
                            <CardDescription>Monthly financial performance for the current year</CardDescription>
                        </div>
                        <Button variant="ghost" size="sm" asChild>
                            <Link href="/finance/reports/income-statement">
                                View Report <ChevronRight className="ml-1 h-4 w-4" />
                            </Link>
                        </Button>
                    </CardHeader>
                    <CardContent className="pl-2">
                        <div className="h-[300px]">
                            <ResponsiveContainer width="100%" height="100%">
                                <BarChart data={REVENUE_EXPENSE_DATA}>
                                    <CartesianGrid strokeDasharray="3 3" vertical={false} />
                                    <XAxis dataKey="name" axisLine={false} tickLine={false} />
                                    <YAxis axisLine={false} tickLine={false} tickFormatter={(value) => `₵${value / 1000}k`} />
                                    <Tooltip
                                        formatter={(value: number) => formatMoney(value)}
                                        contentStyle={{ borderRadius: '8px', border: 'none', boxShadow: '0 4px 6px -1px rgb(0 0 0 / 0.1)' }}
                                    />
                                    <Legend />
                                    <Bar dataKey="revenue" name="Revenue" fill="#0ea5e9" radius={[4, 4, 0, 0]} />
                                    <Bar dataKey="expenses" name="Expenses" fill="#f43f5e" radius={[4, 4, 0, 0]} />
                                </BarChart>
                            </ResponsiveContainer>
                        </div>
                    </CardContent>
                </Card>
                <Card className="col-span-3">
                    <CardHeader className="flex flex-row items-center justify-between">
                        <div>
                            <CardTitle>Expense Breakdown</CardTitle>
                            <CardDescription>Distribution of expenses by category</CardDescription>
                        </div>
                        <Button variant="ghost" size="sm" asChild>
                            <Link href="/finance/reports/income-statement">
                                Details <ChevronRight className="ml-1 h-4 w-4" />
                            </Link>
                        </Button>
                    </CardHeader>
                    <CardContent>
                        <div className="h-[300px]">
                            <ResponsiveContainer width="100%" height="100%">
                                <PieChart>
                                    <Pie
                                        data={EXPENSE_BREAKDOWN_DATA}
                                        cx="50%"
                                        cy="50%"
                                        innerRadius={60}
                                        outerRadius={80}
                                        paddingAngle={5}
                                        dataKey="value"
                                    >
                                        {EXPENSE_BREAKDOWN_DATA.map((entry, index) => (
                                            <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                                        ))}
                                    </Pie>
                                    <Tooltip formatter={(value: number) => formatMoney(value)} />
                                    <Legend />
                                </PieChart>
                            </ResponsiveContainer>
                        </div>
                    </CardContent>
                </Card>
            </div>

            {/* Recent Activity & Approvals */}
            <div className="grid gap-4 md:grid-cols-2">
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between">
                        <div>
                            <CardTitle>Recent Transactions</CardTitle>
                            <CardDescription>Latest posted journal entries</CardDescription>
                        </div>
                        <Button variant="ghost" size="sm" asChild>
                            <Link href="/finance/journal-entries">
                                View All <ChevronRight className="ml-1 h-4 w-4" />
                            </Link>
                        </Button>
                    </CardHeader>
                    <CardContent>
                        <div className="space-y-4">
                            {RECENT_TRANSACTIONS.map((transaction) => (
                                <div
                                    key={transaction.id}
                                    className="flex items-center justify-between border-b pb-4 last:border-0 last:pb-0 cursor-pointer hover:bg-muted/50 p-2 rounded-md transition-colors"
                                    onClick={() => router.push(`/finance/journal-entries/${transaction.id}`)}
                                >
                                    <div className="flex items-center space-x-4">
                                        <div className={`p-2 rounded-full ${transaction.amount > 0 ? 'bg-green-100 text-green-600' : 'bg-red-100 text-red-600'}`}>
                                            {transaction.amount > 0 ? <ArrowUpRight className="h-4 w-4" /> : <ArrowDownRight className="h-4 w-4" />}
                                        </div>
                                        <div>
                                            <p className="text-sm font-medium leading-none">{transaction.description}</p>
                                            <p className="text-xs text-muted-foreground">{transaction.date}</p>
                                        </div>
                                    </div>
                                    <div className={`font-medium ${transaction.amount > 0 ? 'text-green-600' : 'text-red-600'}`}>
                                        {transaction.amount > 0 ? '+' : ''}{formatMoney(transaction.amount)}
                                    </div>
                                </div>
                            ))}
                        </div>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between">
                        <div>
                            <CardTitle>Pending Approvals</CardTitle>
                            <CardDescription>Draft entries requiring review</CardDescription>
                        </div>
                        <Button variant="ghost" size="sm" asChild>
                            <Link href="/finance/journal-entries?status=Draft">
                                View All <ChevronRight className="ml-1 h-4 w-4" />
                            </Link>
                        </Button>
                    </CardHeader>
                    <CardContent>
                        <div className="space-y-4">
                            {PENDING_APPROVALS.map((approval) => (
                                <div
                                    key={approval.id}
                                    className="flex items-center justify-between border-b pb-4 last:border-0 last:pb-0 cursor-pointer hover:bg-muted/50 p-2 rounded-md transition-colors"
                                    onClick={() => router.push(`/finance/journal-entries/${approval.id}/edit`)}
                                >
                                    <div className="flex items-center space-x-4">
                                        <div className="p-2 rounded-full bg-orange-100 text-orange-600">
                                            <Clock className="h-4 w-4" />
                                        </div>
                                        <div>
                                            <p className="text-sm font-medium leading-none">{approval.description}</p>
                                            <p className="text-xs text-muted-foreground">Req: {approval.requester} • {approval.date}</p>
                                        </div>
                                    </div>
                                    <div className="flex items-center gap-2">
                                        <div className="font-medium">{formatMoney(approval.amount)}</div>
                                        <Button size="sm" variant="outline" className="h-8 w-8 p-0" onClick={(e) => {
                                            e.stopPropagation();
                                            // Handle quick approve logic here
                                        }}>
                                            <CheckCircle2 className="h-4 w-4 text-green-600" />
                                        </Button>
                                    </div>
                                </div>
                            ))}
                            {PENDING_APPROVALS.length === 0 && (
                                <div className="text-center py-4 text-muted-foreground">
                                    No pending approvals
                                </div>
                            )}
                        </div>
                    </CardContent>
                </Card>
            </div>
        </div>
    );
}
