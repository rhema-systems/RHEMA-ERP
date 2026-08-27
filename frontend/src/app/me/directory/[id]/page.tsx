'use client';

import { useParams } from 'next/navigation';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import {
  Briefcase,
  ChevronRight,
  Mail,
  MapPin,
  Phone,
  UserRound,
  Users,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  staffDirectoryService,
  type StaffDirectoryEntry,
} from '@/services/hr/staff-directory.service';

/**
 * One colleague's directory card (area 25 slice 13a).
 *
 * ⚠ This is **not** an employee profile. Everything here is organisational — role, unit path,
 * reporting line, work email. Date of birth, home address, SSNIT/TIN, salary and bank details
 * live behind `HR.Employee.Read` on the desk record and are not projected into this surface at
 * any point. The one personal profile an employee can open is their own, at `/me/profile`.
 *
 * A person who has left, or who never existed, is a 404 — a lookup miss, not a refusal. There is
 * nothing here worth hiding the existence of.
 */

function initials(name: string) {
  const parts = name.split(/\s+/).filter(Boolean);
  if (!parts.length) return '·';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

function PersonLine({ person }: { person: StaffDirectoryEntry }) {
  return (
    <Link
      href={`/me/directory/${person.id}`}
      className="flex items-center gap-3 rounded-md px-2 py-2 hover:bg-muted/60"
    >
      <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary/10 text-[10px] font-semibold text-primary">
        {initials(person.fullName)}
      </span>
      <span className="min-w-0 flex-1">
        <span className="flex items-center gap-2">
          <span className="truncate text-sm font-medium">{person.fullName}</span>
          {person.isSelf && (
            <Badge variant="secondary" className="shrink-0 text-[10px]">
              You
            </Badge>
          )}
        </span>
        <span className="block truncate text-xs text-muted-foreground">{person.positionTitle}</span>
      </span>
    </Link>
  );
}

export default function DirectoryCardPage() {
  const params = useParams();
  const id = params.id as string;

  const { data: person, isLoading, isError, error } = useQuery({
    queryKey: ['me', 'directory', 'card', id],
    queryFn: () => staffDirectoryService.getProfile(id),
    retry: false,
  });

  const notFound = isError && (error as { status?: number })?.status === 404;

  if (isLoading) {
    return (
      <div className="space-y-6">
        <PageHeader title="Staff directory" backHref="/me/directory" />
        <Skeleton className="h-44" />
        <Skeleton className="h-32" />
      </div>
    );
  }

  if (notFound || !person) {
    return (
      <div className="space-y-6">
        <PageHeader title="Staff directory" backHref="/me/directory" />
        <EmptyState
          icon={UserRound}
          title="Nobody by that record"
          description="This person is not listed in the directory. They may have left the organisation."
        />
      </div>
    );
  }

  const phone = person.extension ?? person.businessNumber;

  return (
    <div className="space-y-6">
      <PageHeader
        title={person.fullName}
        description={person.positionTitle}
        backHref="/me/directory"
      />

      <Card>
        <CardContent className="flex flex-col gap-5 p-6 sm:flex-row sm:items-start">
          <span className="flex h-20 w-20 shrink-0 items-center justify-center rounded-full bg-primary/10 text-xl font-semibold text-primary">
            {initials(person.fullName)}
          </span>

          <div className="min-w-0 flex-1 space-y-4">
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <h2 className="text-lg font-semibold">{person.fullName}</h2>
                {person.isSelf && <Badge variant="secondary">This is you</Badge>}
              </div>
              <p className="text-sm text-muted-foreground">
                {person.positionTitle}
                {person.organizationLevelName ? ` · ${person.organizationLevelName}` : ''}
              </p>
              <p className="mt-1 font-mono text-xs text-muted-foreground">{person.employeeNumber}</p>
            </div>

            <div className="grid gap-3 text-sm sm:grid-cols-2">
              <a
                href={`mailto:${person.emailAddress}`}
                className="flex items-center gap-2 text-primary hover:underline"
              >
                <Mail className="h-4 w-4 shrink-0" />
                <span className="truncate">{person.emailAddress}</span>
              </a>

              {/* No employee on DEFAULT has a work number, so this is absent rather than blank. */}
              {phone && (
                <span className="flex items-center gap-2">
                  <Phone className="h-4 w-4 shrink-0 text-muted-foreground" />
                  {phone}
                </span>
              )}

              {person.locationName && (
                <span className="flex items-center gap-2">
                  <MapPin className="h-4 w-4 shrink-0 text-muted-foreground" />
                  {person.locationName}
                </span>
              )}

              {person.organizationUnitName && (
                <span className="flex items-center gap-2">
                  <Briefcase className="h-4 w-4 shrink-0 text-muted-foreground" />
                  {person.organizationUnitName}
                </span>
              )}
            </div>

            {/* Root-first, so it reads the way an org chart is described out loud. */}
            {person.unitPath.length > 0 && (
              <div className="flex flex-wrap items-center gap-1 text-xs text-muted-foreground">
                {person.unitPath.map((step, i) => (
                  <span key={`${step}-${i}`} className="flex items-center gap-1">
                    {i > 0 && <ChevronRight className="h-3 w-3" />}
                    <span className={i === person.unitPath.length - 1 ? 'font-medium text-foreground' : ''}>
                      {step}
                    </span>
                  </span>
                ))}
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium">Reports to</CardTitle>
          </CardHeader>
          <CardContent>
            {person.managerId && person.managerName ? (
              <Button variant="ghost" className="h-auto w-full justify-start px-2 py-2" asChild>
                <Link href={`/me/directory/${person.managerId}`}>
                  <span className="flex items-center gap-3">
                    <span className="flex h-8 w-8 items-center justify-center rounded-full bg-primary/10 text-[10px] font-semibold text-primary">
                      {initials(person.managerName)}
                    </span>
                    <span className="text-left">
                      <span className="block text-sm font-medium">{person.managerName}</span>
                      <span className="block text-xs text-muted-foreground">
                        {person.managerPositionTitle ?? ''}
                      </span>
                    </span>
                  </span>
                </Link>
              </Button>
            ) : (
              // Honest, not apologetic: only ~5% of employee records carry a manager today.
              <p className="px-2 py-2 text-sm text-muted-foreground">
                No reporting line is recorded for this person.
              </p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-sm font-medium">
              <Users className="h-4 w-4" />
              Direct reports
              {person.directReports.length > 0 && (
                <Badge variant="secondary">{person.directReports.length}</Badge>
              )}
            </CardTitle>
          </CardHeader>
          <CardContent>
            {person.directReports.length === 0 ? (
              <p className="px-2 py-2 text-sm text-muted-foreground">
                Nobody is recorded as reporting to this person.
              </p>
            ) : (
              <div className="-mx-2">
                {person.directReports.map((r) => (
                  <PersonLine key={r.id} person={r} />
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
