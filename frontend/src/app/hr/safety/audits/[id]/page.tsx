'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Loader2,
  Play,
  FileText,
  CheckCircle2,
  XCircle,
  Trash2,
  Plus,
  ShieldCheck,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { safetyAuditService } from '@/services/hr/safety-audit.service';
import { safetyReferenceService } from '@/services/hr/safety-reference.service';
import {
  SHE_AUDIT_FINDING_CLASSIFICATION_OPTIONS,
  type SheAudit,
  type SheAuditFinding,
  type SheAuditFindingClassification,
} from '@/types/hr/safety-audits';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline justify-between gap-2 border-b py-1 text-sm">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="text-right font-medium">{value}</dd>
    </div>
  );
}

function FindingStatusBadge({ f }: { f: SheAuditFinding }) {
  switch (f.status) {
    case 'Closed':
      return <Badge variant="secondary">Closed</Badge>;
    case 'Verified':
      return <Badge>Verified</Badge>;
    case 'Resolved':
      return <Badge variant="outline">Resolved — awaiting verification</Badge>;
    default:
      return <Badge variant="destructive">Open</Badge>;
  }
}

/**
 * One audit, full lifecycle: start → record findings (InProgress only) → issue the
 * report → verify + close every finding → close the audit. Finding corrective
 * actions feed the unified CA tracker and the reminder engine.
 */
export default function SheAuditDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  type DialogKind =
    | 'issue-report'
    | 'close'
    | 'cancel'
    | 'add-team'
    | 'add-finding'
    | 'resolve-finding'
    | 'verify-finding'
    | 'add-action'
    | null;
  const [dialog, setDialog] = useState<DialogKind>(null);
  const [busy, setBusy] = useState(false);
  const [targetFinding, setTargetFinding] = useState<SheAuditFinding | null>(null);

  // dialog state
  const [summaryText, setSummaryText] = useState('');
  const [reportPath, setReportPath] = useState('');
  const [actorId, setActorId] = useState<string | null>(null);
  const [notes, setNotes] = useState('');
  const [teamRole, setTeamRole] = useState('');
  const [classification, setClassification] = useState<SheAuditFindingClassification>('MinorNonConformity');
  const [clauseRef, setClauseRef] = useState('');
  const [description, setDescription] = useState('');
  const [evidence, setEvidence] = useState('');
  const [dueDate, setDueDate] = useState('');
  const [templateId, setTemplateId] = useState('');

  const { data: audit, isLoading } = useQuery({
    queryKey: ['hr', 'safety-audits', 'detail', id],
    queryFn: () => safetyAuditService.getById(id),
    enabled: !!id,
  });

  const { data: templates = [] } = useQuery({
    queryKey: ['hr', 'safety-reference', 'ca-templates'],
    queryFn: () => safetyReferenceService.getCorrectiveActionTemplates(true),
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-audits'] });
  };

  const act = async (title: string, fn: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await fn();
      await refresh();
      toast({ title });
      setDialog(null);
      setTargetFinding(null);
      setActorId(null);
      setNotes('');
    } catch (error: any) {
      toast({
        title: 'Refused',
        description: error?.message || 'The action failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }
  if (!audit) {
    return (
      <div className="p-6">
        <EmptyState title="Audit not found" description="It may have been deleted." />
      </div>
    );
  }

  const a: SheAudit = audit;
  const openFindings = a.findings.filter((f) => f.status !== 'Closed').length;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${a.auditNumber} — ${a.title}`}
        description={`${a.typeName} audit${a.standard ? ` against ${a.standard}` : ''} — ${a.locationName ?? 'whole organisation'}.`}
        backHref="/hr/safety/audits"
        actions={
          <div className="flex flex-wrap gap-2">
            {a.status === 'Planned' && (
              <>
                <Button onClick={() => void act('Audit started', () => safetyAuditService.start(a.id))} disabled={busy}>
                  <Play className="mr-2 h-4 w-4" />
                  Start audit
                </Button>
                <Button variant="outline" onClick={() => setDialog('cancel')} disabled={busy}>
                  <XCircle className="mr-2 h-4 w-4" />
                  Cancel
                </Button>
                <Button
                  variant="destructive"
                  disabled={busy}
                  onClick={() => {
                    if (!window.confirm('Delete this planned audit?')) return;
                    void act('Audit deleted', async () => {
                      await safetyAuditService.remove(a.id);
                      router.push('/hr/safety/audits');
                    });
                  }}
                >
                  <Trash2 className="mr-2 h-4 w-4" />
                  Delete
                </Button>
              </>
            )}
            {a.status === 'InProgress' && (
              <>
                <Button onClick={() => setDialog('issue-report')} disabled={busy}>
                  <FileText className="mr-2 h-4 w-4" />
                  Issue report
                </Button>
                <Button variant="outline" onClick={() => setDialog('cancel')} disabled={busy}>
                  <XCircle className="mr-2 h-4 w-4" />
                  Cancel
                </Button>
              </>
            )}
            {a.status === 'ReportIssued' && (
              <Button onClick={() => setDialog('close')} disabled={busy || openFindings > 0}
                title={openFindings > 0 ? `${openFindings} finding(s) still open` : undefined}>
                <CheckCircle2 className="mr-2 h-4 w-4" />
                Close audit
              </Button>
            )}
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-2">
        <Badge variant={a.status === 'Closed' ? 'secondary' : a.status === 'Cancelled' ? 'outline' : 'default'}>
          {a.statusName}
        </Badge>
        {openFindings > 0 && a.status === 'ReportIssued' && (
          <Badge variant="destructive">{openFindings} finding(s) block closure</Badge>
        )}
        <Badge variant="outline">Lead: {a.leadAuditorName}</Badge>
        {a.externalAuditorName && (
          <Badge variant="outline">
            External: {a.externalAuditorName}
            {a.externalAuditorOrganization ? ` (${a.externalAuditorOrganization})` : ''}
          </Badge>
        )}
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Plan &amp; execution</CardTitle>
          </CardHeader>
          <CardContent>
            <dl>
              <Row label="Planned" value={`${fmtDate(a.plannedStartDate)} → ${fmtDate(a.plannedEndDate)}`} />
              <Row label="Actual" value={`${fmtDate(a.actualStartDate)} → ${fmtDate(a.actualEndDate)}`} />
              <Row label="Scope" value={a.scope ?? '—'} />
              <Row label="Objectives" value={a.objectives ?? '—'} />
              <Row label="Organization unit" value={a.organizationUnitName ?? '—'} />
            </dl>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Report &amp; closure</CardTitle>
          </CardHeader>
          <CardContent>
            <dl>
              <Row label="Report issued" value={fmtDate(a.reportIssuedDate)} />
              <Row label="Report document" value={a.reportDocumentPath ?? '—'} />
              <Row label="Closed" value={a.closedDate ? `${fmtDate(a.closedDate)} by ${a.closedByName ?? '—'}` : '—'} />
              <Row label="Closure notes" value={a.closureNotes ?? '—'} />
            </dl>
            {a.summary && <p className="text-muted-foreground mt-3 whitespace-pre-wrap text-sm">{a.summary}</p>}
          </CardContent>
        </Card>
      </div>

      <Tabs defaultValue="findings">
        <TabsList>
          <TabsTrigger value="findings">Findings ({a.findings.length})</TabsTrigger>
          <TabsTrigger value="team">Audit team ({a.teamMembers.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="findings" className="space-y-4">
          {a.status === 'InProgress' && (
            <Button variant="outline" onClick={() => setDialog('add-finding')}>
              <Plus className="mr-2 h-4 w-4" />
              Record finding
            </Button>
          )}
          {a.findings.length === 0 ? (
            <EmptyState
              title="No findings"
              description={
                a.status === 'Planned'
                  ? 'Findings are recorded once the audit starts.'
                  : 'Nothing recorded against this audit.'
              }
            />
          ) : (
            a.findings.map((f) => (
              <Card key={f.id}>
                <CardHeader className="pb-2">
                  <CardTitle className="flex flex-wrap items-center gap-2 text-base">
                    #{f.findingNumber} · {SHE_AUDIT_FINDING_CLASSIFICATION_OPTIONS.find((o) => o.value === f.classification)?.label}
                    {f.clauseReference && (
                      <span className="text-muted-foreground text-sm font-normal">({f.clauseReference})</span>
                    )}
                    <FindingStatusBadge f={f} />
                  </CardTitle>
                </CardHeader>
                <CardContent className="space-y-3">
                  <p className="text-sm">{f.description}</p>
                  {f.evidence && <p className="text-muted-foreground text-sm">Evidence: {f.evidence}</p>}
                  <div className="text-muted-foreground flex flex-wrap gap-4 text-xs">
                    <span>Responsible: {f.responsiblePersonName ?? '—'}</span>
                    <span>Due: {fmtDate(f.dueDate)}</span>
                    {f.resolvedDate && <span>Resolved: {fmtDate(f.resolvedDate)}</span>}
                    {f.verifiedDate && (
                      <span>
                        Verified: {fmtDate(f.verifiedDate)} by {f.verifiedByName ?? '—'}
                      </span>
                    )}
                  </div>
                  {f.actions.length > 0 && (
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Corrective action</TableHead>
                          <TableHead>Assigned to</TableHead>
                          <TableHead>Due</TableHead>
                          <TableHead>Status</TableHead>
                          <TableHead />
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {f.actions.map((x) => (
                          <TableRow key={x.id}>
                            <TableCell>{x.correctiveActionTemplateTitle}</TableCell>
                            <TableCell>{x.assignedToName ?? '—'}</TableCell>
                            <TableCell className="tabular-nums">{fmtDate(x.dueDate)}</TableCell>
                            <TableCell>
                              <Badge variant={x.status === 'Completed' || x.status === 'Verified' ? 'secondary' : 'outline'}>
                                {x.statusName}
                              </Badge>
                            </TableCell>
                            <TableCell className="text-right">
                              {f.status !== 'Closed' && x.status !== 'Completed' && x.status !== 'Verified' && (
                                <Button
                                  size="sm"
                                  variant="outline"
                                  disabled={busy}
                                  onClick={() =>
                                    void act('Action completed', () =>
                                      safetyAuditService.updateFindingAction({
                                        id: x.id,
                                        status: 'Completed',
                                        dueDate: x.dueDate,
                                        completionDate: new Date().toISOString(),
                                        completionNotes: null,
                                        assignedToId: x.assignedToId,
                                      }),
                                    )
                                  }
                                >
                                  Mark complete
                                </Button>
                              )}
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  )}
                  <div className="flex flex-wrap gap-2">
                    {f.status !== 'Closed' && (
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setTargetFinding(f);
                          setTemplateId('');
                          setDueDate('');
                          setActorId(null);
                          setDialog('add-action');
                        }}
                      >
                        <Plus className="mr-1 h-3 w-3" />
                        Add action
                      </Button>
                    )}
                    {f.status === 'Open' && (
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setTargetFinding(f);
                          setNotes(f.resolutionNotes ?? '');
                          setDialog('resolve-finding');
                        }}
                      >
                        Record resolution
                      </Button>
                    )}
                    {f.status === 'Resolved' && (
                      <Button
                        size="sm"
                        onClick={() => {
                          setTargetFinding(f);
                          setActorId(null);
                          setNotes('');
                          setDialog('verify-finding');
                        }}
                      >
                        <ShieldCheck className="mr-1 h-3 w-3" />
                        Verify effectiveness
                      </Button>
                    )}
                    {f.status === 'Verified' && (
                      <Button
                        size="sm"
                        disabled={busy}
                        onClick={() =>
                          void act(`Finding #${f.findingNumber} closed`, () =>
                            safetyAuditService.closeFinding(f.id),
                          )
                        }
                      >
                        Close finding
                      </Button>
                    )}
                  </div>
                </CardContent>
              </Card>
            ))
          )}
        </TabsContent>

        <TabsContent value="team" className="space-y-4">
          {(a.status === 'Planned' || a.status === 'InProgress') && (
            <Button
              variant="outline"
              onClick={() => {
                setActorId(null);
                setTeamRole('');
                setDialog('add-team');
              }}
            >
              <Plus className="mr-2 h-4 w-4" />
              Add team member
            </Button>
          )}
          {a.teamMembers.length === 0 ? (
            <EmptyState title="No team members" description="The lead auditor works alone so far." />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Member</TableHead>
                      <TableHead>Role</TableHead>
                      <TableHead />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {a.teamMembers.map((m) => (
                      <TableRow key={m.id}>
                        <TableCell>{m.employeeName}</TableCell>
                        <TableCell>{m.role}</TableCell>
                        <TableCell className="text-right">
                          <Button
                            size="sm"
                            variant="ghost"
                            disabled={busy}
                            onClick={() =>
                              void act('Team member removed', () =>
                                safetyAuditService.removeTeamMember(m.id),
                              )
                            }
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </TabsContent>
      </Tabs>

      {/* ── dialogs ── */}
      <Dialog open={dialog === 'issue-report'} onOpenChange={(o) => !o && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Issue audit report</DialogTitle>
            <DialogDescription>
              Moves the audit to Report Issued — findings then work toward verified closure.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Summary</Label>
              <Textarea rows={4} value={summaryText} onChange={(e) => setSummaryText(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Report document path</Label>
              <Input value={reportPath} onChange={(e) => setReportPath(e.target.value)} placeholder="/uploads/audits/…" />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || summaryText.trim().length === 0}
              onClick={() =>
                void act('Report issued', () =>
                  safetyAuditService.issueReport(a.id, summaryText.trim(), reportPath.trim() || null),
                )
              }
            >
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Issue report
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={dialog === 'close'} onOpenChange={(o) => !o && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Close audit</DialogTitle>
            <DialogDescription>Refused while any finding is still open.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Closed by</Label>
              <EmployeePicker value={actorId} onChange={setActorId} />
            </div>
            <div className="space-y-2">
              <Label>Closure notes</Label>
              <Textarea rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || !actorId}
              onClick={() =>
                void act('Audit closed', () =>
                  safetyAuditService.close(a.id, actorId!, notes || null),
                )
              }
            >
              Close audit
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={dialog === 'cancel'} onOpenChange={(o) => !o && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Cancel audit</DialogTitle>
            <DialogDescription>For audits that will not take place. The reason is kept.</DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Reason</Label>
            <Textarea rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Back</Button>
            <Button
              variant="destructive"
              disabled={busy || notes.trim().length === 0}
              onClick={() => void act('Audit cancelled', () => safetyAuditService.cancel(a.id, notes.trim()))}
            >
              Cancel audit
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={dialog === 'add-team'} onOpenChange={(o) => !o && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add team member</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Employee</Label>
              <EmployeePicker value={actorId} onChange={setActorId} />
            </div>
            <div className="space-y-2">
              <Label>Role on the audit</Label>
              <Input value={teamRole} onChange={(e) => setTeamRole(e.target.value)} placeholder="Auditor / Observer / Technical expert" />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || !actorId || teamRole.trim().length === 0}
              onClick={() =>
                void act('Team member added', () =>
                  safetyAuditService.addTeamMember(a.id, actorId!, teamRole.trim()),
                )
              }
            >
              Add
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={dialog === 'add-finding'} onOpenChange={(o) => !o && setDialog(null)}>
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>Record finding</DialogTitle>
            <DialogDescription>Numbers are assigned per audit in sequence.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Classification</Label>
                <Select value={classification} onValueChange={(v) => setClassification(v as SheAuditFindingClassification)}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {SHE_AUDIT_FINDING_CLASSIFICATION_OPTIONS.map((o) => (
                      <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Clause reference</Label>
                <Input value={clauseRef} onChange={(e) => setClauseRef(e.target.value)} placeholder="e.g. 45001 §8.1.2" />
              </div>
            </div>
            <div className="space-y-2">
              <Label>Description</Label>
              <Textarea rows={3} value={description} onChange={(e) => setDescription(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Evidence</Label>
              <Textarea rows={2} value={evidence} onChange={(e) => setEvidence(e.target.value)} />
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Responsible person</Label>
                <EmployeePicker value={actorId} onChange={setActorId} />
              </div>
              <div className="space-y-2">
                <Label>Due date</Label>
                <Input type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || description.trim().length === 0}
              onClick={() =>
                void act('Finding recorded', () =>
                  safetyAuditService.addFinding({
                    auditId: a.id,
                    classification,
                    clauseReference: clauseRef.trim() || null,
                    description: description.trim(),
                    evidence: evidence.trim() || null,
                    responsiblePersonId: actorId,
                    dueDate: dueDate ? new Date(dueDate).toISOString() : null,
                  }),
                )
              }
            >
              Record finding
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={dialog === 'resolve-finding'} onOpenChange={(o) => !o && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record resolution — finding #{targetFinding?.findingNumber}</DialogTitle>
            <DialogDescription>Moves the finding to Resolved; verification follows separately.</DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Resolution notes</Label>
            <Textarea rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || !targetFinding || notes.trim().length === 0}
              onClick={() =>
                void act('Resolution recorded', () =>
                  safetyAuditService.updateFinding({
                    id: targetFinding!.id,
                    classification: targetFinding!.classification,
                    clauseReference: targetFinding!.clauseReference,
                    description: targetFinding!.description,
                    evidence: targetFinding!.evidence,
                    responsiblePersonId: targetFinding!.responsiblePersonId,
                    dueDate: targetFinding!.dueDate,
                    resolutionNotes: notes.trim(),
                    resolvedDate: new Date().toISOString(),
                  }),
                )
              }
            >
              Record resolution
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={dialog === 'verify-finding'} onOpenChange={(o) => !o && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Verify effectiveness — finding #{targetFinding?.findingNumber}</DialogTitle>
            <DialogDescription>Confirms the resolution actually works. Closing follows.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Verified by</Label>
              <EmployeePicker value={actorId} onChange={setActorId} />
            </div>
            <div className="space-y-2">
              <Label>Verification notes</Label>
              <Textarea rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || !targetFinding || !actorId}
              onClick={() =>
                void act('Finding verified', () =>
                  safetyAuditService.verifyFinding(targetFinding!.id, actorId!, notes || null),
                )
              }
            >
              Verify
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={dialog === 'add-action'} onOpenChange={(o) => !o && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add corrective action — finding #{targetFinding?.findingNumber}</DialogTitle>
            <DialogDescription>
              Appears in the unified corrective-action tracker; overdue actions remind and escalate.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Template</Label>
              <Select value={templateId} onValueChange={setTemplateId}>
                <SelectTrigger><SelectValue placeholder="Pick a corrective-action template" /></SelectTrigger>
                <SelectContent>
                  {templates.map((t) => (
                    <SelectItem key={t.id} value={t.id}>{t.title}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Assigned to</Label>
                <EmployeePicker value={actorId} onChange={setActorId} />
              </div>
              <div className="space-y-2">
                <Label>Due date</Label>
                <Input type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || !targetFinding || !templateId}
              onClick={() =>
                void act('Action added', () =>
                  safetyAuditService.addFindingAction({
                    findingId: targetFinding!.id,
                    correctiveActionTemplateId: templateId,
                    dueDate: dueDate ? new Date(dueDate).toISOString() : null,
                    assignedToId: actorId,
                  }),
                )
              }
            >
              Add action
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
