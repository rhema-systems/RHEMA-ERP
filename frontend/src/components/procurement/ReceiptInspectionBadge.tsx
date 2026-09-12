import React from 'react';
import { AlertCircle, CheckCircle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';

type ReceiptInspectionBadgeProps = {
  requiresInspection: boolean;
  inspectionDate?: string | null;
  inspectionResult?: string | null;
  className?: string;
};

export function ReceiptInspectionBadge({ requiresInspection, inspectionDate, inspectionResult, className = '' }: ReceiptInspectionBadgeProps) {
  if (!requiresInspection) return null;

  // This flag records the requirement, not whether the inspection is still outstanding.
  // Use the saved completion evidence, as on the receipt details page.
  const result = inspectionResult?.trim().toLowerCase();
  const completed = Boolean(inspectionDate && result && result !== 'pending');
  const Icon = completed ? CheckCircle : AlertCircle;

  return (
    <Badge variant="outline" className={`${completed
      ? 'border-green-200 bg-green-50 text-green-800'
      : 'border-amber-200 bg-amber-50 text-amber-800'} ${className}`}>
      <Icon className="h-3 w-3 mr-1" aria-hidden="true" />
      {completed ? 'Inspection Complete' : 'Inspection Required'}
    </Badge>
  );
}
