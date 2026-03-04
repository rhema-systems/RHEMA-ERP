'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowDown, ArrowUp, Pencil, Plus, Trash2, Wand2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { ehcAdminService, type EhcServiceRequestTypeAdmin, type UpsertEhcServiceRequestTypeAdmin } from '@/services/ehcAdminService';

const defaultFormDefinition = JSON.stringify(
  {
    version: 1,
    fields: [
      { key: 'details', label: 'Details', type: 'textarea', required: true },
    ],
  },
  null,
  2,
);

type FieldType = 'text' | 'textarea' | 'number' | 'select' | 'checkbox' | 'date';
type EditorMode = 'builder' | 'json';

interface FormField {
  key: string;
  label: string;
  type: FieldType;
  required?: boolean;
  options?: string[];
  placeholder?: string;
  helpText?: string;
}

interface FormDefinition {
  version?: number;
  fields: FormField[];
}

const parseFormDefinition = (raw: string): { def: FormDefinition | null; error: string | null } => {
  try {
    const parsed = JSON.parse(raw);
    if (!parsed || typeof parsed !== 'object') return { def: null, error: 'Form definition must be a JSON object.' };
    if (!Array.isArray(parsed.fields)) return { def: null, error: "Form definition must contain a 'fields' array." };

    const def: FormDefinition = {
      version: typeof parsed.version === 'number' ? parsed.version : 1,
      fields: parsed.fields
        .filter((f: any) => f && typeof f === 'object')
        .map((f: any) => ({
          key: String(f.key ?? '').trim(),
          label: String(f.label ?? '').trim(),
          type: String(f.type ?? 'text').trim().toLowerCase() as FieldType,
          required: Boolean(f.required),
          options: Array.isArray(f.options) ? f.options.map((o: any) => String(o)) : undefined,
          placeholder: typeof f.placeholder === 'string' ? f.placeholder : undefined,
          helpText: typeof f.helpText === 'string' ? f.helpText : undefined,
        })) as FormField[],
    };

    return { def, error: null };
  } catch {
    return { def: null, error: 'Invalid JSON.' };
  }
};

const stringifyFormDefinition = (fields: FormField[]): string => {
  const def: FormDefinition = {
    version: 1,
    fields: fields.map((f) => {
      const out: any = {
        key: (f.key || '').trim(),
        label: (f.label || '').trim(),
        type: f.type,
        required: Boolean(f.required),
      };
      if (f.type === 'select') out.options = (f.options || []).map((o) => String(o));
      if (f.placeholder) out.placeholder = f.placeholder;
      if (f.helpText) out.helpText = f.helpText;
      return out;
    }),
  };
  return JSON.stringify(def, null, 2);
};

const makeUniqueKey = (existingKeys: string[], base: string) => {
  const set = new Set(existingKeys.map((k) => k.toLowerCase()));
  let i = 1;
  let k = base;
  while (set.has(k.toLowerCase())) {
    i += 1;
    k = `${base}${i}`;
  }
  return k;
};

export default function HelpdeskServiceCatalogAdminPage() {
  const qc = useQueryClient();
  const { data, isLoading, error } = useQuery({
    queryKey: ['ehc', 'admin', 'service-catalog', 'request-types'],
    queryFn: () => ehcAdminService.listServiceRequestTypes(),
  });

  const [editing, setEditing] = useState<EhcServiceRequestTypeAdmin | null>(null);
  const [mode, setMode] = useState<EditorMode>('builder');
  const [builderError, setBuilderError] = useState<string | null>(null);
  const [fields, setFields] = useState<FormField[]>([{ key: 'details', label: 'Details', type: 'textarea', required: true }]);
  const [form, setForm] = useState<UpsertEhcServiceRequestTypeAdmin>({
    code: '',
    name: '',
    description: '',
    isActive: true,
    workflowName: '',
    formDefinitionJson: defaultFormDefinition,
  });

  const items = useMemo(() => data || [], [data]);

  const save = useMutation({
    mutationFn: async () => {
      const payload: UpsertEhcServiceRequestTypeAdmin = {
        code: form.code.trim(),
        name: form.name.trim(),
        description: form.description?.trim() || null,
        isActive: Boolean(form.isActive),
        workflowName: form.workflowName?.trim() || null,
        formDefinitionJson: form.formDefinitionJson,
      };
      if (editing) return ehcAdminService.updateServiceRequestType(editing.id, payload);
      return ehcAdminService.createServiceRequestType(payload);
    },
    onSuccess: async () => {
      setEditing(null);
      setMode('builder');
      setBuilderError(null);
      setFields([{ key: 'details', label: 'Details', type: 'textarea', required: true }]);
      setForm({
        code: '',
        name: '',
        description: '',
        isActive: true,
        workflowName: '',
        formDefinitionJson: defaultFormDefinition,
      });
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'service-catalog', 'request-types'] });
    },
  });

  const del = useMutation({
    mutationFn: (id: string) => ehcAdminService.deleteServiceRequestType(id),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'service-catalog', 'request-types'] });
    },
  });

  const startEdit = (c: EhcServiceRequestTypeAdmin) => {
    setEditing(c);
    const raw = c.formDefinitionJson || defaultFormDefinition;
    const parsed = parseFormDefinition(raw);
    setFields(parsed.def?.fields?.length ? parsed.def.fields : [{ key: 'details', label: 'Details', type: 'textarea', required: true }]);
    setBuilderError(parsed.error);
    setForm({
      code: c.code,
      name: c.name,
      description: c.description || '',
      isActive: c.isActive,
      workflowName: c.workflowName || '',
      formDefinitionJson: raw,
    });
  };

  const formatJson = () => {
    try {
      const parsed = JSON.parse(form.formDefinitionJson);
      setForm((f) => ({ ...f, formDefinitionJson: JSON.stringify(parsed, null, 2) }));
    } catch {
      // ignore; backend will validate on save
    }
  };

  const useTemplate = () => {
    const parsed = parseFormDefinition(defaultFormDefinition);
    setFields(parsed.def?.fields?.length ? parsed.def.fields : [{ key: 'details', label: 'Details', type: 'textarea', required: true }]);
    setBuilderError(parsed.error);
    setForm((f) => ({ ...f, formDefinitionJson: defaultFormDefinition }));
  };

  const syncFromBuilder = (nextFields: FormField[]) => {
    setFields(nextFields);
    setBuilderError(null);
    setForm((f) => ({ ...f, formDefinitionJson: stringifyFormDefinition(nextFields) }));
  };

  const switchToBuilder = () => {
    const parsed = parseFormDefinition(form.formDefinitionJson);
    if (!parsed.def)
    {
      setBuilderError(parsed.error || 'Invalid form definition.');
      setMode('json');
      return;
    }
    setFields(parsed.def.fields?.length ? parsed.def.fields : [{ key: 'details', label: 'Details', type: 'textarea', required: true }]);
    setBuilderError(null);
    setMode('builder');
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Service Catalog</h1>
        <p className="text-slate-600 mt-1">Configure request types with dynamic forms and optional workflow approvals.</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{editing ? 'Edit request type' : 'Create request type'}</CardTitle>
          <CardDescription>Codes must be unique per tenant. If Workflow Name is set, it must match an active workflow definition name for entity type ServiceRequest.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Code</Label>
              <Input value={form.code} onChange={(e) => setForm((f) => ({ ...f, code: e.target.value }))} placeholder="e.g. IT_ACCESS" />
            </div>
            <div className="space-y-2">
              <Label>Name</Label>
              <Input value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} placeholder="e.g. System Access Request" />
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Workflow Name (optional)</Label>
              <Input value={form.workflowName || ''} onChange={(e) => setForm((f) => ({ ...f, workflowName: e.target.value }))} placeholder="e.g. ServiceRequestApproval" />
            </div>
            <div className="space-y-2">
              <Label>Status</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.isActive ? 'active' : 'inactive'}
                onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.value === 'active' }))}
              >
                <option value="active">Active</option>
                <option value="inactive">Inactive</option>
              </select>
            </div>
          </div>

          <div className="space-y-2">
            <Label>Description</Label>
            <Textarea value={form.description || ''} onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))} rows={3} />
          </div>

          <div className="space-y-2">
            <div className="flex items-center justify-between gap-2">
              <Label>Form Definition</Label>
              <div className="flex items-center gap-2">
                <Button
                  type="button"
                  variant={mode === 'builder' ? 'default' : 'outline'}
                  size="sm"
                  onClick={switchToBuilder}
                >
                  Builder
                </Button>
                <Button
                  type="button"
                  variant={mode === 'json' ? 'default' : 'outline'}
                  size="sm"
                  onClick={() => setMode('json')}
                >
                  JSON
                </Button>
                <Button type="button" variant="outline" size="sm" onClick={useTemplate}>
                  <Wand2 className="w-4 h-4 mr-2" />
                  Template
                </Button>
                <Button type="button" variant="outline" size="sm" onClick={formatJson} disabled={mode !== 'json'}>
                  Format
                </Button>
              </div>
            </div>

            {mode === 'builder' ? (
              <div className="space-y-3 rounded-md border p-3">
                {builderError ? <div className="text-sm text-red-600">{builderError}</div> : null}

                <div className="flex items-center justify-between">
                  <div className="text-sm text-slate-600">Fields</div>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => {
                      const existing = fields.map((f) => f.key);
                      const key = makeUniqueKey(existing, 'field');
                      syncFromBuilder([
                        ...fields,
                        { key, label: 'New field', type: 'text', required: false },
                      ]);
                    }}
                  >
                    <Plus className="w-4 h-4 mr-2" /> Add field
                  </Button>
                </div>

                <div className="space-y-3">
                  {fields.map((f, idx) => (
                    <div key={`${f.key}-${idx}`} className="rounded-md border p-3 space-y-3">
                      <div className="flex items-center justify-between gap-2">
                        <div className="font-medium text-sm">Field {idx + 1}</div>
                        <div className="flex items-center gap-2">
                          <Button
                            type="button"
                            variant="outline"
                            size="sm"
                            disabled={idx === 0}
                            onClick={() => {
                              const next = [...fields];
                              const tmp = next[idx - 1];
                              next[idx - 1] = next[idx];
                              next[idx] = tmp;
                              syncFromBuilder(next);
                            }}
                          >
                            <ArrowUp className="w-4 h-4" />
                          </Button>
                          <Button
                            type="button"
                            variant="outline"
                            size="sm"
                            disabled={idx === fields.length - 1}
                            onClick={() => {
                              const next = [...fields];
                              const tmp = next[idx + 1];
                              next[idx + 1] = next[idx];
                              next[idx] = tmp;
                              syncFromBuilder(next);
                            }}
                          >
                            <ArrowDown className="w-4 h-4" />
                          </Button>
                          <Button
                            type="button"
                            variant="destructive"
                            size="sm"
                            onClick={() => {
                              const next = fields.filter((_, i) => i !== idx);
                              syncFromBuilder(next.length ? next : [{ key: 'details', label: 'Details', type: 'textarea', required: true }]);
                            }}
                          >
                            <Trash2 className="w-4 h-4" />
                          </Button>
                        </div>
                      </div>

                      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                        <div className="space-y-1">
                          <Label>Key</Label>
                          <Input
                            value={f.key}
                            onChange={(e) => {
                              const next = [...fields];
                              next[idx] = { ...f, key: e.target.value };
                              syncFromBuilder(next);
                            }}
                            placeholder="e.g. department"
                          />
                        </div>
                        <div className="space-y-1 sm:col-span-2">
                          <Label>Label</Label>
                          <Input
                            value={f.label}
                            onChange={(e) => {
                              const next = [...fields];
                              next[idx] = { ...f, label: e.target.value };
                              syncFromBuilder(next);
                            }}
                            placeholder="e.g. Department"
                          />
                        </div>
                      </div>

                      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                        <div className="space-y-1">
                          <Label>Type</Label>
                          <select
                            className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                            value={f.type}
                            onChange={(e) => {
                              const t = e.target.value as FieldType;
                              const next = [...fields];
                              next[idx] = {
                                ...f,
                                type: t,
                                options: t === 'select' ? (f.options?.length ? f.options : ['Option 1']) : undefined,
                              };
                              syncFromBuilder(next);
                            }}
                          >
                            <option value="text">Text</option>
                            <option value="textarea">Textarea</option>
                            <option value="number">Number</option>
                            <option value="select">Select</option>
                            <option value="checkbox">Checkbox</option>
                            <option value="date">Date</option>
                          </select>
                        </div>
                        <div className="space-y-1">
                          <Label>Required</Label>
                          <div className="h-10 flex items-center">
                            <Switch
                              checked={Boolean(f.required)}
                              onCheckedChange={(checked) => {
                                const next = [...fields];
                                next[idx] = { ...f, required: checked };
                                syncFromBuilder(next);
                              }}
                            />
                          </div>
                        </div>
                        <div className="space-y-1">
                          <Label>Placeholder (optional)</Label>
                          <Input
                            value={f.placeholder || ''}
                            onChange={(e) => {
                              const next = [...fields];
                              next[idx] = { ...f, placeholder: e.target.value || undefined };
                              syncFromBuilder(next);
                            }}
                            placeholder="Hint shown in the field"
                          />
                        </div>
                      </div>

                      {f.type === 'select' ? (
                        <div className="space-y-1">
                          <Label>Options (one per line)</Label>
                          <Textarea
                            value={(f.options || []).join('\n')}
                            onChange={(e) => {
                              const next = [...fields];
                              const options = e.target.value
                                .split('\n')
                                .map((x) => x.trim())
                                .filter(Boolean);
                              next[idx] = { ...f, options };
                              syncFromBuilder(next);
                            }}
                            rows={4}
                            className="font-mono text-xs"
                          />
                        </div>
                      ) : null}

                      <div className="space-y-1">
                        <Label>Help text (optional)</Label>
                        <Input
                          value={f.helpText || ''}
                          onChange={(e) => {
                            const next = [...fields];
                            next[idx] = { ...f, helpText: e.target.value || undefined };
                            syncFromBuilder(next);
                          }}
                          placeholder="Short guidance shown under the field"
                        />
                      </div>

                      <div className="text-xs text-slate-500">
                        Keys must be unique. Supported types: text, textarea, number, select, checkbox, date.
                      </div>
                    </div>
                  ))}

                  {!fields.length ? <div className="text-sm text-slate-600">No fields configured.</div> : null}
                </div>
              </div>
            ) : (
              <>
                <Textarea
                  value={form.formDefinitionJson}
                  onChange={(e) => setForm((f) => ({ ...f, formDefinitionJson: e.target.value }))}
                  rows={10}
                  className="font-mono text-xs"
                />
                <div className="text-xs text-slate-500">Required: a JSON object containing a <code>fields</code> array.</div>
              </>
            )}
          </div>

          <div className="flex items-center gap-2">
            <Button onClick={() => save.mutate()} disabled={save.isPending || !form.code.trim() || !form.name.trim()}>
              {save.isPending ? 'Saving…' : editing ? 'Save changes' : 'Create'}
            </Button>
            {editing ? (
              <Button variant="outline" onClick={() => setEditing(null)}>
                Cancel
              </Button>
            ) : null}
          </div>
          {save.isError ? <div className="text-sm text-red-600">Failed to save. Check JSON schema and try again.</div> : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Request types</CardTitle>
          <CardDescription>{isLoading ? 'Loading…' : `${items.length} item(s)`}</CardDescription>
        </CardHeader>
        <CardContent>
          {error ? (
            <div className="text-sm text-red-600">Failed to load.</div>
          ) : (
            <div className="space-y-2">
              {items.map((c) => (
                <div key={c.id} className="flex items-center justify-between rounded-md border p-3">
                  <div>
                    <div className="font-medium flex items-center gap-2">
                      <span>{c.name}</span>
                      <span className="text-xs text-slate-500">({c.code})</span>
                      {!c.isActive ? <span className="text-xs text-slate-500">Inactive</span> : null}
                    </div>
                    {c.description ? <div className="text-sm text-slate-600">{c.description}</div> : null}
                    {c.workflowName ? <div className="text-xs text-slate-500">Workflow: {c.workflowName}</div> : null}
                  </div>
                  <div className="flex items-center gap-2">
                    <Button variant="outline" size="sm" onClick={() => startEdit(c)}>
                      <Pencil className="w-4 h-4 mr-1" /> Edit
                    </Button>
                    <Button variant="destructive" size="sm" onClick={() => del.mutate(c.id)} disabled={del.isPending}>
                      <Trash2 className="w-4 h-4 mr-1" /> Delete
                    </Button>
                  </div>
                </div>
              ))}

              {!items.length ? <div className="text-sm text-slate-600">No request types yet.</div> : null}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
