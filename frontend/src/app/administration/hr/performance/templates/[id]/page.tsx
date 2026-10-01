'use client';

import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Lock, Power } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
import { useWorkflowRecord } from '@/hooks/useWorkflowRecord';
import { TemplateStructureEditor } from '@/components/hr/performance/TemplateStructureEditor';
import {
  appraisalCycleTemplateService,
  appraisalTemplateService,
} from '@/services/hr/appraisal.service';
import { humanizeEnum } from '@/lib/hr/attendance-format';
import { scopeLabel } from '@/lib/hr/appraisal-scope';

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 py-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

/**
 * One appraisal template: its scope, its structure, and where it stands in approval.
 *
 * Approval runs on the generic workflow engine, so this page never sets the approval status
 * itself — it calls submit / approve / reject and refetches, and
 * `AppraisalTemplateWorkflowStatusAdapter` decides what the status becomes. Who may approve
 * comes from the published AppraisalTemplate workflow definition; a caller who is not an
 * approver for the current step gets 403 rather than a hidden button.
 *
 * ⚠ The structure is locked while appraisals are scored on the template or an open cycle has it
 * (performance closure E-e, D-66), and nothing changes while it awaits approval. Both are enforced
 * server-side on every structural write; the editor reads the server's `isLocked` and reason (P-7 —
 * it froze on any assignment, while the server refused only an open cycle's), so the affordances
 * disappear instead of failing, and copying is the way forward. A change to an approved template
 * sends it back to Draft.
 */
export default function AppraisalTemplateDetailPage() {
  const params = useParams();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const id = (params?.id as string) ?? '';

  const {
    data: template,
    isLoading,
    isError,
  } = useQuery({
    queryKey: ['hr', 'appraisal-templates', id],
    queryFn: () => appraisalTemplateService.getById(id),
    enabled: !!id,
  });

  const { data: assignments } = useQuery({
    queryKey: ['hr', 'appraisal-cycle-templates', 'by-template', id],
    queryFn: () => appraisalCycleTemplateService.getByTemplate(id),
    enabled: !!id,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'appraisal-templates'] });
  };

  /**
   * The engine's own notion of "can act" is the summary it returns; these two flags only say
   * which affordances are worth offering for the status we can see locally.
   */
  const workflow = useWorkflowRecord({
    recallPrompt: 'reason',
    entityType: 'AppraisalTemplate',
    entityId: id,
    entityLabel: 'Appraisal Template',
    entityNumber: template?.templateName,
    status: template?.approvalStatus ?? 'Draft',
    canSubmit: template?.approvalStatus === 'Draft' || template?.approvalStatus === 'Rejected',
    canApproveReject: template?.approvalStatus === 'PendingApproval',
    enabled: !!template,
    commands: {
      submit: () => appraisalTemplateService.submitForApproval(id),
      approve: () => appraisalTemplateService.approve(id),
      reject: (ctx) => appraisalTemplateService.reject(id, ctx.comments || 'Rejected'),
      afterAction: refresh,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  const toggleActive = async () => {
    if (!template) return;
    try {
      await appraisalTemplateService.setActive(id, !template.isActive);
      await refresh();
      toast({
        title: template.isActive ? 'Deactivated' : 'Activated',
        description: template.isActive
          ? 'The template will not be offered to new cycles.'
          : 'The template passed validation and is now active.',
      });
    } catch (e: any) {
      toast({
        title: 'Could not change the active status',
        description: e?.message || 'Activation validates weights and grade bands first.',
        variant: 'destructive',
      });
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !template) {
    return (
      <div className="p-6">
        <EmptyState title="Template not found" description="It may have been removed." />
      </div>
    );
  }

  const liveAssignments = (assignments ?? []).filter((a) => a.isActive);
  // The server's own lock (E-e): appraisals scored on it, or an open cycle has it. Pending approval
  // is read-only too — what the approver decides is what is there.
  const pending = template.approvalStatus === 'PendingApproval';
  const frozen = template.isLocked || pending;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={template.templateName}
        description={template.description || 'No description.'}
        backHref="/administration/hr/performance/templates"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={humanizeEnum(template.approvalStatus)} />
            <StatusBadge active={template.isActive} />
            <Button variant="outline" onClick={toggleActive}>
              <Power className="mr-2 h-4 w-4" />
              {template.isActive ? 'Deactivate' : 'Activate'}
            </Button>
            <WorkflowApprovalActions {...workflow.actionProps} />
          </div>
        }
      />

      {template.isLocked && (
        <Card className="border-amber-500/50">
          <CardContent className="flex items-start gap-3 p-4">
            <Lock className="mt-0.5 h-4 w-4 shrink-0 text-amber-600 dark:text-amber-500" />
            <div className="space-y-1 text-sm">
              <p className="font-medium">This template is locked</p>
              <p className="text-muted-foreground">
                {template.lockReason
                  ? `${template.lockReason.charAt(0).toUpperCase()}${template.lockReason.slice(1)}.`
                  : 'Appraisals rely on it.'}{' '}
                Its structure cannot change underneath them — copy the template from the list page
                and change the copy. Its name and description can still be edited.
              </p>
            </div>
          </CardContent>
        </Card>
      )}

      {!template.isLocked && pending && (
        <Card className="border-amber-500/50">
          <CardContent className="flex items-start gap-3 p-4">
            <Lock className="mt-0.5 h-4 w-4 shrink-0 text-amber-600 dark:text-amber-500" />
            <div className="space-y-1 text-sm">
              <p className="font-medium">Awaiting approval</p>
              <p className="text-muted-foreground">
                Nothing changes while it is with the approver. Recall it to make a change.
              </p>
            </div>
          </CardContent>
        </Card>
      )}

      {!frozen && template.approvalStatus === 'Approved' && (
        <Card className="border-sky-500/50">
          <CardContent className="space-y-1 p-4 text-sm">
            <p className="font-medium">Approved</p>
            <p className="text-muted-foreground">
              A change to its structure sends it back to Draft: it is approved again before a cycle
              can generate on it.
            </p>
          </CardContent>
        </Card>
      )}

      {template.approvalStatus === 'Rejected' && template.rejectionReason && (
        <Card className="border-red-500/50">
          <CardContent className="space-y-1 p-4 text-sm">
            <p className="font-medium">Rejected</p>
            <p className="whitespace-pre-wrap text-muted-foreground">{template.rejectionReason}</p>
          </CardContent>
        </Card>
      )}

      <Tabs defaultValue="structure">
        <TabsList>
          <TabsTrigger value="structure">Structure</TabsTrigger>
          <TabsTrigger value="details">Details</TabsTrigger>
          <TabsTrigger value="cycles">Cycles ({liveAssignments.length})</TabsTrigger>
          <WorkflowTabTrigger value="workflow" />
        </TabsList>

        <TabsContent value="structure" className="pt-4">
          <TemplateStructureEditor templateId={id} readOnly={frozen} />
        </TabsContent>

        <TabsContent value="details" className="pt-4">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Scope and approval</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
              <InfoRow label="Scope" value={<Badge variant="outline">{scopeLabel(template)}</Badge>} />
              <InfoRow label="Organisation level" value={template.organizationLevelName} />
              <InfoRow label="Organisation unit" value={template.organizationUnitName} />
              <InfoRow label="Position" value={template.positionTitle} />
              <InfoRow
                label="Approval status"
                value={humanizeEnum(template.approvalStatus)}
              />
              <InfoRow label="Submitted" value={template.submittedDate?.slice(0, 10)} />
              <InfoRow label="Decided" value={template.approvalDate?.slice(0, 10)} />
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="cycles" className="pt-4">
          <Card>
            <CardContent className="p-4">
              {(assignments ?? []).length === 0 ? (
                <EmptyState
                  title="Not assigned to any cycle"
                  description="Assign it from a cycle's Templates tab — only an approved, active template can be used."
                />
              ) : (
                <ul className="space-y-2 text-sm">
                  {(assignments ?? []).map((a) => (
                    <li key={a.id} className="flex items-center justify-between gap-3">
                      <span>{a.cycleCode ?? a.appraisalCycleId}</span>
                      <span className="flex items-center gap-2">
                        {a.cycleStatus && <StatusBadge status={a.cycleStatus} />}
                        {a.templateInUseInCycle && <Badge variant="secondary">Appraisals on it</Badge>}
                        <Badge variant="outline">Priority {a.priority}</Badge>
                        <StatusBadge active={a.isActive} />
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <WorkflowTabContent
          value="workflow"
          entityType="AppraisalTemplate"
          entityId={id}
          entityLabel="Appraisal Template"
          entityNumber={template.templateName}
          status={template.approvalStatus}
          onAfterAction={async () => {
            await refresh();
            await workflow.refresh();
          }}
        />
      </Tabs>
    </div>
  );
}
