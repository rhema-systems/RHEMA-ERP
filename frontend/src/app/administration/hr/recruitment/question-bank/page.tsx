'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { HelpCircle, Loader2, Pencil, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import {
  interviewQuestionBankService as bank,
} from '@/services/hr/interviews.service';
import type { InterviewQuestionTypeSummary } from '@/types/hr/interviews';
import { QuestionTypeDialog } from '@/components/hr/recruitment/QuestionTypeDialog';
import { QuestionListPanel } from '@/components/hr/recruitment/QuestionListPanel';

/**
 * The interview question bank: question types, and the questions inside each.
 *
 * Two panes rather than two pages — a question only means anything in the context of its type, and
 * the weight/band you give it is chosen relative to its siblings.
 *
 * ⚠ Inactive rows are listed here on purpose. This is the only screen that can reactivate one, so
 * filtering them out would strand them; the interview-scheduling pickers filter for themselves.
 */
export default function InterviewQuestionBankPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [selectedTypeId, setSelectedTypeId] = useState<string | null>(null);
  const [editingType, setEditingType] = useState<InterviewQuestionTypeSummary | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);

  const types = useQuery({
    queryKey: ['hr', 'interview-question-types'],
    queryFn: () => bank.getTypes(),
  });

  const rows = types.data ?? [];
  const selectedType = rows.find((t) => t.id === selectedTypeId) ?? null;

  const removeType = useMutation({
    mutationFn: (id: string) => bank.deleteType(id),
    onSuccess: (_data, id) => {
      if (selectedTypeId === id) setSelectedTypeId(null);
      queryClient.invalidateQueries({ queryKey: ['hr', 'interview-question-types'] });
      toast({ title: 'Question type deleted' });
    },
    onError: (error: any) =>
      toast({
        title: 'Could not delete the question type',
        description: error?.message ?? 'A type that is in use by a preset or an interview cannot be removed.',
        variant: 'destructive',
      }),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Interview question bank"
        description="The questions candidates are asked, grouped by type, with the weight and score band each is marked against."
        backHref="/administration/hr/recruitment"
      />

      <div className="grid gap-6 lg:grid-cols-[minmax(0,380px)_minmax(0,1fr)]">
        <Card>
          <CardHeader className="flex flex-row items-start justify-between gap-4 space-y-0">
            <div>
              <CardTitle className="text-base">Question types</CardTitle>
              <CardDescription>
                A section of the interview — &ldquo;Competency&rdquo;, &ldquo;Technical&rdquo;. Presets and
                question plans are built from these.
              </CardDescription>
            </div>
            <Button
              size="sm"
              onClick={() => {
                setEditingType(null);
                setDialogOpen(true);
              }}
            >
              <Plus className="mr-1.5 h-4 w-4" />
              New
            </Button>
          </CardHeader>
          <CardContent className="p-0">
            {types.isLoading ? (
              <div className="flex items-center justify-center py-16">
                <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
              </div>
            ) : rows.length === 0 ? (
              <div className="py-10">
                <EmptyState
                  icon={HelpCircle}
                  title="No question types"
                  description="Add a type before writing questions — every question belongs to one."
                />
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Type</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead className="w-[90px]" />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rows.map((type) => (
                    <TableRow
                      key={type.id}
                      className={`cursor-pointer ${selectedTypeId === type.id ? 'bg-muted/60' : ''}`}
                      onClick={() => setSelectedTypeId(type.id)}
                    >
                      <TableCell>
                        <div className="font-medium">{type.typeName}</div>
                        {type.code && (
                          <div className="text-xs text-muted-foreground">{type.code}</div>
                        )}
                      </TableCell>
                      <TableCell>
                        <StatusBadge active={type.isActive} />
                      </TableCell>
                      <TableCell>
                        <div className="flex justify-end gap-1">
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label={`Edit ${type.typeName}`}
                            onClick={(e) => {
                              e.stopPropagation();
                              setEditingType(type);
                              setDialogOpen(true);
                            }}
                          >
                            <Pencil className="h-4 w-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label={`Delete ${type.typeName}`}
                            onClick={(e) => {
                              e.stopPropagation();
                              removeType.mutate(type.id);
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

        {selectedType ? (
          <QuestionListPanel type={selectedType} />
        ) : (
          <Card>
            <CardContent className="py-16">
              <EmptyState
                icon={HelpCircle}
                title="Select a question type"
                description="Its questions, weights and score bands appear here."
              />
            </CardContent>
          </Card>
        )}
      </div>

      <QuestionTypeDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        questionType={editingType}
        onSaved={(saved) => {
          queryClient.invalidateQueries({ queryKey: ['hr', 'interview-question-types'] });
          setSelectedTypeId(saved.id);
        }}
      />
    </div>
  );
}
