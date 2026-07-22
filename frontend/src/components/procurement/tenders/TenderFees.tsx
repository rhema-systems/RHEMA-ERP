 
'use client';

import { useState } from 'react';
import { type TenderFormData } from '@/app/procurement/tenders/new/page';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Plus, Trash2, DollarSign, Edit2, X } from 'lucide-react';
import { toast } from 'sonner';
import { tenderService, type CreateTenderFeeDto, type TenderFeeDto } from '@/services/tenderService';

interface TenderFeesProps {
  formData: TenderFormData;
  updateFormData: (data: Partial<TenderFormData>) => void;
  tenderId: string | null;
}

interface FeeFormData {
  id?: string;
  feeType: string;
  amount: number | null;
  currency: string;
  paymentMethod: string;
  isMandatory: boolean;
  dueDate: string;
  description: string;
  bankAccountDetails: string;
}

const FEE_TYPES = [
  { value: 'DocumentFee', label: 'Document Fee' },
  { value: 'BidBond', label: 'Bid Bond' },
  { value: 'PerformanceBond', label: 'Performance Bond' },
  { value: 'TenderFee', label: 'Tender Fee' },
  { value: 'ProcessingFee', label: 'Processing Fee' },
  { value: 'Other', label: 'Other' },
];

const CURRENCIES = ['USD', 'GHS', 'EUR', 'GBP', 'KES', 'NGN', 'ZAR'];

const PAYMENT_METHODS = [
  { value: 'Online', label: 'Online Payment' },
  { value: 'BankTransfer', label: 'Bank Transfer' },
  { value: 'Cash', label: 'Cash' },
  { value: 'Cheque', label: 'Cheque' },
  { value: 'MobileMoney', label: 'Mobile Money' },
];

export default function TenderFees({ formData, updateFormData, tenderId }: TenderFeesProps) {
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [currentFee, setCurrentFee] = useState<FeeFormData>({
    feeType: 'DocumentFee',
    amount: null,
    currency: 'GHS',
    paymentMethod: 'Online',
    isMandatory: true,
    dueDate: '',
    description: '',
    bankAccountDetails: '',
  });

  const handleAddFee = async () => {
    if (!currentFee.feeType || !currentFee.amount || currentFee.amount <= 0) {
      toast.error('Fee type and amount are required');
      return;
    }

    console.log('TenderFees - handleAddFee called');
    console.log('TenderFees - tenderId:', tenderId);
    console.log('TenderFees - currentFee:', currentFee);
    console.log('TenderFees - editingIndex:', editingIndex);

    if (tenderId) {
      // Save via API
      try {
        const amount = currentFee.amount ?? 0;
        const feeDto: CreateTenderFeeDto = {
          feeType: currentFee.feeType,
          amount,
          currency: currentFee.currency,
          paymentMethod: currentFee.paymentMethod,
          isMandatory: currentFee.isMandatory,
          dueDate: currentFee.dueDate || undefined,
          description: currentFee.description || undefined,
          bankAccountDetails: currentFee.bankAccountDetails || undefined,
        };

        if (editingIndex !== null && currentFee.id) {
          // Update existing fee
          console.log('TenderFees - Updating fee via API, feeId:', currentFee.id);
          const updated = await tenderService.updateTenderFee(tenderId, currentFee.id, feeDto);
          const updatedFees = [...formData.fees];
          updatedFees[editingIndex] = {
            id: updated.id,
            feeType: updated.feeType,
            amount: updated.amount,
            currency: updated.currency,
            paymentMethod: updated.paymentMethod,
            isMandatory: updated.isMandatory,
            dueDate: updated.dueDate ? new Date(updated.dueDate).toISOString().slice(0, 16) : '',
            description: updated.description || '',
            bankAccountDetails: updated.bankAccountDetails || '',
          };
          updateFormData({ fees: updatedFees });
          toast.success('Fee updated successfully');
        } else {
          // Add new fee
          console.log('TenderFees - Adding fee via API');
          const created = await tenderService.addTenderFee(tenderId, feeDto);
          updateFormData({
            fees: [...formData.fees, {
              id: created.id,
              feeType: created.feeType,
              amount: created.amount,
              currency: created.currency,
              paymentMethod: created.paymentMethod,
              isMandatory: created.isMandatory,
              dueDate: created.dueDate ? new Date(created.dueDate).toISOString().slice(0, 16) : '',
              description: created.description || '',
              bankAccountDetails: created.bankAccountDetails || '',
            }]
          });
          toast.success('Fee added successfully');
        }
      } catch (error: any) {
        console.error('TenderFees - Error saving fee:', error);
        toast.error(error.message || 'Failed to save fee');
        return;
      }
    } else {
      // Save to local state only (no tender yet)
      console.log('TenderFees - No tender yet, saving to local state only');
      const feeToSave = {
        ...currentFee,
        amount: currentFee.amount ?? 0,
      };
      if (editingIndex !== null) {
        const updated = [...formData.fees];
        updated[editingIndex] = feeToSave;
        updateFormData({ fees: updated });
        toast.success('Fee updated');
      } else {
        updateFormData({
          fees: [...formData.fees, feeToSave]
        });
        toast.success('Fee added');
      }
    }

    // Reset form
    setCurrentFee({
      feeType: 'DocumentFee',
      amount: null,
      currency: 'GHS',
      paymentMethod: 'Online',
      isMandatory: true,
      dueDate: '',
      description: '',
      bankAccountDetails: '',
    });
    setEditingIndex(null);
  };

  const handleEditFee = (index: number) => {
    const fee = formData.fees[index];
    console.log('TenderFees - handleEditFee called');
    console.log('TenderFees - fee to edit:', fee);
    console.log('TenderFees - fee.bankAccountDetails:', fee.bankAccountDetails);
    console.log('TenderFees - typeof fee.bankAccountDetails:', typeof fee.bankAccountDetails);
    console.log('TenderFees - index:', index);

    const newCurrentFee = {
      id: fee.id,
      feeType: fee.feeType,
      amount: fee.amount,
      currency: fee.currency,
      paymentMethod: fee.paymentMethod,
      isMandatory: fee.isMandatory,
      dueDate: fee.dueDate || '',
      description: fee.description || '',
      bankAccountDetails: fee.bankAccountDetails || '',
    };

    console.log('TenderFees - newCurrentFee:', newCurrentFee);
    console.log('TenderFees - newCurrentFee.bankAccountDetails:', newCurrentFee.bankAccountDetails);

    setCurrentFee(newCurrentFee);
    setEditingIndex(index);
  };

  const handleDeleteFee = async (index: number) => {
    const fee = formData.fees[index];

    if (tenderId && fee.id) {
      // Delete via API
      try {
        await tenderService.deleteTenderFee(tenderId, fee.id);
        const updated = formData.fees.filter((_, i) => i !== index);
        updateFormData({ fees: updated });
        toast.success('Fee deleted successfully');
      } catch (error: any) {
        console.error('TenderFees - Error deleting fee:', error);
        toast.error(error.message || 'Failed to delete fee');
        return;
      }
    } else {
      // Delete from local state
      const updated = formData.fees.filter((_, i) => i !== index);
      updateFormData({ fees: updated });
      toast.success('Fee removed');
    }

    if (editingIndex === index) {
      setEditingIndex(null);
      setCurrentFee({
        feeType: 'DocumentFee',
        amount: null,
        currency: 'GHS',
        paymentMethod: 'Online',
        isMandatory: true,
        dueDate: '',
        description: '',
        bankAccountDetails: '',
      });
    }
  };

  const handleCancelEdit = () => {
    setEditingIndex(null);
    setCurrentFee({
      feeType: 'DocumentFee',
      amount: null,
      currency: 'GHS',
      paymentMethod: 'Online',
      isMandatory: true,
      dueDate: '',
      description: '',
      bankAccountDetails: '',
    });
  };

  const formatCurrency = (amount: number, currency: string) => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: currency || 'USD',
    }).format(amount);
  };

  const formatDate = (dateString: string) => {
    if (!dateString) return '';
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' });
  };

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold">Tender Fees</h3>
        <p className="text-sm text-gray-500">
          Configure fees that bidders must pay to participate in this tender
        </p>
      </div>

      {/* Fee Form */}
      <Card key={editingIndex !== null ? `edit-${editingIndex}` : 'new'}>
        <CardHeader>
          <CardTitle className="text-base">
            {editingIndex !== null ? 'Edit Fee' : 'Add Fee'}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <Label htmlFor="feeType">Fee Type *</Label>
              <Select
                key={`feeType-${editingIndex}`}
                value={currentFee.feeType}
                onValueChange={(value) => setCurrentFee({ ...currentFee, feeType: value })}
              >
                <SelectTrigger id="feeType">
                  <SelectValue placeholder="Select fee type" />
                </SelectTrigger>
                <SelectContent>
                  {FEE_TYPES.map((type) => (
                    <SelectItem key={type.value} value={type.value}>
                      {type.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div>
              <Label htmlFor="amount">Amount *</Label>
              <Input
                id="amount"
                type="number"
                step="0.01"
                min="0"
                value={currentFee.amount || ''}
                onChange={(e) => setCurrentFee({ ...currentFee, amount: parseFloat(e.target.value) || null })}
                placeholder="0.00"
              />
            </div>

            <div>
              <Label htmlFor="currency">Currency</Label>
              <Select
                key={`currency-${editingIndex}`}
                value={currentFee.currency}
                onValueChange={(value) => setCurrentFee({ ...currentFee, currency: value })}
              >
                <SelectTrigger id="currency">
                  <SelectValue placeholder="Select currency" />
                </SelectTrigger>
                <SelectContent>
                  {CURRENCIES.map((curr) => (
                    <SelectItem key={curr} value={curr}>
                      {curr}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div>
              <Label htmlFor="paymentMethod">Payment Method *</Label>
              <Select
                key={`paymentMethod-${editingIndex}`}
                value={currentFee.paymentMethod}
                onValueChange={(value) => setCurrentFee({ ...currentFee, paymentMethod: value })}
              >
                <SelectTrigger id="paymentMethod">
                  <SelectValue placeholder="Select payment method" />
                </SelectTrigger>
                <SelectContent>
                  {PAYMENT_METHODS.map((method) => (
                    <SelectItem key={method.value} value={method.value}>
                      {method.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div>
              <Label htmlFor="dueDate">Due Date</Label>
              <Input
                id="dueDate"
                type="datetime-local"
                value={currentFee.dueDate}
                onChange={(e) => setCurrentFee({ ...currentFee, dueDate: e.target.value })}
              />
            </div>

            <div className="flex items-center space-x-2">
              <input
                type="checkbox"
                id="isMandatory"
                checked={currentFee.isMandatory}
                onChange={(e) => setCurrentFee({ ...currentFee, isMandatory: e.target.checked })}
                className="h-4 w-4 rounded border-gray-300"
              />
              <Label htmlFor="isMandatory" className="font-normal">
                Mandatory Fee
              </Label>
            </div>
          </div>

          <div>
            <Label htmlFor="description">Description</Label>
            <Textarea
              key={`description-${editingIndex}`}
              id="description"
              value={currentFee.description || ''}
              onChange={(e) => setCurrentFee({ ...currentFee, description: e.target.value })}
              placeholder="Fee description or instructions"
              rows={2}
            />
          </div>

          <div>
            <Label htmlFor="bankAccountDetails">Bank Account Details</Label>
            <Textarea
              key={`bankAccountDetails-${editingIndex}`}
              id="bankAccountDetails"
              value={currentFee.bankAccountDetails || ''}
              onChange={(e) => setCurrentFee({ ...currentFee, bankAccountDetails: e.target.value })}
              placeholder="Bank name, account number, etc."
              rows={2}
            />
          </div>

          <div className="flex gap-2">
            <Button type="button" onClick={handleAddFee} className="flex-1">
              <Plus className="h-4 w-4 mr-2" />
              {editingIndex !== null ? 'Update Fee' : 'Add Fee'}
            </Button>
            {editingIndex !== null && (
              <Button type="button" variant="outline" onClick={handleCancelEdit}>
                <X className="h-4 w-4 mr-2" />
                Cancel
              </Button>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Fees List */}
      {formData.fees.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Configured Fees ({formData.fees.length})</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {formData.fees.map((fee, index) => (
                <div
                  key={index}
                  className={`p-4 border rounded-lg ${
                    editingIndex === index ? 'border-blue-500 bg-blue-50' : 'border-gray-200'
                  }`}
                >
                  <div className="flex items-start justify-between">
                    <div className="flex-1">
                      <div className="flex items-center gap-2">
                        <DollarSign className="h-4 w-4 text-gray-500" />
                        <h4 className="font-medium">{fee.feeType}</h4>
                        {fee.isMandatory && (
                          <span className="px-2 py-0.5 text-xs bg-red-100 text-red-700 rounded">
                            Mandatory
                          </span>
                        )}
                      </div>
                      <div className="mt-2 space-y-1 text-sm text-gray-600">
                        <p className="font-semibold text-lg text-gray-900">
                          {formatCurrency(fee.amount, fee.currency)}
                        </p>
                        <p>Payment Method: {fee.paymentMethod}</p>
                        {fee.dueDate && <p>Due Date: {formatDate(fee.dueDate)}</p>}
                        {fee.description && <p className="text-gray-500">{fee.description}</p>}
                        {fee.bankAccountDetails && (
                          <p className="text-gray-500">
                            <span className="font-medium">Bank Details:</span> {fee.bankAccountDetails}
                          </p>
                        )}
                      </div>
                    </div>
                    <div className="flex gap-2">
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        onClick={() => handleEditFee(index)}
                      >
                        <Edit2 className="h-4 w-4" />
                      </Button>
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        onClick={() => handleDeleteFee(index)}
                      >
                        <Trash2 className="h-4 w-4 text-red-500" />
                      </Button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {formData.fees.length === 0 && (
        <div className="text-center py-12 text-gray-500 border-2 border-dashed border-gray-300 rounded-lg">
          <DollarSign className="h-12 w-12 mx-auto mb-4 text-gray-400" />
          <p>No fees configured yet</p>
          <p className="text-sm mt-2">Add fees that bidders must pay to participate in this tender</p>
        </div>
      )}
    </div>
  );
}
