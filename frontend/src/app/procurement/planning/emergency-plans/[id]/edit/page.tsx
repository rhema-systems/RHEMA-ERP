'use client';

import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import { toast } from 'sonner';

import { EmergencyPlanForm } from '../../EmergencyPlanForm';
import {
  emergencyProcurementPlanService,
  type EmergencyProcurementPlanDetailDto,
} from '@/services/procurementPlanningService';

export default function EditEmergencyPlanPage() {
  const params = useParams<{ id: string }>();
  const [plan, setPlan] = useState<EmergencyProcurementPlanDetailDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const loadPlan = async () => {
      try {
        setLoading(true);
        const data = await emergencyProcurementPlanService.getPlanById(params.id);
        setPlan(data);
      } catch (error) {
        console.error('Error loading emergency plan:', error);
        toast.error('Failed to load emergency plan');
      } finally {
        setLoading(false);
      }
    };

    loadPlan();
  }, [params.id]);

  if (loading) {
    return <div className="p-6 text-muted-foreground">Loading emergency plan...</div>;
  }

  if (!plan) {
    return <div className="p-6 text-muted-foreground">Emergency plan not found</div>;
  }

  return <EmergencyPlanForm mode="edit" initialValue={plan} />;
}
