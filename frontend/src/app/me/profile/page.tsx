'use client';

import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertCircle,
  Banknote,
  BadgeCheck,
  Clock,
  Landmark,
  Loader2,
  Lock,
  Pencil,
  ShieldCheck,
  UserRound,
} from 'lucide-react';
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
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { useToast } from '@/hooks/use-toast';
import {
  myProfileService,
  BANK_PROFILE_FIELDS,
  type EmployeeProfileField,
  type MyProfile,
  type MyProfileBankDetail,
} from '@/services/hr/my-profile.service';
import { cn } from '@/lib/utils';

/**
 * My Profile (area 25 slice 12a, decision D6).
 *
 * The screen's whole job is to make one distinction obvious: some of this you correct
 * yourself, and some of it HR has to approve. Contact numbers are edited in place. A name, a
 * date of birth, a statutory number or a bank account opens a request with a reason and
 * evidence — because a wrong bank account is a payroll fraud vector and a wrong date of birth
 * moves a retirement date. Employment placement is neither: it is shown read-only with no
 * affordance at all, because asking HR to change your own salary is not a workflow.
 *
 * ⚠ Account numbers arrive MASKED from the server. Never render one as if it were complete.
 */

const fmtDate = (v?: string | null) =>
  v ? new Date(v).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' }) : '—';

const show = (v?: string | null) => (v && String(v).trim() !== '' ? v : '—');

/**
 * The bank fields, which are the only ones rendered from a list — every other requestable
 * field is a named row below, so a second copy of them here would be config that silently
 * drifts out of step with what the screen actually shows.
 */
const BANK_FIELD_DEFS: { field: EmployeeProfileField; label: string }[] = [
  { field: 'BankName', label: 'Bank' },
  { field: 'BankBranchName', label: 'Branch' },
  { field: 'BankAccountNumber', label: 'Account number' },
  { field: 'BankAccountName', label: 'Account name' },
  { field: 'MobileMoneyNumber', label: 'Mobile money number' },
];

function Field({
  label,
  value,
  pending,
  onRequest,
}: {
  label: string;
  value: React.ReactNode;
  pending?: boolean;
  onRequest?: () => void;
}) {
  return (
    <div className="flex items-start justify-between gap-3 border-b py-2.5 last:border-b-0">
      <div className="min-w-0">
        <div className="text-xs uppercase tracking-wide text-muted-foreground">{label}</div>
        <div className="mt-0.5 break-words">{value}</div>
      </div>
      {pending ? (
        <Badge variant="secondary" className="shrink-0 gap-1">
          <Clock className="h-3 w-3" /> Awaiting HR
        </Badge>
      ) : onRequest ? (
        <Button variant="ghost" size="sm" className="shrink-0" onClick={onRequest}>
          <Pencil className="mr-1 h-3.5 w-3.5" /> Request change
        </Button>
      ) : null}
    </div>
  );
}

export default function MyProfilePage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data: profile, isLoading, isError } = useQuery({
    queryKey: ['me', 'profile'],
    queryFn: () => myProfileService.getProfile(),
  });

  const pending = useMemo(
    () => new Set<EmployeeProfileField>(profile?.fieldsWithPendingRequests ?? []),
    [profile],
  );

  // ── The direct-edit form ────────────────────────────────────────────────
  const [contact, setContact] = useState({
    mobileNumber: '',
    telephoneNumber: '',
    businessNumber: '',
    extension: '',
  });
  useEffect(() => {
    if (!profile) return;
    setContact({
      mobileNumber: profile.mobileNumber ?? '',
      telephoneNumber: profile.telephoneNumber ?? '',
      businessNumber: profile.businessNumber ?? '',
      extension: profile.extension ?? '',
    });
  }, [profile]);

  const saveContact = useMutation({
    mutationFn: () => myProfileService.updateContactDetails(contact),
    onSuccess: (updated) => {
      queryClient.setQueryData(['me', 'profile'], updated);
      toast({ title: 'Saved', description: 'Your contact details are up to date.' });
    },
    onError: (e: any) =>
      toast({
        title: 'Not saved',
        description: e?.message || 'Your contact details could not be saved.',
        variant: 'destructive',
      }),
  });

  // ── The change-request dialog ───────────────────────────────────────────
  const [openField, setOpenField] = useState<{ field: EmployeeProfileField; label: string; hint?: string } | null>(null);
  const [newValue, setNewValue] = useState('');
  const [reason, setReason] = useState('');
  const [bankDetailId, setBankDetailId] = useState<string | null>(null);

  const isBankField = openField ? BANK_PROFILE_FIELDS.includes(openField.field) : false;

  const submitRequest = useMutation({
    mutationFn: ({ field, isBank }: { field: EmployeeProfileField; isBank: boolean }) =>
      myProfileService.createChangeRequest({
        reason,
        bankDetailId: isBank ? bankDetailId : null,
        items: [{ field, newValue }],
      }),
    onSuccess: (created) => {
      queryClient.invalidateQueries({ queryKey: ['me', 'profile'] });
      queryClient.invalidateQueries({ queryKey: ['me', 'profile', 'change-requests'] });
      setOpenField(null);
      toast({
        title: `Request ${created.requestNumber} sent to HR`,
        description: 'You will see the outcome here, with HR\'s comment.',
      });
    },
    onError: (e: any) =>
      toast({
        title: 'Request not sent',
        description: e?.message || 'The request could not be filed.',
        variant: 'destructive',
      }),
  });

  const startRequest = (f: { field: EmployeeProfileField; label: string; hint?: string }, bank?: MyProfileBankDetail) => {
    setOpenField(f);
    setNewValue('');
    setReason('');
    setBankDetailId(bank?.id ?? profile?.bankDetails.find((b) => b.isPrimary)?.id ?? profile?.bankDetails[0]?.id ?? null);
  };

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-64" />
      </div>
    );
  }

  if (isError || !profile) {
    return (
      <div className="space-y-6">
        <PageHeader title="My Profile" backHref="/me" />
        <p className="text-sm text-muted-foreground">
          Your profile could not be loaded right now. Try again in a moment.
        </p>
      </div>
    );
  }

  const requestableFor = (field: EmployeeProfileField, label: string, hint?: string) =>
    pending.has(field) ? undefined : () => startRequest({ field, label, hint });

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Profile"
        description="What the organisation holds about you. Contact numbers you can correct yourself; anything that affects your identity or your pay goes to HR with a reason."
        backHref="/me"
      />

      <Tabs defaultValue="personal">
        <TabsList>
          <TabsTrigger value="personal">Personal</TabsTrigger>
          <TabsTrigger value="contact">Contact</TabsTrigger>
          <TabsTrigger value="pay">Bank & statutory</TabsTrigger>
          <TabsTrigger value="employment">Employment</TabsTrigger>
          <TabsTrigger value="people">My people</TabsTrigger>
        </TabsList>

        {/* ── Personal ─────────────────────────────────────────────── */}
        <TabsContent value="personal" className="mt-4">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="flex items-center gap-2 text-base">
                <UserRound className="h-4 w-4" /> Name & identity
              </CardTitle>
            </CardHeader>
            <CardContent className="pt-0">
              <Field label="Employee number" value={<span className="font-mono">{profile.employeeNumber}</span>} />
              <Field label="First name" value={show(profile.firstName)} pending={pending.has('FirstName')} onRequest={requestableFor('FirstName', 'First name')} />
              <Field label="Middle name" value={show(profile.middleName)} pending={pending.has('MiddleName')} onRequest={requestableFor('MiddleName', 'Middle name')} />
              <Field label="Last name" value={show(profile.lastName)} pending={pending.has('LastName')} onRequest={requestableFor('LastName', 'Last name')} />
              <Field label="Title" value={show(profile.title)} pending={pending.has('Title')} onRequest={requestableFor('Title', 'Title')} />
              <Field label="Date of birth" value={fmtDate(profile.dateOfBirth)} pending={pending.has('DateOfBirth')} onRequest={requestableFor('DateOfBirth', 'Date of birth', 'yyyy-mm-dd')} />
              <Field label="Gender" value={show(profile.gender)} pending={pending.has('Gender')} onRequest={requestableFor('Gender', 'Gender')} />
              <Field label="Marital status" value={show(profile.maritalStatus)} />
              <Field label="Religion" value={show(profile.religion)} />
              <Field label="Blood type" value={show(profile.bloodType)} />
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Contact ──────────────────────────────────────────────── */}
        <TabsContent value="contact" className="mt-4 space-y-4">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Yours to change</CardTitle>
              <p className="text-sm text-muted-foreground">
                Saved straight away — no approval needed. Clear a box to remove the number.
              </p>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-1.5">
                  <Label htmlFor="mobileNumber">Mobile number</Label>
                  <Input id="mobileNumber" value={contact.mobileNumber}
                    onChange={(e) => setContact({ ...contact, mobileNumber: e.target.value })} />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="telephoneNumber">Telephone</Label>
                  <Input id="telephoneNumber" value={contact.telephoneNumber}
                    onChange={(e) => setContact({ ...contact, telephoneNumber: e.target.value })} />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="businessNumber">Business number</Label>
                  <Input id="businessNumber" value={contact.businessNumber}
                    onChange={(e) => setContact({ ...contact, businessNumber: e.target.value })} />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="extension">Extension</Label>
                  <Input id="extension" value={contact.extension}
                    onChange={(e) => setContact({ ...contact, extension: e.target.value })} />
                </div>
              </div>
              <Button onClick={() => saveContact.mutate()} disabled={saveContact.isPending}>
                {saveContact.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Save contact details
              </Button>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="flex items-center gap-2 text-base">
                <Lock className="h-4 w-4" /> Email & address
              </CardTitle>
              <p className="text-sm text-muted-foreground">
                Your email signs you in, and your address is on official correspondence — HR
                confirms changes to these.
              </p>
            </CardHeader>
            <CardContent className="pt-0">
              <Field label="Email address" value={show(profile.emailAddress)} pending={pending.has('EmailAddress')} onRequest={requestableFor('EmailAddress', 'Email address')} />
              <Field label="Address" value={show(profile.address)} pending={pending.has('Address')} onRequest={requestableFor('Address', 'Address')} />
              <Field label="City" value={show(profile.city)} pending={pending.has('City')} onRequest={requestableFor('City', 'City')} />
              <Field label="Region / state" value={show(profile.state)} pending={pending.has('State')} onRequest={requestableFor('State', 'Region / state')} />
              <Field label="Postal code" value={show(profile.postalCode)} pending={pending.has('PostalCode')} onRequest={requestableFor('PostalCode', 'Postal code')} />
              <Field label="Digital address" value={show(profile.digitalAddress)} pending={pending.has('DigitalAddress')} onRequest={requestableFor('DigitalAddress', 'Digital address')} />
              <Field label="Country" value={show(profile.countryName)} />
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Bank & statutory ─────────────────────────────────────── */}
        <TabsContent value="pay" className="mt-4 space-y-4">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="flex items-center gap-2 text-base">
                <Landmark className="h-4 w-4" /> Where you are paid
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4 pt-0">
              {profile.bankDetails.length === 0 ? (
                <p className="text-sm text-muted-foreground">
                  No bank account is on file. Ask HR to add one — a new account cannot be created
                  from here.
                </p>
              ) : (
                profile.bankDetails.map((b) => (
                  <div key={b.id} className="rounded-lg border p-3">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-medium">{b.bankName}</span>
                      {b.isPrimary && <Badge variant="secondary">Primary</Badge>}
                      {b.isVerified ? (
                        <Badge variant="outline" className="gap-1">
                          <BadgeCheck className="h-3 w-3" /> Verified
                        </Badge>
                      ) : (
                        <Badge variant="outline" className="gap-1 text-amber-700 dark:text-amber-400">
                          <AlertCircle className="h-3 w-3" /> Not verified
                        </Badge>
                      )}
                    </div>
                    <div className="mt-1 text-sm text-muted-foreground">
                      {show(b.branchName)} · {b.accountName}
                    </div>
                    {/* Masked by the server — the last four digits only. */}
                    <div className="mt-1 font-mono text-sm">{b.accountNumber}</div>
                    <div className="mt-3 flex flex-wrap gap-2">
                      {BANK_FIELD_DEFS.map((f) => (
                        <Button
                          key={f.field}
                          variant="outline"
                          size="sm"
                          disabled={pending.has(f.field)}
                          onClick={() => startRequest(f, b)}
                        >
                          {pending.has(f.field) ? `${f.label} — awaiting HR` : `Change ${f.label.toLowerCase()}`}
                        </Button>
                      ))}
                    </div>
                  </div>
                ))
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="flex items-center gap-2 text-base">
                <ShieldCheck className="h-4 w-4" /> Statutory numbers
              </CardTitle>
            </CardHeader>
            <CardContent className="pt-0">
              <Field label="SSNIT number" value={show(profile.socialSecurityNumber)} pending={pending.has('SocialSecurityNumber')} onRequest={requestableFor('SocialSecurityNumber', 'SSNIT number')} />
              <Field label="TIN" value={show(profile.tinNumber)} pending={pending.has('TINNumber')} onRequest={requestableFor('TINNumber', 'TIN')} />
              <Field label="Tax number" value={show(profile.taxNumber)} pending={pending.has('TaxNumber')} onRequest={requestableFor('TaxNumber', 'Tax number')} />
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Employment (read-only, deliberately) ─────────────────── */}
        <TabsContent value="employment" className="mt-4">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="flex items-center gap-2 text-base">
                <Banknote className="h-4 w-4" /> Your job
              </CardTitle>
              <p className="text-sm text-muted-foreground">
                Held by HR. If something here is wrong, raise it with HR directly — it is not a
                personal-data correction.
              </p>
            </CardHeader>
            <CardContent className="pt-0">
              <Field label="Position" value={show(profile.positionTitle)} />
              <Field label="Department" value={show(profile.departmentName)} />
              <Field label="Section" value={show(profile.sectionName)} />
              <Field label="Organisation unit" value={show(profile.organizationUnitName)} />
              <Field label="Location" value={show(profile.locationName)} />
              <Field label="Manager" value={show(profile.managerName)} />
              <Field label="Employment type" value={show(profile.employmentType)} />
              <Field label="Status" value={show(profile.staffStatus)} />
              <Field label="Date employed" value={fmtDate(profile.dateEmployed)} />
              <Field label="Confirmed on" value={fmtDate(profile.confirmationDate)} />
              <Field
                label="Years of service"
                value={profile.yearsOfService === null || profile.yearsOfService === undefined
                  ? '—'
                  : `${profile.yearsOfService}`}
              />
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── My people ────────────────────────────────────────────── */}
        <TabsContent value="people" className="mt-4 space-y-4">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Emergency contacts</CardTitle>
            </CardHeader>
            <CardContent className="pt-0">
              {profile.emergencyContacts.length === 0 ? (
                <p className="py-2 text-sm text-muted-foreground">
                  Nobody is recorded as your emergency contact. Ask HR to add one — it matters most
                  on the day nobody has time to look for it.
                </p>
              ) : (
                profile.emergencyContacts.map((c) => (
                  <div key={c.id} className="flex flex-wrap items-center gap-2 border-b py-2.5 last:border-b-0">
                    <span className="font-medium">
                      {c.firstName} {c.lastName}
                    </span>
                    <Badge variant="outline">{c.relationship}</Badge>
                    {c.isPrimary && <Badge variant="secondary">Primary</Badge>}
                    <span className="text-sm text-muted-foreground">{c.phoneNumber}</span>
                  </div>
                ))
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Dependants</CardTitle>
            </CardHeader>
            <CardContent className="pt-0">
              {profile.dependents.length === 0 ? (
                <p className="py-2 text-sm text-muted-foreground">No dependants are recorded.</p>
              ) : (
                profile.dependents.map((d) => (
                  <div key={d.id} className="flex flex-wrap items-center gap-2 border-b py-2.5 last:border-b-0">
                    <span className="font-medium">
                      {d.firstName} {d.lastName}
                    </span>
                    <Badge variant="outline">{d.relationship}</Badge>
                    {d.isEligibleForBenefits && <Badge variant="secondary">Benefit-eligible</Badge>}
                    <span className="text-sm text-muted-foreground">{fmtDate(d.dateOfBirth)}</span>
                  </div>
                ))
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* ── The request dialog ───────────────────────────────────────── */}
      <Dialog open={!!openField} onOpenChange={(o) => !o && setOpenField(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Request a change: {openField?.label}</DialogTitle>
            <DialogDescription>
              HR reviews this before anything changes. Say what it should be and why, and attach
              your evidence from the request once it is filed.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="newValue">
                New {openField?.label.toLowerCase()}
                {openField?.hint && (
                  <span className="ml-1 font-normal text-muted-foreground">({openField.hint})</span>
                )}
              </Label>
              <Input
                id="newValue"
                value={newValue}
                onChange={(e) => setNewValue(e.target.value)}
                placeholder={openField?.hint ?? ''}
              />
            </div>

            {isBankField && (
              <div className="rounded-md border bg-muted/40 p-3 text-sm">
                <div className="font-medium">Account being changed</div>
                <div className="mt-1 text-muted-foreground">
                  {(() => {
                    const b = profile.bankDetails.find((x) => x.id === bankDetailId);
                    return b ? `${b.bankName} · ${b.accountNumber}` : 'No account selected';
                  })()}
                </div>
              </div>
            )}

            <div className="space-y-1.5">
              <Label htmlFor="reason">Why is it changing?</Label>
              <Textarea
                id="reason"
                rows={3}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                placeholder="e.g. Married on 12 July; marriage certificate to follow."
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpenField(null)}>
              Cancel
            </Button>
            <Button
              onClick={() =>
                openField && submitRequest.mutate({ field: openField.field, isBank: isBankField })
              }
              disabled={
                submitRequest.isPending ||
                newValue.trim() === '' ||
                reason.trim() === '' ||
                (isBankField && !bankDetailId)
              }
            >
              {submitRequest.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Send to HR
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
