'use client';

import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import { toast } from 'sonner';

import { SupplierConsolidationForm } from '../../SupplierConsolidationForm';
import {
  supplierConsolidationService,
  type SupplierConsolidationDetailDto,
} from '@/services/procurementPlanningService';

export default function EditSupplierConsolidationPage() {
  const params = useParams<{ id: string }>();
  const [consolidation, setConsolidation] = useState<SupplierConsolidationDetailDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    supplierConsolidationService.getConsolidationById(params.id)
      .then(setConsolidation)
      .catch((error) => {
        console.error('Error loading supplier consolidation:', error);
        toast.error('Failed to load supplier consolidation');
      })
      .finally(() => setLoading(false));
  }, [params.id]);

  if (loading) {
    return <div className="py-12 text-center text-muted-foreground">Loading supplier consolidation...</div>;
  }

  if (!consolidation) {
    return <div className="py-12 text-center text-muted-foreground">Supplier consolidation not found.</div>;
  }

  return <SupplierConsolidationForm mode="edit" initialValue={consolidation} />;
}
