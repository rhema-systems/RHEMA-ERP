'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ClipboardList, Clock, Loader2, Lock, Pencil, Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { recruitmentTestService as tests } from '@/services/hr/recruitment-test.service';
import type { RecruitmentTest } from '@/types/hr/recruitment-tests';
import { RecruitmentTestDialog } from '@/components/hr/recruitment/RecruitmentTestDialog';

/**
 * The test papers a candidate can be asked to sit (round 4, lane E).
 *
 * ⚠ Two states are shown on every row and neither is decoration. **Active** is what makes a paper
 * assignable, and activating it is where the server checks that every question can actually be
 * marked. **In use** means somebody has sat it, which freezes it: editing a question after it has
 * been answered would rewrite what that person was marked on.
 */
export default function RecruitmentTestsPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<RecruitmentTest | null>(null);

  const list = useQuery({
    queryKey: ['hr', 'recruitment-tests'],
    queryFn: () => tests.getTests(),
  });

  const rows = list.data ?? [];

  const remove = useMutation({
    mutationFn: (id: string) => tests.deleteTest(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'recruitment-tests'] });
      toast({ title: 'Test deleted' });
    },
    onError: (error: any) =>
      toast({
        title: 'Could not delete the test',
        description: error?.message ?? 'A paper somebody has sat cannot be deleted.',
        variant: 'destructive',
      }),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Recruitment tests"
        description="Aptitude and knowledge papers, sat online by candidates and marked by the system. A test's score feeds the vacancy's shortlisting blend once it is finalised."
        backHref="/administration/hr/recruitment"
        actions={
          <Button
            onClick={() => {
              setEditing(null);
              setDialogOpen(true);
            }}
          >
            <Plus className="mr-2 h-4 w-4" />
            New test
          </Button>
        }
      />

      <Card>
        <CardContent className="p-0">
          {list.isLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={ClipboardList}
              title="No test papers yet"
              description="Create a paper, add its questions, then activate it. Only an active paper can be assigned to a vacancy."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Code</TableHead>
                  <TableHead>Name</TableHead>
                  <TableHead className="text-right">Questions</TableHead>
                  <TableHead className="text-right">Marks</TableHead>
                  <TableHead>Duration</TableHead>
                  <TableHead>Pass mark</TableHead>
                  <TableHead>State</TableHead>
                  <TableHead className="w-[120px]" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((test) => (
                  <TableRow key={test.id}>
                    <TableCell className="font-mono text-xs">{test.testCode}</TableCell>
                    <TableCell>
                      <Link
                        href={`/administration/hr/recruitment/tests/${test.id}`}
                        className="font-medium hover:underline"
                      >
                        {test.name}
                      </Link>
                      {test.description && (
                        <p className="text-xs text-muted-foreground line-clamp-1">{test.description}</p>
                      )}
                    </TableCell>
                    <TableCell className="text-right">{test.questionCount}</TableCell>
                    <TableCell className="text-right">{test.totalPoints}</TableCell>
                    <TableCell>
                      {test.durationMinutes ? (
                        <span className="inline-flex items-center gap-1 text-sm">
                          <Clock className="h-3 w-3" />
                          {test.durationMinutes} min
                        </span>
                      ) : (
                        <span className="text-sm text-muted-foreground">Untimed</span>
                      )}
                    </TableCell>
                    <TableCell className="text-sm">
                      {test.passMarkPercent != null ? `${test.passMarkPercent}%` : '—'}
                    </TableCell>
                    <TableCell>
                      <div className="flex flex-wrap items-center gap-1">
                        <Badge variant={test.isActive ? 'default' : 'secondary'}>
                          {test.isActive ? 'Active' : 'Draft'}
                        </Badge>
                        {/* ⚠ Not cosmetic: this is the row that can no longer be edited. */}
                        {test.hasSittings && (
                          <Badge variant="outline" className="gap-1">
                            <Lock className="h-3 w-3" />
                            In use
                          </Badge>
                        )}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div className="flex items-center justify-end gap-1">
                        <Button
                          variant="ghost"
                          size="icon"
                          disabled={test.hasSittings}
                          title={
                            test.hasSittings
                              ? 'Candidates have sat this paper, so it can no longer be changed'
                              : 'Edit'
                          }
                          onClick={() => {
                            setEditing(test);
                            setDialogOpen(true);
                          }}
                        >
                          <Pencil className="h-4 w-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="icon"
                          disabled={test.hasSittings || remove.isPending}
                          title={
                            test.hasSittings
                              ? 'Candidates have sat this paper, so it cannot be deleted'
                              : 'Delete'
                          }
                          onClick={() => {
                            if (
                              window.confirm(
                                `Delete "${test.name}"? Any assignments of it are withdrawn too.`,
                              )
                            ) {
                              remove.mutate(test.id);
                            }
                          }}
                        >
                          <Trash2 className="h-4 w-4 text-destructive" />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <RecruitmentTestDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        test={editing}
        onSaved={() => queryClient.invalidateQueries({ queryKey: ['hr', 'recruitment-tests'] })}
      />
    </div>
  );
}
