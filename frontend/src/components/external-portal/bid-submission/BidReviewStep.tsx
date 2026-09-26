'use client';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { CheckCircle, FileText, DollarSign, Package, AlertCircle, Upload } from 'lucide-react';
import { type TenderDetailDto } from '@/services/tenderService';
import { type CreateTenderBidDto, type TenderBidDocumentDto } from '@/services/tenderBidService';

interface BidReviewStepProps {
  tender: TenderDetailDto;
  bidData: CreateTenderBidDto;
  uploadedDocuments?: TenderBidDocumentDto[];
  selectedLotIds?: string[];
}

export default function BidReviewStep({ tender, bidData, uploadedDocuments = [], selectedLotIds }: BidReviewStepProps) {
  // Get selected lots
  const selectedLots = selectedLotIds && selectedLotIds.length > 0
    ? tender.lots?.filter(lot => selectedLotIds.includes(lot.id)) || []
    : tender.lots || [];

  const calculateItemTotal = (item: any) => {
    return item.offeredQuantity * item.unitPrice;
  };

  const calculateTotalBidAmount = () => {
    return bidData.items.reduce((sum, item) => sum + calculateItemTotal(item), 0);
  };

  const calculateLotTotal = (lotItems: typeof selectedLots[0]['items']) => {
    return (lotItems || []).reduce((sum, tenderItem) => {
      const bidItem = bidData.items.find(item => item.tenderItemId === tenderItem.id);
      return sum + (bidItem ? calculateItemTotal(bidItem) : 0);
    }, 0);
  };

  // Get proposal documents
  const technicalProposalDoc = uploadedDocuments.find(doc => doc.documentType === 'TechnicalProposal');
  const commercialProposalDoc = uploadedDocuments.find(doc => doc.documentType === 'CommercialProposal');

  return (
    <div className="space-y-6">
      {/* Summary */}
      <Card className="bg-blue-50 border-blue-200">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-blue-900">
            <CheckCircle className="h-5 w-5" />
            Review Your Bid
          </CardTitle>
          <CardDescription className="text-blue-700">
            Please review all information carefully before submitting
          </CardDescription>
        </CardHeader>
      </Card>

      {/* Tender Information */}
      <Card>
        <CardHeader>
          <CardTitle>Tender Information</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <p className="text-sm text-gray-500">Tender Number</p>
              <p className="font-medium">{tender.tenderNumber}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Tender Title</p>
              <p className="font-medium">{tender.title}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Tender Type</p>
              <Badge>{tender.tenderType}</Badge>
            </div>

          </div>
        </CardContent>
      </Card>

      {/* Bid Lots */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Package className="h-5 w-5" />
            Bid Lots ({selectedLots.length} lot{selectedLots.length !== 1 ? 's' : ''}, {bidData.items.length} item{bidData.items.length !== 1 ? 's' : ''})
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-6">
          {selectedLots.map((lot) => (
            <div key={lot.id} className="border rounded-lg overflow-hidden">
              {/* Lot Header */}
              <div className="bg-gray-50 px-4 py-3 border-b flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Badge variant="outline" className="font-mono">{lot.lotCode}</Badge>
                  <span className="font-medium">{lot.title}</span>
                </div>
                <div className="font-bold text-green-600">
                  {tender.currency} {calculateLotTotal(lot.items).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                </div>
              </div>
              {/* Lot Items */}
              <div className="overflow-x-auto">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-[50px]">#</TableHead>
                      <TableHead className="min-w-[250px]">Item Description</TableHead>
                      <TableHead className="min-w-[120px]">Quantity</TableHead>
                      <TableHead className="min-w-[150px]">Unit Price</TableHead>
                      <TableHead className="min-w-[150px]">Total</TableHead>
                      <TableHead className="min-w-[120px]">Delivery</TableHead>
                      <TableHead className="min-w-[150px]">Brand/Model</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(lot.items || []).map((tenderItem, index) => {
                      const bidItem = bidData.items.find(item => item.tenderItemId === tenderItem.id);
                      if (!bidItem) return null;
                      return (
                        <TableRow key={tenderItem.id}>
                          <TableCell>{index + 1}</TableCell>
                          <TableCell>
                            <div>
                              <p className="font-medium">{tenderItem.description}</p>
                              {tenderItem.specifications && (
                                <p className="text-xs text-gray-500">{tenderItem.specifications}</p>
                              )}
                            </div>
                          </TableCell>
                          <TableCell>{bidItem.offeredQuantity} {tenderItem.unitOfMeasure || ''}</TableCell>
                          <TableCell>
                            {tender.currency} {bidItem.unitPrice.toLocaleString()}
                          </TableCell>
                          <TableCell className="font-bold text-green-600">
                            {tender.currency} {calculateItemTotal(bidItem).toLocaleString()}
                          </TableCell>
                          <TableCell>{bidItem.deliveryDays ? `${bidItem.deliveryDays} days` : 'N/A'}</TableCell>
                          <TableCell>
                            {bidItem.brand && (
                              <div className="text-sm">
                                <p className="font-medium">{bidItem.brand}</p>
                                {bidItem.model && <p className="text-gray-500">{bidItem.model}</p>}
                              </div>
                            )}
                            {!bidItem.brand && <span className="text-gray-400">-</span>}
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </div>
            </div>
          ))}

          <div className="flex justify-end">
            <div className="bg-green-50 border border-green-200 rounded-lg p-6 min-w-[450px] max-w-[600px]">
              <div className="space-y-3">
                <div className="flex items-center justify-between text-sm text-gray-600">
                  <span>Total Lots:</span>
                  <span className="font-medium">{selectedLots.length}</span>
                </div>
                <div className="flex items-center justify-between text-sm text-gray-600">
                  <span>Total Items:</span>
                  <span className="font-medium">{bidData.items.length}</span>
                </div>
                <div className="border-t border-green-200 pt-3 mt-2">
                  <div className="space-y-2">
                    <p className="text-base font-semibold text-gray-900">Total Bid Amount:</p>
                    <p className="text-3xl font-bold text-green-600 break-words">
                      {tender.currency} {calculateTotalBidAmount().toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                    </p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Proposals */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <FileText className="h-5 w-5" />
            Proposals
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div>
            <p className="font-medium mb-2">Technical Proposal</p>
            {technicalProposalDoc ? (
              <div className="flex items-center gap-3 p-3 bg-green-50 border border-green-200 rounded">
                <CheckCircle className="h-5 w-5 text-green-600" />
                <div>
                  <p className="text-sm font-medium text-green-900">{technicalProposalDoc.documentName}</p>
                  <p className="text-xs text-green-700">
                    Document uploaded • {technicalProposalDoc.fileSize ? `${(technicalProposalDoc.fileSize / 1024).toFixed(1)} KB` : 'Unknown size'}
                  </p>
                </div>
              </div>
            ) : (
              <div className="bg-gray-50 p-4 rounded border">
                <p className="text-sm whitespace-pre-wrap">
                  {bidData.technicalProposal || 'No technical proposal provided'}
                </p>
              </div>
            )}
          </div>

          <div>
            <p className="font-medium mb-2">Commercial Proposal</p>
            {commercialProposalDoc ? (
              <div className="flex items-center gap-3 p-3 bg-green-50 border border-green-200 rounded">
                <CheckCircle className="h-5 w-5 text-green-600" />
                <div>
                  <p className="text-sm font-medium text-green-900">{commercialProposalDoc.documentName}</p>
                  <p className="text-xs text-green-700">
                    Document uploaded • {commercialProposalDoc.fileSize ? `${(commercialProposalDoc.fileSize / 1024).toFixed(1)} KB` : 'Unknown size'}
                  </p>
                </div>
              </div>
            ) : (
              <div className="bg-gray-50 p-4 rounded border">
                <p className="text-sm whitespace-pre-wrap">
                  {bidData.commercialProposal || 'No commercial proposal provided'}
                </p>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Uploaded Documents */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Upload className="h-5 w-5" />
            Uploaded Documents ({uploadedDocuments.length})
          </CardTitle>
        </CardHeader>
        <CardContent>
          {uploadedDocuments.length === 0 ? (
            <p className="text-center py-8 text-gray-500">No documents uploaded</p>
          ) : (
            <div className="space-y-2">
              {uploadedDocuments.map((doc) => (
                <div key={doc.id} className="flex items-center justify-between p-3 bg-green-50 border border-green-200 rounded">
                  <div className="flex items-center gap-3">
                    <CheckCircle className="h-5 w-5 text-green-600" />
                    <div>
                      <p className="font-medium text-sm">{doc.documentName}</p>
                      {doc.tenderBidItemId && <p className="text-xs text-gray-600">Lot {doc.lotNumber}: {doc.itemDescription}</p>}
                      <p className="text-xs text-gray-500">
                        {doc.documentType} • {doc.fileSize ? `${(doc.fileSize / 1024).toFixed(1)} KB` : 'Unknown size'}
                      </p>
                    </div>
                  </div>
                  <Badge className="bg-green-600">Uploaded</Badge>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Terms */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <DollarSign className="h-5 w-5" />
            Terms & Conditions
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <p className="text-sm text-gray-500">Delivery Period</p>
              <p className="font-medium">{bidData.deliveryDays ? `${bidData.deliveryDays} days` : 'Not specified'}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Payment Terms</p>
              <p className="font-medium">{bidData.paymentTerms || 'Not specified'}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Warranty Terms</p>
              <p className="font-medium">{bidData.warrantyTerms || 'Not specified'}</p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Warning */}
      <Card className="bg-yellow-50 border-yellow-200">
        <CardContent className="pt-6">
          <div className="flex items-start gap-3">
            <AlertCircle className="h-5 w-5 text-yellow-600 mt-0.5" />
            <div>
              <p className="font-medium text-yellow-900">Important Notice</p>
              <p className="text-sm text-yellow-700 mt-1">
                Once submitted, your bid cannot be modified. Please ensure all information is accurate
                and complete before submitting. You can save as draft if you need more time.
              </p>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
