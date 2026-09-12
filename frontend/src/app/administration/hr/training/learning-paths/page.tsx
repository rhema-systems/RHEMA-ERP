'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Search, MoreHorizontal, Eye, Trash2, Route, Award } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
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
import { learningPathService } from '@/services/hr/learning-path.service';
import { LEARNING_PATH_STATUS_OPTIONS } from '@/types/hr/learning-paths';
import type { LearningPathSummary } from '@/types/hr/learning-paths';

const statusLabel = (v: string) =>
  LEARNING_PATH_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;

export default function LearningPathsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [search, setSearch] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<LearningPathSummary | null>(null);
  const [busy, setBusy] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'training', 'learning-paths'],
    queryFn: () => learningPathService.getAll(),
  });

  const rows = useMemo(() => {
    const term = search.trim().toLowerCase();
    const all = data ?? [];
    if (!term) return all;
    return all.filter(
      (p) =>
        p.name.toLowerCase().includes(term) ||
        (p.organizationUnitName ?? '').toLowerCase().includes(term) ||
        (p.positionTitle ?? '').toLowerCase().includes(term),
    );
  }, [data, search]);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Learning Paths"
        description="Ordered curricula — a sequence of programmes someone works through, each optionally gated on the one before."
        backHref="/administration/hr/training"
        actions={
          <Button onClick={() => router.push('/administration/hr/training/learning-paths/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Path
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <CardTitle>Paths</CardTitle>
              <CardDescription>
                A path with no programmes has nothing to enrol anyone onto.
              </CardDescription>
            </div>
            <div className="relative w-64">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search paths…"
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
                  <TableHead>Path</TableHead>
                  <TableHead>Applies to</TableHead>
                  <TableHead>Programmes</TableHead>
                  <TableHead>Duration</TableHead>
                  <TableHead>Certificate</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(4)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(7)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[90px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7}>
                      <EmptyState
                        icon={Route}
                        title={search ? 'No matching paths' : 'No learning paths'}
                        description={
                          search
                            ? 'Try a different search.'
                            : 'Build a curriculum — an induction, a supervisor track, a trade progression.'
                        }
                        action={
                          !search ? (
                            <Button
                              size="sm"
                              onClick={() => router.push('/administration/hr/training/learning-paths/new')}
                            >
                              <Plus className="mr-2 h-4 w-4" /> New Path
                            </Button>
                          ) : undefined
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((p) => (
                    <TableRow
                      key={p.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/administration/hr/training/learning-paths/${p.id}`)}
                    >
                      <TableCell className="font-medium">{p.name}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {[p.organizationUnitName, p.positionTitle].filter(Boolean).join(' · ') ||
                          'Anyone'}
                      </TableCell>
                      <TableCell>
                        {p.totalProgramsCount === 0 ? (
                          <span className="text-xs text-amber-600">None yet</span>
                        ) : (
                          <span>{p.totalProgramsCount}</span>
                        )}
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {p.estimatedDurationDays ? `${p.estimatedDurationDays}d` : '—'}
                      </TableCell>
                      <TableCell>
                        {p.providesCertificate ? (
                          <Badge variant="secondary" className="text-[10px]">
                            <Award className="mr-1 h-3 w-3" /> Yes
                          </Badge>
                        ) : (
                          <span className="text-muted-foreground">—</span>
                        )}
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={statusLabel(p.status)} />
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
                                router.push(`/administration/hr/training/learning-paths/${p.id}`);
                              }}
                            >
                              <Eye className="mr-2 h-4 w-4" /> View details
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              className="text-destructive focus:text-destructive"
                              onClick={(e) => {
                                e.stopPropagation();
                                setDeleteTarget(p);
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
        onOpenChange={(o) => !o && setDeleteTarget(null)}
        title="Delete learning path"
        description={
          deleteTarget
            ? `Delete "${deleteTarget.name}"? Anyone already enrolled keeps their record, but the path itself goes.`
            : ''
        }
        confirmText="Delete"
        variant="destructive"
        isLoading={busy}
        onConfirm={async () => {
          if (!deleteTarget) return false;
          setBusy(true);
          try {
            await learningPathService.remove(deleteTarget.id);
            await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'learning-paths'] });
            toast({ title: 'Deleted' });
            setDeleteTarget(null);
            return true;
          } catch (error: any) {
            toast({
              title: 'Error',
              description: error?.message || 'Failed to delete.',
              variant: 'destructive',
            });
            return false;
          } finally {
            setBusy(false);
          }
        }}
      />
    </div>
  );
}
