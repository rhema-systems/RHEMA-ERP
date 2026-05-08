'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Checkbox } from '@/components/ui/checkbox';
import { ArrowLeft, Award, TrendingUp, TrendingDown } from 'lucide-react';
import { toast } from 'sonner';
import * as tenderBidService from '@/services/tenderBidService';
import * as tenderService from '@/services/tenderService';
import { type TenderBidSummaryDto } from '@/services/tenderBidService';
import { type TenderDetailDto } from '@/services/tenderService';

export default function CompareBidsPage() {
  const params = useParams();
  const router = useRouter();
  const tenderId = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';

  const [tender, setTender] = useState<TenderDetailDto | null>(null);
  const [bids, setBids] = useState<TenderBidSummaryDto[]>([]);
  const [selectedBidIds, setSelectedBidIds] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (tenderId) {
      loadData();
    }
  }, [tenderId]);

  const loadData = async () => {
    try {
      setLoading(true);
      const [tenderData, bidsData] = await Promise.all([
        tenderService.getTenderById(tenderId),
        tenderBidService.getBidsByTenderId(tenderId),
      ]);
      setTender(tenderData);
      setBids(bidsData);
      
      // Auto-select all bids for comparison
      setSelectedBidIds(bidsData.map(b => b.id));
    } catch (error) {
      console.error('Error loading data:', error);
      toast.error('Failed to load bid comparison data');
    } finally {
      setLoading(false);
    }
  };

  const toggleBidSelection = (bidId: string) => {
    setSelectedBidIds(prev => 
      prev.includes(bidId) 
        ? prev.filter(id => id !== bidId)
        : [...prev, bidId]
    );
  };

  const selectedBids = bids.filter(bid => selectedBidIds.includes(bid.id));

  const formatCurrency = (amount?: number, currency?: string) => {
    if (amount === undefined || amount === null) return 'N/A';
    return `${currency || 'USD'} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  };

  const getLowestBid = () => {
    if (selectedBids.length === 0) return null;
    return selectedBids.reduce((lowest, bid) => 
      (bid.totalBidAmount || 0) < (lowest.totalBidAmount || 0) ? bid : lowest
    );
  };

  const lowestBid = getLowestBid();

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <p className="text-lg text-gray-600">Loading bid comparison...</p>
      </div>
    );
  }

  if (!tender) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <p className="text-lg text-gray-600">Tender not found</p>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" onClick={() => router.push(`/procurement/tenders/${tenderId}`)}>
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back to Tender
          </Button>
          <div>
            <h1 className="text-3xl font-bold">Compare Bids</h1>
            <p className="text-gray-500">{tender.title} - {tender.tenderNumber}</p>
          </div>
        </div>
      </div>

      {/* Bid Selection */}
      <Card>
        <CardHeader>
          <CardTitle>Select Bids to Compare</CardTitle>
          <CardDescription>Choose which bids you want to compare side-by-side</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-2">
            {bids.map((bid) => (
              <div key={bid.id} className="flex items-center gap-3 p-3 border rounded-lg hover:bg-gray-50">
                <Checkbox
                  checked={selectedBidIds.includes(bid.id)}
                  onCheckedChange={() => toggleBidSelection(bid.id)}
                />
                <div className="flex-1">
                  <p className="font-semibold">{bid.businessPartnerName}</p>
                  <p className="text-sm text-gray-500">Bid #{bid.bidNumber}</p>
                </div>
                <div className="text-right">
                  <p className="font-semibold">{formatCurrency(bid.totalBidAmount, bid.currency)}</p>
                  <Badge variant={bid.status === 'Submitted' ? 'default' : 'outline'} className="mt-1">
                    {bid.status}
                  </Badge>
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      {/* Comparison Table */}
      {selectedBids.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Bid Comparison</CardTitle>
            <CardDescription>Comparing {selectedBids.length} bid(s)</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-48">Criteria</TableHead>
                    {selectedBids.map((bid) => (
                      <TableHead key={bid.id} className="text-center">
                        <div>
                          <p className="font-semibold">{bid.businessPartnerName}</p>
                          <p className="text-xs text-gray-500">{bid.bidNumber}</p>
                        </div>
                      </TableHead>
                    ))}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {/* Total Bid Amount */}
                  <TableRow>
                    <TableCell className="font-semibold">Total Bid Amount</TableCell>
                    {selectedBids.map((bid) => (
                      <TableCell key={bid.id} className="text-center">
                        <div className="flex flex-col items-center gap-1">
                          <p className="font-bold text-lg">{formatCurrency(bid.totalBidAmount, bid.currency)}</p>
                          {lowestBid && bid.id === lowestBid.id && (
                            <Badge variant="default" className="bg-green-100 text-green-800">
                              <TrendingDown className="h-3 w-3 mr-1" />
                              Lowest
                            </Badge>
                          )}
                          {lowestBid && bid.id !== lowestBid.id && bid.totalBidAmount && lowestBid.totalBidAmount && (
                            <p className="text-xs text-red-600">
                              +{formatCurrency(bid.totalBidAmount - lowestBid.totalBidAmount, bid.currency)}
                            </p>
                          )}
                        </div>
                      </TableCell>
                    ))}
                  </TableRow>

                  {/* Status */}
                  <TableRow>
                    <TableCell className="font-semibold">Status</TableCell>
                    {selectedBids.map((bid) => (
                      <TableCell key={bid.id} className="text-center">
                        <Badge variant={bid.status === 'Submitted' ? 'default' : 'outline'}>
                          {bid.status}
                        </Badge>
                      </TableCell>
                    ))}
                  </TableRow>

                  {/* Submitted Date */}
                  <TableRow>
                    <TableCell className="font-semibold">Submitted Date</TableCell>
                    {selectedBids.map((bid) => (
                      <TableCell key={bid.id} className="text-center">
                        {bid.submittedDate ? new Date(bid.submittedDate).toLocaleDateString() : 'N/A'}
                      </TableCell>
                    ))}
                  </TableRow>

                  {/* Actions */}
                  <TableRow>
                    <TableCell className="font-semibold">Actions</TableCell>
                    {selectedBids.map((bid) => (
                      <TableCell key={bid.id} className="text-center">
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => router.push(`/procurement/bids/${bid.id}`)}
                        >
                          View Details
                        </Button>
                      </TableCell>
                    ))}
                  </TableRow>
                </TableBody>
              </Table>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Summary */}
      {selectedBids.length > 0 && lowestBid && (
        <Card className="border-green-200 bg-green-50">
          <CardHeader>
            <CardTitle className="text-green-800 flex items-center gap-2">
              <Award className="h-5 w-5" />
              Recommended Bid (Lowest Price)
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div>
                <p className="text-sm text-green-700">Business Partner</p>
                <p className="font-bold text-green-900">{lowestBid.businessPartnerName}</p>
              </div>
              <div>
                <p className="text-sm text-green-700">Bid Number</p>
                <p className="font-bold text-green-900">{lowestBid.bidNumber}</p>
              </div>
              <div>
                <p className="text-sm text-green-700">Total Amount</p>
                <p className="font-bold text-green-900">{formatCurrency(lowestBid.totalBidAmount, lowestBid.currency)}</p>
              </div>
            </div>
            <div className="mt-4 pt-4 border-t border-green-200">
              <p className="text-sm text-green-700">
                <strong>Note:</strong> This recommendation is based on price only. Please review all evaluation criteria including quality, delivery, and experience before making a final decision.
              </p>
            </div>
          </CardContent>
        </Card>
      )}

      {selectedBids.length === 0 && (
        <Card>
          <CardContent className="py-12">
            <p className="text-center text-gray-500">Please select at least one bid to compare</p>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
