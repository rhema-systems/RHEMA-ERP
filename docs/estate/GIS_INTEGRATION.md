# Estate GIS Integration

## Architecture

- **SQL Server Spatial** is the authoritative parcel geometry store.
- **GeoServer** connects to the `RhemaERP_GIS` database and publishes the
  `estate.LandParcels` table through WMS/WFS.
- **ArcGIS Feature Service** is optional for ArcGIS editing, analysis, or
  hosted layers.
- **RHEMA ERP** stores provider configuration and links each Estate land asset
  to a provider, layer, and feature ID. Provider credentials remain encrypted
  on the API server.

The ERP API proxies GeoJSON to the browser, so GeoServer credentials and
ArcGIS tokens are never returned to frontend code.

## SQL Server

1. Run `database/gis/001_create_estate_spatial_store.sql`.
2. Create a dedicated GeoServer SQL login.
3. Grant that login `SELECT`, `INSERT`, `UPDATE`, and `DELETE` on the `estate`
   schema only.
4. Confirm the official survey CRS and EPSG code.
5. Add the spatial index after the real parcel coordinate extent is known.

Use SQL Server `geometry` for the local projected survey coordinates. Do not
store local Northing/Easting values in a `geography` column.

## GeoServer

1. Install the GeoServer SQL Server extension that exactly matches the
   GeoServer version.
2. Create an SQL Server data store for `RhemaERP_GIS`.
3. Publish `estate.LandParcels` in an Estate workspace.
4. Set the declared source CRS to the confirmed survey EPSG code.
5. Enable WFS 2.0 and WMS for the published layer.
6. Keep anonymous write access disabled.

The ERP uses WFS `GetCapabilities` for layer discovery and WFS 2.0
`GetFeature` with `resourceId` for parcel geometry.

## RHEMA ERP Setup

1. Open **Estate > GIS Integration**.
2. Enter the SQL Server host, port, database, schema, and source CRS.
3. Enter the GeoServer URL and workspace.
4. Optionally enter an ArcGIS Feature Service URL and access token.
5. Save, test connections, and load layers.
6. Open **Estate > Land Management**, select a land asset, and choose
   **Link GIS**.
7. Select its provider and layer, then enter the GeoServer feature ID or
   ArcGIS object ID.

Linked parcels open on the GIS base map. The original survey-plan view remains
available for checking beacon coordinates.

## Production Controls

- Use HTTPS for GeoServer and ArcGIS endpoints.
- Public GIS endpoints are permitted after DNS validation. Private, loopback,
  and reserved network targets must be explicitly approved by deployment
  administrators through `EstateGisNetworkSecurity:AllowedHosts`. DNS is
  revalidated for each server connection and HTTP redirects are disabled.
- Restrict GIS setup to tenant and system administrators.
- Rotate GeoServer passwords and ArcGIS tokens on a defined schedule.
- Keep GeoServer behind the API or an authenticated reverse proxy.
- Back up the ERP database and `RhemaERP_GIS` database together.
- Monitor failed connection tests and GIS sync status.
- Confirm the source CRS with the Survey/GIS team before loading production
  parcels.
