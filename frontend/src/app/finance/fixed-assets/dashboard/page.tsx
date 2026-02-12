'use client';

import {
    Building2,
    TrendingUp,
    AlertCircle,
    PieChart,
    DollarSign,
    Calendar,
    Package,
    Activity
} from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export default function FixedAssetsDashboard() {
    const stats = [
        { title: 'Total Assets', value: '1,284', icon: Package, color: 'text-blue-600', bg: 'bg-blue-100' },
        { title: 'Net Book Value', value: 'GH₵ 4,250,000', icon: DollarSign, color: 'text-green-600', bg: 'bg-green-100' },
        { title: 'Depreciation (YTD)', value: 'GH₵ 450,000', icon: TrendingUp, color: 'text-amber-600', bg: 'bg-amber-100' },
        { title: 'Pending Verification', value: '12', icon: AlertCircle, color: 'text-red-600', bg: 'bg-red-100' },
    ];

    return (
        <div className="p-6 space-y-6">
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-2xl font-bold text-slate-900 dark:text-white">Fixed Assets Dashboard</h1>
                    <p className="text-slate-500 dark:text-slate-400">Overview of organization's fixed assets and financial health.</p>
                </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
                {stats.map((stat, index) => (
                    <Card key={index}>
                        <CardContent className="p-6">
                            <div className="flex items-center justify-between">
                                <div>
                                    <p className="text-sm font-medium text-slate-500 dark:text-slate-400">{stat.title}</p>
                                    <h3 className="text-2xl font-bold mt-1 text-slate-900 dark:text-white">{stat.value}</h3>
                                </div>
                                <div className={`${stat.bg} p-3 rounded-xl`}>
                                    <stat.icon className={`h-6 w-6 ${stat.color}`} />
                                </div>
                            </div>
                        </CardContent>
                    </Card>
                ))}
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                <Card>
                    <CardHeader>
                        <CardTitle>Asset Distribution by Category</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="h-[300px] flex items-center justify-center border-2 border-dashed rounded-lg bg-slate-50 dark:bg-slate-800/50">
                            <div className="text-center">
                                <PieChart className="h-12 w-12 text-slate-300 mx-auto mb-2" />
                                <p className="text-slate-500">Charts Coming Soon</p>
                            </div>
                        </div>
                    </CardContent>
                </Card>

                <Card>
                    <CardHeader>
                        <CardTitle>Recent Asset Activity</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="space-y-4">
                            {[1, 2, 3, 4].map((i) => (
                                <div key={i} className="flex items-center justify-between p-3 rounded-lg hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors">
                                    <div className="flex items-center gap-3">
                                        <div className="h-10 w-10 rounded-full bg-blue-50 dark:bg-blue-900/20 flex items-center justify-center">
                                            <Activity className="h-5 w-5 text-blue-600" />
                                        </div>
                                        <div>
                                            <p className="text-sm font-medium text-slate-900 dark:text-white">Asset Acquisition</p>
                                            <p className="text-xs text-slate-500">Toyota Hilux - FA00{i}</p>
                                        </div>
                                    </div>
                                    <div className="text-right">
                                        <p className="text-sm font-medium text-slate-900 dark:text-white">Completed</p>
                                        <p className="text-xs text-slate-500">2 hours ago</p>
                                    </div>
                                </div>
                            ))}
                        </div>
                    </CardContent>
                </Card>
            </div>
        </div>
    );
}
