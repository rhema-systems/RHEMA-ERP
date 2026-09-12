'use client';

import React from 'react';
import { CRS } from 'leaflet';
import {
  CircleMarker,
  MapContainer,
  Polygon,
  Polyline,
  Tooltip,
  useMap,
} from 'react-leaflet';
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
const GRID_LINE_STYLE = {
  color: '#cbd5e1',
  opacity: 0.55,
  weight: 1,
};

function numberValue(value: string | boolean | undefined): number | null {
  if (typeof value !== 'string' || value.trim() === '') return null;

  const parsed = Number(value);
  if (Number.isFinite(parsed)) return parsed;

  const normalized = value.replace(/,/g, '').match(/[+-]?\d+(\.\d+)?/)?.[0];
  if (!normalized) return null;

  const parsedNormalized = Number(normalized);
  return Number.isFinite(parsedNormalized) ? parsedNormalized : null;
}

function numberFromUnknown(value: unknown): number | null {
  if (typeof value === 'number') return Number.isFinite(value) ? value : null;
  if (typeof value !== 'string' || value.trim() === '') return null;

  const parsed = Number(value);
  if (Number.isFinite(parsed)) return parsed;

  const normalized = value.replace(/,/g, '').match(/[+-]?\d+(\.\d+)?/)?.[0];
  if (!normalized) return null;

  const parsedNormalized = Number(normalized);
  return Number.isFinite(parsedNormalized) ? parsedNormalized : null;
}

function parseBoundary(value: string | boolean | undefined): BeaconPoint[] {
  if (typeof value !== 'string' || value.trim() === '') return [];

  try {
    const parsed = JSON.parse(value);
    const points = Array.isArray(parsed)
      ? parsed
      : Array.isArray(parsed?.beacons)
        ? parsed.beacons
        : [];

    return points
      .map((item: any, index: number): BeaconPoint | null => {
        if (Array.isArray(item) && item.length >= 2) {
          const northing = numberFromUnknown(item[0]);
          const easting = numberFromUnknown(item[1]);
          return northing != null && easting != null
            ? { beacon: `Beacon ${index + 1}`, northing, easting }
            : null;
        }

        const northing = numberFromUnknown(item?.northing ?? item?.Northing ?? item?.northingFeet ?? item?.NorthingFeet ?? item?.northing_ft);
        const easting = numberFromUnknown(item?.easting ?? item?.Easting ?? item?.eastingFeet ?? item?.EastingFeet ?? item?.easting_ft);
        if (northing == null || easting == null) return null;

        return {
          beacon: `${item?.beacon ?? item?.Beacon ?? item?.beaconIndex ?? item?.BeaconIndex ?? item?.index ?? `Beacon ${index + 1}`}`,
          northing,
          easting,
          bearing: item?.bearing ?? item?.Bearing,
          distance: item?.distance ?? item?.Distance ?? item?.distance_ft,
        };
      })
      .filter((item: BeaconPoint | null): item is BeaconPoint => Boolean(item));
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

function pointsFromOwnerBeacons(values: WorkspaceValues): BeaconPoint[] {
  return [1, 2, 3, 4]
    .map((index): BeaconPoint | null => {
      const northing = numberValue(values[`ownerBeacon${index}NorthingFeet`]);
      const easting = numberValue(values[`ownerBeacon${index}EastingFeet`]);
      if (northing == null || easting == null) return null;

      return {
        beacon: `Beacon ${index}`,
        northing,
        easting,
      };
    })
    .filter((item): item is BeaconPoint => Boolean(item));
}

function sameBoundary(left: BeaconPoint[], right: BeaconPoint[]) {
  if (left.length !== right.length) return false;

  return left.every((point, index) => {
    const other = right[index];
    return (
      other &&
      point.beacon === other.beacon &&
      point.northing === other.northing &&
      point.easting === other.easting &&
      `${point.bearing ?? ''}` === `${other.bearing ?? ''}` &&
      `${point.distance ?? ''}` === `${other.distance ?? ''}`
    );
  });
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

function gridStep(span: number): number {
  if (!Number.isFinite(span) || span <= 0) return 25;

  const raw = span / 6;
  const magnitude = Math.pow(10, Math.floor(Math.log10(raw)));
  const normalized = raw / magnitude;

  if (normalized <= 1) return magnitude;
  if (normalized <= 2) return 2 * magnitude;
  if (normalized <= 5) return 5 * magnitude;
  return 10 * magnitude;
}

function gridLines(points: BeaconPoint[]): PlanPoint[][] {
  if (points.length < 2) return [];

  const northings = points.map((point) => point.northing);
  const eastings = points.map((point) => point.easting);
  const minNorth = Math.min(...northings);
  const maxNorth = Math.max(...northings);
  const minEast = Math.min(...eastings);
  const maxEast = Math.max(...eastings);
  const spanNorth = Math.max(maxNorth - minNorth, 1);
  const spanEast = Math.max(maxEast - minEast, 1);
  const padding = Math.max(spanNorth, spanEast) * 0.2;
  const northStep = gridStep(spanNorth);
  const eastStep = gridStep(spanEast);
  const startNorth = Math.floor((minNorth - padding) / northStep) * northStep;
  const endNorth = Math.ceil((maxNorth + padding) / northStep) * northStep;
  const startEast = Math.floor((minEast - padding) / eastStep) * eastStep;
  const endEast = Math.ceil((maxEast + padding) / eastStep) * eastStep;
  const lines: PlanPoint[][] = [];

  for (let northing = startNorth; northing <= endNorth; northing += northStep) {
    lines.push([
      [northing, startEast],
      [northing, endEast],
    ]);
  }

  for (let easting = startEast; easting <= endEast; easting += eastStep) {
    lines.push([
      [startNorth, easting],
      [endNorth, easting],
    ]);
  }

  return lines;
}

function MapSync({ points }: { points: BeaconPoint[] }) {
  const map = useMap();

  React.useEffect(() => {
    window.setTimeout(() => map.invalidateSize(), 0);

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
  comparisonValues,
  comparisonLabel = 'Ownership classification',
  readOnly = false,
  title = 'Survey Plan View',
  description = 'Draw the parcel from beacon northing/easting coordinates in feet, matching the survey plan format.',
}: {
  values: WorkspaceValues;
  onChange?: (values: WorkspaceValues) => void;
  comparisonValues?: WorkspaceValues;
  comparisonLabel?: string;
  readOnly?: boolean;
  title?: string;
  description?: string;
}) {
  const boundaryPoints = React.useMemo(() => {
    const savedBoundary = parseBoundary(values.boundaryCoordinates);
    return savedBoundary.length > 0 ? savedBoundary : pointsFromBeacons(values);
  }, [values]);
  const comparisonPoints = React.useMemo(
    () => (comparisonValues ? pointsFromOwnerBeacons(comparisonValues) : []),
    [comparisonValues]
  );
  const mapPoints = React.useMemo(
    () => [...boundaryPoints, ...comparisonPoints],
    [boundaryPoints, comparisonPoints]
  );
  const center = React.useMemo(() => centerOf(mapPoints), [mapPoints]);
  const surveyGridLines = React.useMemo(() => gridLines(mapPoints), [mapPoints]);

  const saveBoundary = (points: BeaconPoint[]) => {
    onChange?.({
      ...values,
      boundaryCoordinates: points.length ? JSON.stringify(points) : '',
    });
  };

  React.useEffect(() => {
    if (readOnly || !onChange) return;

    const points = pointsFromBeacons(values);
    if (points.length < 3) return;

    const savedBoundary = parseBoundary(values.boundaryCoordinates);
    if (sameBoundary(points, savedBoundary)) return;

    onChange({
      ...values,
      boundaryCoordinates: JSON.stringify(points),
    });
  }, [onChange, readOnly, values]);

  const drawFromBeacons = () => {
    const points = pointsFromBeacons(values);
    if (points.length < 3) return;
    saveBoundary(points);
  };

  const polygonPoints = boundaryPoints.map(toPlanPoint);
  const comparisonPolygonPoints = comparisonPoints.map(toPlanPoint);

  return (
    <div className="md:col-span-2 space-y-3 rounded-lg border bg-card p-4">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <Label className="text-sm font-semibold">{title}</Label>
          <p className="mt-1 text-xs text-muted-foreground">
            {description}
          </p>
        </div>
        {!readOnly && (
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
        )}
      </div>

      <div className="overflow-hidden rounded-md border bg-muted">
        {mapPoints.length >= 3 ? (
          <MapContainer
            center={center}
            zoom={0}
            minZoom={-5}
            maxZoom={5}
            crs={CRS.Simple}
            scrollWheelZoom
            className="h-[360px] w-full bg-background"
            style={{
              backgroundColor: '#f8fafc',
              backgroundImage:
                'linear-gradient(#e2e8f0 1px, transparent 1px), linear-gradient(90deg, #e2e8f0 1px, transparent 1px)',
              backgroundSize: '32px 32px',
            }}
          >
            <MapSync points={mapPoints} />
            {surveyGridLines.map((line, index) => (
              <Polyline
                key={`grid-${index}`}
                positions={line}
                pathOptions={GRID_LINE_STYLE}
                interactive={false}
              />
            ))}
            {polygonPoints.length >= 3 && (
              <Polygon positions={polygonPoints} pathOptions={{ color: '#2563eb', fillColor: '#2563eb', fillOpacity: 0.16 }}>
                <Tooltip sticky>
                  <div className="space-y-1">
                    <div className="font-medium">Cadastral survey</div>
                    {boundaryPoints.map((point) => (
                      <div key={`${point.beacon}-${point.northing}-${point.easting}`}>{formatPoint(point)}</div>
                    ))}
                  </div>
                </Tooltip>
              </Polygon>
            )}
            {comparisonPolygonPoints.length >= 3 && (
              <Polygon positions={comparisonPolygonPoints} pathOptions={{ color: '#dc2626', fillColor: '#f97316', fillOpacity: 0.18 }}>
                <Tooltip sticky>
                  <div className="space-y-1">
                    <div className="font-medium">{comparisonLabel}</div>
                    {comparisonPoints.map((point) => (
                      <div key={`${point.beacon}-${point.northing}-${point.easting}`}>{formatPoint(point)}</div>
                    ))}
                  </div>
                </Tooltip>
              </Polygon>
            )}
            {boundaryPoints.map((point) => (
              <CircleMarker
                key={`survey-${point.beacon}-${point.northing}-${point.easting}`}
                center={toPlanPoint(point)}
                radius={5}
                pathOptions={{
                  color: '#1d4ed8',
                  fillColor: '#ffffff',
                  fillOpacity: 1,
                  weight: 2,
                }}
              >
                <Tooltip direction="top" permanent>
                  S {point.beacon.replace(/^Beacon\s*/i, '')}
                </Tooltip>
              </CircleMarker>
            ))}
            {comparisonPoints.map((point) => (
              <CircleMarker
                key={`owner-${point.beacon}-${point.northing}-${point.easting}`}
                center={toPlanPoint(point)}
                radius={5}
                pathOptions={{
                  color: '#dc2626',
                  fillColor: '#ffffff',
                  fillOpacity: 1,
                  weight: 2,
                }}
              >
                <Tooltip direction="bottom" permanent>
                  O {point.beacon.replace(/^Beacon\s*/i, '')}
                </Tooltip>
              </CircleMarker>
            ))}
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
        <span>{boundaryPoints.length ? `${boundaryPoints.length} cadastral beacon point${boundaryPoints.length === 1 ? '' : 's'}` : 'No cadastral boundary captured yet'}</span>
        {comparisonValues && (
          <span>{comparisonPoints.length ? `${comparisonPoints.length} ownership beacon point${comparisonPoints.length === 1 ? '' : 's'}` : 'No ownership classification boundary captured yet'}</span>
        )}
        {comparisonValues && (
          <>
            <span className="inline-flex items-center gap-1">
              <span className="h-2.5 w-2.5 rounded-full bg-blue-600" />
              Cadastral survey
            </span>
            <span className="inline-flex items-center gap-1">
              <span className="h-2.5 w-2.5 rounded-full bg-orange-500" />
              {comparisonLabel}
            </span>
          </>
        )}
      </div>
    </div>
  );
}
