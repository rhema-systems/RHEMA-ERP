'use client';

import Link from 'next/link';
import React from 'react';
import { ArrowLeft, RefreshCw } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { estateFacilitiesService, type FacilitiesBudgetReport, type FacilitiesBudgetYear } from '@/services/estate-facilities.service';

export default function FacilitiesBudgetPage() {
  const [years, setYears] = React.useState<FacilitiesBudgetYear[]>([]);
  const [yearId, setYearId] = React.useState('');
  const [report, setReport] = React.useState<FacilitiesBudgetReport | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  const loadYears = React.useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const options = await estateFacilitiesService.getBudgetYears();
      setYears(options);
      setYearId((current) => options.some((year) => year.id === current)
        ? current : (options.find((year) => year.hasOfficialBudget)?.id ?? options[0]?.id ?? ''));
    } catch {
      setError('Unable to load fiscal years.');
    } finally {
      setLoading(false);
    }
  }, []);

  React.useEffect(() => { void loadYears(); }, [loadYears]);

  React.useEffect(() => {
    setReport(null);
    if (!yearId) return;
    const selected = years.find((year) => year.id === yearId);
    if (!selected?.hasOfficialBudget) { setReport(null); return; }
    let active = true;
    setLoading(true);
    setError(null);
    void estateFacilitiesService.getBudgetReport(yearId)
      .then((result) => { if (active) setReport(result); })
      .catch(() => { if (active) { setReport(null); setError('Unable to load the official Facilities budget.'); } })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [yearId, years]);

  const selectedYear = years.find((year) => year.id === yearId);
  const money = (amount: number) => `${report?.currencyCode ?? 'GHS'} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

  return <div className="space-y-5">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div className="flex items-center gap-3">
        <Button asChild size="icon" variant="ghost" title="Back to Facilities dashboard">
          <Link href="/estate/facilities/dashboard" aria-label="Back to Facilities dashboard"><ArrowLeft className="h-4 w-4" /></Link>
        </Button>
        <h1 className="text-xl font-semibold">Facilities budget vs actual</h1>
      </div>
      <Button size="icon" variant="outline" onClick={() => void loadYears()} title="Refresh budget" aria-label="Refresh budget">
        <RefreshCw className="h-4 w-4" />
      </Button>
    </div>

    <div className="flex flex-wrap items-end gap-4 border-b pb-4">
      <div className="w-64 space-y-1">
        <label className="text-sm font-medium" htmlFor="facilities-budget-year">Fiscal year</label>
        <Select value={yearId} onValueChange={setYearId}>
          <SelectTrigger id="facilities-budget-year"><SelectValue placeholder="Select fiscal year" /></SelectTrigger>
          <SelectContent>{years.map((year) => <SelectItem key={year.id} value={year.id}>{year.fiscalYearName}</SelectItem>)}</SelectContent>
        </Select>
      </div>
      <div className="text-sm text-muted-foreground">Department: Facilities</div>
      {report ? <div className="text-sm text-muted-foreground">Official budget: {report.scenarioName}</div> : null}
    </div>

    {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
    {loading ? <p className="text-sm text-muted-foreground">Loading budget...</p> : null}
    {!loading && years.length === 0 ? <p className="text-sm text-muted-foreground">No fiscal years are configured.</p> : null}
    {!loading && selectedYear && !selectedYear.hasOfficialBudget ? (
      <p className="text-sm text-muted-foreground">No official budget has been adopted for {selectedYear.fiscalYearName}.</p>
    ) : null}
    {!loading && report && report.lines.length === 0 ? (
      <p className="text-sm text-muted-foreground">The official budget has no expense lines assigned to the Facilities department dimension.</p>
    ) : null}
    {report && report.lines.length > 0 ? <>
      <div className="flex flex-wrap gap-x-8 gap-y-2 text-sm">
        <div>Planned <strong className="ml-2">{money(report.plannedAmount)}</strong></div>
        <div>Actual expense <strong className="ml-2">{money(report.actualExpense)}</strong></div>
        <div>Variance <strong className="ml-2">{money(report.variance)}</strong></div>
      </div>
      <div className="overflow-x-auto rounded-md border">
        <Table>
          <TableHeader><TableRow>
            <TableHead>Period</TableHead><TableHead>Account</TableHead>
            <TableHead className="text-right">Planned</TableHead>
            <TableHead className="text-right">Actual</TableHead>
            <TableHead className="text-right">Variance</TableHead>
          </TableRow></TableHeader>
          <TableBody>{report.lines.map((line, index) => <TableRow key={`${line.periodCode}-${line.accountCode}-${index}`}>
            <TableCell>{line.periodCode}</TableCell>
            <TableCell><span className="font-medium">{line.accountCode}</span> {line.accountName}</TableCell>
            <TableCell className="text-right">{money(line.plannedAmount)}</TableCell>
            <TableCell className="text-right">{money(line.actualExpense)}</TableCell>
            <TableCell className="text-right">{money(line.variance)}</TableCell>
          </TableRow>)}</TableBody>
        </Table>
      </div>
    </> : null}
  </div>;
}
