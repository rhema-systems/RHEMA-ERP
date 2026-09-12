'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Plus, ShieldAlert } from 'lucide-react';
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
import { RiskBadge } from '@/components/hr/safety/RiskBadge';
import { safetyHazardService } from '@/services/hr/safety-hazard.service';
import {
  SHE_HAZARD_STATUS_OPTIONS,
  SHE_HAZARD_CATEGORY_OPTIONS,
  type SheHazardStatus,
  type SheHazardCategory,
} from '@/types/hr/safety-hazards';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * The living hazard register (HR view), highest residual risk first. Employees flag hazards
 * through the open report form; this is where SHE assesses them, records the hierarchy of
 * controls, and tracks each one to resolution.
 */
export default function HazardRegisterPage() {
  const [status, setStatus] = useState<SheHazardStatus | 'all'>('all');
  const [category, setCategory] = useState<SheHazardCategory | 'all'>('all');
  const [highRiskOnly, setHighRiskOnly] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'safety-hazards', status, highRiskOnly],
    queryFn: () =>
      highRiskOnly
        ? safetyHazardService.getHighResidualRisk()
        : status === 'all'
          ? safetyHazardService.getAll()
          : safetyHazardService.getByStatus(status),
  });

  const items = (data ?? []).filter((h) => category === 'all' || h.category === category);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Hazard Register"
        description="Every identified hazard with its inherent and residual risk, highest residual risk first. Controls and corrective actions live on the hazard itself."
        backHref="/hr/safety"
        actions={
          <Button asChild>
            <Link href="/hr/safety/hazards/new">
              <Plus className="mr-2 h-4 w-4" />
              Record hazard
            </Link>
          </Button>
        }
      />

      <div className="flex flex-wrap items-center gap-3">
        <Select
          value={status}
          onValueChange={(v) => setStatus(v as SheHazardStatus | 'all')}
          disabled={highRiskOnly}
        >
          <SelectTrigger className="w-52">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            {SHE_HAZARD_STATUS_OPTIONS.map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={category} onValueChange={(v) => setCategory(v as SheHazardCategory | 'all')}>
          <SelectTrigger className="w-52">
            <SelectValue placeholder="All categories" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All categories</SelectItem>
            {SHE_HAZARD_CATEGORY_OPTIONS.map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <div className="flex items-center gap-2">
          <Switch id="high-risk" checked={highRiskOnly} onCheckedChange={setHighRiskOnly} />
          <Label htmlFor="high-risk" className="flex items-center gap-1 text-sm">
            <ShieldAlert className="h-4 w-4 text-red-600" />
            High risk only (score ≥ 12)
          </Label>
        </div>
        {data && (
          <span className="text-muted-foreground text-sm">
            {items.length} hazard{items.length === 1 ? '' : 's'}
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
              title="No hazards"
              description={
                status === 'all' && category === 'all' && !highRiskOnly
                  ? 'Nothing on the register yet. Hazards land here from the open report form.'
                  : 'Nothing matches these filters.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Hazard</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead>Location</TableHead>
                  <TableHead className="text-right">Inherent</TableHead>
                  <TableHead className="text-right">Residual</TableHead>
                  <TableHead>Risk level</TableHead>
                  <TableHead>Owner</TableHead>
                  <TableHead>Review due</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((h) => (
                  <TableRow key={h.id}>
                    <TableCell className="font-medium">
                      <Link href={`/hr/safety/hazards/${h.id}`} className="hover:underline">
                        {h.name}
                      </Link>
                      {h.code && (
                        <span className="text-muted-foreground ml-2 font-mono text-xs">
                          {h.code}
                        </span>
                      )}
                      {!h.isActive && (
                        <Badge variant="outline" className="ml-2">
                          Inactive
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell>{h.categoryName}</TableCell>
                    <TableCell>{h.locationName ?? '—'}</TableCell>
                    <TableCell className="text-right tabular-nums">{h.inherentRiskScore}</TableCell>
                    <TableCell className="text-right tabular-nums">{h.residualRiskScore}</TableCell>
                    <TableCell>
                      <RiskBadge level={h.residualRiskLevel} label={h.residualRiskLevelName} />
                    </TableCell>
                    <TableCell>{h.ownerName ?? '—'}</TableCell>
                    <TableCell>{fmtDate(h.reviewDueDate)}</TableCell>
                    <TableCell>
                      <StatusBadge status={h.statusName} />
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
