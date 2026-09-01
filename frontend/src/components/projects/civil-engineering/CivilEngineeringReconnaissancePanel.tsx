'use client';

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Camera, CheckCircle2, MapPin, Plus, Save, Trash2 } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
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
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringDesignService } from '@/services/civil-engineering-design.service';
import {
  documentManagementService,
  type CentralDocumentRecord,
} from '@/services/document-management.service';
import type {
  CivilEngineeringConstraintCategory,
  CivilEngineeringConstraintResolutionStatus,
  CivilEngineeringConstraintSeverity,
  CivilEngineeringDesignCase,
  CivilEngineeringDesignLookups,
  CivilEngineeringReconnaissanceItemKind,
  CivilEngineeringReconnaissanceItemRequest,
  CivilEngineeringReconnaissanceReport,
} from '@/types/civil-engineering-design';

type DraftItem = CivilEngineeringReconnaissanceItemRequest & { key: string };

const kinds: CivilEngineeringReconnaissanceItemKind[] = [
  'Constraint',
  'InformationSource',
  'Photo',
];
const categories: CivilEngineeringConstraintCategory[] = [
  'Access',
  'Topography',
  'Drainage',
  'Soil',
  'Utilities',
  'Environment',
  'Boundary',
  'ExistingStructure',
  'Safety',
  'Regulatory',
];
const severities: CivilEngineeringConstraintSeverity[] = [
  'Low',
  'Medium',
  'High',
  'Critical',
];
const resolutionStatuses: CivilEngineeringConstraintResolutionStatus[] = [
  'Open',
  'Mitigated',
  'Accepted',
];
const newKey = () => crypto.randomUUID();
const today = () => new Date().toISOString().slice(0, 10);
const message = (error: unknown) =>
  (error as { response?: { detail?: string }; message?: string }).response
    ?.detail ||
  (error as Error)?.message ||
  'The reconnaissance request could not be completed.';

const blankItem = (
  kind: CivilEngineeringReconnaissanceItemKind = 'Constraint'
): DraftItem => ({
  key: newKey(),
  kind,
  description: '',
  constraintCategory: kind === 'Constraint' ? 'Access' : undefined,
  severity: kind === 'Constraint' ? 'Medium' : undefined,
  resolutionStatus: kind === 'Constraint' ? 'Open' : undefined,
  blocksDesign: false,
  displayOrder: 0,
});

export function CivilEngineeringReconnaissancePanel({
  designCase,
  lookups,
  documents,
  onWorkflowChanged,
}: {
  designCase: CivilEngineeringDesignCase;
  lookups: CivilEngineeringDesignLookups;
  documents: CentralDocumentRecord[];
  onWorkflowChanged: () => Promise<void> | void;
}) {
  const { hasPermission, hasRole } = useAuth();
  const { toast } = useToast();
  const editable =
    designCase.stage === 'SceInformationGathering' &&
    hasPermission('civil-engineering.design.manage') &&
    hasRole('TDC_SUPERVISING_CIVIL_ENGINEER');
  const [reports, setReports] = useState<
    CivilEngineeringReconnaissanceReport[]
  >([]);
  const [loading, setLoading] = useState(false);
  const [visitDate, setVisitDate] = useState(today());
  const [siteLocation, setSiteLocation] = useState('');
  const [summary, setSummary] = useState('');
  const [siteConditions, setSiteConditions] = useState('');
  const [reason, setReason] = useState('');
  const [items, setItems] = useState<DraftItem[]>([]);

  const draft = useMemo(
    () => reports.find((item) => item.status === 'Draft'),
    [reports]
  );
  const eligibleDocuments = useMemo(
    () =>
      documents.filter(
        (item) =>
          item.lifecycleStatus === 'Active' &&
          item.versionStatus === 'Published' &&
          Boolean(item.currentVersion)
      ),
    [documents]
  );

  const setEditor = useCallback(
    (report?: CivilEngineeringReconnaissanceReport) => {
      setVisitDate(report?.visitDate?.slice(0, 10) || today());
      setSiteLocation(report?.siteLocation || '');
      setSummary(report?.summary || '');
      setSiteConditions(report?.siteConditions || '');
      setReason('');
      setItems(
        report?.items.map((item, index) => ({
          key: item.id || newKey(),
          kind: item.kind,
          description: item.description,
          informationSourceSectionId:
            item.informationSourceSectionId || undefined,
          centralDocumentRecordId: item.centralDocumentRecordId || undefined,
          centralDocumentVersionId: item.centralDocumentVersionId || undefined,
          constraintCategory: item.constraintCategory || undefined,
          severity: item.severity || undefined,
          resolutionStatus: item.resolutionStatus || undefined,
          blocksDesign: item.blocksDesign,
          displayOrder: index,
        })) ?? []
      );
    },
    []
  );

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const values = await civilEngineeringDesignService.listReconnaissance(
        designCase.id
      );
      setReports(values);
      setEditor(values.find((item) => item.status === 'Draft'));
    } catch (error) {
      toast({
        title: 'Unable to load site reconnaissance',
        description: message(error),
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  }, [designCase.id, setEditor, toast]);

  useEffect(() => {
    void load();
  }, [load]);

  const updateItem = (key: string, patch: Partial<DraftItem>) =>
    setItems((current) =>
      current.map((item) => (item.key === key ? { ...item, ...patch } : item))
    );

  const changeKind = (
    item: DraftItem,
    kind: CivilEngineeringReconnaissanceItemKind
  ) => {
    const reset = blankItem(kind);
    updateItem(item.key, { ...reset, key: item.key });
  };

  const materializeItems = async () => {
    const documentIds = Array.from(
      new Set(
        items
          .filter((item) => item.kind !== 'Constraint')
          .map((item) => item.centralDocumentRecordId)
          .filter((value): value is string => Boolean(value))
      )
    );
    const details = await Promise.all(
      documentIds.map((id) => documentManagementService.getRecord(id))
    );
    return items.map(
      (item, index): CivilEngineeringReconnaissanceItemRequest => {
        if (item.kind === 'Constraint') {
          return {
            kind: item.kind,
            description: item.description.trim(),
            constraintCategory: item.constraintCategory,
            severity: item.severity,
            resolutionStatus: item.resolutionStatus,
            blocksDesign: item.blocksDesign,
            displayOrder: index,
          };
        }
        const detail = details.find(
          (value) => value?.record.id === item.centralDocumentRecordId
        );
        const version = detail?.versions.find(
          (value) =>
            value.status === 'Published' &&
            value.versionNumber === detail.record.currentVersion
        );
        if (!detail || !version) {
          throw new Error(
            'Every photo and information source must select a current Published DMS document.'
          );
        }
        return {
          kind: item.kind,
          description: item.description.trim(),
          informationSourceSectionId:
            item.kind === 'InformationSource'
              ? item.informationSourceSectionId
              : undefined,
          centralDocumentRecordId: detail.record.id,
          centralDocumentVersionId: version.id,
          blocksDesign: false,
          displayOrder: index,
        };
      }
    );
  };

  const save = async () => {
    if (
      siteLocation.trim().length < 3 ||
      summary.trim().length < 10 ||
      siteConditions.trim().length < 5 ||
      items.length === 0 ||
      items.some((item) => item.description.trim().length < 3)
    ) {
      toast({
        title: 'Complete the reconnaissance record',
        description:
          'Enter the visit details and a description for every constraint, source, or photo.',
        variant: 'destructive',
      });
      return;
    }
    if (draft && reason.trim().length < 5) {
      toast({
        title: 'Enter an amendment reason',
        description:
          'Draft amendments require a reason of at least five characters.',
        variant: 'destructive',
      });
      return;
    }
    setLoading(true);
    try {
      const request = {
        clientRequestId: newKey(),
        visitDate: new Date(`${visitDate}T12:00:00`).toISOString(),
        siteLocation: siteLocation.trim(),
        summary: summary.trim(),
        siteConditions: siteConditions.trim(),
        items: await materializeItems(),
      };
      const saved = draft
        ? await civilEngineeringDesignService.updateReconnaissance(
            designCase.id,
            draft.id,
            { ...request, rowVersion: draft.rowVersion, reason: reason.trim() }
          )
        : await civilEngineeringDesignService.createReconnaissance(
            designCase.id,
            request
          );
      toast({
        title: 'Site reconnaissance saved',
        description: `${saved.reportNumber} remains controlled inside this design case.`,
        variant: 'success',
      });
      await load();
      await onWorkflowChanged();
    } catch (error) {
      toast({
        title: 'Unable to save site reconnaissance',
        description: message(error),
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  };

  const complete = async () => {
    if (!draft || reason.trim().length < 5) {
      toast({
        title: 'Enter a completion reason',
        description: 'Save the draft and enter a reason before completing it.',
        variant: 'destructive',
      });
      return;
    }
    setLoading(true);
    try {
      const completed =
        await civilEngineeringDesignService.completeReconnaissance(
          designCase.id,
          draft.id,
          {
            clientRequestId: newKey(),
            rowVersion: draft.rowVersion,
            reason: reason.trim(),
          }
        );
      toast({
        title: 'Site reconnaissance completed',
        description: `${completed.reportNumber} is now immutable and available for design assignment.`,
        variant: 'success',
      });
      await load();
      await onWorkflowChanged();
    } catch (error) {
      toast({
        title: 'Unable to complete site reconnaissance',
        description: message(error),
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="rounded-lg border p-4">
      <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h4 className="flex items-center gap-2 font-medium">
            <MapPin className="h-4 w-4" /> Site reconnaissance
          </h4>
          <p className="mt-1 text-sm text-muted-foreground">
            Site observations, section inputs, photos, and design constraints.
          </p>
        </div>
        <Badge variant="outline">
          {reports.filter((item) => item.status === 'Completed').length}{' '}
          completed
        </Badge>
      </div>

      {editable ? (
        <div className="space-y-4 rounded-md bg-muted/30 p-3">
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            <div className="space-y-2">
              <Label htmlFor="civil-recon-date">Visit date</Label>
              <Input
                id="civil-recon-date"
                type="date"
                value={visitDate}
                max={today()}
                onChange={(event) => setVisitDate(event.target.value)}
              />
            </div>
            <div className="space-y-2 md:col-span-1 xl:col-span-3">
              <Label htmlFor="civil-recon-location">Site location</Label>
              <Input
                id="civil-recon-location"
                value={siteLocation}
                onChange={(event) => setSiteLocation(event.target.value)}
                placeholder="Controlled project site or work area"
              />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="civil-recon-summary">
                Reconnaissance summary
              </Label>
              <Textarea
                id="civil-recon-summary"
                rows={2}
                value={summary}
                onChange={(event) => setSummary(event.target.value)}
              />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="civil-recon-conditions">Site conditions</Label>
              <Textarea
                id="civil-recon-conditions"
                rows={2}
                value={siteConditions}
                onChange={(event) => setSiteConditions(event.target.value)}
              />
            </div>
          </div>

          <div className="space-y-3">
            {items.map((item) => (
              <div
                key={item.key}
                className="rounded-md border bg-background p-3"
              >
                <div className="grid gap-3 lg:grid-cols-4">
                  <div className="space-y-2">
                    <Label>Item type</Label>
                    <Select
                      value={item.kind}
                      onValueChange={(value) =>
                        changeKind(
                          item,
                          value as CivilEngineeringReconnaissanceItemKind
                        )
                      }
                    >
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {kinds.map((value) => (
                          <SelectItem key={value} value={value}>
                            {value}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2 lg:col-span-3">
                    <Label>Description</Label>
                    <Input
                      value={item.description}
                      onChange={(event) =>
                        updateItem(item.key, {
                          description: event.target.value,
                        })
                      }
                    />
                  </div>

                  {item.kind === 'InformationSource' ? (
                    <div className="space-y-2 lg:col-span-2">
                      <Label>Source section</Label>
                      <Select
                        value={item.informationSourceSectionId || '__none__'}
                        onValueChange={(value) =>
                          updateItem(item.key, {
                            informationSourceSectionId:
                              value === '__none__' ? undefined : value,
                          })
                        }
                      >
                        <SelectTrigger>
                          <SelectValue placeholder="Select section" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="__none__">
                            Select section
                          </SelectItem>
                          {lookups.informationSourceSections.map((section) => (
                            <SelectItem
                              key={section.sectionId}
                              value={section.sectionId}
                            >
                              {section.label}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                  ) : null}

                  {item.kind !== 'Constraint' ? (
                    <div className="space-y-2 lg:col-span-2">
                      <Label>Published DMS document</Label>
                      <Select
                        value={item.centralDocumentRecordId || '__none__'}
                        onValueChange={(value) =>
                          updateItem(item.key, {
                            centralDocumentRecordId:
                              value === '__none__' ? undefined : value,
                            centralDocumentVersionId: undefined,
                          })
                        }
                      >
                        <SelectTrigger>
                          <SelectValue placeholder="Select document" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="__none__">
                            Select document
                          </SelectItem>
                          {eligibleDocuments.map((document) => (
                            <SelectItem key={document.id} value={document.id}>
                              {document.documentReference} — {document.title}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                  ) : (
                    <>
                      <div className="space-y-2">
                        <Label>Category</Label>
                        <Select
                          value={item.constraintCategory}
                          onValueChange={(value) =>
                            updateItem(item.key, {
                              constraintCategory:
                                value as CivilEngineeringConstraintCategory,
                            })
                          }
                        >
                          <SelectTrigger>
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {categories.map((value) => (
                              <SelectItem key={value} value={value}>
                                {value}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                      <div className="space-y-2">
                        <Label>Severity</Label>
                        <Select
                          value={item.severity}
                          onValueChange={(value) =>
                            updateItem(item.key, {
                              severity:
                                value as CivilEngineeringConstraintSeverity,
                            })
                          }
                        >
                          <SelectTrigger>
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {severities.map((value) => (
                              <SelectItem key={value} value={value}>
                                {value}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                      <div className="space-y-2">
                        <Label>Resolution</Label>
                        <Select
                          value={item.resolutionStatus}
                          onValueChange={(value) =>
                            updateItem(item.key, {
                              resolutionStatus:
                                value as CivilEngineeringConstraintResolutionStatus,
                            })
                          }
                        >
                          <SelectTrigger>
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {resolutionStatuses.map((value) => (
                              <SelectItem key={value} value={value}>
                                {value}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                      <label className="flex items-center gap-2 self-end pb-2 text-sm">
                        <Checkbox
                          checked={item.blocksDesign}
                          onCheckedChange={(checked) =>
                            updateItem(item.key, {
                              blocksDesign: checked === true,
                            })
                          }
                        />
                        Blocks design
                      </label>
                    </>
                  )}
                </div>
                <div className="mt-3 flex justify-end">
                  <Button
                    type="button"
                    size="sm"
                    variant="ghost"
                    onClick={() =>
                      setItems((current) =>
                        current.filter((value) => value.key !== item.key)
                      )
                    }
                  >
                    <Trash2 className="mr-2 h-4 w-4" /> Remove
                  </Button>
                </div>
              </div>
            ))}
            <div className="flex flex-wrap gap-2">
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={() =>
                  setItems((current) => [...current, blankItem('Constraint')])
                }
              >
                <Plus className="mr-2 h-4 w-4" /> Constraint
              </Button>
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={() =>
                  setItems((current) => [
                    ...current,
                    blankItem('InformationSource'),
                  ])
                }
              >
                <Plus className="mr-2 h-4 w-4" /> Section input
              </Button>
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={() =>
                  setItems((current) => [...current, blankItem('Photo')])
                }
              >
                <Camera className="mr-2 h-4 w-4" /> Photo
              </Button>
            </div>
          </div>

          <div className="grid gap-3 md:grid-cols-[minmax(240px,1fr),auto]">
            <Input
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              placeholder={
                draft
                  ? 'Amendment or completion reason'
                  : 'Completion reason after first save'
              }
            />
            <div className="flex flex-wrap justify-end gap-2">
              <Button
                variant="outline"
                onClick={() => void save()}
                disabled={loading}
              >
                <Save className="mr-2 h-4 w-4" />{' '}
                {draft ? 'Save changes' : 'Save draft'}
              </Button>
              {draft ? (
                <Button onClick={() => void complete()} disabled={loading}>
                  <CheckCircle2 className="mr-2 h-4 w-4" /> Complete
                </Button>
              ) : null}
            </div>
          </div>
        </div>
      ) : null}

      <div className="mt-4 space-y-2">
        {reports.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            No site visit has been recorded.
          </p>
        ) : (
          reports.map((report) => (
            <div key={report.id} className="rounded-md border p-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="font-medium">{report.reportNumber}</div>
                <Badge
                  variant={
                    report.status === 'Completed' ? 'default' : 'secondary'
                  }
                >
                  {report.status}
                </Badge>
              </div>
              <div className="mt-1 text-sm">{report.siteLocation}</div>
              <div className="mt-1 text-xs text-muted-foreground">
                {new Date(report.visitDate).toLocaleDateString()} ·{' '}
                {report.items.length} item(s)
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
