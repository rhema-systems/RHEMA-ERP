'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import {
  Loader2,
  Search,
  ClipboardPen,
  Megaphone,
  HandCoins,
  Stamp,
  CheckCircle2,
  Trash2,
  AlertTriangle,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
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
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { LinkedErCasesPanel } from '@/components/hr/employee-relations/LinkedErCasesPanel';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { safetyIncidentService } from '@/services/hr/safety-incident.service';
import { safetyReferenceService } from '@/services/hr/safety-reference.service';
import type { SafetyIncident, SafetyIncidentInvolvedPerson, SafetyIncidentWitness, SafetyIncidentInvestigationTeamMember, SafetyIncidentCorrectiveAction, SafetyIncidentFollowUp, SafetyIncidentDocument, SheBodySide } from '@/types/hr/safety-incidents';
import {
  SHE_INVOLVED_PERSON_ROLE_OPTIONS,
  SHE_INJURY_CLASSIFICATION_OPTIONS,
  SHE_ROOT_CAUSE_METHOD_OPTIONS,
  SHE_INCIDENT_STATUS_OPTIONS,
  SHE_CORRECTIVE_ACTION_PRIORITY_OPTIONS,
  SHE_CORRECTIVE_ACTION_STATUS_OPTIONS,
  SHE_INCIDENT_DOCUMENT_TYPE_OPTIONS,
  SHE_BODY_SIDE_OPTIONS,
  SHE_STATUTORY_SUBMISSION_TYPE_OPTIONS,
  SHE_STATUTORY_SUBMISSION_METHOD_OPTIONS,
} from '@/types/hr/safety-incidents';
import type {
  SheStatutoryIncidentSubmission,
  SheStatutorySubmissionMethod,
  SheStatutorySubmissionType,
} from '@/types/hr/safety-incidents';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);

function InfoRow({ label, value }: { label: string; value?: string | number | null }) {
  return (
    <div className="flex items-baseline justify-between gap-4 border-b py-1.5 text-sm">
      <span className="text-muted-foreground shrink-0">{label}</span>
      <span className="text-right font-medium">{value ?? '—'}</span>
    </div>
  );
}

// ── Child-collection schemas ─────────────────────────────────────────────────

const personSchema = z.object({
  isEmployee: z.boolean(),
  employeeId: z.string().optional().or(z.literal('')),
  fullName: z.string().min(1, 'A name is required').max(200),
  organizationOrCompany: z.string().max(100).optional().or(z.literal('')),
  roleInIncident: z.enum(['PrimaryVictim', 'SecondaryVictim', 'Perpetrator', 'Bystander', 'Responder']),
  wasOnDuty: z.boolean(),
  activityBeingPerformed: z.string().max(500).optional().or(z.literal('')),
  wasUsingPpe: z.boolean(),
  ppeUsed: z.string().max(500).optional().or(z.literal('')),
  ppeWasAdequate: z.boolean(),
  wasInjured: z.boolean(),
  injuryDescription: z.string().max(1000).optional().or(z.literal('')),
  injuryClassification: z
    .enum(['FirstAidCase', 'MedicalTreatmentCase', 'RestrictedWorkCase', 'LostTimeInjury', 'PermanentDisability', 'Fatality'])
    .optional()
    .or(z.literal('')),
  injuryTypeId: z.string().optional().or(z.literal('')),
  isFatal: z.boolean(),
  resultedInTimeOff: z.boolean(),
  lostDays: z.coerce.number().min(0).max(3650).optional(),
});
type PersonForm = z.input<typeof personSchema>;
const emptyPerson: PersonForm = {
  isEmployee: true,
  employeeId: '',
  fullName: '',
  organizationOrCompany: '',
  roleInIncident: 'PrimaryVictim',
  wasOnDuty: true,
  activityBeingPerformed: '',
  wasUsingPpe: false,
  ppeUsed: '',
  ppeWasAdequate: false,
  wasInjured: false,
  injuryDescription: '',
  injuryClassification: '',
  injuryTypeId: '',
  isFatal: false,
  resultedInTimeOff: false,
  lostDays: undefined,
};

const witnessSchema = z.object({
  isEmployee: z.boolean(),
  employeeId: z.string().optional().or(z.literal('')),
  name: z.string().min(1, 'A name is required').max(200),
  emailAddress: z.string().max(100).optional().or(z.literal('')),
  phoneNumber: z.string().max(30).optional().or(z.literal('')),
  statement: z.string().max(3000).optional().or(z.literal('')),
  statementSigned: z.boolean(),
  interviewedById: z.string().optional().or(z.literal('')),
  interviewDate: z.string().optional().or(z.literal('')),
});
type WitnessForm = z.input<typeof witnessSchema>;
const emptyWitness: WitnessForm = {
  isEmployee: true,
  employeeId: '',
  name: '',
  emailAddress: '',
  phoneNumber: '',
  statement: '',
  statementSigned: false,
  interviewedById: '',
  interviewDate: '',
};

const teamSchema = z.object({
  employeeId: z.string().min(1, 'Choose an employee'),
  role: z.string().max(100),
});
type TeamForm = z.input<typeof teamSchema>;
const emptyTeam: TeamForm = { employeeId: '', role: '' };

const caSchema = z.object({
  actionDescription: z.string().min(1, 'Describe the action').max(1000),
  priority: z.enum(['Critical', 'High', 'Medium', 'Low']),
  status: z.enum(['Pending', 'InProgress', 'Completed', 'Verified', 'Overdue', 'Cancelled']),
  responsiblePersonId: z.string().min(1, 'Choose who owns this action'),
  dueDate: z.string().min(1, 'A due date is required'),
  completionDate: z.string().optional().or(z.literal('')),
  completionNotes: z.string().max(1000).optional().or(z.literal('')),
});
type CaForm = z.input<typeof caSchema>;
const emptyCa: CaForm = {
  actionDescription: '',
  priority: 'Medium',
  status: 'Pending',
  responsiblePersonId: '',
  dueDate: '',
  completionDate: '',
  completionNotes: '',
};

const followUpSchema = z.object({
  followUpDate: z.string().min(1, 'A date is required'),
  actionsTaken: z.string().max(1000).optional().or(z.literal('')),
  personCondition: z.string().max(500).optional().or(z.literal('')),
  notes: z.string().min(1, 'Notes are required').max(2000),
  furtherFollowUpRequired: z.boolean(),
  nextFollowUpDate: z.string().optional().or(z.literal('')),
  conductedById: z.string().min(1, 'Who conducted it?'),
});
type FollowUpForm = z.input<typeof followUpSchema>;
const emptyFollowUp: FollowUpForm = {
  followUpDate: new Date().toISOString().slice(0, 10),
  actionsTaken: '',
  personCondition: '',
  notes: '',
  furtherFollowUpRequired: false,
  nextFollowUpDate: '',
  conductedById: '',
};

const documentSchema = z.object({
  fileName: z.string().min(1, 'A file name is required').max(255),
  filePath: z.string().min(1, 'A file path is required').max(500),
  type: z.enum(['Photo', 'WitnessStatement', 'MedicalReport', 'InvestigationReport', 'RegulatoryNotification', 'InsuranceClaim', 'CCTV', 'Other']),
  description: z.string().max(500).optional().or(z.literal('')),
  uploadedById: z.string().min(1, 'Who uploaded it?'),
});
type DocumentForm = z.input<typeof documentSchema>;
const emptyDocument: DocumentForm = {
  fileName: '',
  filePath: '',
  type: 'Photo',
  description: '',
  uploadedById: '',
};

/**
 * One incident, end to end: triage, investigation, statutory notification, corrective actions,
 * follow-ups and closure. Severity-based approval routing (FR-SHE-224) is deliberately absent —
 * triage is manual — and the statutory banner below is the FR-SHE-103 flag: a reportable
 * incident that has not yet been notified shouts until someone records the notification.
 */
export default function IncidentDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [busy, setBusy] = useState(false);
  const [dialog, setDialog] = useState<
    'investigate' | 'findings' | 'notify' | 'claim' | 'review' | 'close' | 'submission' | null
  >(null);

  // Dialog fields (deliberately plain state — each dialog is 3-6 fields).
  const [leadId, setLeadId] = useState<string | null>(null);
  const [targetDate, setTargetDate] = useState('');
  const [rcMethod, setRcMethod] = useState('');
  const [rca, setRca] = useState('');
  const [findings, setFindings] = useState('');
  const [likelihoodAfter, setLikelihoodAfter] = useState('');
  const [severityAfter, setSeverityAfter] = useState('');
  const [bodyId, setBodyId] = useState('');
  const [authorityRef, setAuthorityRef] = useState('');
  const [notifiedById, setNotifiedById] = useState<string | null>(null);
  const [claimRef, setClaimRef] = useState('');
  const [claimAmount, setClaimAmount] = useState('');
  const [reviewerId, setReviewerId] = useState<string | null>(null);
  const [reviewComments, setReviewComments] = useState('');
  const [newStatus, setNewStatus] = useState('');
  const [closerId, setCloserId] = useState<string | null>(null);
  const [closureNotes, setClosureNotes] = useState('');
  const [lessonsLearned, setLessonsLearned] = useState('');
  // Statutory submissions (slice 15)
  const [subBodyId, setSubBodyId] = useState('');
  const [subType, setSubType] = useState<SheStatutorySubmissionType>('InitialNotification');
  const [subMethod, setSubMethod] = useState<SheStatutorySubmissionMethod>('OnlinePortal');
  const [subRef, setSubRef] = useState('');
  const [subDocPath, setSubDocPath] = useState('');
  const [subNotes, setSubNotes] = useState('');
  const [submitterId, setSubmitterId] = useState<string | null>(null);
  const [ackTarget, setAckTarget] = useState<SheStatutoryIncidentSubmission | null>(null);
  const [ackRef, setAckRef] = useState('');
  const [verifyCa, setVerifyCa] = useState<SafetyIncidentCorrectiveAction | null>(null);
  const [verifierId, setVerifierId] = useState<string | null>(null);
  const [verifyNotes, setVerifyNotes] = useState('');
  const [bpPersonId, setBpPersonId] = useState('');
  const [bpPartId, setBpPartId] = useState('');
  const [bpSide, setBpSide] = useState('');

  const { data: incident, isLoading, isError } = useQuery({
    queryKey: ['hr', 'safety-incidents', 'detail', id],
    queryFn: () => safetyIncidentService.getById(id),
    enabled: !!id,
  });

  const { data: injuryTypes = [] } = useQuery({
    queryKey: ['hr', 'safety-reference', 'injury-types', 'active'],
    queryFn: () => safetyReferenceService.getInjuryTypes(true),
  });
  const { data: bodyParts = [] } = useQuery({
    queryKey: ['hr', 'safety-reference', 'body-parts', 'active'],
    queryFn: () => safetyReferenceService.getBodyParts(true),
  });
  const { data: regulatoryBodies = [] } = useQuery({
    queryKey: ['hr', 'safety-reference', 'regulatory-bodies'],
    queryFn: () => safetyReferenceService.getRegulatoryBodies(true),
  });

  const detailKey = ['hr', 'safety-incidents', 'detail', id];
  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-incidents'] });
  };

  const act = async (label: string, fn: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await fn();
      await refresh();
      toast({ title: label });
      setDialog(null);
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || `${label} failed.`, variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }
  if (isError || !incident) {
    return (
      <div className="p-6">
        <EmptyState title="Incident not found" description="It may have been deleted." />
      </div>
    );
  }

  const inc: SafetyIncident = incident;
  const isClosed = inc.status === 'Closed';
  const statutoryPending = inc.reportableToAuthority && !inc.authorityNotificationDate;
  const openCas = inc.correctiveActions.filter(
    (a) => a.status !== 'Completed' && a.status !== 'Verified' && a.status !== 'Cancelled',
  ).length;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={inc.incidentNumber}
        description={`${inc.categoryName}${inc.incidentTypeName ? ` · ${inc.incidentTypeName}` : ''} — ${fmtDate(inc.incidentDate)}, ${inc.locationName ?? inc.specificArea ?? 'location unrecorded'}.`}
        backHref="/hr/safety/incidents"
        actions={
          <div className="flex flex-wrap gap-2">
            {!isClosed && (
              <>
                <Button variant="outline" onClick={() => setDialog('investigate')}>
                  <Search className="mr-2 h-4 w-4" />
                  {inc.requiresInvestigation ? 'Reassign investigation' : 'Assign investigation'}
                </Button>
                <Button variant="outline" onClick={() => setDialog('findings')}>
                  <ClipboardPen className="mr-2 h-4 w-4" />
                  Record findings
                </Button>
                <Button variant="outline" onClick={() => setDialog('claim')}>
                  <HandCoins className="mr-2 h-4 w-4" />
                  Insurance claim
                </Button>
              </>
            )}
            <Button variant="outline" onClick={() => setDialog('review')}>
              <Stamp className="mr-2 h-4 w-4" />
              Review
            </Button>
            {!isClosed && (
              <Button onClick={() => setDialog('close')} disabled={openCas > 0}
                title={openCas > 0 ? `${openCas} corrective action(s) still open` : undefined}>
                <CheckCircle2 className="mr-2 h-4 w-4" />
                Close incident
              </Button>
            )}
            <Button
              variant="destructive"
              disabled={busy}
              onClick={() => {
                if (!window.confirm('Delete this incident and everything on it?')) return;
                void act('Incident deleted', async () => {
                  await safetyIncidentService.remove(inc.id);
                  router.push('/hr/safety/incidents');
                });
              }}
            >
              <Trash2 className="mr-2 h-4 w-4" />
              Delete
            </Button>
          </div>
        }
      />

      {statutoryPending && (
        <Card className="border-red-300 bg-red-50 dark:bg-red-950/20">
          <CardContent className="flex items-center justify-between gap-4 py-4">
            <div className="flex items-center gap-3">
              <AlertTriangle className="h-5 w-5 shrink-0 text-red-600" />
              <div>
                <p className="font-medium text-red-900 dark:text-red-200">
                  Statutorily reportable — authority not yet notified
                </p>
                <p className="text-sm text-red-800/80 dark:text-red-300/80">
                  {inc.incidentTypeName
                    ? `Incidents of type "${inc.incidentTypeName}" must be reported to the authority.`
                    : 'This incident is flagged as reportable to a regulatory authority.'}{' '}
                  Record the notification as soon as it has been made.
                </p>
              </div>
            </div>
            <Button onClick={() => setDialog('notify')}>
              <Megaphone className="mr-2 h-4 w-4" />
              Record notification
            </Button>
          </CardContent>
        </Card>
      )}

      <div className="flex flex-wrap items-center gap-2">
        <StatusBadge status={inc.statusName} />
        <Badge variant={inc.severity === 'Major' || inc.severity === 'Catastrophic' ? 'destructive' : 'secondary'}>
          {inc.severityName}
        </Badge>
        {inc.isLostTimeInjury && (
          <Badge variant="destructive">Lost-time injury · {inc.totalLostDays} day(s)</Badge>
        )}
        {inc.reportableToAuthority && inc.authorityNotificationDate && (
          <Badge variant="secondary">
            Notified {inc.reportedToBodyName ?? 'authority'} on {fmtDate(inc.authorityNotificationDate)}
            {inc.authorityReferenceNumber ? ` · ${inc.authorityReferenceNumber}` : ''}
          </Badge>
        )}
        <Badge variant="outline">
          Reported by {inc.reportedByName || '—'} on {fmtDate(inc.reportedDate)}
        </Badge>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">What happened</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <p className="whitespace-pre-wrap text-sm">{inc.description}</p>
            <InfoRow label="Specific area" value={inc.specificArea} />
            <InfoRow label="Immediate cause" value={inc.immediateCause} />
            <InfoRow label="Contributing factors" value={inc.contributingFactors} />
            <InfoRow label="Immediate action taken" value={inc.immediateActionTaken} />
            <InfoRow
              label="Could have caused injury"
              value={inc.couldHaveCausedInjury ? `Yes — ${inc.potentialConsequence ?? 'unspecified'}` : 'No'}
            />
            <InfoRow
              label="Risk score (before)"
              value={inc.riskScoreBefore != null ? `${inc.riskScoreBefore} (L${inc.likelihoodBefore} × S${inc.severityBefore})` : null}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Investigation & governance</CardTitle>
          </CardHeader>
          <CardContent className="space-y-1">
            <InfoRow label="Lead investigator" value={inc.leadInvestigatorName} />
            <InfoRow label="Started" value={fmtDate(inc.investigationStartDate)} />
            <InfoRow label="Target" value={fmtDate(inc.investigationTargetDate)} />
            <InfoRow label="Completed" value={fmtDate(inc.investigationCompleteDate)} />
            <InfoRow label="Method" value={inc.rootCauseMethodName} />
            <InfoRow label="Root cause" value={inc.rootCauseAnalysis} />
            <InfoRow label="Findings" value={inc.investigationFindings} />
            <InfoRow
              label="Risk score (after)"
              value={inc.riskScoreAfter != null ? `${inc.riskScoreAfter} (L${inc.likelihoodAfter} × S${inc.severityAfter})` : null}
            />
            <InfoRow
              label="Reviewed"
              value={inc.reviewedByName ? `${inc.reviewedByName}, ${fmtDate(inc.reviewedDate)}` : null}
            />
            <InfoRow label="Review comments" value={inc.reviewComments} />
            {inc.insuranceClaimFiled && (
              <InfoRow
                label="Insurance claim"
                value={`${inc.claimReferenceNumber ?? 'filed'} · ${inc.claimApproved ? 'approved' : 'pending'}${inc.amountPaid != null ? ` · paid ${inc.amountPaid}` : ''}`}
              />
            )}
            {isClosed && (
              <InfoRow
                label="Closed"
                value={`${inc.closedByName ?? '—'}, ${fmtDate(inc.closedDate)}${inc.closureNotes ? ` — ${inc.closureNotes}` : ''}`}
              />
            )}
            <InfoRow label="Lessons learned" value={inc.lessonsLearned} />
          </CardContent>
        </Card>
      </div>

      {/* Lane 6: the reverse ER link. HR-desk screen only — the read is ER-permission gated. */}
      <LinkedErCasesPanel source="SafetyIncident" recordId={id} />

      <Tabs defaultValue="persons">
        <TabsList>
          <TabsTrigger value="persons">People ({inc.involvedPersons.length})</TabsTrigger>
          <TabsTrigger value="witnesses">Witnesses ({inc.witnesses.length})</TabsTrigger>
          <TabsTrigger value="team">Investigation team ({inc.investigationTeam.length})</TabsTrigger>
          <TabsTrigger value="cas">
            Corrective actions ({inc.correctiveActions.length}
            {openCas > 0 ? `, ${openCas} open` : ''})
          </TabsTrigger>
          <TabsTrigger value="followups">Follow-ups ({inc.followUps.length})</TabsTrigger>
          <TabsTrigger value="documents">Documents ({inc.documents.length})</TabsTrigger>
          <TabsTrigger value="statutory">Statutory ({inc.statutorySubmissions.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="statutory" className="space-y-4">
          {inc.reportableToAuthority ? (
            <Button
              variant="outline"
              onClick={() => {
                setSubBodyId('');
                setSubRef('');
                setSubDocPath('');
                setSubNotes('');
                setSubmitterId(null);
                setDialog('submission');
              }}
            >
              Record statutory submission
            </Button>
          ) : (
            <p className="text-muted-foreground text-sm">
              This incident is not flagged as reportable to an authority — submissions are recorded
              only for reportable incidents (flag it via edit, or use Notify authority).
            </p>
          )}
          {inc.statutorySubmissions.length === 0 ? (
            <p className="text-muted-foreground text-sm">
              No submissions recorded{inc.reportableToAuthority ? ' — the statutory duty is still pending.' : '.'}
            </p>
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Authority</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Method</TableHead>
                      <TableHead>Submitted</TableHead>
                      <TableHead>Reference</TableHead>
                      <TableHead>Acknowledged</TableHead>
                      <TableHead />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {inc.statutorySubmissions.map((s) => (
                      <TableRow key={s.id}>
                        <TableCell className="font-medium">{s.regulatoryBodyName}</TableCell>
                        <TableCell>{s.typeName}</TableCell>
                        <TableCell>{s.methodName}</TableCell>
                        <TableCell className="tabular-nums">
                          {fmtDate(s.submissionDate)}
                          <span className="text-muted-foreground ml-1 text-xs">by {s.submittedByName}</span>
                        </TableCell>
                        <TableCell className="font-mono text-sm">{s.referenceNumber ?? '—'}</TableCell>
                        <TableCell>
                          {s.acknowledgementReceived ? (
                            <Badge variant="secondary">
                              {fmtDate(s.acknowledgementDate)}
                              {s.acknowledgementReference ? ` · ${s.acknowledgementReference}` : ''}
                            </Badge>
                          ) : (
                            <Badge variant="outline">Pending</Badge>
                          )}
                        </TableCell>
                        <TableCell className="text-right">
                          {!s.acknowledgementReceived && (
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => {
                                setAckTarget(s);
                                setAckRef('');
                              }}
                            >
                              Record acknowledgement
                            </Button>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="persons" className="space-y-4">
          <ResourceCollectionTab<SafetyIncidentInvolvedPerson, PersonForm>
            parentId={inc.id}
            title="involved persons"
            singular="involved person"
            queryKey={[...detailKey, 'persons']}
            invalidateKeys={[detailKey]}
            list={async () => (await safetyIncidentService.getById(inc.id)).involvedPersons}
            create={(incidentId, values) => {
              const v = personSchema.parse(values);
              return safetyIncidentService.addInvolvedPerson(incidentId, {
                incidentId,
                isEmployee: v.isEmployee,
                employeeId: blank(v.employeeId),
                fullName: v.fullName,
                organizationOrCompany: blank(v.organizationOrCompany),
                roleInIncident: v.roleInIncident,
                wasOnDuty: v.wasOnDuty,
                activityBeingPerformed: blank(v.activityBeingPerformed),
                wasUsingPpe: v.wasUsingPpe,
                ppeUsed: blank(v.ppeUsed),
                ppeWasAdequate: v.ppeWasAdequate,
                wasInjured: v.wasInjured,
                injuryDescription: v.wasInjured ? blank(v.injuryDescription) : null,
                injuryClassification: v.wasInjured && v.injuryClassification ? v.injuryClassification : null,
                injuryTypeId: v.wasInjured ? blank(v.injuryTypeId) : null,
                isFatal: v.wasInjured && v.isFatal,
                firstAidGiven: false,
                medicalTreatmentRequired: false,
                resultedInTimeOff: v.resultedInTimeOff,
                lostDays: v.resultedInTimeOff ? (v.lostDays ?? null) : null,
                onLightDuty: false,
              });
            }}
            update={(_incidentId, personId, values) => {
              const v = personSchema.parse(values);
              return safetyIncidentService.updateInvolvedPerson(personId, {
                id: personId,
                fullName: v.fullName,
                organizationOrCompany: blank(v.organizationOrCompany),
                roleInIncident: v.roleInIncident,
                wasOnDuty: v.wasOnDuty,
                activityBeingPerformed: blank(v.activityBeingPerformed),
                wasUsingPpe: v.wasUsingPpe,
                ppeUsed: blank(v.ppeUsed),
                ppeWasAdequate: v.ppeWasAdequate,
                wasInjured: v.wasInjured,
                injuryDescription: v.wasInjured ? blank(v.injuryDescription) : null,
                injuryClassification: v.wasInjured && v.injuryClassification ? v.injuryClassification : null,
                injuryTypeId: v.wasInjured ? blank(v.injuryTypeId) : null,
                isFatal: v.wasInjured && v.isFatal,
                firstAidGiven: false,
                medicalTreatmentRequired: false,
                resultedInTimeOff: v.resultedInTimeOff,
                lostDays: v.resultedInTimeOff ? (v.lostDays ?? null) : null,
                onLightDuty: false,
              });
            }}
            remove={(_incidentId, personId) => safetyIncidentService.removeInvolvedPerson(personId)}
            getId={(p) => p.id}
            emptyDescription="Nobody recorded yet. Add everyone affected — the lost-time roll-up derives from these rows."
            columns={[
              {
                header: 'Name',
                cell: (p) => (
                  <div>
                    <span className="font-medium">{p.fullName}</span>
                    {p.employeeNumber && (
                      <span className="text-muted-foreground ml-2 text-xs">{p.employeeNumber}</span>
                    )}
                  </div>
                ),
              },
              { header: 'Role', cell: (p) => p.roleInIncidentName },
              {
                header: 'Injury',
                cell: (p) =>
                  p.wasInjured ? (
                    <Badge variant={p.isFatal ? 'destructive' : 'secondary'}>
                      {p.isFatal ? 'Fatal' : (p.injuryClassificationName ?? 'Injured')}
                      {p.injuryTypeName ? ` · ${p.injuryTypeName}` : ''}
                    </Badge>
                  ) : (
                    <span className="text-muted-foreground">—</span>
                  ),
              },
              {
                header: 'Body parts',
                cell: (p) =>
                  p.injuredBodyParts.length > 0
                    ? p.injuredBodyParts
                        .map((b) => `${b.bodyPartName}${b.sideName ? ` (${b.sideName})` : ''}`)
                        .join(', ')
                    : '—',
              },
              { header: 'Lost days', cell: (p) => p.lostDays ?? 0 },
            ]}
            schema={personSchema}
            emptyForm={emptyPerson}
            toForm={(p) => ({
              isEmployee: p.isEmployee,
              employeeId: p.employeeId ?? '',
              fullName: p.fullName,
              organizationOrCompany: p.organizationOrCompany ?? '',
              roleInIncident: p.roleInIncident,
              wasOnDuty: p.wasOnDuty,
              activityBeingPerformed: p.activityBeingPerformed ?? '',
              wasUsingPpe: p.wasUsingPpe,
              ppeUsed: p.ppeUsed ?? '',
              ppeWasAdequate: p.ppeWasAdequate,
              wasInjured: p.wasInjured,
              injuryDescription: p.injuryDescription ?? '',
              injuryClassification: p.injuryClassification ?? '',
              injuryTypeId: p.injuryTypeId ?? '',
              isFatal: p.isFatal,
              resultedInTimeOff: p.resultedInTimeOff,
              lostDays: p.lostDays ?? undefined,
            })}
            renderFields={(form, editing) => {
              const isEmployee = !!form.watch('isEmployee');
              const wasInjured = !!form.watch('wasInjured');
              const timeOff = !!form.watch('resultedInTimeOff');
              return (
                <div className="space-y-4">
                  {!editing && (
                    <SwitchField form={form} name="isEmployee" label="Is an employee" />
                  )}
                  {!editing && isEmployee && (
                    <EmployeePickerField form={form} name="employeeId" label="Employee" />
                  )}
                  <FieldRow>
                    <TextField form={form} name="fullName" label="Full name" required />
                    <SelectField
                      form={form}
                      name="roleInIncident"
                      label="Role in incident"
                      required
                      options={SHE_INVOLVED_PERSON_ROLE_OPTIONS}
                    />
                  </FieldRow>
                  {!isEmployee && (
                    <TextField form={form} name="organizationOrCompany" label="Organisation / company" />
                  )}
                  <TextField form={form} name="activityBeingPerformed" label="Activity at the time" />
                  <FieldRow>
                    <SwitchField form={form} name="wasOnDuty" label="Was on duty" />
                    <SwitchField form={form} name="wasUsingPpe" label="Was using PPE" />
                  </FieldRow>
                  {!!form.watch('wasUsingPpe') && (
                    <FieldRow>
                      <TextField form={form} name="ppeUsed" label="PPE used" />
                      <SwitchField form={form} name="ppeWasAdequate" label="PPE was adequate" />
                    </FieldRow>
                  )}
                  <SwitchField form={form} name="wasInjured" label="Was injured" />
                  {wasInjured && (
                    <>
                      <FieldRow>
                        <SelectField
                          form={form}
                          name="injuryClassification"
                          label="Classification"
                          allowEmpty
                          emptyLabel="Unclassified"
                          options={SHE_INJURY_CLASSIFICATION_OPTIONS}
                        />
                        <SelectField
                          form={form}
                          name="injuryTypeId"
                          label="Injury type"
                          allowEmpty
                          emptyLabel="Not set"
                          options={injuryTypes.map((t) => ({ value: t.id, label: t.name }))}
                        />
                      </FieldRow>
                      <TextareaField form={form} name="injuryDescription" label="Injury description" rows={2} />
                      <FieldRow>
                        <SwitchField form={form} name="isFatal" label="Fatal" />
                        <SwitchField form={form} name="resultedInTimeOff" label="Resulted in time off" />
                      </FieldRow>
                      {timeOff && <NumberField form={form} name="lostDays" label="Lost days" />}
                    </>
                  )}
                </div>
              );
            }}
          />

          {inc.involvedPersons.some((p) => p.wasInjured) && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Add injured body part</CardTitle>
              </CardHeader>
              <CardContent className="flex flex-wrap items-end gap-3">
                <div className="min-w-48 space-y-2">
                  <Label>Person</Label>
                  <Select value={bpPersonId} onValueChange={setBpPersonId}>
                    <SelectTrigger><SelectValue placeholder="Choose" /></SelectTrigger>
                    <SelectContent>
                      {inc.involvedPersons.filter((p) => p.wasInjured).map((p) => (
                        <SelectItem key={p.id} value={p.id}>{p.fullName}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="min-w-48 space-y-2">
                  <Label>Body part</Label>
                  <Select value={bpPartId} onValueChange={setBpPartId}>
                    <SelectTrigger><SelectValue placeholder="Choose" /></SelectTrigger>
                    <SelectContent>
                      {bodyParts.map((b) => (
                        <SelectItem key={b.id} value={b.id}>{b.name}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="min-w-36 space-y-2">
                  <Label>Side</Label>
                  <Select value={bpSide} onValueChange={setBpSide}>
                    <SelectTrigger><SelectValue placeholder="—" /></SelectTrigger>
                    <SelectContent>
                      {SHE_BODY_SIDE_OPTIONS.map((o) => (
                        <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <Button
                  disabled={busy || !bpPersonId || !bpPartId}
                  onClick={() =>
                    act('Body part recorded', async () => {
                      await safetyIncidentService.addInjuredBodyPart(bpPersonId, {
                        involvedPersonId: bpPersonId,
                        bodyPartId: bpPartId,
                        side: (bpSide || null) as SheBodySide | null,
                      });
                      setBpPartId('');
                      setBpSide('');
                    })
                  }
                >
                  Add
                </Button>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="witnesses">
          <ResourceCollectionTab<SafetyIncidentWitness, WitnessForm>
            parentId={inc.id}
            title="witnesses"
            singular="witness"
            queryKey={[...detailKey, 'witnesses']}
            invalidateKeys={[detailKey]}
            list={async () => (await safetyIncidentService.getById(inc.id)).witnesses}
            create={(incidentId, values) => {
              const v = witnessSchema.parse(values);
              return safetyIncidentService.addWitness(incidentId, {
                incidentId,
                isEmployee: v.isEmployee,
                employeeId: blank(v.employeeId),
                name: v.name,
                emailAddress: blank(v.emailAddress),
                phoneNumber: blank(v.phoneNumber),
                statement: blank(v.statement),
                statementDate: v.statement ? new Date().toISOString() : null,
                statementSigned: v.statementSigned,
                interviewedById: blank(v.interviewedById),
                interviewDate: blank(v.interviewDate),
              });
            }}
            update={(_incidentId, witnessId, values) => {
              const v = witnessSchema.parse(values);
              return safetyIncidentService.updateWitness(witnessId, {
                id: witnessId,
                name: v.name,
                emailAddress: blank(v.emailAddress),
                phoneNumber: blank(v.phoneNumber),
                statement: blank(v.statement),
                statementDate: v.statement ? new Date().toISOString() : null,
                statementSigned: v.statementSigned,
                interviewedById: blank(v.interviewedById),
                interviewDate: blank(v.interviewDate),
              });
            }}
            remove={(_incidentId, witnessId) => safetyIncidentService.removeWitness(witnessId)}
            getId={(w) => w.id}
            emptyDescription="No witnesses recorded."
            columns={[
              { header: 'Name', cell: (w) => <span className="font-medium">{w.name}</span> },
              { header: 'Contact', cell: (w) => w.phoneNumber ?? w.emailAddress ?? '—' },
              {
                header: 'Statement',
                cell: (w) =>
                  w.statement ? (
                    <Badge variant={w.statementSigned ? 'secondary' : 'outline'}>
                      {w.statementSigned ? 'Signed' : 'Unsigned'}
                    </Badge>
                  ) : (
                    '—'
                  ),
              },
              {
                header: 'Interviewed',
                cell: (w) => (w.interviewedByName ? `${w.interviewedByName}, ${fmtDate(w.interviewDate)}` : '—'),
              },
            ]}
            schema={witnessSchema}
            emptyForm={emptyWitness}
            toForm={(w) => ({
              isEmployee: w.isEmployee,
              employeeId: w.employeeId ?? '',
              name: w.name,
              emailAddress: w.emailAddress ?? '',
              phoneNumber: w.phoneNumber ?? '',
              statement: w.statement ?? '',
              statementSigned: w.statementSigned,
              interviewedById: w.interviewedById ?? '',
              interviewDate: w.interviewDate ? w.interviewDate.slice(0, 10) : '',
            })}
            renderFields={(form, editing) => (
              <div className="space-y-4">
                {!editing && <SwitchField form={form} name="isEmployee" label="Is an employee" />}
                {!editing && !!form.watch('isEmployee') && (
                  <EmployeePickerField form={form} name="employeeId" label="Employee" />
                )}
                <TextField form={form} name="name" label="Name" required />
                <FieldRow>
                  <TextField form={form} name="emailAddress" label="Email" type="email" />
                  <TextField form={form} name="phoneNumber" label="Phone" type="tel" />
                </FieldRow>
                <TextareaField form={form} name="statement" label="Statement" rows={3} />
                <SwitchField form={form} name="statementSigned" label="Statement signed" />
                <FieldRow>
                  <EmployeePickerField form={form} name="interviewedById" label="Interviewed by" />
                  <DateField form={form} name="interviewDate" label="Interview date" />
                </FieldRow>
              </div>
            )}
          />
        </TabsContent>

        <TabsContent value="team">
          <ResourceCollectionTab<SafetyIncidentInvestigationTeamMember, TeamForm>
            parentId={inc.id}
            title="investigation team"
            singular="team member"
            queryKey={[...detailKey, 'team']}
            invalidateKeys={[detailKey]}
            allowUpdate={false}
            list={async () => (await safetyIncidentService.getById(inc.id)).investigationTeam}
            create={(incidentId, values) => {
              const v = teamSchema.parse(values);
              return safetyIncidentService.addInvestigationTeamMember(incidentId, {
                incidentId,
                employeeId: v.employeeId,
                role: v.role,
              });
            }}
            update={() => Promise.reject(new Error('Team members are added and removed, not edited.'))}
            remove={(_incidentId, memberId) => safetyIncidentService.removeInvestigationTeamMember(memberId)}
            getId={(m) => m.id}
            emptyDescription="No team yet — the lead investigator can work alone, but serious incidents warrant a panel."
            columns={[
              { header: 'Member', cell: (m) => <span className="font-medium">{m.employeeName}</span> },
              { header: 'Role', cell: (m) => m.role || '—' },
              { header: 'Joined', cell: (m) => fmtDate(m.joinedDate) },
            ]}
            schema={teamSchema}
            emptyForm={emptyTeam}
            toForm={(m) => ({ employeeId: m.employeeId, role: m.role })}
            renderFields={(form) => (
              <div className="space-y-4">
                <EmployeePickerField form={form} name="employeeId" label="Employee" required />
                <TextField form={form} name="role" label="Role on the team" placeholder="e.g. Technical expert" />
              </div>
            )}
          />
        </TabsContent>

        <TabsContent value="cas">
          <ResourceCollectionTab<SafetyIncidentCorrectiveAction, CaForm>
            parentId={inc.id}
            title="corrective actions"
            singular="corrective action"
            queryKey={[...detailKey, 'cas']}
            invalidateKeys={[detailKey]}
            list={async () => (await safetyIncidentService.getById(inc.id)).correctiveActions}
            create={(incidentId, values) => {
              const v = caSchema.parse(values);
              return safetyIncidentService.addCorrectiveAction(incidentId, {
                incidentId,
                actionDescription: v.actionDescription,
                priority: v.priority,
                responsiblePersonId: v.responsiblePersonId,
                dueDate: v.dueDate,
              });
            }}
            update={(_incidentId, actionId, values) => {
              const v = caSchema.parse(values);
              return safetyIncidentService.updateCorrectiveAction(actionId, {
                id: actionId,
                actionDescription: v.actionDescription,
                priority: v.priority,
                status: v.status,
                responsiblePersonId: v.responsiblePersonId,
                dueDate: v.dueDate,
                completionDate: blank(v.completionDate),
                completionNotes: blank(v.completionNotes),
              });
            }}
            remove={(_incidentId, actionId) => safetyIncidentService.removeCorrectiveAction(actionId)}
            getId={(a) => a.id}
            emptyDescription="No corrective actions. An incident type with defaults would have populated some at creation."
            columns={[
              {
                header: 'Action',
                cell: (a) => (
                  <div className="max-w-md">
                    <span className="font-medium">{a.actionDescription}</span>
                    {a.incidentTypeCorrectiveActionId && (
                      <Badge variant="outline" className="ml-2">auto</Badge>
                    )}
                  </div>
                ),
              },
              { header: 'Priority', cell: (a) => a.priorityName },
              { header: 'Owner', cell: (a) => a.responsiblePersonName },
              {
                header: 'Due',
                cell: (a) => (
                  <span className={a.isOverdue ? 'font-medium text-red-600' : ''}>
                    {fmtDate(a.dueDate)}
                  </span>
                ),
              },
              {
                header: 'Status',
                cell: (a) => (
                  <div className="flex items-center gap-1">
                    <StatusBadge status={a.statusName} />
                    {a.effectivenessVerified && <CheckCircle2 className="h-4 w-4 text-green-600" />}
                  </div>
                ),
              },
            ]}
            actions={[
              {
                label: 'Verify effectiveness',
                visible: (a) => a.status === 'Completed' && !a.effectivenessVerified,
                run: async (a) => {
                  setVerifyCa(a);
                  return Promise.resolve();
                },
              },
            ]}
            schema={caSchema}
            emptyForm={emptyCa}
            toForm={(a) => ({
              actionDescription: a.actionDescription,
              priority: a.priority,
              status: a.status,
              responsiblePersonId: a.responsiblePersonId,
              dueDate: a.dueDate.slice(0, 10),
              completionDate: a.completionDate ? a.completionDate.slice(0, 10) : '',
              completionNotes: a.completionNotes ?? '',
            })}
            renderFields={(form, editing) => (
              <div className="space-y-4">
                <TextareaField form={form} name="actionDescription" label="Action" rows={2} required />
                <FieldRow>
                  <SelectField
                    form={form}
                    name="priority"
                    label="Priority"
                    required
                    options={SHE_CORRECTIVE_ACTION_PRIORITY_OPTIONS}
                  />
                  <DateField form={form} name="dueDate" label="Due date" required />
                </FieldRow>
                <EmployeePickerField form={form} name="responsiblePersonId" label="Responsible person" required />
                {editing && (
                  <>
                    <SelectField
                      form={form}
                      name="status"
                      label="Status"
                      required
                      options={SHE_CORRECTIVE_ACTION_STATUS_OPTIONS}
                    />
                    <FieldRow>
                      <DateField form={form} name="completionDate" label="Completion date" />
                    </FieldRow>
                    <TextareaField form={form} name="completionNotes" label="Completion notes" rows={2} />
                  </>
                )}
              </div>
            )}
          />
        </TabsContent>

        <TabsContent value="followups">
          <ResourceCollectionTab<SafetyIncidentFollowUp, FollowUpForm>
            parentId={inc.id}
            title="follow-ups"
            singular="follow-up"
            queryKey={[...detailKey, 'followups']}
            invalidateKeys={[detailKey]}
            allowUpdate={false}
            list={async () => (await safetyIncidentService.getById(inc.id)).followUps}
            create={(incidentId, values) => {
              const v = followUpSchema.parse(values);
              return safetyIncidentService.addFollowUp(incidentId, {
                incidentId,
                followUpDate: v.followUpDate,
                actionsTaken: blank(v.actionsTaken),
                personCondition: blank(v.personCondition),
                notes: v.notes,
                furtherFollowUpRequired: v.furtherFollowUpRequired,
                nextFollowUpDate: v.furtherFollowUpRequired ? blank(v.nextFollowUpDate) : null,
                conductedById: v.conductedById,
              });
            }}
            update={() => Promise.reject(new Error('Follow-ups are a log — add a new one instead.'))}
            getId={(f) => f.id}
            emptyDescription="No follow-ups logged."
            columns={[
              { header: 'Date', cell: (f) => fmtDate(f.followUpDate) },
              { header: 'Notes', cell: (f) => <span className="line-clamp-2 max-w-md">{f.notes}</span> },
              { header: 'Condition', cell: (f) => f.personCondition ?? '—' },
              { header: 'By', cell: (f) => f.conductedByName },
              {
                header: 'Next',
                cell: (f) => (f.furtherFollowUpRequired ? fmtDate(f.nextFollowUpDate) : '—'),
              },
            ]}
            schema={followUpSchema}
            emptyForm={emptyFollowUp}
            toForm={(f) => ({
              followUpDate: f.followUpDate.slice(0, 10),
              actionsTaken: f.actionsTaken ?? '',
              personCondition: f.personCondition ?? '',
              notes: f.notes,
              furtherFollowUpRequired: f.furtherFollowUpRequired,
              nextFollowUpDate: f.nextFollowUpDate ? f.nextFollowUpDate.slice(0, 10) : '',
              conductedById: f.conductedById,
            })}
            renderFields={(form) => (
              <div className="space-y-4">
                <FieldRow>
                  <DateField form={form} name="followUpDate" label="Date" required />
                  <EmployeePickerField form={form} name="conductedById" label="Conducted by" required />
                </FieldRow>
                <TextareaField form={form} name="notes" label="Notes" rows={3} required />
                <TextField form={form} name="actionsTaken" label="Actions taken" />
                <TextField form={form} name="personCondition" label="Person's condition" />
                <SwitchField form={form} name="furtherFollowUpRequired" label="Further follow-up required" />
                {!!form.watch('furtherFollowUpRequired') && (
                  <DateField form={form} name="nextFollowUpDate" label="Next follow-up" />
                )}
              </div>
            )}
          />
        </TabsContent>

        <TabsContent value="documents">
          <ResourceCollectionTab<SafetyIncidentDocument, DocumentForm>
            parentId={inc.id}
            title="documents"
            singular="document"
            queryKey={[...detailKey, 'documents']}
            invalidateKeys={[detailKey]}
            allowUpdate={false}
            list={async () => (await safetyIncidentService.getById(inc.id)).documents}
            create={(incidentId, values) => {
              const v = documentSchema.parse(values);
              return safetyIncidentService.addDocument(incidentId, {
                incidentId,
                fileName: v.fileName,
                filePath: v.filePath,
                type: v.type,
                description: blank(v.description),
                uploadedById: v.uploadedById,
              });
            }}
            update={() => Promise.reject(new Error('Documents are added and removed, not edited.'))}
            remove={(_incidentId, documentId) => safetyIncidentService.removeDocument(documentId)}
            getId={(d) => d.id}
            emptyDescription="No documents attached — photos, statements and reports belong here."
            dialogHint="Paths reference the document store; the shared upload flow lands with the attachment work."
            columns={[
              { header: 'File', cell: (d) => <span className="font-medium">{d.fileName}</span> },
              { header: 'Type', cell: (d) => d.typeName },
              { header: 'Description', cell: (d) => d.description ?? '—' },
              { header: 'Uploaded', cell: (d) => `${d.uploadedByName}, ${fmtDate(d.uploadDate)}` },
            ]}
            schema={documentSchema}
            emptyForm={emptyDocument}
            toForm={(d) => ({
              fileName: d.fileName,
              filePath: d.filePath,
              type: d.type,
              description: d.description ?? '',
              uploadedById: d.uploadedById,
            })}
            renderFields={(form) => (
              <div className="space-y-4">
                <FieldRow>
                  <TextField form={form} name="fileName" label="File name" required />
                  <SelectField
                    form={form}
                    name="type"
                    label="Type"
                    required
                    options={SHE_INCIDENT_DOCUMENT_TYPE_OPTIONS}
                  />
                </FieldRow>
                <TextField form={form} name="filePath" label="File path" required />
                <TextField form={form} name="description" label="Description" />
                <EmployeePickerField form={form} name="uploadedById" label="Uploaded by" required />
              </div>
            )}
          />
        </TabsContent>
      </Tabs>

      {/* ── Workflow dialogs ── */}

      <Dialog open={dialog === 'investigate'} onOpenChange={(o) => !o && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Assign investigation</DialogTitle>
            <DialogDescription>Moves the incident to Investigation In Progress.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Lead investigator</Label>
              <EmployeePicker value={leadId} onChange={(v) => setLeadId(v)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="target-date">Target completion</Label>
              <Input id="target-date" type="date" value={targetDate} onChange={(e) => setTargetDate(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Root-cause method</Label>
              <Select value={rcMethod} onValueChange={setRcMethod}>
                <SelectTrigger><SelectValue placeholder="Choose later" /></SelectTrigger>
                <SelectContent>
                  {SHE_ROOT_CAUSE_METHOD_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || !leadId}
              onClick={() => {
                if (!leadId) return;
                void act('Investigation assigned', () =>
                  safetyIncidentService.assignInvestigation(inc.id, {
                    incidentId: inc.id,
                    leadInvestigatorId: leadId,
                    investigationTargetDate: blank(targetDate),
                    rootCauseMethod: (rcMethod || null) as never,
                  }),
                );
              }}
            >
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Assign
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={dialog === 'findings'} onOpenChange={(o) => !o && setDialog(null)}>
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>Record investigation findings</DialogTitle>
            <DialogDescription>
              Completing the investigation moves the incident to Pending Corrective Actions.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="rca">Root-cause analysis</Label>
              <Textarea id="rca" rows={3} value={rca} onChange={(e) => setRca(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="findings">Findings</Label>
              <Textarea id="findings" rows={3} value={findings} onChange={(e) => setFindings(e.target.value)} />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label htmlFor="l-after">Likelihood after (1–5)</Label>
                <Input id="l-after" type="number" min={1} max={5} value={likelihoodAfter} onChange={(e) => setLikelihoodAfter(e.target.value)} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="s-after">Severity after (1–5)</Label>
                <Input id="s-after" type="number" min={1} max={5} value={severityAfter} onChange={(e) => setSeverityAfter(e.target.value)} />
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy}
              onClick={() =>
                act('Findings recorded', () =>
                  safetyIncidentService.recordInvestigation(inc.id, {
                    incidentId: inc.id,
                    rootCauseAnalysis: blank(rca),
                    investigationFindings: blank(findings),
                    likelihoodAfter: likelihoodAfter ? Number(likelihoodAfter) : null,
                    severityAfter: severityAfter ? Number(severityAfter) : null,
                  }),
                )
              }
            >
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={dialog === 'notify'} onOpenChange={(o) => !o && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record authority notification</DialogTitle>
            <DialogDescription>
              The statutory submission itself happens outside the system for now — this records
              that it was made, to whom, and under what reference.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Regulatory body</Label>
              <Select value={bodyId} onValueChange={setBodyId}>
                <SelectTrigger><SelectValue placeholder="Choose" /></SelectTrigger>
                <SelectContent>
                  {regulatoryBodies.map((b) => (
                    <SelectItem key={b.id} value={b.id}>
                      {b.shortName ? `${b.shortName} — ${b.name}` : b.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="authority-ref">Authority reference number</Label>
              <Input id="authority-ref" value={authorityRef} onChange={(e) => setAuthorityRef(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Notified by</Label>
              <EmployeePicker value={notifiedById} onChange={(v) => setNotifiedById(v)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || !bodyId || !notifiedById}
              onClick={() => {
                if (!notifiedById) return;
                void act('Notification recorded', () =>
                  safetyIncidentService.notifyAuthority(inc.id, {
                    incidentId: inc.id,
                    reportedToBodyId: bodyId,
                    authorityReferenceNumber: blank(authorityRef),
                    authorityNotifiedById: notifiedById,
                  }),
                );
              }}
            >
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={dialog === 'claim'} onOpenChange={(o) => !o && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Insurance claim</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="claim-ref">Claim reference</Label>
              <Input id="claim-ref" value={claimRef} onChange={(e) => setClaimRef(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="claim-amount">Claim amount</Label>
              <Input id="claim-amount" type="number" min={0} step="0.01" value={claimAmount} onChange={(e) => setClaimAmount(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy}
              onClick={() =>
                act('Claim recorded', () =>
                  safetyIncidentService.fileClaim(inc.id, {
                    incidentId: inc.id,
                    claimReferenceNumber: blank(claimRef),
                    claimAmount: claimAmount ? Number(claimAmount) : null,
                    claimApproved: false,
                  }),
                )
              }
            >
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={dialog === 'review'} onOpenChange={(o) => !o && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Governance review</DialogTitle>
            <DialogDescription>
              Severity-based approval routing is not built yet — the reviewer decides the next
              status by hand, including reopening a closed incident.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Reviewed by</Label>
              <EmployeePicker value={reviewerId} onChange={(v) => setReviewerId(v)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="review-comments">Comments</Label>
              <Textarea id="review-comments" rows={3} value={reviewComments} onChange={(e) => setReviewComments(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Move to status</Label>
              <Select value={newStatus} onValueChange={setNewStatus}>
                <SelectTrigger><SelectValue placeholder="Leave unchanged" /></SelectTrigger>
                <SelectContent>
                  {SHE_INCIDENT_STATUS_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || !reviewerId}
              onClick={() => {
                if (!reviewerId) return;
                void act('Review recorded', () =>
                  safetyIncidentService.review(inc.id, {
                    incidentId: inc.id,
                    reviewedById: reviewerId,
                    reviewComments: blank(reviewComments),
                    newStatus: (newStatus || null) as never,
                  }),
                );
              }}
            >
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record review
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={dialog === 'close'} onOpenChange={(o) => !o && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Close incident</DialogTitle>
            <DialogDescription>
              The server refuses closure while any corrective action is still open.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Closed by</Label>
              <EmployeePicker value={closerId} onChange={(v) => setCloserId(v)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="closure-notes">Closure notes</Label>
              <Textarea
                id="closure-notes"
                rows={3}
                value={closureNotes}
                onChange={(e) => setClosureNotes(e.target.value)}
                placeholder="What was concluded, and anything the register should remember."
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="lessons-learned">Lessons learned</Label>
              <Textarea
                id="lessons-learned"
                rows={3}
                value={lessonsLearned}
                onChange={(e) => setLessonsLearned(e.target.value)}
                placeholder="What the organisation takes away — what changes so this does not recur."
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || !closerId}
              onClick={() => {
                if (!closerId) return;
                void act('Incident closed', () =>
                  safetyIncidentService.close(inc.id, {
                    incidentId: inc.id,
                    closedById: closerId,
                    closureNotes: blank(closureNotes),
                    lessonsLearned: blank(lessonsLearned),
                  }),
                );
              }}
            >
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={dialog === 'submission'} onOpenChange={(o) => !o && setDialog(null)}>
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>Record statutory submission</DialogTitle>
            <DialogDescription>
              The permanent record of a report to the authority (FR-SHE-103). The first submission
              stamps the incident&apos;s notification fields.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Regulatory body</Label>
                <Select value={subBodyId} onValueChange={setSubBodyId}>
                  <SelectTrigger><SelectValue placeholder="Pick the authority" /></SelectTrigger>
                  <SelectContent>
                    {regulatoryBodies.map((b) => (
                      <SelectItem key={b.id} value={b.id}>{b.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Submitted by</Label>
                <EmployeePicker value={submitterId} onChange={setSubmitterId} />
              </div>
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Submission type</Label>
                <Select value={subType} onValueChange={(v) => setSubType(v as SheStatutorySubmissionType)}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {SHE_STATUTORY_SUBMISSION_TYPE_OPTIONS.map((o) => (
                      <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Method</Label>
                <Select value={subMethod} onValueChange={(v) => setSubMethod(v as SheStatutorySubmissionMethod)}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {SHE_STATUTORY_SUBMISSION_METHOD_OPTIONS.map((o) => (
                      <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Authority reference</Label>
                <Input value={subRef} onChange={(e) => setSubRef(e.target.value)} />
              </div>
              <div className="space-y-2">
                <Label>Submitted document path</Label>
                <Input value={subDocPath} onChange={(e) => setSubDocPath(e.target.value)} placeholder="/uploads/statutory/…" />
              </div>
            </div>
            <div className="space-y-2">
              <Label>Notes</Label>
              <Textarea rows={2} value={subNotes} onChange={(e) => setSubNotes(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || !subBodyId || !submitterId}
              onClick={() =>
                void act('Statutory submission recorded', () =>
                  safetyIncidentService.addStatutorySubmission(inc.id, {
                    incidentId: inc.id,
                    regulatoryBodyId: subBodyId,
                    type: subType,
                    method: subMethod,
                    referenceNumber: blank(subRef),
                    submittedById: submitterId!,
                    documentPath: blank(subDocPath),
                    notes: blank(subNotes),
                  }),
                )
              }
            >
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record submission
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!ackTarget} onOpenChange={(o) => !o && setAckTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record acknowledgement</DialogTitle>
            <DialogDescription>
              {ackTarget?.regulatoryBodyName} — submitted {fmtDate(ackTarget?.submissionDate)}.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Acknowledgement reference</Label>
            <Input value={ackRef} onChange={(e) => setAckRef(e.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAckTarget(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || !ackTarget}
              onClick={() => {
                const target = ackTarget!;
                void act('Acknowledgement recorded', async () => {
                  await safetyIncidentService.updateStatutorySubmission({
                    id: target.id,
                    referenceNumber: target.referenceNumber,
                    documentPath: target.documentPath,
                    acknowledgementReceived: true,
                    acknowledgementDate: new Date().toISOString(),
                    acknowledgementReference: blank(ackRef),
                    notes: target.notes,
                  });
                  setAckTarget(null);
                });
              }}
            >
              Record
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!verifyCa} onOpenChange={(o) => !o && setVerifyCa(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Verify effectiveness</DialogTitle>
            <DialogDescription>{verifyCa?.actionDescription}</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Verified by</Label>
              <EmployeePicker value={verifierId} onChange={(v) => setVerifierId(v)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="verify-notes">Effectiveness notes</Label>
              <Textarea id="verify-notes" rows={2} value={verifyNotes} onChange={(e) => setVerifyNotes(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setVerifyCa(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || !verifierId}
              onClick={() => {
                if (!verifyCa || !verifierId) return;
                const target = verifyCa;
                void act('Corrective action verified', async () => {
                  await safetyIncidentService.verifyCorrectiveAction(target.id, {
                    correctiveActionId: target.id,
                    verifiedById: verifierId,
                    effectivenessReviewNotes: blank(verifyNotes),
                  });
                  setVerifyCa(null);
                });
              }}
            >
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Verify
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
