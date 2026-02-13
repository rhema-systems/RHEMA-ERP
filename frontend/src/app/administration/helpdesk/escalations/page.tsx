'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Pencil, Plus, Save, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { ehcAdminService, type CreateEhcEscalationPolicyAdmin, type EhcEscalationPolicyAdmin, type EhcEscalationTrigger } from '@/services/ehcAdminService';
import { ehcInternalTicketService } from '@/services/ehcInternalTicketService';
import type { EhcTicketPriority, EhcTicketType } from '@/services/ehcTicketService';

type LevelForm = {
  delayMinutes: number;
  notifyRolesCsv: string;
  notifyAssignedAgent: boolean;
  notifyUserId: string;
  addInternalComment: boolean;
  reassignToRole: string;
  reassignToUserId: string;
};

const defaultLevelForm: LevelForm = {
  delayMinutes: 0,
  notifyRolesCsv: 'HelpdeskSupervisor',
  notifyAssignedAgent: true,
  notifyUserId: '',
  addInternalComment: true,
  reassignToRole: '',
  reassignToUserId: '',
};

const triggerOptions: Array<{ value: EhcEscalationTrigger; label: string; hint: string }> = [
  { value: 'FirstResponseDueSoon', label: 'First response due soon', hint: 'Triggers before first response SLA due time.' },
  { value: 'FirstResponseBreached', label: 'First response breached', hint: 'Triggers when first response SLA is breached.' },
  { value: 'ResolutionDueSoon', label: 'Resolution due soon', hint: 'Triggers before resolution SLA due time.' },
  { value: 'ResolutionBreached', label: 'Resolution breached', hint: 'Triggers when resolution SLA is breached.' },
];

function parseCsv(value: string): string[] {
  return (value || '')
    .split(',')
    .map((x) => x.trim())
    .filter(Boolean);
}

export default function HelpdeskEscalationsAdminPage() {
  const qc = useQueryClient();

  const { data: categories } = useQuery({
    queryKey: ['ehc', 'admin', 'categories'],
    queryFn: () => ehcAdminService.listCategories(),
  });

  const { data: departments } = useQuery({
    queryKey: ['ehc', 'internal', 'lookups', 'departments'],
    queryFn: () => ehcInternalTicketService.listDepartments(),
  });

  const { data: agents } = useQuery({
    queryKey: ['ehc', 'internal', 'lookups', 'agents'],
    queryFn: () => ehcInternalTicketService.listAgents(),
  });

  const { data: policies, isLoading, error } = useQuery({
    queryKey: ['ehc', 'admin', 'escalationPolicies'],
    queryFn: () => ehcAdminService.listEscalationPolicies(),
  });

  const categoryLabelById = useMemo(() => {
    const items = categories || [];
    const byId = new Map(items.map((c) => [c.id, c]));
    const cache = new Map<string, string>();

    const build = (id: string): string => {
      const cached = cache.get(id);
      if (cached) return cached;
      const c = byId.get(id);
      if (!c) return id;
      if (!c.parentCategoryId) {
        cache.set(id, c.name);
        return c.name;
      }
      const parent = build(c.parentCategoryId);
      const label = `${parent} / ${c.name}`;
      cache.set(id, label);
      return label;
    };

    for (const c of items) build(c.id);
    return cache;
  }, [categories]);

  const [editing, setEditing] = useState<EhcEscalationPolicyAdmin | null>(null);
  const [levels, setLevels] = useState<LevelForm[]>([{ ...defaultLevelForm }]);
  const [form, setForm] = useState<Omit<CreateEhcEscalationPolicyAdmin, 'levels'>>({
    name: '',
    isActive: true,
    priority: 0,
    trigger: 'FirstResponseBreached',
    dueSoonMinutes: 15,
    ticketType: null,
    ticketPriority: null,
    categoryId: null,
    subcategoryId: null,
    departmentId: null,
  });

  const topLevelCategories = useMemo(() => (categories || []).filter((c) => !c.parentCategoryId), [categories]);
  const subcategoryOptions = useMemo(() => {
    const items = categories || [];
    if (!form.categoryId) return [];
    return items
      .filter((c) => c.parentCategoryId === form.categoryId)
      .map((c) => ({ id: c.id, label: c.name }))
      .sort((a, b) => a.label.localeCompare(b.label));
  }, [categories, form.categoryId]);

  const save = useMutation({
    mutationFn: async () => {
      const payload: CreateEhcEscalationPolicyAdmin = {
        ...form,
        name: form.name.trim(),
        dueSoonMinutes: Number(form.dueSoonMinutes) || 15,
        priority: Number(form.priority) || 0,
        categoryId: form.categoryId || null,
        subcategoryId: form.subcategoryId || null,
        departmentId: form.departmentId || null,
        levels: levels.map((l, idx) => ({
          level: idx + 1,
          delayMinutes: Number(l.delayMinutes) || 0,
          notifyRoles: parseCsv(l.notifyRolesCsv),
          notifyAssignedAgent: !!l.notifyAssignedAgent,
          notifyUserId: l.notifyUserId || null,
          addInternalComment: !!l.addInternalComment,
          reassignToRole: l.reassignToRole?.trim() ? l.reassignToRole.trim() : null,
          reassignToUserId: l.reassignToUserId || null,
        })),
      };

      if (editing) {
        await ehcAdminService.updateEscalationPolicy(editing.id, payload);
        return;
      }
      await ehcAdminService.createEscalationPolicy(payload);
    },
    onSuccess: async () => {
      setEditing(null);
      setForm({
        name: '',
        isActive: true,
        priority: 0,
        trigger: 'FirstResponseBreached',
        dueSoonMinutes: 15,
        ticketType: null,
        ticketPriority: null,
        categoryId: null,
        subcategoryId: null,
        departmentId: null,
      });
      setLevels([{ ...defaultLevelForm }]);
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'escalationPolicies'] });
    },
  });

  const del = useMutation({
    mutationFn: (id: string) => ehcAdminService.deleteEscalationPolicy(id),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'escalationPolicies'] });
    },
  });

  const startEdit = (p: EhcEscalationPolicyAdmin) => {
    setEditing(p);
    setForm({
      name: p.name,
      isActive: p.isActive,
      priority: p.priority,
      trigger: p.trigger,
      dueSoonMinutes: p.dueSoonMinutes,
      ticketType: (p.ticketType as any) || null,
      ticketPriority: (p.ticketPriority as any) || null,
      categoryId: p.categoryId || null,
      subcategoryId: p.subcategoryId || null,
      departmentId: p.departmentId || null,
    });

    const ls = (p.levels || []).sort((a, b) => a.level - b.level);
    setLevels(
      ls.length > 0
        ? ls.map((l) => ({
            delayMinutes: l.delayMinutes,
            notifyRolesCsv: (l.notifyRoles || []).join(', '),
            notifyAssignedAgent: l.notifyAssignedAgent,
            notifyUserId: l.notifyUserId || '',
            addInternalComment: l.addInternalComment,
            reassignToRole: l.reassignToRole || '',
            reassignToUserId: l.reassignToUserId || '',
          }))
        : [{ ...defaultLevelForm }]
    );
  };

  const triggerMeta = triggerOptions.find((t) => t.value === form.trigger);
  const showDueSoonMinutes = form.trigger === 'FirstResponseDueSoon' || form.trigger === 'ResolutionDueSoon';

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Escalation Policies</h1>
        <p className="text-slate-600 mt-1">Configure multi-level escalations for SLA warnings/breaches.</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{editing ? 'Edit escalation policy' : 'Create escalation policy'}</CardTitle>
          <CardDescription>
            {triggerMeta ? (
              <span>
                Trigger: <span className="font-medium">{triggerMeta.label}</span> — {triggerMeta.hint}
              </span>
            ) : (
              'Choose a trigger and define one or more levels.'
            )}
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Name</Label>
              <Input value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} placeholder="e.g. Critical breach escalation" />
            </div>
            <div className="space-y-2">
              <Label>Active</Label>
              <div className="h-10 flex items-center gap-3">
                <Switch checked={!!form.isActive} onCheckedChange={(v) => setForm((f) => ({ ...f, isActive: !!v }))} />
                <span className="text-sm text-slate-600">{form.isActive ? 'Enabled' : 'Disabled'}</span>
              </div>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div className="space-y-2">
              <Label>Trigger</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.trigger}
                onChange={(e) => setForm((f) => ({ ...f, trigger: e.target.value as EhcEscalationTrigger }))}
              >
                {triggerOptions.map((t) => (
                  <option key={t.value} value={t.value}>
                    {t.label}
                  </option>
                ))}
              </select>
            </div>
            <div className="space-y-2">
              <Label>Priority</Label>
              <Input type="number" value={String(form.priority)} onChange={(e) => setForm((f) => ({ ...f, priority: Number(e.target.value) }))} />
            </div>
            <div className="space-y-2">
              <Label>Due soon (minutes)</Label>
              <Input
                type="number"
                disabled={!showDueSoonMinutes}
                value={String(form.dueSoonMinutes)}
                onChange={(e) => setForm((f) => ({ ...f, dueSoonMinutes: Number(e.target.value) }))}
                min={1}
              />
              {!showDueSoonMinutes ? <div className="text-xs text-slate-500">Only used for “due soon” triggers.</div> : null}
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div className="space-y-2">
              <Label>Ticket type</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.ticketType || ''}
                onChange={(e) => setForm((f) => ({ ...f, ticketType: (e.target.value || null) as EhcTicketType | null }))}
              >
                <option value="">Any</option>
                <option value="Enquiry">Enquiry</option>
                <option value="Complaint">Complaint</option>
                <option value="Helpdesk">Helpdesk</option>
              </select>
            </div>
            <div className="space-y-2">
              <Label>Priority</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.ticketPriority || ''}
                onChange={(e) => setForm((f) => ({ ...f, ticketPriority: (e.target.value || null) as EhcTicketPriority | null }))}
              >
                <option value="">Any</option>
                <option value="Low">Low</option>
                <option value="Medium">Medium</option>
                <option value="High">High</option>
                <option value="Critical">Critical</option>
              </select>
            </div>
            <div className="space-y-2">
              <Label>Department</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.departmentId || ''}
                onChange={(e) => setForm((f) => ({ ...f, departmentId: e.target.value || null }))}
              >
                <option value="">Any</option>
                {(departments || []).map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Category</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.categoryId || ''}
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    categoryId: e.target.value || null,
                    subcategoryId: null,
                  }))
                }
              >
                <option value="">Any</option>
                {topLevelCategories.map((c) => (
                  <option key={c.id} value={c.id}>
                    {categoryLabelById.get(c.id) || c.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="space-y-2">
              <Label>Subcategory</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.subcategoryId || ''}
                onChange={(e) => setForm((f) => ({ ...f, subcategoryId: e.target.value || null }))}
                disabled={!form.categoryId}
              >
                <option value="">Any</option>
                {subcategoryOptions.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.label}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <div>
                <div className="font-medium">Levels</div>
                <div className="text-xs text-slate-500">Levels execute in order; each may notify/reassign.</div>
              </div>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setLevels((ls) => [...ls, { ...defaultLevelForm }])}
              >
                <Plus className="h-4 w-4 mr-2" />
                Add level
              </Button>
            </div>

            <div className="space-y-4">
              {levels.map((l, idx) => (
                <Card key={idx}>
                  <CardHeader className="pb-2">
                    <div className="flex items-center justify-between gap-2">
                      <CardTitle className="text-sm">Level {idx + 1}</CardTitle>
                      {levels.length > 1 ? (
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => setLevels((ls) => ls.filter((_, i) => i !== idx))}
                          title="Remove level"
                        >
                          <Trash2 className="h-4 w-4 text-red-600" />
                        </Button>
                      ) : null}
                    </div>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                      <div className="space-y-2">
                        <Label>Delay (minutes)</Label>
                        <Input
                          type="number"
                          min={0}
                          value={String(l.delayMinutes)}
                          onChange={(e) =>
                            setLevels((ls) => ls.map((x, i) => (i === idx ? { ...x, delayMinutes: Number(e.target.value) } : x)))
                          }
                        />
                      </div>
                      <div className="space-y-2">
                        <Label>Notify roles (CSV)</Label>
                        <Input
                          value={l.notifyRolesCsv}
                          onChange={(e) =>
                            setLevels((ls) => ls.map((x, i) => (i === idx ? { ...x, notifyRolesCsv: e.target.value } : x)))
                          }
                          placeholder="HelpdeskSupervisor, HelpdeskManager"
                        />
                      </div>
                      <div className="space-y-2">
                        <Label>Notify assigned agent</Label>
                        <div className="h-10 flex items-center gap-3">
                          <Switch
                            checked={!!l.notifyAssignedAgent}
                            onCheckedChange={(v) =>
                              setLevels((ls) => ls.map((x, i) => (i === idx ? { ...x, notifyAssignedAgent: !!v } : x)))
                            }
                          />
                          <span className="text-sm text-slate-600">{l.notifyAssignedAgent ? 'Yes' : 'No'}</span>
                        </div>
                      </div>
                    </div>

                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                      <div className="space-y-2">
                        <Label>Extra notify user (optional)</Label>
                        <select
                          className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                          value={l.notifyUserId || ''}
                          onChange={(e) =>
                            setLevels((ls) => ls.map((x, i) => (i === idx ? { ...x, notifyUserId: e.target.value } : x)))
                          }
                        >
                          <option value="">None</option>
                          {(agents || []).map((a) => (
                            <option key={a.id} value={a.id}>
                              {a.name}
                            </option>
                          ))}
                        </select>
                      </div>
                      <div className="space-y-2">
                        <Label>Add internal comment</Label>
                        <div className="h-10 flex items-center gap-3">
                          <Switch
                            checked={!!l.addInternalComment}
                            onCheckedChange={(v) =>
                              setLevels((ls) => ls.map((x, i) => (i === idx ? { ...x, addInternalComment: !!v } : x)))
                            }
                          />
                          <span className="text-sm text-slate-600">{l.addInternalComment ? 'Yes' : 'No'}</span>
                        </div>
                      </div>
                    </div>

                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                      <div className="space-y-2">
                        <Label>Reassign to role (optional)</Label>
                        <Input
                          value={l.reassignToRole}
                          onChange={(e) =>
                            setLevels((ls) => ls.map((x, i) => (i === idx ? { ...x, reassignToRole: e.target.value } : x)))
                          }
                          placeholder="HelpdeskManager"
                        />
                      </div>
                      <div className="space-y-2">
                        <Label>Reassign to user (optional)</Label>
                        <select
                          className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                          value={l.reassignToUserId || ''}
                          onChange={(e) =>
                            setLevels((ls) => ls.map((x, i) => (i === idx ? { ...x, reassignToUserId: e.target.value } : x)))
                          }
                        >
                          <option value="">None</option>
                          {(agents || []).map((a) => (
                            <option key={a.id} value={a.id}>
                              {a.name}
                            </option>
                          ))}
                        </select>
                      </div>
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          </div>

          <div className="flex items-center gap-2">
            <Button onClick={() => save.mutate()} disabled={save.isPending || !form.name.trim()}>
              {editing ? <Save className="h-4 w-4 mr-2" /> : <Plus className="h-4 w-4 mr-2" />}
              {save.isPending ? 'Saving...' : editing ? 'Save' : 'Create'}
            </Button>
            {editing ? (
              <Button
                variant="outline"
                onClick={() => {
                  setEditing(null);
                  setForm({
                    name: '',
                    isActive: true,
                    priority: 0,
                    trigger: 'FirstResponseBreached',
                    dueSoonMinutes: 15,
                    ticketType: null,
                    ticketPriority: null,
                    categoryId: null,
                    subcategoryId: null,
                    departmentId: null,
                  });
                  setLevels([{ ...defaultLevelForm }]);
                }}
              >
                Cancel
              </Button>
            ) : null}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Existing</CardTitle>
          <CardDescription>Click a row to edit.</CardDescription>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="text-slate-600">Loading...</div>
          ) : error ? (
            <div className="text-red-600">Failed to load escalation policies.</div>
          ) : !policies || policies.length === 0 ? (
            <div className="text-slate-600">No escalation policies yet.</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="text-left text-slate-500">
                  <tr>
                    <th className="py-2 pr-4">Name</th>
                    <th className="py-2 pr-4">Trigger</th>
                    <th className="py-2 pr-4">Scope</th>
                    <th className="py-2 pr-4">Levels</th>
                    <th className="py-2 pr-4 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {policies.map((p) => {
                    const scopeParts = [
                      p.ticketType ? `Type: ${p.ticketType}` : null,
                      p.ticketPriority ? `Priority: ${p.ticketPriority}` : null,
                      p.departmentId ? `Department: ${(departments || []).find((d) => d.id === p.departmentId)?.name || '—'}` : null,
                      p.categoryId ? `Category: ${categoryLabelById.get(p.categoryId) || '—'}` : null,
                      p.subcategoryId ? `Sub: ${categoryLabelById.get(p.subcategoryId) || '—'}` : null,
                    ].filter(Boolean);

                    return (
                      <tr key={p.id} className="hover:bg-slate-50">
                        <td className="py-2 pr-4 font-medium text-slate-900">
                          <Button variant="link" className="h-auto p-0 text-blue-700" onClick={() => startEdit(p)} title="Edit policy">
                            {p.name}
                          </Button>
                        </td>
                        <td className="py-2 pr-4">
                          {triggerOptions.find((t) => t.value === p.trigger)?.label || p.trigger}
                        </td>
                        <td className="py-2 pr-4 text-slate-700">{scopeParts.length ? scopeParts.join(' • ') : 'Any'}</td>
                        <td className="py-2 pr-4">{(p.levels || []).length}</td>
                        <td className="py-2 pr-4 text-right">
                          <Button variant="ghost" size="sm" onClick={() => startEdit(p)} title="Edit">
                            <Pencil className="h-4 w-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={(e) => {
                              e.stopPropagation();
                              del.mutate(p.id);
                            }}
                            disabled={del.isPending}
                            title="Delete"
                          >
                            <Trash2 className="h-4 w-4 text-red-600" />
                          </Button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
