import type {
  CreateTenderDto,
  CreateTenderItemDto,
  UpdateTenderDto,
} from '@/services/tenderService';

export interface PersistedTenderFormData {
  title: string;
  description: string;
  tenderType: string;
  submissionDeadline: string;
  openingDate: string;
  estimatedValue: number | null;
  currency: string;
  minimumPerformanceRating: number | null;
  requiresPrequalification: boolean;
  allowPartialBids: boolean;
  priceWeightage: number;
  qualityWeightage: number;
  deliveryWeightage: number;
  experienceWeightage: number;
  evaluationCriteriaJson: string;
  notes: string;
  termsAndConditions: string;
  bidValidityPeriodDays?: number | null;
  documentRequirements: unknown[];
  requiresAcceptanceDeclaration: boolean;
  evaluationTemplateId: string | null;
  useQCBSEvaluation: boolean;
  technicalWeight: number;
  financialWeight: number;
  minimumTechnicalScore: number;
  items: CreateTenderItemDto[];
}

export function buildUpdateTenderDto(
  formData: PersistedTenderFormData
): UpdateTenderDto {
  return {
    title: formData.title,
    description: formData.description,
    submissionDeadline: formData.submissionDeadline || undefined,
    openingDate: formData.openingDate || undefined,
    estimatedValue: formData.estimatedValue ?? undefined,
    currency: formData.currency,
    minimumPerformanceRating: formData.minimumPerformanceRating ?? undefined,
    requiresPrequalification: formData.requiresPrequalification,
    allowPartialBids: formData.allowPartialBids,
    priceWeightage: formData.priceWeightage,
    qualityWeightage: formData.qualityWeightage,
    deliveryWeightage: formData.deliveryWeightage,
    experienceWeightage: formData.experienceWeightage,
    evaluationCriteriaJson: formData.evaluationCriteriaJson || undefined,
    notes: formData.notes || undefined,
    termsAndConditions: formData.termsAndConditions || undefined,
    bidValidityPeriodDays: formData.bidValidityPeriodDays ?? null,
    requiredDocuments:
      formData.documentRequirements.length > 0
        ? JSON.stringify(formData.documentRequirements)
        : undefined,
    requiresAcceptanceDeclaration: formData.requiresAcceptanceDeclaration,
    evaluationTemplateId: formData.evaluationTemplateId ?? undefined,
    useQCBSEvaluation: formData.useQCBSEvaluation,
    technicalWeight: formData.technicalWeight,
    financialWeight: formData.financialWeight,
    minimumTechnicalScore: formData.minimumTechnicalScore,
  };
}

export function buildCreateTenderDto(
  formData: PersistedTenderFormData,
  sourcePurchaseRequisitionId: string,
  includeFormItems: boolean
): CreateTenderDto {
  return {
    sourcePurchaseRequisitionId,
    tenderType: formData.tenderType,
    ...buildUpdateTenderDto(formData),
    items: includeFormItems ? formData.items : [],
  };
}
