'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useToast } from '@/hooks/use-toast';
import { medicalClinicalService } from '@/services/hr/medical-clinical.service';
import {
  MEDICAL_SERVICE_TYPE_OPTIONS,
  REFERRAL_PRIORITY_OPTIONS,
} from '@/types/hr/medical';
import type {
  MedicalAppointmentDetail,
  MedicalPreAuthorizationDetail,
  MedicalReferralDetail,
  MedicalServiceType,
  MedicalReferralPriority,
} from '@/types/hr/medical';

/**
 * Edit and delete for the three clinical records, in one place because the three dialogs differ
 * only in their fields.
 *
 * ⚠ **Every dialog loads the record by id when it opens — it never binds to the list row.** The
 * list reads are summaries: an appointment row carries 7 fields against the record's 32, a
 * pre-authorisation row 9 against 34. Neither carries `purpose`, `serviceType`, `diagnosis` or
 * `proposedTreatment`, and all of those are written by the update. A dialog seeded from a row
 * would render them blank and then blank them in the database on save — the same defect as D-09
 * and D-12, one layer further out. The `enabled` flag below is what keeps that honest: no fetch,
 * no form.
 *
 * The updates are not patches. Every field is sent, so each form seeds all of them from the
 * fetched record even where it does not show them.
 */

type Kind = 'pre-authorization' | 'referral' | 'appointment';
type ClinicalRecordDetail =
  | MedicalPreAuthorizationDetail
  | MedicalReferralDetail
  | MedicalAppointmentDetail;

const QUERY_KEY: Record<Kind, string> = {
  'pre-authorization': 'medical-preauths',
  referral: 'medical-referrals',
  appointment: 'medical-appointments',
};

const NOUN: Record<Kind, string> = {
  'pre-authorization': 'pre-authorisation',
  referral: 'referral',
  appointment: 'appointment',
};

/** `datetime-local` wants `yyyy-MM-ddTHH:mm`; the API answers an ISO instant or a bare local time. */
const toLocalInput = (v?: string | null) => {
  if (!v) return '';
  const d = new Date(v);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

const toDateInput = (v?: string | null) => (v ? toLocalInput(v).slice(0, 10) : '');

interface Props {
  kind: Kind;
  id: string;
  /** The record's number, shown in both dialogs so the user knows what they are changing. */
  recordLabel: string;
  canWrite: boolean;
  canDelete: boolean;
}

export function ClinicalRecordActions({ kind, id, recordLabel, canWrite, canDelete }: Props) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [editing, setEditing] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);

  // Loaded only while the dialog is open — see the note above on why the row is not enough.
  const { data: record, isLoading } = useQuery<ClinicalRecordDetail>({
    queryKey: ['hr', QUERY_KEY[kind], id, 'detail'],
    enabled: editing,
    queryFn: async (): Promise<ClinicalRecordDetail> => {
      if (kind === 'pre-authorization') {
        return medicalClinicalService.getPreAuthorization(id);
      }
      if (kind === 'referral') {
        return medicalClinicalService.getReferral(id);
      }
      return medicalClinicalService.getAppointment(id);
    },
  });

  const [form, setForm] = useState<Record<string, string | boolean>>({});
  const set = (k: string, v: string | boolean) => setForm((f) => ({ ...f, [k]: v }));

  useEffect(() => {
    if (!record) return;
    if (kind === 'pre-authorization' && 'proposedTreatment' in record) {
      setForm({
        facilityId: record.facilityId ?? '',
        physicianId: record.physicianId ?? '',
        serviceType: record.serviceType,
        isEmergency: record.isEmergency,
        diagnosis: record.diagnosis ?? '',
        proposedTreatment: record.proposedTreatment ?? '',
        plannedServiceDate: toDateInput(record.plannedServiceDate),
        estimatedCost: record.estimatedCost === null || record.estimatedCost === undefined
          ? '' : String(record.estimatedCost),
        notes: record.notes ?? '',
      });
    } else if (kind === 'referral' && 'reasonForReferral' in record) {
      setForm({
        referredToFacilityId: record.referredToFacilityId ?? '',
        referredToPhysicianId: record.referredToPhysicianId ?? '',
        expiryDate: toDateInput(record.expiryDate),
        priority: record.priority,
        diagnosis: record.diagnosis ?? '',
        reasonForReferral: record.reasonForReferral ?? '',
        notes: record.notes ?? '',
      });
    } else if (kind === 'appointment' && 'purpose' in record) {
      setForm({
        physicianId: record.physicianId ?? '',
        appointmentDateTime: toLocalInput(record.appointmentDateTime),
        durationMinutes: record.durationMinutes === null || record.durationMinutes === undefined
          ? '' : String(record.durationMinutes),
        serviceType: record.serviceType,
        purpose: record.purpose ?? '',
        notes: record.notes ?? '',
      });
    }
  }, [record, kind]);

  const done = (message: string) => {
    queryClient.invalidateQueries({ queryKey: ['hr', QUERY_KEY[kind]] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'medical-dashboard', 30] });
    toast({ title: message });
  };
  const onError = (error: unknown) =>
    toast({
      variant: 'destructive',
      title: 'Could not save',
      description: error instanceof Error ? error.message : 'Unexpected error',
    });

  const str = (k: string) => String(form[k] ?? '');
  const orNull = (k: string) => (str(k).trim() === '' ? null : str(k));
  const numOrNull = (k: string) => (str(k).trim() === '' ? null : Number(str(k)));

  const save = useMutation<void, Error>({
    mutationFn: async (): Promise<void> => {
      if (kind === 'pre-authorization') {
        await medicalClinicalService.updatePreAuthorization(id, {
          id,
          facilityId: orNull('facilityId'),
          physicianId: orNull('physicianId'),
          serviceType: str('serviceType') as MedicalServiceType,
          isEmergency: Boolean(form.isEmergency),
          diagnosis: str('diagnosis'),
          proposedTreatment: str('proposedTreatment'),
          plannedServiceDate: orNull('plannedServiceDate'),
          estimatedCost: numOrNull('estimatedCost'),
          notes: orNull('notes'),
        });
        return;
      }
      if (kind === 'referral') {
        await medicalClinicalService.updateReferral(id, {
          id,
          referredToFacilityId: orNull('referredToFacilityId'),
          referredToPhysicianId: orNull('referredToPhysicianId'),
          expiryDate: orNull('expiryDate'),
          priority: str('priority') as MedicalReferralPriority,
          diagnosis: orNull('diagnosis'),
          reasonForReferral: str('reasonForReferral'),
          notes: orNull('notes'),
        });
        return;
      }
      await medicalClinicalService.updateAppointment(id, {
        id,
        physicianId: orNull('physicianId'),
        appointmentDateTime: str('appointmentDateTime'),
        durationMinutes: numOrNull('durationMinutes'),
        serviceType: str('serviceType') as MedicalServiceType,
        purpose: str('purpose'),
        notes: orNull('notes'),
      });
    },
    onSuccess: () => {
      setEditing(false);
      done(`${recordLabel} updated.`);
    },
    onError,
  });

  const remove = useMutation({
    mutationFn: () =>
      kind === 'pre-authorization'
        ? medicalClinicalService.deletePreAuthorization(id)
        : kind === 'referral'
          ? medicalClinicalService.deleteReferral(id)
          : medicalClinicalService.deleteAppointment(id),
    onSuccess: () => {
      setConfirmingDelete(false);
      done(`${recordLabel} deleted.`);
    },
    onError,
  });

  if (!canWrite && !canDelete) return null;

  return (
    <>
      {canWrite && (
        <Button variant="ghost" size="sm" onClick={() => setEditing(true)}>
          Edit
        </Button>
      )}
      {canDelete && (
        <Button variant="ghost" size="sm" onClick={() => setConfirmingDelete(true)}>
          Delete
        </Button>
      )}

      <Dialog open={editing} onOpenChange={(o) => !o && setEditing(false)}>
        <DialogContent className="max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit {recordLabel}</DialogTitle>
            <DialogDescription>
              Every field is written on save, so anything cleared here is cleared on the record.
            </DialogDescription>
          </DialogHeader>

          {isLoading || !record ? (
            <div className="space-y-3">
              <Skeleton className="h-9 w-full" />
              <Skeleton className="h-9 w-full" />
              <Skeleton className="h-20 w-full" />
            </div>
          ) : (
            <div className="space-y-4">
              {kind === 'pre-authorization' && (
                <>
                  <div className="space-y-2">
                    <Label>Service type</Label>
                    <Select value={str('serviceType')} onValueChange={(v) => set('serviceType', v)}>
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectContent>
                        {MEDICAL_SERVICE_TYPE_OPTIONS.map((o) => (
                          <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="flex items-center justify-between rounded-md border p-3">
                    <Label htmlFor="isEmergency">Emergency</Label>
                    <Switch
                      id="isEmergency"
                      checked={Boolean(form.isEmergency)}
                      onCheckedChange={(v) => set('isEmergency', v)}
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>Diagnosis</Label>
                    <Textarea value={str('diagnosis')} onChange={(e) => set('diagnosis', e.target.value)} />
                  </div>
                  <div className="space-y-2">
                    <Label>Proposed treatment</Label>
                    <Textarea
                      value={str('proposedTreatment')}
                      onChange={(e) => set('proposedTreatment', e.target.value)}
                    />
                  </div>
                  <div className="grid grid-cols-2 gap-3">
                    <div className="space-y-2">
                      <Label>Planned service date</Label>
                      <Input
                        type="date"
                        value={str('plannedServiceDate')}
                        onChange={(e) => set('plannedServiceDate', e.target.value)}
                      />
                    </div>
                    <div className="space-y-2">
                      <Label>Estimated cost</Label>
                      <Input
                        type="number"
                        step="0.01"
                        value={str('estimatedCost')}
                        onChange={(e) => set('estimatedCost', e.target.value)}
                      />
                    </div>
                  </div>
                </>
              )}

              {kind === 'referral' && (
                <>
                  <div className="space-y-2">
                    <Label>Priority</Label>
                    <Select value={str('priority')} onValueChange={(v) => set('priority', v)}>
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectContent>
                        {REFERRAL_PRIORITY_OPTIONS.map((o) => (
                          <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label>Reason for referral</Label>
                    <Textarea
                      value={str('reasonForReferral')}
                      onChange={(e) => set('reasonForReferral', e.target.value)}
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>Diagnosis</Label>
                    <Textarea value={str('diagnosis')} onChange={(e) => set('diagnosis', e.target.value)} />
                  </div>
                  <div className="space-y-2">
                    <Label>Expiry date</Label>
                    <Input
                      type="date"
                      value={str('expiryDate')}
                      onChange={(e) => set('expiryDate', e.target.value)}
                    />
                  </div>
                </>
              )}

              {kind === 'appointment' && (
                <>
                  <div className="grid grid-cols-2 gap-3">
                    <div className="space-y-2">
                      <Label>Date and time</Label>
                      <Input
                        type="datetime-local"
                        value={str('appointmentDateTime')}
                        onChange={(e) => set('appointmentDateTime', e.target.value)}
                      />
                    </div>
                    <div className="space-y-2">
                      <Label>Duration (minutes)</Label>
                      <Input
                        type="number"
                        value={str('durationMinutes')}
                        onChange={(e) => set('durationMinutes', e.target.value)}
                      />
                    </div>
                  </div>
                  <div className="space-y-2">
                    <Label>Service type</Label>
                    <Select value={str('serviceType')} onValueChange={(v) => set('serviceType', v)}>
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectContent>
                        {MEDICAL_SERVICE_TYPE_OPTIONS.map((o) => (
                          <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label>Purpose</Label>
                    <Textarea value={str('purpose')} onChange={(e) => set('purpose', e.target.value)} />
                  </div>
                </>
              )}

              <div className="space-y-2">
                <Label>Notes</Label>
                <Textarea value={str('notes')} onChange={(e) => set('notes', e.target.value)} />
              </div>
            </div>
          )}

          <DialogFooter>
            <Button variant="outline" onClick={() => setEditing(false)}>Cancel</Button>
            <Button onClick={() => save.mutate()} disabled={save.isPending || isLoading || !record}>
              {save.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={confirmingDelete} onOpenChange={(o) => !o && setConfirmingDelete(false)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete {recordLabel}?</DialogTitle>
            <DialogDescription>
              This removes the {NOUN[kind]} from the register. Anything already pointing at it — a
              claim raised against it, for instance — keeps its own record.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmingDelete(false)}>Cancel</Button>
            <Button
              variant="destructive"
              onClick={() => remove.mutate()}
              disabled={remove.isPending}
            >
              {remove.isPending ? 'Deleting…' : 'Delete'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
