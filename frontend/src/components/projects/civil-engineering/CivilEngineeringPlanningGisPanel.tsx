'use client';

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { CheckCircle2, MapPinned, Send, ShieldCheck, XCircle } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringDesignService } from '@/services/civil-engineering-design.service';
import type {
  CivilEngineeringDesignCase,
  CivilEngineeringLayoutConformity,
  CivilEngineeringPlanningGisLookups,
  CivilEngineeringPlanningGisValidation,
} from '@/types/civil-engineering-design';

const emptyLookups: CivilEngineeringPlanningGisLookups = {
  estateManagedAssetId: '',
  estateManagedAssetLabel: '',
  developmentApprovalFiles: [],
  planningConditions: [],
  developmentConstraints: [],
  landUseImpacts: [],
  evidenceDocuments: [],
  layoutConformities: [],
};

const layoutLabels: Record<CivilEngineeringLayoutConformity, string> = {
  Conforms: 'Conforms',
  ConformsWithConditions: 'Conforms with conditions',
  NonConforming: 'Does not conform',
};

const message = (error: unknown) => {
  const value = error as { response?: { detail?: string }; message?: string };
  return value.response?.detail || value.message || 'The Planning/GIS request could not be completed.';
};

const newRequestId = () => crypto.randomUUID();

export function CivilEngineeringPlanningGisPanel({
  designCase,
  canManage,
  canApprove,
  onWorkflowChanged,
}: {
  designCase: CivilEngineeringDesignCase;
  canManage: boolean;
  canApprove: boolean;
  onWorkflowChanged: () => Promise<void> | void;
}) {
  const { toast } = useToast();
  const [lookups, setLookups] = useState(emptyLookups);
  const [validations, setValidations] = useState<
    CivilEngineeringPlanningGisValidation[]
  >([]);
  const [loading, setLoading] = useState(false);
  const [developmentApprovalFileId, setDevelopmentApprovalFileId] = useState('');
  const [planningConditionId, setPlanningConditionId] = useState('');
  const [developmentConstraintId, setDevelopmentConstraintId] = useState('');
  const [landUseImpactId, setLandUseImpactId] = useState('');
  const [layoutConformity, setLayoutConformity] = useState<
    CivilEngineeringLayoutConformity | ''
  >('');
  const [evidenceVersionId, setEvidenceVersionId] = useState('');

  const active = useMemo(
    () =>
      validations.find(
        (item) => item.status === 'Draft' || item.status === 'Submitted'
      ),
    [validations]
  );
  const approved = useMemo(
    () => validations.find((item) => item.status === 'Approved'),
    [validations]
  );
  const evidenceDocuments = useMemo(
    () =>
      developmentApprovalFileId
        ? lookups.evidenceDocuments.filter(
            (item) => item.developmentApprovalFileId === developmentApprovalFileId
          )
        : [],
    [developmentApprovalFileId, lookups.evidenceDocuments]
  );

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [lookupValues, values] = await Promise.all([
        civilEngineeringDesignService.planningGisLookups(designCase.id),
        civilEngineeringDesignService.listPlanningGis(designCase.id),
      ]);
      setLookups(lookupValues);
      setValidations(values);
    } catch (error) {
      toast({
        title: 'Unable to load Planning/GIS validation',
        description: message(error),
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  }, [designCase.id, toast]);

  useEffect(() => {
    setDevelopmentApprovalFileId('');
    setPlanningConditionId('');
    setDevelopmentConstraintId('');
    setLandUseImpactId('');
    setLayoutConformity('');
    setEvidenceVersionId('');
    void load();
  }, [designCase.id, load]);

  const create = async () => {
    const evidence = evidenceDocuments.find(
      (item) => item.centralDocumentVersionId === evidenceVersionId
    );
    if (
      !developmentApprovalFileId ||
      !planningConditionId ||
      !developmentConstraintId ||
      !landUseImpactId ||
      !layoutConformity ||
      !evidence
    ) {
      toast({
        title: 'Complete the controlled Planning/GIS validation',
        description:
          'Select the development file, controlled planning values, layout outcome, and current DMS evidence.',
        variant: 'destructive',
      });
      return;
    }
    setLoading(true);
    try {
      await civilEngineeringDesignService.createPlanningGis(designCase.id, {
        clientRequestId: newRequestId(),
        estateManagedAssetId: lookups.estateManagedAssetId,
        developmentApprovalFileId,
        planningConditionId,
        developmentConstraintId,
        landUseImpactId,
        layoutConformity,
        centralDocumentRecordId: evidence.centralDocumentRecordId,
        centralDocumentVersionId: evidence.centralDocumentVersionId,
      });
      toast({
        title: 'Planning/GIS validation saved',
        description: 'The controlled validation is ready for independent review submission.',
        variant: 'success',
      });
      await load();
      await onWorkflowChanged();
    } catch (error) {
      toast({
        title: 'Unable to save Planning/GIS validation',
        description: message(error),
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  };

  const submit = async () => {
    if (!active || active.status !== 'Draft') return;
    setLoading(true);
    try {
      await civilEngineeringDesignService.submitPlanningGis(active.id, {
        clientRequestId: newRequestId(),
        rowVersion: active.rowVersion,
      });
      toast({
        title: 'Planning/GIS validation submitted',
        description: 'A different authorized reviewer must now approve or reject it.',
        variant: 'success',
      });
      await load();
      await onWorkflowChanged();
    } catch (error) {
      toast({
        title: 'Unable to submit Planning/GIS validation',
        description: message(error),
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  };

  const decide = async (outcome: 'Approved' | 'Rejected') => {
    if (!active || active.status !== 'Submitted') return;
    setLoading(true);
    try {
      await civilEngineeringDesignService.decidePlanningGis(active.id, {
        clientRequestId: newRequestId(),
        rowVersion: active.rowVersion,
        outcome,
      });
      toast({
        title: outcome === 'Approved' ? 'Planning/GIS validation approved' : 'Planning/GIS validation rejected',
        description:
          outcome === 'Approved'
            ? 'Technical Civil Engineering assignment may now continue.'
            : 'Create a new controlled validation after the source evidence or planning position is corrected.',
        variant: outcome === 'Approved' ? 'success' : 'destructive',
      });
      await load();
      await onWorkflowChanged();
    } catch (error) {
      toast({
        title: 'Unable to record Planning/GIS decision',
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
            <MapPinned className="h-4 w-4" /> Planning/GIS validation
          </h4>
          <p className="mt-1 text-sm text-muted-foreground">
            Controlled Estate site, development-file, spatial and Planning review before technical assignment.
          </p>
        </div>
        {approved ? (
          <Badge className="gap-1"><CheckCircle2 className="h-3.5 w-3.5" /> Approved</Badge>
        ) : designCase.requirePlanningGisValidation ? (
          <Badge variant="destructive">Required before Civil Engineer assignment</Badge>
        ) : (
          <Badge variant="outline">Not required by this case policy</Badge>
        )}
      </div>

      {active ? (
        <div className="space-y-3 rounded-md bg-muted/30 p-3 text-sm">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <span className="font-medium">{active.status}</span>
              <span className="ml-2 text-muted-foreground">{active.estateManagedAssetLabel}</span>
            </div>
            <Badge variant="outline">{layoutLabels[active.layoutConformity]}</Badge>
          </div>
          <div className="grid gap-2 text-muted-foreground md:grid-cols-2 xl:grid-cols-3">
            <span>{active.developmentApprovalFileLabel}</span>
            <span>{active.planningConditionLabel}</span>
            <span>{active.developmentConstraintLabel}</span>
            <span>{active.landUseImpactLabel}</span>
            <span className="truncate">{active.spatialReference}</span>
            <span>{active.evidenceReference}</span>
          </div>
          {active.status === 'Draft' && canManage ? (
            <Button size="sm" onClick={() => void submit()} disabled={loading}>
              <Send className="mr-2 h-4 w-4" /> Submit for independent review
            </Button>
          ) : null}
          {active.status === 'Submitted' && canApprove ? (
            <div className="flex flex-wrap gap-2">
              <Button size="sm" onClick={() => void decide('Approved')} disabled={loading}>
                <ShieldCheck className="mr-2 h-4 w-4" /> Approve
              </Button>
              <Button size="sm" variant="destructive" onClick={() => void decide('Rejected')} disabled={loading}>
                <XCircle className="mr-2 h-4 w-4" /> Reject
              </Button>
            </div>
          ) : null}
        </div>
      ) : canManage && !approved ? (
        <div className="grid gap-3 rounded-md bg-muted/30 p-3 md:grid-cols-2 xl:grid-cols-3">
          <div className="space-y-2 md:col-span-2 xl:col-span-3">
            <Label>Controlled property / site</Label>
            <div className="rounded-md border bg-background px-3 py-2 text-sm">{lookups.estateManagedAssetLabel || 'Loading controlled site…'}</div>
          </div>
          <div className="space-y-2">
            <Label>Development approval file</Label>
            <Select value={developmentApprovalFileId || '__none__'} onValueChange={(value) => { setDevelopmentApprovalFileId(value === '__none__' ? '' : value); setEvidenceVersionId(''); }}>
              <SelectTrigger><SelectValue placeholder="Select development file" /></SelectTrigger>
              <SelectContent><SelectItem value="__none__">Select development file</SelectItem>{lookups.developmentApprovalFiles.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Planning condition</Label>
            <Select value={planningConditionId || '__none__'} onValueChange={(value) => setPlanningConditionId(value === '__none__' ? '' : value)}>
              <SelectTrigger><SelectValue placeholder="Select planning condition" /></SelectTrigger>
              <SelectContent><SelectItem value="__none__">Select planning condition</SelectItem>{lookups.planningConditions.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Development constraint</Label>
            <Select value={developmentConstraintId || '__none__'} onValueChange={(value) => setDevelopmentConstraintId(value === '__none__' ? '' : value)}>
              <SelectTrigger><SelectValue placeholder="Select constraint" /></SelectTrigger>
              <SelectContent><SelectItem value="__none__">Select constraint</SelectItem>{lookups.developmentConstraints.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Land-use impact</Label>
            <Select value={landUseImpactId || '__none__'} onValueChange={(value) => setLandUseImpactId(value === '__none__' ? '' : value)}>
              <SelectTrigger><SelectValue placeholder="Select land-use impact" /></SelectTrigger>
              <SelectContent><SelectItem value="__none__">Select land-use impact</SelectItem>{lookups.landUseImpacts.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Layout conformity</Label>
            <Select value={layoutConformity || '__none__'} onValueChange={(value) => setLayoutConformity(value === '__none__' ? '' : value as CivilEngineeringLayoutConformity)}>
              <SelectTrigger><SelectValue placeholder="Select conformity" /></SelectTrigger>
              <SelectContent><SelectItem value="__none__">Select conformity</SelectItem>{lookups.layoutConformities.map((item) => <SelectItem key={item} value={item}>{layoutLabels[item]}</SelectItem>)}</SelectContent>
            </Select>
          </div>
          <div className="space-y-2 md:col-span-2 xl:col-span-3">
            <Label>Current Published DMS evidence</Label>
            <Select value={evidenceVersionId || '__none__'} onValueChange={(value) => setEvidenceVersionId(value === '__none__' ? '' : value)} disabled={!developmentApprovalFileId}>
              <SelectTrigger><SelectValue placeholder="Select central-DMS evidence" /></SelectTrigger>
              <SelectContent><SelectItem value="__none__">Select central-DMS evidence</SelectItem>{evidenceDocuments.map((item) => <SelectItem key={item.centralDocumentVersionId} value={item.centralDocumentVersionId}>{item.label}</SelectItem>)}</SelectContent>
            </Select>
          </div>
          <div className="flex justify-end md:col-span-2 xl:col-span-3">
            <Button size="sm" onClick={() => void create()} disabled={loading || !lookups.estateManagedAssetId}>
              <MapPinned className="mr-2 h-4 w-4" /> Save Planning/GIS validation
            </Button>
          </div>
        </div>
      ) : !approved ? (
        <Alert>
          <AlertTitle>Planning/GIS validation pending</AlertTitle>
          <AlertDescription>An authorized Planning/GIS user must create and submit the controlled validation for this case.</AlertDescription>
        </Alert>
      ) : (
        <div className="rounded-md bg-muted/30 p-3 text-sm text-muted-foreground">The current approved Planning/GIS validation allows technical assignment to continue.</div>
      )}
    </div>
  );
}
