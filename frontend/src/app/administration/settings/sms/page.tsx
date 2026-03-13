'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Save } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { adminApiService, type SmsSettings } from '@/services/admin-api.service';

const defaults: SmsSettings = {
  defaultProvider: 'Twilio',
  fallbackProvidersCsv: '',

  twilioEnabled: false,
  twilioAccountSid: '',
  twilioAuthToken: '',
  twilioFromNumber: '',

  ghanaGatewayEnabled: false,
  ghanaGatewayUrlTemplate: '',
  ghanaGatewayApiKey: '',
  ghanaGatewaySenderId: '',
  ghanaGatewayTimeoutSeconds: 10,
};

export default function SmsSettingsPage() {
  const qc = useQueryClient();

  const { data, isLoading, error } = useQuery({
    queryKey: ['sms-settings'],
    queryFn: () => adminApiService.getSmsSettings(),
  });

  const [form, setForm] = useState<SmsSettings>(defaults);

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
        defaultProvider: (form.defaultProvider || 'Twilio').trim(),
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
    },
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">SMS Settings</h1>
        <p className="text-slate-600 mt-1">Configure per-tenant SMS providers (Twilio + Ghana gateway).</p>
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
                <option value="GhanaGateway">GhanaGateway</option>
              </select>
            </div>
            <div className="space-y-2">
              <Label>Fallback providers (CSV)</Label>
              <Input
                value={form.fallbackProvidersCsv}
                onChange={(e) => setForm((f) => ({ ...f, fallbackProvidersCsv: e.target.value }))}
                placeholder="e.g. GhanaGateway"
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
          <CardTitle>Ghana Gateway</CardTitle>
          <CardDescription>Configurable HTTP GET URL template.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center gap-3">
            <Switch checked={!!form.ghanaGatewayEnabled} onCheckedChange={(v) => setForm((f) => ({ ...f, ghanaGatewayEnabled: !!v }))} />
            <span className="text-sm text-slate-700">{form.ghanaGatewayEnabled ? 'Enabled' : 'Disabled'}</span>
          </div>

          <div className="grid grid-cols-1 gap-4">
            <div className="space-y-2">
              <Label>URL template</Label>
              <Input
                value={form.ghanaGatewayUrlTemplate}
                onChange={(e) => setForm((f) => ({ ...f, ghanaGatewayUrlTemplate: e.target.value }))}
                placeholder="https://gateway.example/send?to={to}&from={senderId}&msg={message}&key={apiKey}"
              />
              <div className="text-xs text-slate-500">Supported placeholders: {'{to}'}, {'{message}'}, {'{senderId}'}, {'{apiKey}'}</div>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div className="space-y-2">
              <Label>API key</Label>
              <Input
                type="password"
                value={form.ghanaGatewayApiKey}
                onChange={(e) => setForm((f) => ({ ...f, ghanaGatewayApiKey: e.target.value }))}
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
        </CardContent>
      </Card>

      <div className="flex items-center gap-2">
        <Button onClick={() => save.mutate()} disabled={save.isPending}>
          <Save className="h-4 w-4 mr-2" />
          {save.isPending ? 'Saving...' : 'Save'}
        </Button>
      </div>
    </div>
  );
}

