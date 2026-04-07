import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Plus, RefreshCw } from 'lucide-react';
import type { InventoryRequisitionDto } from '@/services/inventoryRequisitionService';
import { RequisitionStatusMap } from '@/services/inventoryRequisitionService';

type MaterialSnapshot = {
  pendingApproval: number;
  pendingIssue: number;
  totalValue: number;
  completed: number;
};

type ProjectMaterialsTabProps = {
  materialRequisitions: InventoryRequisitionDto[];
  orderedMaterialRequisitions: InventoryRequisitionDto[];
  materialSnapshot: MaterialSnapshot;
  formatMoney: (value: number | undefined, currency?: string | null, maximumFractionDigits?: number) => string;
  formatDateLabel: (value?: string) => string;
  onRefresh: () => void;
  onOpenMaterialDialog: (mode: 'create' | 'edit' | 'view', requisitionId?: string) => void;
  onSubmitRequisition: (requisitionId: string) => void;
  onOpenIssueDialog: (requisitionId: string) => void;
  onOpenReturnDialog: (requisitionId: string) => void;
  onCompleteRequisition: (requisitionId: string) => void;
};

const normalizeRequisitionStatus = (status: number | string) => {
  if (typeof status === 'number') {
    return status;
  }

  const statusMap: Record<string, number> = {
    Draft: 1,
    Submitted: 2,
    Approved: 3,
    InProgress: 4,
    PartiallyIssued: 5,
    Issued: 6,
    Completed: 7,
    Cancelled: 8,
    Rejected: 9,
  };

  return statusMap[status] ?? 0;
};

export function ProjectMaterialsTab({
  materialRequisitions,
  orderedMaterialRequisitions,
  materialSnapshot,
  formatMoney,
  formatDateLabel,
  onRefresh,
  onOpenMaterialDialog,
  onSubmitRequisition,
  onOpenIssueDialog,
  onOpenReturnDialog,
  onCompleteRequisition,
}: ProjectMaterialsTabProps) {
  return (
    <div className="space-y-6">
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle>Project Materials</CardTitle>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" onClick={onRefresh}>
              <RefreshCw className="mr-2 h-4 w-4" />
              Refresh
            </Button>
            <Button size="sm" onClick={() => onOpenMaterialDialog('create')}>
              <Plus className="mr-2 h-4 w-4" />
              New Material Requisition
            </Button>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-4">
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Total Requisitions</div>
              <div className="text-2xl font-semibold">{materialRequisitions.length}</div>
              <div className="text-sm text-muted-foreground">Project-scoped material demand</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Pending Approval</div>
              <div className="text-2xl font-semibold">{materialSnapshot.pendingApproval}</div>
              <div className="text-sm text-muted-foreground">Waiting on workflow approval</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Pending Issue</div>
              <div className="text-2xl font-semibold">{materialSnapshot.pendingIssue}</div>
              <div className="text-sm text-muted-foreground">Approved but not fully issued</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Requested Value</div>
              <div className="text-2xl font-semibold">{formatMoney(materialSnapshot.totalValue)}</div>
              <div className="text-sm text-muted-foreground">Completed {materialSnapshot.completed}</div>
            </div>
          </div>
          <div className="space-y-3">
            {orderedMaterialRequisitions.length === 0 ? (
              <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
                No project-linked inventory requisitions have been raised yet.
              </div>
            ) : null}
            {orderedMaterialRequisitions.map((requisition) => {
              const statusValue = normalizeRequisitionStatus(requisition.status);
              const canIssue = [3, 4, 5].includes(statusValue);
              const canReturn = [5, 6, 7].includes(statusValue);
              const canComplete = statusValue === 6;
              const canEdit = statusValue === 1;
              const canSubmit = statusValue === 1;

              return (
                <div key={requisition.id} className="rounded-lg border p-4">
                  <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
                    <div className="space-y-2">
                      <div className="flex flex-wrap items-center gap-2">
                        <div className="font-medium">{requisition.requisitionNumber}</div>
                        <Badge variant="outline">{RequisitionStatusMap[statusValue] || requisition.status}</Badge>
                        <Badge variant="secondary">{requisition.priority}</Badge>
                        {requisition.currentWorkflowStepName ? (
                          <Badge variant="outline">Step: {requisition.currentWorkflowStepName}</Badge>
                        ) : null}
                      </div>
                      <div className="text-sm text-muted-foreground">
                        Warehouse {requisition.warehouseName}
                        {requisition.locationName ? ` | Location ${requisition.locationName}` : ''} | Request{' '}
                        {formatDateLabel(requisition.requestDate)} | Required {formatDateLabel(requisition.requiredDate)}
                      </div>
                      <div className="text-sm text-muted-foreground">
                        Items {requisition.totalItems} | Quantity {requisition.totalQuantity} | Value {formatMoney(requisition.totalValue)}
                      </div>
                      {requisition.purpose ? <div className="text-sm">{requisition.purpose}</div> : null}
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <Button variant="outline" size="sm" onClick={() => onOpenMaterialDialog('view', requisition.id)}>View</Button>
                      {canEdit ? <Button variant="outline" size="sm" onClick={() => onOpenMaterialDialog('edit', requisition.id)}>Edit</Button> : null}
                      {canSubmit ? <Button variant="outline" size="sm" onClick={() => onSubmitRequisition(requisition.id)}>Submit</Button> : null}
                      {canIssue ? <Button size="sm" onClick={() => onOpenIssueDialog(requisition.id)}>Issue</Button> : null}
                      {canReturn ? <Button variant="outline" size="sm" onClick={() => onOpenReturnDialog(requisition.id)}>Return</Button> : null}
                      {canComplete ? <Button variant="outline" size="sm" onClick={() => onCompleteRequisition(requisition.id)}>Complete</Button> : null}
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
