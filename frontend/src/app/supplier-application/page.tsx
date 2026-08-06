'use client';

import React, { useEffect, useRef, useState } from 'react';
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Copy, KeyRound, LockKeyhole, ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';
import {
  PublicCaptchaChallenge,
  type PublicCaptchaChallengeHandle,
} from '@/components/security/PublicCaptchaChallenge';
import { supplierApplicantAccessService as service } from '@/services/procurement-supplier-applicant-access.service';
import { settingsService } from '@/services/settings';
import type {
  SupplierApplicantChannel,
  SupplierApplicantIssueResult,
  SupplierRegistrationCategory,
} from '@/types/procurement-supplier-applicant-access';

export default function SupplierApplicationAccessPage() {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [challengeSent, setChallengeSent] = useState(false);
  const [issued, setIssued] = useState<SupplierApplicantIssueResult | null>(
    null
  );
  const [channel, setChannel] = useState<SupplierApplicantChannel>('Email');
  const [contact, setContact] = useState('');
  const [companyName, setCompanyName] = useState('');
  const [retainedRegistrationId, setRetainedRegistrationId] = useState('');
  const [category, setCategory] =
    useState<SupplierRegistrationCategory>('Goods');
  const [otpCode, setOtpCode] = useState('');
  const [applicationToken, setApplicationToken] = useState('');
  const [captchaLoading, setCaptchaLoading] = useState(true);
  const [captchaEnabled, setCaptchaEnabled] = useState(false);
  const [captchaProvider, setCaptchaProvider] = useState<
    'recaptcha' | 'hcaptcha'
  >('recaptcha');
  const [recaptchaSiteKey, setRecaptchaSiteKey] = useState<string | null>(null);
  const [hCaptchaSiteKey, setHCaptchaSiteKey] = useState<string | null>(null);
  const [applyCaptchaToken, setApplyCaptchaToken] = useState<string | null>(
    null
  );
  const [loginCaptchaToken, setLoginCaptchaToken] = useState<string | null>(
    null
  );
  const applyCaptchaRef = useRef<PublicCaptchaChallengeHandle>(null);
  const loginCaptchaRef = useRef<PublicCaptchaChallengeHandle>(null);

  useEffect(() => {
    const retainedId = new URLSearchParams(window.location.search).get(
      'retainedRegistrationId'
    );
    if (retainedId) setRetainedRegistrationId(retainedId);
  }, []);

  useEffect(() => {
    let active = true;
    settingsService
      .getPublicSecuritySettings()
      .then((settings) => {
        if (!active) return;
        setCaptchaEnabled(Boolean(settings.captchaEnabled));
        setCaptchaProvider(
          settings.captchaProvider === 'hcaptcha' ? 'hcaptcha' : 'recaptcha'
        );
        setRecaptchaSiteKey(settings.recaptchaSiteKey ?? null);
        setHCaptchaSiteKey(settings.hCaptchaSiteKey ?? null);
      })
      .finally(() => {
        if (active) setCaptchaLoading(false);
      });
    return () => {
      active = false;
    };
  }, []);

  const resetApplyCaptcha = () => {
    setApplyCaptchaToken(null);
    applyCaptchaRef.current?.reset();
  };

  const resetLoginCaptcha = () => {
    setLoginCaptchaToken(null);
    loginCaptchaRef.current?.reset();
  };

  const requestCode = async () => {
    if (captchaLoading) return;
    if (captchaEnabled && !applyCaptchaToken) {
      toast.error('Complete the CAPTCHA verification before continuing.');
      return;
    }
    setBusy(true);
    try {
      await service.requestChallenge({
        channel,
        contact,
        recaptchaToken: applyCaptchaToken ?? undefined,
      });
      setChallengeSent(true);
      toast.success('Verification code sent.');
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Could not send code.'
      );
    } finally {
      resetApplyCaptcha();
      setBusy(false);
    }
  };

  const verifyAndIssue = async () => {
    if (captchaLoading) return;
    setBusy(true);
    try {
      const result = await service.verifyAndIssue({
        channel,
        contact,
        otpCode,
        companyName,
        registrationCategory: category,
        retainedRegistrationId: retainedRegistrationId || undefined,
      });
      setIssued(result);
      const restrictedSessionToken =
        result.applicantSessionToken ?? result.paymentSessionToken;
      if (restrictedSessionToken) {
        service.setSessionToken(restrictedSessionToken);
        setApplicationToken('');
        toast.success(
          result.resumedExistingApplication
            ? 'Existing application recovered securely.'
            : 'Contact verified. Continue to payment.'
        );
      } else if (result.paymentOnly || result.resumedExistingApplication) {
        throw new Error('The restricted applicant session was not returned.');
      } else {
        setApplicationToken(result.applicationToken ?? '');
        toast.success('Application token issued.');
      }
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Verification could not be completed.'
      );
    } finally {
      setBusy(false);
    }
  };

  const retainedApplication = Boolean(retainedRegistrationId);

  const copyIssuedToken = async () => {
    if (!issued?.applicationToken) return;
    try {
      await navigator.clipboard.writeText(issued.applicationToken);
      toast.success('Application token copied.');
    } catch {
      toast.error('Could not copy the token. Select it and copy it manually.');
    }
  };

  const login = async () => {
    if (captchaLoading) return;
    if (captchaEnabled && !loginCaptchaToken) {
      toast.error('Complete the CAPTCHA verification before continuing.');
      return;
    }
    setBusy(true);
    try {
      const session = await service.startSession({
        applicationToken,
        recaptchaToken: loginCaptchaToken ?? undefined,
      });
      service.setSessionToken(session.sessionToken);
      router.push('/supplier-application/portal');
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'The application token was not accepted.'
      );
    } finally {
      resetLoginCaptcha();
      setBusy(false);
    }
  };

  return (
    <main className="light min-h-screen bg-slate-50 px-4 py-10 text-slate-950 [color-scheme:light]">
      <div className="mx-auto max-w-5xl">
        <div className="mb-8 flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
          <div>
            <Badge className="mb-3 bg-emerald-100 text-emerald-800 hover:bg-emerald-100">
              TDC supplier onboarding
            </Badge>
            <h1 className="text-3xl font-semibold tracking-tight">
              Secure supplier application access
            </h1>
            <p className="mt-2 max-w-2xl text-sm text-slate-600">
              Verify one email address or phone number, obtain an
              application-bound token, and use it to submit documents and track
              the review. No supplier account or supplier privileges are created
              before approval.
            </p>
          </div>
          <div className="flex items-center gap-2 text-xs text-slate-600">
            <ShieldCheck className="h-4 w-4 text-emerald-600" />
            Token closes only on approval or rejection
          </div>
        </div>

        <Tabs defaultValue="apply" className="grid gap-5">
          <TabsList className="w-full max-w-md border bg-white">
            <TabsTrigger
              value="apply"
              className="flex-1 text-slate-600 data-[state=active]:bg-white data-[state=active]:text-slate-950"
            >
              Apply for token
            </TabsTrigger>
            <TabsTrigger
              value="login"
              className="flex-1 text-slate-600 data-[state=active]:bg-white data-[state=active]:text-slate-950"
            >
              Token login
            </TabsTrigger>
          </TabsList>

          <TabsContent value="apply">
            <Card className="border-slate-200 bg-white text-slate-950 shadow-sm">
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <KeyRound className="h-5 w-5 text-emerald-600" />
                  Verify contact and start application
                </CardTitle>
                <CardDescription className="text-slate-600">
                  {retainedApplication
                    ? 'Verify the email address or phone number already recorded on this draft. Its original application data and audit ownership will be retained.'
                    : 'The effective DEC-007 configuration determines whether this token is free or paid.'}
                </CardDescription>
              </CardHeader>
              <CardContent className="grid gap-5">
                <div className="grid gap-4 md:grid-cols-2">
                  <div className="grid gap-2">
                    <Label
                      htmlFor="supplier-verification-channel"
                      className="text-slate-700"
                    >
                      Verification channel
                    </Label>
                    <select
                      id="supplier-verification-channel"
                      className="h-10 rounded-md border border-slate-300 bg-white px-3 text-slate-950 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20"
                      value={channel}
                      onChange={(event) =>
                        setChannel(
                          event.target.value as SupplierApplicantChannel
                        )
                      }
                    >
                      <option value="Email">Email</option>
                      <option value="Sms">SMS / phone</option>
                    </select>
                  </div>
                  <div className="grid gap-2">
                    <Label
                      htmlFor="supplier-contact"
                      className="text-slate-700"
                    >
                      {channel === 'Email' ? 'Email address' : 'Phone number'}
                    </Label>
                    <Input
                      id="supplier-contact"
                      className="border-slate-300 bg-white text-slate-950 caret-slate-950 placeholder:text-slate-400 focus-visible:border-blue-500 focus-visible:bg-white focus-visible:ring-blue-500/20"
                      value={contact}
                      onChange={(event) => setContact(event.target.value)}
                      placeholder={
                        channel === 'Email' ? 'name@company.com' : '+233...'
                      }
                    />
                  </div>
                  {!retainedApplication && (
                    <>
                      <div className="grid gap-2">
                        <Label
                          htmlFor="supplier-company-name"
                          className="text-slate-700"
                        >
                          Company name
                        </Label>
                        <Input
                          id="supplier-company-name"
                          className="border-slate-300 bg-white text-slate-950 caret-slate-950 placeholder:text-slate-400 focus-visible:border-blue-500 focus-visible:bg-white focus-visible:ring-blue-500/20"
                          value={companyName}
                          onChange={(event) =>
                            setCompanyName(event.target.value)
                          }
                        />
                      </div>
                      <div className="grid gap-2">
                        <Label
                          htmlFor="supplier-registration-category"
                          className="text-slate-700"
                        >
                          Registration category
                        </Label>
                        <select
                          id="supplier-registration-category"
                          className="h-10 rounded-md border border-slate-300 bg-white px-3 text-slate-950 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20"
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
                    </>
                  )}
                </div>

                {!challengeSent &&
                  (captchaLoading ? (
                    <p className="text-sm text-slate-600">
                      Loading the security challenge…
                    </p>
                  ) : (
                    <PublicCaptchaChallenge
                      ref={applyCaptchaRef}
                      id="supplier-apply-captcha"
                      enabled={captchaEnabled}
                      provider={captchaProvider}
                      theme="light"
                      recaptchaSiteKey={recaptchaSiteKey}
                      hCaptchaSiteKey={hCaptchaSiteKey}
                      onChange={setApplyCaptchaToken}
                    />
                  ))}

                {!challengeSent ? (
                  <Button
                    className="w-fit bg-emerald-600 text-white hover:bg-emerald-700"
                    disabled={
                      busy ||
                      captchaLoading ||
                      !contact ||
                      (!retainedApplication && !companyName) ||
                      (captchaEnabled && !applyCaptchaToken)
                    }
                    onClick={requestCode}
                  >
                    Send verification code
                  </Button>
                ) : (
                  <div className="grid gap-3 rounded-lg border border-slate-200 bg-slate-50 p-4">
                    <Label
                      htmlFor="supplier-verification-code"
                      className="text-slate-700"
                    >
                      Six-digit verification code
                    </Label>
                    <Input
                      id="supplier-verification-code"
                      className="max-w-xs border-slate-300 bg-white text-slate-950 caret-slate-950 tracking-[0.35em] focus-visible:border-blue-500 focus-visible:bg-white focus-visible:ring-blue-500/20"
                      value={otpCode}
                      maxLength={6}
                      onChange={(event) => setOtpCode(event.target.value)}
                    />
                    <Button
                      className="w-fit bg-emerald-600 text-white hover:bg-emerald-700"
                      disabled={busy || captchaLoading || otpCode.length !== 6}
                      onClick={verifyAndIssue}
                    >
                      {retainedApplication
                        ? 'Verify and secure existing application'
                        : 'Verify and continue'}
                    </Button>
                  </div>
                )}

                {issued && (
                  <Alert
                    className={
                      issued.paymentOnly
                        ? 'border-amber-300 bg-amber-50 text-amber-950'
                        : 'border-emerald-300 bg-emerald-50 text-emerald-950'
                    }
                  >
                    <AlertDescription className="grid gap-3">
                      <div>
                        Application <strong>{issued.registrationNumber}</strong>{' '}
                        {issued.resumedExistingApplication
                          ? 'was recovered.'
                          : 'was created.'}{' '}
                        {issued.paymentOnly
                          ? 'The application token is withheld until trusted payment verification.'
                          : issued.resumedExistingApplication
                            ? 'Continue the same application; no duplicate registration or token was created.'
                            : 'Copy the token now; plaintext is never stored.'}
                      </div>
                      {!issued.paymentOnly && issued.applicationToken && (
                        <div className="flex items-start gap-2 rounded border bg-white p-2">
                          <code className="min-w-0 flex-1 break-all px-1 py-1 text-sm text-slate-950">
                            {issued.applicationToken}
                          </code>
                          <Button
                            type="button"
                            variant="outline"
                            size="icon"
                            className="shrink-0 border-slate-300 bg-white text-slate-700 hover:bg-slate-100 hover:text-slate-950"
                            aria-label="Copy application token"
                            title="Copy application token"
                            onClick={copyIssuedToken}
                          >
                            <Copy aria-hidden="true" />
                          </Button>
                        </div>
                      )}
                      {(issued.paymentOnly ||
                        issued.resumedExistingApplication) &&
                        (issued.applicantSessionToken ||
                          issued.paymentSessionToken) && (
                          <Button
                            type="button"
                            className="w-fit bg-amber-600 text-white hover:bg-amber-700"
                            onClick={() =>
                              router.push('/supplier-application/portal')
                            }
                          >
                            {issued.paymentOnly
                              ? 'Continue to payment'
                              : 'Continue application'}
                          </Button>
                        )}
                      <div className="flex flex-wrap gap-2 text-xs">
                        <Badge variant="outline">
                          {String(issued.feeMode)}
                        </Badge>
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
            <Card className="border-slate-200 bg-white text-slate-950 shadow-sm">
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <LockKeyhole className="h-5 w-5 text-blue-600" />
                  Restricted applicant portal
                </CardTitle>
                <CardDescription className="text-slate-600">
                  This does not sign you into the approved supplier portal.
                </CardDescription>
              </CardHeader>
              <CardContent className="grid gap-4">
                <div className="grid gap-2">
                  <Label
                    htmlFor="supplier-application-token"
                    className="text-slate-700"
                  >
                    Application token
                  </Label>
                  <Input
                    id="supplier-application-token"
                    className="border-slate-300 bg-white text-slate-950 caret-slate-950 placeholder:text-slate-400 focus-visible:border-blue-500 focus-visible:bg-white focus-visible:ring-blue-500/20"
                    value={applicationToken}
                    onChange={(event) =>
                      setApplicationToken(event.target.value)
                    }
                    autoComplete="off"
                  />
                </div>
                {captchaLoading ? (
                  <p className="text-sm text-slate-600">
                    Loading the security challenge…
                  </p>
                ) : (
                  <PublicCaptchaChallenge
                    ref={loginCaptchaRef}
                    id="supplier-login-captcha"
                    enabled={captchaEnabled}
                    provider={captchaProvider}
                    theme="light"
                    recaptchaSiteKey={recaptchaSiteKey}
                    hCaptchaSiteKey={hCaptchaSiteKey}
                    onChange={setLoginCaptchaToken}
                  />
                )}
                <Button
                  className="w-fit bg-blue-600 text-white hover:bg-blue-700"
                  disabled={
                    busy ||
                    captchaLoading ||
                    !applicationToken ||
                    (captchaEnabled && !loginCaptchaToken)
                  }
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
