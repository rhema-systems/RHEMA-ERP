'use client';

import { useEffect, useState, useRef } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  ArrowLeft,
  FileText,
  Package,
  DollarSign,
  Clock,
  CheckCircle,
  XCircle,
  AlertCircle,
  Eye,
  Info,
  Award,
  Download,
  Trophy,
  Shield,
  Upload,
  Loader2,
  Bell,
  TrendingDown,
} from 'lucide-react';
import { toast } from 'sonner';
import * as tenderBidService from '@/services/tenderBidService';
import { type TenderBidDetailDto } from '@/services/tenderBidService';
import { tenderService, type TenderDetailDto } from '@/services/tenderService';
import * as performanceBondService from '@/services/performanceBondService';
import { type PerformanceBondRequestDto } from '@/services/performanceBondService';
import * as tenderAwardService from '@/services/tenderAwardService';
import { type TenderAwardDto } from '@/services/tenderAwardService';
import { format } from 'date-fns';

export default function BidDetailPage() {
  const params = useParams();
  const router = useRouter();
  const bidId = params.id as string;
  const performanceBondFileRef = useRef<HTMLInputElement>(null);

  const [bid, setBid] = useState<TenderBidDetailDto | null>(null);
  const [tender, setTender] = useState<TenderDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [withdrawing, setWithdrawing] = useState(false);

  // Award state (for showing negotiated pricing)
  const [award, setAward] = useState<TenderAwardDto | null>(null);

  // Performance bond state
  const [performanceBondRequest, setPerformanceBondRequest] = useState<PerformanceBondRequestDto | null>(null);
  const [performanceBondFile, setPerformanceBondFile] = useState<File | null>(null);
  const [uploadingBond, setUploadingBond] = useState(false);
  const [activeTab, setActiveTab] = useState('items');

  useEffect(() => {
    if (bidId) {
      loadBid();
    }
  }, [bidId]);

  const loadBid = async () => {
    try {
      setLoading(true);
      const data = await tenderBidService.getBidById(bidId);
      setBid(data);

      // Load tender details to get required documents
      if (data.tenderId) {
        try {
          const tenderData = await tenderService.getTenderById(data.tenderId);
          setTender(tenderData);
        } catch (error) {
          console.error('Error loading tender:', error);
          // Don't show error - bid details are more important
        }
      }

      // Check for performance bond request and award info
      if (data.status === 'Awarded') {
        try {
          const bondRequest = await performanceBondService.getPerformanceBondByBidId(bidId);
          setPerformanceBondRequest(bondRequest);
        } catch {
          // No performance bond request yet, that's fine
        }

        // Fetch award information to get negotiated pricing
        try {
          const awardData = await tenderAwardService.getAwardByBidId(bidId);
          setAward(awardData);
        } catch {
          // Award info not available, that's fine
        }
      }
    } catch (error) {
      console.error('Error loading bid:', error);
      toast.error('Failed to load bid details');
    } finally {
      setLoading(false);
    }
  };

  const handlePerformanceBondFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      if (file.size > 20 * 1024 * 1024) {
        toast.error('File size must be less than 20MB');
        return;
      }
      setPerformanceBondFile(file);
    }
  };

  const handleUploadPerformanceBond = async () => {
    if (!performanceBondFile || !performanceBondRequest) return;

    try {
      setUploadingBond(true);

      const result = await performanceBondService.submitPerformanceBond(
        performanceBondRequest.id,
        performanceBondFile
      );

      setPerformanceBondRequest(result);
      toast.success('Performance bond document submitted successfully!');
      setPerformanceBondFile(null);
    } catch (error: any) {
      console.error('Error uploading performance bond:', error);
      toast.error(error.message || 'Failed to upload performance bond document');
    } finally {
      setUploadingBond(false);
    }
  };

  const handleDownloadTemplate = async () => {
    if (!performanceBondRequest) return;

    try {
      toast.info('Downloading performance bond template...');
      const blob = await performanceBondService.downloadPerformanceBondTemplate(performanceBondRequest.id);
      performanceBondService.triggerFileDownload(blob, performanceBondRequest.templateFileName || 'performance-bond-template.pdf');
    } catch (error: any) {
      console.error('Error downloading template:', error);
      toast.error(error.message || 'Failed to download template');
    }
  };

  const handleWithdraw = async () => {
    if (!bid) return;

    if (!confirm('Are you sure you want to withdraw this bid? This action cannot be undone.')) {
      return;
    }

    const reason = prompt('Please provide a reason for withdrawal:');
    if (!reason) {
      toast.error('Withdrawal reason is required');
      return;
    }

    try {
      setWithdrawing(true);
      await tenderBidService.withdrawBid(bidId, { reason });
      toast.success('Bid withdrawn successfully');
      loadBid(); // Reload to show updated status
    } catch (error) {
      console.error('Error withdrawing bid:', error);
      toast.error('Failed to withdraw bid');
    } finally {
      setWithdrawing(false);
    }
  };

  const handleViewTender = () => {
    if (bid) {
      router.push(`/external-portal/tenders/${bid.tenderId}`);
    }
  };

  const handleEditBid = () => {
    if (bid) {
      router.push(`/external-portal/tenders/${bid.tenderId}/submit-bid`);
    }
  };

  const getStatusBadge = (status: string) => {
    const statusConfig: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', className: string, icon: any }> = {
      'Draft': { variant: 'secondary', className: 'bg-gray-100 text-gray-800', icon: Clock },
      'Submitted': { variant: 'default', className: 'bg-blue-100 text-blue-800', icon: CheckCircle },
      'Opened': { variant: 'default', className: 'bg-purple-100 text-purple-800', icon: Eye },
      'UnderEvaluation': { variant: 'default', className: 'bg-yellow-100 text-yellow-800', icon: AlertCircle },
      'Accepted': { variant: 'default', className: 'bg-green-100 text-green-800', icon: CheckCircle },
      'Awarded': { variant: 'default', className: 'bg-gradient-to-r from-amber-400 to-yellow-500 text-white', icon: Trophy },
      'Rejected': { variant: 'destructive', className: 'bg-red-100 text-red-800', icon: XCircle },
      'Withdrawn': { variant: 'outline', className: 'bg-gray-100 text-gray-600', icon: XCircle },
    };

    const config = statusConfig[status] || { variant: 'outline' as const, className: '', icon: FileText };
    const Icon = config.icon;

    return (
      <Badge variant={config.variant} className={config.className}>
        <Icon className="h-3 w-3 mr-1" />
        {status}
      </Badge>
    );
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return 'N/A';
    try {
      return format(new Date(dateString), 'PPP');
    } catch {
      return dateString;
    }
  };

  const calculateItemTotal = (item: any) => {
    return item.offeredQuantity * item.unitPrice;
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <Clock className="h-12 w-12 animate-spin mx-auto mb-4 text-blue-500" />
          <p className="text-lg text-gray-600">Loading bid details...</p>
        </div>
      </div>
    );
  }

  if (!bid) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <AlertCircle className="h-12 w-12 mx-auto mb-4 text-red-500" />
          <p className="text-lg text-gray-600">Bid not found</p>
          <Button onClick={() => router.push('/external-portal/my-bids')} className="mt-4">
            Back to My Bids
          </Button>
        </div>
      </div>
    );
  }

  const canWithdraw = bid.status === 'Submitted' || bid.status === 'Draft';

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" onClick={() => router.push('/external-portal/my-bids')}>
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold">Bid Details</h1>
            <p className="text-gray-500 font-mono">{bid.bidNumber}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {getStatusBadge(bid.status)}
        </div>
      </div>

      {/* Award Celebration Banner */}
      {bid.status === 'Awarded' && (
        <div className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-emerald-500 via-green-500 to-teal-600 p-8 text-white shadow-2xl">
          {/* Decorative blur elements */}
          <div className="absolute top-0 right-0 -mt-4 -mr-4 h-40 w-40 rounded-full bg-white/10 blur-3xl" />
          <div className="absolute bottom-0 left-0 -mb-8 -ml-8 h-32 w-32 rounded-full bg-white/10 blur-2xl" />
          <div className="absolute top-1/2 right-10 h-20 w-20 rounded-full bg-yellow-400/20 blur-xl" />

          {/* Animated celebration emojis */}
          <div className="absolute top-4 left-8 text-4xl animate-bounce" style={{ animationDelay: '0s', animationDuration: '2s' }}>✨</div>
          <div className="absolute top-8 right-20 text-3xl animate-bounce" style={{ animationDelay: '0.3s', animationDuration: '2.2s' }}>🎉</div>
          <div className="absolute bottom-6 right-8 text-3xl animate-bounce" style={{ animationDelay: '0.6s', animationDuration: '1.8s' }}>⭐</div>
          <div className="absolute bottom-12 left-16 text-2xl animate-bounce" style={{ animationDelay: '0.4s', animationDuration: '2.1s' }}>🎊</div>
          <div className="absolute top-1/3 left-1/4 text-2xl animate-bounce" style={{ animationDelay: '0.2s', animationDuration: '2.3s' }}>🏅</div>

          <div className="relative z-10 text-center">
            {/* Large Trophy Badge */}
            <div className="mx-auto mb-4 flex h-24 w-24 items-center justify-center rounded-full bg-gradient-to-br from-yellow-300 to-yellow-500 shadow-lg ring-4 ring-white/30 animate-pulse">
              <Trophy className="h-14 w-14 text-yellow-900" />
            </div>

            <h2 className="text-3xl font-bold tracking-tight mb-2">
              🎉 Congratulations! 🎉
            </h2>
            <p className="text-xl text-white/95 mb-2">Your Bid Has Been Awarded!</p>
            <p className="text-white/80 max-w-md mx-auto">
              You have successfully won the contract for this tender.
              Our team will contact you shortly with the next steps.
            </p>

            <Badge className="mt-4 bg-white/20 text-white border-white/30 hover:bg-white/30 text-sm px-4 py-1.5">
              <Trophy className="h-4 w-4 mr-2" />
              Contract Awarded
            </Badge>
          </div>
        </div>
      )}

      {/* Negotiated Pricing Information */}
      {bid.status === 'Awarded' && award && (
        <Card className="border-emerald-200 bg-gradient-to-r from-emerald-50 to-green-50">
          <CardHeader className="pb-3">
            <CardTitle className="flex items-center gap-2 text-emerald-800">
              <DollarSign className="h-5 w-5" />
              Award Pricing Details
              {award.isNegotiated && (
                <Badge className="bg-emerald-600 text-white ml-2">
                  <TrendingDown className="h-3 w-3 mr-1" />
                  Negotiated
                </Badge>
              )}
            </CardTitle>
            <CardDescription className="text-emerald-700">
              {award.isNegotiated
                ? 'Final contract amount after successful negotiation'
                : 'Contract amount as per your original bid'}
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              {/* Original Bid Amount */}
              <div className="bg-white rounded-lg p-4 border border-emerald-100 shadow-sm">
                <p className="text-sm text-gray-500 mb-1">Original Bid Amount</p>
                <p className="text-xl font-bold text-gray-700">
                  {award.currency || bid.currency} {award.originalBidAmount.toLocaleString()}
                </p>
              </div>

              {/* Final Contract Amount */}
              <div className="bg-white rounded-lg p-4 border border-emerald-200 shadow-sm ring-2 ring-emerald-500/20">
                <p className="text-sm text-emerald-600 mb-1 font-medium">Final Contract Amount</p>
                <p className="text-2xl font-bold text-emerald-700">
                  {award.currency || bid.currency} {award.awardedAmount.toLocaleString()}
                </p>
              </div>

              {/* Negotiation Savings (only show if negotiated) */}
              {award.isNegotiated && award.negotiationSavings > 0 && (
                <div className="bg-white rounded-lg p-4 border border-amber-100 shadow-sm">
                  <p className="text-sm text-amber-600 mb-1">Negotiation Adjustment</p>
                  <p className="text-xl font-bold text-amber-700 flex items-center gap-1">
                    <TrendingDown className="h-5 w-5" />
                    {award.currency || bid.currency} {award.negotiationSavings.toLocaleString()}
                  </p>
                  <p className="text-xs text-gray-500 mt-1">
                    ({((award.negotiationSavings / award.originalBidAmount) * 100).toFixed(1)}% reduction)
                  </p>
                </div>
              )}
            </div>

            {/* Award Date */}
            <div className="mt-4 pt-4 border-t border-emerald-100 flex items-center justify-between text-sm">
              <span className="text-gray-500">Award Date:</span>
              <span className="font-medium text-gray-700">{formatDate(award.awardDate)}</span>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Accepted Status Alert (legacy support) */}
      {bid.status === 'Accepted' && (
        <Card className="border-green-300 bg-green-50">
          <CardContent className="pt-6">
            <div className="flex items-center gap-3">
              <Award className="h-6 w-6 text-green-600" />
              <div>
                <p className="font-medium text-green-900">Congratulations! Your bid has been accepted</p>
                <p className="text-sm text-green-700">
                  You will be contacted soon regarding the next steps.
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Rejected Status Alert */}
      {bid.status === 'Rejected' && (
        <Card className="border-red-300 bg-red-50">
          <CardContent className="pt-6">
            <div className="flex items-center gap-3">
              <XCircle className="h-6 w-6 text-red-600" />
              <div>
                <p className="font-medium text-red-900">Your bid was not successful</p>
                <p className="text-sm text-red-700">
                  Thank you for your participation. We encourage you to bid on future tenders.
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Quick Info */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Tender</CardTitle>
          </CardHeader>
          <CardContent>
            <p
              className="font-medium text-blue-600 cursor-pointer hover:underline"
              onClick={handleViewTender}
            >
              {bid.tenderNumber}
            </p>
            <p className="text-sm text-gray-600">{bid.tenderTitle}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Total Bid Amount</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-xl font-bold text-green-600">
              {bid.currency} {bid.totalBidAmount.toLocaleString()}
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Submitted Date</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="font-medium">{formatDate(bid.submittedDate)}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Delivery Period</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="font-medium">{bid.deliveryDays ? `${bid.deliveryDays} days` : 'N/A'}</p>
          </CardContent>
        </Card>
      </div>

      {/* Tabs */}
      <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-4">
        <TabsList>
          <TabsTrigger value="items">
            <Package className="h-4 w-4 mr-2" />
            Lots ({new Set(bid.items?.map(item => item.lotCode).filter(Boolean)).size || 0})
          </TabsTrigger>
          <TabsTrigger value="proposals">
            <FileText className="h-4 w-4 mr-2" />
            Proposals
          </TabsTrigger>
          <TabsTrigger value="terms">
            <DollarSign className="h-4 w-4 mr-2" />
            Terms
          </TabsTrigger>
          <TabsTrigger value="documents">
            <FileText className="h-4 w-4 mr-2" />
            Documents ({bid.documents?.filter(doc => doc.documentType !== 'TechnicalProposal' && doc.documentType !== 'CommercialProposal').length || 0})
          </TabsTrigger>
          <TabsTrigger value="info">
            <Info className="h-4 w-4 mr-2" />
            Information
          </TabsTrigger>
          {/* Performance Bond Tab - shown when awarded and there's a request */}
          {performanceBondRequest && (
            <TabsTrigger value="performance-bond" className="relative">
              <Shield className="h-4 w-4 mr-2" />
              Performance Bond
              {performanceBondRequest.status === 'Pending' && (
                <span className="absolute -top-1 -right-1 flex h-3 w-3">
                  <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-400 opacity-75"></span>
                  <span className="relative inline-flex rounded-full h-3 w-3 bg-red-500"></span>
                </span>
              )}
            </TabsTrigger>
          )}
        </TabsList>

        {/* Bid Lots Tab */}
        <TabsContent value="items">
          <Card>
            <CardHeader>
              <CardTitle>Bid Lots</CardTitle>
              <CardDescription>Lots and pricing in your bid</CardDescription>
            </CardHeader>
            <CardContent>
              {bid.items && bid.items.length > 0 ? (
                <>
                  {/* Group items by lot */}
                  {(() => {
                    // Group items by lotCode
                    const lotGroups = bid.items.reduce((acc, item) => {
                      const lotCode = item.lotCode || 'Unassigned';
                      if (!acc[lotCode]) {
                        acc[lotCode] = [];
                      }
                      acc[lotCode].push(item);
                      return acc;
                    }, {} as Record<string, typeof bid.items>);

                    return Object.entries(lotGroups).map(([lotCode, items]) => (
                      <div key={lotCode} className="mb-6 border rounded-lg overflow-hidden">
                        {/* Lot Header */}
                        <div className="bg-gray-50 px-4 py-3 border-b flex items-center justify-between">
                          <div className="flex items-center gap-2">
                            <Badge variant="outline" className="font-mono">{lotCode}</Badge>
                            <span className="text-sm text-gray-600">{items.length} item(s)</span>
                          </div>
                          <div className="font-bold text-green-600">
                            {bid.currency} {items.reduce((sum, item) => sum + calculateItemTotal(item), 0).toLocaleString()}
                          </div>
                        </div>
                        {/* Lot Items */}
                        <Table>
                          <TableHeader>
                            <TableRow>
                              <TableHead>#</TableHead>
                              <TableHead>Description</TableHead>
                              <TableHead>Quantity</TableHead>
                              <TableHead>Unit Price</TableHead>
                              <TableHead>Total</TableHead>
                              <TableHead>Delivery</TableHead>
                              <TableHead>Brand/Model</TableHead>
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {items.map((item, index) => (
                              <TableRow key={item.id}>
                                <TableCell>{index + 1}</TableCell>
                                <TableCell>
                                  <div>
                                    <p className="font-medium">{item.tenderItemDescription}</p>
                                    {item.specifications && (
                                      <p className="text-sm text-gray-500 whitespace-pre-wrap">{item.specifications}</p>
                                    )}
                                  </div>
                                </TableCell>
                                <TableCell>
                                  {item.offeredQuantity}
                                  {item.unitOfMeasure && <span className="text-gray-500 text-sm ml-1">{item.unitOfMeasure}</span>}
                                </TableCell>
                                <TableCell>
                                  {bid.currency} {item.unitPrice.toLocaleString()}
                                </TableCell>
                                <TableCell className="font-bold text-green-600">
                                  {bid.currency} {calculateItemTotal(item).toLocaleString()}
                                </TableCell>
                                <TableCell>{item.deliveryDays ? `${item.deliveryDays} days` : 'N/A'}</TableCell>
                                <TableCell>
                                  {item.brand || item.model ? `${item.brand || ''} ${item.model || ''}`.trim() : 'N/A'}
                                </TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                      </div>
                    ));
                  })()}

                  <div className="mt-4 flex justify-end">
                    <div className="bg-green-50 border border-green-200 rounded-lg p-4 min-w-64 max-w-md">
                      <div className="flex items-center justify-between gap-4">
                        <span className="text-lg font-medium whitespace-nowrap">Total:</span>
                        <span className="text-2xl font-bold text-green-600 break-all text-right">
                          {bid.currency} {bid.totalBidAmount.toLocaleString()}
                        </span>
                      </div>
                    </div>
                  </div>
                </>
              ) : (
                <div className="text-center py-12">
                  <Package className="h-12 w-12 mx-auto mb-4 text-gray-400" />
                  <p className="text-gray-500 mb-2">No lots selected yet</p>
                  {bid.status === 'Draft' && (
                    <p className="text-sm text-gray-400 mb-4">
                      Continue editing your bid to select lots and enter pricing
                    </p>
                  )}
                  {bid.status === 'Draft' && (
                    <Button onClick={handleEditBid} size="sm">
                      <FileText className="h-4 w-4 mr-2" />
                      Continue Editing
                    </Button>
                  )}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Proposals Tab */}
        <TabsContent value="proposals">
          <div className="space-y-4">
            {/* Technical Proposal */}
            <Card>
              <CardHeader>
                <CardTitle>Technical Proposal</CardTitle>
              </CardHeader>
              <CardContent>
                {(() => {
                  const technicalDoc = bid.documents?.find(doc => doc.documentType === 'TechnicalProposal');

                  if (technicalDoc) {
                    return (
                      <div className="space-y-3">
                        <div className="flex items-center gap-2 text-green-600">
                          <CheckCircle className="h-5 w-5" />
                          <span className="font-medium">Document uploaded</span>
                        </div>
                        <div className="flex items-center justify-between p-4 border rounded-lg bg-green-50 border-green-200">
                          <div className="flex items-center gap-3">
                            <FileText className="h-5 w-5 text-green-600" />
                            <div>
                              <p className="font-medium">{technicalDoc.documentName}</p>
                              <p className="text-xs text-gray-500">
                                Uploaded: {format(new Date(technicalDoc.uploadedDate), 'MMM dd, yyyy')}
                                {technicalDoc.fileSize && ` • ${(technicalDoc.fileSize / 1024).toFixed(1)} KB`}
                              </p>
                            </div>
                          </div>
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => tenderBidService.downloadBidDocument(bid.id, technicalDoc.id, technicalDoc.documentName)}
                          >
                            <Download className="h-4 w-4 mr-2" />
                            Download
                          </Button>
                        </div>
                      </div>
                    );
                  }

                  if (bid.technicalProposal) {
                    return (
                      <div className="space-y-3">
                        <div className="flex items-center gap-2 text-blue-600">
                          <FileText className="h-5 w-5" />
                          <span className="font-medium">Text proposal</span>
                        </div>
                        <div className="p-4 border rounded-lg bg-gray-50">
                          <p className="whitespace-pre-wrap text-gray-700">{bid.technicalProposal}</p>
                        </div>
                      </div>
                    );
                  }

                  return <p className="text-gray-500">No technical proposal provided</p>;
                })()}
              </CardContent>
            </Card>

            {/* Commercial Proposal */}
            <Card>
              <CardHeader>
                <CardTitle>Commercial Proposal</CardTitle>
              </CardHeader>
              <CardContent>
                {(() => {
                  const commercialDoc = bid.documents?.find(doc => doc.documentType === 'CommercialProposal');

                  if (commercialDoc) {
                    return (
                      <div className="space-y-3">
                        <div className="flex items-center gap-2 text-green-600">
                          <CheckCircle className="h-5 w-5" />
                          <span className="font-medium">Document uploaded</span>
                        </div>
                        <div className="flex items-center justify-between p-4 border rounded-lg bg-green-50 border-green-200">
                          <div className="flex items-center gap-3">
                            <FileText className="h-5 w-5 text-green-600" />
                            <div>
                              <p className="font-medium">{commercialDoc.documentName}</p>
                              <p className="text-xs text-gray-500">
                                Uploaded: {format(new Date(commercialDoc.uploadedDate), 'MMM dd, yyyy')}
                                {commercialDoc.fileSize && ` • ${(commercialDoc.fileSize / 1024).toFixed(1)} KB`}
                              </p>
                            </div>
                          </div>
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => tenderBidService.downloadBidDocument(bid.id, commercialDoc.id, commercialDoc.documentName)}
                          >
                            <Download className="h-4 w-4 mr-2" />
                            Download
                          </Button>
                        </div>
                      </div>
                    );
                  }

                  if (bid.commercialProposal) {
                    return (
                      <div className="space-y-3">
                        <div className="flex items-center gap-2 text-blue-600">
                          <FileText className="h-5 w-5" />
                          <span className="font-medium">Text proposal</span>
                        </div>
                        <div className="p-4 border rounded-lg bg-gray-50">
                          <p className="whitespace-pre-wrap text-gray-700">{bid.commercialProposal}</p>
                        </div>
                      </div>
                    );
                  }

                  return <p className="text-gray-500">No commercial proposal provided</p>;
                })()}
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        {/* Terms Tab */}
        <TabsContent value="terms">
          <Card>
            <CardHeader>
              <CardTitle>Terms & Conditions</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div>
                <p className="text-sm text-gray-500">Delivery Period</p>
                <p className="font-medium">{bid.deliveryDays ? `${bid.deliveryDays} days` : 'Not specified'}</p>
              </div>

              <div>
                <p className="text-sm text-gray-500">Payment Terms</p>
                <p className="font-medium whitespace-pre-wrap">
                  {bid.paymentTerms || 'Not specified'}
                </p>
              </div>

              <div>
                <p className="text-sm text-gray-500">Warranty Terms</p>
                <p className="font-medium whitespace-pre-wrap">
                  {bid.warrantyTerms || 'Not specified'}
                </p>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* Documents Tab */}
        <TabsContent value="documents">
          <Card>
            <CardHeader>
              <CardTitle>Supporting Documents</CardTitle>
              <CardDescription>
                {(() => {
                  try {
                    const requirements = tender?.requiredDocuments ? JSON.parse(tender.requiredDocuments) : [];
                    const requiredCount = requirements.filter((r: any) => r.isRequired).length;
                    const optionalCount = requirements.length - requiredCount;
                    // Exclude proposal documents from count
                    const uploadedCount = bid.documents?.filter(
                      doc => doc.documentType !== 'TechnicalProposal' && doc.documentType !== 'CommercialProposal'
                    ).length || 0;
                    return requirements.length > 0
                      ? `${requirements.length} requirement(s) - ${requiredCount} required, ${optionalCount} optional • ${uploadedCount} file(s) uploaded`
                      : `${uploadedCount} file(s) uploaded`;
                  } catch {
                    const uploadedCount = bid.documents?.filter(
                      doc => doc.documentType !== 'TechnicalProposal' && doc.documentType !== 'CommercialProposal'
                    ).length || 0;
                    return `${uploadedCount} file(s) uploaded`;
                  }
                })()}
              </CardDescription>
            </CardHeader>
            <CardContent>
              {(() => {
                try {
                  const requirements = tender?.requiredDocuments ? JSON.parse(tender.requiredDocuments) : [];
                  // Filter out proposal documents - only show required documents
                  const uploadedDocs = (bid.documents || []).filter(
                    doc => doc.documentType !== 'TechnicalProposal' && doc.documentType !== 'CommercialProposal'
                  );

                  if (requirements.length === 0 && uploadedDocs.length === 0) {
                    return <p className="text-center py-8 text-gray-500">No document requirements or uploads</p>;
                  }

                  // If no requirements defined, show all documents in a simple list
                  if (requirements.length === 0) {
                    return (
                      <div className="space-y-3">
                        {uploadedDocs.map((document) => (
                          <div
                            key={document.id}
                            className="flex items-center justify-between p-4 border rounded-lg hover:bg-gray-50"
                          >
                            <div className="flex items-center gap-3">
                              <FileText className="h-5 w-5 text-blue-600" />
                              <div>
                                <p className="font-medium">{document.documentName}</p>
                                <div className="flex items-center gap-3 text-sm text-gray-500">
                                  <span>{document.documentType}</span>
                                  {document.fileSize && (
                                    <>
                                      <span>•</span>
                                      <span>{(document.fileSize / 1024).toFixed(1)} KB</span>
                                    </>
                                  )}
                                </div>
                              </div>
                            </div>
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={() => tenderBidService.downloadBidDocument(bid.id, document.id, document.documentName)}
                            >
                              <Download className="h-4 w-4 mr-2" />
                              Download
                            </Button>
                          </div>
                        ))}
                      </div>
                    );
                  }

                  // Show requirements with their uploaded files
                  return (
                    <div className="space-y-4">
                      {requirements.map((req: any, index: number) => {
                        const matchingDocs = uploadedDocs.filter(doc => doc.documentType === req.documentType);

                        return (
                          <div key={index} className="border rounded-lg overflow-hidden">
                            <div className="bg-gray-50 p-4 border-b">
                              <div className="flex items-start justify-between">
                                <div className="flex-1">
                                  <div className="flex items-center gap-2">
                                    <FileText className="h-5 w-5 text-blue-600" />
                                    <p className="font-medium">{req.documentName}</p>
                                    <Badge variant={req.isRequired ? 'destructive' : 'secondary'} className="text-xs">
                                      {req.isRequired ? 'Required' : 'Optional'}
                                    </Badge>
                                    {matchingDocs.length > 0 && (
                                      <Badge className="bg-green-600 text-xs">
                                        <CheckCircle className="h-3 w-3 mr-1" />
                                        {matchingDocs.length} uploaded
                                      </Badge>
                                    )}
                                  </div>
                                  <p className="text-sm text-gray-600 mt-2">
                                    Type: <span className="font-medium">{req.documentType}</span>
                                  </p>
                                  {req.description && (
                                    <p className="text-sm text-gray-500 mt-1">{req.description}</p>
                                  )}
                                </div>
                              </div>
                            </div>

                            {/* Uploaded Files for this Requirement */}
                            {matchingDocs.length > 0 ? (
                              <div className="p-4 space-y-2">
                                {matchingDocs.map((doc) => (
                                  <div
                                    key={doc.id}
                                    className="flex items-center justify-between p-3 bg-white border rounded-lg hover:bg-gray-50"
                                  >
                                    <div className="flex items-center gap-3">
                                      <CheckCircle className="h-5 w-5 text-green-600" />
                                      <div>
                                        <p className="font-medium text-sm">{doc.documentName}</p>
                                        <p className="text-xs text-gray-500">
                                          Uploaded: {format(new Date(doc.uploadedDate), 'MMM dd, yyyy')}
                                          {doc.fileSize && ` • ${(doc.fileSize / 1024).toFixed(1)} KB`}
                                        </p>
                                      </div>
                                    </div>
                                    <Button
                                      variant="ghost"
                                      size="sm"
                                      onClick={() => tenderBidService.downloadBidDocument(bid.id, doc.id, doc.documentName)}
                                    >
                                      <Download className="h-4 w-4 mr-2" />
                                      Download
                                    </Button>
                                  </div>
                                ))}
                              </div>
                            ) : (
                              <div className="p-4 text-center text-gray-500 text-sm">
                                <AlertCircle className="h-5 w-5 mx-auto mb-2 text-yellow-600" />
                                No document uploaded for this requirement
                              </div>
                            )}
                          </div>
                        );
                      })}

                      {/* Show any uploaded documents that don't match requirements */}
                      {(() => {
                        const requiredTypes = requirements.map((r: any) => r.documentType);
                        const unmatchedDocs = uploadedDocs.filter(doc => !requiredTypes.includes(doc.documentType));

                        if (unmatchedDocs.length > 0) {
                          return (
                            <div className="border rounded-lg overflow-hidden">
                              <div className="bg-gray-50 p-4 border-b">
                                <div className="flex items-center gap-2">
                                  <FileText className="h-5 w-5 text-gray-600" />
                                  <p className="font-medium">Other Documents</p>
                                  <Badge variant="secondary" className="text-xs">
                                    {unmatchedDocs.length} file(s)
                                  </Badge>
                                </div>
                              </div>
                              <div className="p-4 space-y-2">
                                {unmatchedDocs.map((doc) => (
                                  <div
                                    key={doc.id}
                                    className="flex items-center justify-between p-3 bg-white border rounded-lg hover:bg-gray-50"
                                  >
                                    <div className="flex items-center gap-3">
                                      <FileText className="h-5 w-5 text-blue-600" />
                                      <div>
                                        <p className="font-medium text-sm">{doc.documentName}</p>
                                        <p className="text-xs text-gray-500">
                                          {doc.documentType}
                                          {doc.fileSize && ` • ${(doc.fileSize / 1024).toFixed(1)} KB`}
                                        </p>
                                      </div>
                                    </div>
                                    <Button
                                      variant="ghost"
                                      size="sm"
                                      onClick={() => tenderBidService.downloadBidDocument(bid.id, doc.id, doc.documentName)}
                                    >
                                      <Download className="h-4 w-4 mr-2" />
                                      Download
                                    </Button>
                                  </div>
                                ))}
                              </div>
                            </div>
                          );
                        }
                        return null;
                      })()}
                    </div>
                  );
                } catch (error) {
                  console.error('Error rendering documents:', error);
                  return <p className="text-center py-8 text-red-500">Error loading documents</p>;
                }
              })()}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Information Tab */}
        <TabsContent value="info">
          <Card>
            <CardHeader>
              <CardTitle>Bid Information</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <p className="text-sm text-gray-500">Bid Number</p>
                  <p className="font-medium font-mono">{bid.bidNumber}</p>
                </div>
                <div>
                  <p className="text-sm text-gray-500">Status</p>
                  <div className="mt-1">{getStatusBadge(bid.status)}</div>
                </div>
                <div>
                  <p className="text-sm text-gray-500">Submitted Date</p>
                  <p className="font-medium">{formatDate(bid.submittedDate)}</p>
                </div>
                {bid.openedDate && (
                  <div>
                    <p className="text-sm text-gray-500">Opened Date</p>
                    <p className="font-medium">{formatDate(bid.openedDate)}</p>
                  </div>
                )}
                {bid.openedByName && (
                  <div>
                    <p className="text-sm text-gray-500">Opened By</p>
                    <p className="font-medium">{bid.openedByName}</p>
                  </div>
                )}
                <div>
                  <p className="text-sm text-gray-500">Total Amount</p>
                  <p className="font-medium text-green-600">
                    {bid.currency} {bid.totalBidAmount.toLocaleString()}
                  </p>
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* Performance Bond Tab */}
        {performanceBondRequest && (
          <TabsContent value="performance-bond">
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Shield className="h-5 w-5 text-purple-600" />
                  Performance Bond
                  {performanceBondRequest.status === 'Pending' && (
                    <Badge className="bg-red-500 text-white animate-pulse">
                      <Bell className="h-3 w-3 mr-1" />
                      Action Required
                    </Badge>
                  )}
                  {performanceBondRequest.status === 'Submitted' && (
                    <Badge className="bg-blue-500 text-white">Under Review</Badge>
                  )}
                  {performanceBondRequest.status === 'Approved' && (
                    <Badge className="bg-green-500 text-white">Approved</Badge>
                  )}
                  {performanceBondRequest.status === 'Rejected' && (
                    <Badge className="bg-red-500 text-white">Rejected</Badge>
                  )}
                </CardTitle>
                <CardDescription>
                  {performanceBondRequest.status === 'Pending'
                    ? 'Please download the template, complete it, and upload your performance bond document.'
                    : performanceBondRequest.status === 'Submitted'
                    ? 'Your performance bond document has been submitted and is under review.'
                    : performanceBondRequest.status === 'Rejected'
                    ? `Your performance bond was rejected. ${performanceBondRequest.rejectionReason || 'Please resubmit.'}`
                    : 'Your performance bond has been approved.'}
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-6">
                {/* Status Timeline */}
                <div className="flex items-center gap-2">
                  <div className={`flex items-center justify-center w-8 h-8 rounded-full ${performanceBondRequest.status !== 'Pending' ? 'bg-green-500' : 'bg-purple-500'} text-white`}>
                    1
                  </div>
                  <div className={`flex-1 h-1 ${performanceBondRequest.status !== 'Pending' ? 'bg-green-500' : 'bg-gray-300'}`} />
                  <div className={`flex items-center justify-center w-8 h-8 rounded-full ${performanceBondRequest.status === 'Submitted' || performanceBondRequest.status === 'Approved' ? 'bg-green-500' : 'bg-gray-300'} text-white`}>
                    2
                  </div>
                  <div className={`flex-1 h-1 ${performanceBondRequest.status === 'Approved' ? 'bg-green-500' : 'bg-gray-300'}`} />
                  <div className={`flex items-center justify-center w-8 h-8 rounded-full ${performanceBondRequest.status === 'Approved' ? 'bg-green-500' : 'bg-gray-300'} text-white`}>
                    3
                  </div>
                </div>
                <div className="flex justify-between text-sm text-gray-600">
                  <span>Requested</span>
                  <span>Submitted</span>
                  <span>Approved</span>
                </div>

                {/* Pending State - Download Template & Upload */}
                {(performanceBondRequest.status === 'Pending' || performanceBondRequest.status === 'Rejected') && (
                  <div className="space-y-6">
                    {/* Alert */}
                    <div className={`${performanceBondRequest.status === 'Rejected' ? 'bg-red-50 border-red-200' : 'bg-yellow-50 border-yellow-200'} border rounded-lg p-4`}>
                      <div className="flex items-start gap-3">
                        <AlertCircle className="h-5 w-5 text-yellow-600 mt-0.5" />
                        <div>
                          <p className="font-medium text-yellow-800">Action Required</p>
                          <p className="text-sm text-yellow-700 mt-1">
                            You need to submit your performance bond document to proceed with the contract.
                            Please download the template, complete it with your bank or insurance provider,
                            and upload the completed document.
                          </p>
                        </div>
                      </div>
                    </div>

                    {/* Step 1: Download Template */}
                    <div className="border rounded-lg p-4">
                      <h4 className="font-medium mb-3 flex items-center gap-2">
                        <span className="bg-purple-100 text-purple-700 rounded-full w-6 h-6 flex items-center justify-center text-sm">1</span>
                        Download Template
                      </h4>
                      <p className="text-sm text-gray-600 mb-3">
                        Download the performance bond template and have it completed by your bank or insurance provider.
                      </p>
                      <Button onClick={handleDownloadTemplate} variant="outline">
                        <Download className="h-4 w-4 mr-2" />
                        Download Template
                      </Button>
                    </div>

                    {/* Step 2: Upload Completed Document */}
                    <div className="border rounded-lg p-4">
                      <h4 className="font-medium mb-3 flex items-center gap-2">
                        <span className="bg-purple-100 text-purple-700 rounded-full w-6 h-6 flex items-center justify-center text-sm">2</span>
                        Upload Completed Document
                      </h4>
                      <p className="text-sm text-gray-600 mb-3">
                        Upload your completed performance bond document.
                      </p>

                      <div className="border-2 border-dashed border-gray-300 rounded-lg p-6 text-center">
                        {performanceBondFile ? (
                          <div className="flex items-center justify-center gap-3">
                            <FileText className="h-8 w-8 text-purple-500" />
                            <div className="text-left">
                              <p className="font-medium">{performanceBondFile.name}</p>
                              <p className="text-sm text-gray-500">
                                {(performanceBondFile.size / 1024 / 1024).toFixed(2)} MB
                              </p>
                            </div>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => setPerformanceBondFile(null)}
                            >
                              <XCircle className="h-4 w-4 text-red-500" />
                            </Button>
                          </div>
                        ) : (
                          <>
                            <Upload className="h-8 w-8 mx-auto text-gray-400 mb-2" />
                            <p className="text-sm text-gray-600 mb-2">
                              Click to upload or drag and drop
                            </p>
                            <p className="text-xs text-gray-500">
                              PDF, DOC, DOCX (Max 20MB)
                            </p>
                          </>
                        )}
                        <Input
                          ref={performanceBondFileRef}
                          type="file"
                          accept=".pdf,.doc,.docx"
                          onChange={handlePerformanceBondFileChange}
                          className={performanceBondFile ? 'hidden' : 'mt-4'}
                        />
                      </div>

                      <Button
                        onClick={handleUploadPerformanceBond}
                        disabled={!performanceBondFile || uploadingBond}
                        className="mt-4 w-full bg-purple-600 hover:bg-purple-700"
                      >
                        {uploadingBond ? (
                          <>
                            <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                            Uploading...
                          </>
                        ) : (
                          <>
                            <Upload className="h-4 w-4 mr-2" />
                            Submit Performance Bond
                          </>
                        )}
                      </Button>
                    </div>
                  </div>
                )}

                {/* Submitted State */}
                {performanceBondRequest.status === 'Submitted' && (
                  <div className="space-y-4">
                    <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
                      <div className="flex items-start gap-3">
                        <Clock className="h-5 w-5 text-blue-600 mt-0.5" />
                        <div>
                          <p className="font-medium text-blue-800">Under Review</p>
                          <p className="text-sm text-blue-700 mt-1">
                            Your performance bond document is being reviewed. You will be notified once it's approved.
                          </p>
                        </div>
                      </div>
                    </div>

                    <div className="border rounded-lg p-4">
                      <h4 className="font-medium mb-2">Submitted Document</h4>
                      <div className="flex items-center gap-3">
                        <FileText className="h-6 w-6 text-blue-600" />
                        <div>
                          <p className="font-medium">{performanceBondRequest.submittedFileName}</p>
                          <p className="text-sm text-gray-500">
                            Submitted on {formatDate(performanceBondRequest.submittedDate)}
                          </p>
                        </div>
                      </div>
                    </div>
                  </div>
                )}

                {/* Approved State */}
                {performanceBondRequest.status === 'Approved' && (
                  <div className="bg-green-50 border border-green-200 rounded-lg p-4">
                    <div className="flex items-start gap-3">
                      <CheckCircle className="h-5 w-5 text-green-600 mt-0.5" />
                      <div>
                        <p className="font-medium text-green-800">Performance Bond Approved</p>
                        <p className="text-sm text-green-700 mt-1">
                          Your performance bond has been approved. You can now proceed with the contract.
                        </p>
                        <p className="text-sm text-green-600 mt-2">
                          Approved on {formatDate(performanceBondRequest.reviewedDate)} by {performanceBondRequest.reviewedByName}
                        </p>
                      </div>
                    </div>
                  </div>
                )}
              </CardContent>
            </Card>
          </TabsContent>
        )}
      </Tabs>

      {/* Actions */}
      {bid.status === 'Draft' && (
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="font-medium">Continue Editing Your Bid</p>
                <p className="text-sm text-gray-500">
                  Your bid is saved as a draft. Continue editing to complete and submit your bid.
                </p>
              </div>
              <div className="flex gap-2">
                <Button onClick={handleEditBid}>
                  <FileText className="h-4 w-4 mr-2" />
                  Continue Editing
                </Button>
                <Button variant="destructive" onClick={handleWithdraw} disabled={withdrawing}>
                  <XCircle className="h-4 w-4 mr-2" />
                  {withdrawing ? 'Withdrawing...' : 'Withdraw Bid'}
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {bid.status === 'Submitted' && (
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="font-medium">Withdraw Bid</p>
                <p className="text-sm text-gray-500">
                  You can withdraw your bid if you no longer wish to participate
                </p>
              </div>
              <Button variant="destructive" onClick={handleWithdraw} disabled={withdrawing}>
                <XCircle className="h-4 w-4 mr-2" />
                {withdrawing ? 'Withdrawing...' : 'Withdraw Bid'}
              </Button>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

