'use client';

import Link from 'next/link';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

export default function FleetPage() {
  const tiles = [
    { title: 'Dashboard', href: '/maintenance/fleet/dashboard', description: 'KPIs and fleet analytics overview' },
    { title: 'Fleets', href: '/maintenance/fleet/vehicles', description: 'Manage fleet vehicles (assets)' },
    { title: 'Trips', href: '/maintenance/fleet/trips', description: 'Trip requests, approvals and dispatch' },
    { title: 'Compliance', href: '/maintenance/fleet/compliance', description: 'Registrations, insurance, permits and reminders' },
    { title: 'Fuel', href: '/maintenance/fleet/fuel', description: 'Fuel transactions and consumption tracking' },
    { title: 'Defects', href: '/maintenance/fleet/defects', description: 'Incidents/defects and conversion to Work Orders' },
    { title: 'PM Plans', href: '/maintenance/fleet/pm', description: 'Preventive maintenance plans (schedules) per vehicle' },
    { title: 'Tyres', href: '/maintenance/fleet/tyres', description: 'Tyre tracking per vehicle (positions, tread depth)' },
    { title: 'Batteries', href: '/maintenance/fleet/batteries', description: 'Battery tracking per vehicle' },
    { title: 'External Repairs', href: '/maintenance/fleet/external-repairs', description: 'Vendor repairs and external maintenance records' },
    { title: 'Costs', href: '/maintenance/fleet/costs', description: 'Fleet cost ledger and KPIs' },
    { title: 'Reports', href: '/maintenance/fleet/reports', description: 'Cost and utilization summaries (by vehicle)' },
  ];

  return (
    <div className="space-y-6 p-6">
      <div>
        <h1 className="text-2xl font-semibold">Fleet Management</h1>
        <p className="text-muted-foreground">Fleet operations under Maintenance</p>
      </div>

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
        {tiles.map((t) => (
          <Link key={t.href} href={t.href} className="block">
            <Card className="h-full hover:bg-muted/40">
              <CardHeader>
                <CardTitle>{t.title}</CardTitle>
                <CardDescription>{t.description}</CardDescription>
              </CardHeader>
              <CardContent />
            </Card>
          </Link>
        ))}
      </div>
    </div>
  );
}

