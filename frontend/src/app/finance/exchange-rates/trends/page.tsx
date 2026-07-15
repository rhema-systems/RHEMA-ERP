'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { LineChart as LucideLineChart, TrendingUp, TrendingDown, Minus, ArrowLeft } from 'lucide-react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip, Legend, ResponsiveContainer } from 'recharts';
import type { TrendAnalysisDto } from '@/types/finance';
import { financeService } from '@/services/finance.service';
import Link from 'next/link';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';

const MOCK_CURRENCIES = ['USD', 'EUR', 'GBP', 'CAD', 'JPY'];

export default function ExchangeRateTrendsPage() {
    const [currencyCode, setCurrencyCode] = useState<string>('USD');
    const [months, setMonths] = useState<number>(6);
    const [trends, setTrends] = useState<TrendAnalysisDto[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const fetchTrends = async () => {
            setIsLoading(true);
            setError(null);
            try {
                // Fetch trends from the backend
                const data = await financeService.getExchangeRateTrends(currencyCode, months);
                setTrends(data);
            } catch (err: any) {
                console.error('Failed to fetch exchange rate trends', err);
                setError(err?.response?.data?.message || 'Failed to load exchange rate trends. Please try again.');
            } finally {
                setIsLoading(false);
            }
        };

        fetchTrends();
    }, [currencyCode, months]);

    const formatDate = (dateString: string) => {
        return new Date(dateString).toLocaleDateString('en-US', {
            month: 'short',
            day: 'numeric',
            year: 'numeric'
        });
    };

    const formatCurrency = (value: number | null | undefined) => {
        return typeof value === 'number' ? value.toFixed(4) : 'N/A';
    };

    const latestTrend = trends.length > 0 ? trends[trends.length - 1] : null;
    const latestChangePercentage = latestTrend?.changePercentage ?? 0;

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <LucideLineChart className="h-8 w-8" />
                        Exchange Rate Trends
                    </h1>
                    <p className="text-muted-foreground">
                        Analyze historical exchange rate trends and volatility
                    </p>
                </div>
                <div className="flex gap-2">
                    <Button asChild variant="outline">
                        <Link href="/finance/exchange-rates">
                            <ArrowLeft className="mr-2 h-4 w-4" />
                            Back to Rates
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
                        <BreadcrumbLink href="/finance">Finance</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/finance/exchange-rates">Exchange Rates</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>Trends</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Filters */}
            <Card>
                <CardHeader>
                    <CardTitle>Analysis Settings</CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="flex flex-wrap gap-4">
                        <div className="space-y-2 min-w-[200px]">
                            <label className="text-sm font-medium">Target Currency</label>
                            <Select value={currencyCode} onValueChange={setCurrencyCode}>
                                <SelectTrigger>
                                    <SelectValue placeholder="Select Currency" />
                                </SelectTrigger>
                                <SelectContent>
                                    {MOCK_CURRENCIES.map(curr => (
                                        <SelectItem key={curr} value={curr}>{curr}</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2 min-w-[200px]">
                            <label className="text-sm font-medium">Time Period</label>
                            <Select value={months.toString()} onValueChange={(v) => setMonths(parseInt(v))}>
                                <SelectTrigger>
                                    <SelectValue placeholder="Select Period" />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="1">Last 1 Month</SelectItem>
                                    <SelectItem value="3">Last 3 Months</SelectItem>
                                    <SelectItem value="6">Last 6 Months</SelectItem>
                                    <SelectItem value="12">Last 1 Year</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                    </div>
                </CardContent>
            </Card>

            {/* Loading/Error States */}
            {isLoading && (
                <div className="space-y-4">
                    <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                        <Skeleton className="h-32 w-full" />
                        <Skeleton className="h-32 w-full" />
                        <Skeleton className="h-32 w-full" />
                        <Skeleton className="h-32 w-full" />
                    </div>
                    <Skeleton className="h-[400px] w-full" />
                </div>
            )}

            {error && (
                <Alert variant="destructive">
                    <AlertDescription>{error}</AlertDescription>
                </Alert>
            )}

            {!isLoading && !error && trends.length === 0 && (
                <Alert>
                    <AlertDescription>No trend data available for the selected currency and period.</AlertDescription>
                </Alert>
            )}

            {/* Data Visualization */}
            {!isLoading && !error && trends.length > 0 && latestTrend && (
                <>
                    {/* Summary Cards */}
                    <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                        <Card>
                            <CardHeader className="pb-2">
                                <CardTitle className="text-sm font-medium text-muted-foreground">Latest Rate</CardTitle>
                            </CardHeader>
                            <CardContent>
                                <div className="text-2xl font-bold">{formatCurrency(latestTrend.rate)}</div>
                                <div className="text-sm flex items-center mt-1">
                                    {latestChangePercentage > 0 ? (
                                        <span className="text-green-600 flex items-center">
                                            <TrendingUp className="mr-1 h-3 w-3" />
                                            +{latestChangePercentage.toFixed(2)}%
                                        </span>
                                    ) : latestChangePercentage < 0 ? (
                                        <span className="text-red-600 flex items-center">
                                            <TrendingDown className="mr-1 h-3 w-3" />
                                            {latestChangePercentage.toFixed(2)}%
                                        </span>
                                    ) : (
                                        <span className="text-muted-foreground flex items-center">
                                            <Minus className="mr-1 h-3 w-3" />
                                            0.00%
                                        </span>
                                    )}
                                    <span className="text-muted-foreground ml-2">from previous</span>
                                </div>
                            </CardContent>
                        </Card>
                        
                        <Card>
                            <CardHeader className="pb-2">
                                <CardTitle className="text-sm font-medium text-muted-foreground">Moving Average</CardTitle>
                            </CardHeader>
                            <CardContent>
                                <div className="text-2xl font-bold">{formatCurrency(latestTrend.movingAverage)}</div>
                                <p className="text-xs text-muted-foreground mt-1">7-period average</p>
                            </CardContent>
                        </Card>

                        <Card>
                            <CardHeader className="pb-2">
                                <CardTitle className="text-sm font-medium text-muted-foreground">Highest Rate</CardTitle>
                            </CardHeader>
                            <CardContent>
                                <div className="text-2xl font-bold text-green-600">{formatCurrency(latestTrend.maxRate)}</div>
                                <p className="text-xs text-muted-foreground mt-1">in selected period</p>
                            </CardContent>
                        </Card>

                        <Card>
                            <CardHeader className="pb-2">
                                <CardTitle className="text-sm font-medium text-muted-foreground">Lowest Rate</CardTitle>
                            </CardHeader>
                            <CardContent>
                                <div className="text-2xl font-bold text-red-600">{formatCurrency(latestTrend.minRate)}</div>
                                <p className="text-xs text-muted-foreground mt-1">in selected period</p>
                            </CardContent>
                        </Card>
                    </div>

                    {/* Chart */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Rate History</CardTitle>
                            <CardDescription>
                                {currencyCode} to Base Currency exchange rate over the last {months} month(s)
                            </CardDescription>
                        </CardHeader>
                        <CardContent>
                            <div className="h-[400px] w-full">
                                <ResponsiveContainer width="100%" height="100%">
                                    <LineChart
                                        data={trends}
                                        margin={{
                                            top: 5,
                                            right: 30,
                                            left: 20,
                                            bottom: 5,
                                        }}
                                    >
                                        <CartesianGrid strokeDasharray="3 3" opacity={0.3} />
                                        <XAxis 
                                            dataKey="date" 
                                            tickFormatter={formatDate}
                                            tick={{ fontSize: 12 }}
                                            tickMargin={10}
                                        />
                                        <YAxis 
                                            domain={['auto', 'auto']} 
                                            tickFormatter={formatCurrency}
                                            tick={{ fontSize: 12 }}
                                        />
                                        <RechartsTooltip 
                                            labelFormatter={formatDate}
                                            formatter={(value, name) => [
                                                typeof value === 'number' ? formatCurrency(value) : 'N/A',
                                                name === 'rate' ? 'Actual Rate' : 'Moving Avg'
                                            ]}
                                            contentStyle={{ borderRadius: '8px', border: '1px solid #e2e8f0' }}
                                        />
                                        <Legend />
                                        <Line 
                                            type="monotone" 
                                            dataKey="rate" 
                                            name="Actual Rate"
                                            stroke="#2563eb" 
                                            strokeWidth={2}
                                            dot={{ r: 4 }}
                                            activeDot={{ r: 8 }}
                                        />
                                        <Line 
                                            type="monotone" 
                                            dataKey="movingAverage" 
                                            name="Moving Average"
                                            stroke="#f59e0b" 
                                            strokeWidth={2}
                                            strokeDasharray="5 5"
                                            dot={false}
                                        />
                                    </LineChart>
                                </ResponsiveContainer>
                            </div>
                        </CardContent>
                    </Card>
                </>
            )}
        </div>
    );
}
