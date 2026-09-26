'use client';

import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { FileText, Loader2, Printer } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
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
import { assetRegisterService } from '@/services/hr/asset-register.service';
import { locationService } from '@/services/hr/location.service';
import { COMPANY_ASSET_STATUSES } from '@/types/hr/assets';
import type { AssetRegisterGroup } from '@/types/hr/assets';
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';

const ANY = '__any__';
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtNum = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { maximumFractionDigits: 2 });

function Stat({ label, value, tone }: { label: string; value: React.ReactNode; tone?: 'danger' | 'warning' }) {
  return (
    <div className="rounded-md border p-3">
      <div className="text-xs uppercase tracking-wide text-muted-foreground">{label}</div>
      <div className={
        tone === 'danger' ? 'mt-1 text-xl font-semibold text-red-600 dark:text-red-500'
          : tone === 'warning' ? 'mt-1 text-xl font-semibold text-amber-600 dark:text-amber-500'
            : 'mt-1 text-xl font-semibold'
      }>
        {value}
      </div>
    </div>
  );
}

function Breakdown({ title, rows, total }: { title: string; rows: AssetRegisterGroup[]; total: number }) {
  const sum = rows.reduce((n, r) => n + r.assetCount, 0);
  return (
    <Card className="break-inside-avoid">
      <CardHeader className="pb-2">
        <CardTitle className="text-base">{title}</CardTitle>
      </CardHeader>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{title}</TableHead>
              <TableHead className="text-right">Assets</TableHead>
              <TableHead className="text-right">Issued</TableHead>
              <TableHead className="text-right">Purchase cost</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.id ?? r.name}>
                {/* An unplaced asset is grouped and NAMED, never dropped — a breakdown that
                    silently omits it produces columns that do not add up to their own header. */}
                <TableCell className={r.id === null ? 'text-muted-foreground' : ''}>{r.name}</TableCell>
                <TableCell className="text-right">{r.assetCount}</TableCell>
                <TableCell className="text-right">{r.assignedCount}</TableCell>
                <TableCell className="text-right">{fmtNum(r.totalPurchaseCost)}</TableCell>
              </TableRow>
            ))}
            <TableRow className="font-medium">
              <TableCell>Total</TableCell>
              <TableCell className="text-right">{sum}</TableCell>
              <TableCell colSpan={2} className="text-right text-xs text-muted-foreground">
                {sum === total ? 'adds up to the register' : `⚠ register total is ${total}`}
              </TableCell>
            </TableRow>
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

/**
 * The register report — the estate counted and totalled on one printable page.
 *
 * A summary, not a listing. The rows are already on the register under the same filters, and
 * repeating them would make the document unbounded in exactly the case it is most wanted.
 *
 * ⚠ **The watchlist counts here are DISJOINT** — "due soon" excludes "overdue" — while the
 * watchlist *screens* are inclusive. That is deliberate and it is why the labels here read
 * "due soon" rather than "due": three numbers in a row get added up, and a reader entitled to do
 * that arithmetic must not be misled by it. Do not copy these figures onto a list screen, or
 * vice versa.
 *
 * ⚠ **The filters propagate into those counts.** A report filtered to one location carries that
 * location's overdue count, not the organisation's — which is why the printed header states every
 * filter in force. A page that showed the numbers without the scope would be read as the estate.
 *
 * ⚠ The two money totals are summed **without a currency**: the register holds no currency code for
 * a purchase cost or an insured value. Safe only because nothing in it can express a second
 * currency, and registered for the HR↔Finance sweep rather than solved here.
 */
export default function AssetRegisterReportPage() {
  const [assetTypeId, setAssetTypeId] = useState(ANY);
  const [unitId, setUnitId] = useState(ANY);
  const [locationId, setLocationId] = useState(ANY);
  const [status, setStatus] = useState(ANY);
  const [asOf, setAsOf] = useState('');

  const { data: types = [] } = useQuery({
    queryKey: ['hr', 'assets', 'types'],
    queryFn: () => assetRegisterService.getTypes(),
  });
  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const { data: r, isLoading } = useQuery({
    queryKey: ['hr', 'assets', 'report', assetTypeId, unitId, locationId, status, asOf],
    queryFn: () =>
      assetRegisterService.getRegisterReport({
        assetTypeId: assetTypeId === ANY ? undefined : assetTypeId,
        unitId: unitId === ANY ? undefined : unitId,
        locationId: locationId === ANY ? undefined : locationId,
        status: status === ANY
          ? undefined
          : COMPANY_ASSET_STATUSES.find((s) => s.label === status)?.value,
        asOf: asOf || undefined,
      }),
  });

  /**
   * ⚠ A Tailwind `print:hidden` on this page's own header hides the header and nothing else — the
   * sidebar and the top bar still land on the paper. The repo's established pattern (finance's PO
   * and GRV screens, payroll's payslip) is a body class that hides everything and re-shows one
   * print root, and that is what actually prints a document rather than a screenshot of the app.
   */
  const print = () => {
    const cleanup = () => {
      document.body.classList.remove('printing-hr-asset-report');
      window.removeEventListener('afterprint', cleanup);
    };
    document.body.classList.add('printing-hr-asset-report');
    window.addEventListener('afterprint', cleanup);
    window.print();
  };

  // Leaving the class behind would blank the next screen the user prints from.
  useEffect(() => () => document.body.classList.remove('printing-hr-asset-report'), []);

  const filtersInForce = [
    r?.assetTypeName && `type: ${r.assetTypeName}`,
    r?.unitName && `unit: ${r.unitName}`,
    r?.locationName && `location: ${r.locationName}`,
    r?.statusName && `status: ${r.statusName}`,
  ].filter(Boolean) as string[];

  return (
    <div className="space-y-6 p-6 print:p-0">
      {/* The controls are the page's, not the document's. */}
      <div className="hr-asset-report-no-print print:hidden">
        <PageHeader
          title="Asset register report"
          description="The estate counted and totalled on one page."
          backHref="/hr/assets"
          actions={
            <Button onClick={print}>
              <Printer className="mr-2 h-4 w-4" /> Print
            </Button>
          }
        />
      </div>

      <Card className="hr-asset-report-no-print print:hidden">
        <CardContent className="grid gap-4 p-4 sm:grid-cols-3 lg:grid-cols-5">
          <div className="space-y-2">
            <Label>Asset type</Label>
            <Select value={assetTypeId} onValueChange={setAssetTypeId}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value={ANY}>All types</SelectItem>
                {types.map((t) => <SelectItem key={t.id} value={t.id}>{t.name}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <OrganizationUnitPicker
              value={unitId === ANY ? '' : unitId}
              onChange={(id) => setUnitId(id || ANY)}
              allowNone="All units"
              unitLabel="Unit"
              idPrefix="report-unit"
            />
          </div>
          <div className="space-y-2">
            <Label>Location</Label>
            <Select value={locationId} onValueChange={setLocationId}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value={ANY}>All locations</SelectItem>
                {locations.map((l) => <SelectItem key={l.id} value={l.id}>{l.name}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Status</Label>
            <Select value={status} onValueChange={setStatus}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value={ANY}>Any status</SelectItem>
                {COMPANY_ASSET_STATUSES.map((s) =>
                  <SelectItem key={s.label} value={s.label}>{s.text}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>As at</Label>
            <Input type="date" value={asOf} onChange={(e) => setAsOf(e.target.value)} />
          </div>
        </CardContent>
      </Card>

      {isLoading || !r ? (
        <div className="flex justify-center p-10">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : (
        <div className="hr-asset-report-print-root space-y-6">
          {/* ── The printed header. It must state the scope, or the page reads as the estate. ── */}
          <div className="border-b pb-4">
            <div className="flex items-start gap-2">
              <FileText className="mt-1 h-5 w-5 text-muted-foreground print:hidden" />
              <div>
                <h2 className="text-xl font-semibold">Company asset register</h2>
                <p className="text-sm text-muted-foreground">
                  As at {fmtDate(r.asOf)} · generated {new Date(r.generatedAt).toLocaleString()}
                </p>
                <p className="mt-1 text-sm">
                  {filtersInForce.length === 0
                    ? 'The whole register, unfiltered.'
                    : <>Filtered — <span className="font-medium">{filtersInForce.join(' · ')}</span>.
                      {' '}Every figure below, the watchlist counts included, covers only this selection.</>}
                </p>
              </div>
            </div>
          </div>

          <section className="space-y-3 break-inside-avoid">
            <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
              The population
            </h3>
            <div className="grid gap-3 sm:grid-cols-3 lg:grid-cols-5">
              <Stat label="Assets" value={r.assetCount} />
              <Stat label="Issued out" value={r.assignedCount} />
              <Stat label="In store" value={r.unassignedCount} />
              <Stat label="Issuable" value={r.assignableCount} />
              <Stat label="Rentable" value={r.rentableCount} />
              <Stat label="Insured" value={r.insuredCount} />
              <Stat label="Uninsured" value={r.uninsuredCount} />
              <Stat label="From fixed assets" value={r.fromFixedAssetsCount} />
              <Stat label="Registered in HR" value={r.hrCreatedCount} />
              <Stat label="Linked to Maintenance" value={r.linkedToMaintenanceCount} />
            </div>
          </section>

          <section className="space-y-3 break-inside-avoid">
            <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
              Money
            </h3>
            <div className="grid gap-3 sm:grid-cols-3">
              <Stat label="Total purchase cost" value={fmtNum(r.totalPurchaseCost)} />
              <Stat label="Total insured value" value={fmtNum(r.totalInsuredValue)} />
              <Stat label="No purchase cost recorded" value={r.assetsWithoutPurchaseCost} />
            </div>
            <p className="text-xs text-muted-foreground">
              Totals are unqualified by currency: the register holds no currency code for a purchase
              cost or an insured value. {r.assetsWithoutPurchaseCost > 0 && (
                <>They also exclude {r.assetsWithoutPurchaseCost} asset
                  {r.assetsWithoutPurchaseCost === 1 ? '' : 's'} with no cost recorded.</>
              )}
            </p>
          </section>

          <section className="space-y-3 break-inside-avoid">
            <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
              Needing attention
            </h3>
            <div className="grid gap-3 sm:grid-cols-3 lg:grid-cols-4">
              <Stat label="Service due soon" value={r.maintenanceDueSoonCount} tone="warning" />
              <Stat label="Service overdue" value={r.maintenanceOverdueCount} tone="danger" />
              <Stat label="Never scheduled" value={r.maintenanceUnscheduledCount} tone="warning" />
              <Stat label="Returns overdue" value={r.returnsOverdueCount} tone="danger" />
              <Stat label="Cover lapsing soon" value={r.insuranceExpiringSoonCount} tone="warning" />
              <Stat label="Cover lapsed" value={r.insuranceExpiredCount} tone="danger" />
              <Stat label="Cover never dated" value={r.insuranceUndatedCount} tone="warning" />
              <Stat
                label="Charges outstanding"
                value={`${r.outstandingSurchargeCount} · ${fmtNum(r.outstandingSurchargeAmount)}`}
              />
            </div>
            <p className="text-xs text-muted-foreground">
              These counts do not overlap: an asset counted as overdue is not also counted as due
              soon. The watchlist screens read the other way round — a list of what is due includes
              what has already lapsed — so the two are not interchangeable.
            </p>
          </section>

          <section className="grid gap-4 lg:grid-cols-2">
            <Breakdown title="By status" rows={r.byStatus} total={r.assetCount} />
            <Breakdown title="By condition" rows={r.byCondition} total={r.assetCount} />
            <Breakdown title="By asset type" rows={r.byAssetType} total={r.assetCount} />
            <Breakdown title="By unit" rows={r.byUnit} total={r.assetCount} />
            <Breakdown title="By location" rows={r.byLocation} total={r.assetCount} />
          </section>
        </div>
      )}
    </div>
  );
}
