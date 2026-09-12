'use client';

import { useEffect } from 'react';
import { useParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, Loader2, Printer } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { mePortalService } from '@/services/hr/me-portal.service';
import type { PayslipTransaction } from '@/services/hr/me-portal.service';

/**
 * One payslip, rendered from its FROZEN snapshot (area 25 slice 10, D4) — what payroll
 * published, never a recomputation. Printing reuses payroll's own `printing-payroll-payslip`
 * body-class family, so the printed document matches the payslips payroll prints from the desk.
 *
 * A payslip can 404 after it was seen: payroll recalculating a run deletes and regenerates its
 * snapshots. The error copy says so instead of pretending the id never existed.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function MyPayslipPage() {
  const { id } = useParams<{ id: string }>();

  const { data: detail, isLoading, isError } = useQuery({
    queryKey: ['me', 'payslips', id],
    queryFn: () => mePortalService.getPayslip(id),
    retry: false,
  });

  // The established print pattern: flag the body, print, and always clean up — a class left
  // behind would blank the next screen the user prints from.
  const print = () => {
    const cleanup = () => {
      document.body.classList.remove('printing-payroll-payslip');
      window.removeEventListener('afterprint', cleanup);
    };
    document.body.classList.add('printing-payroll-payslip');
    window.addEventListener('afterprint', cleanup);
    window.print();
  };
  useEffect(() => () => document.body.classList.remove('printing-payroll-payslip'), []);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }

  if (isError || !detail) {
    return (
      <div className="space-y-6">
        <PageHeader title="Payslip" backHref="/me/payslips" />
        <Alert>
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>
            This payslip is not available. It may not be yours — or payroll may have
            recalculated its pay run, which replaces the published payslips. Check your list
            again, or ask the payroll office.
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  const doc = detail.payslip;
  const ccy = detail.currencyCode;
  const money = (v: number) =>
    `${ccy} ${v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

  const lineRows = (rows: PayslipTransaction[]) =>
    rows.map((t, i) => (
      // The synthetic "Income Tax - Total" row carries an empty-guid id — never key on id alone.
      <TableRow key={`${t.id}-${i}`}>
        <TableCell>{t.description}</TableCell>
        <TableCell className="text-right tabular-nums">{money(t.amount)}</TableCell>
      </TableRow>
    ));

  return (
    <div className="space-y-6">
      <div className="payroll-payslip-no-print">
        <PageHeader
          title={detail.payslipNumber}
          description={`Pay period ${fmtDate(detail.payPeriodFrom)} — ${fmtDate(detail.payPeriodTo)} · run ${detail.runNumber}`}
          backHref="/me/payslips"
          actions={
            <Button onClick={print}>
              <Printer className="mr-2 h-4 w-4" />
              Print
            </Button>
          }
        />
      </div>

      {!doc ? (
        <Alert>
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>
            The stored copy of this payslip could not be read. The headline figures below are
            from the payroll register; contact the payroll office for the full breakdown.
            <span className="mt-2 block">
              Gross {money(detail.grossIncome)} · Tax {money(detail.taxAmount)} · Net{' '}
              {money(detail.netIncome)}
            </span>
          </AlertDescription>
        </Alert>
      ) : (
        <div className="payroll-payslip-print-root space-y-6">
          <Card>
            <CardContent className="space-y-4 p-6">
              <div className="text-center">
                <p className="text-lg font-semibold">{doc.companyName ?? ''}</p>
                {doc.companyAddress && (
                  <p className="text-muted-foreground text-sm">{doc.companyAddress}</p>
                )}
                <p className="mt-1 font-medium">
                  Payslip {detail.payslipNumber}
                  {detail.isSeparateBonusRun && (
                    <Badge variant="secondary" className="ml-2">
                      Bonus run
                    </Badge>
                  )}
                </p>
                <p className="text-muted-foreground text-sm">
                  {fmtDate(doc.payPeriodFrom)} — {fmtDate(doc.payPeriodTo)}
                </p>
              </div>
              <div className="grid gap-x-8 gap-y-1 text-sm sm:grid-cols-2">
                <span>
                  <span className="text-muted-foreground">Employee: </span>
                  {doc.employeeName}{' '}
                  <span className="text-muted-foreground font-mono text-xs">
                    {doc.employeeNumber}
                  </span>
                </span>
                {doc.positionTitle && (
                  <span>
                    <span className="text-muted-foreground">Position: </span>
                    {doc.positionTitle}
                  </span>
                )}
                {doc.departmentName && (
                  <span>
                    <span className="text-muted-foreground">Department: </span>
                    {doc.departmentName}
                  </span>
                )}
                {doc.jobLocation && (
                  <span>
                    <span className="text-muted-foreground">Location: </span>
                    {doc.jobLocation}
                  </span>
                )}
                {doc.ssfNumber && (
                  <span>
                    <span className="text-muted-foreground">SSF no.: </span>
                    {doc.ssfNumber}
                  </span>
                )}
                {doc.staffTin && (
                  <span>
                    <span className="text-muted-foreground">TIN: </span>
                    {doc.staffTin}
                  </span>
                )}
              </div>
            </CardContent>
          </Card>

          <div className="grid gap-6 md:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Earnings</CardTitle>
              </CardHeader>
              <CardContent className="p-0">
                {doc.earnings.length === 0 ? (
                  <p className="text-muted-foreground px-6 pb-6 text-sm">None.</p>
                ) : (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Item</TableHead>
                        <TableHead className="text-right">Amount</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>{lineRows(doc.earnings)}</TableBody>
                  </Table>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="text-base">Deductions</CardTitle>
              </CardHeader>
              <CardContent className="p-0">
                {doc.deductions.length === 0 ? (
                  <p className="text-muted-foreground px-6 pb-6 text-sm">None this period.</p>
                ) : (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Item</TableHead>
                        <TableHead className="text-right">Amount</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>{lineRows(doc.deductions)}</TableBody>
                  </Table>
                )}
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardContent className="grid gap-x-8 gap-y-2 p-6 text-sm sm:grid-cols-3">
              <div>
                <p className="text-muted-foreground">Gross income</p>
                <p className="tabular-nums">{money(doc.grossIncome)}</p>
              </div>
              <div>
                <p className="text-muted-foreground">Taxable income</p>
                <p className="tabular-nums">{money(doc.taxableIncome)}</p>
              </div>
              <div>
                <p className="text-muted-foreground">Income tax</p>
                <p className="tabular-nums">{money(doc.incomeTax)}</p>
              </div>
              <div>
                <p className="text-muted-foreground">Your contribution</p>
                <p className="tabular-nums">{money(doc.employeeContribution)}</p>
              </div>
              <div>
                <p className="text-muted-foreground">Employer contribution</p>
                <p className="tabular-nums">{money(doc.employerContribution)}</p>
              </div>
              <div>
                <p className="text-muted-foreground font-medium">Net pay</p>
                <p className="text-base font-semibold tabular-nums">{money(doc.netIncome)}</p>
              </div>
            </CardContent>
          </Card>

          {doc.contributions.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Pension & contributions</CardTitle>
              </CardHeader>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Scheme</TableHead>
                      <TableHead className="text-right">You</TableHead>
                      <TableHead className="text-right">Employer</TableHead>
                      <TableHead className="text-right">Total</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {doc.contributions.map((c, i) => (
                      <TableRow key={`${c.item}-${i}`}>
                        <TableCell>{c.item}</TableCell>
                        <TableCell className="text-right tabular-nums">
                          {money(c.employeeContribution)}
                        </TableCell>
                        <TableCell className="text-right tabular-nums">
                          {money(c.employerContribution)}
                        </TableCell>
                        <TableCell className="text-right tabular-nums">
                          {money(c.totalContribution)}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}

          {doc.bankDetails.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Paid to</CardTitle>
              </CardHeader>
              <CardContent className="space-y-1 p-6 pt-0 text-sm">
                {doc.bankDetails.map((b, i) => (
                  <p key={`${b.bankName}-${i}`}>
                    {b.bankName ?? 'Bank'}
                    {b.accountNumber ? ` · ${b.accountNumber}` : ''} ·{' '}
                    <span className="tabular-nums">
                      {b.currencyCode}{' '}
                      {b.amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                    </span>
                  </p>
                ))}
              </CardContent>
            </Card>
          )}
        </div>
      )}
    </div>
  );
}
