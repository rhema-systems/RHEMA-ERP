'use client';

import { useEffect, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { taxDataService } from '@/services/finance/tax-data.service';
import type { VATReconciliation } from '@/types/tax';
import { FileText, Download, TrendingUp, TrendingDown } from 'lucide-react';
import Link from 'next/link';

export default function VATReconciliationPage() {
    const [reconciliation, setReconciliation] = useState<VATReconciliation | null>(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        loadReconciliation();
    }, []);

    const loadReconciliation = async () => {
        try {
            const startDate = '2024-12-01';
            const endDate = '2024-12-31';
            const data = await taxDataService.getVATReconciliation(startDate, endDate);
            setReconciliation(data);
        } catch (error) {
            console.error('Failed to load VAT reconciliation:', error);
        } finally {
            setLoading(false);
        }
    };

    const formatCurrency = (amount: number) => {
        return `GHS ${amount.toLocaleString('en-GH', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    };

    return (
        <div className="p-6 space-y-6">
            {/* Header */}
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-3xl font-bold">VAT Reconciliation</h1>
                    <p className="text-muted-foreground">Output VAT vs Input VAT for December 2024</p>
                </div>
                <div className="flex gap-2">
                    <Link href="/finance/tax/reports">
                        <Button variant="outline">
                            <FileText className="mr-2 h-4 w-4" />
                            All Reports
                        </Button>
                    </Link>
                    <Button variant="outline">
                        <Download className="mr-2 h-4 w-4" />
                        Export
                    </Button>
                </div>
            </div>

            {loading ? (
                <div className="text-center py-12 text-muted-foreground">Loading...</div>
            ) : reconciliation ? (
                <>
                    {/* Summary Card */}
                    <Card className="bg-gradient-to-br from-blue-50 to-purple-50 border-blue-200">
                        <CardHeader>
                            <CardTitle className="text-2xl">Total Tax Payable</CardTitle>
                            <CardDescription>Net amount due to GRA for {reconciliation.period}</CardDescription>
                        </CardHeader>
                        <CardContent>
                            <div className="text-4xl font-bold text-primary">
                                {formatCurrency(reconciliation.totalPayable)}
                            </div>
                            <p className="text-sm text-muted-foreground mt-2">
                                VAT + NHIL + GETFL (Output - Input)
                            </p>
                        </CardContent>
                    </Card>

                    {/* VAT Breakdown */}
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                        {/* VAT */}
                        <Card>
                            <CardHeader>
                                <CardTitle className="flex items-center gap-2">
                                    <div className="h-3 w-3 rounded-full bg-blue-500"></div>
                                    Value Added Tax (VAT)
                                </CardTitle>
                                <CardDescription>15% on transactions</CardDescription>
                            </CardHeader>
                            <CardContent className="space-y-4">
                                <div className="flex justify-between items-center p-3 bg-green-50 rounded-lg">
                                    <div className="flex items-center gap-2">
                                        <TrendingUp className="h-4 w-4 text-green-600" />
                                        <span className="text-sm font-medium">Output VAT</span>
                                    </div>
                                    <span className="font-bold text-green-700">
                                        {formatCurrency(reconciliation.outputVAT)}
                                    </span>
                                </div>
                                <div className="flex justify-between items-center p-3 bg-red-50 rounded-lg">
                                    <div className="flex items-center gap-2">
                                        <TrendingDown className="h-4 w-4 text-red-600" />
                                        <span className="text-sm font-medium">Input VAT</span>
                                    </div>
                                    <span className="font-bold text-red-700">
                                        {formatCurrency(reconciliation.inputVAT)}
                                    </span>
                                </div>
                                <div className="border-t pt-3">
                                    <div className="flex justify-between items-center">
                                        <span className="font-semibold">Net VAT Payable</span>
                                        <span className="text-lg font-bold text-blue-600">
                                            {formatCurrency(reconciliation.netVATPayable)}
                                        </span>
                                    </div>
                                </div>
                            </CardContent>
                        </Card>

                        {/* NHIL */}
                        <Card>
                            <CardHeader>
                                <CardTitle className="flex items-center gap-2">
                                    <div className="h-3 w-3 rounded-full bg-purple-500"></div>
                                    NHIL
                                </CardTitle>
                                <CardDescription>2.5% National Health Insurance Levy</CardDescription>
                            </CardHeader>
                            <CardContent className="space-y-4">
                                <div className="flex justify-between items-center p-3 bg-green-50 rounded-lg">
                                    <div className="flex items-center gap-2">
                                        <TrendingUp className="h-4 w-4 text-green-600" />
                                        <span className="text-sm font-medium">Output NHIL</span>
                                    </div>
                                    <span className="font-bold text-green-700">
                                        {formatCurrency(reconciliation.outputNHIL)}
                                    </span>
                                </div>
                                <div className="flex justify-between items-center p-3 bg-red-50 rounded-lg">
                                    <div className="flex items-center gap-2">
                                        <TrendingDown className="h-4 w-4 text-red-600" />
                                        <span className="text-sm font-medium">Input NHIL</span>
                                    </div>
                                    <span className="font-bold text-red-700">
                                        {formatCurrency(reconciliation.inputNHIL)}
                                    </span>
                                </div>
                                <div className="border-t pt-3">
                                    <div className="flex justify-between items-center">
                                        <span className="font-semibold">Net NHIL Payable</span>
                                        <span className="text-lg font-bold text-purple-600">
                                            {formatCurrency(reconciliation.netNHILPayable)}
                                        </span>
                                    </div>
                                </div>
                            </CardContent>
                        </Card>

                        {/* GETFL */}
                        <Card>
                            <CardHeader>
                                <CardTitle className="flex items-center gap-2">
                                    <div className="h-3 w-3 rounded-full bg-orange-500"></div>
                                    GETFL
                                </CardTitle>
                                <CardDescription>2.5% Ghana Education Trust Fund Levy</CardDescription>
                            </CardHeader>
                            <CardContent className="space-y-4">
                                <div className="flex justify-between items-center p-3 bg-green-50 rounded-lg">
                                    <div className="flex items-center gap-2">
                                        <TrendingUp className="h-4 w-4 text-green-600" />
                                        <span className="text-sm font-medium">Output GETFL</span>
                                    </div>
                                    <span className="font-bold text-green-700">
                                        {formatCurrency(reconciliation.outputGETFL)}
                                    </span>
                                </div>
                                <div className="flex justify-between items-center p-3 bg-red-50 rounded-lg">
                                    <div className="flex items-center gap-2">
                                        <TrendingDown className="h-4 w-4 text-red-600" />
                                        <span className="text-sm font-medium">Input GETFL</span>
                                    </div>
                                    <span className="font-bold text-red-700">
                                        {formatCurrency(reconciliation.inputGETFL)}
                                    </span>
                                </div>
                                <div className="border-t pt-3">
                                    <div className="flex justify-between items-center">
                                        <span className="font-semibold">Net GETFL Payable</span>
                                        <span className="text-lg font-bold text-orange-600">
                                            {formatCurrency(reconciliation.netGETFLPayable)}
                                        </span>
                                    </div>
                                </div>
                            </CardContent>
                        </Card>
                    </div>

                    {/* Summary Table */}
                    <Card>
                        <CardHeader>
                            <CardTitle>Reconciliation Summary</CardTitle>
                            <CardDescription>Detailed breakdown of all tax components</CardDescription>
                        </CardHeader>
                        <CardContent>
                            <div className="overflow-x-auto">
                                <table className="w-full">
                                    <thead>
                                        <tr className="border-b">
                                            <th className="text-left p-3 font-semibold">Tax Type</th>
                                            <th className="text-right p-3 font-semibold">Output (Sales)</th>
                                            <th className="text-right p-3 font-semibold">Input (Purchases)</th>
                                            <th className="text-right p-3 font-semibold">Net Payable</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <tr className="border-b hover:bg-accent">
                                            <td className="p-3">
                                                <div className="flex items-center gap-2">
                                                    <div className="h-3 w-3 rounded-full bg-blue-500"></div>
                                                    <span className="font-medium">VAT (15%)</span>
                                                </div>
                                            </td>
                                            <td className="p-3 text-right text-green-700 font-medium">
                                                {formatCurrency(reconciliation.outputVAT)}
                                            </td>
                                            <td className="p-3 text-right text-red-700 font-medium">
                                                {formatCurrency(reconciliation.inputVAT)}
                                            </td>
                                            <td className="p-3 text-right font-bold text-blue-600">
                                                {formatCurrency(reconciliation.netVATPayable)}
                                            </td>
                                        </tr>
                                        <tr className="border-b hover:bg-accent">
                                            <td className="p-3">
                                                <div className="flex items-center gap-2">
                                                    <div className="h-3 w-3 rounded-full bg-purple-500"></div>
                                                    <span className="font-medium">NHIL (2.5%)</span>
                                                </div>
                                            </td>
                                            <td className="p-3 text-right text-green-700 font-medium">
                                                {formatCurrency(reconciliation.outputNHIL)}
                                            </td>
                                            <td className="p-3 text-right text-red-700 font-medium">
                                                {formatCurrency(reconciliation.inputNHIL)}
                                            </td>
                                            <td className="p-3 text-right font-bold text-purple-600">
                                                {formatCurrency(reconciliation.netNHILPayable)}
                                            </td>
                                        </tr>
                                        <tr className="border-b hover:bg-accent">
                                            <td className="p-3">
                                                <div className="flex items-center gap-2">
                                                    <div className="h-3 w-3 rounded-full bg-orange-500"></div>
                                                    <span className="font-medium">GETFL (2.5%)</span>
                                                </div>
                                            </td>
                                            <td className="p-3 text-right text-green-700 font-medium">
                                                {formatCurrency(reconciliation.outputGETFL)}
                                            </td>
                                            <td className="p-3 text-right text-red-700 font-medium">
                                                {formatCurrency(reconciliation.inputGETFL)}
                                            </td>
                                            <td className="p-3 text-right font-bold text-orange-600">
                                                {formatCurrency(reconciliation.netGETFLPayable)}
                                            </td>
                                        </tr>
                                        <tr className="bg-primary/10 font-bold">
                                            <td className="p-3">TOTAL</td>
                                            <td className="p-3 text-right text-green-700">
                                                {formatCurrency(reconciliation.outputVAT + reconciliation.outputNHIL + reconciliation.outputGETFL)}
                                            </td>
                                            <td className="p-3 text-right text-red-700">
                                                {formatCurrency(reconciliation.inputVAT + reconciliation.inputNHIL + reconciliation.inputGETFL)}
                                            </td>
                                            <td className="p-3 text-right text-xl text-primary">
                                                {formatCurrency(reconciliation.totalPayable)}
                                            </td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>
                        </CardContent>
                    </Card>

                    {/* Quick Links */}
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        <Link href="/finance/tax/reports/input-vat">
                            <Card className="hover:bg-accent cursor-pointer transition-colors">
                                <CardContent className="p-6">
                                    <h3 className="font-semibold mb-2">View Input VAT Register</h3>
                                    <p className="text-sm text-muted-foreground">
                                        Detailed list of all purchase transactions with VAT
                                    </p>
                                </CardContent>
                            </Card>
                        </Link>
                        <Link href="/finance/tax/reports/output-vat">
                            <Card className="hover:bg-accent cursor-pointer transition-colors">
                                <CardContent className="p-6">
                                    <h3 className="font-semibold mb-2">View Output VAT Register</h3>
                                    <p className="text-sm text-muted-foreground">
                                        Detailed list of all sales transactions with VAT
                                    </p>
                                </CardContent>
                            </Card>
                        </Link>
                    </div>
                </>
            ) : (
                <div className="text-center py-12 text-muted-foreground">
                    No reconciliation data available
                </div>
            )}
        </div>
    );
}
