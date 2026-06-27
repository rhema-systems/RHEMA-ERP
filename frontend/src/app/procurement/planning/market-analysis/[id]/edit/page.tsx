'use client';

import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import { toast } from 'sonner';

import { MarketAnalysisForm } from '../../MarketAnalysisForm';
import { marketAnalysisService, type MarketAnalysisDetailDto } from '@/services/procurementPlanningService';

export default function EditMarketAnalysisPage() {
  const params = useParams<{ id: string }>();
  const [analysis, setAnalysis] = useState<MarketAnalysisDetailDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    marketAnalysisService.getAnalysisById(params.id)
      .then(setAnalysis)
      .catch((error) => {
        console.error('Error loading market analysis:', error);
        toast.error('Failed to load market analysis');
      })
      .finally(() => setLoading(false));
  }, [params.id]);

  if (loading) {
    return <div className="py-12 text-center text-muted-foreground">Loading market analysis...</div>;
  }

  if (!analysis) {
    return <div className="py-12 text-center text-muted-foreground">Market analysis not found.</div>;
  }

  return <MarketAnalysisForm mode="edit" initialValue={analysis} />;
}
