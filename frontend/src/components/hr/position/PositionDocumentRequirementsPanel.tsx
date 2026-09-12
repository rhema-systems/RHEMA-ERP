'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FileCheck2, Loader2, Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { HR_ADMIN_ROLES } from '@/components/hr/common/PermissionGate';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { employeeDocumentService } from '@/services/hr/employee-document.service';

/**
 * The documents a position requires its holder to have on file.
 *
 * ⚠ **It reports; it does not block.** Nothing here refuses an appointment or a payroll run for a
 * missing document. Area 8's FR-HR-173 vacancy rule was built as a hard block exactly as specified
 * and refused nearly every movement — and blocking here would stop the very transaction that gets
 * somebody employed and able to supply the document. What it does is make the question answerable
 * on each employee's Documents tab.
 *
 * ⚠ Lives BESIDE the position form, not inside it: requirements are their own endpoints rather than
 * part of the position payload, and they only mean anything for a position that already exists.
 */
export function PositionDocumentRequirementsPanel({ positionId }: { positionId: string }) {
  const { toast } = useToast();
  const qc = useQueryClient();
  const { hasAnyPermission, hasAnyRole } = useAuth();

  // The server gates every write here on EmployeeAdmin. Gate the affordance to match.
  const canEdit = hasAnyPermission(['HR.Employee.Admin']) || hasAnyRole(HR_ADMIN_ROLES);

  const [typeId, setTypeId] = useState('');
  const [mandatory, setMandatory] = useState(true);
  const [notes, setNotes] = useState('');

  const { data: requirements, isLoading } = useQuery({
    queryKey: ['position-document-requirements', positionId],
    queryFn: () => employeeDocumentService.getRequirements(positionId),
    enabled: !!positionId,
  });

  const { data: types } = useQuery({
    queryKey: ['employee-document-types'],
    queryFn: () => employeeDocumentService.getTypes(),
  });

  const refresh = () =>
    void qc.invalidateQueries({ queryKey: ['position-document-requirements', positionId] });

  const add = useMutation({
    mutationFn: () => employeeDocumentService.addRequirement({
      positionId, documentTypeId: typeId, isMandatory: mandatory, notes: notes || null,
    }),
    onSuccess: () => {
      toast({ title: 'Requirement added' });
      setTypeId(''); setMandatory(true); setNotes('');
      refresh();
    },
    onError: (e: unknown) => toast({
      variant: 'destructive', title: 'Not added',
      // The server explains itself — "That document is already required for this position" — so
      // show what it said rather than a generic failure.
      description: e instanceof Error ? e.message : 'Refused',
    }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => employeeDocumentService.deleteRequirement(id),
    onSuccess: () => { toast({ title: 'Requirement removed' }); refresh(); },
    onError: (e: unknown) => toast({
      variant: 'destructive', title: 'Not removed',
      description: e instanceof Error ? e.message : 'Refused',
    }),
  });

  const toggleMandatory = useMutation({
    mutationFn: (r: { id: string; isMandatory: boolean; notes: string | null }) =>
      employeeDocumentService.updateRequirement(r.id, { isMandatory: !r.isMandatory, notes: r.notes }),
    onSuccess: refresh,
    onError: (e: unknown) => toast({
      variant: 'destructive', title: 'Not saved',
      description: e instanceof Error ? e.message : 'Refused',
    }),
  });

  // Only offer types that are not already required — the server refuses a duplicate, and offering
  // one that will be refused is a button that teaches nothing.
  const available = (types ?? []).filter(
    (t) => !(requirements ?? []).some((r) => r.documentTypeId === t.id));

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <FileCheck2 className="h-4 w-4" />
          Documents this position requires
        </CardTitle>
        <CardDescription>
          Each holder&apos;s Documents tab shows which of these they have on file. Nothing is
          blocked by a missing document — it is reported so it can be chased.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {isLoading ? (
          <div className="flex justify-center p-6">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : !requirements || requirements.length === 0 ? (
          <EmptyState
            icon={FileCheck2}
            title="Nothing required"
            description="Add the documents somebody in this post must have on file."
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Document</TableHead>
                <TableHead className="w-[140px]">Mandatory</TableHead>
                <TableHead>Notes</TableHead>
                {canEdit && <TableHead className="w-[60px]" />}
              </TableRow>
            </TableHeader>
            <TableBody>
              {requirements.map((r) => (
                <TableRow key={r.id}>
                  <TableCell className="font-medium">{r.documentTypeName}</TableCell>
                  <TableCell>
                    {canEdit ? (
                      <Checkbox
                        checked={r.isMandatory}
                        onCheckedChange={() => toggleMandatory.mutate({
                          id: r.id, isMandatory: r.isMandatory, notes: r.notes,
                        })}
                        aria-label={`${r.documentTypeName} is mandatory`}
                      />
                    ) : (
                      <Badge variant={r.isMandatory ? 'default' : 'secondary'}>
                        {r.isMandatory ? 'Mandatory' : 'Expected'}
                      </Badge>
                    )}
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">{r.notes ?? '—'}</TableCell>
                  {canEdit && (
                    <TableCell>
                      <Button
                        variant="ghost" size="icon"
                        aria-label={`Remove ${r.documentTypeName}`}
                        onClick={() => remove.mutate(r.id)}
                      >
                        <Trash2 className="h-4 w-4 text-destructive" />
                      </Button>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}

        {canEdit && (
          <div className="flex flex-wrap items-end gap-3 border-t pt-4">
            <div className="min-w-[220px] flex-1 space-y-2">
              <Label>Add a requirement</Label>
              <Select value={typeId} onValueChange={setTypeId}>
                <SelectTrigger>
                  <SelectValue placeholder={
                    available.length === 0 ? 'Every document type is already required' : 'Choose a document type'
                  } />
                </SelectTrigger>
                <SelectContent>
                  {available.map((t) => (
                    <SelectItem key={t.id} value={t.id}>{t.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="min-w-[200px] flex-1 space-y-2">
              <Label>Notes</Label>
              <Input
                value={notes} placeholder="Optional"
                onChange={(e) => setNotes(e.target.value)}
              />
            </div>
            <div className="flex items-center gap-2 pb-2">
              <Checkbox
                id="req-mandatory"
                checked={mandatory}
                onCheckedChange={(v) => setMandatory(v === true)}
              />
              <Label htmlFor="req-mandatory" className="cursor-pointer">Mandatory</Label>
            </div>
            <Button
              className="mb-0.5"
              onClick={() => add.mutate()}
              disabled={!typeId || add.isPending}
            >
              {add.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Plus className="mr-2 h-4 w-4" />}
              Add
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
