'use client';

import React from 'react';
import { useMemo, useState } from 'react';
import { ChevronDown, ChevronUp, Download, Search } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { authService } from '@/services/auth';
import type { AccountingBookInitialization, AccountingBookInitializationLine } from '@/types/finance';

type EvidenceFilter = 'all' | 'adjustments' | 'exceptions';

const money = (value: number) => new Intl.NumberFormat(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
}).format(value);

const modeLabels: Record<AccountingBookInitialization['mode'], string> = {
    IndependentOpeningBalances: 'Independent opening balances',
    BaseBookCopyAtCutoff: 'Copy source-book balances at cutoff',
    BaseBalancesWithOpeningAdjustments: 'Source-book balances with opening adjustments',
};

const csv = (value: string | number) => {
    const text = String(value);
    const spreadsheetSafe = typeof value === 'string' && /^[=+\-@]/.test(text) ? `'${text}` : text;
    return `"${spreadsheetSafe.replaceAll('"', '""')}"`;
};

export const clientFacingAccountNumber = (accountNumber: string | undefined, tenantCode: string | undefined) => {
    if (!accountNumber || !tenantCode) return accountNumber ?? '';
    const prefix = `${tenantCode}-`;
    return accountNumber.toLocaleUpperCase().startsWith(prefix.toLocaleUpperCase())
        ? accountNumber.slice(prefix.length)
        : accountNumber;
};

function evidenceValues(initialization: AccountingBookInitialization, line: AccountingBookInitializationLine) {
    const proposedSignedBalance = line.openingDebit - line.openingCredit;
    const expectedSignedBalance = initialization.mode === 'IndependentOpeningBalances'
        ? proposedSignedBalance
        : line.baseBookSignedBalance + line.openingAdjustment;
    const variance = proposedSignedBalance - expectedSignedBalance;
    const isException = Math.abs(variance) > 0.005 || line.openingDebit < 0 || line.openingCredit < 0
        || line.openingDebit > 0 && line.openingCredit > 0;
    return { proposedSignedBalance, expectedSignedBalance, variance, isException };
}

export function InitializationEvidencePack({ initialization }: { initialization: AccountingBookInitialization }) {
    const [expanded, setExpanded] = useState(initialization.status === 'PendingApproval');
    const [filter, setFilter] = useState<EvidenceFilter>('all');
    const [search, setSearch] = useState('');
    const currency = initialization.lines[0]?.currencyCode || '';
    const tenantCode = authService.getCurrentTenant()?.code;

    const evidence = useMemo(() => {
        const rows = initialization.lines.map(line => ({ ...line, ...evidenceValues(initialization, line) }));
        return {
            rows,
            adjustmentCount: rows.filter(line => Math.abs(line.openingAdjustment) > 0.005).length,
            netAdjustments: rows.reduce((total, line) => total + line.openingAdjustment, 0),
            absoluteAdjustments: rows.reduce((total, line) => total + Math.abs(line.openingAdjustment), 0),
            exceptionCount: rows.filter(line => line.isException).length,
        };
    }, [initialization]);

    const visibleRows = useMemo(() => {
        const term = search.trim().toLowerCase();
        return evidence.rows.filter(line => {
            if (filter === 'adjustments' && Math.abs(line.openingAdjustment) <= 0.005) return false;
            if (filter === 'exceptions' && !line.isException) return false;
            return !term || `${line.accountNumber ?? ''} ${line.accountName ?? ''} ${line.accountType ?? ''}`.toLowerCase().includes(term);
        });
    }, [evidence.rows, filter, search]);

    const downloadEvidence = () => {
        const headings = ['Account number', 'Account name', 'Account type', 'Currency', 'Source signed balance',
            'Opening adjustment', 'Opening debit', 'Opening credit', 'Resulting signed balance', 'Variance'];
        const rows = evidence.rows.map(line => [line.accountNumber ?? '', line.accountName ?? '', line.accountType ?? '',
            line.currencyCode, line.baseBookSignedBalance, line.openingAdjustment, line.openingDebit, line.openingCredit,
            line.proposedSignedBalance, line.variance]);
        const content = [headings, ...rows].map(row => row.map(csv).join(',')).join('\r\n');
        const url = URL.createObjectURL(new Blob([content], { type: 'text/csv;charset=utf-8' }));
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = `${initialization.accountingBookCode}-initialization-v${initialization.version}-evidence.csv`;
        anchor.click();
        URL.revokeObjectURL(url);
    };

    const source = initialization.sourceAccountingBookCode
        ? `${initialization.sourceAccountingBookCode} accounting book`
        : 'Independent opening evidence';
    const difference = initialization.totalDebits - initialization.totalCredits;

    return <div className="space-y-4">
        <div className="grid gap-3 rounded-md border bg-muted/20 p-4 text-sm sm:grid-cols-2 lg:grid-cols-4">
            <div><span className="text-muted-foreground">Target book</span><p className="font-medium">{initialization.accountingBookCode}</p></div>
            <div><span className="text-muted-foreground">Source authority</span><p className="font-medium">{source}</p></div>
            <div><span className="text-muted-foreground">Initialization method</span><p>{modeLabels[initialization.mode]}</p></div>
            <div><span className="text-muted-foreground">Status</span><div className="mt-1"><Badge>{initialization.status}</Badge></div></div>
            <div><span className="text-muted-foreground">Cutoff</span><p>{initialization.cutoffFiscalPeriodCode} · {initialization.cutoffDate.slice(0, 10)}</p></div>
            <div><span className="text-muted-foreground">Prepared by</span><p>{initialization.preparedByName || `User ${initialization.preparedByUserId}`}</p></div>
            <div><span className="text-muted-foreground">Prepared on</span><p>{new Date(initialization.preparedAtUtc).toLocaleString()}</p></div>
            <div><span className="text-muted-foreground">Preparation reference</span><p>{initialization.idempotencyKey}</p></div>
            <div className="sm:col-span-2 lg:col-span-4"><span className="text-muted-foreground">Preparation reason</span><p>{initialization.reason}</p></div>
        </div>

        <div className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-6">
            <div className="rounded-md border p-3"><span className="text-muted-foreground">Coverage</span><p className="text-lg font-semibold">{initialization.coveredAccountCount}/{initialization.requiredAccountCount}</p><p className="text-xs text-muted-foreground">{initialization.isCoverageComplete ? 'Complete' : 'Incomplete'}</p></div>
            <div className="rounded-md border p-3"><span className="text-muted-foreground">Opening debits</span><p className="text-lg font-semibold">{money(initialization.totalDebits)}</p><p className="text-xs text-muted-foreground">{currency}</p></div>
            <div className="rounded-md border p-3"><span className="text-muted-foreground">Opening credits</span><p className="text-lg font-semibold">{money(initialization.totalCredits)}</p><p className="text-xs text-muted-foreground">{currency}</p></div>
            <div className="rounded-md border p-3"><span className="text-muted-foreground">Difference</span><p className="text-lg font-semibold">{money(difference)}</p><p className="text-xs text-muted-foreground">{initialization.isBalanced ? 'Balanced' : 'Out of balance'}</p></div>
            <div className="rounded-md border p-3"><span className="text-muted-foreground">Adjustments</span><p className="text-lg font-semibold">{evidence.adjustmentCount}</p><p className="text-xs text-muted-foreground">Absolute {money(evidence.absoluteAdjustments)} {currency}</p></div>
            <div className="rounded-md border p-3"><span className="text-muted-foreground">Exceptions</span><p className="text-lg font-semibold">{evidence.exceptionCount}</p><p className="text-xs text-muted-foreground">Net adjustment {money(evidence.netAdjustments)} {currency}</p></div>
        </div>

        {initialization.decidedByUserId && <div className="rounded-md border p-3 text-sm"><span className="text-muted-foreground">Decision evidence</span><p>{initialization.status} by {initialization.decidedByName || `user ${initialization.decidedByUserId}`}{initialization.decidedAtUtc ? ` on ${new Date(initialization.decidedAtUtc).toLocaleString()}` : ''}</p>{initialization.decisionReason && <p className="text-muted-foreground">Reason: {initialization.decisionReason}</p>}</div>}

        <div className="rounded-md border">
            <div className="flex flex-wrap items-center justify-between gap-2 p-3">
                <div><p className="font-medium">Account-level reconciliation</p><p className="text-sm text-muted-foreground">Review source balances, adjustments and proposed opening balances before deciding.</p></div>
                <div className="flex gap-2">
                    <Button type="button" variant="outline" size="sm" onClick={downloadEvidence}><Download className="mr-2 h-4 w-4" />Download CSV</Button>
                    <Button type="button" variant="outline" size="sm" onClick={() => setExpanded(current => !current)}>{expanded ? <ChevronUp className="mr-2 h-4 w-4" /> : <ChevronDown className="mr-2 h-4 w-4" />}{expanded ? 'Hide evidence' : 'Review evidence'}</Button>
                </div>
            </div>
            {expanded && <div className="space-y-3 border-t p-3">
                <div className="flex flex-wrap items-center gap-2">
                    <div className="relative min-w-[240px] flex-1"><Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" /><Input aria-label="Search initialization evidence" className="pl-9" placeholder="Search account number or name…" value={search} onChange={event => setSearch(event.target.value)} /></div>
                    {(['all', 'adjustments', 'exceptions'] as EvidenceFilter[]).map(value => <Button key={value} type="button" size="sm" variant={filter === value ? 'default' : 'outline'} aria-pressed={filter === value} onClick={() => setFilter(value)}>{value === 'all' ? `All (${evidence.rows.length})` : value === 'adjustments' ? `Adjustments (${evidence.adjustmentCount})` : `Exceptions (${evidence.exceptionCount})`}</Button>)}
                </div>
                <div className="max-h-[460px] overflow-auto rounded-md border">
                    <Table>
                        <TableHeader><TableRow><TableHead>Account</TableHead><TableHead>Type</TableHead><TableHead className="text-right">Source signed</TableHead><TableHead className="text-right">Adjustment</TableHead><TableHead className="text-right">Debit</TableHead><TableHead className="text-right">Credit</TableHead><TableHead className="text-right">Resulting signed</TableHead><TableHead className="text-right">Variance</TableHead></TableRow></TableHeader>
                        <TableBody>{visibleRows.length === 0 ? <TableRow><TableCell colSpan={8} className="h-20 text-center text-muted-foreground">No evidence lines match this view.</TableCell></TableRow> : visibleRows.map(line => <TableRow key={line.accountId} className={line.isException ? 'bg-destructive/5' : undefined}><TableCell><p className="font-medium">{clientFacingAccountNumber(line.accountNumber, tenantCode) || line.accountId}</p><p className="text-xs text-muted-foreground">{line.accountName || 'Account name unavailable'}</p></TableCell><TableCell>{line.accountType || '—'}</TableCell><TableCell className="text-right tabular-nums">{initialization.mode === 'IndependentOpeningBalances' ? '—' : money(line.baseBookSignedBalance)}</TableCell><TableCell className="text-right tabular-nums">{money(line.openingAdjustment)}</TableCell><TableCell className="text-right tabular-nums">{money(line.openingDebit)}</TableCell><TableCell className="text-right tabular-nums">{money(line.openingCredit)}</TableCell><TableCell className="text-right tabular-nums">{money(line.proposedSignedBalance)}</TableCell><TableCell className="text-right tabular-nums">{initialization.mode === 'IndependentOpeningBalances' ? '—' : money(line.variance)}</TableCell></TableRow>)}</TableBody>
                    </Table>
                </div>
            </div>}
        </div>

        <div className="text-sm"><span className="text-muted-foreground">Evidence verification code</span><p className="break-all font-mono text-xs">{initialization.evidenceFingerprint}</p><p className="mt-1 text-xs text-muted-foreground">A system-generated SHA-256 checksum. Finance re-derives it with the reconciliation checksum to detect any change before activation.</p></div>
    </div>;
}
