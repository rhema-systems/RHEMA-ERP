'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Banknote, Home, Loader2 } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { assetRegisterService } from '@/services/hr/asset-register.service';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtNum = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { minimumFractionDigits: 2 });

const thisMonth = () => new Date().toISOString().slice(0, 7);

/**
 * What payroll may deduct for company assets — AST-3 and AST-10, area 16 slice 12b.
 *
 * ⚠ **HR declares; payroll deducts.** Nothing on this page computes a payslip, writes to payroll,
 * or holds a deduction of its own. It is a read-only projection of two things HR already decided:
 * a rental arrangement on a custody, and an approved surcharge with a payroll-deduction plan.
 * Decision D2 draws that line and this screen is the whole of HR's side of it.
 *
 * ⚠ **The rental figures are NOT prorated.** Each line carries the full periodic rate and the
 * window it applies to; how much of it falls in a given pay run is payroll's calculation, made with
 * payroll's calendar. `isPartialPeriod` says the window is short — it does **not** mean the amount
 * has been reduced. HR computing a part-month here would be guessing at another module's period
 * boundaries in the one place a mistake reaches somebody's take-home pay.
 *
 * ⚠ A benefit in kind is declared for tax and **not** deducted: `isDeductible` is false on those
 * lines and the table says so, because a column of amounts that mixes the two is a column somebody
 * will total.
 */
export default function AssetPayrollPage() {
  const [period, setPeriod] = useState(thisMonth());

  const { data: rental = [], isLoading: loadingRental } = useQuery({
    queryKey: ['hr', 'assets', 'payroll', 'rental', period],
    queryFn: () => assetRegisterService.getRentalPayrollLines(period || undefined),
  });

  const { data: surcharges = [], isLoading: loadingSurcharges } = useQuery({
    queryKey: ['hr', 'assets', 'payroll', 'surcharges'],
    queryFn: () => assetRegisterService.getSurchargePayrollLines(),
  });

  const deductibleRental = rental.filter((r) => r.isDeductible);
  const benefitLines = rental.filter((r) => !r.isDeductible);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Payroll deductions"
        description="What HR has declared against company assets. Payroll runs the deduction."
        backHref="/hr/assets"
      />

      <Card>
        <CardContent className="p-4 text-sm text-muted-foreground">
          HR states what is owed and how it was said to be recovered. Nothing here computes a
          payslip or writes anything to payroll.
        </CardContent>
      </Card>

      <Tabs defaultValue="rental">
        <TabsList>
          <TabsTrigger value="rental">Asset rental ({rental.length})</TabsTrigger>
          <TabsTrigger value="surcharges">Surcharge recovery ({surcharges.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="rental" className="space-y-4 pt-4">
          <Card>
            <CardContent className="flex flex-wrap items-end gap-4 p-4">
              <div className="space-y-2">
                <Label>Pay period</Label>
                <Input
                  type="month"
                  className="w-44"
                  value={period}
                  onChange={(e) => setPeriod(e.target.value)}
                />
              </div>
              <p className="flex-1 pb-2 text-sm text-muted-foreground">
                ⚠ These amounts are <span className="font-medium">not prorated</span>. Each line is
                the full periodic rate plus the window it applies to; apportioning it to a pay run is
                payroll&rsquo;s calculation with payroll&rsquo;s calendar.
              </p>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-0">
              {loadingRental ? (
                <div className="flex justify-center p-10">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : deductibleRental.length === 0 ? (
                <EmptyState
                  icon={Home}
                  title="Nothing to deduct"
                  description="No custody in this period carries rental terms that are deducted from pay."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead>Asset</TableHead>
                      <TableHead className="text-right">Rate</TableHead>
                      <TableHead>Frequency</TableHead>
                      <TableHead>Applies to</TableHead>
                      <TableHead>In force from</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {deductibleRental.map((r) => (
                      <TableRow key={r.assignmentId}>
                        <TableCell>
                          <div className="font-medium">{r.employeeName}</div>
                          <div className="text-xs text-muted-foreground">
                            {r.employeeNumber ?? '—'}
                          </div>
                        </TableCell>
                        <TableCell>
                          <Link href={`/hr/assets/register/${r.assetId}`} className="hover:underline">
                            {r.assetName}
                          </Link>
                          <div className="text-xs text-muted-foreground">
                            {r.assetNumber} · {r.assetTypeName}
                          </div>
                        </TableCell>
                        <TableCell className="text-right">
                          {r.currencyCode} {fmtNum(r.rentalAmount)}
                        </TableCell>
                        <TableCell>{r.frequencyName}</TableCell>
                        <TableCell>
                          {fmtDate(r.periodStart)} – {fmtDate(r.periodEnd)}
                          {r.isPartialPeriod && (
                            <div className="text-xs text-amber-600 dark:text-amber-500">
                              part period — the rate above is still the full one
                            </div>
                          )}
                        </TableCell>
                        <TableCell>
                          {fmtDate(r.effectiveFrom)}
                          {r.effectiveTo && <> – {fmtDate(r.effectiveTo)}</>}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>

          {benefitLines.length > 0 && (
            <Card>
              <CardContent className="p-0">
                <div className="border-b p-4">
                  <p className="text-sm font-medium">Benefits in kind — declared, not deducted</p>
                  <p className="text-sm text-muted-foreground">
                    These are kept apart on purpose: they are a tax declaration, and adding them to
                    the column above would produce a total nobody should act on.
                  </p>
                </div>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead>Asset</TableHead>
                      <TableHead className="text-right">Value of the benefit</TableHead>
                      <TableHead>Applies to</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {benefitLines.map((r) => (
                      <TableRow key={r.assignmentId}>
                        <TableCell>{r.employeeName}</TableCell>
                        <TableCell>{r.assetName}</TableCell>
                        <TableCell className="text-right">
                          {r.currencyCode} {fmtNum(r.benefitInKindValue ?? r.rentalAmount)}
                        </TableCell>
                        <TableCell>{fmtDate(r.periodStart)} – {fmtDate(r.periodEnd)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="surcharges" className="space-y-4 pt-4">
          <Card>
            <CardContent className="p-4 text-sm text-muted-foreground">
              Only <span className="font-medium">approved</span> charges with an outstanding balance
              and a payroll-deduction plan appear here. A charge the employee is still disputing is
              not yet a debt.
            </CardContent>
          </Card>
          <Card>
            <CardContent className="p-0">
              {loadingSurcharges ? (
                <div className="flex justify-center p-10">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : surcharges.length === 0 ? (
                <EmptyState
                  icon={Banknote}
                  title="Nothing to recover"
                  description="No approved charge has a payroll-deduction plan against it."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead>Charge</TableHead>
                      <TableHead>Asset</TableHead>
                      <TableHead className="text-right">Outstanding</TableHead>
                      <TableHead className="text-right">Instalment</TableHead>
                      <TableHead>Instalments</TableHead>
                      <TableHead>Starts</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {surcharges.map((l) => (
                      <TableRow key={l.surchargeId}>
                        <TableCell>
                          <div className="font-medium">{l.employeeName}</div>
                          <div className="text-xs text-muted-foreground">
                            {l.employeeNumber ?? '—'}
                          </div>
                        </TableCell>
                        <TableCell>
                          <Link
                            href={`/hr/assets/surcharges/${l.surchargeId}`}
                            className="hover:underline"
                          >
                            {l.surchargeNumber}
                          </Link>
                        </TableCell>
                        <TableCell>{l.assetName}</TableCell>
                        <TableCell className="text-right">
                          {l.currencyCode} {fmtNum(l.amountOutstanding)}
                        </TableCell>
                        <TableCell className="text-right">
                          {l.currencyCode} {fmtNum(l.instalmentAmount)}
                        </TableCell>
                        <TableCell>{l.instalmentCount ?? '—'}</TableCell>
                        <TableCell>{fmtDate(l.recoveryStartDate)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
