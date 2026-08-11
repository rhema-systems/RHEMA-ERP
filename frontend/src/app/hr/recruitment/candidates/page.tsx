'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Plus, Search, Star, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useAuth } from '@/hooks/use-auth';
import { jobCandidateService } from '@/services/hr/recruitment-pipeline.service';
import type { JobCandidateSummary } from '@/types/hr/recruitment-pipeline';

function CandidateTable({
  rows,
  isLoading,
  emptyTitle,
  emptyDescription,
}: {
  rows: JobCandidateSummary[];
  isLoading: boolean;
  emptyTitle: string;
  emptyDescription: string;
}) {
  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (rows.length === 0) {
    return <EmptyState icon={Users} title={emptyTitle} description={emptyDescription} />;
  }

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead className="w-36">Number</TableHead>
          <TableHead>Name</TableHead>
          <TableHead>Email</TableHead>
          <TableHead className="w-36">Phone</TableHead>
          <TableHead>Location</TableHead>
          <TableHead className="w-32">Talent pool</TableHead>
          <TableHead className="w-28 text-right">Applications</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((c) => (
          <TableRow key={c.id}>
            <TableCell className="font-mono text-xs text-muted-foreground">{c.candidateNumber}</TableCell>
            <TableCell className="font-medium">
              <Link href={`/hr/recruitment/candidates/${c.id}`} className="hover:underline">
                {c.fullName}
              </Link>
            </TableCell>
            <TableCell className="text-sm">{c.email}</TableCell>
            <TableCell className="text-sm">{c.phone || '—'}</TableCell>
            <TableCell className="text-sm text-muted-foreground">
              {[c.city, c.countryName].filter(Boolean).join(', ') || '—'}
            </TableCell>
            <TableCell>{c.isInTalentPool ? <StatusBadge status="Active" /> : '—'}</TableCell>
            <TableCell className="text-right tabular-nums">{c.applicationCount}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

/**
 * Candidates — the people behind applications.
 *
 * ⚠ HR-only, including the reads: these records carry date of birth, contact details, CVs and
 * recruiters' private notes.
 *
 * The list has no server-side search, so the search box resolves an **exact email** through
 * `GET /email/{email}` — the endpoint that exists for spotting a duplicate before creating one.
 * It returns null rather than 404 when nobody matches.
 */
export default function CandidatesPage() {
  const { hasAnyRole } = useAuth();
  const isHr = hasAnyRole(['SuperAdmin', 'HR']);

  const [page, setPage] = useState(1);
  const [emailTerm, setEmailTerm] = useState('');
  const [lookupEmail, setLookupEmail] = useState('');

  const list = useQuery({
    queryKey: ['hr', 'candidates', page],
    queryFn: () => jobCandidateService.getPaged(page, 20),
  });

  const pool = useQuery({
    queryKey: ['hr', 'candidates', 'talent-pool'],
    queryFn: () => jobCandidateService.getTalentPool(),
  });

  const lookup = useQuery({
    queryKey: ['hr', 'candidate-by-email', lookupEmail],
    queryFn: () => jobCandidateService.getByEmail(lookupEmail),
    enabled: lookupEmail.length > 0,
  });

  const paged = list.data;
  const totalPages = paged?.totalPages ?? 1;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Candidates"
        description="Everyone who has applied, plus the talent pool kept for future vacancies."
        backHref="/hr/recruitment"
        actions={
          isHr ? (
            <Button asChild>
              <Link href="/hr/recruitment/candidates/new">
                <Plus className="mr-2 h-4 w-4" />
                New candidate
              </Link>
            </Button>
          ) : undefined
        }
      />

      <Card>
        <CardContent className="flex flex-wrap items-end gap-3 pt-6">
          <div className="min-w-[280px] flex-1 space-y-1.5">
            <label className="text-xs text-muted-foreground" htmlFor="candidate-email">
              Find by email address
            </label>
            <div className="flex gap-2">
              <Input
                id="candidate-email"
                value={emailTerm}
                onChange={(e) => setEmailTerm(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && setLookupEmail(emailTerm.trim())}
                placeholder="candidate@example.com"
              />
              <Button variant="secondary" onClick={() => setLookupEmail(emailTerm.trim())}>
                <Search className="mr-2 h-4 w-4" />
                Find
              </Button>
              {lookupEmail && (
                <Button
                  variant="ghost"
                  onClick={() => {
                    setLookupEmail('');
                    setEmailTerm('');
                  }}
                >
                  Clear
                </Button>
              )}
            </div>
            <p className="text-xs text-muted-foreground">
              Exact match. Use this before creating a candidate — the API refuses a duplicate email.
            </p>
          </div>
        </CardContent>
      </Card>

      {lookupEmail && (
        <Card>
          <CardContent className="pt-6">
            {lookup.isLoading ? (
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            ) : lookup.data ? (
              <div className="flex items-center justify-between">
                <div>
                  <Link
                    href={`/hr/recruitment/candidates/${lookup.data.id}`}
                    className="font-medium hover:underline"
                  >
                    {lookup.data.fullName}
                  </Link>
                  <p className="text-sm text-muted-foreground">
                    {lookup.data.candidateNumber} · {lookup.data.email}
                  </p>
                </div>
                <StatusBadge status={lookup.data.isInTalentPool ? 'Active' : 'Inactive'} />
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">
                No candidate has that email address — it is free to use on a new record.
              </p>
            )}
          </CardContent>
        </Card>
      )}

      <Tabs defaultValue="all">
        <TabsList>
          <TabsTrigger value="all">All candidates</TabsTrigger>
          <TabsTrigger value="pool">
            <Star className="mr-2 h-3.5 w-3.5" />
            Talent pool
          </TabsTrigger>
        </TabsList>

        <TabsContent value="all" className="mt-4 space-y-4">
          <Card>
            <CardContent className="p-0">
              <CandidateTable
                rows={paged?.items ?? []}
                isLoading={list.isLoading}
                emptyTitle="No candidates yet"
                emptyDescription="Candidates arrive with applications, or can be added by hand."
              />
            </CardContent>
          </Card>

          {(paged?.totalCount ?? 0) > 0 && (
            <div className="flex items-center justify-between text-sm text-muted-foreground">
              <span>
                Page {paged?.page ?? 1} of {totalPages} · {paged?.totalCount ?? 0} candidates
              </span>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!paged?.hasPrevious}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!paged?.hasNext}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </TabsContent>

        <TabsContent value="pool" className="mt-4">
          <Card>
            <CardContent className="p-0">
              <CandidateTable
                rows={pool.data ?? []}
                isLoading={pool.isLoading}
                emptyTitle="Talent pool is empty"
                emptyDescription="Add a strong candidate to the pool from their record to keep them in view for future vacancies."
              />
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
