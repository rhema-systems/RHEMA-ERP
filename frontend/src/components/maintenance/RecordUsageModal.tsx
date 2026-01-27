'use client';

import React, { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { useToast } from '@/hooks/use-toast';
import { assetUsageTrackingService, CreateAssetUsageTrackingRequest } from '@/services/assetUsageTrackingService';
import { Loader2 } from 'lucide-react';

interface RecordUsageModalProps {
  assetId: string;
  assetName: string;
  assetNumber: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess?: () => void;
}

export function RecordUsageModal({
  assetId,
  assetName,
  assetNumber,
  open,
  onOpenChange,
  onSuccess
}: RecordUsageModalProps) {
  const { toast } = useToast();
  const [loading, setLoading] = useState(false);
  const [recordedAt, setRecordedAt] = useState<string>(
    new Date().toISOString().slice(0, 16)
  );
  const [mileage, setMileage] = useState<string>('');
  const [operatingHours, setOperatingHours] = useState<string>('');
  const [cycles, setCycles] = useState<string>('');
  const [fuelConsumed, setFuelConsumed] = useState<string>('');
  const [notes, setNotes] = useState<string>('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    // Validate that at least one metric is provided
    if (!mileage && !operatingHours && !cycles && !fuelConsumed) {
      toast({
        title: 'Validation Error',
        description: 'Please enter at least one usage metric',
        variant: 'destructive'
      });
      return;
    }

    try {
      setLoading(true);

      const request: CreateAssetUsageTrackingRequest = {
        assetId,
        recordedAt: new Date(recordedAt).toISOString(),
        mileage: mileage ? parseFloat(mileage) : undefined,
        mileageUnit: mileage ? 'km' : undefined,
        operatingHours: operatingHours ? parseFloat(operatingHours) : undefined,
        cycles: cycles ? parseFloat(cycles) : undefined,
        fuelConsumed: fuelConsumed ? parseFloat(fuelConsumed) : undefined,
        fuelUnit: fuelConsumed ? 'liters' : undefined,
        dataSource: 'Manual Entry',
        notes: notes || undefined,
        isValidated: true
      };

      await assetUsageTrackingService.createUsageRecord(request);

      toast({
        title: 'Success',
        description: 'Usage record created successfully',
      });

      // Reset form
      setMileage('');
      setOperatingHours('');
      setCycles('');
      setFuelConsumed('');
      setNotes('');
      setRecordedAt(new Date().toISOString().slice(0, 16));

      onOpenChange(false);
      onSuccess?.();
    } catch (error) {
      console.error('Error creating usage record:', error);
      toast({
        title: 'Error',
        description: 'Failed to create usage record',
        variant: 'destructive'
      });
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Record Asset Usage</DialogTitle>
          <div className="mt-2">
            <p className="font-semibold text-foreground">{assetName}</p>
            <p className="text-sm text-muted-foreground">{assetNumber}</p>
          </div>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-4">
          {/* Date/Time */}
          <div>
            <Label htmlFor="recordedAt" className="text-sm">
              Recorded Date & Time
            </Label>
            <Input
              id="recordedAt"
              type="datetime-local"
              value={recordedAt}
              onChange={(e) => setRecordedAt(e.target.value)}
              required
              className="mt-1"
            />
            <p className="text-xs text-muted-foreground mt-1">
              When was this reading taken?
            </p>
          </div>

          {/* Mileage */}
          <div>
            <Label htmlFor="mileage" className="text-sm">
              Mileage (km)
            </Label>
            <Input
              id="mileage"
              type="number"
              placeholder="0"
              value={mileage}
              onChange={(e) => setMileage(e.target.value)}
              step="0.01"
              min="0"
              className="mt-1"
            />
            <p className="text-xs text-muted-foreground mt-1">
              Total kilometers traveled
            </p>
          </div>

          {/* Operating Hours */}
          <div>
            <Label htmlFor="operatingHours" className="text-sm">
              Operating Hours (hrs)
            </Label>
            <Input
              id="operatingHours"
              type="number"
              placeholder="0"
              value={operatingHours}
              onChange={(e) => setOperatingHours(e.target.value)}
              step="0.1"
              min="0"
              className="mt-1"
            />
            <p className="text-xs text-muted-foreground mt-1">
              Total hours of operation
            </p>
          </div>

          {/* Cycles */}
          <div>
            <Label htmlFor="cycles" className="text-sm">
              Cycles
            </Label>
            <Input
              id="cycles"
              type="number"
              placeholder="0"
              value={cycles}
              onChange={(e) => setCycles(e.target.value)}
              step="1"
              min="0"
              className="mt-1"
            />
            <p className="text-xs text-muted-foreground mt-1">
              Number of operational cycles
            </p>
          </div>

          {/* Fuel Consumed */}
          <div>
            <Label htmlFor="fuelConsumed" className="text-sm">
              Fuel Consumed (liters)
            </Label>
            <Input
              id="fuelConsumed"
              type="number"
              placeholder="0"
              value={fuelConsumed}
              onChange={(e) => setFuelConsumed(e.target.value)}
              step="0.01"
              min="0"
              className="mt-1"
            />
            <p className="text-xs text-muted-foreground mt-1">
              Fuel/coolant consumed
            </p>
          </div>

          {/* Notes */}
          <div>
            <Label htmlFor="notes" className="text-sm">
              Notes (Optional)
            </Label>
            <Textarea
              id="notes"
              placeholder="Any observations or notes about the asset condition..."
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              className="mt-1"
              rows={3}
            />
          </div>

          {/* Validation message */}
          <p className="text-xs text-muted-foreground">
            * At least one metric must be provided
          </p>

          {/* Submit Button */}
          <div className="flex gap-2 justify-end">
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
              disabled={loading}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={loading}>
              {loading && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record Usage
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
