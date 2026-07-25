'use client';

import { useEffect, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { taxDataService } from '@/services/finance/tax-data.service';
import { Tax, TaxCategory } from '@/types/tax';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import {
    Calculator,
    FileText,
    Settings,
    TrendingUp,
    AlertCircle,
    CheckCircle,
    XCircle,
    Layers
} from 'lucide-react';

export default function TaxConfigurationPage() {
    const pathname = usePathname() ?? '';
    const administrationMode = pathname.startsWith('/administration/finance');
    const configurationBasePath = administrationMode
        ? '/administration/finance/tax'
        : '/finance/tax/configuration';
    const [taxes, setTaxes] = useState<Tax[]>([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        loadTaxes();
    }, []);

    const loadTaxes = async () => {
        try {
            const data = await taxDataService.getTaxes();
            setTaxes(data);
        } catch (error) {
            console.error('Failed to load taxes:', error);
        } finally {
            setLoading(false);
        }
    };

    const activeTaxes = taxes.filter(t => t.isActive);
    const inactiveTaxes = taxes.filter(t => !t.isActive);
    const standardTaxes = taxes.filter(t => t.category === TaxCategory.Standard);
    const whtTaxes = taxes.filter(t => t.category === TaxCategory.Withholding);

    return (
        <div className="p-6 space-y-6">
            {/* Header */}
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-3xl font-bold">Tax Configuration</h1>
                    <p className="text-muted-foreground">Manage taxes, tax groups, and configuration for Ghana tax system</p>
                </div>
                <div className="flex gap-2">
                    <Link href="/finance/tax/calculator">
                        <Button variant="outline">
                            <Calculator className="mr-2 h-4 w-4" />
                            Tax Calculator
                        </Button>
                    </Link>
                    <Link href="/finance/tax/reports">
                        <Button variant="outline">
                            <FileText className="mr-2 h-4 w-4" />
                            Tax Reports
                        </Button>
                    </Link>
                    <Button variant="secondary" onClick={async () => {
                        if (confirm('Are you sure you want to seed default Ghana taxes? This may create duplicates if taxes already exist.')) {
                            try {
                                setLoading(true);
                                await taxDataService.seedGhanaTaxes();
                                await loadTaxes();
                                alert('Ghana taxes seeded successfully!');
                            } catch (e) {
                                console.error(e);
                                alert('Failed to seed taxes.');
                            } finally {
                                setLoading(false);
                            }
                        }
                    }}>
                        <Layers className="mr-2 h-4 w-4" />
                        Seed Ghana Taxes
                    </Button>
                </div>
            </div>

            {/* Summary Cards */}
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                        <CardTitle className="text-sm font-medium">Active Taxes</CardTitle>
                        <CheckCircle className="h-4 w-4 text-green-600" />
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{activeTaxes.length}</div>
                        <p className="text-xs text-muted-foreground">
                            {taxes.length} total taxes
                        </p>
                    </CardContent>
                </Card>

                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                        <CardTitle className="text-sm font-medium">Standard Taxes</CardTitle>
                        <TrendingUp className="h-4 w-4 text-purple-600" />
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{standardTaxes.length}</div>
                        <p className="text-xs text-muted-foreground">
                            Include VAT, Levies
                        </p>
                    </CardContent>
                </Card>

                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                        <CardTitle className="text-sm font-medium">Withholding Taxes</CardTitle>
                        <AlertCircle className="h-4 w-4 text-orange-600" />
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{whtTaxes.length}</div>
                        <p className="text-xs text-muted-foreground">
                            Goods, Services, Works
                        </p>
                    </CardContent>
                </Card>

                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                        <CardTitle className="text-sm font-medium">Inactive Taxes</CardTitle>
                        <XCircle className="h-4 w-4 text-gray-400" />
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{inactiveTaxes.length}</div>
                        <p className="text-xs text-muted-foreground">
                            Suspended or disabled
                        </p>
                    </CardContent>
                </Card>
            </div>

            {/* Quick Links */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <Link href={`${configurationBasePath}/taxes`}>
                    <Card className="hover:bg-accent cursor-pointer transition-colors">
                        <CardHeader>
                            <CardTitle className="flex items-center gap-2">
                                <Settings className="h-5 w-5" />
                                Taxes
                            </CardTitle>
                            <CardDescription>
                                Manage individual tax definitions (VAT, NHIL, WHT, etc.)
                            </CardDescription>
                        </CardHeader>
                        <CardContent>
                            <p className="text-sm text-muted-foreground">
                                {taxes.length} taxes configured
                            </p>
                        </CardContent>
                    </Card>
                </Link>

                <Link href={`${configurationBasePath}/groups`}>
                    <Card className="hover:bg-accent cursor-pointer transition-colors">
                        <CardHeader>
                            <CardTitle className="flex items-center gap-2">
                                <Layers className="h-5 w-5" />
                                Tax Groups
                            </CardTitle>
                            <CardDescription>
                                Configure tax bundles and compound calculations
                            </CardDescription>
                        </CardHeader>
                        <CardContent>
                            <p className="text-sm text-muted-foreground">
                                Manage VAT Schemes and WHT Groups
                            </p>
                        </CardContent>
                    </Card>
                </Link>

                <Link href={`${configurationBasePath}/rules`}>
                    <Card className="hover:bg-accent cursor-pointer transition-colors">
                        <CardHeader>
                            <CardTitle className="flex items-center gap-2">
                                <FileText className="h-5 w-5" />
                                Tax Rules
                            </CardTitle>
                            <CardDescription>
                                Rules for automatic tax group selection
                            </CardDescription>
                        </CardHeader>
                        <CardContent>
                            <p className="text-sm text-muted-foreground">
                                Configure context-based tax application
                            </p>
                        </CardContent>
                    </Card>
                </Link>
            </div>

            {/* Active Taxes */}
            <Card>
                <CardHeader>
                    <CardTitle>Active Taxes</CardTitle>
                    <CardDescription>Currently enabled taxes in the system</CardDescription>
                </CardHeader>
                <CardContent>
                    {loading ? (
                        <div className="text-center py-8 text-muted-foreground">Loading...</div>
                    ) : (
                        <div className="space-y-3">
                            {activeTaxes.map(tax => (
                                <div
                                    key={tax.id}
                                    className="flex items-center justify-between p-4 border rounded-lg hover:bg-accent transition-colors"
                                >
                                    <div className="flex-1">
                                        <div className="flex items-center gap-3">
                                            <h3 className="font-semibold">{tax.name}</h3>
                                            <Badge variant="outline">{tax.code}</Badge>
                                            <Badge variant="secondary">{tax.rate}%</Badge>
                                        </div>
                                        <div className="flex gap-2 mt-2">
                                            <Badge
                                                variant="outline"
                                                className={
                                                    tax.category === TaxCategory.Standard ? 'bg-blue-50 text-blue-700 border-blue-200' :
                                                        tax.category === TaxCategory.Withholding ? 'bg-orange-50 text-orange-700 border-orange-200' :
                                                            'bg-gray-50 text-gray-700 border-gray-200'
                                                }
                                            >
                                                {tax.category}
                                            </Badge>
                                            <Badge
                                                variant="outline"
                                                className="bg-green-50 text-green-700 border-green-200"
                                            >
                                                {tax.applicability}
                                            </Badge>
                                            {tax.thresholdAmount && (
                                                <Badge variant="outline">
                                                    Threshold: {tax.thresholdAmount.toLocaleString()}
                                                </Badge>
                                            )}
                                        </div>
                                    </div>
                                    <div className="flex items-center gap-2">
                                        <Link href={`${configurationBasePath}/taxes/${tax.id}`}>
                                            <Button variant="ghost" size="sm">
                                                View Details
                                            </Button>
                                        </Link>
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
