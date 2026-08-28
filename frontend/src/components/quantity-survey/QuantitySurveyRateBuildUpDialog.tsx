'use client';

import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Calculator, Plus, Trash2 } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
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
import { ScrollArea } from '@/components/ui/scroll-area';
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
  QuantitySurveyRateBuildUpCalculationMethod,
  QuantitySurveyRateBuildUpPercentageBasis,
  QuantitySurveyRateComponent,
  QuantitySurveyRateItemCategory,
  quantitySurveyRateLibraryService,
  type PreviewQuantitySurveyRateBuildUp,
  type QuantitySurveyLookupOption,
  type QuantitySurveyRateBuildUpLineRequest,
  type QuantitySurveyRateBuildUpPreview,
} from '@/services/quantity-survey-rate-library.service';

const NONE = '__none__';
const today = () => new Date().toISOString().slice(0, 10);
const COMPONENTS = [
  [QuantitySurveyRateComponent.Material, 'Material'],
  [QuantitySurveyRateComponent.Labour, 'Labour'],
  [QuantitySurveyRateComponent.Plant, 'Plant'],
  [QuantitySurveyRateComponent.Equipment, 'Equipment'],
  [QuantitySurveyRateComponent.Subcontract, 'Subcontract'],
  [QuantitySurveyRateComponent.Overhead, 'Overhead'],
  [QuantitySurveyRateComponent.Profit, 'Profit'],
  [QuantitySurveyRateComponent.Attendance, 'Attendance'],
  [QuantitySurveyRateComponent.Contingency, 'Contingency'],
  [QuantitySurveyRateComponent.Wastage, 'Wastage'],
  [QuantitySurveyRateComponent.Transport, 'Transport'],
  [QuantitySurveyRateComponent.Other, 'Other'],
] as const;
const PERCENTAGE_COMPONENTS = new Set<QuantitySurveyRateComponent>([
  QuantitySurveyRateComponent.Overhead,
  QuantitySurveyRateComponent.Profit,
  QuantitySurveyRateComponent.Attendance,
  QuantitySurveyRateComponent.Contingency,
  QuantitySurveyRateComponent.Wastage,
]);

type LookupSets = {
  currencies: QuantitySurveyLookupOption[];
  projectTypes: QuantitySurveyLookupOption[];
  locations: QuantitySurveyLookupOption[];
  businessPartners: QuantitySurveyLookupOption[];
  evidenceDocuments: QuantitySurveyLookupOption[];
};

type Props = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  itemId: string | null;
  itemLabel: string;
  lookups: LookupSets;
  onPrepared: () => Promise<void> | void;
};

const emptyForm = (currencyId = ''): PreviewQuantitySurveyRateBuildUp => ({
  sourceDate: today(),
  currencyId,
  effectiveFrom: today(),
  effectiveTo: null,
  projectTypeId: null,
  locationId: null,
  businessPartnerId: null,
  centralDocumentVersionId: null,
  lines: [],
});

const componentLabel = (value: QuantitySurveyRateComponent) =>
  COMPONENTS.find(([key]) => key === value)?.[1] ?? String(value);

const sourceCategory = (component: QuantitySurveyRateComponent) => {
  switch (component) {
    case QuantitySurveyRateComponent.Material:
      return QuantitySurveyRateItemCategory.Material;
    case QuantitySurveyRateComponent.Labour:
      return QuantitySurveyRateItemCategory.Labour;
    case QuantitySurveyRateComponent.Plant:
      return QuantitySurveyRateItemCategory.Plant;
    case QuantitySurveyRateComponent.Equipment:
      return QuantitySurveyRateItemCategory.Equipment;
    case QuantitySurveyRateComponent.Subcontract:
      return QuantitySurveyRateItemCategory.Subcontract;
    default:
      return QuantitySurveyRateItemCategory.StandardItem;
  }
};

const numberOrNull = (value: string) =>
  value.trim() === '' ? null : Number(value);

function LookupSelect({
  value,
  options,
  placeholder,
  allowNone = true,
  onChange,
}: {
  value?: string | null;
  options: QuantitySurveyLookupOption[];
  placeholder: string;
  allowNone?: boolean;
  onChange: (value: string | null) => void;
}) {
  return (
    <Select
      value={value || (allowNone ? NONE : undefined)}
      onValueChange={(next) => onChange(next === NONE ? null : next)}
    >
      <SelectTrigger>
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        {allowNone ? (
          <SelectItem value={NONE}>Not applicable</SelectItem>
        ) : null}
        {options.map((option) => (
          <SelectItem key={option.value} value={option.value}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

export function QuantitySurveyRateBuildUpDialog({
  open,
  onOpenChange,
  itemId,
  itemLabel,
  lookups,
  onPrepared,
}: Props) {
  const { toast } = useToast();
  const [form, setForm] = useState<PreviewQuantitySurveyRateBuildUp>(() =>
    emptyForm()
  );
  const [preview, setPreview] =
    useState<QuantitySurveyRateBuildUpPreview | null>(null);
  const [changeReason, setChangeReason] = useState('');
  const [clientRequestId, setClientRequestId] = useState('');
  const defaultCurrencyId = lookups.currencies[0]?.value ?? '';

  useEffect(() => {
    if (!open) return;
    setForm(emptyForm(defaultCurrencyId));
    setPreview(null);
    setChangeReason('');
    setClientRequestId(crypto.randomUUID());
  }, [open, defaultCurrencyId]);

  const context = useQuery({
    queryKey: [
      'quantity-survey-rate-build-up-context',
      itemId,
      form.sourceDate,
      form.effectiveFrom,
      form.currencyId,
      form.projectTypeId,
      form.locationId,
      form.businessPartnerId,
    ],
    queryFn: () => {
      if (!itemId) throw new Error('Select a rate-library item first.');
      return quantitySurveyRateLibraryService.rateBuildUpContext(itemId, {
        sourceDate: form.sourceDate,
        effectiveAt: form.effectiveFrom,
        currencyId: form.currencyId,
        projectTypeId: form.projectTypeId || undefined,
        locationId: form.locationId || undefined,
        businessPartnerId: form.businessPartnerId || undefined,
      });
    },
    enabled: Boolean(
      open && itemId && form.sourceDate && form.effectiveFrom && form.currencyId
    ),
  });
  const history = useQuery({
    queryKey: ['quantity-survey-rate-build-ups', itemId],
    queryFn: () => {
      if (!itemId) throw new Error('Select a rate-library item first.');
      return quantitySurveyRateLibraryService.rateBuildUps(itemId);
    },
    enabled: Boolean(open && itemId),
  });

  useEffect(() => {
    const policy = context.data;
    if (!policy) return;
    const invalidProjectType =
      (!policy.requireProjectType && Boolean(form.projectTypeId)) ||
      (Boolean(form.projectTypeId) &&
        !policy.allowedProjectTypeIds.includes(form.projectTypeId as string));
    const invalidLocation =
      (!policy.requireLocation && Boolean(form.locationId)) ||
      (Boolean(form.locationId) &&
        !policy.allowedLocationIds.includes(form.locationId as string));
    const invalidBusinessPartner =
      !policy.allowBusinessPartner && Boolean(form.businessPartnerId);
    if (!invalidProjectType && !invalidLocation && !invalidBusinessPartner)
      return;

    setPreview(null);
    setForm((current) => ({
      ...current,
      projectTypeId: invalidProjectType ? null : current.projectTypeId,
      locationId: invalidLocation ? null : current.locationId,
      businessPartnerId: invalidBusinessPartner
        ? null
        : current.businessPartnerId,
      lines: current.lines.map((line) => ({ ...line, sourceRateId: null })),
    }));
  }, [
    context.data,
    form.businessPartnerId,
    form.locationId,
    form.projectTypeId,
  ]);

  const setChangedForm = (
    update: (
      current: PreviewQuantitySurveyRateBuildUp
    ) => PreviewQuantitySurveyRateBuildUp
  ) => {
    setPreview(null);
    setForm(update);
  };

  const setChangedContext = (
    update: (
      current: PreviewQuantitySurveyRateBuildUp
    ) => PreviewQuantitySurveyRateBuildUp
  ) =>
    setChangedForm((current) => {
      const next = update(current);
      return {
        ...next,
        lines: next.lines.map((line) => ({ ...line, sourceRateId: null })),
      };
    });

  const addLine = () => {
    const allowed = context.data?.allowedComponents ?? [];
    const component =
      allowed.find((value) => !PERCENTAGE_COMPONENTS.has(value)) ?? allowed[0];
    if (component === undefined) return;
    setChangedForm((current) => ({
      ...current,
      lines: [
        ...current.lines,
        {
          sequence: current.lines.length + 1,
          component,
          calculationMethod: PERCENTAGE_COMPONENTS.has(component)
            ? QuantitySurveyRateBuildUpCalculationMethod.Percentage
            : QuantitySurveyRateBuildUpCalculationMethod.QuantityTimesPublishedRate,
          percentageBasis: PERCENTAGE_COMPONENTS.has(component)
            ? QuantitySurveyRateBuildUpPercentageBasis.DirectCost
            : null,
          sourceRateId: null,
          quantity: null,
          percentage: null,
          fixedAmount: null,
          description: '',
        },
      ],
    }));
  };

  const updateLine = (
    index: number,
    update: Partial<QuantitySurveyRateBuildUpLineRequest>
  ) =>
    setChangedForm((current) => ({
      ...current,
      lines: current.lines.map((line, lineIndex) =>
        lineIndex === index ? { ...line, ...update } : line
      ),
    }));

  const removeLine = (index: number) =>
    setChangedForm((current) => ({
      ...current,
      lines: current.lines
        .filter((_, lineIndex) => lineIndex !== index)
        .map((line, lineIndex) => ({ ...line, sequence: lineIndex + 1 })),
    }));

  const previewMutation = useMutation({
    mutationFn: () => {
      if (!itemId) throw new Error('Select a rate-library item first.');
      return quantitySurveyRateLibraryService.previewRateBuildUp(itemId, form);
    },
    onSuccess: setPreview,
    onError: (error: Error) =>
      toast({
        title: 'Unable to calculate rate build-up',
        description: error.message,
        variant: 'destructive',
      }),
  });

  const prepareMutation = useMutation({
    mutationFn: () => {
      if (!itemId || !preview)
        throw new Error('Preview the current calculation before preparing it.');
      return quantitySurveyRateLibraryService.prepareRateBuildUp(itemId, {
        ...form,
        clientRequestId,
        previewIntegrityHash: preview.integrityHash,
        changeReason,
      });
    },
    onSuccess: async (value) => {
      await onPrepared();
      onOpenChange(false);
      toast({
        title: `${value.buildUpNumber} prepared`,
        description:
          'The calculated rate is a draft pending independent publication.',
        variant: 'success',
      });
    },
    onError: (error: Error) =>
      toast({
        title: 'Unable to prepare rate build-up',
        description: error.message,
        variant: 'destructive',
      }),
  });

  const allowedComponents = context.data?.allowedComponents ?? [];
  const projectTypeOptions = useMemo(() => {
    if (!context.data?.requireProjectType) return [];
    const allowed = new Set(context.data.allowedProjectTypeIds);
    return lookups.projectTypes.filter((value) => allowed.has(value.value));
  }, [context.data, lookups.projectTypes]);
  const locationOptions = useMemo(() => {
    if (!context.data?.requireLocation) return [];
    const allowed = new Set(context.data.allowedLocationIds);
    return lookups.locations.filter((value) => allowed.has(value.value));
  }, [context.data, lookups.locations]);
  const policyDimensionsReady = Boolean(
    context.data &&
    (!context.data.requireProjectType || form.projectTypeId) &&
    (!context.data.requireLocation || form.locationId) &&
    (!context.data.requireBusinessPartner || form.businessPartnerId)
  );
  const caps = useMemo(
    () =>
      context.data
        ? `Caps: overhead ${context.data.maximumOverheadPercent}%, profit ${context.data.maximumProfitPercent}%, contingency ${context.data.maximumContingencyPercent}%, wastage ${context.data.maximumWastagePercent}% · ${context.data.decimalPlaces} decimal place(s)`
        : null,
    [context.data]
  );

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-7xl">
        <DialogHeader>
          <DialogTitle>Rate build-up · {itemLabel}</DialogTitle>
          <DialogDescription>
            Compose controlled published rates and policy-capped additions. The
            saved result enters the existing draft-to-published lifecycle.
          </DialogDescription>
        </DialogHeader>
        <ScrollArea className="max-h-[74vh] pr-3">
          <div className="space-y-4">
            <div className="grid gap-3 md:grid-cols-4">
              <div className="space-y-1">
                <Label>Source-rate date</Label>
                <Input
                  type="date"
                  value={form.sourceDate}
                  onChange={(event) =>
                    setChangedContext((value) => ({
                      ...value,
                      sourceDate: event.target.value,
                    }))
                  }
                />
              </div>
              <div className="space-y-1">
                <Label>Effective from</Label>
                <Input
                  type="date"
                  value={form.effectiveFrom}
                  onChange={(event) =>
                    setChangedContext((value) => ({
                      ...value,
                      effectiveFrom: event.target.value,
                    }))
                  }
                />
              </div>
              <div className="space-y-1">
                <Label>Effective to</Label>
                <Input
                  type="date"
                  value={form.effectiveTo ?? ''}
                  onChange={(event) =>
                    setChangedForm((value) => ({
                      ...value,
                      effectiveTo: event.target.value || null,
                    }))
                  }
                />
              </div>
              <div className="space-y-1">
                <Label>Currency</Label>
                <LookupSelect
                  value={form.currencyId}
                  options={lookups.currencies}
                  placeholder="Select currency"
                  allowNone={false}
                  onChange={(currencyId) =>
                    setChangedContext((value) => ({
                      ...value,
                      currencyId: currencyId ?? '',
                    }))
                  }
                />
              </div>
              {context.data?.requireProjectType ? (
                <div className="space-y-1">
                  <Label>Project type *</Label>
                  <LookupSelect
                    value={form.projectTypeId}
                    options={projectTypeOptions}
                    placeholder="Select permitted project type"
                    allowNone={false}
                    onChange={(projectTypeId) =>
                      setChangedContext((value) => ({
                        ...value,
                        projectTypeId,
                      }))
                    }
                  />
                </div>
              ) : null}
              {context.data?.requireLocation ? (
                <div className="space-y-1">
                  <Label>Location *</Label>
                  <LookupSelect
                    value={form.locationId}
                    options={locationOptions}
                    placeholder="Select permitted location"
                    allowNone={false}
                    onChange={(locationId) =>
                      setChangedContext((value) => ({ ...value, locationId }))
                    }
                  />
                </div>
              ) : null}
              {context.data?.allowBusinessPartner ? (
                <div className="space-y-1">
                  <Label>
                    Supplier / contractor
                    {context.data.requireBusinessPartner ? ' *' : ''}
                  </Label>
                  <LookupSelect
                    value={form.businessPartnerId}
                    options={lookups.businessPartners}
                    placeholder="Select business partner"
                    allowNone={!context.data.requireBusinessPartner}
                    onChange={(businessPartnerId) =>
                      setChangedContext((value) => ({
                        ...value,
                        businessPartnerId,
                      }))
                    }
                  />
                </div>
              ) : null}
              <div className="space-y-1">
                <Label>Central DMS evidence</Label>
                <LookupSelect
                  value={form.centralDocumentVersionId}
                  options={lookups.evidenceDocuments}
                  placeholder="Select published evidence"
                  onChange={(centralDocumentVersionId) =>
                    setChangedForm((value) => ({
                      ...value,
                      centralDocumentVersionId,
                    }))
                  }
                />
              </div>
            </div>

            <div className="flex flex-wrap items-center justify-between gap-2 rounded-md border bg-muted/30 px-3 py-2 text-sm">
              <span>
                {caps ??
                  (context.isLoading
                    ? 'Loading effective QS policy…'
                    : 'Select valid dates and currency.')}
              </span>
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={addLine}
                disabled={!context.data}
              >
                <Plus className="mr-2 h-4 w-4" /> Add component
              </Button>
            </div>
            {context.error ? (
              <p className="text-sm text-destructive">
                {(context.error as Error).message}
              </p>
            ) : null}

            <div className="overflow-x-auto rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-14">#</TableHead>
                    <TableHead>Component</TableHead>
                    <TableHead>Method</TableHead>
                    <TableHead>Controlled source / basis</TableHead>
                    <TableHead className="w-36">Input</TableHead>
                    <TableHead>Description</TableHead>
                    <TableHead className="w-16" />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {form.lines.map((line, index) => {
                    const isPercentage = PERCENTAGE_COMPONENTS.has(
                      line.component
                    );
                    const sources = (context.data?.sources ?? []).filter(
                      (source) =>
                        source.category === sourceCategory(line.component)
                    );
                    return (
                      <TableRow key={`${line.sequence}-${index}`}>
                        <TableCell>{line.sequence}</TableCell>
                        <TableCell className="min-w-40">
                          <Select
                            value={String(line.component)}
                            onValueChange={(raw) => {
                              const component = Number(
                                raw
                              ) as QuantitySurveyRateComponent;
                              const percentage =
                                PERCENTAGE_COMPONENTS.has(component);
                              updateLine(index, {
                                component,
                                calculationMethod: percentage
                                  ? QuantitySurveyRateBuildUpCalculationMethod.Percentage
                                  : QuantitySurveyRateBuildUpCalculationMethod.QuantityTimesPublishedRate,
                                percentageBasis: percentage
                                  ? component ===
                                    QuantitySurveyRateComponent.Wastage
                                    ? QuantitySurveyRateBuildUpPercentageBasis.MaterialSubtotal
                                    : QuantitySurveyRateBuildUpPercentageBasis.DirectCost
                                  : null,
                                sourceRateId: null,
                                quantity: null,
                                percentage: null,
                                fixedAmount: null,
                              });
                            }}
                          >
                            <SelectTrigger>
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              {allowedComponents.map((component) => (
                                <SelectItem
                                  key={component}
                                  value={String(component)}
                                  disabled={
                                    PERCENTAGE_COMPONENTS.has(component) &&
                                    form.lines.some(
                                      (candidate, candidateIndex) =>
                                        candidateIndex !== index &&
                                        candidate.component === component
                                    )
                                  }
                                >
                                  {componentLabel(component)}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </TableCell>
                        <TableCell className="min-w-48">
                          <Select
                            value={String(line.calculationMethod)}
                            disabled={
                              isPercentage ||
                              ![
                                QuantitySurveyRateComponent.Transport,
                                QuantitySurveyRateComponent.Other,
                              ].includes(line.component)
                            }
                            onValueChange={(raw) =>
                              updateLine(index, {
                                calculationMethod: Number(
                                  raw
                                ) as QuantitySurveyRateBuildUpCalculationMethod,
                                sourceRateId: null,
                                quantity: null,
                                fixedAmount: null,
                              })
                            }
                          >
                            <SelectTrigger>
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              {isPercentage ? (
                                <SelectItem
                                  value={String(
                                    QuantitySurveyRateBuildUpCalculationMethod.Percentage
                                  )}
                                >
                                  Percentage
                                </SelectItem>
                              ) : (
                                <>
                                  <SelectItem
                                    value={String(
                                      QuantitySurveyRateBuildUpCalculationMethod.QuantityTimesPublishedRate
                                    )}
                                  >
                                    Quantity × published rate
                                  </SelectItem>
                                  {[
                                    QuantitySurveyRateComponent.Transport,
                                    QuantitySurveyRateComponent.Other,
                                  ].includes(line.component) ? (
                                    <SelectItem
                                      value={String(
                                        QuantitySurveyRateBuildUpCalculationMethod.FixedAmount
                                      )}
                                    >
                                      Fixed amount
                                    </SelectItem>
                                  ) : null}
                                </>
                              )}
                            </SelectContent>
                          </Select>
                        </TableCell>
                        <TableCell className="min-w-72">
                          {line.calculationMethod ===
                          QuantitySurveyRateBuildUpCalculationMethod.QuantityTimesPublishedRate ? (
                            <Select
                              value={line.sourceRateId ?? undefined}
                              onValueChange={(sourceRateId) =>
                                updateLine(index, { sourceRateId })
                              }
                            >
                              <SelectTrigger>
                                <SelectValue placeholder="Select published source rate" />
                              </SelectTrigger>
                              <SelectContent>
                                {sources.map((source) => (
                                  <SelectItem
                                    key={source.rateId}
                                    value={source.rateId}
                                  >
                                    {source.itemCode} · {source.itemName} ·{' '}
                                    {source.currencyCode}{' '}
                                    {source.unitRate.toLocaleString()} /{' '}
                                    {source.unitOfMeasure}
                                  </SelectItem>
                                ))}
                              </SelectContent>
                            </Select>
                          ) : line.calculationMethod ===
                            QuantitySurveyRateBuildUpCalculationMethod.Percentage ? (
                            <Select
                              value={String(
                                line.percentageBasis ??
                                  QuantitySurveyRateBuildUpPercentageBasis.DirectCost
                              )}
                              disabled={
                                line.component ===
                                QuantitySurveyRateComponent.Wastage
                              }
                              onValueChange={(raw) =>
                                updateLine(index, {
                                  percentageBasis: Number(
                                    raw
                                  ) as QuantitySurveyRateBuildUpPercentageBasis,
                                })
                              }
                            >
                              <SelectTrigger>
                                <SelectValue />
                              </SelectTrigger>
                              <SelectContent>
                                <SelectItem
                                  value={String(
                                    QuantitySurveyRateBuildUpPercentageBasis.MaterialSubtotal
                                  )}
                                >
                                  Material subtotal
                                </SelectItem>
                                <SelectItem
                                  value={String(
                                    QuantitySurveyRateBuildUpPercentageBasis.DirectCost
                                  )}
                                >
                                  Direct cost
                                </SelectItem>
                                <SelectItem
                                  value={String(
                                    QuantitySurveyRateBuildUpPercentageBasis.RunningTotal
                                  )}
                                >
                                  Running total
                                </SelectItem>
                              </SelectContent>
                            </Select>
                          ) : (
                            <span className="text-sm text-muted-foreground">
                              Entered directly
                            </span>
                          )}
                        </TableCell>
                        <TableCell>
                          <Input
                            type="number"
                            min="0"
                            step={
                              line.calculationMethod ===
                              QuantitySurveyRateBuildUpCalculationMethod.Percentage
                                ? '0.0001'
                                : '0.000001'
                            }
                            value={
                              line.calculationMethod ===
                              QuantitySurveyRateBuildUpCalculationMethod.Percentage
                                ? (line.percentage ?? '')
                                : line.calculationMethod ===
                                    QuantitySurveyRateBuildUpCalculationMethod.FixedAmount
                                  ? (line.fixedAmount ?? '')
                                  : (line.quantity ?? '')
                            }
                            onChange={(event) => {
                              const value = numberOrNull(event.target.value);
                              updateLine(
                                index,
                                line.calculationMethod ===
                                  QuantitySurveyRateBuildUpCalculationMethod.Percentage
                                  ? { percentage: value }
                                  : line.calculationMethod ===
                                      QuantitySurveyRateBuildUpCalculationMethod.FixedAmount
                                    ? { fixedAmount: value }
                                    : { quantity: value }
                              );
                            }}
                          />
                        </TableCell>
                        <TableCell className="min-w-52">
                          <Input
                            value={line.description ?? ''}
                            placeholder={
                              line.component ===
                              QuantitySurveyRateComponent.Other
                                ? 'Required description'
                                : 'Optional note'
                            }
                            onChange={(event) =>
                              updateLine(index, {
                                description: event.target.value,
                              })
                            }
                          />
                        </TableCell>
                        <TableCell>
                          <Button
                            type="button"
                            size="icon"
                            variant="ghost"
                            aria-label="Remove component"
                            onClick={() => removeLine(index)}
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                  {form.lines.length === 0 ? (
                    <TableRow>
                      <TableCell
                        colSpan={7}
                        className="py-8 text-center text-muted-foreground"
                      >
                        Add the first policy-enabled component.
                      </TableCell>
                    </TableRow>
                  ) : null}
                </TableBody>
              </Table>
            </div>

            {preview ? (
              <div className="rounded-md border bg-card p-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex flex-wrap gap-2">
                    <Badge variant="outline">
                      Material {preview.currencyCode}{' '}
                      {preview.materialSubtotal.toLocaleString()}
                    </Badge>
                    <Badge variant="outline">
                      Direct {preview.currencyCode}{' '}
                      {preview.directCost.toLocaleString()}
                    </Badge>
                    <Badge variant="outline">
                      Additions {preview.currencyCode}{' '}
                      {preview.addOnCost.toLocaleString()}
                    </Badge>
                  </div>
                  <strong>
                    Unit rate: {preview.currencyCode}{' '}
                    {preview.unitRate.toLocaleString()}
                  </strong>
                </div>
                <div className="mt-3 grid gap-1 text-sm">
                  {preview.lines.map((line) => (
                    <div
                      key={line.sequence}
                      className="flex justify-between gap-4 border-t py-1 first:border-0"
                    >
                      <span>
                        {line.sequence}. {componentLabel(line.component)} ·{' '}
                        {line.description}
                      </span>
                      <span>
                        {preview.currencyCode}{' '}
                        {line.calculatedAmount.toLocaleString()}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            ) : null}

            <div className="space-y-1">
              <Label>Preparation reason</Label>
              <Textarea
                value={changeReason}
                onChange={(event) => setChangeReason(event.target.value)}
                placeholder="Explain why this calculated rate should enter approval."
              />
            </div>

            {(history.data?.length ?? 0) > 0 ? (
              <div className="rounded-md border p-3">
                <h3 className="mb-2 font-medium">Previous build-ups</h3>
                <div className="space-y-1 text-sm">
                  {history.data?.slice(0, 5).map((value) => (
                    <div
                      key={value.id}
                      className="flex justify-between border-t py-1 first:border-0"
                    >
                      <span>
                        {value.buildUpNumber} · v{value.version} ·{' '}
                        {new Date(value.preparedAt).toLocaleDateString()}
                      </span>
                      <span>
                        {value.currencyCode} {value.unitRate.toLocaleString()} ·{' '}
                        {value.generatedRate.lifecycleStatus === 0
                          ? 'Draft'
                          : value.generatedRate.lifecycleStatus === 1
                            ? 'Published'
                            : 'Retired'}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            ) : null}
          </div>
        </ScrollArea>
        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            onClick={() => onOpenChange(false)}
          >
            Cancel
          </Button>
          <Button
            type="button"
            variant="outline"
            onClick={() => previewMutation.mutate()}
            disabled={
              previewMutation.isPending ||
              context.isLoading ||
              !policyDimensionsReady ||
              form.lines.length === 0
            }
          >
            <Calculator className="mr-2 h-4 w-4" />{' '}
            {previewMutation.isPending ? 'Calculating…' : 'Preview calculation'}
          </Button>
          <Button
            type="button"
            onClick={() => prepareMutation.mutate()}
            disabled={
              prepareMutation.isPending ||
              !preview ||
              !policyDimensionsReady ||
              !changeReason.trim() ||
              !clientRequestId
            }
          >
            {prepareMutation.isPending ? 'Preparing…' : 'Prepare rate draft'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
