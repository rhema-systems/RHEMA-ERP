'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Plus, CheckCircle2, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { safetyRiskAssessmentService } from '@/services/hr/safety-risk-assessment.service';
import {
  SHE_RISK_ASSESSMENT_STATUS_OPTIONS,
  SHE_RISK_ASSESSMENT_TYPE_OPTIONS,
  type SheRiskAssessmentStatus,
  type SheRiskAssessmentType,
} from '@/types/hr/safety-hazards';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * The risk-assessment register (HIRA/JHA/Pre-Task), newest first. Preparation, hazard lines,
 * approval and acknowledgements all run from the assessment itself. The acknowledgement checker
 * below answers the floor question — "has this person signed the assessments that cover them?"
 */
export default function RiskAssessmentRegisterPage() {
  const [status, setStatus] = useState<SheRiskAssessmentStatus | 'all'>('all');
  const [type, setType] = useState<SheRiskAssessmentType | 'all'>('all');
  const [checkEmployeeId, setCheckEmployeeId] = useState<string | null>(null);
  const [checkEmployeeLabel, setCheckEmployeeLabel] = useState<string | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'safety-risk-assessments', status],
    queryFn: () =>
      status === 'all'
        ? safetyRiskAssessmentService.getAll()
        : safetyRiskAssessmentService.getByStatus(status),
  });

  const { data: ackRows = [], isLoading: ackLoading } = useQuery({
    queryKey: ['hr', 'safety-risk-assessments', 'for-acknowledgement', checkEmployeeId],
    queryFn: () => safetyRiskAssessmentService.getForAcknowledgement(checkEmployeeId!),
    enabled: !!checkEmployeeId,
  });

  const items = (data ?? []).filter((r) => type === 'all' || r.type === type);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Risk Assessments"
        description="Formal, versioned assessments — HIRA, job hazard analyses and pre-task assessments — from draft through approval to workforce sign-off."
        backHref="/hr/safety"
        actions={
          <Button asChild>
            <Link href="/hr/safety/risk-assessments/new">
              <Plus className="mr-2 h-4 w-4" />
              New assessment
            </Link>
          </Button>
        }
      />

      <div className="flex flex-wrap items-center gap-3">
        <Select value={status} onValueChange={(v) => setStatus(v as SheRiskAssessmentStatus | 'all')}>
          <SelectTrigger className="w-52">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            {SHE_RISK_ASSESSMENT_STATUS_OPTIONS.map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={type} onValueChange={(v) => setType(v as SheRiskAssessmentType | 'all')}>
          <SelectTrigger className="w-64">
            <SelectValue placeholder="All types" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All types</SelectItem>
            {SHE_RISK_ASSESSMENT_TYPE_OPTIONS.map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        {data && (
          <span className="text-muted-foreground text-sm">
            {items.length} assessment{items.length === 1 ? '' : 's'}
          </span>
        )}
      </div>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
            </div>
          ) : items.length === 0 ? (
            <EmptyState
              title="No risk assessments"
              description={
                status === 'all' && type === 'all'
                  ? 'Prepare the first assessment for a task, location or activity.'
                  : 'Nothing matches these filters.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Title</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Location</TableHead>
                  <TableHead>Prepared by</TableHead>
                  <TableHead>Prepared</TableHead>
                  <TableHead>Valid until</TableHead>
                  <TableHead className="text-right">Hazards</TableHead>
                  <TableHead className="text-right">v</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell className="font-medium">
                      <Link
                        href={`/hr/safety/risk-assessments/${r.id}`}
                        className="font-mono hover:underline"
                      >
                        {r.assessmentNumber}
                      </Link>
                    </TableCell>
                    <TableCell>{r.title}</TableCell>
                    <TableCell>{r.typeName}</TableCell>
                    <TableCell>{r.locationName ?? '—'}</TableCell>
                    <TableCell>{r.preparedByName || '—'}</TableCell>
                    <TableCell>{fmtDate(r.preparedDate)}</TableCell>
                    <TableCell>{fmtDate(r.validUntil)}</TableCell>
                    <TableCell className="text-right tabular-nums">{r.hazardCount}</TableCell>
                    <TableCell className="text-right tabular-nums">{r.version}</TableCell>
                    <TableCell>
                      <StatusBadge status={r.statusName} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Acknowledgement check</CardTitle>
          <CardDescription>
            Every approved or active assessment, with whether the chosen employee has signed it.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="max-w-md">
            <EmployeePicker
              value={checkEmployeeId}
              initialLabel={checkEmployeeLabel}
              onChange={(id, label) => {
                setCheckEmployeeId(id);
                setCheckEmployeeLabel(label);
              }}
            />
          </div>
          {checkEmployeeId &&
            (ackLoading ? (
              <div className="flex items-center justify-center py-8">
                <Loader2 className="text-muted-foreground h-5 w-5 animate-spin" />
              </div>
            ) : ackRows.length === 0 ? (
              <p className="text-muted-foreground text-sm">
                No approved or active assessments to acknowledge.
              </p>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Number</TableHead>
                    <TableHead>Title</TableHead>
                    <TableHead>Type</TableHead>
                    <TableHead>Location</TableHead>
                    <TableHead>Valid until</TableHead>
                    <TableHead>Signed</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {ackRows.map((a) => (
                    <TableRow key={a.riskAssessmentId}>
                      <TableCell>
                        <Link
                          href={`/hr/safety/risk-assessments/${a.riskAssessmentId}`}
                          className="font-mono hover:underline"
                        >
                          {a.assessmentNumber}
                        </Link>
                      </TableCell>
                      <TableCell>{a.title}</TableCell>
                      <TableCell>{a.typeName}</TableCell>
                      <TableCell>{a.locationName ?? '—'}</TableCell>
                      <TableCell>{fmtDate(a.validUntil)}</TableCell>
                      <TableCell>
                        {a.acknowledgedByMe ? (
                          <span className="flex items-center gap-1 text-green-700 dark:text-green-400">
                            <CheckCircle2 className="h-4 w-4" />
                            {fmtDate(a.acknowledgedDate)}
                          </span>
                        ) : (
                          <span className="flex items-center gap-1 text-red-700 dark:text-red-400">
                            <XCircle className="h-4 w-4" />
                            Not signed
                          </span>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            ))}
        </CardContent>
      </Card>
    </div>
  );
}
