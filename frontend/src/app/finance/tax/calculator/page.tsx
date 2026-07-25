'use client';

import { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { taxDataService } from '@/services/finance/tax-data.service';
import type { TaxCalculationResult } from '@/types/tax';
import { Calculator, Info, RotateCcw } from 'lucide-react';

export default function TaxCalculatorPage() {
    const [baseAmount, setBaseAmount] = useState<number>(10000);
    const [transactionType, setTransactionType] = useState<string>('SaleOfGoods');
    const [result, setResult] = useState<TaxCalculationResult | null>(null);
    const [loading, setLoading] = useState(false);

    const calculateTax = async () => {
        setLoading(true);
        try {
            const res = await taxDataService.calculateTax({
                baseAmount,
                transactionType,
                // transactionDate: new Date().toISOString() // Removed from new request type if not needed, or handled by backend default
            });
            setResult(res);
        } catch (error) {
            console.error('Failed to calculate tax:', error);
        } finally {
            setLoading(false);
        }
    };

    const reset = () => {
        setResult(null);
        setBaseAmount(10000);
    };

    return (
        <div className="p-6 space-y-6">
            {/* Header */}
            <div>
                <h1 className="text-3xl font-bold">Tax Calculator</h1>
                <p className="text-muted-foreground">Calculate Ghana taxes on transaction amounts using the new tax engine</p>
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                {/* Input Panel */}
                <Card>
                    <CardHeader>
                        <CardTitle>Transaction Details</CardTitle>
                        <CardDescription>Enter the transaction amount and type</CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <div>
                            <Label htmlFor="baseAmount">Base Amount (GHS)</Label>
                            <Input
                                id="baseAmount"
                                type="number"
                                value={baseAmount}
                                onChange={(e) => setBaseAmount(Number(e.target.value))}
                                className="mt-1"
                                min="0"
                                step="0.01"
                            />
                        </div>

                        <div>
                            <Label>Transaction Type</Label>
                            <div className="flex gap-2 mt-2">
                                <Button
                                    variant={transactionType === 'SaleOfGoods' ? 'default' : 'outline'}
                                    onClick={() => setTransactionType('SaleOfGoods')}
                                    className="flex-1"
                                >
                                    Sale
                                </Button>
                                <Button
                                    variant={transactionType === 'PurchaseOfGoods' ? 'default' : 'outline'}
                                    onClick={() => setTransactionType('PurchaseOfGoods')}
                                    className="flex-1"
                                >
                                    Purchase
                                </Button>
                            </div>
                        </div>

                        <div className="flex gap-2">
                            <Button
                                onClick={calculateTax}
                                className="flex-1"
                                disabled={loading}
                            >
                                <Calculator className="mr-2 h-4 w-4" />
                                {loading ? 'Calculating...' : 'Calculate Taxes'}
                            </Button>
                            {result && (
                                <Button variant="outline" onClick={reset}>
                                    <RotateCcw className="h-4 w-4" />
                                </Button>
                            )}
                        </div>

                        {/* Info Box */}
                        <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 mt-4">
                            <div className="flex gap-2">
                                <Info className="h-5 w-5 text-blue-600 flex-shrink-0 mt-0.5" />
                                <div className="text-sm text-blue-900">
                                    <p className="font-semibold mb-1">Standard Rates (Ghana)</p>
                                    <ul className="space-y-1 text-xs">
                                        <li>• VAT: 15% (Standard)</li>
                                        <li>• NHIL: 2.5%</li>
                                        <li>• GETFund: 2.5%</li>
                                        <li>• WHT: 3% (Goods), 7.5% (Services)</li>
                                    </ul>
                                </div>
                            </div>
                        </div>
                    </CardContent>
                </Card>

                {/* Results Panel */}
                <Card>
                    <CardHeader>
                        <CardTitle>Tax Breakdown</CardTitle>
                        <CardDescription>
                            {result ? 'Calculated tax amounts' : 'Enter amount and click Calculate'}
                        </CardDescription>
                    </CardHeader>
                    <CardContent>
                        {result ? (
                            <div className="space-y-4">
                                {/* Base Amount */}
                                <div className="flex justify-between items-center p-3 bg-gray-50 rounded-lg">
                                    <span className="font-medium">Base Amount</span>
                                    <span className="text-lg font-bold">
                                        GHS {result.baseAmount?.toLocaleString('en-GH', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                                    </span>
                                </div>

                                {/* Tax Calculations */}
                                <div className="space-y-2">
                                    {result.taxBreakdowns.map((calc, i) => (
                                        <div
                                            key={i}
                                            className={`flex justify-between items-center p-3 rounded-lg ${calc.taxCategory === 'Withholding' ? 'bg-orange-50 border border-orange-200' : 'bg-accent'
                                                }`}
                                        >
                                            <div>
                                                <p className="font-semibold">{calc.taxName}</p>
                                                <div className="flex flex-wrap gap-2 text-xs text-muted-foreground">
                                                    <span>{calc.taxRate}% on GHS {calc.taxableAmount.toLocaleString('en-GH', { minimumFractionDigits: 2 })}</span>
                                                    <Badge variant="outline" className="text-[10px] h-5">
                                                        {calc.compoundBasis}
                                                    </Badge>
                                                </div>
                                            </div>
                                            <p className={`font-bold ${calc.taxCategory === 'Withholding' ? 'text-orange-700' : ''}`}>
                                                {calc.taxCategory === 'Withholding' ? '-' : '+'} GHS {calc.taxAmount.toLocaleString('en-GH', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                                            </p>
                                        </div>
                                    ))}
                                </div>

                                {/* Totals */}
                                <div className="border-t pt-3 space-y-2">
                                    <div className="flex justify-between items-center">
                                        <span className="font-semibold">Total Tax Added</span>
                                        <span className="text-lg font-bold text-green-600">
                                            GHS {result.totalTaxAmount.toLocaleString('en-GH', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                                        </span>
                                    </div>

                                    <div className="flex justify-between items-center p-4 bg-primary/10 rounded-lg mt-2">
                                        <span className="text-lg font-semibold">Grand Total</span>
                                        <span className="text-2xl font-bold text-primary">
                                            GHS {result.grandTotal.toLocaleString('en-GH', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                                        </span>
                                    </div>
                                    <div className="text-right text-xs text-muted-foreground">
                                        Effective Tax Rate: {result.effectiveTaxRate.toFixed(2)}%
                                    </div>
                                </div>
                            </div>
                        ) : (
                            <div className="text-center py-12 text-muted-foreground">
                                <Calculator className="h-12 w-12 mx-auto mb-3 opacity-50" />
                                <p>Enter an amount and click Calculate to see tax breakdown</p>
                            </div>
                        )}
                    </CardContent>
                </Card>
            </div>

            {/* Example Scenarios */}
            <Card>
                <CardHeader>
                    <CardTitle>Example Scenarios</CardTitle>
                    <CardDescription>Click to try common transaction amounts</CardDescription>
                </CardHeader>
                <CardContent>
                    <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                        {[
                            { amount: 1000, label: 'GHS 1,000', type: 'Sale' },
                            { amount: 5000, label: 'GHS 5,000', type: 'Purchase' },
                            { amount: 10000, label: 'GHS 10,000', type: 'Sale' },
                            { amount: 50000, label: 'GHS 50,000', type: 'Purchase' },
                        ].map((scenario, i) => (
                            <Button
                                key={i}
                                variant="outline"
                                onClick={() => {
                                    setBaseAmount(scenario.amount);
                                    setTransactionType(scenario.type === 'Sale' ? 'SaleOfGoods' : 'PurchaseOfGoods');
                                    // setTimeout(() => calculateTax(), 100); // Let user click calculate
                                }}
                            >
                                {scenario.label}
                                <Badge variant="secondary" className="ml-2 text-xs">
                                    {scenario.type}
                                </Badge>
                            </Button>
                        ))}
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}
