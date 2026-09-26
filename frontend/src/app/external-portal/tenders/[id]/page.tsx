'use client';
// Cache buster: 2024-12-03-v2
import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  ArrowLeft,
  FileText,
  Calendar,
  DollarSign,
  Clock,
  AlertCircle,
  Download,
  Send,
  Eye,
  Package,
  FileCheck,
  Info,
  MessageSquare,
  MailCheck,
} from 'lucide-react';
import { toast } from 'sonner';
import { tenderService, type TenderDetailDto } from '@/services/tenderService';
import * as tenderBidService from '@/services/tenderBidService';
import { format, formatDistanceToNow } from 'date-fns';
import { TenderClarifications } from '@/components/procurement/tenders/TenderClarifications';
import { getSupplierTenderBidPath } from '@/lib/tender-bid-routing';

export default function ExternalTenderDetailPage() {
  const params = useParams();
  const router = useRouter();
  const tenderId = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';

  const [tender, setTender] = useState<TenderDetailDto | null>(null);
  const [myBid, setMyBid] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [loadingBid, setLoadingBid] = useState(true);

  useEffect(() => {
    const loadTender = async () => {
      try {
        setLoading(true);
        const data = await tenderService.getTenderById(tenderId);
        console.log('External Portal - Tender Data:', data);
        console.log('External Portal - Required Documents:', data.requiredDocuments);
        setTender(data);
      } catch (error) {
        console.error('Error loading tender:', error);
        toast.error('Failed to load tender details');
      } finally {
        setLoading(false);
      }
    };

    const loadMyBid = async () => {
      try {
        setLoadingBid(true);
        // Get my bids for this tender
        const bids = await tenderBidService.getMyBids();
        // Filter to find bid for this specific tender
        const myBidForThisTender = bids.find(bid => bid.tenderId === tenderId);
        if (myBidForThisTender) {
          setMyBid(myBidForThisTender);
        }
      } catch (error) {
        console.error('Error loading my bid:', error);
        // Don't show error toast - it's okay if there's no bid yet
      } finally {
        setLoadingBid(false);
      }
    };

    if (tenderId) {
      loadTender();
      loadMyBid();
    }
  }, [tenderId]);

  const handleStartBid = () => {
    if (!tender) return;
    if (!tender.submissionDeadline) {
      toast.error('Tender submission deadline is not available');
      return;
    }

    if (new Date(tender.submissionDeadline) < new Date()) {
      toast.error('Tender submission deadline has passed');
      return;
    }

    // Always navigate to bid initiation page
    // The initiation page will auto-redirect to submit-bid if already complete
    router.push(`/external-portal/tenders/${tenderId}/initiate-bid`);
  };

  const handleViewMyBid = () => {
    if (myBid) {
      router.push(getSupplierTenderBidPath(tenderId, myBid));
    }
  };

  const getDeadlineStatus = (deadline?: string) => {
    if (!deadline) {
      return { text: 'Deadline not set', variant: 'secondary' as const, urgent: false };
    }

    const deadlineDate = new Date(deadline);
    const now = new Date();
    const hoursRemaining = (deadlineDate.getTime() - now.getTime()) / (1000 * 60 * 60);

    if (hoursRemaining < 0) {
      return { text: 'Closed', variant: 'destructive' as const, urgent: false };
    } else if (hoursRemaining < 24) {
      return { text: 'Closing in ' + Math.round(hoursRemaining) + ' hours', variant: 'destructive' as const, urgent: true };
    } else if (hoursRemaining < 72) {
      return { text: formatDistanceToNow(deadlineDate, { addSuffix: true }), variant: 'default' as const, urgent: true };
    } else {
      return { text: formatDistanceToNow(deadlineDate, { addSuffix: true }), variant: 'secondary' as const, urgent: false };
    }
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return 'N/A';
    try {
      return format(new Date(dateString), 'PPP');
    } catch {
      return dateString;
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <Clock className="h-12 w-12 animate-spin mx-auto mb-4 text-blue-500" />
          <p className="text-lg text-gray-600">Loading tender details...</p>
        </div>
      </div>
    );
  }

  if (!tender) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <AlertCircle className="h-12 w-12 mx-auto mb-4 text-red-500" />
          <p className="text-lg text-gray-600">Tender not found</p>
          <Button onClick={() => router.push('/external-portal/tenders')} className="mt-4">
            Back to Tenders
          </Button>
        </div>
      </div>
    );
  }

  const isExpired = tender.submissionDeadline ? new Date(tender.submissionDeadline) < new Date() : false;
  const deadlineStatus = getDeadlineStatus(tender.submissionDeadline);
  const canAskQuestions =
    !myBid &&
    (tender.submissionDeadline
      ? new Date(tender.submissionDeadline) > new Date()
      : false);

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" onClick={() => router.push('/external-portal/tenders')}>
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold">{tender.title}</h1>
            <p className="text-gray-500 font-mono">{tender.tenderNumber}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            onClick={() =>
              router.push(
                `/external-portal/tenders/${tenderId}/document-controls`
              )
            }
          >
            <FileCheck className="h-4 w-4 mr-2" />
            Controlled documents
          </Button>
          <Button
            variant="outline"
            onClick={() =>
              router.push(`/external-portal/tenders/${tenderId}/award-status`)
            }
          >
            <MailCheck className="h-4 w-4 mr-2" />
            Award status
          </Button>
          <Badge>{tender.tenderType}</Badge>
          <Badge variant={deadlineStatus.variant}>{deadlineStatus.text}</Badge>
        </div>
      </div>

      {/* Urgent Notice */}
      {deadlineStatus.urgent && !isExpired && (
        <Card className="border-orange-300 bg-orange-50">
          <CardContent className="pt-6">
            <div className="flex items-center gap-3">
              <AlertCircle className="h-6 w-6 text-orange-600" />
              <div>
                <p className="font-medium text-orange-900">Deadline Approaching!</p>
                <p className="text-sm text-orange-700">
                  This tender closes {deadlineStatus.text}. Submit your bid soon to avoid missing the deadline.
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* My Bid Status */}
      {!loadingBid && myBid && (
        <Card className="border-blue-300 bg-blue-50">
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-3">
                <FileCheck className="h-6 w-6 text-blue-600" />
                <div>
                  <p className="font-medium text-blue-900">You have submitted a bid</p>
                  <p className="text-sm text-blue-700">
                    Bid Number: {myBid.bidNumber} • Status: {myBid.status}
                  </p>
                </div>
              </div>
              <Button variant="outline" onClick={handleViewMyBid}>
                <Eye className="h-4 w-4 mr-2" />
                View My Bid
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Quick Info */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Tender Type</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-xl font-bold">{tender.tenderType}</p>
            <p className="text-xs text-gray-500 mt-1">
              {tender.tenderType === 'RFQ' && 'Request for Quotation'}
              {tender.tenderType === 'RFP' && 'Request for Proposal'}
              {tender.tenderType === 'ITB' && 'Invitation to Bid'}
              {tender.tenderType === 'EOI' && 'Expression of Interest'}
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Submission Deadline</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm font-bold">{formatDate(tender.submissionDeadline)}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Opening Date</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm font-bold">{formatDate(tender.openingDate)}</p>
          </CardContent>
        </Card>
      </div>

      {/* Tabs */}
      <Tabs defaultValue="overview" className="space-y-4">
        <TabsList>
          <TabsTrigger value="overview">
            <Info className="h-4 w-4 mr-2" />
            Overview
          </TabsTrigger>
          <TabsTrigger value="items">
            <Package className="h-4 w-4 mr-2" />
            Lots ({tender.items?.length || 0})
          </TabsTrigger>
          <TabsTrigger value="proposals">
            <FileCheck className="h-4 w-4 mr-2" />
            Proposals
          </TabsTrigger>
          <TabsTrigger value="documents">
            <FileText className="h-4 w-4 mr-2" />
            Documents ({tender.documents?.length || 0})
          </TabsTrigger>
          <TabsTrigger value="fees">
            <DollarSign className="h-4 w-4 mr-2" />
            Fees ({tender.fees?.length || 0})
          </TabsTrigger>
          <TabsTrigger value="clarifications">
            <MessageSquare className="h-4 w-4 mr-2" />
            Clarifications ({tender.clarifications?.length || 0})
          </TabsTrigger>
          <TabsTrigger value="terms">
            <FileCheck className="h-4 w-4 mr-2" />
            Terms & Conditions
          </TabsTrigger>
        </TabsList>

        {/* Overview Tab */}
        <TabsContent value="overview">
          <Card>
            <CardHeader>
              <CardTitle>Tender Description</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <p className="text-gray-700 whitespace-pre-wrap">
                {tender.description || 'No description provided'}
              </p>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4 pt-4 border-t">
                <div>
                  <p className="text-sm text-gray-500">Published Date</p>
                  <p className="font-medium">{formatDate(tender.publishDate)}</p>
                </div>
                <div>
                  <p className="text-sm text-gray-500">Closing Date</p>
                  <p className="font-medium">{formatDate(tender.submissionDeadline)}</p>
                </div>
                <div>
                  <p className="text-sm text-gray-500">Allows Partial Bids</p>
                  <p className="font-medium">{tender.allowPartialBids ? 'Yes' : 'No'}</p>
                </div>
                {/* TODO: Temporarily hidden - Requires Prequalification feature */}
                {/* <div>
                  <p className="text-sm text-gray-500">Requires Prequalification</p>
                  <p className="font-medium">{tender.requiresPrequalification ? 'Yes' : 'No'}</p>
                </div> */}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* Lots Tab */}
        <TabsContent value="items">
          <Card>
            <CardHeader>
              <CardTitle>Tender Lots</CardTitle>
              <CardDescription>{tender.lots?.length || 0} lot(s) available for bidding</CardDescription>
            </CardHeader>
            <CardContent>
              {tender.lots && tender.lots.length > 0 ? (
                <div className="space-y-4">
                  {tender.lots.map((lot) => (
                    <div key={lot.id} className="border rounded-lg overflow-hidden">
                      {/* Lot Header */}
                      <div className="bg-gray-50 px-4 py-3 border-b">
                        <div className="flex justify-between items-start">
                          <div>
                            <h4 className="font-medium">{lot.lotCode}: {lot.title}</h4>
                            {lot.description && (
                              <p className="text-sm text-gray-500 mt-1">{lot.description}</p>
                            )}
                          </div>
                          <div className="text-right text-sm">
                            <p className="text-gray-500">{lot.items?.length || 0} item(s)</p>
                          </div>
                        </div>
                      </div>
                      {/* Lot Items */}
                      {lot.items && lot.items.length > 0 && (
                        <Table>
                          <TableHeader>
                            <TableRow>
                              <TableHead className="w-16">#</TableHead>
                              <TableHead>Item Code</TableHead>
                              <TableHead>Description</TableHead>
                              <TableHead>Quantity</TableHead>
                              <TableHead>Unit</TableHead>
                              <TableHead>Specifications</TableHead>
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {lot.items.map((item) => (
                              <TableRow key={item.id}>
                                <TableCell>{item.lineNumber}</TableCell>
                                <TableCell>{item.itemCode || '-'}</TableCell>
                                <TableCell>{item.description}</TableCell>
                                <TableCell>{item.quantity}</TableCell>
                                <TableCell>{item.unitOfMeasure || '-'}</TableCell>
                                <TableCell className="max-w-xs truncate">{item.specifications || '-'}</TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                      )}
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-center text-gray-500 py-8">No lots specified</p>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Proposal Tab */}
        <TabsContent value="proposals">
          <Card>
            <CardHeader>
              <CardTitle>Proposal Template</CardTitle>
              <CardDescription>Download this template to prepare your technical and commercial proposals</CardDescription>
            </CardHeader>
            <CardContent>
              {(() => {
                // Support both new 'ProposalTemplate' and legacy 'TechnicalProposalTemplate' for backward compatibility
                const proposalTemplate = tender.documents?.find(doc => doc.documentType === 'ProposalTemplate')
                  || tender.documents?.find(doc => doc.documentType === 'TechnicalProposalTemplate');

                if (!proposalTemplate) {
                  return <p className="text-center text-gray-500 py-8">No proposal template available</p>;
                }

                return (
                  <div className="max-w-md mx-auto">
                    {/* Proposal Template */}
                    <Card className="border-2 border-blue-200 bg-blue-50/50">
                      <CardHeader className="pb-3">
                        <div className="flex items-center gap-2">
                          <FileText className="h-5 w-5 text-blue-600" />
                          <CardTitle className="text-lg">Proposal Template</CardTitle>
                        </div>
                        <CardDescription>
                          Use this template to prepare your technical and commercial proposals
                        </CardDescription>
                      </CardHeader>
                      <CardContent>
                        <div className="space-y-3">
                          <p className="text-sm font-medium">{proposalTemplate.documentName}</p>
                          <Button
                            variant="outline"
                            className="w-full"
                            onClick={() => tenderService.downloadTenderDocument(tenderId, proposalTemplate.id, proposalTemplate.documentName)}
                          >
                            <Download className="h-4 w-4 mr-2" />
                            Download Template
                          </Button>
                        </div>
                      </CardContent>
                    </Card>
                  </div>
                );
              })()}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Documents Tab */}
        <TabsContent value="documents" className="space-y-4">
          {/* Required Documents Section */}
          <Card>
            <CardHeader>
              <CardTitle>Required Documents for Bid Submission</CardTitle>
              <CardDescription>
                {(() => {
                  try {
                    const requirements = tender.requiredDocuments ? JSON.parse(tender.requiredDocuments) : [];
                    const requiredCount = requirements.filter((r: any) => r.isRequired).length;
                    const optionalCount = requirements.length - requiredCount;
                    return requirements.length > 0
                      ? `${requirements.length} document(s) - ${requiredCount} required, ${optionalCount} optional`
                      : 'No specific document requirements';
                  } catch {
                    return 'No specific document requirements';
                  }
                })()}
              </CardDescription>
            </CardHeader>
            <CardContent>
              {(() => {
                try {
                  const requirements = tender.requiredDocuments ? JSON.parse(tender.requiredDocuments) : [];

                  if (requirements.length === 0) {
                    return <p className="text-center py-8 text-gray-500">No specific document requirements for this tender</p>;
                  }

                  return (
                    <div className="space-y-3">
                      {requirements.map((req: any, index: number) => (
                        <div
                          key={index}
                          className="border rounded-lg p-4 bg-gray-50"
                        >
                          <div className="flex items-start justify-between">
                            <div className="flex-1">
                              <div className="flex items-center gap-2">
                                <FileText className="h-5 w-5 text-blue-600" />
                                <p className="font-medium">{req.documentName}</p>
                                <Badge variant={req.isRequired ? 'destructive' : 'secondary'} className="text-xs">
                                  {req.isRequired ? 'Required' : 'Optional'}
                                </Badge>
                              </div>
                              <p className="text-sm text-gray-600 mt-2">
                                Type: <span className="font-medium">{req.documentType}</span>
                              </p>
                              {req.description && (
                                <p className="text-sm text-gray-500 mt-1">{req.description}</p>
                              )}
                              <p className="text-xs text-gray-500 mt-2">
                                Max Size: {req.maxFileSizeMB || 20}MB • Allowed Types: {req.allowedFileTypes || 'PDF'}
                              </p>
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>
                  );
                } catch (error) {
                  console.error('Error parsing document requirements:', error);
                  return <p className="text-center py-8 text-red-500">Error loading document requirements</p>;
                }
              })()}
            </CardContent>
          </Card>

          {/* Tender Documents Section */}
          <Card>
            <CardHeader>
              <CardTitle>Tender Documents</CardTitle>
              <CardDescription>Download tender documents and specifications</CardDescription>
            </CardHeader>
            <CardContent>
              {(() => {
                // Filter out AcceptanceDeclaration (shown separately in bid initiation flow) and proposal templates (shown in Proposal tab)
                const tenderDocs = tender.documents?.filter(doc =>
                  doc.documentType !== 'AcceptanceDeclaration' &&
                  doc.documentType !== 'ProposalTemplate' &&
                  doc.documentType !== 'TechnicalProposalTemplate' &&
                  doc.documentType !== 'CommercialProposalTemplate'
                ) || [];

                return tenderDocs.length > 0 ? (
                  <div className="space-y-2">
                    {tenderDocs.map((doc) => (
                      <div
                        key={doc.id}
                        className="flex items-center justify-between p-3 border rounded-lg hover:bg-gray-50"
                      >
                        <div className="flex items-center gap-3">
                          <FileText className="h-5 w-5 text-blue-600" />
                          <div>
                            <p className="font-medium">{doc.documentName}</p>
                            <p className="text-sm text-gray-500">{doc.documentType}</p>
                          </div>
                        </div>
                        <Button variant="outline" size="sm">
                          <Download className="h-4 w-4 mr-2" />
                          Download
                        </Button>
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-center text-gray-500 py-8">No documents available</p>
                );
              })()}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Fees Tab */}
        <TabsContent value="fees">
          <Card>
            <CardHeader>
              <CardTitle>Tender Fees</CardTitle>
              <CardDescription>
                {tender.fees?.length || 0} fee(s) - Please ensure all mandatory fees are paid before the deadline
              </CardDescription>
            </CardHeader>
            <CardContent>
              {!tender.fees || tender.fees.length === 0 ? (
                <p className="text-center py-8 text-gray-500">No fees required for this tender</p>
              ) : (
                <div className="space-y-4">
                  {tender.fees.map((fee) => (
                    <Card key={fee.id} className="border-2">
                      <CardHeader className="pb-3">
                        <div className="flex items-start justify-between">
                          <div className="space-y-1">
                            <div className="flex items-center gap-2">
                              <Badge className="text-sm">{fee.feeType}</Badge>
                              {fee.isMandatory ? (
                                <Badge variant="destructive">Required</Badge>
                              ) : (
                                <Badge variant="outline">Optional</Badge>
                              )}
                            </div>
                            <CardTitle className="text-2xl font-bold text-green-600">
                              {fee.currency} {fee.amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                            </CardTitle>
                          </div>
                          {fee.dueDate && (
                            <div className="text-right">
                              <p className="text-sm text-gray-500">Due Date</p>
                              <p className="font-semibold">{format(new Date(fee.dueDate), 'PP')}</p>
                              <p className="text-xs text-gray-500">
                                ({formatDistanceToNow(new Date(fee.dueDate), { addSuffix: true })})
                              </p>
                            </div>
                          )}
                        </div>
                        {fee.description && (
                          <CardDescription className="mt-2">{fee.description}</CardDescription>
                        )}
                      </CardHeader>
                      <CardContent>
                        <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
                          <div className="flex items-center gap-2 mb-3">
                            <DollarSign className="h-5 w-5 text-blue-600" />
                            <h4 className="font-semibold text-blue-900">Payment Details</h4>
                          </div>
                          <div className="space-y-2">
                            <div className="flex items-start gap-2">
                              <span className="text-sm font-medium text-blue-900 min-w-[120px]">Payment Method:</span>
                              <span className="text-sm text-blue-800 font-semibold">{fee.paymentMethod}</span>
                            </div>
                            {fee.bankAccountDetails && (
                              <div className="flex items-start gap-2">
                                <span className="text-sm font-medium text-blue-900 min-w-[120px]">Payment Details:</span>
                                <div className="text-sm text-blue-800 whitespace-pre-wrap flex-1 bg-white p-3 rounded border border-blue-200 font-mono">
                                  {fee.bankAccountDetails}
                                </div>
                              </div>
                            )}
                            {!fee.bankAccountDetails && (
                              <div className="flex items-start gap-2">
                                <AlertCircle className="h-4 w-4 text-amber-500 mt-0.5" />
                                <span className="text-sm text-amber-700">
                                  Payment details not provided. Please contact the procurement office for payment information.
                                </span>
                              </div>
                            )}
                          </div>
                        </div>
                      </CardContent>
                    </Card>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Clarifications Tab */}
        <TabsContent value="clarifications">
          <TenderClarifications
            tenderId={tenderId}
            canAskQuestions={canAskQuestions}
          />
        </TabsContent>

        {/* Terms Tab */}
        <TabsContent value="terms">
          <Card>
            <CardHeader>
              <CardTitle>Terms & Conditions</CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-gray-700 whitespace-pre-wrap">
                {tender.termsAndConditions || 'No terms and conditions specified'}
              </p>
              {tender.bidValidityPeriodDays != null && <p className="mt-2">Bid validity: {tender.bidValidityPeriodDays} calendar days from submission closing.</p>}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Action Buttons */}
      <Card>
        <CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div>
              <p className="font-medium">Ready to submit your bid?</p>
              <p className="text-sm text-gray-500">
                Review all tender details before submitting your bid
              </p>
            </div>
            <div className="flex gap-2">
              {!myBid && !isExpired && (
                <Button onClick={handleStartBid} size="lg">
                  <Send className="h-4 w-4 mr-2" />
                  Bid
                </Button>
              )}
              {myBid && myBid.status === 'Draft' && (
                <Button onClick={handleViewMyBid} size="lg">
                  <Send className="h-4 w-4 mr-2" />
                  Continue Bid
                </Button>
              )}
              {myBid && myBid.status !== 'Draft' && (
                <Button variant="outline" onClick={handleViewMyBid}>
                  <Eye className="h-4 w-4 mr-2" />
                  View My Bid
                </Button>
              )}
              {isExpired && (
                <Button disabled size="lg">
                  Tender Closed
                </Button>
              )}
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
