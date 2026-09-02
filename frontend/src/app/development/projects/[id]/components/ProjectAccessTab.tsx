import { type Dispatch, type SetStateAction } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import type { BusinessPartnerDto } from '@/services/businessPartnerService';
import type { Asset as MaintenanceAssetLookupOption } from '@/services/maintenanceDataService';
import type {
  CreateProjectAssetLinkDto,
  CreateProjectExternalAccessPolicyDto,
  ProjectDetailDto,
  ProjectIntegrationSummaryDto,
  ProjectJobCardLinkOptionDto,
} from '@/services/projectService';
import type { FixedAsset } from '@/types/fixed-assets';

type ExternalArtifactOption = {
  id: string;
  label: string;
};

type ProjectAccessTabProps = {
  project: ProjectDetailDto;
  integrationSummary: ProjectIntegrationSummaryDto | null;
  formatMoney: (value: number | undefined, currency?: string | null, maximumFractionDigits?: number) => string;
  formatCatalogLabel: (value: string) => string;
  boolValue: (value?: boolean) => string;
  assetLink: CreateProjectAssetLinkDto;
  setAssetLink: Dispatch<SetStateAction<CreateProjectAssetLinkDto>>;
  assetLinkTypeOptions: string[];
  assetLinkStatusOptions: string[];
  maintenanceAssets: MaintenanceAssetLookupOption[];
  financeFixedAssets: FixedAsset[];
  jobCards: ProjectJobCardLinkOptionDto[];
  onAddAssetLink: () => void;
  externalPolicy: CreateProjectExternalAccessPolicyDto;
  setExternalPolicy: Dispatch<SetStateAction<CreateProjectExternalAccessPolicyDto>>;
  businessPartners: BusinessPartnerDto[];
  externalArtifactOptions: ExternalArtifactOption[];
  onSaveExternalPolicy: () => void;
  onDeleteExternalPolicy: (policyId: string) => void;
};

export function ProjectAccessTab({
  project,
  integrationSummary,
  formatMoney,
  formatCatalogLabel,
  boolValue,
  assetLink,
  setAssetLink,
  assetLinkTypeOptions,
  assetLinkStatusOptions,
  maintenanceAssets,
  financeFixedAssets,
  jobCards,
  onAddAssetLink,
  externalPolicy,
  setExternalPolicy,
  businessPartners,
  externalArtifactOptions,
  onSaveExternalPolicy,
  onDeleteExternalPolicy,
}: ProjectAccessTabProps) {
  const availableJobCards = assetLink.maintenanceAssetId
    ? jobCards.filter((jobCard) => jobCard.assetId === assetLink.maintenanceAssetId || jobCard.id === assetLink.jobCardId)
    : jobCards;

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader><CardTitle>ERP Integrations</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          {integrationSummary ? (
            <>
              <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Procurement Requisitions</div><div className="text-2xl font-semibold">{integrationSummary.purchaseRequisitionCount}</div><div className="text-sm text-muted-foreground">Pending {integrationSummary.pendingPurchaseRequisitionCount} | Amount {formatMoney(integrationSummary.purchaseRequisitionAmount)}</div></div>
                <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Purchase Orders</div><div className="text-2xl font-semibold">{integrationSummary.purchaseOrderCount}</div><div className="text-sm text-muted-foreground">Open {integrationSummary.openPurchaseOrderCount} | Amount {formatMoney(integrationSummary.purchaseOrderAmount)}</div></div>
                <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Purchase Receipts</div><div className="text-2xl font-semibold">{integrationSummary.purchaseReceiptCount}</div><div className="text-sm text-muted-foreground">Pending inspection {integrationSummary.pendingPurchaseReceiptInspectionCount}</div></div>
                <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Inventory Requisitions</div><div className="text-2xl font-semibold">{integrationSummary.inventoryRequisitionCount}</div><div className="text-sm text-muted-foreground">Pending {integrationSummary.pendingInventoryRequisitionCount} | Value {formatMoney(integrationSummary.inventoryRequisitionValue)}</div></div>
                <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Issued Inventory</div><div className="text-2xl font-semibold">{integrationSummary.issuedInventoryRequisitionCount}</div><div className="text-sm text-muted-foreground">Net {formatMoney(integrationSummary.netIssuedInventoryValue)} | Returned {formatMoney(integrationSummary.returnedInventoryValue)}</div></div>
                <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Commercial Tracking</div><div className="text-2xl font-semibold">{integrationSummary.invoiceRequestCount + integrationSummary.revenueRecognitionCount}</div><div className="text-sm text-muted-foreground">Invoices {integrationSummary.invoiceRequestCount} | Revenue entries {integrationSummary.revenueRecognitionCount}</div></div>
              </div>
              <div className="flex flex-wrap gap-2">
                <Badge variant={integrationSummary.hasBusinessPartner ? 'secondary' : 'outline'}>{integrationSummary.hasBusinessPartner ? 'Business partner linked' : 'No business partner'}</Badge>
                <Badge variant={integrationSummary.hasContract ? 'secondary' : 'outline'}>{integrationSummary.hasContract ? 'Contract linked' : 'No contract'}</Badge>
                <Badge variant={integrationSummary.hasPortfolio ? 'secondary' : 'outline'}>{integrationSummary.hasPortfolio ? 'Portfolio linked' : 'No portfolio'}</Badge>
                <Badge variant={integrationSummary.hasProgram ? 'secondary' : 'outline'}>{integrationSummary.hasProgram ? 'Program linked' : 'No program'}</Badge>
              </div>
              <div className="space-y-2">
                {integrationSummary.warnings.length === 0 ? <div className="text-sm text-muted-foreground">No integration warnings are currently flagged.</div> : null}
                {integrationSummary.warnings.map((warning, index) => <div key={`${warning}-${index}`} className="rounded-lg border p-4 text-sm text-muted-foreground">{warning}</div>)}
              </div>
              <div className="space-y-3">
                {integrationSummary.links.length === 0 ? <div className="text-sm text-muted-foreground">No linked ERP records have been discovered yet.</div> : null}
                {integrationSummary.links.map((link, index) => <div key={`${link.linkType}-${link.reference}-${index}`} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{formatCatalogLabel(link.linkType)}</div><div className="text-sm text-muted-foreground">{link.reference}</div></div><Badge variant="outline">{link.status}</Badge></div>)}
              </div>
            </>
          ) : <div className="text-sm text-muted-foreground">Integration summary is only available to users with project access.</div>}
        </CardContent>
      </Card>
      <Card>
        <CardHeader><CardTitle>Asset Links</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-4">
            <div className="grid gap-2">
              <Label>Maintenance Asset</Label>
              <Select
                value={assetLink.maintenanceAssetId || 'none'}
                onValueChange={(value) => setAssetLink((current) => {
                  if (value === 'none') {
                    return {
                      ...current,
                      maintenanceAssetId: undefined,
                      jobCardId: undefined,
                    };
                  }

                  return {
                    ...current,
                    maintenanceAssetId: value,
                    jobCardId:
                      !current.jobCardId
                      || jobCards.some((jobCard) => jobCard.id === current.jobCardId && jobCard.assetId === value)
                        ? current.jobCardId
                        : undefined,
                  };
                })}
              >
                <SelectTrigger><SelectValue placeholder="Select maintenance asset" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">No maintenance asset</SelectItem>
                  {maintenanceAssets.map((asset) => (
                    <SelectItem key={asset.id} value={asset.id}>
                      {asset.assetCode ? `${asset.assetCode} - ${asset.assetName}` : asset.assetName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label>Finance Fixed Asset</Label>
              <Select value={assetLink.fixedAssetId || 'none'} onValueChange={(value) => setAssetLink((current) => ({ ...current, fixedAssetId: value === 'none' ? undefined : value }))}>
                <SelectTrigger><SelectValue placeholder="Select fixed asset" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">No fixed asset</SelectItem>
                  {financeFixedAssets.map((asset) => (
                    <SelectItem key={asset.id} value={asset.id}>
                      {asset.assetCode ? `${asset.assetCode} - ${asset.name}` : asset.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label>Job Card</Label>
              <Select value={assetLink.jobCardId || 'none'} onValueChange={(value) => setAssetLink((current) => ({ ...current, jobCardId: value === 'none' ? undefined : value }))}>
                <SelectTrigger><SelectValue placeholder="Select job card" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">No job card</SelectItem>
                  {availableJobCards.map((jobCard) => (
                    <SelectItem key={jobCard.id} value={jobCard.id}>
                      {jobCard.jobCardNumber} - {jobCard.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2"><Label>Link Type</Label><Select value={assetLink.linkType || assetLinkTypeOptions[0]} onValueChange={(value) => setAssetLink((current) => ({ ...current, linkType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{assetLinkTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Status</Label><Select value={assetLink.status || assetLinkStatusOptions[0]} onValueChange={(value) => setAssetLink((current) => ({ ...current, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{assetLinkStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2 md:col-span-2"><Label>Notes</Label><Input value={assetLink.notes || ''} onChange={(event) => setAssetLink((current) => ({ ...current, notes: event.target.value }))} /></div>
            <div className="flex items-end"><Button onClick={onAddAssetLink}><Plus className="mr-2 h-4 w-4" />Link Asset</Button></div>
          </div>
          <div className="space-y-3">
            {project.assetLinks.length === 0 ? <div className="text-sm text-muted-foreground">No maintenance or asset links have been added yet.</div> : null}
            {project.assetLinks.map((link) => <div key={link.id} className="rounded-lg border p-4"><div className="font-medium">{link.assetName || link.jobCardNumber || link.linkType}</div><div className="text-sm text-muted-foreground">{link.status} | maintenance asset {link.maintenanceAssetId || 'N/A'} | finance fixed asset {link.fixedAssetId || 'N/A'} | legacy company asset {link.companyAssetId || 'N/A'} | job card {link.jobCardId || 'N/A'}</div>{link.notes ? <div className="mt-2 text-sm text-muted-foreground">{link.notes}</div> : null}</div>)}
          </div>
        </CardContent>
      </Card>
      <Card>
        <CardHeader><CardTitle>External Access Policies</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-4">
            <div className="grid gap-2"><Label>Business Partner</Label><Select value={externalPolicy.businessPartnerId || 'none'} onValueChange={(value) => setExternalPolicy((current) => ({ ...current, businessPartnerId: value === 'none' ? '' : value }))}><SelectTrigger><SelectValue placeholder="Select business partner" /></SelectTrigger><SelectContent><SelectItem value="none">Select business partner</SelectItem>{businessPartners.map((partner) => <SelectItem key={partner.id} value={partner.id}>{partner.partnerName}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Artifact Type</Label><Select value={externalPolicy.artifactType || 'Project'} onValueChange={(value) => setExternalPolicy((current) => ({ ...current, artifactType: value, artifactId: value === 'Project' ? undefined : current.artifactId }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{['Project', 'Document', 'Deliverable', 'WorkItem'].map((item) => <SelectItem key={item} value={item}>{item}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2">
              <Label>Artifact</Label>
              {externalPolicy.artifactType === 'Project' ? (
                <Input value="Project-wide access" disabled />
              ) : (
                <Select value={externalPolicy.artifactId || 'none'} onValueChange={(value) => setExternalPolicy((current) => ({ ...current, artifactId: value === 'none' ? undefined : value }))}>
                  <SelectTrigger><SelectValue placeholder="Select artifact" /></SelectTrigger>
                  <SelectContent><SelectItem value="none">Select artifact</SelectItem>{externalArtifactOptions.map((artifact) => <SelectItem key={artifact.id} value={artifact.id}>{artifact.label}</SelectItem>)}</SelectContent>
                </Select>
              )}
            </div>
            <div className="grid gap-2"><Label>Access Level</Label><Select value={externalPolicy.accessLevel || 'Read'} onValueChange={(value) => setExternalPolicy((current) => ({ ...current, accessLevel: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{['Read', 'Collaborate', 'Approve'].map((item) => <SelectItem key={item} value={item}>{item}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Can Comment</Label><Select value={boolValue(externalPolicy.canComment)} onValueChange={(value) => setExternalPolicy((current) => ({ ...current, canComment: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">No</SelectItem><SelectItem value="true">Yes</SelectItem></SelectContent></Select></div>
            <div className="grid gap-2"><Label>Can Upload</Label><Select value={boolValue(externalPolicy.canUpload)} onValueChange={(value) => setExternalPolicy((current) => ({ ...current, canUpload: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">No</SelectItem><SelectItem value="true">Yes</SelectItem></SelectContent></Select></div>
            <div className="grid gap-2"><Label>Can Approve</Label><Select value={boolValue(externalPolicy.canApprove)} onValueChange={(value) => setExternalPolicy((current) => ({ ...current, canApprove: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">No</SelectItem><SelectItem value="true">Yes</SelectItem></SelectContent></Select></div>
            <div className="grid gap-2 md:col-span-2"><Label>Notes</Label><Input value={externalPolicy.notes || ''} onChange={(event) => setExternalPolicy((current) => ({ ...current, notes: event.target.value }))} /></div>
            <div className="flex items-end"><Button disabled={!externalPolicy.businessPartnerId || (externalPolicy.artifactType !== 'Project' && !externalPolicy.artifactId)} onClick={onSaveExternalPolicy}><Plus className="mr-2 h-4 w-4" />Save Policy</Button></div>
          </div>
          <div className="space-y-3">
            {project.externalAccessPolicies.length === 0 ? <div className="text-sm text-muted-foreground">No external access policies are configured yet.</div> : null}
            {project.externalAccessPolicies.map((policy) => <div key={policy.id} className="rounded-lg border p-4"><div className="flex items-start justify-between gap-3"><div><div className="font-medium">{policy.businessPartnerName || policy.businessPartnerId}</div><div className="text-sm text-muted-foreground">{policy.artifactType} | {policy.artifactLabel || 'Project-wide'} | {policy.accessLevel} | comment {policy.canComment ? 'yes' : 'no'} | upload {policy.canUpload ? 'yes' : 'no'} | approve {policy.canApprove ? 'yes' : 'no'}</div>{policy.notes ? <div className="mt-2 text-sm text-muted-foreground">{policy.notes}</div> : null}</div><Button variant="ghost" size="sm" onClick={() => onDeleteExternalPolicy(policy.id)}><Trash2 className="h-4 w-4" /></Button></div></div>)}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
