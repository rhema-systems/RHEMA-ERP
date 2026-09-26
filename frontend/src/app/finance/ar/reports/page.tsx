'use client';

import { useEffect, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
} from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { arService } from '@/services/ar-service';
import { formatCurrency } from '@/lib/utils';
import { format } from 'date-fns';
import { Skeleton } from '@/components/ui/skeleton';
import { PartnerStatementReport } from '@/components/finance/PartnerStatementReport';
import type {
    DetailedLedgerReport,
    LedgerPartnerOption,
} from '@/components/finance/PartnerDetailedLedgerReport';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from '@/components/ui/table';
import { useAuth } from '@/hooks/use-auth';
import { ArAgingExportButton } from '@/components/finance/ar/ArAgingExportButton';
import { ReportPdfActions } from '@/components/finance/reports/ReportPdfActions';

const REPORT_TABS = ['aging', 'statements'] as const;

function getReportTab(tab: string | null) {
    return REPORT_TABS.find((reportTab) => reportTab === tab) ?? 'aging';
}

export default function ArReportsPage() {
    const searchParams = useSearchParams();
    const tabParam = searchParams.get('tab');
    const [activeTab, setActiveTab] = useState(() => getReportTab(tabParam));

    useEffect(() => {
        setActiveTab(getReportTab(tabParam));
    }, [tabParam]);

    return (
        <div className="space-y-8 p-8 max-w-[1600px] mx-auto">
            <div>
                <h1 className="text-3xl font-bold tracking-tight">AR Reports</h1>
                <p className="text-muted-foreground mt-2">
                    Analyze your receivables and generate customer statements.
                </p>
            </div>

            <Tabs value={activeTab} onValueChange={(value) => setActiveTab(value as 'aging' | 'statements')} className="space-y-4">
                <TabsList>
                    <TabsTrigger value="aging">Aging Analysis</TabsTrigger>
                    <TabsTrigger value="statements">Customer Statements</TabsTrigger>
                </TabsList>

                <TabsContent value="aging">
                    <AgingReportView />
                </TabsContent>

                <TabsContent value="statements">
                    <CustomerStatementsView />
                </TabsContent>
            </Tabs>
        </div>
    );
}

function AgingReportView() {
    const { hasPermission } = useAuth();
    const canExport = hasPermission('Finance.Reports.Export');
    const [asOfDate, setAsOfDate] = useState(() => format(new Date(), 'yyyy-MM-dd'));

    const { data: agingReport, isLoading } = useQuery({
        queryKey: ['ar-aging-report', asOfDate],
        queryFn: () => arService.getAgingReport(asOfDate),
    });

    return (
        <Card>
            <CardHeader>
                <div className="flex items-center justify-between">
                    <div>
                        <CardTitle>Aged Receivables</CardTitle>
                        <CardDescription>Breakdown of outstanding balances by days overdue</CardDescription>
                    </div>
                    <div className="flex items-center space-x-2">
                        <Input
                            type="date"
                            aria-label="AR aging as-of date"
                            value={asOfDate}
                            onChange={(event) => event.target.value && setAsOfDate(event.target.value)}
                            className="w-full sm:w-[200px]"
                        />
                        {canExport && (
                            <>
                                <ArAgingExportButton asOfDate={asOfDate} isReportLoading={isLoading} />
                                <ReportPdfActions
                                    reportName="AR aging report"
                                    onDownloadPdf={() => arService.downloadAgingReportPdf(asOfDate)}
                                    onPrint={() => arService.printAgingReport(asOfDate)}
                                    disabled={isLoading || !agingReport}
                                />
                            </>
                        )}
                    </div>
                </div>
            </CardHeader>
            <CardContent>
                {isLoading ? (
                    <div className="space-y-4">
                        <Skeleton className="h-12 w-full" />
                        <Skeleton className="h-64 w-full" />
                    </div>
                ) : !agingReport ? (
                    <div className="text-center py-12 text-muted-foreground">No data available</div>
                ) : (
                    <div className="space-y-6">
                        {/* Summary Bar */}
                        <div className="grid grid-cols-5 gap-4">
                            {agingReport.buckets.map((bucket, i) => (
                                <Card key={i} className="bg-muted/30">
                                    <CardContent className="p-4 text-center">
                                        <div className="text-sm font-medium text-muted-foreground mb-1">{bucket.bucketName}</div>
                                        <div className="text-xl font-bold">{formatCurrency(bucket.amount, agingReport.currencyCode)}</div>
                                        <div className="text-xs text-muted-foreground mt-1">{bucket.customerCount} customers</div>
                                    </CardContent>
                                </Card>
                            ))}
                        </div>

                        {/* Detailed Table (Placeholder structure as real detail data might be needed from API) */}
                        <div className="border rounded-md">
                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHead>Bucket</TableHead>
                                        <TableHead className="text-right">Balance</TableHead>
                                        <TableHead className="text-right">Count</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {agingReport.buckets.map((bucket, i) => (
                                        <TableRow key={i}>
                                            <TableCell className="font-medium">{bucket.bucketName}</TableCell>
                                            <TableCell className="text-right">{formatCurrency(bucket.amount, agingReport.currencyCode)}</TableCell>
                                            <TableCell className="text-right">{bucket.customerCount}</TableCell>
                                        </TableRow>
                                    ))}
                                    <TableRow className="bg-muted/50 font-bold">
                                        <TableCell>Total</TableCell>
                                        <TableCell className="text-right">{formatCurrency(agingReport.summary.grandTotal, agingReport.currencyCode)}</TableCell>
                                        <TableCell className="text-right"></TableCell>
                                    </TableRow>
                                </TableBody>
                            </Table>
                        </div>
                    </div>
                )}
            </CardContent>
        </Card>
    );
}

function CustomerStatementsView() {
    const { hasPermission } = useAuth();
    const canExport = hasPermission('Finance.Reports.Export');
    const [partners, setPartners] = useState<LedgerPartnerOption[]>([]);
    const [partnersLoading, setPartnersLoading] = useState(true);

    useEffect(() => {
        let isMounted = true;

        const loadCustomers = async () => {
            setPartnersLoading(true);
            try {
                const result = await arService.getCustomers({ page: 1, pageSize: 500, includeBalances: false });
                if (!isMounted) return;

                setPartners(
                    result.items
                        .map((customer) => ({
                            id: customer.id,
                            code: customer.customerCode,
                            name: customer.customerName,
                            currencyCode: customer.currencyCode,
                        }))
                        .sort((a, b) => a.name.localeCompare(b.name))
                );
            } catch (error) {
                console.error('Failed to load customer statement partners', error);
                if (isMounted) setPartners([]);
            } finally {
                if (isMounted) setPartnersLoading(false);
            }
        };

        loadCustomers();

        return () => {
            isMounted = false;
        };
    }, []);

    return (
        <PartnerStatementReport
            title="Customer Statements"
            description="Generate receivable statements of account with opening balance, period movements, and closing balance."
            partnerLabel="Customer"
            partnerPluralLabel="Customers"
            currencyToggleLabel="Show transactions in customer currency where available"
            exportFilePrefix="customer-statement"
            partners={partners}
            partnersLoading={partnersLoading}
            canExport={canExport}
            loadReport={async (params): Promise<DetailedLedgerReport> => {
                const report = await arService.getCustomerDetailedLedger({
                    fromDate: params.fromDate,
                    toDate: params.toDate,
                    businessPartnerIds: params.partnerIds,
                    showCustomerCurrency: params.showPartnerCurrency,
                });

                return {
                    fromDate: report.fromDate,
                    toDate: report.toDate,
                    currencyCode: report.currencyCode,
                    totalOpeningBalance: report.totalOpeningBalance,
                    totalDebits: report.totalDebits,
                    totalCredits: report.totalCredits,
                    totalClosingBalance: report.totalClosingBalance,
                    currencyTotals: report.currencyTotals ?? [],
                    warnings: report.warnings ?? [],
                    accounts: report.customers.map((customer) => ({
                        id: customer.businessPartnerId,
                        code: customer.customerCode,
                        name: customer.customerName,
                        currencyCode: customer.currencyCode,
                        openingBalance: customer.openingBalance,
                        totalDebits: customer.totalDebits,
                        totalCredits: customer.totalCredits,
                        closingBalance: customer.closingBalance,
                        lines: customer.lines,
                    })),
                };
            }}
            downloadCsv={(params) => arService.downloadCustomerStatementCsv({
                fromDate: params.fromDate,
                toDate: params.toDate,
                businessPartnerIds: params.partnerIds,
                showCustomerCurrency: params.showPartnerCurrency,
            })}
            downloadPdf={(params) => arService.downloadCustomerStatementPdf({
                fromDate: params.fromDate,
                toDate: params.toDate,
                businessPartnerIds: params.partnerIds,
                showCustomerCurrency: params.showPartnerCurrency,
            })}
            printPdf={(params) => arService.printCustomerStatement({
                fromDate: params.fromDate,
                toDate: params.toDate,
                businessPartnerIds: params.partnerIds,
                showCustomerCurrency: params.showPartnerCurrency,
            })}
        />
    )
}
