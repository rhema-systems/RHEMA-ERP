'use client';

/**
 * Area 25 slice 2 — the self-service portal's top navigation.
 *
 * Spec: the Blazor `PortalTopNav` (6 groups + actions). Deliberately a TOP nav, not a
 * sidebar: self-service users are casual users whose journeys start from dashboard tiles;
 * the visual contrast with the sidebar desk is itself the "which world am I in?" signal.
 *
 * D3 rule for the links: a group shows ONLY destinations that are genuinely self-service
 * today. Entries still pointing at `/hr/*` are the screens the move-in slices (4–9)
 * re-home — each slice moves the screen AND this link in the same commit. Never add a
 * dead "coming soon" link here.
 */

import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from '@/components/ui/sheet';
import { Button } from '@/components/ui/button';
import { ThemeToggle } from '@/components/ui/ThemeToggle';
import {
  Bell,
  Briefcase,
  CheckSquare,
  ChevronDown,
  Clock,
  GraduationCap,
  HeartPulse,
  Home,
  LayoutDashboard,
  LogOut,
  Menu,
  Rocket,
  Sparkles,
  TrendingUp,
  UserRound,
} from 'lucide-react';
import { useAuth } from '@/hooks/use-auth';
import { hasDeskAccess } from '@/lib/auth-routing';
import { mePortalService } from '@/services/hr/me-portal.service';
import { cn } from '@/lib/utils';

interface NavLink {
  label: string;
  href: string;
}

interface NavGroup {
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  links: NavLink[];
}

// The move-in slices rewrite these hrefs onto /me/* as each domain re-homes (D3).
const NAV_GROUPS: NavGroup[] = [
  {
    label: 'Time, Leave & Pay',
    icon: Clock,
    links: [
      // Slice 4: leave + attendance live in the portal.
      { label: 'My Leave', href: '/me/leave' },
      { label: 'Leave Planner', href: '/me/leave/planner' },
      { label: 'My Encashments', href: '/me/leave/encashments' },
      { label: 'My Attendance', href: '/me/attendance' },
      // Slice 7: travel lives in the portal.
      { label: 'My Travel', href: '/me/travel' },
      // Slice 10: the read-only payslip adapter over payroll's published snapshots.
      { label: 'My Payslips', href: '/me/payslips' },
    ],
  },
  {
    label: 'Performance',
    icon: TrendingUp,
    links: [
      // Slice 5: the performance my-screens live in the portal.
      { label: 'My Appraisals', href: '/me/performance/appraisals' },
      { label: 'My Goals', href: '/me/performance/goals' },
      { label: 'My Development Plans', href: '/me/performance/development-plans' },
      { label: 'Peer Reviews', href: '/me/performance/peer-reviews' },
      { label: 'My Check-Ins', href: '/me/performance/check-ins' },
      { label: 'My Journal', href: '/me/performance/journal' },
    ],
  },
  {
    label: 'Learning',
    icon: GraduationCap,
    links: [
      // Slice 6: training & learning live in the portal.
      { label: 'My Training', href: '/me/training' },
      { label: 'Request Training', href: '/me/training/requests/new' },
      { label: 'My Learning Paths', href: '/me/learning' },
      { label: 'My Waitlist', href: '/me/training/waitlist' },
      { label: 'My Service Bonds', href: '/me/training/bonds' },
      { label: 'My Mentoring', href: '/me/mentoring' },
      // Slice 7: orientation lives in the portal.
      { label: 'My Orientations', href: '/me/orientation' },
    ],
  },
  {
    label: 'Career & Jobs',
    icon: Rocket,
    links: [
      // Slice 7: movements, the career timeline, probation and the oath live in the portal.
      { label: 'My Movements', href: '/me/movements' },
      { label: 'Career Timeline', href: '/me/movements/career-path' },
      { label: 'My Probation', href: '/me/probation' },
      { label: 'My Oath of Secrecy', href: '/me/oath' },
      { label: 'My Competencies', href: '/hr/competencies/me' },
      { label: 'Internal Job Board', href: '/hr/recruitment/job-board' },
      { label: 'My Panel Interviews', href: '/hr/recruitment/my-panel' },
      // Slice 9: discipline + grievances live in the portal. They sit in this group because
      // they are records about your employment, not benefits or safety.
      { label: 'My Disciplinary Record', href: '/me/discipline' },
      { label: 'My Grievances', href: '/me/grievances' },
    ],
  },
  {
    label: 'Health & Safety',
    icon: HeartPulse,
    links: [
      // Slice 8: medical + safety live in the portal, hub-first — coverage, claims,
      // appointments and the health record hang off /me/medical; PPE, risk assessments and
      // my-reports off /me/safety. Report-a-concern is the one deep entry (it must be loud).
      { label: 'My Medical', href: '/me/medical' },
      { label: 'My Medical Claims', href: '/me/medical/claims' },
      { label: 'My Safety', href: '/me/safety' },
      { label: 'Report a Concern', href: '/me/safety/report' },
      // Slice 9: awards + assets live in the portal.
      { label: 'My Awards', href: '/me/awards' },
      { label: 'My Assets', href: '/me/assets' },
    ],
  },
];

function initialsOf(name: string): string {
  const parts = name.split(/[\s._@]+/).filter(Boolean);
  if (parts.length === 0) return '·';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[1][0]).toUpperCase();
}

/** A count bubble on an action button. Renders nothing at zero — an empty badge is noise. */
function CountBadge({ count, className }: { count: number; className?: string }) {
  if (count <= 0) return null;
  return (
    <span
      className={cn(
        'absolute -right-0.5 -top-0.5 flex h-4 min-w-[1rem] items-center justify-center rounded-full px-1 text-[10px] font-bold leading-none text-white',
        className,
      )}
    >
      {count > 99 ? '99+' : count}
    </span>
  );
}

export function PortalTopNav() {
  const pathname = usePathname();
  const router = useRouter();
  const { user, logout, isLoggingOut } = useAuth();
  const [mobileOpen, setMobileOpen] = useState(false);

  const displayName =
    [user?.firstName, user?.lastName].filter(Boolean).join(' ') || user?.username || '';
  const deskUser = hasDeskAccess(user);

  // Slice 11: the badges. One cheap counts read, shared by both action buttons; a failure
  // leaves the buttons unbadged rather than breaking the chrome every portal page renders.
  const { data: counts } = useQuery({
    queryKey: ['me', 'inbox-counts'],
    queryFn: () => mePortalService.getInboxCounts(),
    staleTime: 60_000,
    retry: false,
  });
  const inboxCount =
    (counts?.pendingApprovals ?? 0) + (counts?.pendingTasks ?? 0) + (counts?.actionItems ?? 0);
  const unreadCount = counts?.unreadNotifications ?? 0;

  const groupActive = (group: NavGroup) => group.links.some((l) => pathname?.startsWith(l.href));

  return (
    <header className="sticky top-0 z-40 border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/80">
      {/* the portal's signature: a warm accent band the desk chrome doesn't have */}
      <div className="h-1 w-full bg-gradient-to-r from-primary via-primary/60 to-primary/20" />
      <div className="mx-auto flex h-14 max-w-7xl items-center gap-2 px-4">
        <Link href="/me" className="flex items-center gap-2 font-semibold">
          <Sparkles className="h-5 w-5 text-primary" />
          <span className="whitespace-nowrap">My Workspace</span>
        </Link>

        {/* desktop nav */}
        <nav className="ml-4 hidden flex-1 items-center gap-1 lg:flex">
          <Button
            variant="ghost"
            size="sm"
            asChild
            className={cn(pathname === '/me' && 'bg-accent text-accent-foreground')}
          >
            <Link href="/me">
              <Home className="mr-1 h-4 w-4" /> Home
            </Link>
          </Button>
          {NAV_GROUPS.map((group) => (
            <DropdownMenu key={group.label}>
              <DropdownMenuTrigger asChild>
                <Button
                  variant="ghost"
                  size="sm"
                  className={cn(groupActive(group) && 'bg-accent text-accent-foreground')}
                >
                  <group.icon className="mr-1 h-4 w-4" />
                  {group.label}
                  <ChevronDown className="ml-1 h-3 w-3 opacity-60" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="start" className="w-56">
                {group.links.map((link) => (
                  <DropdownMenuItem key={link.href} asChild>
                    <Link href={link.href} className="w-full cursor-pointer">
                      {link.label}
                    </Link>
                  </DropdownMenuItem>
                ))}
              </DropdownMenuContent>
            </DropdownMenu>
          ))}
        </nav>

        <div className="ml-auto flex items-center gap-1">
          {/* Slice 11: both destinations are portal-side now. They used to point at the desk
              /workflow/inbox (which strands records — cross-module #15) and the /notifications
              admin console, which an Employee-only user has no business seeing. */}
          <Button
            variant="ghost"
            size="sm"
            asChild
            title="Approvals & tasks"
            className={cn('relative', pathname?.startsWith('/me/inbox') && 'bg-accent')}
          >
            <Link href="/me/inbox">
              <CheckSquare className="h-4 w-4" />
              <span className="ml-1 hidden xl:inline">Approvals</span>
              <CountBadge count={inboxCount} className="bg-amber-500" />
            </Link>
          </Button>
          <Button
            variant="ghost"
            size="sm"
            asChild
            title="Notifications"
            className={cn('relative', pathname?.startsWith('/me/notifications') && 'bg-accent')}
          >
            <Link href="/me/notifications">
              <Bell className="h-4 w-4" />
              <CountBadge count={unreadCount} className="bg-destructive" />
            </Link>
          </Button>
          <ThemeToggle />

          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <button
                className="ml-1 flex h-8 w-8 items-center justify-center rounded-full bg-primary text-xs font-semibold text-primary-foreground"
                title={displayName}
              >
                {initialsOf(displayName)}
              </button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-60">
              <DropdownMenuLabel>
                <div className="font-medium">{displayName}</div>
                <div className="text-xs font-normal text-muted-foreground">
                  Employee Self-Service
                </div>
              </DropdownMenuLabel>
              <DropdownMenuSeparator />
              {/* Two different things, deliberately both here: /me/profile is the EMPLOYEE
                  record the organisation holds; /profile is the sign-in account. */}
              <DropdownMenuItem asChild>
                <Link href="/me/profile" className="cursor-pointer">
                  <UserRound className="mr-2 h-4 w-4" /> My profile
                </Link>
              </DropdownMenuItem>
              <DropdownMenuItem asChild>
                <Link href="/profile" className="cursor-pointer">
                  Account &amp; password
                </Link>
              </DropdownMenuItem>
              {deskUser && (
                <DropdownMenuItem asChild>
                  <Link href="/dashboard" className="cursor-pointer">
                    <Briefcase className="mr-2 h-4 w-4" /> Back to ERP
                  </Link>
                </DropdownMenuItem>
              )}
              <DropdownMenuSeparator />
              <DropdownMenuItem
                disabled={isLoggingOut}
                onClick={() => logout(undefined)}
                className="cursor-pointer"
              >
                <LogOut className="mr-2 h-4 w-4" /> Sign out
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>

          {/* mobile menu */}
          <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
            <SheetTrigger asChild>
              <Button variant="ghost" size="sm" className="lg:hidden" aria-label="Menu">
                <Menu className="h-5 w-5" />
              </Button>
            </SheetTrigger>
            <SheetContent side="right" className="w-72 overflow-y-auto">
              <SheetHeader>
                <SheetTitle className="flex items-center gap-2">
                  <Sparkles className="h-4 w-4 text-primary" /> My Workspace
                </SheetTitle>
              </SheetHeader>
              <nav className="mt-4 space-y-4">
                <Link
                  href="/me"
                  onClick={() => setMobileOpen(false)}
                  className="flex items-center gap-2 rounded-md px-2 py-1.5 text-sm font-medium hover:bg-accent"
                >
                  <LayoutDashboard className="h-4 w-4" /> Home
                </Link>
                {NAV_GROUPS.map((group) => (
                  <div key={group.label}>
                    <div className="flex items-center gap-2 px-2 pb-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                      <group.icon className="h-3.5 w-3.5" /> {group.label}
                    </div>
                    {group.links.map((link) => (
                      <Link
                        key={link.href}
                        href={link.href}
                        onClick={() => setMobileOpen(false)}
                        className="block rounded-md px-2 py-1.5 text-sm hover:bg-accent"
                      >
                        {link.label}
                      </Link>
                    ))}
                  </div>
                ))}
                {deskUser && (
                  <Button
                    variant="outline"
                    className="w-full"
                    onClick={() => {
                      setMobileOpen(false);
                      router.push('/dashboard');
                    }}
                  >
                    <Briefcase className="mr-2 h-4 w-4" /> Back to ERP
                  </Button>
                )}
              </nav>
            </SheetContent>
          </Sheet>
        </div>
      </div>
    </header>
  );
}
