'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { CalendarClock, HeartPulse, Receipt, ShieldPlus, Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { medicalMeService } from '@/services/hr/medical-me.service';

/**
 * My Medical — the coverage hub (area 25 slice 8, spec #27).
 *
 * Built from nothing: the census found NO self-shaped coverage read anywhere, so this rides the
 * new `api/medical/me/insurance-policies` self arm. The active policy with its utilisation is the
 * centrepiece; claims, appointments and the health record are one hop away. Everything is
 * token-scoped — no id on this screen identifies anybody else.
 */
const money = (v: number) =>
  v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const LINKS = [
  {
    href: '/me/medical/claims',
    icon: Receipt,
    title: 'My Medical Claims',
    text: 'File a reimbursement claim and follow it through assessment.',
  },
  {
    href: '/me/medical/appointments',
    icon: CalendarClock,
    title: 'My Appointments',
    text: 'Clinic appointments booked for you, past and upcoming.',
  },
  {
    href: '/me/medical/health',
    icon: HeartPulse,
    title: 'My Health Record',
    text: 'Your health profile, examinations and workplace health surveillance.',
  },
] as const;

export default function MyMedicalPage() {
  // 404 = no active policy; that is the empty state, not an error.
  const { data: policy, isLoading: policyLoading } = useQuery({
    queryKey: ['me', 'medical', 'active-policy'],
    queryFn: () => medicalMeService.getActivePolicy(),
    retry: false,
  });

  const { data: dependents = [] } = useQuery({
    queryKey: ['me', 'medical', 'policy-dependents', policy?.id],
    queryFn: () => medicalMeService.getPolicyDependents(policy?.id as string),
    enabled: !!policy?.id && policy.coversDependents,
  });

  const utilisation = policy
    ? Math.min(100, Math.max(0, Math.round(policy.utilizationPercentage)))
    : 0;

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Medical"
        description="Your insurance coverage, claims, appointments and health record — in one place."
        backHref="/me"
      />

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <ShieldPlus className="h-5 w-5" />
            My coverage
          </CardTitle>
        </CardHeader>
        <CardContent>
          {policyLoading ? null : !policy ? (
            <p className="text-muted-foreground text-sm">
              No active medical insurance policy is on record for you. If you believe you are
              covered, contact HR — coverage appears here once HR registers the policy.
            </p>
          ) : (
            <div className="space-y-4">
              <div className="flex flex-wrap items-center gap-2">
                <span className="font-medium">{policy.providerName}</span>
                <span className="text-muted-foreground">· {policy.planName}</span>
                {policy.benefitTierName && (
                  <span className="text-muted-foreground">· {policy.benefitTierName}</span>
                )}
                <Badge variant="secondary">{policy.statusName}</Badge>
              </div>
              <div className="text-muted-foreground grid gap-x-8 gap-y-1 text-sm sm:grid-cols-2">
                <span>
                  Policy <span className="text-foreground font-mono">{policy.policyNumber}</span>
                </span>
                {policy.membershipNumber && (
                  <span>
                    Membership{' '}
                    <span className="text-foreground font-mono">{policy.membershipNumber}</span>
                  </span>
                )}
                <span>
                  Valid {fmtDate(policy.startDate)} —{' '}
                  {policy.endDate ? fmtDate(policy.endDate) : 'open-ended'}
                </span>
              </div>
              <div className="space-y-1">
                <div className="flex items-baseline justify-between text-sm">
                  <span className="text-muted-foreground">Annual limit used</span>
                  <span className="tabular-nums">
                    {money(policy.utilizedAmount)} / {money(policy.annualLimit)} ·{' '}
                    <span className="font-medium">{money(policy.remainingLimit)} left</span>
                  </span>
                </div>
                <div className="bg-muted h-2 w-full overflow-hidden rounded-full">
                  <div
                    className={`h-full rounded-full ${utilisation >= 90 ? 'bg-destructive' : 'bg-primary'}`}
                    style={{ width: `${utilisation}%` }}
                  />
                </div>
              </div>
              {policy.coversDependents && (
                <div className="space-y-2">
                  <p className="flex items-center gap-2 text-sm font-medium">
                    <Users className="h-4 w-4" /> Covered dependants
                  </p>
                  {dependents.length === 0 ? (
                    <p className="text-muted-foreground text-sm">
                      Your policy covers dependants, but none are registered yet — contact HR to
                      add them.
                    </p>
                  ) : (
                    <ul className="text-muted-foreground space-y-1 text-sm">
                      {dependents.map((d) => (
                        <li key={d.id} className="flex flex-wrap items-center gap-2">
                          <span className="text-foreground">{d.dependentName}</span>
                          {d.relationship && <span>· {d.relationship}</span>}
                          <span className="tabular-nums">
                            · {money(d.remainingLimit)} of {money(d.annualLimit)} left
                          </span>
                          {!d.isActive && <Badge variant="outline">Inactive</Badge>}
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      <div className="grid gap-4 md:grid-cols-3">
        {LINKS.map(({ href, icon: Icon, title, text }) => (
          <Link key={href} href={href}>
            <Card className="hover:bg-muted/50 h-full transition-colors">
              <CardContent className="flex items-start gap-3 p-4">
                <Icon className="text-muted-foreground mt-0.5 h-5 w-5 shrink-0" />
                <div>
                  <p className="font-medium">{title}</p>
                  <p className="text-muted-foreground text-sm">{text}</p>
                </div>
              </CardContent>
            </Card>
          </Link>
        ))}
      </div>
    </div>
  );
}
