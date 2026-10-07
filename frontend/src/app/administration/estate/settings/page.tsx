'use client';

import Link from 'next/link';
import React from 'react';
import { ArrowLeft, Loader2, Save } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { estateSettingsService } from '@/services/estate-settings.service';

export default function EstatePlotSettingsPage() {
  const [squareMetersPerPlot, setSquareMetersPerPlot] = React.useState('');
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);

  React.useEffect(() => {
    let active = true;
    const load = async () => {
      try {
        const settings = await estateSettingsService.getPlotSettings();
        if (active) {
          setSquareMetersPerPlot(
            settings.squareMetersPerPlot == null
              ? ''
              : String(settings.squareMetersPerPlot)
          );
        }
      } catch (error) {
        toast.error(
          error instanceof Error
            ? error.message
            : 'Unable to load Estate plot setup.'
        );
      } finally {
        if (active) setIsLoading(false);
      }
    };
    void load();
    return () => {
      active = false;
    };
  }, []);

  const save = async () => {
    const trimmed = squareMetersPerPlot.trim();
    const parsed = trimmed ? Number(trimmed) : null;
    if (parsed != null && (!Number.isFinite(parsed) || parsed <= 0)) {
      toast.error('Enter a square-meter value greater than zero.');
      return;
    }

    setIsSaving(true);
    try {
      const saved = await estateSettingsService.savePlotSettings({
        squareMetersPerPlot: parsed,
      });
      setSquareMetersPerPlot(
        saved.squareMetersPerPlot == null
          ? ''
          : String(saved.squareMetersPerPlot)
      );
      toast.success('Estate plot setup saved.');
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to save Estate plot setup.'
      );
    } finally {
      setIsSaving(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex min-h-[360px] items-center justify-center">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const previewArea = 2000;
  const parsed = Number(squareMetersPerPlot);
  const previewPlots =
    Number.isFinite(parsed) && parsed > 0
      ? Math.round((previewArea / parsed + Number.EPSILON) * 100) / 100
      : null;

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="flex items-start gap-3">
          <Button asChild variant="ghost" size="icon" aria-label="Back to Estate Setup">
            <Link href="/administration/estate">
              <ArrowLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <p className="text-sm font-medium text-teal-700">Estate</p>
            <h1 className="text-2xl font-bold">Land Plot Setup</h1>
          </div>
        </div>
        <Button onClick={() => void save()} disabled={isSaving}>
          {isSaving ? (
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          ) : (
            <Save className="mr-2 h-4 w-4" />
          )}
          Save
        </Button>
      </div>

      <Card className="max-w-2xl">
        <CardHeader>
          <CardTitle className="text-base">Plot Conversion</CardTitle>
        </CardHeader>
        <CardContent className="space-y-5">
          <div className="space-y-2">
            <Label htmlFor="square-meters-per-plot">Square meters per plot</Label>
            <Input
              id="square-meters-per-plot"
              type="number"
              min="0.01"
              step="0.01"
              inputMode="decimal"
              value={squareMetersPerPlot}
              onChange={(event) => setSquareMetersPerPlot(event.target.value)}
              placeholder="Example: 1000"
            />
          </div>

          <div className="rounded-md border bg-muted/30 p-4 text-sm">
            <div className="text-muted-foreground">Portal listing preview</div>
            <div className="mt-1 font-medium">
              {previewPlots == null
                ? 'Set a value to calculate plot equivalents.'
                : `${previewArea.toLocaleString()} sqm = ${previewPlots.toLocaleString(undefined, {
                    maximumFractionDigits: 2,
                  })} plot${previewPlots === 1 ? '' : 's'}`}
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
