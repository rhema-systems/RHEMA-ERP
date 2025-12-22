'use client';

import React, { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Label } from '@/components/ui/label';
import { Calculator, TrendingUp, ArrowRight } from 'lucide-react';
import Link from 'next/link';
import type { RatioDefinition, RatioCalculationResult } from '@/types/unit-accounts';

// MOCK DATA
const MOCK_RATIOS: RatioDefinition[] = [
    {
        id: 'rd-1',
        code: 'REV-EMP',
        name: 'Revenue Per Employee',
        description: 'Total revenue divided by employee headcount',
        numeratorType: 'FinancialAccount',
        denominatorType: 'UnitAccount',
        resultFormat: 'Currency',
        decimalPlaces: 2,
        isActive: true,
        createdAt: '',
        createdBy: '',
    },
    {
        id: 'rd-2',
        code: 'COST-SQFT',
        name: 'Cost Per Square Foot',
        description: 'Operating costs per unit of floor space',
        numeratorType: 'FinancialAccount',
        denominatorType: 'UnitAccount',
        resultFormat: 'Currency',
        decimalPlaces: 2,
        isActive: true,
        createdAt: '',
        createdBy: '',
    },
    {
        id: 'rd-3',
        code: 'UNITS-HR',
        name: 'Units Per Machine Hour',
        description: 'Production efficiency ratio',
        numeratorType: 'UnitAccount',
        denominatorType: 'UnitAccount',
        resultFormat: 'Decimal',
        decimalPlaces: 2,
        isActive: true,
        createdAt: '',
        createdBy: '',
    },
    {
        id: 'rd-4',
        code: 'GP-PCT',
        name: 'Gross Profit Margin',
        description: 'Gross profit as percentage of revenue',
        numeratorType: 'FinancialAccount',
        denominatorType: 'FinancialAccount',
        resultFormat: 'Percentage',
        decimalPlaces: 1,
        isActive: true,
        createdAt: '',
        createdBy: '',
    },
];

const MOCK_PERIODS = [
    { id: 'fp-12', name: 'December 2024' },
    { id: 'fp-11', name: 'November 2024' },
    { id: 'fp-10', name: 'October 2024' },
    { id: 'fp-9', name: 'September 2024' },
];

// Mock calculation results
const MOCK_RESULTS: Record<string, RatioCalculationResult> = {
    'rd-1': {
        ratioId: 'rd-1',
        ratioCode: 'REV-EMP',
        ratioName: 'Revenue Per Employee',
        numeratorValue: 4500000,
        denominatorValue: 125,
        result: 36000,
        resultFormatted: 'GHS 36,000.00',
        calculatedAt: new Date().toISOString(),
        periodName: 'December 2024',
    },
    'rd-2': {
        ratioId: 'rd-2',
        ratioCode: 'COST-SQFT',
        ratioName: 'Cost Per Square Foot',
        numeratorValue: 750000,
        denominatorValue: 15000,
        result: 50,
        resultFormatted: 'GHS 50.00',
        calculatedAt: new Date().toISOString(),
        periodName: 'December 2024',
    },
    'rd-3': {
        ratioId: 'rd-3',
        ratioCode: 'UNITS-HR',
        ratioName: 'Units Per Machine Hour',
        numeratorValue: 8500,
        denominatorValue: 2500.5,
        result: 3.4,
        resultFormatted: '3.40',
        calculatedAt: new Date().toISOString(),
        periodName: 'December 2024',
    },
    'rd-4': {
        ratioId: 'rd-4',
        ratioCode: 'GP-PCT',
        ratioName: 'Gross Profit Margin',
        numeratorValue: 1800000,
        denominatorValue: 4500000,
        result: 40,
        resultFormatted: '40.0%',
        calculatedAt: new Date().toISOString(),
        periodName: 'December 2024',
    },
};

export default function RatioCalculatorPage() {
    const [selectedRatioId, setSelectedRatioId] = useState<string>('');
    const [selectedPeriodId, setSelectedPeriodId] = useState<string>('fp-12');
    const [result, setResult] = useState<RatioCalculationResult | null>(null);

    const selectedRatio = MOCK_RATIOS.find((r) => r.id === selectedRatioId);

    const handleCalculate = () => {
        if (selectedRatioId && MOCK_RESULTS[selectedRatioId]) {
            setResult(MOCK_RESULTS[selectedRatioId]);
        } else {
            alert('DEMO MODE: Please select a ratio to calculate.');
        }
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div>
                <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                    <Calculator className="h-8 w-8" />
                    Ratio Calculator
                </h1>
                <p className="text-muted-foreground">
                    Calculate KPI ratios for any period
                </p>
                <p className="text-sm text-orange-600 mt-1">
                    ⚠️ DEMO FRONTEND UI
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
                        <BreadcrumbLink href="/finance/ratio-definitions">Ratio Definitions</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>Calculator</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                {/* Calculator Form */}
                <Card>
                    <CardHeader>
                        <CardTitle>Calculate Ratio</CardTitle>
                        <CardDescription>
                            Select a ratio and period to calculate
                        </CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-6">
                        <div className="space-y-2">
                            <Label>Ratio Definition</Label>
                            <Select value={selectedRatioId} onValueChange={setSelectedRatioId}>
                                <SelectTrigger>
                                    <SelectValue placeholder="Select a ratio..." />
                                </SelectTrigger>
                                <SelectContent>
                                    {MOCK_RATIOS.map((ratio) => (
                                        <SelectItem key={ratio.id} value={ratio.id}>
                                            {ratio.code} - {ratio.name}
                                        </SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>

                        {selectedRatio && (
                            <div className="p-4 bg-muted rounded-lg space-y-2">
                                <p className="text-sm font-medium">{selectedRatio.name}</p>
                                <p className="text-xs text-muted-foreground">{selectedRatio.description}</p>
                                <div className="flex items-center gap-2 pt-2">
                                    <Badge variant="outline">
                                        {selectedRatio.numeratorType === 'FinancialAccount' ? 'Financial' : 'Unit'}
                                    </Badge>
                                    <ArrowRight className="h-4 w-4 text-muted-foreground" />
                                    <Badge variant="outline">
                                        {selectedRatio.denominatorType === 'FinancialAccount' ? 'Financial' : 'Unit'}
                                    </Badge>
                                    <span className="text-muted-foreground">=</span>
                                    <Badge>{selectedRatio.resultFormat}</Badge>
                                </div>
                            </div>
                        )}

                        <div className="space-y-2">
                            <Label>Fiscal Period</Label>
                            <Select value={selectedPeriodId} onValueChange={setSelectedPeriodId}>
                                <SelectTrigger>
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    {MOCK_PERIODS.map((period) => (
                                        <SelectItem key={period.id} value={period.id}>
                                            {period.name}
                                        </SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>

                        <Button
                            className="w-full"
                            onClick={handleCalculate}
                            disabled={!selectedRatioId}
                        >
                            <Calculator className="mr-2 h-4 w-4" />
                            Calculate
                        </Button>
                    </CardContent>
                </Card>

                {/* Result Display */}
                <Card>
                    <CardHeader>
                        <CardTitle className="flex items-center gap-2">
                            <TrendingUp className="h-5 w-5" />
                            Calculation Result
                        </CardTitle>
                        <CardDescription>
                            {result ? `Calculated for ${result.periodName}` : 'Select a ratio and click Calculate'}
                        </CardDescription>
                    </CardHeader>
                    <CardContent>
                        {result ? (
                            <div className="space-y-6">
                                <div className="text-center py-8 bg-muted rounded-lg">
                                    <p className="text-sm text-muted-foreground mb-2">
                                        {result.ratioName}
                                    </p>
                                    <p className="text-5xl font-bold text-blue-600">
                                        {result.resultFormatted}
                                    </p>
                                </div>

                                <div className="grid grid-cols-2 gap-4">
                                    <div className="p-4 border rounded-lg">
                                        <p className="text-sm text-muted-foreground">Numerator</p>
                                        <p className="text-xl font-semibold font-mono">
                                            {result.numeratorValue.toLocaleString()}
                                        </p>
                                    </div>
                                    <div className="p-4 border rounded-lg">
                                        <p className="text-sm text-muted-foreground">Denominator</p>
                                        <p className="text-xl font-semibold font-mono">
                                            {result.denominatorValue.toLocaleString()}
                                        </p>
                                    </div>
                                </div>

                                <div className="text-center text-sm text-muted-foreground">
                                    {result.numeratorValue.toLocaleString()} ÷ {result.denominatorValue.toLocaleString()} = {result.result}
                                </div>
                            </div>
                        ) : (
                            <div className="text-center py-12 text-muted-foreground">
                                <Calculator className="h-12 w-12 mx-auto mb-4 opacity-50" />
                                <p>No calculation performed yet.</p>
                                <p className="text-sm">Select a ratio and period, then click Calculate.</p>
                            </div>
                        )}
                    </CardContent>
                </Card>
            </div>

            {/* Quick Calculate All */}
            <Card>
                <CardHeader>
                    <CardTitle>All Ratios Overview</CardTitle>
                    <CardDescription>
                        Quick view of all active ratios for the selected period
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
                        {MOCK_RATIOS.map((ratio) => {
                            const mockResult = MOCK_RESULTS[ratio.id];
                            return (
                                <div key={ratio.id} className="p-4 border rounded-lg">
                                    <p className="text-sm text-muted-foreground">{ratio.code}</p>
                                    <p className="font-medium truncate">{ratio.name}</p>
                                    <p className="text-2xl font-bold text-blue-600 mt-2">
                                        {mockResult?.resultFormatted || '-'}
                                    </p>
                                </div>
                            );
                        })}
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}
