'use client';

import React from 'react';
import { CRS } from 'leaflet';
import { MapContainer, Polygon, Tooltip, useMap } from 'react-leaflet';
import { LocateFixed, MousePointer2, RotateCcw } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';

type WorkspaceValues = Record<string, string | boolean>;
type PlanPoint = [number, number];

interface BeaconPoint {
  beacon: string;
  northing: number;
  easting: number;
  bearing?: string;
  distance?: string;
}

const DEFAULT_CENTER: PlanPoint = [0, 0];

function numberValue(value: string | boolean | undefined): number | null {
  if (typeof value !== 'string' || value.trim() === '') return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function parseBoundary(value: string | boolean | undefined): BeaconPoint[] {
  if (typeof value !== 'string' || value.trim() === '') return [];

  try {
    const parsed = JSON.parse(value);
    if (!Array.isArray(parsed)) return [];

    return parsed
      .map((item, index): BeaconPoint | null => {
        if (Array.isArray(item) && item.length >= 2) {
          const northing = Number(item[0]);
          const easting = Number(item[1]);
          return Number.isFinite(northing) && Number.isFinite(easting)
            ? { beacon: `Beacon ${index + 1}`, northing, easting }
            : null;
        }

        const northing = Number(item?.northing ?? item?.Northing ?? item?.northingFeet ?? item?.NorthingFeet);
        const easting = Number(item?.easting ?? item?.Easting ?? item?.eastingFeet ?? item?.EastingFeet);
        if (!Number.isFinite(northing) || !Number.isFinite(easting)) return null;

        return {
          beacon: `${item?.beacon ?? item?.Beacon ?? item?.beaconIndex ?? item?.BeaconIndex ?? `Beacon ${index + 1}`}`,
          northing,
          easting,
          bearing: item?.bearing ?? item?.Bearing,
          distance: item?.distance ?? item?.Distance,
        };
      })
      .filter((item): item is BeaconPoint => Boolean(item));
  } catch {
    return [];
  }
}

function pointsFromBeacons(values: WorkspaceValues): BeaconPoint[] {
  return [1, 2, 3, 4]
    .map((index): BeaconPoint | null => {
      const northing = numberValue(values[`beacon${index}NorthingFeet`]);
      const easting = numberValue(values[`beacon${index}EastingFeet`]);
      if (northing == null || easting == null) return null;

      return {
        beacon: `${values[`beacon${index}Index`] || `Beacon ${index}`}`,
        northing,
        easting,
        bearing: typeof values[`beacon${index}Bearing`] === 'string' ? `${values[`beacon${index}Bearing`]}` : undefined,
        distance: typeof values[`beacon${index}DistanceFeet`] === 'string' ? `${values[`beacon${index}DistanceFeet`]}` : undefined,
      };
    })
    .filter((item): item is BeaconPoint => Boolean(item));
}

function toPlanPoint(point: BeaconPoint): PlanPoint {
  return [point.northing, point.easting];
}

function centerOf(points: BeaconPoint[]): PlanPoint {
  if (points.length === 0) return DEFAULT_CENTER;

  const totals = points.reduce(
    (sum, point) => ({ northing: sum.northing + point.northing, easting: sum.easting + point.easting }),
    { northing: 0, easting: 0 }
  );

  return [totals.northing / points.length, totals.easting / points.length];
}

function MapSync({ points }: { points: BeaconPoint[] }) {
  const map = useMap();

  React.useEffect(() => {
    if (points.length >= 2) {
      map.fitBounds(points.map(toPlanPoint), { padding: [28, 28] });
      return;
    }

    map.setView(DEFAULT_CENTER, 0);
  }, [map, points]);

  return null;
}

function formatPoint(point: BeaconPoint) {
  return `${point.beacon}: N ${point.northing.toFixed(2)} ft, E ${point.easting.toFixed(2)} ft`;
}

export default function CadastralMapPanel({
  values,
  onChange,
}: {
  values: WorkspaceValues;
  onChange: (values: WorkspaceValues) => void;
}) {
  const boundaryPoints = React.useMemo(() => parseBoundary(values.boundaryCoordinates), [values.boundaryCoordinates]);
  const center = React.useMemo(() => centerOf(boundaryPoints), [boundaryPoints]);

  const saveBoundary = (points: BeaconPoint[]) => {
    onChange({
      ...values,
      boundaryCoordinates: points.length ? JSON.stringify(points) : '',
    });
  };

  const drawFromBeacons = () => {
    const points = pointsFromBeacons(values);
    if (points.length < 3) return;
    saveBoundary(points);
  };

  const polygonPoints = boundaryPoints.map(toPlanPoint);

  return (
    <div className="md:col-span-2 space-y-3 rounded-lg border bg-card p-4">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <Label className="text-sm font-semibold">Survey Plan View</Label>
          <p className="mt-1 text-xs text-muted-foreground">
            Draw the parcel from beacon northing/easting coordinates in feet, matching the survey plan format.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button type="button" size="sm" variant="outline" onClick={drawFromBeacons}>
            <LocateFixed className="mr-2 h-4 w-4" />
            Draw Beacons
          </Button>
          <Button type="button" size="sm" variant="ghost" onClick={() => saveBoundary([])}>
            <RotateCcw className="mr-2 h-4 w-4" />
            Clear
          </Button>
        </div>
      </div>

      <div className="overflow-hidden rounded-md border bg-muted">
        {boundaryPoints.length >= 3 ? (
          <MapContainer
            center={center}
            zoom={0}
            minZoom={-5}
            maxZoom={5}
            crs={CRS.Simple}
            scrollWheelZoom
            className="h-[360px] w-full bg-background"
          >
            <MapSync points={boundaryPoints} />
            <Polygon positions={polygonPoints} pathOptions={{ color: '#2563eb', fillColor: '#2563eb', fillOpacity: 0.18 }}>
              <Tooltip sticky>
                <div className="space-y-1">
                  {boundaryPoints.map((point) => (
                    <div key={`${point.beacon}-${point.northing}-${point.easting}`}>{formatPoint(point)}</div>
                  ))}
                </div>
              </Tooltip>
            </Polygon>
          </MapContainer>
        ) : (
          <div className="flex h-[360px] flex-col items-center justify-center gap-3 text-center text-sm text-muted-foreground">
            <MousePointer2 className="h-5 w-5" />
            <span>Enter at least three beacon northing/easting pairs, then draw the survey plan.</span>
          </div>
        )}
      </div>

      <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
        <MousePointer2 className="h-3.5 w-3.5" />
        <span>{boundaryPoints.length ? `${boundaryPoints.length} beacon point${boundaryPoints.length === 1 ? '' : 's'} captured` : 'No beacon boundary captured yet'}</span>
        {boundaryPoints[0] && <span>First point: {formatPoint(boundaryPoints[0])}</span>}
      </div>
    </div>
  );
}
