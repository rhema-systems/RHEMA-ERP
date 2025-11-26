'use client';

import { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { 
  ArrowLeft, 
  Edit,
  Ban,
  CheckCircle,
  Star,
  Building2,
  Mail,
  Phone,
  MapPin,
  FileText,
  Award,
  DollarSign,
  AlertCircle
} from 'lucide-react';
import { toast } from 'sonner';
import { businessPartnerService, type BusinessPartnerDetailDto } from '@/services/businessPartnerService';

export default function BusinessPartnerDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = params.id as string;

  const [partner, setPartner] = useState<BusinessPartnerDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  
  // Dialog states
  const [suspendDialogOpen, setSuspendDialogOpen] = useState(false);
  const [activateDialogOpen, setActivateDialogOpen] = useState(false);

  useEffect(() => {
    loadPartner();
  }, [id]);

  const loadPartner = async () => {
    try {
      setLoading(true);
      const data = await businessPartnerService.getById(id);
      setPartner(data);
    } catch (error) {
      console.error('Error loading business partner:', error);
      toast.error('Failed to load business partner details');
    } finally {
      setLoading(false);
    }
  };

  const handleSuspend = async () => {
    try {
      setActionLoading(true);
      await businessPartnerService.suspendPartner(id);
      toast.success('Business partner suspended');
      setSuspendDialogOpen(false);
      loadPartner();
    } catch (error) {
      console.error('Error suspending partner:', error);
      toast.error('Failed to suspend business partner');
    } finally {
      setActionLoading(false);
    }
  };

  const handleActivate = async () => {
    try {
      setActionLoading(true);
      await businessPartnerService.activatePartner(id);
      toast.success('Business partner activated');
      setActivateDialogOpen(false);
      loadPartner();
    } catch (error) {
      console.error('Error activating partner:', error);
      toast.error('Failed to activate business partner');
    } finally {
      setActionLoading(false);
    }
  };

  const getStatusBadge = (status: string) => {
    const statusConfig: Record<string, { label: string; variant: 'default' | 'secondary' | 'destructive' | 'outline' }> = {
      Active: { label: 'Active', variant: 'default' },
      Inactive: { label: 'Inactive', variant: 'secondary' },
      Suspended: { label: 'Suspended', variant: 'destructive' },
      PendingApproval: { label: 'Pending Approval', variant: 'outline' },
    };

    const config = statusConfig[status] || { label: status, variant: 'secondary' };
    return <Badge variant={config.variant}>{config.label}</Badge>;
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto mb-4"></div>
          <p>Loading business partner details...</p>
        </div>
      </div>
    );
  }

  if (!partner) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <AlertCircle className="w-12 h-12 text-red-600 mx-auto mb-4" />
          <p>Business partner not found</p>
          <Button onClick={() => router.back()} className="mt-4">
            Go Back
          </Button>
        </div>
      </div>
    );
  }

  const canSuspend = partner.status === 'Active';
  const canActivate = partner.status === 'Suspended' || partner.status === 'Inactive';

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="outline" onClick={() => router.back()}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back
          </Button>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-3xl font-bold">{partner.companyName}</h1>
              {partner.isPreferred && <Star className="w-6 h-6 text-yellow-500 fill-yellow-500" />}
              {partner.isBlacklisted && <Badge variant="destructive">Blacklisted</Badge>}
            </div>
            <p className="text-gray-600 mt-1">
              {partner.partnerCode} • {getStatusBadge(partner.status)}
            </p>
          </div>
        </div>

        <div className="flex gap-2">
          <Button onClick={() => router.push(`/procurement/business-partners/${id}/edit`)}>
            <Edit className="w-4 h-4 mr-2" />
            Edit
          </Button>
          {canSuspend && (
            <Button onClick={() => setSuspendDialogOpen(true)} variant="destructive">
              <Ban className="w-4 h-4 mr-2" />
              Suspend
            </Button>
          )}
          {canActivate && (
            <Button onClick={() => setActivateDialogOpen(true)} className="bg-green-600 hover:bg-green-700">
              <CheckCircle className="w-4 h-4 mr-2" />
              Activate
            </Button>
          )}
        </div>
      </div>

      {/* Performance Summary */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-medium text-gray-600">Performance Rating</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex items-center gap-2">
              <Star className="w-5 h-5 text-yellow-500 fill-yellow-500" />
              <span className="text-2xl font-bold">
                {partner.performanceRating ? partner.performanceRating.toFixed(1) : 'N/A'}
              </span>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-medium text-gray-600">Partner Type</CardTitle>
          </CardHeader>
          <CardContent>
            <Badge variant="outline" className="text-lg">{partner.partnerType}</Badge>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-medium text-gray-600">Approval Status</CardTitle>
          </CardHeader>
          <CardContent>
            <Badge variant={partner.approvalStatus === 'Approved' ? 'default' : 'outline'}>
              {partner.approvalStatus}
            </Badge>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-medium text-gray-600">Preferred Partner</CardTitle>
          </CardHeader>
          <CardContent>
            <Badge variant={partner.isPreferred ? 'default' : 'secondary'}>
              {partner.isPreferred ? 'Yes' : 'No'}
            </Badge>
          </CardContent>
        </Card>
      </div>

      {/* Content Tabs */}
      <Tabs defaultValue="details" className="space-y-4">
        <TabsList>
          <TabsTrigger value="details">Company Details</TabsTrigger>
          <TabsTrigger value="contacts">Contacts ({partner.contacts?.length || 0})</TabsTrigger>
          <TabsTrigger value="documents">Documents ({partner.documents?.length || 0})</TabsTrigger>
          <TabsTrigger value="licenses">Licenses ({partner.licenses?.length || 0})</TabsTrigger>
          <TabsTrigger value="financial">Financial Info</TabsTrigger>
        </TabsList>

        {/* Company Details Tab */}
        <TabsContent value="details" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Building2 className="w-5 h-5" />
                Company Information
              </CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <Label className="text-gray-600">Company Name</Label>
                <p className="font-semibold">{partner.companyName}</p>
              </div>
              {partner.tradingName && (
                <div>
                  <Label className="text-gray-600">Trading Name</Label>
                  <p className="font-semibold">{partner.tradingName}</p>
                </div>
              )}
              <div>
                <Label className="text-gray-600">Partner Code</Label>
                <p className="font-semibold font-mono">{partner.partnerCode}</p>
              </div>
              {partner.registrationNumber && (
                <div>
                  <Label className="text-gray-600">Registration Number</Label>
                  <p className="font-semibold">{partner.registrationNumber}</p>
                </div>
              )}
              {partner.taxNumber && (
                <div>
                  <Label className="text-gray-600">Tax Number</Label>
                  <p className="font-semibold">{partner.taxNumber}</p>
                </div>
              )}
              {partner.vatNumber && (
                <div>
                  <Label className="text-gray-600">VAT Number</Label>
                  <p className="font-semibold">{partner.vatNumber}</p>
                </div>
              )}
              <div>
                <Label className="text-gray-600">Email</Label>
                <p className="font-semibold">{partner.email}</p>
              </div>
              <div>
                <Label className="text-gray-600">Phone</Label>
                <p className="font-semibold">{partner.phone}</p>
              </div>
              {partner.physicalAddress && (
                <div className="md:col-span-2">
                  <Label className="text-gray-600">Physical Address</Label>
                  <p className="font-semibold">
                    {partner.physicalAddress}
                    {partner.city && `, ${partner.city}`}
                    {partner.country && `, ${partner.country}`}
                  </p>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Contacts Tab */}
        <TabsContent value="contacts">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Mail className="w-5 h-5" />
                Contact Persons
              </CardTitle>
            </CardHeader>
            <CardContent>
              {!partner.contacts || partner.contacts.length === 0 ? (
                <p className="text-center text-gray-500 py-8">No contacts available</p>
              ) : (
                <div className="space-y-3">
                  {partner.contacts.map((contact, index) => (
                    <div key={index} className="p-4 border rounded-lg">
                      <div className="grid grid-cols-2 gap-3">
                        <div>
                          <Label className="text-gray-600">Name</Label>
                          <p className="font-semibold">{contact.name}</p>
                        </div>
                        <div>
                          <Label className="text-gray-600">Position</Label>
                          <p className="font-semibold">{contact.position || 'N/A'}</p>
                        </div>
                        <div>
                          <Label className="text-gray-600">Email</Label>
                          <p className="font-semibold">{contact.email}</p>
                        </div>
                        <div>
                          <Label className="text-gray-600">Phone</Label>
                          <p className="font-semibold">{contact.phone}</p>
                        </div>
                        {contact.isPrimary && (
                          <div className="col-span-2">
                            <Badge>Primary Contact</Badge>
                          </div>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Documents Tab */}
        <TabsContent value="documents">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <FileText className="w-5 h-5" />
                Documents
              </CardTitle>
            </CardHeader>
            <CardContent>
              {!partner.documents || partner.documents.length === 0 ? (
                <p className="text-center text-gray-500 py-8">No documents available</p>
              ) : (
                <div className="space-y-3">
                  {partner.documents.map((doc, index) => (
                    <div key={index} className="flex items-center justify-between p-4 border rounded-lg">
                      <div className="flex items-center gap-3">
                        <FileText className="w-5 h-5 text-blue-600" />
                        <div>
                          <p className="font-medium">{doc.documentType}</p>
                          <p className="text-sm text-gray-600">{doc.documentName}</p>
                        </div>
                      </div>
                      {doc.isVerified && <Badge className="bg-green-600">Verified</Badge>}
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Licenses Tab */}
        <TabsContent value="licenses">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Award className="w-5 h-5" />
                Licenses & Certifications
              </CardTitle>
            </CardHeader>
            <CardContent>
              {!partner.licenses || partner.licenses.length === 0 ? (
                <p className="text-center text-gray-500 py-8">No licenses available</p>
              ) : (
                <div className="space-y-3">
                  {partner.licenses.map((license, index) => (
                    <div key={index} className="p-4 border rounded-lg">
                      <div className="grid grid-cols-2 gap-3">
                        <div>
                          <Label className="text-gray-600">License Number</Label>
                          <p className="font-semibold">{license.licenseNumber}</p>
                        </div>
                        <div>
                          <Label className="text-gray-600">Issuing Authority</Label>
                          <p className="font-semibold">{license.issuingAuthority}</p>
                        </div>
                        <div>
                          <Label className="text-gray-600">Issue Date</Label>
                          <p className="font-semibold">{new Date(license.issueDate).toLocaleDateString()}</p>
                        </div>
                        {license.expiryDate && (
                          <div>
                            <Label className="text-gray-600">Expiry Date</Label>
                            <p className="font-semibold">{new Date(license.expiryDate).toLocaleDateString()}</p>
                          </div>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Financial Info Tab */}
        <TabsContent value="financial">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <DollarSign className="w-5 h-5" />
                Financial Information
              </CardTitle>
            </CardHeader>
            <CardContent>
              {!partner.financialInfo ? (
                <p className="text-center text-gray-500 py-8">No financial information available</p>
              ) : (
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {partner.financialInfo.bankName && (
                    <div>
                      <Label className="text-gray-600">Bank Name</Label>
                      <p className="font-semibold">{partner.financialInfo.bankName}</p>
                    </div>
                  )}
                  {partner.financialInfo.bankAccountNumber && (
                    <div>
                      <Label className="text-gray-600">Account Number</Label>
                      <p className="font-semibold font-mono">{partner.financialInfo.bankAccountNumber}</p>
                    </div>
                  )}
                  {partner.financialInfo.annualRevenue && (
                    <div>
                      <Label className="text-gray-600">Annual Revenue</Label>
                      <p className="font-semibold">${partner.financialInfo.annualRevenue.toLocaleString()}</p>
                    </div>
                  )}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Suspend Dialog */}
      <Dialog open={suspendDialogOpen} onOpenChange={setSuspendDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Suspend Business Partner</DialogTitle>
            <DialogDescription>
              Are you sure you want to suspend this business partner? They will not be able to participate in any transactions.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setSuspendDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleSuspend} disabled={actionLoading} variant="destructive">
              {actionLoading ? 'Suspending...' : 'Suspend Partner'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Activate Dialog */}
      <Dialog open={activateDialogOpen} onOpenChange={setActivateDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Activate Business Partner</DialogTitle>
            <DialogDescription>
              Are you sure you want to activate this business partner? They will be able to participate in transactions.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setActivateDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleActivate} disabled={actionLoading} className="bg-green-600 hover:bg-green-700">
              {actionLoading ? 'Activating...' : 'Activate Partner'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

