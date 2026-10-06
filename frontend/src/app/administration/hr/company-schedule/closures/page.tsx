'use client';

import { useState } from 'react';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Megaphone, X } from 'lucide-react';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { OrganizationUnitPickerField } from '@/components/hr/common/OrganizationUnitPickerField';
import {
  DateField,
  FieldRow,
  SelectField,
  SwitchField,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import {
  ClosureAnnounceDialog,
  closureAnnouncementKey,
} from '@/components/hr/company-schedule/ClosureAnnounceDialog';
import { businessClosureService } from '@/services/hr/company-schedule.service';
import { useSiteOptions } from '@/components/hr/company-schedule/siteOptions';
import { useAuth } from '@/hooks/use-auth';
import { CLOSURE_TYPE_OPTIONS } from '@/types/hr/company-schedule';
import type {
  BusinessClosure,
  ClosureType,
  CreateBusinessClosure,
  LeaveRechargeResult,
} from '@/types/hr/company-schedule';

const orNull = (s?: string) => (s && s.trim() ? s.trim() : null);
const today = () => new Date().toISOString().slice(0, 10);
const day = (iso: string) =>
  new Date(`${iso.slice(0, 10)}T00:00:00`).toLocaleDateString('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  });
const typeLabel = (t: ClosureType) => CLOSURE_TYPE_OPTIONS.find((o) => o.value === t)?.label ?? t;
const isNonWorking = (t: ClosureType | string) => t !== 'PartialClosure';
/** Over once its last day has passed — never, for a closure that recurs every year. */
const isOver = (c: BusinessClosure) => !c.recursAnnually && c.endDate.slice(0, 10) < today();

/** A partial closure covers exactly ONE scope (1a): the audience resolver unions its rules, so
 *  "this unit at that site" cannot be expressed. Every other type takes its scope from the type. */
type PartialScope = 'company' | 'site' | 'unit';

const schema = z
  .object({
    title: z.string().min(1, 'Title is required').max(200),
    reason: z.string().max(1000).optional().or(z.literal('')),
    startDate: z.string().min(1, 'Start date is required'),
    endDate: z.string().min(1, 'End date is required'),
    type: z.enum(['FullClosure', 'PartialClosure', 'DepartmentClosure', 'StationClosure']),
    partialScope: z.enum(['company', 'site', 'unit']),
    locationId: z.string().optional().or(z.literal('')),
    organizationUnitId: z.string().optional().or(z.literal('')),
    recursAnnually: z.boolean(),
    isPaidClosure: z.boolean(),
    communicationNotes: z.string().max(1000).optional().or(z.literal('')),
  })
  .superRefine((v, ctx) => {
    if (v.endDate < v.startDate)
      ctx.addIssue({ code: 'custom', path: ['endDate'], message: 'The last day cannot be before the first' });
    if (v.recursAnnually && v.endDate >= v.startDate) {
      const days = (Date.parse(v.endDate) - Date.parse(v.startDate)) / 86_400_000;
      if (days >= 365)
        ctx.addIssue({
          code: 'custom',
          path: ['recursAnnually'],
          message: 'A closure that recurs every year has to be shorter than a year',
        });
    }
    const needsSite = v.type === 'StationClosure' || (v.type === 'PartialClosure' && v.partialScope === 'site');
    const needsUnit = v.type === 'DepartmentClosure' || (v.type === 'PartialClosure' && v.partialScope === 'unit');
    if (needsSite && !v.locationId)
      ctx.addIssue({ code: 'custom', path: ['locationId'], message: 'Choose the site' });
    if (needsUnit && !v.organizationUnitId)
      ctx.addIssue({ code: 'custom', path: ['organizationUnitId'], message: 'Choose the organisation unit' });
  });

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  title: '',
  reason: '',
  startDate: today(),
  endDate: today(),
  type: 'FullClosure',
  partialScope: 'company',
  locationId: '',
  organizationUnitId: '',
  recursAnnually: false,
  isPaidClosure: true,
  communicationNotes: '',
};

/**
 * The form's values as the API takes them. Only the scope the type uses is sent — a site left in the
 * form from an earlier choice of type would otherwise be refused as a contradiction. The two derived
 * flags follow the type; the server sets them the same way whatever is sent.
 */
function toPayload(v: FormValues): CreateBusinessClosure {
  const partial = v.type === 'PartialClosure';
  const useSite = v.type === 'StationClosure' || (partial && v.partialScope === 'site');
  const useUnit = v.type === 'DepartmentClosure' || (partial && v.partialScope === 'unit');
  return {
    title: v.title.trim(),
    reason: orNull(v.reason),
    startDate: v.startDate,
    endDate: v.endDate,
    type: v.type,
    affectsAllStations: v.type === 'FullClosure' || (partial && v.partialScope === 'company'),
    locationId: useSite ? orNull(v.locationId) : null,
    organizationUnitId: useUnit ? orNull(v.organizationUnitId) : null,
    recursAnnually: v.recursAnnually,
    isPaidClosure: v.isPaidClosure,
    countsAsWorkingDay: partial,
    communicationNotes: orNull(v.communicationNotes),
  };
}

/** What the last save or removal did, kept on screen until dismissed — a toast is gone too soon to
 *  read a list of recounted leave. */
interface LastResult {
  verb: 'added' | 'updated' | 'removed';
  title: string;
  /** The saved closure, when it may be announced; absent after a removal. */
  offer?: BusinessClosure;
  warnings: string[];
  recharge?: LeaveRechargeResult | null;
}

/**
 * Business closures — the days the organisation, a site or a unit is shut (company-schedule final
 * closure, lane 1).
 *
 * - **The type decides the scope** (D-1): whole company, one site, one organisation unit with
 *   everything beneath it, or reduced operations for exactly one of those. Only the matching
 *   picker renders, and the server refuses a contradiction.
 * - **Whether the day is worked follows the type**, so the switch is shown locked: a closure is a
 *   day off — leave over it is not charged — and reduced operations are a working day.
 * - **Saving recounts approved leave** over the changed days (lane 1c); the panel above the list
 *   shows what was recounted, what was left for HR, and the save's warnings.
 * - **Announcing is a separate click** (L1-1): the panel offers it after a save, and every row
 *   that is not over has it in its menu.
 *
 * ⚠ **No recorded-by field** — the API records whoever saves it from the token.
 */
export default function BusinessClosuresPage() {
  // Lane 5a (C-16): the places staff can be placed, not the whole location tree.
  const sites = useSiteOptions();
  // Lane 5a (R4-6.6, F-20): removing a closure is Admin (`DELETE closures/{id}`) — not offered to the HR desk.
  const canDelete = useAuth().hasPermission('HR.Company.Admin');

  const [last, setLast] = useState<LastResult | null>(null);
  const [announcing, setAnnouncing] = useState<BusinessClosure | null>(null);

  const remember = (verb: 'added' | 'updated', saved: BusinessClosure) => {
    setLast({
      verb,
      title: saved.title,
      offer: isNonWorking(saved.type) && !isOver(saved) ? saved : undefined,
      warnings: saved.warnings ?? [],
      recharge: saved.leaveRecharge,
    });
    return saved;
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Business closures"
        description="Days the company, a site or an organisation unit is closed, or runs reduced operations."
        backHref="/administration/hr/company-schedule"
      />

      {last && (
        <SaveResultPanel
          result={last}
          onDismiss={() => setLast(null)}
          onAnnounce={(c) => setAnnouncing(c)}
        />
      )}

      <ResourceCollectionTab<BusinessClosure, FormValues>
        parentId="all"
        title="closures"
        singular="closure"
        queryKey={['hr', 'company-schedule', 'closures']}
        dialogHint="You are recorded as the person who entered it. Telling staff is a separate step: announce it once it is final."
        emptyDescription="No closures recorded yet."
        list={() => businessClosureService.getAll()}
        create={async (_p, v) => remember('added', await businessClosureService.create(toPayload(v)))}
        update={async (_p, id, v) =>
          remember('updated', await businessClosureService.update(id, { ...toPayload(v), id }))
        }
        allowRemove={canDelete}
        remove={async (_p, id) => {
          const removed = await businessClosureService.remove(id);
          setLast({ verb: 'removed', title: 'The closure', warnings: [], recharge: removed });
          return removed;
        }}
        savedDescription={(saved) => {
          const c = saved as BusinessClosure;
          const parts: string[] = [];
          if (c.warnings?.length) parts.push(`${c.warnings.length} warning${c.warnings.length === 1 ? '' : 's'}`);
          const n = c.leaveRecharge?.recharged.length ?? 0;
          if (n) parts.push(`${n} leave request${n === 1 ? '' : 's'} recounted`);
          return parts.length ? `${parts.join(' · ')} — see the summary above the list.` : null;
        }}
        getId={(c) => c.id}
        itemLabel={(c) => c.title}
        dialogClassName="sm:max-w-[640px]"
        actions={[
          {
            label: 'Announce to staff…',
            visible: (c) => !isOver(c),
            run: async (c) => setAnnouncing(c),
          },
        ]}
        columns={[
          { header: 'Title', cell: (c) => c.title },
          { header: 'Kind', cell: (c) => typeLabel(c.type) },
          {
            header: 'When',
            cell: (c) => (
              <span>
                {c.startDate.slice(0, 10) === c.endDate.slice(0, 10)
                  ? day(c.startDate)
                  : `${day(c.startDate)} → ${day(c.endDate)}`}
                {c.recursAnnually && <span className="block text-xs text-muted-foreground">Every year</span>}
              </span>
            ),
          },
          { header: 'Covers', cell: (c) => c.scopeDescription || '—' },
          { header: 'Working day', cell: (c) => (c.countsAsWorkingDay ? 'Yes — reduced operations' : 'No — a day off') },
          { header: 'Paid', cell: (c) => <StatusBadge status={c.isPaidClosure ? 'Paid' : 'Unpaid'} /> },
          { header: 'Recorded by', cell: (c) => c.announcedByName || '—' },
        ]}
        schema={schema}
        emptyForm={empty}
        toForm={(c) => ({
          title: c.title,
          reason: c.reason ?? '',
          startDate: c.startDate.slice(0, 10),
          endDate: c.endDate.slice(0, 10),
          type: c.type,
          partialScope: c.organizationUnitId ? 'unit' : c.locationId ? 'site' : 'company',
          locationId: c.locationId ?? '',
          organizationUnitId: c.organizationUnitId ?? '',
          recursAnnually: c.recursAnnually,
          isPaidClosure: c.isPaidClosure,
          communicationNotes: c.communicationNotes ?? '',
        })}
        renderFields={(form) => {
          const type = form.watch('type');
          const partialScope = form.watch('partialScope') as PartialScope;
          const partial = type === 'PartialClosure';
          const showSite = type === 'StationClosure' || (partial && partialScope === 'site');
          const showUnit = type === 'DepartmentClosure' || (partial && partialScope === 'unit');
          return (
            <>
              <TextField form={form} name="title" label="Title" required />
              <SelectField
                form={form}
                name="type"
                label="What closes"
                required
                options={CLOSURE_TYPE_OPTIONS.map((o) => ({ value: o.value, label: o.label }))}
                description={CLOSURE_TYPE_OPTIONS.find((o) => o.value === type)?.hint}
              />
              {partial && (
                <SelectField
                  form={form}
                  name="partialScope"
                  label="Reduced operations for"
                  required
                  options={[
                    { value: 'company', label: 'The whole company' },
                    { value: 'site', label: 'One site' },
                    { value: 'unit', label: 'One organisation unit' },
                  ]}
                  description="One scope only — the whole company, a site, or a unit with everything beneath it."
                />
              )}
              {showSite && (
                <SelectField
                  form={form}
                  name="locationId"
                  label="Site"
                  required
                  options={sites.optionsFor(form.watch('locationId'))}
                  placeholder={sites.isLoading ? 'Loading sites…' : 'Select…'}
                  description="Covers the staff based at exactly this site. Only the places staff are assigned to are offered."
                />
              )}
              {showUnit && (
                <OrganizationUnitPickerField
                  form={form}
                  name="organizationUnitId"
                  label="Organisation unit"
                  required
                  hint="Covers the unit and every unit beneath it, wherever their staff sit."
                />
              )}
              <FieldRow>
                <DateField form={form} name="startDate" label="First day" required />
                <DateField form={form} name="endDate" label="Last day" required />
              </FieldRow>
              <SwitchField
                form={form}
                name="recursAnnually"
                label="Recurs every year"
                description="The same dates every year from the first — the year-end stocktake is typed once."
              />
              <FieldRow>
                <SwitchField
                  form={form}
                  name="isPaidClosure"
                  label="Staff are paid"
                  description="Recorded for payroll, which decides what an unpaid day is worth."
                />
                <LockedSwitch
                  label="A working day"
                  checked={partial}
                  description={
                    partial
                      ? 'Reduced operations are still a working day: leave over them is charged.'
                      : 'A closure is a day off for the staff it covers: leave over it is not charged.'
                  }
                />
              </FieldRow>
              <TextareaField form={form} name="reason" label="Reason" />
              <TextareaField form={form} name="communicationNotes" label="Notes on how staff were told" />
            </>
          );
        }}
      />

      <ClosureAnnounceDialog
        closureId={announcing?.id ?? null}
        closureTitle={announcing?.title}
        open={!!announcing}
        onOpenChange={(open) => !open && setAnnouncing(null)}
        onAnnounced={() => setLast((l) => (l && l.offer?.id === announcing?.id ? { ...l, offer: undefined } : l))}
      />
    </div>
  );
}

/** A switch that shows a value the type decides — locked, with the reason under the label. */
function LockedSwitch({ label, checked, description }: { label: string; checked: boolean; description: string }) {
  return (
    <div className="flex items-center justify-between rounded-md border p-3">
      <div className="space-y-0.5">
        <Label>{label}</Label>
        <p className="text-xs text-muted-foreground">{description}</p>
      </div>
      <Switch checked={checked} disabled aria-readonly />
    </div>
  );
}

/** What the last save or removal did: its warnings, the leave it recounted, and the offer to announce. */
function SaveResultPanel({
  result,
  onDismiss,
  onAnnounce,
}: {
  result: LastResult;
  onDismiss: () => void;
  onAnnounce: (closure: BusinessClosure) => void;
}) {
  const { offer, recharge, warnings } = result;
  const { data: preview } = useQuery({
    queryKey: closureAnnouncementKey(offer?.id ?? ''),
    queryFn: () => businessClosureService.getAnnouncementPreview(offer?.id ?? ''),
    enabled: !!offer,
  });

  return (
    <Card>
      <CardContent className="space-y-4 pt-6">
        <div className="flex items-start justify-between gap-4">
          <p className="font-medium">
            {result.verb === 'removed' ? 'The closure was removed.' : `“${result.title}” was ${result.verb}.`}
          </p>
          <Button variant="ghost" size="icon" onClick={onDismiss} aria-label="Dismiss">
            <X className="h-4 w-4" />
          </Button>
        </div>

        {warnings.length > 0 && (
          <ul className="space-y-1 rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-100">
            {warnings.map((w) => (
              <li key={w}>⚠ {w}</li>
            ))}
          </ul>
        )}

        {recharge && (
          <RechargeSummary
            recharge={recharge}
            retry={
              result.verb === 'removed'
                ? 'the one-time leave recount for existing closures (Company Admin) retries them'
                : 'saving the closure again retries them'
            }
          />
        )}

        {offer &&
          (preview && !preview.canAnnounce ? (
            <p className="text-sm text-muted-foreground">
              No active staff are covered by this closure, so there is nobody to tell.
            </p>
          ) : (
            <div className="flex flex-wrap items-center gap-3">
              <Button onClick={() => onAnnounce(offer)} disabled={!preview}>
                <Megaphone className="mr-2 h-4 w-4" />
                {preview ? `Announce to the ${preview.staffCovered} staff it covers` : 'Announce to staff'}
              </Button>
              <span className="text-xs text-muted-foreground">
                Nothing is sent until you confirm. Leave it until the dates are final.
              </span>
            </div>
          ))}
      </CardContent>
    </Card>
  );
}

function RechargeSummary({ recharge, retry }: { recharge: LeaveRechargeResult; retry: string }) {
  const line = (l: LeaveRechargeResult['recharged'][number]) =>
    `${l.requestNumber} · ${l.employeeName} · ${l.leaveTypeName}, ${day(l.startDate)} → ${day(l.endDate)}: ${l.oldDays} → ${l.newDays} day${l.newDays === 1 ? '' : 's'}`;

  if (!recharge.recharged.length && !recharge.notRecharged.length && !recharge.failures.length)
    return <p className="text-sm text-muted-foreground">No approved leave changed with these days.</p>;

  return (
    <div className="space-y-3 text-sm">
      {recharge.recharged.length > 0 && (
        <div>
          <p className="font-medium">
            Leave recounted ({recharge.recharged.length}) — balances and attendance updated, and each employee told:
          </p>
          <ul className="ml-4 list-disc text-muted-foreground">
            {recharge.recharged.map((l) => (
              <li key={l.requestId}>{line(l)}</li>
            ))}
          </ul>
        </div>
      )}
      {recharge.notRecharged.length > 0 && (
        <div>
          <p className="font-medium">
            Left as charged ({recharge.notRecharged.length}) — in a finished leave year, whose unused days may already
            have been carried over. Adjust these by hand:
          </p>
          <ul className="ml-4 list-disc text-muted-foreground">
            {recharge.notRecharged.map((l) => (
              <li key={l.requestId}>{line(l)}</li>
            ))}
          </ul>
        </div>
      )}
      {recharge.failures.length > 0 && (
        <div className="text-red-600">
          <p className="font-medium">Could not be recounted ({recharge.failures.length}) — {retry}:</p>
          <ul className="ml-4 list-disc">
            {recharge.failures.map((f) => (
              <li key={f}>{f}</li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
