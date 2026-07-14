'use client';

import React from 'react';
import { CRS } from 'leaflet';
import { MapContainer, Polygon, Tooltip, useMap } from 'react-leaflet';
import { MapPin, Ruler } from 'lucide-react';

type PlanPoint = [number, number];

interface BeaconPoint {
  beacon: string;
  northing: number;
  easting: number;
  bearing?: string;
  distance?: number;
}

const DEFAULT_CENTER: PlanPoint = [0, 0];

function parseBoundary(value?: string): BeaconPoint[] {
  if (!value?.trim()) return [];

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
          bearing: `${item?.bearing ?? item?.Bearing ?? ''}`.trim() || undefined,
          distance: Number.isFinite(Number(item?.distance ?? item?.Distance ?? item?.distanceFeet ?? item?.DistanceFeet))
            ? Number(item?.distance ?? item?.Distance ?? item?.distanceFeet ?? item?.DistanceFeet)
            : undefined,
        };
      })
      .filter((item): item is BeaconPoint => Boolean(item));
  } catch {
    return [];
  }
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

export default function LandBankMap({ boundaryCoordinates }: { boundaryCoordinates?: string }) {
  const points = React.useMemo(() => parseBoundary(boundaryCoordinates), [boundaryCoordinates]);
  const center = React.useMemo(() => centerOf(points), [points]);

  return (
    <div className="space-y-4">
      <div className="overflow-hidden rounded-md border bg-muted">
        <div className="flex items-center justify-between gap-3 border-b bg-background px-4 py-3">
          <div>
            <p className="text-sm font-semibold text-foreground">Survey Plan View</p>
            <p className="text-xs text-muted-foreground">Beacon Northing and Easting coordinates are recorded in feet.</p>
          </div>
          <Ruler className="h-5 w-5 text-muted-foreground" />
        </div>
        {points.length >= 3 ? (
          <MapContainer
            center={center}
            zoom={0}
            minZoom={-5}
            maxZoom={5}
            crs={CRS.Simple}
            scrollWheelZoom
            className="h-[360px] w-full bg-background"
          >
            <MapSync points={points} />
            <Polygon positions={points.map(toPlanPoint)} pathOptions={{ color: '#0f766e', fillColor: '#14b8a6', fillOpacity: 0.2 }}>
              <Tooltip sticky>
                <div className="space-y-1">
                  {points.map((point) => (
                    <div key={`${point.beacon}-${point.northing}-${point.easting}`}>{formatPoint(point)}</div>
                  ))}
                </div>
              </Tooltip>
            </Polygon>
          </MapContainer>
        ) : (
          <div className="flex h-[360px] flex-col items-center justify-center gap-3 text-center text-sm text-muted-foreground">
            <div className="flex h-12 w-12 items-center justify-center rounded-md border bg-background">
              <MapPin className="h-5 w-5" />
            </div>
            <span>No verified beacon boundary has been pushed to this land bank record yet.</span>
          </div>
        )}
      </div>

      {points.length ? (
        <div className="overflow-hidden rounded-md border bg-background">
          <div className="border-b px-4 py-3">
            <p className="text-sm font-semibold text-foreground">Beacon Schedule</p>
            <p className="text-xs text-muted-foreground">Survey-plan coordinates and boundary measurements.</p>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full min-w-[680px] text-left text-sm">
              <thead className="bg-muted/60 text-xs uppercase text-muted-foreground">
                <tr>
                  <th className="px-4 py-3 font-medium">Beacon index</th>
                  <th className="px-4 py-3 font-medium">Northing (Y), ft</th>
                  <th className="px-4 py-3 font-medium">Easting (X), ft</th>
                  <th className="px-4 py-3 font-medium">Bearing</th>
                  <th className="px-4 py-3 font-medium">Distance, ft</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {points.map((point) => (
                  <tr key={`${point.beacon}-${point.northing}-${point.easting}`}>
                    <td className="px-4 py-3 font-medium text-foreground">{point.beacon}</td>
                    <td className="px-4 py-3 tabular-nums text-foreground">{point.northing.toFixed(2)}</td>
                    <td className="px-4 py-3 tabular-nums text-foreground">{point.easting.toFixed(2)}</td>
                    <td className="px-4 py-3 text-foreground">{point.bearing || 'Not recorded'}</td>
                    <td className="px-4 py-3 tabular-nums text-foreground">
                      {point.distance == null ? 'Not recorded' : point.distance.toFixed(2)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : null}
    </div>
  );
}
