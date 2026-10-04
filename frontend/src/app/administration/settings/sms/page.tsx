'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Save, Send, WalletCards } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import PhoneInput from '@/components/ui/phone-input';
import { Switch } from '@/components/ui/switch';
import { useToast } from '@/hooks/use-toast';
import {
  adminApiService,
  getAdminProblemMessage,
  type SmsBalance,
  type SmsSettings,
} from '@/services/admin-api.service';

const defaults: SmsSettings = {
  isConfigured: false,
  defaultProvider: 'GhanaGateway',
  fallbackProvidersCsv: '',

  twilioEnabled: false,
  twilioAccountSid: '',
  twilioAuthToken: '',
  twilioAuthTokenConfigured: false,
  twilioFromNumber: '',

  ghanaGatewayEnabled: false,
  ghanaGatewayUrlTemplate: 'https://api.mnotify.com/api/sms/quick',
  ghanaGatewayApiKey: '',
  ghanaGatewayApiKeyConfigured: false,
  ghanaGatewaySenderId: '',
  ghanaGatewayTimeoutSeconds: 10,
};

export default function SmsSettingsPage() {
  const qc = useQueryClient();
  const { toast } = useToast();

  const { data, isLoading, error } = useQuery({
    queryKey: ['sms-settings'],
    queryFn: () => adminApiService.getSmsSettings(),
  });

  const [form, setForm] = useState<SmsSettings>(defaults);
  const [balance, setBalance] = useState<SmsBalance | null>(null);
  const [testDialogOpen, setTestDialogOpen] = useState(false);
  const [testPhoneNumber, setTestPhoneNumber] = useState('');
  const [testAsOtp, setTestAsOtp] = useState(false);

  useEffect(() => {
    if (!data) return;
    setForm({
      ...defaults,
      ...data,
      ghanaGatewayTimeoutSeconds: data.ghanaGatewayTimeoutSeconds || 10,
    });
  }, [data]);

  const save = useMutation({
    mutationFn: async () => {
      const payload: SmsSettings = {
        ...form,
        defaultProvider: (form.defaultProvider || 'GhanaGateway').trim(),
        fallbackProvidersCsv: (form.fallbackProvidersCsv || '').trim(),
        twilioAccountSid: (form.twilioAccountSid || '').trim(),
        twilioAuthToken: (form.twilioAuthToken || '').trim(),
        twilioFromNumber: (form.twilioFromNumber || '').trim(),
        ghanaGatewayUrlTemplate: (form.ghanaGatewayUrlTemplate || '').trim(),
        ghanaGatewayApiKey: (form.ghanaGatewayApiKey || '').trim(),
        ghanaGatewaySenderId: (form.ghanaGatewaySenderId || '').trim(),
        ghanaGatewayTimeoutSeconds: Number(form.ghanaGatewayTimeoutSeconds) || 10,
      };
      await adminApiService.saveSmsSettings(payload);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['sms-settings'] });
      toast({ title: 'SMS settings saved', description: 'The tenant SMS configuration was updated.', variant: 'success' });
    },
    onError: (saveError) => {
      toast({
        title: 'SMS settings could not be saved',
        description: saveError instanceof Error ? saveError.message : 'Review the provider settings and try again.',
        variant: 'destructive',
      });
    },
  });

  const checkBalance = useMutation({
    mutationFn: () => adminApiService.getSmsBalance(),
    onSuccess: (result) => {
      setBalance(result);
      toast({
        title: 'mNotify balance checked',
        description: `SMS balance: ${result.balance.toLocaleString()} · Bonus: ${result.bonus.toLocaleString()}`,
        variant: 'success',
      });
    },
    onError: (balanceError) => {
      toast({
        title: 'SMS balance could not be checked',
        description: getAdminProblemMessage(balanceError, 'Check the saved mNotify configuration and try again.'),
        variant: 'destructive',
      });
    },
  });

  const sendTest = useMutation({
    mutationFn: ({ phoneNumber, isOtp }: { phoneNumber: string; isOtp: boolean }) =>
      adminApiService.sendTestSms(phoneNumber, isOtp),
    onSuccess: (result) => {
      toast({
        title: testAsOtp ? 'Test verification SMS sent' : 'Test SMS sent',
        description: result.message || (testAsOtp
          ? 'The sample verification message was accepted by the configured SMS provider.'
          : 'The test message was accepted by the configured SMS provider.'),
        variant: 'success',
      });
      setTestPhoneNumber('');
    },
    onError: (testError) => {
      toast({
        title: 'Test SMS could not be sent',
        description: getAdminProblemMessage(testError, 'Check the recipient and saved provider settings, then try again.'),
        variant: 'destructive',
      });
    },
  });

  const testPhoneIsValid = /^\+[1-9]\d{7,14}$/.test(testPhoneNumber);
  const mNotifyActionsEnabled =
    form.isConfigured &&
    form.ghanaGatewayEnabled &&
    form.ghanaGatewayApiKeyConfigured;

  const confirmTestSms = async () => {
    if (!testPhoneIsValid) {
      toast({
        title: 'Enter a valid recipient',
        description: 'Select the country code and enter a valid phone number.',
        variant: 'destructive',
      });
      return false;
    }

    try {
      await sendTest.mutateAsync({ phoneNumber: testPhoneNumber, isOtp: testAsOtp });
      return true;
    } catch {
      return false;
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">SMS Settings</h1>
        <p className="text-slate-600 mt-1">Configure per-tenant SMS providers (Twilio and mNotify).</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Provider Selection</CardTitle>
          <CardDescription>Default provider will be tried first, then fallbacks in order.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {isLoading ? <div className="text-slate-600">Loading...</div> : null}
          {error ? <div className="text-red-600">Failed to load SMS settings.</div> : null}

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Default provider</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={form.defaultProvider}
                onChange={(e) => setForm((f) => ({ ...f, defaultProvider: e.target.value }))}
              >
                <option value="Twilio">Twilio</option>
                <option value="GhanaGateway">mNotify</option>
              </select>
            </div>
            <div className="space-y-2">
              <Label>Fallback providers (CSV)</Label>
              <Input
                value={form.fallbackProvidersCsv}
                onChange={(e) => setForm((f) => ({ ...f, fallbackProvidersCsv: e.target.value }))}
                placeholder="e.g. mNotify"
              />
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Twilio</CardTitle>
          <CardDescription>Uses Twilio REST API.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center gap-3">
            <Switch checked={!!form.twilioEnabled} onCheckedChange={(v) => setForm((f) => ({ ...f, twilioEnabled: !!v }))} />
            <span className="text-sm text-slate-700">{form.twilioEnabled ? 'Enabled' : 'Disabled'}</span>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div className="space-y-2">
              <Label>Account SID</Label>
              <Input value={form.twilioAccountSid} onChange={(e) => setForm((f) => ({ ...f, twilioAccountSid: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Auth token</Label>
              <Input
                type="password"
                value={form.twilioAuthToken}
                onChange={(e) => setForm((f) => ({ ...f, twilioAuthToken: e.target.value }))}
                placeholder={form.twilioAuthTokenConfigured ? 'Configured — enter a new token to replace it' : ''}
              />
            </div>
            <div className="space-y-2">
              <Label>From number (E.164)</Label>
              <Input value={form.twilioFromNumber} onChange={(e) => setForm((f) => ({ ...f, twilioFromNumber: e.target.value }))} placeholder="+1XXXXXXXXXX" />
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>mNotify</CardTitle>
          <CardDescription>Sends JSON requests through the mNotify Quick SMS API. Verification messages use regular SMS credits.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center gap-3">
            <Switch checked={!!form.ghanaGatewayEnabled} onCheckedChange={(v) => setForm((f) => ({ ...f, ghanaGatewayEnabled: !!v }))} />
            <span className="text-sm text-slate-700">{form.ghanaGatewayEnabled ? 'Enabled' : 'Disabled'}</span>
          </div>

          <div className="grid grid-cols-1 gap-4">
            <div className="space-y-2">
              <Label>Quick SMS endpoint</Label>
              <Input
                value={form.ghanaGatewayUrlTemplate}
                onChange={(e) => setForm((f) => ({ ...f, ghanaGatewayUrlTemplate: e.target.value }))}
                placeholder="https://api.mnotify.com/api/sms/quick"
              />
              <div className="text-xs text-slate-500">The API key is added securely as the required key query parameter.</div>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div className="space-y-2">
              <Label>API key</Label>
              <Input
                type="password"
                value={form.ghanaGatewayApiKey}
                onChange={(e) => setForm((f) => ({ ...f, ghanaGatewayApiKey: e.target.value }))}
                placeholder={form.ghanaGatewayApiKeyConfigured ? 'Configured — enter a new key to replace it' : ''}
              />
            </div>
            <div className="space-y-2">
              <Label>Sender ID</Label>
              <Input value={form.ghanaGatewaySenderId} onChange={(e) => setForm((f) => ({ ...f, ghanaGatewaySenderId: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Timeout (seconds)</Label>
              <Input
                type="number"
                min={1}
                max={60}
                value={String(form.ghanaGatewayTimeoutSeconds)}
                onChange={(e) => setForm((f) => ({ ...f, ghanaGatewayTimeoutSeconds: Number(e.target.value) }))}
              />
            </div>
          </div>

          {balance ? (
            <div className="rounded-md border bg-slate-50 px-4 py-3 text-sm text-slate-700" role="status">
              <span className="font-medium">Current SMS balance:</span>{' '}
              {balance.balance.toLocaleString()}
              <span className="mx-2 text-slate-400">·</span>
              <span className="font-medium">Bonus:</span> {balance.bonus.toLocaleString()}
            </div>
          ) : null}
        </CardContent>
      </Card>

      <div className="flex flex-wrap items-center gap-2">
        <Button onClick={() => save.mutate()} disabled={save.isPending}>
          <Save className="h-4 w-4 mr-2" />
          {save.isPending ? 'Saving...' : 'Save'}
        </Button>
        <Button
          type="button"
          variant="outline"
          onClick={() => checkBalance.mutate()}
          disabled={!mNotifyActionsEnabled || checkBalance.isPending}
        >
          <WalletCards className="h-4 w-4 mr-2" />
          {checkBalance.isPending ? 'Checking...' : 'Check balance'}
        </Button>
        <Button
          type="button"
          variant="outline"
          onClick={() => setTestDialogOpen(true)}
          disabled={!form.isConfigured || sendTest.isPending}
        >
          <Send className="h-4 w-4 mr-2" />
          Send test SMS
        </Button>
      </div>

      <ConfirmationDialog
        open={testDialogOpen}
        onOpenChange={setTestDialogOpen}
        title={testAsOtp ? 'Send test verification SMS' : 'Send test SMS'}
        description={testAsOtp
          ? 'Send a sample verification message through the same regular SMS delivery path as property enquiries. The sample code cannot verify a contact. SMS credits may be charged.'
          : "Send a regular message through the tenant's saved SMS configuration. SMS credits may be charged."}
        confirmText={testAsOtp ? 'Send verification test' : 'Send test SMS'}
        onConfirm={confirmTestSms}
        isLoading={sendTest.isPending}
        confirmDisabled={!testPhoneIsValid}
      >
        <div className="space-y-4">
          <Label htmlFor="sms-test-recipient">Recipient phone number</Label>
          <PhoneInput
            id="sms-test-recipient"
            value={testPhoneNumber}
            onChange={setTestPhoneNumber}
            error={testPhoneNumber.length > 0 && !testPhoneIsValid}
            placeholder="Enter recipient number"
          />
          {testPhoneNumber.length > 0 && !testPhoneIsValid ? (
            <p className="text-xs text-red-600">Enter a valid phone number including its country code.</p>
          ) : (
            <p className="text-xs text-slate-500">For Ghana, enter the national number without the leading zero.</p>
          )}
          <div className="flex items-start justify-between gap-4 rounded-md border border-slate-200 p-3">
            <div className="space-y-1">
              <Label htmlFor="sms-test-as-otp">Send a sample verification message</Label>
              <p className="text-xs text-slate-500">
                Uses the same regular SMS delivery path as public property enquiries. The test code is not valid.
              </p>
            </div>
            <Switch
              id="sms-test-as-otp"
              checked={testAsOtp}
              onCheckedChange={setTestAsOtp}
              aria-label="Send a sample verification message"
            />
          </div>
        </div>
      </ConfirmationDialog>
    </div>
  );
}

