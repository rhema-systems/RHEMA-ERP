'use client';

import React from 'react';
import dynamic from 'next/dynamic';
import { Copy, Edit3, Loader2, Plus, Save, Trash2, X } from 'lucide-react';
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
import { Textarea } from '@/components/ui/textarea';
import {
  estateLandManagementService,
  type EstateLandDemarcation,
  type EstateManagedAsset,
  type SaveEstateLandDemarcation,
} from '@/services/estate-land-management.service';

const LandBankMap = dynamic(() => import('./LandBankMap'), { ssr: false });

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

type PendingDemarcation = SaveEstateLandDemarcation & {
  id: string;
};

const emptyBeacons = (): Beacon[] =>
  Array.from({ length: 4 }, (_, index) => ({
    beacon: `Beacon ${index + 1}`,
    northing: '',
    easting: '',
    bearing: '',
    distance: '',
  }));

function parseBoundary(value?: string): Beacon[] {
  if (!value?.trim()) return emptyBeacons();

  try {
    const parsed = JSON.parse(value);
    if (!Array.isArray(parsed)) return emptyBeacons();

    const points = parsed
      .map((item, index): Beacon | null => {
        const northing = Number(
          Array.isArray(item)
            ? item[0]
            : item?.northing ??
                item?.Northing ??
                item?.northingFeet ??
                item?.NorthingFeet
        );
        const easting = Number(
          Array.isArray(item)
            ? item[1]
            : item?.easting ??
                item?.Easting ??
                item?.eastingFeet ??
                item?.EastingFeet
        );
        if (!Number.isFinite(northing) || !Number.isFinite(easting)) return null;

        const distance = Number(
          item?.distance ??
            item?.Distance ??
            item?.distanceFeet ??
            item?.DistanceFeet
        );
        return {
          beacon: `${item?.beacon ?? item?.Beacon ?? item?.beaconIndex ?? item?.BeaconIndex ?? `Beacon ${index + 1}`}`,
          northing: `${northing}`,
          easting: `${easting}`,
          bearing: `${item?.bearing ?? item?.Bearing ?? ''}`.trim(),
          distance: Number.isFinite(distance) ? `${distance}` : '',
        };
      })
      .filter((item): item is Beacon => Boolean(item));

    return points.length >= 3 ? points : emptyBeacons();
  } catch {
    return emptyBeacons();
  }
}

function serializeBoundary(beacons: Beacon[]) {
  const points = beacons
    .map((item): ParsedBeacon | null => {
      const northingText = item.northing.trim();
      const eastingText = item.easting.trim();
      if (!northingText || !eastingText) return null;

      const northing = Number(northingText);
      const easting = Number(eastingText);
      if (!Number.isFinite(northing) || !Number.isFinite(easting)) return null;

      const distanceText = item.distance.trim();
      const distance = distanceText ? Number(distanceText) : Number.NaN;
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

function formatArea(areaSquareFeet: number) {
  const acres = areaSquareFeet / 43560;
  return `${areaSquareFeet.toLocaleString(undefined, { maximumFractionDigits: 2 })} sq ft (${acres.toLocaleString(undefined, { maximumFractionDigits: 4 })} acres)`;
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
  onSaved: () => Promise<void> | void;
}) {
  const [demarcations, setDemarcations] = React.useState<
    EstateLandDemarcation[]
  >([]);
  const [pendingDemarcations, setPendingDemarcations] = React.useState<
    PendingDemarcation[]
  >([]);
  const [editingId, setEditingId] = React.useState<string | null>(null);
  const [description, setDescription] = React.useState('');
  const [beacons, setBeacons] = React.useState<Beacon[]>(emptyBeacons);
  const [boundaryVerified, setBoundaryVerified] = React.useState(false);
  const [loading, setLoading] = React.useState(false);
  const [saving, setSaving] = React.useState(false);
  const [deletingId, setDeletingId] = React.useState<string | null>(null);

  const resetEditor = React.useCallback(() => {
    setEditingId(null);
    setDescription('');
    setBeacons(emptyBeacons());
    setBoundaryVerified(false);
  }, []);

  const loadDemarcations = React.useCallback(async () => {
    if (!asset) return;
    setLoading(true);
    try {
      setDemarcations(
        await estateLandManagementService.getLandDemarcations(asset.id)
      );
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to load land demarcations.'
      );
    } finally {
      setLoading(false);
    }
  }, [asset]);

  React.useEffect(() => {
    if (!open || !asset) return;
    resetEditor();
    setPendingDemarcations([]);
    void loadDemarcations();
  }, [asset, loadDemarcations, open, resetEditor]);

  const boundaryCoordinates = React.useMemo(
    () => serializeBoundary(beacons),
    [beacons]
  );
  const isIncomplete =
    !description.trim() ||
    beacons.length < 3 ||
    beacons.some(
      (item) =>
        !item.beacon.trim() ||
        !item.northing.trim() ||
        !item.easting.trim()
    ) ||
    !boundaryCoordinates;
  const hasDraftValues =
    Boolean(description.trim()) ||
    beacons.some((item) =>
      [item.beacon, item.northing, item.easting, item.bearing, item.distance]
        .some((value) => value.trim())
    ) ||
    boundaryVerified;

  const mapDemarcations = React.useMemo(
    () => [
      ...demarcations
        .filter((item) => item.id !== editingId)
        .map((item) => ({
          id: item.id,
          description: `Parcel ${item.demarcationNumber}: ${item.description}`,
          boundaryCoordinates: item.boundaryCoordinates,
        })),
      ...pendingDemarcations.map((item, index) => ({
        id: item.id,
        description: `Unsaved Parcel ${demarcations.length + index + 1}: ${item.description}`,
        boundaryCoordinates: item.boundaryCoordinates,
        isDraft: true,
      })),
      ...(boundaryCoordinates
        ? [
            {
              id: 'draft',
              description: description.trim() || 'Unsaved demarcation',
              boundaryCoordinates,
              isDraft: true,
            },
          ]
        : []),
    ],
    [
      boundaryCoordinates,
      demarcations,
      description,
      editingId,
      pendingDemarcations,
    ]
  );

  const editDemarcation = (demarcation: EstateLandDemarcation) => {
    if (demarcation.isAssignedToProject) {
      toast.error(
        'This demarcation is assigned to a project. Reassign the project land before editing it.'
      );
      return;
    }

    setEditingId(demarcation.id);
    setDescription(demarcation.description);
    setBeacons(parseBoundary(demarcation.boundaryCoordinates));
    setBoundaryVerified(demarcation.boundaryVerified);
  };

  const addPendingDemarcation = () => {
    if (editingId) {
      toast.error('Finish or cancel the saved demarcation edit first.');
      return;
    }

    if (isIncomplete) {
      toast.error('Enter a description and at least three complete beacons.');
      return;
    }

    setPendingDemarcations((current) => [
      ...current,
      {
        id: `pending-${Date.now()}-${Math.random().toString(36).slice(2)}`,
        description: description.trim(),
        beaconCount: beacons.length,
        boundaryCoordinates,
        boundaryVerified,
      },
    ]);
    resetEditor();
  };

  const editPendingDemarcation = (item: PendingDemarcation) => {
    setPendingDemarcations((current) =>
      current.filter((pending) => pending.id !== item.id)
    );
    setEditingId(null);
    setDescription(item.description);
    setBeacons(parseBoundary(item.boundaryCoordinates));
    setBoundaryVerified(item.boundaryVerified);
  };

  const removePendingDemarcation = (id: string) => {
    setPendingDemarcations((current) =>
      current.filter((pending) => pending.id !== id)
    );
  };

  const useWholeParcel = () => {
    if (
      !asset?.boundaryVerified ||
      !asset.boundaryCoordinates?.trim()
    ) {
      toast.error(
        'The main cadastral boundary must be recorded and verified first.'
      );
      return;
    }

    if (demarcations.length || pendingDemarcations.length) {
      toast.error(
        'Whole Parcel can only be used when no other demarcations exist.'
      );
      return;
    }

    const parentBeacons = parseBoundary(asset.boundaryCoordinates);
    if (!serializeBoundary(parentBeacons)) {
      toast.error('The main cadastral boundary coordinates are invalid.');
      return;
    }

    setEditingId(null);
    setDescription('Whole parcel');
    setBeacons(parentBeacons);
    setBoundaryVerified(true);
    toast.success('The complete parent boundary is ready to save as one parcel.');
  };

  const save = async () => {
    if (!asset) {
      return;
    }

    const currentPayload: SaveEstateLandDemarcation | null = isIncomplete
      ? null
      : {
          description: description.trim(),
          beaconCount: beacons.length,
          boundaryCoordinates,
          boundaryVerified,
        };
    if (editingId && !currentPayload) {
      toast.error('Enter a description and at least three complete beacons.');
      return;
    }
    if (!editingId && pendingDemarcations.length === 0 && !currentPayload) {
      toast.error('Add at least one demarcation before saving.');
      return;
    }

    let savedCount = 0;
    try {
      setSaving(true);
      if (editingId) {
        if (!currentPayload) return;
        await estateLandManagementService.updateLandDemarcation(
          asset.id,
          editingId,
          currentPayload
        );
        toast.success('Demarcation updated.');
      } else {
        const drafts: Array<{
          pendingId?: string;
          payload: SaveEstateLandDemarcation;
        }> = [
          ...pendingDemarcations.map(({ id, ...payload }) => ({
            pendingId: id,
            payload,
          })),
          ...(currentPayload ? [{ payload: currentPayload }] : []),
        ];
        for (const draft of drafts) {
          await estateLandManagementService.createLandDemarcation(
            asset.id,
            draft.payload
          );
          savedCount += 1;
          if (draft.pendingId) {
            setPendingDemarcations((current) =>
              current.filter((pending) => pending.id !== draft.pendingId)
            );
          } else {
            resetEditor();
          }
        }
        toast.success(
          drafts.length === 1
            ? 'Demarcation added within the main cadastral boundary.'
            : `${drafts.length} demarcations added within the main cadastral boundary.`
        );
        setPendingDemarcations([]);
      }
      resetEditor();
      await loadDemarcations();
      await onSaved();
    } catch (error) {
      await loadDemarcations();
      const message =
        error instanceof Error ? error.message : 'Unable to save demarcation.';
      toast.error(
        savedCount > 0
          ? `${savedCount} demarcation${savedCount === 1 ? '' : 's'} saved. Remaining drafts were preserved. ${message}`
          : message
      );
    } finally {
      setSaving(false);
    }
  };

  const remove = async (demarcation: EstateLandDemarcation) => {
    if (demarcation.isAssignedToProject) {
      toast.error(
        'This demarcation is assigned to a project. Reassign the project land before deleting it.'
      );
      return;
    }

    if (
      !asset ||
      !window.confirm(
        `Delete Parcel ${demarcation.demarcationNumber}: ${demarcation.description}?`
      )
    ) {
      return;
    }

    try {
      setDeletingId(demarcation.id);
      await estateLandManagementService.deleteLandDemarcation(
        asset.id,
        demarcation.id
      );
      if (editingId === demarcation.id) resetEditor();
      await loadDemarcations();
      await onSaved();
      toast.success('Demarcation deleted.');
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Unable to delete demarcation.'
      );
    } finally {
      setDeletingId(null);
    }
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => !saving && !deletingId && onOpenChange(next)}
    >
      <DialogContent className="max-h-[92vh] max-w-6xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Demarcations - {asset?.assetCode}</DialogTitle>
        </DialogHeader>

        <div className="grid gap-3 sm:grid-cols-3">
          <div className="rounded-md border p-3">
            <p className="text-xs text-muted-foreground">Main cadastral</p>
            <p className="mt-1 text-sm font-medium">
              {asset?.cadastreDescription || asset?.name}
            </p>
          </div>
          <div className="rounded-md border p-3">
            <p className="text-xs text-muted-foreground">Survey plan</p>
            <p className="mt-1 text-sm font-medium">
              {asset?.surveyPlanNumber || 'Not recorded'}
            </p>
          </div>
          <div className="rounded-md border p-3">
            <p className="text-xs text-muted-foreground">Demarcations</p>
            <p className="mt-1 text-sm font-medium">{demarcations.length}</p>
          </div>
        </div>

        <div className="overflow-hidden rounded-md border">
          <div className="flex items-center justify-between gap-3 border-b px-4 py-3">
            <p className="text-sm font-semibold">Defined parcels</p>
            <div className="flex flex-wrap justify-end gap-2">
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={
                  saving ||
                  deletingId !== null ||
                  !asset?.boundaryVerified ||
                  !asset.boundaryCoordinates?.trim() ||
                  demarcations.length > 0 ||
                  pendingDemarcations.length > 0
                }
                title={
                  demarcations.length || pendingDemarcations.length
                    ? 'Whole Parcel cannot overlap another demarcation.'
                    : 'Copy the complete verified parent boundary into one demarcation.'
                }
                onClick={useWholeParcel}
              >
                <Copy className="mr-2 h-4 w-4" />
                Use Whole Parcel
              </Button>
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={resetEditor}
              >
                <Plus className="mr-2 h-4 w-4" />
                New Demarcation
              </Button>
            </div>
          </div>
          {loading ? (
            <div className="flex items-center justify-center gap-2 py-8 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              Loading demarcations
            </div>
          ) : demarcations.length || pendingDemarcations.length ? (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[720px] text-sm">
                <thead className="bg-muted/60 text-left text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="px-4 py-3 font-medium">Parcel</th>
                    <th className="px-4 py-3 font-medium">Description</th>
                    <th className="px-4 py-3 font-medium">Area</th>
                    <th className="px-4 py-3 font-medium">Beacons</th>
                    <th className="px-4 py-3 font-medium">Status</th>
                    <th className="px-4 py-3 font-medium" aria-label="Actions" />
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {demarcations.map((demarcation) => (
                    <tr key={demarcation.id}>
                      <td className="px-4 py-3 font-medium">
                        {demarcation.demarcationNumber}
                      </td>
                      <td className="max-w-xs px-4 py-3">
                        {demarcation.description}
                      </td>
                      <td className="px-4 py-3 tabular-nums">
                        {formatArea(demarcation.areaSquareFeet)}
                      </td>
                      <td className="px-4 py-3 tabular-nums">
                        {demarcation.beaconCount}
                      </td>
                      <td className="px-4 py-3">
                        {demarcation.isAssignedToProject
                          ? 'Assigned to project'
                          : demarcation.boundaryVerified
                            ? 'Verified'
                            : 'Draft'}
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex justify-end gap-1">
                          <Button
                            type="button"
                            size="icon"
                            variant="ghost"
                            title={
                              demarcation.isAssignedToProject
                                ? 'Assigned demarcations cannot be edited.'
                                : 'Edit demarcation'
                            }
                            disabled={demarcation.isAssignedToProject}
                            onClick={() => editDemarcation(demarcation)}
                          >
                            <Edit3 className="h-4 w-4" />
                          </Button>
                          <Button
                            type="button"
                            size="icon"
                            variant="ghost"
                            title={
                              demarcation.isAssignedToProject
                                ? 'Assigned demarcations cannot be deleted.'
                                : 'Delete demarcation'
                            }
                            disabled={
                              demarcation.isAssignedToProject ||
                              deletingId === demarcation.id
                            }
                            onClick={() => void remove(demarcation)}
                          >
                            {deletingId === demarcation.id ? (
                              <Loader2 className="h-4 w-4 animate-spin" />
                            ) : (
                              <Trash2 className="h-4 w-4" />
                            )}
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))}
                  {pendingDemarcations.map((pending, index) => (
                    <tr key={pending.id}>
                      <td className="px-4 py-3 font-medium">
                        {demarcations.length + index + 1}
                      </td>
                      <td className="max-w-xs px-4 py-3">
                        {pending.description}
                      </td>
                      <td className="px-4 py-3 tabular-nums">Pending save</td>
                      <td className="px-4 py-3 tabular-nums">
                        {pending.beaconCount}
                      </td>
                      <td className="px-4 py-3">
                        {pending.boundaryVerified ? 'Verified' : 'Draft'}
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex justify-end gap-1">
                          <Button
                            type="button"
                            size="icon"
                            variant="ghost"
                            title="Edit pending demarcation"
                            onClick={() => editPendingDemarcation(pending)}
                          >
                            <Edit3 className="h-4 w-4" />
                          </Button>
                          <Button
                            type="button"
                            size="icon"
                            variant="ghost"
                            title="Remove pending demarcation"
                            onClick={() => removePendingDemarcation(pending.id)}
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="py-8 text-center text-sm text-muted-foreground">
              No demarcations have been added.
            </div>
          )}
        </div>

        <div className="space-y-2">
          <label htmlFor="demarcation-description" className="text-sm font-medium">
            Demarcation Description <span className="text-destructive">*</span>
          </label>
          <Textarea
            id="demarcation-description"
            value={description}
            maxLength={1000}
            rows={3}
            onChange={(event) => setDescription(event.target.value)}
          />
        </div>

        <div className="overflow-x-auto rounded-md border">
          <table className="w-full min-w-[850px] text-sm">
            <thead className="bg-muted">
              <tr>
                {[
                  'Beacon index',
                  'Northing (Y), ft',
                  'Easting (X), ft',
                  'Bearing',
                  'Distance, ft',
                  '',
                ].map((label) => (
                  <th key={label} className="px-3 py-2 text-left font-medium">
                    {label}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y">
              {beacons.map((beacon, index) => (
                <tr key={index}>
                  {(
                    [
                      'beacon',
                      'northing',
                      'easting',
                      'bearing',
                      'distance',
                    ] as const
                  ).map((key) => (
                    <td key={key} className="p-2">
                      <Input
                        type={
                          ['northing', 'easting', 'distance'].includes(key)
                            ? 'number'
                            : 'text'
                        }
                        value={beacon[key]}
                        onChange={(event) =>
                          setBeacons((current) =>
                            current.map((item, row) =>
                              row === index
                                ? { ...item, [key]: event.target.value }
                                : item
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
                      title="Remove beacon"
                      disabled={beacons.length <= 3}
                      onClick={() =>
                        setBeacons((current) =>
                          current.filter((_, row) => row !== index)
                        )
                      }
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-wrap gap-2">
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
            <Button
              type="button"
              variant="outline"
              disabled={saving || editingId !== null || isIncomplete}
              onClick={addPendingDemarcation}
            >
              <Plus className="mr-2 h-4 w-4" />
              Add Demarcation
            </Button>
          </div>
          <label className="flex items-center gap-2 text-sm">
            <Checkbox
              checked={boundaryVerified}
              onCheckedChange={(checked) =>
                setBoundaryVerified(checked === true)
              }
            />
            Demarcation boundary verified
          </label>
        </div>

        <LandBankMap
          boundaryCoordinates={asset?.boundaryCoordinates}
          demarcations={mapDemarcations}
          forceSurvey
          showBeaconSchedule={false}
        />

        <DialogFooter>
          {editingId ? (
            <Button variant="outline" onClick={resetEditor} disabled={saving}>
              <X className="mr-2 h-4 w-4" />
              Cancel Edit
            </Button>
          ) : null}
          <Button
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={saving}
          >
            Close
          </Button>
          <Button
            onClick={() => void save()}
            disabled={
              saving ||
              (editingId
                ? isIncomplete
                : pendingDemarcations.length === 0 &&
                  (isIncomplete || !hasDraftValues))
            }
          >
            {saving ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-2 h-4 w-4" />
            )}
            {editingId
              ? 'Update Demarcation'
              : pendingDemarcations.length > 1
                ? 'Save Demarcations'
                : 'Save Demarcation'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
