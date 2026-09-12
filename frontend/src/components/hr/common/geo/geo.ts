/**
 * Coordinate helpers shared by the map picker, the location form and the geofence-zone dialog.
 * No Leaflet here on purpose: pages import these without pulling the map bundle in.
 */

export interface GeoPoint {
  lat: number;
  lng: number;
}

export type GeoPickerMode = 'point' | 'circle' | 'polygon';

export interface GeoPickerValue {
  /** The pin (point mode) or the circle centre. */
  centre?: GeoPoint | null;
  /** Circle mode only. */
  radiusMetres?: number | null;
  /** Polygon mode only, in drawing order. */
  polygon?: GeoPoint[];
}

/** Six decimals is ~11 cm; more than that is noise from the GPS and clutter in the field. */
export const round6 = (n: number) => Math.round(n * 1e6) / 1e6;

export const isValidLat = (n: number) => Number.isFinite(n) && n >= -90 && n <= 90;
export const isValidLng = (n: number) => Number.isFinite(n) && n >= -180 && n <= 180;

/** '' | undefined -> null, otherwise the number (NaN stays NaN so validation can name it). */
export function toNumberOrNull(value: string | number | null | undefined): number | null {
  if (value === null || value === undefined) return null;
  if (typeof value === 'number') return value;
  const trimmed = value.trim();
  if (trimmed === '') return null;
  return Number(trimmed);
}

/**
 * Reads the zone's `PolygonCoordinatesJson`. Mirrors the server's `GeofencePolygon.TryParse`:
 * `[{"lat","lng"}]` is canonical, but `latitude`/`longitude`/`lon` keys and `[lat, lng]` pairs are
 * accepted because the field used to be typed by hand. Invalid input yields an empty list.
 */
export function parsePolygonJson(json: string | null | undefined): GeoPoint[] {
  if (!json || !json.trim()) return [];
  let parsed: unknown;
  try {
    parsed = JSON.parse(json);
  } catch {
    return [];
  }
  if (!Array.isArray(parsed)) return [];

  const points: GeoPoint[] = [];
  for (const item of parsed) {
    let lat: number | null = null;
    let lng: number | null = null;
    if (Array.isArray(item) && item.length >= 2) {
      lat = Number(item[0]);
      lng = Number(item[1]);
    } else if (item && typeof item === 'object') {
      const rec = item as Record<string, unknown>;
      const pick = (...keys: string[]) => {
        for (const k of Object.keys(rec)) {
          if (keys.includes(k.toLowerCase())) return Number(rec[k]);
        }
        return null;
      };
      lat = pick('lat', 'latitude');
      lng = pick('lng', 'lon', 'long', 'longitude');
    }
    if (lat === null || lng === null || !isValidLat(lat) || !isValidLng(lng)) return [];
    points.push({ lat, lng });
  }

  // A GeoJSON-style ring repeats its first vertex; the picker closes the ring itself.
  if (points.length > 1) {
    const first = points[0];
    const last = points[points.length - 1];
    if (first.lat === last.lat && first.lng === last.lng) points.pop();
  }
  return points;
}

/** Canonical `[{"lat":..,"lng":..}]` form, the shape the server documents. */
export function polygonToJson(points: GeoPoint[]): string {
  return JSON.stringify(points.map((p) => ({ lat: round6(p.lat), lng: round6(p.lng) })));
}

/** Rough size of a polygon, for the register column. Metres across the bounding box diagonal. */
export function polygonSpanMetres(points: GeoPoint[]): number | null {
  if (points.length < 2) return null;
  const lats = points.map((p) => p.lat);
  const lngs = points.map((p) => p.lng);
  const midLat = (Math.min(...lats) + Math.max(...lats)) / 2;
  const dLat = (Math.max(...lats) - Math.min(...lats)) * 111320;
  const dLng = (Math.max(...lngs) - Math.min(...lngs)) * 111320 * Math.cos((midLat * Math.PI) / 180);
  return Math.round(Math.sqrt(dLat * dLat + dLng * dLng));
}
