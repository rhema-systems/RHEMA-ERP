'use client';

import { useState, type ReactNode } from 'react';
import { useParams } from 'next/navigation';
import { z } from 'zod';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { BadgeCheck, Loader2, Pencil } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyContractorService } from '@/services/hr/safety-contractor.service';
import { locationService } from '@/services/hr/location.service';
import {
  SHE_CONTRACTOR_STATUS_OPTIONS,
  SHE_PREQUALIFICATION_OUTCOME_OPTIONS,
  SHE_CONTRACTOR_DOCUMENT_TYPE_OPTIONS,
  SHE_INSPECTION_RESULT_OPTIONS,
  SHE_INSPECTION_STATUS_OPTIONS,
  SHE_NON_COMPLIANCE_SEVERITY_OPTIONS,
  SHE_NON_COMPLIANCE_EDIT_STATUS_OPTIONS,
  SHE_CONTRACTOR_SANCTION_OPTIONS,
} from '@/types/hr/safety-contractors';
import type {
  SheContractorStatus,
  SheContractorDocumentType,
  SheInspectionResult,
  SheInspectionStatus,
  SheNonComplianceSeverity,
  SheNonComplianceStatus,
  SheContractorSanction,
  SheContractorInduction,
  SheContractorInspection,
  SheContractorNonCompliance,
  SheContractorDocument,
} from '@/types/hr/safety-contractors';

/**
 * Contractor detail: company facts and pre-qualification standing, the edit and pre-qualify
 * dialogs, and the four child registers — worker inductions, contractor SHE inspections
 * (permanent records, no delete), non-compliance notices (closed through their own dialog,
 * never deleted) and the document file (verify once, then it is fixed).
 *
 * Both scores on this page are hand-entered by the assessor/inspector; the aggregate is
 * computed — the SHE Analytics screen ranks contractors by average inspection score (FR-CON-001).
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);
const dateOrNull = (v?: string) => (v && v.length > 0 ? new Date(v).toISOString() : null);

const editSchema = z.object({
  companyName: z.string().min(1, 'A company name is required').max(200),
  tradingName: z.string().max(100).optional().or(z.literal('')),
  address: z.string().max(300).optional().or(z.literal('')),
  phone: z.string().max(50).optional().or(z.literal('')),
  email: z.string().max(100).optional().or(z.literal('')),
  registrationNumber: z.string().max(50).optional().or(z.literal('')),
  primaryContactName: z.string().max(100).optional().or(z.literal('')),
  primaryContactPhone: z.string().max(50).optional().or(z.literal('')),
  primaryContactEmail: z.string().max(100).optional().or(z.literal('')),
  isActive: z.boolean(),
});
type EditForm = z.input<typeof editSchema>;

const preQualifySchema = z.object({
  sheStatus: z.string().min(1, 'An outcome is required'),
  preQualificationScore: z.coerce.number().min(0).max(100).optional(),
  preQualificationDate: z.string().min(1, 'An assessment date is required'),
  preQualificationExpiryDate: z.string().optional().or(z.literal('')),
  preQualifiedById: z.string().min(1, 'An assessor is required'),
  sheConditions: z.string().max(1000).optional().or(z.literal('')),
});
type PreQualifyForm = z.input<typeof preQualifySchema>;

const inductionSchema = z.object({
  workerName: z.string().min(1, 'A worker name is required').max(200),
  workerIdOrPassport: z.string().max(50).optional().or(z.literal('')),
  trade: z.string().max(100).optional().or(z.literal('')),
  inductionDate: z.string().min(1, 'A date is required'),
  conductedById: z.string().min(1, 'An inductor is required'),
  inductionPassed: z.boolean(),
  inductionExpiryDate: z.string().optional().or(z.literal('')),
  notes: z.string().max(500).optional().or(z.literal('')),
});
type InductionForm = z.input<typeof inductionSchema>;

const inspectionSchema = z.object({
  inspectionNumber: z.string().min(1, 'An inspection number is required').max(30),
  inspectionDate: z.string().min(1, 'A date is required'),
  locationId: z.string().optional().or(z.literal('')),
  specificArea: z.string().max(200).optional().or(z.literal('')),
  inspectorId: z.string().min(1, 'An inspector is required'),
  complianceScore: z.coerce.number().min(0).max(100).optional(),
  result: z.string().min(1),
  findings: z.string().max(3000).optional().or(z.literal('')),
  recommendedActions: z.string().max(2000).optional().or(z.literal('')),
  nextInspectionDate: z.string().optional().or(z.literal('')),
  status: z.string().min(1),
});
type InspectionForm = z.input<typeof inspectionSchema>;

const nonComplianceSchema = z.object({
  noticeNumber: z.string().min(1, 'A notice number is required').max(30),
  issuedDate: z.string().min(1, 'An issue date is required'),
  issuedById: z.string().min(1, 'An issuer is required'),
  violationDescription: z.string().min(1, 'A description is required').max(2000),
  severity: z.string().min(1),
  rectificationDeadline: z.string().min(1, 'A deadline is required'),
  // Edit-only fields — ignored on create.
  status: z.string().min(1),
  rectificationDate: z.string().optional().or(z.literal('')),
  contractorResponse: z.string().max(1000).optional().or(z.literal('')),
  sanctionApplied: z.string().optional().or(z.literal('')),
});
type NonComplianceForm = z.input<typeof nonComplianceSchema>;

const documentSchema = z.object({
  documentType: z.string().min(1),
  fileName: z.string().min(1, 'A file name is required').max(255),
  filePath: z.string().min(1, 'A file path is required').max(500),
  title: z.string().max(200).optional().or(z.literal('')),
  documentDate: z.string().optional().or(z.literal('')),
  expiryDate: z.string().optional().or(z.literal('')),
  uploadedById: z.string().min(1, 'An uploader is required'),
});
type DocumentForm = z.input<typeof documentSchema>;

const closeSchema = z.object({
  closedById: z.string().min(1, 'Who closed it is required'),
  closedDate: z.string().min(1, 'A close date is required'),
  rectificationDate: z.string().optional().or(z.literal('')),
  closureNotes: z.string().max(1000).optional().or(z.literal('')),
});
type CloseForm = z.input<typeof closeSchema>;

const verifySchema = z.object({
  verifiedById: z.string().min(1, 'A verifier is required'),
  verifiedDate: z.string().min(1, 'A verification date is required'),
  verificationNotes: z.string().max(300).optional().or(z.literal('')),
});
type VerifyForm = z.input<typeof verifySchema>;

const statusLabel = (v: string) =>
  SHE_CONTRACTOR_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;

const statusVariant = (v: string): 'default' | 'secondary' | 'destructive' | 'outline' => {
  if (v === 'Approved') return 'secondary';
  if (v === 'Suspended' || v === 'Revoked' || v === 'Blacklisted') return 'destructive';
  return 'outline';
};

const severityVariant = (v: string): 'default' | 'secondary' | 'destructive' | 'outline' =>
  v === 'Critical' || v === 'Imminent' ? 'destructive' : v === 'Major' ? 'default' : 'secondary';

function Fact({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div>
      <div className="text-muted-foreground text-xs">{label}</div>
      <div className="text-sm font-medium">{value ?? '—'}</div>
    </div>
  );
}

export default function ContractorDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [editOpen, setEditOpen] = useState(false);
  const [preQualifyOpen, setPreQualifyOpen] = useState(false);
  const [closing, setClosing] = useState<SheContractorNonCompliance | null>(null);
  const [verifying, setVerifying] = useState<SheContractorDocument | null>(null);
  const [busy, setBusy] = useState(false);

  const contractorKey = ['hr', 'safety-contractors', id];

  const { data: contractor } = useQuery({
    queryKey: contractorKey,
    queryFn: () => safetyContractorService.getById(id),
  });
  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const editForm = useForm<EditForm>({ resolver: zodResolver(editSchema) });
  const preQualifyForm = useForm<PreQualifyForm>({ resolver: zodResolver(preQualifySchema) });
  const closeForm = useForm<CloseForm>({ resolver: zodResolver(closeSchema) });
  const verifyForm = useForm<VerifyForm>({ resolver: zodResolver(verifySchema) });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'safety-contractors'] });

  const fail = (fallback: string) => (error: any) =>
    toast({
      title: 'Error',
      description: error?.message || fallback,
      variant: 'destructive',
    });

  const openEdit = () => {
    if (!contractor) return;
    editForm.reset({
      companyName: contractor.companyName,
      tradingName: contractor.tradingName ?? '',
      address: contractor.address ?? '',
      phone: contractor.phone ?? '',
      email: contractor.email ?? '',
      registrationNumber: contractor.registrationNumber ?? '',
      primaryContactName: contractor.primaryContactName ?? '',
      primaryContactPhone: contractor.primaryContactPhone ?? '',
      primaryContactEmail: contractor.primaryContactEmail ?? '',
      isActive: contractor.isActive,
    });
    setEditOpen(true);
  };

  const submitEdit = editForm.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = editSchema.parse(values);
      await safetyContractorService.update(id, {
        id,
        companyName: v.companyName,
        tradingName: blank(v.tradingName),
        address: blank(v.address),
        phone: blank(v.phone),
        email: blank(v.email),
        registrationNumber: blank(v.registrationNumber),
        primaryContactName: blank(v.primaryContactName),
        primaryContactPhone: blank(v.primaryContactPhone),
        primaryContactEmail: blank(v.primaryContactEmail),
        isActive: v.isActive,
      });
      await invalidate();
      toast({ title: 'Contractor updated' });
      setEditOpen(false);
    } catch (error: any) {
      fail('Updating failed.')(error);
    } finally {
      setBusy(false);
    }
  });

  const openPreQualify = () => {
    if (!contractor) return;
    preQualifyForm.reset({
      sheStatus: contractor.sheStatus === 'PendingAssessment' ? 'Approved' : contractor.sheStatus,
      preQualificationScore: contractor.preQualificationScore ?? undefined,
      preQualificationDate: new Date().toISOString().slice(0, 10),
      preQualificationExpiryDate: contractor.preQualificationExpiryDate?.slice(0, 10) ?? '',
      preQualifiedById: contractor.preQualifiedById ?? '',
      sheConditions: contractor.sheConditions ?? '',
    });
    setPreQualifyOpen(true);
  };

  const submitPreQualify = preQualifyForm.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = preQualifySchema.parse(values);
      await safetyContractorService.preQualify(id, {
        contractorId: id,
        sheStatus: v.sheStatus as SheContractorStatus,
        preQualificationScore: v.preQualificationScore ?? null,
        preQualificationDate: new Date(v.preQualificationDate).toISOString(),
        preQualificationExpiryDate: dateOrNull(v.preQualificationExpiryDate),
        preQualifiedById: v.preQualifiedById,
        sheConditions: blank(v.sheConditions),
      });
      await invalidate();
      toast({ title: 'Pre-qualification recorded' });
      setPreQualifyOpen(false);
    } catch (error: any) {
      fail('Recording the pre-qualification failed.')(error);
    } finally {
      setBusy(false);
    }
  });

  const submitClose = closeForm.handleSubmit(async (values) => {
    if (!closing) return;
    setBusy(true);
    try {
      const v = closeSchema.parse(values);
      await safetyContractorService.closeNonCompliance(closing.id, {
        nonComplianceId: closing.id,
        closedById: v.closedById,
        closedDate: new Date(v.closedDate).toISOString(),
        rectificationDate: dateOrNull(v.rectificationDate),
        closureNotes: blank(v.closureNotes),
      });
      await invalidate();
      toast({ title: 'Notice closed', description: closing.noticeNumber });
      setClosing(null);
    } catch (error: any) {
      fail('Closing the notice failed.')(error);
    } finally {
      setBusy(false);
    }
  });

  const submitVerify = verifyForm.handleSubmit(async (values) => {
    if (!verifying) return;
    setBusy(true);
    try {
      const v = verifySchema.parse(values);
      await safetyContractorService.verifyDocument(verifying.id, {
        documentId: verifying.id,
        verifiedById: v.verifiedById,
        verifiedDate: new Date(v.verifiedDate).toISOString(),
        verificationNotes: blank(v.verificationNotes),
      });
      await invalidate();
      toast({ title: 'Document verified', description: verifying.title || verifying.fileName });
      setVerifying(null);
    } catch (error: any) {
      fail('Verifying the document failed.')(error);
    } finally {
      setBusy(false);
    }
  });

  if (!contractor) return null;

  const openNotices = contractor.nonCompliances.filter((n) => n.status !== 'Closed').length;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${contractor.contractorCode} — ${contractor.companyName}`}
        description={contractor.tradingName ? `Trading as ${contractor.tradingName}` : undefined}
        backHref="/hr/safety/contractors"
        actions={
          <div className="flex gap-2">
            <Button variant="outline" onClick={openPreQualify}>
              <BadgeCheck className="mr-2 h-4 w-4" /> Pre-qualify
            </Button>
            <Button variant="outline" onClick={openEdit}>
              <Pencil className="mr-2 h-4 w-4" /> Edit
            </Button>
          </div>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-3 text-base">
            <Badge variant={statusVariant(contractor.sheStatus)}>
              {statusLabel(contractor.sheStatus)}
            </Badge>
            <StatusBadge status={contractor.isActive ? 'Active' : 'Inactive'} />
            {openNotices > 0 && (
              <Badge variant="destructive">
                {openNotices} open notice{openNotices === 1 ? '' : 's'}
              </Badge>
            )}
            {contractor.preQualificationExpiryDate &&
              new Date(contractor.preQualificationExpiryDate) < new Date() && (
                <Badge variant="destructive">Pre-qualification expired</Badge>
              )}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
            <Fact
              label="Pre-qual score (hand-entered)"
              value={
                contractor.preQualificationScore != null
                  ? `${contractor.preQualificationScore} / 100`
                  : '—'
              }
            />
            <Fact label="Assessed" value={fmtDate(contractor.preQualificationDate)} />
            <Fact label="Expires" value={fmtDate(contractor.preQualificationExpiryDate)} />
            <Fact label="Assessed by" value={contractor.preQualifiedByName} />
            <Fact label="Registration no." value={contractor.registrationNumber} />
            <Fact label="Phone" value={contractor.phone} />
            <Fact label="Email" value={contractor.email} />
            <Fact
              label="Primary contact"
              value={
                contractor.primaryContactName
                  ? `${contractor.primaryContactName}${contractor.primaryContactPhone ? ` · ${contractor.primaryContactPhone}` : ''}`
                  : '—'
              }
            />
          </div>
          {contractor.address && <p className="text-sm">{contractor.address}</p>}
          {contractor.sheConditions && (
            <div>
              <div className="text-muted-foreground mb-1 text-xs">SHE conditions</div>
              <p className="whitespace-pre-wrap text-sm">{contractor.sheConditions}</p>
            </div>
          )}
        </CardContent>
      </Card>

      <Tabs defaultValue="inductions">
        <TabsList>
          <TabsTrigger value="inductions">Inductions ({contractor.inductions.length})</TabsTrigger>
          <TabsTrigger value="inspections">
            SHE inspections ({contractor.sheInspections.length})
          </TabsTrigger>
          <TabsTrigger value="non-compliances">
            Non-compliances ({contractor.nonCompliances.length})
          </TabsTrigger>
          <TabsTrigger value="documents">Documents ({contractor.documents.length})</TabsTrigger>
        </TabsList>

        {/* ── Inductions ── */}
        <TabsContent value="inductions" className="mt-4">
          <ResourceCollectionTab<SheContractorInduction, InductionForm>
            parentId={id}
            title="worker inductions"
            singular="induction"
            queryKey={[...contractorKey, 'inductions']}
            invalidateKeys={[contractorKey]}
            list={async () => (await safetyContractorService.getById(id)).inductions}
            create={(contractorId, values) => {
              const v = inductionSchema.parse(values);
              return safetyContractorService.addInduction(contractorId, {
                contractorId,
                workerName: v.workerName,
                workerIdOrPassport: blank(v.workerIdOrPassport),
                trade: blank(v.trade),
                inductionDate: new Date(v.inductionDate).toISOString(),
                conductedById: v.conductedById,
                inductionPassed: v.inductionPassed,
                inductionExpiryDate: dateOrNull(v.inductionExpiryDate),
                notes: blank(v.notes),
              });
            }}
            update={(_contractorId, inductionId, values) => {
              const v = inductionSchema.parse(values);
              return safetyContractorService.updateInduction(inductionId, {
                id: inductionId,
                workerName: v.workerName,
                workerIdOrPassport: blank(v.workerIdOrPassport),
                trade: blank(v.trade),
                inductionDate: new Date(v.inductionDate).toISOString(),
                inductionPassed: v.inductionPassed,
                inductionExpiryDate: dateOrNull(v.inductionExpiryDate),
                notes: blank(v.notes),
              });
            }}
            remove={(_contractorId, inductionId) =>
              safetyContractorService.removeInduction(inductionId)
            }
            columns={[
              {
                header: 'Worker',
                cell: (i) => <span className="font-medium">{i.workerName}</span>,
              },
              { header: 'ID / passport', cell: (i) => i.workerIdOrPassport ?? '—' },
              { header: 'Trade', cell: (i) => i.trade ?? '—' },
              { header: 'Inducted', cell: (i) => fmtDate(i.inductionDate) },
              { header: 'By', cell: (i) => i.conductedByName },
              {
                header: 'Result',
                cell: (i) =>
                  i.inductionPassed ? (
                    <Badge variant="secondary">Passed</Badge>
                  ) : (
                    <Badge variant="destructive">Failed</Badge>
                  ),
              },
              {
                header: 'Expires',
                cell: (i) =>
                  i.inductionExpiryDate && new Date(i.inductionExpiryDate) < new Date() ? (
                    <Badge variant="destructive">Expired {fmtDate(i.inductionExpiryDate)}</Badge>
                  ) : (
                    fmtDate(i.inductionExpiryDate)
                  ),
              },
            ]}
            schema={inductionSchema}
            emptyForm={{
              workerName: '',
              workerIdOrPassport: '',
              trade: '',
              inductionDate: new Date().toISOString().slice(0, 10),
              conductedById: '',
              inductionPassed: true,
              inductionExpiryDate: '',
              notes: '',
            }}
            toForm={(i) => ({
              workerName: i.workerName,
              workerIdOrPassport: i.workerIdOrPassport ?? '',
              trade: i.trade ?? '',
              inductionDate: i.inductionDate.slice(0, 10),
              conductedById: i.conductedById,
              inductionPassed: i.inductionPassed,
              inductionExpiryDate: i.inductionExpiryDate?.slice(0, 10) ?? '',
              notes: i.notes ?? '',
            })}
            renderFields={(f, editing) => (
              <>
                <FieldRow>
                  <TextField form={f} name="workerName" label="Worker name" required />
                  <TextField form={f} name="workerIdOrPassport" label="ID / passport" />
                </FieldRow>
                <FieldRow>
                  <TextField form={f} name="trade" label="Trade" />
                  <DateField form={f} name="inductionDate" label="Induction date" required />
                </FieldRow>
                {!editing ? (
                  <EmployeePickerField form={f} name="conductedById" label="Conducted by" required />
                ) : (
                  <div className="text-muted-foreground text-sm">
                    The inductor is fixed once recorded.
                  </div>
                )}
                <FieldRow>
                  <DateField form={f} name="inductionExpiryDate" label="Induction expires" />
                  <SwitchField form={f} name="inductionPassed" label="Passed" />
                </FieldRow>
                <TextareaField form={f} name="notes" label="Notes" rows={2} />
              </>
            )}
            getId={(i) => i.id}
            emptyDescription="Every contractor worker must be inducted before starting on site."
          />
        </TabsContent>

        {/* ── SHE inspections ── */}
        <TabsContent value="inspections" className="mt-4">
          <ResourceCollectionTab<SheContractorInspection, InspectionForm>
            parentId={id}
            title="SHE inspections"
            singular="inspection"
            queryKey={[...contractorKey, 'inspections']}
            invalidateKeys={[contractorKey]}
            list={() => safetyContractorService.getInspections(id)}
            create={(contractorId, values) => {
              const v = inspectionSchema.parse(values);
              return safetyContractorService.addInspection(contractorId, {
                contractorId,
                inspectionNumber: v.inspectionNumber,
                inspectionDate: new Date(v.inspectionDate).toISOString(),
                locationId: blank(v.locationId),
                specificArea: blank(v.specificArea),
                inspectorId: v.inspectorId,
                complianceScore: v.complianceScore ?? null,
                result: v.result as SheInspectionResult,
                findings: blank(v.findings),
                recommendedActions: blank(v.recommendedActions),
                nextInspectionDate: dateOrNull(v.nextInspectionDate),
                status: v.status as SheInspectionStatus,
              });
            }}
            update={(_contractorId, inspectionId, values) => {
              const v = inspectionSchema.parse(values);
              return safetyContractorService.updateInspection(inspectionId, {
                id: inspectionId,
                inspectionDate: new Date(v.inspectionDate).toISOString(),
                locationId: blank(v.locationId),
                specificArea: blank(v.specificArea),
                complianceScore: v.complianceScore ?? null,
                result: v.result as SheInspectionResult,
                findings: blank(v.findings),
                recommendedActions: blank(v.recommendedActions),
                nextInspectionDate: dateOrNull(v.nextInspectionDate),
                status: v.status as SheInspectionStatus,
              });
            }}
            columns={[
              {
                header: 'Number',
                cell: (i) => <span className="font-mono">{i.inspectionNumber}</span>,
              },
              { header: 'Date', cell: (i) => fmtDate(i.inspectionDate) },
              {
                header: 'Where',
                cell: (i) =>
                  `${i.locationName ?? '—'}${i.specificArea ? ` · ${i.specificArea}` : ''}`,
              },
              { header: 'Inspector', cell: (i) => i.inspectorName },
              {
                header: 'Score',
                cell: (i) => (i.complianceScore != null ? `${i.complianceScore} / 100` : '—'),
              },
              {
                header: 'Result',
                cell: (i) => (
                  <Badge variant={i.result === 'Fail' ? 'destructive' : 'secondary'}>
                    {SHE_INSPECTION_RESULT_OPTIONS.find((o) => o.value === i.result)?.label ??
                      i.result}
                  </Badge>
                ),
              },
              { header: 'Status', cell: (i) => <StatusBadge status={i.status} /> },
              { header: 'Next due', cell: (i) => fmtDate(i.nextInspectionDate) },
            ]}
            schema={inspectionSchema}
            emptyForm={{
              inspectionNumber: '',
              inspectionDate: new Date().toISOString().slice(0, 10),
              locationId: '',
              specificArea: '',
              inspectorId: '',
              complianceScore: undefined,
              result: 'Pass',
              findings: '',
              recommendedActions: '',
              nextInspectionDate: '',
              status: 'Completed',
            }}
            toForm={(i) => ({
              inspectionNumber: i.inspectionNumber,
              inspectionDate: i.inspectionDate.slice(0, 10),
              locationId: i.locationId ?? '',
              specificArea: i.specificArea ?? '',
              inspectorId: i.inspectorId,
              complianceScore: i.complianceScore ?? undefined,
              result: i.result,
              findings: i.findings ?? '',
              recommendedActions: i.recommendedActions ?? '',
              nextInspectionDate: i.nextInspectionDate?.slice(0, 10) ?? '',
              status: i.status,
            })}
            renderFields={(f, editing) => (
              <>
                <FieldRow>
                  {!editing ? (
                    <TextField
                      form={f}
                      name="inspectionNumber"
                      label="Inspection number (unique)"
                      required
                    />
                  ) : (
                    <div className="text-muted-foreground self-end pb-2 text-sm">
                      <span className="font-mono">{f.getValues('inspectionNumber')}</span> — fixed
                      at creation.
                    </div>
                  )}
                  <DateField form={f} name="inspectionDate" label="Inspection date" required />
                </FieldRow>
                <FieldRow>
                  <SelectField
                    form={f}
                    name="locationId"
                    label="Location"
                    allowEmpty
                    emptyLabel="Not set"
                    options={locations.map((l) => ({ value: l.id, label: l.name }))}
                  />
                  <TextField form={f} name="specificArea" label="Specific area" />
                </FieldRow>
                {!editing ? (
                  <EmployeePickerField form={f} name="inspectorId" label="Inspector" required />
                ) : (
                  <div className="text-muted-foreground text-sm">
                    The inspector is fixed once recorded.
                  </div>
                )}
                <FieldRow>
                  <NumberField
                    form={f}
                    name="complianceScore"
                    label="Compliance score 0–100 (hand-entered; averaged into the ranking)"
                  />
                  <SelectField
                    form={f}
                    name="result"
                    label="Result"
                    required
                    options={SHE_INSPECTION_RESULT_OPTIONS}
                  />
                </FieldRow>
                <TextareaField form={f} name="findings" label="Findings" rows={3} />
                <TextareaField
                  form={f}
                  name="recommendedActions"
                  label="Recommended actions"
                  rows={2}
                />
                <FieldRow>
                  <DateField form={f} name="nextInspectionDate" label="Next inspection due" />
                  <SelectField
                    form={f}
                    name="status"
                    label="Status"
                    required
                    options={SHE_INSPECTION_STATUS_OPTIONS}
                  />
                </FieldRow>
              </>
            )}
            getId={(i) => i.id}
            dialogClassName="sm:max-w-[640px]"
            emptyDescription="Site inspections of this contractor's work. Inspection records are permanent — they cannot be deleted."
          />
        </TabsContent>

        {/* ── Non-compliances ── */}
        <TabsContent value="non-compliances" className="mt-4">
          <ResourceCollectionTab<SheContractorNonCompliance, NonComplianceForm>
            parentId={id}
            title="non-compliance notices"
            singular="notice"
            queryKey={[...contractorKey, 'non-compliances']}
            invalidateKeys={[contractorKey, ['hr', 'safety-contractors']]}
            list={() => safetyContractorService.getNonCompliances(id)}
            create={(contractorId, values) => {
              const v = nonComplianceSchema.parse(values);
              return safetyContractorService.addNonCompliance(contractorId, {
                contractorId,
                noticeNumber: v.noticeNumber,
                issuedDate: new Date(v.issuedDate).toISOString(),
                issuedById: v.issuedById,
                violationDescription: v.violationDescription,
                severity: v.severity as SheNonComplianceSeverity,
                rectificationDeadline: new Date(v.rectificationDeadline).toISOString(),
              });
            }}
            update={(_contractorId, noticeId, values) => {
              const v = nonComplianceSchema.parse(values);
              return safetyContractorService.updateNonCompliance(noticeId, {
                id: noticeId,
                violationDescription: v.violationDescription,
                severity: v.severity as SheNonComplianceSeverity,
                rectificationDeadline: new Date(v.rectificationDeadline).toISOString(),
                status: v.status as SheNonComplianceStatus,
                rectificationDate: dateOrNull(v.rectificationDate),
                contractorResponse: blank(v.contractorResponse),
                sanctionApplied: (blank(v.sanctionApplied) as SheContractorSanction | null) ?? null,
              });
            }}
            actions={[
              {
                label: 'Close notice…',
                visible: (n) => n.status !== 'Closed',
                run: async (n) => {
                  closeForm.reset({
                    closedById: '',
                    closedDate: new Date().toISOString().slice(0, 10),
                    rectificationDate: n.rectificationDate?.slice(0, 10) ?? '',
                    closureNotes: '',
                  });
                  setClosing(n);
                },
              },
            ]}
            columns={[
              {
                header: 'Notice',
                cell: (n) => <span className="font-mono">{n.noticeNumber}</span>,
              },
              { header: 'Issued', cell: (n) => fmtDate(n.issuedDate) },
              { header: 'By', cell: (n) => n.issuedByName },
              {
                header: 'Severity',
                cell: (n) => (
                  <Badge variant={severityVariant(n.severity)}>{n.severityName}</Badge>
                ),
              },
              {
                header: 'Deadline',
                cell: (n) =>
                  n.status !== 'Closed' && new Date(n.rectificationDeadline) < new Date() ? (
                    <Badge variant="destructive">
                      Overdue {fmtDate(n.rectificationDeadline)}
                    </Badge>
                  ) : (
                    fmtDate(n.rectificationDeadline)
                  ),
              },
              {
                header: 'Repeat',
                cell: (n) =>
                  n.isRepeatViolation ? (
                    <Badge variant="destructive">#{n.repeatCount + 1}</Badge>
                  ) : (
                    <span className="text-muted-foreground text-sm">First</span>
                  ),
              },
              { header: 'Sanction', cell: (n) => n.sanctionAppliedName ?? '—' },
              { header: 'Status', cell: (n) => <StatusBadge status={n.status} /> },
            ]}
            schema={nonComplianceSchema}
            emptyForm={{
              noticeNumber: '',
              issuedDate: new Date().toISOString().slice(0, 10),
              issuedById: '',
              violationDescription: '',
              severity: 'Minor',
              rectificationDeadline: '',
              status: 'Open',
              rectificationDate: '',
              contractorResponse: '',
              sanctionApplied: '',
            }}
            toForm={(n) => ({
              noticeNumber: n.noticeNumber,
              issuedDate: n.issuedDate.slice(0, 10),
              issuedById: n.issuedById,
              violationDescription: n.violationDescription,
              severity: n.severity,
              rectificationDeadline: n.rectificationDeadline.slice(0, 10),
              status: n.status,
              rectificationDate: n.rectificationDate?.slice(0, 10) ?? '',
              contractorResponse: n.contractorResponse ?? '',
              sanctionApplied: n.sanctionApplied ?? '',
            })}
            renderFields={(f, editing) => (
              <>
                <FieldRow>
                  {!editing ? (
                    <TextField form={f} name="noticeNumber" label="Notice number (unique)" required />
                  ) : (
                    <div className="text-muted-foreground self-end pb-2 text-sm">
                      <span className="font-mono">{f.getValues('noticeNumber')}</span> — fixed at
                      creation.
                    </div>
                  )}
                  {!editing ? (
                    <DateField form={f} name="issuedDate" label="Issued" required />
                  ) : (
                    <div />
                  )}
                </FieldRow>
                {!editing && (
                  <EmployeePickerField form={f} name="issuedById" label="Issued by" required />
                )}
                <TextareaField
                  form={f}
                  name="violationDescription"
                  label="Violation"
                  rows={3}
                  required
                />
                <FieldRow>
                  <SelectField
                    form={f}
                    name="severity"
                    label="Severity"
                    required
                    options={SHE_NON_COMPLIANCE_SEVERITY_OPTIONS}
                  />
                  <DateField
                    form={f}
                    name="rectificationDeadline"
                    label="Rectification deadline"
                    required
                  />
                </FieldRow>
                {editing && (
                  <>
                    <FieldRow>
                      <SelectField
                        form={f}
                        name="status"
                        label="Status (closing has its own dialog)"
                        required
                        options={SHE_NON_COMPLIANCE_EDIT_STATUS_OPTIONS}
                      />
                      <DateField form={f} name="rectificationDate" label="Rectified on" />
                    </FieldRow>
                    <TextareaField
                      form={f}
                      name="contractorResponse"
                      label="Contractor response"
                      rows={2}
                    />
                    <SelectField
                      form={f}
                      name="sanctionApplied"
                      label="Sanction applied"
                      allowEmpty
                      emptyLabel="None"
                      options={SHE_CONTRACTOR_SANCTION_OPTIONS}
                    />
                  </>
                )}
              </>
            )}
            getId={(n) => n.id}
            dialogClassName="sm:max-w-[640px]"
            emptyDescription="Notices issued against this contractor. Repeat flags are computed from prior notices; a closed notice can no longer be edited."
          />
        </TabsContent>

        {/* ── Documents ── */}
        <TabsContent value="documents" className="mt-4">
          <ResourceCollectionTab<SheContractorDocument, DocumentForm>
            parentId={id}
            title="documents"
            singular="document"
            queryKey={[...contractorKey, 'documents']}
            invalidateKeys={[contractorKey, ['hr', 'safety-contractors']]}
            list={() => safetyContractorService.getDocuments(id)}
            create={(contractorId, values) => {
              const v = documentSchema.parse(values);
              return safetyContractorService.addDocument(contractorId, {
                contractorId,
                documentType: v.documentType as SheContractorDocumentType,
                fileName: v.fileName,
                filePath: v.filePath,
                title: blank(v.title),
                documentDate: dateOrNull(v.documentDate),
                expiryDate: dateOrNull(v.expiryDate),
                uploadedById: v.uploadedById,
              });
            }}
            update={() => Promise.reject(new Error('Documents cannot be edited.'))}
            allowUpdate={false}
            remove={(_contractorId, documentId) =>
              safetyContractorService.removeDocument(documentId)
            }
            actions={[
              {
                label: 'Verify…',
                visible: (d) => !d.isVerified,
                run: async (d) => {
                  verifyForm.reset({
                    verifiedById: '',
                    verifiedDate: new Date().toISOString().slice(0, 10),
                    verificationNotes: '',
                  });
                  setVerifying(d);
                },
              },
            ]}
            columns={[
              {
                header: 'Document',
                cell: (d) => (
                  <div>
                    <div className="font-medium">{d.title || d.fileName}</div>
                    <div className="text-muted-foreground text-xs">{d.fileName}</div>
                  </div>
                ),
              },
              { header: 'Type', cell: (d) => d.documentTypeName },
              { header: 'Dated', cell: (d) => fmtDate(d.documentDate) },
              {
                header: 'Expires',
                cell: (d) =>
                  d.expiryDate && new Date(d.expiryDate) < new Date() ? (
                    <Badge variant="destructive">Expired {fmtDate(d.expiryDate)}</Badge>
                  ) : (
                    fmtDate(d.expiryDate)
                  ),
              },
              {
                header: 'Uploaded',
                cell: (d) => `${fmtDate(d.uploadedDate)} · ${d.uploadedByName}`,
              },
              {
                header: 'Verification',
                cell: (d) =>
                  d.isVerified ? (
                    <Badge variant="secondary">
                      Verified {fmtDate(d.verifiedDate)}
                      {d.verifiedByName ? ` · ${d.verifiedByName}` : ''}
                    </Badge>
                  ) : (
                    <Badge variant="outline">Unverified</Badge>
                  ),
              },
            ]}
            schema={documentSchema}
            emptyForm={{
              documentType: 'InsuranceCertificate',
              fileName: '',
              filePath: '',
              title: '',
              documentDate: '',
              expiryDate: '',
              uploadedById: '',
            }}
            toForm={(d) => ({
              documentType: d.documentType,
              fileName: d.fileName,
              filePath: d.filePath,
              title: d.title ?? '',
              documentDate: d.documentDate?.slice(0, 10) ?? '',
              expiryDate: d.expiryDate?.slice(0, 10) ?? '',
              uploadedById: d.uploadedById,
            })}
            renderFields={(f) => (
              <>
                <FieldRow>
                  <SelectField
                    form={f}
                    name="documentType"
                    label="Document type"
                    required
                    options={SHE_CONTRACTOR_DOCUMENT_TYPE_OPTIONS}
                  />
                  <TextField form={f} name="title" label="Title" />
                </FieldRow>
                <FieldRow>
                  <TextField form={f} name="fileName" label="File name" required />
                  <TextField form={f} name="filePath" label="File path" required />
                </FieldRow>
                <FieldRow>
                  <DateField form={f} name="documentDate" label="Document date" />
                  <DateField form={f} name="expiryDate" label="Expiry date" />
                </FieldRow>
                <EmployeePickerField form={f} name="uploadedById" label="Uploaded by" required />
              </>
            )}
            getId={(d) => d.id}
            emptyDescription="Insurance, method statements, competency certificates and other papers on file for this contractor."
          />
        </TabsContent>
      </Tabs>

      {/* ── Edit dialog ── */}
      <Dialog open={editOpen} onOpenChange={(o) => !busy && setEditOpen(o)}>
        <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit contractor</DialogTitle>
            <DialogDescription>
              {contractor.contractorCode} — the code itself cannot change. Pre-qualification is
              recorded through its own dialog.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitEdit} className="space-y-4">
            <FieldRow>
              <TextField form={editForm} name="companyName" label="Company name" required />
              <TextField form={editForm} name="tradingName" label="Trading name" />
            </FieldRow>
            <TextareaField form={editForm} name="address" label="Address" rows={2} />
            <FieldRow>
              <TextField form={editForm} name="phone" label="Phone" />
              <TextField form={editForm} name="email" label="Email" />
            </FieldRow>
            <FieldRow>
              <TextField form={editForm} name="registrationNumber" label="Registration number" />
              <TextField form={editForm} name="primaryContactName" label="Primary contact" />
            </FieldRow>
            <FieldRow>
              <TextField form={editForm} name="primaryContactPhone" label="Contact phone" />
              <TextField form={editForm} name="primaryContactEmail" label="Contact email" />
            </FieldRow>
            <SwitchField form={editForm} name="isActive" label="Active" />
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => setEditOpen(false)}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Save
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Pre-qualify dialog ── */}
      <Dialog open={preQualifyOpen} onOpenChange={(o) => !busy && setPreQualifyOpen(o)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Record pre-qualification</DialogTitle>
            <DialogDescription>
              The SHE assessment outcome for {contractor.companyName}. The score is hand-entered —
              nothing computes it.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitPreQualify} className="space-y-4">
            <FieldRow>
              <SelectField
                form={preQualifyForm}
                name="sheStatus"
                label="Outcome"
                required
                options={SHE_PREQUALIFICATION_OUTCOME_OPTIONS}
              />
              <NumberField form={preQualifyForm} name="preQualificationScore" label="Score 0–100" />
            </FieldRow>
            <FieldRow>
              <DateField
                form={preQualifyForm}
                name="preQualificationDate"
                label="Assessment date"
                required
              />
              <DateField
                form={preQualifyForm}
                name="preQualificationExpiryDate"
                label="Valid until"
              />
            </FieldRow>
            <EmployeePickerField
              form={preQualifyForm}
              name="preQualifiedById"
              label="Assessed by"
              required
              initialLabel={contractor.preQualifiedByName ?? undefined}
            />
            <TextareaField
              form={preQualifyForm}
              name="sheConditions"
              label="Conditions (e.g. valid PLI required per work order)"
              rows={2}
            />
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => setPreQualifyOpen(false)}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Record outcome
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Close-notice dialog ── */}
      <Dialog open={closing !== null} onOpenChange={(o) => !busy && !o && setClosing(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Close notice {closing?.noticeNumber}</DialogTitle>
            <DialogDescription>
              Closing is final — a closed notice can no longer be edited or re-closed.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitClose} className="space-y-4">
            <EmployeePickerField form={closeForm} name="closedById" label="Closed by" required />
            <FieldRow>
              <DateField form={closeForm} name="closedDate" label="Closed on" required />
              <DateField form={closeForm} name="rectificationDate" label="Rectified on" />
            </FieldRow>
            <TextareaField form={closeForm} name="closureNotes" label="Closure notes" rows={3} />
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => setClosing(null)}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Close notice
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Verify-document dialog ── */}
      <Dialog open={verifying !== null} onOpenChange={(o) => !busy && !o && setVerifying(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Verify document</DialogTitle>
            <DialogDescription>
              {verifying?.title || verifying?.fileName} — verification is recorded once and cannot
              be repeated.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitVerify} className="space-y-4">
            <EmployeePickerField
              form={verifyForm}
              name="verifiedById"
              label="Verified by"
              required
            />
            <DateField form={verifyForm} name="verifiedDate" label="Verified on" required />
            <TextareaField
              form={verifyForm}
              name="verificationNotes"
              label="Verification notes"
              rows={2}
            />
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => setVerifying(null)}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Verify
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
