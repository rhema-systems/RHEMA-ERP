'use client';

import { useEffect, useMemo, useRef, useState } from 'react';
import L from 'leaflet';
import {
  Circle,
  MapContainer,
  Marker,
  Polygon,
  Polyline,
  TileLayer,
  Tooltip,
  useMap,
  useMapEvents,
} from 'react-leaflet';
import { LocateFixed, Loader2, RotateCcw, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Slider } from '@/components/ui/slider';
import { isValidLat, isValidLng, round6, type GeoPickerMode, type GeoPickerValue, type GeoPoint } from './geo';

/**
 * The Leaflet half of the picker. Loaded through `GeoPicker` with `ssr: false` — Leaflet touches
 * `window` at import time, so this file must never be imported directly from a page.
 *
 * Tiles are the public OpenStreetMap servers, the same default the Estate module's land-bank map
 * uses. That is fine for an admin screen; a production deployment behind a firewall, or one with
 * real traffic, should pass a `tileUrl` pointing at a hosted or self-hosted tile source.
 */

const DEFAULT_TILE_URL = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';
const OSM_ATTRIBUTION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';

/** Accra. The client's sites are in Ghana; the map opens here when there is nothing to show yet. */
const DEFAULT_CENTRE: [number, number] = [5.6037, -0.187];
const DEFAULT_ZOOM = 12;
const PLACED_ZOOM = 17;

const MAX_RADIUS_SLIDER = 2000;

/** Leaflet's stock marker needs image assets Next does not serve; a CSS pin needs none. */
const centreIcon = L.divIcon({
  className: '',
  html: '<div style="width:18px;height:18px;border-radius:50%;background:#2563eb;border:3px solid #fff;box-shadow:0 1px 4px rgba(0,0,0,.45)"></div>',
  iconSize: [18, 18],
  iconAnchor: [9, 9],
});

const vertexIcon = L.divIcon({
  className: '',
  html: '<div style="width:12px;height:12px;border-radius:50%;background:#fff;border:2px solid #2563eb;box-shadow:0 1px 3px rgba(0,0,0,.4)"></div>',
  iconSize: [12, 12],
  iconAnchor: [6, 6],
});

const referenceIcon = L.divIcon({
  className: '',
  html: '<div style="width:14px;height:14px;border-radius:50%;background:#f97316;border:2px solid #fff;box-shadow:0 1px 3px rgba(0,0,0,.4)"></div>',
  iconSize: [14, 14],
  iconAnchor: [7, 7],
});

export interface GeoPickerReference {
  centre?: GeoPoint | null;
  radiusMetres?: number | null;
  polygon?: GeoPoint[];
  label?: string;
}

export interface GeoPickerMapProps {
  mode: GeoPickerMode;
  value: GeoPickerValue;
  onChange: (next: GeoPickerValue) => void;
  /** Pixel height of the map. */
  height?: number;
  tileUrl?: string;
  disabled?: boolean;
  /** A read-only overlay, e.g. the linked zone while placing a location's pin. */
  reference?: GeoPickerReference | null;
}

const toLatLng = (p: GeoPoint): [number, number] => [p.lat, p.lng];

function ClickCapture({ onClick }: { onClick: (p: GeoPoint) => void }) {
  useMapEvents({
    click: (e) => onClick({ lat: round6(e.latlng.lat), lng: round6(e.latlng.lng) }),
  });
  return null;
}

/** Fits the initial content once, and re-measures the container (dialogs mount it at 0×0). */
function FitOnMount({ bounds }: { bounds: L.LatLngBounds | null }) {
  const map = useMap();
  const fitted = useRef(false);
  useEffect(() => {
    const handle = window.setTimeout(() => {
      map.invalidateSize();
      if (bounds && !fitted.current) {
        fitted.current = true;
        if (bounds.isValid()) {
          if (bounds.getNorthEast().equals(bounds.getSouthWest())) {
            map.setView(bounds.getCenter(), PLACED_ZOOM);
          } else {
            map.fitBounds(bounds, { padding: [28, 28], maxZoom: PLACED_ZOOM });
          }
        }
      }
    }, 0);
    return () => window.clearTimeout(handle);
  }, [map, bounds]);
  return null;
}

/** Moves the view when the user asks for their position or types a coordinate. */
function FlyTo({ target }: { target: { point: GeoPoint; key: number } | null }) {
  const map = useMap();
  useEffect(() => {
    if (!target) return;
    map.flyTo(toLatLng(target.point), Math.max(map.getZoom(), PLACED_ZOOM), { duration: 0.6 });
  }, [map, target]);
  return null;
}

export default function GeoPickerMap({
  mode,
  value,
  onChange,
  height = 320,
  tileUrl,
  disabled = false,
  reference,
}: GeoPickerMapProps) {
  const [locating, setLocating] = useState(false);
  const [locateMessage, setLocateMessage] = useState<string | null>(null);
  const [flyTarget, setFlyTarget] = useState<{ point: GeoPoint; key: number } | null>(null);

  const centre = value.centre && isValidLat(value.centre.lat) && isValidLng(value.centre.lng) ? value.centre : null;
  const polygon = useMemo(() => (value.polygon ?? []).filter((p) => isValidLat(p.lat) && isValidLng(p.lng)), [value.polygon]);
  const radius = value.radiusMetres && value.radiusMetres > 0 ? value.radiusMetres : null;

  // Computed once: the map should not re-fit while the user is placing points.
  const [initialBounds] = useState<L.LatLngBounds | null>(() => {
    const pts: [number, number][] = [];
    if (centre) pts.push(toLatLng(centre));
    for (const p of polygon) pts.push(toLatLng(p));
    if (reference?.centre) pts.push(toLatLng(reference.centre));
    for (const p of reference?.polygon ?? []) pts.push(toLatLng(p));
    if (!pts.length) return null;
    const b = L.latLngBounds(pts);
    if (mode === 'circle' && centre && radius) b.extend(L.latLng(centre.lat, centre.lng).toBounds(radius * 2));
    if (reference?.centre && reference.radiusMetres) {
      b.extend(L.latLng(reference.centre.lat, reference.centre.lng).toBounds(reference.radiusMetres * 2));
    }
    return b;
  });

  // A typed coordinate moves the view too; only when it changes by more than GPS jitter.
  const lastCentreRef = useRef<GeoPoint | null>(centre);
  useEffect(() => {
    if (!centre) return;
    const last = lastCentreRef.current;
    lastCentreRef.current = centre;
    if (!last || Math.abs(last.lat - centre.lat) > 0.002 || Math.abs(last.lng - centre.lng) > 0.002) {
      setFlyTarget({ point: centre, key: Date.now() });
    }
  }, [centre]);

  const handleClick = (p: GeoPoint) => {
    if (disabled) return;
    if (mode === 'polygon') {
      onChange({ ...value, polygon: [...polygon, p] });
    } else {
      onChange({ ...value, centre: p });
    }
  };

  const moveVertex = (index: number, p: GeoPoint) => {
    const next = polygon.slice();
    next[index] = p;
    onChange({ ...value, polygon: next });
  };

  const undoVertex = () => onChange({ ...value, polygon: polygon.slice(0, -1) });
  const clear = () =>
    onChange(mode === 'polygon' ? { ...value, polygon: [] } : { ...value, centre: null });

  const useMyLocation = () => {
    if (typeof navigator === 'undefined' || !navigator.geolocation) {
      setLocateMessage('This browser cannot report a position.');
      return;
    }
    setLocating(true);
    setLocateMessage(null);
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        const p = { lat: round6(pos.coords.latitude), lng: round6(pos.coords.longitude) };
        setLocating(false);
        setLocateMessage(`Position captured (±${Math.round(pos.coords.accuracy)} m).`);
        setFlyTarget({ point: p, key: Date.now() });
        handleClick(p);
      },
      (err) => {
        setLocating(false);
        setLocateMessage(
          err.code === err.PERMISSION_DENIED
            ? 'Location access was refused. Allow it in the browser, and note that it only works over HTTPS.'
            : 'Could not get a position. Try again outdoors or type the coordinates.',
        );
      },
      { enableHighAccuracy: true, timeout: 12000, maximumAge: 0 },
    );
  };

  const hint =
    mode === 'polygon'
      ? polygon.length < 3
        ? `Click the map to add corners (${polygon.length}/3 minimum). Drag a corner to adjust it.`
        : `${polygon.length} corners. Click to add more, drag to adjust.`
      : mode === 'circle'
        ? centre
          ? 'Drag the pin or click elsewhere to move the centre. Set the radius below.'
          : 'Click the map to set the centre of the zone.'
        : centre
          ? 'Drag the pin or click elsewhere to move it.'
          : 'Click the map where this site is.';

  return (
    <div className="space-y-2">
      <div className="flex flex-wrap items-center gap-2">
        <Button type="button" size="sm" variant="outline" onClick={useMyLocation} disabled={disabled || locating}>
          {locating ? <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" /> : <LocateFixed className="mr-1.5 h-3.5 w-3.5" />}
          Use my position
        </Button>
        {mode === 'polygon' && (
          <Button type="button" size="sm" variant="ghost" onClick={undoVertex} disabled={disabled || polygon.length === 0}>
            <RotateCcw className="mr-1.5 h-3.5 w-3.5" />
            Undo corner
          </Button>
        )}
        <Button
          type="button"
          size="sm"
          variant="ghost"
          onClick={clear}
          disabled={disabled || (mode === 'polygon' ? polygon.length === 0 : !centre)}
        >
          <Trash2 className="mr-1.5 h-3.5 w-3.5" />
          Clear
        </Button>
        <span className="text-xs text-muted-foreground">{hint}</span>
      </div>

      <div className="overflow-hidden rounded-md border" style={{ height }}>
        <MapContainer
          center={centre ? toLatLng(centre) : DEFAULT_CENTRE}
          zoom={centre ? PLACED_ZOOM : DEFAULT_ZOOM}
          scrollWheelZoom
          style={{ height: '100%', width: '100%' }}
        >
          <TileLayer url={tileUrl || DEFAULT_TILE_URL} attribution={OSM_ATTRIBUTION} />
          <FitOnMount bounds={initialBounds} />
          <FlyTo target={flyTarget} />
          {!disabled && <ClickCapture onClick={handleClick} />}

          {/* Read-only context, e.g. the zone a location is linked to. */}
          {reference?.centre && (
            <Marker position={toLatLng(reference.centre)} icon={referenceIcon} interactive={false}>
              {reference.label && <Tooltip permanent direction="top" offset={[0, -8]}>{reference.label}</Tooltip>}
            </Marker>
          )}
          {reference?.centre && reference.radiusMetres ? (
            <Circle
              center={toLatLng(reference.centre)}
              radius={reference.radiusMetres}
              pathOptions={{ color: '#f97316', fillColor: '#f97316', fillOpacity: 0.08, weight: 1.5, dashArray: '6 4' }}
              interactive={false}
            />
          ) : null}
          {reference?.polygon && reference.polygon.length >= 3 && (
            <Polygon
              positions={reference.polygon.map(toLatLng)}
              pathOptions={{ color: '#f97316', fillColor: '#f97316', fillOpacity: 0.08, weight: 1.5, dashArray: '6 4' }}
              interactive={false}
            >
              {reference.label && <Tooltip sticky>{reference.label}</Tooltip>}
            </Polygon>
          )}

          {mode !== 'polygon' && centre && (
            <Marker
              position={toLatLng(centre)}
              icon={centreIcon}
              draggable={!disabled}
              eventHandlers={{
                dragend: (e) => {
                  const ll = (e.target as L.Marker).getLatLng();
                  onChange({ ...value, centre: { lat: round6(ll.lat), lng: round6(ll.lng) } });
                },
              }}
            />
          )}
          {mode === 'circle' && centre && radius ? (
            <Circle
              center={toLatLng(centre)}
              radius={radius}
              pathOptions={{ color: '#2563eb', fillColor: '#2563eb', fillOpacity: 0.15, weight: 2 }}
              interactive={false}
            />
          ) : null}

          {mode === 'polygon' && polygon.length >= 3 && (
            <Polygon
              positions={polygon.map(toLatLng)}
              pathOptions={{ color: '#2563eb', fillColor: '#2563eb', fillOpacity: 0.15, weight: 2 }}
              interactive={false}
            />
          )}
          {mode === 'polygon' && polygon.length > 0 && polygon.length < 3 && (
            <Polyline positions={polygon.map(toLatLng)} pathOptions={{ color: '#2563eb', weight: 2, dashArray: '4 4' }} interactive={false} />
          )}
          {mode === 'polygon' &&
            polygon.map((p, i) => (
              <Marker
                key={`${i}-${polygon.length}`}
                position={toLatLng(p)}
                icon={vertexIcon}
                draggable={!disabled}
                eventHandlers={{
                  dragend: (e) => {
                    const ll = (e.target as L.Marker).getLatLng();
                    moveVertex(i, { lat: round6(ll.lat), lng: round6(ll.lng) });
                  },
                }}
              >
                <Tooltip direction="top" offset={[0, -6]}>{`Corner ${i + 1}`}</Tooltip>
              </Marker>
            ))}
        </MapContainer>
      </div>

      {mode === 'circle' && (
        <div className="flex items-center gap-3">
          <span className="w-24 shrink-0 text-xs text-muted-foreground">Radius</span>
          <Slider
            className="flex-1"
            min={10}
            max={MAX_RADIUS_SLIDER}
            step={10}
            disabled={disabled}
            value={[Math.min(Math.max(radius ?? 100, 10), MAX_RADIUS_SLIDER)]}
            onValueChange={([r]) => onChange({ ...value, radiusMetres: r })}
          />
          <span className="w-20 shrink-0 text-right text-xs tabular-nums text-muted-foreground">
            {radius ? `${radius} m` : '—'}
          </span>
        </div>
      )}

      {locateMessage && <p className="text-xs text-muted-foreground">{locateMessage}</p>}
    </div>
  );
}
