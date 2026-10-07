'use client';

import Link from 'next/link';
import {
    ArrowRight,
    Banknote,
    TrendingUp,
    Landmark,
} from 'lucide-react';
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
} from '@/components/ui/card';

const CASH_REPORTS = [
    {
        id: 'cash-position',
        title: 'Cash Position',
        description: 'Cash availability across bank accounts, currencies, and account types.',
        href: '/finance/cash/reports/cash-position',
        icon: Banknote,
        color: 'bg-emerald-600',
    },
    {
        id: 'bank-reconciliation',
        title: 'Bank Reconciliation',
        description: 'Printable bank-to-GL reconciliation statement with outstanding and unmatched items.',
        href: '/finance/reports/bank-reconciliation',
        icon: Landmark,
        color: 'bg-teal-600',
    },
    {
        id: 'cash-flow-statement',
        title: 'Cash Flow Statement',
        description: 'Operating, investing, and financing cash flow statement for a reporting period.',
        href: '/finance/reports/cash-flow',
        icon: TrendingUp,
        color: 'bg-amber-600',
    },
];

export default function CashReportsPage() {
    return (
        <div className="space-y-6 p-8 max-w-[1600px] mx-auto">
            <div>
                <h1 className="text-3xl font-bold tracking-tight">Cash Reports</h1>
                <p className="text-muted-foreground">Cash management and treasury reporting</p>
            </div>

            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
                {CASH_REPORTS.map((report) => {
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
