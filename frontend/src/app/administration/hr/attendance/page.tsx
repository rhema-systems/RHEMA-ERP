'use client';

import {
  CalendarClock,
  Repeat,
  CalendarDays,
  Wallet,
  MapPin,
  Fingerprint,
  BellRing,
  Timer,
} from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid, type NavCardItem } from '@/components/hr/common/NavCardGrid';

/**
 * Setup for Attendance & Time. The operational screens — daily attendance, approvals,
 * alerts, exports — live under `/hr/attendance`; everything configured here shapes how
 * those behave.
 */
const items: NavCardItem[] = [
  {
    title: 'Work Schedules',
    description: 'Standard hours, working days, breaks and overtime rules, plus their shifts.',
    href: '/administration/hr/attendance/work-schedules',
    icon: CalendarClock,
  },
  {
    title: 'Shift Rotations',
    description: 'Cycles that move teams through a sequence of shifts on a fixed rhythm.',
    href: '/administration/hr/attendance/shift-rotations',
    icon: Repeat,
  },
  {
    title: 'Holiday Calendars',
    description: 'Public holidays that decide which days are non-working.',
    href: '/administration/hr/attendance/holiday-calendars',
    icon: CalendarDays,
  },
  {
    title: 'Pay Periods',
    description: 'Cut-off windows that attendance summaries and payroll exports group by.',
    href: '/administration/hr/attendance/pay-periods',
    icon: Wallet,
  },
  {
    title: 'Geofence Zones',
    description: 'GPS boundaries that clock-in and clock-out punches are checked against.',
    href: '/administration/hr/attendance/geofence-zones',
    icon: MapPin,
  },
  {
    title: 'Devices',
    description: 'Biometric readers, card terminals and kiosks that feed raw punches in.',
    href: '/administration/hr/attendance/devices',
    icon: Fingerprint,
  },
  {
    title: 'Alert Rules',
    description: 'Thresholds that raise alerts on absence, lateness and overtime.',
    href: '/administration/hr/attendance/alert-rules',
    icon: BellRing,
  },
  {
    title: 'Overtime Policies',
    description: 'Which positions may claim overtime, and the per-employee exceptions.',
    href: '/administration/hr/attendance/overtime-policies',
    icon: Timer,
  },
];

export default function AttendanceSetupPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Attendance & Time Setup"
        description="Schedules, shifts, holidays, pay periods and the rules attendance is measured against."
        backHref="/administration/hr"
      />
      <NavCardGrid items={items} />
    </div>
  );
}
