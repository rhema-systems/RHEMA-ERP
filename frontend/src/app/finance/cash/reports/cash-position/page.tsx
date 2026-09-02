'use client';

import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { ArrowLeft, Wallet, Building2, AlertCircle } from 'lucide-react';
import { format } from 'date-fns';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import { formatCurrency } from '@/lib/utils';

import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import { DOCUMENT_TYPES, documentOutputService } from '@/services/document-output.service';
import { ReportPdfActions } from '@/components/finance/reports/ReportPdfActions';
import { useAuth } from '@/hooks/use-auth';

export default function CashPositionPage() {
    const router = useRouter();
    const { hasPermission } = useAuth();
    const canExport = hasPermission('Finance.Reports.Export');

    const { data: position, isLoading, error } = useQuery({
        queryKey: ['cash-position'],
        queryFn: () => cashManagementDataService.getCashPosition(),
    });

    if (isLoading) {
        return <CashPositionSkeleton />;
    }

    if (error || !position) {
        return (
            <div className="p-8">
                <Alert variant="destructive">
                    <AlertTitle>Error</AlertTitle>
                    <AlertDescription>Failed to load cash position data.</AlertDescription>
                </Alert>
            </div>
        );
    }

    return (
        <div className="p-8 max-w-[1600px] mx-auto space-y-8">
            <div className="flex items-center justify-between">
                <div className="flex items-center space-x-4">
                    <Button variant="ghost" size="icon" onClick={() => router.push('/finance/cash/accounts')}>
                        <ArrowLeft className="h-4 w-4" />
                    </Button>
                    <div>
                        <h1 className="text-3xl font-bold tracking-tight">Cash Position</h1>
                        <p className="text-muted-foreground">
                            Real-time overview of cash availability across all accounts.
                        </p>
                    </div>
                </div>
                {canExport && (
                    <ReportPdfActions
                        reportName="cash position report"
                        onDownloadPdf={() => documentOutputService.downloadReportDocument(
                            DOCUMENT_TYPES.financeCashPositionReport)}
                        onPrint={() => documentOutputService.printReportDocument(
                            DOCUMENT_TYPES.financeCashPositionReport)}
                    />
                )}
            </div>

            {/* Top Level Summary Cards */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
                <Card className="bg-primary text-primary-foreground">
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium opacity-90">Total Liquidity (Base)</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-3xl font-bold">{formatCurrency(position.totalBalance, position.currency)}</div>
                        <p className="text-xs opacity-75 mt-1">Aggregated across {position.accountCount} accounts</p>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">Checking Balance</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold text-blue-600">
                            {formatCurrency(position.byAccountType.find(t => t.type === 'Checking')?.balance || 0, position.currency)}
                        </div>
                        <p className="text-xs text-muted-foreground mt-1">Operational funds</p>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">Savings Balance</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold text-green-600">
                            {formatCurrency(position.byAccountType.find(t => t.type === 'Savings')?.balance || 0, position.currency)}
                        </div>
                        <p className="text-xs text-muted-foreground mt-1">Reserves</p>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">Foreign Currency Holding</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">
                            {position.byCurrency.length > 1 ? (position.byCurrency.length - 1) : 0}
                        </div>
                        <p className="text-xs text-muted-foreground mt-1">Currencies other than {position.currency}</p>
                    </CardContent>
                </Card>
            </div>

            {/* Detailed Breakdowns */}
            <div className="grid gap-6 md:grid-cols-2">
                {/* Breakdown by Currency */}
                <Card>
                    <CardHeader>
                        <CardTitle className="flex items-center gap-2">
                            <Wallet className="h-5 w-5" />
                            Breakdown by Currency
                        </CardTitle>
                        <CardDescription>Cash holdings grouped by currency</CardDescription>
                    </CardHeader>
                    <CardContent>
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead>Currency</TableHead>
                                    <TableHead className="text-right">Account Count</TableHead>
                                    <TableHead className="text-right">Balance ({position.currency} Base Eq)</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {position.byCurrency.map((item) => (
                                    <TableRow key={item.currency}>
                                        <TableCell className="font-medium">
                                            <Badge variant="outline">{item.currency}</Badge>
                                        </TableCell>
                                        <TableCell className="text-right">{item.count}</TableCell>
                                        <TableCell className="text-right font-bold">
                                            {formatCurrency(item.balance, item.currency)}
                                        </TableCell>
                                    </TableRow>
                                ))}
                            </TableBody>
                        </Table>
                    </CardContent>
                </Card>

                {/* Breakdown by Account Type */}
                <Card>
                    <CardHeader>
                        <CardTitle className="flex items-center gap-2">
                            <Building2 className="h-5 w-5" />
                            Breakdown by Account Type
                        </CardTitle>
                        <CardDescription>Distribution across facility types</CardDescription>
                    </CardHeader>
                    <CardContent>
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead>Account Type</TableHead>
                                    <TableHead className="text-right">Count</TableHead>
                                    <TableHead className="text-right">Balance (Base Eq)</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {position.byAccountType.map((item) => (
                                    <TableRow key={item.type}>
                                        <TableCell className="font-medium">{item.type}</TableCell>
                                        <TableCell className="text-right">{item.count}</TableCell>
                                        <TableCell className="text-right font-bold">
                                            {formatCurrency(item.balance, position.currency)}
                                        </TableCell>
                                    </TableRow>
                                ))}
                            </TableBody>
                        </Table>
                    </CardContent>
                </Card>
            </div>

            <Card className="bg-muted/30">
                <CardContent className="p-6">
                    <div className="flex items-center gap-4 text-sm text-muted-foreground">
                        <AlertCircle className="h-5 w-5" />
                        <p>
                            Note: All monetary balances are posted functional-currency ({position.currency}) GL amounts.
                            Currency rows identify the native currency of the underlying bank accounts; the values remain {position.currency} base equivalents and are not native-currency totals.
                        </p>
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}

function CashPositionSkeleton() {
    return (
        <div className="p-8 max-w-[1600px] mx-auto space-y-8">
            <div className="flex justify-between">
                <Skeleton className="h-10 w-64" />
                <Skeleton className="h-10 w-32" />
            </div>
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                <Skeleton className="h-32" />
                <Skeleton className="h-32" />
                <Skeleton className="h-32" />
                <Skeleton className="h-32" />
            </div>
            <div className="grid grid-cols-2 gap-6">
                <Skeleton className="h-64" />
                <Skeleton className="h-64" />
            </div>
        </div>
    )
}
