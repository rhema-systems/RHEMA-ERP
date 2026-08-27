'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Briefcase, ClipboardCheck, FileEdit } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { formatDate } from '@/lib/hr/attendance-format';
import { jobApplicationService } from '@/services/hr/recruitment-pipeline.service';

/**
 * The applications this employee has made through the internal job board (area 25 slice 13b).
 *
 * Split out of the job board's second tab, because it is a record with a life of its own: a draft
 * to finish, a status to follow, a decision to withdraw. Drafts are listed — they are the reason
 * the split earns its place, since a draft on a tab nobody opens is a draft nobody finishes.
 */
export default function MyApplicationsPage() {
  const { data: applications = [], isLoading, isError } = useQuery({
    queryKey: ['me', 'jobs', 'my-applications'],
    queryFn: () => jobApplicationService.getMyApplications(),
  });

  const drafts = applications.filter((a) => a.status === 'Draft');
  const live = applications.filter((a) => a.status !== 'Draft');

  return (
    <div className="space-y-6">
      <PageHeader
        title="My applications"
        description="Roles you have applied for, and drafts you have not sent yet."
        backHref="/me/jobs"
        actions={
          <Button variant="outline" asChild>
            <Link href="/me/jobs">
              <Briefcase className="mr-2 h-4 w-4" /> Job board
            </Link>
          </Button>
        }
      />

      {isLoading ? (
        <Skeleton className="h-48" />
      ) : isError ? (
        <p className="text-sm text-muted-foreground">
          Your applications could not be loaded right now. Try again in a moment.
        </p>
      ) : applications.length === 0 ? (
        <EmptyState
          icon={ClipboardCheck}
          title="No applications yet"
          description="Roles you apply for on the internal job board appear here."
        />
      ) : (
        <div className="space-y-4">
          {drafts.length > 0 && (
            <Card className="border-amber-500/40">
              <CardContent className="p-4">
                <p className="mb-3 flex items-center gap-2 text-sm font-medium">
                  <FileEdit className="h-4 w-4 text-amber-600" />
                  {drafts.length === 1 ? 'A draft you have not sent' : `${drafts.length} drafts you have not sent`}
                </p>
                <div className="space-y-2">
                  {drafts.map((a) => (
                    <Link
                      key={a.id}
                      href={`/me/jobs/applications/${a.id}`}
                      className="flex items-center gap-3 rounded-md border p-3 text-sm hover:bg-muted/50"
                    >
                      <span className="min-w-0 flex-1 truncate font-medium">{a.jobTitle}</span>
                      <Badge variant="outline">Draft</Badge>
                    </Link>
                  ))}
                </div>
              </CardContent>
            </Card>
          )}

          {live.length > 0 && (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Reference</TableHead>
                      <TableHead>Role</TableHead>
                      <TableHead>Applied</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="w-24" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {live.map((a) => (
                      <TableRow key={a.id}>
                        <TableCell className="font-mono text-xs">{a.applicationNumber}</TableCell>
                        <TableCell className="font-medium">{a.jobTitle}</TableCell>
                        <TableCell className="text-sm">{formatDate(a.applicationDate)}</TableCell>
                        <TableCell>
                          <StatusBadge status={a.status} />
                        </TableCell>
                        <TableCell className="text-right">
                          <Button variant="ghost" size="sm" asChild>
                            <Link href={`/me/jobs/applications/${a.id}`}>Open</Link>
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </div>
      )}
    </div>
  );
}
