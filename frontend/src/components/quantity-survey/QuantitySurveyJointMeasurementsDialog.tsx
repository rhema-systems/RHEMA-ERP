'use client';

import { Ruler } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { QuantitySurveyJointMeasurementWorkspace } from '@/components/quantity-survey/QuantitySurveyJointMeasurementWorkspace';
import { useAuth } from '@/hooks/use-auth';

export function QuantitySurveyJointMeasurementsDialog({ projectId }: { projectId: string }) {
  const { hasPermission } = useAuth();
  if (!hasPermission('quantity-survey.workspace.read')) return null;

  return (
    <Dialog>
      <DialogTrigger asChild>
        <Button variant="outline" className="gap-2">
          <Ruler className="h-4 w-4" />
          Joint measurements
        </Button>
      </DialogTrigger>
      <DialogContent className="max-h-[94vh] max-w-[96vw] overflow-y-auto xl:max-w-[1500px]">
        <DialogHeader>
          <DialogTitle>Joint measurements and remeasurement requests</DialogTitle>
        </DialogHeader>
        <QuantitySurveyJointMeasurementWorkspace projectId={projectId} />
      </DialogContent>
    </Dialog>
  );
}
