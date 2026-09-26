'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Checkbox } from '@/components/ui/checkbox';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import type { PaymentTermListDto } from '@/services/financeCommonService';
import type { BusinessPartnerWithholdingTaxOption } from '@/services/businessPartnerService';
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
}

const dateOnly = (value?: string) => (value ? value.slice(0, 10) : '');
const newLine = (): WhtLineForm => ({
  key: crypto.randomUUID(), categoryCode: '', categoryName: '', withholdingTaxId: '', isDefaultForAp: false,
});

/**
 * Finance-owned editor embedded in the Procurement Business Partner screen. Procurement owns the
 * identity and role rows; this panel owns only governed AP/AR profile versions. Keeping the panel
 * separate makes that ownership boundary visible to future module maintainers.
 */
export function BusinessPartnerFinanceProfilesPanel({ businessPartnerId, paymentTerms, withholdingTaxes }: Props) {
  const [data, setData] = useState<BusinessPartnerFinanceProfileSet | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [apRoleId, setApRoleId] = useState('');
  const [arRoleId, setArRoleId] = useState('');
  const [apProfile, setApProfile] = useState<BusinessPartnerApProfile | null>(null);
  const [arProfile, setArProfile] = useState<BusinessPartnerArProfile | null>(null);
  const [apFrom, setApFrom] = useState('');
  const [apTo, setApTo] = useState('');
  const [apReference, setApReference] = useState('');
  const [apPaymentTermId, setApPaymentTermId] = useState('');
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
    setLoading(true);
    try {
      const profiles = await businessPartnerFinanceProfileService.get(businessPartnerId);
      setData(profiles);
      setApRoleId((current) => current || profiles.roles.find((role) => role.roleType !== 'Customer')?.id || '');
      setArRoleId((current) => current || profiles.roles.find((role) => role.roleType === 'Customer')?.id || '');
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Could not load Finance profiles.');
    } finally {
      setLoading(false);
    }
  }, [businessPartnerId]);

  useEffect(() => { void load(); }, [load]);

  const apRoles = useMemo(() => data?.roles.filter((role) => role.roleType !== 'Customer') ?? [], [data]);
  const arRoles = useMemo(() => data?.roles.filter((role) => role.roleType === 'Customer') ?? [], [data]);

  useEffect(() => {
    const latest = apRoles.find((role) => role.id === apRoleId)?.apProfiles[0] ?? null;
    setApProfile(latest);
    setApFrom(dateOnly(latest?.effectiveFrom));
    setApTo(dateOnly(latest?.effectiveTo));
    setApReference(latest?.apReferenceNumber || '');
    setApPaymentTermId(latest?.paymentTermId || '');
    setSubjectToWithholding(latest?.subjectToWithholding ?? false);
    setWhtLines(latest?.withholdingDefaults.map((line) => ({
      key: line.id, categoryCode: line.categoryCode, categoryName: line.categoryName || '',
      withholdingTaxId: line.withholdingTaxId, isDefaultForAp: line.isDefaultForAp,
    })) ?? []);
  }, [apRoleId, apRoles]);

  useEffect(() => {
    const latest = arRoles.find((role) => role.id === arRoleId)?.arProfiles[0] ?? null;
    setArProfile(latest);
    setArFrom(dateOnly(latest?.effectiveFrom));
    setArTo(dateOnly(latest?.effectiveTo));
    setArReference(latest?.arReferenceNumber || '');
    setArPaymentTermId(latest?.paymentTermId || '');
    setCreditLimit(latest?.creditLimit == null ? '' : String(latest.creditLimit));
    setIsWithholdingAgent(latest?.isWithholdingAgent ?? false);
  }, [arRoleId, arRoles]);

  const run = async (operation: () => Promise<unknown>, success: string) => {
    setBusy(true);
    try { await operation(); toast.success(success); setDecisionReason(''); await load(); }
    catch (error) { toast.error(error instanceof Error ? error.message : 'Finance profile action failed.'); }
    finally { setBusy(false); }
  };

  const saveAp = () => {
    if (!apRoleId || !apFrom) { toast.error('Select an AP role and effective-from date.'); return; }
    if (subjectToWithholding && whtLines.filter((line) => line.isDefaultForAp).length !== 1) {
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
        subjectToWithholding,
        withholdingDefaults: whtLines.map((line) => ({
          categoryCode: line.categoryCode, categoryName: line.categoryName || undefined,
          withholdingTaxId: line.withholdingTaxId, isDefaultForAp: line.isDefaultForAp, isActive: true,
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

  if (loading) return <p className="py-8 text-center text-muted-foreground">Loading governed Finance profiles...</p>;
  if (!data) return <p className="py-8 text-center text-destructive">Finance profiles could not be loaded.</p>;

  return (
    <div className="space-y-5 py-3">
      <div className="rounded-md border border-blue-200 bg-blue-50 p-3 text-sm text-blue-900">
        Procurement owns this partner&apos;s identity and roles. Finance owns the effective-dated AP/AR profiles below.
        AP and AR control accounts continue to come from Finance settings; the legacy Accounts tab does not override them.
      </div>

      {apRoles.length > 0 && (
        <Card>
          <CardHeader><CardTitle>Accounts Payable profile</CardTitle><CardDescription>Supplier and Contractor defaults, category WHT and maker-checker approval.</CardDescription></CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 md:grid-cols-3">
              <Field label="AP role"><Select value={apRoleId} onValueChange={setApRoleId}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{apRoles.map((role) => <SelectItem key={role.id} value={role.id}>{role.roleType}</SelectItem>)}</SelectContent></Select></Field>
              <Field label="Effective from"><Input type="date" value={apFrom} onChange={(e) => setApFrom(e.target.value)} disabled={apProfile?.status !== undefined && apProfile.status !== 'Draft'} /></Field>
              <Field label="Effective through (optional)"><Input type="date" value={apTo} onChange={(e) => setApTo(e.target.value)} disabled={apProfile?.status !== undefined && apProfile.status !== 'Draft'} /></Field>
              <Field label="AP reference"><Input value={apReference} onChange={(e) => setApReference(e.target.value)} disabled={apProfile?.status !== undefined && apProfile.status !== 'Draft'} /></Field>
              <Field label="Payment terms"><PaymentTermSelect value={apPaymentTermId} onChange={setApPaymentTermId} terms={paymentTerms} disabled={apProfile?.status !== undefined && apProfile.status !== 'Draft'} /></Field>
              <div className="flex items-end gap-3 pb-2"><Switch id="ap-wht" checked={subjectToWithholding} onCheckedChange={setSubjectToWithholding} disabled={apProfile?.status !== undefined && apProfile.status !== 'Draft'} /><Label htmlFor="ap-wht">Subject to withholding</Label></div>
            </div>
            {subjectToWithholding && <div className="space-y-3">
              <div className="flex items-center justify-between"><h4 className="font-medium">Category-specific WHT defaults</h4><Button type="button" variant="outline" size="sm" disabled={apProfile?.status !== undefined && apProfile.status !== 'Draft'} onClick={() => setWhtLines((lines) => [...lines, newLine()])}><Plus className="mr-1 h-4 w-4" />Add category</Button></div>
              {whtLines.map((line) => <div key={line.key} className="grid items-end gap-3 rounded-md border p-3 md:grid-cols-[1fr_1fr_2fr_auto_auto]">
                <Field label="Category code"><Input value={line.categoryCode} disabled={apProfile?.status !== undefined && apProfile.status !== 'Draft'} onChange={(e) => setWhtLines((lines) => lines.map((item) => item.key === line.key ? { ...item, categoryCode: e.target.value } : item))} placeholder="SERVICES" /></Field>
                <Field label="Category name"><Input value={line.categoryName} disabled={apProfile?.status !== undefined && apProfile.status !== 'Draft'} onChange={(e) => setWhtLines((lines) => lines.map((item) => item.key === line.key ? { ...item, categoryName: e.target.value } : item))} placeholder="Services" /></Field>
                <Field label="WHT configuration"><Select value={line.withholdingTaxId} disabled={apProfile?.status !== undefined && apProfile.status !== 'Draft'} onValueChange={(value) => setWhtLines((lines) => lines.map((item) => item.key === line.key ? { ...item, withholdingTaxId: value } : item))}><SelectTrigger><SelectValue placeholder="Select WHT" /></SelectTrigger><SelectContent>{withholdingTaxes.map((tax) => <SelectItem key={tax.id} value={tax.id}>{tax.code} — {tax.rate}%</SelectItem>)}</SelectContent></Select></Field>
                <div className="flex items-center gap-2 pb-2"><Checkbox checked={line.isDefaultForAp} disabled={apProfile?.status !== undefined && apProfile.status !== 'Draft'} onCheckedChange={() => setWhtLines((lines) => lines.map((item) => ({ ...item, isDefaultForAp: item.key === line.key })))} /><span className="text-sm">Default</span></div>
                <Button type="button" variant="ghost" size="icon" aria-label="Remove WHT category" disabled={apProfile?.status !== undefined && apProfile.status !== 'Draft'} onClick={() => setWhtLines((lines) => lines.filter((item) => item.key !== line.key))}><Trash2 className="h-4 w-4" /></Button>
              </div>)}
            </div>}
            <ProfileActions ledger="ap" profile={apProfile} busy={busy} reason={decisionReason} onReason={setDecisionReason} onSave={saveAp} onDecision={decision} />
          </CardContent>
        </Card>
      )}

      {arRoles.length > 0 && (
        <Card>
          <CardHeader><CardTitle>Accounts Receivable profile</CardTitle><CardDescription>Customer credit and withholding-agent defaults.</CardDescription></CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 md:grid-cols-3">
              <Field label="Customer role"><Select value={arRoleId} onValueChange={setArRoleId}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{arRoles.map((role) => <SelectItem key={role.id} value={role.id}>{role.roleType}</SelectItem>)}</SelectContent></Select></Field>
              <Field label="Effective from"><Input type="date" value={arFrom} onChange={(e) => setArFrom(e.target.value)} disabled={arProfile?.status !== undefined && arProfile.status !== 'Draft'} /></Field>
              <Field label="Effective through (optional)"><Input type="date" value={arTo} onChange={(e) => setArTo(e.target.value)} disabled={arProfile?.status !== undefined && arProfile.status !== 'Draft'} /></Field>
              <Field label="AR reference"><Input value={arReference} onChange={(e) => setArReference(e.target.value)} disabled={arProfile?.status !== undefined && arProfile.status !== 'Draft'} /></Field>
              <Field label="Payment terms"><PaymentTermSelect value={arPaymentTermId} onChange={setArPaymentTermId} terms={paymentTerms} disabled={arProfile?.status !== undefined && arProfile.status !== 'Draft'} /></Field>
              <Field label="Credit limit"><Input type="number" min="0" step="0.01" value={creditLimit} onChange={(e) => setCreditLimit(e.target.value)} disabled={arProfile?.status !== undefined && arProfile.status !== 'Draft'} /></Field>
              <div className="flex items-center gap-3"><Switch id="ar-agent" checked={isWithholdingAgent} onCheckedChange={setIsWithholdingAgent} disabled={arProfile?.status !== undefined && arProfile.status !== 'Draft'} /><Label htmlFor="ar-agent">Customer is a withholding agent</Label></div>
            </div>
            <ProfileActions ledger="ar" profile={arProfile} busy={busy} reason={decisionReason} onReason={setDecisionReason} onSave={saveAr} onDecision={decision} />
          </CardContent>
        </Card>
      )}

      {apRoles.length === 0 && arRoles.length === 0 && <p className="text-sm text-muted-foreground">This partner has no canonical roles. Recreate it or migrate its legacy PartnerType before preparing Finance profiles.</p>}
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return <div className="space-y-1.5"><Label>{label}</Label>{children}</div>;
}

function PaymentTermSelect({ value, onChange, terms, disabled }: { value: string; onChange: (value: string) => void; terms: PaymentTermListDto[]; disabled?: boolean }) {
  return <Select value={value || '__none__'} disabled={disabled} onValueChange={(item) => onChange(item === '__none__' ? '' : item)}><SelectTrigger><SelectValue placeholder="Select terms" /></SelectTrigger><SelectContent><SelectItem value="__none__">None</SelectItem>{terms.map((term) => <SelectItem key={term.id} value={term.id}>{term.code} — {term.name}</SelectItem>)}</SelectContent></Select>;
}

function ProfileActions({ ledger, profile, busy, reason, onReason, onSave, onDecision }: {
  ledger: 'ap' | 'ar'; profile: BusinessPartnerApProfile | BusinessPartnerArProfile | null; busy: boolean;
  reason: string; onReason: (value: string) => void; onSave: () => void | Promise<unknown>;
  onDecision: (ledger: 'ap' | 'ar', profile: BusinessPartnerApProfile | BusinessPartnerArProfile, action: 'submit' | 'approve' | 'reject') => void;
}) {
  return <div className="flex flex-wrap items-end gap-2 border-t pt-4">
    <div className="mr-auto"><p className="text-sm font-medium">{profile ? `Version ${profile.versionNumber} — ${profile.status}` : 'No profile version yet'}</p>{profile?.decisionReason && <p className="text-xs text-muted-foreground">Decision: {profile.decisionReason}</p>}</div>
    {profile?.status === 'Submitted' && <Input className="max-w-xs" value={reason} onChange={(e) => onReason(e.target.value)} placeholder="Decision reason (required for rejection)" />}
    {(!profile || profile.status === 'Draft' || profile.status === 'Rejected' || profile.status === 'Approved') && <Button type="button" variant="outline" disabled={busy} onClick={onSave}>{profile?.status === 'Draft' ? 'Save draft' : 'Create new draft'}</Button>}
    {profile?.status === 'Draft' && <Button type="button" disabled={busy} onClick={() => onDecision(ledger, profile, 'submit')}>Submit for approval</Button>}
    {profile?.status === 'Submitted' && <><Button type="button" variant="outline" disabled={busy} onClick={() => onDecision(ledger, profile, 'reject')}>Reject</Button><Button type="button" disabled={busy} onClick={() => onDecision(ledger, profile, 'approve')}>Approve</Button></>}
  </div>;
}
