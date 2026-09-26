'use client';

import { useMemo, useState } from 'react';
import { isQsExtensionDecision } from '@/lib/quantity-survey-architecture-scope';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams, useRouter } from 'next/navigation';
import {
  ArrowLeft,
  CheckCircle2,
  Copy,
  Edit,
  Eye,
  FileCheck2,
  RefreshCw,
  ShieldCheck,
  Trash2,
} from 'lucide-react';

import { QuantitySurveyDecisionEditor } from '@/components/quantity-survey/configuration/QuantitySurveyDecisionEditor';
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
import { Progress } from '@/components/ui/progress';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { quantitySurveyConfigurationService } from '@/services/quantity-survey-configuration.service';
import type {
  QsDecision,
  UpdateQsProfileRequest,
} from '@/types/quantity-survey-configuration';

type LifecycleAction = 'publish' | 'retire' | 'delete' | null;

const errorMessage = (error: unknown) => {
  const value = error as {
    message?: string;
    response?: { detail?: string; title?: string };
  };
  return (
    value.response?.detail ||
    value.response?.title ||
    value.message ||
    'The request failed.'
  );
};

const decisionBadge = (decision: QsDecision) => {
  if (decision.isComplete)
    return (
      <Badge>
        <CheckCircle2 className="mr-1 h-3 w-3" />
        Complete
      </Badge>
    );
  if (decision.status === 'Rejected')
    return <Badge variant="destructive">Rejected</Badge>;
  return <Badge variant="secondary">{decision.status}</Badge>;
};

export default function QuantitySurveyConfigurationDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const client = useQueryClient();
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canManage = hasPermission('quantity-survey.configuration.manage');
  const canApprove = hasPermission('quantity-survey.configuration.approve');
  const canAudit = hasPermission('quantity-survey.audit.read');
  const [decisionKey, setDecisionKey] = useState<string>();
  const [showOptionalDecisions, setShowOptionalDecisions] = useState(false);
  const [editOpen, setEditOpen] = useState(false);
  const [cloneOpen, setCloneOpen] = useState(false);
  const [lifecycleAction, setLifecycleAction] = useState<LifecycleAction>(null);
  const [reason, setReason] = useState('');
  const [cloneDate, setCloneDate] = useState('');
  const [cloneSummary, setCloneSummary] = useState('');
  const [editForm, setEditForm] = useState<UpdateQsProfileRequest>();

  const profileQuery = useQuery({
    queryKey: ['quantity-survey-configuration-profile', id],
    queryFn: () => quantitySurveyConfigurationService.get(id),
    enabled: Boolean(id),
  });
  const schemasQuery = useQuery({
    queryKey: ['quantity-survey-configuration-schemas'],
    queryFn: quantitySurveyConfigurationService.schemas,
  });
  const lookupsQuery = useQuery({
    queryKey: ['quantity-survey-configuration-lookups'],
    queryFn: quantitySurveyConfigurationService.lookups,
  });
  const historyQuery = useQuery({
    queryKey: ['quantity-survey-configuration-history', id],
    queryFn: () => quantitySurveyConfigurationService.history(id),
    enabled: Boolean(id) && canAudit,
  });
  const profile = profileQuery.data;
  const editable = Boolean(
    profile && profile.lifecycleStatus === 'Draft' && canManage
  );
  const selectedDecision = useMemo(
    () => profile?.decisions.find((item) => item.decisionKey === decisionKey),
    [decisionKey, profile]
  );
  const selectedSchema = useMemo(
    () => schemasQuery.data?.find((item) => item.decisionKey === decisionKey),
    [decisionKey, schemasQuery.data]
  );

  const refresh = async () => {
    await Promise.all([
      client.invalidateQueries({
        queryKey: ['quantity-survey-configuration-profile', id],
      }),
      client.invalidateQueries({
        queryKey: ['quantity-survey-configuration-profiles'],
      }),
      canAudit
        ? client.invalidateQueries({
            queryKey: ['quantity-survey-configuration-history', id],
          })
        : Promise.resolve(),
    ]);
  };

  const validate = useMutation({
    mutationFn: () => quantitySurveyConfigurationService.validate(id),
    onSuccess: async (result) => {
      toast({
        title: result.isValid
          ? 'Validation passed'
          : 'Validation needs attention',
        description: result.isValid
          ? 'All configured decisions passed publication validation.'
          : `${result.errors.length} blocking issue(s) remain.`,
        variant: result.isValid ? 'success' : 'destructive',
      });
      await refresh();
    },
    onError: (error) =>
      toast({
        title: 'Validation failed',
        description: errorMessage(error),
        variant: 'destructive',
      }),
  });

  const update = useMutation({
    mutationFn: () => {
      if (!editForm) throw new Error('Profile edit data is unavailable.');
      return quantitySurveyConfigurationService.update(id, {
        ...editForm,
        name: editForm.name.trim(),
        effectiveTo: editForm.effectiveTo || undefined,
        changeSummary: editForm.changeSummary?.trim() || undefined,
        reason: editForm.reason?.trim() || undefined,
      });
    },
    onSuccess: async () => {
      setEditOpen(false);
      toast({
        title: 'Draft updated',
        description: 'The effective dates and revision history were updated.',
        variant: 'success',
      });
      await refresh();
    },
    onError: (error) =>
      toast({
        title: 'Unable to update draft',
        description: errorMessage(error),
        variant: 'destructive',
      }),
  });

  const clone = useMutation({
    mutationFn: () =>
      quantitySurveyConfigurationService.clone(
        id,
        cloneDate || undefined,
        cloneSummary.trim() || undefined
      ),
    onSuccess: async (result) => {
      setCloneOpen(false);
      toast({
        title: 'Next draft created',
        description:
          'Values were copied while approvals and evidence were reset.',
        variant: 'success',
      });
      await refresh();
      router.push(
        `/administration/project-management/quantity-survey-config/${result.id}`
      );
    },
    onError: (error) =>
      toast({
        title: 'Unable to clone profile',
        description: errorMessage(error),
        variant: 'destructive',
      }),
  });

  const lifecycle = useMutation({
    mutationFn: async () => {
      if (!profile || !lifecycleAction)
        throw new Error('No lifecycle action is selected.');
      const request = { rowVersion: profile.rowVersion, reason: reason.trim() };
      if (lifecycleAction === 'publish')
        return quantitySurveyConfigurationService.publish(id, request);
      if (lifecycleAction === 'retire')
        return quantitySurveyConfigurationService.retire(id, request);
      await quantitySurveyConfigurationService.deleteDraft(id, request);
      return null;
    },
    onSuccess: async (result) => {
      const completed = lifecycleAction;
      setLifecycleAction(null);
      setReason('');
      toast({
        title:
          completed === 'publish'
            ? 'Profile published'
            : completed === 'retire'
              ? 'Profile retired'
              : 'Draft deleted',
        description:
          'The governed lifecycle action and audit revision were committed.',
        variant: 'success',
      });
      await refresh();
      if (!result)
        router.push(
          '/administration/project-management/quantity-survey-config'
        );
    },
    onError: (error) =>
      toast({
        title: 'Lifecycle action rejected',
        description: errorMessage(error),
        variant: 'destructive',
      }),
  });

  if (profileQuery.isLoading)
    return (
      <div className="flex min-h-64 items-center justify-center">
        <RefreshCw className="h-6 w-6 animate-spin" />
      </div>
    );
  if (!profile)
    return (
      <Alert variant="destructive">
        <AlertTitle>Profile unavailable</AlertTitle>
        <AlertDescription>
          {profileQuery.error
            ? errorMessage(profileQuery.error)
            : 'The QS configuration profile was not found for this tenant.'}
        </AlertDescription>
      </Alert>
    );

  const completion = profile.totalDecisionCount
    ? (profile.completeDecisionCount / profile.totalDecisionCount) * 100
    : 0;
  const openEditor = () => {
    setEditForm({
      name: profile.name,
      effectiveFrom: profile.effectiveFrom.slice(0, 19),
      effectiveTo: profile.effectiveTo?.slice(0, 19),
      changeSummary: profile.changeSummary,
      isDefault: profile.isDefault,
      rowVersion: profile.rowVersion,
      reason: '',
    });
    setEditOpen(true);
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-start">
        <div>
          <Button
            variant="ghost"
            className="mb-2 -ml-3"
            onClick={() =>
              router.push(
                '/administration/project-management/quantity-survey-config'
              )
            }
          >
            <ArrowLeft className="mr-2 h-4 w-4" />
            QS configuration
          </Button>
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="text-3xl font-bold">{profile.name}</h1>
            <Badge
              variant={
                profile.lifecycleStatus === 'Published'
                  ? 'default'
                  : 'secondary'
              }
            >
              {profile.lifecycleStatus}
            </Badge>
          </div>
          <p className="mt-1 text-muted-foreground">
            {profile.profileCode} · version {profile.version} ·{' '}
            {new Date(profile.effectiveFrom).toLocaleDateString()} –{' '}
            {profile.effectiveTo
              ? new Date(profile.effectiveTo).toLocaleDateString()
              : 'open-ended'}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => profileQuery.refetch()}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
          {editable && (
            <Button variant="outline" onClick={openEditor}>
              <Edit className="mr-2 h-4 w-4" />
              Edit profile
            </Button>
          )}
          {profile.lifecycleStatus !== 'Draft' && canManage && (
            <Button
              variant="outline"
              onClick={() => {
                setCloneDate('');
                setCloneSummary('');
                setCloneOpen(true);
              }}
            >
              <Copy className="mr-2 h-4 w-4" />
              Clone draft
            </Button>
          )}
          {editable && (
            <Button
              variant="outline"
              onClick={() => validate.mutate()}
              disabled={validate.isPending}
            >
              <ShieldCheck className="mr-2 h-4 w-4" />
              Validate
            </Button>
          )}
          {profile.lifecycleStatus === 'Draft' && canApprove && (
            <Button onClick={() => setLifecycleAction('publish')}>
              <FileCheck2 className="mr-2 h-4 w-4" />
              Publish
            </Button>
          )}
          {profile.lifecycleStatus === 'Published' && canApprove && (
            <Button
              variant="destructive"
              onClick={() => setLifecycleAction('retire')}
            >
              Retire
            </Button>
          )}
          {editable && (
            <Button
              variant="destructive"
              onClick={() => setLifecycleAction('delete')}
            >
              <Trash2 className="mr-2 h-4 w-4" />
              Delete draft
            </Button>
          )}
        </div>
      </div>

      {profile.lifecycleStatus !== 'Draft' && (
        <Alert>
          <Eye className="h-4 w-4" />
          <AlertTitle>Immutable configuration version</AlertTitle>
          <AlertDescription>
            Published and retired versions are read-only. Create the next draft
            to make a governed change.
          </AlertDescription>
        </Alert>
      )}
      <Tabs defaultValue="decisions" className="space-y-4">
        <TabsList className="h-auto flex-wrap">
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="decisions">
            Decisions ({profile.completeDecisionCount}/
            {profile.totalDecisionCount})
          </TabsTrigger>
          <TabsTrigger value="validation">
            Validation ({profile.validation.errors.length})
          </TabsTrigger>
          <TabsTrigger value="evidence">Evidence</TabsTrigger>
          {canAudit && <TabsTrigger value="history">History</TabsTrigger>}
        </TabsList>
        <TabsContent value="overview" className="space-y-4">
          <div className="grid gap-4 md:grid-cols-3">
            <Card>
              <CardHeader className="pb-2">
                <CardDescription>Decision readiness</CardDescription>
                <CardTitle>
                  {profile.completeDecisionCount}/{profile.totalDecisionCount}
                </CardTitle>
              </CardHeader>
              <CardContent>
                <Progress value={completion} />
                <p className="mt-2 text-xs text-muted-foreground">
                  Approved, effective and evidenced
                </p>
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardDescription>Validation</CardDescription>
                <CardTitle>
                  {profile.validation.isValid
                    ? 'Passed'
                    : `${profile.validation.errors.length} issue(s)`}
                </CardTitle>
              </CardHeader>
              <CardContent className="text-sm text-muted-foreground">
                Server-controlled publication gate
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardDescription>Last activity</CardDescription>
                <CardTitle className="text-base">
                  {new Date(profile.updatedAt).toLocaleString()}
                </CardTitle>
              </CardHeader>
              <CardContent className="text-sm text-muted-foreground">
                Version {profile.version}
              </CardContent>
            </Card>
          </div>
          <Card>
            <CardHeader>
              <CardTitle>Profile lineage</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-4 text-sm md:grid-cols-2">
              <div>
                <p className="text-muted-foreground">Change summary</p>
                <p>{profile.changeSummary || 'No summary supplied.'}</p>
              </div>
              <div>
                <p className="text-muted-foreground">Supersedes</p>
                <p>{profile.supersedesProfileId || 'Initial version'}</p>
              </div>
              <div>
                <p className="text-muted-foreground">Published</p>
                <p>
                  {profile.publishedAt
                    ? new Date(profile.publishedAt).toLocaleString()
                    : 'Not published'}
                </p>
              </div>
              <div>
                <p className="text-muted-foreground">Default family</p>
                <p>{profile.isDefault ? 'Yes' : 'No'}</p>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="decisions">
          <Card>
            <CardHeader>
              <CardTitle>QS configuration</CardTitle>
              <CardDescription>
                Configure the processes you use. Unconfigured decisions do not
                prevent publication. Configured decisions still require approval
                and supporting evidence.
              </CardDescription>
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={showOptionalDecisions}
                  onChange={(event) =>
                    setShowOptionalDecisions(event.target.checked)
                  }
                />
                Show optional configuration
              </label>
            </CardHeader>
            <CardContent className="divide-y p-0">
              {profile.decisions
                .filter(
                  (decision) =>
                    showOptionalDecisions ||
                    !isQsExtensionDecision(decision.decisionKey)
                )
                .map((decision) => (
                  <button
                    key={decision.id}
                    className="flex w-full flex-col gap-3 px-6 py-4 text-left hover:bg-muted/50 sm:flex-row sm:items-center sm:justify-between"
                    onClick={() => setDecisionKey(decision.decisionKey)}
                  >
                    <div>
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-mono text-xs font-semibold text-primary">
                          {decision.decisionKey}
                        </span>
                        <span className="font-medium">
                          {decision.displayName}
                        </span>
                      </div>
                      <p className="mt-1 text-sm text-muted-foreground">
                        {decision.ownerGroup} · approval{' '}
                        {decision.approvalStatus.toLowerCase()} · evidence{' '}
                        {decision.evidenceStatus.toLowerCase()}
                      </p>
                    </div>
                    <div className="flex items-center gap-2">
                      {decisionBadge(decision)}
                      <Eye className="h-4 w-4 text-muted-foreground" />
                    </div>
                  </button>
                ))}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="validation" className="space-y-4">
          {profile.validation.isValid && (
            <Alert>
              <CheckCircle2 className="h-4 w-4" />
              <AlertTitle>Publication validation passed</AlertTitle>
              <AlertDescription>
                All configured decisions currently pass.
              </AlertDescription>
            </Alert>
          )}
          {profile.validation.errors.map((issue, index) => (
            <Alert key={`${issue.code}-${index}`} variant="destructive">
              <AlertTitle>
                {issue.decisionKey ? `${issue.decisionKey}: ` : ''}
                {issue.code}
              </AlertTitle>
              <AlertDescription>{issue.message}</AlertDescription>
            </Alert>
          ))}
          {profile.validation.warnings.map((issue, index) => (
            <Alert key={`${issue.code}-warning-${index}`}>
              <AlertTitle>
                {issue.decisionKey ? `${issue.decisionKey}: ` : ''}
                {issue.code}
              </AlertTitle>
              <AlertDescription>{issue.message}</AlertDescription>
            </Alert>
          ))}
        </TabsContent>

        <TabsContent value="evidence">
          <Card>
            <CardHeader>
              <CardTitle>Central DMS evidence register</CardTitle>
              <CardDescription>
                References remain bound to governed DMS records; this module
                does not duplicate uploaded files.
              </CardDescription>
            </CardHeader>
            <CardContent className="divide-y p-0">
              {profile.decisions.map((decision) => (
                <div
                  key={decision.id}
                  className="flex items-center justify-between gap-4 px-6 py-4"
                >
                  <div>
                    <p className="font-medium">
                      {decision.decisionKey} · {decision.displayName}
                    </p>
                    <p className="text-sm text-muted-foreground">
                      {decision.evidence.length
                        ? decision.evidence
                            .map((item) =>
                              `${item.documentReference} ${item.versionNumber ?? ''}`.trim()
                            )
                            .join(', ')
                        : 'No current evidence linked'}
                    </p>
                  </div>
                  <Badge
                    variant={decision.evidence.length ? 'default' : 'secondary'}
                  >
                    {decision.evidenceStatus}
                  </Badge>
                </div>
              ))}
            </CardContent>
          </Card>
        </TabsContent>

        {canAudit && (
          <TabsContent value="history">
            <Card>
              <CardHeader>
                <CardTitle>Immutable revision history</CardTitle>
                <CardDescription>
                  Actor, roles, reason, result and correlation context.
                </CardDescription>
              </CardHeader>
              <CardContent className="divide-y p-0">
                {historyQuery.isLoading ? (
                  <p className="p-6 text-sm text-muted-foreground">
                    Loading history…
                  </p>
                ) : (historyQuery.data ?? []).length ? (
                  (historyQuery.data ?? []).map((item) => (
                    <div
                      key={item.id}
                      className="grid gap-2 px-6 py-4 text-sm md:grid-cols-[180px_180px_1fr]"
                    >
                      <div>
                        <p className="font-medium">{item.action}</p>
                        <p className="text-xs text-muted-foreground">
                          {item.result}
                        </p>
                      </div>
                      <div>
                        <p>{item.actorName}</p>
                        <p className="text-xs text-muted-foreground">
                          {item.actorRoles || 'System'}
                        </p>
                      </div>
                      <div>
                        <p>{item.reason || 'No reason supplied'}</p>
                        <p className="mt-1 break-all text-xs text-muted-foreground">
                          {new Date(item.timestamp).toLocaleString()} ·{' '}
                          {item.correlationId}
                        </p>
                      </div>
                    </div>
                  ))
                ) : (
                  <p className="p-6 text-sm text-muted-foreground">
                    No revision history is available.
                  </p>
                )}
              </CardContent>
            </Card>
          </TabsContent>
        )}
      </Tabs>

      <Dialog
        open={Boolean(selectedDecision)}
        onOpenChange={(open) => {
          if (!open) setDecisionKey(undefined);
        }}
      >
        <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-5xl">
          {selectedDecision && selectedSchema && lookupsQuery.data ? (
            <>
              <DialogHeader>
                <DialogTitle>
                  {selectedDecision.decisionKey} ·{' '}
                  {selectedDecision.displayName}
                </DialogTitle>
                <DialogDescription>
                  {selectedDecision.description}
                </DialogDescription>
              </DialogHeader>
              <QuantitySurveyDecisionEditor
                key={`${selectedDecision.id}-${selectedDecision.rowVersion}`}
                profileId={id}
                decision={selectedDecision}
                schema={selectedSchema}
                lookups={lookupsQuery.data}
                editable={editable}
                canApprove={canApprove && profile.lifecycleStatus === 'Draft'}
                onChanged={refresh}
              />
            </>
          ) : (
            <div className="flex min-h-48 items-center justify-center">
              <RefreshCw className="h-6 w-6 animate-spin" />
            </div>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent className="sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>Edit configuration draft</DialogTitle>
            <DialogDescription>
              Changing profile dates revalidates each configured decision period.
            </DialogDescription>
          </DialogHeader>
          {editForm && (
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2 sm:col-span-2">
                <Label>Name</Label>
                <Input
                  value={editForm.name}
                  onChange={(event) =>
                    setEditForm(
                      (current) =>
                        current && { ...current, name: event.target.value }
                    )
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>Effective from (UTC)</Label>
                <Input
                  type="datetime-local"
                  step="1"
                  value={editForm.effectiveFrom}
                  onChange={(event) =>
                    setEditForm(
                      (current) =>
                        current && {
                          ...current,
                          effectiveFrom: event.target.value,
                        }
                    )
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>Effective to (UTC, optional)</Label>
                <Input
                  type="datetime-local"
                  step="1"
                  value={editForm.effectiveTo ?? ''}
                  onChange={(event) =>
                    setEditForm(
                      (current) =>
                        current && {
                          ...current,
                          effectiveTo: event.target.value || undefined,
                        }
                    )
                  }
                />
              </div>
              <div className="space-y-2 sm:col-span-2">
                <Label>Change summary</Label>
                <Textarea
                  value={editForm.changeSummary ?? ''}
                  onChange={(event) =>
                    setEditForm(
                      (current) =>
                        current && {
                          ...current,
                          changeSummary: event.target.value,
                        }
                    )
                  }
                />
              </div>
              <div className="flex items-center gap-3 sm:col-span-2">
                <Switch
                  checked={editForm.isDefault}
                  onCheckedChange={(next) =>
                    setEditForm(
                      (current) => current && { ...current, isDefault: next }
                    )
                  }
                />
                <span className="text-sm">Default QS configuration family</span>
              </div>
              <div className="space-y-2 sm:col-span-2">
                <Label>Change reason</Label>
                <Input
                  value={editForm.reason ?? ''}
                  onChange={(event) =>
                    setEditForm(
                      (current) =>
                        current && { ...current, reason: event.target.value }
                    )
                  }
                />
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => update.mutate()}
              disabled={
                update.isPending ||
                !editForm?.name.trim() ||
                !editForm?.effectiveFrom
              }
            >
              {update.isPending ? 'Saving…' : 'Save changes'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={cloneOpen} onOpenChange={setCloneOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Create next draft version</DialogTitle>
            <DialogDescription>
              Values and lineage are copied. Approval and evidence are
              deliberately reset.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Effective from (UTC)</Label>
              <Input
                type="datetime-local"
                step="1"
                value={cloneDate}
                onChange={(event) => setCloneDate(event.target.value)}
              />
              <p className="text-xs text-muted-foreground">
                Choose a time after the preceding version starts, including for
                same-day revisions. A future time schedules the replacement.
              </p>
            </div>
            <div className="space-y-2">
              <Label>Change summary</Label>
              <Textarea
                value={cloneSummary}
                onChange={(event) => setCloneSummary(event.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCloneOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => clone.mutate()}
              disabled={clone.isPending || !cloneDate || !cloneSummary.trim()}
            >
              {clone.isPending ? 'Creating…' : 'Create draft'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(lifecycleAction)}
        onOpenChange={(open) => {
          if (!open) setLifecycleAction(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {lifecycleAction === 'publish'
                ? 'Publish this configuration version?'
                : lifecycleAction === 'retire'
                  ? 'Retire this published version?'
                  : 'Delete this draft?'}
            </DialogTitle>
            <DialogDescription>
              {lifecycleAction === 'publish'
                ? 'Every controlled decision must be independently approved, period-valid and backed by a current published DMS version.'
                : lifecycleAction === 'retire'
                  ? 'The version becomes historical and read-only.'
                  : 'The soft-deleted version remains reserved for version sequencing and audit integrity.'}
            </DialogDescription>
          </DialogHeader>
          {lifecycle.error && (
            <Alert variant="destructive">
              <AlertDescription>{errorMessage(lifecycle.error)}</AlertDescription>
            </Alert>
          )}
          <div className="space-y-2">
            <Label>Reason *</Label>
            <Textarea
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              placeholder="Required for the audit trail"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setLifecycleAction(null)}>
              Cancel
            </Button>
            <Button
              variant={
                lifecycleAction === 'delete' || lifecycleAction === 'retire'
                  ? 'destructive'
                  : 'default'
              }
              onClick={() => lifecycle.mutate()}
              disabled={lifecycle.isPending || !reason.trim()}
            >
              {lifecycle.isPending ? 'Applying…' : 'Confirm'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
