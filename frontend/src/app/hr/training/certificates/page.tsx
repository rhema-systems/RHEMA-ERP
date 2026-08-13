'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import {
  Search,
  MoreHorizontal,
  Ban,
  Award,
  ShieldCheck,
  Copy,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { TextareaField } from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { trainingCertificateService } from '@/services/hr/training-certificate.service';
import { CERTIFICATE_STATUS_OPTIONS } from '@/types/hr/training-certificates';
import type {
  TrainingCertificateSummary,
  CertificateVerificationResult,
} from '@/types/hr/training-certificates';

const statusLabel = (v: string) =>
  CERTIFICATE_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;
const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * Certificates this organisation has issued.
 *
 * There is no "issue certificate" button here on purpose — a certificate is issued against a
 * specific completed nomination, so it belongs on that record rather than on a register that has no
 * nomination in hand. This screen is the register, the expiry watch, and revocation.
 */
export default function TrainingCertificatesPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [view, setView] = useState<'mine' | 'expiring' | 'employee'>('mine');
  const [search, setSearch] = useState('');
  const [revokeTarget, setRevokeTarget] = useState<TrainingCertificateSummary | null>(null);
  const [busy, setBusy] = useState(false);

  // Verification runs against the same anonymous endpoint a third party would hit, so what HR sees
  // here is exactly what an outside checker sees — including a revoked or unknown code.
  const [checkOpen, setCheckOpen] = useState(false);
  const [checkCode, setCheckCode] = useState('');
  const [checking, setChecking] = useState(false);
  const [checkResult, setCheckResult] = useState<CertificateVerificationResult | null>(null);

  const employeeForm = useForm<{ employeeId: string }>({ defaultValues: { employeeId: '' } });
  const selectedEmployee = employeeForm.watch('employeeId');
  const revokeForm = useForm<{ revokedReason: string }>({ defaultValues: { revokedReason: '' } });

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'training', 'certificates', view, selectedEmployee],
    queryFn: () => {
      if (view === 'expiring') return trainingCertificateService.getExpiring(90);
      if (view === 'employee' && selectedEmployee) {
        return trainingCertificateService.getForEmployee(selectedEmployee);
      }
      return trainingCertificateService.getMine();
    },
  });

  const rows = useMemo(() => {
    const term = search.trim().toLowerCase();
    const all = data ?? [];
    if (!term) return all;
    return all.filter(
      (c) =>
        c.certificateNumber.toLowerCase().includes(term) ||
        c.certificateName.toLowerCase().includes(term) ||
        c.employeeName.toLowerCase().includes(term) ||
        c.programName.toLowerCase().includes(term),
    );
  }, [data, search]);

  const copyVerification = async (code?: string | null) => {
    if (!code) return;
    try {
      await navigator.clipboard.writeText(code);
      toast({ title: 'Copied', description: 'Verification code copied to the clipboard.' });
    } catch {
      toast({ title: 'Could not copy', description: code, variant: 'destructive' });
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Training Certificates"
        description="Certificates issued off completed training, and the codes third parties use to check them."
        backHref="/hr/training"
        actions={
          <Button variant="outline" onClick={() => setCheckOpen(true)}>
            <ShieldCheck className="mr-2 h-4 w-4" /> Check a code
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <CardTitle>Register</CardTitle>
              <CardDescription>
                Certificates are issued from a completed nomination, not from here.
              </CardDescription>
            </div>
            <div className="flex items-center gap-2">
              <Tabs value={view} onValueChange={(v) => setView(v as typeof view)}>
                <TabsList>
                  <TabsTrigger value="mine">Mine</TabsTrigger>
                  <TabsTrigger value="expiring">Expiring</TabsTrigger>
                  <TabsTrigger value="employee">By employee</TabsTrigger>
                </TabsList>
              </Tabs>
              <div className="relative w-56">
                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Search…"
                  className="pl-8"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
              </div>
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {view === 'employee' && (
            <div className="max-w-sm">
              <EmployeePickerField form={employeeForm} name="employeeId" label="Employee" />
            </div>
          )}

          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Certificate</TableHead>
                  <TableHead>Holder</TableHead>
                  <TableHead>Programme</TableHead>
                  <TableHead>Issued</TableHead>
                  <TableHead>Expires</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(4)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(7)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[90px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : view === 'employee' && !selectedEmployee ? (
                  <TableRow>
                    <TableCell colSpan={7}>
                      <EmptyState
                        icon={Award}
                        title="Pick an employee"
                        description="Choose someone above to see the certificates they hold."
                      />
                    </TableCell>
                  </TableRow>
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7}>
                      <EmptyState
                        icon={Award}
                        title={view === 'expiring' ? 'Nothing expiring' : 'No certificates'}
                        description={
                          view === 'expiring'
                            ? 'No certificates fall due in the next 90 days.'
                            : 'Certificates appear here once they are issued against a completed nomination.'
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((c) => (
                    <TableRow key={c.id}>
                      <TableCell>
                        <div className="font-medium">{c.certificateName}</div>
                        <div className="font-mono text-[11px] text-muted-foreground">
                          {c.certificateNumber}
                        </div>
                      </TableCell>
                      <TableCell className="text-muted-foreground">{c.employeeName}</TableCell>
                      <TableCell className="text-muted-foreground">{c.programName}</TableCell>
                      <TableCell className="text-muted-foreground">{fmt(c.issuedDate)}</TableCell>
                      <TableCell>
                        {c.expiryDate ? (
                          <span className={c.isExpired ? 'text-destructive' : undefined}>
                            {fmt(c.expiryDate)}
                          </span>
                        ) : (
                          <span className="text-muted-foreground">No expiry</span>
                        )}
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-1.5">
                          <StatusBadge status={statusLabel(c.status)} />
                          {c.isExpired && c.status !== 'Revoked' && (
                            <Badge variant="outline" className="text-[10px]">
                              Lapsed
                            </Badge>
                          )}
                        </div>
                      </TableCell>
                      <TableCell>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" className="h-8 w-8 p-0">
                              <span className="sr-only">Open menu</span>
                              <MoreHorizontal className="h-4 w-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuLabel>Actions</DropdownMenuLabel>
                            <DropdownMenuItem
                              disabled={!c.verificationCode}
                              onClick={() => copyVerification(c.verificationCode)}
                            >
                              <Copy className="mr-2 h-4 w-4" /> Copy verification code
                            </DropdownMenuItem>
                            <DropdownMenuItem
                              disabled={!c.verificationCode}
                              onClick={() => {
                                setCheckCode(c.verificationCode ?? '');
                                setCheckResult(null);
                                setCheckOpen(true);
                              }}
                            >
                              <ShieldCheck className="mr-2 h-4 w-4" /> Check this code
                            </DropdownMenuItem>
                            {c.status !== 'Revoked' && (
                              <>
                                <DropdownMenuSeparator />
                                <DropdownMenuItem
                                  className="text-destructive focus:text-destructive"
                                  onClick={() => {
                                    revokeForm.reset({ revokedReason: '' });
                                    setRevokeTarget(c);
                                  }}
                                >
                                  <Ban className="mr-2 h-4 w-4" /> Revoke
                                </DropdownMenuItem>
                              </>
                            )}
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <Dialog open={checkOpen} onOpenChange={setCheckOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Check a verification code</DialogTitle>
            <DialogDescription>
              Runs the same anonymous check an outside party would — so this is exactly what they
              would see.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3 py-2">
            <Input
              value={checkCode}
              onChange={(e) => setCheckCode(e.target.value)}
              placeholder="Verification code"
              className="font-mono"
            />
            {checkResult && (
              <div className="rounded-md border p-3 text-sm">
                {!checkResult.found ? (
                  <p className="font-medium text-destructive">No certificate matches that code.</p>
                ) : (
                  <div className="space-y-1">
                    <p className="font-medium">
                      {checkResult.isValid ? (
                        <span className="text-green-600">Valid — {checkResult.status}</span>
                      ) : (
                        <span className="text-destructive">Not valid — {checkResult.status}</span>
                      )}
                    </p>
                    <p className="text-muted-foreground">{checkResult.certificateName}</p>
                    <p className="text-muted-foreground">
                      {checkResult.employeeName} · {checkResult.programName}
                    </p>
                    <p className="text-muted-foreground">
                      Issued {fmt(checkResult.issuedDate)}
                      {checkResult.expiryDate ? ` · expires ${fmt(checkResult.expiryDate)}` : ''}
                    </p>
                    {checkResult.revokedReason && (
                      <p className="text-destructive">Revoked: {checkResult.revokedReason}</p>
                    )}
                  </div>
                )}
              </div>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCheckOpen(false)} disabled={checking}>
              Close
            </Button>
            <Button
              disabled={checking || !checkCode.trim()}
              onClick={async () => {
                setChecking(true);
                setCheckResult(null);
                try {
                  setCheckResult(await trainingCertificateService.verifyByCode(checkCode.trim()));
                } catch (error: any) {
                  toast({
                    title: 'Check failed',
                    description: error?.message || 'Could not run the check.',
                    variant: 'destructive',
                  });
                } finally {
                  setChecking(false);
                }
              }}
            >
              Check
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={revokeTarget !== null} onOpenChange={(o) => !o && setRevokeTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Revoke certificate</DialogTitle>
            <DialogDescription>
              {revokeTarget
                ? `"${revokeTarget.certificateName}" for ${revokeTarget.employeeName}. Anyone checking the verification code will immediately see it as revoked, along with this reason — so write it for an outside reader.`
                : ''}
            </DialogDescription>
          </DialogHeader>
          <div className="py-2">
            <TextareaField form={revokeForm} name="revokedReason" label="Reason" rows={3} required />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRevokeTarget(null)} disabled={busy}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              disabled={busy}
              onClick={async () => {
                if (!revokeTarget) return;
                const reason = revokeForm.getValues('revokedReason').trim();
                if (!reason) {
                  toast({ title: 'A reason is required', variant: 'destructive' });
                  return;
                }
                setBusy(true);
                try {
                  await trainingCertificateService.revoke(revokeTarget.id, { revokedReason: reason });
                  await queryClient.invalidateQueries({
                    queryKey: ['hr', 'training', 'certificates'],
                  });
                  toast({ title: 'Revoked' });
                  setRevokeTarget(null);
                } catch (error: any) {
                  toast({
                    title: 'Error',
                    description: error?.message || 'Failed to revoke.',
                    variant: 'destructive',
                  });
                } finally {
                  setBusy(false);
                }
              }}
            >
              Revoke
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
