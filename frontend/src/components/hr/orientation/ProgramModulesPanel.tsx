'use client';

import { useState } from 'react';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { orientationProgramService } from '@/services/hr/orientation-program.service';
import {
  ORIENTATION_MODULE_TYPE_OPTIONS,
  ORIENTATION_CONTENT_TYPE_OPTIONS,
} from '@/types/hr/orientation';
import type { OrientationModule, OrientationContentItem } from '@/types/hr/orientation';

const moduleSchema = z.object({
  title: z.string().min(1, 'A title is required').max(200),
  description: z.string().max(2000).optional().or(z.literal('')),
  sequenceOrder: z.coerce.number().min(0).max(9999),
  moduleType: z.enum([
    'InformationContent',
    'VideoLesson',
    'Interactive',
    'Assessment',
    'Acknowledgement',
    'Survey',
    'LiveSession',
  ]),
  estimatedDurationMinutes: z.coerce.number().min(0).max(100000).optional(),
  isSequentiallyRequired: z.boolean(),
  isOptional: z.boolean(),
  isActive: z.boolean(),
});
type ModuleForm = z.infer<typeof moduleSchema>;

const emptyModule: ModuleForm = {
  title: '',
  description: '',
  sequenceOrder: 0,
  moduleType: 'InformationContent',
  estimatedDurationMinutes: undefined,
  isSequentiallyRequired: false,
  isOptional: false,
  isActive: true,
};

const contentSchema = z.object({
  title: z.string().min(1, 'A title is required').max(200),
  description: z.string().max(2000).optional().or(z.literal('')),
  contentType: z.enum([
    'Video',
    'Audio',
    'PDF',
    'Document',
    'Presentation',
    'ExternalLink',
    'EmbeddedWebPage',
    'Image',
    'Text',
    'Quiz',
  ]),
  resourceUrl: z.string().max(1000).optional().or(z.literal('')),
  originalFileName: z.string().max(400).optional().or(z.literal('')),
  mediaDurationSeconds: z.coerce.number().min(0).max(1000000).optional(),
  sequenceOrder: z.coerce.number().min(0).max(9999),
  isRequired: z.boolean(),
  isActive: z.boolean(),
});
type ContentForm = z.infer<typeof contentSchema>;

const emptyContent: ContentForm = {
  title: '',
  description: '',
  contentType: 'Document',
  resourceUrl: '',
  originalFileName: '',
  mediaDurationSeconds: undefined,
  sequenceOrder: 0,
  isRequired: true,
  isActive: true,
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);
const numOrNull = (v?: number) => (v === undefined || Number.isNaN(v) ? null : v);

const moduleTypeLabel = (v: string) =>
  ORIENTATION_MODULE_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;
const contentTypeLabel = (v: string) =>
  ORIENTATION_CONTENT_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;

/**
 * Modules, and the content items inside whichever module is selected.
 *
 * The two are separate collections on the API — content items hang off a module, not off the
 * programme — so rather than nesting one table inside another, the module list drives a selection
 * and the content table below follows it. Selecting is what the count column does: it is the only
 * route to a module's contents, so it reads as a link rather than a number.
 */
export function ProgramModulesPanel({ programId }: { programId: string }) {
  const [selected, setSelected] = useState<OrientationModule | null>(null);

  const modulesKey = ['hr', 'orientation-programs', programId, 'modules'];

  return (
    <div className="space-y-6">
      <ResourceCollectionTab<OrientationModule, ModuleForm>
        parentId={programId}
        title="modules"
        singular="module"
        queryKey={modulesKey}
        invalidateKeys={[['hr', 'orientation-programs', programId]]}
        dialogHint="A section of the programme. Content items go inside it."
        emptyDescription="No modules yet. Add one, then put its content inside."
        list={() => orientationProgramService.getModules(programId)}
        create={(id, values) =>
          orientationProgramService.addModule(id, {
            programId: id,
            ...values,
            description: blank(values.description),
            estimatedDurationMinutes: numOrNull(values.estimatedDurationMinutes),
          })
        }
        update={(_p, moduleId, values) =>
          orientationProgramService.updateModule(moduleId, {
            id: moduleId,
            ...values,
            description: blank(values.description),
            estimatedDurationMinutes: numOrNull(values.estimatedDurationMinutes),
          })
        }
        remove={(_p, moduleId) =>
          orientationProgramService.removeModule(moduleId).then((r) => {
            // Removing the module whose content is on screen would otherwise leave the panel below
            // pointed at something that no longer exists.
            setSelected((cur) => (cur?.id === moduleId ? null : cur));
            return r;
          })
        }
        getId={(m) => m.id}
        columns={[
          { header: '#', cell: (m) => m.sequenceOrder, className: 'w-[60px]' },
          {
            header: 'Module',
            cell: (m) => (
              <div>
                <span className="font-medium">{m.title}</span>
                {m.isOptional && (
                  <Badge variant="outline" className="ml-2 text-[10px]">
                    Optional
                  </Badge>
                )}
                {m.description && (
                  <div className="text-muted-foreground mt-0.5 line-clamp-1 text-xs">
                    {m.description}
                  </div>
                )}
              </div>
            ),
          },
          { header: 'Type', cell: (m) => moduleTypeLabel(m.moduleType) },
          {
            header: 'Duration',
            cell: (m) => (m.estimatedDurationMinutes ? `${m.estimatedDurationMinutes} min` : '—'),
          },
          {
            header: 'Content',
            cell: (m) => (
              <Button
                variant="link"
                size="sm"
                className="h-auto p-0"
                onClick={() => setSelected(m)}
              >
                {m.contentItemCount} {m.contentItemCount === 1 ? 'item' : 'items'}
              </Button>
            ),
          },
          { header: 'Status', cell: (m) => <StatusBadge active={m.isActive} /> },
        ]}
        schema={moduleSchema as any}
        emptyForm={emptyModule}
        toForm={(m) => ({
          title: m.title,
          description: m.description ?? '',
          sequenceOrder: m.sequenceOrder,
          moduleType: m.moduleType,
          estimatedDurationMinutes: m.estimatedDurationMinutes ?? undefined,
          isSequentiallyRequired: m.isSequentiallyRequired,
          isOptional: m.isOptional,
          isActive: m.isActive,
        })}
        renderFields={(form) => (
          <>
            <TextField form={form} name="title" label="Title" required />
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <FieldRow>
              <SelectField
                form={form}
                name="moduleType"
                label="Type"
                required
                options={ORIENTATION_MODULE_TYPE_OPTIONS}
              />
              <NumberField form={form} name="sequenceOrder" label="Sequence" required />
            </FieldRow>
            <NumberField
              form={form}
              name="estimatedDurationMinutes"
              label="Estimated duration (minutes)"
            />
            <SwitchField
              form={form}
              name="isSequentiallyRequired"
              label="Must be done in order"
              description="Content inside this module has to be worked through in sequence."
            />
            <FieldRow>
              <SwitchField
                form={form}
                name="isOptional"
                label="Optional"
                description="Not counted towards completion."
              />
              <SwitchField form={form} name="isActive" label="Active" />
            </FieldRow>
          </>
        )}
      />

      {selected && (
        <div className="space-y-3 rounded-md border p-4">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <h3 className="font-medium">Content in “{selected.title}”</h3>
              <p className="text-muted-foreground text-sm">
                {selected.isSequentiallyRequired
                  ? 'Worked through in sequence order.'
                  : 'Can be worked through in any order.'}
              </p>
            </div>
            <Button variant="ghost" size="sm" onClick={() => setSelected(null)}>
              Close
            </Button>
          </div>

          <ResourceCollectionTab<OrientationContentItem, ContentForm>
            parentId={selected.id}
            title="content items"
            singular="content item"
            queryKey={['hr', 'orientation-modules', selected.id, 'content-items']}
            invalidateKeys={[modulesKey]}
            dialogHint="A video, document, link or page the participant works through."
            emptyDescription="Nothing in this module yet."
            list={(moduleId) => orientationProgramService.getContentItems(moduleId)}
            create={(moduleId, values) =>
              orientationProgramService.addContentItem(moduleId, {
                moduleId,
                ...values,
                description: blank(values.description),
                resourceUrl: blank(values.resourceUrl),
                originalFileName: blank(values.originalFileName),
                mediaDurationSeconds: numOrNull(values.mediaDurationSeconds),
                fileSizeBytes: null,
              })
            }
            update={(_m, itemId, values) =>
              orientationProgramService.updateContentItem(itemId, {
                id: itemId,
                ...values,
                description: blank(values.description),
                resourceUrl: blank(values.resourceUrl),
                originalFileName: blank(values.originalFileName),
                mediaDurationSeconds: numOrNull(values.mediaDurationSeconds),
                fileSizeBytes: null,
              })
            }
            remove={(_m, itemId) => orientationProgramService.removeContentItem(itemId)}
            getId={(c) => c.id}
            columns={[
              { header: '#', cell: (c) => c.sequenceOrder, className: 'w-[60px]' },
              { header: 'Title', cell: (c) => <span className="font-medium">{c.title}</span> },
              { header: 'Type', cell: (c) => contentTypeLabel(c.contentType) },
              {
                header: 'Resource',
                cell: (c) =>
                  c.resourceUrl ? (
                    <a
                      href={c.resourceUrl}
                      target="_blank"
                      rel="noreferrer"
                      className="text-primary max-w-[220px] truncate hover:underline"
                    >
                      {c.originalFileName || c.resourceUrl}
                    </a>
                  ) : (
                    '—'
                  ),
              },
              {
                header: 'Required',
                cell: (c) =>
                  c.isRequired ? <Badge variant="secondary">Required</Badge> : <span>Optional</span>,
              },
              { header: 'Status', cell: (c) => <StatusBadge active={c.isActive} /> },
            ]}
            schema={contentSchema as any}
            emptyForm={emptyContent}
            toForm={(c) => ({
              title: c.title,
              description: c.description ?? '',
              contentType: c.contentType,
              resourceUrl: c.resourceUrl ?? '',
              originalFileName: c.originalFileName ?? '',
              mediaDurationSeconds: c.mediaDurationSeconds ?? undefined,
              sequenceOrder: c.sequenceOrder,
              isRequired: c.isRequired,
              isActive: c.isActive,
            })}
            renderFields={(form) => (
              <>
                <TextField form={form} name="title" label="Title" required />
                <TextareaField form={form} name="description" label="Description" rows={2} />
                <FieldRow>
                  <SelectField
                    form={form}
                    name="contentType"
                    label="Content type"
                    required
                    options={ORIENTATION_CONTENT_TYPE_OPTIONS}
                  />
                  <NumberField form={form} name="sequenceOrder" label="Sequence" required />
                </FieldRow>
                <TextField
                  form={form}
                  name="resourceUrl"
                  label="Resource URL"
                  placeholder="https://…"
                />
                <FieldRow>
                  <TextField form={form} name="originalFileName" label="Display file name" />
                  <NumberField
                    form={form}
                    name="mediaDurationSeconds"
                    label="Media length (seconds)"
                  />
                </FieldRow>
                <FieldRow>
                  <SwitchField
                    form={form}
                    name="isRequired"
                    label="Required"
                    description="Counts towards completing the programme."
                  />
                  <SwitchField form={form} name="isActive" label="Active" />
                </FieldRow>
              </>
            )}
          />
        </div>
      )}
    </div>
  );
}
