'use client';

import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { AlertCircle, CheckCircle2, Download, RefreshCw, ShieldCheck } from 'lucide-react';
import { format } from 'date-fns';
import { accountsPayableService } from '@/services/accountsPayableService';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import type { ProcurementFinanceReconciliationReport } from '@/types/ap';

function money(value: number, currency: string) {
    return new Intl.NumberFormat(undefined, {
        style: 'currency',
        currency,
        maximumFractionDigits: 2,
    }).format(value);
}

export function ProcurementFinanceReconciliation({ initialData }: { initialData?: ProcurementFinanceReconciliationReport }) {
    const [asOfDate, setAsOfDate] = useState(format(new Date(), 'yyyy-MM-dd'));
    const [purchaseOrderId, setPurchaseOrderId] = useState('');
    const [downloading, setDownloading] = useState(false);
    const query = useQuery({
        queryKey: ['ap-procurement-finance-reconciliation', asOfDate, purchaseOrderId],
        queryFn: () => accountsPayableService.getProcurementFinanceReconciliation({
            asOfDate,
            purchaseOrderId: purchaseOrderId.trim() || undefined,
        }),
        initialData,
    });

    const download = async () => {
        setDownloading(true);
        try {
            const blob = await accountsPayableService.downloadProcurementFinanceReconciliation({
                asOfDate,
                purchaseOrderId: purchaseOrderId.trim() || undefined,
            });
            const url = URL.createObjectURL(blob);
            const anchor = document.createElement('a');
            anchor.href = url;
            anchor.download = `procurement-finance-reconciliation-${asOfDate}.csv`;
            anchor.click();
            URL.revokeObjectURL(url);
        } finally {
            setDownloading(false);
        }
    };

    if (query.isLoading) {
        return <Skeleton className="h-[520px] w-full" data-testid="procurement-reconciliation-loading" />;
    }

    if (query.isError || !query.data) {
        return (
            <Alert variant="destructive">
                <AlertCircle className="h-4 w-4" />
                <AlertTitle>Unable to load procurement/Finance reconciliation</AlertTitle>
                <AlertDescription className="flex flex-wrap items-center justify-between gap-3">
                    <span>{query.error instanceof Error ? query.error.message : 'The report could not be loaded.'}</span>
                    <Button size="sm" variant="outline" onClick={() => query.refetch()}>
                        <RefreshCw className="mr-2 h-4 w-4" /> Retry
                    </Button>
                </AlertDescription>
            </Alert>
        );
    }

    const report = query.data;
    return (
        <div className="space-y-5" data-testid="procurement-finance-reconciliation">
            <Card>
                <CardHeader>
                    <div className="flex flex-wrap items-start justify-between gap-4">
                        <div>
                            <div className="mb-2 flex flex-wrap items-center gap-2">
                                <Badge variant="outline">{report.ruleCode}</Badge>
                                <Badge variant="outline">{report.taskCode}</Badge>
                                <Badge variant={report.isReconciled ? 'default' : 'destructive'}>
                                    {report.isReconciled ? 'Reconciled' : 'Exceptions found'}
                                </Badge>
                            </div>
                            <CardTitle>Procurement and Finance reconciliation</CardTitle>
                            <CardDescription>
                                One explainable view over authoritative commitments, receipts, AP settlements,
                                central GL postings and reversals, retention, and contract milestones.
                            </CardDescription>
                        </div>
                        <div className="flex flex-wrap gap-2">
                            <input
                                aria-label="Reconciliation as of date"
                                className="h-10 rounded-md border bg-background px-3 text-sm"
                                type="date"
                                value={asOfDate}
                                onChange={(event) => setAsOfDate(event.target.value)}
                            />
                            <input
                                aria-label="Purchase order id filter"
                                className="h-10 w-64 rounded-md border bg-background px-3 text-sm"
                                placeholder="Optional purchase order ID"
                                value={purchaseOrderId}
                                onChange={(event) => setPurchaseOrderId(event.target.value)}
                            />
                            <Button variant="outline" disabled={downloading} onClick={download}>
                                <Download className="mr-2 h-4 w-4" />
                                {downloading ? 'Exporting…' : 'Export CSV'}
                            </Button>
                        </div>
                    </div>
                </CardHeader>
                <CardContent className="space-y-4">
                    <Alert>
                        <ShieldCheck className="h-4 w-4" />
                        <AlertTitle>Authoritative shared controls</AlertTitle>
                        <AlertDescription>
                            This report does not allocate payments or post money. It reads the existing Finance,
                            Procurement, and Projects records. Decision lineage: {report.decisionKeys.join(', ')}.
                        </AlertDescription>
                    </Alert>
                    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
                        <Metric label="Purchase orders" value={report.purchaseOrderCount} />
                        <Metric label="Issues" value={report.issueCount} tone={report.issueCount ? 'danger' : 'normal'} />
                        <Metric label="Controlled reversals" value={report.controlledReversalCount} />
                        <Metric label="Unbalanced postings" value={report.unbalancedPostingCount} tone={report.unbalancedPostingCount ? 'danger' : 'normal'} />
                        <Metric
                            label="AP control variance"
                            value={report.apControlReconciliation.variance.toFixed(2)}
                            tone={Math.abs(report.apControlReconciliation.variance) > 0.01 ? 'danger' : 'normal'}
                        />
                    </div>
                </CardContent>
            </Card>

            {report.currencySummaries.map((summary) => (
                <Card key={summary.currencyCode}>
                    <CardHeader className="pb-3">
                        <CardTitle className="text-base">{summary.currencyCode} control totals</CardTitle>
                        <CardDescription>Amounts remain currency-separated; this view performs no implicit conversion.</CardDescription>
                    </CardHeader>
                    <CardContent className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
                        <MoneyMetric label="Purchase orders" value={summary.purchaseOrderAmount} currency={summary.currencyCode} />
                        <MoneyMetric label="Commitments" value={summary.commitmentAmount} currency={summary.currencyCode} />
                        <MoneyMetric label="Accepted receipts" value={summary.acceptedReceiptAmount} currency={summary.currencyCode} />
                        <MoneyMetric label="AP invoices" value={summary.invoiceAmount} currency={summary.currencyCode} />
                        <MoneyMetric label="Settled" value={summary.settledAmount} currency={summary.currencyCode} />
                        <MoneyMetric label="Invoice GL" value={summary.invoicePostedAmount} currency={summary.currencyCode} />
                        <MoneyMetric label="Payment GL" value={summary.paymentPostedAmount} currency={summary.currencyCode} />
                        <MoneyMetric label="Retention held" value={summary.retentionHeldAmount} currency={summary.currencyCode} />
                        <MoneyMetric label="Retention released" value={summary.retentionReleasedAmount} currency={summary.currencyCode} />
                        <MoneyMetric label="Milestones" value={summary.milestoneAmount} currency={summary.currencyCode} />
                    </CardContent>
                </Card>
            ))}

            <Card>
                <CardHeader>
                    <CardTitle>Purchase-order reconciliation register</CardTitle>
                    <CardDescription>Traceable source totals and explicit variance reasons for follow-up.</CardDescription>
                </CardHeader>
                <CardContent>
                    <div className="overflow-x-auto rounded-md border">
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead>PO / status</TableHead>
                                    <TableHead>Commitment / order</TableHead>
                                    <TableHead>Receipt / invoice</TableHead>
                                    <TableHead>Settlement / GL</TableHead>
                                    <TableHead>Retention</TableHead>
                                    <TableHead>Milestones</TableHead>
                                    <TableHead>Result</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {report.rows.map((row) => (
                                    <TableRow key={row.purchaseOrderId}>
                                        <TableCell>
                                            <div className="font-medium">{row.purchaseOrderNumber}</div>
                                            <div className="text-xs text-muted-foreground">{row.purchaseOrderStatus} · {row.currencyCode}</div>
                                        </TableCell>
                                        <TableCell>
                                            <div>{money(row.commitmentAmount, row.currencyCode)}</div>
                                            <div className="text-xs text-muted-foreground">of {money(row.commitmentGroupOrderAmount, row.currencyCode)}</div>
                                        </TableCell>
                                        <TableCell>
                                            <div>{money(row.acceptedReceiptAmount, row.currencyCode)}</div>
                                            <div className="text-xs text-muted-foreground">invoice {money(row.invoiceAmount, row.currencyCode)}</div>
                                        </TableCell>
                                        <TableCell>
                                            <div>{money(row.settledAmount, row.currencyCode)}</div>
                                            <div className="text-xs text-muted-foreground">invoice GL {money(row.invoicePostedAmount, row.currencyCode)} · payment GL {money(row.paymentPostedAmount, row.currencyCode)}</div>
                                        </TableCell>
                                        <TableCell>
                                            <div>{money(row.retentionOutstandingAmount, row.currencyCode)} outstanding</div>
                                            <div className="text-xs text-muted-foreground">held {money(row.retentionHeldAmount, row.currencyCode)} · released {money(row.retentionReleasedAmount, row.currencyCode)}</div>
                                        </TableCell>
                                        <TableCell>
                                            <div>{money(row.milestoneAmount, row.currencyCode)}</div>
                                            <div className="text-xs text-muted-foreground">paid {money(row.paidMilestoneAmount, row.currencyCode)}</div>
                                        </TableCell>
                                        <TableCell className="min-w-72">
                                            <div className="mb-2 flex items-center gap-2">
                                                {row.isReconciled ? <CheckCircle2 className="h-4 w-4 text-emerald-600" /> : <AlertCircle className="h-4 w-4 text-destructive" />}
                                                <span className="font-medium">{row.isReconciled ? 'Reconciled' : `${row.issues.length} issue(s)`}</span>
                                            </div>
                                            <div className="space-y-1 text-xs text-muted-foreground">
                                                {row.issues.map((issue) => (
                                                    <div key={`${issue.code}-${issue.sourceDocumentId ?? ''}`}>
                                                        <Badge variant={issue.severity === 'Error' ? 'destructive' : 'outline'} className="mr-1">{issue.code}</Badge>
                                                        {issue.message}
                                                    </div>
                                                ))}
                                            </div>
                                        </TableCell>
                                    </TableRow>
                                ))}
                                {report.rows.length === 0 && (
                                    <TableRow>
                                        <TableCell colSpan={7} className="py-10 text-center text-muted-foreground">
                                            No operative purchase orders were found for this date and tenant.
                                        </TableCell>
                                    </TableRow>
                                )}
                            </TableBody>
                        </Table>
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}

function Metric({ label, value, tone = 'normal' }: { label: string; value: number | string; tone?: 'normal' | 'danger' }) {
    return (
        <div className="rounded-md border p-3">
            <div className="text-xs text-muted-foreground">{label}</div>
            <div className={`text-xl font-semibold ${tone === 'danger' ? 'text-destructive' : ''}`}>{value}</div>
        </div>
    );
}

function MoneyMetric({ label, value, currency }: { label: string; value: number; currency: string }) {
    return <Metric label={label} value={money(value, currency)} />;
}
