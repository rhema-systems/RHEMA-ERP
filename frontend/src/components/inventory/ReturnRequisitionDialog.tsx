'use client';

import React, { useEffect, useState } from 'react';
import { format } from 'date-fns';
import { CheckCircle } from 'lucide-react';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Card, CardContent } from '@/components/ui/card';
import {
  inventoryRequisitionService,
  InventoryRequisitionDetailDto,
  ReturnRequisitionDto,
  RequisitionStatusMap,
} from '@/services/inventoryRequisitionService';
import { useToast } from '@/hooks/use-toast';

interface ReturnRequisitionDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  requisitionId: string | null;
  onSuccess: () => void;
}

interface ReturnItemState {
  itemId: string;
  itemCode: string;
  itemName: string;
  issuedQuantity: number;
  returningQuantity: number;
  unitOfMeasure: string;
  locationId?: string;
  locationName?: string;
}

const normalizeStatus = (status: number | string | undefined): number => {
  if (status === undefined || status === null) return 0;
  if (typeof status === 'number') return status;
  const statusMap: Record<string, number> = {
    Draft: 1, Submitted: 2, Approved: 3, InProgress: 4,
    PartiallyIssued: 5, Issued: 6, Completed: 7, Cancelled: 8, Rejected: 9,
  };
  return statusMap[status] || 0;
};

export function ReturnRequisitionDialog({ open, onOpenChange, requisitionId, onSuccess }: ReturnRequisitionDialogProps) {
  const { toast } = useToast();
  const [loading, setLoading] = useState(false);
  const [processing, setProcessing] = useState(false);
  const [requisition, setRequisition] = useState<InventoryRequisitionDetailDto | null>(null);
  const [returnItems, setReturnItems] = useState<ReturnItemState[]>([]);
  const [notes, setNotes] = useState('');

  useEffect(() => {
    if (open && requisitionId) {
      void loadRequisition();
    }
  }, [open, requisitionId]);

  const loadRequisition = async () => {
    if (!requisitionId) return;
    try {
      setLoading(true);
      const detail = await inventoryRequisitionService.getById(requisitionId);
      setRequisition(detail);
      setReturnItems(detail.items.map((item) => ({
        itemId: item.id,
        itemCode: item.itemCode,
        itemName: item.itemName,
        issuedQuantity: item.issuedQuantity || 0,
        returningQuantity: 0,
        unitOfMeasure: item.unitOfMeasure,
        locationId: item.locationId || detail.locationId,
        locationName: item.locationName || detail.locationName,
      })));
      setNotes('');
    } catch (error) {
      console.error('Error loading requisition for return', error);
      toast({ title: 'Error', description: 'Failed to load requisition', variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  };

  const setQuantity = (itemId: string, quantity: number) => {
    setReturnItems((items) => items.map((item) => {
      if (item.itemId !== itemId) return item;
      const valid = Math.max(0, Math.min(quantity, item.issuedQuantity));
      return { ...item, returningQuantity: valid };
    }));
  };

  const returnAll = () => {
    setReturnItems((items) => items.map((item) => ({ ...item, returningQuantity: item.issuedQuantity })));
  };

  const handleReturn = async () => {
    if (!requisitionId) {
      return;
    }

    const items = returnItems.filter((item) => item.returningQuantity > 0);
    if (items.length === 0) {
      toast({ title: 'Validation Error', description: 'Enter at least one return quantity', variant: 'destructive' });
      return;
    }

    try {
      setProcessing(true);
      const dto: ReturnRequisitionDto = {
        items: items.map((item) => ({
          itemId: item.itemId,
          returnedQuantity: item.returningQuantity,
          locationId: item.locationId,
        })),
        notes: notes || undefined,
      };
      await inventoryRequisitionService.returnItems(requisitionId, dto);
      toast({ title: 'Success', description: 'Items returned to stock successfully' });
      onSuccess();
      onOpenChange(false);
    } catch (error: unknown) {
      console.error('Error returning requisition items', error);
      const errorMessage = error instanceof Error ? error.message : 'Failed to return items';
      toast({ title: 'Error', description: errorMessage, variant: 'destructive' });
    } finally {
      setProcessing(false);
    }
  };

  const totalReturning = returnItems.reduce((sum, item) => sum + item.returningQuantity, 0);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>
            Return Issued Items
            {requisition ? <span className="ml-2 text-muted-foreground">#{requisition.requisitionNumber}</span> : null}
          </DialogTitle>
        </DialogHeader>

        {loading ? (
          <div className="flex justify-center py-8"><div className="h-8 w-8 animate-spin rounded-full border-b-2 border-primary"></div></div>
        ) : requisition ? (
          <div className="space-y-4">
            <Card>
              <CardContent className="pt-4">
                <div className="grid grid-cols-3 gap-4 text-sm">
                  <div><span className="text-muted-foreground">Department:</span> <span className="font-medium">{requisition.departmentName}</span></div>
                  <div><span className="text-muted-foreground">Warehouse:</span> <span className="font-medium">{requisition.warehouseName}</span></div>
                  <div><span className="text-muted-foreground">Status:</span> <Badge variant="outline">{RequisitionStatusMap[normalizeStatus(requisition.status)] || requisition.status}</Badge></div>
                  <div><span className="text-muted-foreground">Request Date:</span> <span className="font-medium">{requisition.requestDateFormatted || format(new Date(requisition.requestDate), 'dd/MM/yyyy')}</span></div>
                  <div><span className="text-muted-foreground">Issued Date:</span> <span className="font-medium">{requisition.issuedDate ? format(new Date(requisition.issuedDate), 'dd/MM/yyyy') : 'N/A'}</span></div>
                  <div><span className="text-muted-foreground">Project:</span> <span className="font-medium">{requisition.projectCode || 'Not linked'}</span></div>
                  <div><span className="text-muted-foreground">Location:</span> <span className="font-medium">{requisition.locationName || 'Warehouse level'}</span></div>
                </div>
              </CardContent>
            </Card>

            <div className="flex items-center justify-between">
              <h4 className="font-medium">Issued Items Available For Return</h4>
              <Button variant="outline" size="sm" onClick={returnAll}>Return All Issued</Button>
            </div>

            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Item</TableHead>
                  <TableHead className="text-center">Issued Qty</TableHead>
                  <TableHead className="text-center">Return Qty</TableHead>
                  <TableHead>Location</TableHead>
                  <TableHead>UoM</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {returnItems.map((item) => (
                  <TableRow key={item.itemId}>
                    <TableCell>
                      <div className="font-medium">{item.itemCode}</div>
                      <div className="text-sm text-muted-foreground">{item.itemName}</div>
                    </TableCell>
                    <TableCell className="text-center">
                      {item.issuedQuantity > 0 ? <Badge variant="outline">{item.issuedQuantity}</Badge> : <Badge variant="secondary"><CheckCircle className="mr-1 h-3 w-3" />No issued qty</Badge>}
                    </TableCell>
                    <TableCell className="text-center">
                      <Input
                        type="number"
                        min="0"
                        max={item.issuedQuantity}
                        value={item.returningQuantity}
                        onChange={(e) => setQuantity(item.itemId, Number(e.target.value || '0'))}
                        className="w-24 text-center"
                        disabled={item.issuedQuantity <= 0}
                      />
                    </TableCell>
                    <TableCell>{item.locationName || requisition.locationName || 'Warehouse level'}</TableCell>
                    <TableCell>{item.unitOfMeasure}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>

            <div className="space-y-2">
              <Label>Return Notes</Label>
              <Textarea rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Reason for return or adjustment" />
            </div>

            <div className="rounded-md bg-muted p-3 text-sm">
              Total return quantity: <span className="font-medium">{totalReturning}</span>
            </div>
          </div>
        ) : null}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button onClick={() => void handleReturn()} disabled={processing || loading || totalReturning <= 0}>
            {processing ? 'Processing...' : 'Return To Stock'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
