'use client';

import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { SelectField, SwitchField, TextField } from '@/components/hr/employee/tabs/fields';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import { safetyPpeService } from '@/services/hr/safety-ppe.service';
import type { JobPpeRequirement } from '@/types/hr/job-architecture';
import { childKey, idOrNull, nullIfBlank, type ChildPanelProps } from './shared';

/**
 * ⚠ The API accepts a row with neither a catalogue link nor a name, and it renders as a blank line
 * forever — `DisplayName` resolves to the empty string when both are absent. The schema refuses it
 * here because there is no way to correct such a row from the list: it has no label to click.
 */
const schema = z
  .object({
    ppeTypeId: z.string().optional(),
    customPpeName: z.string().max(200).optional(),
    isMandatory: z.boolean(),
    notes: z.string().max(500).optional(),
  })
  .refine((v) => !!idOrNull(v.ppeTypeId) || !!nullIfBlank(v.customPpeName), {
    message: 'Choose an item from the catalogue, or type a name',
    path: ['customPpeName'],
  });

type Form = z.infer<typeof schema>;

const empty: Form = { ppeTypeId: '', customPpeName: '', isMandatory: true, notes: '' };

/**
 * Protective equipment the job requires, linked to the safety module's PPE catalogue.
 *
 * ⚠ **The catalogue link is what makes a row actionable.** A row with `ppeTypeId` set is the same
 * item the safety module issues, tracks a lifespan for and can expire; a row with only
 * `customPpeName` is a sentence. `DisplayName` prefers the catalogue name whenever both are
 * present, so typing a name next to a chosen item does not override it — the free-text box is for
 * things genuinely not in the catalogue, and the form says so rather than offering two
 * interchangeable ways to name the same hard hat.
 */
export function PpeRequirementsPanel({
  jobDescriptionId,
  canAuthor,
  canDelete,
  invalidateKeys,
}: ChildPanelProps) {
  const { data: ppeTypes } = useQuery({
    queryKey: ['hr', 'safety', 'ppe-types', 'active'],
    queryFn: () => safetyPpeService.getTypes(true),
    staleTime: 5 * 60 * 1000,
  });

  const ppeOptions = (ppeTypes ?? []).map((t) => ({
    value: t.id,
    label: t.code ? `${t.name} (${t.code})` : t.name,
  }));

  return (
    <ResourceCollectionTab<JobPpeRequirement, Form>
      parentId={jobDescriptionId}
      title="PPE requirements"
      singular="PPE requirement"
      queryKey={childKey(jobDescriptionId, 'ppe-requirements')}
      invalidateKeys={invalidateKeys}
      readOnly={!canAuthor}
      dialogHint="An item of protective equipment the holder needs."
      emptyDescription="Record the protective equipment the job requires. Items from the catalogue can be issued and tracked."
      list={(id) => jobArchitectureService.getPpeRequirements(id)}
      create={(id, v) =>
        jobArchitectureService.addPpeRequirement(id, {
          ppeTypeId: idOrNull(v.ppeTypeId),
          customPpeName: idOrNull(v.ppeTypeId) ? null : nullIfBlank(v.customPpeName),
          isMandatory: v.isMandatory,
          notes: nullIfBlank(v.notes),
        })
      }
      update={(_id, requirementId, v) =>
        jobArchitectureService.updatePpeRequirement(requirementId, {
          ppeTypeId: idOrNull(v.ppeTypeId),
          customPpeName: idOrNull(v.ppeTypeId) ? null : nullIfBlank(v.customPpeName),
          isMandatory: v.isMandatory,
          notes: nullIfBlank(v.notes),
        })
      }
      remove={
        canDelete
          ? (_id, requirementId) => jobArchitectureService.deletePpeRequirement(requirementId)
          : undefined
      }
      getId={(p) => p.id}
      columns={[
        {
          header: 'Item',
          // The read DTO computes `displayName`, but a create response that skipped the re-include
          // would leave it empty — so fall back the same way the server does.
          cell: (p) => p.ppeTypeName || p.customPpeName || '—',
        },
        {
          header: 'Source',
          cell: (p) =>
            p.ppeTypeId ? (
              <Badge variant="outline">Catalogue</Badge>
            ) : (
              <Badge variant="secondary">Free text</Badge>
            ),
        },
        {
          header: '',
          cell: (p) =>
            p.isMandatory ? <Badge variant="outline">Mandatory</Badge> : <Badge variant="secondary">Optional</Badge>,
        },
        { header: 'Notes', cell: (p) => <span className="text-muted-foreground">{p.notes || '—'}</span> },
      ]}
      schema={schema}
      emptyForm={empty}
      toForm={(p) => ({
        ppeTypeId: p.ppeTypeId ?? '',
        customPpeName: p.customPpeName ?? '',
        isMandatory: p.isMandatory,
        notes: p.notes ?? '',
      })}
      renderFields={(form) => {
        const fromCatalogue = !!form.watch('ppeTypeId');
        return (
          <>
            <SelectField
              form={form}
              name="ppeTypeId"
              label="From the PPE catalogue"
              allowEmpty
              emptyLabel="Not in the catalogue"
              placeholder="Choose an item"
              options={ppeOptions}
            />
            {!fromCatalogue && (
              <TextField
                form={form}
                name="customPpeName"
                label="Item name"
                required
                placeholder="e.g. Chainsaw trousers"
              />
            )}
            {fromCatalogue && (
              <p className="text-xs text-muted-foreground">
                Issuance, lifespan and expiry for this item are tracked by the safety module.
              </p>
            )}
            <SwitchField
              form={form}
              name="isMandatory"
              label="Mandatory"
              description="Turn off where the item is issued on request rather than always worn."
            />
            <TextField form={form} name="notes" label="Notes" placeholder="Optional" />
          </>
        );
      }}
    />
  );
}
