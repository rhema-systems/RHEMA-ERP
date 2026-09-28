'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Checkbox } from '@/components/ui/checkbox';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useAuth } from '@/hooks/use-auth';
import type { PaymentTermListDto } from '@/services/financeCommonService';
import { businessPartnerService, type BusinessPartnerPostingOptions, type BusinessPartnerWithholdingTaxOption } from '@/services/businessPartnerService';
import {
  businessPartnerFinanceProfileService,
  type BusinessPartnerApProfile,
  type BusinessPartnerArProfile,
  type BusinessPartnerFinanceProfileSet,
} from '@/services/businessPartnerFinanceProfileService';

interface Props {
  businessPartnerId: string;
  paymentTerms: PaymentTermListDto[];
  withholdingTaxes: BusinessPartnerWithholdingTaxOption[];
}

interface WhtLineForm {
  key: string;
  categoryCode: string;
  categoryName: string;
  withholdingTaxId: string;
  isDefaultForAp: boolean;
  isActive: boolean;
}

const dateOnly = (value?: string) => (value ? value.slice(0, 10) : '');
const newLine = (): WhtLineForm => ({
  key: crypto.randomUUID(), categoryCode: '', categoryName: '', withholdingTaxId: '', isDefaultForAp: false, isActive: true,
});

/**
 * Finance-owned editor embedded in the Procurement Business Partner screen. Procurement owns the
 * identity and role rows; this panel owns only governed AP/AR profile versions. Keeping the panel
 * separate makes that ownership boundary visible to future module maintainers.
 */
export function BusinessPartnerFinanceProfilesPanel({ businessPartnerId, paymentTerms, withholdingTaxes }: Props) {
  const { user, hasPermission } = useAuth();
  const [data, setData] = useState<BusinessPartnerFinanceProfileSet | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [apRoleId, setApRoleId] = useState('');
  const [arRoleId, setArRoleId] = useState('');
  const [apVersionId, setApVersionId] = useState('');
  const [arVersionId, setArVersionId] = useState('');
  const loadSequence = useRef(0);
  const loadedPartnerId = useRef('');
  const [postingOptions, setPostingOptions] = useState<BusinessPartnerPostingOptions | null>(null);
  const [optionsError, setOptionsError] = useState(false);
  const [apProfile, setApProfile] = useState<BusinessPartnerApProfile | null>(null);
  const [arProfile, setArProfile] = useState<BusinessPartnerArProfile | null>(null);
  const [apFrom, setApFrom] = useState('');
  const [apTo, setApTo] = useState('');
  const [apReference, setApReference] = useState('');
  const [apPaymentTermId, setApPaymentTermId] = useState('');
  const [apExpenseAccountId, setApExpenseAccountId] = useState('');
  const [apTaxGroupId, setApTaxGroupId] = useState('');
  const [subjectToWithholding, setSubjectToWithholding] = useState(false);
  const [whtLines, setWhtLines] = useState<WhtLineForm[]>([]);
  const [arFrom, setArFrom] = useState('');
  const [arTo, setArTo] = useState('');
  const [arReference, setArReference] = useState('');
  const [arPaymentTermId, setArPaymentTermId] = useState('');
  const [creditLimit, setCreditLimit] = useState('');
  const [isWithholdingAgent, setIsWithholdingAgent] = useState(false);
  const [decisionReason, setDecisionReason] = useState('');

  const load = useCallback(async () => {
    const sequence = ++loadSequence.current;
    setLoading(true);
    try {
      const profiles = await businessPartnerFinanceProfileService.get(businessPartnerId);
      if (sequence !== loadSequence.current) return;
      const samePartner = loadedPartnerId.current === businessPartnerId;
      loadedPartnerId.current = businessPartnerId;
      setData(profiles);
      const payable = profiles.roles.filter((role) => role.roleType === 'Supplier' || role.roleType === 'Contractor');
      const receivable = profiles.roles.filter((role) => role.roleType === 'Customer');
      setApRoleId((current) => samePartner && payable.some(role => role.id === current) ? current : (payable.find(role => role.status === 'Active') || payable[0])?.id || '');
      setArRoleId((current) => samePartner && receivable.some(role => role.id === current) ? current : (receivable.find(role => role.status === 'Active') || receivable[0])?.id || '');
      setApVersionId('');
      setArVersionId('');
    } catch (error) {
      if (sequence !== loadSequence.current) return;
      setData(null);
      toast.error(error instanceof Error ? error.message : 'Could not load Finance profiles.');
    } finally {
      if (sequence === loadSequence.current) setLoading(false);
    }
  }, [businessPartnerId]);

  useEffect(() => { void load(); return () => { loadSequence.current += 1; }; }, [load]);

  useEffect(() => {
    let current = true;
    setPostingOptions(null);
    setOptionsError(false);
    void businessPartnerService.getPostingOptions('Supplier').then(options => {
      if (current) setPostingOptions(options);
    }).catch(() => { if (current) setOptionsError(true); });
    return () => { current = false; };
  }, [businessPartnerId]);

  const apRoles = useMemo(() => data?.roles.filter((role) => role.roleType === 'Supplier' || role.roleType === 'Contractor') ?? [], [data]);
  const arRoles = useMemo(() => data?.roles.filter((role) => role.roleType === 'Customer') ?? [], [data]);

  useEffect(() => {
    const profiles = apRoles.find((role) => role.id === apRoleId)?.apProfiles ?? [];
    const latest = profiles.find(profile => profile.id === apVersionId) ?? [...profiles].sort((a, b) => b.versionNumber - a.versionNumber)[0] ?? null;
    setApProfile(latest);
    setApFrom(dateOnly(latest?.effectiveFrom));
    setApTo(dateOnly(latest?.effectiveTo));
    setApReference(latest?.apReferenceNumber || '');
    setApPaymentTermId(latest?.paymentTermId || '');
    setApExpenseAccountId(latest?.defaultExpenseAccountId || '');
    setApTaxGroupId(latest?.defaultTaxGroupId || '');
    setSubjectToWithholding(latest?.subjectToWithholding ?? false);
    setWhtLines(latest?.withholdingDefaults.map((line) => ({
      key: line.id, categoryCode: line.categoryCode, categoryName: line.categoryName || '',
      withholdingTaxId: line.withholdingTaxId, isDefaultForAp: line.isDefaultForAp, isActive: line.isActive,
    })) ?? []);
  }, [apRoleId, apRoles, apVersionId]);

  useEffect(() => {
    const profiles = arRoles.find((role) => role.id === arRoleId)?.arProfiles ?? [];
    const latest = profiles.find(profile => profile.id === arVersionId) ?? [...profiles].sort((a, b) => b.versionNumber - a.versionNumber)[0] ?? null;
    setArProfile(latest);
    setArFrom(dateOnly(latest?.effectiveFrom));
    setArTo(dateOnly(latest?.effectiveTo));
    setArReference(latest?.arReferenceNumber || '');
    setArPaymentTermId(latest?.paymentTermId || '');
    setCreditLimit(latest?.creditLimit == null ? '' : String(latest.creditLimit));
    setIsWithholdingAgent(latest?.isWithholdingAgent ?? false);
  }, [arRoleId, arRoles, arVersionId]);

  const run = async (operation: () => Promise<unknown>, success: string) => {
    setBusy(true);
    try { await operation(); toast.success(success); setDecisionReason(''); await load(); }
    catch (error) { toast.error(error instanceof Error ? error.message : 'Finance profile action failed.'); }
    finally { setBusy(false); }
  };

  const saveAp = () => {
    if (!apRoleId || !apFrom) { toast.error('Select an AP role and effective-from date.'); return; }
    if (subjectToWithholding && whtLines.filter((line) => line.isActive && line.isDefaultForAp).length !== 1) {
      toast.error('Select exactly one default AP WHT configuration.'); return;
    }
    if (whtLines.some((line) => !line.categoryCode.trim() || !line.withholdingTaxId)) {
      toast.error('Complete the category and WHT configuration on every WHT row.'); return;
    }
    return run(() => businessPartnerFinanceProfileService.saveAp(
      businessPartnerId, apProfile?.status === 'Draft' ? apProfile.id : null,
      {
        businessPartnerRoleId: apRoleId, effectiveFrom: apFrom, effectiveTo: apTo || null,
        apReferenceNumber: apReference || null, paymentTermId: apPaymentTermId || null,
        defaultExpenseAccountId: apExpenseAccountId || null, defaultTaxGroupId: apTaxGroupId || null,
        subjectToWithholding,
        withholdingDefaults: whtLines.map((line) => ({
          categoryCode: line.categoryCode, categoryName: line.categoryName || undefined,
          withholdingTaxId: line.withholdingTaxId, isDefaultForAp: line.isDefaultForAp, isActive: line.isActive,
        })),
      }), 'AP profile draft saved.');
  };

  const saveAr = () => {
    if (!arRoleId || !arFrom) { toast.error('Select the Customer role and effective-from date.'); return; }
    const parsedLimit = creditLimit === '' ? null : Number(creditLimit);
    if (parsedLimit != null && (!Number.isFinite(parsedLimit) || parsedLimit < 0)) {
      toast.error('AR credit limit must be zero or greater.'); return;
    }
    return run(() => businessPartnerFinanceProfileService.saveAr(
      businessPartnerId, arProfile?.status === 'Draft' ? arProfile.id : null,
      {
        businessPartnerRoleId: arRoleId, effectiveFrom: arFrom, effectiveTo: arTo || null,
        arReferenceNumber: arReference || null, paymentTermId: arPaymentTermId || null,
        creditLimit: parsedLimit, isWithholdingAgent,
      }), 'AR profile draft saved.');
  };

  const decision = (ledger: 'ap' | 'ar', profile: BusinessPartnerApProfile | BusinessPartnerArProfile, action: 'submit' | 'approve' | 'reject') =>
    run(() => businessPartnerFinanceProfileService.decide(businessPartnerId, ledger, profile.id, action, decisionReason),
      `${ledger.toUpperCase()} profile ${action === 'submit' ? 'submitted' : action === 'approve' ? 'approved' : 'rejected'}.`);

  const apRole = apRoles.find(role => role.id === apRoleId);
  const arRole = arRoles.find(role => role.id === arRoleId);
  const apReadOnly = busy || apRole?.status !== 'Active' || (!!apProfile && apProfile.status !== 'Draft');
  const arReadOnly = busy || arRole?.status !== 'Active' || (!!arProfile && arProfile.status !== 'Draft');
  const apDirty = apProfile?.status === 'Draft' && (
    apFrom !== dateOnly(apProfile.effectiveFrom) || apTo !== dateOnly(apProfile.effectiveTo) ||
    apReference !== (apProfile.apReferenceNumber || '') || apPaymentTermId !== (apProfile.paymentTermId || '') ||
    apExpenseAccountId !== (apProfile.defaultExpenseAccountId || '') || apTaxGroupId !== (apProfile.defaultTaxGroupId || '') ||
    subjectToWithholding !== apProfile.subjectToWithholding || whtLines.length !== apProfile.withholdingDefaults.length ||
    whtLines.some((line, index) => {
      const saved = apProfile.withholdingDefaults[index];
      return !saved || line.categoryCode !== saved.categoryCode || line.categoryName !== (saved.categoryName || '') ||
        line.withholdingTaxId !== saved.withholdingTaxId || line.isDefaultForAp !== saved.isDefaultForAp || line.isActive !== saved.isActive;
    })
  );
  const arDirty = arProfile?.status === 'Draft' && (
    arFrom !== dateOnly(arProfile.effectiveFrom) || arTo !== dateOnly(arProfile.effectiveTo) ||
    arReference !== (arProfile.arReferenceNumber || '') || arPaymentTermId !== (arProfile.paymentTermId || '') ||
    (creditLimit === '' ? null : Number(creditLimit)) !== (arProfile.creditLimit ?? null) || isWithholdingAgent !== arProfile.isWithholdingAgent
  );
  const expenseOptions = postingOptions?.accounts.filter(account => account.status === 'Active' && account.allowDirectPosting && !account.isControlAccount && (account.accountType === 'Asset' || account.accountType === 'Expense')).map(account => ({ id: account.id, label: `${account.accountCode || account.accountNumber} — ${account.accountName}` })) ?? [];
  const taxOptions = postingOptions?.taxGroups.filter(group => group.isActive && group.applicability !== 'Sales').map(group => ({ id: group.id, label: `${group.code} — ${group.name}` })) ?? [];

  if (loading) return <p className="py-8 text-center text-muted-foreground">Loading governed Finance profiles...</p>;
  if (!data) return <p className="py-8 text-center text-destructive">Finance profiles could not be loaded.</p>;

  return (
    <div className="space-y-5 py-3" onKeyDown={event => {
      // The panel is embedded in the identity form; Enter must not save that unrelated form.
      if (event.key === 'Enter' && event.target instanceof HTMLInputElement) event.preventDefault();
    }}>
      <div className="rounded-md border border-blue-200 bg-blue-50 p-3 text-sm text-blue-900">
        Procurement owns this partner&apos;s identity and roles. Finance owns the effective-dated AP/AR profiles below.
        AP and AR control accounts come from Finance settings. The Accounts tabs show their sources.
      </div>

      {apRoles.length > 0 && (
        <Card role="region" aria-label="Accounts Payable profile">
          <CardHeader><CardTitle>Accounts Payable profile</CardTitle><CardDescription>Supplier and Contractor defaults, category WHT and maker-checker approval.</CardDescription></CardHeader>
          <CardContent className="space-y-4">
            <ProfileVersionSelect ledger="AP" profiles={apRole?.apProfiles ?? []} value={apProfile?.id} onChange={setApVersionId} disabled={busy} />
            {apRole?.status === 'Inactive' && <p className="text-sm text-muted-foreground">This role is inactive. Its profile history is read-only.</p>}
            <div className="grid gap-4 md:grid-cols-3">
              <Field label="AP role"><Select value={apRoleId} disabled={busy} onValueChange={value => { if (!value) return; setApRoleId(value); setApVersionId(''); }}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{apRoles.map((role) => <SelectItem key={role.id} value={role.id}>{role.roleType}</SelectItem>)}</SelectContent></Select></Field>
              <Field label="Effective from"><Input type="date" value={apFrom} onChange={(e) => setApFrom(e.target.value)} disabled={apReadOnly} /></Field>
              <Field label="Effective through (optional)"><Input type="date" value={apTo} onChange={(e) => setApTo(e.target.value)} disabled={apReadOnly} /></Field>
              <Field label="AP reference"><Input value={apReference} onChange={(e) => setApReference(e.target.value)} disabled={apReadOnly} /></Field>
              <Field label="Payment terms"><PaymentTermSelect value={apPaymentTermId} onChange={setApPaymentTermId} terms={paymentTerms} disabled={apReadOnly} /></Field>
              <Field label="Default expense / cost account"><ConfigurationSelect label="Default expense / cost account" value={apExpenseAccountId} onChange={setApExpenseAccountId} options={expenseOptions} disabled={apReadOnly || !postingOptions} /></Field>
              <Field label="Default tax group"><ConfigurationSelect label="Default tax group" value={apTaxGroupId} onChange={setApTaxGroupId} options={taxOptions} disabled={apReadOnly || !postingOptions} /></Field>
              <div className="flex items-end gap-3 pb-2"><Switch id="ap-wht" checked={subjectToWithholding} onCheckedChange={setSubjectToWithholding} disabled={apReadOnly} /><Label htmlFor="ap-wht">Subject to withholding</Label></div>
            </div>
            {optionsError && <p role="alert" className="text-sm text-destructive">Expense and tax options could not be loaded. Existing selections are preserved.</p>}
            {subjectToWithholding && <div className="space-y-3">
              <div className="flex items-center justify-between"><h4 className="font-medium">Category-specific WHT defaults</h4><Button type="button" variant="outline" size="sm" disabled={apReadOnly} onClick={() => setWhtLines((lines) => [...lines, newLine()])}><Plus className="mr-1 h-4 w-4" />Add category</Button></div>
              {whtLines.map((line) => <div key={line.key} className="grid items-end gap-3 rounded-md border p-3 md:grid-cols-[1fr_1fr_2fr_auto_auto_auto]">
                <Field label="Category code"><Input value={line.categoryCode} disabled={apReadOnly} onChange={(e) => setWhtLines((lines) => lines.map((item) => item.key === line.key ? { ...item, categoryCode: e.target.value } : item))} placeholder="SERVICES" /></Field>
                <Field label="Category name"><Input value={line.categoryName} disabled={apReadOnly} onChange={(e) => setWhtLines((lines) => lines.map((item) => item.key === line.key ? { ...item, categoryName: e.target.value } : item))} placeholder="Services" /></Field>
                <Field label="WHT configuration"><Select value={line.withholdingTaxId} disabled={apReadOnly} onValueChange={(value) => { if (value) setWhtLines((lines) => lines.map((item) => item.key === line.key ? { ...item, withholdingTaxId: value } : item)); }}><SelectTrigger><SelectValue placeholder="Select WHT" /></SelectTrigger><SelectContent>{withholdingTaxes.map((tax) => <SelectItem key={tax.id} value={tax.id}>{tax.code} — {tax.rate}%</SelectItem>)}</SelectContent></Select></Field>
                <div className="flex items-center gap-2 pb-2"><Checkbox aria-label={`Active WHT category ${line.categoryCode}`} checked={line.isActive} disabled={apReadOnly} onCheckedChange={checked => setWhtLines(lines => lines.map(item => item.key === line.key ? { ...item, isActive: checked === true } : item))} /><span className="text-sm">Active</span></div>
                <div className="flex items-center gap-2 pb-2"><Checkbox aria-label={`Default WHT category ${line.categoryCode}`} checked={line.isDefaultForAp} disabled={apReadOnly || !line.isActive} onCheckedChange={() => setWhtLines((lines) => lines.map((item) => ({ ...item, isDefaultForAp: item.key === line.key })))} /><span className="text-sm">Default</span></div>
                <Button type="button" variant="ghost" size="icon" aria-label="Remove WHT category" disabled={apReadOnly} onClick={() => setWhtLines((lines) => lines.filter((item) => item.key !== line.key))}><Trash2 className="h-4 w-4" /></Button>
              </div>)}
            </div>}
            <ProfileActions ledger="ap" profile={apProfile} busy={busy || apRole?.status !== 'Active'} dirty={apDirty} reason={decisionReason} onReason={setDecisionReason} onSave={saveAp} onDecision={decision}
              canDecide={hasPermission('Finance.BusinessPartners.Profiles.Approve')}
              isMaker={!!apProfile?.submittedById && !!user?.id && apProfile.submittedById.toLowerCase() === user.id.toLowerCase()} />
          </CardContent>
        </Card>
      )}

      {arRoles.length > 0 && (
        <Card role="region" aria-label="Accounts Receivable profile">
          <CardHeader><CardTitle>Accounts Receivable profile</CardTitle><CardDescription>Customer credit and withholding-agent defaults.</CardDescription></CardHeader>
          <CardContent className="space-y-4">
            <ProfileVersionSelect ledger="AR" profiles={arRole?.arProfiles ?? []} value={arProfile?.id} onChange={setArVersionId} disabled={busy} />
            {arRole?.status === 'Inactive' && <p className="text-sm text-muted-foreground">This role is inactive. Its profile history is read-only.</p>}
            <div className="grid gap-4 md:grid-cols-3">
              <Field label="Customer role"><Select value={arRoleId} disabled={busy} onValueChange={value => { if (!value) return; setArRoleId(value); setArVersionId(''); }}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{arRoles.map((role) => <SelectItem key={role.id} value={role.id}>{role.roleType}</SelectItem>)}</SelectContent></Select></Field>
              <Field label="Effective from"><Input type="date" value={arFrom} onChange={(e) => setArFrom(e.target.value)} disabled={arReadOnly} /></Field>
              <Field label="Effective through (optional)"><Input type="date" value={arTo} onChange={(e) => setArTo(e.target.value)} disabled={arReadOnly} /></Field>
              <Field label="AR reference"><Input value={arReference} onChange={(e) => setArReference(e.target.value)} disabled={arReadOnly} /></Field>
              <Field label="Payment terms"><PaymentTermSelect value={arPaymentTermId} onChange={setArPaymentTermId} terms={paymentTerms} disabled={arReadOnly} /></Field>
              <Field label="Credit limit"><Input type="number" min="0" step="0.01" value={creditLimit} onChange={(e) => setCreditLimit(e.target.value)} disabled={arReadOnly} /></Field>
              <div className="flex items-center gap-3"><Switch id="ar-agent" checked={isWithholdingAgent} onCheckedChange={setIsWithholdingAgent} disabled={arReadOnly} /><Label htmlFor="ar-agent">Customer is a withholding agent</Label></div>
            </div>
            <ProfileActions ledger="ar" profile={arProfile} busy={busy || arRole?.status !== 'Active'} dirty={arDirty} reason={decisionReason} onReason={setDecisionReason} onSave={saveAr} onDecision={decision}
              canDecide={hasPermission('Finance.BusinessPartners.Profiles.Approve')}
              isMaker={!!arProfile?.submittedById && !!user?.id && arProfile.submittedById.toLowerCase() === user.id.toLowerCase()} />
          </CardContent>
        </Card>
      )}

      {apRoles.length === 0 && arRoles.length === 0 && <p className="text-sm text-muted-foreground">This partner has no canonical roles. Recreate it or migrate its legacy PartnerType before preparing Finance profiles.</p>}
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return <div role="group" aria-label={label} className="space-y-1.5"><Label>{label}</Label>{children}</div>;
}

function ProfileVersionSelect({ ledger, profiles, value, onChange, disabled }: { ledger: string; profiles: Array<BusinessPartnerApProfile | BusinessPartnerArProfile>; value?: string; onChange: (value: string) => void; disabled: boolean }) {
  if (!profiles.length) return null;
  return <Field label={`${ledger} profile version`}><Select value={value || ''} onValueChange={item => { if (item) onChange(item); }} disabled={disabled}><SelectTrigger aria-label={`${ledger} profile version`}><SelectValue /></SelectTrigger><SelectContent>{[...profiles].sort((a, b) => b.versionNumber - a.versionNumber).map(profile => <SelectItem key={profile.id} value={profile.id}>Version {profile.versionNumber} — {profile.status} ({dateOnly(profile.effectiveFrom)}{profile.effectiveTo ? ` to ${dateOnly(profile.effectiveTo)}` : ' onwards'})</SelectItem>)}</SelectContent></Select></Field>;
}

function ConfigurationSelect({ label, value, onChange, options, disabled }: { label: string; value: string; onChange: (value: string) => void; options: Array<{ id: string; label: string }>; disabled: boolean }) {
  // Radix's hidden native form select can emit an empty change while options hydrate.
  // Clearing is an explicit selection of __none__; an empty event must not erase saved defaults.
  return <Select value={value || '__none__'} onValueChange={item => { if (item) onChange(item === '__none__' ? '' : item); }} disabled={disabled}><SelectTrigger aria-label={label}><SelectValue /></SelectTrigger><SelectContent><SelectItem value="__none__">None</SelectItem>{value && !options.some(option => option.id === value) && <SelectItem value={value} disabled>Saved selection — unavailable</SelectItem>}{options.map(option => <SelectItem key={option.id} value={option.id}>{option.label}</SelectItem>)}</SelectContent></Select>;
}

function PaymentTermSelect({ value, onChange, terms, disabled }: { value: string; onChange: (value: string) => void; terms: PaymentTermListDto[]; disabled?: boolean }) {
  return <Select value={value || '__none__'} disabled={disabled} onValueChange={item => { if (item) onChange(item === '__none__' ? '' : item); }}><SelectTrigger><SelectValue placeholder="Select terms" /></SelectTrigger><SelectContent><SelectItem value="__none__">None</SelectItem>{terms.map((term) => <SelectItem key={term.id} value={term.id}>{term.code} — {term.name}</SelectItem>)}</SelectContent></Select>;
}

function ProfileActions({ ledger, profile, busy, dirty, reason, onReason, onSave, onDecision, canDecide, isMaker }: {
  ledger: 'ap' | 'ar'; profile: BusinessPartnerApProfile | BusinessPartnerArProfile | null; busy: boolean; dirty: boolean;
  reason: string; onReason: (value: string) => void; onSave: () => void | Promise<unknown>;
  onDecision: (ledger: 'ap' | 'ar', profile: BusinessPartnerApProfile | BusinessPartnerArProfile, action: 'submit' | 'approve' | 'reject') => void;
  canDecide: boolean; isMaker: boolean;
}) {
  return <div className="flex flex-wrap items-end gap-2 border-t pt-4">
    <div className="mr-auto"><p className="text-sm font-medium">{profile ? `Version ${profile.versionNumber} — ${profile.status}` : 'No profile version yet'}</p>{profile?.decisionReason && <p className="text-xs text-muted-foreground">Decision: {profile.decisionReason}</p>}</div>
    {dirty && <p className="text-sm text-muted-foreground">Save draft changes before submitting.</p>}
    {profile?.status === 'Submitted' && isMaker && <p role="status" className="max-w-md text-sm text-muted-foreground">Awaiting an independent Finance approver. You submitted this profile, so maker-checker control prevents you from approving or rejecting it.</p>}
    {profile?.status === 'Submitted' && !isMaker && !canDecide && <p role="status" className="max-w-md text-sm text-muted-foreground">Awaiting a different user with the Approve Business Partner Finance Profiles permission.</p>}
    {profile?.status === 'Submitted' && !isMaker && canDecide && <Input className="max-w-xs" value={reason} onChange={(e) => onReason(e.target.value)} placeholder="Decision reason (required for rejection)" />}
    {(!profile || profile.status === 'Draft' || profile.status === 'Rejected' || profile.status === 'Approved') && <Button type="button" variant="outline" disabled={busy} onClick={onSave}>{profile?.status === 'Draft' ? 'Save draft' : 'Create new draft'}</Button>}
    {profile?.status === 'Draft' && <Button type="button" disabled={busy || dirty} onClick={() => onDecision(ledger, profile, 'submit')}>Submit for approval</Button>}
    {profile?.status === 'Submitted' && !isMaker && canDecide && <><Button type="button" variant="outline" disabled={busy} onClick={() => onDecision(ledger, profile, 'reject')}>Reject</Button><Button type="button" disabled={busy} onClick={() => onDecision(ledger, profile, 'approve')}>Approve</Button></>}
  </div>;
}
