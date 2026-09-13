'use client';

import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
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
import { Textarea } from '@/components/ui/textarea';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { CheckProviderPicker } from '@/components/hr/recruitment/CheckProviderPicker';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { formatDate, formatDateTime, humanizeEnum } from '@/lib/hr/attendance-format';
import { preEmploymentCheckService, preEmploymentCheckTemplateService } from '@/services/hr/offers.service';
import {
  CHECK_ITEM_STATUSES,
  OUTSTANDING_CHECK_ITEM_STATUSES,
  PRE_EMPLOYMENT_CHECK_TYPES,
  REFERENCE_RATINGS,
  REFERENCE_RESPONSE_METHODS,
  type CheckItemStatus,
  type CreatePreEmploymentCheckItem,
  type PreEmploymentCheckItem as CheckItem,
  type PreEmploymentCheckType,
} from '@/types/hr/offers';

const blankItem = (): CreatePreEmploymentCheckItem => ({
  checkType: 'BackgroundCheck',
  name: '',
  serviceProviderName: '',
  serviceProviderSupplierId: null,
  instructions: '',
  isMandatory: true,
  isBlockingOnFail: true,
  expectedDays: null,
});

/**
 * Pre-employment checks against a conditional offer, embedded on the offer detail.
 *
 * ⚠ **The rule that matters here: recording a Pass/Fail result overrides whatever status was
 * chosen.** The server derives Status from Passed whenever Passed is set — Verified when true,
 * Failed when false — so the status picker only has effect when the result is left "Not yet
 * recorded". Waiving a check or marking it Not Applicable is a separate action from recording a
 * result, and both count as *settled* for completion, not as a failure.
 *
 * Completing the check is refused while any mandatory or blocking item is still outstanding
 * (Pending/Requested); a genuine blocking failure lands the whole check on Failed, which is
 * terminal. When it completes clean, a `ConditionallyAccepted` offer moves to `ChecksCleared`.
 */
export function PreEmploymentChecksPanel({
  offerId,
  offerIsConditional,
  canManage,
}: {
  offerId: string;
  offerIsConditional: boolean;
  canManage: boolean;
}) {
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [creatingCheck, setCreatingCheck] = useState(false);
  const [addingItem, setAddingItem] = useState(false);
  const [itemForm, setItemForm] = useState<CreatePreEmploymentCheckItem>(blankItem);
  const [applyingTemplate, setApplyingTemplate] = useState(false);
  const [templateId, setTemplateId] = useState('');
  const [overwriteExisting, setOverwriteExisting] = useState(false);

  const check = useQuery({
    queryKey: ['hr', 'offer-check', offerId],
    queryFn: () => preEmploymentCheckService.getByOffer(offerId),
    enabled: !!offerId,
  });

  const checkId = check.data?.id ?? null;

  const detail = useQuery({
    queryKey: ['hr', 'pre-employment-check', checkId],
    queryFn: () => preEmploymentCheckService.getWithItems(checkId as string),
    enabled: !!checkId,
  });

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'offer-check', offerId] });
    if (checkId) await queryClient.invalidateQueries({ queryKey: ['hr', 'pre-employment-check', checkId] });
    // The offer itself carries preEmploymentCheckStatus, which moves when the check completes.
    await queryClient.invalidateQueries({ queryKey: ['hr', 'offers', offerId] });
  };

  const createCheck = useMutation({
    mutationFn: () => preEmploymentCheckService.create({ jobOfferId: offerId, items: [] }),
    onSuccess: async () => {
      await invalidate();
      setCreatingCheck(false);
      toast({ title: 'Pre-employment checks started' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not start checks', description: e?.message, variant: 'destructive' }),
  });

  const addItem = useMutation({
    mutationFn: () => preEmploymentCheckService.addItem(checkId as string, itemForm),
    onSuccess: async () => {
      await invalidate();
      setAddingItem(false);
      setItemForm(blankItem());
      toast({ title: 'Check added' });
    },
    onError: (e: any) => toast({ title: 'Could not add it', description: e?.message, variant: 'destructive' }),
  });

  const removeItem = useMutation({
    mutationFn: (itemId: string) => preEmploymentCheckService.removeItem(itemId),
    onSuccess: async () => {
      await invalidate();
      toast({ title: 'Check removed' });
    },
    onError: (e: any) => toast({ title: 'Could not remove it', description: e?.message, variant: 'destructive' }),
  });

  const complete = useMutation({
    mutationFn: () => preEmploymentCheckService.complete(checkId as string),
    onSuccess: async () => {
      await invalidate();
      toast({ title: 'Pre-employment check completed' });
    },
    onError: (e: any) =>
      toast({ title: 'Cannot complete yet', description: e?.message, variant: 'destructive' }),
  });

  const templates = useQuery({
    queryKey: ['hr', 'pre-employment-check-templates'],
    queryFn: () => preEmploymentCheckTemplateService.getAll(),
    enabled: applyingTemplate,
  });

  const applyTemplate = useMutation({
    mutationFn: () =>
      preEmploymentCheckService.applyTemplate(checkId as string, { templateId, overwriteExisting }),
    onSuccess: async () => {
      await invalidate();
      setApplyingTemplate(false);
      setTemplateId('');
      setOverwriteExisting(false);
      toast({ title: 'Template applied' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not apply the template', description: e?.message, variant: 'destructive' }),
  });

  if (check.isLoading) {
    return (
      <Card>
        <CardContent className="py-10 text-center text-sm text-muted-foreground">Loading…</CardContent>
      </Card>
    );
  }

  if (!check.data) {
    return (
      <Card>
        <CardContent className="p-0">
          <EmptyState
            title="No pre-employment checks yet"
            description={
              offerIsConditional
                ? 'This offer is conditional on checks clearing. Start them once the candidate has responded.'
                : 'Checks can still be recorded even though this offer is not marked conditional.'
            }
            action={
              canManage && (
                <Button onClick={() => setCreatingCheck(true)}>Start pre-employment checks</Button>
              )
            }
          />
        </CardContent>
      </Card>
    );
  }

  const c = check.data;
  const items = detail.data?.items ?? [];
  const canComplete = c.overallStatus !== 'Completed' && c.overallStatus !== 'Failed';

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="flex flex-row items-start justify-between space-y-0 pb-3">
          <div>
            <CardTitle className="text-base">Pre-employment checks</CardTitle>
            <p className="mt-1 text-sm text-muted-foreground">
              {c.completedItems} of {c.totalItems} recorded · {c.passedItems} passed
              {c.failedItems > 0 ? ` · ${c.failedItems} failed` : ''}
              {c.coordinatedByName ? ` · Coordinated by ${c.coordinatedByName}` : ''}
            </p>
          </div>
          <div className="flex items-center gap-2">
            <StatusBadge status={c.overallStatus} />
            {canManage && canComplete && (
              <Button size="sm" onClick={() => complete.mutate()} disabled={complete.isPending}>
                {complete.isPending ? 'Completing…' : 'Complete check'}
              </Button>
            )}
          </div>
        </CardHeader>
        {c.notes && (
          <CardContent className="pt-0">
            <p className="text-sm text-muted-foreground">{c.notes}</p>
          </CardContent>
        )}
      </Card>

      {canManage && (
        <div className="flex flex-wrap gap-2">
          <Button size="sm" variant="outline" onClick={() => setAddingItem(true)}>
            Add a check
          </Button>
          <Button size="sm" variant="outline" onClick={() => setApplyingTemplate(true)}>
            Apply a template
          </Button>
        </div>
      )}

      {items.length === 0 ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              title="No checks recorded"
              description="Add individual checks, or apply a template to seed a standard set."
            />
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardContent className="p-0">
            <Accordion type="multiple" className="px-4">
              {items.map((item) => (
                <ItemRow
                  key={item.id}
                  checkId={c.id}
                  item={item}
                  canManage={canManage}
                  onRemove={() => removeItem.mutate(item.id)}
                  afterChange={invalidate}
                />
              ))}
            </Accordion>
          </CardContent>
        </Card>
      )}

      <ConfirmationDialog
        open={creatingCheck}
        onOpenChange={setCreatingCheck}
        title="Start pre-employment checks"
        description="This raises the check record against the offer. Add individual checks or apply a template next."
        confirmText={createCheck.isPending ? 'Starting…' : 'Start'}
        onConfirm={async () => {
          await createCheck.mutateAsync();
          return true;
        }}
      />

      {/* ── add item ─────────────────────────────────────────────────────── */}
      <Dialog open={addingItem} onOpenChange={setAddingItem}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add a check</DialogTitle>
          </DialogHeader>
          <div className="grid gap-4 py-2">
            <div className="space-y-1.5">
              <Label>Type</Label>
              <Select
                value={itemForm.checkType}
                onValueChange={(v) => setItemForm({ ...itemForm, checkType: v as PreEmploymentCheckType })}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {PRE_EMPLOYMENT_CHECK_TYPES.map((t) => (
                    <SelectItem key={t} value={t}>
                      {humanizeEnum(t)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="itemName">Name (optional)</Label>
              <Input
                id="itemName"
                value={itemForm.name ?? ''}
                onChange={(e) => setItemForm({ ...itemForm, name: e.target.value })}
                placeholder="Defaults to the check type"
              />
            </div>
            {/* Round 3, lane G (D-14): the provider from the suppliers set up for this check type;
                another supplier from the register, or a typed name when it is not a supplier. */}
            <CheckProviderPicker
              checkType={itemForm.checkType}
              supplierId={itemForm.serviceProviderSupplierId ?? null}
              name={itemForm.serviceProviderName ?? ''}
              onChange={({ supplierId, name }) =>
                setItemForm({ ...itemForm, serviceProviderSupplierId: supplierId || null, serviceProviderName: name })
              }
            />
            <div className="space-y-1.5">
              <Label htmlFor="itemInstructions">Instructions</Label>
              <Textarea
                id="itemInstructions"
                rows={2}
                value={itemForm.instructions ?? ''}
                onChange={(e) => setItemForm({ ...itemForm, instructions: e.target.value })}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="expectedDays">Expected turnaround (days)</Label>
              <Input
                id="expectedDays"
                type="number"
                min={0}
                value={itemForm.expectedDays ?? ''}
                onChange={(e) =>
                  setItemForm({
                    ...itemForm,
                    expectedDays: e.target.value === '' ? null : Number(e.target.value),
                  })
                }
                className="w-32"
              />
            </div>
            <div className="flex items-center gap-2">
              <Checkbox
                id="isMandatory"
                checked={itemForm.isMandatory}
                onCheckedChange={(checked) => setItemForm({ ...itemForm, isMandatory: checked === true })}
              />
              <Label htmlFor="isMandatory" className="font-normal">
                Mandatory
              </Label>
            </div>
            <div className="flex items-center gap-2">
              <Checkbox
                id="isBlocking"
                checked={itemForm.isBlockingOnFail}
                onCheckedChange={(checked) => setItemForm({ ...itemForm, isBlockingOnFail: checked === true })}
              />
              <Label htmlFor="isBlocking" className="font-normal">
                Blocks clearance on failure
              </Label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddingItem(false)}>
              Cancel
            </Button>
            <Button onClick={() => addItem.mutate()} disabled={addItem.isPending}>
              {addItem.isPending ? 'Adding…' : 'Add'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── apply template ───────────────────────────────────────────────── */}
      <Dialog open={applyingTemplate} onOpenChange={setApplyingTemplate}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Apply a template</DialogTitle>
            <DialogDescription>
              Seeds check items from the template. Existing items of the same type are skipped unless
              you choose to overwrite them.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <Label>Template</Label>
              <Select value={templateId} onValueChange={setTemplateId}>
                <SelectTrigger>
                  <SelectValue placeholder="Choose a template" />
                </SelectTrigger>
                <SelectContent>
                  {(templates.data ?? []).map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name} ({t.itemCount})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="flex items-center gap-2">
              <Checkbox
                id="overwriteExisting"
                checked={overwriteExisting}
                onCheckedChange={(checked) => setOverwriteExisting(checked === true)}
              />
              <Label htmlFor="overwriteExisting" className="font-normal">
                Overwrite items that already exist for the same check type
              </Label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setApplyingTemplate(false)}>
              Cancel
            </Button>
            <Button onClick={() => applyTemplate.mutate()} disabled={!templateId || applyTemplate.isPending}>
              {applyTemplate.isPending ? 'Applying…' : 'Apply'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function ItemRow({
  checkId,
  item,
  canManage,
  onRemove,
  afterChange,
}: {
  checkId: string;
  item: CheckItem;
  canManage: boolean;
  onRemove: () => void;
  afterChange: () => Promise<void>;
}) {
  const { toast } = useToast();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [editing, setEditing] = useState(false);
  const [status, setStatus] = useState<CheckItemStatus>(item.status);
  const [result, setResult] = useState<'unset' | 'pass' | 'fail'>(
    item.passed === true ? 'pass' : item.passed === false ? 'fail' : 'unset',
  );
  const [remarks, setRemarks] = useState(item.remarks ?? '');
  const [requestedDate, setRequestedDate] = useState(item.requestedDate?.slice(0, 10) ?? '');
  const [receivedDate, setReceivedDate] = useState(item.receivedDate?.slice(0, 10) ?? '');
  const [reviewedById, setReviewedById] = useState<string | null>(item.reviewedById ?? null);
  const [reviewedByLabel, setReviewedByLabel] = useState<string | null>(item.reviewedByName ?? null);

  const isOutstanding = OUTSTANDING_CHECK_ITEM_STATUSES.includes(item.status);

  const save = useMutation({
    mutationFn: () =>
      preEmploymentCheckService.updateItem(item.id, {
        status,
        passed: result === 'unset' ? null : result === 'pass',
        remarks: remarks.trim() || null,
        requestedDate: requestedDate || null,
        receivedDate: receivedDate || null,
        reviewedById,
        reviewedDate: reviewedById ? new Date().toISOString() : null,
      }),
    onSuccess: async () => {
      await afterChange();
      setEditing(false);
      toast({ title: 'Saved' });
    },
    onError: (e: any) => toast({ title: 'Could not save', description: e?.message, variant: 'destructive' }),
  });

  const upload = useMutation({
    mutationFn: (file: File) => preEmploymentCheckService.uploadItemDocument(item.id, file),
    onSuccess: async () => {
      await afterChange();
      if (fileInputRef.current) fileInputRef.current.value = '';
      toast({ title: 'Evidence uploaded' });
    },
    onError: (e: any) =>
      toast({ title: 'Upload refused', description: e?.message, variant: 'destructive' }),
  });

  const download = async () => {
    try {
      await preEmploymentCheckService.downloadItemDocument(item.id, item.documentFileName ?? 'evidence');
    } catch (e: any) {
      toast({ title: 'Download failed', description: e?.message, variant: 'destructive' });
    }
  };

  return (
    <AccordionItem value={item.id}>
      <AccordionTrigger className="hover:no-underline">
        <div className="flex flex-1 items-center justify-between pr-4 text-left">
          <div>
            <span className="font-medium">{item.displayName}</span>
            <span className="ml-2 text-xs text-muted-foreground">
              {item.isMandatory && 'Mandatory'}
              {item.isMandatory && item.isBlockingOnFail && ' · '}
              {item.isBlockingOnFail && 'Blocking'}
            </span>
          </div>
          <StatusBadge status={item.status} />
        </div>
      </AccordionTrigger>
      <AccordionContent className="space-y-4">
        <div className="grid grid-cols-2 gap-x-6 gap-y-2 text-sm md:grid-cols-3">
          <div>
            <p className="text-xs text-muted-foreground">Service provider</p>
            <p>{item.serviceProviderName || '—'}</p>
          </div>
          <div>
            <p className="text-xs text-muted-foreground">Requested</p>
            <p>{formatDate(item.requestedDate)}</p>
          </div>
          <div>
            <p className="text-xs text-muted-foreground">Received</p>
            <p>{formatDate(item.receivedDate)}</p>
          </div>
          <div>
            <p className="text-xs text-muted-foreground">Reviewed by</p>
            <p>{item.reviewedByName || '—'}</p>
          </div>
          <div>
            <p className="text-xs text-muted-foreground">Result</p>
            <p>{item.passed === true ? 'Passed' : item.passed === false ? 'Failed' : 'Not yet recorded'}</p>
          </div>
          <div>
            <p className="text-xs text-muted-foreground">Evidence</p>
            {item.hasDocument ? (
              <Button variant="link" size="sm" className="h-auto p-0" onClick={download}>
                {item.documentFileName || 'Download'}
              </Button>
            ) : (
              <p>None</p>
            )}
          </div>
        </div>
        {item.instructions && (
          <p className="text-sm text-muted-foreground">
            <span className="font-medium">Instructions: </span>
            {item.instructions}
          </p>
        )}
        {item.remarks && (
          <p className="text-sm text-muted-foreground">
            <span className="font-medium">Remarks: </span>
            {item.remarks}
          </p>
        )}

        {canManage && (
          <div className="flex flex-wrap items-center gap-2 border-t pt-3">
            <Button size="sm" variant="outline" onClick={() => setEditing(true)}>
              Record result
            </Button>
            <input
              ref={fileInputRef}
              type="file"
              className="hidden"
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) upload.mutate(file);
              }}
            />
            <Button
              size="sm"
              variant="outline"
              onClick={() => fileInputRef.current?.click()}
              disabled={upload.isPending}
            >
              {upload.isPending ? 'Uploading…' : 'Upload evidence'}
            </Button>
            <Button size="sm" variant="ghost" onClick={onRemove}>
              Remove
            </Button>
          </div>
        )}
        {isOutstanding && (item.isMandatory || item.isBlockingOnFail) && (
          <p className="text-xs text-muted-foreground">
            This holds up completion of the whole check until a result, a waiver, or Not Applicable is
            recorded.
          </p>
        )}

        {item.checkType === 'ReferenceCheck' && (
          <ReferenceResponsesSection checkItemId={item.id} canManage={canManage} afterChange={afterChange} />
        )}
      </AccordionContent>

      <Dialog open={editing} onOpenChange={setEditing}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record result — {item.displayName}</DialogTitle>
            <DialogDescription>
              Recording Passed or Failed sets the status to Verified or Failed automatically. Leave
              the result as &ldquo;Not yet recorded&rdquo; to set Pending, Requested, Received, Waived
              or Not Applicable instead.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2">
            <div className="space-y-1.5">
              <Label>Result</Label>
              <Select value={result} onValueChange={(v) => setResult(v as typeof result)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="unset">Not yet recorded</SelectItem>
                  <SelectItem value="pass">Passed</SelectItem>
                  <SelectItem value="fail">Failed</SelectItem>
                </SelectContent>
              </Select>
            </div>
            {result === 'unset' && (
              <div className="space-y-1.5">
                <Label>Status</Label>
                <Select value={status} onValueChange={(v) => setStatus(v as CheckItemStatus)}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {CHECK_ITEM_STATUSES.filter((s) => s !== 'Verified' && s !== 'Failed').map((s) => (
                      <SelectItem key={s} value={s}>
                        {humanizeEnum(s)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label htmlFor="requestedDate">Requested</Label>
                <Input
                  id="requestedDate"
                  type="date"
                  value={requestedDate}
                  onChange={(e) => setRequestedDate(e.target.value)}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="receivedDate">Received</Label>
                <Input
                  id="receivedDate"
                  type="date"
                  value={receivedDate}
                  onChange={(e) => setReceivedDate(e.target.value)}
                />
              </div>
            </div>
            <div className="space-y-1.5">
              <Label>Reviewed by</Label>
              <EmployeePicker
                value={reviewedById}
                initialLabel={reviewedByLabel}
                onChange={(id, label) => {
                  setReviewedById(id);
                  setReviewedByLabel(label);
                }}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="itemRemarks">Remarks</Label>
              <Textarea id="itemRemarks" rows={3} value={remarks} onChange={(e) => setRemarks(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditing(false)}>
              Cancel
            </Button>
            <Button onClick={() => save.mutate()} disabled={save.isPending}>
              {save.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AccordionItem>
  );
}

const blankReferenceForm = () => ({
  refereeName: '',
  refereeOrganisation: '',
  refereePosition: '',
  refereeEmail: '',
  refereePhone: '',
  responseMethod: 'Email' as const,
  overallRating: null as (typeof REFERENCE_RATINGS)[number] | null,
  comments: '',
  wouldRehire: null as boolean | null,
  confirmedDatesOfEmployment: null as boolean | null,
  confirmedPositionHeld: null as boolean | null,
  confirmedReasonForLeaving: null as boolean | null,
});

/**
 * Referee responses behind a Reference Check item. A distinct sub-collection rather than a field on
 * the item itself — a check may (and often does) contact more than one referee.
 */
function ReferenceResponsesSection({
  checkItemId,
  canManage,
  afterChange,
}: {
  checkItemId: string;
  canManage: boolean;
  afterChange: () => Promise<void>;
}) {
  const { toast } = useToast();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [adding, setAdding] = useState(false);
  const [uploadingFor, setUploadingFor] = useState<string | null>(null);
  const [form, setForm] = useState(blankReferenceForm);

  const responses = useQuery({
    queryKey: ['hr', 'reference-responses', checkItemId],
    queryFn: () => preEmploymentCheckService.getReferenceResponses(checkItemId),
    enabled: !!checkItemId,
  });

  const refresh = async () => {
    await responses.refetch();
    await afterChange();
  };

  const add = useMutation({
    mutationFn: () =>
      preEmploymentCheckService.addReferenceResponse(checkItemId, {
        refereeName: form.refereeName.trim(),
        refereeOrganisation: form.refereeOrganisation.trim(),
        refereePosition: form.refereePosition.trim(),
        refereeEmail: form.refereeEmail.trim(),
        refereePhone: form.refereePhone.trim() || null,
        responseMethod: form.responseMethod,
        overallRating: form.overallRating,
        comments: form.comments.trim() || null,
        wouldRehire: form.wouldRehire,
        confirmedDatesOfEmployment: form.confirmedDatesOfEmployment,
        confirmedPositionHeld: form.confirmedPositionHeld,
        confirmedReasonForLeaving: form.confirmedReasonForLeaving,
      }),
    onSuccess: async () => {
      await refresh();
      setAdding(false);
      setForm(blankReferenceForm());
      toast({ title: 'Reference recorded' });
    },
    onError: (e: any) => toast({ title: 'Could not save', description: e?.message, variant: 'destructive' }),
  });

  const upload = useMutation({
    mutationFn: ({ responseId, file }: { responseId: string; file: File }) =>
      preEmploymentCheckService.uploadReferenceDocument(responseId, file),
    onSuccess: async () => {
      await refresh();
      setUploadingFor(null);
      if (fileInputRef.current) fileInputRef.current.value = '';
      toast({ title: 'Written reference uploaded' });
    },
    onError: (e: any) =>
      toast({ title: 'Upload refused', description: e?.message, variant: 'destructive' }),
  });

  const download = async (responseId: string, fileName: string | null | undefined) => {
    try {
      await preEmploymentCheckService.downloadReferenceDocument(responseId, fileName ?? 'reference');
    } catch (e: any) {
      toast({ title: 'Download failed', description: e?.message, variant: 'destructive' });
    }
  };

  const rows = responses.data ?? [];

  return (
    <div className="space-y-3 border-t pt-3">
      <div className="flex items-center justify-between">
        <p className="text-sm font-medium">Referee responses</p>
        {canManage && (
          <Button size="sm" variant="outline" onClick={() => setAdding(true)}>
            Record a reference
          </Button>
        )}
      </div>

      {rows.length === 0 ? (
        <p className="text-sm text-muted-foreground">No referee has responded yet.</p>
      ) : (
        <div className="space-y-2">
          {rows.map((r) => (
            <div key={r.id} className="rounded-md border p-3 text-sm">
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                  <p className="font-medium">{r.refereeName}</p>
                  <p className="text-xs text-muted-foreground">
                    {r.refereePosition} · {r.refereeOrganisation}
                  </p>
                </div>
                <div className="flex items-center gap-2 text-xs text-muted-foreground">
                  <span>{humanizeEnum(r.responseMethod)}</span>
                  {r.overallRating && <StatusBadge status={r.overallRating} />}
                </div>
              </div>
              {r.comments && <p className="mt-2 whitespace-pre-wrap">{r.comments}</p>}
              <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
                {r.wouldRehire != null && <span>Would rehire: {r.wouldRehire ? 'Yes' : 'No'}</span>}
                {r.confirmedDatesOfEmployment != null && (
                  <span>Dates confirmed: {r.confirmedDatesOfEmployment ? 'Yes' : 'No'}</span>
                )}
                {r.confirmedPositionHeld != null && (
                  <span>Position confirmed: {r.confirmedPositionHeld ? 'Yes' : 'No'}</span>
                )}
                {r.responseDate && <span>Responded {formatDateTime(r.responseDate)}</span>}
              </div>
              <div className="mt-2 flex items-center gap-2">
                {r.hasDocument ? (
                  <Button
                    variant="link"
                    size="sm"
                    className="h-auto p-0"
                    onClick={() => download(r.id, r.documentFileName)}
                  >
                    {r.documentFileName || 'Download written reference'}
                  </Button>
                ) : (
                  canManage && (
                    <Button size="sm" variant="ghost" onClick={() => setUploadingFor(r.id)}>
                      Upload written reference
                    </Button>
                  )
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      <input
        ref={fileInputRef}
        type="file"
        className="hidden"
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file && uploadingFor) upload.mutate({ responseId: uploadingFor, file });
        }}
      />

      <Dialog open={adding} onOpenChange={setAdding}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record a reference</DialogTitle>
          </DialogHeader>
          <div className="grid gap-4 py-2">
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label htmlFor="refName">Referee name</Label>
                <Input
                  id="refName"
                  value={form.refereeName}
                  onChange={(e) => setForm({ ...form, refereeName: e.target.value })}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="refPosition">Their position</Label>
                <Input
                  id="refPosition"
                  value={form.refereePosition}
                  onChange={(e) => setForm({ ...form, refereePosition: e.target.value })}
                />
              </div>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="refOrg">Organisation</Label>
              <Input
                id="refOrg"
                value={form.refereeOrganisation}
                onChange={(e) => setForm({ ...form, refereeOrganisation: e.target.value })}
              />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label htmlFor="refEmail">Email</Label>
                <Input
                  id="refEmail"
                  type="email"
                  value={form.refereeEmail}
                  onChange={(e) => setForm({ ...form, refereeEmail: e.target.value })}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="refPhone">Phone</Label>
                <Input
                  id="refPhone"
                  value={form.refereePhone}
                  onChange={(e) => setForm({ ...form, refereePhone: e.target.value })}
                />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>How they responded</Label>
                <Select
                  value={form.responseMethod}
                  onValueChange={(v) => setForm({ ...form, responseMethod: v as typeof form.responseMethod })}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {REFERENCE_RESPONSE_METHODS.map((m) => (
                      <SelectItem key={m} value={m}>
                        {humanizeEnum(m)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label>Overall rating</Label>
                <Select
                  value={form.overallRating ?? '__none'}
                  onValueChange={(v) =>
                    setForm({ ...form, overallRating: v === '__none' ? null : (v as typeof form.overallRating) })
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__none">Not rated</SelectItem>
                    {REFERENCE_RATINGS.map((r) => (
                      <SelectItem key={r} value={r}>
                        {humanizeEnum(r)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="refComments">Comments</Label>
              <Textarea
                id="refComments"
                rows={3}
                value={form.comments}
                onChange={(e) => setForm({ ...form, comments: e.target.value })}
              />
            </div>
            <div className="grid grid-cols-3 gap-3">
              {(
                [
                  ['wouldRehire', 'Would rehire'],
                  ['confirmedDatesOfEmployment', 'Dates confirmed'],
                  ['confirmedPositionHeld', 'Position confirmed'],
                ] as const
              ).map(([key, label]) => (
                <div key={key} className="flex items-center gap-2">
                  <Checkbox
                    id={key}
                    checked={form[key] === true}
                    onCheckedChange={(checked) => setForm({ ...form, [key]: checked === true ? true : null })}
                  />
                  <Label htmlFor={key} className="text-xs font-normal">
                    {label}
                  </Label>
                </div>
              ))}
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAdding(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => add.mutate()}
              disabled={!form.refereeName.trim() || !form.refereeEmail.trim() || add.isPending}
            >
              {add.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
