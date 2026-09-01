'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Activity } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { trainingNominationService } from '@/services/hr/training-nomination.service';
import { trainingRequestService } from '@/services/hr/training-request.service';
import { trainingCompletionService } from '@/services/hr/training-completion.service';
import { trainingCertificateService } from '@/services/hr/training-certificate.service';
import { trainingComplianceService } from '@/services/hr/training-compliance.service';

const fmt = (d?: string | null) => (d ? new Date(d).toLocaleDateString() : '—');
const spaced = (v?: string | null) => (v ? v.replace(/([a-z])([A-Z0-9])/g, '$1 $2') : '—');

function Loading() {
  return (
    <div className="space-y-2 p-4">
      {[...Array(3)].map((_, i) => (
        <Skeleton key={i} className="h-5 w-full" />
      ))}
    </div>
  );
}

/**
 * Everything one employee has done, or is booked to do, in training — nominations, requests,
 * completions, certificates and mandatory compliance — on one desk screen.
 *
 * Finish-plan lane 4 (2026-09-01). TDC's demo feedback asked for a grouped "Training Activities"
 * screen. The self-service portal already had this shape for the person themselves (My Training);
 * the desk did not — an officer answering "what training has this person had?" had to open five
 * registers and filter each. Every read here is a per-employee endpoint that already existed.
 */
export default function TrainingActivitiesPage() {
  const [employeeId, setEmployeeId] = useState<string | null>(null);
  const [employeeLabel, setEmployeeLabel] = useState<string | null>(null);
  const enabled = !!employeeId;
  const id = employeeId ?? '';

  const nominations = useQuery({
    queryKey: ['hr', 'training', 'activities', id, 'nominations'],
    queryFn: () => trainingNominationService.getByEmployee(id),
    enabled,
  });
  const requests = useQuery({
    queryKey: ['hr', 'training', 'activities', id, 'requests'],
    queryFn: () => trainingRequestService.getByEmployee(id),
    enabled,
  });
  const completions = useQuery({
    queryKey: ['hr', 'training', 'activities', id, 'completions'],
    queryFn: () => trainingCompletionService.getByEmployee(id),
    enabled,
  });
  const certificates = useQuery({
    queryKey: ['hr', 'training', 'activities', id, 'certificates'],
    queryFn: () => trainingCertificateService.getForEmployee(id),
    enabled,
  });
  const compliance = useQuery({
    queryKey: ['hr', 'training', 'activities', id, 'compliance'],
    queryFn: () => trainingComplianceService.getRecordsForEmployee(id),
    enabled,
  });

  const noms = nominations.data ?? [];
  const reqs = requests.data ?? [];
  const comps = completions.data ?? [];
  const certs = certificates.data ?? [];
  const compl = compliance.data ?? [];
  const overdue = compl.filter((c) => c.isOverdue && !c.isExempt).length;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Training Activities"
        description="One employee's training, grouped: what they are booked on, asked for, completed, hold a certificate for, and still owe."
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Employee</CardTitle>
          <CardDescription>Pick the person whose training record you want to see.</CardDescription>
        </CardHeader>
        <CardContent className="max-w-md">
          <EmployeePicker
            value={employeeId}
            initialLabel={employeeLabel}
            onChange={(v, label) => {
              setEmployeeId(v);
              setEmployeeLabel(label);
            }}
          />
        </CardContent>
      </Card>

      {!employeeId ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={Activity}
              title="No employee chosen"
              description="Choose an employee above to see their training activities."
            />
          </CardContent>
        </Card>
      ) : (
        <Tabs defaultValue="nominations">
          <TabsList className="flex-wrap">
            <TabsTrigger value="nominations">Nominations ({noms.length})</TabsTrigger>
            <TabsTrigger value="requests">Requests ({reqs.length})</TabsTrigger>
            <TabsTrigger value="completions">Completions ({comps.length})</TabsTrigger>
            <TabsTrigger value="certificates">Certificates ({certs.length})</TabsTrigger>
            <TabsTrigger value="compliance">
              Mandatory ({compl.length}
              {overdue > 0 ? `, ${overdue} overdue` : ''})
            </TabsTrigger>
          </TabsList>

          <TabsContent value="nominations" className="pt-4">
            <Card>
              <CardContent className="p-0">
                {nominations.isLoading ? (
                  <Loading />
                ) : noms.length === 0 ? (
                  <EmptyState icon={Activity} title="No nominations" description="Never booked onto a schedule." />
                ) : (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Nomination</TableHead>
                        <TableHead>Programme</TableHead>
                        <TableHead>Type</TableHead>
                        <TableHead>Starts</TableHead>
                        <TableHead>Status</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {noms.map((n) => (
                        <TableRow key={n.id}>
                          <TableCell className="font-mono text-xs">
                            <Link className="underline" href={`/hr/training/nominations/${n.id}`}>
                              {n.nominationNumber}
                            </Link>
                          </TableCell>
                          <TableCell>{n.programName}</TableCell>
                          <TableCell className="text-muted-foreground">{n.type}</TableCell>
                          <TableCell>{fmt(n.trainingStartDate)}</TableCell>
                          <TableCell>
                            <StatusBadge status={spaced(n.status)} />
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="requests" className="pt-4">
            <Card>
              <CardContent className="p-0">
                {requests.isLoading ? (
                  <Loading />
                ) : reqs.length === 0 ? (
                  <EmptyState icon={Activity} title="No requests" description="Has not asked for training outside the catalogue." />
                ) : (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Request</TableHead>
                        <TableHead>Title</TableHead>
                        <TableHead>Linked programme</TableHead>
                        <TableHead>Requested</TableHead>
                        <TableHead>Status</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {reqs.map((r) => (
                        <TableRow key={r.id}>
                          <TableCell className="font-mono text-xs">
                            <Link className="underline" href={`/hr/training/requests/${r.id}`}>
                              {r.requestNumber}
                            </Link>
                          </TableCell>
                          <TableCell>{r.requestedTrainingTitle}</TableCell>
                          <TableCell className="text-muted-foreground">{r.linkedProgramName || '—'}</TableCell>
                          <TableCell>{fmt(r.requestDate)}</TableCell>
                          <TableCell>
                            <StatusBadge status={spaced(r.status)} />
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="completions" className="pt-4">
            <Card>
              <CardContent className="p-0">
                {completions.isLoading ? (
                  <Loading />
                ) : comps.length === 0 ? (
                  <EmptyState icon={Activity} title="No completions" description="No outcome has been recorded yet." />
                ) : (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Programme</TableHead>
                        <TableHead>Completed</TableHead>
                        <TableHead>Status</TableHead>
                        <TableHead className="text-right">Score</TableHead>
                        <TableHead>Result</TableHead>
                        <TableHead>Verified</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {comps.map((c) => (
                        <TableRow key={c.id}>
                          <TableCell>{c.programName}</TableCell>
                          <TableCell>{fmt(c.completionDate)}</TableCell>
                          <TableCell>
                            <StatusBadge status={spaced(c.status)} />
                          </TableCell>
                          <TableCell className="text-right">{c.finalScore ?? '—'}</TableCell>
                          <TableCell>
                            <Badge variant={c.isPassed ? 'default' : 'destructive'}>
                              {c.isPassed ? 'Passed' : 'Not passed'}
                            </Badge>
                          </TableCell>
                          <TableCell className="text-muted-foreground">
                            {c.isVerifiedByManager ? fmt(c.verificationDate) : 'Not yet'}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="certificates" className="pt-4">
            <Card>
              <CardContent className="p-0">
                {certificates.isLoading ? (
                  <Loading />
                ) : certs.length === 0 ? (
                  <EmptyState icon={Activity} title="Nothing issued" description="No training certificate has been issued to this employee." />
                ) : (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Certificate</TableHead>
                        <TableHead>Programme</TableHead>
                        <TableHead>Issued</TableHead>
                        <TableHead>Expires</TableHead>
                        <TableHead>Status</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {certs.map((c) => (
                        <TableRow key={c.id}>
                          <TableCell>
                            <div className="font-mono text-xs">{c.certificateNumber}</div>
                            <div className="text-xs text-muted-foreground">{c.certificateName}</div>
                          </TableCell>
                          <TableCell>{c.programName}</TableCell>
                          <TableCell>{fmt(c.issuedDate)}</TableCell>
                          <TableCell className={c.isExpired ? 'text-red-600' : ''}>
                            {c.expiryDate ? fmt(c.expiryDate) : 'Never'}
                          </TableCell>
                          <TableCell>
                            <StatusBadge status={spaced(c.status)} />
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="compliance" className="pt-4">
            <Card>
              <CardContent className="p-0">
                {compliance.isLoading ? (
                  <Loading />
                ) : compl.length === 0 ? (
                  <EmptyState icon={Activity} title="No mandatory training assigned" description="No compliance requirement applies to this employee." />
                ) : (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Requirement</TableHead>
                        <TableHead>Programme</TableHead>
                        <TableHead>Status</TableHead>
                        <TableHead>Last completed</TableHead>
                        <TableHead>Next due</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {compl.map((c) => (
                        <TableRow key={c.id}>
                          <TableCell>
                            <div className="font-medium">{c.requirementName}</div>
                            <div className="text-xs text-muted-foreground">{c.requirementCode}</div>
                          </TableCell>
                          <TableCell>{c.programName}</TableCell>
                          <TableCell>
                            <div className="flex flex-wrap items-center gap-1">
                              <StatusBadge status={spaced(c.status)} />
                              {c.isOverdue && !c.isExempt && <Badge variant="destructive">Overdue</Badge>}
                              {c.isExempt && <Badge variant="outline">Exempt</Badge>}
                            </div>
                          </TableCell>
                          <TableCell>{fmt(c.lastCompletedDate)}</TableCell>
                          <TableCell className={c.isOverdue && !c.isExempt ? 'text-red-600' : ''}>
                            {fmt(c.nextDueDate)}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </CardContent>
            </Card>
          </TabsContent>
        </Tabs>
      )}
    </div>
  );
}
