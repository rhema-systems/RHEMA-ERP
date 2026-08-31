'use client';

import { Suspense, useState } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { CheckCircle2, Loader2, MailWarning } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useToast } from '@/hooks/use-toast';
import { completeClientSetup } from '@/services/hr/client-portal.service';

/**
 * The invite-completion page consultant-client invite emails link to
 * (`{PortalUrl}/client-portal/setup?token=…&email=…`). Consuming the emailed token here is the
 * mailbox proof: the server sets the chosen password, confirms the email and activates the
 * account in one step, after which the contact signs in on the ordinary login page.
 */
function ClientSetupInner() {
  const params = useSearchParams();
  const token = params?.get('token') ?? '';
  const email = params?.get('email') ?? '';
  const { toast } = useToast();

  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [done, setDone] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (password !== confirmPassword) {
      toast({ title: 'Passwords do not match', variant: 'destructive' });
      return;
    }
    setSubmitting(true);
    try {
      await completeClientSetup(email, token, password, confirmPassword);
      setDone(true);
    } catch (err: any) {
      toast({
        title: 'Could not complete setup',
        description: err?.message,
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  if (!token || !email) {
    return (
      <Card className="w-full max-w-md">
        <CardHeader>
          <div className="flex items-center gap-2">
            <MailWarning className="h-5 w-5 text-muted-foreground" />
            <CardTitle>This link is not valid</CardTitle>
          </div>
          <CardDescription>
            The setup link is incomplete. Please open the link from your invitation email, or ask
            your consultant firm contact to resend the invite.
          </CardDescription>
        </CardHeader>
      </Card>
    );
  }

  if (done) {
    return (
      <Card className="w-full max-w-md">
        <CardHeader>
          <div className="flex items-center gap-2">
            <CheckCircle2 className="h-5 w-5 text-green-600" />
            <CardTitle>Account ready</CardTitle>
          </div>
          <CardDescription>
            Your client portal account is set up. Sign in with your email address and the password
            you just chose.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Button asChild className="w-full">
            <Link href="/login">Go to sign in</Link>
          </Button>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="w-full max-w-md">
      <CardHeader>
        <CardTitle>Complete your account setup</CardTitle>
        <CardDescription>
          Choose a password for <span className="font-medium">{email}</span> to activate your
          client portal access.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={submit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="setup-password">New password</Label>
            <Input
              id="setup-password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              minLength={8}
              maxLength={100}
              required
              autoComplete="new-password"
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="setup-confirm">Confirm password</Label>
            <Input
              id="setup-confirm"
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              minLength={8}
              maxLength={100}
              required
              autoComplete="new-password"
            />
          </div>
          <Button type="submit" className="w-full" disabled={submitting}>
            {submitting ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
            Activate account
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

export default function ClientSetupPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-50 px-4">
      <Suspense
        fallback={<Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />}
      >
        <ClientSetupInner />
      </Suspense>
    </div>
  );
}
