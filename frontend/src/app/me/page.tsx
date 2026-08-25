'use client';

/**
 * Area 25 slice 3 — the portal landing, now the real personal aggregate.
 *
 * One `GET employee-portal/home` read powers the header numbers; every figure deep-links to
 * the detail screen whose service computed it, so the two can never honestly disagree (the
 * area-7 wrong-numbers lesson lives in the backend + harness, not here). If the aggregate
 * read fails, the quick links still render — the portal degrades, it does not die.
 * Payslip and announcement tiles appear in slices 10/12 when their stubs go live.
 */

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import {
  AlertTriangle,
  Award,
  BookOpen,
  Briefcase,
  CalendarDays,
  CheckCircle2,
  CheckSquare,
  ClipboardList,
  Clock,
  Compass,
  FileWarning,
  GraduationCap,
  HeartPulse,
  Laptop,
  MessageSquare,
  Plane,
  Rocket,
  ShieldAlert,
  Target,
  TreePalm,
  Users,
} from 'lucide-react';
import { useAuth } from '@/hooks/use-auth';
import { mePortalService } from '@/services/hr/me-portal.service';

interface Tile {
  label: string;
  hint: string;
  href: string;
  icon: React.ComponentType<{ className?: string }>;
}

const TILE_SECTIONS: { title: string; tiles: Tile[] }[] = [
  {
    title: 'My work life',
    tiles: [
      { label: 'My Leave', hint: 'Balances, requests and approvals', href: '/me/leave', icon: TreePalm },
      { label: 'My Attendance', hint: 'Punch in and out, see your month', href: '/me/attendance', icon: Clock },
      { label: 'My Travel', hint: 'Raise and track travel requests', href: '/hr/travel/mine', icon: Plane },
      { label: 'My Appraisals', hint: 'Reviews and self-evaluations', href: '/hr/performance/appraisals', icon: Target },
      { label: 'My Check-Ins', hint: 'Conversations with your manager', href: '/hr/performance/check-ins', icon: MessageSquare },
      { label: 'My Journal', hint: 'Your private work notes', href: '/hr/performance/journal', icon: BookOpen },
      { label: 'My Training', hint: 'Courses, requests and certificates', href: '/hr/training/my-training', icon: GraduationCap },
      { label: 'My Learning Paths', hint: 'Guided development journeys', href: '/hr/training/my-learning', icon: Compass },
      { label: 'My Orientations', hint: 'Onboarding checklists', href: '/hr/orientation/mine', icon: ClipboardList },
      { label: 'Peer Evaluations', hint: 'Feedback you owe colleagues', href: '/hr/performance/peer-reviews', icon: Users },
    ],
  },
  {
    title: 'Benefits, kit & safety',
    tiles: [
      { label: 'My Medical Claims', hint: 'File and follow expense claims', href: '/hr/medical/my-claims', icon: HeartPulse },
      { label: 'My Awards', hint: 'Nominate, vote, celebrate', href: '/hr/awards/me', icon: Award },
      { label: 'My Assets', hint: 'What you hold, and requests', href: '/hr/assets/me', icon: Laptop },
      { label: 'My PPE', hint: 'Protective equipment issued to you', href: '/hr/safety/my-ppe', icon: ShieldAlert },
      { label: 'Internal Job Board', hint: 'Openings you can apply for', href: '/hr/recruitment/job-board', icon: Briefcase },
    ],
  },
];

function ActionChip({
  count,
  label,
  href,
}: {
  count: number;
  label: string;
  href: string;
}) {
  if (count === 0) return null;
  return (
    <Link
      href={href}
      className="flex items-center gap-2 rounded-lg border border-amber-300/60 bg-amber-50 px-3 py-2 text-sm font-medium text-amber-900 transition-colors hover:bg-amber-100 dark:border-amber-500/40 dark:bg-amber-950/40 dark:text-amber-200 dark:hover:bg-amber-950/70"
    >
      <span className="flex h-6 w-6 items-center justify-center rounded-full bg-amber-500 text-xs font-bold text-white">
        {count}
      </span>
      {label}
    </Link>
  );
}

function StatCard({
  icon: Icon,
  title,
  value,
  detail,
  href,
}: {
  icon: React.ComponentType<{ className?: string }>;
  title: string;
  value: React.ReactNode;
  detail?: React.ReactNode;
  href: string;
}) {
  return (
    <Link href={href} className="group">
      <Card className="h-full transition-colors group-hover:border-primary/50">
        <CardContent className="p-4">
          <div className="flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
            <Icon className="h-3.5 w-3.5" /> {title}
          </div>
          <div className="mt-2 text-2xl font-bold leading-none">{value}</div>
          {detail && <div className="mt-2 text-xs text-muted-foreground">{detail}</div>}
        </CardContent>
      </Card>
    </Link>
  );
}

export default function MeLandingPage() {
  const { user } = useAuth();
  const firstName = user?.firstName || user?.username || 'there';
  const today = new Date().toLocaleDateString(undefined, {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
  });
  const hour = new Date().getHours();
  const greeting = hour < 12 ? 'Good morning' : hour < 17 ? 'Good afternoon' : 'Good evening';

  const { data: home, isLoading, isError } = useQuery({
    queryKey: ['me-portal', 'home'],
    queryFn: () => mePortalService.getHome(),
    staleTime: 60_000,
  });

  const pendingTotal = home
    ? home.movementsAwaitingMyResponse +
      home.surchargesAwaitingMyResponse +
      home.assetsAwaitingAcknowledgement
    : 0;

  // The most meaningful balance first: the type with the most available days.
  const topBalance = home?.leaveBalances?.length
    ? [...home.leaveBalances].sort((a, b) => b.availableDays - a.availableDays)[0]
    : null;
  const leaveTypesCount = home?.leaveBalances?.length ?? 0;
  const openReqs = home?.openAssetRequisitionCount ?? 0;
  const expiringCerts = home?.expiringCertificatesCount ?? 0;

  return (
    <div className="space-y-8">
      <div>
        <p className="text-sm text-muted-foreground">{today}</p>
        <h1 className="mt-1 text-3xl font-bold tracking-tight">
          {greeting}, {firstName}
        </h1>
      </div>

      {/* ── Waiting on you ─────────────────────────────────────────────── */}
      <section>
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          Waiting on you
        </h2>
        {isLoading ? (
          <div className="flex gap-3">
            <Skeleton className="h-10 w-48" />
            <Skeleton className="h-10 w-48" />
          </div>
        ) : (
          <div className="flex flex-wrap items-center gap-3">
            <ActionChip
              count={home?.movementsAwaitingMyResponse ?? 0}
              label="Movements need your response"
              href="/hr/movements/mine"
            />
            <ActionChip
              count={home?.surchargesAwaitingMyResponse ?? 0}
              label="Charges awaiting your reply"
              href="/hr/assets/me"
            />
            <ActionChip
              count={home?.assetsAwaitingAcknowledgement ?? 0}
              label="Assets to sign for"
              href="/hr/assets/me"
            />
            {pendingTotal === 0 && !isError && (
              <span className="flex items-center gap-2 text-sm text-muted-foreground">
                <CheckCircle2 className="h-4 w-4 text-emerald-500" /> Nothing needs your
                action right now.
              </span>
            )}
            <Link
              href="/workflow/inbox"
              className="flex items-center gap-2 rounded-lg border px-3 py-2 text-sm font-medium transition-colors hover:bg-accent"
            >
              <CheckSquare className="h-4 w-4" /> Approvals & tasks
            </Link>
          </div>
        )}
      </section>

      {/* ── At a glance ────────────────────────────────────────────────── */}
      <section>
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          At a glance
        </h2>
        {isLoading ? (
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            {[0, 1, 2, 3].map((i) => (
              <Skeleton key={i} className="h-28" />
            ))}
          </div>
        ) : isError ? (
          <p className="text-sm text-muted-foreground">
            Your summary could not be loaded right now — everything below still works.
          </p>
        ) : (
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <StatCard
              icon={TreePalm}
              title="Leave available"
              value={
                topBalance ? (
                  <>
                    {topBalance.availableDays}
                    <span className="ml-1 text-sm font-normal text-muted-foreground">days</span>
                  </>
                ) : (
                  '—'
                )
              }
              detail={
                topBalance
                  ? `${topBalance.leaveTypeName}${leaveTypesCount > 1 ? ` · +${leaveTypesCount - 1} more types` : ''}`
                  : 'No balance recorded yet'
              }
              href="/me/leave"
            />
            <StatCard
              icon={CalendarDays}
              title="Next holiday"
              value={
                home?.nextHoliday
                  ? new Date(home.nextHoliday.date).toLocaleDateString(undefined, {
                      day: 'numeric',
                      month: 'short',
                    })
                  : '—'
              }
              detail={home?.nextHoliday?.name ?? 'None in the next 12 months'}
              href="/me/leave"
            />
            <StatCard
              icon={Laptop}
              title="Assets held"
              value={home?.assetsHeldCount ?? 0}
              detail={
                openReqs > 0
                  ? `${openReqs} open request${openReqs === 1 ? '' : 's'}`
                  : 'No open requests'
              }
              href="/hr/assets/me"
            />
            <StatCard
              icon={GraduationCap}
              title="My learning"
              value={`${home?.trainingComplianceRate ?? 100}%`}
              detail={
                expiringCerts > 0
                  ? `${expiringCerts} certificate${expiringCerts === 1 ? '' : 's'} expiring soon`
                  : `${home?.activeCertificatesCount ?? 0} active certificates`
              }
              href="/hr/training/my-training"
            />
          </div>
        )}
      </section>

      {/* ── Expiring documents (only when there are any) ───────────────── */}
      {!!home?.expiringDocuments?.length && (
        <section>
          <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
            <FileWarning className="h-4 w-4 text-amber-500" /> Documents expiring soon
          </h2>
          <div className="space-y-2">
            {home.expiringDocuments.map((doc) => (
              <Link
                key={doc.id}
                href="/hr/training/my-training"
                className="flex items-center justify-between rounded-lg border px-3 py-2 text-sm transition-colors hover:bg-accent"
              >
                <span className="flex items-center gap-2">
                  <AlertTriangle className="h-4 w-4 text-amber-500" />
                  <span className="font-medium">{doc.name}</span>
                  {doc.kind && <span className="text-muted-foreground">({doc.kind})</span>}
                </span>
                <span className="text-muted-foreground">
                  expires in {doc.daysUntilExpiry} day{doc.daysUntilExpiry === 1 ? '' : 's'}
                </span>
              </Link>
            ))}
          </div>
        </section>
      )}

      {/* ── Quick links ────────────────────────────────────────────────── */}
      {TILE_SECTIONS.map((section) => (
        <section key={section.title}>
          <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
            {section.title}
          </h2>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {section.tiles.map((tile) => (
              <Link key={tile.href} href={tile.href} className="group">
                <Card className="h-full transition-colors group-hover:border-primary/50 group-hover:bg-accent/40">
                  <CardContent className="flex items-start gap-3 p-4">
                    <div className="rounded-lg bg-primary/10 p-2 text-primary">
                      <tile.icon className="h-5 w-5" />
                    </div>
                    <div>
                      <div className="font-medium leading-tight">{tile.label}</div>
                      <div className="mt-1 text-xs text-muted-foreground">{tile.hint}</div>
                    </div>
                  </CardContent>
                </Card>
              </Link>
            ))}
          </div>
        </section>
      ))}

      {/* Movements/career deep links live in the top nav; the "Waiting on you" chips carry
          the counts. My Movements keeps a tile too since it is the most personal register. */}
      <section>
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          My career
        </h2>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          <Link href="/hr/movements/mine" className="group">
            <Card className="h-full transition-colors group-hover:border-primary/50 group-hover:bg-accent/40">
              <CardContent className="flex items-start gap-3 p-4">
                <div className="rounded-lg bg-primary/10 p-2 text-primary">
                  <Rocket className="h-5 w-5" />
                </div>
                <div>
                  <div className="font-medium leading-tight">My Movements</div>
                  <div className="mt-1 text-xs text-muted-foreground">
                    Transfers, promotions and your career timeline
                  </div>
                </div>
              </CardContent>
            </Card>
          </Link>
          <Link href="/hr/competencies/me" className="group">
            <Card className="h-full transition-colors group-hover:border-primary/50 group-hover:bg-accent/40">
              <CardContent className="flex items-start gap-3 p-4">
                <div className="rounded-lg bg-primary/10 p-2 text-primary">
                  <Compass className="h-5 w-5" />
                </div>
                <div>
                  <div className="font-medium leading-tight">My Competencies</div>
                  <div className="mt-1 text-xs text-muted-foreground">
                    Your skills profile and gaps
                  </div>
                </div>
              </CardContent>
            </Card>
          </Link>
        </div>
      </section>
    </div>
  );
}
