'use client';

import React, { useEffect, useMemo, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { ArrowUpRight, ArrowDownRight, DollarSign, CreditCard, Wallet, TrendingUp, CheckCircle2, Clock, ChevronRight, Loader2 } from 'lucide-react';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, PieChart, Pie, Cell, Legend } from 'recharts';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { Account, JournalEntry } from '@/types/finance';
import { useToast } from '@/hooks/use-toast';

const COLORS = ['#0088FE', '#00C49F', '#FFBB28', '#FF8042', '#8884d8'];
const MONTH_LABELS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

function getEntryDate(entry: JournalEntry): Date {
    return new Date(entry.postingDate || entry.postedDate || entry.entryDate || entry.transactionDate || entry.createdAt);
}

function getAccountType(account?: Account): string {
    return account?.accountType || '';
}

export default function FinanceDashboardPage() {
    const router = useRouter();
    const { toast } = useToast();
    const [entries, setEntries] = useState<JournalEntry[]>([]);
    const [pendingApprovals, setPendingApprovals] = useState<JournalEntry[]>([]);
    const [accounts, setAccounts] = useState<Account[]>([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        let cancelled = false;

        async function loadDashboardData() {
            try {
                setLoading(true);
                const [journalEntries, chartAccounts, approvalEntries] = await Promise.all([
                    financeDataService.getJournalEntries(),
                    financeDataService.getAccounts(),
                    financeDataService.getPendingJournalApprovals().catch(() => []),
                ]);

                if (!cancelled) {
                    setEntries(journalEntries || []);
                    setAccounts(chartAccounts || []);
                    setPendingApprovals(approvalEntries || []);
                }
            } catch (err: any) {
                if (!cancelled) {
                    toast({
                        title: 'Error',
                        description: err?.message || 'Failed to load finance dashboard data',
                        variant: 'destructive',
                    });
                }
            } finally {
                if (!cancelled) setLoading(false);
            }
        }

        loadDashboardData();
        return () => {
            cancelled = true;
        };
    }, [toast]);

    const formatMoney = (amount: number) => {
        return new Intl.NumberFormat('en-GH', {
            style: 'currency',
            currency: 'GHS',
            minimumFractionDigits: 0,
            maximumFractionDigits: 0,
        }).format(amount || 0);
    };

    const dashboardData = useMemo(() => {
        const accountsById = new Map(accounts.map(account => [account.id, account]));
        const currentYear = new Date().getFullYear();
        const postedEntries = entries.filter(entry => entry.postingStatus === 'Posted');
        const ytdEntries = postedEntries.filter(entry => getEntryDate(entry).getFullYear() === currentYear);

        let revenue = 0;
        let expenses = 0;
        let cashOnHand = 0;
        const monthly = MONTH_LABELS.map(name => ({ name, revenue: 0, expenses: 0 }));
        const expenseBreakdown = new Map<string, number>();

        for (const entry of ytdEntries) {
            const month = getEntryDate(entry).getMonth();

            for (const line of entry.transactions) {
                const account = accountsById.get(line.accountId);
                const accountType = getAccountType(account);
                const debit = line.debitAmount || 0;
                const credit = line.creditAmount || 0;

                if (accountType === 'Revenue') {
                    const amount = credit - debit;
                    revenue += amount;
                    monthly[month].revenue += amount;
                }

                if (accountType === 'Expense') {
                    const amount = debit - credit;
                    expenses += amount;
                    monthly[month].expenses += amount;
                    const defaultMapping = account?.accountingBooks?.find(mapping => mapping.isEnabled && mapping.accountingBookIsDefault)
                        ?? account?.accountingBooks?.find(mapping => mapping.isEnabled);
                    const groupName = defaultMapping?.accountClassificationName || account?.accountName || 'Unclassified expenses';
                    expenseBreakdown.set(groupName, (expenseBreakdown.get(groupName) || 0) + amount);
                }
            }
        }

        for (const entry of postedEntries) {
            for (const line of entry.transactions) {
                const account = accountsById.get(line.accountId);
                const isCash = account?.accountingBooks?.some(mapping => mapping.isEnabled
                    && (mapping.accountClassificationSystemRole === 'Cash'
                        || mapping.accountClassificationSystemRole === 'Bank'));
                if (getAccountType(account) === 'Asset' && isCash) {
                    cashOnHand += (line.debitAmount || 0) - (line.creditAmount || 0);
                }
            }
        }

        const expenseChart = Array.from(expenseBreakdown.entries())
            .map(([name, value]) => ({ name, value }))
            .filter(item => item.value > 0)
            .sort((a, b) => b.value - a.value)
            .slice(0, 5);

        return {
            kpis: {
                revenue,
                expenses,
                netProfit: revenue - expenses,
                cashOnHand,
            },
            monthly,
            expenseChart,
            recentTransactions: postedEntries
                .slice()
                .sort((a, b) => getEntryDate(b).getTime() - getEntryDate(a).getTime())
                .slice(0, 5)
                .map(entry => ({
                    id: entry.id,
                    date: getEntryDate(entry).toLocaleDateString('en-GB'),
                    description: entry.description || entry.journalEntryNumber,
                    amount: entry.totalDebitAmount || entry.totalDebit || 0,
                    journalNumber: entry.journalEntryNumber,
                })),
            pendingApprovals: pendingApprovals
                .slice()
                .sort((a, b) => getEntryDate(b).getTime() - getEntryDate(a).getTime())
                .slice(0, 5),
        };
    }, [accounts, entries, pendingApprovals]);

    return (
        <div className="space-y-6">
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Finance Dashboard</h1>
                    <p className="text-muted-foreground">Live overview from posted journals, accounts, and approval queues</p>
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

            {loading ? (
                <div className="flex min-h-[240px] items-center justify-center rounded-lg border bg-card">
                    <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
                </div>
            ) : (
                <>
                    <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
                        <Link href="/finance/reports/income-statement" className="block transition-transform hover:scale-[1.02]">
                            <Card className="cursor-pointer hover:border-blue-500/50">
                                <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                                    <CardTitle className="text-sm font-medium">Total Revenue (YTD)</CardTitle>
                                    <DollarSign className="h-4 w-4 text-muted-foreground" />
                                </CardHeader>
                                <CardContent>
                                    <div className="text-2xl font-bold">{formatMoney(dashboardData.kpis.revenue)}</div>
                                    <p className="text-xs text-muted-foreground flex items-center mt-1">
                                        <ArrowUpRight className="h-4 w-4 text-green-500 mr-1" />
                                        Posted revenue journals this year
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
                                    <div className="text-2xl font-bold">{formatMoney(dashboardData.kpis.expenses)}</div>
                                    <p className="text-xs text-muted-foreground flex items-center mt-1">
                                        <ArrowDownRight className="h-4 w-4 text-red-500 mr-1" />
                                        Posted expense journals this year
                                    </p>
                                </CardContent>
                            </Card>
                        </Link>
                        <Link href="/finance/reports/income-statement" className="block transition-transform hover:scale-[1.02]">
                            <Card className="cursor-pointer hover:border-green-500/50">
                                <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                                    <CardTitle className="text-sm font-medium">Net Profit</CardTitle>
                                    <TrendingUp className="h-4 w-4 text-muted-foreground" />
                                </CardHeader>
                                <CardContent>
                                    <div className="text-2xl font-bold">{formatMoney(dashboardData.kpis.netProfit)}</div>
                                    <p className="text-xs text-muted-foreground flex items-center mt-1">
                                        <ArrowUpRight className="h-4 w-4 text-green-500 mr-1" />
                                        Revenue less expenses
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
                                    <div className="text-2xl font-bold">{formatMoney(dashboardData.kpis.cashOnHand)}</div>
                                    <p className="text-xs text-muted-foreground flex items-center mt-1">
                                        <ArrowUpRight className="h-4 w-4 text-green-500 mr-1" />
                                        Posted cash and bank activity
                                    </p>
                                </CardContent>
                            </Card>
                        </Link>
                    </div>

                    <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-7">
                        <Card className="col-span-4">
                            <CardHeader className="flex flex-row items-center justify-between">
                                <div>
                                    <CardTitle>Revenue vs Expenses</CardTitle>
                                    <CardDescription>Monthly posted financial performance for the current year</CardDescription>
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
                                        <BarChart data={dashboardData.monthly}>
                                            <CartesianGrid strokeDasharray="3 3" vertical={false} />
                                            <XAxis dataKey="name" axisLine={false} tickLine={false} />
                                            <YAxis axisLine={false} tickLine={false} tickFormatter={(value) => `GHS ${Number(value) / 1000}k`} />
                                            <Tooltip formatter={(value: number) => formatMoney(value)} />
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
                                    <CardDescription>Distribution of posted expenses by account category</CardDescription>
                                </div>
                                <Button variant="ghost" size="sm" asChild>
                                    <Link href="/finance/reports/income-statement">
                                        Details <ChevronRight className="ml-1 h-4 w-4" />
                                    </Link>
                                </Button>
                            </CardHeader>
                            <CardContent>
                                <div className="h-[300px]">
                                    {dashboardData.expenseChart.length === 0 ? (
                                        <div className="flex h-full items-center justify-center text-sm text-muted-foreground">No posted expenses yet</div>
                                    ) : (
                                        <ResponsiveContainer width="100%" height="100%">
                                            <PieChart>
                                                <Pie data={dashboardData.expenseChart} cx="50%" cy="50%" innerRadius={60} outerRadius={80} paddingAngle={5} dataKey="value">
                                                    {dashboardData.expenseChart.map((entry, index) => (
                                                        <Cell key={`cell-${entry.name}`} fill={COLORS[index % COLORS.length]} />
                                                    ))}
                                                </Pie>
                                                <Tooltip formatter={(value: number) => formatMoney(value)} />
                                                <Legend />
                                            </PieChart>
                                        </ResponsiveContainer>
                                    )}
                                </div>
                            </CardContent>
                        </Card>
                    </div>

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
                                    {dashboardData.recentTransactions.map(transaction => (
                                        <div
                                            key={transaction.id}
                                            className="flex items-center justify-between border-b pb-4 last:border-0 last:pb-0 cursor-pointer hover:bg-muted/50 p-2 rounded-md transition-colors"
                                            onClick={() => router.push(`/finance/journal-entries/${transaction.id}`)}
                                        >
                                            <div className="flex items-center space-x-4">
                                                <div className={`p-2 rounded-full ${transaction.amount >= 0 ? 'bg-green-100 text-green-600' : 'bg-red-100 text-red-600'}`}>
                                                    {transaction.amount >= 0 ? <ArrowUpRight className="h-4 w-4" /> : <ArrowDownRight className="h-4 w-4" />}
                                                </div>
                                                <div>
                                                    <p className="text-sm font-medium leading-none">{transaction.description}</p>
                                                    <p className="text-xs text-muted-foreground">{transaction.journalNumber} - {transaction.date}</p>
                                                </div>
                                            </div>
                                            <div className={`font-medium ${transaction.amount >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                                                {transaction.amount >= 0 ? '+' : ''}{formatMoney(transaction.amount)}
                                            </div>
                                        </div>
                                    ))}
                                    {dashboardData.recentTransactions.length === 0 && (
                                        <div className="text-center py-4 text-muted-foreground">No posted journal entries yet</div>
                                    )}
                                </div>
                            </CardContent>
                        </Card>
                        <Card>
                            <CardHeader className="flex flex-row items-center justify-between">
                                <div>
                                    <CardTitle>Pending Approvals</CardTitle>
                                    <CardDescription>Journal entries awaiting approval</CardDescription>
                                </div>
                                <Button variant="ghost" size="sm" asChild>
                                    <Link href="/finance/approvals">
                                        View All <ChevronRight className="ml-1 h-4 w-4" />
                                    </Link>
                                </Button>
                            </CardHeader>
                            <CardContent>
                                <div className="space-y-4">
                                    {dashboardData.pendingApprovals.map(approval => (
                                        <div
                                            key={approval.id}
                                            className="flex items-center justify-between border-b pb-4 last:border-0 last:pb-0 cursor-pointer hover:bg-muted/50 p-2 rounded-md transition-colors"
                                            onClick={() => router.push(`/finance/journal-entries/${approval.id}`)}
                                        >
                                            <div className="flex items-center space-x-4">
                                                <div className="p-2 rounded-full bg-orange-100 text-orange-600">
                                                    <Clock className="h-4 w-4" />
                                                </div>
                                                <div>
                                                    <p className="text-sm font-medium leading-none">{approval.description || approval.journalEntryNumber}</p>
                                                    <p className="text-xs text-muted-foreground">{approval.journalEntryNumber} - {getEntryDate(approval).toLocaleDateString('en-GB')}</p>
                                                </div>
                                            </div>
                                            <div className="flex items-center gap-2">
                                                <div className="font-medium">{formatMoney(approval.totalDebitAmount)}</div>
                                                <CheckCircle2 className="h-4 w-4 text-muted-foreground" />
                                            </div>
                                        </div>
                                    ))}
                                    {dashboardData.pendingApprovals.length === 0 && (
                                        <div className="text-center py-4 text-muted-foreground">No pending approvals</div>
                                    )}
                                </div>
                            </CardContent>
                        </Card>
                    </div>
                </>
            )}
        </div>
    );
}
