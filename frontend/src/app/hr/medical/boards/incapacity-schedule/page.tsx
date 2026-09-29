'use client';

import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { BookOpen, Loader2, Pencil, Plus, Scale } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { useToast } from '@/components/ui/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { medicalBoardService } from '@/services/hr/medical-board.service';
import {
  INCAPACITY_SCHEDULE_KIND_LABEL,
  type IncapacityScheduleItem,
  type IncapacityScheduleKind,
} from '@/types/hr/medical-board';

const pct = (v: number) => `${Number(v).toLocaleString(undefined, { maximumFractionDigits: 2 })}%`;

interface Draft {
  id: string | null;
  kind: IncapacityScheduleKind;
  injury: string;
  percentage: string;
  source: string;
  appliesToArmOrHand: boolean;
  isActive: boolean;
  sortOrder: string;
}

const BLANK: Draft = {
  id: null, kind: 'Incapacity', injury: '', percentage: '', source: '', appliesToArmOrHand: false, isActive: true, sortOrder: '0',
};

/**
 * The compensation schedule a medical board assesses injuries against (round 5, lane K-II-b).
 *
 * ⚠ **Loaded from PNDCL 187's First and Third Schedules, as read in the Act's primary text**
 * (`docs/HR/catalogues/HR-WORKMENS-COMPENSATION-SCHEDULES.md`), and editable, so an organisation under
 * another schedule — or an insurer's table — names its own rows and their source.
 *
 * ⚠ **An edit here never rewrites a finding.** Each assessed injury keeps the percentage its row had
 * when it was assessed. Rows are retired, never deleted.
 *
 * Reading needs the medical permissions; changing the rates needs Medical administration — they are
 * the statute's figures, configuration rather than casework.
 */
export default function IncapacitySchedulePage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const { hasAnyPermission } = useAuth();
  const canAdmin = hasAnyPermission(['HR.Medical.Admin']);

  const [showRetired, setShowRetired] = useState(false);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [busy, setBusy] = useState(false);

  const queryKey = ['hr', 'medical-boards', 'incapacity-schedule', showRetired];
  const { data: rows = [], isLoading } = useQuery({
    queryKey,
    queryFn: () => medicalBoardService.getSchedule(showRetired),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'medical-boards', 'incapacity-schedule'] });

  const loadDefaults = async () => {
    setBusy(true);
    try {
      const before = rows.length;
      const after = await medicalBoardService.loadDefaultSchedule();
      await refresh();
      const added = after.length - before;
      toast({
        title: added > 0 ? `${added} row${added === 1 ? '' : 's'} added from the Act` : 'Nothing new to add',
        description: added > 0
          ? 'Rows already on the schedule were left as they are.'
          : "Every one of the Act's rows is already on this schedule.",
      });
    } catch (e: any) {
      toast({ title: 'Error', description: e?.message || 'Could not load the schedule.', variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  };

  const save = async () => {
    if (!draft) return;
    setBusy(true);
    try {
      const payload = {
        kind: draft.kind,
        injury: draft.injury.trim(),
        percentage: Number(draft.percentage),
        source: draft.source.trim(),
        appliesToArmOrHand: draft.kind === 'Incapacity' && draft.appliesToArmOrHand,
        isActive: draft.isActive,
        sortOrder: Number(draft.sortOrder) || 0,
      };
      if (draft.id) await medicalBoardService.updateScheduleItem(draft.id, payload);
      else await medicalBoardService.addScheduleItem(payload);
      await refresh();
      toast({ title: draft.id ? 'Row saved' : 'Row added' });
      setDraft(null);
    } catch (e: any) {
      toast({ title: 'Error', description: e?.message || 'Could not save the row.', variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  };

  const edit = (r: IncapacityScheduleItem) =>
    setDraft({
      id: r.id, kind: r.kind, injury: r.injury, percentage: String(r.percentage), source: r.source,
      appliesToArmOrHand: r.appliesToArmOrHand, isActive: r.isActive, sortOrder: String(r.sortOrder),
    });

  const section = (kind: IncapacityScheduleKind, note: string) => {
    const list = rows.filter((r) => r.kind === kind);
    return (
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">{INCAPACITY_SCHEDULE_KIND_LABEL[kind]}</CardTitle>
          <p className="text-sm text-muted-foreground">{note}</p>
        </CardHeader>
        <CardContent className="p-0">
          {list.length === 0 ? (
            <p className="px-6 pb-4 text-sm text-muted-foreground">No rows.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Injury</TableHead>
                  <TableHead className="text-right">%</TableHead>
                  {kind === 'Incapacity' && <TableHead>Arm or hand</TableHead>}
                  <TableHead>Source</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {list.map((r) => (
                  <TableRow key={r.id} className={r.isActive ? '' : 'opacity-60'}>
                    <TableCell>
                      {r.injury}
                      {!r.isActive && <span className="ml-2 text-xs text-muted-foreground">retired</span>}
                    </TableCell>
                    <TableCell className="text-right font-medium">{pct(r.percentage)}</TableCell>
                    {kind === 'Incapacity' && (
                      <TableCell className="text-muted-foreground">{r.appliesToArmOrHand ? '90 % if not favoured' : '—'}</TableCell>
                    )}
                    <TableCell className="text-muted-foreground">{r.source}</TableCell>
                    <TableCell className="text-right">
                      {canAdmin && (
                        <Button variant="ghost" size="sm" onClick={() => edit(r)}>
                          <Pencil className="h-4 w-4" />
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    );
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Compensation schedule"
        description="The injuries a medical board assesses incapacity against, and their percentages of permanent total incapacity"
        backHref="/hr/medical/boards"
        actions={
          canAdmin ? (
            <div className="flex flex-wrap gap-2">
              <Button variant="outline" disabled={busy} onClick={loadDefaults}>
                <BookOpen className="mr-2 h-4 w-4" /> Load the Act&apos;s schedules
              </Button>
              <Button disabled={busy} onClick={() => setDraft({ ...BLANK })}>
                <Plus className="mr-2 h-4 w-4" /> Add a row
              </Button>
            </div>
          ) : undefined
        }
      />

      <Card>
        <CardContent className="space-y-2 pt-6 text-sm">
          <p>
            The Workmen&apos;s Compensation Act 1987 (PNDCL 187) pays <strong>96 months&apos; earnings</strong> for
            permanent total incapacity (s.5), and a partial incapacity its percentage of that (s.6). The
            months, the longest temporary incapacity and the earnings ceiling are on the HR policy page.
          </p>
          <ul className="list-disc space-y-1 pl-5 text-muted-foreground">
            <li>Total loss of the use of a member counts as losing it; partial loss of use, half its percentage.</li>
            <li>An injury to the arm or hand the employee does not favour: ninety percent of its percentage.</li>
            <li>Several injuries are added up, never above 100 % (s.6(2)); 100 % or more is permanent total (s.38).</li>
            <li>An injury the schedule does not name is assessed by the panel against lost earning capacity (s.6(1)(b)).</li>
          </ul>
          <label className="flex items-center gap-2 pt-2">
            <input type="checkbox" checked={showRetired} onChange={(e) => setShowRetired(e.target.checked)} />
            Show retired rows
          </label>
        </CardContent>
      </Card>

      {isLoading ? (
        <div className="flex justify-center py-16"><Loader2 className="h-6 w-6 animate-spin text-muted-foreground" /></div>
      ) : rows.length === 0 ? (
        <EmptyState
          icon={Scale}
          title="The schedule is empty"
          description={canAdmin
            ? "Load the Act's schedules to start from PNDCL 187's First and Third Schedules, or add your own rows."
            : "A medical administrator loads the Act's schedules. Until then, a board can still record the panel's own assessment."}
        />
      ) : (
        <>
          {section('Incapacity', 'Third Schedule (s.6): the injury carries this percentage.')}
          {section('Disfigurement', 'First Schedule (s.8): a practitioner determines the figure, up to this percentage.')}
        </>
      )}

      <Dialog open={!!draft} onOpenChange={(o) => !o && setDraft(null)}>
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>{draft?.id ? 'Change a row' : 'Add a row'}</DialogTitle>
            <DialogDescription>
              ⚠ Injuries already assessed keep the percentage they were assessed at — a change here only
              affects the next assessment.
            </DialogDescription>
          </DialogHeader>
          {draft && (
            <div className="space-y-3">
              <div className="space-y-1.5">
                <Label htmlFor="row-kind">Schedule</Label>
                <Select value={draft.kind} onValueChange={(v) => setDraft({ ...draft, kind: v as IncapacityScheduleKind })}>
                  <SelectTrigger id="row-kind"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {(Object.keys(INCAPACITY_SCHEDULE_KIND_LABEL) as IncapacityScheduleKind[]).map((k) => (
                      <SelectItem key={k} value={k}>{INCAPACITY_SCHEDULE_KIND_LABEL[k]}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="row-injury">Injury</Label>
                <Input id="row-injury" value={draft.injury} onChange={(e) => setDraft({ ...draft, injury: e.target.value })} />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1.5">
                  <Label htmlFor="row-pct">{draft.kind === 'Disfigurement' ? 'Up to (%)' : 'Percentage'}</Label>
                  <Input id="row-pct" type="number" min={0} max={100} step="0.01" value={draft.percentage}
                    onChange={(e) => setDraft({ ...draft, percentage: e.target.value })} />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="row-order">Order</Label>
                  <Input id="row-order" type="number" value={draft.sortOrder}
                    onChange={(e) => setDraft({ ...draft, sortOrder: e.target.value })} />
                </div>
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="row-source">Source</Label>
                <Input id="row-source" value={draft.source} placeholder="e.g. PNDCL 187, Third Schedule (s.6)"
                  onChange={(e) => setDraft({ ...draft, source: e.target.value })} />
                <p className="text-xs text-muted-foreground">Required — a rate with no source cannot be checked.</p>
              </div>
              {draft.kind === 'Incapacity' && (
                <label className="flex items-center gap-2 text-sm">
                  <input type="checkbox" checked={draft.appliesToArmOrHand}
                    onChange={(e) => setDraft({ ...draft, appliesToArmOrHand: e.target.checked })} />
                  An arm or hand — the side not favoured is rated at 90 %
                </label>
              )}
              <label className="flex items-center gap-2 text-sm">
                <input type="checkbox" checked={draft.isActive} onChange={(e) => setDraft({ ...draft, isActive: e.target.checked })} />
                In use (untick to retire it — rows are never deleted)
              </label>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setDraft(null)} disabled={busy}>Cancel</Button>
            <Button disabled={busy || !draft?.injury.trim() || !draft?.percentage || !draft?.source.trim()} onClick={save}>
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
