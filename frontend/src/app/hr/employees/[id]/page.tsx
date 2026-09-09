'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil, UserX, UserCheck, Ban, RotateCcw } from 'lucide-react';
import { GatedPhoto, PhotoDialog } from '@/components/hr/common/PhotoDialog';
import { employeeDocumentService } from '@/services/hr/employee-document.service';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { employeeService } from '@/services/hr/employee.service';
import type { EmployeeDetail } from '@/types/hr/employee';
import { ContactsTab } from '@/components/hr/employee/tabs/ContactsTab';
import { DocumentsTab } from '@/components/hr/employee/tabs/DocumentsTab';
import { EmergencyContactsTab } from '@/components/hr/employee/tabs/EmergencyContactsTab';
import { DependentsTab } from '@/components/hr/employee/tabs/DependentsTab';
import { QualificationsTab } from '@/components/hr/employee/tabs/QualificationsTab';
import { SkillsTab } from '@/components/hr/employee/tabs/SkillsTab';
import { CertificationsTab } from '@/components/hr/employee/tabs/CertificationsTab';
import { CertificationComplianceCard } from '@/components/hr/employee/CertificationComplianceCard';
import { IdentificationTab } from '@/components/hr/employee/tabs/IdentificationTab';
import { WorkHistoryTab } from '@/components/hr/employee/tabs/WorkHistoryTab';
import { ContractsTab } from '@/components/hr/employee/tabs/ContractsTab';
import { ExpatriateTab } from '@/components/hr/employee/tabs/ExpatriateTab';
import { PositionHistoryTab } from '@/components/hr/employee/tabs/PositionHistoryTab';
import { SalaryTab } from '@/components/hr/employee/tabs/SalaryTab';
import { RefereesTab } from '@/components/hr/employee/tabs/RefereesTab';
import { RelieversTab } from '@/components/hr/employee/tabs/RelieversTab';
import { TeamsTab } from '@/components/hr/employee/tabs/TeamsTab';
import { GuarantorsTab } from '@/components/hr/employee/tabs/GuarantorsTab';
import { BankDetailsTab } from '@/components/hr/employee/tabs/BankDetailsTab';
import { offPayrollReasonLabel, PAYROLL_ISSUE_LABELS, PROBATION_SOURCE_LABEL } from '@/types/hr/employee';

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 py-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

function InfoCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-base">{title}</CardTitle>
      </CardHeader>
      <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">{children}</CardContent>
    </Card>
  );
}

const yn = (v: boolean) => (v ? 'Yes' : 'No');
const money = (v?: number | null) =>
  v == null ? '—' : new Intl.NumberFormat('en-GH', { style: 'currency', currency: 'GHS' }).format(v);

export default function EmployeeDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [action, setAction] = useState<null | 'deactivate' | 'activate' | 'terminate' | 'reinstate'>(null);
  const [busy, setBusy] = useState(false);
  const [termDate, setTermDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [termReason, setTermReason] = useState('');
  const [photoOpen, setPhotoOpen] = useState(false);
  // Bumped after an upload so the header avatar refetches the same gated URL.
  const [photoVersion, setPhotoVersion] = useState(0);

  const { data: e, isLoading, isError } = useQuery({
    queryKey: ['hr', 'employees', id, 'details'],
    queryFn: () => employeeService.getDetails(id),
    enabled: !!id,
  });

  const { data: manager } = useQuery({
    queryKey: ['hr', 'employees', e?.managerId, 'lookup'],
    queryFn: () => employeeService.getById(e?.managerId as string),
    enabled: !!e?.managerId,
  });

  // Payroll's own answer beside HR's flag — whether the person is set up and active in Payroll.
  const { data: payroll } = useQuery({
    queryKey: ['hr', 'employees', id, 'payroll-status'],
    queryFn: () => employeeService.getPayrollStatus(id),
    enabled: !!id,
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'employees'] });

  const runAction = async () => {
    if (!action || !e) return false;
    if (action === 'terminate' && !termReason.trim()) {
      toast({ title: 'Reason required', description: 'Enter a termination reason.', variant: 'destructive' });
      return false;
    }
    setBusy(true);
    try {
      if (action === 'deactivate') await employeeService.deactivate(e.id);
      else if (action === 'activate') await employeeService.activate(e.id);
      else if (action === 'reinstate') await employeeService.reinstate(e.id);
      else if (action === 'terminate')
        await employeeService.terminate(e.id, { terminationDate: termDate, terminationReason: termReason.trim() });
      await refresh();
      toast({ title: 'Done', description: `${e.fullName} updated.` });
      setAction(null);
      setTermReason('');
      return true;
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Action failed.', variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }
  if (isError || !e) {
    return (
      <div className="p-6">
        <EmptyState title="Employee not found" description="This employee may have been deleted." />
      </div>
    );
  }

  const isTerminated = e.staffStatus === 'Terminated';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={e.fullName || `${e.firstName} ${e.lastName}`}
        description={`${e.employeeNumber}${e.positionTitle ? ` · ${e.positionTitle}` : ''}`}
        backHref="/hr/employees"
        actions={
          <div className="flex items-center gap-2">
            {/* The employee's photograph, through the gate (round 2, lane A-7). Click to view or
                replace it — the route existed since lane 3a with nothing calling it. */}
            <button
              type="button"
              className="rounded-full ring-offset-background focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2"
              title={e.hasPhoto ? 'View or replace the photograph' : 'Add a photograph'}
              onClick={() => setPhotoOpen(true)}
            >
              <GatedPhoto
                endpoint={employeeDocumentService.employeePhotoUrl(e.id)}
                enabled={!!e.hasPhoto || !!e.picturePath}
                version={photoVersion}
                alt={e.fullName}
                className="h-10 w-10"
              />
            </button>
            <StatusBadge status={e.staffStatus} />
            <Button variant="outline" onClick={() => router.push(`/hr/employees/${e.id}/edit`)}>
              <Pencil className="mr-2 h-4 w-4" /> Edit
            </Button>
            {isTerminated ? (
              <Button variant="outline" onClick={() => setAction('reinstate')}>
                <RotateCcw className="mr-2 h-4 w-4" /> Reinstate
              </Button>
            ) : (
              <>
                {e.isActive ? (
                  <Button variant="outline" onClick={() => setAction('deactivate')}>
                    <UserX className="mr-2 h-4 w-4" /> Deactivate
                  </Button>
                ) : (
                  <Button variant="outline" onClick={() => setAction('activate')}>
                    <UserCheck className="mr-2 h-4 w-4" /> Activate
                  </Button>
                )}
                <Button variant="destructive" onClick={() => setAction('terminate')}>
                  <Ban className="mr-2 h-4 w-4" /> Terminate
                </Button>
              </>
            )}
          </div>
        }
      />

      <PhotoDialog
        open={photoOpen}
        onOpenChange={setPhotoOpen}
        title={`Photograph — ${e.fullName}`}
        description="Shown on the profile, the ID card and the organogram."
        endpoint={employeeDocumentService.employeePhotoUrl(e.id)}
        hasPhoto={!!e.hasPhoto || !!e.picturePath}
        upload={(file) => employeeDocumentService.uploadEmployeePhoto(e.id, file)}
        onUploaded={() => { setPhotoVersion((v) => v + 1); void refresh(); }}
        subjectLabel={e.fullName}
      />

      <Tabs defaultValue="overview">
        {/* 15 tabs will not fit a fixed row — let the strip scroll on narrow screens. */}
        <div className="overflow-x-auto pb-1">
          <TabsList className="inline-flex w-max">
            <TabsTrigger value="overview">Overview</TabsTrigger>
            <TabsTrigger value="contacts">Addresses</TabsTrigger>
            <TabsTrigger value="emergency">Emergency</TabsTrigger>
            <TabsTrigger value="dependents">Dependents</TabsTrigger>
            <TabsTrigger value="qualifications">Qualifications</TabsTrigger>
            <TabsTrigger value="skills">Skills</TabsTrigger>
            <TabsTrigger value="certifications">Certifications</TabsTrigger>
            <TabsTrigger value="identification">Identification</TabsTrigger>
            <TabsTrigger value="work-history">Work History</TabsTrigger>
            <TabsTrigger value="contracts">Contracts</TabsTrigger>
            <TabsTrigger value="expatriate">Expatriate</TabsTrigger>
            <TabsTrigger value="position-history">Position History</TabsTrigger>
            {/* Slice 7. Who covers for this employee while they are away — read by the
                leave request form to seed its two reliever slots by priority. */}
            <TabsTrigger value="teams">Teams</TabsTrigger>
            <TabsTrigger value="relievers">Relievers</TabsTrigger>
            <TabsTrigger value="salary">Salary</TabsTrigger>
            <TabsTrigger value="referees">Referees</TabsTrigger>
            <TabsTrigger value="guarantors">Guarantors</TabsTrigger>
            <TabsTrigger value="bank">Bank</TabsTrigger>
            <TabsTrigger value="documents">Documents</TabsTrigger>
          </TabsList>
        </div>

        <TabsContent value="overview" className="space-y-4 pt-4">
          {/* Required vs held vs expired — the strip the demo feedback asked for (round 2, lane C2). */}
          <CertificationComplianceCard employeeId={id} compact />
          <InfoCard title="Personal">
            <InfoRow label="Title" value={e.title} />
            <InfoRow label="Full Name" value={e.fullName} />
            <InfoRow label="Gender" value={e.gender} />
            <InfoRow label="Date of Birth" value={e.dateOfBirth} />
            <InfoRow label="Marital Status" value={e.maritalStatus} />
            <InfoRow label="Religion" value={e.religion} />
            <InfoRow label="Blood Type" value={e.bloodType} />
            <InfoRow label="Expatriate" value={yn(e.isExpatriate)} />
          </InfoCard>

          <InfoCard title="Contact">
            <InfoRow label="Email" value={e.emailAddress} />
            <InfoRow label="Mobile" value={e.mobileNumber} />
            <InfoRow label="Telephone" value={e.telephoneNumber} />
            <InfoRow label="Address" value={e.address} />
            <InfoRow label="City" value={e.city} />
            <InfoRow label="Digital Address" value={e.digitalAddress} />
          </InfoCard>

          <InfoCard title="Employment">
            <InfoRow label="Position" value={e.positionTitle} />
            <InfoRow label="Organization Unit" value={e.organizationUnitName} />
            <InfoRow label="Organization Level" value={e.organizationLevelName} />
            <InfoRow label="Location" value={e.locationName} />
            <InfoRow label="Manager" value={manager?.fullName} />
            <InfoRow label="Employment Type" value={e.employmentType} />
            <InfoRow label="Staff Status" value={e.staffStatus} />
            <InfoRow label="Date Employed" value={e.dateEmployed} />
            {/* The term, and where it came from — a number with no provenance is what the demo
                feedback asked about (E-2b). Source is null on records created before 2026-09-09. */}
            <InfoRow
              label="Probation (days)"
              value={
                e.probationPeriodDays
                  ? `${e.probationPeriodDays}${
                      e.probationSource ? ` — ${PROBATION_SOURCE_LABEL[e.probationSource]}` : ''
                    }`
                  : null
              }
            />
            <InfoRow label="Full-time" value={yn(e.isFullTime)} />
            <InfoRow label="On Probation" value={yn(e.isOnProbation)} />
            {/* ⚠ Read-only, here and everywhere. It is written by confirming the probation record,
                which is what issues the letter; the server refuses it from the employee edit. Shown
                beside the EXPECTED date so "due" and "done" are never read as the same fact. */}
            <InfoRow
              label="Expected Confirmation"
              value={e.confirmationDate ? null : e.expectedConfirmationDate}
            />
            <InfoRow label="Confirmed On" value={e.confirmationDate} />
          </InfoCard>

          <InfoCard title="Compensation & Tax">
            <InfoRow
              label="Payroll"
              value={
                e.isOnPayroll ? (
                  <span className="inline-flex items-center rounded-full bg-emerald-100 px-2 py-0.5 text-xs font-medium text-emerald-800 dark:bg-emerald-900 dark:text-emerald-100">
                    On payroll
                  </span>
                ) : (
                  <span className="inline-flex items-center rounded-full bg-slate-200 px-2 py-0.5 text-xs font-medium text-slate-800 dark:bg-slate-700 dark:text-slate-100">
                    Not on payroll
                  </span>
                )
              }
            />
            {/* Payroll's side: is the person set up and active in the payroll module? The two are
                different owners' facts; a mismatch is named here and listed on the reconciliation screen. */}
            <InfoRow
              label="In Payroll module"
              value={
                payroll
                  ? payroll.hasPayrollProfile
                    ? payroll.payrollActive
                      ? 'Set up · active'
                      : 'Set up · switched off'
                    : 'Not set up'
                  : undefined
              }
            />
            {payroll?.issue && (
              <InfoRow
                label="Attention"
                value={<span className="text-amber-700 dark:text-amber-300">{PAYROLL_ISSUE_LABELS[payroll.issue]}</span>}
              />
            )}
            {e.isOnPayroll ? (
              <>
                {/* The figure HR would quote, resolved by pay basis (lane E1): the placed notch
                    on the scale, payroll's basis when negotiated. The record's own flat figure is
                    the fallback, not the source. */}
                <InfoRow
                  label="Monthly basic pay"
                  value={
                    payroll?.hrMonthlyBasicPay != null
                      ? `${money(payroll.hrMonthlyBasicPay)}${e.payBasis === 'Negotiated' ? ' · Negotiated' : ''}`
                      : e.payBasis === 'Negotiated'
                        ? 'Negotiated — amount not yet entered'
                        : money(e.salary)
                  }
                />
              </>
            ) : (
              <>
                <InfoRow label="Paid instead by" value={offPayrollReasonLabel(e.offPayrollReason)} />
                <InfoRow label="Arrangement" value={e.offPayrollNote} />
              </>
            )}
            <InfoRow label="Tax Number" value={e.taxNumber} />
            <InfoRow label="SSNIT Number" value={e.socialSecurityNumber} />
            <InfoRow label="TIN" value={e.tinNumber} />
          </InfoCard>

          {isTerminated && (
            <InfoCard title="Termination">
              <InfoRow label="Date" value={e.terminationDate?.slice(0, 10)} />
              <InfoRow label="Reason" value={e.terminationReason} />
              <InfoRow label="Notes" value={e.terminationNotes} />
            </InfoCard>
          )}
        </TabsContent>

        <TabsContent value="documents" className="pt-4">
          <DocumentsTab employeeId={id} />
        </TabsContent>
        <TabsContent value="contacts" className="pt-4">
          <ContactsTab employeeId={id} />
        </TabsContent>
        <TabsContent value="emergency" className="pt-4">
          <EmergencyContactsTab employeeId={id} />
        </TabsContent>
        <TabsContent value="dependents" className="pt-4">
          <DependentsTab employeeId={id} />
        </TabsContent>
        <TabsContent value="qualifications" className="pt-4">
          <QualificationsTab employeeId={id} />
        </TabsContent>
        <TabsContent value="skills" className="pt-4">
          <SkillsTab employeeId={id} />
        </TabsContent>
        <TabsContent value="certifications" className="pt-4">
          <CertificationsTab employeeId={id} />
        </TabsContent>
        <TabsContent value="identification" className="pt-4">
          <IdentificationTab employeeId={id} />
        </TabsContent>
        <TabsContent value="work-history" className="pt-4">
          <WorkHistoryTab employeeId={id} />
        </TabsContent>
        <TabsContent value="contracts" className="pt-4">
          <ContractsTab employeeId={id} />
        </TabsContent>
        <TabsContent value="expatriate" className="pt-4">
          <ExpatriateTab employeeId={id} />
        </TabsContent>
        <TabsContent value="position-history" className="pt-4">
          <PositionHistoryTab employeeId={id} />
        </TabsContent>
        <TabsContent value="salary" className="pt-4">
          <SalaryTab employee={e} />
        </TabsContent>
        <TabsContent value="teams" className="pt-4">
          <TeamsTab employeeId={id} />
        </TabsContent>
        <TabsContent value="relievers" className="pt-4">
          <RelieversTab employeeId={id} />
        </TabsContent>
        <TabsContent value="referees" className="pt-4">
          <RefereesTab employeeId={id} />
        </TabsContent>
        <TabsContent value="guarantors" className="pt-4">
          <GuarantorsTab employeeId={id} />
        </TabsContent>
        <TabsContent value="bank" className="pt-4">
          <BankDetailsTab employeeId={id} />
        </TabsContent>
      </Tabs>

      <ConfirmationDialog
        open={action !== null}
        onOpenChange={(open) => !open && setAction(null)}
        title={
          action === 'terminate'
            ? 'Terminate employee'
            : action === 'reinstate'
              ? 'Reinstate employee'
              : action === 'activate'
                ? 'Activate employee'
                : 'Deactivate employee'
        }
        description={action === 'terminate' ? `Terminate ${e.fullName}?` : `${action ?? ''} ${e.fullName}?`}
        confirmText={action === 'terminate' ? 'Terminate' : 'Confirm'}
        variant={action === 'terminate' || action === 'deactivate' ? 'destructive' : 'default'}
        isLoading={busy}
        onConfirm={runAction}
      >
        {action === 'terminate' && (
          <div className="space-y-3">
            <div className="space-y-1">
              <Label htmlFor="termDate">Termination Date</Label>
              <Input id="termDate" type="date" value={termDate} onChange={(ev) => setTermDate(ev.target.value)} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="termReason">Reason</Label>
              <Textarea
                id="termReason"
                rows={3}
                value={termReason}
                onChange={(ev) => setTermReason(ev.target.value)}
                placeholder="Reason for termination"
              />
            </div>
          </div>
        )}
      </ConfirmationDialog>
    </div>
  );
}
