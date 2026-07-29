import { compatibleApiService as apiService } from './compatibleApiService';
import { apiService as rawApiService } from './api.service';
import type { FeatureCollection } from 'geojson';

export interface EstateGisConfiguration {
  id?: string;
  isEnabled: boolean;
  sqlServerHost?: string;
  sqlServerPort?: number;
  sqlServerDatabase?: string;
  sqlServerSchema?: string;
  geoServerBaseUrl?: string;
  geoServerWorkspace?: string;
  defaultFeatureLayer?: string;
  arcGisFeatureServiceUrl?: string;
  baseMapTileUrl?: string;
  sourceCrs: string;
  displayCrs: string;
  hasGeoServerCredentials: boolean;
  hasArcGisToken: boolean;
  connectionStatus: string;
  lastConnectionTestAt?: string;
  lastConnectionMessage?: string;
}

export interface UpdateEstateGisConfiguration {
  isEnabled: boolean;
  sqlServerHost?: string;
  sqlServerPort?: number;
  sqlServerDatabase?: string;
  sqlServerSchema?: string;
  geoServerBaseUrl?: string;
  geoServerWorkspace?: string;
  defaultFeatureLayer?: string;
  arcGisFeatureServiceUrl?: string;
  baseMapTileUrl?: string;
  sourceCrs: string;
  displayCrs: string;
  geoServerUsername?: string;
  geoServerPassword?: string;
  arcGisToken?: string;
  clearGeoServerCredentials?: boolean;
  clearArcGisToken?: boolean;
}

export interface EstateGisRuntimeConfiguration {
  isEnabled: boolean;
  hasGeoServer: boolean;
  hasArcGis: boolean;
  defaultFeatureLayer?: string;
  baseMapTileUrl?: string;
  sourceCrs: string;
  displayCrs: string;
}

export interface EstateGisLayer {
  provider: 'GeoServer' | 'ArcGIS';
  layerReference: string;
  title: string;
  sourceCrs?: string;
}

export interface EstateGisProviderTest {
  provider: string;
  success: boolean;
  message: string;
}

export interface EstateGisConnectionTest {
  success: boolean;
  testedAt: string;
  providers: EstateGisProviderTest[];
}

export interface EstateAssetGisLink {
  assetId: string;
  provider: string;
  layerReference: string;
  featureId: string;
  sourceCrs: string;
  syncStatus: string;
  lastSyncedAt?: string;
}

export interface LinkEstateAssetGisFeature {
  provider: 'GeoServer' | 'ArcGIS';
  layerReference: string;
  featureId: string;
  sourceCrs: string;
}

interface ApiResponse<T> {
  success?: boolean;
  data?: T;
  message?: string;
}

class EstateGisService {
  async getConfiguration(): Promise<EstateGisConfiguration> {
    const response = await apiService.get<ApiResponse<EstateGisConfiguration>>(
      '/estate/gis/configuration'
    );
    if (!response.data) throw new Error('Unable to load Estate GIS configuration.');
    return response.data;
  }

  async saveConfiguration(
    payload: UpdateEstateGisConfiguration
  ): Promise<EstateGisConfiguration> {
    const response = await apiService.put<ApiResponse<EstateGisConfiguration>>(
      '/estate/gis/configuration',
      payload
    );
    if (!response.data)
      throw new Error(response.message || 'Unable to save Estate GIS configuration.');
    return response.data;
  }

  async getRuntimeConfiguration(): Promise<EstateGisRuntimeConfiguration> {
    const response =
      await apiService.get<ApiResponse<EstateGisRuntimeConfiguration>>(
        '/estate/gis/runtime'
      );
    if (!response.data)
      throw new Error('Unable to load Estate GIS map configuration.');
    return response.data;
  }

  async testConnections(): Promise<EstateGisConnectionTest> {
    const response = await apiService.post<ApiResponse<EstateGisConnectionTest>>(
      '/estate/gis/test',
      {}
    );
    if (!response.data)
      throw new Error(response.message || 'Unable to test GIS connections.');
    return response.data;
  }

  async getLayers(): Promise<EstateGisLayer[]> {
    const response = await apiService.get<ApiResponse<EstateGisLayer[]>>(
      '/estate/gis/layers'
    );
    return response.data || [];
  }

  async linkAsset(
    assetId: string,
    payload: LinkEstateAssetGisFeature
  ): Promise<EstateAssetGisLink> {
    const response = await apiService.put<ApiResponse<EstateAssetGisLink>>(
      `/estate/gis/assets/${assetId}/link`,
      payload
    );
    if (!response.data)
      throw new Error(response.message || 'Unable to link the land asset to GIS.');
    return response.data;
  }

  async unlinkAsset(assetId: string): Promise<void> {
    await apiService.delete(`/estate/gis/assets/${assetId}/link`);
  }

  async getAssetGeometry(assetId: string): Promise<FeatureCollection> {
    const response = await rawApiService.request<FeatureCollection | string>(
      `/estate/gis/assets/${assetId}/geometry`
    );
    return typeof response === 'string'
      ? (JSON.parse(response) as FeatureCollection)
      : response;
  }
}

export const estateGisService = new EstateGisService();
