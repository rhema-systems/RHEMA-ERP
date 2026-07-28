'use client';

import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  CheckCircle2,
  Plus,
  RefreshCw,
  ShieldCheck,
  Trash2,
} from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
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
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import {
  requestActions,
  statusTone,
} from '@/lib/procurement-master-data-change';
import {
  buildSupplierMasterPatch,
  hydrateSupplierComplianceDraft,
  supplierMasterResourceTypes,
  type BeneficialOwnerDraft,
  type SupplierMasterChangeDraft,
  type SupplierMasterResourceType,
} from '@/lib/procurement-supplier-master-change';
import { procurementSupplierMasterChangeService } from '@/services/procurement-supplier-master-change.service';
import type { ProcurementMasterDataChange } from '@/types/procurement-master-data-change';

const today = () => new Date().toISOString().slice(0, 10);
const resourceLabels: Record<SupplierMasterResourceType, string> = {
  SupplierProfile: 'Supplier profile',
  SupplierBankDetails: 'Bank details',
  SupplierTaxDetails: 'Tax details',
  SupplierOwnershipDetails: 'Beneficial ownership',
  SupplierCategoryAssignments: 'Category assignments',
  SupplierComplianceStatus: 'Compliance and status',
};

const owner = (): BeneficialOwnerDraft => ({
  name: '',
  ownershipPercent: '100',
  nationality: '',
  registrationNumber: '',
  politicallyExposed: false,
});

const emptyDraft = (): SupplierMasterChangeDraft => ({
  resourceType: 'SupplierProfile',
  partnerName: '',
  legalName: '',
  primaryEmail: '',
  primaryPhone: '',
  physicalAddress: '',
  bankName: '',
  bankAccountNumber: '',
  bankAccountName: '',
  bankBranch: '',
  bankSwiftCode: '',
  bankIBAN: '',
  taxIdentificationNumber: '',
  vatNumber: '',
  isTaxExempt: false,
  taxExemptionNumber: '',
  taxExemptionExpiry: '',
  owners: [owner()],
  ownershipVerifiedAtUtc: today(),
  categoryIds: [],
  registrationStatus: 'Approved',
  approvalStatus: 'Approved',
  isActive: true,
  isBlacklisted: false,
  blacklistReason: '',
  blacklistDate: '',
  blacklistExpiryDate: '',
  riskLevel: 'Low',
  complianceStatus: 'Compliant',
  complianceReviewDateUtc: today(),
  complianceValidUntilUtc: '',
  complianceNotes: '',
});

type LifecycleKind =
  | 'submit'
  | 'revalidate'
  | 'approve'
  | 'reject'
  | 'apply'
  | 'cancel';

export default function SupplierMasterChangesPage() {
  const { toast } = useToast();
  const client = useQueryClient();
  const [draftOpen, setDraftOpen] = useState(false);
  const [partnerId, setPartnerId] = useState('');
  const [draft, setDraft] = useState<SupplierMasterChangeDraft>(emptyDraft);
  const [changedFields, setChangedFields] = useState<
    Set<keyof SupplierMasterChangeDraft>
  >(new Set());
  const [reason, setReason] = useState('');
  const [effectiveAtUtc, setEffectiveAtUtc] = useState(today());
  const [evidenceReference, setEvidenceReference] = useState('');
  const [action, setAction] = useState<{
    kind: LifecycleKind;
    request: ProcurementMasterDataChange;
  }>();
  const [comment, setComment] = useState('');

  const summary = useQuery({
    queryKey: ['supplier-master-change-summary'],
    queryFn: procurementSupplierMasterChangeService.summary,
  });
  const policies = useQuery({
    queryKey: ['supplier-master-change-policies'],
    queryFn: procurementSupplierMasterChangeService.policies,
  });
  const requests = useQuery({
    queryKey: ['supplier-master-change-history'],
    queryFn: () =>
      procurementSupplierMasterChangeService.search({ page: 1, pageSize: 200 }),
  });
  const suppliers = useQuery({
    queryKey: ['supplier-master-change-suppliers'],
    queryFn: procurementSupplierMasterChangeService.suppliers,
  });
  const supplierDetail = useQuery({
    queryKey: ['supplier-master-change-supplier', partnerId],
    queryFn: () => procurementSupplierMasterChangeService.supplier(partnerId),
    enabled:
      Boolean(partnerId) && draft.resourceType === 'SupplierComplianceStatus',
  });
  const categories = useQuery({
    queryKey: ['supplier-master-change-categories'],
    queryFn: procurementSupplierMasterChangeService.categories,
  });

  const supplierRequests = useMemo(
    () =>
      (requests.data?.items ?? []).filter((item) =>
        supplierMasterResourceTypes.includes(
          item.resourceType as SupplierMasterResourceType
        )
      ),
    [requests.data]
  );
  const effectiveResources = useMemo(
    () =>
      new Set(
        (policies.data ?? [])
          .filter((item) => item.isEffective)
          .map((item) => item.resourceType)
      ),
    [policies.data]
  );
  const selectedProtected = effectiveResources.has(draft.resourceType);
  const complianceDetailReady =
    draft.resourceType !== 'SupplierComplianceStatus' ||
    supplierDetail.data?.id === partnerId;

  useEffect(() => {
    const detail = supplierDetail.data;
    if (
      draft.resourceType !== 'SupplierComplianceStatus' ||
      detail?.id !== partnerId ||
      changedFields.size > 0
    )
      return;
    setDraft((current) => hydrateSupplierComplianceDraft(current, detail));
  }, [changedFields.size, draft.resourceType, partnerId, supplierDetail.data]);

  const changeDraftField = (
    field: keyof SupplierMasterChangeDraft,
    value: SupplierMasterChangeDraft[keyof SupplierMasterChangeDraft]
  ) => {
    setDraft((current) => ({ ...current, [field]: value }));
    setChangedFields((current) => new Set(current).add(field));
  };

  const invalidate = () =>
    Promise.all([
      client.invalidateQueries({
        queryKey: ['supplier-master-change-summary'],
      }),
      client.invalidateQueries({
        queryKey: ['supplier-master-change-history'],
      }),
      client.invalidateQueries({
        queryKey: ['supplier-master-change-policies'],
      }),
    ]);

  const create = useMutation({
    mutationFn: async () => {
      if (!partnerId) throw new Error('Select a supplier.');
      if (!reason.trim()) throw new Error('Enter a business reason.');
      if (!selectedProtected)
        throw new Error(
          `${resourceLabels[draft.resourceType]} has no effective maker-checker policy.`
        );
      if (!complianceDetailReady)
        throw new Error(
          'Wait for the selected supplier compliance values to load.'
        );
      const patch = buildSupplierMasterPatch(draft, changedFields);
      if (!Object.keys(patch).length)
        throw new Error('Enter at least one proposed field value.');
      return procurementSupplierMasterChangeService.saveDraft({
        resourceType: draft.resourceType,
        targetId: partnerId,
        proposedChangesJson: JSON.stringify(patch),
        reason: reason.trim(),
        effectiveAtUtc,
        evidence: evidenceReference.trim()
          ? [
              {
                referenceKind: 'ExternalReference',
                reference: evidenceReference.trim(),
                label: 'Supplier master-change evidence',
                requirementKey: 'TDC-0308',
              },
            ]
          : [],
      });
    },
    onSuccess: async () => {
      setDraftOpen(false);
      await invalidate();
      toast({
        title: 'Supplier change staged',
        description:
          'The current values were captured as an immutable before snapshot.',
      });
    },
    onError: (error: Error) =>
      toast({
        title: 'Unable to stage supplier change',
        description: error.message,
        variant: 'destructive',
      }),
  });

  const lifecycle = useMutation({
    mutationFn: async () => {
      if (!action) throw new Error('Select a lifecycle action.');
      const { request, kind } = action;
      if (['approve', 'reject', 'cancel'].includes(kind) && !comment.trim())
        throw new Error('Enter an independent decision comment.');
      if (kind === 'submit')
        return procurementSupplierMasterChangeService.submit(
          request.id,
          request.rowVersion,
          comment || undefined
        );
      if (kind === 'revalidate')
        return procurementSupplierMasterChangeService.revalidate(
          request.id,
          request.rowVersion,
          comment || undefined
        );
      if (kind === 'approve')
        return procurementSupplierMasterChangeService.approve(
          request.id,
          request.rowVersion,
          comment
        );
      if (kind === 'reject')
        return procurementSupplierMasterChangeService.reject(
          request.id,
          request.rowVersion,
          comment
        );
      if (kind === 'apply')
        return procurementSupplierMasterChangeService.apply(
          request.id,
          request.rowVersion,
          comment || undefined
        );
      return procurementSupplierMasterChangeService.cancel(
        request.id,
        request.rowVersion,
        comment
      );
    },
    onSuccess: async (value) => {
      setAction(undefined);
      setComment('');
      await invalidate();
      toast({ title: `${value.requestNumber} is ${value.status}` });
    },
    onError: (error: Error) =>
      toast({
        title: 'Supplier control action failed',
        description: error.message,
        variant: 'destructive',
      }),
  });

  const loadingError =
    summary.isError ||
    policies.isError ||
    requests.isError ||
    suppliers.isError ||
    categories.isError;

  return (
    <div className="space-y-6" data-testid="supplier-master-changes-page">
      <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-start">
        <div>
          <h1 className="text-3xl font-bold">Supplier master changes</h1>
          <p className="mt-1 max-w-4xl text-muted-foreground">
            Stage sensitive supplier profile, bank, tax, beneficial ownership,
            category, and compliance changes for independent review before they
            replace active values.
          </p>
        </div>
        <div className="flex gap-2">
          <Button
            variant="outline"
            onClick={() => void invalidate()}
            disabled={requests.isFetching}
          >
            <RefreshCw
              className={`mr-2 h-4 w-4 ${
                requests.isFetching ? 'animate-spin' : ''
              }`}
            />
            Refresh
          </Button>
          <Button
            onClick={() => {
              setDraft(emptyDraft());
              setChangedFields(new Set());
              setPartnerId('');
              setReason('');
              setEffectiveAtUtc(today());
              setEvidenceReference('');
              setDraftOpen(true);
            }}
          >
            <Plus className="mr-2 h-4 w-4" />
            Stage supplier change
          </Button>
        </div>
      </div>

      <Alert className="border-emerald-500/40 bg-emerald-500/5">
        <ShieldCheck className="h-4 w-4" />
        <AlertTitle>Existing maker-checker control reused</AlertTitle>
        <AlertDescription>
          This workspace uses the shared TDC-0007 policy, workflow, evidence,
          revalidation, immutable snapshot, notification, and audit controls. It
          does not edit PR, PO, receipt, stock, or inventory transactions.
        </AlertDescription>
      </Alert>

      {loadingError && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Supplier control data unavailable</AlertTitle>
          <AlertDescription>
            Refresh after checking the API and your tenant procurement role.
          </AlertDescription>
        </Alert>
      )}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        <Metric
          label="Protected families"
          value={
            supplierMasterResourceTypes.filter((item) =>
              effectiveResources.has(item)
            ).length
          }
          detail="of six supplier families"
        />
        <Metric
          label="Draft"
          value={summary.data?.draftCount ?? 0}
          detail="maker preparation"
        />
        <Metric
          label="Pending"
          value={summary.data?.pendingApprovalCount ?? 0}
          detail="independent check"
        />
        <Metric
          label="Revalidation failed"
          value={summary.data?.revalidationFailedCount ?? 0}
          detail="fresh state required"
        />
        <Metric
          label="Applied"
          value={summary.data?.appliedCount ?? 0}
          detail="effective replacements"
        />
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Supplier change history</CardTitle>
          <CardDescription>
            Immutable request history is shown before new actions.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto rounded-lg border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Request</TableHead>
                  <TableHead>Supplier</TableHead>
                  <TableHead>Family</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Effective</TableHead>
                  <TableHead>Lineage</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {supplierRequests.map((item) => {
                  const allowed = requestActions(item);
                  return (
                    <TableRow key={item.id}>
                      <TableCell>
                        <p className="font-medium">{item.requestNumber}</p>
                        <p className="max-w-52 truncate text-xs text-muted-foreground">
                          {item.reason}
                        </p>
                      </TableCell>
                      <TableCell>{item.targetReference}</TableCell>
                      <TableCell>
                        {resourceLabels[
                          item.resourceType as SupplierMasterResourceType
                        ] ?? item.resourceType}
                      </TableCell>
                      <TableCell>
                        <Badge
                          variant="outline"
                          className={statusTone(item.status)}
                        >
                          {item.status.replace(/([A-Z])/g, ' $1').trim()}
                        </Badge>
                      </TableCell>
                      <TableCell>
                        {new Date(item.effectiveAtUtc).toLocaleDateString()}
                      </TableCell>
                      <TableCell>
                        <span className="font-mono text-xs">
                          {item.beforeHash.slice(0, 10)}…
                        </span>
                        {item.revalidationPassed && (
                          <CheckCircle2 className="ml-2 inline h-4 w-4 text-emerald-600" />
                        )}
                      </TableCell>
                      <TableCell>
                        <div className="flex flex-wrap justify-end gap-1">
                          {allowed.canSubmit && (
                            <ActionButton
                              label="Submit"
                              onClick={() =>
                                setAction({ kind: 'submit', request: item })
                              }
                            />
                          )}
                          {allowed.canDecide && (
                            <>
                              <ActionButton
                                label="Approve"
                                onClick={() =>
                                  setAction({ kind: 'approve', request: item })
                                }
                              />
                              <ActionButton
                                label="Reject"
                                onClick={() =>
                                  setAction({ kind: 'reject', request: item })
                                }
                              />
                            </>
                          )}
                          {allowed.canRevalidate && (
                            <ActionButton
                              label="Revalidate"
                              onClick={() =>
                                setAction({ kind: 'revalidate', request: item })
                              }
                            />
                          )}
                          {allowed.canApply && (
                            <ActionButton
                              label="Apply"
                              onClick={() =>
                                setAction({ kind: 'apply', request: item })
                              }
                            />
                          )}
                          {allowed.canCancel && (
                            <ActionButton
                              label="Cancel"
                              onClick={() =>
                                setAction({ kind: 'cancel', request: item })
                              }
                            />
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  );
                })}
                {!requests.isLoading && supplierRequests.length === 0 && (
                  <TableRow>
                    <TableCell
                      colSpan={7}
                      className="h-28 text-center text-muted-foreground"
                      data-testid="supplier-master-empty-state"
                    >
                      No supplier master-change requests have been recorded.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <Dialog open={draftOpen} onOpenChange={setDraftOpen}>
        <DialogContent className="max-h-[92vh] max-w-4xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Stage supplier master change</DialogTitle>
            <DialogDescription>
              Current compliance values are loaded from the selected supplier.
              Only fields you explicitly change are included in the immutable
              proposed patch.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Supplier">
              <Select
                value={partnerId}
                onValueChange={(value) => {
                  setPartnerId(value);
                  setChangedFields(new Set());
                  setDraft((current) => ({
                    ...emptyDraft(),
                    resourceType: current.resourceType,
                  }));
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select supplier" />
                </SelectTrigger>
                <SelectContent>
                  {(suppliers.data ?? []).map((item) => (
                    <SelectItem key={item.id} value={item.id}>
                      {item.partnerCode} — {item.partnerName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>
            <Field label="Protected family">
              <Select
                value={draft.resourceType}
                onValueChange={(value) => {
                  setChangedFields(new Set());
                  setDraft({
                    ...emptyDraft(),
                    resourceType: value as SupplierMasterResourceType,
                  });
                }}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {supplierMasterResourceTypes.map((item) => (
                    <SelectItem key={item} value={item}>
                      {resourceLabels[item]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>
          </div>

          {!selectedProtected && (
            <Alert className="border-amber-500/40 bg-amber-500/5">
              <AlertTriangle className="h-4 w-4" />
              <AlertTitle>Release configuration required</AlertTitle>
              <AlertDescription>
                Activate an effective {resourceLabels[draft.resourceType]}{' '}
                policy in Controlled master-data changes before staging this
                family.
              </AlertDescription>
            </Alert>
          )}

          {draft.resourceType === 'SupplierComplianceStatus' &&
          (!partnerId || !complianceDetailReady) ? (
            <Alert
              className={
                supplierDetail.isError
                  ? 'border-destructive/40 bg-destructive/5'
                  : undefined
              }
            >
              <RefreshCw
                className={`h-4 w-4 ${
                  supplierDetail.isFetching ? 'animate-spin' : ''
                }`}
              />
              <AlertTitle>
                {supplierDetail.isError
                  ? 'Unable to load supplier values'
                  : partnerId
                    ? 'Loading current supplier values'
                    : 'Select a supplier'}
              </AlertTitle>
              <AlertDescription>
                {supplierDetail.isError
                  ? 'Refresh or select the supplier again before staging a compliance change.'
                  : 'Compliance controls remain unavailable until the authoritative current values are loaded.'}
              </AlertDescription>
            </Alert>
          ) : (
            <ResourceFields
              draft={draft}
              onChange={changeDraftField}
              categories={categories.data ?? []}
            />
          )}

          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Effective date">
              <Input
                type="date"
                value={effectiveAtUtc}
                onChange={(event) => setEffectiveAtUtc(event.target.value)}
              />
            </Field>
            <Field label="Shared evidence reference">
              <Input
                value={evidenceReference}
                onChange={(event) => setEvidenceReference(event.target.value)}
                placeholder="Evidence ID, URL, or register reference"
              />
            </Field>
          </div>
          <Field label="Business reason">
            <Textarea
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              placeholder="Why this controlled replacement is required"
            />
          </Field>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDraftOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => create.mutate()}
              disabled={
                create.isPending ||
                !selectedProtected ||
                !complianceDetailReady ||
                changedFields.size === 0
              }
            >
              Save Draft
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={Boolean(action)} onOpenChange={() => setAction(undefined)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {action?.kind
                ? `${action.kind[0].toUpperCase()}${action.kind.slice(1)} ${
                    action.request.requestNumber
                  }`
                : 'Supplier control action'}
            </DialogTitle>
            <DialogDescription>
              Authorization, separation of duties, linked workflow outcome, row
              version, and current supplier snapshot are enforced by the server.
            </DialogDescription>
          </DialogHeader>
          <Field label="Comment">
            <Textarea
              value={comment}
              onChange={(event) => setComment(event.target.value)}
              placeholder="Decision or lifecycle comment"
            />
          </Field>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAction(undefined)}>
              Cancel
            </Button>
            <Button
              onClick={() => lifecycle.mutate()}
              disabled={lifecycle.isPending}
            >
              Confirm
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function ResourceFields({
  draft,
  onChange,
  categories,
}: {
  draft: SupplierMasterChangeDraft;
  onChange: (
    field: keyof SupplierMasterChangeDraft,
    value: SupplierMasterChangeDraft[keyof SupplierMasterChangeDraft]
  ) => void;
  categories: Array<{ id: string; categoryCode: string; categoryName: string }>;
}) {
  const text = (key: keyof SupplierMasterChangeDraft) => ({
    value: String(draft[key] ?? ''),
    onChange: (
      event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>
    ) => onChange(key, event.target.value),
  });

  if (draft.resourceType === 'SupplierProfile')
    return (
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Partner name">
          <Input {...text('partnerName')} />
        </Field>
        <Field label="Legal name">
          <Input {...text('legalName')} />
        </Field>
        <Field label="Primary email">
          <Input type="email" {...text('primaryEmail')} />
        </Field>
        <Field label="Primary phone">
          <Input {...text('primaryPhone')} />
        </Field>
        <div className="sm:col-span-2">
          <Field label="Physical address">
            <Textarea {...text('physicalAddress')} />
          </Field>
        </div>
      </div>
    );
  if (draft.resourceType === 'SupplierBankDetails')
    return (
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Bank name">
          <Input {...text('bankName')} />
        </Field>
        <Field label="Account name">
          <Input {...text('bankAccountName')} />
        </Field>
        <Field label="Account number">
          <Input {...text('bankAccountNumber')} />
        </Field>
        <Field label="Branch">
          <Input {...text('bankBranch')} />
        </Field>
        <Field label="SWIFT code">
          <Input {...text('bankSwiftCode')} />
        </Field>
        <Field label="IBAN">
          <Input {...text('bankIBAN')} />
        </Field>
      </div>
    );
  if (draft.resourceType === 'SupplierTaxDetails')
    return (
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Tax identification number">
          <Input {...text('taxIdentificationNumber')} />
        </Field>
        <Field label="VAT number">
          <Input {...text('vatNumber')} />
        </Field>
        <Field label="Exemption certificate">
          <Input {...text('taxExemptionNumber')} />
        </Field>
        <Field label="Exemption expiry">
          <Input type="date" {...text('taxExemptionExpiry')} />
        </Field>
        <CheckField
          label="Tax exempt"
          checked={draft.isTaxExempt}
          onChange={(checked) => onChange('isTaxExempt', checked)}
        />
      </div>
    );
  if (draft.resourceType === 'SupplierOwnershipDetails')
    return (
      <div className="space-y-4">
        {draft.owners.map((item, index) => (
          <Card key={index}>
            <CardContent className="grid gap-4 pt-5 sm:grid-cols-2">
              <Field label="Owner name">
                <Input
                  value={item.name}
                  onChange={(event) =>
                    onChange(
                      'owners',
                      updateOwner(
                        draft.owners,
                        index,
                        'name',
                        event.target.value
                      )
                    )
                  }
                />
              </Field>
              <Field label="Ownership %">
                <Input
                  type="number"
                  min="0.01"
                  max="100"
                  value={item.ownershipPercent}
                  onChange={(event) =>
                    onChange(
                      'owners',
                      updateOwner(
                        draft.owners,
                        index,
                        'ownershipPercent',
                        event.target.value
                      )
                    )
                  }
                />
              </Field>
              <Field label="Nationality">
                <Input
                  value={item.nationality}
                  onChange={(event) =>
                    onChange(
                      'owners',
                      updateOwner(
                        draft.owners,
                        index,
                        'nationality',
                        event.target.value
                      )
                    )
                  }
                />
              </Field>
              <Field label="Registration / ID">
                <Input
                  value={item.registrationNumber}
                  onChange={(event) =>
                    onChange(
                      'owners',
                      updateOwner(
                        draft.owners,
                        index,
                        'registrationNumber',
                        event.target.value
                      )
                    )
                  }
                />
              </Field>
              <CheckField
                label="Politically exposed"
                checked={item.politicallyExposed}
                onChange={(checked) =>
                  onChange(
                    'owners',
                    updateOwner(
                      draft.owners,
                      index,
                      'politicallyExposed',
                      checked
                    )
                  )
                }
              />
              {draft.owners.length > 1 && (
                <Button
                  type="button"
                  variant="outline"
                  onClick={() =>
                    onChange(
                      'owners',
                      draft.owners.filter(
                        (_, ownerIndex) => ownerIndex !== index
                      )
                    )
                  }
                >
                  <Trash2 className="mr-2 h-4 w-4" />
                  Remove owner
                </Button>
              )}
            </CardContent>
          </Card>
        ))}
        <div className="flex flex-wrap items-end justify-between gap-3">
          <Field label="Ownership verified date">
            <Input type="date" {...text('ownershipVerifiedAtUtc')} />
          </Field>
          <Button
            type="button"
            variant="outline"
            onClick={() =>
              onChange('owners', [
                ...draft.owners,
                { ...owner(), ownershipPercent: '' },
              ])
            }
          >
            <Plus className="mr-2 h-4 w-4" />
            Add owner
          </Button>
        </div>
      </div>
    );
  if (draft.resourceType === 'SupplierCategoryAssignments')
    return (
      <div className="grid gap-3 rounded-lg border p-4 sm:grid-cols-2">
        {categories.map((item) => (
          <CheckField
            key={item.id}
            label={`${item.categoryCode} — ${item.categoryName}`}
            checked={draft.categoryIds.includes(item.id)}
            onChange={(checked) =>
              onChange(
                'categoryIds',
                checked
                  ? [...draft.categoryIds, item.id]
                  : draft.categoryIds.filter((id) => id !== item.id)
              )
            }
          />
        ))}
        {!categories.length && (
          <p className="text-sm text-muted-foreground">
            No active current-tenant supplier categories are available.
          </p>
        )}
      </div>
    );
  return (
    <div className="grid gap-4 sm:grid-cols-2">
      <SelectField
        label="Registration status"
        value={draft.registrationStatus}
        values={['Approved', 'Suspended', 'Blacklisted', 'Inactive']}
        onChange={(value) => onChange('registrationStatus', value)}
      />
      <SelectField
        label="Approval status"
        value={draft.approvalStatus}
        values={['Approved', 'Pending', 'Rejected']}
        onChange={(value) => onChange('approvalStatus', value)}
      />
      <SelectField
        label="Compliance status"
        value={draft.complianceStatus}
        values={[
          'Compliant',
          'Conditional',
          'NonCompliant',
          'Suspended',
          'PendingReview',
        ]}
        onChange={(value) => onChange('complianceStatus', value)}
      />
      <SelectField
        label="Risk level"
        value={draft.riskLevel}
        values={['Low', 'Medium', 'High', 'Critical']}
        onChange={(value) => onChange('riskLevel', value)}
      />
      <CheckField
        label="Supplier active"
        checked={draft.isActive}
        onChange={(checked) => onChange('isActive', checked)}
      />
      <CheckField
        label="Blacklisted"
        checked={draft.isBlacklisted}
        onChange={(checked) => onChange('isBlacklisted', checked)}
      />
      <Field label="Blacklist reason">
        <Input {...text('blacklistReason')} />
      </Field>
      <Field label="Blacklist date">
        <Input type="date" {...text('blacklistDate')} />
      </Field>
      <Field label="Blacklist expiry">
        <Input type="date" {...text('blacklistExpiryDate')} />
      </Field>
      <Field label="Compliance review date">
        <Input type="date" {...text('complianceReviewDateUtc')} />
      </Field>
      <Field label="Compliance valid until">
        <Input type="date" {...text('complianceValidUntilUtc')} />
      </Field>
      <div className="sm:col-span-2">
        <Field label="Compliance notes">
          <Textarea {...text('complianceNotes')} />
        </Field>
      </div>
    </div>
  );
}

function updateOwner(
  owners: BeneficialOwnerDraft[],
  index: number,
  key: keyof BeneficialOwnerDraft,
  value: string | boolean
): BeneficialOwnerDraft[] {
  return owners.map((item, ownerIndex) =>
    ownerIndex === index ? { ...item, [key]: value } : item
  );
}

function Field({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-1.5">
      <Label>{label}</Label>
      {children}
    </div>
  );
}

function CheckField({
  label,
  checked,
  onChange,
}: {
  label: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
}) {
  return (
    <label className="flex items-center gap-2 rounded-md border p-3 text-sm">
      <Checkbox
        checked={checked}
        onCheckedChange={(value) => onChange(value === true)}
      />
      {label}
    </label>
  );
}

function SelectField({
  label,
  value,
  values,
  onChange,
}: {
  label: string;
  value: string;
  values: string[];
  onChange: (value: string) => void;
}) {
  return (
    <Field label={label}>
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {values.map((item) => (
            <SelectItem key={item} value={item}>
              {item}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </Field>
  );
}

function Metric({
  label,
  value,
  detail,
}: {
  label: string;
  value: number;
  detail: string;
}) {
  return (
    <Card>
      <CardContent className="pt-5">
        <p className="text-sm text-muted-foreground">{label}</p>
        <p className="mt-1 text-2xl font-semibold">{value}</p>
        <p className="text-xs text-muted-foreground">{detail}</p>
      </CardContent>
    </Card>
  );
}

function ActionButton({
  label,
  onClick,
}: {
  label: string;
  onClick: () => void;
}) {
  return (
    <Button size="sm" variant="outline" onClick={onClick}>
      {label}
    </Button>
  );
}
