'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Info, Loader2, Pencil, Plus, Scale, Target, Trash2, TriangleAlert } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useToast } from '@/components/ui/use-toast';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { GradeRangeDialog } from '@/components/hr/performance/GradeRangeDialog';
import { appraisalCriteriaService, appraisalTemplateService } from '@/services/hr/appraisal.service';
import { kpiDefinitionService } from '@/services/hr/goals.service';
import type {
  AppraisalSectionKind,
  AppraisalTemplateItem,
  AppraisalTemplateSection,
} from '@/types/hr/appraisal';

/**
 * The body of an appraisal template: weighted sections, each holding weighted items.
 *
 * Two kinds of section (performance closure lane L, D-15). A **fixed** section holds this
 * template's own items — competencies, shared KPIs (one KPI and one target for everyone on the
 * template) and free-text questions. A **goals** section holds none: on each appraisal it is filled
 * with the employee's own locked goals, one row each. A template has one goals section at most.
 *
 * Three rules decide whether the template can ever be used, and all three are shown live
 * rather than discovered when activation is refused:
 *   1. section weights total 100,
 *   2. item weights total 100 *within each fixed section*,
 *   3. every item has at least one grade band — except a free-text question with no weight, which
 *      is never scored (P-8).
 *
 * A fourth rule is enforced server-side and cannot be pre-empted here: the same criterion or
 * KPI may not appear twice anywhere in the template, in any section. That comes back as a 409
 * with a readable message.
 *
 * ⚠ When the template is assigned to an Open or InProgress cycle every write below is refused —
 * the form an appraisal was scored on must not change underneath it. Clone it instead. The
 * parent passes `readOnly` in that case so the affordances disappear rather than failing.
 */

interface SectionDraft {
  sectionName: string;
  description: string;
  weight: number;
  displayOrder: number;
  kind: AppraisalSectionKind;
}

interface ItemDraft {
  kind: 'criterion' | 'kpi' | 'question';
  competencyId: string;
  kpiDefinitionId: string;
  customQuestion: string;
  kpiTargetValue: string;
  kpiMinValue: string;
  kpiMaxValue: string;
  weight: number;
  displayOrder: number;
}

const emptySection: SectionDraft = {
  sectionName: '',
  description: '',
  weight: 0,
  displayOrder: 0,
  kind: 'Fixed',
};

/** A free-text question with no weight is never scored, so it needs no grade bands (P-8). */
const needsBands = (item: AppraisalTemplateItem) =>
  !(item.weight === 0 && !item.competencyId && !item.kpiDefinitionId);

const emptyItem: ItemDraft = {
  kind: 'criterion',
  competencyId: '',
  kpiDefinitionId: '',
  customQuestion: '',
  kpiTargetValue: '',
  kpiMinValue: '',
  kpiMaxValue: '',
  weight: 0,
  displayOrder: 0,
};

/** What an item is called on screen — whichever of its three shapes it took. */
export function itemLabel(item: AppraisalTemplateItem): string {
  return item.competencyName || item.kpiName || item.customQuestion || 'Untitled item';
}

export function TemplateStructureEditor({
  templateId,
  readOnly = false,
}: {
  templateId: string;
  readOnly?: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [sectionDialog, setSectionDialog] = useState<{
    open: boolean;
    editing: AppraisalTemplateSection | null;
    draft: SectionDraft;
  }>({ open: false, editing: null, draft: emptySection });

  const [itemDialog, setItemDialog] = useState<{
    open: boolean;
    sectionId: string;
    editing: AppraisalTemplateItem | null;
    draft: ItemDraft;
  }>({ open: false, sectionId: '', editing: null, draft: emptyItem });

  const [bands, setBands] = useState<{ itemId: string | null; label: string }>({
    itemId: null,
    label: '',
  });

  const [pendingDelete, setPendingDelete] = useState<
    | { kind: 'section'; id: string; name: string }
    | { kind: 'item'; sectionId: string; id: string; name: string }
    | null
  >(null);

  const sectionsQuery = useQuery({
    queryKey: ['hr', 'appraisal-template-sections', templateId],
    queryFn: () => appraisalTemplateService.getSections(templateId),
    enabled: !!templateId,
  });

  const sections = useMemo(
    () => [...(sectionsQuery.data ?? [])].sort((a, b) => a.displayOrder - b.displayOrder),
    [sectionsQuery.data],
  );

  // One items query per section. The sections endpoint does include its items, but the item
  // rows there do not carry `gradeRangeCount`, which is exactly what rule 3 needs.
  const itemQueries = useQueries({
    queries: sections.map((s) => ({
      queryKey: ['hr', 'appraisal-template-items', s.id],
      queryFn: () => appraisalTemplateService.getItems(s.id),
    })),
  });

  const itemsBySection = useMemo(() => {
    const map = new Map<string, AppraisalTemplateItem[]>();
    sections.forEach((s, index) => {
      const rows = (itemQueries[index]?.data ?? []) as AppraisalTemplateItem[];
      map.set(s.id, [...rows].sort((a, b) => a.displayOrder - b.displayOrder));
    });
    return map;
  }, [sections, itemQueries]);

  const { data: criteria } = useQuery({
    queryKey: ['hr', 'appraisal-criteria'],
    queryFn: () => appraisalCriteriaService.getAll(),
    staleTime: 5 * 60 * 1000,
  });
  const { data: kpis } = useQuery({
    queryKey: ['hr', 'kpi-definitions'],
    queryFn: () => kpiDefinitionService.getAll(),
    staleTime: 5 * 60 * 1000,
  });

  const sectionWeightTotal = sections.reduce((sum, s) => sum + s.weight, 0);
  const itemsLoading = itemQueries.some((q) => q.isLoading);
  const goalsSection = sections.find((s) => s.kind === 'EmployeeGoals');

  const problems: string[] = [];
  if (sections.length === 0) {
    problems.push('Add at least one section.');
  } else {
    if (sectionWeightTotal !== 100)
      problems.push(`Section weights total ${sectionWeightTotal}, not 100.`);
    if (!itemsLoading) {
      for (const section of sections) {
        const rows = itemsBySection.get(section.id) ?? [];
        if (rows.length === 0) continue;
        if (section.kind === 'EmployeeGoals') {
          problems.push(
            `“${section.sectionName}” is a goals section but holds ${rows.length} item(s); remove them — its rows are each employee's goals.`,
          );
          continue;
        }
        const total = rows.reduce((sum, i) => sum + i.weight, 0);
        if (total !== 100)
          problems.push(`Item weights in “${section.sectionName}” total ${total}, not 100.`);
        const missing = rows.filter((i) => i.gradeRangeCount === 0 && needsBands(i));
        if (missing.length > 0)
          problems.push(
            `${missing.length} item(s) in “${section.sectionName}” have no grade bands.`,
          );
      }
    }
  }

  const refreshSections = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'appraisal-template-sections', templateId] });
  const refreshItems = (sectionId: string) =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'appraisal-template-items', sectionId] });

  const failed = (verb: string) => (e: any) =>
    toast({
      title: 'Error',
      description: e?.message || `Failed to ${verb}.`,
      variant: 'destructive',
    });

  const saveSection = useMutation({
    mutationFn: async () => {
      const { editing, draft } = sectionDialog;
      const payload = {
        appraisalTemplateId: templateId,
        sectionName: draft.sectionName,
        description: draft.description || null,
        displayOrder: draft.displayOrder,
        weight: draft.weight,
        kind: draft.kind,
      };
      return editing
        ? appraisalTemplateService.updateSection(templateId, editing.id, {
            id: editing.id,
            ...payload,
          })
        : appraisalTemplateService.addSection(templateId, payload);
    },
    onSuccess: async () => {
      await refreshSections();
      setSectionDialog({ open: false, editing: null, draft: emptySection });
      toast({ title: 'Saved', description: 'Section saved.' });
    },
    onError: failed('save the section'),
  });

  const saveItem = useMutation({
    mutationFn: async () => {
      const { editing, sectionId, draft } = itemDialog;
      const num = (value: string) => (value.trim() === '' ? null : Number(value));
      const payload = {
        appraisalTemplateSectionId: sectionId,
        competencyId: draft.kind === 'criterion' ? draft.competencyId || null : null,
        kpiDefinitionId: draft.kind === 'kpi' ? draft.kpiDefinitionId || null : null,
        customQuestion: draft.kind === 'question' ? draft.customQuestion || null : null,
        kpiTargetValue: draft.kind === 'kpi' ? num(draft.kpiTargetValue) : null,
        kpiMinValue: draft.kind === 'kpi' ? num(draft.kpiMinValue) : null,
        kpiMaxValue: draft.kind === 'kpi' ? num(draft.kpiMaxValue) : null,
        displayOrder: draft.displayOrder,
        weight: draft.weight,
      };
      return editing
        ? appraisalTemplateService.updateItem(sectionId, editing.id, { id: editing.id, ...payload })
        : appraisalTemplateService.addItem(sectionId, payload);
    },
    onSuccess: async () => {
      await refreshItems(itemDialog.sectionId);
      setItemDialog({ open: false, sectionId: '', editing: null, draft: emptyItem });
      toast({ title: 'Saved', description: 'Item saved.' });
    },
    onError: failed('save the item'),
  });

  const runDelete = useMutation({
    mutationFn: async () => {
      if (!pendingDelete) return;
      if (pendingDelete.kind === 'section')
        return appraisalTemplateService.removeSection(templateId, pendingDelete.id);
      return appraisalTemplateService.removeItem(pendingDelete.sectionId, pendingDelete.id);
    },
    onSuccess: async () => {
      if (pendingDelete?.kind === 'section') await refreshSections();
      else if (pendingDelete) await refreshItems(pendingDelete.sectionId);
      setPendingDelete(null);
      toast({ title: 'Removed', description: 'Deleted.' });
    },
    onError: (e: any) => {
      failed('delete')(e);
      setPendingDelete(null);
    },
  });

  const openNewSection = () =>
    setSectionDialog({
      open: true,
      editing: null,
      draft: {
        ...emptySection,
        displayOrder: sections.length + 1,
        // Offer whatever weight is still unallocated; it is the answer most of the time.
        weight: Math.max(0, 100 - sectionWeightTotal),
      },
    });

  const openEditSection = (section: AppraisalTemplateSection) =>
    setSectionDialog({
      open: true,
      editing: section,
      draft: {
        sectionName: section.sectionName,
        description: section.description ?? '',
        weight: section.weight,
        displayOrder: section.displayOrder,
        kind: section.kind ?? 'Fixed',
      },
    });

  const openNewItem = (sectionId: string) => {
    const rows = itemsBySection.get(sectionId) ?? [];
    const allocated = rows.reduce((sum, i) => sum + i.weight, 0);
    setItemDialog({
      open: true,
      sectionId,
      editing: null,
      draft: {
        ...emptyItem,
        displayOrder: rows.length + 1,
        weight: Math.max(0, 100 - allocated),
      },
    });
  };

  const openEditItem = (sectionId: string, item: AppraisalTemplateItem) =>
    setItemDialog({
      open: true,
      sectionId,
      editing: item,
      draft: {
        kind: item.competencyId ? 'criterion' : item.kpiDefinitionId ? 'kpi' : 'question',
        competencyId: item.competencyId ?? '',
        kpiDefinitionId: item.kpiDefinitionId ?? '',
        customQuestion: item.customQuestion ?? '',
        kpiTargetValue: item.kpiTargetValue?.toString() ?? '',
        kpiMinValue: item.kpiMinValue?.toString() ?? '',
        kpiMaxValue: item.kpiMaxValue?.toString() ?? '',
        weight: item.weight,
        displayOrder: item.displayOrder,
      },
    });

  const itemDraftValid = (() => {
    const { kind, competencyId, kpiDefinitionId, customQuestion } = itemDialog.draft;
    if (kind === 'criterion') return !!competencyId;
    if (kind === 'kpi') return !!kpiDefinitionId;
    return customQuestion.trim().length > 0;
  })();

  if (sectionsQuery.isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  // The goals section's rule, for the dialog: one per template, and only an empty section becomes one.
  const editingSection = sectionDialog.editing;
  const otherGoalsSection = goalsSection && goalsSection.id !== editingSection?.id ? goalsSection : null;
  const editingHasItems =
    !!editingSection && (itemsBySection.get(editingSection.id) ?? []).length > 0;
  const goalsKindBlocked = otherGoalsSection
    ? `“${otherGoalsSection.sectionName}” is already this template's goals section — a template has one.`
    : editingHasItems && editingSection?.kind !== 'EmployeeGoals'
      ? 'This section has items. Remove them first — a goals section holds each employee\'s goals instead.'
      : null;

  return (
    <div className="space-y-4">
      <p className="flex items-start gap-2 text-sm text-muted-foreground">
        <Info className="mt-0.5 h-4 w-4 shrink-0" />
        <span>
          A <strong>fixed</strong> section holds this template&apos;s own items: competencies,
          shared KPIs — one KPI and one target for everyone on this template — and questions. A{' '}
          <strong>goals</strong> section holds no items: on each appraisal it is filled with the
          employee&apos;s own locked goals. Personal targets are goals, not template KPIs.
        </span>
      </p>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <Scale className="h-4 w-4 text-muted-foreground" />
            <span className="text-sm">Section weights</span>
            <Badge variant={sectionWeightTotal === 100 ? 'default' : 'destructive'}>
              {sectionWeightTotal} / 100
            </Badge>
          </div>
          {problems.length === 0 && sections.length > 0 && !itemsLoading ? (
            <p className="flex items-center gap-2 text-sm text-emerald-600 dark:text-emerald-500">
              <CheckCircle2 className="h-4 w-4" />
              Structurally complete — this template can be activated and submitted.
            </p>
          ) : (
            problems.map((message) => (
              <p
                key={message}
                className="flex items-start gap-2 text-sm text-amber-600 dark:text-amber-500"
              >
                <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0" />
                {message}
              </p>
            ))
          )}
        </div>
        {!readOnly && (
          <Button size="sm" onClick={openNewSection}>
            <Plus className="mr-2 h-4 w-4" />
            Add section
          </Button>
        )}
      </div>

      {sections.length === 0 ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              title="No sections yet"
              description="An appraisal form is a set of weighted sections — start with one, then add the items it scores."
              action={
                readOnly ? undefined : (
                  <Button size="sm" variant="outline" onClick={openNewSection}>
                    <Plus className="mr-2 h-4 w-4" />
                    Add section
                  </Button>
                )
              }
            />
          </CardContent>
        </Card>
      ) : (
        sections.map((section) => {
          const rows = itemsBySection.get(section.id) ?? [];
          const itemTotal = rows.reduce((sum, i) => sum + i.weight, 0);
          return (
            <Card key={section.id}>
              <CardHeader className="flex flex-row flex-wrap items-start justify-between gap-3 space-y-0 pb-3">
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <span className="font-medium">{section.sectionName}</span>
                    <Badge variant="outline">Weight {section.weight}</Badge>
                    {section.kind === 'EmployeeGoals' ? (
                      <Badge variant="secondary">
                        <Target className="mr-1 h-3 w-3" />
                        Employee goals
                      </Badge>
                    ) : (
                      rows.length > 0 && (
                        <Badge variant={itemTotal === 100 ? 'secondary' : 'destructive'}>
                          Items {itemTotal} / 100
                        </Badge>
                      )
                    )}
                  </div>
                  {section.description && (
                    <p className="text-sm text-muted-foreground">{section.description}</p>
                  )}
                </div>
                {!readOnly && (
                  <div className="flex items-center gap-2">
                    {section.kind !== 'EmployeeGoals' && (
                      <Button size="sm" variant="outline" onClick={() => openNewItem(section.id)}>
                        <Plus className="mr-2 h-4 w-4" />
                        Add item
                      </Button>
                    )}
                    <Button size="icon" variant="ghost" onClick={() => openEditSection(section)}>
                      <Pencil className="h-4 w-4" />
                      <span className="sr-only">Edit section</span>
                    </Button>
                    <Button
                      size="icon"
                      variant="ghost"
                      onClick={() =>
                        setPendingDelete({
                          kind: 'section',
                          id: section.id,
                          name: section.sectionName,
                        })
                      }
                    >
                      <Trash2 className="h-4 w-4 text-red-600" />
                      <span className="sr-only">Delete section</span>
                    </Button>
                  </div>
                )}
              </CardHeader>
              <CardContent className="p-0">
                {section.kind === 'EmployeeGoals' && rows.length === 0 ? (
                  <p className="px-6 pb-6 text-sm text-muted-foreground">
                    Filled on each appraisal by the employee&apos;s locked goals — one row per goal, each
                    weighted within this section&apos;s {section.weight}% by its own weight. A goal with a
                    target is measured against it; one without is rated on the overall grade scale. The
                    rows are built when the manager locks the goal set.
                  </p>
                ) : rows.length === 0 ? (
                  <p className="px-6 pb-6 text-sm text-muted-foreground">
                    No items yet. A section with no items contributes nothing to a score.
                  </p>
                ) : (
                  <div className="overflow-x-auto">
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Item</TableHead>
                          <TableHead>Type</TableHead>
                          <TableHead className="text-right">Target</TableHead>
                          <TableHead className="text-right">Weight</TableHead>
                          <TableHead>Grade bands</TableHead>
                          {!readOnly && <TableHead className="w-[130px]" />}
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {rows.map((item) => (
                          <TableRow key={item.id}>
                            <TableCell className="font-medium">{itemLabel(item)}</TableCell>
                            <TableCell>
                              <Badge variant="outline">
                                {item.competencyId
                                  ? 'Criterion'
                                  : item.kpiDefinitionId
                                    ? 'KPI'
                                    : 'Question'}
                              </Badge>
                            </TableCell>
                            <TableCell className="text-right tabular-nums">
                              {item.kpiTargetValue ?? '—'}
                            </TableCell>
                            <TableCell className="text-right tabular-nums">{item.weight}</TableCell>
                            <TableCell>
                              {item.gradeRangeCount === 0 ? (
                                needsBands(item) ? (
                                  <Badge variant="destructive">None</Badge>
                                ) : (
                                  <span className="text-xs text-muted-foreground">Not scored</span>
                                )
                              ) : (
                                <Badge variant="secondary">{item.gradeRangeCount}</Badge>
                              )}
                            </TableCell>
                            {!readOnly && (
                              <TableCell>
                                <div className="flex items-center gap-1">
                                  <Button
                                    size="sm"
                                    variant="ghost"
                                    onClick={() =>
                                      setBands({ itemId: item.id, label: itemLabel(item) })
                                    }
                                  >
                                    Bands
                                  </Button>
                                  <Button
                                    size="icon"
                                    variant="ghost"
                                    className="h-8 w-8"
                                    onClick={() => openEditItem(section.id, item)}
                                  >
                                    <Pencil className="h-4 w-4" />
                                    <span className="sr-only">Edit item</span>
                                  </Button>
                                  <Button
                                    size="icon"
                                    variant="ghost"
                                    className="h-8 w-8"
                                    onClick={() =>
                                      setPendingDelete({
                                        kind: 'item',
                                        sectionId: section.id,
                                        id: item.id,
                                        name: itemLabel(item),
                                      })
                                    }
                                  >
                                    <Trash2 className="h-4 w-4 text-red-600" />
                                    <span className="sr-only">Delete item</span>
                                  </Button>
                                </div>
                              </TableCell>
                            )}
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                )}
              </CardContent>
            </Card>
          );
        })
      )}

      {/* ── Section dialog ─────────────────────────────────────────────────── */}
      <Dialog
        open={sectionDialog.open}
        onOpenChange={(open) => setSectionDialog((s) => ({ ...s, open }))}
      >
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>{sectionDialog.editing ? 'Edit section' : 'Add section'}</DialogTitle>
            <DialogDescription>
              Sections carry the weights that split an appraisal; they must total 100.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <fieldset className="space-y-2">
              <legend className="text-sm font-medium">What fills this section</legend>
              <label className="flex items-start gap-2 text-sm">
                <input
                  type="radio"
                  name="sectionKind"
                  className="mt-1"
                  checked={sectionDialog.draft.kind === 'Fixed'}
                  onChange={() =>
                    setSectionDialog((s) => ({ ...s, draft: { ...s.draft, kind: 'Fixed' } }))
                  }
                />
                <span>
                  <span className="font-medium">This template&apos;s items</span>
                  <span className="block text-muted-foreground">
                    Competencies, shared KPIs with one target for everyone, and questions.
                  </span>
                </span>
              </label>
              <label className="flex items-start gap-2 text-sm">
                <input
                  type="radio"
                  name="sectionKind"
                  className="mt-1"
                  disabled={!!goalsKindBlocked && sectionDialog.draft.kind !== 'EmployeeGoals'}
                  checked={sectionDialog.draft.kind === 'EmployeeGoals'}
                  onChange={() =>
                    setSectionDialog((s) => ({ ...s, draft: { ...s.draft, kind: 'EmployeeGoals' } }))
                  }
                />
                <span>
                  <span className="font-medium">Each employee&apos;s locked goals</span>
                  <span className="block text-muted-foreground">
                    No items here: every appraisal gets one row per goal the manager locked, weighted by
                    the goals&apos; own weights.
                  </span>
                  {goalsKindBlocked && sectionDialog.draft.kind !== 'EmployeeGoals' && (
                    <span className="mt-1 block text-amber-600 dark:text-amber-500">
                      {goalsKindBlocked}
                    </span>
                  )}
                </span>
              </label>
            </fieldset>
            <div className="space-y-2">
              <label className="text-sm font-medium" htmlFor="sectionName">
                Section name <span className="text-red-500">*</span>
              </label>
              <input
                id="sectionName"
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={sectionDialog.draft.sectionName}
                placeholder="e.g. Key result areas"
                onChange={(e) =>
                  setSectionDialog((s) => ({
                    ...s,
                    draft: { ...s.draft, sectionName: e.target.value },
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium" htmlFor="sectionDescription">
                Description
              </label>
              <textarea
                id="sectionDescription"
                rows={2}
                className="flex w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={sectionDialog.draft.description}
                onChange={(e) =>
                  setSectionDialog((s) => ({
                    ...s,
                    draft: { ...s.draft, description: e.target.value },
                  }))
                }
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <label className="text-sm font-medium" htmlFor="sectionWeight">
                  Weight
                </label>
                <input
                  id="sectionWeight"
                  type="number"
                  min={0}
                  max={100}
                  className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                  value={sectionDialog.draft.weight}
                  onChange={(e) =>
                    setSectionDialog((s) => ({
                      ...s,
                      draft: { ...s.draft, weight: Number(e.target.value) },
                    }))
                  }
                />
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium" htmlFor="sectionOrder">
                  Display order
                </label>
                <input
                  id="sectionOrder"
                  type="number"
                  min={0}
                  className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                  value={sectionDialog.draft.displayOrder}
                  onChange={(e) =>
                    setSectionDialog((s) => ({
                      ...s,
                      draft: { ...s.draft, displayOrder: Number(e.target.value) },
                    }))
                  }
                />
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setSectionDialog((s) => ({ ...s, open: false }))}
            >
              Cancel
            </Button>
            <Button
              onClick={() => saveSection.mutate()}
              disabled={saveSection.isPending || !sectionDialog.draft.sectionName.trim()}
            >
              {saveSection.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save section
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Item dialog ────────────────────────────────────────────────────── */}
      <Dialog open={itemDialog.open} onOpenChange={(open) => setItemDialog((s) => ({ ...s, open }))}>
        <DialogContent className="sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>{itemDialog.editing ? 'Edit item' : 'Add item'}</DialogTitle>
            <DialogDescription>
              An item scores a criterion, a KPI or a written question. The same criterion or KPI
              may only appear once in the whole template.
            </DialogDescription>
          </DialogHeader>
          <div className="max-h-[55vh] space-y-4 overflow-y-auto py-2">
            <div className="flex gap-2">
              {(['criterion', 'kpi', 'question'] as const).map((kind) => (
                <Button
                  key={kind}
                  type="button"
                  size="sm"
                  variant={itemDialog.draft.kind === kind ? 'default' : 'outline'}
                  onClick={() =>
                    setItemDialog((s) => ({ ...s, draft: { ...s.draft, kind } }))
                  }
                >
                  {kind === 'criterion' ? 'Criterion' : kind === 'kpi' ? 'KPI' : 'Question'}
                </Button>
              ))}
            </div>

            {itemDialog.draft.kind === 'criterion' && (
              <div className="space-y-2">
                <label className="text-sm font-medium">Criterion</label>
                <select
                  className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                  value={itemDialog.draft.competencyId}
                  onChange={(e) =>
                    setItemDialog((s) => ({
                      ...s,
                      draft: { ...s.draft, competencyId: e.target.value },
                    }))
                  }
                >
                  <option value="">Select a criterion…</option>
                  {(criteria ?? [])
                    .filter((c) => c.isActive || c.id === itemDialog.draft.competencyId)
                    .map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.criteriaName}
                      </option>
                    ))}
                </select>
              </div>
            )}

            {itemDialog.draft.kind === 'kpi' && (
              <>
                <div className="space-y-2">
                  <label className="text-sm font-medium">KPI definition</label>
                  <select
                    className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                    value={itemDialog.draft.kpiDefinitionId}
                    onChange={(e) =>
                      setItemDialog((s) => ({
                        ...s,
                        draft: { ...s.draft, kpiDefinitionId: e.target.value },
                      }))
                    }
                  >
                    <option value="">Select a KPI…</option>
                    {(kpis ?? [])
                      .filter((k) => k.isActive || k.id === itemDialog.draft.kpiDefinitionId)
                      .map((k) => (
                        <option key={k.id} value={k.id}>
                          {k.kpiName}
                        </option>
                      ))}
                  </select>
                </div>
                <div className="grid grid-cols-3 gap-3">
                  {(
                    [
                      ['kpiTargetValue', 'Target'],
                      ['kpiMinValue', 'Minimum'],
                      ['kpiMaxValue', 'Maximum'],
                    ] as const
                  ).map(([field, label]) => (
                    <div key={field} className="space-y-2">
                      <label className="text-sm font-medium">{label}</label>
                      <input
                        type="number"
                        step="any"
                        className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                        value={itemDialog.draft[field]}
                        onChange={(e) =>
                          setItemDialog((s) => ({
                            ...s,
                            draft: { ...s.draft, [field]: e.target.value },
                          }))
                        }
                      />
                    </div>
                  ))}
                </div>
                <p className="text-xs text-muted-foreground">
                  A target set here is the template default. An employee’s own locked goal takes
                  precedence over it when the appraisal is generated.
                </p>
              </>
            )}

            {itemDialog.draft.kind === 'question' && (
              <div className="space-y-2">
                <label className="text-sm font-medium">Question</label>
                <textarea
                  rows={2}
                  className="flex w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                  placeholder="e.g. Describe the candidate’s biggest contribution this period."
                  value={itemDialog.draft.customQuestion}
                  onChange={(e) =>
                    setItemDialog((s) => ({
                      ...s,
                      draft: { ...s.draft, customQuestion: e.target.value },
                    }))
                  }
                />
              </div>
            )}

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <label className="text-sm font-medium">Weight</label>
                <input
                  type="number"
                  min={0}
                  max={100}
                  className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                  value={itemDialog.draft.weight}
                  onChange={(e) =>
                    setItemDialog((s) => ({
                      ...s,
                      draft: { ...s.draft, weight: Number(e.target.value) },
                    }))
                  }
                />
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">Display order</label>
                <input
                  type="number"
                  min={0}
                  className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                  value={itemDialog.draft.displayOrder}
                  onChange={(e) =>
                    setItemDialog((s) => ({
                      ...s,
                      draft: { ...s.draft, displayOrder: Number(e.target.value) },
                    }))
                  }
                />
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setItemDialog((s) => ({ ...s, open: false }))}>
              Cancel
            </Button>
            <Button onClick={() => saveItem.mutate()} disabled={saveItem.isPending || !itemDraftValid}>
              {saveItem.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save item
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <GradeRangeDialog
        itemId={bands.itemId}
        itemLabel={bands.label}
        open={bands.itemId !== null}
        onOpenChange={(open) => !open && setBands({ itemId: null, label: '' })}
        onSaved={() => {
          // The band count lives on the item row, so the section's items must be re-read.
          for (const section of sections) void refreshItems(section.id);
        }}
      />

      <ConfirmationDialog
        open={pendingDelete !== null}
        onOpenChange={(open) => !open && setPendingDelete(null)}
        title={pendingDelete?.kind === 'section' ? 'Delete this section?' : 'Delete this item?'}
        description={
          pendingDelete?.kind === 'section'
            ? `“${pendingDelete.name}” and every item in it will be removed.`
            : `“${pendingDelete?.name}” will be removed, along with its grade bands.`
        }
        confirmText="Delete"
        variant="destructive"
        isLoading={runDelete.isPending}
        onConfirm={async () => {
          await runDelete.mutateAsync();
        }}
      />
    </div>
  );
}
