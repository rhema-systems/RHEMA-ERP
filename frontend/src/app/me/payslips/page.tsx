'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Banknote } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { mePortalService } from '@/services/hr/me-portal.service';

/**
 * My Payslips (area 25 slice 10, decision D4) — the read-only adapter over payroll's frozen
 * `PayrollPayslipSnapshot` rows. Nothing here touches payroll's live run: the portal shows
 * exactly what payroll has PUBLISHED (generated snapshots for), newest pay period first, and
 * nothing earlier — which is why the empty state explains itself rather than apologising.
 *
 * Token-scoped like every portal read: no employee id anywhere, and a colleague's payslip id
 * simply does not resolve.
 */
const fmtDate = (v: string) => new Date(v).toLocaleDateString();
const money = (v: number, ccy: string) =>
  `${ccy} ${v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

export default function MyPayslipsPage() {
  const { data: payslips = [], isLoading } = useQuery({
    queryKey: ['me', 'payslips'],
    queryFn: () => mePortalService.getPayslips(),
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Payslips"
        description="Every payslip payroll has published for you, newest first. Open one to see the full breakdown and print it."
        backHref="/me"
      />

      {isLoading ? null : payslips.length === 0 ? (
        <EmptyState
          icon={Banknote}
          title="No payslips yet"
          description="Payslips appear here when payroll publishes them for a pay period you are in. If you expected one, ask the payroll office whether the period has been finalised."
        />
      ) : (
        <Card>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Pay period</TableHead>
                  <TableHead>Payslip</TableHead>
                  <TableHead className="text-right">Gross</TableHead>
                  <TableHead className="text-right">Tax</TableHead>
                  <TableHead className="text-right">Net</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {payslips.map((p) => (
                  <TableRow key={p.id}>
                    <TableCell>
                      {fmtDate(p.payPeriodFrom)} — {fmtDate(p.payPeriodTo)}
                      {p.isSeparateBonusRun && (
                        <Badge variant="secondary" className="ml-2">
                          Bonus
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell className="font-mono text-sm">
                      <Link href={`/me/payslips/${p.id}`} className="hover:underline">
                        {p.payslipNumber}
                      </Link>
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {money(p.grossIncome, p.currencyCode)}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {money(p.taxAmount, p.currencyCode)}
                    </TableCell>
                    <TableCell className="text-right font-medium tabular-nums">
                      {money(p.netIncome, p.currencyCode)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
