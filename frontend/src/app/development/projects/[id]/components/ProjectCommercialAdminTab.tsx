import { type Dispatch, type SetStateAction, useMemo } from 'react';
import Link from 'next/link';
import { ExternalLink, Pencil, Plus, Save, Trash2, X } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { QuantitySurveyValuationWorksheetDialog } from '@/components/quantity-survey/QuantitySurveyValuationWorksheetDialog';
import { QuantitySurveyPaymentCertificateDialog } from '@/components/quantity-survey/QuantitySurveyPaymentCertificateDialog';
import { QuantitySurveyAdvanceRecoveryDialog } from '@/components/quantity-survey/QuantitySurveyAdvanceRecoveryDialog';
import { QuantitySurveyFinalAccountDialog } from '@/components/quantity-survey/QuantitySurveyFinalAccountDialog';
import { QuantitySurveyMaterialReconciliationDialog } from '@/components/quantity-survey/QuantitySurveyMaterialReconciliationDialog';
import { QuantitySurveyVariationDialog } from '@/components/quantity-survey/QuantitySurveyVariationDialog';
import { QuantitySurveyContractClaimsWorkspace } from '@/components/quantity-survey/QuantitySurveyContractClaimsWorkspace';
import { QuantitySurveyDayworkWorkspace } from '@/components/quantity-survey/QuantitySurveyDayworkWorkspace';
import { QuantitySurveySubcontractWorkspace } from '@/components/quantity-survey/QuantitySurveySubcontractWorkspace';
import { useAuth } from '@/hooks/use-auth';
import { getQuantitySurveyWorkspaceAccess } from '@/lib/quantity-survey-workspace-access';
import type { ContractDto } from '@/services/contractService';
import type {
  CreateProjectExtensionOfTimeDto,
  CreateProjectInterimValuationDto,
  CreateProjectPaymentCertificateDto,
  CreateProjectVariationOrderDto,
  ProjectDetailDto,
  ProjectExtensionOfTimeDto,
  ProjectInterimValuationDto,
  ProjectPackageDto,
  ProjectPaymentCertificateDto,
  ProjectPhaseDto,
  ProjectVariationOrderDto,
} from '@/services/projectService';

type Props = {
  project: ProjectDetailDto;
  phases: ProjectPhaseDto[];
  packages: ProjectPackageDto[];
  activeContracts: ContractDto[];
  currencyOptions: string[];
  variationOrderDraft: CreateProjectVariationOrderDto;
  setVariationOrderDraft: Dispatch<
    SetStateAction<CreateProjectVariationOrderDto>
  >;
  editingVariationOrderId: string | null;
  interimValuationDraft: CreateProjectInterimValuationDto;
  setInterimValuationDraft: Dispatch<
    SetStateAction<CreateProjectInterimValuationDto>
  >;
  editingInterimValuationId: string | null;
  paymentCertificateDraft: CreateProjectPaymentCertificateDto;
  setPaymentCertificateDraft: Dispatch<
    SetStateAction<CreateProjectPaymentCertificateDto>
  >;
  editingPaymentCertificateId: string | null;
  extensionOfTimeDraft: CreateProjectExtensionOfTimeDto;
  setExtensionOfTimeDraft: Dispatch<
    SetStateAction<CreateProjectExtensionOfTimeDto>
  >;
  editingExtensionOfTimeId: string | null;
  variationOrderStatusOptions: string[];
  variationOrderTypeOptions: string[];
  interimValuationStatusOptions: string[];
  paymentCertificateStatusOptions: string[];
  extensionOfTimeStatusOptions: string[];
  formatCatalogLabel: (value?: string | null) => string;
  getCurrencyOptionLabel: (code: string) => string;
  formatDateLabel: (value?: string) => string;
  formatMoney: (
    value: number | undefined,
    currency?: string | null,
    maximumFractionDigits?: number
  ) => string;
  onSaveVariationOrder: () => void;
  onEditVariationOrder: (variationOrder: ProjectVariationOrderDto) => void;
  onCancelVariationOrderEdit: () => void;
  onDeleteVariationOrder: (variationOrderId: string) => void;
  onSaveInterimValuation: () => void;
  onEditInterimValuation: (valuation: ProjectInterimValuationDto) => void;
  onCancelInterimValuationEdit: () => void;
  onDeleteInterimValuation: (valuationId: string) => void;
  onSavePaymentCertificate: () => void;
  onEditPaymentCertificate: (certificate: ProjectPaymentCertificateDto) => void;
  onCancelPaymentCertificateEdit: () => void;
  onDeletePaymentCertificate: (certificateId: string) => void;
  onSaveExtensionOfTime: () => void;
  onEditExtensionOfTime: (extension: ProjectExtensionOfTimeDto) => void;
  onCancelExtensionOfTimeEdit: () => void;
  onDeleteExtensionOfTime: (extensionId: string) => void;
};

const flattenPhases = (
  phases: ProjectPhaseDto[],
  depth = 0
): Array<{
  id: string;
  label: string;
  name: string;
  completionWeightPercent: number;
}> =>
  phases.flatMap((phase) => [
    {
      id: phase.id,
      label: `${' '.repeat(depth * 2)}${phase.name}`.trimStart(),
      name: phase.name,
      completionWeightPercent: phase.completionWeightPercent,
    },
    ...flattenPhases(phase.children || [], depth + 1),
  ]);

const contractLabel = (contract: ContractDto) =>
  [contract.contractNumber, contract.contractTitle]
    .filter(Boolean)
    .join(' - ') ||
  contract.contractNumber ||
  contract.contractTitle ||
  contract.id;

const formatWeight = (value?: number) =>
  `${Number(value ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 })}%`;
const normalizeId = (value?: string | null) =>
  (value || '').trim().toLowerCase();

export const usesCivilWorksEotWorkflow = (
  contracts: ContractDto[],
  contractId?: string | null
) => {
  const normalizedContractId = normalizeId(contractId);
  if (!normalizedContractId) return false;

  return contracts.some(
    contract =>
      normalizeId(contract.id) === normalizedContractId &&
      contract.contractType?.trim().toLowerCase() === 'works'
  );
};

export function ProjectCommercialAdminTab(props: Props) {
  const { hasPermission } = useAuth();
  const { canManageValuations, canManageVariations } =
    getQuantitySurveyWorkspaceAccess(hasPermission);
  const {
    project,
    phases,
    packages,
    activeContracts,
    currencyOptions,
    variationOrderDraft,
    setVariationOrderDraft,
    editingVariationOrderId,
    interimValuationDraft,
    setInterimValuationDraft,
    editingInterimValuationId,
    paymentCertificateDraft,
    setPaymentCertificateDraft,
    editingPaymentCertificateId,
    extensionOfTimeDraft,
    setExtensionOfTimeDraft,
    editingExtensionOfTimeId,
    variationOrderStatusOptions,
    variationOrderTypeOptions,
    paymentCertificateStatusOptions,
    extensionOfTimeStatusOptions,
    formatCatalogLabel,
    getCurrencyOptionLabel,
    formatDateLabel,
    formatMoney,
    onSaveVariationOrder,
    onEditVariationOrder,
    onCancelVariationOrderEdit,
    onDeleteVariationOrder,
    onSaveInterimValuation,
    onEditInterimValuation,
    onCancelInterimValuationEdit,
    onDeleteInterimValuation,
    onSavePaymentCertificate,
    onEditPaymentCertificate,
    onCancelPaymentCertificateEdit,
    onDeletePaymentCertificate,
    onSaveExtensionOfTime,
    onEditExtensionOfTime,
    onCancelExtensionOfTimeEdit,
    onDeleteExtensionOfTime,
  } = props;
  const phaseOptions = useMemo(() => flattenPhases(phases), [phases]);
  const worksContracts = useMemo(
    () =>
      activeContracts.filter(
        (contract) => contract.contractType?.toLowerCase() === 'works'
      ),
    [activeContracts]
  );
  const selectedExtensionUsesCivilWorkflow = useMemo(
    () =>
      usesCivilWorksEotWorkflow(
        activeContracts,
        extensionOfTimeDraft.contractId
      ),
    [activeContracts, extensionOfTimeDraft.contractId]
  );
  const milestoneOptions = useMemo(
    () =>
      [...project.milestones].sort(
        (left, right) =>
          String(left.targetDate).localeCompare(String(right.targetDate)) ||
          left.title.localeCompare(right.title)
      ),
    [project.milestones]
  );
  const completedWorkComponentIds = useMemo(
    () =>
      new Set(
        (interimValuationDraft.completedProjectPackageIds || []).map((item) =>
          normalizeId(item)
        )
      ),
    [interimValuationDraft.completedProjectPackageIds]
  );
  const selectedInterimValuationMilestone = useMemo(
    () =>
      milestoneOptions.find(
        (item) =>
          normalizeId(item.id) ===
          normalizeId(interimValuationDraft.projectMilestoneId)
      ) ?? null,
    [interimValuationDraft.projectMilestoneId, milestoneOptions]
  );
  const interimValuationPackageGroups = useMemo(
    () =>
      selectedInterimValuationMilestone
        ? selectedInterimValuationMilestone.phases.map((phaseSelection) => {
            const phasePackages = [...packages]
              .filter(
                (item) =>
                  normalizeId(item.projectPhaseId) ===
                  normalizeId(phaseSelection.projectPhaseId)
              )
              .sort(
                (left, right) =>
                  (left.sortOrder ?? 0) - (right.sortOrder ?? 0) ||
                  (left.code || left.name).localeCompare(
                    right.code || right.name
                  )
              );
            const totalWeight = phasePackages.reduce(
              (sum, item) => sum + (item.completionWeightPercent ?? 0),
              0
            );
            const completedWeight = phasePackages
              .filter((item) =>
                completedWorkComponentIds.has(normalizeId(item.id))
              )
              .reduce(
                (sum, item) => sum + (item.completionWeightPercent ?? 0),
                0
              );
            const phaseBudgetTotal = phasePackages.reduce(
              (sum, item) => sum + (item.budgetAmount ?? 0),
              0
            );
            const completedBudgetTotal = phasePackages
              .filter((item) =>
                completedWorkComponentIds.has(normalizeId(item.id))
              )
              .reduce((sum, item) => sum + (item.budgetAmount ?? 0), 0);

            return {
              phaseSelection,
              phasePackages,
              totalWeight,
              completedWeight,
              phaseBudgetTotal,
              completedBudgetTotal,
              completionPercent:
                totalWeight > 0 ? (completedWeight / totalWeight) * 100 : 0,
            };
          })
        : [],
    [completedWorkComponentIds, packages, selectedInterimValuationMilestone]
  );
  const interimValuationProgressPreview = useMemo(
    () =>
      interimValuationPackageGroups.reduce((sum, group) => {
        if (group.totalWeight <= 0) {
          return sum;
        }

        return (
          sum +
          group.phaseSelection.completionWeightPercent *
            (group.completedWeight / group.totalWeight)
        );
      }, 0),
    [interimValuationPackageGroups]
  );
  const interimValuationGrossWorkValue = useMemo(
    () =>
      Array.from(completedWorkComponentIds).reduce((sum, packageId) => {
        const projectPackage = packages.find(
          (item) => normalizeId(item.id) === packageId
        );
        return sum + (projectPackage?.budgetAmount ?? 0);
      }, 0),
    [completedWorkComponentIds, packages]
  );
  const interimValuationRetentionAmount = useMemo(() => {
    if ((interimValuationDraft.retentionAmount ?? 0) > 0) {
      return interimValuationDraft.retentionAmount ?? 0;
    }

    const retentionPercentage = interimValuationDraft.retentionPercentage ?? 0;
    if (retentionPercentage <= 0) {
      return 0;
    }

    const valuationBase =
      interimValuationGrossWorkValue +
      (interimValuationDraft.materialsOnSiteValue ?? 0) +
      (interimValuationDraft.variationValue ?? 0);
    return (valuationBase * retentionPercentage) / 100;
  }, [
    interimValuationDraft.materialsOnSiteValue,
    interimValuationDraft.retentionAmount,
    interimValuationDraft.retentionPercentage,
    interimValuationDraft.variationValue,
    interimValuationGrossWorkValue,
  ]);
  const interimValuationNetDue = useMemo(
    () =>
      Math.max(
        0,
        interimValuationGrossWorkValue +
          (interimValuationDraft.materialsOnSiteValue ?? 0) +
          (interimValuationDraft.variationValue ?? 0) -
          interimValuationRetentionAmount -
          (interimValuationDraft.previousCertifiedAmount ?? 0)
      ),
    [
      interimValuationDraft.materialsOnSiteValue,
      interimValuationDraft.previousCertifiedAmount,
      interimValuationDraft.variationValue,
      interimValuationGrossWorkValue,
      interimValuationRetentionAmount,
    ]
  );
  const approvedVariationAmount = useMemo(
    () =>
      project.variationOrders.reduce(
        (sum, item) => sum + (item.approvedAmount ?? 0),
        0
      ),
    [project.variationOrders]
  );
  const netValuationAmount = useMemo(
    () =>
      project.interimValuations.reduce(
        (sum, item) => sum + (item.netValuationAmount ?? 0),
        0
      ),
    [project.interimValuations]
  );
  const netCertifiedAmount = useMemo(
    () =>
      project.paymentCertificates.reduce(
        (sum, item) => sum + (item.netCertifiedAmount ?? 0),
        0
      ),
    [project.paymentCertificates]
  );
  const approvedExtensionDays = useMemo(
    () =>
      project.extensionOfTimeRequests.reduce(
        (sum, item) => sum + (item.daysApproved ?? 0),
        0
      ),
    [project.extensionOfTimeRequests]
  );
  const selectedPaymentCertificateValuation = useMemo(
    () =>
      project.interimValuations.find(
        (item) =>
          normalizeId(item.id) ===
          normalizeId(paymentCertificateDraft.projectInterimValuationId)
      ) ?? null,
    [
      paymentCertificateDraft.projectInterimValuationId,
      project.interimValuations,
    ]
  );
  const toggleCompletedWorkComponent = (
    workComponentId: string,
    checked: boolean
  ) => {
    setInterimValuationDraft((current) => {
      const currentIds = current.completedProjectPackageIds || [];
      const normalizedWorkComponentId = normalizeId(workComponentId);
      return {
        ...current,
        completedProjectPackageIds: checked
          ? [
              ...currentIds.filter(
                (item) => normalizeId(item) !== normalizedWorkComponentId
              ),
              workComponentId,
            ]
          : currentIds.filter(
              (item) => normalizeId(item) !== normalizedWorkComponentId
            ),
      };
    });
  };

  return (
    <div className="space-y-6">
      <div className="grid gap-4 md:grid-cols-3 xl:grid-cols-6">
        <Card>
          <CardContent className="p-4">
            <div className="text-sm text-muted-foreground">Variations</div>
            <div className="text-2xl font-semibold">
              {project.variationOrders.length}
            </div>
            <div className="text-sm text-muted-foreground">
              {formatMoney(
                approvedVariationAmount,
                project.finalAccount?.currency || currencyOptions[0]
              )}
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-4">
            <div className="text-sm text-muted-foreground">Valuations</div>
            <div className="text-2xl font-semibold">
              {project.interimValuations.length}
            </div>
            <div className="text-sm text-muted-foreground">
              {formatMoney(
                netValuationAmount,
                project.finalAccount?.currency || currencyOptions[0]
              )}
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-4">
            <div className="text-sm text-muted-foreground">Certificates</div>
            <div className="text-2xl font-semibold">
              {project.paymentCertificates.length}
            </div>
            <div className="text-sm text-muted-foreground">
              {formatMoney(
                netCertifiedAmount,
                project.finalAccount?.currency || currencyOptions[0]
              )}
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-4">
            <div className="text-sm text-muted-foreground">EOT</div>
            <div className="text-2xl font-semibold">
              {project.extensionOfTimeRequests.length}
            </div>
            <div className="text-sm text-muted-foreground">
              {approvedExtensionDays} approved day(s)
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-4">
            <div className="text-sm text-muted-foreground">Final Account</div>
            <div className="text-2xl font-semibold">
              {project.finalAccount
                ? formatMoney(
                    project.finalAccount.finalAccountValue,
                    project.finalAccount.currency
                  )
                : '-'}
            </div>
            <div className="text-sm text-muted-foreground">
              {formatCatalogLabel(project.finalAccount?.status || 'NotSet')}
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-4">
            <div className="text-sm text-muted-foreground">Contracts</div>
            <div className="text-2xl font-semibold">
              {activeContracts.length}
            </div>
            <div className="text-sm text-muted-foreground">
              {packages.filter((item) => !!item.contractId).length} work
              component(s) linked
            </div>
          </CardContent>
        </Card>
      </div>

      <Card data-testid="qs-retention-control-entry">
        <CardHeader>
          <CardTitle>Retention and closeout controls</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <p className="text-sm text-muted-foreground">
            Retention releases use the shared Works contract control, DEC-009
            policy, workflow approval and Finance reconciliation ledger.
          </p>
          {worksContracts.length > 0 ? (
            <div className="flex flex-wrap gap-2">
              {worksContracts.map((contract) => (
                <Button key={contract.id} variant="outline" asChild>
                  <Link
                    href={`/procurement/contracts/${contract.id}?tab=works-closeout`}
                  >
                    {contractLabel(contract)}
                    <ExternalLink className="ml-2 h-4 w-4" />
                  </Link>
                </Button>
              ))}
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">
              No active Works contract is linked to this project.
            </p>
          )}
        </CardContent>
      </Card>

      <Card className="hidden">
        <CardHeader>
          <CardTitle>Variation Orders</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-4">
            <div className="grid gap-2 md:col-span-2">
              <Label>Title</Label>
              <Input
                value={variationOrderDraft.title ?? ''}
                onChange={(event) =>
                  setVariationOrderDraft((current) => ({
                    ...current,
                    title: event.target.value,
                  }))
                }
              />
            </div>
            <div className="grid gap-2">
              <Label>Reference</Label>
              <Input
                value={variationOrderDraft.referenceNumber ?? ''}
                onChange={(event) =>
                  setVariationOrderDraft((current) => ({
                    ...current,
                    referenceNumber: event.target.value || undefined,
                  }))
                }
              />
            </div>
            <div className="grid gap-2">
              <Label>Type</Label>
              <Select
                value={
                  variationOrderDraft.variationType ||
                  variationOrderTypeOptions[0]
                }
                onValueChange={(value) =>
                  setVariationOrderDraft((current) => ({
                    ...current,
                    variationType: value,
                  }))
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {variationOrderTypeOptions.map((item) => (
                    <SelectItem key={item} value={item}>
                      {formatCatalogLabel(item)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label>Status</Label>
              <Select
                value={
                  variationOrderDraft.status || variationOrderStatusOptions[0]
                }
                onValueChange={(value) =>
                  setVariationOrderDraft((current) => ({
                    ...current,
                    status: value,
                  }))
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {variationOrderStatusOptions.map((item) => (
                    <SelectItem key={item} value={item}>
                      {formatCatalogLabel(item)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label>Phase</Label>
              <Select
                value={variationOrderDraft.projectPhaseId || 'none'}
                onValueChange={(value) =>
                  setVariationOrderDraft((current) => ({
                    ...current,
                    projectPhaseId: value === 'none' ? undefined : value,
                  }))
                }
              >
                <SelectTrigger>
                  <SelectValue placeholder="No phase" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">No phase</SelectItem>
                  {phaseOptions.map((phase) => (
                    <SelectItem key={phase.id} value={phase.id}>
                      {phase.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label>Work Component</Label>
              <Select
                value={variationOrderDraft.projectPackageId || 'none'}
                onValueChange={(value) =>
                  setVariationOrderDraft((current) => ({
                    ...current,
                    projectPackageId: value === 'none' ? undefined : value,
                  }))
                }
              >
                <SelectTrigger>
                  <SelectValue placeholder="No work component" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">No work component</SelectItem>
                  {packages.map((item) => (
                    <SelectItem key={item.id} value={item.id}>
                      {item.code ? `${item.code} - ${item.name}` : item.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label>Contract</Label>
              <Select
                value={variationOrderDraft.contractId || 'none'}
                onValueChange={(value) =>
                  setVariationOrderDraft((current) => ({
                    ...current,
                    contractId: value === 'none' ? undefined : value,
                  }))
                }
              >
                <SelectTrigger>
                  <SelectValue placeholder="No contract" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">No contract</SelectItem>
                  {activeContracts.map((contract) => (
                    <SelectItem key={contract.id} value={contract.id}>
                      {contractLabel(contract)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label>Requested</Label>
              <Input
                type="date"
                value={variationOrderDraft.requestedDate ?? ''}
                onChange={(event) =>
                  setVariationOrderDraft((current) => ({
                    ...current,
                    requestedDate: event.target.value || undefined,
                  }))
                }
              />
            </div>
            <div className="grid gap-2">
              <Label>Estimate</Label>
              <Input
                type="number"
                value={variationOrderDraft.estimatedAmount ?? ''}
                onChange={(event) =>
                  setVariationOrderDraft((current) => ({
                    ...current,
                    estimatedAmount: event.target.value
                      ? Number(event.target.value)
                      : undefined,
                  }))
                }
              />
            </div>
            <div className="grid gap-2">
              <Label>Approved</Label>
              <Input
                type="number"
                value={variationOrderDraft.approvedAmount ?? ''}
                onChange={(event) =>
                  setVariationOrderDraft((current) => ({
                    ...current,
                    approvedAmount: event.target.value
                      ? Number(event.target.value)
                      : undefined,
                  }))
                }
              />
            </div>
            <div className="grid gap-2">
              <Label>Schedule Days</Label>
              <Input
                type="number"
                value={variationOrderDraft.scheduleImpactDays ?? ''}
                onChange={(event) =>
                  setVariationOrderDraft((current) => ({
                    ...current,
                    scheduleImpactDays: event.target.value
                      ? Number(event.target.value)
                      : undefined,
                  }))
                }
              />
            </div>
            <div className="grid gap-2">
              <Label>Currency</Label>
              <Select
                value={variationOrderDraft.currency || currencyOptions[0]}
                onValueChange={(value) =>
                  setVariationOrderDraft((current) => ({
                    ...current,
                    currency: value,
                  }))
                }
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select currency" />
                </SelectTrigger>
                <SelectContent>
                  {currencyOptions.map((item) => (
                    <SelectItem key={item} value={item}>
                      {getCurrencyOptionLabel(item)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2 md:col-span-4">
              <Label>Notes</Label>
              <Textarea
                rows={2}
                value={variationOrderDraft.notes ?? ''}
                onChange={(event) =>
                  setVariationOrderDraft((current) => ({
                    ...current,
                    notes: event.target.value || undefined,
                  }))
                }
              />
            </div>
          </div>
          <div className="flex justify-end gap-2">
            {editingVariationOrderId ? (
              <Button variant="outline" onClick={onCancelVariationOrderEdit}>
                <X className="mr-2 h-4 w-4" />
                Cancel
              </Button>
            ) : null}
            <Button
              disabled={!variationOrderDraft.title?.trim()}
              onClick={onSaveVariationOrder}
            >
              {editingVariationOrderId ? (
                <Save className="mr-2 h-4 w-4" />
              ) : (
                <Plus className="mr-2 h-4 w-4" />
              )}
              {editingVariationOrderId ? 'Save Variation' : 'Add Variation'}
            </Button>
          </div>
          <div className="space-y-3">
            {project.variationOrders.length === 0 ? (
              <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
                No variation orders recorded yet.
              </div>
            ) : (
              project.variationOrders.map((item) => (
                <div key={item.id} className="rounded-lg border p-4">
                  <div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
                    <div className="space-y-2">
                      <div className="flex flex-wrap items-center gap-2">
                        <div className="font-medium">{item.title}</div>
                        <Badge variant="outline">
                          {formatCatalogLabel(item.variationType)}
                        </Badge>
                        <Badge>{formatCatalogLabel(item.status)}</Badge>
                        {item.projectPackageName ? (
                          <Badge variant="secondary">
                            {item.projectPackageName}
                          </Badge>
                        ) : null}
                      </div>
                      <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
                        {item.referenceNumber ? (
                          <span>{item.referenceNumber}</span>
                        ) : null}
                        <span>{formatDateLabel(item.requestedDate)}</span>
                        <span>
                          {formatMoney(item.approvedAmount, item.currency)}
                        </span>
                      </div>
                      {item.notes ? (
                        <div className="text-sm text-muted-foreground whitespace-pre-wrap">
                          {item.notes}
                        </div>
                      ) : null}
                    </div>
                    <div className="flex items-center gap-2">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => onEditVariationOrder(item)}
                      >
                        <Pencil className="mr-2 h-4 w-4" />
                        Edit
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => onDeleteVariationOrder(item.id)}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  </div>
                </div>
              ))
            )}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Variations and change orders</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-wrap items-center justify-between gap-3">
          <p className="max-w-3xl text-sm text-muted-foreground">
            Prepare site-instruction and change-request valuations against
            controlled Approved BoQ lines, retain clean central-DMS evidence,
            and route the result through the configured QS workflow.
          </p>
          <QuantitySurveyVariationDialog projectId={project.id} />
        </CardContent>
      </Card>

      <QuantitySurveyDayworkWorkspace projectId={project.id} />
      <QuantitySurveyContractClaimsWorkspace projectId={project.id} />
      <QuantitySurveySubcontractWorkspace projectId={project.id} />

      <div className="grid gap-6 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Interim Valuations</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {canManageValuations ? (
              <>
                <div className="grid gap-4 md:grid-cols-2">
                  <div className="grid gap-2">
                    <Label>Title</Label>
                    <Input
                      value={interimValuationDraft.title ?? ''}
                      onChange={(event) =>
                        setInterimValuationDraft((current) => ({
                          ...current,
                          title: event.target.value,
                        }))
                      }
                    />
                  </div>
                  <div className="grid gap-2">
                    <Label>Valuation No.</Label>
                    <Input
                      value={interimValuationDraft.valuationNumber ?? ''}
                      onChange={(event) =>
                        setInterimValuationDraft((current) => ({
                          ...current,
                          valuationNumber: event.target.value || undefined,
                        }))
                      }
                    />
                  </div>
                  <div className="grid gap-2">
                    <Label>Status</Label>
                    <Input
                      value="Draft — controlled by the valuation workflow"
                      disabled
                    />
                  </div>
                  <div className="grid gap-2">
                    <Label>Date</Label>
                    <Input
                      type="date"
                      value={interimValuationDraft.valuationDate ?? ''}
                      onChange={(event) =>
                        setInterimValuationDraft((current) => ({
                          ...current,
                          valuationDate: event.target.value || undefined,
                        }))
                      }
                    />
                  </div>
                  <div className="grid gap-2">
                    <Label>Milestone</Label>
                    <Select
                      value={interimValuationDraft.projectMilestoneId || 'none'}
                      onValueChange={(value) => {
                        const projectMilestoneId =
                          value === 'none' ? undefined : value;
                        const milestone = milestoneOptions.find(
                          (item) =>
                            normalizeId(item.id) ===
                            normalizeId(projectMilestoneId)
                        );
                        const allowedPackageIds = new Set(
                          milestone
                            ? packages
                                .filter((item) =>
                                  milestone.phases.some(
                                    (phase) =>
                                      normalizeId(phase.projectPhaseId) ===
                                      normalizeId(item.projectPhaseId)
                                  )
                                )
                                .map((item) => normalizeId(item.id))
                            : []
                        );
                        setInterimValuationDraft((current) => ({
                          ...current,
                          projectMilestoneId,
                          projectPhaseId: milestone?.phases[0]?.projectPhaseId,
                          projectPackageId: undefined,
                          completedProjectPackageIds: (
                            current.completedProjectPackageIds || []
                          ).filter((id) =>
                            allowedPackageIds.has(normalizeId(id))
                          ),
                        }));
                      }}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="No milestone" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">No milestone</SelectItem>
                        {milestoneOptions.map((item) => (
                          <SelectItem key={item.id} value={item.id}>
                            {item.title}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="grid gap-2">
                    <Label>Contract</Label>
                    <Select
                      value={interimValuationDraft.contractId || 'none'}
                      onValueChange={(value) =>
                        setInterimValuationDraft((current) => ({
                          ...current,
                          contractId: value === 'none' ? undefined : value,
                        }))
                      }
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="No contract" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">No contract</SelectItem>
                        {activeContracts.map((contract) => (
                          <SelectItem key={contract.id} value={contract.id}>
                            {contractLabel(contract)}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="grid gap-2">
                    <Label>Gross Work</Label>
                    <Input
                      value={formatMoney(
                        interimValuationGrossWorkValue,
                        interimValuationDraft.currency
                      )}
                      readOnly
                      className="bg-muted"
                    />
                    <div className="text-xs text-muted-foreground">
                      Derived from the budget total of the completed work
                      components in this valuation.
                    </div>
                  </div>
                  <div className="grid gap-2">
                    <Label>Due Payment (Net Valuation)</Label>
                    <Input
                      value={formatMoney(
                        interimValuationNetDue,
                        interimValuationDraft.currency
                      )}
                      readOnly
                      className="bg-muted"
                    />
                    <div className="text-xs text-muted-foreground">
                      Gross work less retention and previously certified
                      amounts.
                    </div>
                  </div>
                  <div className="grid gap-2">
                    <Label>Retention</Label>
                    <Input
                      type="number"
                      value={interimValuationDraft.retentionAmount ?? ''}
                      onChange={(event) =>
                        setInterimValuationDraft((current) => ({
                          ...current,
                          retentionAmount: event.target.value
                            ? Number(event.target.value)
                            : undefined,
                        }))
                      }
                    />
                  </div>
                  <div className="grid gap-2">
                    <Label>Currency</Label>
                    <Select
                      value={
                        interimValuationDraft.currency || currencyOptions[0]
                      }
                      onValueChange={(value) =>
                        setInterimValuationDraft((current) => ({
                          ...current,
                          currency: value,
                        }))
                      }
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Select currency" />
                      </SelectTrigger>
                      <SelectContent>
                        {currencyOptions.map((item) => (
                          <SelectItem key={item} value={item}>
                            {getCurrencyOptionLabel(item)}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="grid gap-2 md:col-span-2">
                    <Label>Notes</Label>
                    <Textarea
                      rows={2}
                      value={interimValuationDraft.notes ?? ''}
                      onChange={(event) =>
                        setInterimValuationDraft((current) => ({
                          ...current,
                          notes: event.target.value || undefined,
                        }))
                      }
                    />
                  </div>
                </div>
                {selectedInterimValuationMilestone ? (
                  <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-4">
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div className="space-y-1">
                        <div className="text-sm font-semibold text-slate-900">
                          Milestone Completion Checklist
                        </div>
                        <div className="text-xs text-slate-500">
                          Mark the completed work components for{' '}
                          {selectedInterimValuationMilestone.title}. The saved
                          selections feed the weighted project progress.
                        </div>
                      </div>
                      <div className="flex flex-wrap gap-2">
                        <Badge variant="outline">
                          {completedWorkComponentIds.size} completed
                        </Badge>
                        <Badge
                          variant="outline"
                          className="border-blue-200 bg-blue-50 text-blue-700"
                        >
                          Project Progress{' '}
                          {formatWeight(interimValuationProgressPreview)}
                        </Badge>
                      </div>
                    </div>
                    <div className="mt-4 space-y-3">
                      {interimValuationPackageGroups.length === 0 ? (
                        <div className="rounded-lg border border-dashed bg-white p-4 text-sm text-muted-foreground">
                          This milestone does not have any phase deliverables
                          yet.
                        </div>
                      ) : (
                        interimValuationPackageGroups.map((group) => (
                          <div
                            key={group.phaseSelection.projectPhaseId}
                            className="rounded-lg border bg-white p-4"
                          >
                            <div className="flex flex-wrap items-center justify-between gap-2">
                              <div className="flex flex-wrap items-center gap-2">
                                <div className="font-medium text-slate-900">
                                  {group.phaseSelection.phaseName}
                                </div>
                                <Badge
                                  variant="outline"
                                  className="border-blue-200 bg-blue-50 text-blue-700"
                                >
                                  Phase Weight{' '}
                                  {formatWeight(
                                    group.phaseSelection.completionWeightPercent
                                  )}
                                </Badge>
                                <Badge variant="outline">
                                  Completion{' '}
                                  {formatWeight(group.completionPercent)}
                                </Badge>
                              </div>
                              <div className="space-y-1 text-right text-xs text-slate-500">
                                <div>
                                  Completed Weight{' '}
                                  {formatWeight(group.completedWeight)} of{' '}
                                  {formatWeight(group.totalWeight)}
                                </div>
                                <div>
                                  Phase Total{' '}
                                  {formatMoney(
                                    group.phaseBudgetTotal,
                                    interimValuationDraft.currency
                                  )}
                                </div>
                                <div>
                                  Completed Amount{' '}
                                  {formatMoney(
                                    group.completedBudgetTotal,
                                    interimValuationDraft.currency
                                  )}
                                </div>
                              </div>
                            </div>
                            <div className="mt-3 grid gap-2">
                              {group.phasePackages.length === 0 ? (
                                <div className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
                                  No work components are currently linked to
                                  this phase.
                                </div>
                              ) : (
                                group.phasePackages.map((projectPackage) => (
                                  <label
                                    key={projectPackage.id}
                                    className="flex items-start gap-3 rounded-md border px-3 py-3 text-sm"
                                  >
                                    <Checkbox
                                      checked={completedWorkComponentIds.has(
                                        normalizeId(projectPackage.id)
                                      )}
                                      onCheckedChange={(checked) =>
                                        toggleCompletedWorkComponent(
                                          projectPackage.id,
                                          Boolean(checked)
                                        )
                                      }
                                    />
                                    <div className="min-w-0 flex-1">
                                      <div className="font-medium text-slate-900">
                                        {projectPackage.code
                                          ? `${projectPackage.code} - ${projectPackage.name}`
                                          : projectPackage.name}
                                      </div>
                                      <div className="text-xs text-slate-500">
                                        Work component weight{' '}
                                        {formatWeight(
                                          projectPackage.completionWeightPercent
                                        )}
                                      </div>
                                      <div className="text-xs text-slate-500">
                                        Budget total{' '}
                                        {formatMoney(
                                          projectPackage.budgetAmount ?? 0,
                                          interimValuationDraft.currency
                                        )}
                                      </div>
                                    </div>
                                  </label>
                                ))
                              )}
                            </div>
                          </div>
                        ))
                      )}
                    </div>
                  </div>
                ) : milestoneOptions.length > 0 ? (
                  <div className="rounded-lg border border-dashed p-4 text-sm text-muted-foreground">
                    Select a milestone to load its saved phases and work
                    components for completion tracking.
                  </div>
                ) : (
                  <div className="rounded-lg border border-dashed p-4 text-sm text-muted-foreground">
                    Create milestone deliverables in the Plan tab before using
                    milestone-based interim valuation progress.
                  </div>
                )}
                <div className="flex justify-end gap-2">
                  {editingInterimValuationId ? (
                    <Button
                      variant="outline"
                      onClick={onCancelInterimValuationEdit}
                    >
                      <X className="mr-2 h-4 w-4" />
                      Cancel
                    </Button>
                  ) : null}
                  <Button
                    disabled={!interimValuationDraft.title?.trim()}
                    onClick={onSaveInterimValuation}
                  >
                    {editingInterimValuationId ? (
                      <Save className="mr-2 h-4 w-4" />
                    ) : (
                      <Plus className="mr-2 h-4 w-4" />
                    )}
                    {editingInterimValuationId
                      ? 'Save Valuation'
                      : 'Add Valuation'}
                  </Button>
                </div>
              </>
            ) : null}
            <div className="space-y-3">
              {project.interimValuations.length === 0 ? (
                <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
                  No interim valuations recorded yet.
                </div>
              ) : (
                project.interimValuations.map((item) => (
                  <div key={item.id} className="rounded-lg border p-4">
                    <div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
                      <div className="space-y-2">
                        <div className="flex flex-wrap items-center gap-2">
                          <div className="font-medium">{item.title}</div>
                          <Badge>{formatCatalogLabel(item.status)}</Badge>
                          {item.projectMilestoneTitle ? (
                            <Badge variant="secondary">
                              {item.projectMilestoneTitle}
                            </Badge>
                          ) : null}
                        </div>
                        <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
                          {item.valuationNumber ? (
                            <span>{item.valuationNumber}</span>
                          ) : null}
                          <span>{formatDateLabel(item.valuationDate)}</span>
                          <span>
                            {formatMoney(
                              item.netValuationAmount,
                              item.currency
                            )}
                          </span>
                          <span>
                            {item.completedProjectPackageIds.length} completed
                            work component(s)
                          </span>
                        </div>
                      </div>
                      <div className="flex flex-wrap items-center gap-2">
                        <QuantitySurveyValuationWorksheetDialog
                          projectId={project.id}
                          interimValuationId={item.id}
                          interimValuationStatus={item.status}
                          currency={item.currency}
                        />
                        {canManageValuations ? (
                          <>
                            <Button
                              variant="outline"
                              size="sm"
                              disabled={item.status !== 'Draft'}
                              onClick={() => onEditInterimValuation(item)}
                            >
                              <Pencil className="mr-2 h-4 w-4" />
                              Edit
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              disabled={item.status !== 'Draft'}
                              onClick={() => onDeleteInterimValuation(item.id)}
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </>
                        ) : null}
                      </div>
                    </div>
                  </div>
                ))
              )}
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between gap-3">
            <CardTitle>Payment Certificates</CardTitle>
            <div className="flex flex-wrap gap-2">
              <QuantitySurveyAdvanceRecoveryDialog projectId={project.id} />
              <QuantitySurveyMaterialReconciliationDialog
                projectId={project.id}
              />
              <QuantitySurveyPaymentCertificateDialog projectId={project.id} />
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="hidden" aria-hidden="true">
              <div className="grid gap-4 md:grid-cols-2">
                <div className="grid gap-2">
                  <Label>Title</Label>
                  <Input
                    value={paymentCertificateDraft.title ?? ''}
                    onChange={(event) =>
                      setPaymentCertificateDraft((current) => ({
                        ...current,
                        title: event.target.value,
                      }))
                    }
                  />
                </div>
                <div className="grid gap-2">
                  <Label>Certificate No.</Label>
                  <Input
                    value={paymentCertificateDraft.certificateNumber ?? ''}
                    onChange={(event) =>
                      setPaymentCertificateDraft((current) => ({
                        ...current,
                        certificateNumber: event.target.value || undefined,
                      }))
                    }
                  />
                </div>
                <div className="grid gap-2">
                  <Label>Status</Label>
                  <Select
                    value={
                      paymentCertificateDraft.status ||
                      paymentCertificateStatusOptions[0]
                    }
                    onValueChange={(value) =>
                      setPaymentCertificateDraft((current) => ({
                        ...current,
                        status: value,
                      }))
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {paymentCertificateStatusOptions.map((item) => (
                        <SelectItem key={item} value={item}>
                          {formatCatalogLabel(item)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Issue Date</Label>
                  <Input
                    type="date"
                    value={paymentCertificateDraft.issueDate ?? ''}
                    onChange={(event) =>
                      setPaymentCertificateDraft((current) => ({
                        ...current,
                        issueDate: event.target.value || undefined,
                      }))
                    }
                  />
                </div>
                <div className="grid gap-2">
                  <Label>Due Date</Label>
                  <Input
                    type="date"
                    value={paymentCertificateDraft.paymentDueDate ?? ''}
                    onChange={(event) =>
                      setPaymentCertificateDraft((current) => ({
                        ...current,
                        paymentDueDate: event.target.value || undefined,
                      }))
                    }
                  />
                </div>
                <div className="grid gap-2">
                  <Label>Valuation</Label>
                  <Select
                    value={
                      paymentCertificateDraft.projectInterimValuationId ||
                      'none'
                    }
                    onValueChange={(value) => {
                      const projectInterimValuationId =
                        value === 'none' ? undefined : value;
                      const valuation = project.interimValuations.find(
                        (item) =>
                          normalizeId(item.id) ===
                          normalizeId(projectInterimValuationId)
                      );
                      const grossCertifiedAmount = valuation
                        ? (valuation.grossWorkValue ?? 0) +
                          (valuation.materialsOnSiteValue ?? 0) +
                          (valuation.variationValue ?? 0)
                        : undefined;
                      setPaymentCertificateDraft((current) => ({
                        ...current,
                        projectInterimValuationId,
                        projectPhaseId:
                          valuation?.projectPhaseId || current.projectPhaseId,
                        projectPackageId:
                          valuation?.projectPackageId || undefined,
                        contractId: valuation?.contractId || current.contractId,
                        title: valuation
                          ? `Payment Certificate - ${valuation.valuationNumber || valuation.title}`
                          : current.title,
                        grossCertifiedAmount,
                        retentionHeldAmount:
                          valuation?.retentionAmount ??
                          current.retentionHeldAmount,
                        netCertifiedAmount:
                          valuation?.netValuationAmount ??
                          current.netCertifiedAmount,
                        currency: valuation?.currency || current.currency,
                        notes:
                          valuation && !current.notes
                            ? valuation.notes || current.notes
                            : current.notes,
                      }));
                    }}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="No valuation" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No valuation</SelectItem>
                      {project.interimValuations.map((item) => (
                        <SelectItem key={item.id} value={item.id}>
                          {item.valuationNumber
                            ? `${item.valuationNumber} - ${item.title}`
                            : item.title}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Gross Certified</Label>
                  <Input
                    type="number"
                    value={paymentCertificateDraft.grossCertifiedAmount ?? ''}
                    onChange={(event) =>
                      setPaymentCertificateDraft((current) => ({
                        ...current,
                        grossCertifiedAmount: event.target.value
                          ? Number(event.target.value)
                          : undefined,
                      }))
                    }
                  />
                </div>
                <div className="grid gap-2">
                  <Label>Net Certified</Label>
                  <Input
                    type="number"
                    value={paymentCertificateDraft.netCertifiedAmount ?? ''}
                    onChange={(event) =>
                      setPaymentCertificateDraft((current) => ({
                        ...current,
                        netCertifiedAmount: event.target.value
                          ? Number(event.target.value)
                          : undefined,
                      }))
                    }
                  />
                </div>
                <div className="grid gap-2">
                  <Label>Retention Held</Label>
                  <Input
                    type="number"
                    value={paymentCertificateDraft.retentionHeldAmount ?? ''}
                    onChange={(event) =>
                      setPaymentCertificateDraft((current) => ({
                        ...current,
                        retentionHeldAmount: event.target.value
                          ? Number(event.target.value)
                          : undefined,
                      }))
                    }
                  />
                </div>
                <div className="grid gap-2">
                  <Label>Retention Released</Label>
                  <Input
                    type="number"
                    value={
                      paymentCertificateDraft.retentionReleasedAmount ?? ''
                    }
                    onChange={(event) =>
                      setPaymentCertificateDraft((current) => ({
                        ...current,
                        retentionReleasedAmount: event.target.value
                          ? Number(event.target.value)
                          : undefined,
                      }))
                    }
                  />
                </div>
                <div className="grid gap-2">
                  <Label>Other Deductions</Label>
                  <Input
                    type="number"
                    value={paymentCertificateDraft.otherDeductionsAmount ?? ''}
                    onChange={(event) =>
                      setPaymentCertificateDraft((current) => ({
                        ...current,
                        otherDeductionsAmount: event.target.value
                          ? Number(event.target.value)
                          : undefined,
                      }))
                    }
                  />
                </div>
                <div className="grid gap-2">
                  <Label>Currency</Label>
                  <Select
                    value={
                      paymentCertificateDraft.currency || currencyOptions[0]
                    }
                    onValueChange={(value) =>
                      setPaymentCertificateDraft((current) => ({
                        ...current,
                        currency: value,
                      }))
                    }
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select currency" />
                    </SelectTrigger>
                    <SelectContent>
                      {currencyOptions.map((item) => (
                        <SelectItem key={item} value={item}>
                          {getCurrencyOptionLabel(item)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2 md:col-span-2">
                  <Label>Notes</Label>
                  <Textarea
                    rows={2}
                    value={paymentCertificateDraft.notes ?? ''}
                    onChange={(event) =>
                      setPaymentCertificateDraft((current) => ({
                        ...current,
                        notes: event.target.value || undefined,
                      }))
                    }
                  />
                </div>
              </div>
              {selectedPaymentCertificateValuation ? (
                <div className="rounded-lg border bg-muted/20 p-3 text-sm">
                  <div className="font-medium">Selected Valuation Snapshot</div>
                  <div className="mt-1 text-muted-foreground">
                    Gross{' '}
                    {formatMoney(
                      (selectedPaymentCertificateValuation.grossWorkValue ??
                        0) +
                        (selectedPaymentCertificateValuation.materialsOnSiteValue ??
                          0) +
                        (selectedPaymentCertificateValuation.variationValue ??
                          0),
                      selectedPaymentCertificateValuation.currency
                    )}{' '}
                    · Net{' '}
                    {formatMoney(
                      selectedPaymentCertificateValuation.netValuationAmount,
                      selectedPaymentCertificateValuation.currency
                    )}{' '}
                    · Retention{' '}
                    {formatMoney(
                      selectedPaymentCertificateValuation.retentionAmount,
                      selectedPaymentCertificateValuation.currency
                    )}
                  </div>
                </div>
              ) : null}
              <div className="flex justify-end gap-2">
                {editingPaymentCertificateId ? (
                  <Button
                    variant="outline"
                    onClick={onCancelPaymentCertificateEdit}
                  >
                    <X className="mr-2 h-4 w-4" />
                    Cancel
                  </Button>
                ) : null}
                <Button
                  disabled={!paymentCertificateDraft.title?.trim()}
                  onClick={onSavePaymentCertificate}
                >
                  {editingPaymentCertificateId ? (
                    <Save className="mr-2 h-4 w-4" />
                  ) : (
                    <Plus className="mr-2 h-4 w-4" />
                  )}
                  {editingPaymentCertificateId
                    ? 'Save Certificate'
                    : 'Add Certificate'}
                </Button>
              </div>
            </div>
            <div className="space-y-3">
              {project.paymentCertificates.length === 0 ? (
                <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
                  No payment certificates recorded yet. Generate the first one
                  from an approved valuation in the governed workspace.
                </div>
              ) : (
                project.paymentCertificates.map((item) => (
                  <div key={item.id} className="rounded-lg border p-4">
                    <div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
                      <div className="space-y-2">
                        <div className="flex flex-wrap items-center gap-2">
                          <div className="font-medium">{item.title}</div>
                          <Badge>{formatCatalogLabel(item.status)}</Badge>
                        </div>
                        <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
                          {item.certificateNumber ? (
                            <span>{item.certificateNumber}</span>
                          ) : null}
                          <span>{formatDateLabel(item.issueDate)}</span>
                          <span>
                            {formatMoney(
                              item.netCertifiedAmount,
                              item.currency
                            )}
                          </span>
                        </div>
                      </div>
                      <div className="text-xs text-muted-foreground">
                        Use the governed workspace to amend, approve, issue, or
                        hand off this certificate.
                      </div>
                    </div>
                  </div>
                ))
              )}
            </div>
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-6 xl:grid-cols-[1fr,0.9fr]">
        <Card>
          <CardHeader>
            <CardTitle>Extension of Time</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {canManageVariations ? (
              <div className="grid gap-2">
                <Label>Contract</Label>
                <Select
                  value={extensionOfTimeDraft.contractId || '__unlinked__'}
                  onValueChange={value =>
                    setExtensionOfTimeDraft(current => ({
                      ...current,
                      contractId: value === '__unlinked__' ? undefined : value,
                    }))
                  }
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select contract" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__unlinked__">No linked contract</SelectItem>
                    {activeContracts.map(contract => (
                      <SelectItem key={contract.id} value={contract.id}>
                        {contractLabel(contract)} ·{' '}
                        {contract.contractType || 'Unspecified type'}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            ) : null}
            {selectedExtensionUsesCivilWorkflow ? (
              <div className="rounded-lg border border-blue-200 bg-blue-50 p-4 text-sm text-blue-900">
                Works extensions of time are governed in{' '}
                <span className="font-medium">
                  Site controls → Civil variation and extension of time
                </span>
                . That workspace selects the active Works contract, an applied
                QS variation where there is cost impact, current central-DMS
                evidence, and the configured workflow. Historical EOT records
                remain visible below.
              </div>
            ) : canManageVariations ? (
              <>
                <div className="grid gap-4 md:grid-cols-3">
                  <div className="grid gap-2 md:col-span-2">
                    <Label>Title</Label>
                    <Input
                      value={extensionOfTimeDraft.title ?? ''}
                      onChange={(event) =>
                        setExtensionOfTimeDraft((current) => ({
                          ...current,
                          title: event.target.value,
                        }))
                      }
                    />
                  </div>
                  <div className="grid gap-2">
                    <Label>Reference</Label>
                    <Input
                      value={extensionOfTimeDraft.referenceNumber ?? ''}
                      onChange={(event) =>
                        setExtensionOfTimeDraft((current) => ({
                          ...current,
                          referenceNumber: event.target.value || undefined,
                        }))
                      }
                    />
                  </div>
                  <div className="grid gap-2">
                    <Label>Status</Label>
                    <Select
                      value={
                        extensionOfTimeDraft.status ||
                        extensionOfTimeStatusOptions[0]
                      }
                      onValueChange={(value) =>
                        setExtensionOfTimeDraft((current) => ({
                          ...current,
                          status: value,
                        }))
                      }
                    >
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {extensionOfTimeStatusOptions.map((item) => (
                          <SelectItem key={item} value={item}>
                            {formatCatalogLabel(item)}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="grid gap-2">
                    <Label>Requested</Label>
                    <Input
                      type="date"
                      value={extensionOfTimeDraft.requestedDate ?? ''}
                      onChange={(event) =>
                        setExtensionOfTimeDraft((current) => ({
                          ...current,
                          requestedDate: event.target.value || undefined,
                        }))
                      }
                    />
                  </div>
                  <div className="grid gap-2">
                    <Label>Decision</Label>
                    <Input
                      type="date"
                      value={extensionOfTimeDraft.decisionDate ?? ''}
                      onChange={(event) =>
                        setExtensionOfTimeDraft((current) => ({
                          ...current,
                          decisionDate: event.target.value || undefined,
                        }))
                      }
                    />
                  </div>
                  <div className="grid gap-2">
                    <Label>Days Requested</Label>
                    <Input
                      type="number"
                      value={extensionOfTimeDraft.daysRequested ?? ''}
                      onChange={(event) =>
                        setExtensionOfTimeDraft((current) => ({
                          ...current,
                          daysRequested: event.target.value
                            ? Number(event.target.value)
                            : undefined,
                        }))
                      }
                    />
                  </div>
                  <div className="grid gap-2">
                    <Label>Days Approved</Label>
                    <Input
                      type="number"
                      value={extensionOfTimeDraft.daysApproved ?? ''}
                      onChange={(event) =>
                        setExtensionOfTimeDraft((current) => ({
                          ...current,
                          daysApproved: event.target.value
                            ? Number(event.target.value)
                            : undefined,
                        }))
                      }
                    />
                  </div>
                  <div className="grid gap-2">
                    <Label>Revised Completion</Label>
                    <Input
                      type="date"
                      value={extensionOfTimeDraft.revisedCompletionDate ?? ''}
                      onChange={(event) =>
                        setExtensionOfTimeDraft((current) => ({
                          ...current,
                          revisedCompletionDate:
                            event.target.value || undefined,
                        }))
                      }
                    />
                  </div>
                  <div className="grid gap-2 md:col-span-3">
                    <Label>Reason</Label>
                    <Textarea
                      rows={2}
                      value={extensionOfTimeDraft.reason ?? ''}
                      onChange={(event) =>
                        setExtensionOfTimeDraft((current) => ({
                          ...current,
                          reason: event.target.value || undefined,
                        }))
                      }
                    />
                  </div>
                </div>
                <div className="flex justify-end gap-2">
                  {editingExtensionOfTimeId ? (
                    <Button
                      variant="outline"
                      onClick={onCancelExtensionOfTimeEdit}
                    >
                      <X className="mr-2 h-4 w-4" />
                      Cancel
                    </Button>
                  ) : null}
                  <Button
                    disabled={!extensionOfTimeDraft.title?.trim()}
                    onClick={onSaveExtensionOfTime}
                  >
                    {editingExtensionOfTimeId ? (
                      <Save className="mr-2 h-4 w-4" />
                    ) : (
                      <Plus className="mr-2 h-4 w-4" />
                    )}
                    {editingExtensionOfTimeId ? 'Save EOT' : 'Add EOT'}
                  </Button>
                </div>
              </>
            ) : null}
            <div className="space-y-3">
              {project.extensionOfTimeRequests.length === 0 ? (
                <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
                  No EOT requests recorded yet.
                </div>
              ) : (
                project.extensionOfTimeRequests.map((item) => (
                  <div key={item.id} className="rounded-lg border p-4">
                    <div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
                      <div className="space-y-2">
                        <div className="flex flex-wrap items-center gap-2">
                          <div className="font-medium">{item.title}</div>
                          <Badge>{formatCatalogLabel(item.status)}</Badge>
                        </div>
                        <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
                          {item.referenceNumber ? (
                            <span>{item.referenceNumber}</span>
                          ) : null}
                          <span>{item.daysApproved ?? 0} approved day(s)</span>
                          {item.revisedCompletionDate ? (
                            <span>
                              {formatDateLabel(item.revisedCompletionDate)}
                            </span>
                          ) : null}
                        </div>
                        {item.reason ? (
                          <div className="text-sm text-muted-foreground whitespace-pre-wrap">
                            {item.reason}
                          </div>
                        ) : null}
                      </div>
                      {canManageVariations &&
                      !usesCivilWorksEotWorkflow(
                        activeContracts,
                        item.contractId
                      ) ? (
                        <div className="flex items-center gap-2">
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => onEditExtensionOfTime(item)}
                          >
                            <Pencil className="mr-2 h-4 w-4" />
                            Edit
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => onDeleteExtensionOfTime(item.id)}
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                      ) : null}
                    </div>
                  </div>
                ))
              )}
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Final Account</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center justify-between gap-3">
            <p className="max-w-3xl text-sm text-muted-foreground">
              Reconcile the approved BoQ lineage, contract changes,
              certificates, deductions, retention, and Finance-owned payments
              through the governed final-account workflow.
            </p>
            <QuantitySurveyFinalAccountDialog projectId={project.id} />
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
