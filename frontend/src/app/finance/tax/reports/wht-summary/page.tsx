'use client';

import { useEffect, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { taxDataService } from '@/services/finance/tax-data.service';
import type { WHTSummaryEntry } from '@/types/tax';
import { Download, Filter, Search, FileText } from 'lucide-react';
import Link from 'next/link';

export default function WHTSummaryPage() {
    const [summary, setSummary] = useState<WHTSummaryEntry[]>([]);
    const [filteredSummary, setFilteredSummary] = useState<WHTSummaryEntry[]>([]);
    const [loading, setLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState('');
    const [startDate, setStartDate] = useState('2024-12-01');
    const [endDate, setEndDate] = useState('2024-12-31');

    useEffect(() => {
        loadSummary();
    }, []);

    useEffect(() => {
        filterSummary();
    }, [summary, searchTerm, startDate, endDate]);

    const loadSummary = async () => {
        try {
            const data = await taxDataService.getWHTSummary(startDate, endDate);
            setSummary(data);
        } catch (error) {
            console.error('Failed to load WHT summary:', error);
        } finally {
            setLoading(false);
        }
    };

    const filterSummary = () => {
        let filtered = summary;

        if (searchTerm) {
            const term = searchTerm.toLowerCase();
            filtered = filtered.filter(s =>
                s.supplierName.toLowerCase().includes(term) ||
                s.supplierTIN?.toLowerCase().includes(term)
            );
        }

        setFilteredSummary(filtered);
    };

    const formatCurrency = (amount: number) => {
        return `GHS ${amount.toLocaleString('en-GH', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    };

    const totals = {
        transactions: filteredSummary.reduce((sum, s) => sum + s.transactionCount, 0),
        grossAmount: filteredSummary.reduce((sum, s) => sum + s.grossAmount, 0),
        whtAmount: filteredSummary.reduce((sum, s) => sum + s.whtAmount, 0),
        netAmount: filteredSummary.reduce((sum, s) => sum + s.netAmount, 0),
    };

    const getTaxTypeColor = (taxType: string) => {
        switch (taxType) {
            case 'WHT-GOODS': return 'bg-blue-50 text-blue-700 border-blue-200';
            case 'WHT-SERVICES': return 'bg-purple-50 text-purple-700 border-purple-200';
            case 'WHT-WORKS': return 'bg-orange-50 text-orange-700 border-orange-200';
            default: return 'bg-gray-50 text-gray-700 border-gray-200';
        }
    };

    return (
        <div className="p-6 space-y-6">
            {/* Header */}
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-3xl font-bold">Withholding Tax Summary</h1>
                    <p className="text-muted-foreground">WHT deducted by supplier - December 2024</p>
                </div>
                <div className="flex gap-2">
                    <Link href="/finance/tax/reports">
                        <Button variant="outline">
                            All Reports
                        </Button>
                    </Link>
                    <Button variant="outline">
                        <Download className="mr-2 h-4 w-4" />
                        Export
                    </Button>
                </div>
            </div>

            {/* Filters */}
            <Card>
                <CardHeader>
                    <CardTitle className="flex items-center gap-2">
                        <Filter className="h-5 w-5" />
                        Filters
                    </CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                        <div>
                            <Label htmlFor="search">Search Supplier</Label>
                            <div className="relative mt-1">
                                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                                <Input
                                    id="search"
                                    placeholder="Name or TIN..."
                                    value={searchTerm}
                                    onChange={(e) => setSearchTerm(e.target.value)}
                                    className="pl-8"
                                />
                            </div>
                        </div>
                        <div>
                            <Label htmlFor="startDate">Start Date</Label>
                            <Input
                                id="startDate"
                                type="date"
                                value={startDate}
                                onChange={(e) => {
                                    setStartDate(e.target.value);
                                    loadSummary();
                                }}
                                className="mt-1"
                            />
                        </div>
                        <div>
                            <Label htmlFor="endDate">End Date</Label>
                            <Input
                                id="endDate"
                                type="date"
                                value={endDate}
                                onChange={(e) => {
                                    setEndDate(e.target.value);
                                    loadSummary();
                                }}
                                className="mt-1"
                            />
                        </div>
                        <div className="flex items-end">
                            <Button
                                variant="outline"
                                onClick={() => {
                                    setSearchTerm('');
                                    setStartDate('2024-12-01');
                                    setEndDate('2024-12-31');
                                    loadSummary();
                                }}
                                className="w-full"
                            >
                                Reset
                            </Button>
                        </div>
                    </div>
                </CardContent>
            </Card>

            {/* Summary Cards */}
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">Suppliers</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{filteredSummary.length}</div>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">Transactions</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{totals.transactions}</div>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">Gross Amount</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{formatCurrency(totals.grossAmount)}</div>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">Total WHT</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold text-orange-600">{formatCurrency(totals.whtAmount)}</div>
                    </CardContent>
                </Card>
            </div>

            {/* WHT Summary Table */}
            <Card>
                <CardHeader>
                    <CardTitle>WHT by Supplier</CardTitle>
                    <CardDescription>
                        Showing {filteredSummary.length} of {summary.length} suppliers
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    {loading ? (
                        <div className="text-center py-8 text-muted-foreground">Loading...</div>
                    ) : filteredSummary.length === 0 ? (
                        <div className="text-center py-8 text-muted-foreground">
                            No WHT transactions found matching your filters
                        </div>
                    ) : (
                        <div className="overflow-x-auto">
                            <table className="w-full">
                                <thead>
                                    <tr className="border-b">
                                        <th className="text-left p-3 font-semibold">Supplier</th>
                                        <th className="text-left p-3 font-semibold">TIN</th>
                                        <th className="text-left p-3 font-semibold">WHT Type</th>
                                        <th className="text-center p-3 font-semibold">Transactions</th>
                                        <th className="text-right p-3 font-semibold">Gross Amount</th>
                                        <th className="text-right p-3 font-semibold">WHT Rate</th>
                                        <th className="text-right p-3 font-semibold">WHT Amount</th>
                                        <th className="text-right p-3 font-semibold">Net Paid</th>
                                        <th className="text-center p-3 font-semibold">Certificate</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {filteredSummary.map((entry, index) => (
                                        <tr key={index} className="border-b hover:bg-accent">
                                            <td className="p-3 font-medium">{entry.supplierName}</td>
                                            <td className="p-3 text-sm text-muted-foreground">
                                                {entry.supplierTIN || 'N/A'}
                                            </td>
                                            <td className="p-3">
                                                <Badge variant="outline" className={getTaxTypeColor(entry.taxType)}>
                                                    {entry.taxType}
                                                </Badge>
                                            </td>
                                            <td className="p-3 text-center">{entry.transactionCount}</td>
                                            <td className="p-3 text-right">{formatCurrency(entry.grossAmount)}</td>
                                            <td className="p-3 text-right">{entry.whtRate}%</td>
                                            <td className="p-3 text-right font-semibold text-orange-600">
                                                {formatCurrency(entry.whtAmount)}
                                            </td>
                                            <td className="p-3 text-right">{formatCurrency(entry.netAmount)}</td>
                                            <td className="p-3 text-center">
                                                <Link href="/finance/tax/reports/wht-certificates">
                                                    <Button variant="ghost" size="sm" aria-label="Open WHT certificates">
                                                        <FileText className="h-4 w-4" />
                                                    </Button>
                                                </Link>
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                                <tfoot className="bg-muted/50">
                                    <tr className="font-bold">
                                        <td colSpan={3} className="p-3">TOTAL</td>
                                        <td className="p-3 text-center">{totals.transactions}</td>
                                        <td className="p-3 text-right">{formatCurrency(totals.grossAmount)}</td>
                                        <td className="p-3"></td>
                                        <td className="p-3 text-right text-orange-600">{formatCurrency(totals.whtAmount)}</td>
                                        <td className="p-3 text-right">{formatCurrency(totals.netAmount)}</td>
                                        <td className="p-3"></td>
                                    </tr>
                                </tfoot>
                            </table>
                        </div>
                    )}
                </CardContent>
            </Card>

            {/* WHT Information */}
            <Card className="bg-blue-50 border-blue-200">
                <CardHeader>
                    <CardTitle className="text-blue-900">Ghana WHT Rates</CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
                        <div className="bg-white p-4 rounded-lg border border-blue-200">
                            <div className="flex items-center gap-2 mb-2">
                                <div className="h-3 w-3 rounded-full bg-blue-500"></div>
                                <h4 className="font-semibold">WHT on Goods</h4>
                            </div>
                            <p className="text-muted-foreground">Rate: 3%</p>
                            <p className="text-muted-foreground">Threshold: GHS 2,000</p>
                        </div>
                        <div className="bg-white p-4 rounded-lg border border-purple-200">
                            <div className="flex items-center gap-2 mb-2">
                                <div className="h-3 w-3 rounded-full bg-purple-500"></div>
                                <h4 className="font-semibold">WHT on Services</h4>
                            </div>
                            <p className="text-muted-foreground">Rate: 7.5%</p>
                            <p className="text-muted-foreground">Threshold: GHS 2,000</p>
                        </div>
                        <div className="bg-white p-4 rounded-lg border border-orange-200">
                            <div className="flex items-center gap-2 mb-2">
                                <div className="h-3 w-3 rounded-full bg-orange-500"></div>
                                <h4 className="font-semibold">WHT on Works</h4>
                            </div>
                            <p className="text-muted-foreground">Rate: 5%</p>
                            <p className="text-muted-foreground">Threshold: GHS 2,000</p>
                        </div>
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
                                Detailed purchase transactions with VAT
                            </p>
                        </CardContent>
                    </Card>
                </Link>
                <Link href="/finance/tax/reports/vat-reconciliation">
                    <Card className="hover:bg-accent cursor-pointer transition-colors">
                        <CardContent className="p-6">
                            <h3 className="font-semibold mb-2">View VAT Reconciliation</h3>
                            <p className="text-sm text-muted-foreground">
                                Compare input and output VAT
                            </p>
                        </CardContent>
                    </Card>
                </Link>
            </div>
        </div>
    );
}
