'use client';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { FileText, TrendingUp, TrendingDown, AlertCircle, Calculator } from 'lucide-react';
import Link from 'next/link';

export default function TaxReportsPage() {
    return (
        <div className="p-6 space-y-6">
            {/* Header */}
            <div>
                <h1 className="text-3xl font-bold">Tax Reports</h1>
                <p className="text-muted-foreground">Ghana Revenue Authority (GRA) compliance reports</p>
            </div>

            {/* VAT Reports */}
            <div>
                <h2 className="text-xl font-semibold mb-4">VAT Reports</h2>
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                    <Link href="/finance/tax/reports/input-vat">
                        <Card className="hover:bg-accent cursor-pointer transition-colors h-full">
                            <CardHeader>
                                <CardTitle className="flex items-center gap-2">
                                    <TrendingDown className="h-5 w-5 text-green-600" />
                                    Input VAT Register
                                </CardTitle>
                                <CardDescription>
                                    VAT paid on purchases
                                </CardDescription>
                            </CardHeader>
                            <CardContent>
                                <p className="text-sm text-muted-foreground">
                                    Detailed list of all purchase transactions with VAT, NHIL, and GETFL breakdown
                                </p>
                            </CardContent>
                        </Card>
                    </Link>

                    <Link href="/finance/tax/reports/output-vat">
                        <Card className="hover:bg-accent cursor-pointer transition-colors h-full">
                            <CardHeader>
                                <CardTitle className="flex items-center gap-2">
                                    <TrendingUp className="h-5 w-5 text-blue-600" />
                                    Output VAT Register
                                </CardTitle>
                                <CardDescription>
                                    VAT collected on sales
                                </CardDescription>
                            </CardHeader>
                            <CardContent>
                                <p className="text-sm text-muted-foreground">
                                    Detailed list of all sales transactions with VAT, NHIL, and GETFL breakdown
                                </p>
                            </CardContent>
                        </Card>
                    </Link>

                    <Link href="/finance/tax/reports/vat-reconciliation">
                        <Card className="hover:bg-accent cursor-pointer transition-colors h-full">
                            <CardHeader>
                                <CardTitle className="flex items-center gap-2">
                                    <FileText className="h-5 w-5 text-purple-600" />
                                    VAT Reconciliation
                                </CardTitle>
                                <CardDescription>
                                    Output vs Input VAT
                                </CardDescription>
                            </CardHeader>
                            <CardContent>
                                <p className="text-sm text-muted-foreground">
                                    Compare output and input VAT to calculate net amount payable to GRA
                                </p>
                            </CardContent>
                        </Card>
                    </Link>
                </div>
            </div>

            {/* Withholding Tax Reports */}
            <div>
                <h2 className="text-xl font-semibold mb-4">Withholding Tax Reports</h2>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <Link href="/finance/tax/reports/withholding-tax">
                        <Card className="hover:bg-accent cursor-pointer transition-colors h-full">
                            <CardHeader>
                                <CardTitle className="flex items-center gap-2">
                                    <AlertCircle className="h-5 w-5 text-orange-600" />
                                    WHT Summary
                                </CardTitle>
                                <CardDescription>
                                    Withholding tax by supplier
                                </CardDescription>
                            </CardHeader>
                            <CardContent>
                                <p className="text-sm text-muted-foreground">
                                    Summary of withholding tax deducted from suppliers (Goods: 3%, Services: 7.5%, Works: 5%)
                                </p>
                            </CardContent>
                        </Card>
                    </Link>

                    <Card className="opacity-60">
                        <CardHeader>
                            <CardTitle className="flex items-center gap-2">
                                <FileText className="h-5 w-5 text-gray-600" />
                                WHT Certificates
                            </CardTitle>
                            <CardDescription>
                                Generate WHT certificates
                            </CardDescription>
                        </CardHeader>
                        <CardContent>
                            <p className="text-sm text-muted-foreground">
                                Generate and print withholding tax certificates for suppliers (Coming Soon)
                            </p>
                        </CardContent>
                    </Card>
                </div>
            </div>

            {/* Tax Tools */}
            <div>
                <h2 className="text-xl font-semibold mb-4">Tax Tools</h2>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <Link href="/finance/tax/calculator">
                        <Card className="hover:bg-accent cursor-pointer transition-colors h-full">
                            <CardHeader>
                                <CardTitle className="flex items-center gap-2">
                                    <Calculator className="h-5 w-5 text-blue-600" />
                                    Tax Calculator
                                </CardTitle>
                                <CardDescription>
                                    Calculate Ghana taxes
                                </CardDescription>
                            </CardHeader>
                            <CardContent>
                                <p className="text-sm text-muted-foreground">
                                    Interactive calculator for VAT, NHIL, GETFL, and WHT on any transaction amount
                                </p>
                            </CardContent>
                        </Card>
                    </Link>

                    <Link href="/finance/tax/configuration">
                        <Card className="hover:bg-accent cursor-pointer transition-colors h-full">
                            <CardHeader>
                                <CardTitle className="flex items-center gap-2">
                                    <FileText className="h-5 w-5 text-purple-600" />
                                    Tax Configuration
                                </CardTitle>
                                <CardDescription>
                                    Manage tax types and rates
                                </CardDescription>
                            </CardHeader>
                            <CardContent>
                                <p className="text-sm text-muted-foreground">
                                    Configure tax types, rates, rules, and applicability for the Ghana tax system
                                </p>
                            </CardContent>
                        </Card>
                    </Link>
                </div>
            </div>

            {/* Information Card */}
            <Card className="bg-blue-50 border-blue-200">
                <CardHeader>
                    <CardTitle className="text-blue-900">Ghana Tax System</CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
                        <div>
                            <h4 className="font-semibold mb-2">Standard Taxes</h4>
                            <ul className="space-y-1 text-muted-foreground">
                                <li>• VAT: 15% (Simple on base amount)</li>
                                <li>• NHIL: 2.5% (Compound on base)</li>
                                <li>• GETFL: 2.5% (Compound on base + NHIL)</li>
                                <li>• COVID-19 Levy: 1% (Inactive)</li>
                            </ul>
                        </div>
                        <div>
                            <h4 className="font-semibold mb-2">Withholding Taxes</h4>
                            <ul className="space-y-1 text-muted-foreground">
                                <li>• WHT on Goods: 3% (Threshold: GHS 2,000)</li>
                                <li>• WHT on Services: 7.5% (Threshold: GHS 2,000)</li>
                                <li>• WHT on Works: 5% (Threshold: GHS 2,000)</li>
                            </ul>
                        </div>
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}
