'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import {
  AlertTriangle,
  ClipboardList,
  FileText,
  Loader2,
  Plus,
  Search,
  Users,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
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
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import type { JobDescription, JobDescriptionStatus } from '@/types/hr/job-architecture';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const STATUS_TONE: Record<JobDescriptionStatus, string> = {
  Draft: 'bg-slate-100 text-slate-700',
  PendingReview: 'bg-amber-100 text-amber-800',
  UnderRevision: 'bg-orange-100 text-orange-800',
  Approved: 'bg-emerald-100 text-emerald-800',
  Active: 'bg-emerald-100 text-emerald-800',
  Superseded: 'bg-slate-100 text-slate-500',
  Archived: 'bg-slate-100 text-slate-500',
};

/** `statusName` carries the enum NAME, not its [Description] label — "PendingReview", not "Pending Review". */
const STATUS_LABEL: Record<JobDescriptionStatus, string> = {
  Draft: 'Draft',
  PendingReview: 'Pending review',
  UnderRevision: 'Under revision',
  Approved: 'Approved',
  Active: 'Active',
  Superseded: 'Superseded',
  Archived: 'Archived',
};

export default function JobDescriptionsPage() {
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<JobDescriptionStatus | 'all'>('all');

  const { data, isLoading, isError } = useQuery({
    queryKey: ['job-descriptions', 'paged'],
    queryFn: () => jobArchitectureService.getJobDescriptionsPaged({ pageNumber: 1, pageSize: 200 }),
  });

  // Coverage sits on the register rather than a separate dashboard: the useful question about job
  // descriptions is not "how many are there" but "how many positions still have none".
  const { data: analytics } = useQuery({
    queryKey: ['job-descriptions', 'analytics'],
    queryFn: () => jobArchitectureService.getAnalytics(),
  });

  const rows = useMemo(() => {
    const all = data?.items ?? [];
    const term = search.trim().toLowerCase();
    return all.filter((jd) => {
      if (status !== 'all' && (jd.statusName ?? jd.status) !== status) return false;
      if (!term) return true;
      return (
        jd.jobTitle?.toLowerCase().includes(term) ||
        jd.positionTitle?.toLowerCase().includes(term) ||
        jd.jobDescriptionNumber?.toLowerCase().includes(term)
      );
    });
  }, [data, search, status]);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Job descriptions"
        description="What each position is accountable for, and the version currently in force (FR-HR-134)."
        actions={
          <div className="flex gap-2">
            <Link href="/hr/job-descriptions/gaps">
              <Button variant="outline">
                <ClipboardList className="mr-2 h-4 w-4" />
                Coverage
              </Button>
            </Link>
            <Link href="/hr/job-descriptions/new">
              <Button>
                <Plus className="mr-2 h-4 w-4" />
                New job description
              </Button>
            </Link>
          </div>
        }
      />

      {analytics && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <SummaryCard
            icon={<FileText className="h-4 w-4" />}
            label="Approved"
            value={analytics.approvedCount}
            hint={`${analytics.totalJobDescriptions} in total`}
          />
          <SummaryCard
            icon={<Users className="h-4 w-4" />}
            label="Positions covered"
            value={`${analytics.positionsCovered} / ${analytics.totalPositions}`}
            hint={`${analytics.positionsUncovered} with no approved description`}
          />
          <SummaryCard
            icon={<ClipboardList className="h-4 w-4" />}
            label="Due for review"
            value={analytics.dueForReviewCount}
            hint="within 30 days"
          />
          {/* The only tile here that is a problem rather than a progress figure. */}
          <SummaryCard
            icon={<AlertTriangle className="h-4 w-4" />}
            label="Posts over establishment"
            value={analytics.positionsOverStrength}
            hint={`${analytics.positionsEstablished} established`}
            tone={analytics.positionsOverStrength > 0 ? 'warning' : undefined}
          />
        </div>
      )}

      <Card>
        <CardContent className="space-y-4 pt-6">
          <div className="flex flex-col gap-3 sm:flex-row">
            <div className="relative flex-1">
              <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                placeholder="Search by job title, position or number"
                className="pl-9"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
            <Select value={status} onValueChange={(v) => setStatus(v as JobDescriptionStatus | 'all')}>
              <SelectTrigger className="sm:w-56">
                <SelectValue placeholder="All statuses" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                {(Object.keys(STATUS_LABEL) as JobDescriptionStatus[]).map((s) => (
                  <SelectItem key={s} value={s}>
                    {STATUS_LABEL[s]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {isLoading ? (
            <div className="flex items-center justify-center py-16 text-muted-foreground">
              <Loader2 className="mr-2 h-5 w-5 animate-spin" />
              Loading job descriptions…
            </div>
          ) : isError ? (
            <EmptyState
              icon={AlertTriangle}
              title="Could not load job descriptions"
              description="Try again, or check that you have permission to view them."
            />
          ) : rows.length === 0 ? (
            <EmptyState
              icon={FileText}
              title="No job descriptions match"
              description={
                search || status !== 'all'
                  ? 'Clear the filters to see everything.'
                  : 'Start by describing a position.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Job title</TableHead>
                  <TableHead>Position</TableHead>
                  <TableHead>Family</TableHead>
                  <TableHead>Level</TableHead>
                  <TableHead className="text-right">Version</TableHead>
                  <TableHead>Effective</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((jd: JobDescription) => {
                  const s = (jd.statusName ?? jd.status) as JobDescriptionStatus;
                  return (
                    <TableRow key={jd.id} className="cursor-pointer">
                      <TableCell className="font-mono text-xs">
                        <Link href={`/hr/job-descriptions/${jd.id}`} className="hover:underline">
                          {jd.jobDescriptionNumber}
                        </Link>
                      </TableCell>
                      <TableCell className="font-medium">
                        <Link href={`/hr/job-descriptions/${jd.id}`} className="hover:underline">
                          {jd.jobTitle}
                        </Link>
                      </TableCell>
                      <TableCell>{jd.positionTitle || '—'}</TableCell>
                      <TableCell>{jd.jobFamilyName || '—'}</TableCell>
                      <TableCell>{jd.jobLevelName || '—'}</TableCell>
                      <TableCell className="text-right">v{jd.versionNumber}</TableCell>
                      <TableCell>{fmtDate(jd.effectiveDate)}</TableCell>
                      <TableCell>
                        <Badge className={STATUS_TONE[s] ?? 'bg-slate-100 text-slate-700'}>
                          {STATUS_LABEL[s] ?? s}
                        </Badge>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function SummaryCard({
  icon,
  label,
  value,
  hint,
  tone,
}: {
  icon: React.ReactNode;
  label: string;
  value: React.ReactNode;
  hint?: string;
  tone?: 'warning';
}) {
  return (
    <Card>
      <CardContent className="pt-6">
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          {icon}
          {label}
        </div>
        <div
          className={`mt-2 text-2xl font-semibold ${
            tone === 'warning' ? 'text-amber-700' : ''
          }`}
        >
          {value}
        </div>
        {hint && <div className="mt-1 text-xs text-muted-foreground">{hint}</div>}
      </CardContent>
    </Card>
  );
}
