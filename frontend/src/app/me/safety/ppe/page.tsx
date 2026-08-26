'use client';

import { useQuery } from '@tanstack/react-query';
import { HardHat } from 'lucide-react';
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
import { safetyPpeService } from '@/services/hr/safety-ppe.service';
import type { PpeIssuance } from '@/types/hr/safety-ppe';

/**
 * The employee's own PPE record — open to every authenticated employee, resolved from the
 * login's linked employee (no SHE role needed). Read-only: issuing and returns are recorded by
 * the SHE team on the issuance register.
 *
 * Area 25 slice 8: re-homed from /hr/safety/my-ppe into the portal shell (D3 — moved, not
 * redirected; the desk keeps the SHE issuance register at /hr/safety/ppe/issuances).
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

function statusOf(i: PpeIssuance): { label: string; variant: 'secondary' | 'destructive' | 'outline' } {
  if (i.isReturned) return { label: 'Returned', variant: 'outline' };
  if (i.expectedReturnDate && new Date(i.expectedReturnDate) < new Date())
    return { label: 'Overdue return', variant: 'destructive' };
  return { label: 'Held', variant: 'secondary' };
}

export default function MyPpePage() {
  const { data: issuances = [], isLoading, error } = useQuery({
    queryKey: ['me', 'safety', 'ppe'],
    queryFn: () => safetyPpeService.getMyIssuances(),
  });

  const held = issuances.filter((i) => !i.isReturned);

  return (
    <div className="space-y-6">
      <PageHeader
        title="My PPE"
        description="Everything issued to you — what you hold, what is due back and what has expired. Contact the SHE team for replacements or returns."
        backHref="/me/safety"
      />

      {error ? (
        <EmptyState
          icon={HardHat}
          title="Your PPE record is unavailable"
          description={(error as Error)?.message || 'Your login may not be linked to an employee record — contact your administrator.'}
        />
      ) : isLoading ? null : issuances.length === 0 ? (
        <EmptyState
          icon={HardHat}
          title="No PPE on record"
          description="Nothing has been issued to you yet."
        />
      ) : (
        <>
          {held.some((i) => i.expiryDate && new Date(i.expiryDate) < new Date()) && (
            <Card className="border-destructive/50">
              <CardContent className="p-4 text-sm text-destructive">
                Some of your PPE has passed its expiry date — arrange a replacement with the SHE
                team.
              </CardContent>
            </Card>
          )}
          <Card>
            <CardContent className="p-0">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>PPE type</TableHead>
                    <TableHead>Issued</TableHead>
                    <TableHead>Expiry</TableHead>
                    <TableHead>Due back</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Issued by</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {issuances.map((i) => {
                    const s = statusOf(i);
                    const expired = !!i.expiryDate && new Date(i.expiryDate) < new Date();
                    return (
                      <TableRow key={i.id}>
                        <TableCell>
                          <span className="font-medium">{i.ppeTypeName}</span>
                          {i.size ? ` · ${i.size}` : ''}
                          {i.serialNumber && (
                            <span className="text-muted-foreground ml-1 font-mono text-xs">
                              {i.serialNumber}
                            </span>
                          )}
                        </TableCell>
                        <TableCell className="text-sm">
                          {fmtDate(i.issueDate)} · qty {i.quantity}
                        </TableCell>
                        <TableCell className="text-sm">
                          {expired && !i.isReturned ? (
                            <Badge variant="destructive">Expired {fmtDate(i.expiryDate)}</Badge>
                          ) : (
                            fmtDate(i.expiryDate)
                          )}
                        </TableCell>
                        <TableCell className="text-sm">
                          {i.isReturned
                            ? `Returned ${fmtDate(i.actualReturnDate)}`
                            : fmtDate(i.expectedReturnDate)}
                        </TableCell>
                        <TableCell>
                          <Badge variant={s.variant}>{s.label}</Badge>
                        </TableCell>
                        <TableCell className="text-sm">{i.issuedByName}</TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );
}
