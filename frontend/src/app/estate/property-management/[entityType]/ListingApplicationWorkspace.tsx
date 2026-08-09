'use client';

import Link from 'next/link';
import React from 'react';
import { useSearchParams } from 'next/navigation';
import {
  CheckCircle2,
  ChevronRight,
  ClipboardCheck,
  ExternalLink,
  FileSignature,
  Loader2,
  Save,
  Send,
  Settings2,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import {
  procedureCaseService,
  type ProcedureCaseDetail,
  type ProcedureCaseField,
  type ProcedureCaseSummary,
} from '@/services/procedure-case.service';
import {
  documentManagementService,
  type CentralDocumentGenerationTemplate,
  type GeneratedCentralDocumentResult,
} from '@/services/document-management.service';

const ENTITY_TYPE = 'EstatePropertyManagementListingApplication';

const SUMMARY_FIELD_KEYS = [
  'applicationReference',
  'customerName',
  'customerAccountReference',
  'sourceReference',
  'propertyUnit',
  'listingReference',
  'requestType',
  'listingType',
  'listingPrice',
  'offerAmount',
  'currency',
  'requestedLeaseTerm',
  'requestMessage',
  'decisionStatus',
  'customerNotificationStatus',
  'customerAcceptanceStatus',
  'agreementTemplateReference',
  'generatedAgreementReference',
  'signedAgreementReference',
  'moveInDate',
  'billingStartStatus',
  'receivedDate',
];

const errorMessage = (error: unknown, fallback: string) =>
  error instanceof Error ? error.message : fallback;

const formatValue = (field: ProcedureCaseField): string => {
  if (!field.value) return 'Not provided';
  if (field.fieldType !== 'date') return field.value;

  const date = new Date(field.value);
  return Number.isNaN(date.getTime())
    ? field.value
    : new Intl.DateTimeFormat('en-GB').format(date);
};

export function ListingApplicationWorkspace() {
  const searchParams = useSearchParams();
  const requestedCaseId = searchParams.get('caseId');
  const [cases, setCases] = React.useState<ProcedureCaseSummary[]>([]);
  const [selectedCase, setSelectedCase] =
    React.useState<ProcedureCaseDetail | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [completionNotes, setCompletionNotes] = React.useState('');
  const [generationTemplates, setGenerationTemplates] = React.useState<
    CentralDocumentGenerationTemplate[]
  >([]);
  const [selectedTemplateCode, setSelectedTemplateCode] = React.useState('');
  const [generatedAgreement, setGeneratedAgreement] =
    React.useState<GeneratedCentralDocumentResult | null>(null);

  const loadCases = React.useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await procedureCaseService.listCases(
        'PropertyManagement',
        ENTITY_TYPE
      );
      setCases(data);
      const targetId =
        requestedCaseId && data.some((item) => item.id === requestedCaseId)
          ? requestedCaseId
          : data[0]?.id;
      setSelectedCase(
        targetId ? await procedureCaseService.getCase(targetId) : null
      );
    } catch (loadError) {
      setError(errorMessage(loadError, 'Unable to load property requests.'));
    } finally {
      setIsLoading(false);
    }
  }, [requestedCaseId]);

  React.useEffect(() => {
    void loadCases();
  }, [loadCases]);

  React.useEffect(() => {
    let mounted = true;

    const loadTemplates = async () => {
      try {
        const templates =
          await documentManagementService.getGenerationTemplates('Estate');
        if (!mounted) return;
        const leaseTemplates = templates.filter((template) => {
          const value = `${template.templateCode} ${template.title} ${template.documentType} ${template.body}`.toLowerCase();
          return (
            value.includes('lease') ||
            value.includes('tenancy') ||
            value.includes('agreement') ||
            value.includes('rent')
          );
        });
        const available = leaseTemplates.length ? leaseTemplates : templates;
        setGenerationTemplates(available);
        setSelectedTemplateCode((current) => current || available[0]?.templateCode || '');
      } catch {
        setGenerationTemplates([]);
      }
    };

    void loadTemplates();
    return () => {
      mounted = false;
    };
  }, []);

  const selectCase = async (caseId: string) => {
    setIsSaving(true);
    setError(null);
    try {
      setSelectedCase(await procedureCaseService.getCase(caseId));
      setCompletionNotes('');
    } catch (selectError) {
      setError(errorMessage(selectError, 'Unable to open the property request.'));
    } finally {
      setIsSaving(false);
    }
  };

  const updateField = (key: string, value: string) => {
    setSelectedCase((current) =>
      current
        ? {
            ...current,
            fields: current.fields.map((field) =>
              field.key === key ? { ...field, value } : field
            ),
          }
        : current
    );
  };

  const saveCurrentStage = async () => {
    if (!selectedCase) return;

    setIsSaving(true);
    setError(null);
    try {
      const editableKeys = new Set(selectedCase.currentStageFieldKeys);
      const fieldValues = Object.fromEntries(
        selectedCase.fields
          .filter((field) => editableKeys.has(field.key))
          .map((field) => [field.key, field.value ?? null])
      );
      setSelectedCase(
        await procedureCaseService.updateFields(selectedCase.id, {
          fieldValues,
          referenceNumber: selectedCase.referenceNumber,
          applicantName: selectedCase.applicantName,
          sourceDepartment: selectedCase.sourceDepartment,
          receivedDate: selectedCase.receivedDate,
          description: selectedCase.description,
        })
      );
    } catch (saveError) {
      setError(errorMessage(saveError, 'Unable to save the current stage.'));
    } finally {
      setIsSaving(false);
    }
  };

  const updateChecklist = async (itemId: string, checked: boolean) => {
    if (!selectedCase) return;

    setIsSaving(true);
    setError(null);
    try {
      setSelectedCase(
        await procedureCaseService.updateChecklistItem(
          selectedCase.id,
          itemId,
          checked
        )
      );
    } catch (checkError) {
      setError(errorMessage(checkError, 'Unable to update stage confirmation.'));
    } finally {
      setIsSaving(false);
    }
  };

  const completeStage = async () => {
    if (!selectedCase) return;

    setIsSaving(true);
    setError(null);
    try {
      const updated = await procedureCaseService.completeStage(
        selectedCase.id,
        completionNotes || null
      );
      setSelectedCase(updated);
      setCompletionNotes('');
      setCases(
        await procedureCaseService.listCases('PropertyManagement', ENTITY_TYPE)
      );
    } catch (completeError) {
      setError(errorMessage(completeError, 'Unable to complete the stage.'));
    } finally {
      setIsSaving(false);
    }
  };

  const generateAgreement = async () => {
    if (!selectedCase || !selectedTemplateCode) return;

    setIsSaving(true);
    setError(null);
    try {
      const fields = Object.fromEntries(
        selectedCase.fields.map((field) => [field.key, field.value ?? null])
      );
      const result = await documentManagementService.generateDocumentFromTemplate({
        templateCode: selectedTemplateCode,
        sourceModule: 'Estate',
        sourceLabel: 'Property Management listing application',
        sourceEntityType: selectedCase.entityType,
        sourceRecordReference:
          selectedCase.referenceNumber || selectedCase.title,
        sourceRecordId: selectedCase.id,
        caseTitle: selectedCase.title,
        caseReference: selectedCase.referenceNumber || '',
        applicantName: selectedCase.applicantName || '',
        purpose: 'Lease / tenancy agreement generation',
        mergeValues: {
          ...fields,
          ApplicantName: selectedCase.applicantName || '',
          CaseReference: selectedCase.referenceNumber || '',
          PropertyNumber: fields.propertyUnit || fields.listingReference || '',
          TenantName: fields.customerName || selectedCase.applicantName || '',
          RentAmount: fields.listingPrice || '',
          Currency: fields.currency || '',
          LeaseTerm: fields.requestedLeaseTerm || '',
        },
      });

      const editableKeys = new Set(selectedCase.currentStageFieldKeys);
      const updatedFields = selectedCase.fields.map((field) => {
        if (field.key === 'agreementTemplateReference') {
          return { ...field, value: selectedTemplateCode };
        }
        if (field.key === 'generatedAgreementReference') {
          return { ...field, value: result.dmsReference };
        }
        if (field.key === 'billingStartStatus' && !field.value) {
          return { ...field, value: 'Blocked - signature pending' };
        }
        return field;
      });
      const fieldValues = Object.fromEntries(
        updatedFields
          .filter(
            (field) =>
              editableKeys.has(field.key) ||
              field.key === 'agreementTemplateReference' ||
              field.key === 'generatedAgreementReference' ||
              field.key === 'billingStartStatus'
          )
          .map((field) => [field.key, field.value ?? null])
      );
      const updated = await procedureCaseService.updateFields(selectedCase.id, {
        fieldValues,
        referenceNumber: selectedCase.referenceNumber,
        applicantName: selectedCase.applicantName,
        sourceDepartment: selectedCase.sourceDepartment,
        receivedDate: selectedCase.receivedDate,
        description: selectedCase.description,
      });
      setGeneratedAgreement(result);
      setSelectedCase(updated);
    } catch (generateError) {
      setError(errorMessage(generateError, 'Unable to generate the agreement.'));
    } finally {
      setIsSaving(false);
    }
  };

  if (isLoading) {
    return (
      <Card>
        <CardContent className="flex items-center justify-center gap-2 py-16 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Loading property requests
        </CardContent>
      </Card>
    );
  }

  const summaryFields = selectedCase?.fields.filter((field) =>
    SUMMARY_FIELD_KEYS.includes(field.key)
  );
  const editableFields = selectedCase?.fields.filter((field) =>
    selectedCase.currentStageFieldKeys.includes(field.key)
  );
  const stageItems =
    selectedCase?.checklistItems.filter(
      (item) => item.stageIndex === selectedCase.currentStageIndex
    ) ?? [];
  const stages = Array.from(
    new Map(
      (selectedCase?.checklistItems ?? [])
        .sort((left, right) => left.stageIndex - right.stageIndex)
        .map((item) => [item.stageIndex, item.stageName])
    ).entries()
  );
  const stageConfirmed = stageItems.every((item) => item.isCompleted);

  return (
    <div className="space-y-4">
      {error ? (
        <div className="rounded-md border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
          {error}
        </div>
      ) : null}

      <div className="grid gap-4 lg:grid-cols-[320px_minmax(0,1fr)]">
        <Card className="h-fit">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <ClipboardCheck className="h-4 w-4" />
              Request queue
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {cases.length === 0 ? (
              <p className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
                Customer bids and rental requests will appear here after they
                are submitted from a published listing.
              </p>
            ) : (
              cases.map((item) => (
                <button
                  type="button"
                  key={item.id}
                  onClick={() => void selectCase(item.id)}
                  className={`w-full rounded-md border p-3 text-left transition-colors hover:bg-muted/60 ${
                    selectedCase?.id === item.id
                      ? 'border-primary bg-primary/5'
                      : 'border-border'
                  }`}
                >
                  <div className="flex items-start justify-between gap-2">
                    <span className="text-sm font-medium">
                      {item.referenceNumber || item.title}
                    </span>
                    <ChevronRight className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
                  </div>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {item.applicantName || 'Customer'}
                  </p>
                  <div className="mt-2 flex flex-wrap gap-1">
                    <Badge variant="secondary">{item.currentStageName}</Badge>
                    {!item.usesConfiguredWorkflow ? (
                      <Badge variant="destructive">Legacy</Badge>
                    ) : null}
                  </div>
                </button>
              ))
            )}
          </CardContent>
        </Card>

        {!selectedCase ? (
          <Card>
            <CardContent className="py-16 text-center text-sm text-muted-foreground">
              Select a property request to begin.
            </CardContent>
          </Card>
        ) : (
          <div className="space-y-4">
            {!selectedCase.usesConfiguredWorkflow ? (
              <Card className="border-amber-400/50 bg-amber-50/70 dark:bg-amber-950/20">
                <CardContent className="flex flex-col gap-3 py-4 sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <p className="font-medium">Legacy request workflow</p>
                    <p className="text-sm text-muted-foreground">
                      This request was opened before the central workflow was
                      published. New requests use Workflow Setup only.
                    </p>
                  </div>
                  <Button asChild variant="outline" size="sm">
                    <Link href="/administration/workflow?entityType=EstatePropertyManagementListingApplication">
                      <Settings2 className="mr-2 h-4 w-4" />
                      Open workflow setup
                    </Link>
                  </Button>
                </CardContent>
              </Card>
            ) : null}

            <Card>
              <CardHeader className="space-y-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <CardTitle className="text-lg">
                      {selectedCase.referenceNumber || selectedCase.title}
                    </CardTitle>
                    <p className="mt-1 text-sm text-muted-foreground">
                      {selectedCase.applicantName || 'Customer request'}
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Badge>{selectedCase.status}</Badge>
                    <Badge variant="outline">
                      {selectedCase.currentAssignedRole || 'Unassigned'}
                    </Badge>
                  </div>
                </div>

                {stages.length > 0 ? (
                  <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-5">
                    {stages.map(([index, name], position) => {
                      const isCurrent =
                        index === selectedCase.currentStageIndex;
                      const isComplete =
                        index < selectedCase.currentStageIndex ||
                        selectedCase.status === 'Completed';
                      return (
                        <div
                          key={`${index}-${name}`}
                          className={`rounded-md border px-3 py-2 text-xs ${
                            isCurrent
                              ? 'border-primary bg-primary/5 text-foreground'
                              : 'border-border text-muted-foreground'
                          }`}
                        >
                          <div className="mb-1 flex items-center gap-1 font-medium">
                            {isComplete ? (
                              <CheckCircle2 className="h-3.5 w-3.5 text-emerald-600" />
                            ) : null}
                            Stage {position + 1}
                          </div>
                          {name}
                        </div>
                      );
                    })}
                  </div>
                ) : null}
              </CardHeader>
            </Card>

            <div className="grid gap-4 xl:grid-cols-2">
              <Card>
                <CardHeader>
                  <CardTitle className="text-base">Submitted request</CardTitle>
                </CardHeader>
                <CardContent className="grid gap-3 sm:grid-cols-2">
                  {summaryFields?.map((field) => (
                    <div
                      key={field.id}
                      className={
                        field.fieldType === 'textarea' ? 'sm:col-span-2' : ''
                      }
                    >
                      <p className="text-xs font-medium text-muted-foreground">
                        {field.label}
                      </p>
                      <p className="mt-1 whitespace-pre-wrap text-sm">
                        {formatValue(field)}
                      </p>
                    </div>
                  ))}
                </CardContent>
              </Card>

              <Card>
                <CardHeader>
                  <CardTitle className="text-base">
                    Current stage: {selectedCase.currentStageName}
                  </CardTitle>
                  <p className="text-sm text-muted-foreground">
                    Only the fields needed by this stage are shown.
                  </p>
                </CardHeader>
                <CardContent className="space-y-4">
                  {editableFields?.length ? (
                    editableFields.map((field) => (
                      <div key={field.id} className="space-y-1.5">
                        <label className="text-sm font-medium" htmlFor={field.id}>
                          {field.label}
                        </label>
                        {field.fieldType === 'select' && field.options?.length ? (
                          <Select
                            value={field.value || undefined}
                            onValueChange={(value) =>
                              updateField(field.key, value)
                            }
                            disabled={
                              isSaving || !selectedCase.canEditCurrentStage
                            }
                          >
                            <SelectTrigger id={field.id}>
                              <SelectValue placeholder="Select" />
                            </SelectTrigger>
                            <SelectContent>
                              {field.options.map((option) => (
                                <SelectItem key={option} value={option}>
                                  {option}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        ) : field.fieldType === 'textarea' ? (
                          <Textarea
                            id={field.id}
                            value={field.value ?? ''}
                            onChange={(event) =>
                              updateField(field.key, event.target.value)
                            }
                            disabled={
                              isSaving || !selectedCase.canEditCurrentStage
                            }
                          />
                        ) : (
                          <Input
                            id={field.id}
                            type={field.fieldType === 'date' ? 'date' : 'text'}
                            value={field.value ?? ''}
                            onChange={(event) =>
                              updateField(field.key, event.target.value)
                            }
                            disabled={
                              isSaving || !selectedCase.canEditCurrentStage
                            }
                          />
                        )}
                      </div>
                    ))
                  ) : (
                    <p className="text-sm text-muted-foreground">
                      This workflow stage has no configured fields.
                    </p>
                  )}

                  {editableFields?.length ? (
                    <Button
                      type="button"
                      variant="outline"
                      className="gap-2"
                      onClick={() => void saveCurrentStage()}
                      disabled={isSaving || !selectedCase.canEditCurrentStage}
                    >
                      {isSaving ? (
                        <Loader2 className="h-4 w-4 animate-spin" />
                      ) : (
                        <Save className="h-4 w-4" />
                      )}
                      Save stage updates
                    </Button>
                  ) : null}
                </CardContent>
              </Card>
            </div>

            {selectedCase.fields.some(
              (field) => field.key === 'agreementTemplateReference'
            ) ? (
              <Card>
                <CardHeader>
                  <CardTitle className="flex items-center gap-2 text-base">
                    <FileSignature className="h-4 w-4" />
                    Agreement generation
                  </CardTitle>
                  <p className="text-sm text-muted-foreground">
                    Generate the lease/tenancy agreement from an approved
                    Estate document template after the customer request has
                    been approved and accepted.
                  </p>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div className="grid gap-3 md:grid-cols-[1fr_auto]">
                    <Select
                      value={selectedTemplateCode || undefined}
                      onValueChange={setSelectedTemplateCode}
                      disabled={isSaving || generationTemplates.length === 0}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Select agreement template" />
                      </SelectTrigger>
                      <SelectContent>
                        {generationTemplates.map((template) => (
                          <SelectItem
                            key={template.templateCode}
                            value={template.templateCode}
                          >
                            {template.title}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <Button
                      type="button"
                      className="gap-2"
                      onClick={() => void generateAgreement()}
                      disabled={
                        isSaving ||
                        generationTemplates.length === 0 ||
                        !selectedTemplateCode
                      }
                    >
                      {isSaving ? (
                        <Loader2 className="h-4 w-4 animate-spin" />
                      ) : (
                        <FileSignature className="h-4 w-4" />
                      )}
                      Generate agreement
                    </Button>
                  </div>
                  {generationTemplates.length === 0 ? (
                    <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
                      No Estate agreement templates are available. Upload or
                      activate a generation template in Central DMS first.
                    </p>
                  ) : null}
                  {generatedAgreement ? (
                    <div className="rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-900">
                      Agreement generated with DMS reference{' '}
                      <span className="font-semibold">
                        {generatedAgreement.dmsReference}
                      </span>
                      {generatedAgreement.pdfUrl ? (
                        <Button
                          asChild
                          variant="link"
                          size="sm"
                          className="ml-2 h-auto px-0 text-emerald-900"
                        >
                          <a
                            href={generatedAgreement.pdfUrl}
                            target="_blank"
                            rel="noreferrer"
                          >
                            <ExternalLink className="mr-1 h-3.5 w-3.5" />
                            Open PDF
                          </a>
                        </Button>
                      ) : null}
                    </div>
                  ) : null}
                </CardContent>
              </Card>
            ) : null}

            {selectedCase.usesConfiguredWorkflow &&
            selectedCase.status !== 'Completed' ? (
              <Card>
                <CardHeader>
                  <CardTitle className="text-base">Complete this stage</CardTitle>
                </CardHeader>
                <CardContent className="space-y-4">
                  {stageItems.map((item) => (
                    <label
                      key={item.id}
                      className="flex items-start gap-3 rounded-md border p-3 text-sm"
                    >
                      <Checkbox
                        checked={item.isCompleted}
                        onCheckedChange={(checked) =>
                          void updateChecklist(item.id, checked === true)
                        }
                        disabled={
                          isSaving || !selectedCase.canEditCurrentStage
                        }
                      />
                      <span>{item.text}</span>
                    </label>
                  ))}
                  <Textarea
                    value={completionNotes}
                    onChange={(event) => setCompletionNotes(event.target.value)}
                    placeholder="Stage completion notes (optional)"
                    disabled={isSaving || !selectedCase.canEditCurrentStage}
                  />
                  <Button
                    type="button"
                    className="gap-2"
                    onClick={() => void completeStage()}
                    disabled={
                      isSaving ||
                      !selectedCase.canEditCurrentStage ||
                      !stageConfirmed
                    }
                  >
                    {isSaving ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <Send className="h-4 w-4" />
                    )}
                    Complete stage and route forward
                  </Button>
                </CardContent>
              </Card>
            ) : null}
          </div>
        )}
      </div>
    </div>
  );
}
