'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Search, MoreHorizontal, Trash2, ClipboardPen, Users, Eye } from 'lucide-react';
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
import { trainingNeedsAssessmentService } from '@/services/hr/training-needs-assessment.service';
import { TRAINING_PRIORITY_OPTIONS, ASSESSMENT_SOURCE_OPTIONS } from '@/types/hr/training';
import type { TrainingNeedsAssessmentSummary } from '@/types/hr/training';

const priorityLabel = (value: string) =>
  TRAINING_PRIORITY_OPTIONS.find((o) => o.value === value)?.label ?? value;

// The API sends the enum member name ("SkillsGapAnalysis"); without this the column read as one
// unspaced word instead of the label the rest of the module uses.
const sourceLabel = (value: string) =>
  ASSESSMENT_SOURCE_OPTIONS.find((o) => o.value === value)?.label ?? value;

export default function NeedsAssessmentsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [search, setSearch] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<TrainingNeedsAssessmentSummary | null>(null);
  const [deleting, setDeleting] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'training', 'needs-assessments'],
    queryFn: () => trainingNeedsAssessmentService.getAll(),
  });

  const rows = useMemo(() => {
    const term = search.trim().toLowerCase();
    const all = data ?? [];
    if (!term) return all;
    return all.filter(
      (a) =>
        a.employeeName.toLowerCase().includes(term) || a.employeeNumber.toLowerCase().includes(term),
    );
  }, [data, search]);

  const handleDelete = async () => {
    if (!deleteTarget) return false;
    setDeleting(true);
    try {
      await trainingNeedsAssessmentService.remove(deleteTarget.id);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'needs-assessments'] });
      toast({ title: 'Deleted', description: `Assessment for "${deleteTarget.employeeName}" was removed.` });
      setDeleteTarget(null);
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to delete assessment.',
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
        title="Training Needs Assessments"
        description="Gaps identified for individual employees, with recommended programs and skill targets."
        backHref="/hr/training"
        actions={
          <div className="flex gap-2">
            <Button
              variant="outline"
              onClick={() => router.push('/hr/training/needs-assessments/bulk')}
            >
              <Users className="mr-2 h-4 w-4" /> Bulk Create
            </Button>
            <Button onClick={() => router.push('/hr/training/needs-assessments/new')}>
              <Plus className="mr-2 h-4 w-4" /> New Assessment
            </Button>
          </div>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Assessments</CardTitle>
            <div className="relative w-64">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search by employee…"
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
                  <TableHead>Employee</TableHead>
                  <TableHead>Year</TableHead>
                  <TableHead>Source</TableHead>
                  <TableHead>Priority</TableHead>
                  <TableHead>Programs</TableHead>
                  <TableHead>Skill gaps</TableHead>
                  <TableHead>Provided</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(8)].map((__, j) => (
                        <TableCell key={j}><Skeleton className="h-4 w-[100px]" /></TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={8}>
                      <EmptyState
                        icon={ClipboardPen}
                        title={search ? 'No matching assessments' : 'No assessments yet'}
                        description={search ? 'Try a different search.' : 'Identify a training need for an employee.'}
                        action={
                          !search ? (
                            <Button
                              size="sm"
                              onClick={() => router.push('/hr/training/needs-assessments/new')}
                            >
                              <Plus className="mr-2 h-4 w-4" /> New Assessment
                            </Button>
                          ) : undefined
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((a) => (
                    <TableRow
                      key={a.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/hr/training/needs-assessments/${a.id}`)}
                    >
                      <TableCell className="font-medium">
                        {a.employeeName}
                        <div className="text-xs text-muted-foreground">{a.employeeNumber}</div>
                      </TableCell>
                      <TableCell>{a.year}</TableCell>
                      <TableCell className="text-muted-foreground">{sourceLabel(a.source)}</TableCell>
                      <TableCell><StatusBadge status={priorityLabel(a.priority)} /></TableCell>
                      <TableCell>{a.recommendedProgramsCount}</TableCell>
                      <TableCell>{a.skillGapsCount}</TableCell>
                      <TableCell>{a.trainingProvided ? 'Yes' : 'No'}</TableCell>
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
                                router.push(`/hr/training/needs-assessments/${a.id}`);
                              }}
                            >
                              <Eye className="mr-2 h-4 w-4" /> View details
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              className="text-destructive focus:text-destructive"
                              onClick={(e) => {
                                e.stopPropagation();
                                setDeleteTarget(a);
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
        title="Delete assessment"
        description={
          deleteTarget
            ? `Are you sure you want to delete the assessment for "${deleteTarget.employeeName}"? This cannot be undone.`
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
