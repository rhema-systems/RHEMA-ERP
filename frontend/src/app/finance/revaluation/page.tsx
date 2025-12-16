'use client';

import React, { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { DollarSign, RefreshCw, ArrowRight, CheckCircle, AlertTriangle } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { useRouter } from 'next/navigation';

// MOCK DATA for Preview
const MOCK_PREVIEW_RESULTS = [
    { accountCode: '1100', accountName: 'Accounts Receivable', currency: 'USD', foreignBalance: 10000, oldRate: 12.5, newRate: 13.0, oldBase: 125000, newBase: 130000, adjustment: 5000, type: 'Gain' },
    { accountCode: '2000', accountName: 'Accounts Payable', currency: 'USD', foreignBalance: -5000, oldRate: 12.5, newRate: 13.0, oldBase: -62500, newBase: -65000, adjustment: -2500, type: 'Loss' },
];

export default function CurrencyRevaluationPage() {
    const router = useRouter();
    const [step, setStep] = useState(1); // 1: Parameters, 2: Preview, 3: Result
    const [isLoading, setIsLoading] = useState(false);

    const [params, setParams] = useState({
        revaluationDate: new Date().toISOString().split('T')[0],
        currencyCode: 'all',
        rateType: 'MonthEnd',
        unrealizedGainLossAccount: '7000', // Mock ID
    });

    const handlePreview = () => {
        setIsLoading(true);
        // Simulate API call
        setTimeout(() => {
            setIsLoading(false);
            setStep(2);
        }, 1000);
    };

    const handlePost = () => {
        setIsLoading(true);
        // Simulate API call
        setTimeout(() => {
            setIsLoading(false);
            setStep(3);
        }, 1000);
    };

    const formatCurrency = (amount: number) => {
        return new Intl.NumberFormat('en-GH', {
            style: 'currency',
            currency: 'GHS',
        }).format(Math.abs(amount));
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <RefreshCw className="h-8 w-8" />
                        Currency Revaluation
                    </h1>
                    <p className="text-muted-foreground">
                        Revalue foreign currency accounts and post unrealized gains/losses
                    </p>
                    <p className="text-sm text-orange-600 mt-1">
                        ⚠️ DEMO MODE - Using mock data (backend not connected)
                    </p>
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
                        <BreadcrumbPage>Revaluation</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Steps Indicator */}
            <div className="flex items-center justify-center space-x-4 mb-8">
                <div className={`flex items-center gap-2 ${step >= 1 ? 'text-primary font-bold' : 'text-muted-foreground'}`}>
                    <div className={`w-8 h-8 rounded-full flex items-center justify-center border-2 ${step >= 1 ? 'border-primary bg-primary text-primary-foreground' : 'border-muted-foreground'}`}>1</div>
                    <span>Parameters</span>
                </div>
                <div className={`w-16 h-0.5 ${step >= 2 ? 'bg-primary' : 'bg-muted'}`} />
                <div className={`flex items-center gap-2 ${step >= 2 ? 'text-primary font-bold' : 'text-muted-foreground'}`}>
                    <div className={`w-8 h-8 rounded-full flex items-center justify-center border-2 ${step >= 2 ? 'border-primary bg-primary text-primary-foreground' : 'border-muted-foreground'}`}>2</div>
                    <span>Preview</span>
                </div>
                <div className={`w-16 h-0.5 ${step >= 3 ? 'bg-primary' : 'bg-muted'}`} />
                <div className={`flex items-center gap-2 ${step >= 3 ? 'text-primary font-bold' : 'text-muted-foreground'}`}>
                    <div className={`w-8 h-8 rounded-full flex items-center justify-center border-2 ${step >= 3 ? 'border-primary bg-primary text-primary-foreground' : 'border-muted-foreground'}`}>3</div>
                    <span>Result</span>
                </div>
            </div>

            {/* Step 1: Parameters */}
            {step === 1 && (
                <Card className="max-w-2xl mx-auto">
                    <CardHeader>
                        <CardTitle>Revaluation Parameters</CardTitle>
                        <CardDescription>Select criteria for currency revaluation</CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <div className="grid grid-cols-2 gap-4">
                            <div className="space-y-2">
                                <Label htmlFor="date">Revaluation Date</Label>
                                <Input
                                    id="date"
                                    type="date"
                                    value={params.revaluationDate}
                                    onChange={(e) => setParams({ ...params, revaluationDate: e.target.value })}
                                />
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="rateType">Rate Type</Label>
                                <Select
                                    value={params.rateType}
                                    onValueChange={(value) => setParams({ ...params, rateType: value })}
                                >
                                    <SelectTrigger>
                                        <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="MonthEnd">Month End</SelectItem>
                                        <SelectItem value="Average">Average</SelectItem>
                                        <SelectItem value="Daily">Daily Spot</SelectItem>
                                    </SelectContent>
                                </Select>
                            </div>
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="currency">Currency</Label>
                            <Select
                                value={params.currencyCode}
                                onValueChange={(value) => setParams({ ...params, currencyCode: value })}
                            >
                                <SelectTrigger>
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="all">All Currencies</SelectItem>
                                    <SelectItem value="USD">USD - US Dollar</SelectItem>
                                    <SelectItem value="EUR">EUR - Euro</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="account">Unrealized Gain/Loss Account</Label>
                            <Select
                                value={params.unrealizedGainLossAccount}
                                onValueChange={(value) => setParams({ ...params, unrealizedGainLossAccount: value })}
                            >
                                <SelectTrigger>
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="7000">7000 - Unrealized Gain/Loss (Expense)</SelectItem>
                                    <SelectItem value="4500">4500 - Unrealized Gain/Loss (Revenue)</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>

                        <Alert>
                            <AlertTriangle className="h-4 w-4" />
                            <AlertTitle>Important</AlertTitle>
                            <AlertDescription>
                                This process will calculate unrealized gains and losses for all open foreign currency items and generate a reversing journal entry.
                            </AlertDescription>
                        </Alert>

                        <div className="pt-4 flex justify-end">
                            <Button onClick={handlePreview} disabled={isLoading}>
                                {isLoading ? 'Calculating...' : 'Preview Results'}
                                {!isLoading && <ArrowRight className="ml-2 h-4 w-4" />}
                            </Button>
                        </div>
                    </CardContent>
                </Card>
            )}

            {/* Step 2: Preview */}
            {step === 2 && (
                <div className="space-y-6">
                    <Card>
                        <CardHeader>
                            <CardTitle>Revaluation Preview</CardTitle>
                            <CardDescription>Review calculated adjustments before posting</CardDescription>
                        </CardHeader>
                        <CardContent>
                            <div className="rounded-md border">
                                <table className="w-full text-sm">
                                    <thead>
                                        <tr className="border-b bg-muted/50">
                                            <th className="p-3 text-left font-medium">Account</th>
                                            <th className="p-3 text-left font-medium">Currency</th>
                                            <th className="p-3 text-right font-medium">Foreign Bal</th>
                                            <th className="p-3 text-right font-medium">Old Rate</th>
                                            <th className="p-3 text-right font-medium">New Rate</th>
                                            <th className="p-3 text-right font-medium">Old Base</th>
                                            <th className="p-3 text-right font-medium">New Base</th>
                                            <th className="p-3 text-right font-medium">Adjustment</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {MOCK_PREVIEW_RESULTS.map((row, index) => (
                                            <tr key={index} className="border-b last:border-0 hover:bg-muted/50">
                                                <td className="p-3">
                                                    <div className="font-semibold">{row.accountCode}</div>
                                                    <div className="text-muted-foreground text-xs">{row.accountName}</div>
                                                </td>
                                                <td className="p-3">{row.currency}</td>
                                                <td className="p-3 text-right">{row.foreignBalance.toLocaleString()}</td>
                                                <td className="p-3 text-right">{row.oldRate.toFixed(4)}</td>
                                                <td className="p-3 text-right">{row.newRate.toFixed(4)}</td>
                                                <td className="p-3 text-right">{row.oldBase.toLocaleString()}</td>
                                                <td className="p-3 text-right">{row.newBase.toLocaleString()}</td>
                                                <td className="p-3 text-right font-bold">
                                                    <span className={row.adjustment > 0 ? 'text-green-600' : 'text-red-600'}>
                                                        {formatCurrency(row.adjustment)}
                                                    </span>
                                                    <Badge variant="outline" className="ml-2 text-xs">
                                                        {row.type}
                                                    </Badge>
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                    <tfoot>
                                        <tr className="bg-muted/50 font-bold">
                                            <td colSpan={7} className="p-3 text-right">Net Adjustment:</td>
                                            <td className="p-3 text-right text-green-600">
                                                {formatCurrency(2500)} (Gain)
                                            </td>
                                        </tr>
                                    </tfoot>
                                </table>
                            </div>

                            <div className="flex justify-between mt-6">
                                <Button variant="outline" onClick={() => setStep(1)}>Back</Button>
                                <Button onClick={handlePost} disabled={isLoading}>
                                    {isLoading ? 'Posting...' : 'Post Revaluation'}
                                </Button>
                            </div>
                        </CardContent>
                    </Card>
                </div>
            )}

            {/* Step 3: Result */}
            {step === 3 && (
                <Card className="max-w-md mx-auto text-center py-8">
                    <CardContent className="space-y-4">
                        <div className="flex justify-center">
                            <CheckCircle className="h-16 w-16 text-green-500" />
                        </div>
                        <h2 className="text-2xl font-bold">Revaluation Posted!</h2>
                        <p className="text-muted-foreground">
                            Journal Entry <strong>JE-2024-006</strong> has been created successfully.
                        </p>
                        <div className="pt-4 space-x-4">
                            <Button variant="outline" onClick={() => setStep(1)}>
                                Start New Revaluation
                            </Button>
                            <Button onClick={() => router.push('/finance/journal-entries')}>
                                View Journal Entries
                            </Button>
                        </div>
                    </CardContent>
                </Card>
            )}
        </div>
    );
}
