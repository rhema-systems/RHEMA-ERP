'use client';

import { useEffect, useState } from 'react';
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
  type ProjectReadyLandDemarcation,
} from '@/services/estate-land-management.service';

type ReadyLandPortionSelectProps = {
  value?: string;
  onValueChange: (value?: string) => void;
  projectId?: string;
  required?: boolean;
  label?: string;
};

const formatReadyLandPortionLabel = (
  portion: ProjectReadyLandDemarcation
) => {
  const isWholeParcel =
    portion.description.trim().toLowerCase() === 'whole parcel';
  const portionName = isWholeParcel
    ? 'Whole Parcel'
    : `Portion ${portion.demarcationNumber}: ${portion.description}`;
  const acres = portion.areaSquareFeet / 43560;
  const area =
    acres >= 0.01
      ? `${acres.toLocaleString(undefined, {
          maximumFractionDigits: 2,
        })} acres`
      : `${portion.areaSquareFeet.toLocaleString(undefined, {
          maximumFractionDigits: 0,
        })} sq ft`;
  const parent = [
    portion.assetCode,
    portion.assetName,
    portion.assetLocation,
  ]
    .filter(Boolean)
    .join(' - ');
  return `${portionName} - ${area} (${parent})${
    portion.isCurrentProjectSelection ? ' - Current selection' : ''
  }`;
};

export function ReadyLandPortionSelect({
  value,
  onValueChange,
  projectId,
  required = false,
  label = 'Demarcated Land',
}: ReadyLandPortionSelectProps) {
  const [portions, setPortions] = useState<ProjectReadyLandDemarcation[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string>();

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      setLoading(true);
      setLoadError(undefined);
      try {
        const available =
          await estateLandManagementService.getProjectReadyLandDemarcations(
            projectId
          );
        if (!cancelled) {
          setPortions(available);
        }
      } catch (error) {
        if (!cancelled) {
          setPortions([]);
          setLoadError(
            error instanceof Error
              ? error.message
              : 'Failed to load project-ready land.'
          );
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    void load();
    return () => {
      cancelled = true;
    };
  }, [projectId]);

  return (
    <div className="grid gap-2">
      <Label>
        {label}
        {required ? <span className="text-destructive"> *</span> : null}
      </Label>
      <Select
        value={value || 'none'}
        onValueChange={(nextValue) =>
          onValueChange(nextValue === 'none' ? undefined : nextValue)
        }
        disabled={loading}
      >
        <SelectTrigger>
          <SelectValue
            placeholder={loading ? 'Loading ready land...' : 'Select ready land'}
          />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="none">No land selected</SelectItem>
          {portions.map((portion) => (
            <SelectItem
              key={portion.demarcationId}
              value={portion.landReference}
            >
              {formatReadyLandPortionLabel(portion)}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      {loadError ? (
        <div className="text-xs text-destructive">{loadError}</div>
      ) : !loading && portions.length === 0 ? (
        <div className="text-xs text-muted-foreground">
          No verified, unused demarcated land is ready for project management.
        </div>
      ) : null}
    </div>
  );
}
