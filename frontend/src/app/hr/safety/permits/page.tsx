'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Plus, PauseCircle, TimerOff } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Switch } from '@/components/ui/switch';
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
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { safetyPermitService } from '@/services/hr/safety-permit.service';
import {
  SHE_PERMIT_STATUS_OPTIONS,
  SHE_PERMIT_TYPE_OPTIONS,
  type ShePermitStatus,
  type ShePermitType,
} from '@/types/hr/safety-permits';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

type QuickView = 'all' | 'expiring' | 'suspended';

/**
 * The permit register, newest planned start first. Approval, suspension, extension and close-out
 * run from the permit itself. "Expiring" is a query over the validity window — expiry is not yet
 * automatic (that is the slice-13 job engine), so nothing here flips to Expired on its own.
 */
export default function PermitRegisterPage() {
  const [view, setView] = useState<QuickView>('all');
  const [status, setStatus] = useState<ShePermitStatus | 'all'>('all');
  const [type, setType] = useState<ShePermitType | 'all'>('all');

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'safety-permits', view, status],
    queryFn: () =>
      view === 'expiring'
        ? safetyPermitService.getExpiring(3)
        : view === 'suspended'
          ? safetyPermitService.getSuspended()
          : status === 'all'
            ? safetyPermitService.getAll()
            : safetyPermitService.getByStatus(status),
  });

  const items = (data ?? []).filter((p) => type === 'all' || p.permitType === type);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Permits to Work"
        description="Authorisation for hazardous work. A permit cannot be approved until its hazards, controls — and for hot work and confined spaces, gas testing — are recorded."
        backHref="/hr/safety"
        actions={
          <Button asChild>
            <Link href="/hr/safety/permits/new">
              <Plus className="mr-2 h-4 w-4" />
              Request permit
            </Link>
          </Button>
        }
      />

      <div className="flex flex-wrap items-center gap-3">
        <Select
          value={status}
          onValueChange={(v) => setStatus(v as ShePermitStatus | 'all')}
          disabled={view !== 'all'}
        >
          <SelectTrigger className="w-52">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            {SHE_PERMIT_STATUS_OPTIONS.map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={type} onValueChange={(v) => setType(v as ShePermitType | 'all')}>
          <SelectTrigger className="w-56">
            <SelectValue placeholder="All types" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All types</SelectItem>
            {SHE_PERMIT_TYPE_OPTIONS.map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <div className="flex items-center gap-2">
          <Switch
            id="view-expiring"
            checked={view === 'expiring'}
            onCheckedChange={(on) => setView(on ? 'expiring' : 'all')}
          />
          <Label htmlFor="view-expiring" className="flex items-center gap-1 text-sm">
            <TimerOff className="h-4 w-4 text-amber-600" />
            Window ends in 3 days
          </Label>
        </div>
        <div className="flex items-center gap-2">
          <Switch
            id="view-suspended"
            checked={view === 'suspended'}
            onCheckedChange={(on) => setView(on ? 'suspended' : 'all')}
          />
          <Label htmlFor="view-suspended" className="flex items-center gap-1 text-sm">
            <PauseCircle className="h-4 w-4 text-red-600" />
            Suspended
          </Label>
        </div>
        {data && (
          <span className="text-muted-foreground text-sm">
            {items.length} permit{items.length === 1 ? '' : 's'}
          </span>
        )}
      </div>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
            </div>
          ) : items.length === 0 ? (
            <EmptyState
              title="No permits"
              description={
                view === 'all' && status === 'all' && type === 'all'
                  ? 'Request the first permit for hazardous work.'
                  : 'Nothing matches these filters.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Work</TableHead>
                  <TableHead>Location</TableHead>
                  <TableHead>Requested by</TableHead>
                  <TableHead>Contractor</TableHead>
                  <TableHead>Window</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((p) => (
                  <TableRow key={p.id}>
                    <TableCell className="font-medium">
                      <Link
                        href={`/hr/safety/permits/${p.id}`}
                        className="font-mono hover:underline"
                      >
                        {p.permitNumber}
                      </Link>
                    </TableCell>
                    <TableCell>{p.permitTypeName}</TableCell>
                    <TableCell className="max-w-[280px] truncate">{p.workDescription}</TableCell>
                    <TableCell>{p.locationName ?? '—'}</TableCell>
                    <TableCell>{p.requestedByName || '—'}</TableCell>
                    <TableCell>{p.contractorName ?? '—'}</TableCell>
                    <TableCell className="whitespace-nowrap">
                      {fmtDate(p.plannedStartDate)} → {fmtDate(p.plannedEndDate)}
                    </TableCell>
                    <TableCell>
                      <span className="flex items-center gap-2">
                        <StatusBadge status={p.statusName} />
                        {p.isSuspended && <Badge variant="destructive">Suspended</Badge>}
                      </span>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
