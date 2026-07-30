'use client';

import React from 'react';
import { CRS, geoJSON as createGeoJsonLayer } from 'leaflet';
import type { FeatureCollection, GeoJsonObject } from 'geojson';
import {
  CircleMarker,
  GeoJSON as GeoJsonLayer,
  MapContainer,
  Polygon,
  Polyline,
  TileLayer,
  Tooltip,
  useMap,
} from 'react-leaflet';
import { Globe2, Loader2, MapPin, Ruler } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  estateGisService,
  type EstateGisRuntimeConfiguration,
} from '@/services/estate-gis.service';

type PlanPoint = [number, number];

interface BeaconPoint {
  beacon: string;
  northing: number;
  easting: number;
  bearing?: string;
  distance?: number;
}

const DEFAULT_CENTER: PlanPoint = [0, 0];
const DEFAULT_BASE_MAP_URL =
  'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';
const GRID_LINE_STYLE = {
  color: '#94a3b8',
  opacity: 0.5,
  weight: 1,
};

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

function toPlanPoint(
  point: BeaconPoint,
  origin: PlanPoint = DEFAULT_CENTER
): PlanPoint {
  return [point.northing - origin[0], point.easting - origin[1]];
}

function normalizePlanPoint(point: PlanPoint, origin: PlanPoint): PlanPoint {
  return [point[0] - origin[0], point[1] - origin[1]];
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

function surveyZoom(points: BeaconPoint[]): number {
  if (points.length < 2) return 0;

  const northings = points.map((point) => point.northing);
  const eastings = points.map((point) => point.easting);
  const span = Math.max(
    Math.max(...northings) - Math.min(...northings),
    Math.max(...eastings) - Math.min(...eastings),
    1
  );
  return Math.max(-5, Math.min(5, Math.floor(Math.log2(280 / span))));
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

function MapSync({
  points,
  origin,
  fitToPoints,
}: {
  points: BeaconPoint[];
  origin: PlanPoint;
  fitToPoints: boolean;
}) {
  const map = useMap();

  React.useEffect(() => {
    const syncMap = () => {
      map.invalidateSize();

      if (fitToPoints && points.length >= 2) {
        map.fitBounds(
          points.map((point) => toPlanPoint(point, origin)),
          { padding: [28, 28] }
        );
        return;
      }

      map.setView(DEFAULT_CENTER, surveyZoom(points));
    };
    const syncTimer = window.setTimeout(syncMap, 0);
    const postAnimationTimer = window.setTimeout(syncMap, 250);
    const resizeObserver = new ResizeObserver(syncMap);
    resizeObserver.observe(map.getContainer());

    return () => {
      window.clearTimeout(syncTimer);
      window.clearTimeout(postAnimationTimer);
      resizeObserver.disconnect();
    };
  }, [fitToPoints, map, origin, points]);

  return null;
}

function GisMapSync({ geometry }: { geometry: FeatureCollection }) {
  const map = useMap();

  React.useEffect(() => {
    const syncMap = () => {
      map.invalidateSize();
      const bounds = createGeoJsonLayer(geometry as GeoJsonObject).getBounds();
      if (bounds.isValid()) map.fitBounds(bounds, { padding: [28, 28] });
    };
    const syncTimer = window.setTimeout(syncMap, 0);
    const postAnimationTimer = window.setTimeout(syncMap, 250);
    const resizeObserver = new ResizeObserver(syncMap);
    resizeObserver.observe(map.getContainer());

    return () => {
      window.clearTimeout(syncTimer);
      window.clearTimeout(postAnimationTimer);
      resizeObserver.disconnect();
    };
  }, [geometry, map]);

  return null;
}

function formatPoint(point: BeaconPoint) {
  return `${point.beacon}: N ${point.northing.toFixed(2)} ft, E ${point.easting.toFixed(2)} ft`;
}

function SurveyPlanCanvas({
  points,
  demarcations,
}: {
  points: BeaconPoint[];
  demarcations: Array<{
    id: string;
    description: string;
    isDraft?: boolean;
    points: BeaconPoint[];
  }>;
}) {
  const allPoints = [
    ...points,
    ...demarcations.flatMap((demarcation) => demarcation.points),
  ];
  const northings = allPoints.map((point) => point.northing);
  const eastings = allPoints.map((point) => point.easting);
  const minNorth = Math.min(...northings);
  const maxNorth = Math.max(...northings);
  const minEast = Math.min(...eastings);
  const maxEast = Math.max(...eastings);
  const spanNorth = Math.max(maxNorth - minNorth, 1);
  const spanEast = Math.max(maxEast - minEast, 1);
  const padding = Math.max(spanNorth, spanEast) * 0.18;
  const viewBox = [
    minEast - padding,
    -maxNorth - padding,
    spanEast + padding * 2,
    spanNorth + padding * 2,
  ].join(' ');
  const markerRadius = Math.max(Math.min(spanNorth, spanEast) * 0.018, 3);
  const labelSize = markerRadius * 3.2;
  const toSvgPoints = (boundary: BeaconPoint[]) =>
    boundary.map((point) => `${point.easting},${-point.northing}`).join(' ');

  return (
    <div
      className="h-[360px] w-full overflow-hidden bg-background"
      style={{
        backgroundColor: '#f8fafc',
        backgroundImage:
          'linear-gradient(#e2e8f0 1px, transparent 1px), linear-gradient(90deg, #e2e8f0 1px, transparent 1px)',
        backgroundSize: '32px 32px',
      }}
    >
      <svg
        aria-label="Survey boundary plan"
        className="h-full w-full"
        preserveAspectRatio="xMidYMid meet"
        role="img"
        viewBox={viewBox}
      >
        <polygon
          fill="#60a5fa"
          fillOpacity="0.08"
          points={toSvgPoints(points)}
          stroke="#1d4ed8"
          strokeWidth="3"
          vectorEffect="non-scaling-stroke"
        >
          <title>Main cadastral boundary</title>
        </polygon>

        {demarcations.map((demarcation, index) => {
          const color = demarcation.isDraft
            ? '#dc2626'
            : ['#0f766e', '#7c3aed', '#c2410c', '#047857'][index % 4];
          return (
            <polygon
              key={demarcation.id}
              fill={color}
              fillOpacity={demarcation.isDraft ? 0.12 : 0.22}
              points={toSvgPoints(demarcation.points)}
              stroke={color}
              strokeDasharray={demarcation.isDraft ? '8 6' : undefined}
              strokeWidth="3"
              vectorEffect="non-scaling-stroke"
            >
              <title>{demarcation.description}</title>
            </polygon>
          );
        })}

        {points.map((point) => (
          <g key={`survey-beacon-${point.beacon}-${point.northing}-${point.easting}`}>
            <circle
              cx={point.easting}
              cy={-point.northing}
              fill="#ffffff"
              r={markerRadius}
              stroke="#1d4ed8"
              strokeWidth="2"
              vectorEffect="non-scaling-stroke"
            >
              <title>{formatPoint(point)}</title>
            </circle>
            <text
              fill="#1e3a8a"
              fontSize={labelSize}
              fontWeight="600"
              textAnchor="middle"
              x={point.easting}
              y={-point.northing - markerRadius * 2}
            >
              {point.beacon}
            </text>
          </g>
        ))}
      </svg>
    </div>
  );
}

interface LandBankMapProps {
  assetId?: string;
  boundaryCoordinates?: string;
  demarcations?: Array<{
    id: string;
    description: string;
    boundaryCoordinates: string;
    isDraft?: boolean;
  }>;
  gisFeatureId?: string;
  gisLayerReference?: string;
  gisProvider?: string;
  forceSurvey?: boolean;
  showBeaconSchedule?: boolean;
}

export default function LandBankMap({
  assetId,
  boundaryCoordinates,
  demarcations = [],
  gisFeatureId,
  gisLayerReference,
  gisProvider,
  forceSurvey = false,
  showBeaconSchedule = true,
}: LandBankMapProps) {
  const points = React.useMemo(() => parseBoundary(boundaryCoordinates), [boundaryCoordinates]);
  const demarcationPolygons = React.useMemo(
    () =>
      demarcations
        .map((item) => ({ ...item, points: parseBoundary(item.boundaryCoordinates) }))
        .filter((item) => item.points.length >= 3),
    [demarcations]
  );
  const allSurveyPoints = React.useMemo(
    () => [...points, ...demarcationPolygons.flatMap((item) => item.points)],
    [demarcationPolygons, points]
  );
  const surveyOrigin = React.useMemo(
    () => centerOf(allSurveyPoints),
    [allSurveyPoints]
  );
  const surveyGridLines = React.useMemo(() => gridLines(allSurveyPoints), [allSurveyPoints]);
  const isLinked = Boolean(!forceSurvey && assetId && gisFeatureId && gisLayerReference);
  const [mode, setMode] = React.useState<'gis' | 'survey'>(
    isLinked ? 'gis' : 'survey'
  );
  const [configuration, setConfiguration] =
    React.useState<EstateGisRuntimeConfiguration | null>(null);
  const [gisGeometry, setGisGeometry] = React.useState<FeatureCollection | null>(
    null
  );
  const [gisError, setGisError] = React.useState<string | null>(null);
  const [isLoadingGis, setIsLoadingGis] = React.useState(false);

  React.useEffect(() => {
    let active = true;

    const load = async () => {
      setGisGeometry(null);
      setGisError(null);
      if (!isLinked || !assetId) {
        setMode('survey');
        return;
      }

      setIsLoadingGis(true);
      try {
        const gisConfiguration =
          await estateGisService.getRuntimeConfiguration();
        if (!active) return;
        setConfiguration(gisConfiguration);
        if (!gisConfiguration.isEnabled) {
          setGisError('Estate GIS integration is disabled.');
          setMode('survey');
          return;
        }

        const geometry = await estateGisService.getAssetGeometry(assetId);
        if (!active) return;
        setGisGeometry(geometry);
        setMode(geometry.features.length ? 'gis' : 'survey');
        if (!geometry.features.length)
          setGisError('The linked GIS feature returned no geometry.');
      } catch (error) {
        if (!active) return;
        setGisError(
          error instanceof Error ? error.message : 'Unable to load GIS geometry.'
        );
        setMode('survey');
      } finally {
        if (active) setIsLoadingGis(false);
      }
    };

    void load();
    return () => {
      active = false;
    };
  }, [assetId, gisFeatureId, gisLayerReference, isLinked]);

  return (
    <div className="space-y-4">
      <div className="overflow-hidden rounded-md border bg-muted">
        <div className="flex items-center justify-between gap-3 border-b bg-background px-4 py-3">
          <div>
            <p className="text-sm font-semibold text-foreground">
              {mode === 'gis' ? 'GIS Map' : 'Survey Plan View'}
            </p>
            <p className="text-xs text-muted-foreground">
              {mode === 'gis'
                ? `${gisProvider || 'GIS'} - ${gisLayerReference}`
                : 'Beacon Northing and Easting coordinates are recorded in feet.'}
            </p>
          </div>
          <div className="flex items-center gap-1">
            {isLinked ? (
              <>
                <Button
                  type="button"
                  size="sm"
                  variant={mode === 'gis' ? 'default' : 'ghost'}
                  onClick={() => setMode('gis')}
                  disabled={!gisGeometry || isLoadingGis}
                >
                  {isLoadingGis ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Globe2 className="mr-2 h-4 w-4" />
                  )}
                  GIS
                </Button>
                <Button
                  type="button"
                  size="sm"
                  variant={mode === 'survey' ? 'default' : 'ghost'}
                  onClick={() => setMode('survey')}
                >
                  <Ruler className="mr-2 h-4 w-4" />
                  Survey
                </Button>
              </>
            ) : (
              <Ruler className="h-5 w-5 text-muted-foreground" />
            )}
          </div>
        </div>
        {mode === 'gis' && gisGeometry ? (
          <MapContainer
            center={[0, 0]}
            zoom={2}
            scrollWheelZoom
            className="h-[420px] w-full bg-background"
          >
            <TileLayer
              url={configuration?.baseMapTileUrl || DEFAULT_BASE_MAP_URL}
              attribution={
                configuration?.baseMapTileUrl
                  ? undefined
                  : '&copy; OpenStreetMap contributors'
              }
            />
            <GeoJsonLayer
              data={gisGeometry as GeoJsonObject}
              style={{
                color: '#0f766e',
                fillColor: '#14b8a6',
                fillOpacity: 0.22,
                weight: 3,
              }}
            />
            <GisMapSync geometry={gisGeometry} />
          </MapContainer>
        ) : allSurveyPoints.length >= 3 && forceSurvey ? (
          <SurveyPlanCanvas
            points={points}
            demarcations={demarcationPolygons}
          />
        ) : allSurveyPoints.length >= 3 ? (
          <MapContainer
            center={DEFAULT_CENTER}
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
            <MapSync
              points={allSurveyPoints}
              origin={surveyOrigin}
              fitToPoints={!forceSurvey}
            />
            {surveyGridLines.map((line, index) => (
              <Polyline
                key={`grid-${index}`}
                positions={line.map((point) =>
                  normalizePlanPoint(point, surveyOrigin)
                )}
                pathOptions={GRID_LINE_STYLE}
                interactive={false}
              />
            ))}
            <Polygon
              positions={points.map((point) =>
                toPlanPoint(point, surveyOrigin)
              )}
              pathOptions={{
                color: '#1d4ed8',
                fillColor: '#60a5fa',
                fillOpacity: 0.08,
                weight: 3,
              }}
            >
              <Tooltip sticky>
                <div className="mb-1 font-medium">Main cadastral boundary</div>
                <div className="space-y-1">
                  {points.map((point) => (
                    <div key={`${point.beacon}-${point.northing}-${point.easting}`}>{formatPoint(point)}</div>
                  ))}
                </div>
              </Tooltip>
            </Polygon>
            {points.map((point) => (
              <CircleMarker
                key={`beacon-${point.beacon}-${point.northing}-${point.easting}`}
                center={toPlanPoint(point, surveyOrigin)}
                radius={5}
                pathOptions={{
                  color: '#1d4ed8',
                  fillColor: '#ffffff',
                  fillOpacity: 1,
                  weight: 2,
                }}
              >
                <Tooltip direction="top" permanent>
                  {point.beacon}
                </Tooltip>
              </CircleMarker>
            ))}
            {demarcationPolygons.map((demarcation, index) => {
              const color = demarcation.isDraft
                ? '#dc2626'
                : ['#0f766e', '#7c3aed', '#c2410c', '#047857'][index % 4];
              return (
                <Polygon
                  key={demarcation.id}
                  positions={demarcation.points.map((point) =>
                    toPlanPoint(point, surveyOrigin)
                  )}
                  pathOptions={{
                    color,
                    fillColor: color,
                    fillOpacity: demarcation.isDraft ? 0.12 : 0.22,
                    dashArray: demarcation.isDraft ? '8 6' : undefined,
                    weight: 3,
                  }}
                >
                  <Tooltip sticky>
                    <div className="font-medium">
                      {demarcation.isDraft ? 'Unsaved demarcation' : demarcation.description}
                    </div>
                  </Tooltip>
                </Polygon>
              );
            })}
          </MapContainer>
        ) : (
          <div className="flex h-[360px] flex-col items-center justify-center gap-3 text-center text-sm text-muted-foreground">
            <div className="flex h-12 w-12 items-center justify-center rounded-md border bg-background">
              <MapPin className="h-5 w-5" />
            </div>
            <span>No verified beacon boundary has been pushed to this land bank record yet.</span>
          </div>
        )}
        {gisError ? (
          <div className="border-t bg-background px-4 py-2 text-xs text-amber-700">
            {gisError}
          </div>
        ) : null}
      </div>

      {showBeaconSchedule && points.length ? (
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
