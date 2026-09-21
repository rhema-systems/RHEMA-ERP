import QuantitySurveyEscalationFormulaPage from '@/components/quantity-survey/QuantitySurveyEscalationFormulaPage';
import { isQsOptionalFeatureEnabled } from '@/lib/quantity-survey-architecture-scope';
import { notFound } from 'next/navigation';

export default function Page() {
  if (!isQsOptionalFeatureEnabled('escalation')) notFound();
  return <QuantitySurveyEscalationFormulaPage />;
}
