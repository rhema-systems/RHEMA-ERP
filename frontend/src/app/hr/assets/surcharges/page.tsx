'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Banknote, Loader2, Receipt, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
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
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useDebounce } from '@/hooks/use-debounce';
import { assetRegisterService } from '@/services/hr/asset-register.service';
import type { AssetSurchargeSummary } from '@/types/hr/assets';

const PAGE_SIZE = 20;
const ANY = '__any__';
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtNum = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { minimumFractionDigits: 2 });

const STATUSES = [
  'Draft', 'WithEmployee', 'Submitted', 'Approved', 'Rejected',
  'Recovering', 'Recovered', 'Waived', 'Cancelled',
];

function SurchargeTable({ rows }: { rows: AssetSurchargeSummary[] }) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Charge</TableHead>
          <TableHead>Asset</TableHead>
          <TableHead>Against</TableHead>
          <TableHead>Reason</TableHead>
          <TableHead className="text-right">Assessed</TableHead>
          <TableHead className="text-right">Outstanding</TableHead>
          <TableHead>Their answer</TableHead>
          <TableHead>Status</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((s) => (
          <TableRow key={s.id}>
            <TableCell>
              <Link href={`/hr/assets/surcharges/${s.id}`} className="hover:underline">
                {s.surchargeNumber}
              </Link>
              <div className="text-xs text-muted-foreground">{fmtDate(s.raisedAt)}</div>
            </TableCell>
            <TableCell>
              <div>{s.assetName}</div>
              <div className="text-xs text-muted-foreground">{s.assetNumber}</div>
            </TableCell>
            <TableCell>{s.employeeName}</TableCell>
            <TableCell>{s.reasonName}</TableCell>
            <TableCell className="text-right">{s.currencyCode} {fmtNum(s.assessedAmount)}</TableCell>
            <TableCell className="text-right">{s.currencyCode} {fmtNum(s.amountOutstanding)}</TableCell>
            <TableCell>
              {/* The right of reply is the point of D9 — a disputed charge must be visible as one. */}
              {s.isDisputed
                ? <span className="text-amber-600 dark:text-amber-500">Disputed</span>
                : s.employeeResponseName}
            </TableCell>
            <TableCell><StatusBadge status={s.statusName} /></TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

/**
 * Charges raised for damage or loss — AST-3, decision D9.
 *
 * Three views, because they answer three questions. "All charges" is the register. "Still owed" is
 * approved money not yet recovered. "Payroll deductions" is the read-only projection payroll
 * consumes — **HR declares what is owed and payroll runs the deduction**; nothing on this screen
 * computes a payslip, and only approved charges on a payroll-deduction plan appear there at all.
 */
export default function AssetSurchargesPage() {
  const [view, setView] = useState<'all' | 'outstanding' | 'payroll'>('all');
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<string>(ANY);
  const debouncedSearch = useDebounce(search, 300);

  const { data: paged, isLoading } = useQuery({
    queryKey: ['hr', 'assets', 'surcharges', page, debouncedSearch, status],
    queryFn: () =>
      // ⚠ `page`, not `pageNumber` — the only paged read in this area that does.
      assetRegisterService.getSurchargesPaged({
        page,
        pageSize: PAGE_SIZE,
        searchTerm: debouncedSearch || undefined,
        status: status === ANY ? undefined : status,
      }),
    enabled: view === 'all',
  });

  const { data: outstanding = [], isLoading: loadingOutstanding } = useQuery({
    queryKey: ['hr', 'assets', 'surcharges', 'outstanding'],
    queryFn: () => assetRegisterService.getOutstandingSurcharges(),
    enabled: view === 'outstanding',
  });

  const { data: payroll = [], isLoading: loadingPayroll } = useQuery({
    queryKey: ['hr', 'assets', 'surcharges', 'payroll'],
    queryFn: () => assetRegisterService.getSurchargePayrollLines(),
    enabled: view === 'payroll',
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Asset surcharges"
        description="Charges raised for damage or loss, and what payroll may recover."
        backHref="/hr/assets"
      />

      <div className="flex flex-wrap gap-2">
        <Button variant={view === 'all' ? 'default' : 'outline'} size="sm" onClick={() => setView('all')}>
          <Receipt className="mr-2 h-4 w-4" /> All charges
        </Button>
        <Button variant={view === 'outstanding' ? 'default' : 'outline'} size="sm"
          onClick={() => setView('outstanding')}>
          Still owed
        </Button>
        <Button variant={view === 'payroll' ? 'default' : 'outline'} size="sm"
          onClick={() => setView('payroll')}>
          <Banknote className="mr-2 h-4 w-4" /> Payroll deductions
        </Button>
      </div>

      {view === 'all' && (
        <Card>
          <CardContent className="flex flex-wrap gap-3 p-4">
            <div className="relative min-w-[240px] flex-1">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input className="pl-8" placeholder="Charge number, asset, employee…"
                value={search} onChange={(e) => { setSearch(e.target.value); setPage(1); }} />
            </div>
            <Select value={status} onValueChange={(v) => { setStatus(v); setPage(1); }}>
              <SelectTrigger className="w-[190px]"><SelectValue placeholder="Any status" /></SelectTrigger>
              <SelectContent>
                <SelectItem value={ANY}>Any status</SelectItem>
                {STATUSES.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}
              </SelectContent>
            </Select>
          </CardContent>
        </Card>
      )}

      {view === 'payroll' && (
        <Card>
          <CardContent className="p-4 text-sm text-muted-foreground">
            HR declares what is owed and how it was said to be recovered. Payroll runs the deduction.
            Only approved charges on a payroll-deduction plan appear here — a charge the employee is
            still disputing is not yet a debt.
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="p-0">
          {view === 'payroll' ? (
            loadingPayroll ? (
              <div className="flex justify-center p-10">
                <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
              </div>
            ) : payroll.length === 0 ? (
              <EmptyState icon={Banknote} title="Nothing for payroll"
                description="No approved charge has a payroll-deduction plan against it." />
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
                    <TableHead>Recovery starts</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {payroll.map((l) => (
                    <TableRow key={l.surchargeId}>
                      <TableCell>
                        <div className="font-medium">{l.employeeName}</div>
                        <div className="text-xs text-muted-foreground">{l.employeeNumber ?? '—'}</div>
                      </TableCell>
                      <TableCell>
                        <Link href={`/hr/assets/surcharges/${l.surchargeId}`} className="hover:underline">
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
            )
          ) : (view === 'all' ? isLoading : loadingOutstanding) ? (
            <div className="flex justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : (view === 'all' ? paged?.items ?? [] : outstanding).length === 0 ? (
            <EmptyState
              icon={Receipt}
              title={view === 'all' ? 'No charges match' : 'Nothing outstanding'}
              description={view === 'all'
                ? 'No charge answers to those filters.'
                : 'Every approved charge has been recovered, waived or written off.'}
            />
          ) : (
            <SurchargeTable rows={view === 'all' ? paged?.items ?? [] : outstanding} />
          )}
        </CardContent>
      </Card>

      {view === 'all' && paged && paged.totalPages > 1 && (
        <div className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground">
            Page {paged.page} of {paged.totalPages} · {paged.totalCount} charge
            {paged.totalCount === 1 ? '' : 's'}
          </p>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={!paged.hasPrevious}
              onClick={() => setPage((p) => p - 1)}>Previous</Button>
            <Button variant="outline" size="sm" disabled={!paged.hasNext}
              onClick={() => setPage((p) => p + 1)}>Next</Button>
          </div>
        </div>
      )}
    </div>
  );
}
