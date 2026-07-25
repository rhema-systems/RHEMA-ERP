'use client';

import React from 'react';
import Link from 'next/link';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import {
    FileText,
    TrendingUp,
    Scale,
    Banknote,
    Globe,
    ListTree,
    ArrowRight
} from 'lucide-react';

const REPORTS = [
    {
        id: 'trial-balance',
        title: 'Trial Balance',
        description: 'Statement of all ledger account balances with opening, period activity, and closing balances',
        href: '/finance/reports/trial-balance',
        icon: FileText,
        color: 'bg-blue-500',
    },
    {
        id: 'income-statement',
        title: 'Income Statement',
        description: 'Profit and Loss statement showing revenue, expenses, and net income for a period',
        href: '/finance/reports/income-statement',
        icon: TrendingUp,
        color: 'bg-green-500',
    },
    {
        id: 'balance-sheet',
        title: 'Balance Sheet',
        description: 'Statement of Financial Position showing assets, liabilities, and equity',
        href: '/finance/reports/balance-sheet',
        icon: Scale,
        color: 'bg-purple-500',
    },
    {
        id: 'cash-flow',
        title: 'Cash Flow Statement',
        description: 'Statement of cash flows from operating, investing, and financing activities',
        href: '/finance/reports/cash-flow',
        icon: Banknote,
        color: 'bg-amber-500',
    },
    {
        id: 'multi-currency',
        title: 'Multi-Currency Detail',
        description: 'Foreign currency balances with exchange rates and base currency equivalents',
        href: '/finance/reports/multi-currency',
        icon: Globe,
        color: 'bg-cyan-500',
    },
    {
        id: 'detailed-ledger',
        title: 'Detailed Ledger',
        description: 'Transaction listing for selected GL accounts and reporting date ranges',
        href: '/finance/reports/detailed-ledger',
        icon: ListTree,
        color: 'bg-slate-600',
    },
];

export default function FinanceReportsPage() {
    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div>
                <h1 className="text-3xl font-bold tracking-tight">Finance Reports</h1>
                <p className="text-muted-foreground">
                    Financial statements and analytical reports
                </p>
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
                        <BreadcrumbPage>Reports</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Reports Grid */}
            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
                {REPORTS.map((report) => {
                    const Icon = report.icon;
                    return (
                        <Link key={report.id} href={report.href}>
                            <Card className="h-full hover:shadow-lg transition-shadow cursor-pointer group">
                                <CardHeader className="flex flex-row items-start gap-4 pb-2">
                                    <div className={`p-3 rounded-lg ${report.color}`}>
                                        <Icon className="h-6 w-6 text-white" />
                                    </div>
                                    <div className="flex-1">
                                        <CardTitle className="text-lg group-hover:text-primary transition-colors flex items-center gap-2">
                                            {report.title}
                                            <ArrowRight className="h-4 w-4 opacity-0 -translate-x-2 group-hover:opacity-100 group-hover:translate-x-0 transition-all" />
                                        </CardTitle>
                                    </div>
                                </CardHeader>
                                <CardContent>
                                    <CardDescription className="text-sm">
                                        {report.description}
                                    </CardDescription>
                                </CardContent>
                            </Card>
                        </Link>
                    );
                })}
            </div>
        </div>
    );
}
