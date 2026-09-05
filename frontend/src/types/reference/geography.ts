/**
 * Administrative geography — the shared Region / District / Town reference tree.
 *
 * ⚠ Not an HR type. HR is the first consumer; Estate, Sales, Procurement and Inventory carry the
 * same free-text region and district columns and are expected to move onto this.
 * See docs/GEOGRAPHY-REFERENCE-DESIGN.md.
 */

/** Why an area answers to an alternate name. Mirrors the backend `GeoAreaAliasKind`. */
export type GeoAreaAliasKind =
  | 'FormerName'
  | 'Spelling'
  | 'Abbreviation'
  | 'Vernacular'
  | 'Other';

export interface GeoAreaAlias {
  id: string;
  geoAreaId: string;
  alias: string;
  kind: GeoAreaAliasKind;
  /** Ready to print — the backend already turned the enum into words. */
  kindLabel: string;
  notes: string | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
}

export interface GeoAreaAliasRequest {
  geoAreaId: string;
  alias: string;
  kind: GeoAreaAliasKind;
  notes: string | null;
}

/** One country's way of dividing itself up. */
export interface GeoScheme {
  id: string;
  countryId: string;
  countryName: string;
  countryCode: string;
  name: string;
  code: string;
  description: string | null;
  isDefault: boolean;
  isActive: boolean;
  /** How many tiers — the depth of this scheme's address form. */
  levelCount: number;
  /** Areas loaded against it. Zero means the seed never ran. */
  areaCount: number;
  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
}

/** A scheme with its tiers — everything an address form needs to render itself. */
export interface GeoSchemeDetail extends GeoScheme {
  levels: GeoLevel[];
}

export interface GeoSchemeRequest {
  countryId: string;
  name: string;
  code: string;
  description: string | null;
  isDefault: boolean;
  isActive: boolean;
}

/**
 * One tier of a scheme.
 *
 * ⚠ `name` is the **label the address form prints**. That is how one component serves every
 * country: Region / District / Town in Ghana, State / LGA / Ward in Nigeria, no code difference.
 */
export interface GeoLevel {
  id: string;
  schemeId: string;
  schemeName: string;
  name: string;
  code: string;
  description: string | null;
  /** 1 = broadest. Unique within the scheme; it is what orders the cascade. */
  levelNumber: number;
  isRequiredInAddress: boolean;
  allowsAddressAssignment: boolean;
  isActive: boolean;
  areaCount: number;
  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
}

export interface GeoLevelRequest {
  schemeId: string;
  name: string;
  code: string;
  description: string | null;
  levelNumber: number;
  isRequiredInAddress: boolean;
  allowsAddressAssignment: boolean;
  isActive: boolean;
}

export interface GeoArea {
  id: string;
  schemeId: string;
  schemeName: string;
  geoLevelId: string;
  geoLevelName: string;
  levelNumber: number;
  parentAreaId: string | null;
  parentAreaName: string | null;
  name: string;
  /** The official statutory code. The seeder's idempotency key, so it must stay stable. */
  code: string;
  path: string;
  latitude: number | null;
  longitude: number | null;
  polygonCoordinatesJson: string | null;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  supersededByGeoAreaId: string | null;
  supersededByAreaName: string | null;
  isActive: boolean;
  notes: string | null;
  /** End-dated: still resolves for records that point at it, never offered for new ones. */
  isHistorical: boolean;
  childCount: number;
  aliases: GeoAreaAlias[];
  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
}

export interface GeoAreaRequest {
  schemeId: string;
  geoLevelId: string;
  parentAreaId: string | null;
  name: string;
  code: string;
  latitude: number | null;
  longitude: number | null;
  polygonCoordinatesJson: string | null;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  supersededByGeoAreaId: string | null;
  isActive: boolean;
  notes: string | null;
}

export interface GeoAreaTreeNode {
  id: string;
  geoLevelId: string;
  geoLevelName: string;
  levelNumber: number;
  parentAreaId: string | null;
  name: string;
  code: string;
  isActive: boolean;
  isHistorical: boolean;
  children: GeoAreaTreeNode[];
}

/** The lean shape a cascading dropdown needs. */
export interface GeoAreaOption {
  id: string;
  name: string;
  code: string;
  geoLevelId: string;
  parentAreaId: string | null;
}

/** An area resolved from a name, with the ancestors that place it. */
export interface GeoAreaResolution {
  id: string;
  name: string;
  geoLevelId: string;
  geoLevelName: string;
  /** Broadest first, ending with the area itself. */
  ancestors: GeoAreaOption[];
  /** Set when the match came through an alternate name rather than the current one. */
  matchedAlias: string | null;
  isHistorical: boolean;
  supersededByGeoAreaId: string | null;
}
