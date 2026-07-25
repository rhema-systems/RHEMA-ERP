'use client';

import React, { useEffect, useMemo, useState } from 'react';
import {
    TrendingUp,
    AlertCircle,
    DollarSign,
    Package,
    Activity,
    ArrowUpRight,
    ArrowDownRight,
    Loader2,
    RefreshCw,
    Repeat,
    Trash2,
    ClipboardCheck,
    CheckCircle,
    XCircle,
} from 'lucide-react';
import Link from 'next/link';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import type {
    FixedAsset,
    FixedAssetCategory,
    AssetTransfer,
    AssetDisposal,
    AssetVerificationSession,
} from '@/types/fixed-assets';

// ——— Tiny Donut Chart (SVG, no dependencies) ———
function DonutChart({ data }: { data: { label: string; value: number; color: string }[] }) {
    const total = data.reduce((s, d) => s + d.value, 0);
    if (total === 0) {
        return (
            <div className="h-[280px] flex items-center justify-center text-muted-foreground text-sm">
                No assets to display
            </div>
        );
    }

    const radius = 80;
    const cx = 120;
    const cy = 120;
    const strokeWidth = 36;
    const circumference = 2 * Math.PI * radius;

    let cumulativePercent = 0;

    return (
        <div className="flex flex-col items-center gap-4">
            <svg width={240} height={240} viewBox="0 0 240 240">
                {data.filter(d => d.value > 0).map((slice, i) => {
                    const percent = slice.value / total;
                    const dashArray = `${circumference * percent} ${circumference * (1 - percent)}`;
                    const dashOffset = -circumference * cumulativePercent;
                    cumulativePercent += percent;
                    return (
                        <circle
                            key={i}
                            cx={cx}
                            cy={cy}
                            r={radius}
                            fill="none"
                            stroke={slice.color}
                            strokeWidth={strokeWidth}
                            strokeDasharray={dashArray}
                            strokeDashoffset={dashOffset}
                            transform={`rotate(-90 ${cx} ${cy})`}
                            className="transition-all duration-500"
                        />
                    );
                })}
                <text x={cx} y={cy - 8} textAnchor="middle" className="fill-slate-900 dark:fill-white font-bold text-2xl">{total}</text>
                <text x={cx} y={cy + 14} textAnchor="middle" className="fill-slate-500 text-xs">Total Assets</text>
            </svg>
            <div className="flex flex-wrap gap-x-4 gap-y-1 justify-center max-w-[280px]">
                {data.filter(d => d.value > 0).map((slice, i) => (
                    <div key={i} className="flex items-center gap-1.5 text-xs">
                        <span className="w-2.5 h-2.5 rounded-full inline-block" style={{ backgroundColor: slice.color }} />
                        <span className="text-slate-600 dark:text-slate-400">{slice.label}</span>
                        <span className="font-semibold text-slate-900 dark:text-white">{slice.value}</span>
                    </div>
                ))}
            </div>
        </div>
    );
}

// ——— Activity Item Types ———
interface ActivityItem {
    id: string;
    type: 'acquisition' | 'transfer' | 'disposal' | 'verification';
    title: string;
    subtitle: string;
    status: string;
    statusVariant: 'default' | 'secondary' | 'destructive' | 'outline';
    date: string;
    icon: React.ComponentType<{ className?: string }>;
    href: string;
}

const CATEGORY_COLORS = [
    '#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6',
    '#ec4899', '#14b8a6', '#f97316', '#6366f1', '#06b6d4',
    '#84cc16', '#d946ef',
];

export default function FixedAssetsDashboard() {
    const [assets, setAssets] = useState<FixedAsset[]>([]);
    const [categories, setCategories] = useState<FixedAssetCategory[]>([]);
    const [transfers, setTransfers] = useState<AssetTransfer[]>([]);
    const [disposals, setDisposals] = useState<AssetDisposal[]>([]);
    const [verifications, setVerifications] = useState<AssetVerificationSession[]>([]);
    const [loading, setLoading] = useState(true);

    const loadData = async () => {
        setLoading(true);
        try {
            const [a, c, t, d, v] = await Promise.all([
                fixedAssetsDataService.getAssets(),
                fixedAssetsDataService.getCategories(),
                fixedAssetsDataService.getTransfers(),
                fixedAssetsDataService.getDisposals(),
                fixedAssetsDataService.getVerificationSessions(),
            ]);
            setAssets(a || []);
            setCategories(c || []);
            setTransfers(t || []);
            setDisposals(d || []);
            setVerifications(v || []);
        } catch (err) {
            console.error('Dashboard load error:', err);
        }
        setLoading(false);
    };

    useEffect(() => { loadData(); }, []);

    // ——— Computed Stats ———
    const totalAssets = assets.length;
    const totalNBV = assets.reduce((s, a) => s + (a.netBookValue || 0), 0);
    const totalAcquisitionCost = assets.reduce((s, a) => s + (a.acquisitionCost || 0), 0);
    const totalAccumDepr = totalAcquisitionCost - totalNBV;

    const pendingVerification = verifications.filter(v =>
        v.status === 'Draft' || v.status === 'InProgress'
    ).reduce((s, v) => s + (v.totalItems - v.verifiedItems), 0);

    // ——— Chart Data (by category) ———
    const chartData = useMemo(() => {
        const catMap = new Map<string, { label: string; count: number }>();
        categories.forEach(c => catMap.set(c.id, { label: c.name, count: 0 }));
        assets.forEach(a => {
            const entry = catMap.get(a.fixedAssetCategoryId);
            if (entry) entry.count++;
            else catMap.set(a.fixedAssetCategoryId, { label: a.fixedAssetCategoryName || 'Unknown', count: 1 });
        });

        return Array.from(catMap.values())
            .filter(e => e.count > 0)
            .sort((a, b) => b.count - a.count)
            .map((e, i) => ({
                label: e.label,
                value: e.count,
                color: CATEGORY_COLORS[i % CATEGORY_COLORS.length],
            }));
    }, [assets, categories]);

    // ——— Recent Activity Feed (real data) ———
    const activityItems: ActivityItem[] = useMemo(() => {
        const items: ActivityItem[] = [];

        // Recent asset acquisitions (newest assets by createdAt)
        assets
            .slice()
            .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
            .slice(0, 5)
            .forEach(a => {
                items.push({
                    id: `acq-${a.id}`,
                    type: 'acquisition',
                    title: 'Asset Acquisition',
                    subtitle: `${a.name} — ${a.assetCode}`,
                    status: a.status,
                    statusVariant: a.status === 'Active' ? 'default' : 'secondary',
                    date: a.createdAt,
                    icon: ArrowUpRight,
                    href: `/finance/fixed-assets/register/${a.id}/edit`,
                });
            });

        // Recent transfers
        transfers
            .slice()
            .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
            .slice(0, 3)
            .forEach(t => {
                items.push({
                    id: `xfr-${t.id}`,
                    type: 'transfer',
                    title: 'Asset Transfer',
                    subtitle: `${t.fixedAssetName || t.assetCode} → ${t.toLocation}`,
                    status: t.status,
                    statusVariant: t.status === 'Approved' || t.status === 'Completed' ? 'default' : t.status === 'Rejected' ? 'destructive' : 'secondary',
                    date: t.createdAt,
                    icon: Repeat,
                    href: '/finance/fixed-assets/transfers',
                });
            });

        // Recent disposals
        disposals
            .slice()
            .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
            .slice(0, 3)
            .forEach(d => {
                items.push({
                    id: `dsp-${d.id}`,
                    type: 'disposal',
                    title: 'Asset Disposal',
                    subtitle: `${d.fixedAssetName || d.assetCode} (${d.disposalType})`,
                    status: d.status,
                    statusVariant: d.status === 'Completed' ? 'default' : d.status === 'Rejected' ? 'destructive' : 'secondary',
                    date: d.createdAt,
                    icon: Trash2,
                    href: '/finance/fixed-assets/disposals',
                });
            });

        // Sort all by date descending, take top 6
        return items.sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime()).slice(0, 6);
    }, [assets, transfers, disposals]);

    const formatMoney = (amount: number) =>
        new Intl.NumberFormat('en-GH', { style: 'currency', currency: 'GHS', minimumFractionDigits: 0, maximumFractionDigits: 0 }).format(amount);

    const formatRelativeTime = (dateStr: string) => {
        const diff = Date.now() - new Date(dateStr).getTime();
        const mins = Math.floor(diff / 60000);
        if (mins < 1) return 'Just now';
        if (mins < 60) return `${mins}m ago`;
        const hours = Math.floor(mins / 60);
        if (hours < 24) return `${hours}h ago`;
        const days = Math.floor(hours / 24);
        if (days < 30) return `${days}d ago`;
        return new Date(dateStr).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    };

    const getActivityIcon = (item: ActivityItem) => {
        const IconComponent = item.icon;
        const colorMap: Record<string, string> = {
            acquisition: 'text-blue-600 bg-blue-50 dark:bg-blue-900/20',
            transfer: 'text-amber-600 bg-amber-50 dark:bg-amber-900/20',
            disposal: 'text-red-600 bg-red-50 dark:bg-red-900/20',
            verification: 'text-emerald-600 bg-emerald-50 dark:bg-emerald-900/20',
        };
        const classes = colorMap[item.type] || 'text-slate-600 bg-slate-50';
        return (
            <div className={`h-10 w-10 rounded-full flex items-center justify-center ${classes}`}>
                <IconComponent className="h-5 w-5" />
            </div>
        );
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-[400px]">
                <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
        );
    }

    return (
        <div className="p-6 space-y-6">
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-2xl font-bold text-slate-900 dark:text-white">Fixed Assets Dashboard</h1>
                    <p className="text-slate-500 dark:text-slate-400">Overview of organization&apos;s fixed assets and financial health.</p>
                </div>
                <Button variant="outline" size="sm" onClick={loadData} className="gap-2">
                    <RefreshCw className="h-4 w-4" /> Refresh
                </Button>
            </div>

            {/* KPI Cards */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
                <Card>
                    <CardContent className="p-6">
                        <div className="flex items-center justify-between">
                            <div>
                                <p className="text-sm font-medium text-slate-500 dark:text-slate-400">Total Assets</p>
                                <h3 className="text-2xl font-bold mt-1 text-slate-900 dark:text-white">{totalAssets.toLocaleString()}</h3>
                            </div>
                            <div className="bg-blue-100 dark:bg-blue-900/30 p-3 rounded-xl">
                                <Package className="h-6 w-6 text-blue-600" />
                            </div>
                        </div>
                    </CardContent>
                </Card>
                <Card>
                    <CardContent className="p-6">
                        <div className="flex items-center justify-between">
                            <div>
                                <p className="text-sm font-medium text-slate-500 dark:text-slate-400">Net Book Value</p>
                                <h3 className="text-2xl font-bold mt-1 text-slate-900 dark:text-white">{formatMoney(totalNBV)}</h3>
                            </div>
                            <div className="bg-green-100 dark:bg-green-900/30 p-3 rounded-xl">
                                <DollarSign className="h-6 w-6 text-green-600" />
                            </div>
                        </div>
                    </CardContent>
                </Card>
                <Card>
                    <CardContent className="p-6">
                        <div className="flex items-center justify-between">
                            <div>
                                <p className="text-sm font-medium text-slate-500 dark:text-slate-400">Accumulated Depreciation</p>
                                <h3 className="text-2xl font-bold mt-1 text-slate-900 dark:text-white">{formatMoney(totalAccumDepr)}</h3>
                            </div>
                            <div className="bg-amber-100 dark:bg-amber-900/30 p-3 rounded-xl">
                                <TrendingUp className="h-6 w-6 text-amber-600" />
                            </div>
                        </div>
                    </CardContent>
                </Card>
                <Card>
                    <CardContent className="p-6">
                        <div className="flex items-center justify-between">
                            <div>
                                <p className="text-sm font-medium text-slate-500 dark:text-slate-400">Pending Verification</p>
                                <h3 className="text-2xl font-bold mt-1 text-slate-900 dark:text-white">{pendingVerification}</h3>
                            </div>
                            <div className="bg-red-100 dark:bg-red-900/30 p-3 rounded-xl">
                                <AlertCircle className="h-6 w-6 text-red-600" />
                            </div>
                        </div>
                    </CardContent>
                </Card>
            </div>

            {/* Chart + Activity */}
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                {/* Donut Chart */}
                <Card>
                    <CardHeader>
                        <CardTitle>Asset Distribution by Category</CardTitle>
                        <CardDescription>{categories.length} categories across {totalAssets} assets</CardDescription>
                    </CardHeader>
                    <CardContent>
                        <DonutChart data={chartData} />
                    </CardContent>
                </Card>

                {/* Recent Activity Feed */}
                <Card>
                    <CardHeader>
                        <CardTitle>Recent Asset Activity</CardTitle>
                        <CardDescription>Latest acquisitions, transfers, and disposals</CardDescription>
                    </CardHeader>
                    <CardContent>
                        {activityItems.length === 0 ? (
                            <div className="text-center py-12 text-muted-foreground">
                                <Activity className="h-10 w-10 mx-auto mb-2 opacity-20" />
                                <p>No recent activity</p>
                            </div>
                        ) : (
                            <div className="space-y-1">
                                {activityItems.map((item) => (
                                    <Link
                                        key={item.id}
                                        href={item.href}
                                        className="flex items-center justify-between p-3 rounded-lg hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors group"
                                    >
                                        <div className="flex items-center gap-3 min-w-0">
                                            {getActivityIcon(item)}
                                            <div className="min-w-0">
                                                <p className="text-sm font-medium text-slate-900 dark:text-white truncate">{item.title}</p>
                                                <p className="text-xs text-slate-500 truncate">{item.subtitle}</p>
                                            </div>
                                        </div>
                                        <div className="text-right flex-shrink-0 ml-3">
                                            <Badge variant={item.statusVariant} className="text-[10px] mb-0.5">{item.status}</Badge>
                                            <p className="text-xs text-slate-500">{formatRelativeTime(item.date)}</p>
                                        </div>
                                    </Link>
                                ))}
                            </div>
                        )}
                    </CardContent>
                </Card>
            </div>

            {/* Quick Stats Row */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-slate-500">Active Transfers</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="flex items-baseline gap-2">
                            <span className="text-3xl font-bold text-slate-900 dark:text-white">
                                {transfers.filter(t => t.status === 'PendingApproval').length}
                            </span>
                            <span className="text-sm text-slate-500">pending approval</span>
                        </div>
                        <p className="text-xs text-muted-foreground mt-1">{transfers.filter(t => t.status === 'Completed').length} completed total</p>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-slate-500">Active Disposals</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="flex items-baseline gap-2">
                            <span className="text-3xl font-bold text-slate-900 dark:text-white">
                                {disposals.filter(d => d.status === 'PendingApproval').length}
                            </span>
                            <span className="text-sm text-slate-500">pending approval</span>
                        </div>
                        <p className="text-xs text-muted-foreground mt-1">{disposals.filter(d => d.status === 'Completed').length} completed total</p>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-slate-500">Status Breakdown</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="flex flex-wrap gap-2">
                            {['Active', 'Draft', 'FullyDepreciated', 'Disposed', 'HeldForSale', 'UnderConstruction', 'OnHold'].map(status => {
                                const count = assets.filter(a => a.status === status).length;
                                if (count === 0) return null;
                                return (
                                    <Badge key={status} variant="outline" className="text-xs">
                                        {status.replace(/([A-Z])/g, ' $1').trim()} ({count})
                                    </Badge>
                                );
                            })}
                        </div>
                    </CardContent>
                </Card>
            </div>
        </div>
    );
}
