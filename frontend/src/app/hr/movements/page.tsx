'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Plus, Clock, CalendarClock, Handshake, UserCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
import { movementService } from '@/services/hr/movement.service';
import {
  MOVEMENT_TYPES,
  type StaffMovementType,
  type StaffMovementSummary,
} from '@/types/hr/movements';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

type QuickView = 'all' | 'pending-approval' | 'awaiting-acceptance' | 'awaiting-handover' | 'expiring';

const VIEWS: { key: QuickView; label: string; icon: typeof Clock; hint: string }[] = [
  { key: 'all', label: 'All movements', icon: Clock, hint: 'Every movement on record' },
  {
    key: 'pending-approval',
    label: 'Pending approval',
    icon: Clock,
    hint: 'Somewhere in the approval route',
  },
  {
    key: 'awaiting-acceptance',
    label: 'Awaiting employee',
    icon: UserCheck,
    hint: 'The employee has not yet accepted or declined',
  },
  {
    key: 'awaiting-handover',
    label: 'Awaiting handover',
    icon: Handshake,
    hint: 'Handover required and not yet completed',
  },
  {
    key: 'expiring',
    label: 'Ending soon',
    icon: CalendarClock,
    hint: 'Temporary assignments ending within 30 days',
  },
];

/**
 * The movement register. A movement is the paperwork for a change to someone's position, unit,
 * reporting line and pay — raised here, routed for approval, then applied to the employee record.
 */
export default function MovementRegisterPage() {
  const [view, setView] = useState<QuickView>('all');
  const [type, setType] = useState<StaffMovementType | 'all'>('all');

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'movements', 'register', view],
    queryFn: (): Promise<StaffMovementSummary[]> => {
      switch (view) {
        case 'pending-approval':
          return movementService.getPendingApproval();
        case 'awaiting-acceptance':
          return movementService.getPendingEmployeeAcceptance();
        case 'awaiting-handover':
          return movementService.getPendingHandover();
        case 'expiring':
          return movementService.getExpiringTemporary(30);
        default:
          return movementService.getAll();
      }
    },
  });

  const items = (data ?? []).filter((m) => type === 'all' || m.movementType === type);
  const activeView = VIEWS.find((v) => v.key === view) ?? VIEWS[0];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Staff movements"
        description="Promotions, transfers, demotions, secondments, acting appointments and redesignations."
        backHref="/hr"
        actions={
          <Button asChild>
            <Link href="/hr/movements/new">
              <Plus className="mr-2 h-4 w-4" />
              Raise movement
            </Link>
          </Button>
        }
      />

      <div className="flex flex-wrap gap-2">
        {VIEWS.map((v) => (
          <Button
            key={v.key}
            variant={view === v.key ? 'default' : 'outline'}
            size="sm"
            onClick={() => setView(v.key)}
          >
            <v.icon className="mr-2 h-4 w-4" />
            {v.label}
          </Button>
        ))}
      </div>

      <Card>
        <CardContent className="p-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="text-sm text-muted-foreground">{activeView.hint}</p>
            <Select value={type} onValueChange={(v) => setType(v as StaffMovementType | 'all')}>
              <SelectTrigger className="w-56">
                <SelectValue placeholder="All types" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All types</SelectItem>
                {MOVEMENT_TYPES.map((t) => (
                  <SelectItem key={t} value={t}>
                    {t.replace(/([A-Z])/g, ' $1').trim()}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : items.length === 0 ? (
            <EmptyState
              title="No movements"
              description={
                view === 'all'
                  ? 'Nothing has been raised yet.'
                  : 'Nothing is in this queue at the moment.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>From</TableHead>
                  <TableHead>To</TableHead>
                  <TableHead>Effective</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((m) => (
                  <TableRow key={m.id} className="cursor-pointer">
                    <TableCell className="font-medium">
                      <Link href={`/hr/movements/${m.id}`} className="hover:underline">
                        {m.movementNumber}
                      </Link>
                    </TableCell>
                    <TableCell>
                      <div>{m.employeeName}</div>
                      {m.employeeNumber && (
                        <div className="text-xs text-muted-foreground">{m.employeeNumber}</div>
                      )}
                    </TableCell>
                    <TableCell>
                      <div>{m.movementTypeName}</div>
                      {m.isTemporary && (
                        <div className="text-xs text-muted-foreground">
                          Temporary — ends {fmtDate(m.temporaryEndDate)}
                        </div>
                      )}
                    </TableCell>
                    <TableCell>
                      <div>{m.currentPositionTitle || '—'}</div>
                      <div className="text-xs text-muted-foreground">
                        {m.currentOrganizationUnitName}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div>{m.newPositionTitle || '—'}</div>
                      <div className="text-xs text-muted-foreground">
                        {m.newOrganizationUnitName}
                      </div>
                    </TableCell>
                    <TableCell>{fmtDate(m.effectiveDate)}</TableCell>
                    <TableCell>
                      <StatusBadge status={m.status} />
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
