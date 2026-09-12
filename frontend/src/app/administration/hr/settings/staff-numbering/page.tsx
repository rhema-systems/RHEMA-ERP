'use client';

/**
 * Staff numbering — the rules that decide what number a new employee is given.
 *
 * ⚠ **There is no auto/manual switch on this screen, deliberately.** The ABSENCE of a rule for a
 * register IS manual entry: HR types the number and nothing needs configuring. A global toggle
 * beside a per-register table would be two sources of truth for one question, and the pair would
 * eventually disagree in a way nobody could predict.
 *
 * ⚠ **The example is composed by the SERVER**, by the same code that issues the number. Rebuilding
 * the format in TypeScript would be a second implementation, and the copy that can drift is always
 * the one the user is looking at.
 *
 * ⚠ **A rule and its counter are two different facts.** The rule says what a number looks like; the
 * counter says which one comes next, and it only knows about numbers it issued itself. Load a
 * register from anywhere else and the counter sits at zero while thousands of numbers are in use —
 * so every auto-numbered rule carries a counter panel that says, in advance, whether the next hire
 * would be handed somebody else's number.
 */

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  CheckCircle2,
  Hash,
  Loader2,
  Pencil,
  Plus,
  RefreshCw,
  Trash2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
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
import { Skeleton } from '@/components/ui/skeleton';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { referenceDimensionService } from '@/services/hr/lookup.service';
import { EMPLOYMENT_TYPE_OPTIONS } from '@/types/hr/employee';
import type {
  EmploymentRegister,
  StaffNumberCounterState,
  StaffNumberFormat,
  StaffNumberFormatRequest,
} from '@/types/hr/lookups';

const RULES_KEY = ['hr', 'staff-number-formats'] as const;

/** The sentinel the register select uses for "the tenant default", which the wire sends as null. */
const DEFAULT_REGISTER = '__default__';

const emptyRule: StaffNumberFormatRequest = {
  name: '',
  appliesToEmploymentType: null,
  prefix: '',
  separator: '',
  includeYear: false,
  yearDigits: 4,
  sequenceDigits: 4,
  suffix: '',
  autoGenerate: true,
  sequenceKey: 'EMP',
  isActive: true,
};

function toRequest(rule: StaffNumberFormat): StaffNumberFormatRequest {
  return {
    name: rule.name,
    appliesToEmploymentType: rule.appliesToEmploymentType ?? null,
    prefix: rule.prefix,
    separator: rule.separator,
    includeYear: rule.includeYear,
    yearDigits: rule.yearDigits,
    sequenceDigits: rule.sequenceDigits,
    suffix: rule.suffix,
    autoGenerate: rule.autoGenerate,
    sequenceKey: rule.sequenceKey,
    isActive: rule.isActive,
  };
}

export default function StaffNumberingPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [editing, setEditing] = useState<StaffNumberFormat | null>(null);
  const [creating, setCreating] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<StaffNumberFormat | null>(null);
  const [deleting, setDeleting] = useState(false);

  const { data: rules, isLoading } = useQuery({
    queryKey: RULES_KEY,
    queryFn: () => referenceDimensionService.getStaffNumberFormats(),
  });

  const handleDelete = async () => {
    if (!deleteTarget) return false;
    setDeleting(true);
    try {
      await referenceDimensionService.removeStaffNumberFormat(deleteTarget.id);
      await queryClient.invalidateQueries({ queryKey: RULES_KEY });
      toast({
        title: 'Rule removed',
        description: `${deleteTarget.appliesToName} are now numbered by hand.`,
      });
      setDeleteTarget(null);
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to remove the numbering rule.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setDeleting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Staff Numbering"
        description="How each register of employees is numbered, and what number comes next."
        backHref="/administration/hr/settings"
        actions={
          <Button onClick={() => setCreating(true)}>
            <Plus className="mr-2 h-4 w-4" /> New Rule
          </Button>
        }
      />

      {isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-32 w-full" />
          <Skeleton className="h-32 w-full" />
        </div>
      ) : (rules ?? []).length === 0 ? (
        /*
         * ⚠ An empty list is a MEANINGFUL state, not an unconfigured screen. With no rule, every
         * register is numbered by hand — a valid and deliberate setup, and the wrong thing to
         * present as a blank table waiting to be filled.
         */
        <EmptyState
          icon={Hash}
          title="Every register is numbered by hand"
          description="No numbering rule is configured, so HR types the staff number when an employee is added. Add a rule for any register the system should number itself."
          action={
            <Button onClick={() => setCreating(true)}>
              <Plus className="mr-2 h-4 w-4" /> New Rule
            </Button>
          }
        />
      ) : (
        <div className="space-y-4">
          {(rules ?? []).map((rule) => (
            <RuleCard
              key={rule.id}
              rule={rule}
              onEdit={() => setEditing(rule)}
              onDelete={() => setDeleteTarget(rule)}
            />
          ))}
          <p className="text-xs text-muted-foreground">
            A register with no rule of its own follows the default rule. A register with no rule at
            all is numbered by hand — removing a rule is how you go back to that.
          </p>
        </div>
      )}

      <RuleDialog open={creating} rule={null} onClose={() => setCreating(false)} />
      <RuleDialog open={editing !== null} rule={editing} onClose={() => setEditing(null)} />

      <ConfirmationDialog
        open={deleteTarget !== null}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title="Remove this numbering rule?"
        description={
          deleteTarget
            ? `${deleteTarget.appliesToName} will go back to being numbered by hand. Numbers already issued are unaffected — they are recorded on the employee, not derived from the rule.`
            : ''
        }
        confirmText="Remove"
        variant="destructive"
        isLoading={deleting}
        onConfirm={handleDelete}
      />
    </div>
  );
}

// ── one rule, with its counter ──────────────────────────────────────────────

function RuleCard({
  rule,
  onEdit,
  onDelete,
}: {
  rule: StaffNumberFormat;
  onEdit: () => void;
  onDelete: () => void;
}) {
  return (
    <Card>
      <CardHeader className="pb-3">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <CardTitle className="flex items-center gap-2">
              {rule.name}
              {!rule.isActive && <Badge variant="outline">Retired</Badge>}
              {rule.appliesToEmploymentType === null && <Badge variant="secondary">Default</Badge>}
            </CardTitle>
            <CardDescription>
              {rule.appliesToEmploymentType === null
                ? 'Applies to every register without a rule of its own.'
                : `Applies to ${rule.appliesToName} staff.`}
            </CardDescription>
          </div>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" onClick={onEdit}>
              <Pencil className="mr-2 h-4 w-4" /> Edit
            </Button>
            <Button variant="outline" size="sm" onClick={onDelete}>
              <Trash2 className="mr-2 h-4 w-4" /> Remove
            </Button>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex flex-wrap items-center gap-x-8 gap-y-2 text-sm">
          <div>
            <span className="text-muted-foreground">Looks like </span>
            <span className="font-mono font-medium">{rule.example}</span>
          </div>
          <div>
            <span className="text-muted-foreground">Counter </span>
            <span className="font-mono">{rule.sequenceKey}</span>
            {rule.includeYear && <span className="text-muted-foreground"> · resets each year</span>}
          </div>
          <div className="text-muted-foreground">
            {rule.autoGenerate ? 'Issued by the system' : 'Entered by hand'}
          </div>
        </div>

        {/*
         * The counter panel is shown only for a rule that issues numbers. A hand-entered register
         * has no counter to be behind, and showing one would invite somebody to "fix" a number
         * that nothing consults.
         */}
        {rule.autoGenerate && rule.isActive && <CounterPanel rule={rule} />}
      </CardContent>
    </Card>
  );
}

function CounterPanel({ rule }: { rule: StaffNumberFormat }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data: counter, isLoading } = useQuery({
    queryKey: ['hr', 'staff-number-counter', rule.id],
    queryFn: () => referenceDimensionService.getStaffNumberCounter(rule.id),
  });

  const reconcile = useMutation({
    mutationFn: () => referenceDimensionService.reconcileStaffNumberCounter(rule.id),
    onSuccess: async (state: StaffNumberCounterState) => {
      queryClient.setQueryData(['hr', 'staff-number-counter', rule.id], state);
      await queryClient.invalidateQueries({ queryKey: RULES_KEY });
      toast({
        title: 'Counter reconciled',
        description: `The next number for ${rule.name} is ${state.nextNumber}.`,
      });
    },
    onError: (error: any) =>
      toast({
        title: 'Error',
        description: error?.message || 'Failed to reconcile the counter.',
        variant: 'destructive',
      }),
  });

  if (isLoading) return <Skeleton className="h-24 w-full" />;
  if (!counter) return null;

  const behind = counter.counterIsBehind || counter.nextNumberIsInUse;

  return (
    <div
      className={`rounded-md border p-4 ${
        behind
          ? 'border-amber-300 bg-amber-50 dark:border-amber-900/60 dark:bg-amber-950/30'
          : 'bg-muted/40'
      }`}
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="max-w-3xl space-y-1">
          <div className="flex items-center gap-2 text-sm font-medium">
            {behind ? (
              <AlertTriangle className="h-4 w-4 text-amber-600" />
            ) : (
              <CheckCircle2 className="h-4 w-4 text-emerald-600" />
            )}
            {behind ? 'The counter is behind the register' : 'The counter matches the register'}
          </div>
          <p className="text-sm text-muted-foreground">
            Next number{' '}
            <span className="font-mono font-medium text-foreground">{counter.nextNumber}</span>
            {counter.nextNumberIsInUse && (
              <span className="font-medium text-amber-700 dark:text-amber-400">
                {' '}
                — already in use
              </span>
            )}
            .{' '}
            {counter.numbersInRegister > 0 ? (
              <>
                {counter.numbersInRegister} number
                {counter.numbersInRegister === 1 ? '' : 's'} in the register match this rule, the
                highest being{' '}
                <span className="font-mono text-foreground">{counter.highestNumberInRegister}</span>
                .
              </>
            ) : (
              <>No number in the register matches this rule yet.</>
            )}
          </p>
          {behind && (
            /*
             * Said in full rather than left to a warning colour. This is the failure the panel
             * exists for, and it surfaces far from here — as a create that dies on a unique index,
             * on a screen with nothing to do with numbering.
             */
            <p className="text-sm text-amber-800 dark:text-amber-300">
              Staff numbers already in the register were never counted — they were loaded, seeded or
              migrated in. Until the counter is moved past them, the next employee added will be
              given a number somebody already has, and the create will fail on the unique index with
              nothing pointing back to here.
            </p>
          )}
          {counter.numbersNotMatchingFormat > 0 && (
            <p className="text-xs text-muted-foreground">
              {counter.numbersNotMatchingFormat} other staff number
              {counter.numbersNotMatchingFormat === 1 ? '' : 's'} did not fit this rule and were not
              counted — another register&apos;s numbering, or another year&apos;s.
            </p>
          )}
        </div>
        <Button
          variant={behind ? 'default' : 'outline'}
          size="sm"
          disabled={reconcile.isPending || (!behind && counter.numbersInRegister === 0)}
          onClick={() => reconcile.mutate()}
        >
          {reconcile.isPending ? (
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          ) : (
            <RefreshCw className="mr-2 h-4 w-4" />
          )}
          Reconcile counter
        </Button>
      </div>
    </div>
  );
}

// ── the editor, with a server-composed preview ──────────────────────────────

function RuleDialog({
  open,
  rule,
  onClose,
}: {
  open: boolean;
  rule: StaffNumberFormat | null;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [values, setValues] = useState<StaffNumberFormatRequest>(emptyRule);
  const [saving, setSaving] = useState(false);
  const [preview, setPreview] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setValues(rule ? toRequest(rule) : emptyRule);
    setPreview(rule?.example ?? null);
  }, [open, rule]);

  /*
   * ⚠ The example comes from the server on every change to the shape. Composing it here would be a
   * second implementation of the format, and the copy the user is looking at is exactly the one
   * that must not be able to disagree with the one that issues the number.
   */
  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    const handle = setTimeout(() => {
      referenceDimensionService
        .previewStaffNumberFormat({
          prefix: values.prefix,
          separator: values.separator,
          includeYear: values.includeYear,
          yearDigits: values.yearDigits,
          sequenceDigits: values.sequenceDigits,
          suffix: values.suffix,
        })
        .then((r) => {
          if (!cancelled) setPreview(r.example);
        })
        .catch(() => {
          if (!cancelled) setPreview(null);
        });
    }, 250);
    return () => {
      cancelled = true;
      clearTimeout(handle);
    };
  }, [
    open,
    values.prefix,
    values.separator,
    values.includeYear,
    values.yearDigits,
    values.sequenceDigits,
    values.suffix,
  ]);

  const set = <K extends keyof StaffNumberFormatRequest>(
    key: K,
    value: StaffNumberFormatRequest[K],
  ) => setValues((v) => ({ ...v, [key]: value }));

  const handleSave = async () => {
    if (!values.name.trim()) {
      toast({ title: 'A name is required', variant: 'destructive' });
      return;
    }
    if (!values.sequenceKey.trim()) {
      toast({ title: 'A counter key is required', variant: 'destructive' });
      return;
    }
    setSaving(true);
    try {
      if (rule) {
        await referenceDimensionService.updateStaffNumberFormat(rule.id, values);
      } else {
        await referenceDimensionService.createStaffNumberFormat(values);
      }
      await queryClient.invalidateQueries({ queryKey: RULES_KEY });
      toast({ title: rule ? 'Rule updated' : 'Rule created' });
      onClose();
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to save the numbering rule.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{rule ? 'Edit numbering rule' : 'New numbering rule'}</DialogTitle>
          <DialogDescription>
            One rule per register. The register it applies to decides which employees it numbers;
            the default rule catches everything without a rule of its own.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="rule-name">Name</Label>
              <Input
                id="rule-name"
                value={values.name}
                placeholder="Permanent staff"
                onChange={(e) => set('name', e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="rule-register">Applies to</Label>
              <Select
                value={values.appliesToEmploymentType ?? DEFAULT_REGISTER}
                onValueChange={(v) =>
                  set(
                    'appliesToEmploymentType',
                    v === DEFAULT_REGISTER ? null : (v as EmploymentRegister),
                  )
                }
              >
                <SelectTrigger id="rule-register">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={DEFAULT_REGISTER}>All other staff (default)</SelectItem>
                  {EMPLOYMENT_TYPE_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid gap-4 sm:grid-cols-4">
            <div className="space-y-2">
              <Label htmlFor="rule-prefix">Prefix</Label>
              <Input
                id="rule-prefix"
                value={values.prefix}
                placeholder="EMP"
                maxLength={10}
                onChange={(e) => set('prefix', e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="rule-separator">Separator</Label>
              <Input
                id="rule-separator"
                value={values.separator}
                placeholder="/"
                maxLength={3}
                onChange={(e) => set('separator', e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="rule-digits">Number width</Label>
              <Input
                id="rule-digits"
                type="number"
                min={1}
                max={12}
                value={values.sequenceDigits}
                onChange={(e) => set('sequenceDigits', Number(e.target.value))}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="rule-suffix">Suffix</Label>
              <Input
                id="rule-suffix"
                value={values.suffix}
                maxLength={10}
                onChange={(e) => set('suffix', e.target.value)}
              />
            </div>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2 rounded-md border p-3">
              <div className="flex items-center justify-between">
                <Label htmlFor="rule-year">Include the year</Label>
                <Switch
                  id="rule-year"
                  checked={values.includeYear}
                  onCheckedChange={(v) => set('includeYear', v)}
                />
              </div>
              {/*
               * One switch, not two. A number that prints the year must reset annually or it climbs
               * for ever; one that does not print it must never reset, or January reissues last
               * year's numbers. Separate controls would let somebody pick the pair that collides.
               */}
              <p className="text-xs text-muted-foreground">
                The counter also resets each year when the year is printed, and never resets when it
                is not.
              </p>
              {values.includeYear && (
                <div className="space-y-2 pt-1">
                  <Label htmlFor="rule-year-digits">Year digits</Label>
                  <Select
                    value={String(values.yearDigits)}
                    onValueChange={(v) => set('yearDigits', Number(v))}
                  >
                    <SelectTrigger id="rule-year-digits">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="2">Two — 26</SelectItem>
                      <SelectItem value="4">Four — 2026</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              )}
            </div>

            <div className="space-y-3 rounded-md border p-3">
              <div className="flex items-center justify-between">
                <Label htmlFor="rule-auto">Issue numbers automatically</Label>
                <Switch
                  id="rule-auto"
                  checked={values.autoGenerate}
                  onCheckedChange={(v) => set('autoGenerate', v)}
                />
              </div>
              <p className="text-xs text-muted-foreground">
                Off means HR types the number for this register. While it is on a supplied number is
                refused, so it cannot occupy one the counter is about to issue — an existing
                employee is recorded through the import instead, which keeps the number they have.
              </p>
              <div className="flex items-center justify-between">
                <Label htmlFor="rule-active">Active</Label>
                <Switch
                  id="rule-active"
                  checked={values.isActive}
                  onCheckedChange={(v) => set('isActive', v)}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="rule-key">Counter key</Label>
                <Input
                  id="rule-key"
                  value={values.sequenceKey}
                  maxLength={30}
                  onChange={(e) => set('sequenceKey', e.target.value)}
                />
                <p className="text-xs text-muted-foreground">
                  Registers sharing a key share one counter. Give a register its own key to number
                  it independently.
                </p>
              </div>
            </div>
          </div>

          <div className="rounded-md border bg-muted/40 p-3">
            <div className="text-xs uppercase tracking-wide text-muted-foreground">
              The first number this rule produces
            </div>
            <div className="pt-1 font-mono text-lg">{preview ?? '—'}</div>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={handleSave} disabled={saving}>
            {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {rule ? 'Save changes' : 'Create rule'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
