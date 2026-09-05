'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Briefcase, Loader2, MapPin, Search } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { formatDate } from '@/lib/hr/attendance-format';
import { publicCareersService } from '@/services/hr/careers.service';

/**
 * The anonymous public job board (api/public). Browse public, apply logged-in — the apply
 * action lives on the vacancy page and requires a candidate account.
 */
export default function CareersBoardPage() {
  const [search, setSearch] = useState('');

  const tenant = useQuery({
    queryKey: ['careers', 'tenant'],
    queryFn: () => publicCareersService.resolveTenant(),
    staleTime: Infinity,
  });

  const vacancies = useQuery({
    queryKey: ['careers', 'vacancies', tenant.data?.id, search],
    queryFn: () => publicCareersService.getVacancies(tenant.data?.id ?? '', { search: search || undefined }),
    enabled: !!tenant.data?.id,
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Open positions{tenant.data?.name ? ` at ${tenant.data.name}` : ''}</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Browse freely — you&apos;ll need an account to apply, so we can keep you posted on your application.
        </p>
      </div>

      <div className="relative max-w-md">
        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          className="pl-9"
          placeholder="Search roles…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </div>

      {tenant.isError ? (
        <p className="text-sm text-destructive">The careers portal is not available right now.</p>
      ) : vacancies.isLoading || tenant.isLoading ? (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : (vacancies.data ?? []).length === 0 ? (
        <Card>
          <CardContent className="py-12 text-center text-muted-foreground">
            <Briefcase className="mx-auto mb-3 h-8 w-8" />
            No open positions right now — check back soon.
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-3">
          {(vacancies.data ?? []).map((v) => (
            <Link key={v.id} href={`/careers/${v.id}`} className="block">
              <Card className="transition-colors hover:border-primary/50">
                <CardContent className="flex flex-wrap items-center justify-between gap-3 py-4">
                  <div>
                    <div className="font-medium">{v.jobTitle}</div>
                    <div className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-muted-foreground">
                      {v.departmentName && <span>{v.departmentName}</span>}
                      {v.locationName && (
                        <span className="flex items-center gap-1">
                          <MapPin className="h-3.5 w-3.5" />
                          {v.locationName}
                        </span>
                      )}
                      <span>{v.employmentTypeName}</span>
                      <span>{v.workModeName}</span>
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    {v.isSalaryVisible && v.salaryRangeMin != null && (
                      <Badge variant="secondary">
                        {v.salaryCurrencyCode ?? ''} {v.salaryRangeMin?.toLocaleString()} – {v.salaryRangeMax?.toLocaleString()}
                      </Badge>
                    )}
                    {v.applicationDeadline && (
                      <span className="text-xs text-muted-foreground">
                        Apply by {formatDate(v.applicationDeadline)}
                      </span>
                    )}
                  </div>
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
