'use client';

import dynamic from 'next/dynamic';
import type { GeoPickerMapProps } from './GeoPickerMap';

/**
 * Map picker for a site pin, a circular zone or a polygon zone. Leaflet cannot render on the
 * server, so the map itself is loaded client-side only — the same pattern the Estate module uses
 * for its land-bank map. Import this, never `GeoPickerMap` directly.
 */
const GeoPickerMap = dynamic(() => import('./GeoPickerMap'), {
  ssr: false,
  loading: () => <div className="h-[320px] animate-pulse rounded-md border bg-muted" />,
});

export type { GeoPickerMapProps as GeoPickerProps, GeoPickerReference } from './GeoPickerMap';

export function GeoPicker(props: GeoPickerMapProps) {
  return <GeoPickerMap {...props} />;
}
