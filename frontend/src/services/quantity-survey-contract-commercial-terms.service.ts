import { apiService } from '@/services/api.service';

const root = '/quantity-survey/contract-commercial-terms';

export interface ContractCommercialTerms {
  contractId: string;
  projectId: string;
  contractNumber: string;
  contractTitle: string;
  contractStatus: string;
  contractValue: number;
  currency: string;
  paymentTermId?: string | null;
  provisionalSumAmount: number;
  contingencyAmount: number;
  retentionPercentage: number;
  defectsLiabilityDays?: number | null;
  retentionClause?: string | null;
  allowSectionalTakeover: boolean;
  sectionalTakeoverClause?: string | null;
  allowSubcontracting: boolean;
  subcontractPaymentTermId?: string | null;
  subcontractTerms?: string | null;
  claimNoticePeriodDays?: number | null;
  claimClause?: string | null;
  commercialTermsContractDocumentId?: string | null;
  configuredAt?: string | null;
  rowVersion: string;
}

export interface ContractCommercialTermsWorkspace {
  terms: ContractCommercialTerms;
  paymentTerms: Array<{
    id: string;
    code: string;
    name: string;
    dueDays: number;
    applicableTo: string;
  }>;
  contractDocuments: Array<{
    id: string;
    documentType: string;
    fileName: string;
    centralDocumentRecordId: string;
    centralDocumentVersionId: string;
  }>;
  isEditable: boolean;
  maximumRetentionPercentage: number;
  maximumDefectsLiabilityDays: number;
  sectionalTakeoverReleasePercentage: number;
  allowRetentionBond: boolean;
  controlProvisionalSums: boolean;
  controlContingencies: boolean;
  controlDefectsLiability: boolean;
  controlSectionalTakeover: boolean;
  controlSubcontracts: boolean;
  controlClaimClauses: boolean;
  requireCommercialTermsDocument: boolean;
  readinessBlockers: string[];
}

export type ConfigureContractCommercialTermsRequest = Omit<
  ContractCommercialTerms,
  | 'contractId'
  | 'projectId'
  | 'contractNumber'
  | 'contractTitle'
  | 'contractStatus'
  | 'contractValue'
  | 'currency'
  | 'configuredAt'
> & { clientRequestId: string };

export const quantitySurveyContractCommercialTermsService = {
  workspace: (contractId: string) =>
    apiService.get<ContractCommercialTermsWorkspace>(`${root}/${contractId}`),
  configure: (
    contractId: string,
    request: ConfigureContractCommercialTermsRequest
  ) =>
    apiService.put<ContractCommercialTermsWorkspace>(
      `${root}/${contractId}`,
      request
    ),
};
