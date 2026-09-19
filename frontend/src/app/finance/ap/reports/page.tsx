'use client';

import { useEffect, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import {
    AlertCircle,
    Download,
} from 'lucide-react';
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
import { accountsPayableService } from '@/services/accountsPayableService';
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
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { useAuth } from '@/hooks/use-auth';
import { Badge } from '@/components/ui/badge';
import type { VendorInvoiceMatchExceptionStatus } from '@/types/ap';
import { ProcurementFinanceReconciliation } from '@/components/finance/ProcurementFinanceReconciliation';
import { ApAgingExportButton } from '@/components/finance/ap/ApAgingExportButton';
import { ReportPdfActions } from '@/components/finance/reports/ReportPdfActions';
import { useTenant } from '@/contexts/TenantContext';
import {
    apAgingReportQueryKey,
    apCashRequirementsQueryKey,
} from '@/lib/finance/ap-report-query-keys';

const REPORT_TABS = ['aging', 'cash', 'statements', 'match-exceptions', 'procurement-reconciliation'] as const;

function getReportTab(tab: string | null) {
    return REPORT_TABS.find((reportTab) => reportTab === tab) ?? 'aging';
}

function getReportErrorMessage(error: unknown) {
    const message = error instanceof Error ? error.message : '';

    if (message.includes('SubledgerUnappliedSettlementBalances')) {
        return 'The AP settlement read-model schema is incomplete. The database is missing SubledgerUnappliedSettlementBalances; apply the pending finance settlement migration before rerunning this report.';
    }

    return message || 'The report could not be loaded.';
}

export default function ApReportsPage() {
    const searchParams = useSearchParams();
    const tabParam = searchParams.get('tab');
    const [activeTab, setActiveTab] = useState(() => getReportTab(tabParam));

    useEffect(() => {
        setActiveTab(getReportTab(tabParam));
    }, [tabParam]);

    return (
        <div className="space-y-8 p-8 max-w-[1600px] mx-auto">
            <div>
                <h1 className="text-3xl font-bold tracking-tight">AP Reports</h1>
                <p className="text-muted-foreground mt-2">
                    Analyze your payables, cash requirements, and generate supplier statements.
                </p>
            </div>

            <Tabs value={activeTab} onValueChange={(value) => setActiveTab(value as typeof activeTab)} className="space-y-4">
                <TabsList>
                    <TabsTrigger value="aging">AP Aging Analysis</TabsTrigger>
                    <TabsTrigger value="cash">Cash Requirements</TabsTrigger>
                    <TabsTrigger value="statements">Supplier Statements</TabsTrigger>
                    <TabsTrigger value="match-exceptions">Match Exceptions</TabsTrigger>
                    <TabsTrigger value="procurement-reconciliation">Procurement Reconciliation</TabsTrigger>
                </TabsList>

                <TabsContent value="aging">
                    <ApAgingReportView />
                </TabsContent>

                <TabsContent value="cash">
                    <CashRequirementsView />
                </TabsContent>

                <TabsContent value="statements">
                    <SupplierStatementsView />
                </TabsContent>

                <TabsContent value="match-exceptions">
                    <MatchExceptionReportView />
                </TabsContent>

                <TabsContent value="procurement-reconciliation">
                    <ProcurementFinanceReconciliation />
                </TabsContent>
            </Tabs>
        </div>
    );
}

function MatchExceptionReportView() {
    const { hasPermission } = useAuth();
    const canExport = hasPermission('Finance.Reports.Export');
    const today = format(new Date(), 'yyyy-MM-dd');
    const thirtyDaysAgo = format(new Date(Date.now() - 30 * 24 * 60 * 60 * 1000), 'yyyy-MM-dd');
    const [fromDate, setFromDate] = useState(thirtyDaysAgo);
    const [toDate, setToDate] = useState(today);
    const [status, setStatus] = useState<VendorInvoiceMatchExceptionStatus | ''>('');
    const [downloading, setDownloading] = useState(false);
    const query = useQuery({
        queryKey: ['ap-match-exception-report', fromDate, toDate, status],
        queryFn: () => accountsPayableService.getThreeWayMatchExceptionReport({
            fromDate,
            toDate,
            status: status || undefined,
        }),
    });

    const download = async () => {
        setDownloading(true);
        try {
            const blob = await accountsPayableService.downloadThreeWayMatchExceptionReport({
                fromDate,
                toDate,
                status: status || undefined,
            });
            const url = URL.createObjectURL(blob);
            const anchor = document.createElement('a');
            anchor.href = url;
            anchor.download = `ap-match-exceptions-${today}.csv`;
            anchor.click();
            URL.revokeObjectURL(url);
        } finally {
            setDownloading(false);
        }
    };

    return (
        <Card>
            <CardHeader>
                <div className="flex flex-wrap items-start justify-between gap-4">
                    <div>
                        <CardTitle>Three-way-match exception register</CardTitle>
                        <CardDescription>AP-006 approvals, expiry, evidence lineage, and corrective-action follow-up.</CardDescription>
                    </div>
                    <div className="flex flex-wrap gap-2">
                        <input aria-label="Match exception from date" className="h-10 rounded-md border bg-background px-3 text-sm" type="date" value={fromDate} onChange={event => setFromDate(event.target.value)} />
                        <input aria-label="Match exception to date" className="h-10 rounded-md border bg-background px-3 text-sm" type="date" value={toDate} onChange={event => setToDate(event.target.value)} />
                        <select aria-label="Match exception status" className="h-10 rounded-md border bg-background px-3 text-sm" value={status} onChange={event => setStatus(event.target.value as VendorInvoiceMatchExceptionStatus | '')}>
                            <option value="">All statuses</option>
                            <option value="PendingApproval">Pending approval</option>
                            <option value="Approved">Approved</option>
                            <option value="Rejected">Rejected</option>
                            <option value="Cancelled">Cancelled</option>
                            <option value="Expired">Expired</option>
                        </select>
                        {canExport && (
                            <>
                                <Button variant="outline" disabled={downloading || query.isLoading} onClick={download}>
                                    <Download className="mr-2 h-4 w-4" /> {downloading ? 'Exporting…' : 'Export CSV'}
                                </Button>
                                <ReportPdfActions
                                    reportName="AP match exception report"
                                    onDownloadPdf={() => accountsPayableService.downloadMatchExceptionReportPdf({
                                        fromDate,
                                        toDate,
                                        status: status || undefined,
                                    })}
                                    onPrint={() => accountsPayableService.printMatchExceptionReport({
                                        fromDate,
                                        toDate,
                                        status: status || undefined,
                                    })}
                                    disabled={query.isLoading || !query.data}
                                />
                            </>
                        )}
                    </div>
                </div>
            </CardHeader>
            <CardContent className="space-y-5">
                {query.isLoading ? (
                    <Skeleton className="h-64 w-full" />
                ) : query.isError ? (
                    <Alert variant="destructive">
                        <AlertCircle className="h-4 w-4" />
                        <AlertTitle>Unable to load AP-006 register</AlertTitle>
                        <AlertDescription>{getReportErrorMessage(query.error)}</AlertDescription>
                    </Alert>
                ) : query.data && (
                    <>
                        <div className="grid gap-3 md:grid-cols-4">
                            <Metric label="Total exceptions" value={query.data.totalCount} />
                            <Metric label="Currently approved" value={query.data.approvedCount} />
                            <Metric label="Expired" value={query.data.expiredCount} />
                            <Metric label="Open corrective actions" value={query.data.openCorrectiveActionCount} />
                        </div>
                        <div className="overflow-x-auto rounded-md border">
                            <Table>
                                <TableHeader><TableRow>
                                    <TableHead>Invoice / supplier</TableHead><TableHead>PO</TableHead><TableHead>Status</TableHead>
                                    <TableHead>Variance</TableHead><TableHead>Root cause</TableHead><TableHead>Corrective action</TableHead>
                                    <TableHead>Evidence</TableHead>
                                </TableRow></TableHeader>
                                <TableBody>
                                    {query.data.rows.map(row => (
                                        <TableRow key={row.exceptionId}>
                                            <TableCell><div className="font-medium">{row.invoiceNumber}</div><div className="text-xs text-muted-foreground">{row.supplierName}</div></TableCell>
                                            <TableCell>{row.purchaseOrderNumber}</TableCell>
                                            <TableCell><Badge variant="outline">{row.status}</Badge></TableCell>
                                            <TableCell>{row.varianceType}<div className="text-xs text-muted-foreground">{row.maximumVariancePercentage}% max</div></TableCell>
                                            <TableCell>{row.rootCauseCategory}<div className="max-w-72 text-xs text-muted-foreground">{row.rootCauseDescription}</div></TableCell>
                                            <TableCell>{row.correctiveActionOwnerName}<div className="text-xs text-muted-foreground">{row.correctiveActionStatus} · due {format(new Date(row.correctiveActionDueAtUtc), 'PP')}</div></TableCell>
                                            <TableCell>{row.evidenceCount}</TableCell>
                                        </TableRow>
                                    ))}
                                    {query.data.rows.length === 0 && <TableRow><TableCell colSpan={7} className="py-8 text-center text-muted-foreground">No AP-006 exceptions in this period.</TableCell></TableRow>}
                                </TableBody>
                            </Table>
                        </div>
                    </>
                )}
            </CardContent>
        </Card>
    );
}

function Metric({ label, value }: { label: string; value: number }) {
    return <div className="rounded-md border p-4"><div className="text-xs text-muted-foreground">{label}</div><div className="text-2xl font-bold">{value}</div></div>;
}

function ApAgingReportView() {
    const { currentTenantCode, isLoadingTenants } = useTenant();
    const { hasPermission } = useAuth();
    const canExport = hasPermission('Finance.Reports.Export');
    const [asOfDate, setAsOfDate] = useState(() => format(new Date(), 'yyyy-MM-dd'));

    const { data: agingReport, isLoading, isError, error } = useQuery({
        queryKey: apAgingReportQueryKey(currentTenantCode, asOfDate),
        queryFn: () => accountsPayableService.getAgingReport(asOfDate),
        enabled: !isLoadingTenants && Boolean(currentTenantCode),
    });

    return (
        <Card>
            <CardHeader>
                <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                    <div className="min-w-0">
                        <CardTitle>Aged Payables (AP Aging)</CardTitle>
                        <CardDescription>Breakdown of outstanding balances to suppliers by days overdue</CardDescription>
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
                        <Input
                            type="date"
                            aria-label="AP aging as-of date"
                            value={asOfDate}
                            onChange={(event) => event.target.value && setAsOfDate(event.target.value)}
                            className="w-full sm:w-[200px]"
                        />
                        {canExport && (
                            <>
                                <ApAgingExportButton asOfDate={asOfDate} isReportLoading={isLoading} />
                                <ReportPdfActions
                                    reportName="AP aging report"
                                    onDownloadPdf={() => accountsPayableService.downloadAgingReportPdf(asOfDate)}
                                    onPrint={() => accountsPayableService.printAgingReport(asOfDate)}
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
                ) : isError ? (
                    <Alert variant="destructive">
                        <AlertCircle className="h-4 w-4" />
                        <AlertTitle>Unable to load AP aging</AlertTitle>
                        <AlertDescription>{getReportErrorMessage(error)}</AlertDescription>
                    </Alert>
                ) : !agingReport ? (
                    <div className="text-center py-12 text-muted-foreground">No data available</div>
                ) : (
                    <div className="space-y-6">
                        {/* Summary Bar */}
                        <div className="grid grid-cols-5 gap-4">
                            <Card className="bg-muted/30">
                                <CardContent className="p-4 text-center">
                                    <div className="text-sm font-medium text-muted-foreground mb-1">Current</div>
                                    <div className="text-xl font-bold text-green-600">{formatCurrency(agingReport.current, agingReport.currencyCode)}</div>
                                </CardContent>
                            </Card>
                            <Card className="bg-muted/30">
                                <CardContent className="p-4 text-center">
                                    <div className="text-sm font-medium text-muted-foreground mb-1">1 - 30 Days</div>
                                    <div className="text-xl font-bold text-amber-500">{formatCurrency(agingReport.thirtyDays, agingReport.currencyCode)}</div>
                                </CardContent>
                            </Card>
                            <Card className="bg-muted/30">
                                <CardContent className="p-4 text-center">
                                    <div className="text-sm font-medium text-muted-foreground mb-1">31 - 60 Days</div>
                                    <div className="text-xl font-bold text-amber-600">{formatCurrency(agingReport.sixtyDays, agingReport.currencyCode)}</div>
                                </CardContent>
                            </Card>
                            <Card className="bg-muted/30">
                                <CardContent className="p-4 text-center">
                                    <div className="text-sm font-medium text-muted-foreground mb-1">61 - 90+ Days</div>
                                    <div className="text-xl font-bold text-red-600">{formatCurrency(agingReport.ninetyPlusDays, agingReport.currencyCode)}</div>
                                </CardContent>
                            </Card>
                            <Card className="bg-primary/5 border-primary/20">
                                <CardContent className="p-4 text-center">
                                    <div className="text-sm font-medium text-primary mb-1">Total Outstanding</div>
                                    <div className="text-2xl font-black text-primary">{formatCurrency(agingReport.totalOutstanding, agingReport.currencyCode)}</div>
                                    <div className="text-xs text-muted-foreground mt-1">{agingReport.totalSuppliers} suppliers</div>
                                </CardContent>
                            </Card>
                        </div>

                        {/* Detailed Table */}
                        <div className="border rounded-md">
                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHead>Supplier</TableHead>
                                        <TableHead className="text-right">Current</TableHead>
                                        <TableHead className="text-right">1-30 Days</TableHead>
                                        <TableHead className="text-right">31-60 Days</TableHead>
                                        <TableHead className="text-right">61-90+ Days</TableHead>
                                        <TableHead className="text-right">Total Balance</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {agingReport.supplierDetails?.map((supplier, i) => (
                                        <TableRow key={i}>
                                            <TableCell className="font-medium">
                                                {supplier.supplierName}
                                                <div className="text-xs text-muted-foreground">{supplier.invoiceCount} invoices</div>
                                            </TableCell>
                                            <TableCell className="text-right">{supplier.current > 0 ? formatCurrency(supplier.current, agingReport.currencyCode) : '-'}</TableCell>
                                            <TableCell className="text-right">{supplier.thirtyDays > 0 ? formatCurrency(supplier.thirtyDays, agingReport.currencyCode) : '-'}</TableCell>
                                            <TableCell className="text-right">{supplier.sixtyDays > 0 ? formatCurrency(supplier.sixtyDays, agingReport.currencyCode) : '-'}</TableCell>
                                            <TableCell className="text-right">{supplier.ninetyPlusDays > 0 ? formatCurrency(supplier.ninetyPlusDays, agingReport.currencyCode) : '-'}</TableCell>
                                            <TableCell className="text-right font-bold text-primary">{formatCurrency(supplier.totalOutstanding, agingReport.currencyCode)}</TableCell>
                                        </TableRow>
                                    ))}
                                    {(!agingReport.supplierDetails || agingReport.supplierDetails.length === 0) && (
                                        <TableRow>
                                            <TableCell colSpan={6} className="text-center py-6 text-muted-foreground">
                                                No suppliers with outstanding balances.
                                            </TableCell>
                                        </TableRow>
                                    )}
                                    {agingReport.supplierDetails && agingReport.supplierDetails.length > 0 && (
                                        <TableRow className="bg-muted/50 font-bold">
                                            <TableCell>Total</TableCell>
                                            <TableCell className="text-right">{formatCurrency(agingReport.current, agingReport.currencyCode)}</TableCell>
                                            <TableCell className="text-right">{formatCurrency(agingReport.thirtyDays, agingReport.currencyCode)}</TableCell>
                                            <TableCell className="text-right">{formatCurrency(agingReport.sixtyDays, agingReport.currencyCode)}</TableCell>
                                            <TableCell className="text-right">{formatCurrency(agingReport.ninetyPlusDays, agingReport.currencyCode)}</TableCell>
                                            <TableCell className="text-right text-primary text-lg">{formatCurrency(agingReport.totalOutstanding, agingReport.currencyCode)}</TableCell>
                                        </TableRow>
                                    )}
                                </TableBody>
                            </Table>
                        </div>
                    </div>
                )}
            </CardContent>
        </Card>
    );
}

function CashRequirementsView() {
    const { currentTenantCode, isLoadingTenants } = useTenant();
    const { hasPermission } = useAuth();
    const canExport = hasPermission('Finance.Reports.Export');
    const [asOfDate, setAsOfDate] = useState(() => format(new Date(), 'yyyy-MM-dd'));

    const { data: forecastReport, isLoading } = useQuery({
        queryKey: apCashRequirementsQueryKey(currentTenantCode, asOfDate),
        queryFn: () => accountsPayableService.getCashRequirementForecast(asOfDate),
        enabled: !isLoadingTenants && Boolean(currentTenantCode),
    });

    return (
        <Card>
            <CardHeader>
                <div className="flex items-center justify-between">
                    <div>
                        <CardTitle>Cash Requirements Forecast</CardTitle>
                        <CardDescription>Estimated cash needed to pay obligations over upcoming periods</CardDescription>
                    </div>
                    <div className="flex items-center space-x-2">
                        <Input
                            type="date"
                            aria-label="Cash requirements as-of date"
                            value={asOfDate}
                            onChange={(event) => event.target.value && setAsOfDate(event.target.value)}
                            className="w-[200px]"
                        />
                        {canExport && (
                            <ReportPdfActions
                                reportName="AP cash requirements forecast"
                                onDownloadPdf={() => accountsPayableService.downloadCashRequirementsPdf(asOfDate)}
                                onPrint={() => accountsPayableService.printCashRequirements(asOfDate)}
                                disabled={isLoading || !forecastReport}
                            />
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
                ) : !forecastReport ? (
                    <div className="text-center py-12 text-muted-foreground">No data available</div>
                ) : (
                    <div className="space-y-6">
                        <div className="flex gap-8 mb-6">
                            <div>
                                <h3 className="text-sm font-semibold text-muted-foreground uppercase tracking-wider">Total Payable</h3>
                                <p className="text-3xl font-bold">{formatCurrency(forecastReport.totalPayable, forecastReport.currencyCode)}</p>
                            </div>
                            <div>
                                <h3 className="text-sm font-semibold text-red-500 uppercase tracking-wider">Overdue</h3>
                                <p className="text-3xl font-bold text-red-600">{formatCurrency(forecastReport.overdueAmount, forecastReport.currencyCode)}</p>
                            </div>
                        </div>

                        <div className="border rounded-md">
                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHead>Period</TableHead>
                                        <TableHead>Date Range</TableHead>
                                        <TableHead className="text-right">Invoices</TableHead>
                                        <TableHead className="text-right">Available Discounts</TableHead>
                                        <TableHead className="text-right">Amount Required</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {forecastReport.periods?.map((period, i) => (
                                        <TableRow key={i}>
                                            <TableCell className="font-semibold">{period.period}</TableCell>
                                            <TableCell className="text-muted-foreground text-sm">
                                                {format(new Date(period.periodStart), 'MMM dd')} - {format(new Date(period.periodEnd), 'MMM dd, yyyy')}
                                            </TableCell>
                                            <TableCell className="text-right">{period.invoiceCount}</TableCell>
                                            <TableCell className="text-right text-green-600">{period.discountAvailable > 0 ? formatCurrency(period.discountAvailable, forecastReport.currencyCode) : '-'}</TableCell>
                                            <TableCell className="text-right font-medium">{formatCurrency(period.amountDue, forecastReport.currencyCode)}</TableCell>
                                        </TableRow>
                                    ))}
                                    {(!forecastReport.periods || forecastReport.periods.length === 0) && (
                                        <TableRow>
                                            <TableCell colSpan={5} className="text-center py-6 text-muted-foreground">
                                                No upcoming payments required.
                                            </TableCell>
                                        </TableRow>
                                    )}
                                </TableBody>
                            </Table>
                        </div>
                    </div>
                )}
            </CardContent>
        </Card>
    );
}

function SupplierStatementsView() {
    const { hasPermission } = useAuth();
    const canExport = hasPermission('Finance.Reports.Export');
    const [partners, setPartners] = useState<LedgerPartnerOption[]>([]);
    const [partnersLoading, setPartnersLoading] = useState(true);

    useEffect(() => {
        let isMounted = true;

        const loadPartners = async () => {
            setPartnersLoading(true);
            try {
                const allPartners = await accountsPayableService.getInvoiceSupplierEntryOptions();
                if (!isMounted) return;

                setPartners(
                    allPartners
                        .map((partner) => ({
                            id: partner.id,
                            code: partner.code,
                            name: partner.name,
                            currencyCode: partner.currency ?? undefined,
                        }))
                        .sort((a, b) => a.name.localeCompare(b.name))
                );
            } catch (error) {
                console.error('Failed to load supplier statement partners', error);
                if (isMounted) setPartners([]);
            } finally {
                if (isMounted) setPartnersLoading(false);
            }
        };

        loadPartners();

        return () => {
            isMounted = false;
        };
    }, []);

    return (
        <PartnerStatementReport
            title="Supplier Statements"
            description="Generate payable statements of account with opening balance, period movements, and closing balance."
            partnerLabel="Supplier"
            partnerPluralLabel="Suppliers"
            currencyToggleLabel="Show transactions in supplier currency where available"
            exportFilePrefix="supplier-statement"
            partners={partners}
            partnersLoading={partnersLoading}
            loadReport={async (params): Promise<DetailedLedgerReport> => {
                const report = await accountsPayableService.getSupplierDetailedLedger({
                    fromDate: params.fromDate,
                    toDate: params.toDate,
                    supplierIds: params.partnerIds,
                    showSupplierCurrency: params.showPartnerCurrency,
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
                    accounts: report.suppliers.map((supplier) => ({
                        id: supplier.businessPartnerId ?? supplier.supplierId,
                        code: supplier.supplierCode,
                        name: supplier.supplierName,
                        currencyCode: supplier.currencyCode,
                        openingBalance: supplier.openingBalance,
                        totalDebits: supplier.totalDebits,
                        totalCredits: supplier.totalCredits,
                        closingBalance: supplier.closingBalance,
                        lines: supplier.lines,
                    })),
                };
            }}
            downloadCsv={(params) => accountsPayableService.downloadSupplierStatementCsv({
                fromDate: params.fromDate,
                toDate: params.toDate,
                supplierIds: params.partnerIds,
                showSupplierCurrency: params.showPartnerCurrency,
            })}
            downloadPdf={(params) => accountsPayableService.downloadSupplierStatementDocument({
                fromDate: params.fromDate,
                toDate: params.toDate,
                supplierIds: params.partnerIds,
                showSupplierCurrency: params.showPartnerCurrency,
                format: 'pdf',
            })}
            printPdf={(params) => accountsPayableService.printSupplierStatementDocument({
                fromDate: params.fromDate,
                toDate: params.toDate,
                supplierIds: params.partnerIds,
                showSupplierCurrency: params.showPartnerCurrency,
            })}
            downloadXlsx={(params) => accountsPayableService.downloadSupplierStatementDocument({
                fromDate: params.fromDate,
                toDate: params.toDate,
                supplierIds: params.partnerIds,
                showSupplierCurrency: params.showPartnerCurrency,
                format: 'xlsx',
            })}
            canExport={canExport}
        />
    )
}
