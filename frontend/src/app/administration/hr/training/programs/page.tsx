'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Search, MoreHorizontal, Trash2, BookOpen, Eye } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
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
import { trainingProgramService } from '@/services/hr/training-program.service';
import { TRAINING_TYPE_OPTIONS } from '@/types/hr/training';
import type { TrainingProgramSummary } from '@/types/hr/training';

const typeLabel = (value: string) => TRAINING_TYPE_OPTIONS.find((o) => o.value === value)?.label ?? value;

export default function TrainingProgramsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [search, setSearch] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<TrainingProgramSummary | null>(null);
  const [deleting, setDeleting] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'training', 'programs'],
    queryFn: () => trainingProgramService.getAll(),
  });

  const programs = useMemo(() => {
    const term = search.trim().toLowerCase();
    const all = data ?? [];
    if (!term) return all;
    return all.filter(
      (p) =>
        p.programName.toLowerCase().includes(term) || p.programCode.toLowerCase().includes(term),
    );
  }, [data, search]);

  const handleDelete = async () => {
    if (!deleteTarget) return false;
    setDeleting(true);
    try {
      await trainingProgramService.remove(deleteTarget.id);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'programs'] });
      toast({ title: 'Deleted', description: `"${deleteTarget.programName}" was removed.` });
      setDeleteTarget(null);
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to delete program.',
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
        title="Training Programs"
        description="The catalog of training programs that can be scheduled and delivered."
        backHref="/administration/hr/training"
        actions={
          <Button onClick={() => router.push('/administration/hr/training/programs/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Program
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Programs</CardTitle>
            <div className="relative w-64">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search programs…"
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
                  <TableHead>Type</TableHead>
                  <TableHead>Duration</TableHead>
                  <TableHead>Cost</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {Array.from({ length: 8 }).map((__, j) => (
                        <TableCell key={j}><Skeleton className="h-4 w-[80px]" /></TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : programs.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={8}>
                      <EmptyState
                        icon={BookOpen}
                        title={search ? 'No matching programs' : 'No programs yet'}
                        description={search ? 'Try a different search.' : 'Create your first training program.'}
                        action={
                          !search ? (
                            <Button size="sm" onClick={() => router.push('/administration/hr/training/programs/new')}>
                              <Plus className="mr-2 h-4 w-4" /> New Program
                            </Button>
                          ) : undefined
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  programs.map((program) => (
                    <TableRow
                      key={program.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/administration/hr/training/programs/${program.id}`)}
                    >
                      <TableCell className="font-mono text-xs">{program.programCode}</TableCell>
                      <TableCell className="font-medium">
                        <span className="inline-flex items-center gap-2">
                          {program.categoryColor && (
                            <span
                              className="h-2.5 w-2.5 rounded-full border"
                              style={{ backgroundColor: program.categoryColor }}
                            />
                          )}
                          {program.programName}
                          {program.providesCertificate && (
                            <Badge variant="secondary" className="text-[10px]">Certificate</Badge>
                          )}
                        </span>
                      </TableCell>
                      <TableCell className="text-muted-foreground">{program.categoryName || '—'}</TableCell>
                      <TableCell className="text-muted-foreground">{typeLabel(program.type)}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {program.durationDays}d / {program.durationHours}h
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {program.currency} {program.costPerParticipant.toFixed(2)}
                      </TableCell>
                      <TableCell>
                        <StatusBadge active={program.isActive} />
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
                                router.push(`/administration/hr/training/programs/${program.id}`);
                              }}
                            >
                              <Eye className="mr-2 h-4 w-4" /> View details
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              className="text-destructive focus:text-destructive"
                              onClick={(e) => {
                                e.stopPropagation();
                                setDeleteTarget(program);
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
        title="Delete program"
        description={
          deleteTarget
            ? `Are you sure you want to delete "${deleteTarget.programName}"? This cannot be undone.`
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
