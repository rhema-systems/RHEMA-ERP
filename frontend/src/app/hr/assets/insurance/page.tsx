'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, ShieldCheck } from 'lucide-react';
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
import type { AssetInsuranceWatchItem } from '@/types/hr/assets';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtNum = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { maximumFractionDigits: 2 });

function DaysCell({ days }: { days: number | null }) {
  if (days === null) return <span className="text-muted-foreground">—</span>;
  if (days < 0) {
    return (
      <span className="font-medium text-red-600 dark:text-red-500">
        lapsed {Math.abs(days)} days ago
      </span>
    );
  }
  return <span className={days <= 14 ? 'text-amber-600 dark:text-amber-500' : ''}>in {days} days</span>;
}

function InsuranceTable({
  rows, showDays = true, emptyTitle, emptyBody,
}: {
  rows: AssetInsuranceWatchItem[];
  showDays?: boolean;
  emptyTitle: string;
  emptyBody: string;
}) {
  if (rows.length === 0) {
    return <EmptyState icon={ShieldCheck} title={emptyTitle} description={emptyBody} />;
  }
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Asset</TableHead>
          <TableHead>Type</TableHead>
          <TableHead>Policy</TableHead>
          <TableHead className="text-right">Insured for</TableHead>
          <TableHead>Held by</TableHead>
          <TableHead>Status</TableHead>
          <TableHead>Cover ends</TableHead>
          {showDays && <TableHead>When</TableHead>}
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((r) => (
          <TableRow key={r.id}>
            <TableCell>
              <Link href={`/hr/assets/register/${r.id}`} className="hover:underline">
                <div className="font-medium">{r.assetName}</div>
                <div className="text-xs text-muted-foreground">{r.assetNumber}</div>
              </Link>
            </TableCell>
            <TableCell>{r.assetTypeName}</TableCell>
            <TableCell>{r.insurancePolicyNumber ?? <span className="text-muted-foreground">Not recorded</span>}</TableCell>
            <TableCell className="text-right">{fmtNum(r.insuredValue)}</TableCell>
            <TableCell>
              {r.currentAssignedToName ?? <span className="text-muted-foreground">In store</span>}
            </TableCell>
            {/* ⚠ A LostStolen asset is deliberately still on these lists — a theft claim is exactly
                what the policy is for. Only disposed assets drop off. */}
            <TableCell>{r.statusName}</TableCell>
            <TableCell>
              {r.isDated
                ? fmtDate(r.insuranceExpiryDate)
                : <span className="text-amber-600 dark:text-amber-500">Never dated</span>}
            </TableCell>
            {showDays && <TableCell><DaysCell days={r.daysRemaining} /></TableCell>}
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

/**
 * Insurance — the same three jobs the maintenance lists answer, over the cover — AST-4.
 *
 * ⚠ **The renewal window defaults to 60 days, not maintenance's 30.** A service can be booked in a
 * fortnight; a renewal is a quotation, an approval and a payment. (An assumed number rather than
 * TDC's, and flagged as such alongside the area's other assumed windows.)
 *
 * ⚠ Like the maintenance lists, "lapsing" is **inclusive** of "lapsed" — a renewal plan that hid
 * the policies already gone would be planning around a fiction. The lapsed tab is that population
 * cut out, because "what are we running uninsured" is somebody else's question.
 *
 * The undated tab is the shape most likely to be a claim nobody can actually make: an asset
 * somebody ticked as insured and never dated appears on no renewal list and can never become due.
 */
export default function AssetInsurancePage() {
  const [daysAhead, setDaysAhead] = useState(60);

  const { data: expiring = [], isLoading: loadingExpiring } = useQuery({
    queryKey: ['hr', 'assets', 'insurance', 'expiring', daysAhead],
    queryFn: () => assetRegisterService.getInsuranceExpiring(daysAhead),
  });
  const { data: expired = [], isLoading: loadingExpired } = useQuery({
    queryKey: ['hr', 'assets', 'insurance', 'expired'],
    queryFn: () => assetRegisterService.getInsuranceExpired(),
  });
  const { data: undated = [], isLoading: loadingUndated } = useQuery({
    queryKey: ['hr', 'assets', 'insurance', 'undated'],
    queryFn: () => assetRegisterService.getInsuranceUndated(),
  });

  const asOf = expiring[0]?.asOf ?? expired[0]?.asOf ?? null;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Insurance"
        description="Cover lapsing, cover lapsed, and policies nobody ever dated."
        backHref="/hr/assets"
      />

      {asOf && (
        <p className="text-sm text-muted-foreground">
          Answered by the server as at {fmtDate(asOf)}.
        </p>
      )}

      <Tabs defaultValue="expiring">
        <TabsList>
          <TabsTrigger value="expiring">Lapsing ({expiring.length})</TabsTrigger>
          <TabsTrigger value="expired">Lapsed ({expired.length})</TabsTrigger>
          <TabsTrigger value="undated">Never dated ({undated.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="expiring" className="space-y-4 pt-4">
          <Card>
            <CardContent className="flex flex-wrap items-end gap-4 p-4">
              <div className="space-y-2">
                <Label>Renewal horizon</Label>
                <Input
                  type="number"
                  min={1}
                  className="w-28"
                  value={daysAhead}
                  onChange={(e) => setDaysAhead(Math.max(1, Number(e.target.value) || 1))}
                />
              </div>
              <p className="pb-2 text-sm text-muted-foreground">
                days — longer than the servicing window on purpose: a renewal is a quotation, an
                approval and a payment. This list <span className="font-medium">includes</span>{' '}
                cover that has already lapsed.
              </p>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="p-0">
              {loadingExpiring ? (
                <div className="flex justify-center p-10">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : (
                <InsuranceTable
                  rows={expiring}
                  emptyTitle="Nothing to renew"
                  emptyBody={`No policy lapses inside the next ${daysAhead} days.`}
                />
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="expired" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {loadingExpired ? (
                <div className="flex justify-center p-10">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : (
                <InsuranceTable
                  rows={expired}
                  emptyTitle="Nothing uninsured"
                  emptyBody="No asset is running on cover that has already lapsed."
                />
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="undated" className="space-y-4 pt-4">
          <Card>
            <CardContent className="p-4 text-sm text-muted-foreground">
              These assets are marked insured and have never been given an expiry date. Both lists
              above require one, so they appear on neither and can never become due — which makes
              this the shape most likely to be a claim nobody can actually make.
            </CardContent>
          </Card>
          <Card>
            <CardContent className="p-0">
              {loadingUndated ? (
                <div className="flex justify-center p-10">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : (
                <InsuranceTable
                  rows={undated}
                  showDays={false}
                  emptyTitle="Every policy is dated"
                  emptyBody="No insured asset is missing an expiry date."
                />
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
