'use client';

import { useQuery } from '@tanstack/react-query';
import type { UseFormReturn } from 'react-hook-form';
import { z } from 'zod';
import { AlertTriangle } from 'lucide-react';
import { Label } from '@/components/ui/label';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { cn } from '@/lib/utils';
import {
  TextField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { trainingVendorService } from '@/services/hr/training-vendor.service';
import { trainerService } from '@/services/hr/trainer.service';
import { ORIENTATION_FACILITATOR_ROLE_OPTIONS } from '@/types/hr/orientation';
import type {
  OrientationSessionFacilitator,
  OrientationSessionFacilitatorCreateRequest,
} from '@/types/hr/orientation';

/**
 * Round 4, lane M — who delivers a session: one of our employees, a vendor (and its trainer, once
 * known) from the training vendor register, or somebody from outside typed by hand.
 *
 * A register pick sends only the two ids. The server takes the name, email and organisation from the
 * register as a SNAPSHOT when the pick is made or changed, so renaming the vendor later does not
 * rewrite what the session says — and ignores anything typed alongside. Editing a facilitator without
 * changing the pick (confirming, a note) keeps the snapshot as it was.
 */
export type FacilitatorMode = 'employee' | 'register' | 'other';

const NO_TRAINER = '__to_be_confirmed__';

export const facilitatorSchema = z
  .object({
    mode: z.enum(['employee', 'register', 'other']),
    employeeId: z.string().optional().or(z.literal('')),
    vendorId: z.string().optional().or(z.literal('')),
    trainerProfileId: z.string().optional().or(z.literal('')),
    externalFacilitatorName: z.string().max(200).optional().or(z.literal('')),
    externalFacilitatorEmail: z.string().max(200).optional().or(z.literal('')),
    externalFacilitatorOrganization: z.string().max(200).optional().or(z.literal('')),
    role: z.enum(['Lead', 'CoFacilitator', 'SubjectMatterExpert', 'Observer']),
    hasConfirmed: z.boolean(),
    notes: z.string().max(1000).optional().or(z.literal('')),
    // Carried for display only, never sent: the chosen employee's name (the picker cannot look it up
    // from an id, so a reopened dialog showed an empty search box) and the register's note on the pick.
    employeeName: z.string().optional().nullable(),
    registerNote: z.string().optional().nullable(),
  })
  // The server refuses each of these with a 422; catching them here says which field is missing.
  .superRefine((v, ctx) => {
    if (v.mode === 'employee' && !v.employeeId)
      ctx.addIssue({ code: 'custom', path: ['employeeId'], message: 'Pick the employee.' });
    if (v.mode === 'register' && !v.vendorId)
      ctx.addIssue({ code: 'custom', path: ['vendorId'], message: 'Pick the vendor.' });
    if (v.mode === 'other') {
      if (!v.externalFacilitatorName?.trim())
        ctx.addIssue({ code: 'custom', path: ['externalFacilitatorName'], message: 'Give their name.' });
      const email = v.externalFacilitatorEmail?.trim();
      if (email && !z.string().email().safeParse(email).success)
        ctx.addIssue({ code: 'custom', path: ['externalFacilitatorEmail'], message: 'That is not an email address.' });
    }
  });

export type FacilitatorForm = z.infer<typeof facilitatorSchema>;

export const emptyFacilitator: FacilitatorForm = {
  mode: 'employee',
  employeeId: '',
  vendorId: '',
  trainerProfileId: '',
  externalFacilitatorName: '',
  externalFacilitatorEmail: '',
  externalFacilitatorOrganization: '',
  role: 'Lead',
  hasConfirmed: false,
  notes: '',
  employeeName: null,
  registerNote: null,
};

const blank = (v?: string | null) => (v && v.trim().length > 0 ? v.trim() : null);

export const facilitatorModeOf = (f: OrientationSessionFacilitator): FacilitatorMode =>
  f.employeeId ? 'employee' : f.externalFacilitatorVendorId ? 'register' : 'other';

export function facilitatorToForm(f: OrientationSessionFacilitator): FacilitatorForm {
  return {
    mode: facilitatorModeOf(f),
    employeeId: f.employeeId ?? '',
    vendorId: f.externalFacilitatorVendorId ?? '',
    trainerProfileId: f.externalFacilitatorTrainerProfileId ?? '',
    externalFacilitatorName: f.externalFacilitatorName ?? '',
    externalFacilitatorEmail: f.externalFacilitatorEmail ?? '',
    externalFacilitatorOrganization: f.externalFacilitatorOrganization ?? '',
    role: f.role,
    hasConfirmed: f.hasConfirmed,
    notes: f.notes ?? '',
    employeeName: f.employeeName ?? null,
    registerNote: f.registerNote ?? null,
  };
}

/** The request for the chosen mode — every other mode's fields sent as null, so a switch clears them. */
export function facilitatorRequest(
  values: FacilitatorForm,
): Omit<OrientationSessionFacilitatorCreateRequest, 'sessionId'> {
  const typed = values.mode === 'other';
  return {
    employeeId: values.mode === 'employee' ? blank(values.employeeId) : null,
    externalFacilitatorVendorId: values.mode === 'register' ? blank(values.vendorId) : null,
    externalFacilitatorTrainerProfileId: values.mode === 'register' ? blank(values.trainerProfileId) : null,
    externalFacilitatorName: typed ? blank(values.externalFacilitatorName) : null,
    externalFacilitatorEmail: typed ? blank(values.externalFacilitatorEmail) : null,
    externalFacilitatorOrganization: typed ? blank(values.externalFacilitatorOrganization) : null,
    role: values.role,
    hasConfirmed: values.hasConfirmed,
    notes: blank(values.notes),
  };
}

/**
 * A facilitator exactly as it stands, for an update that changes one thing (the confirmed flag).
 * ⚠ The update writes every field, and a missing register id reads as "not from the register" —
 * so the pick must travel with it, or confirming would quietly turn it into a typed facilitator.
 */
export const facilitatorAsItStands = (
  f: OrientationSessionFacilitator,
): Omit<OrientationSessionFacilitatorCreateRequest, 'sessionId'> =>
  facilitatorRequest(facilitatorToForm(f));

/** The name to show for a facilitator: the person, or the vendor while it has yet to name one. */
export const facilitatorName = (f: OrientationSessionFacilitator) =>
  f.employeeName ?? f.externalFacilitatorName ?? f.externalFacilitatorOrganization ?? '—';

/** The line under the name. */
export function facilitatorSource(f: OrientationSessionFacilitator): string {
  if (f.employeeId) return 'Employee';
  if (f.externalFacilitatorVendorId) {
    const parts = [
      f.externalFacilitatorOrganization,
      f.externalFacilitatorTrainerProfileId ? null : 'trainer to be confirmed',
      f.externalFacilitatorEmail,
    ].filter(Boolean);
    return `From the training register · ${parts.join(' · ')}`;
  }
  return (
    [f.externalFacilitatorOrganization, f.externalFacilitatorEmail].filter(Boolean).join(' · ') ||
    'External'
  );
}

/** The register's warning about an earlier pick, if it has one. */
export function FacilitatorRegisterNote({ note }: { note?: string | null }) {
  if (!note) return null;
  return (
    <div className="mt-1 flex items-start gap-1 text-xs text-amber-700 dark:text-amber-400">
      <AlertTriangle className="mt-0.5 h-3 w-3 shrink-0" />
      <span>{note}</span>
    </div>
  );
}

const MODES: { value: FacilitatorMode; title: string; hint: string }[] = [
  { value: 'employee', title: 'One of our employees', hint: 'Picked from the employee list.' },
  {
    value: 'register',
    title: 'From the training vendor register',
    hint: 'A vendor on file, and its trainer once known.',
  },
  { value: 'other', title: 'Someone else', hint: 'Type their details — for a one-off speaker.' },
];

const errorOf = (form: UseFormReturn<FacilitatorForm>, name: keyof FacilitatorForm) =>
  form.formState.errors[name]?.message as string | undefined;

/**
 * The dialog's fields. What was saved — the pick, its snapshot, the register's note — is read from the
 * form's default values: the list panel resets the form from the row when it opens it for editing,
 * and hands its render prop only a boolean, not the row.
 */
export function FacilitatorFields({ form }: { form: UseFormReturn<FacilitatorForm> }) {
  const saved = form.formState.defaultValues;
  const mode = form.watch('mode');
  const vendorId = form.watch('vendorId') ?? '';
  const trainerProfileId = form.watch('trainerProfileId') ?? '';

  // The register lives with Training (HR.Training.Read). Every role that writes orientation reads
  // it today; a role that could not would get a 403 here, which is said — not drawn as "no vendors".
  const vendorsQuery = useQuery({
    queryKey: ['hr', 'training-vendors', 'active'],
    queryFn: () => trainingVendorService.getActive(),
    enabled: mode === 'register',
    retry: (count, error: any) => error?.status !== 403 && count < 2,
  });
  const trainersQuery = useQuery({
    queryKey: ['hr', 'trainers', 'vendor', vendorId],
    queryFn: () => trainerService.getByVendorId(vendorId),
    enabled: mode === 'register' && !!vendorId,
    retry: (count, error: any) => error?.status !== 403 && count < 2,
  });

  const vendors = vendorsQuery.data ?? [];
  const trainers = (trainersQuery.data ?? []).filter(
    (t) => t.isActive || t.id === trainerProfileId,
  );
  const registerRefused = (vendorsQuery.error as any)?.status === 403;
  // A vendor picked earlier may have been blacklisted or retired since — it is not in the active
  // list, so it is shown from what the row saved rather than as an empty select.
  const savedVendorMissing = !!vendorId && !vendors.some((v) => v.id === vendorId);
  const samePickAsSaved =
    saved?.mode === 'register' &&
    !!saved.vendorId &&
    saved.vendorId === vendorId &&
    (saved.trainerProfileId ?? '') === trainerProfileId;

  return (
    <>
      <fieldset className="space-y-2">
        <legend className="mb-1 text-sm font-medium">Who is it?</legend>
        <RadioGroup
          value={mode}
          onValueChange={(v) => form.setValue('mode', v as FacilitatorMode, { shouldValidate: false })}
          className="gap-1.5"
        >
          {MODES.map((m) => (
            <label
              key={m.value}
              className={cn(
                'flex cursor-pointer items-start gap-3 rounded-md border p-2.5 transition-colors hover:bg-accent',
                mode === m.value && 'border-primary bg-accent/60',
              )}
            >
              <RadioGroupItem value={m.value} className="mt-0.5" />
              <span className="min-w-0">
                <span className="block text-sm font-medium">{m.title}</span>
                <span className="text-muted-foreground block text-xs">{m.hint}</span>
              </span>
            </label>
          ))}
        </RadioGroup>
      </fieldset>

      {mode === 'employee' && (
        <EmployeePickerField
          form={form}
          name="employeeId"
          label="Employee"
          required
          initialLabel={saved?.employeeName ?? null}
        />
      )}

      {mode === 'register' &&
        (registerRefused ? (
          <p className="text-muted-foreground rounded-md border border-dashed p-3 text-sm">
            You cannot read the training vendor register (that needs the training read permission), so
            pick an employee or type their details instead.
          </p>
        ) : (
          <div className="space-y-3">
            <FieldRow>
              <div className="space-y-2">
                <Label htmlFor="facilitator-vendor">
                  Vendor <span className="text-destructive">*</span>
                </Label>
                <Select
                  value={vendorId || undefined}
                  onValueChange={(next) => {
                    form.setValue('vendorId', next, { shouldValidate: true });
                    // A trainer belongs to one vendor — changing the vendor clears them.
                    form.setValue('trainerProfileId', '');
                  }}
                >
                  <SelectTrigger id="facilitator-vendor">
                    <SelectValue
                      placeholder={vendorsQuery.isLoading ? 'Loading the register…' : 'Pick a vendor'}
                    />
                  </SelectTrigger>
                  <SelectContent>
                    {savedVendorMissing && (
                      <SelectItem value={vendorId}>
                        {saved?.externalFacilitatorOrganization || 'The vendor saved earlier'}
                      </SelectItem>
                    )}
                    {vendors.map((v) => (
                      <SelectItem key={v.id} value={v.id}>
                        {v.name}
                        {v.isPreferred ? ' · preferred' : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {errorOf(form, 'vendorId') && (
                  <p className="text-destructive text-xs">{errorOf(form, 'vendorId')}</p>
                )}
                {!vendorsQuery.isLoading && vendors.length === 0 && !vendorsQuery.error && (
                  <p className="text-muted-foreground text-xs">
                    The register has no active vendor. Add one under Training → Vendors.
                  </p>
                )}
                {vendorsQuery.error && !registerRefused && (
                  <p className="text-destructive text-xs">The register could not be loaded.</p>
                )}
              </div>
              <div className="space-y-2">
                <Label htmlFor="facilitator-trainer">Trainer</Label>
                <Select
                  value={trainerProfileId || NO_TRAINER}
                  onValueChange={(next) =>
                    form.setValue('trainerProfileId', next === NO_TRAINER ? '' : next)
                  }
                  disabled={!vendorId}
                >
                  <SelectTrigger id="facilitator-trainer">
                    <SelectValue placeholder="Pick the vendor first" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={NO_TRAINER}>To be confirmed by the vendor</SelectItem>
                    {trainers.map((t) => (
                      <SelectItem key={t.id} value={t.id}>
                        {t.name}
                        {t.isActive ? '' : ' · inactive'}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-muted-foreground text-xs">
                  {vendorId && !trainersQuery.isLoading && trainers.length === 0
                    ? 'This vendor has no active trainer on file.'
                    : 'Leave it to be confirmed while the vendor has not named anyone.'}
                </p>
              </div>
            </FieldRow>
            <p className="text-muted-foreground text-xs">
              {samePickAsSaved
                ? `Saved as: ${[saved?.externalFacilitatorName, saved?.externalFacilitatorOrganization, saved?.externalFacilitatorEmail].filter(Boolean).join(' · ')}. It stays as it was, even if the register has changed since.`
                : 'The name, email and organisation are taken from the register when you save, and kept as they are then — renaming the vendor later does not change this session.'}
            </p>
            <FacilitatorRegisterNote note={samePickAsSaved ? saved?.registerNote : null} />
          </div>
        ))}

      {mode === 'other' && (
        <>
          <FieldRow>
            <TextField form={form} name="externalFacilitatorName" label="Name" required />
            <TextField form={form} name="externalFacilitatorOrganization" label="Organisation" />
          </FieldRow>
          <TextField form={form} name="externalFacilitatorEmail" label="Email" />
        </>
      )}

      <FieldRow>
        <SelectField
          form={form}
          name="role"
          label="Role"
          required
          options={ORIENTATION_FACILITATOR_ROLE_OPTIONS}
        />
        <SwitchField form={form} name="hasConfirmed" label="Has confirmed" />
      </FieldRow>
      <TextareaField form={form} name="notes" label="Notes" rows={2} />
    </>
  );
}
