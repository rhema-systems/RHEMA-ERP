'use client';

import { useState } from 'react';
import { useAuth } from '@/hooks/use-auth';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { adminApiService, type User } from '@/services/admin-api.service';
import { licenseTypeService, type LicenseTypeDto } from '@/services/partnerConfigService';
import { businessPartnerService } from '@/services/businessPartnerService';
import { linkExistingExternalUser } from '@/services/businessPartnerUserService';
import { toast } from 'sonner';

export function BusinessPartnerAccessSetup({ partnerId, onSaved }: { partnerId: string; onSaved: () => Promise<void> }) {
  const { hasRole } = useAuth();
  const [mode, setMode] = useState<'account' | 'licence' | null>(null);
  const [users, setUsers] = useState<User[]>([]);
  const [types, setTypes] = useState<LicenseTypeDto[]>([]);
  const [selected, setSelected] = useState('');
  const [number, setNumber] = useState('');
  const [authority, setAuthority] = useState('');
  const [issued, setIssued] = useState('');
  const [expires, setExpires] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  if (!hasRole('SuperAdmin') && !hasRole('TenantAdmin')) return null;

  const open = async (next: 'account' | 'licence') => {
    setMode(next); setSelected(''); setError(''); setBusy(true);
    try {
      if (next === 'account') setUsers((await adminApiService.getUsers()).filter(u =>
        u.isActive && u.roles.length === 1 && u.roles[0].toLowerCase() === 'externaluser'));
      else setTypes(await licenseTypeService.getActive());
    } catch (e) { setError(e instanceof Error ? e.message : 'Unable to load setup options.'); }
    finally { setBusy(false); }
  };
  const save = async () => {
    setBusy(true); setError('');
    try {
      if (mode === 'account') await linkExistingExternalUser(partnerId, selected);
      else await businessPartnerService.addPartnerLicense(partnerId, {
        licenseTypeId: selected, licenseNumber: number.trim(), issuingAuthority: authority.trim(),
        issueDate: issued, ...(expires ? { expiryDate: expires } : {}),
      });
      toast.success(mode === 'account' ? 'Portal account linked to this partner.' : 'Licence recorded.');
      setMode(null); setNumber(''); setAuthority(''); setIssued(''); setExpires('');
      await onSaved();
    } catch (e) { setError(e instanceof Error ? e.message : 'The request could not be completed.'); }
    finally { setBusy(false); }
  };
  return <>
    <Button variant="outline" onClick={() => void open('account')}>Link portal account</Button>
    <Button variant="outline" onClick={() => void open('licence')}>Record licence</Button>
    <Dialog open={mode !== null} onOpenChange={value => { if (!value && !busy) setMode(null); }}>
      <DialogContent className="max-h-[85vh] overflow-y-auto">
        <DialogHeader><DialogTitle>{mode === 'account' ? 'Link existing portal account' : 'Record partner licence'}</DialogTitle>
          <DialogDescription>{mode === 'account'
            ? 'Select an active ExternalUser. This grants access to this business partner only, without changing its password. Accounts linked to another partner cannot be selected for reassignment.'
            : 'Record the licence supplied by the partner. Partner approval and procurement eligibility checks still apply.'}</DialogDescription></DialogHeader>
        {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
        <Label>{mode === 'account' ? 'Portal account' : 'Licence type'}</Label>
        <Select value={selected} onValueChange={setSelected} disabled={busy}>
          <SelectTrigger aria-label={mode === 'account' ? 'Portal account' : 'Licence type'}><SelectValue placeholder="Select an option" /></SelectTrigger>
          <SelectContent>{mode === 'account' ? users.map(u => <SelectItem key={u.id} value={u.id}>{u.username} · {u.email}</SelectItem>)
            : types.map(t => <SelectItem key={t.id} value={t.id}>{t.licenseName}</SelectItem>)}</SelectContent>
        </Select>
        {mode === 'licence' && <>
          <Label htmlFor="partner-licence-number">Licence number</Label><Input id="partner-licence-number" value={number} onChange={e => setNumber(e.target.value)} disabled={busy} maxLength={100} />
          <Label htmlFor="partner-licence-authority">Issuing authority</Label><Input id="partner-licence-authority" value={authority} onChange={e => setAuthority(e.target.value)} disabled={busy} maxLength={200} />
          <Label htmlFor="partner-licence-issued">Issue date</Label><Input id="partner-licence-issued" type="date" value={issued} onChange={e => setIssued(e.target.value)} disabled={busy} />
          <Label htmlFor="partner-licence-expires">Expiry date (optional)</Label><Input id="partner-licence-expires" type="date" value={expires} onChange={e => setExpires(e.target.value)} disabled={busy} />
        </>}
        <DialogFooter><Button variant="outline" disabled={busy} onClick={() => setMode(null)}>Cancel</Button>
          <Button onClick={() => void save()} disabled={busy || !selected || (mode === 'licence' && (!number.trim() || !authority.trim() || !issued))}>{busy ? 'Saving…' : mode === 'account' ? 'Link account' : 'Save licence'}</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  </>;
}
