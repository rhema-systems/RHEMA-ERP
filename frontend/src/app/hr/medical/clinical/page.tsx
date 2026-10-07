'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ClipboardCheck, Share2, CalendarClock } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { ClinicalRecordActions } from '@/components/hr/medical/ClinicalRecordActions';
import { HR_ADMIN_ROLES, HR_ROLES } from '@/components/hr/common/PermissionGate';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { medicalClinicalService } from '@/services/hr/medical-clinical.service';
import {
  MEDICAL_SERVICE_TYPE_OPTIONS,
  REFERRAL_PRIORITY_OPTIONS,
} from '@/types/hr/medical';
import type {
  MedicalPreAuthorizationSummary,
  MedicalReferralSummary,
  MedicalAppointmentSummary,
} from '@/types/hr/medical';

/**
 * The clinical surface — what happens BEFORE a claim exists.
 *
 * A pre-authorisation is the insurer agreeing in advance to pay; a referral is a hand-off to
 * another clinician; an appointment is a booked visit. Each carries its own number so a claim can
 * point back at it, which is why they are worth recording even when no claim ever follows.
 *
 * Actions are offered only where the record's state admits them — approving an already-decided
 * pre-authorisation, or checking in a cancelled appointment, would simply be refused.
 */
const money = (v?: number | null) =>
  v === null || v === undefined
    ? '—'
    : v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');
const label = (opts: { value: string; label: string }[], v: string) =>
  opts.find((o) => o.value === v)?.label ?? v;
/** The serialised enum name as words: "PendingApproval" → "Pending approval", "CheckedIn" → "Checked in". */
const humanise = (v: string) => {
  const words = v.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
  return words.charAt(0).toUpperCase() + words.slice(1);
};

function PreAuthBadge({ status }: { status: string }) {
  if (status === 'Approved') return <Badge variant="secondary">Approved</Badge>;
  if (status === 'Rejected' || status === 'Expired' || status === 'Cancelled')
    return <Badge variant="destructive">{status}</Badge>;
  return <Badge variant="outline">{humanise(status)}</Badge>;
}

function PriorityBadge({ priority }: { priority: string }) {
  if (priority === 'Emergency') return <Badge variant="destructive">Emergency</Badge>;
  if (priority === 'Urgent') return <Badge variant="outline">Urgent</Badge>;
  return <span className="text-muted-foreground">Routine</span>;
}

export default function MedicalClinicalPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasAnyPermission, hasAnyRole } = useAuth();

  /**
   * The controller's ladder: create and update are `HR.Medical.Write`, **every delete is
   * `HR.Medical.Admin`**, and the HR role holds Write but not Admin. The role check beside each
   * permission mirrors `HrPermissions.RoleGrants`, which is what keeps a tenant working whose
   * permission rows have not been seeded — same idiom as the insurance detail screen.
   */
  const canWrite =
    hasAnyPermission(['HR.Medical.Write', 'HR.Medical.Admin']) || hasAnyRole(HR_ROLES);
  const canDelete = hasAnyPermission(['HR.Medical.Admin']) || hasAnyRole(HR_ADMIN_ROLES);

  const [approving, setApproving] = useState<MedicalPreAuthorizationSummary | null>(null);
  const [authorizedAmount, setAuthorizedAmount] = useState('');
  const [rejecting, setRejecting] = useState<MedicalPreAuthorizationSummary | null>(null);
  const [rejectionReason, setRejectionReason] = useState('');
  const [completing, setCompleting] = useState<MedicalReferralSummary | null>(null);
  const [outcome, setOutcome] = useState('');
  const [cancelling, setCancelling] = useState<MedicalAppointmentSummary | null>(null);
  const [cancellationReason, setCancellationReason] = useState('');

  const { data: preAuths = [] } = useQuery({
    queryKey: ['hr', 'medical-preauths'],
    queryFn: () => medicalClinicalService.getPreAuthorizations(),
  });
  const { data: pendingPreAuths = [] } = useQuery({
    queryKey: ['hr', 'medical-preauths', 'pending'],
    queryFn: () => medicalClinicalService.getPendingPreAuthorizations(),
  });
  const { data: referrals = [] } = useQuery({
    queryKey: ['hr', 'medical-referrals'],
    queryFn: () => medicalClinicalService.getReferrals(),
  });
  const { data: appointments = [] } = useQuery({
    queryKey: ['hr', 'medical-appointments'],
    queryFn: () => medicalClinicalService.getAppointments(),
  });
  const { data: upcoming = [] } = useQuery({
    queryKey: ['hr', 'medical-appointments', 'upcoming'],
    queryFn: () => medicalClinicalService.getUpcomingAppointments(30),
  });

  const refresh = (key: string) => {
    queryClient.invalidateQueries({ queryKey: ['hr', key] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'medical-dashboard', 30] });
  };
  const onError = (error: unknown) =>
    toast({
      variant: 'destructive',
      title: 'Could not complete that',
      description: error instanceof Error ? error.message : 'Unexpected error',
    });

  const approve = useMutation({
    mutationFn: (id: string) =>
      medicalClinicalService.approvePreAuthorization(
        id,
        authorizedAmount ? Number(authorizedAmount) : null,
      ),
    onSuccess: () => {
      toast({ title: 'Pre-authorisation approved' });
      setApproving(null);
      setAuthorizedAmount('');
      refresh('medical-preauths');
    },
    onError,
  });

  const reject = useMutation({
    mutationFn: (id: string) =>
      medicalClinicalService.rejectPreAuthorization(id, rejectionReason),
    onSuccess: () => {
      toast({ title: 'Pre-authorisation rejected' });
      setRejecting(null);
      setRejectionReason('');
      refresh('medical-preauths');
    },
    onError,
  });

  const complete = useMutation({
    mutationFn: (id: string) => medicalClinicalService.completeReferral(id, outcome || null),
    onSuccess: () => {
      toast({ title: 'Referral completed' });
      setCompleting(null);
      setOutcome('');
      refresh('medical-referrals');
    },
    onError,
  });

  const checkIn = useMutation({
    mutationFn: (id: string) => medicalClinicalService.checkIn(id),
    onSuccess: () => {
      toast({ title: 'Checked in' });
      refresh('medical-appointments');
    },
    onError,
  });

  const checkOut = useMutation({
    mutationFn: (id: string) => medicalClinicalService.checkOut(id),
    onSuccess: () => {
      toast({ title: 'Checked out' });
      refresh('medical-appointments');
    },
    onError,
  });

  const cancel = useMutation({
    mutationFn: (id: string) => medicalClinicalService.cancelAppointment(id, cancellationReason),
    onSuccess: () => {
      toast({ title: 'Appointment cancelled' });
      setCancelling(null);
      setCancellationReason('');
      refresh('medical-appointments');
    },
    onError,
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Clinical"
        description="Pre-authorisations, referrals and appointments — what happens before a claim exists. A claim can point back at a pre-authorisation or a referral."
        backHref="/hr/medical"
      />

      <Tabs defaultValue="pre-auth">
        <TabsList>
          <TabsTrigger value="pre-auth">
            Pre-authorisations ({pendingPreAuths.length} pending)
          </TabsTrigger>
          <TabsTrigger value="referrals">Referrals ({referrals.length})</TabsTrigger>
          <TabsTrigger value="appointments">
            Appointments ({upcoming.length} upcoming)
          </TabsTrigger>
        </TabsList>

        <TabsContent value="pre-auth" className="mt-4">
          {preAuths.length === 0 ? (
            <EmptyState
              title="No pre-authorisations"
              description="Nothing has been sent to an insurer for advance approval."
              icon={ClipboardCheck}
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Authorisation</TableHead>
                      <TableHead>Employee</TableHead>
                      <TableHead>Service</TableHead>
                      <TableHead>Planned</TableHead>
                      <TableHead className="text-right">Estimated</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="w-[60px]" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {preAuths.map((p) => (
                      <TableRow key={p.id}>
                        <TableCell className="font-mono text-sm">{p.authorizationNumber}</TableCell>
                        <TableCell className="font-medium">{p.employeeName}</TableCell>
                        <TableCell>{label(MEDICAL_SERVICE_TYPE_OPTIONS, p.serviceType)}</TableCell>
                        <TableCell>{fmtDate(p.plannedServiceDate)}</TableCell>
                        <TableCell className="text-right tabular-nums">
                          {money(p.estimatedCost)}
                        </TableCell>
                        <TableCell>
                          <PreAuthBadge status={p.status} />
                        </TableCell>
                        <TableCell>
                          {/* Only an undecided request can be decided; edit and delete are not
                              state-gated, because correcting a record is not a transition. */}
                          <ClinicalRecordActions
                            kind="pre-authorization"
                            id={p.id}
                            recordLabel={p.authorizationNumber}
                            canWrite={canWrite}
                            canDelete={canDelete}
                            steps={
                              canWrite && (p.status === 'Requested' || p.status === 'PendingApproval')
                                ? [
                                    { label: 'Approve', onSelect: () => setApproving(p) },
                                    { label: 'Reject', onSelect: () => setRejecting(p) },
                                  ]
                                : []
                            }
                          />
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="referrals" className="mt-4">
          {referrals.length === 0 ? (
            <EmptyState
              title="No referrals"
              description="Nobody has been referred on to another clinician."
              icon={Share2}
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Referral</TableHead>
                      <TableHead>Employee</TableHead>
                      <TableHead>Priority</TableHead>
                      <TableHead>Referred</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="w-[60px]" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {referrals.map((r) => (
                      <TableRow key={r.id}>
                        <TableCell className="font-mono text-sm">{r.referralNumber}</TableCell>
                        <TableCell className="font-medium">{r.employeeName}</TableCell>
                        <TableCell>
                          <PriorityBadge priority={r.priority} />
                        </TableCell>
                        <TableCell>{fmtDate(r.referralDate)}</TableCell>
                        <TableCell>
                          <Badge variant="outline">{humanise(r.status)}</Badge>
                        </TableCell>
                        <TableCell>
                          <ClinicalRecordActions
                            kind="referral"
                            id={r.id}
                            recordLabel={r.referralNumber}
                            canWrite={canWrite}
                            canDelete={canDelete}
                            steps={
                              canWrite && ['Pending', 'Issued', 'Accepted'].includes(r.status)
                                ? [{ label: 'Complete', onSelect: () => setCompleting(r) }]
                                : []
                            }
                          />
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="appointments" className="mt-4">
          {appointments.length === 0 ? (
            <EmptyState
              title="No appointments"
              description="Nothing is booked."
              icon={CalendarClock}
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Appointment</TableHead>
                      <TableHead>Employee</TableHead>
                      <TableHead>Facility</TableHead>
                      <TableHead>When</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="w-[60px]" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {appointments.map((a) => (
                      <TableRow key={a.id}>
                        <TableCell className="font-mono text-sm">{a.appointmentNumber}</TableCell>
                        <TableCell className="font-medium">{a.employeeName}</TableCell>
                        <TableCell>{a.facilityName}</TableCell>
                        <TableCell>{fmtDateTime(a.appointmentDateTime)}</TableCell>
                        <TableCell>
                          <Badge variant="outline">{humanise(a.status)}</Badge>
                        </TableCell>
                        <TableCell>
                          {/* The visit runs check-in → check-out; each shows only at its point. */}
                          <ClinicalRecordActions
                            kind="appointment"
                            id={a.id}
                            recordLabel={a.appointmentNumber}
                            canWrite={canWrite}
                            canDelete={canDelete}
                            steps={
                              !canWrite
                                ? []
                                : ['Scheduled', 'Confirmed'].includes(a.status)
                                  ? [
                                      { label: 'Check in', onSelect: () => checkIn.mutate(a.id) },
                                      { label: 'Cancel appointment', onSelect: () => setCancelling(a) },
                                    ]
                                  : ['CheckedIn', 'InProgress'].includes(a.status)
                                    ? [{ label: 'Check out', onSelect: () => checkOut.mutate(a.id) }]
                                    : []
                            }
                          />
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </TabsContent>
      </Tabs>

      <Dialog open={!!approving} onOpenChange={(o) => !o && setApproving(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Approve {approving?.authorizationNumber}</DialogTitle>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="authorized-amount">Amount authorised</Label>
            <Input
              id="authorized-amount"
              type="number"
              value={authorizedAmount}
              onChange={(e) => setAuthorizedAmount(e.target.value)}
              placeholder={approving?.estimatedCost ? String(approving.estimatedCost) : undefined}
            />
            <p className="text-xs text-muted-foreground">
              Leave blank to authorise the estimated cost. The approver is recorded from your
              account.
            </p>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setApproving(null)}>
              Cancel
            </Button>
            <Button
              disabled={!approving || approve.isPending}
              onClick={() => approving && approve.mutate(approving.id)}
            >
              Approve
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!rejecting} onOpenChange={(o) => !o && setRejecting(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reject {rejecting?.authorizationNumber}</DialogTitle>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="rejection-reason">Reason</Label>
            <Textarea
              id="rejection-reason"
              value={rejectionReason}
              onChange={(e) => setRejectionReason(e.target.value)}
              rows={3}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRejecting(null)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              disabled={!rejecting || !rejectionReason.trim() || reject.isPending}
              onClick={() => rejecting && reject.mutate(rejecting.id)}
            >
              Reject
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!completing} onOpenChange={(o) => !o && setCompleting(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Complete {completing?.referralNumber}</DialogTitle>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="outcome">Outcome</Label>
            <Textarea
              id="outcome"
              value={outcome}
              onChange={(e) => setOutcome(e.target.value)}
              rows={3}
              placeholder="What came of the referral?"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCompleting(null)}>
              Cancel
            </Button>
            <Button
              disabled={!completing || complete.isPending}
              onClick={() => completing && complete.mutate(completing.id)}
            >
              Complete
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!cancelling} onOpenChange={(o) => !o && setCancelling(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Cancel {cancelling?.appointmentNumber}</DialogTitle>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="cancellation-reason">Reason</Label>
            <Textarea
              id="cancellation-reason"
              value={cancellationReason}
              onChange={(e) => setCancellationReason(e.target.value)}
              rows={3}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelling(null)}>
              Keep it
            </Button>
            <Button
              variant="destructive"
              disabled={!cancelling || !cancellationReason.trim() || cancel.isPending}
              onClick={() => cancelling && cancel.mutate(cancelling.id)}
            >
              Cancel appointment
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
