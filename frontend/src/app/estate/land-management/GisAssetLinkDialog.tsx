'use client';

import Link from 'next/link';
import React from 'react';
import { ExternalLink, Link2, Loader2, Unlink } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  estateGisService,
  type EstateGisLayer,
} from '@/services/estate-gis.service';
import type { EstateManagedAsset } from '@/services/estate-land-management.service';

interface GisAssetLinkDialogProps {
  asset: EstateManagedAsset | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onLinked: () => Promise<void>;
}

export default function GisAssetLinkDialog({
  asset,
  open,
  onOpenChange,
  onLinked,
}: GisAssetLinkDialogProps) {
  const [provider, setProvider] = React.useState<'GeoServer' | 'ArcGIS'>(
    'GeoServer'
  );
  const [layerReference, setLayerReference] = React.useState('');
  const [featureId, setFeatureId] = React.useState('');
  const [sourceCrs, setSourceCrs] = React.useState('');
  const [layers, setLayers] = React.useState<EstateGisLayer[]>([]);
  const [availableProviders, setAvailableProviders] = React.useState({
    geoServer: false,
    arcGis: false,
  });
  const [isConfigured, setIsConfigured] = React.useState(false);
  const [isLoading, setIsLoading] = React.useState(false);
  const [isSaving, setIsSaving] = React.useState(false);

  React.useEffect(() => {
    if (!open || !asset) return;

    setProvider(
      asset.gisProvider?.toLowerCase() === 'arcgis' ? 'ArcGIS' : 'GeoServer'
    );
    setLayerReference(asset.gisLayerReference || '');
    setFeatureId(asset.gisFeatureId || '');
    setSourceCrs(asset.gisSourceCrs || '');
    setIsLoading(true);

    const load = async () => {
      try {
        const configuration = await estateGisService.getRuntimeConfiguration();
        setIsConfigured(configuration.isEnabled);
        setAvailableProviders({
          geoServer: configuration.hasGeoServer,
          arcGis: configuration.hasArcGis,
        });
        if (!asset.gisSourceCrs) setSourceCrs(configuration.sourceCrs);
        if (configuration.isEnabled) {
          const linkedToArcGis =
            asset.gisProvider?.toLowerCase() === 'arcgis';
          setProvider(
            linkedToArcGis && configuration.hasArcGis
              ? 'ArcGIS'
              : configuration.hasGeoServer
                ? 'GeoServer'
                : 'ArcGIS'
          );
          const discoveredLayers = await estateGisService.getLayers();
          setLayers(discoveredLayers);
          if (!asset.gisLayerReference && configuration.defaultFeatureLayer) {
            const defaultLayer = discoveredLayers.find(
              (layer) =>
                layer.layerReference === configuration.defaultFeatureLayer
            );
            if (defaultLayer) setProvider(defaultLayer.provider);
            setLayerReference(configuration.defaultFeatureLayer);
          }
        }
      } catch (error) {
        toast.error(error instanceof Error ? error.message : 'Unable to load GIS layers.');
      } finally {
        setIsLoading(false);
      }
    };

    void load();
  }, [asset, open]);

  const providerLayers = layers.filter((layer) => layer.provider === provider);

  const save = async () => {
    if (!asset) return;
    setIsSaving(true);
    try {
      await estateGisService.linkAsset(asset.id, {
        provider,
        layerReference,
        featureId,
        sourceCrs,
      });
      await onLinked();
      toast.success('Land asset linked to GIS.');
      onOpenChange(false);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Unable to link land to GIS.');
    } finally {
      setIsSaving(false);
    }
  };

  const unlink = async () => {
    if (!asset) return;
    setIsSaving(true);
    try {
      await estateGisService.unlinkAsset(asset.id);
      await onLinked();
      toast.success('Land asset unlinked from GIS.');
      onOpenChange(false);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Unable to unlink land from GIS.');
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[calc(100vw-2rem)] max-w-2xl">
        <DialogHeader>
          <DialogTitle>GIS Asset Link</DialogTitle>
        </DialogHeader>

        {isLoading ? (
          <div className="flex min-h-56 items-center justify-center">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : !isConfigured ? (
          <div className="flex min-h-56 flex-col items-center justify-center gap-4 rounded-md border">
            <p className="text-sm text-muted-foreground">
              Estate GIS is not enabled.
            </p>
            <Button asChild variant="outline">
              <Link href="/estate/gis">
                <ExternalLink className="mr-2 h-4 w-4" />
                GIS Integration
              </Link>
            </Button>
          </div>
        ) : (
          <div className="grid gap-5 py-2 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>Provider</Label>
              <Select
                value={provider}
                onValueChange={(value) => {
                  setProvider(value as 'GeoServer' | 'ArcGIS');
                  setLayerReference('');
                }}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {availableProviders.geoServer ? (
                    <SelectItem value="GeoServer">GeoServer</SelectItem>
                  ) : null}
                  {availableProviders.arcGis ? (
                    <SelectItem value="ArcGIS">ArcGIS</SelectItem>
                  ) : null}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="gis-source-crs">Source CRS</Label>
              <Input
                id="gis-source-crs"
                value={sourceCrs}
                onChange={(event) => setSourceCrs(event.target.value)}
                placeholder="EPSG:XXXX"
              />
            </div>

            <div className="space-y-2 sm:col-span-2">
              <Label>Layer</Label>
              {providerLayers.length ? (
                <Select value={layerReference} onValueChange={setLayerReference}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select GIS layer" />
                  </SelectTrigger>
                  <SelectContent>
                    {providerLayers.map((layer) => (
                      <SelectItem
                        key={`${layer.provider}:${layer.layerReference}`}
                        value={layer.layerReference}
                      >
                        {layer.title} ({layer.layerReference})
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              ) : (
                <Input
                  value={layerReference}
                  onChange={(event) => setLayerReference(event.target.value)}
                  placeholder={
                    provider === 'ArcGIS'
                      ? 'Numeric ArcGIS layer ID'
                      : 'workspace:layer'
                  }
                />
              )}
            </div>

            <div className="space-y-2 sm:col-span-2">
              <Label htmlFor="gis-feature-id">
                {provider === 'ArcGIS' ? 'Object ID' : 'Feature ID'}
              </Label>
              <Input
                id="gis-feature-id"
                value={featureId}
                onChange={(event) => setFeatureId(event.target.value)}
              />
            </div>
          </div>
        )}

        <DialogFooter className="gap-2 sm:justify-between">
          <div>
            {asset?.gisFeatureId && isConfigured ? (
              <Button
                type="button"
                variant="destructive"
                onClick={() => void unlink()}
                disabled={isSaving}
              >
                <Unlink className="mr-2 h-4 w-4" />
                Unlink
              </Button>
            ) : null}
          </div>
          <div className="flex gap-2">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            {isConfigured ? (
              <Button
                type="button"
                onClick={() => void save()}
                disabled={
                  isSaving ||
                  !provider ||
                  !layerReference.trim() ||
                  !featureId.trim() ||
                  !sourceCrs.trim()
                }
              >
                {isSaving ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Link2 className="mr-2 h-4 w-4" />
                )}
                Link
              </Button>
            ) : null}
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
