'use client';

import { useState } from 'react';
import { z } from 'zod';
import { useQueryClient } from '@tanstack/react-query';
import { Paperclip, ShieldCheck } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
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
import { useToast } from '@/components/ui/use-toast';
import { AttachFileDialog } from '@/components/hr/common/AttachFileDialog';
import { CertificationPicker } from '@/components/hr/common/CertificationPicker';
import { CertificationComplianceCard } from '@/components/hr/employee/CertificationComplianceCard';
import { certificationService } from '@/services/hr/certification.service';
import { employeeDocumentService } from '@/services/hr/employee-document.service';
import { hrDocumentService } from '@/services/hr/hr-document.service';
import type { EmployeeCertification, EmployeeCertificationStatus } from '@/types/hr/certification';
import { EmployeeSubResourceTab } from './EmployeeSubResourceTab';
import { DateField, FieldRow, TextField, TextareaField } from './fields';

const schema = z.object({
  certificationId: z.string().min(1, 'Choose the certification'),
  certificateNumber: z.string().max(100).optional().or(z.literal('')),
  issuedOn: z.string().optional().or(z.literal('')),
  expiresOn: z.string().optional().or(z.literal('')),
  notes: z.string().max(1000).optional().or(z.literal('')),
});

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  certificationId: '',
  certificateNumber: '',
  issuedOn: '',
  expiresOn: '',
  notes: '',
};

const STATUS_STYLES: Record<EmployeeCertificationStatus, string> = {
  Valid: 'bg-emerald-100 text-emerald-800 hover:bg-emerald-100',
  ExpiringSoon: 'bg-amber-100 text-amber-800 hover:bg-amber-100',
  Expired: 'bg-red-100 text-red-800 hover:bg-red-100',
  Revoked: 'bg-muted text-muted-foreground hover:bg-muted',
};

const STATUS_LABELS: Record<EmployeeCertificationStatus, string> = {
  Valid: 'Valid',
  ExpiringSoon: 'Expiring soon',
  Expired: 'Expired',
  Revoked: 'Revoked',
};

/**
 * What the employee holds (demo feedback round 2, lane C2): each credential from the catalogue,
 * its number and dates, its evidence file through the gate, verification by someone other than
 * the holder, and revocation with a reason. The compliance card above it sets the list against
 * what the position requires.
 *
 * Expiry left blank is computed by the server from the issue date and the catalogue's validity.
 */
export function CertificationsTab({ employeeId }: { employeeId: string }) {
  const qc = useQueryClient();
  const { toast } = useToast();
  const [evidenceFor, setEvidenceFor] = useState<EmployeeCertification | null>(null);
  const [revoking, setRevoking] = useState<EmployeeCertification | null>(null);
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);

  const refresh = () => {
    void qc.invalidateQueries({ queryKey: ['hr', 'employees', employeeId, 'certifications'] });
    void qc.invalidateQueries({ queryKey: ['hr', 'employees', employeeId, 'certification-compliance'] });
    void qc.invalidateQueries({ queryKey: ['hr', 'employees', employeeId, 'skills'] });
  };

  const downloadEvidence = async (c: EmployeeCertification) => {
    try {
      await hrDocumentService.download(
        employeeDocumentService.certificationEvidenceUrl(c.id),
        c.evidenceFileName ?? 'certification-evidence',
      );
    } catch {
      toast({ variant: 'destructive', title: 'Could not download that file' });
    }
  };

  const revoke = async () => {
    if (!revoking || !reason.trim()) return;
    setSaving(true);
    try {
      await certificationService.revokeEmployeeCertification(employeeId, revoking.id, { reason: reason.trim() });
      toast({ title: 'Credential revoked' });
      setRevoking(null);
      setReason('');
      refresh();
    } catch (e) {
      toast({ variant: 'destructive', title: 'Not revoked', description: e instanceof Error ? e.message : 'Refused' });
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-4">
      <CertificationComplianceCard employeeId={employeeId} />

      <EmployeeSubResourceTab<EmployeeCertification, FormValues>
        employeeId={employeeId}
        title="certifications"
        singular="certification"
        queryKey="certifications"
        getId={(c) => c.id}
        list={(id) => certificationService.getEmployeeCertifications(id)}
        create={(id, v) =>
          certificationService.addEmployeeCertification(id, {
            employeeId: id,
            certificationId: v.certificationId,
            certificateNumber: v.certificateNumber || null,
            issuedOn: v.issuedOn || null,
            expiresOn: v.expiresOn || null,
            notes: v.notes || null,
          })
        }
        // The certification itself is fixed once recorded; a different credential is a new row.
        update={(id, rowId, v) =>
          certificationService.updateEmployeeCertification(id, rowId, {
            id: rowId,
            certificateNumber: v.certificateNumber || null,
            issuedOn: v.issuedOn || null,
            expiresOn: v.expiresOn || null,
            notes: v.notes || null,
          })
        }
        remove={(id, rowId) => certificationService.removeEmployeeCertification(id, rowId)}
        actions={[
          {
            label: (c) => (c.hasEvidence ? 'Evidence…' : 'Attach evidence…'),
            run: async (c) => setEvidenceFor(c),
          },
          {
            label: 'Mark verified',
            visible: (c) => !c.isVerified && !c.isRevoked,
            run: async (c) => {
              await certificationService.verifyEmployeeCertification(employeeId, c.id);
              refresh();
            },
          },
          {
            label: 'Revoke…',
            visible: (c) => !c.isRevoked,
            destructive: true,
            run: async (c) => setRevoking(c),
          },
        ]}
        columns={[
          {
            header: 'Certification',
            cell: (c) => (
              <div>
                <div className="font-medium">{c.certificationName}</div>
                <div className="text-xs text-muted-foreground">
                  {c.certifyingBodyName}
                  {c.kind !== 'Certification' ? ` · ${c.kind}` : ''}
                </div>
              </div>
            ),
          },
          { header: 'Number', cell: (c) => c.certificateNumber || '—' },
          { header: 'Issued', cell: (c) => (c.issuedOn ? c.issuedOn.slice(0, 10) : '—') },
          {
            header: 'Expires',
            cell: (c) =>
              c.expiresOn ? (
                <span className={c.status === 'Expired' ? 'text-red-600' : undefined}>
                  {c.expiresOn.slice(0, 10)}
                  {c.daysUntilExpiry != null && c.daysUntilExpiry >= 0 && c.status === 'ExpiringSoon'
                    ? ` (${c.daysUntilExpiry}d)`
                    : ''}
                </span>
              ) : (
                'Does not expire'
              ),
          },
          {
            header: 'Status',
            cell: (c) => (
              <div className="flex flex-wrap gap-1">
                <Badge className={STATUS_STYLES[c.status]}>{STATUS_LABELS[c.status]}</Badge>
                {c.isVerified && (
                  <Badge variant="secondary" title={c.verifiedByName ? `Verified by ${c.verifiedByName}` : undefined}>
                    <ShieldCheck className="mr-1 h-3 w-3" /> Verified
                  </Badge>
                )}
              </div>
            ),
          },
          {
            header: 'Evidence',
            cell: (c) =>
              c.hasEvidence ? (
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-7 px-2"
                  onClick={(e) => {
                    e.stopPropagation();
                    void downloadEvidence(c);
                  }}
                  title={c.evidenceFileName ?? 'Download'}
                >
                  <Paperclip className="mr-1 h-3.5 w-3.5" /> file
                </Button>
              ) : (
                <span className="text-xs text-muted-foreground">none</span>
              ),
          },
        ]}
        schema={schema}
        emptyForm={empty}
        toForm={(c) => ({
          certificationId: c.certificationId,
          certificateNumber: c.certificateNumber ?? '',
          issuedOn: c.issuedOn?.slice(0, 10) ?? '',
          expiresOn: c.expiresOn?.slice(0, 10) ?? '',
          notes: c.notes ?? '',
        })}
        renderFields={(form) => (
          <>
            <CertificationPicker
              idPrefix="employee-certification"
              value={form.watch('certificationId')}
              onChange={(id) => form.setValue('certificationId', id, { shouldValidate: true, shouldDirty: true })}
              error={form.formState.errors.certificationId?.message as string | undefined}
              hint="Pick the body, then the credential it issues."
            />
            <FieldRow>
              <TextField form={form} name="certificateNumber" label="Certificate number" />
              <DateField form={form} name="issuedOn" label="Issued on" />
            </FieldRow>
            <DateField form={form} name="expiresOn" label="Expires on (blank = from the catalogue's validity)" />
            <TextareaField form={form} name="notes" label="Notes" />
          </>
        )}
      />

      <AttachFileDialog
        open={evidenceFor !== null}
        onOpenChange={(o) => !o && setEvidenceFor(null)}
        title={`Evidence — ${evidenceFor?.certificationName ?? ''}`}
        description="The certificate itself, through the controlled upload gate."
        currentFileName={evidenceFor?.hasEvidence ? evidenceFor.evidenceFileName : null}
        currentFileSize={evidenceFor?.evidenceFileSizeBytes}
        downloadUrl={evidenceFor ? employeeDocumentService.certificationEvidenceUrl(evidenceFor.id) : undefined}
        upload={(file) => employeeDocumentService.uploadCertificationEvidence(evidenceFor!.id, file)}
        onUploaded={() => refresh()}
      />

      <Dialog open={revoking !== null} onOpenChange={(o) => { if (!o) { setRevoking(null); setReason(''); } }}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Revoke {revoking?.certificationName}</DialogTitle>
            <DialogDescription>
              A revoked credential no longer satisfies any requirement. This is recorded, not undone.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="revokeReason">Why *</Label>
            <Textarea id="revokeReason" rows={3} value={reason} onChange={(e) => setReason(e.target.value)} />
            <Input type="hidden" value={revoking?.id ?? ''} readOnly />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRevoking(null)} disabled={saving}>
              Cancel
            </Button>
            <Button variant="destructive" onClick={revoke} disabled={saving || !reason.trim()}>
              Revoke
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
