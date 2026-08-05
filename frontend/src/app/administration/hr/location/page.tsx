'use client';

import { MapPin, ListTree } from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';

export default function LocationSetupPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Location"
        description="Where people work: a structure defines its levels, and locations are the actual sites."
      />

      <NavCardGrid
        items={[
          {
            title: 'Structures',
            description: 'Templates that group a set of location levels.',
            href: '/administration/hr/location/structures',
            icon: MapPin,
          },
          {
            title: 'Levels',
            description: 'The tiers within a structure — region, city, site.',
            href: '/administration/hr/location/levels',
            icon: ListTree,
          },
          {
            title: 'Locations',
            description: 'The actual work locations, with address and contact details.',
            href: '/administration/hr/location/locations',
            icon: MapPin,
          },
        ]}
      />
    </div>
  );
}
