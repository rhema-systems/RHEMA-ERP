'use client';

import { useState, useEffect } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { ScrollArea } from '@/components/ui/scroll-area';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Loader2, Users, DollarSign, Check, AlertCircle, Save } from 'lucide-react';
import { toast } from 'sonner';
import * as negotiationService from '@/services/negotiationService';
import { TenderNegotiationDto, TenderNegotiationItemDto } from '@/services/negotiationService';

interface NegotiationInviteDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  tenderId: string;
  tenderBidId: string;
  lotId?: string;
  bidLotId?: string;
  businessPartnerName: string;
  onNegotiationComplete?: (negotiatedAmount: number) => void;
}

export default function NegotiationInviteDialog({
  open,
  onOpenChange,
  tenderId,
  tenderBidId,
  lotId,
  bidLotId,
  businessPartnerName,
  onNegotiationComplete
}: NegotiationInviteDialogProps) {
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [savingDraft, setSavingDraft] = useState(false);
  const [negotiation, setNegotiation] = useState<TenderNegotiationDto | null>(null);
  const [editedItems, setEditedItems] = useState<Map<string, { price: number; notes: string }>>(new Map());
  const [notes, setNotes] = useState('');
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);

  useEffect(() => {
    if (open && tenderId && tenderBidId) {
      loadNegotiation();
    }
  }, [open, tenderId, tenderBidId, lotId]);

  const loadNegotiation = async () => {
    try {
      setLoading(true);
      // First check if a negotiation already exists
      let existing = await negotiationService.getNegotiationByTenderAndBid(tenderId, tenderBidId, lotId);
      
      if (!existing) {
        // Create a new negotiation
        existing = await negotiationService.createNegotiation({
          tenderId,
          tenderBidId,
          lotId,
          bidLotId,
          notes: `Negotiation initiated for ${businessPartnerName}`
        });
        toast.success('Negotiation invitation sent successfully!');
      }
      
      setNegotiation(existing);
      
      // Initialize edited items with existing negotiated prices
      const initialEdits = new Map<string, { price: number; notes: string }>();
      existing.items.forEach(item => {
        initialEdits.set(item.id, {
          price: item.negotiatedUnitPrice ?? item.originalUnitPrice,
          notes: item.notes ?? ''
        });
      });
      setEditedItems(initialEdits);
      setNotes(existing.notes ?? '');
      setHasUnsavedChanges(false);
    } catch (error) {
      console.error('Error loading negotiation:', error);
      toast.error('Failed to load negotiation details');
    } finally {
      setLoading(false);
    }
  };

  const handlePriceChange = (itemId: string, price: number) => {
    setEditedItems(prev => {
      const newMap = new Map(prev);
      const current = newMap.get(itemId) ?? { price: 0, notes: '' };
      newMap.set(itemId, { ...current, price });
      return newMap;
    });
    setHasUnsavedChanges(true);
  };

  const handleItemNotesChange = (itemId: string, itemNotes: string) => {
    setEditedItems(prev => {
      const newMap = new Map(prev);
      const current = newMap.get(itemId) ?? { price: 0, notes: '' };
      newMap.set(itemId, { ...current, notes: itemNotes });
      return newMap;
    });
    setHasUnsavedChanges(true);
  };

  const handleNotesChange = (value: string) => {
    setNotes(value);
    setHasUnsavedChanges(true);
  };

  const handleSaveDraft = async () => {
    if (!negotiation) return;
    
    try {
      setSavingDraft(true);
      const items = negotiation.items.map(item => {
        const edited = editedItems.get(item.id);
        return {
          itemId: item.id,
          negotiatedUnitPrice: edited?.price ?? item.originalUnitPrice,
          notes: edited?.notes
        };
      });

      const updated = await negotiationService.saveDraft(negotiation.id, { items, notes });
      setNegotiation(updated);
      setHasUnsavedChanges(false);
      toast.success('Draft saved successfully!');
    } catch (error) {
      console.error('Error saving draft:', error);
      toast.error('Failed to save draft');
    } finally {
      setSavingDraft(false);
    }
  };

  const calculateTotal = () => {
    if (!negotiation) return 0;
    return negotiation.items.reduce((total, item) => {
      const edited = editedItems.get(item.id);
      const unitPrice = edited?.price ?? item.negotiatedUnitPrice ?? item.originalUnitPrice;
      return total + (unitPrice * item.quantity);
    }, 0);
  };

  const calculateSavings = () => {
    if (!negotiation) return 0;
    return negotiation.originalAmount - calculateTotal();
  };

  const handleComplete = async () => {
    if (!negotiation) return;
    
    try {
      setSubmitting(true);
      const items = negotiation.items.map(item => {
        const edited = editedItems.get(item.id);
        return {
          itemId: item.id,
          negotiatedUnitPrice: edited?.price ?? item.originalUnitPrice,
          notes: edited?.notes
        };
      });

      await negotiationService.completeNegotiation(negotiation.id, { items, notes });
      toast.success('Negotiation completed successfully!');
      onNegotiationComplete?.(calculateTotal());
      onOpenChange(false);
    } catch (error) {
      console.error('Error completing negotiation:', error);
      toast.error('Failed to complete negotiation');
    } finally {
      setSubmitting(false);
    }
  };

  const getStatusBadge = (status: string) => {
    const variants: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
      'Invited': 'secondary',
      'InProgress': 'default',
      'Completed': 'outline',
      'Cancelled': 'destructive'
    };
    return <Badge variant={variants[status] || 'default'}>{status}</Badge>;
  };

  const formatCurrency = (amount: number, currency?: string) => {
    return `${currency || 'ETB'} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-4xl max-h-[90vh]">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Users className="h-5 w-5 text-blue-600" />
            Price Negotiation - {businessPartnerName}
          </DialogTitle>
          <DialogDescription>
            Review and negotiate prices for lot items before final award
          </DialogDescription>
        </DialogHeader>

        {loading ? (
          <div className="flex items-center justify-center py-12">
            <Loader2 className="h-8 w-8 animate-spin text-blue-600" />
          </div>
        ) : negotiation ? (
          <div className="space-y-4">
            {/* Status and Summary */}
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <span className="text-sm text-muted-foreground">Status:</span>
                {getStatusBadge(negotiation.status)}
              </div>
              {negotiation.lotCode && (
                <Badge variant="outline">{negotiation.lotCode}: {negotiation.lotTitle}</Badge>
              )}
            </div>

            {/* Items Table */}
            <ScrollArea className="h-[400px] border rounded-lg">
              <Table>
                <TableHeader>
                  <TableRow className="bg-muted/50">
                    <TableHead className="w-[30%]">Item Description</TableHead>
                    <TableHead className="text-right">Qty</TableHead>
                    <TableHead className="text-right">Original Price</TableHead>
                    <TableHead className="text-right">Original Total</TableHead>
                    <TableHead className="text-right">Negotiated Price</TableHead>
                    <TableHead className="text-right">New Total</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {negotiation.items.map((item) => {
                    const edited = editedItems.get(item.id);
                    const negotiatedPrice = edited?.price ?? item.originalUnitPrice;
                    const newTotal = negotiatedPrice * item.quantity;
                    const saving = item.originalTotalPrice - newTotal;

                    return (
                      <TableRow key={item.id}>
                        <TableCell className="font-medium">
                          <div>{item.itemDescription}</div>
                          <div className="text-xs text-muted-foreground">{item.unitOfMeasure}</div>
                        </TableCell>
                        <TableCell className="text-right">{item.quantity}</TableCell>
                        <TableCell className="text-right">{formatCurrency(item.originalUnitPrice, negotiation.currency)}</TableCell>
                        <TableCell className="text-right">{formatCurrency(item.originalTotalPrice, negotiation.currency)}</TableCell>
                        <TableCell className="text-right">
                          <Input
                            type="number"
                            step="0.01"
                            value={negotiatedPrice}
                            onChange={(e) => handlePriceChange(item.id, parseFloat(e.target.value) || 0)}
                            className="w-32 text-right"
                            disabled={negotiation.status === 'Completed' || negotiation.status === 'Cancelled'}
                          />
                        </TableCell>
                        <TableCell className="text-right">
                          <div>{formatCurrency(newTotal, negotiation.currency)}</div>
                          {saving > 0 && (
                            <div className="text-xs text-green-600">-{formatCurrency(saving, negotiation.currency)}</div>
                          )}
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </ScrollArea>

            {/* Summary */}
            <div className="grid grid-cols-3 gap-4 bg-muted/50 p-4 rounded-lg">
              <div>
                <Label className="text-muted-foreground">Original Total</Label>
                <p className="text-lg font-semibold">{formatCurrency(negotiation.originalAmount, negotiation.currency)}</p>
              </div>
              <div>
                <Label className="text-muted-foreground">Negotiated Total</Label>
                <p className="text-lg font-semibold text-blue-600">{formatCurrency(calculateTotal(), negotiation.currency)}</p>
              </div>
              <div>
                <Label className="text-muted-foreground">Total Savings</Label>
                <p className={`text-lg font-semibold ${calculateSavings() > 0 ? 'text-green-600' : 'text-red-600'}`}>
                  {calculateSavings() >= 0 ? '-' : '+'}{formatCurrency(Math.abs(calculateSavings()), negotiation.currency)}
                </p>
              </div>
            </div>

            {/* Notes */}
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <Label>Negotiation Notes</Label>
                {hasUnsavedChanges && (
                  <span className="text-xs text-amber-600">Unsaved changes</span>
                )}
              </div>
              <Textarea
                value={notes}
                onChange={(e) => handleNotesChange(e.target.value)}
                placeholder="Add notes about the negotiation..."
                rows={3}
                disabled={negotiation.status === 'Completed' || negotiation.status === 'Cancelled'}
              />
            </div>
          </div>
        ) : (
          <div className="flex items-center justify-center py-12">
            <AlertCircle className="h-8 w-8 text-red-500" />
            <span className="ml-2">Failed to load negotiation details</span>
          </div>
        )}

        <DialogFooter className="gap-2 sm:gap-0">
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={submitting || savingDraft}>
            {negotiation?.status === 'Completed' ? 'Close' : 'Cancel'}
          </Button>
          {negotiation && negotiation.status !== 'Completed' && negotiation.status !== 'Cancelled' && (
            <>
              <Button
                variant="outline"
                onClick={handleSaveDraft}
                disabled={submitting || savingDraft || !hasUnsavedChanges}
                className="border-blue-300 text-blue-700 hover:bg-blue-50"
              >
                {savingDraft ? (
                  <>
                    <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                    Saving...
                  </>
                ) : (
                  <>
                    <Save className="h-4 w-4 mr-2" />
                    Save Draft
                  </>
                )}
              </Button>
              <Button onClick={handleComplete} disabled={submitting || savingDraft} className="bg-green-600 hover:bg-green-700">
                {submitting ? (
                  <>
                    <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                    Completing...
                  </>
                ) : (
                  <>
                    <Check className="h-4 w-4 mr-2" />
                    Complete Negotiation
                  </>
                )}
              </Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
