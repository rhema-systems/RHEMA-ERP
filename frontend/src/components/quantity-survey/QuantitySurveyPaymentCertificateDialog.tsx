'use client';

import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  CheckCircle2,
  Download,
  FileCheck2,
  History,
  Landmark,
  RefreshCw,
  Save,
  Send,
  XCircle,
} from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogDescription,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
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
import { CentralDocumentViewerDialog, type CentralDocumentViewerFile } from '@/components/document-management/CentralDocumentViewerDialog';
import { useAuth } from '@/hooks/use-auth';
import { getQuantitySurveyWorkspaceAccess } from '@/lib/quantity-survey-workspace-access';
import {
  quantitySurveyPaymentCertificateService as service,
  type PaymentCertificateLookups,
  type PaymentCertificateRevision,
  type QuantitySurveyPaymentCertificate,
} from '@/services/quantity-survey-payment-certificate.service';

type Props = { projectId: string };
type Draft = {
  paymentDueDate: string;
  advanceRecoveryAgreementId: string;
  advanceRecoveryAmount: number;
  materialReconciliationId: string;
  otherDeductionsAmount: number;
  notes: string;
};

const emptyDraft: Draft = {
  paymentDueDate: '',
  advanceRecoveryAgreementId: '',
  advanceRecoveryAmount: 0,
  materialReconciliationId: '',
  otherDeductionsAmount: 0,
  notes: '',
};
const numberValue = (value: string) => Math.max(0, Number(value || 0));
const money = (value: number, currency: string) =>
  new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: currency || 'GHS',
    maximumFractionDigits: 2,
  }).format(value || 0);

export function QuantitySurveyPaymentCertificateDialog({ projectId }: Props) {
  const { hasPermission } = useAuth();
  const canRead = hasPermission('quantity-survey.workspace.read');
  const canManage =
    getQuantitySurveyWorkspaceAccess(hasPermission).canManageCertificates;
  const canApprove = hasPermission('quantity-survey.transactions.approve');
  const canAudit = hasPermission('quantity-survey.audit.read');
  const requests = useRef<Record<string, { fingerprint: string; id: string }>>(
    {}
  );
  const [open, setOpen] = useState(false);
  const [pdfPreview, setPdfPreview] = useState<CentralDocumentViewerFile | null>(null);
  const [preparedPdf, setPreparedPdf] = useState<{ url: string; fileName: string; certificateId: string } | null>(null);
  const [preparingPdf, setPreparingPdf] = useState(false);
  useEffect(() => () => { if (preparedPdf) URL.revokeObjectURL(preparedPdf.url); }, [preparedPdf]);
  useEffect(() => { if (!open) setPreparedPdf(null); }, [open, preparedPdf]);
  useEffect(() => { if (!open) setPdfPreview(null); }, [open]);
  const [loading, setLoading] = useState(false);
  const [working, setWorking] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [lookups, setLookups] = useState<PaymentCertificateLookups>({
    eligibleValuations: [],
    eligibleAdvanceRecoveries: [],
    approvedMaterialReconciliations: [],
  });
  const [certificates, setCertificates] = useState<
    QuantitySurveyPaymentCertificate[]
  >([]);
  const [selectedId, setSelectedId] = useState('');
  const [worksheetId, setWorksheetId] = useState('');
  const [draft, setDraft] = useState<Draft>(emptyDraft);
  const [reason, setReason] = useState('');
  const [history, setHistory] = useState<PaymentCertificateRevision[]>([]);

  const selected = useMemo(
    () => certificates.find((value) => value.id === selectedId),
    [certificates, selectedId]
  );
  const selectedLookup = useMemo(
    () =>
      lookups.eligibleValuations.find(
        (value) => value.worksheetId === worksheetId
      ),
    [lookups.eligibleValuations, worksheetId]
  );
  const eligibleRecoveries = useMemo(
    () =>
      lookups.eligibleAdvanceRecoveries.filter(
        (value) =>
          value.contractId === selectedLookup?.contractId &&
          value.remainingBalance > 0
      ),
    [lookups.eligibleAdvanceRecoveries, selectedLookup?.contractId]
  );

  const applyDraft = (value?: QuantitySurveyPaymentCertificate) =>
    setDraft(
      value
        ? {
            paymentDueDate: value.paymentDueDate?.slice(0, 10) || '',
            advanceRecoveryAgreementId: value.advanceRecoveryAgreementId || '',
            advanceRecoveryAmount: value.advanceRecoveryAmount,
            materialReconciliationId: value.materialReconciliationId || '',
            otherDeductionsAmount: value.otherDeductionsAmount,
            notes: value.notes || '',
          }
        : emptyDraft
    );

  const load = useCallback(
    async (preferredId?: string) => {
      setLoading(true);
      try {
        const [lookupResult, values] = await Promise.all([
          service.lookups(projectId),
          service.list(projectId),
        ]);
        setLookups(lookupResult);
        setCertificates(values);
        const nextId =
          preferredId && values.some((value) => value.id === preferredId)
            ? preferredId
            : values[0]?.id || '';
        setSelectedId(nextId);
        applyDraft(values.find((value) => value.id === nextId));
        setWorksheetId(lookupResult.eligibleValuations[0]?.worksheetId || '');
        setHistory([]);
      } catch (error) {
        toast.error(
          error instanceof Error
            ? error.message
            : 'Failed to load payment certificates'
        );
      } finally {
        setLoading(false);
      }
    },
    [projectId]
  );

  const requestId = (action: string, payload: object) => {
    const fingerprint = JSON.stringify(payload);
    if (requests.current[action]?.fingerprint !== fingerprint)
      requests.current[action] = { fingerprint, id: crypto.randomUUID() };
    return requests.current[action].id;
  };
  const complete = async (
    value: QuantitySurveyPaymentCertificate,
    message: string
  ) => {
    requests.current = {};
    setReason('');
    await load(value.id);
    toast.success(message);
  };
  const run = async (
    action: () => Promise<QuantitySurveyPaymentCertificate>,
    message: string
  ) => {
    setWorking(true);
    setActionError(null);
    try {
      await complete(await action(), message);
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Payment-certificate action failed';
      setActionError(message);
      toast.error(message);
    } finally {
      setWorking(false);
    }
  };

  const generate = () => {
    if (!worksheetId)
      return toast.error('Select an approved certificate-ready valuation.');
    const payload = { worksheetId, ...draft };
    return run(
      () =>
        service.generate(projectId, {
          clientRequestId: requestId('generate', payload),
          valuationWorksheetId: worksheetId,
          advanceRecoveryAgreementId: draft.advanceRecoveryAgreementId || null,
          materialReconciliationId: draft.materialReconciliationId || null,
          paymentDueDate: draft.paymentDueDate || null,
          advanceRecoveryAmount: draft.advanceRecoveryAmount,
          otherDeductionsAmount: draft.otherDeductionsAmount,
          notes: draft.notes || null,
        }),
      'Governed payment certificate generated'
    );
  };
  const save = () => {
    if (!selected) return;
    const payload = { id: selected.id, ...draft };
    return run(
      () =>
        service.update(selected.id, {
          clientRequestId: requestId('update', payload),
          rowVersion: selected.rowVersion,
          paymentDueDate: draft.paymentDueDate || null,
          materialReconciliationId: draft.materialReconciliationId || null,
          advanceRecoveryAmount: draft.advanceRecoveryAmount,
          otherDeductionsAmount: draft.otherDeductionsAmount,
          notes: draft.notes || null,
        }),
      'Draft certificate updated'
    );
  };
  const lifecycle = (
    action: 'submit' | 'approve' | 'reject' | 'handoffToAp',
    message: string
  ) => {
    if (!selected) return;
    if (reason.trim().length < 5)
      return toast.error('Enter a clear reason of at least 5 characters.');
    const payload = {
      id: selected.id,
      reason: reason.trim(),
      rowVersion: selected.rowVersion,
    };
    return run(
      () =>
        service[action](selected.id, {
          clientRequestId: requestId(action, payload),
          rowVersion: selected.rowVersion,
          reason: reason.trim(),
        }),
      message
    );
  };
  const showHistory = async () => {
    if (!selected) return;
    try {
      setHistory(await service.history(selected.id));
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Failed to load audit history'
      );
    }
  };
  const exportDocument = async () => {
    if (!selected) return;
    setPreparingPdf(true);
    try {
      const blob = await service.document(selected.id);
      setPreparedPdf({ url: URL.createObjectURL(blob), certificateId: selected.id,
        fileName: `${selected.certificateNumber || 'payment-certificate'}.pdf` });
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Failed to generate certificate PDF'
      );
    } finally { setPreparingPdf(false); }
  };

  if (!canRead) return null;
  return (
    <>
    <Dialog
      open={open}
      onOpenChange={(value) => {
        setOpen(value);
        if (value) void load();
      }}
    >
      <DialogTrigger asChild>
        <Button size="sm">
          <FileCheck2 className="mr-2 h-4 w-4" />
          Payment certificate workspace
        </Button>
      </DialogTrigger>
      <DialogContent className="max-h-[92vh] max-w-6xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Governed payment certificates</DialogTitle>
          <DialogDescription>Prepare certificates from approved valuations and follow their review and Finance handoff.</DialogDescription>
        </DialogHeader>
        {actionError && <div role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">{actionError}</div>}
        <div className="rounded-md border bg-muted/20 p-3 text-sm text-muted-foreground">
          Certificates originate from independently approved valuations. Policy,
          workflow, template, tax and AP account lineage are selected by the
          effective QS configuration and cannot be typed or overridden here.
        </div>
        <div className="grid gap-5 lg:grid-cols-[0.38fr,0.62fr]">
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <div className="font-medium">Certificate register</div>
              <Button
                variant="outline"
                size="sm"
                disabled={loading}
                onClick={() => void load(selectedId)}
              >
                <RefreshCw className="mr-2 h-4 w-4" />
                Refresh
              </Button>
            </div>
            {certificates.length === 0 ? (
              <div className="rounded-md border border-dashed p-5 text-sm text-muted-foreground">
                No governed certificates yet.
              </div>
            ) : (
              certificates.map((value) => (
                <button
                  key={value.id}
                  type="button"
                  onClick={() => {
                    setSelectedId(value.id);
                    applyDraft(value);
                    setHistory([]);
                  }}
                  className={`w-full rounded-md border p-3 text-left ${selectedId === value.id ? 'border-primary bg-primary/5' : 'hover:bg-muted/30'}`}
                >
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-medium">
                      {value.certificateNumber}
                    </span>
                    <Badge variant="outline">{value.status}</Badge>
                  </div>
                  <div className="mt-1 text-sm text-muted-foreground">
                    {money(value.netCertifiedAmount, value.currency)} ·{' '}
                    {value.apHandoffStatus}
                  </div>
                </button>
              ))
            )}
          </div>
          <div className="space-y-4">
            {!selected ? (
              <div className="space-y-4 rounded-md border p-4">
                <div className="font-medium">
                  Generate from an approved valuation
                </div>
                <div className="grid gap-2">
                  <Label>Certificate-ready valuation</Label>
                  <Select value={worksheetId} onValueChange={setWorksheetId}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select approved valuation" />
                    </SelectTrigger>
                    <SelectContent>
                      {lookups.eligibleValuations.map((value) => (
                        <SelectItem
                          key={value.worksheetId}
                          value={value.worksheetId}
                        >
                          {value.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                {selectedLookup ? (
                  <div className="rounded-md bg-muted/30 p-3 text-sm">
                    Certified to date{' '}
                    {money(
                      selectedLookup.certifiedToDateAmount,
                      selectedLookup.currency
                    )}{' '}
                    · Previous{' '}
                    {money(
                      selectedLookup.previousCertificateAmount,
                      selectedLookup.currency
                    )}{' '}
                    · Current retention{' '}
                    {money(
                      selectedLookup.currentRetentionAmount,
                      selectedLookup.currency
                    )}
                  </div>
                ) : null}
                <CertificateInputs
                  draft={draft}
                  setDraft={setDraft}
                  recoveries={eligibleRecoveries}
                  materials={lookups.approvedMaterialReconciliations.filter(
                    (value) => value.worksheetId === worksheetId
                  )}
                  grossAmount={selectedLookup?.currentGrossAmount || 0}
                  disabled={!canManage || working}
                />
                <Button
                  disabled={!canManage || working || !worksheetId}
                  onClick={generate}
                >
                  <FileCheck2 className="mr-2 h-4 w-4" />
                  Generate controlled draft
                </Button>
              </div>
            ) : (
              <>
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div>
                    <div className="text-lg font-semibold">
                      {selected.certificateNumber}
                    </div>
                    <div className="text-sm text-muted-foreground">
                      {selected.title}
                    </div>
                  </div>
                  <div className="flex gap-2">
                    <Badge>{selected.status}</Badge>
                    <Badge variant="outline">{selected.approvalStatus}</Badge>
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-3 rounded-md border p-3 text-sm md:grid-cols-4">
                  <Metric
                    label="Current gross"
                    value={money(
                      selected.grossCertifiedAmount,
                      selected.currency
                    )}
                  />
                  <Metric
                    label="Tax"
                    value={`${money(selected.taxAmount, selected.currency)} (${selected.taxHandling})`}
                  />
                  <Metric
                    label="Net payable"
                    value={money(
                      selected.netCertifiedAmount,
                      selected.currency
                    )}
                  />
                  <Metric
                    label="Finance AP"
                    value={`${selected.vendorInvoiceNumber || selected.apHandoffStatus} · ${selected.paymentStatus}`}
                  />
                  <Metric
                    label="Finance reconciliation"
                    value={`${selected.reconciliationStatus} · ${selected.financePostingStatus}`}
                  />
                  {selected.vendorInvoiceId ? (
                    <Metric
                      label="Paid / balance"
                      value={`${money(selected.financePaidAmount || 0, selected.currency)} / ${money(selected.financeBalanceAmount || 0, selected.currency)}`}
                    />
                  ) : null}
                </div>
                <CertificateInputs
                  draft={draft}
                  setDraft={setDraft}
                  recoveries={lookups.eligibleAdvanceRecoveries.filter(
                    (value) =>
                      value.agreementId === selected.advanceRecoveryAgreementId
                  )}
                  materials={lookups.approvedMaterialReconciliations.filter(
                    (value) =>
                      value.reconciliationId ===
                      selected.materialReconciliationId
                  )}
                  grossAmount={selected.grossCertifiedAmount}
                  disabled={
                    !canManage || working || selected.status !== 'Draft'
                  }
                />
                <div className="grid gap-2">
                  <Label>Action reason</Label>
                  <Textarea
                    rows={2}
                    value={reason}
                    onChange={(event) => setReason(event.target.value)}
                    placeholder="Record the business reason for submission, decision or AP handoff"
                  />
                </div>
                <div className="flex flex-wrap gap-2">
                  {selected.status === 'Draft' && canManage ? (
                    <>
                      <Button
                        variant="outline"
                        disabled={working}
                        onClick={save}
                      >
                        <Save className="mr-2 h-4 w-4" />
                        Save draft
                      </Button>
                      <Button
                        disabled={working}
                        onClick={() =>
                          lifecycle(
                            'submit',
                            'Certificate submitted for independent approval'
                          )
                        }
                      >
                        <Send className="mr-2 h-4 w-4" />
                        Submit
                      </Button>
                    </>
                  ) : null}
                  {selected.status === 'Issued' && canApprove ? (
                    <>
                      <Button
                        disabled={working}
                        onClick={() =>
                          lifecycle('approve', 'Certificate approved')
                        }
                      >
                        <CheckCircle2 className="mr-2 h-4 w-4" />
                        Approve
                      </Button>
                      <Button
                        variant="destructive"
                        disabled={working}
                        onClick={() =>
                          lifecycle('reject', 'Certificate rejected')
                        }
                      >
                        <XCircle className="mr-2 h-4 w-4" />
                        Reject
                      </Button>
                    </>
                  ) : null}
                  {selected.status === 'Approved' &&
                  canManage &&
                  !selected.vendorInvoiceId ? (
                    <Button
                      disabled={working}
                      onClick={() =>
                        lifecycle('handoffToAp', 'Finance AP invoice created')
                      }
                    >
                      <Landmark className="mr-2 h-4 w-4" />
                      Create AP invoice
                    </Button>
                  ) : null}
                  {['Approved', 'Paid'].includes(selected.status) &&
                  canAudit ? (
                    <>
                    <Button variant="outline" onClick={() => setPdfPreview({
                      title: selected.certificateNumber || 'Payment certificate',
                      fileName: `${selected.certificateNumber || 'payment-certificate'}.pdf`,
                      contentType: 'application/pdf',
                      repositoryPath: `/api/quantity-survey/payment-certificates/${encodeURIComponent(selected.id)}/document`,
                      sourceLabel: 'QS payment certificate',
                    })}>Preview PDF</Button>
                    <Button variant="outline" disabled={preparingPdf} onClick={exportDocument}>
                      <Download className="mr-2 h-4 w-4" />
                      {preparingPdf ? 'Preparing PDF...' : 'PDF'}
                    </Button>
                    </>
                  ) : null}
                  {canAudit && preparedPdf?.certificateId === selected.id && (
                    <Button variant="outline" asChild><a href={preparedPdf.url} download={preparedPdf.fileName}>Download PDF</a></Button>
                  )}
                  {canAudit ? (
                    <Button variant="outline" onClick={showHistory}>
                      <History className="mr-2 h-4 w-4" />
                      History
                    </Button>
                  ) : null}
                </div>
                {history.length > 0 ? (
                  <div className="space-y-2 rounded-md border p-3">
                    <div className="font-medium">Immutable history</div>
                    {history.map((value) => (
                      <div
                        key={value.id}
                        className="border-b py-2 text-sm last:border-0"
                      >
                        <div className="font-medium">{value.action}</div>
                        <div className="text-muted-foreground">
                          {value.actorName} ·{' '}
                          {new Date(value.createdAt).toLocaleString()} ·{' '}
                          {value.correlationId}
                        </div>
                      </div>
                    ))}
                  </div>
                ) : null}
              </>
            )}
            {selected ? (
              <Button
                variant="ghost"
                size="sm"
                onClick={() => {
                  setSelectedId('');
                  applyDraft();
                  setHistory([]);
                }}
              >
                Generate another certificate
              </Button>
            ) : null}
          </div>
        </div>
      </DialogContent>
    </Dialog>
    <CentralDocumentViewerDialog file={pdfPreview} open={pdfPreview !== null}
      onOpenChange={value => { if (!value) setPdfPreview(null); }} enableAnnotations={false} enableSaveCopy />
    </>
  );
}

function CertificateInputs({
  draft,
  setDraft,
  disabled = false,
  recoveries,
  materials,
  grossAmount,
}: {
  draft: Draft;
  setDraft: (value: Draft) => void;
  disabled?: boolean;
  recoveries: PaymentCertificateLookups['eligibleAdvanceRecoveries'];
  materials: PaymentCertificateLookups['approvedMaterialReconciliations'];
  grossAmount: number;
}) {
  const update = <K extends keyof Draft>(key: K, value: Draft[K]) =>
    setDraft({ ...draft, [key]: value });
  const selectedMaterial = materials.find(
    (value) => value.reconciliationId === draft.materialReconciliationId
  );
  return (
    <div className="grid gap-3 md:grid-cols-2">
      <div className="grid gap-2">
        <Label>Payment due date</Label>
        <Input
          type="date"
          disabled={disabled}
          value={draft.paymentDueDate}
          onChange={(event) => update('paymentDueDate', event.target.value)}
        />
      </div>
      <div className="grid gap-2">
        <Label>Approved advance recovery</Label>
        <Select
          disabled={disabled}
          value={draft.advanceRecoveryAgreementId || 'none'}
          onValueChange={(value) => {
            const agreement = recoveries.find(
              (item) => item.agreementId === value
            );
            setDraft({
              ...draft,
              advanceRecoveryAgreementId: agreement?.agreementId || '',
              advanceRecoveryAmount: agreement
                ? Math.min(
                    agreement.remainingBalance,
                    Math.round(grossAmount * agreement.recoveryPercentage) / 100
                  )
                : 0,
            });
          }}
        >
          <SelectTrigger>
            <SelectValue placeholder="No advance recovery" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="none">No advance recovery</SelectItem>
            {recoveries.map((value) => (
              <SelectItem key={value.agreementId} value={value.agreementId}>
                {value.label} · {money(value.remainingBalance, value.currency)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <div className="text-xs text-muted-foreground">
          Governed deduction:{' '}
          {money(draft.advanceRecoveryAmount, recoveries[0]?.currency || 'GHS')}
        </div>
      </div>
      <div className="grid gap-2">
        <Label>Approved material reconciliation</Label>
        <Select
          disabled={disabled}
          value={draft.materialReconciliationId || 'none'}
          onValueChange={(value) =>
            update('materialReconciliationId', value === 'none' ? '' : value)
          }
        >
          <SelectTrigger>
            <SelectValue placeholder="No governed material values" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="none">No governed material values</SelectItem>
            {materials.map((value) => (
              <SelectItem
                key={value.reconciliationId}
                value={value.reconciliationId}
              >
                {value.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        {selectedMaterial ? (
          <div className="text-xs text-muted-foreground">
            On site{' '}
            {money(
              selectedMaterial.materialOnSiteAmount,
              selectedMaterial.currency
            )}{' '}
            · Off site{' '}
            {money(
              selectedMaterial.materialOffSiteAmount,
              selectedMaterial.currency
            )}{' '}
            · TDC deduction{' '}
            {money(
              selectedMaterial.tdcSuppliedDeductionAmount,
              selectedMaterial.currency
            )}
          </div>
        ) : null}
      </div>
      <div className="grid gap-2">
        <Label>Other deductions</Label>
        <Input
          type="number"
          min="0"
          step="0.01"
          disabled={disabled}
          value={draft.otherDeductionsAmount}
          onChange={(event) =>
            update('otherDeductionsAmount', numberValue(event.target.value))
          }
        />
      </div>
      <div className="grid gap-2 md:col-span-2">
        <Label>Certificate notes</Label>
        <Textarea
          rows={2}
          disabled={disabled}
          value={draft.notes}
          onChange={(event) => update('notes', event.target.value)}
        />
      </div>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="font-medium">{value}</div>
    </div>
  );
}
