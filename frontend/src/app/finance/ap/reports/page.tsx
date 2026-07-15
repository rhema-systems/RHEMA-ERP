'use client';

import { useEffect, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import {
    Calendar as CalendarIcon,
    Download,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
} from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
    Popover,
    PopoverContent,
    PopoverTrigger,
} from '@/components/ui/popover';
import { Calendar } from '@/components/ui/calendar';
import { accountsPayableService } from '@/services/accountsPayableService';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';
import { formatCurrency, cn } from '@/lib/utils';
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

const REPORT_TABS = ['aging', 'cash', 'statements'] as const;
const supplierPartnerTypes = new Set(['supplier', 'contractor', 'both']);

const isSupplierPartner = (partner: BusinessPartnerDto) =>
    supplierPartnerTypes.has((partner.partnerType ?? '').toLowerCase());

function getReportTab(tab: string | null) {
    return REPORT_TABS.find((reportTab) => reportTab === tab) ?? 'aging';
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

            <Tabs value={activeTab} onValueChange={(value) => setActiveTab(value as 'aging' | 'cash' | 'statements')} className="space-y-4">
                <TabsList>
                    <TabsTrigger value="aging">AP Aging Analysis</TabsTrigger>
                    <TabsTrigger value="cash">Cash Requirements</TabsTrigger>
                    <TabsTrigger value="statements">Supplier Statements</TabsTrigger>
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
            </Tabs>
        </div>
    );
}

function ApAgingReportView() {
    const [asOfDate, setAsOfDate] = useState<Date>(new Date());

    const { data: agingReport, isLoading } = useQuery({
        queryKey: ['ap-aging-report', asOfDate],
        queryFn: () => accountsPayableService.getAgingReport(format(asOfDate, 'yyyy-MM-dd')),
    });

    return (
        <Card>
            <CardHeader>
                <div className="flex items-center justify-between">
                    <div>
                        <CardTitle>Aged Payables (AP Aging)</CardTitle>
                        <CardDescription>Breakdown of outstanding balances to suppliers by days overdue</CardDescription>
                    </div>
                    <div className="flex items-center space-x-2">
                        <Popover>
                            <PopoverTrigger asChild>
                                <Button
                                    variant="outline"
                                    className={cn(
                                        "w-[200px] justify-start text-left font-normal",
                                        !asOfDate && "text-muted-foreground"
                                    )}
                                >
                                    <CalendarIcon className="mr-2 h-4 w-4" />
                                    {asOfDate ? format(asOfDate, "PPP") : <span>As of Date</span>}
                                </Button>
                            </PopoverTrigger>
                            <PopoverContent className="w-auto p-0">
                                <Calendar
                                    mode="single"
                                    selected={asOfDate}
                                    onSelect={(date) => date && setAsOfDate(date)}
                                    initialFocus
                                />
                            </PopoverContent>
                        </Popover>
                        <Button variant="outline" size="icon">
                            <Download className="h-4 w-4" />
                        </Button>
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
                            <Card className="bg-muted/30">
                                <CardContent className="p-4 text-center">
                                    <div className="text-sm font-medium text-muted-foreground mb-1">Current</div>
                                    <div className="text-xl font-bold text-green-600">{formatCurrency(agingReport.current)}</div>
                                </CardContent>
                            </Card>
                            <Card className="bg-muted/30">
                                <CardContent className="p-4 text-center">
                                    <div className="text-sm font-medium text-muted-foreground mb-1">1 - 30 Days</div>
                                    <div className="text-xl font-bold text-amber-500">{formatCurrency(agingReport.thirtyDays)}</div>
                                </CardContent>
                            </Card>
                            <Card className="bg-muted/30">
                                <CardContent className="p-4 text-center">
                                    <div className="text-sm font-medium text-muted-foreground mb-1">31 - 60 Days</div>
                                    <div className="text-xl font-bold text-amber-600">{formatCurrency(agingReport.sixtyDays)}</div>
                                </CardContent>
                            </Card>
                            <Card className="bg-muted/30">
                                <CardContent className="p-4 text-center">
                                    <div className="text-sm font-medium text-muted-foreground mb-1">61 - 90+ Days</div>
                                    <div className="text-xl font-bold text-red-600">{formatCurrency(agingReport.ninetyPlusDays)}</div>
                                </CardContent>
                            </Card>
                            <Card className="bg-primary/5 border-primary/20">
                                <CardContent className="p-4 text-center">
                                    <div className="text-sm font-medium text-primary mb-1">Total Outstanding</div>
                                    <div className="text-2xl font-black text-primary">{formatCurrency(agingReport.totalOutstanding)}</div>
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
                                            <TableCell className="text-right">{supplier.current > 0 ? formatCurrency(supplier.current) : '-'}</TableCell>
                                            <TableCell className="text-right">{supplier.thirtyDays > 0 ? formatCurrency(supplier.thirtyDays) : '-'}</TableCell>
                                            <TableCell className="text-right">{supplier.sixtyDays > 0 ? formatCurrency(supplier.sixtyDays) : '-'}</TableCell>
                                            <TableCell className="text-right">{supplier.ninetyPlusDays > 0 ? formatCurrency(supplier.ninetyPlusDays) : '-'}</TableCell>
                                            <TableCell className="text-right font-bold text-primary">{formatCurrency(supplier.totalOutstanding)}</TableCell>
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
                                            <TableCell className="text-right">{formatCurrency(agingReport.current)}</TableCell>
                                            <TableCell className="text-right">{formatCurrency(agingReport.thirtyDays)}</TableCell>
                                            <TableCell className="text-right">{formatCurrency(agingReport.sixtyDays)}</TableCell>
                                            <TableCell className="text-right">{formatCurrency(agingReport.ninetyPlusDays)}</TableCell>
                                            <TableCell className="text-right text-primary text-lg">{formatCurrency(agingReport.totalOutstanding)}</TableCell>
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
    const [asOfDate, setAsOfDate] = useState<Date>(new Date());

    const { data: forecastReport, isLoading } = useQuery({
        queryKey: ['ap-cash-requirements', asOfDate],
        queryFn: () => accountsPayableService.getCashRequirementForecast(format(asOfDate, 'yyyy-MM-dd')),
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
                        <Popover>
                            <PopoverTrigger asChild>
                                <Button
                                    variant="outline"
                                    className={cn(
                                        "w-[200px] justify-start text-left font-normal",
                                        !asOfDate && "text-muted-foreground"
                                    )}
                                >
                                    <CalendarIcon className="mr-2 h-4 w-4" />
                                    {asOfDate ? format(asOfDate, "PPP") : <span>As of Date</span>}
                                </Button>
                            </PopoverTrigger>
                            <PopoverContent className="w-auto p-0">
                                <Calendar
                                    mode="single"
                                    selected={asOfDate}
                                    onSelect={(date) => date && setAsOfDate(date)}
                                    initialFocus
                                />
                            </PopoverContent>
                        </Popover>
                        <Button variant="outline" size="icon">
                            <Download className="h-4 w-4" />
                        </Button>
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
                                <p className="text-3xl font-bold">{formatCurrency(forecastReport.totalPayable)}</p>
                            </div>
                            <div>
                                <h3 className="text-sm font-semibold text-red-500 uppercase tracking-wider">Overdue</h3>
                                <p className="text-3xl font-bold text-red-600">{formatCurrency(forecastReport.overdueAmount)}</p>
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
                                            <TableCell className="text-right text-green-600">{period.discountAvailable > 0 ? formatCurrency(period.discountAvailable) : '-'}</TableCell>
                                            <TableCell className="text-right font-medium">{formatCurrency(period.amountDue)}</TableCell>
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
    const [partners, setPartners] = useState<LedgerPartnerOption[]>([]);
    const [partnersLoading, setPartnersLoading] = useState(true);

    useEffect(() => {
        let isMounted = true;

        const loadPartners = async () => {
            setPartnersLoading(true);
            try {
                const allPartners = await businessPartnerService.getAllPartnersForDropdown();
                if (!isMounted) return;

                setPartners(
                    allPartners
                        .filter(isSupplierPartner)
                        .map((partner) => ({
                            id: partner.id,
                            code: partner.partnerCode,
                            name: partner.partnerName,
                            currencyCode: partner.currency,
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
        />
    )
}
