'use client';

import React from 'react';
import { useParams, useRouter } from 'next/navigation';
import { ArrowLeft } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { ListingApplicationWorkspace } from '../../ListingApplicationWorkspace';

export default function PropertyManagementListingCasePage() {
  const router = useRouter();
  const params = useParams<{
    entityType?: string | string[];
  }>();
  const entityTypeValue = Array.isArray(params?.entityType)
    ? params.entityType[0]
    : params?.entityType;
  const entityType = entityTypeValue ? decodeURIComponent(entityTypeValue) : '';

  return (
    <div className="space-y-4">
      <Button
        variant="ghost"
        className="w-fit gap-2 px-0"
        onClick={() => {
          if (window.history.length > 1) {
            router.back();
            return;
          }

          router.push(
            `/estate/property-management/${encodeURIComponent(entityType)}`
          );
        }}
      >
        <ArrowLeft className="h-4 w-4" />
        Back to requests
      </Button>
      <ListingApplicationWorkspace />
    </div>
  );
}
