'use client';

import { Hospital, Stethoscope, ShieldPlus, Layers, FileHeart } from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';

/**
 * Medical & Health landing page.
 *
 * Slice 4 ships the reference and configuration registers — everything downstream needs a
 * facility to point at, and all four registers start empty. Health records, claims and the
 * clinical surface arrive with slices 5–7; add them here in that order as they land.
 */
export default function MedicalHomePage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Medical & Health"
        description="Occupational health records, medical claims and the facilities, insurers and benefit schemes behind them. Access requires medical permissions."
      />

      <NavCardGrid
        items={[
          {
            title: 'Health Records',
            description:
              'Employee health profiles, conditions, allergies and medical examinations, with the recall list.',
            href: '/hr/medical/health',
            icon: FileHeart,
          },
          {
            title: 'Healthcare Facilities',
            description:
              'Hospitals, clinics and pharmacies the organisation deals with, and which of them accept NHIS.',
            href: '/hr/medical/facilities',
            icon: Hospital,
          },
          {
            title: 'Physicians',
            description:
              'Doctors practising at those facilities, with licence details and verification.',
            href: '/hr/medical/physicians',
            icon: Stethoscope,
          },
          {
            title: 'Insurance Providers',
            description: 'Insurers and the plans they sell, with their limits and claim deadlines.',
            href: '/hr/medical/insurance',
            icon: ShieldPlus,
          },
          {
            title: 'Benefit Schemes',
            description:
              'What staff at each level are entitled to, as tiers with annual and sub-limits.',
            href: '/hr/medical/schemes',
            icon: Layers,
          },
        ]}
      />
    </div>
  );
}
