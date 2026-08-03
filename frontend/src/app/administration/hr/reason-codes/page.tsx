'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Search, MoreHorizontal, Pencil, Ban, Tags } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Skeleton } from '@/components/ui/skeleton';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { reasonCodeService } from '@/services/hr/lookup.service';
import { REASON_CODE_CATEGORY_OPTIONS, type ReasonCode } from '@/types/hr/lookups';

const categoryLabel = (value: string) =>
  REASON_CODE_CATEGORY_OPTIONS.find((o) => o.value === value)?.label ?? value;

export default function ReasonCodesPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [search, setSearch] = useState('');
  const [deactivateTarget, setDeactivateTarget] = useState<ReasonCode | null>(null);
  const [deactivating, setDeactivating] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'reason-codes'],
    queryFn: () => reasonCodeService.getAll(),
  });

  const codes = useMemo(() => {
    const term = search.trim().toLowerCase();
    const all = data ?? [];
    if (!term) return all;
    return all.filter(
      (c) => c.name.toLowerCase().includes(term) || c.code.toLowerCase().includes(term),
    );
  }, [data, search]);

  // The API has no delete for reason codes — they are deactivated so historical
  // references keep resolving.
  const handleDeactivate = async () => {
    if (!deactivateTarget) return false;
    setDeactivating(true);
    try {
      await reasonCodeService.deactivate(deactivateTarget.id);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'reason-codes'] });
      toast({ title: 'Deactivated', description: `"${deactivateTarget.name}" was deactivated.` });
      setDeactivateTarget(null);
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to deactivate reason code.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setDeactivating(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Reason Codes"
        description="Standard reasons offered on HR actions such as leave adjustments."
        actions={
          <Button onClick={() => router.push('/administration/hr/reason-codes/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Reason Code
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Reason Codes</CardTitle>
            <div className="relative w-64">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search reason codes…"
                className="pl-8"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Code</TableHead>
                  <TableHead>Name</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead>Description</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      <TableCell><Skeleton className="h-4 w-[70px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[170px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[120px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[200px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[70px]" /></TableCell>
                      <TableCell><Skeleton className="h-8 w-8" /></TableCell>
                    </TableRow>
                  ))
                ) : codes.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <EmptyState
                        icon={Tags}
                        title={search ? 'No matching reason codes' : 'No reason codes yet'}
                        description={
                          search ? 'Try a different search.' : 'Create your first reason code.'
                        }
                        action={
                          !search ? (
                            <Button
                              size="sm"
                              onClick={() => router.push('/administration/hr/reason-codes/new')}
                            >
                              <Plus className="mr-2 h-4 w-4" /> New Reason Code
                            </Button>
                          ) : undefined
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  codes.map((c) => (
                    <TableRow
                      key={c.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/administration/hr/reason-codes/${c.id}/edit`)}
                    >
                      <TableCell className="font-medium">{c.code}</TableCell>
                      <TableCell>{c.name}</TableCell>
                      <TableCell>{categoryLabel(c.category)}</TableCell>
                      <TableCell className="max-w-[280px] truncate text-muted-foreground">
                        {c.description || '—'}
                      </TableCell>
                      <TableCell>
                        <StatusBadge active={c.isActive} />
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
                                router.push(`/administration/hr/reason-codes/${c.id}/edit`);
                              }}
                            >
                              <Pencil className="mr-2 h-4 w-4" /> Edit
                            </DropdownMenuItem>
                            {c.isActive && (
                              <>
                                <DropdownMenuSeparator />
                                <DropdownMenuItem
                                  className="text-destructive focus:text-destructive"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    setDeactivateTarget(c);
                                  }}
                                >
                                  <Ban className="mr-2 h-4 w-4" /> Deactivate
                                </DropdownMenuItem>
                              </>
                            )}
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
        open={deactivateTarget !== null}
        onOpenChange={(open) => !open && setDeactivateTarget(null)}
        title="Deactivate reason code"
        description={
          deactivateTarget
            ? `"${deactivateTarget.name}" will no longer be offered on new records. Existing records keep it.`
            : ''
        }
        confirmText="Deactivate"
        variant="destructive"
        isLoading={deactivating}
        onConfirm={handleDeactivate}
      />
    </div>
  );
}
