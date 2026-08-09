'use client';

import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CheckCircle2,
  Clock3,
  KeyRound,
  PencilLine,
  RefreshCw,
  RotateCw,
  ShieldAlert,
  Users,
} from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
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
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { supplierApplicantAccessService as service } from '@/services/procurement-supplier-applicant-access.service';

const dateTime = (value?: string) =>
  value ? new Date(value).toLocaleString() : '—';

export default function SupplierApplicantAccessAdministrationPage() {
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canRecover = hasPermission('procurement.supplier.approve');
  const [busyId, setBusyId] = useState<string>();
  const [correction, setCorrection] = useState<{
    registrationId: string;
    companyName: string;
    channel: 'Email' | 'Sms';
    contact: string;
    reason: string;
    otpCode: string;
    maskedContact?: string;
    challengeSent: boolean;
  }>();
  const summary = useQuery({
    queryKey: ['supplier-applicant-access-summary'],
    queryFn: service.adminSummary,
  });
  const history = useQuery({
    queryKey: ['supplier-applicant-access-history'],
    queryFn: service.adminHistory,
  });

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: ['supplier-applicant-access-summary'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['supplier-applicant-access-history'],
      }),
    ]);
  };

  const run = async (registrationId: string, action: 'resend' | 'retry') => {
    setBusyId(`${registrationId}-${action}`);
    try {
      if (action === 'resend') await service.resend(registrationId);
      else await service.retryActivation(registrationId);
      toast.success(
        action === 'resend'
          ? 'Temporary credential reissued through the verified channel.'
          : 'Supplier account activation retried.'
      );
      await refresh();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Action failed.');
    } finally {
      setBusyId(undefined);
    }
  };

  const sendCorrectionChallenge = async () => {
    if (!correction) return;
    setBusyId(`${correction.registrationId}-contact-challenge`);
    try {
      const challenge = await service.requestContactCorrectionChallenge(
        correction.registrationId,
        { channel: correction.channel, contact: correction.contact }
      );
      setCorrection((current) =>
        current
          ? {
              ...current,
              maskedContact: challenge.maskedContact,
              challengeSent: true,
              otpCode: '',
            }
          : current
      );
      toast.success(challenge.message);
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Verification failed.'
      );
    } finally {
      setBusyId(undefined);
    }
  };

  const confirmCorrection = async () => {
    if (!correction) return;
    setBusyId(`${correction.registrationId}-contact-confirm`);
    try {
      const result = await service.confirmContactCorrection(
        correction.registrationId,
        {
          channel: correction.channel,
          contact: correction.contact,
          otpCode: correction.otpCode,
          reason: correction.reason,
        }
      );
      if (result.credentialDelivered) toast.success(result.message);
      else toast.warning(result.message);
      setCorrection(undefined);
      await refresh();
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Correction failed.'
      );
    } finally {
      setBusyId(undefined);
    }
  };

  const cards = [
    ['Applications', summary.data?.totalApplications ?? 0, Users],
    ['In progress', summary.data?.applicationInProgress ?? 0, Clock3],
    ['Credentials sent', summary.data?.credentialDelivered ?? 0, KeyRound],
    ['Activated', summary.data?.activated ?? 0, CheckCircle2],
    ['Activation failed', summary.data?.activationFailed ?? 0, ShieldAlert],
  ] as const;

  return (
    <div
      className="space-y-6 p-6"
      data-testid="supplier-applicant-access-admin"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Supplier applicant access</h1>
          <p className="text-sm text-muted-foreground">
            Shared control for verified applications, terminal token closure,
            credential delivery and first-login activation.
          </p>
        </div>
        <Button variant="outline" onClick={() => void refresh()}>
          <RefreshCw className="mr-2 h-4 w-4" />
          Refresh
        </Button>
      </div>

      <Alert>
        <ShieldAlert className="h-4 w-4" />
        <AlertTitle>Separated access lifecycle</AlertTitle>
        <AlertDescription>
          Applicant sessions expose only application, document, payment and
          status functions. Approved portal privileges begin only after
          credential activation.
        </AlertDescription>
      </Alert>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        {cards.map(([label, value, Icon]) => (
          <Card key={label}>
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">{label}</CardTitle>
              <Icon className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent className="text-2xl font-semibold">
              {value}
            </CardContent>
          </Card>
        ))}
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Applicant access history</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Application</TableHead>
                <TableHead>Verified contact</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Credential</TableHead>
                <TableHead>Notification</TableHead>
                <TableHead className="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(history.data || []).map((item) => {
                const status = String(item.status);
                const isPending =
                  status === 'ApprovedPendingCredentialDelivery' ||
                  status === '1';
                const isDelivered =
                  status === 'CredentialDelivered' || status === '2';
                const isFailed =
                  status === 'ActivationFailed' || status === '5';
                const hasProvisionedLogin = Boolean(item.loginIdentifier);
                const canResend =
                  canRecover &&
                  hasProvisionedLogin &&
                  (isDelivered || isPending);
                const canRetry =
                  canRecover &&
                  (isFailed || (isPending && !hasProvisionedLogin));
                return (
                  <TableRow key={item.id}>
                    <TableCell>
                      <div className="font-medium">{item.companyName}</div>
                      <div className="text-xs text-muted-foreground">
                        {item.registrationNumber}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div>{item.verifiedContactMasked}</div>
                      <div className="text-xs text-muted-foreground">
                        {item.verifiedChannel}
                      </div>
                    </TableCell>
                    <TableCell>
                      <Badge variant="outline">{status}</Badge>
                    </TableCell>
                    <TableCell>
                      <div>{item.loginIdentifier || 'Not provisioned'}</div>
                      <div className="text-xs text-muted-foreground">
                        Expires {dateTime(item.temporaryCredentialExpiresAtUtc)}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div>
                        {item.lastNotificationStatus || 'Not attempted'}
                      </div>
                      <div className="text-xs text-muted-foreground">
                        {item.notificationAttemptCount} attempt(s)
                      </div>
                    </TableCell>
                    <TableCell className="space-x-2 text-right">
                      {canRetry && (
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={Boolean(busyId)}
                          onClick={() => void run(item.registrationId, 'retry')}
                        >
                          <RotateCw className="mr-2 h-3.5 w-3.5" />
                          Retry
                        </Button>
                      )}
                      {canRetry && !hasProvisionedLogin && (
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={Boolean(busyId)}
                          onClick={() =>
                            setCorrection({
                              registrationId: item.registrationId,
                              companyName: item.companyName,
                              channel:
                                item.verifiedChannel === 'Sms'
                                  ? 'Sms'
                                  : 'Email',
                              contact: '',
                              reason: '',
                              otpCode: '',
                              challengeSent: false,
                            })
                          }
                        >
                          <PencilLine className="mr-2 h-3.5 w-3.5" />
                          Correct contact
                        </Button>
                      )}
                      {canResend && (
                        <Button
                          size="sm"
                          disabled={Boolean(busyId)}
                          onClick={() =>
                            void run(item.registrationId, 'resend')
                          }
                        >
                          <KeyRound className="mr-2 h-3.5 w-3.5" />
                          Resend
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })}
              {!history.isLoading && !history.data?.length && (
                <TableRow>
                  <TableCell
                    colSpan={6}
                    className="py-10 text-center text-muted-foreground"
                  >
                    No token-gated supplier applications have been issued.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Dialog
        open={Boolean(correction)}
        onOpenChange={(open) => {
          if (!open && !busyId) setCorrection(undefined);
        }}
      >
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Correct verified supplier contact</DialogTitle>
            <DialogDescription>
              {correction?.companyName}. The new contact must complete OTP
              verification before the application and credential-delivery record
              are changed.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="grid gap-2 sm:grid-cols-[140px_1fr]">
              <div className="space-y-2">
                <Label htmlFor="correction-channel">Channel</Label>
                <Select
                  value={correction?.channel}
                  disabled={correction?.challengeSent}
                  onValueChange={(value: 'Email' | 'Sms') =>
                    setCorrection((current) =>
                      current
                        ? {
                            ...current,
                            channel: value,
                            challengeSent: false,
                            maskedContact: undefined,
                          }
                        : current
                    )
                  }
                >
                  <SelectTrigger id="correction-channel">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Email">Email</SelectItem>
                    <SelectItem value="Sms">SMS / phone</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="correction-contact">New supplier contact</Label>
                <Input
                  id="correction-contact"
                  type={correction?.channel === 'Email' ? 'email' : 'tel'}
                  value={correction?.contact || ''}
                  disabled={correction?.challengeSent}
                  placeholder={
                    correction?.channel === 'Email'
                      ? 'supplier@example.com'
                      : '+233...'
                  }
                  onChange={(event) =>
                    setCorrection((current) =>
                      current
                        ? {
                            ...current,
                            contact: event.target.value,
                            challengeSent: false,
                            maskedContact: undefined,
                          }
                        : current
                    )
                  }
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="correction-reason">Correction reason</Label>
              <Textarea
                id="correction-reason"
                value={correction?.reason || ''}
                maxLength={500}
                placeholder="Explain why the verified supplier contact must be corrected."
                onChange={(event) =>
                  setCorrection((current) =>
                    current
                      ? { ...current, reason: event.target.value }
                      : current
                  )
                }
              />
            </div>
            {correction?.challengeSent && (
              <div className="space-y-2">
                <Label htmlFor="correction-otp">
                  Verification code sent to {correction.maskedContact}
                </Label>
                <Input
                  id="correction-otp"
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  maxLength={6}
                  value={correction.otpCode}
                  onChange={(event) =>
                    setCorrection((current) =>
                      current
                        ? {
                            ...current,
                            otpCode: event.target.value
                              .replace(/\D/g, '')
                              .slice(0, 6),
                          }
                        : current
                    )
                  }
                />
              </div>
            )}
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              disabled={Boolean(busyId)}
              onClick={() => setCorrection(undefined)}
            >
              Cancel
            </Button>
            {!correction?.challengeSent ? (
              <Button
                disabled={
                  Boolean(busyId) ||
                  !correction?.contact.trim() ||
                  (correction?.reason.trim().length ?? 0) < 10
                }
                onClick={() => void sendCorrectionChallenge()}
              >
                Send verification code
              </Button>
            ) : (
              <Button
                disabled={Boolean(busyId) || correction.otpCode.length !== 6}
                onClick={() => void confirmCorrection()}
              >
                Verify, correct and provision
              </Button>
            )}
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
