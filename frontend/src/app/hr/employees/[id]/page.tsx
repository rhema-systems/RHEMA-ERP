'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil, UserX, UserCheck, Ban, RotateCcw } from 'lucide-react';
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

  const { data: e, isLoading, isError } = useQuery({
    queryKey: ['hr', 'employees', id, 'details'],
    queryFn: () => employeeService.getDetails(id),
    enabled: !!id,
  });

  const { data: manager } = useQuery({
    queryKey: ['hr', 'employees', e?.managerId, 'lookup'],
    queryFn: () => employeeService.getById(e!.managerId as string),
    enabled: !!e?.managerId,
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

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="contacts">Contacts</TabsTrigger>
          <TabsTrigger value="dependents">Dependents</TabsTrigger>
          <TabsTrigger value="qualifications">Qualifications</TabsTrigger>
          <TabsTrigger value="bank">Bank</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="space-y-4 pt-4">
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
            <InfoRow label="Probation (days)" value={e.probationPeriodDays} />
            <InfoRow label="Full-time" value={yn(e.isFullTime)} />
            <InfoRow label="On Probation" value={yn(e.isOnProbation)} />
          </InfoCard>

          <InfoCard title="Compensation & Tax">
            <InfoRow label="Salary" value={money(e.salary)} />
            <InfoRow label="Tax Number" value={e.taxNumber} />
            <InfoRow label="SSNIT Number" value={e.socialSecurityNumber} />
            <InfoRow label="TIN" value={e.tinNumber} />
            <InfoRow label="Pay Tax" value={yn(e.payTax)} />
            <InfoRow label="SS Fund" value={yn(e.ssFund)} />
          </InfoCard>

          {isTerminated && (
            <InfoCard title="Termination">
              <InfoRow label="Date" value={e.terminationDate?.slice(0, 10)} />
              <InfoRow label="Reason" value={e.terminationReason} />
              <InfoRow label="Notes" value={e.terminationNotes} />
            </InfoCard>
          )}
        </TabsContent>

        {(['contacts', 'dependents', 'qualifications', 'bank'] as const).map((tab) => (
          <TabsContent key={tab} value={tab} className="pt-4">
            <Card>
              <CardContent className="py-8">
                <EmptyState
                  title="Coming soon"
                  description="This section will be built in a follow-up iteration."
                />
              </CardContent>
            </Card>
          </TabsContent>
        ))}
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
