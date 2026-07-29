'use client';

import Link from 'next/link';
import React from 'react';
import {
  ArrowLeft,
  Check,
  Database,
  Globe2,
  Layers3,
  Loader2,
  PlugZap,
  RefreshCw,
  Save,
  Server,
} from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { useAuth } from '@/hooks/use-auth';
import {
  estateGisService,
  type EstateGisConfiguration,
  type EstateGisLayer,
  type UpdateEstateGisConfiguration,
} from '@/services/estate-gis.service';

const ADMIN_ROLES = ['admin', 'Admin', 'SystemAdmin', 'SuperAdmin', 'TenantAdmin'];

const EMPTY_CONFIGURATION: EstateGisConfiguration = {
  isEnabled: false,
  sourceCrs: '',
  displayCrs: 'EPSG:4326',
  baseMapTileUrl: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
  hasGeoServerCredentials: false,
  hasArcGisToken: false,
  connectionStatus: 'NotTested',
};

function statusVariant(status: string): 'default' | 'secondary' | 'destructive' | 'outline' {
  if (status === 'Connected') return 'default';
  if (status === 'Failed') return 'destructive';
  return 'outline';
}

export default function EstateGisIntegrationPage() {
  const { hasAnyRole } = useAuth();
  const canAdminister = hasAnyRole(ADMIN_ROLES);
  const [configuration, setConfiguration] =
    React.useState<EstateGisConfiguration>(EMPTY_CONFIGURATION);
  const [geoServerUsername, setGeoServerUsername] = React.useState('');
  const [geoServerPassword, setGeoServerPassword] = React.useState('');
  const [arcGisToken, setArcGisToken] = React.useState('');
  const [clearGeoServerCredentials, setClearGeoServerCredentials] =
    React.useState(false);
  const [clearArcGisToken, setClearArcGisToken] = React.useState(false);
  const [layers, setLayers] = React.useState<EstateGisLayer[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [isTesting, setIsTesting] = React.useState(false);
  const [isLoadingLayers, setIsLoadingLayers] = React.useState(false);

  React.useEffect(() => {
    const load = async () => {
      try {
        setConfiguration(await estateGisService.getConfiguration());
      } catch (error) {
        toast.error(error instanceof Error ? error.message : 'Unable to load GIS configuration.');
      } finally {
        setIsLoading(false);
      }
    };
    void load();
  }, []);

  const update = <K extends keyof EstateGisConfiguration>(
    key: K,
    value: EstateGisConfiguration[K]
  ) => setConfiguration((current) => ({ ...current, [key]: value }));

  const buildPayload = (): UpdateEstateGisConfiguration => ({
    isEnabled: configuration.isEnabled,
    sqlServerHost: configuration.sqlServerHost,
    sqlServerPort: configuration.sqlServerPort,
    sqlServerDatabase: configuration.sqlServerDatabase,
    sqlServerSchema: configuration.sqlServerSchema,
    geoServerBaseUrl: configuration.geoServerBaseUrl,
    geoServerWorkspace: configuration.geoServerWorkspace,
    defaultFeatureLayer: configuration.defaultFeatureLayer,
    arcGisFeatureServiceUrl: configuration.arcGisFeatureServiceUrl,
    baseMapTileUrl: configuration.baseMapTileUrl,
    sourceCrs: configuration.sourceCrs,
    displayCrs: configuration.displayCrs,
    geoServerUsername: geoServerUsername || undefined,
    geoServerPassword: geoServerPassword || undefined,
    arcGisToken: arcGisToken || undefined,
    clearGeoServerCredentials,
    clearArcGisToken,
  });

  const save = async () => {
    setIsSaving(true);
    try {
      const saved = await estateGisService.saveConfiguration(buildPayload());
      setConfiguration(saved);
      setGeoServerUsername('');
      setGeoServerPassword('');
      setArcGisToken('');
      setClearGeoServerCredentials(false);
      setClearArcGisToken(false);
      toast.success('Estate GIS configuration saved.');
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Unable to save GIS configuration.');
    } finally {
      setIsSaving(false);
    }
  };

  const test = async () => {
    setIsTesting(true);
    try {
      const result = await estateGisService.testConnections();
      const refreshed = await estateGisService.getConfiguration();
      setConfiguration(refreshed);
      result.providers.forEach((provider) =>
        provider.success
          ? toast.success(`${provider.provider}: ${provider.message}`)
          : toast.error(`${provider.provider}: ${provider.message}`)
      );
    } catch (error) {
      const refreshed = await estateGisService.getConfiguration().catch(() => null);
      if (refreshed) setConfiguration(refreshed);
      toast.error(error instanceof Error ? error.message : 'GIS connection test failed.');
    } finally {
      setIsTesting(false);
    }
  };

  const loadLayers = async () => {
    setIsLoadingLayers(true);
    try {
      setLayers(await estateGisService.getLayers());
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Unable to load GIS layers.');
    } finally {
      setIsLoadingLayers(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex min-h-[420px] items-center justify-center">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div className="flex items-start gap-3">
          <Button asChild variant="ghost" size="icon" aria-label="Back to Land Management">
            <Link href="/estate/land-management">
              <ArrowLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <p className="text-sm font-medium text-teal-700">Estate</p>
            <h1 className="text-2xl font-bold">GIS Integration</h1>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <Badge variant={statusVariant(configuration.connectionStatus)}>
            {configuration.connectionStatus}
          </Badge>
          {canAdminister ? (
            <>
              <Button variant="outline" onClick={() => void test()} disabled={isTesting}>
                {isTesting ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <PlugZap className="mr-2 h-4 w-4" />
                )}
                Test
              </Button>
              <Button onClick={() => void save()} disabled={isSaving}>
                {isSaving ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Save className="mr-2 h-4 w-4" />
                )}
                Save
              </Button>
            </>
          ) : null}
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardContent className="flex items-center justify-between p-5">
            <div>
              <p className="text-sm text-muted-foreground">SQL Server Spatial</p>
              <p className="mt-1 font-semibold">
                {configuration.sqlServerDatabase || 'Not configured'}
              </p>
            </div>
            <Database className="h-5 w-5 text-muted-foreground" />
          </CardContent>
        </Card>
        <Card>
          <CardContent className="flex items-center justify-between p-5">
            <div>
              <p className="text-sm text-muted-foreground">GeoServer</p>
              <p className="mt-1 font-semibold">
                {configuration.geoServerBaseUrl ? 'Configured' : 'Not configured'}
              </p>
            </div>
            <Server className="h-5 w-5 text-muted-foreground" />
          </CardContent>
        </Card>
        <Card>
          <CardContent className="flex items-center justify-between p-5">
            <div>
              <p className="text-sm text-muted-foreground">ArcGIS</p>
              <p className="mt-1 font-semibold">
                {configuration.arcGisFeatureServiceUrl ? 'Configured' : 'Not configured'}
              </p>
            </div>
            <Globe2 className="h-5 w-5 text-muted-foreground" />
          </CardContent>
        </Card>
      </div>

      <div className="flex items-center justify-between gap-4 border-y py-4">
        <Label htmlFor="gis-enabled" className="text-base font-semibold">
          Estate GIS enabled
        </Label>
        <Switch
          id="gis-enabled"
          checked={configuration.isEnabled}
          onCheckedChange={(checked) => update('isEnabled', checked)}
          disabled={!canAdminister}
        />
      </div>

      <div className="grid gap-6 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Database className="h-4 w-4" />
              SQL Server Spatial
            </CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-2">
            <Field
              label="Server host"
              value={configuration.sqlServerHost || ''}
              onChange={(value) => update('sqlServerHost', value)}
              disabled={!canAdminister}
            />
            <Field
              label="Port"
              type="number"
              value={configuration.sqlServerPort?.toString() || ''}
              onChange={(value) =>
                update('sqlServerPort', value ? Number.parseInt(value, 10) : undefined)
              }
              disabled={!canAdminister}
            />
            <Field
              label="GIS database"
              value={configuration.sqlServerDatabase || ''}
              onChange={(value) => update('sqlServerDatabase', value)}
              disabled={!canAdminister}
            />
            <Field
              label="Schema"
              value={configuration.sqlServerSchema || ''}
              onChange={(value) => update('sqlServerSchema', value)}
              disabled={!canAdminister}
            />
            <Field
              label="Source CRS"
              value={configuration.sourceCrs}
              onChange={(value) => update('sourceCrs', value)}
              placeholder="EPSG:XXXX"
              disabled={!canAdminister}
            />
            <Field
              label="Display CRS"
              value={configuration.displayCrs}
              onChange={(value) => update('displayCrs', value)}
              placeholder="EPSG:4326"
              disabled={!canAdminister}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Server className="h-4 w-4" />
              GeoServer
            </CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-2">
            <div className="sm:col-span-2">
              <Field
                label="GeoServer URL"
                value={configuration.geoServerBaseUrl || ''}
                onChange={(value) => update('geoServerBaseUrl', value)}
                placeholder="https://gis.example.com/geoserver"
                disabled={!canAdminister}
              />
            </div>
            <Field
              label="Workspace"
              value={configuration.geoServerWorkspace || ''}
              onChange={(value) => update('geoServerWorkspace', value)}
              disabled={!canAdminister}
            />
            <Field
              label="Default feature layer"
              value={configuration.defaultFeatureLayer || ''}
              onChange={(value) => update('defaultFeatureLayer', value)}
              disabled={!canAdminister}
            />
            <Field
              label="Username"
              value={geoServerUsername}
              onChange={setGeoServerUsername}
              placeholder={
                configuration.hasGeoServerCredentials ? 'Stored securely' : undefined
              }
              disabled={!canAdminister || clearGeoServerCredentials}
            />
            <Field
              label="Password"
              type="password"
              value={geoServerPassword}
              onChange={setGeoServerPassword}
              placeholder={
                configuration.hasGeoServerCredentials ? 'Stored securely' : undefined
              }
              disabled={!canAdminister || clearGeoServerCredentials}
            />
            {canAdminister && configuration.hasGeoServerCredentials ? (
              <CredentialClear
                id="clear-geoserver"
                label="Remove stored GeoServer credentials"
                checked={clearGeoServerCredentials}
                onCheckedChange={setClearGeoServerCredentials}
              />
            ) : null}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Globe2 className="h-4 w-4" />
              ArcGIS
            </CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4">
            <Field
              label="Feature service URL"
              value={configuration.arcGisFeatureServiceUrl || ''}
              onChange={(value) => update('arcGisFeatureServiceUrl', value)}
              placeholder="https://services.arcgis.com/.../FeatureServer"
              disabled={!canAdminister}
            />
            <Field
              label="Access token"
              type="password"
              value={arcGisToken}
              onChange={setArcGisToken}
              placeholder={configuration.hasArcGisToken ? 'Stored securely' : undefined}
              disabled={!canAdminister || clearArcGisToken}
            />
            {canAdminister && configuration.hasArcGisToken ? (
              <CredentialClear
                id="clear-arcgis"
                label="Remove stored ArcGIS token"
                checked={clearArcGisToken}
                onCheckedChange={setClearArcGisToken}
              />
            ) : null}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Layers3 className="h-4 w-4" />
              Web Map
            </CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4">
            <Field
              label="Base-map tile URL"
              value={configuration.baseMapTileUrl || ''}
              onChange={(value) => update('baseMapTileUrl', value)}
              placeholder="https://.../{z}/{x}/{y}.png"
              disabled={!canAdminister}
            />
            <div className="flex justify-end">
              <Button
                variant="outline"
                onClick={() => void loadLayers()}
                disabled={isLoadingLayers}
              >
                {isLoadingLayers ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <RefreshCw className="mr-2 h-4 w-4" />
                )}
                Load Layers
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>

      {layers.length ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Available Layers</CardTitle>
          </CardHeader>
          <CardContent className="overflow-x-auto p-0">
            <table className="w-full min-w-[720px] text-sm">
              <thead className="border-y bg-muted/60 text-left text-xs uppercase text-muted-foreground">
                <tr>
                  <th className="px-5 py-3 font-medium">Provider</th>
                  <th className="px-5 py-3 font-medium">Layer</th>
                  <th className="px-5 py-3 font-medium">Reference</th>
                  <th className="px-5 py-3 font-medium">CRS</th>
                  <th className="px-5 py-3 text-right font-medium">Default</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {layers.map((layer) => (
                  <tr key={`${layer.provider}:${layer.layerReference}`}>
                    <td className="px-5 py-3">{layer.provider}</td>
                    <td className="px-5 py-3 font-medium">{layer.title}</td>
                    <td className="px-5 py-3">{layer.layerReference}</td>
                    <td className="px-5 py-3">{layer.sourceCrs || 'Not reported'}</td>
                    <td className="px-5 py-3 text-right">
                      {configuration.defaultFeatureLayer === layer.layerReference ? (
                        <Badge>
                          <Check className="mr-1 h-3.5 w-3.5" />
                          Selected
                        </Badge>
                      ) : canAdminister ? (
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() =>
                            update('defaultFeatureLayer', layer.layerReference)
                          }
                        >
                          Use
                        </Button>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </CardContent>
        </Card>
      ) : null}
    </div>
  );
}

function Field({
  label,
  value,
  onChange,
  type = 'text',
  placeholder,
  disabled,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: React.HTMLInputTypeAttribute;
  placeholder?: string;
  disabled?: boolean;
}) {
  const id = React.useId();
  return (
    <div className="space-y-2">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        type={type}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        disabled={disabled}
      />
    </div>
  );
}

function CredentialClear({
  id,
  label,
  checked,
  onCheckedChange,
}: {
  id: string;
  label: string;
  checked: boolean;
  onCheckedChange: (checked: boolean) => void;
}) {
  return (
    <div className="flex items-center gap-2 sm:col-span-2">
      <Checkbox
        id={id}
        checked={checked}
        onCheckedChange={(value) => onCheckedChange(value === true)}
      />
      <Label htmlFor={id} className="font-normal">
        {label}
      </Label>
    </div>
  );
}
