'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from '@/components/ui/tabs';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { KeyRound, LockKeyhole, ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';
import { supplierApplicantAccessService as service } from '@/services/procurement-supplier-applicant-access.service';
import type {
  SupplierApplicantChannel,
  SupplierApplicantIssueResult,
  SupplierRegistrationCategory,
} from '@/types/procurement-supplier-applicant-access';

export default function SupplierApplicationAccessPage() {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [challengeSent, setChallengeSent] = useState(false);
  const [issued, setIssued] = useState<SupplierApplicantIssueResult | null>(null);
  const [channel, setChannel] = useState<SupplierApplicantChannel>('Email');
  const [contact, setContact] = useState('');
  const [companyName, setCompanyName] = useState('');
  const [category, setCategory] =
    useState<SupplierRegistrationCategory>('Goods');
  const [otpCode, setOtpCode] = useState('');
  const [applicationToken, setApplicationToken] = useState('');

  const requestCode = async () => {
    setBusy(true);
    try {
      await service.requestChallenge({ channel, contact });
      setChallengeSent(true);
      toast.success('Verification code sent.');
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Could not send code.');
    } finally {
      setBusy(false);
    }
  };

  const verifyAndIssue = async () => {
    setBusy(true);
    try {
      const result = await service.verifyAndIssue({
        channel,
        contact,
        otpCode,
        companyName,
        registrationCategory: category,
      });
      setIssued(result);
      setApplicationToken(result.applicationToken);
      toast.success('Application token issued.');
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Verification could not be completed.'
      );
    } finally {
      setBusy(false);
    }
  };

  const login = async () => {
    setBusy(true);
    try {
      const session = await service.startSession({ applicationToken });
      service.setSessionToken(session.sessionToken);
      router.push('/supplier-application/portal');
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'The application token was not accepted.'
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <main className="min-h-screen bg-slate-950 px-4 py-10 text-slate-100">
      <div className="mx-auto max-w-5xl">
        <div className="mb-8 flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
          <div>
            <Badge className="mb-3 bg-emerald-500/15 text-emerald-300 hover:bg-emerald-500/15">
              TDC supplier onboarding
            </Badge>
            <h1 className="text-3xl font-semibold tracking-tight">
              Secure supplier application access
            </h1>
            <p className="mt-2 max-w-2xl text-sm text-slate-400">
              Verify one email address or phone number, obtain an application-bound
              token, and use it to submit documents and track the review. No supplier
              account or supplier privileges are created before approval.
            </p>
          </div>
          <div className="flex items-center gap-2 text-xs text-slate-400">
            <ShieldCheck className="h-4 w-4 text-emerald-400" />
            Token closes only on approval or rejection
          </div>
        </div>

        <Tabs defaultValue="apply" className="grid gap-5">
          <TabsList className="w-full max-w-md bg-slate-900">
            <TabsTrigger value="apply" className="flex-1">
              Apply for token
            </TabsTrigger>
            <TabsTrigger value="login" className="flex-1">
              Token login
            </TabsTrigger>
          </TabsList>

          <TabsContent value="apply">
            <Card className="border-slate-800 bg-slate-900 text-slate-100">
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <KeyRound className="h-5 w-5 text-emerald-400" />
                  Verify contact and issue token
                </CardTitle>
                <CardDescription className="text-slate-400">
                  The effective DEC-007 configuration determines whether this token
                  is free or paid.
                </CardDescription>
              </CardHeader>
              <CardContent className="grid gap-5">
                <div className="grid gap-4 md:grid-cols-2">
                  <div className="grid gap-2">
                    <Label className="text-slate-200">Verification channel</Label>
                    <select
                      className="h-10 rounded-md border border-slate-700 bg-slate-950 px-3"
                      value={channel}
                      onChange={(event) =>
                        setChannel(event.target.value as SupplierApplicantChannel)
                      }
                    >
                      <option value="Email">Email</option>
                      <option value="Sms">SMS / phone</option>
                    </select>
                  </div>
                  <div className="grid gap-2">
                    <Label className="text-slate-200">
                      {channel === 'Email' ? 'Email address' : 'Phone number'}
                    </Label>
                    <Input
                      className="border-slate-700 bg-slate-950"
                      value={contact}
                      onChange={(event) => setContact(event.target.value)}
                      placeholder={
                        channel === 'Email' ? 'name@company.com' : '+233...'
                      }
                    />
                  </div>
                  <div className="grid gap-2">
                    <Label className="text-slate-200">Company name</Label>
                    <Input
                      className="border-slate-700 bg-slate-950"
                      value={companyName}
                      onChange={(event) => setCompanyName(event.target.value)}
                    />
                  </div>
                  <div className="grid gap-2">
                    <Label className="text-slate-200">Registration category</Label>
                    <select
                      className="h-10 rounded-md border border-slate-700 bg-slate-950 px-3"
                      value={category}
                      onChange={(event) =>
                        setCategory(
                          event.target.value as SupplierRegistrationCategory
                        )
                      }
                    >
                      <option value="Goods">Goods</option>
                      <option value="Works">Works</option>
                      <option value="Services">Services</option>
                    </select>
                  </div>
                </div>

                {!challengeSent ? (
                  <Button
                    className="w-fit bg-emerald-600 hover:bg-emerald-500"
                    disabled={busy || !contact || !companyName}
                    onClick={requestCode}
                  >
                    Send verification code
                  </Button>
                ) : (
                  <div className="grid gap-3 rounded-lg border border-slate-700 p-4">
                    <Label className="text-slate-200">
                      Six-digit verification code
                    </Label>
                    <Input
                      className="max-w-xs border-slate-700 bg-slate-950 tracking-[0.35em]"
                      value={otpCode}
                      maxLength={6}
                      onChange={(event) => setOtpCode(event.target.value)}
                    />
                    <Button
                      className="w-fit bg-emerald-600 hover:bg-emerald-500"
                      disabled={busy || otpCode.length !== 6}
                      onClick={verifyAndIssue}
                    >
                      Verify and issue token
                    </Button>
                  </div>
                )}

                {issued && (
                  <Alert className="border-emerald-700 bg-emerald-950/40 text-emerald-100">
                    <AlertDescription className="grid gap-3">
                      <div>
                        Application <strong>{issued.registrationNumber}</strong> was
                        created. Copy the token now; plaintext is never stored.
                      </div>
                      <code className="break-all rounded bg-slate-950 p-3 text-sm">
                        {issued.applicationToken}
                      </code>
                      <div className="flex flex-wrap gap-2 text-xs">
                        <Badge variant="outline">{String(issued.feeMode)}</Badge>
                        <Badge variant="outline">
                          {issued.currencyCode} {issued.totalAmount.toFixed(2)}
                        </Badge>
                        <Badge variant="outline">
                          Delivery {issued.deliveryStatus}
                        </Badge>
                      </div>
                    </AlertDescription>
                  </Alert>
                )}
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="login">
            <Card className="border-slate-800 bg-slate-900 text-slate-100">
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <LockKeyhole className="h-5 w-5 text-sky-400" />
                  Restricted applicant portal
                </CardTitle>
                <CardDescription className="text-slate-400">
                  This does not sign you into the approved supplier portal.
                </CardDescription>
              </CardHeader>
              <CardContent className="grid gap-4">
                <div className="grid gap-2">
                  <Label className="text-slate-200">Application token</Label>
                  <Input
                    className="border-slate-700 bg-slate-950"
                    value={applicationToken}
                    onChange={(event) => setApplicationToken(event.target.value)}
                    autoComplete="off"
                  />
                </div>
                <Button
                  className="w-fit bg-sky-600 hover:bg-sky-500"
                  disabled={busy || !applicationToken}
                  onClick={login}
                >
                  Open application
                </Button>
              </CardContent>
            </Card>
          </TabsContent>
        </Tabs>
      </div>
    </main>
  );
}
