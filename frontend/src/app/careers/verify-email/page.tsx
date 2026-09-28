'use client';

import { Suspense, useEffect, useRef, useState } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { CheckCircle2, Loader2, MailWarning } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { authService } from '@/services/auth';
import { isCandidateUser } from '@/lib/auth-routing';
import { candidateService, publicCareersService } from '@/services/hr/careers.service';

/**
 * Lands the email-confirmation link from the candidate's inbox.
 *
 * ⚠ **Two links land here, and only one of them needs a login.** A link carrying `uid` is the
 * ACTIVATION link sent at registration: the account is inactive, cannot sign in, and confirming
 * the address is what activates it — so that path is deliberately anonymous. A link without
 * `uid` is the older profile-initiated confirmation, raised by an already-active candidate from
 * their own profile, and still goes through the signed-in door.
 *
 * Requiring a login for the first kind is what made careers registration a dead end: activation
 * depended on an SMS OTP, and where no SMS provider is configured the code never arrived.
 */
function VerifyEmailInner() {
  const params = useSearchParams();
  const token = params?.get('token') ?? '';
  const uid = params?.get('uid') ?? '';
  const [state, setState] = useState<'working' | 'done' | 'failed' | 'signin'>('working');
  const [message, setMessage] = useState('');
  const [resendEmail, setResendEmail] = useState('');
  const [resending, setResending] = useState(false);
  const ran = useRef(false);

  useEffect(() => {
    if (ran.current) return;
    ran.current = true;

    if (!token) {
      setState('failed');
      setMessage('The link is missing its confirmation token. Request a new one below.');
      return;
    }

    // ⚠ Round 4: this NO LONGER demands a signed-in candidate, and the reason is the whole
    // point. A self-registered account starts inactive and is activated by proving the email —
    // so requiring a login here required the very thing the link exists to unlock. Combined with
    // an SMS OTP that never arrives where no SMS provider is configured, a registered candidate
    // had no route to an active account at all.
    //
    // The anonymous endpoint takes the uid AND the token, both carried by the link. Where the
    // link has no uid it is one of the old profile-initiated confirmations, which is still a
    // signed-in act and still works through the original door.
    if (uid) {
      publicCareersService
        .activateAccount(uid, token)
        .then((r) => {
          setState('done');
          setMessage(r.message);
        })
        .catch((e: any) => {
          setState('failed');
          setMessage(e?.message ?? 'The activation link is invalid or has expired.');
        });
      return;
    }

    const user = authService.getStoredUser();
    if (!isCandidateUser(user)) {
      setState('signin');
      return;
    }
    candidateService
      .confirmEmail(token)
      .then((r) => {
        setState('done');
        setMessage(r.message);
      })
      .catch((e: any) => {
        setState('failed');
        setMessage(e?.message ?? 'The confirmation link is invalid or has expired.');
      });
  }, [token, uid]);

  const selfUrl = typeof window !== 'undefined' ? window.location.pathname + window.location.search : '/careers/verify-email';

  return (
    <div className="mx-auto max-w-md">
      <Card>
        <CardHeader>
          <CardTitle className="text-xl">Confirm your email</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {state === 'working' && (
            <div className="flex items-center gap-2 text-muted-foreground">
              <Loader2 className="h-5 w-5 animate-spin" />
              Confirming…
            </div>
          )}
          {state === 'signin' && (
            <>
              <p className="text-sm text-muted-foreground">
                Sign in to your candidate account first — the confirmation is tied to it.
              </p>
              <Button asChild>
                <Link href={`/login?redirect=${encodeURIComponent(selfUrl)}`}>Sign in and confirm</Link>
              </Button>
            </>
          )}
          {state === 'done' && (
            <>
              <p className="flex items-center gap-2 text-sm">
                <CheckCircle2 className="h-5 w-5 text-green-600" />
                {message}
              </p>
              {/* An activating visitor has no session yet, so "back to my profile" would bounce
                  them to the login screen anyway. Send them there deliberately instead. */}
              <Button asChild>
                <Link href={uid ? '/login' : '/external-portal/careers/profile'}>
                  {uid ? 'Sign in' : 'Back to my profile'}
                </Link>
              </Button>
            </>
          )}
          {state === 'failed' && (
            <>
              <p className="flex items-center gap-2 text-sm text-destructive">
                <MailWarning className="h-5 w-5" />
                {message}
              </p>
              {uid ? (
                <div className="space-y-2">
                  <p className="text-sm text-muted-foreground">
                    Activation links expire. Enter your email address and we will send a new one.
                  </p>
                  <div className="flex gap-2">
                    <Input
                      type="email"
                      placeholder="you@example.com"
                      value={resendEmail}
                      onChange={(e) => setResendEmail(e.target.value)}
                    />
                    <Button
                      variant="outline"
                      disabled={!resendEmail.trim() || resending}
                      onClick={() => {
                        setResending(true);
                        publicCareersService
                          .resendActivation(resendEmail.trim())
                          .then((r) => setMessage(r.message))
                          .catch((e: any) => setMessage(e?.message ?? 'Could not send a new link.'))
                          .finally(() => setResending(false));
                      }}
                    >
                      {resending ? 'Sending…' : 'Resend'}
                    </Button>
                  </div>
                </div>
              ) : (
                <Button asChild variant="outline">
                  <Link href="/external-portal/careers/profile">Back to my profile</Link>
                </Button>
              )}
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

export default function VerifyEmailPage() {
  return (
    <Suspense
      fallback={
        <div className="flex items-center justify-center py-24">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      }
    >
      <VerifyEmailInner />
    </Suspense>
  );
}
