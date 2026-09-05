'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { MoreHorizontal, Pencil, Plus, Trash2, Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { unionService } from '@/services/hr/union.service';
import type { Union } from '@/types/hr/union';

/**
 * The trade-union register.
 *
 * Two counts per row, deliberately: how many agreements are on file, and how many are actually in
 * force. A union with three agreements whose last one lapsed in 2021 has nothing in force, and a
 * register that reports only the first number is reporting filing-cabinet depth.
 */
export default function UnionsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [deleteTarget, setDeleteTarget] = useState<Union | null>(null);
  const [deleting, setDeleting] = useState(false);

  const { data: unions, isLoading } = useQuery({
    queryKey: ['hr', 'unions'],
    queryFn: () => unionService.getAll(),
  });

  const rows = unions ?? [];

  const handleDelete = async () => {
    if (!deleteTarget) return false;
    setDeleting(true);
    try {
      await unionService.remove(deleteTarget.id);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'unions'] });
      toast({ title: 'Deleted', description: `“${deleteTarget.name}” was removed.` });
      setDeleteTarget(null);
      return true;
    } catch (error) {
      // The server refuses a union that still has agreements and names how many. That sentence is
      // more useful than anything this screen could invent, so it is what the user sees.
      toast({
        title: 'Could not delete this union',
        description: (error as Error)?.message || 'Failed to delete the union.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setDeleting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Unions"
        description="Trade unions that represent a bargaining unit, and the agreements negotiated with each."
        actions={
          <Button onClick={() => router.push('/administration/hr/unions/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Union
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>Register</CardTitle>
          <CardDescription>
            A job description points at a union to say which agreement its role falls under; the
            offer letter prints that union&apos;s name in its bargaining-unit clause.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Union</TableHead>
                  <TableHead>Contact</TableHead>
                  <TableHead className="w-[140px]">Agreements</TableHead>
                  <TableHead className="w-[110px]">Status</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(4)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(5)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-full" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5}>
                      <EmptyState
                        icon={Users}
                        title="No unions recorded yet"
                        description="Add the unions that represent your staff, then record the collective agreements negotiated with each."
                        action={
                          <Button size="sm" onClick={() => router.push('/administration/hr/unions/new')}>
                            <Plus className="mr-2 h-4 w-4" /> New Union
                          </Button>
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((union) => (
                    <TableRow
                      key={union.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/administration/hr/unions/${union.id}`)}
                    >
                      <TableCell>
                        <div className="flex flex-col">
                          <span className="font-medium">{union.name}</span>
                          {union.code && (
                            <span className="text-xs text-muted-foreground">{union.code}</span>
                          )}
                        </div>
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {union.contactPerson || union.contactEmail || union.contactPhone ? (
                          <div className="flex flex-col">
                            {union.contactPerson && <span>{union.contactPerson}</span>}
                            {union.contactEmail && (
                              <span className="text-xs">{union.contactEmail}</span>
                            )}
                            {!union.contactEmail && union.contactPhone && (
                              <span className="text-xs">{union.contactPhone}</span>
                            )}
                          </div>
                        ) : (
                          '—'
                        )}
                      </TableCell>
                      <TableCell>
                        {union.agreementCount === 0 ? (
                          <span className="text-sm text-muted-foreground">None on file</span>
                        ) : (
                          <div className="flex flex-col gap-1">
                            <Badge
                              className={
                                union.inForceAgreementCount > 0
                                  ? 'w-fit bg-emerald-100 text-emerald-800 hover:bg-emerald-100'
                                  : 'w-fit bg-amber-100 text-amber-800 hover:bg-amber-100'
                              }
                            >
                              {union.inForceAgreementCount} in force
                            </Badge>
                            <span className="text-xs text-muted-foreground">
                              {union.agreementCount} on file
                            </span>
                          </div>
                        )}
                      </TableCell>
                      <TableCell>
                        <StatusBadge active={union.isActive} />
                      </TableCell>
                      <TableCell>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button
                              variant="ghost"
                              className="h-8 w-8 p-0"
                              onClick={(e) => e.stopPropagation()}
                            >
                              <span className="sr-only">Open menu</span>
                              <MoreHorizontal className="h-4 w-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuLabel>Actions</DropdownMenuLabel>
                            <DropdownMenuItem
                              onClick={(e) => {
                                e.stopPropagation();
                                router.push(`/administration/hr/unions/${union.id}`);
                              }}
                            >
                              <Pencil className="mr-2 h-4 w-4" /> Open
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              className="text-destructive focus:text-destructive"
                              onClick={(e) => {
                                e.stopPropagation();
                                setDeleteTarget(union);
                              }}
                            >
                              <Trash2 className="mr-2 h-4 w-4" /> Delete
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={deleteTarget !== null}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title="Delete union"
        description={
          deleteTarget
            ? deleteTarget.agreementCount > 0
              ? `“${deleteTarget.name}” has ${deleteTarget.agreementCount} agreement(s) on file. Remove those first — a union cannot be deleted while its agreements would be left behind.`
              : `Are you sure you want to delete “${deleteTarget.name}”?`
            : ''
        }
        confirmText="Delete"
        variant="destructive"
        isLoading={deleting}
        onConfirm={handleDelete}
      />
    </div>
  );
}
