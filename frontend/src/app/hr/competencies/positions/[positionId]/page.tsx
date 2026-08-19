'use client';

import { useEffect, useMemo, useState } from 'react';
import { useParams } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { GraduationCap, Loader2, Plus, Save, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
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
import { employeePositionService } from '@/services/hr/employee-position.service';

type Row = { competencyId: string; requiredProficiencyLevel: number; notes?: string };

/**
 * What a position requires. Edited as a whole set, then saved once.
 *
 * ⚠ **`bulk-set` REPLACES the entire set — anything omitted is removed**, which is why this screen
 * edits a local copy and saves it in one call rather than adding and deleting row by row. It is also
 * the endpoint that used to 500 on every save: it soft-deletes the current set and inserts the new
 * one in a single transaction, and the unique index did not know about the soft delete, so keeping
 * ANY competency across a save collided with its own deleted row. Fixed in slice 5 by filtering the
 * index on `IsDeleted`.
 */
export default function PositionCompetenciesPage() {
  const { positionId } = useParams<{ positionId: string }>();
  const qc = useQueryClient();
  const [rows, setRows] = useState<Row[]>([]);
  const [saving, setSaving] = useState(false);
  const [adding, setAdding] = useState('');

  const { data: existing, isLoading } = useQuery({
    queryKey: ['position-competencies', positionId],
    queryFn: () => jobArchitectureService.getPositionCompetencies(positionId),
    enabled: !!positionId,
  });

  const { data: competencies } = useQuery({
    queryKey: ['competencies', 'active'],
    queryFn: () => jobArchitectureService.getActiveCompetencies(),
  });

  const { data: positions } = useQuery({
    queryKey: ['positions', 'all'],
    queryFn: () => employeePositionService.getAll(),
  });

  const position = useMemo(
    () => (positions ?? []).find((p) => p.id === positionId),
    [positions, positionId],
  );

  useEffect(() => {
    if (existing) {
      setRows(
        existing.map((r) => ({
          competencyId: r.competencyId,
          requiredProficiencyLevel: r.requiredProficiencyLevel,
          notes: r.notes ?? undefined,
        })),
      );
    }
  }, [existing]);

  const byId = useMemo(
    () => new Map((competencies ?? []).map((c) => [c.id, c])),
    [competencies],
  );

  const available = useMemo(
    () => (competencies ?? []).filter((c) => !rows.some((r) => r.competencyId === c.id)),
    [competencies, rows],
  );

  const save = async () => {
    setSaving(true);
    try {
      await jobArchitectureService.bulkSetPositionCompetencies(positionId, rows);
      toast.success('Requirements saved');
      qc.invalidateQueries({ queryKey: ['position-competencies', positionId] });
      qc.invalidateQueries({ queryKey: ['competencies', 'organisation-gaps'] });
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Could not save the requirements');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="Competency requirements"
        description={position ? position.title : 'What this position requires.'}
        backHref="/hr/competencies"
        actions={
          <Button onClick={save} disabled={saving}>
            {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
            Save requirements
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>Add a competency</CardTitle>
        </CardHeader>
        <CardContent className="flex gap-3">
          <Select value={adding} onValueChange={setAdding}>
            <SelectTrigger className="max-w-md">
              <SelectValue placeholder="Choose a competency" />
            </SelectTrigger>
            <SelectContent>
              {available.map((c) => (
                <SelectItem key={c.id} value={c.id}>
                  {c.code} — {c.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button
            variant="outline"
            disabled={!adding}
            onClick={() => {
              const competency = byId.get(adding);
              setRows((r) => [
                ...r,
                {
                  competencyId: adding,
                  // Middle of the competency's own scale, so the default is never above its maximum.
                  requiredProficiencyLevel: Math.max(1, Math.round((competency?.proficiencyScaleMax ?? 5) / 2)),
                },
              ]);
              setAdding('');
            }}
          >
            <Plus className="mr-2 h-4 w-4" />
            Add
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="pt-6">
          {isLoading ? (
            <div className="flex items-center justify-center py-16 text-muted-foreground">
              <Loader2 className="mr-2 h-5 w-5 animate-spin" />
              Loading…
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={GraduationCap}
              title="No requirements set"
              description="Add the competencies this position needs, then save."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Competency</TableHead>
                  <TableHead className="w-40">Required level</TableHead>
                  <TableHead>Notes</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row, index) => {
                  const competency = byId.get(row.competencyId);
                  return (
                    <TableRow key={row.competencyId}>
                      <TableCell>
                        <div className="font-medium">{competency?.name ?? 'Unknown competency'}</div>
                        <div className="font-mono text-xs text-muted-foreground">{competency?.code}</div>
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          <Input
                            type="number"
                            min={1}
                            max={competency?.proficiencyScaleMax ?? 5}
                            value={row.requiredProficiencyLevel}
                            onChange={(e) =>
                              setRows((rs) =>
                                rs.map((r, i) =>
                                  i === index
                                    ? { ...r, requiredProficiencyLevel: Number(e.target.value) }
                                    : r,
                                ),
                              )
                            }
                            className="w-20"
                          />
                          <span className="text-xs text-muted-foreground">
                            of {competency?.proficiencyScaleMax ?? 5}
                          </span>
                        </div>
                      </TableCell>
                      <TableCell>
                        <Input
                          value={row.notes ?? ''}
                          placeholder="Optional"
                          onChange={(e) =>
                            setRows((rs) =>
                              rs.map((r, i) => (i === index ? { ...r, notes: e.target.value } : r)),
                            )
                          }
                        />
                      </TableCell>
                      <TableCell className="text-right">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => setRows((rs) => rs.filter((_, i) => i !== index))}
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
          {rows.length > 0 && (
            <p className="mt-4 text-xs text-muted-foreground">
              Saving replaces the whole set — anything removed above is removed from the position.
            </p>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
