'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Layers, Loader2, Pencil, Plus, Search, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import type { Competency, CompetencyCategory } from '@/types/hr/job-architecture';

const CATEGORIES: CompetencyCategory[] = [
  'Leadership',
  'Technical',
  'Behavioral',
  'Functional',
  'Core',
  'Other',
];

const CATEGORY_TONE: Record<string, string> = {
  Leadership: 'bg-violet-100 text-violet-800',
  Technical: 'bg-sky-100 text-sky-800',
  Behavioral: 'bg-amber-100 text-amber-800',
  Functional: 'bg-teal-100 text-teal-800',
  Core: 'bg-emerald-100 text-emerald-800',
  Other: 'bg-slate-100 text-slate-700',
};

const EMPTY = {
  code: '',
  name: '',
  description: '',
  competencyCategory: 'Technical' as CompetencyCategory,
  proficiencyScaleMax: 5,
  isActive: true,
};

/**
 * The competency framework — setup, so it lives under Administration.
 *
 * ⚠ **Deleting a competency is Admin-tier and rarely what you want.** A competency in use by a
 * position requirement or an employee assessment is referenced from both, and the soft delete keeps
 * those references intact while removing it from every list. Deactivating is usually the honest
 * move; the code becomes reusable either way, because the uniqueness check is scoped to live rows.
 */
export default function CompetencyFrameworkPage() {
  const qc = useQueryClient();
  const [search, setSearch] = useState('');
  const [editing, setEditing] = useState<Competency | null>(null);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState(EMPTY);

  const { data: competencies, isLoading } = useQuery({
    queryKey: ['competencies', 'all'],
    queryFn: () => jobArchitectureService.getCompetencies(),
  });

  const rows = useMemo(() => {
    const term = search.trim().toLowerCase();
    return (competencies ?? []).filter(
      (c) =>
        !term ||
        c.name.toLowerCase().includes(term) ||
        c.code.toLowerCase().includes(term) ||
        c.description?.toLowerCase().includes(term),
    );
  }, [competencies, search]);

  const close = () => {
    setCreating(false);
    setEditing(null);
    setForm(EMPTY);
  };

  const save = useMutation({
    mutationFn: async () => {
      if (editing) {
        return jobArchitectureService.updateCompetency(editing.id, { id: editing.id, ...form });
      }
      return jobArchitectureService.createCompetency(form);
    },
    onSuccess: () => {
      toast.success(editing ? 'Competency updated' : 'Competency added');
      qc.invalidateQueries({ queryKey: ['competencies'] });
      close();
    },
    onError: (e) =>
      // The duplicate-code refusal names the clash, so surface the message rather than a generic one.
      toast.error(e instanceof Error ? e.message : 'Could not save the competency'),
  });

  const remove = useMutation({
    mutationFn: (id: string) => jobArchitectureService.deleteCompetency(id),
    onSuccess: () => {
      toast.success('Competency removed');
      qc.invalidateQueries({ queryKey: ['competencies'] });
    },
    onError: (e) => toast.error(e instanceof Error ? e.message : 'Could not remove it'),
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="Competency framework"
        description="What the organisation expects people to be able to do, and at what level."
        actions={
          <Button
            onClick={() => {
              setForm(EMPTY);
              setCreating(true);
            }}
          >
            <Plus className="mr-2 h-4 w-4" />
            Add competency
          </Button>
        }
      />

      <Card>
        <CardContent className="space-y-4 pt-6">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="Search by name, code or description"
              className="pl-9"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>

          {isLoading ? (
            <div className="flex items-center justify-center py-16 text-muted-foreground">
              <Loader2 className="mr-2 h-5 w-5 animate-spin" />
              Loading…
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Layers}
              title="No competencies yet"
              description="A starter framework ships with the system; add to it as roles need."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Code</TableHead>
                  <TableHead>Competency</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead className="text-right">Scale</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((c) => (
                  <TableRow key={c.id}>
                    <TableCell className="font-mono text-xs">{c.code}</TableCell>
                    <TableCell>
                      <div className="font-medium">{c.name}</div>
                      <div className="text-xs text-muted-foreground line-clamp-1">{c.description}</div>
                    </TableCell>
                    <TableCell>
                      <Badge className={CATEGORY_TONE[c.competencyCategory] ?? CATEGORY_TONE.Other}>
                        {c.competencyCategory}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">1–{c.proficiencyScaleMax}</TableCell>
                    <TableCell>
                      {c.isActive ? (
                        <Badge variant="outline">Active</Badge>
                      ) : (
                        <Badge variant="outline" className="text-muted-foreground">
                          Inactive
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => {
                          setEditing(c);
                          setForm({
                            code: c.code,
                            name: c.name,
                            description: c.description,
                            competencyCategory: c.competencyCategory,
                            proficiencyScaleMax: c.proficiencyScaleMax,
                            isActive: c.isActive,
                          });
                        }}
                      >
                        <Pencil className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => remove.mutate(c.id)}
                        disabled={remove.isPending}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={creating || !!editing} onOpenChange={(open) => !open && close()}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit competency' : 'Add competency'}</DialogTitle>
          </DialogHeader>
          <div className="grid gap-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Code *</Label>
                <Input
                  value={form.code}
                  onChange={(e) => setForm({ ...form, code: e.target.value })}
                  placeholder="e.g. FIN-ANLY"
                />
                {/* The API refuses a duplicate live code, case-insensitively, and says which. */}
                <p className="text-xs text-muted-foreground">Must be unique.</p>
              </div>
              <div className="space-y-2">
                <Label>Category *</Label>
                <Select
                  value={form.competencyCategory}
                  onValueChange={(v) => setForm({ ...form, competencyCategory: v as CompetencyCategory })}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {CATEGORIES.map((c) => (
                      <SelectItem key={c} value={c}>
                        {c}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="space-y-2">
              <Label>Name *</Label>
              <Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
            </div>

            <div className="space-y-2">
              <Label>Description *</Label>
              <Textarea
                rows={3}
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
                placeholder="What someone who has this competency can do."
              />
            </div>

            <div className="space-y-2">
              <Label>Proficiency scale maximum</Label>
              <Input
                type="number"
                min={2}
                max={10}
                value={form.proficiencyScaleMax}
                onChange={(e) => setForm({ ...form, proficiencyScaleMax: Number(e.target.value) })}
              />
              <p className="text-xs text-muted-foreground">
                Position requirements and assessments are recorded against this scale.
              </p>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={close}>
              Cancel
            </Button>
            <Button
              onClick={() => save.mutate()}
              disabled={!form.code.trim() || !form.name.trim() || !form.description.trim() || save.isPending}
            >
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
