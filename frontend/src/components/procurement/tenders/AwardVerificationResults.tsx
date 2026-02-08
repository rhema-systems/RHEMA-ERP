'use client';

import { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Button } from '@/components/ui/button';
import { 
  Shield, 
  CheckCircle2, 
  XCircle, 
  Clock, 
  AlertCircle,
  Building2,
  FileText,
  User,
  Calendar
} from 'lucide-react';
import { toast } from 'sonner';
import { format } from 'date-fns';
import { 
  awardVerificationService, 
  TenderAwardVerification,
  TenderAwardVerificationBidder,
  TenderAwardVerificationItemResult
} from '@/services/awardVerificationService';

interface AwardVerificationResultsProps {
  tenderId: string;
  onVerificationLoaded?: (verification: TenderAwardVerification | null) => void;
}

export function AwardVerificationResults({ tenderId, onVerificationLoaded }: AwardVerificationResultsProps) {
  const [loading, setLoading] = useState(true);
  const [verification, setVerification] = useState<TenderAwardVerification | null>(null);
  const [selectedBidderId, setSelectedBidderId] = useState<string>('');

  useEffect(() => {
    loadVerification();
  }, [tenderId]);

  const loadVerification = async () => {
    try {
      setLoading(true);
      const data = await awardVerificationService.getVerificationByTender(tenderId);
      setVerification(data);
      onVerificationLoaded?.(data);
      
      // Select first bidder by default
      if (data?.bidders && data.bidders.length > 0) {
        setSelectedBidderId(data.bidders[0].id);
      }
    } catch (error) {
      console.error('Error loading verification:', error);
      toast.error('Failed to load verification data');
    } finally {
      setLoading(false);
    }
  };

  const getStatusBadge = (status: string) => {
    const config: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', className: string, icon: React.ReactNode }> = {
      'Pending': { variant: 'outline', className: 'bg-yellow-50 text-yellow-700 border-yellow-200', icon: <Clock className="h-3 w-3 mr-1" /> },
      'InProgress': { variant: 'outline', className: 'bg-blue-50 text-blue-700 border-blue-200', icon: <AlertCircle className="h-3 w-3 mr-1" /> },
      'Completed': { variant: 'default', className: 'bg-green-50 text-green-700 border-green-200', icon: <CheckCircle2 className="h-3 w-3 mr-1" /> },
      'Cancelled': { variant: 'destructive', className: 'bg-red-50 text-red-700 border-red-200', icon: <XCircle className="h-3 w-3 mr-1" /> },
    };
    const c = config[status] || config['Pending'];
    return (
      <Badge variant={c.variant} className={c.className}>
        {c.icon}
        {status}
      </Badge>
    );
  };

  const getItemStatusBadge = (status: string) => {
    const config: Record<string, { className: string, icon: React.ReactNode }> = {
      'Passed': { className: 'bg-green-100 text-green-800', icon: <CheckCircle2 className="h-3 w-3 mr-1" /> },
      'Failed': { className: 'bg-red-100 text-red-800', icon: <XCircle className="h-3 w-3 mr-1" /> },
      'NotApplicable': { className: 'bg-gray-100 text-gray-600', icon: <AlertCircle className="h-3 w-3 mr-1" /> },
      'Pending': { className: 'bg-yellow-100 text-yellow-700', icon: <Clock className="h-3 w-3 mr-1" /> },
    };
    const c = config[status] || config['Pending'];
    return (
      <Badge variant="outline" className={c.className}>
        {c.icon}
        {status === 'NotApplicable' ? 'N/A' : status}
      </Badge>
    );
  };

  const getBidderVerificationSummary = (bidder: TenderAwardVerificationBidder) => {
    const total = bidder.itemResults?.length || 0;
    const passed = bidder.itemResults?.filter(i => i.status === 'Passed').length || 0;
    const failed = bidder.itemResults?.filter(i => i.status === 'Failed').length || 0;
    return { total, passed, failed };
  };

  if (loading) {
    return (
      <Card>
        <CardContent className="pt-6">
          <div className="flex items-center justify-center py-8">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
          </div>
        </CardContent>
      </Card>
    );
  }

  if (!verification) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Shield className="h-5 w-5 text-gray-400" />
            Award Verification
          </CardTitle>
          <CardDescription>No verification process has been started for this tender.</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="text-center py-8 text-gray-500">
            <Shield className="h-12 w-12 mx-auto mb-4 text-gray-300" />
            <p>To start verification, go to the Award tab and select bidders to verify.</p>
          </div>
        </CardContent>
      </Card>
    );
  }

  const selectedBidder = verification.bidders?.find(b => b.id === selectedBidderId);

  return (
    <div className="space-y-6">
      {/* Verification Header */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <Shield className="h-6 w-6 text-purple-600" />
              <div>
                <CardTitle>Award Verification Results</CardTitle>
                <CardDescription>Verification status for selected bidders</CardDescription>
              </div>
            </div>
            {getStatusBadge(verification.status)}
          </div>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
            <div>
              <p className="text-muted-foreground">Started</p>
              <p className="font-medium">{verification.startedDate ? format(new Date(verification.startedDate), 'PPP') : 'N/A'}</p>
            </div>
            <div>
              <p className="text-muted-foreground">Completed</p>
              <p className="font-medium">{verification.completedDate ? format(new Date(verification.completedDate), 'PPP') : 'Not yet'}</p>
            </div>
            <div>
              <p className="text-muted-foreground">Bidders Verified</p>
              <p className="font-medium">{verification.bidders?.length || 0}</p>
            </div>
            <div>
              <p className="text-muted-foreground">Notes</p>
              <p className="font-medium truncate">{verification.notes || 'None'}</p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Bidders Tabs */}
      {verification.bidders && verification.bidders.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">Bidder Verification Details</CardTitle>
          </CardHeader>
          <CardContent>
            <Tabs value={selectedBidderId} onValueChange={setSelectedBidderId}>
              <TabsList className="mb-4 flex-wrap h-auto gap-1">
                {verification.bidders.map((bidder) => {
                  const summary = getBidderVerificationSummary(bidder);
                  return (
                    <TabsTrigger key={bidder.id} value={bidder.id} className="flex items-center gap-2">
                      <Building2 className="h-4 w-4" />
                      <span className="max-w-[150px] truncate">{bidder.businessPartnerName || 'Unknown'}</span>
                      <Badge variant="outline" className={
                        bidder.status === 'Verified' ? 'bg-green-50 text-green-700' :
                        bidder.status === 'Failed' ? 'bg-red-50 text-red-700' :
                        'bg-yellow-50 text-yellow-700'
                      }>
                        {summary.passed}/{summary.total}
                      </Badge>
                    </TabsTrigger>
                  );
                })}
              </TabsList>

              {verification.bidders.map((bidder) => (
                <TabsContent key={bidder.id} value={bidder.id}>
                  {/* Bidder Summary */}
                  <div className="mb-4 p-4 bg-muted/50 rounded-lg">
                    <div className="flex items-center justify-between mb-2">
                      <h4 className="font-semibold flex items-center gap-2">
                        <Building2 className="h-4 w-4" />
                        {bidder.businessPartnerName}
                      </h4>
                      {getStatusBadge(bidder.status)}
                    </div>
                    {bidder.overallComments && (
                      <p className="text-sm text-muted-foreground mt-2">
                        <strong>Comments:</strong> {bidder.overallComments}
                      </p>
                    )}
                    {bidder.verifiedDate && (
                      <p className="text-sm text-muted-foreground">
                        <strong>Verified:</strong> {format(new Date(bidder.verifiedDate), 'PPP p')}
                      </p>
                    )}
                  </div>

                  {/* Checklist Items Table */}
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead className="w-12">#</TableHead>
                        <TableHead>Checklist Item</TableHead>
                        <TableHead className="w-24">Required</TableHead>
                        <TableHead className="w-28">Status</TableHead>
                        <TableHead>Comments</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {bidder.itemResults && bidder.itemResults.length > 0 ? (
                        bidder.itemResults.map((item, index) => (
                          <TableRow key={item.id}>
                            <TableCell>{index + 1}</TableCell>
                            <TableCell>
                              <div>
                                <p className="font-medium">{item.checklistItemText || item.itemText}</p>
                                {item.itemDescription && (
                                  <p className="text-sm text-muted-foreground">{item.itemDescription}</p>
                                )}
                              </div>
                            </TableCell>
                            <TableCell>
                              {item.isRequired ? (
                                <Badge variant="outline" className="bg-red-50 text-red-700">Required</Badge>
                              ) : (
                                <Badge variant="outline" className="bg-gray-50 text-gray-600">Optional</Badge>
                              )}
                            </TableCell>
                            <TableCell>{getItemStatusBadge(item.status)}</TableCell>
                            <TableCell className="max-w-[200px] truncate">{item.comments || '-'}</TableCell>
                          </TableRow>
                        ))
                      ) : (
                        <TableRow>
                          <TableCell colSpan={5} className="text-center text-muted-foreground py-8">
                            No checklist items found
                          </TableCell>
                        </TableRow>
                      )}
                    </TableBody>
                  </Table>
                </TabsContent>
              ))}
            </Tabs>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
