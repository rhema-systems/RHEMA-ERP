'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { LayoutList, Loader2, Pencil, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { interviewQuestionPresetService as presets } from '@/services/hr/interviews.service';
import type { InterviewQuestionPreset } from '@/types/hr/interviews';
import { QuestionPresetDialog } from '@/components/hr/recruitment/QuestionPresetDialog';

/**
 * Interview templates: which question types a panel covers, and how many of each.
 *
 * Applying one when scheduling scaffolds the interview's question plans and draws the questions
 * from the bank in one step — which is the only reason a preset exists.
 */
export default function InterviewQuestionPresetsPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<InterviewQuestionPreset | null>(null);
  const [loadingId, setLoadingId] = useState<string | null>(null);

  const list = useQuery({
    queryKey: ['hr', 'interview-presets'],
    queryFn: () => presets.getAll(),
  });

  const rows = list.data ?? [];

  const openForEdit = async (id: string) => {
    setLoadingId(id);
    try {
      // The list is a summary — the items only come back on the detail read, and the editor needs
      // the whole set because saving replaces it.
      setEditing(await presets.getById(id));
      setDialogOpen(true);
    } catch (error: any) {
      toast({
        title: 'Could not open the preset',
        description: error?.message ?? 'Please try again.',
        variant: 'destructive',
      });
    } finally {
      setLoadingId(null);
    }
  };

  const remove = useMutation({
    mutationFn: (id: string) => presets.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'interview-presets'] });
      toast({ title: 'Preset deleted' });
    },
    onError: (error: any) =>
      toast({
        title: 'Could not delete the preset',
        description: error?.message ?? 'Please try again.',
        variant: 'destructive',
      }),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Interview presets"
        description="Reusable interview shapes — which question types the panel covers, and how many of each."
        backHref="/administration/hr/recruitment"
        actions={
          <Button
            onClick={() => {
              setEditing(null);
              setDialogOpen(true);
            }}
          >
            <Plus className="mr-1.5 h-4 w-4" />
            New preset
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
            <div className="py-10">
              <EmptyState
                icon={LayoutList}
                title="No presets"
                description="A preset saves rebuilding the same question plan for every interview on a role."
              />
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Preset</TableHead>
                  <TableHead className="w-[140px]">Sections</TableHead>
                  <TableHead className="w-[110px]">Status</TableHead>
                  <TableHead className="w-[90px]" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((preset) => (
                  <TableRow key={preset.id}>
                    <TableCell>
                      <div className="font-medium">{preset.name}</div>
                      {preset.description && (
                        <div className="text-sm text-muted-foreground">{preset.description}</div>
                      )}
                    </TableCell>
                    <TableCell>{preset.itemCount}</TableCell>
                    <TableCell>
                      <StatusBadge active={preset.isActive} />
                    </TableCell>
                    <TableCell>
                      <div className="flex justify-end gap-1">
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label={`Edit ${preset.name}`}
                          disabled={loadingId === preset.id}
                          onClick={() => openForEdit(preset.id)}
                        >
                          {loadingId === preset.id ? (
                            <Loader2 className="h-4 w-4 animate-spin" />
                          ) : (
                            <Pencil className="h-4 w-4" />
                          )}
                        </Button>
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label={`Delete ${preset.name}`}
                          onClick={() => remove.mutate(preset.id)}
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

      <QuestionPresetDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        preset={editing}
        onSaved={() => queryClient.invalidateQueries({ queryKey: ['hr', 'interview-presets'] })}
      />
    </div>
  );
}
