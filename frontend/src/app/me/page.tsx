'use client';

/**
 * Area 25 slice 2 — the portal landing (placeholder edition).
 *
 * Slice 3 turns this into the personal aggregate (leave balance, pending-my-action counts,
 * assets held, next training, latest payslip, announcements). Until then: a warm greeting
 * and honest quick access to everything that is genuinely self-service today. Tiles are
 * the portal's primary navigation — the top nav is the fallback.
 */

import Link from 'next/link';
import { Card, CardContent } from '@/components/ui/card';
import {
  Award,
  BookOpen,
  Briefcase,
  CheckSquare,
  ClipboardList,
  Compass,
  GraduationCap,
  HeartPulse,
  Laptop,
  MessageSquare,
  Plane,
  Rocket,
  ShieldAlert,
  Target,
  Users,
} from 'lucide-react';
import { useAuth } from '@/hooks/use-auth';

interface Tile {
  label: string;
  hint: string;
  href: string;
  icon: React.ComponentType<{ className?: string }>;
}

const TILE_SECTIONS: { title: string; tiles: Tile[] }[] = [
  {
    title: 'Things to act on',
    tiles: [
      { label: 'Approvals & Tasks', hint: 'What is waiting on you', href: '/workflow/inbox', icon: CheckSquare },
      { label: 'My Movements', hint: 'Transfers and promotions to respond to', href: '/hr/movements/mine', icon: Rocket },
      { label: 'Peer Evaluations', hint: 'Feedback you owe colleagues', href: '/hr/performance/peer-reviews', icon: Users },
    ],
  },
  {
    title: 'My work life',
    tiles: [
      { label: 'My Travel', hint: 'Raise and track travel requests', href: '/hr/travel/mine', icon: Plane },
      { label: 'My Appraisals', hint: 'Reviews and self-evaluations', href: '/hr/performance/appraisals', icon: Target },
      { label: 'My Check-Ins', hint: 'Conversations with your manager', href: '/hr/performance/check-ins', icon: MessageSquare },
      { label: 'My Journal', hint: 'Your private work notes', href: '/hr/performance/journal', icon: BookOpen },
      { label: 'My Training', hint: 'Courses, requests and certificates', href: '/hr/training/my-training', icon: GraduationCap },
      { label: 'My Learning Paths', hint: 'Guided development journeys', href: '/hr/training/my-learning', icon: Compass },
      { label: 'My Orientations', hint: 'Onboarding checklists', href: '/hr/orientation/mine', icon: ClipboardList },
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

  return (
    <div className="space-y-8">
      <div>
        <p className="text-sm text-muted-foreground">{today}</p>
        <h1 className="mt-1 text-3xl font-bold tracking-tight">
          {greeting}, {firstName}
        </h1>
        <p className="mt-2 max-w-2xl text-muted-foreground">
          Everything about your work life in one place. Your personal dashboard — balances,
          payslips and reminders — is on its way as each area moves in.
        </p>
      </div>

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
    </div>
  );
}
