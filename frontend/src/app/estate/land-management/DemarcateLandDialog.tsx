'use client';

import React from 'react';
import dynamic from 'next/dynamic';
import { Loader2, Plus, Save, Trash2 } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
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
  estateLandManagementService,
  type EstateManagedAsset,
  type UpdateEstateManagedLandDemarcation,
} from '@/services/estate-land-management.service';

const LandBankMap = dynamic(() => import('./LandBankMap'), { ssr: false });

type FormState = Record<string, string>;
type Beacon = {
  beacon: string;
  northing: string;
  easting: string;
  bearing: string;
  distance: string;
};
type ParsedBeacon = {
  beacon: string;
  northing: number;
  easting: number;
  bearing: string;
  distance?: number;
};

const areaUnits = ['sq ft', 'acres', 'hectares', 'sqm'];

function RequiredLabel({ children }: { children: React.ReactNode }) {
  return (
    <Label>
      {children} <span className="text-destructive">*</span>
    </Label>
  );
}

function formatDateInput(value?: string) {
  if (!value) return '';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '' : date.toISOString().slice(0, 10);
}

function initialBeacons(asset?: EstateManagedAsset): Beacon[] {
  const parsed = parseBoundary(asset?.boundaryCoordinates);
  if (parsed.length) {
    return parsed.map((point) => ({
      beacon: point.beacon,
      northing: `${point.northing}`,
      easting: `${point.easting}`,
      bearing: point.bearing,
      distance: point.distance == null ? '' : `${point.distance}`,
    }));
  }

  return Array.from({ length: 4 }, (_, index) => ({
    beacon: `Beacon ${index + 1}`,
    northing: '',
    easting: '',
    bearing: '',
    distance: '',
  }));
}

function parseBoundary(value?: string): ParsedBeacon[] {
  if (!value?.trim()) return [];

  try {
    const parsed = JSON.parse(value);
    if (!Array.isArray(parsed)) return [];

    return parsed
      .map((item, index): ParsedBeacon | null => {
        const northing = Number(
          Array.isArray(item)
            ? item[0]
            : item?.northing ?? item?.Northing ?? item?.northingFeet ?? item?.NorthingFeet
        );
        const easting = Number(
          Array.isArray(item)
            ? item[1]
            : item?.easting ?? item?.Easting ?? item?.eastingFeet ?? item?.EastingFeet
        );
        if (!Number.isFinite(northing) || !Number.isFinite(easting)) return null;

        const distance = Number(item?.distance ?? item?.Distance ?? item?.distanceFeet ?? item?.DistanceFeet);
        return {
          beacon: `${item?.beacon ?? item?.Beacon ?? item?.beaconIndex ?? item?.BeaconIndex ?? `Beacon ${index + 1}`}`,
          northing,
          easting,
          bearing: `${item?.bearing ?? item?.Bearing ?? ''}`.trim(),
          distance: Number.isFinite(distance) ? distance : undefined,
        };
      })
      .filter((item): item is ParsedBeacon => Boolean(item));
  } catch {
    return [];
  }
}

function boundaryCoordinatesFromBeacons(beacons: Beacon[]) {
  const points = beacons
    .map((item): ParsedBeacon | null => {
      const northing = Number(item.northing);
      const easting = Number(item.easting);
      if (!Number.isFinite(northing) || !Number.isFinite(easting)) return null;

      const distance = Number(item.distance);
      return {
        beacon: item.beacon.trim() || 'Beacon',
        northing,
        easting,
        bearing: item.bearing.trim(),
        distance: Number.isFinite(distance) ? distance : undefined,
      };
    })
    .filter((item): item is ParsedBeacon => Boolean(item));

  return points.length >= 3 ? JSON.stringify(points) : '';
}

function buildForm(asset: EstateManagedAsset): FormState {
  return {
    cadastreDescription: asset.cadastreDescription || '',
    region: asset.region || '',
    district: asset.district || '',
    town: asset.town || '',
    areaValue: asset.areaValue == null ? '' : `${asset.areaValue}`,
    areaUnit: asset.areaUnit || 'sq ft',
    areaSquareMeters: asset.areaSquareMeters == null ? '' : `${asset.areaSquareMeters}`,
    surveyorName: asset.surveyorName || '',
    surveyDate: formatDateInput(asset.surveyDate),
    surveyPlanNumber: asset.surveyPlanNumber || '',
    mapSheetNumber: asset.mapSheetNumber || '',
  };
}

export default function DemarcateLandDialog({
  asset,
  open,
  onOpenChange,
  onSaved,
}: {
  asset: EstateManagedAsset | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: (asset: EstateManagedAsset) => Promise<void> | void;
}) {
  const [form, setForm] = React.useState<FormState>(() =>
    asset ? buildForm(asset) : {}
  );
  const [beacons, setBeacons] = React.useState<Beacon[]>(() =>
    initialBeacons(asset || undefined)
  );
  const [boundaryVerified, setBoundaryVerified] = React.useState(false);
  const [ready, setReady] = React.useState(false);
  const [saving, setSaving] = React.useState(false);

  React.useEffect(() => {
    if (!asset || !open) return;
    setForm(buildForm(asset));
    setBeacons(initialBeacons(asset));
    setBoundaryVerified(asset.boundaryVerified);
    setReady(asset.isReadyForProjectManagement);
  }, [asset, open]);

  const boundaryCoordinates = React.useMemo(
    () => boundaryCoordinatesFromBeacons(beacons),
    [beacons]
  );

  const missing = React.useMemo(() => {
    const requiredKeys = [
      'cadastreDescription',
      'region',
      'district',
      'town',
      'areaValue',
      'areaUnit',
      'surveyorName',
      'surveyPlanNumber',
      'mapSheetNumber',
    ];
    return (
      requiredKeys.some((key) => !form[key]?.trim()) ||
      beacons.length < 3 ||
      beacons.some((item) => !item.beacon.trim() || !item.northing.trim() || !item.easting.trim()) ||
      !boundaryCoordinates
    );
  }, [beacons, boundaryCoordinates, form]);

  const setValue = (key: string, value: string) =>
    setForm((current) => ({ ...current, [key]: value }));

  const save = async () => {
    if (!asset) return;
    if (missing) {
      toast.error('Complete the cadastral and beacon fields before saving.');
      return;
    }
    if (ready && !boundaryVerified) {
      toast.error('Verify the boundary before project handoff.');
      return;
    }

    const payload: UpdateEstateManagedLandDemarcation = {
      cadastreDescription: form.cadastreDescription,
      region: form.region,
      district: form.district,
      town: form.town,
      areaValue: Number(form.areaValue),
      areaUnit: form.areaUnit,
      areaSquareMeters: form.areaSquareMeters ? Number(form.areaSquareMeters) : undefined,
      surveyorName: form.surveyorName,
      surveyDate: form.surveyDate || undefined,
      surveyPlanNumber: form.surveyPlanNumber,
      mapSheetNumber: form.mapSheetNumber,
      beaconCount: beacons.length,
      boundaryCoordinates,
      boundaryVerified,
      isReadyForProjectManagement: ready,
    };

    try {
      setSaving(true);
      const updated = await estateLandManagementService.updateLandDemarcation(
        asset.id,
        payload
      );
      toast.success('Land demarcation updated.');
      await onSaved(updated);
      onOpenChange(false);
    } catch (error: any) {
      toast.error(error?.message || 'Unable to update land demarcation.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(next) => !saving && onOpenChange(next)}>
      <DialogContent className="max-h-[92vh] max-w-6xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>
            {asset?.boundaryCoordinates ? 'Re-demarcate Land' : 'Demarcate Land'}
          </DialogTitle>
        </DialogHeader>

        <div className="grid gap-4 md:grid-cols-3">
          {[
            ['cadastreDescription', 'Cadastre Description'],
            ['region', 'Region'],
            ['district', 'District'],
            ['town', 'Town'],
            ['areaValue', 'Survey Area'],
            ['areaSquareMeters', 'Area (sqm)'],
            ['surveyorName', 'Surveyor Name'],
            ['surveyDate', 'Survey Date'],
            ['surveyPlanNumber', 'Survey Plan Number'],
            ['mapSheetNumber', 'Map Sheet Number'],
          ].map(([key, label]) => (
            <div key={key} className="space-y-2">
              <RequiredLabel>{label}</RequiredLabel>
              <Input
                type={
                  key === 'surveyDate'
                    ? 'date'
                    : ['areaValue', 'areaSquareMeters'].includes(key)
                      ? 'number'
                      : 'text'
                }
                value={form[key] || ''}
                onChange={(event) => setValue(key, event.target.value)}
              />
            </div>
          ))}
          <div className="space-y-2">
            <RequiredLabel>Survey Area Unit</RequiredLabel>
            <Select
              value={form.areaUnit || 'sq ft'}
              onValueChange={(value) => setValue('areaUnit', value)}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {areaUnits.map((unit) => (
                  <SelectItem key={unit} value={unit}>
                    {unit}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>

        <div className="overflow-x-auto rounded-md border">
          <table className="w-full min-w-[850px] text-sm">
            <thead className="bg-muted">
              <tr>
                {['Beacon index', 'Northing (Y), ft', 'Easting (X), ft', 'Bearing', 'Distance, ft', ''].map((label) => (
                  <th key={label} className="px-3 py-2 text-left font-medium">
                    {label}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y">
              {beacons.map((beacon, index) => (
                <tr key={index}>
                  {(['beacon', 'northing', 'easting', 'bearing', 'distance'] as const).map((key) => (
                    <td key={key} className="p-2">
                      <Input
                        type={['northing', 'easting', 'distance'].includes(key) ? 'number' : 'text'}
                        value={beacon[key]}
                        onChange={(event) =>
                          setBeacons((current) =>
                            current.map((item, row) =>
                              row === index ? { ...item, [key]: event.target.value } : item
                            )
                          )
                        }
                      />
                    </td>
                  ))}
                  <td className="p-2">
                    <Button
                      type="button"
                      size="icon"
                      variant="ghost"
                      disabled={beacons.length <= 3}
                      onClick={() => setBeacons((current) => current.filter((_, row) => row !== index))}
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <Button
          type="button"
          variant="outline"
          onClick={() =>
            setBeacons((current) => [
              ...current,
              {
                beacon: `Beacon ${current.length + 1}`,
                northing: '',
                easting: '',
                bearing: '',
                distance: '',
              },
            ])
          }
        >
          <Plus className="mr-2 h-4 w-4" />
          Add Beacon
        </Button>

        <LandBankMap boundaryCoordinates={boundaryCoordinates || undefined} />

        <div className="flex flex-wrap gap-5">
          <label className="flex items-center gap-2 text-sm">
            <Checkbox
              checked={boundaryVerified}
              onCheckedChange={(checked) => setBoundaryVerified(checked === true)}
            />
            Boundary verified
          </label>
          <label className="flex items-center gap-2 text-sm">
            <Checkbox checked={ready} onCheckedChange={(checked) => setReady(checked === true)} />
            Mark whole land demarcated and ready for Project Management
          </label>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={save} disabled={saving || missing}>
            {saving ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-2 h-4 w-4" />
            )}
            Save Demarcation
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
