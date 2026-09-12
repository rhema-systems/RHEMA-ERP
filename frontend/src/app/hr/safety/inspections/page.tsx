'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Plus, AlertTriangle, CalendarClock } from 'lucide-react';
import { Button } from '@/components/ui/button';
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
import { RiskBadge } from '@/components/hr/safety/RiskBadge';
import { safetyInspectionService } from '@/services/hr/safety-inspection.service';
import {
  SHE_INSPECTION_STATUS_OPTIONS,
  SHE_INSPECTION_CATEGORY_OPTIONS,
  type SheInspectionStatus,
  type SheInspectionCategory,
} from '@/types/hr/safety-inspections';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

type QuickView = 'all' | 'due' | 'open-findings';

/**
 * The inspection register, newest first. Findings, discovered hazards and close-out run from the
 * inspection itself; the two quick views surface what needs attention (due soon, open findings).
 */
export default function InspectionRegisterPage() {
  const [view, setView] = useState<QuickView>('all');
  const [status, setStatus] = useState<SheInspectionStatus | 'all'>('all');
  const [category, setCategory] = useState<SheInspectionCategory | 'all'>('all');

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'safety-inspections', view, status],
    queryFn: () =>
      view === 'due'
        ? safetyInspectionService.getDue()
        : view === 'open-findings'
          ? safetyInspectionService.getOpenWithFindings()
          : status === 'all'
            ? safetyInspectionService.getAll()
            : safetyInspectionService.getByStatus(status),
  });

  const items = (data ?? []).filter((i) => category === 'all' || i.category === category);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Safety Inspections"
        description="Scheduled and completed inspections with their findings. An inspection cannot close while a finding is unresolved or a corrective action is open."
        backHref="/hr/safety"
        actions={
          <Button asChild>
            <Link href="/hr/safety/inspections/new">
              <Plus className="mr-2 h-4 w-4" />
              Schedule inspection
            </Link>
          </Button>
        }
      />

      <div className="flex flex-wrap items-center gap-3">
        <Select
          value={status}
          onValueChange={(v) => setStatus(v as SheInspectionStatus | 'all')}
          disabled={view !== 'all'}
        >
          <SelectTrigger className="w-56">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            {SHE_INSPECTION_STATUS_OPTIONS.map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select
          value={category}
          onValueChange={(v) => setCategory(v as SheInspectionCategory | 'all')}
        >
          <SelectTrigger className="w-52">
            <SelectValue placeholder="All categories" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All categories</SelectItem>
            {SHE_INSPECTION_CATEGORY_OPTIONS.map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <div className="flex items-center gap-2">
          <Switch
            id="view-due"
            checked={view === 'due'}
            onCheckedChange={(on) => setView(on ? 'due' : 'all')}
          />
          <Label htmlFor="view-due" className="flex items-center gap-1 text-sm">
            <CalendarClock className="h-4 w-4 text-amber-600" />
            Due in 30 days
          </Label>
        </div>
        <div className="flex items-center gap-2">
          <Switch
            id="view-findings"
            checked={view === 'open-findings'}
            onCheckedChange={(on) => setView(on ? 'open-findings' : 'all')}
          />
          <Label htmlFor="view-findings" className="flex items-center gap-1 text-sm">
            <AlertTriangle className="h-4 w-4 text-red-600" />
            Open findings
          </Label>
        </div>
        {data && (
          <span className="text-muted-foreground text-sm">
            {items.length} inspection{items.length === 1 ? '' : 's'}
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
              title="No inspections"
              description={
                view === 'all' && status === 'all' && category === 'all'
                  ? 'Schedule the first inspection.'
                  : 'Nothing matches these filters.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Date</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead>Location</TableHead>
                  <TableHead>Inspector</TableHead>
                  <TableHead>Risk</TableHead>
                  <TableHead className="text-right">Score</TableHead>
                  <TableHead className="text-right">Open items</TableHead>
                  <TableHead>Next due</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((i) => (
                  <TableRow key={i.id}>
                    <TableCell className="font-medium">
                      <Link
                        href={`/hr/safety/inspections/${i.id}`}
                        className="font-mono hover:underline"
                      >
                        {i.inspectionNumber}
                      </Link>
                    </TableCell>
                    <TableCell>{fmtDate(i.inspectionDate)}</TableCell>
                    <TableCell>{i.typeName}</TableCell>
                    <TableCell>{i.categoryName}</TableCell>
                    <TableCell>{i.locationName ?? '—'}</TableCell>
                    <TableCell>{i.inspectorName || '—'}</TableCell>
                    <TableCell>
                      {i.overallRiskRating ? <RiskBadge level={i.overallRiskRating} /> : '—'}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {i.complianceScore != null ? `${i.complianceScore}%` : '—'}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {i.openItemCount > 0 ? (
                        <span className="flex items-center justify-end gap-1">
                          <AlertTriangle className="h-3.5 w-3.5 text-amber-600" />
                          {i.openItemCount}
                        </span>
                      ) : (
                        0
                      )}
                    </TableCell>
                    <TableCell>{fmtDate(i.nextInspectionDueDate)}</TableCell>
                    <TableCell>
                      <StatusBadge status={i.statusName} />
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
