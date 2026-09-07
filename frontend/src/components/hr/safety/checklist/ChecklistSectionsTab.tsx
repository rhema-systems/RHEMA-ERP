'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { ArrowDown, ArrowUp, Loader2, Pencil, Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { useToast } from '@/components/ui/use-toast';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { RiskBadge } from '@/components/hr/safety/RiskBadge';
import { safetyChecklistService } from '@/services/hr/safety-checklist.service';
import { SHE_RISK_LEVEL_OPTIONS } from '@/types/hr/safety-hazards';
import {
  SHE_CHECKLIST_SECTION_KIND_OPTIONS,
  type SheInspectionChecklist,
  type SheInspectionChecklistItem,
  type SheInspectionChecklistSection,
} from '@/types/hr/safety-inspections';
import { EMPTY_GUID } from './checklist-scoring';
import { blank, moveWithin } from './builder-utils';

/**
 * The body of the form: lettered sections, each with its numbered items. Standard sections score
 * C / NC / NA; a Critical section holds the Yes/No disqualifiers. Sections are cards; items are a
 * collection table inside each. Legacy section-less items (from before the builder) show under a
 * synthetic read-only "General" section — publish requires every item to be in a real section.
 */
const sectionSchema = z.object({
  displayOrder: z.coerce.number().min(0).max(999),
  code: z.string().max(10).optional().or(z.literal('')),
  title: z.string().min(1, 'A title is required').max(200),
  description: z.string().max(500).optional().or(z.literal('')),
  kind: z.enum(['Standard', 'Critical']),
});
type SectionForm = z.input<typeof sectionSchema>;

const itemSchema = z.object({
  itemOrder: z.coerce.number().min(0).max(999),
  itemDescription: z.string().min(1, 'Describe what is checked').max(500),
  isMandatory: z.boolean(),
  regulatoryReference: z.string().max(200).optional().or(z.literal('')),
  associatedRiskLevel: z.enum(['Negligible', 'Low', 'Medium', 'High', 'Critical']).optional().or(z.literal('')),
});
type ItemForm = z.input<typeof itemSchema>;

export function ChecklistSectionsTab({ checklist }: { checklist: SheInspectionChecklist }) {
  const id = checklist.id;
  const locked = checklist.isStructureLocked;
  const qc = useQueryClient();
  const { toast } = useToast();
  const refresh = () => qc.invalidateQueries({ queryKey: ['hr', 'safety-checklist', id] });

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<SheInspectionChecklistSection | null>(null);
  const [deleting, setDeleting] = useState<SheInspectionChecklistSection | null>(null);
  const [saving, setSaving] = useState(false);

  const sectionForm = useForm<SectionForm>({
    resolver: zodResolver(sectionSchema) as any,
    defaultValues: { displayOrder: 1, code: '', title: '', description: '', kind: 'Standard' },
  });

  const openCreate = () => {
    setEditing(null);
    const real = checklist.sections.filter((s) => s.id !== EMPTY_GUID);
    const nextLetter = String.fromCharCode(65 + Math.min(real.length, 25));
    sectionForm.reset({ displayOrder: real.length + 1, code: nextLetter, title: '', description: '', kind: 'Standard' });
    setDialogOpen(true);
  };
  const openEdit = (s: SheInspectionChecklistSection) => {
    setEditing(s);
    sectionForm.reset({
      displayOrder: s.displayOrder,
      code: s.code ?? '',
      title: s.title,
      description: s.description ?? '',
      kind: s.kind,
    });
    setDialogOpen(true);
  };

  const fail = (e: any, fallback: string) =>
    toast({ title: 'Error', description: e?.message || fallback, variant: 'destructive' });

  const submitSection = async (values: SectionForm) => {
    const v = sectionSchema.parse(values);
    setSaving(true);
    try {
      if (editing) {
        await safetyChecklistService.updateSection(editing.id, {
          id: editing.id,
          displayOrder: v.displayOrder,
          code: blank(v.code),
          title: v.title,
          description: blank(v.description),
          kind: v.kind,
        });
      } else {
        await safetyChecklistService.addSection(id, {
          checklistId: id,
          displayOrder: v.displayOrder,
          code: blank(v.code),
          title: v.title,
          description: blank(v.description),
          kind: v.kind,
        });
      }
      setDialogOpen(false);
      await refresh();
    } catch (e: any) {
      fail(e, 'Failed to save the section.');
    } finally {
      setSaving(false);
    }
  };

  const moveSection = async (s: SheInspectionChecklistSection, delta: -1 | 1) => {
    try {
      const ids = checklist.sections.filter((x) => x.id !== EMPTY_GUID).map((x) => x.id);
      await safetyChecklistService.reorderSections(id, moveWithin(ids, s.id, delta));
      await refresh();
    } catch (e: any) {
      fail(e, 'Failed to reorder.');
    }
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    try {
      await safetyChecklistService.removeSection(deleting.id);
      setDeleting(null);
      await refresh();
    } catch (e: any) {
      fail(e, 'Failed to delete the section.');
    }
  };

  const itemsOf = (section: SheInspectionChecklistSection) =>
    section.id === EMPTY_GUID
      ? Promise.resolve(section.items)
      : safetyChecklistService.getById(id).then((c) => c.sections.find((s) => s.id === section.id)?.items ?? []);

  const reorderItem = async (section: SheInspectionChecklistSection, itemId: string, delta: -1 | 1) => {
    const ids = (await itemsOf(section)).map((i) => i.id);
    await safetyChecklistService.reorderSectionItems(section.id, moveWithin(ids, itemId, delta));
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-muted-foreground text-sm">
          {checklist.sections.length === 0
            ? 'No sections yet. A template needs at least one standard section with an item before it can be published.'
            : `${checklist.sections.length} section${checklist.sections.length === 1 ? '' : 's'} · ${checklist.itemCount} item${checklist.itemCount === 1 ? '' : 's'}`}
        </p>
        {!locked && (
          <Button size="sm" onClick={openCreate}>
            <Plus className="mr-2 h-4 w-4" />
            Add section
          </Button>
        )}
      </div>

      {checklist.sections.map((section, index) => {
        const synthetic = section.id === EMPTY_GUID;
        const readOnly = locked || synthetic;
        return (
          <Card key={section.id !== EMPTY_GUID ? section.id : `legacy-${section.title}`}>
            <CardHeader className="flex flex-row items-start justify-between gap-4 space-y-0">
              <div>
                <CardTitle className="text-base">
                  {section.code ? `${section.code}. ` : ''}
                  {section.title}{' '}
                  {section.kind === 'Critical' ? (
                    <Badge variant="destructive" className="ml-2 align-middle">
                      Critical · Yes / No
                    </Badge>
                  ) : null}
                  {synthetic ? (
                    <Badge variant="outline" className="ml-2 align-middle">
                      Legacy items — not in a section
                    </Badge>
                  ) : null}
                </CardTitle>
                {section.description ? <p className="text-muted-foreground mt-1 text-sm">{section.description}</p> : null}
              </div>
              {!readOnly && (
                <div className="flex shrink-0 gap-1">
                  <Button variant="ghost" size="icon" title="Move up" disabled={index === 0} onClick={() => moveSection(section, -1)}>
                    <ArrowUp className="h-4 w-4" />
                  </Button>
                  <Button
                    variant="ghost"
                    size="icon"
                    title="Move down"
                    disabled={index === checklist.sections.length - 1}
                    onClick={() => moveSection(section, 1)}
                  >
                    <ArrowDown className="h-4 w-4" />
                  </Button>
                  <Button variant="ghost" size="icon" title="Edit section" onClick={() => openEdit(section)}>
                    <Pencil className="h-4 w-4" />
                  </Button>
                  <Button variant="ghost" size="icon" title="Delete section" className="text-red-600" onClick={() => setDeleting(section)}>
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </div>
              )}
            </CardHeader>
            <CardContent>
              <ResourceCollectionTab<SheInspectionChecklistItem, ItemForm>
                parentId={section.id}
                title="items"
                singular="item"
                queryKey={['hr', 'safety-checklist', id, 'section', section.id, section.title, 'items']}
                invalidateKeys={[['hr', 'safety-checklist', id]]}
                list={() => itemsOf(section)}
                readOnly={readOnly}
                create={(sectionId, values) => {
                  const v = itemSchema.parse(values);
                  return safetyChecklistService.addItem(id, {
                    checklistId: id,
                    sectionId,
                    itemOrder: v.itemOrder,
                    category: null,
                    itemDescription: v.itemDescription,
                    isMandatory: v.isMandatory,
                    regulatoryReference: blank(v.regulatoryReference),
                    associatedRiskLevel: v.associatedRiskLevel === '' ? null : v.associatedRiskLevel,
                  });
                }}
                update={(sectionId, itemId, values) => {
                  const v = itemSchema.parse(values);
                  return safetyChecklistService.updateItem(itemId, {
                    id: itemId,
                    sectionId,
                    itemOrder: v.itemOrder,
                    category: null,
                    itemDescription: v.itemDescription,
                    isMandatory: v.isMandatory,
                    regulatoryReference: blank(v.regulatoryReference),
                    associatedRiskLevel: v.associatedRiskLevel === '' ? null : v.associatedRiskLevel,
                  });
                }}
                remove={(_sectionId, itemId) => safetyChecklistService.removeItem(itemId)}
                actions={[
                  { label: 'Move up', run: (i) => reorderItem(section, i.id, -1), visible: () => !readOnly },
                  { label: 'Move down', run: (i) => reorderItem(section, i.id, 1), visible: () => !readOnly },
                ]}
                columns={[
                  {
                    header: 'No.',
                    cell: (i) => <span className="tabular-nums">{section.kind === 'Critical' ? '—' : i.itemNumber || i.itemOrder}</span>,
                  },
                  { header: section.kind === 'Critical' ? 'Non-conformity' : 'What is checked', cell: (i) => i.itemDescription },
                  { header: 'Mandatory', cell: (i) => (i.isMandatory ? <Badge variant="secondary">Mandatory</Badge> : '—') },
                  { header: 'Reg. reference', cell: (i) => i.regulatoryReference ?? '—' },
                  {
                    header: 'Risk',
                    cell: (i) =>
                      i.associatedRiskLevel ? (
                        <RiskBadge level={i.associatedRiskLevel} label={i.associatedRiskLevelName ?? undefined} />
                      ) : (
                        '—'
                      ),
                  },
                ]}
                schema={itemSchema}
                emptyForm={{
                  itemOrder: section.items.length + 1,
                  itemDescription: '',
                  isMandatory: true,
                  regulatoryReference: '',
                  associatedRiskLevel: '',
                }}
                toForm={(i) => ({
                  itemOrder: i.itemOrder,
                  itemDescription: i.itemDescription,
                  isMandatory: i.isMandatory,
                  regulatoryReference: i.regulatoryReference ?? '',
                  associatedRiskLevel: i.associatedRiskLevel ?? '',
                })}
                renderFields={(f) => (
                  <>
                    <NumberField form={f} name="itemOrder" label="Order within section" required />
                    <TextareaField
                      form={f}
                      name="itemDescription"
                      label={section.kind === 'Critical' ? 'Non-conformity (answered Yes / No)' : 'What is checked'}
                      rows={2}
                    />
                    <FieldRow>
                      <TextField form={f} name="regulatoryReference" label="Regulatory reference" />
                      <SelectField form={f} name="associatedRiskLevel" label="Associated risk" allowEmpty options={SHE_RISK_LEVEL_OPTIONS} />
                    </FieldRow>
                    <SwitchField form={f} name="isMandatory" label="Mandatory" description="Mandatory items must be assessed on every inspection using this checklist." />
                  </>
                )}
                getId={(i) => i.id}
                emptyDescription={
                  section.kind === 'Critical'
                    ? 'Add the conditions that disqualify outright.'
                    : 'Add the checks in this section, in order.'
                }
                dialogHint={readOnly ? 'This template is published; create a new version to change its items.' : undefined}
              />
            </CardContent>
          </Card>
        );
      })}

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <form onSubmit={sectionForm.handleSubmit(submitSection)}>
            <DialogHeader>
              <DialogTitle>{editing ? 'Edit section' : 'Add section'}</DialogTitle>
              <DialogDescription>
                Standard sections score C / NC / NA. A critical section is answered Yes / No, and any Yes forces the
                disqualifying outcome.
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4 py-4">
              <FieldRow>
                <NumberField form={sectionForm} name="displayOrder" label="Order" required />
                <TextField form={sectionForm} name="code" label="Letter / code" placeholder="e.g. A" />
              </FieldRow>
              <TextField form={sectionForm} name="title" label="Title" required placeholder="e.g. Personal Hygiene" />
              <TextareaField form={sectionForm} name="description" label="Description" rows={2} />
              <SelectField form={sectionForm} name="kind" label="Kind" required options={SHE_CHECKLIST_SECTION_KIND_OPTIONS} />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setDialogOpen(false)} disabled={saving}>
                Cancel
              </Button>
              <Button type="submit" disabled={saving}>
                {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {editing ? 'Save' : 'Add section'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={!!deleting}
        onOpenChange={(open) => !open && setDeleting(null)}
        title={`Delete section “${deleting?.title ?? ''}”?`}
        description={`Its ${deleting?.items.length ?? 0} item(s) go with it.`}
        confirmText="Delete"
        variant="destructive"
        onConfirm={confirmDelete}
      />
    </div>
  );
}
