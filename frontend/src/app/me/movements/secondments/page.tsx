'use client';

/**
 * Area 25 slice 7 — my secondments: spec destination #22, built fresh (census verdict E).
 * Secondments are movements of type Secondment; the read is token-derived and rows open
 * the portal movement detail.
 */

import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeftRight } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { mePortalService } from '@/services/hr/me-portal.service';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function MySecondmentsPage() {
  const router = useRouter();
  const { data: rows, isLoading } = useQuery({
    queryKey: ['me', 'movements', 'secondments'],
    queryFn: () => mePortalService.getSecondments(),
  });

  const list = rows ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Secondments"
        description="Temporary placements in another unit or organisation, and how each one stands."
        backHref="/me/movements"
      />

      <Card>
        <CardHeader>
          <CardTitle>Secondments</CardTitle>
          <CardDescription>Open one for its full detail and your response record.</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>To</TableHead>
                  <TableHead>Effective</TableHead>
                  <TableHead>Until</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(3)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(5)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[80px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : list.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5}>
                      <EmptyState
                        icon={ArrowLeftRight}
                        title="No secondments"
                        description="You have not been seconded to another unit or organisation."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  list.map((m) => (
                    <TableRow
                      key={m.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/me/movements/${m.id}`)}
                    >
                      <TableCell className="font-mono text-xs">{m.movementNumber}</TableCell>
                      <TableCell className="font-medium">
                        {m.newPositionTitle}
                        <div className="text-xs text-muted-foreground">
                          {m.newOrganizationUnitName}
                        </div>
                      </TableCell>
                      <TableCell className="text-muted-foreground">{fmtDate(m.effectiveDate)}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {m.temporaryEndDate ? (
                          fmtDate(m.temporaryEndDate)
                        ) : (
                          <span>—</span>
                        )}
                        {m.returnProcessed && (
                          <Badge variant="outline" className="ml-2 text-[10px]">
                            Returned
                          </Badge>
                        )}
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={m.status} />
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
