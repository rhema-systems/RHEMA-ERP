'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { AlertTriangle, CheckCircle2, Loader2, RefreshCw, Save } from 'lucide-react';
import { toast } from 'sonner';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import {
  quantitySurveyContractCommercialTermsService as service,
  type ContractCommercialTerms,
  type ContractCommercialTermsWorkspace,
} from '@/services/quantity-survey-contract-commercial-terms.service';

type Props = { contractId: string; onContractChanged: () => void };

export function QuantitySurveyContractCommercialTermsPanel({
  contractId,
  onContractChanged,
}: Props) {
  const { hasPermission } = useAuth();
  const canRead = hasPermission('quantity-survey.workspace.read');
  const canManage = hasPermission('quantity-survey.final-accounts.manage');
  const requestId = useRef<string | null>(null);
  const [workspace, setWorkspace] =
    useState<ContractCommercialTermsWorkspace | null>(null);
  const [form, setForm] = useState<ContractCommercialTerms | null>(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!canRead) return;
    setLoading(true);
    setLoadError(null);
    try {
      const value = await service.workspace(contractId);
      setWorkspace(value);
      setForm(value.terms);
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Commercial terms could not be loaded.';
      setLoadError(message);
      toast.error(message);
    } finally {
      setLoading(false);
    }
  }, [canRead, contractId]);

  useEffect(() => {
    void load();
  }, [load]);

  const update = <K extends keyof ContractCommercialTerms>(
    key: K,
    value: ContractCommercialTerms[K]
  ) => setForm((current) => (current ? { ...current, [key]: value } : current));

  const save = async () => {
    if (!form || !workspace) return;
    requestId.current ||= crypto.randomUUID();
    setSaving(true);
    try {
      const value = await service.configure(contractId, {
        clientRequestId: requestId.current,
        rowVersion: form.rowVersion,
        paymentTermId: form.paymentTermId,
        provisionalSumAmount: form.provisionalSumAmount,
        contingencyAmount: form.contingencyAmount,
        retentionPercentage: form.retentionPercentage,
        defectsLiabilityDays: form.defectsLiabilityDays,
        retentionClause: form.retentionClause,
        allowSectionalTakeover: form.allowSectionalTakeover,
        sectionalTakeoverClause: form.sectionalTakeoverClause,
        allowSubcontracting: form.allowSubcontracting,
        subcontractPaymentTermId: form.subcontractPaymentTermId,
        subcontractTerms: form.subcontractTerms,
        claimNoticePeriodDays: form.claimNoticePeriodDays,
        claimClause: form.claimClause,
        commercialTermsContractDocumentId:
          form.commercialTermsContractDocumentId,
      });
      requestId.current = null;
      setWorkspace(value);
      setForm(value.terms);
      onContractChanged();
      toast.success('Works-contract commercial terms saved.');
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Commercial terms could not be saved.'
      );
    } finally {
      setSaving(false);
    }
  };

  if (!canRead) return null;
  if (loading)
    return (
      <div className="flex min-h-40 items-center justify-center text-sm text-muted-foreground">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" /> Loading commercial terms…
      </div>
    );
  if (loadError || !workspace || !form)
    return (
      <Alert variant="destructive">
        <AlertTriangle className="h-4 w-4" />
        <AlertTitle>Commercial terms could not be loaded</AlertTitle>
        <AlertDescription className="flex flex-wrap items-center justify-between gap-3">
          <span>{loadError || 'Refresh the contract and try again.'}</span>
          <Button type="button" variant="outline" size="sm" onClick={() => void load()}>
            <RefreshCw className="mr-2 h-4 w-4" /> Retry
          </Button>
        </AlertDescription>
      </Alert>
    );

  const editable = workspace.isEditable && canManage;
  const isDraft = form.contractStatus === 'Draft';
  const money = (value: number) =>
    new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency: form.currency || 'GHS',
    }).format(value || 0);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h3 className="font-semibold">Works-contract commercial terms</h3>
          <p className="text-sm text-muted-foreground">
            {form.contractNumber} · {money(form.contractValue)} · controlled by the effective QS retention and contract policies
          </p>
        </div>
        <div className="flex gap-2">
          <Button type="button" variant="outline" size="sm" onClick={() => void load()} disabled={loading || saving}>
            <RefreshCw className="mr-2 h-4 w-4" /> Refresh
          </Button>
          {editable && (
            <Button type="button" size="sm" onClick={() => void save()} disabled={saving}>
              {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
              Save terms
            </Button>
          )}
        </div>
      </div>

      {!isDraft ? (
        <Alert>
          <CheckCircle2 className="h-4 w-4" />
          <AlertTitle>Commercial terms on record</AlertTitle>
          <AlertDescription>These terms are locked because the contract is no longer in Draft.</AlertDescription>
        </Alert>
      ) : workspace.readinessBlockers.length ? (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Activation is not ready</AlertTitle>
          <AlertDescription>
            <ul className="list-disc space-y-1 pl-5">
              {workspace.readinessBlockers.map((value) => <li key={value}>{value}</li>)}
            </ul>
          </AlertDescription>
        </Alert>
      ) : (
        <Alert>
          <CheckCircle2 className="h-4 w-4" />
          <AlertTitle>Commercial terms ready</AlertTitle>
          <AlertDescription>The current terms pass the activation readiness check.</AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader className="pb-3"><CardTitle className="text-base">Amounts and retention</CardTitle></CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
          <Field label="Payment term">
            <Select disabled={!editable} value={form.paymentTermId || ''} onValueChange={(value) => update('paymentTermId', value)}>
              <SelectTrigger><SelectValue placeholder="Select payment term" /></SelectTrigger>
              <SelectContent>{workspace.paymentTerms.map((value) => (
                <SelectItem key={value.id} value={value.id}>{value.code} · {value.name}</SelectItem>
              ))}</SelectContent>
            </Select>
          </Field>
          {workspace.controlProvisionalSums && <NumberField label="Provisional sum" value={form.provisionalSumAmount} disabled={!editable} onChange={(value) => update('provisionalSumAmount', value)} />}
          {workspace.controlContingencies && <NumberField label="Contingency" value={form.contingencyAmount} disabled={!editable} onChange={(value) => update('contingencyAmount', value)} />}
          <NumberField label={`Retention % (max ${workspace.maximumRetentionPercentage}%)`} value={form.retentionPercentage} disabled={!editable} onChange={(value) => update('retentionPercentage', value)} />
          <Field label="Retention clause" className="md:col-span-2 lg:col-span-4">
            <Textarea disabled={!editable} value={form.retentionClause || ''} onChange={(event) => update('retentionClause', event.target.value)} />
          </Field>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3"><CardTitle className="text-base">Defects, takeover, subcontract and claims</CardTitle></CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2">
          {workspace.controlDefectsLiability && <NumberField label={`Defects liability days (max ${workspace.maximumDefectsLiabilityDays})`} value={form.defectsLiabilityDays || 0} disabled={!editable} onChange={(value) => update('defectsLiabilityDays', value)} />}
          {workspace.controlSectionalTakeover && <Toggle label={`Allow sectional takeover (${workspace.sectionalTakeoverReleasePercentage}% retention release)`} checked={form.allowSectionalTakeover} disabled={!editable} onChange={(value) => update('allowSectionalTakeover', value)} />}
          {workspace.controlSectionalTakeover && form.allowSectionalTakeover && <Field label="Sectional-takeover clause" className="md:col-span-2"><Textarea disabled={!editable} value={form.sectionalTakeoverClause || ''} onChange={(event) => update('sectionalTakeoverClause', event.target.value)} /></Field>}
          {workspace.controlSubcontracts && <Toggle label="Allow subcontracting" checked={form.allowSubcontracting} disabled={!editable} onChange={(value) => update('allowSubcontracting', value)} />}
          {workspace.controlSubcontracts && form.allowSubcontracting && <Field label="Subcontract payment term">
            <Select disabled={!editable} value={form.subcontractPaymentTermId || ''} onValueChange={(value) => update('subcontractPaymentTermId', value)}>
              <SelectTrigger><SelectValue placeholder="Select subcontract payment term" /></SelectTrigger>
              <SelectContent>{workspace.paymentTerms.map((value) => (
                <SelectItem key={value.id} value={value.id}>{value.code} · {value.name}</SelectItem>
              ))}</SelectContent>
            </Select>
          </Field>}
          {workspace.controlSubcontracts && form.allowSubcontracting && <Field label="Subcontract terms" className="md:col-span-2"><Textarea disabled={!editable} value={form.subcontractTerms || ''} onChange={(event) => update('subcontractTerms', event.target.value)} /></Field>}
          {workspace.controlClaimClauses && <NumberField label="Claim notice period (days)" value={form.claimNoticePeriodDays || 0} disabled={!editable} onChange={(value) => update('claimNoticePeriodDays', value)} />}
          {workspace.controlClaimClauses && <Field label="Claim clause" className="md:col-span-2"><Textarea disabled={!editable} value={form.claimClause || ''} onChange={(event) => update('claimClause', event.target.value)} /></Field>}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3"><CardTitle className="text-base">Governed contract evidence</CardTitle></CardHeader>
        <CardContent>
          <Field label={workspace.requireCommercialTermsDocument ? 'Commercial-terms document (required)' : 'Commercial-terms document'}>
            <Select disabled={!editable} value={form.commercialTermsContractDocumentId || ''} onValueChange={(value) => update('commercialTermsContractDocumentId', value)}>
              <SelectTrigger><SelectValue placeholder="Select a clean published contract document" /></SelectTrigger>
              <SelectContent>{workspace.contractDocuments.map((value) => (
                <SelectItem key={value.id} value={value.id}>{value.documentType} · {value.fileName}</SelectItem>
              ))}</SelectContent>
            </Select>
          </Field>
          {!workspace.contractDocuments.length && <p className="mt-2 text-sm text-muted-foreground">Upload and publish a central-DMS contract document in the Documents tab first.</p>}
        </CardContent>
      </Card>
    </div>
  );
}

function Field({ label, className = '', children }: { label: string; className?: string; children: React.ReactNode }) {
  return <div className={`space-y-1.5 ${className}`}><Label>{label}</Label>{children}</div>;
}

function NumberField({ label, value, disabled, onChange }: { label: string; value: number; disabled: boolean; onChange: (value: number) => void }) {
  return <Field label={label}><Input type="number" min={0} step="0.01" disabled={disabled} value={value} onChange={(event) => onChange(Number(event.target.value) || 0)} /></Field>;
}

function Toggle({ label, checked, disabled, onChange }: { label: string; checked: boolean; disabled: boolean; onChange: (value: boolean) => void }) {
  return <label className="flex min-h-10 items-center gap-3 rounded-md border px-3 py-2 text-sm"><Checkbox disabled={disabled} checked={checked} onCheckedChange={(value) => onChange(value === true)} />{label}</label>;
}
