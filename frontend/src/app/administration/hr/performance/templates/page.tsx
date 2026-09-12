'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Copy, Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  TextareaField,
  SelectField,
  SwitchField,
} from '@/components/hr/employee/tabs/fields';
import { appraisalTemplateService } from '@/services/hr/appraisal.service';
import { organizationLevelService } from '@/services/hr/organization-level.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { humanizeEnum } from '@/lib/hr/attendance-format';
import { scopeLabel } from '@/lib/hr/appraisal-scope';
import type { AppraisalTemplateSummary } from '@/types/hr/appraisal';

/**
 * Appraisal templates — the forms appraisals are scored on.
 *
 * Scope is what decides who gets which form, most specific first: position, then unit, then
 * level, then an unscoped template that catches everyone else. A cycle assigns the templates
 * it may draw on, and the coverage preview on the cycle screen is where you see the result of
 * that resolution before anything is generated.
 *
 * Two things a user hits often enough to be worth saying here:
 *  • **Activating validates the structure.** Section weights must total 100, item weights must
 *    total 100 within each section, and every item needs grade bands. The template detail page
 *    shows all three live.
 *  • **A template in a live cycle is frozen.** Every structural edit is refused once it is
 *    assigned to an Open or InProgress cycle. Copy it and change the copy.
 */
const templateSchema = z.object({
  templateName: z.string().min(1, 'Required').max(100),
  description: z.string().max(500).optional(),
  organizationLevelId: z.string().optional(),
  organizationUnitId: z.string().optional(),
  positionId: z.string().optional(),
  isActive: z.boolean(),
});

type TemplateForm = z.input<typeof templateSchema>;

const emptyTemplate: TemplateForm = {
  templateName: '',
  description: '',
  organizationLevelId: '',
  organizationUnitId: '',
  positionId: '',
  isActive: false,
};

const toPayload = (values: TemplateForm) => {
  const v = templateSchema.parse(values);
  return {
    templateName: v.templateName,
    description: v.description || null,
    organizationLevelId: v.organizationLevelId || null,
    organizationUnitId: v.organizationUnitId || null,
    positionId: v.positionId || null,
    isActive: v.isActive,
  };
};

export default function AppraisalTemplatesPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [cloneOf, setCloneOf] = useState<AppraisalTemplateSummary | null>(null);
  const [cloneName, setCloneName] = useState('');

  const { data: levels } = useQuery({
    queryKey: ['hr', 'organization-levels'],
    queryFn: () => organizationLevelService.getAll(),
  });
  const { data: units } = useQuery({
    queryKey: ['hr', 'organization-units'],
    queryFn: () => organizationUnitService.getAll(),
  });
  const { data: positions } = useQuery({
    queryKey: ['hr', 'positions', 'active'],
    queryFn: () => employeePositionService.getActive(),
  });

  const levelOptions = useMemo(
    () => (levels ?? []).map((l) => ({ value: l.id, label: l.name })),
    [levels],
  );
  const unitOptions = useMemo(
    () => (units ?? []).map((u) => ({ value: u.id, label: u.name })),
    [units],
  );
  const positionOptions = useMemo(
    () => (positions ?? []).map((p) => ({ value: p.id, label: p.title })),
    [positions],
  );

  /**
   * The copy keeps the source's sections, items and bands but not its scope — the whole point
   * of copying is usually to point the same form at a different part of the organisation, so
   * the new scope is stated on the copy rather than inherited.
   */
  const clone = useMutation({
    mutationFn: () => {
      // Guarded by the dialog only opening once a source is chosen; the button is disabled
      // until then, so this is a type narrowing rather than a real branch.
      if (!cloneOf) throw new Error('No template selected to copy.');
      return appraisalTemplateService.clone(cloneOf.id, {
        newTemplateName: cloneName.trim(),
        organizationLevelId: cloneOf.organizationLevelId ?? null,
        organizationUnitId: cloneOf.organizationUnitId ?? null,
        positionId: cloneOf.positionId ?? null,
      });
    },
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'appraisal-templates'] });
      toast({ title: 'Copied', description: `"${created.templateName}" created as a draft.` });
      setCloneOf(null);
      setCloneName('');
      router.push(`/administration/hr/performance/templates/${created.id}`);
    },
    onError: (e: any) =>
      toast({
        title: 'Error',
        description: e?.message || 'Failed to copy the template.',
        variant: 'destructive',
      }),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Appraisal Templates"
        description="The forms appraisals are scored on. Scope decides who gets which one; a cycle assigns the ones it may use."
        backHref="/administration/hr/performance"
      />

      <ResourceListPanel<AppraisalTemplateSummary, TemplateForm>
        title="templates"
        singular="template"
        queryKey={['hr', 'appraisal-templates']}
        dialogHint="Leave every scope blank for a template that catches everyone no more specific template covers."
        emptyDescription="Add the appraisal form your organisation scores against."
        list={() => appraisalTemplateService.getSummaries()}
        create={(values) => appraisalTemplateService.create(toPayload(values))}
        update={(id, values) => appraisalTemplateService.update(id, { id, ...toPayload(values) })}
        remove={(id) => appraisalTemplateService.remove(id)}
        getId={(r) => r.id}
        actions={[
          {
            label: (r) => (r.isActive ? 'Deactivate' : 'Activate'),
            run: (r) => appraisalTemplateService.setActive(r.id, !r.isActive),
          },
          {
            label: 'Copy…',
            run: async (r) => {
              setCloneOf(r);
              setCloneName(`${r.templateName} (copy)`);
            },
          },
        ]}
        columns={[
          {
            header: 'Template',
            cell: (r) => (
              <Link
                href={`/administration/hr/performance/templates/${r.id}`}
                className="font-medium hover:underline"
              >
                {r.templateName}
              </Link>
            ),
          },
          { header: 'Scope', cell: (r) => <Badge variant="outline">{scopeLabel(r)}</Badge> },
          {
            header: 'Structure',
            cell: (r) => (
              <span className="text-muted-foreground">
                {r.sectionsCount} section{r.sectionsCount === 1 ? '' : 's'} · {r.totalItemsCount}{' '}
                item{r.totalItemsCount === 1 ? '' : 's'}
              </span>
            ),
          },
          {
            header: 'Approval',
            cell: (r) => <StatusBadge status={humanizeEnum(r.approvalStatus)} />,
          },
          { header: 'Active', cell: (r) => <StatusBadge active={r.isActive} /> },
          {
            header: 'In use',
            cell: (r) =>
              r.hasCycleAssignments ? (
                <Badge variant="secondary">Assigned to a cycle</Badge>
              ) : (
                <span className="text-muted-foreground">—</span>
              ),
          },
        ]}
        schema={templateSchema as any}
        emptyForm={emptyTemplate}
        toForm={(r) => ({
          templateName: r.templateName,
          description: r.description ?? '',
          organizationLevelId: r.organizationLevelId ?? '',
          organizationUnitId: r.organizationUnitId ?? '',
          positionId: r.positionId ?? '',
          isActive: r.isActive,
        })}
        renderFields={(form) => (
          <>
            <TextField
              form={form}
              name="templateName"
              label="Template name"
              required
              placeholder="e.g. Annual appraisal — supervisory staff"
            />
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <SelectField
              form={form}
              name="organizationLevelId"
              label="Organisation level"
              options={levelOptions}
              allowEmpty
              emptyLabel="Any level"
            />
            <SelectField
              form={form}
              name="organizationUnitId"
              label="Organisation unit"
              options={unitOptions}
              allowEmpty
              emptyLabel="Any unit"
            />
            <SelectField
              form={form}
              name="positionId"
              label="Position"
              options={positionOptions}
              allowEmpty
              emptyLabel="Any position"
            />
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="Activating validates the structure first — leave it off until sections and items are in place."
            />
          </>
        )}
      />

      <Dialog open={cloneOf !== null} onOpenChange={(open) => !open && setCloneOf(null)}>
        <DialogContent className="sm:max-w-[480px]">
          <DialogHeader>
            <DialogTitle>Copy template</DialogTitle>
            <DialogDescription>
              The copy takes every section, item and grade band from “{cloneOf?.templateName}” and
              lands as an inactive draft. Change its scope on the copy afterwards.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2 py-2">
            <Label htmlFor="cloneName">New template name</Label>
            <Input
              id="cloneName"
              value={cloneName}
              onChange={(e) => setCloneName(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCloneOf(null)}>
              Cancel
            </Button>
            <Button onClick={() => clone.mutate()} disabled={clone.isPending || !cloneName.trim()}>
              {clone.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Copy className="mr-2 h-4 w-4" />
              )}
              Copy
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
