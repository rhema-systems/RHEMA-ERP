'use client';

import { hasCustomerRole, hasSupplierRole, hasContractorRole } from '@/lib/business-partner-roles';
import { BusinessPartnerCurrentAccountsPanel } from '@/components/procurement/BusinessPartnerCurrentAccountsPanel';


import { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Textarea } from '@/components/ui/textarea';
import { Input } from '@/components/ui/input';
import { format } from 'date-fns';
import {
  ArrowLeft,
  Edit,
  Ban,
  CheckCircle,
  Star,
  Building2,
  MapPin,
  FileText,
  Award,
  DollarSign,
  AlertCircle,
  Download,
  AlertTriangle,
  Clock,
  TrendingUp,
  BarChart3,
  Activity,
  Plus,
  Send,
  XCircle
} from 'lucide-react';
import Link from 'next/link';
import { toast } from 'sonner';
import { businessPartnerService, type BusinessPartnerDetailDto } from '@/services/businessPartnerService';
import {
  businessPartnerFinanceProfileService,
  type BusinessPartnerApProfile,
  type BusinessPartnerArProfile,
  type BusinessPartnerFinanceProfileSet,
} from '@/services/businessPartnerFinanceProfileService';
import { licenseTypeService, type LicenseTypeDto } from '@/services/partnerConfigService';
import { performanceTrackingService, type SupplierPerformanceMetricDto, type QualityIncidentDto, type PerformanceReviewDto } from '@/services/performanceTrackingService';
import { purchasingService, type PurchaseOrderSummaryDto } from '@/services/purchasingService';
import { PerformanceReviewDialog } from '@/components/procurement/PerformanceReviewDialog';
import { PerformanceReviewDetailDialog } from '@/components/procurement/PerformanceReviewDetailDialog';
import { PerformanceTrendsChart } from '@/components/procurement/PerformanceTrendsChart';
import { SupplierBankAccountsPanel, SupplierContactsPanel } from '@/components/procurement/SupplierContactBankDetails';
import { bankAccountsFromRegistrationData, contactsFromRegistrationData } from '@/lib/supplier-registration-details';
import { BusinessPartnerAccessSetup } from '@/components/procurement/BusinessPartnerAccessSetup';
import { useWorkflowSummary } from '@/hooks/useWorkflowSummary';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

const effectiveProfile = <T extends BusinessPartnerApProfile | BusinessPartnerArProfile>(profiles: T[]): T | null => {
  const today = new Date().toISOString().slice(0, 10);
  return [...profiles]
    .filter((profile) => profile.status === 'Approved' && profile.effectiveFrom.slice(0, 10) <= today &&
      (!profile.effectiveTo || profile.effectiveTo.slice(0, 10) >= today))
    .sort((left, right) => right.effectiveFrom.localeCompare(left.effectiveFrom) || right.versionNumber - left.versionNumber)[0] ?? null;
};

export default function BusinessPartnerDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';

  const [partner, setPartner] = useState<BusinessPartnerDetailDto | null>(null);
  const [financeProfiles, setFinanceProfiles] = useState<BusinessPartnerFinanceProfileSet | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [licenseTypes, setLicenseTypes] = useState<LicenseTypeDto[]>([]);

  // Performance tracking state
  const [performanceMetrics, setPerformanceMetrics] = useState<SupplierPerformanceMetricDto[]>([]);
  const [qualityIncidents, setQualityIncidents] = useState<QualityIncidentDto[]>([]);
  const [performanceReviews, setPerformanceReviews] = useState<PerformanceReviewDto[]>([]);
  const [performanceLoading, setPerformanceLoading] = useState(false);
  
  // Purchase orders state
  const [purchaseOrders, setPurchaseOrders] = useState<PurchaseOrderSummaryDto[]>([]);
  const [purchaseOrdersLoading, setPurchaseOrdersLoading] = useState(false);

  // Dialog states
  const [suspendDialogOpen, setSuspendDialogOpen] = useState(false);
  const [activateDialogOpen, setActivateDialogOpen] = useState(false);
  const [blacklistDialogOpen, setBlacklistDialogOpen] = useState(false);
  const [removeBlacklistDialogOpen, setRemoveBlacklistDialogOpen] = useState(false);
  const [rejectDialogOpen, setRejectDialogOpen] = useState(false);
  const [rejectionReason, setRejectionReason] = useState('');

  // Business Partner identity approval is distinct from its effective-dated AP/AR
  // Finance profiles. The workflow summary is authoritative for both routing and
  // maker-checker visibility; never infer approval rights from the user's UI role.
  const partnerWorkflow = useWorkflowSummary({ entityType: 'BusinessPartner', entityId: id });

  // Blacklist form state
  const [blacklistReason, setBlacklistReason] = useState('');
  const [blacklistUntil, setBlacklistUntil] = useState('');

  // Performance Review dialog states
  const [reviewDialogOpen, setReviewDialogOpen] = useState(false);
  const [reviewDetailDialogOpen, setReviewDetailDialogOpen] = useState(false);
  const [selectedReview, setSelectedReview] = useState<PerformanceReviewDto | null>(null);

  const getReviewYear = (review: PerformanceReviewDto) =>
    review.reviewYear ?? new Date(review.reviewDate).getFullYear();

  useEffect(() => {
    loadPartner();
    loadFinanceProfiles();
    loadLicenseTypes();
    loadPerformanceData();
    loadPurchaseOrders();
  }, [id]);

  const loadFinanceProfiles = async () => {
    try {
      setFinanceProfiles(await businessPartnerFinanceProfileService.get(id));
    } catch (error) {
      // Finance profile access is permission-controlled independently of the partner identity.
      // The detail page remains available when the current user cannot view Finance configuration.
      console.warn('Finance profile summary is unavailable:', error);
      setFinanceProfiles(null);
    }
  };

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

  const loadLicenseTypes = async () => {
    try {
      const data = await licenseTypeService.getActive();
      setLicenseTypes(data);
    } catch (error) {
      console.error('Error loading license types:', error);
    }
  };

  const loadPerformanceData = async () => {
    try {
      setPerformanceLoading(true);
      const [metrics, incidents, reviews] = await Promise.all([
        performanceTrackingService.getMetricsByBusinessPartner(id),
        performanceTrackingService.getIncidentsByBusinessPartner(id),
        performanceTrackingService.getReviewsByBusinessPartner(id)
      ]);
      setPerformanceMetrics(metrics);
      setQualityIncidents(incidents);
      setPerformanceReviews(reviews);
    } catch (error) {
      console.error('Error loading performance data:', error);
      // Don't show error toast as this is optional data
    } finally {
      setPerformanceLoading(false);
    }
  };
  
  const loadPurchaseOrders = async () => {
    try {
      setPurchaseOrdersLoading(true);
      const orders = await purchasingService.getPurchaseOrdersBySupplier(id);
      setPurchaseOrders(orders);
    } catch (error) {
      console.error('Error loading purchase orders:', error);
      // Don't show error toast as this is optional data
    } finally {
      setPurchaseOrdersLoading(false);
    }
  };

  const getLicenseTypeName = (licenseTypeId: string, fallbackName?: string): string => {
    if (fallbackName) return fallbackName; // Use the name from backend if available
    const licenseType = licenseTypes.find((lt) => lt.id === licenseTypeId);
    return licenseType?.licenseName || 'Unknown License Type';
  };

  // Helper function to check if a license is expired
  const isLicenseExpired = (expiryDate?: string): boolean => {
    if (!expiryDate) return false;
    return new Date(expiryDate) < new Date();
  };

  // Helper function to check if a license is expiring soon (within 30 days)
  const isLicenseExpiringSoon = (expiryDate?: string): boolean => {
    if (!expiryDate) return false;
    const expiry = new Date(expiryDate);
    const today = new Date();
    const daysUntilExpiry = Math.ceil((expiry.getTime() - today.getTime()) / (1000 * 60 * 60 * 24));
    return daysUntilExpiry > 0 && daysUntilExpiry <= 30;
  };

  // Helper function to get license expiry status
  const getLicenseExpiryStatus = (expiryDate?: string): { status: string; color: string; icon: any } => {
    if (!expiryDate) {
      return { status: 'No Expiry', color: 'text-gray-600', icon: null };
    }

    if (isLicenseExpired(expiryDate)) {
      return { status: 'Expired', color: 'text-red-600', icon: AlertTriangle };
    }

    if (isLicenseExpiringSoon(expiryDate)) {
      return { status: 'Expiring Soon', color: 'text-orange-600', icon: Clock };
    }

    return { status: 'Valid', color: 'text-green-600', icon: CheckCircle };
  };

  // Count expired licenses
  const expiredLicensesCount = partner?.licenses?.filter(license => isLicenseExpired(license.expiryDate)).length || 0;

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

  const handleBlacklist = async () => {
    if (!blacklistReason.trim()) {
      toast.error('Please provide a reason for blacklisting');
      return;
    }

    try {
      setActionLoading(true);
      await businessPartnerService.blacklistPartner(id, blacklistReason, blacklistUntil || undefined);
      toast.success('Business partner blacklisted');
      setBlacklistDialogOpen(false);
      setBlacklistReason('');
      setBlacklistUntil('');
      loadPartner();
    } catch (error) {
      console.error('Error blacklisting partner:', error);
      toast.error('Failed to blacklist business partner');
    } finally {
      setActionLoading(false);
    }
  };

  const handleRemoveFromBlacklist = async () => {
    try {
      setActionLoading(true);
      await businessPartnerService.removeFromBlacklist(id);
      toast.success('Business partner removed from blacklist');
      setRemoveBlacklistDialogOpen(false);
      loadPartner();
    } catch (error) {
      console.error('Error removing partner from blacklist:', error);
      toast.error('Failed to remove business partner from blacklist');
    } finally {
      setActionLoading(false);
    }
  };

  const refreshPartnerWorkflow = async () => {
    await Promise.all([loadPartner(), partnerWorkflow.refresh()]);
  };

  const handleSubmitForApproval = async () => {
    try {
      setActionLoading(true);
      await businessPartnerService.submitPartnerForApproval(id);
      toast.success('Business partner submitted for independent approval');
      await refreshPartnerWorkflow();
    } catch (error: any) {
      console.error('Error submitting business partner:', error);
      toast.error(error?.message || 'Unable to submit the business partner for approval');
    } finally {
      setActionLoading(false);
    }
  };

  const handleApprovePartner = async () => {
    try {
      setActionLoading(true);
      await businessPartnerService.approvePartner(id);
      toast.success('Business partner approved and activated for new transactions');
      await refreshPartnerWorkflow();
    } catch (error: any) {
      console.error('Error approving business partner:', error);
      toast.error(error?.message || 'Unable to approve the business partner');
    } finally {
      setActionLoading(false);
    }
  };

  const handleRejectPartner = async () => {
    if (!rejectionReason.trim()) {
      toast.error('Enter a reason so the maker knows what must be corrected');
      return;
    }

    try {
      setActionLoading(true);
      await businessPartnerService.rejectPartner(id, rejectionReason.trim());
      toast.success('Business partner rejected and returned to the maker');
      setRejectDialogOpen(false);
      setRejectionReason('');
      await refreshPartnerWorkflow();
    } catch (error: any) {
      console.error('Error rejecting business partner:', error);
      toast.error(error?.message || 'Unable to reject the business partner');
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

  const canSuspend = partner.status === 'Active' && !partner.isBlacklisted;
  const canActivate = partner.status === 'Suspended' || partner.status === 'Inactive';
  const canBlacklist = !partner.isBlacklisted;
  const canRemoveFromBlacklist = partner.isBlacklisted;
  const hasActivePartnerWorkflow = partnerWorkflow.summary?.hasActiveInstance === true;
  const canSubmitForApproval =
    partner.approvalStatus !== 'Approved' &&
    partner.approvalStatus !== 'Rejected' &&
    partnerWorkflow.visibility.known &&
    !hasActivePartnerWorkflow;
  const canDecidePartner =
    partnerWorkflow.visibility.showApprovalControls &&
    hasActivePartnerWorkflow &&
    partnerWorkflow.summary?.canCurrentUserApprove === true;
  const partnerContacts = contactsFromRegistrationData(partner as unknown as Record<string, unknown>);
  const partnerBankAccounts = bankAccountsFromRegistrationData(partner as unknown as Record<string, unknown>);
  const currentApProfiles = financeProfiles?.roles
    .filter((role) => role.status === 'Active' && (role.roleType === 'Supplier' || role.roleType === 'Contractor'))
    .map((role) => ({ role, profile: effectiveProfile(role.apProfiles) }))
    .filter((item): item is typeof item & { profile: BusinessPartnerApProfile } => item.profile !== null) ?? [];
  const currentArProfiles = financeProfiles?.roles
    .filter((role) => role.status === 'Active' && role.roleType === 'Customer')
    .map((role) => ({ role, profile: effectiveProfile(role.arProfiles) }))
    .filter((item): item is typeof item & { profile: BusinessPartnerArProfile } => item.profile !== null) ?? [];
  const hasFinanceProfileHistory = financeProfiles?.roles.some((role) => role.apProfiles.length > 0 || role.arProfiles.length > 0) === true;

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
              <h1 className="text-3xl font-bold">{partner.partnerName || partner.companyName}</h1>
              {partner.isPreferred && <Star className="w-6 h-6 text-yellow-500 fill-yellow-500" />}
              {partner.isBlacklisted && <Badge variant="destructive">Blacklisted</Badge>}
            </div>
            <div className="text-gray-600 mt-1 flex items-center gap-2">
              <span>{partner.partnerCode}</span>
              <span>•</span>
              {getStatusBadge(partner.status)}
            </div>
          </div>
        </div>

        <div className="flex gap-2">
          {canSubmitForApproval && (
            <Button onClick={handleSubmitForApproval} disabled={actionLoading}>
              <Send className="w-4 h-4 mr-2" />
              {actionLoading ? 'Submitting...' : 'Submit for approval'}
            </Button>
          )}
          {canDecidePartner && (
            <>
              <Button onClick={() => setRejectDialogOpen(true)} disabled={actionLoading} variant="outline">
                <XCircle className="w-4 h-4 mr-2" />
                Reject
              </Button>
              <Button onClick={handleApprovePartner} disabled={actionLoading} className="bg-green-600 hover:bg-green-700">
                <CheckCircle className="w-4 h-4 mr-2" />
                {actionLoading ? 'Approving...' : 'Approve'}
              </Button>
            </>
          )}
          <BusinessPartnerAccessSetup partnerId={id} onSaved={loadPartner} />
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
          {canBlacklist && (
            <Button onClick={() => setBlacklistDialogOpen(true)} variant="destructive">
              <Ban className="w-4 h-4 mr-2" />
              Blacklist
            </Button>
          )}
          {canRemoveFromBlacklist && (
            <Button onClick={() => setRemoveBlacklistDialogOpen(true)} className="bg-green-600 hover:bg-green-700">
              <CheckCircle className="w-4 h-4 mr-2" />
              Remove from Blacklist
            </Button>
          )}
        </div>
      </div>

      {hasActivePartnerWorkflow && !canDecidePartner && (
        <Card className="border-blue-200 bg-blue-50">
          <CardContent className="pt-4 text-sm text-blue-900">
            This Business Partner is awaiting an independent approval. It appears in the Finance Approval Workbench only for users assigned to the current approval step.
          </CardContent>
        </Card>
      )}
      {partnerWorkflow.error && (
        <Card className="border-amber-300 bg-amber-50">
          <CardContent className="flex items-center justify-between gap-4 pt-4 text-sm text-amber-900">
            <span>Approval status could not be loaded. Submission and decision actions are disabled to protect maker-checker controls.</span>
            <Button variant="outline" size="sm" onClick={() => void partnerWorkflow.refresh()}>Retry</Button>
          </CardContent>
        </Card>
      )}

      {/* Blacklist Warning Banner */}
      {partner.isBlacklisted && (
        <Card className="border-red-500 bg-red-50">
          <CardContent className="pt-6">
            <div className="flex items-start gap-3">
              <AlertTriangle className="w-6 h-6 text-red-600 flex-shrink-0 mt-0.5" />
              <div className="flex-1">
                <h3 className="font-semibold text-red-900 text-lg mb-2">This Partner is Blacklisted</h3>
                {partner.blacklistReason && (
                  <div className="mb-3">
                    <Label className="text-sm font-semibold text-red-800">Reason:</Label>
                    <p className="text-sm text-red-700 mt-1">{partner.blacklistReason}</p>
                  </div>
                )}
                <div className="flex flex-wrap gap-4 text-sm text-red-700">
                  {partner.blacklistDate && (
                    <div>
                      <span className="font-semibold">Blacklisted on:</span> {format(new Date(partner.blacklistDate), 'MMM dd, yyyy')}
                    </div>
                  )}
                  {partner.blacklistExpiryDate && (
                    <div>
                      <span className="font-semibold">Expires on:</span> {format(new Date(partner.blacklistExpiryDate), 'MMM dd, yyyy')}
                    </div>
                  )}
                  {!partner.blacklistExpiryDate && (
                    <div>
                      <span className="font-semibold">Duration:</span> Permanent
                    </div>
                  )}
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

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
        <TabsList className="h-auto flex-wrap justify-start">
          <TabsTrigger value="details">Company Details</TabsTrigger>
          <TabsTrigger value="contacts">Contacts ({partnerContacts.length})</TabsTrigger>
          <TabsTrigger value="bank-accounts">Bank Accounts ({partnerBankAccounts.length})</TabsTrigger>
          <TabsTrigger value="documents">Documents ({partner.documents?.length || 0})</TabsTrigger>
          <TabsTrigger value="licenses" className="relative">
            Licenses ({partner.licenses?.length || 0})
            {expiredLicensesCount > 0 && (
              <Badge variant="destructive" className="ml-2 px-1.5 py-0 text-xs">
                {expiredLicensesCount} Expired
              </Badge>
            )}
          </TabsTrigger>
          <TabsTrigger value="financial">Financial Info</TabsTrigger>
          {(hasSupplierRole(partner.partnerType) || hasContractorRole(partner.partnerType)) && <TabsTrigger value="accounts-payable">Accounts Payable</TabsTrigger>}
          {hasCustomerRole(partner.partnerType) && <TabsTrigger value="accounts-receivable">Accounts Receivable</TabsTrigger>}
          {hasSupplierRole(partner.partnerType) && (
            <TabsTrigger value="purchase-orders">
              Purchase Orders ({purchaseOrders.length})
            </TabsTrigger>
          )}
          <TabsTrigger value="performance" className="relative">
            <Activity className="w-4 h-4 mr-2" />
            Performance
            {performanceMetrics.length > 0 && (
              <Badge variant="secondary" className="ml-2 px-1.5 py-0 text-xs">
                {performanceMetrics[0]?.performanceGrade || 'N/A'}
              </Badge>
            )}
          </TabsTrigger>
        </TabsList>

        {/* Company Details Tab */}
        {(hasSupplierRole(partner.partnerType) || hasContractorRole(partner.partnerType)) && <TabsContent value="accounts-payable">
          <BusinessPartnerCurrentAccountsPanel businessPartnerId={id} partnerType={partner.partnerType} ledger="payables" />
        </TabsContent>}
        {hasCustomerRole(partner.partnerType) && <TabsContent value="accounts-receivable">
          <BusinessPartnerCurrentAccountsPanel businessPartnerId={id} partnerType={partner.partnerType} ledger="receivables" />
        </TabsContent>}
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
                <p className="font-semibold">{partner.partnerName || partner.companyName || 'N/A'}</p>
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
              <div className="md:col-span-2">
                <Label className="text-gray-600">Business Categories</Label>
                {partner.categories?.length ? (
                  <div className="mt-2 flex flex-wrap gap-2">
                    {partner.categories.map((category) => (
                      <Badge key={category} variant="secondary">
                        {category}
                      </Badge>
                    ))}
                  </div>
                ) : (
                  <p className="font-semibold">Not assigned</p>
                )}
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
              {partner.ssnitNumber && (
                <div>
                  <Label className="text-gray-600">SSNIT Number</Label>
                  <p className="font-semibold">{partner.ssnitNumber}</p>
                </div>
              )}
              {partner.vatNumber && (
                <div>
                  <Label className="text-gray-600">VAT Number</Label>
                  <p className="font-semibold">{partner.vatNumber}</p>
                </div>
              )}
              {partner.email && (
                <div>
                  <Label className="text-gray-600">Email</Label>
                  <p className="font-semibold">{partner.email}</p>
                </div>
              )}
              {partner.phone && (
                <div>
                  <Label className="text-gray-600">Phone</Label>
                  <p className="font-semibold">{partner.phone}</p>
                </div>
              )}
              {partner.alternatePhone && (
                <div>
                  <Label className="text-gray-600">Alternate Phone</Label>
                  <p className="font-semibold">{partner.alternatePhone}</p>
                </div>
              )}
              {partner.website && (
                <div>
                  <Label className="text-gray-600">Website</Label>
                  <p className="font-semibold">{partner.website}</p>
                </div>
              )}
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
          <SupplierContactsPanel contacts={partnerContacts} />
        </TabsContent>

        {/* Bank Accounts Tab */}
        <TabsContent value="bank-accounts">
          <SupplierBankAccountsPanel accounts={partnerBankAccounts} />
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
                          {doc.fileSize && (
                            <p className="text-xs text-gray-500">
                              {(doc.fileSize / 1024).toFixed(2)} KB
                            </p>
                          )}
                        </div>
                      </div>
                      <div className="flex items-center gap-2">
                        {doc.isVerified && <Badge className="bg-green-600">Verified</Badge>}
                        {doc.filePath && (
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => {
                              const url = `${API_BASE_URL}/procurement/business-partners/${partner.id}/documents/${doc.id}/download`;
                              const token = localStorage.getItem('token') || localStorage.getItem('authToken');

                              // Create a temporary link to download with auth header
                              fetch(url, {
                                headers: {
                                  ...(token && { 'Authorization': `Bearer ${token}` })
                                }
                              })
                              .then(response => {
                                if (!response.ok) throw new Error('Download failed');
                                return response.blob();
                              })
                              .then(blob => {
                                const blobUrl = window.URL.createObjectURL(blob);
                                const a = document.createElement('a');
                                a.href = blobUrl;
                                a.download = doc.documentName || 'document';
                                document.body.appendChild(a);
                                a.click();
                                window.URL.revokeObjectURL(blobUrl);
                                document.body.removeChild(a);
                                toast.success('Document downloaded successfully');
                              })
                              .catch(error => {
                                console.error('Download error:', error);
                                toast.error('Failed to download document');
                              });
                            }}
                          >
                            <Download className="w-4 h-4 mr-1" />
                            Download
                          </Button>
                        )}
                      </div>
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
                  {partner.licenses.map((license, index) => {
                    const expiryStatus = getLicenseExpiryStatus(license.expiryDate);
                    const isExpired = isLicenseExpired(license.expiryDate);
                    const isExpiringSoon = isLicenseExpiringSoon(license.expiryDate);

                    // Determine card styling based on expiry status
                    const borderColor = isExpired ? 'border-red-500' : isExpiringSoon ? 'border-orange-500' : 'border-blue-500';
                    const bgColor = isExpired ? 'bg-red-50' : isExpiringSoon ? 'bg-orange-50' : 'bg-blue-50';
                    const iconColor = isExpired ? 'text-red-600' : isExpiringSoon ? 'text-orange-600' : 'text-blue-600';
                    const titleColor = isExpired ? 'text-red-900' : isExpiringSoon ? 'text-orange-900' : 'text-blue-900';

                    return (
                      <div key={index} className={`border-l-4 ${borderColor} pl-4 py-3 ${bgColor} rounded-r-lg`}>
                        <div className="flex items-start gap-3">
                          <Award className={`w-6 h-6 ${iconColor} mt-0.5 flex-shrink-0`} />
                          <div className="flex-1">
                            <div className="flex items-start justify-between mb-3">
                              <p className={`font-semibold text-lg ${titleColor}`}>
                                {getLicenseTypeName(license.licenseTypeId, license.licenseTypeName)}
                              </p>
                              {/* Expiry Status Badge */}
                              {license.expiryDate && (
                                <div className="flex items-center gap-1.5">
                                  {expiryStatus.icon && (
                                    <expiryStatus.icon className={`w-4 h-4 ${expiryStatus.color}`} />
                                  )}
                                  <Badge
                                    variant={isExpired ? 'destructive' : isExpiringSoon ? 'outline' : 'default'}
                                    className={isExpiringSoon ? 'border-orange-500 text-orange-700 bg-orange-50' : ''}
                                  >
                                    {expiryStatus.status}
                                  </Badge>
                                </div>
                              )}
                            </div>
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                              <div>
                                <Label className="text-gray-600 text-xs">License Number</Label>
                                <p className="font-semibold">{license.licenseNumber}</p>
                              </div>
                              <div>
                                <Label className="text-gray-600 text-xs">Issuing Authority</Label>
                                <p className="font-semibold">{license.issuingAuthority || 'N/A'}</p>
                              </div>
                              {license.issueDate && (
                                <div>
                                  <Label className="text-gray-600 text-xs">Issue Date</Label>
                                  <p className="font-semibold">{format(new Date(license.issueDate), 'MMM dd, yyyy')}</p>
                                </div>
                              )}
                              {license.expiryDate && (
                                <div>
                                  <Label className="text-gray-600 text-xs">Expiry Date</Label>
                                  <p className={`font-semibold ${expiryStatus.color}`}>
                                    {format(new Date(license.expiryDate), 'MMM dd, yyyy')}
                                  </p>
                                </div>
                              )}
                            </div>
                          </div>
                        </div>
                      </div>
                    );
                  })}
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
            <CardContent className="space-y-6">
              {financeProfiles && (
                <div className="space-y-3">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div>
                      <h3 className="font-semibold text-lg">Current approved Finance profiles</h3>
                      <p className="text-sm text-muted-foreground">Effective profile values used by Finance and transaction processing.</p>
                    </div>
                    <Button variant="outline" size="sm" onClick={() => router.push(`/procurement/business-partners/${id}/edit?tab=finance-profiles`)}>
                      View Finance Profiles
                    </Button>
                  </div>
                  {(currentApProfiles.length > 0 || currentArProfiles.length > 0) ? (
                    <div className="grid gap-4 md:grid-cols-2">
                      {currentApProfiles.map(({ role, profile }) => (
                        <div key={profile.id} className="rounded-lg border p-4">
                          <div className="mb-3 flex items-center justify-between gap-2">
                            <p className="font-semibold">{role.roleType} / Accounts Payable</p>
                            <Badge variant="outline">Version {profile.versionNumber}</Badge>
                          </div>
                          <dl className="grid gap-3 text-sm sm:grid-cols-2">
                            <div><dt className="text-muted-foreground">AP reference</dt><dd className="font-medium">{profile.apReferenceNumber || 'Not set'}</dd></div>
                            <div><dt className="text-muted-foreground">Subject to WHT</dt><dd className="font-medium">{profile.subjectToWithholding ? 'Yes' : 'No'}</dd></div>
                            <div><dt className="text-muted-foreground">Effective from</dt><dd className="font-medium">{format(new Date(profile.effectiveFrom), 'MMM dd, yyyy')}</dd></div>
                          </dl>
                        </div>
                      ))}
                      {currentArProfiles.map(({ role, profile }) => (
                        <div key={profile.id} className="rounded-lg border p-4">
                          <div className="mb-3 flex items-center justify-between gap-2">
                            <p className="font-semibold">{role.roleType} / Accounts Receivable</p>
                            <Badge variant="outline">Version {profile.versionNumber}</Badge>
                          </div>
                          <dl className="grid gap-3 text-sm sm:grid-cols-2">
                            <div><dt className="text-muted-foreground">Credit limit</dt><dd className="font-medium">{profile.creditLimit == null ? 'Not set' : `${partner.currency || 'GHS'} ${profile.creditLimit.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`}</dd></div>
                            <div><dt className="text-muted-foreground">AR reference</dt><dd className="font-medium">{profile.arReferenceNumber || 'Not set'}</dd></div>
                            <div><dt className="text-muted-foreground">Withholding agent</dt><dd className="font-medium">{profile.isWithholdingAgent ? 'Yes' : 'No'}</dd></div>
                            <div><dt className="text-muted-foreground">Effective from</dt><dd className="font-medium">{format(new Date(profile.effectiveFrom), 'MMM dd, yyyy')}</dd></div>
                          </dl>
                        </div>
                      ))}
                    </div>
                  ) : (
                    <p className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                      {hasFinanceProfileHistory
                        ? 'Finance profile changes are saved, but no approved profile is currently effective. Submit the draft and complete independent approval before its values become operational.'
                        : 'No Finance profile has been prepared for this Business Partner.'}
                    </p>
                  )}
                </div>
              )}
              {!partner.financialInfo || partner.financialInfo.length === 0 ? (
                <p className="text-center text-gray-500 py-4">No audited financial statements available</p>
              ) : (
                <div className="space-y-6">
                  {/* Financial Records */}
                  {partner.financialInfo && partner.financialInfo.length > 0 && (
                    <div>
                      <h3 className="font-semibold text-lg mb-3">Financial Records</h3>
                      <div className="space-y-4">
                        {partner.financialInfo.map((financial, index) => (
                          <div key={index} className="p-4 border rounded-lg">
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                              {financial.fiscalYear && (
                                <div>
                                  <Label className="text-gray-600">Fiscal Year</Label>
                                  <p className="font-semibold">{financial.fiscalYear}</p>
                                </div>
                              )}
                              {financial.annualRevenue && (
                                <div>
                                  <Label className="text-gray-600">Annual Revenue</Label>
                                  <p className="font-semibold">${financial.annualRevenue.toLocaleString()}</p>
                                </div>
                              )}
                              {financial.netProfit && (
                                <div>
                                  <Label className="text-gray-600">Net Profit</Label>
                                  <p className="font-semibold">${financial.netProfit.toLocaleString()}</p>
                                </div>
                              )}
                              {financial.totalAssets && (
                                <div>
                                  <Label className="text-gray-600">Total Assets</Label>
                                  <p className="font-semibold">${financial.totalAssets.toLocaleString()}</p>
                                </div>
                              )}
                              {financial.totalLiabilities && (
                                <div>
                                  <Label className="text-gray-600">Total Liabilities</Label>
                                  <p className="font-semibold">${financial.totalLiabilities.toLocaleString()}</p>
                                </div>
                              )}
                              {financial.creditRating && (
                                <div>
                                  <Label className="text-gray-600">Credit Rating</Label>
                                  <p className="font-semibold">{financial.creditRating}</p>
                                </div>
                              )}
                              {financial.isAudited && (
                                <div className="md:col-span-2">
                                  <Badge variant="default">Audited</Badge>
                                  {financial.auditorName && (
                                    <span className="ml-2 text-sm text-gray-600">by {financial.auditorName}</span>
                                  )}
                                </div>
                              )}
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Purchase Orders Tab */}
        {hasSupplierRole(partner.partnerType) && (
          <TabsContent value="purchase-orders">
            <Card>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <CardTitle className="flex items-center gap-2">
                    <FileText className="w-5 h-5" />
                    Purchase Orders
                  </CardTitle>
                  <Link href={`/procurement/purchase-orders/new?supplierId=${id}`}>
                    <Button size="sm">
                      <Plus className="w-4 h-4 mr-2" />
                      New Purchase Order
                    </Button>
                  </Link>
                </div>
                <CardDescription>
                  Purchase orders placed with this supplier
                </CardDescription>
              </CardHeader>
              <CardContent>
                {purchaseOrdersLoading ? (
                  <p className="text-center text-gray-500 py-8">Loading purchase orders...</p>
                ) : purchaseOrders.length === 0 ? (
                  <div className="text-center py-8">
                    <FileText className="w-12 h-12 mx-auto text-gray-400 mb-3" />
                    <p className="text-gray-500 mb-4">No purchase orders yet</p>
                    <Link href={`/procurement/purchase-orders/new?supplierId=${id}`}>
                      <Button>
                        <Plus className="w-4 h-4 mr-2" />
                        Create First Purchase Order
                      </Button>
                    </Link>
                  </div>
                ) : (
                  <div className="space-y-3">
                    {purchaseOrders.slice(0, 10).map((po) => (
                      <Link key={po.id} href={`/procurement/purchase-orders/${po.id}`}>
                        <div className="p-4 border rounded-lg hover:bg-gray-50 transition-colors cursor-pointer">
                          <div className="flex items-center justify-between mb-2">
                            <div>
                              <div className="font-semibold">{po.orderNumber}</div>
                              <div className="text-sm text-gray-600">
                                {format(new Date(po.orderDate), 'MMM dd, yyyy')}
                              </div>
                            </div>
                            <div className="flex items-center gap-2">
                              <Badge variant={
                                po.status === 'Received' ? 'default' :
                                po.status === 'Approved' || po.status === 'Sent' ? 'secondary' :
                                po.status === 'Cancelled' ? 'destructive' :
                                'outline'
                              }>
                                {po.status}
                              </Badge>
                              <div className="text-right">
                                <div className="font-semibold">
                                  ${po.totalAmount.toLocaleString('en-US', { minimumFractionDigits: 2 })}
                                </div>
                                <div className="text-xs text-gray-500">{po.itemCount} items</div>
                              </div>
                            </div>
                          </div>
                          {po.requiredDate && (
                            <div className="text-xs text-gray-500">
                              Required: {format(new Date(po.requiredDate), 'MMM dd, yyyy')}
                            </div>
                          )}
                        </div>
                      </Link>
                    ))}
                    {purchaseOrders.length > 10 && (
                      <div className="text-center pt-2">
                        <Link href={`/procurement/purchase-orders?supplierId=${id}`}>
                          <Button variant="outline" size="sm">
                            View All {purchaseOrders.length} Orders
                          </Button>
                        </Link>
                      </div>
                    )}
                  </div>
                )}
              </CardContent>
            </Card>
          </TabsContent>
        )}

        {/* Performance Tab */}
        <TabsContent value="performance">
          <div className="space-y-4">
            {/* Performance Overview Card */}
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <TrendingUp className="w-5 h-5" />
                  Performance Overview
                </CardTitle>
                <CardDescription>
                  Latest performance metrics and ratings
                </CardDescription>
              </CardHeader>
              <CardContent>
                {performanceLoading ? (
                  <p className="text-center text-gray-500 py-8">Loading performance data...</p>
                ) : performanceMetrics.length === 0 ? (
                  <div className="text-center py-8">
                    <BarChart3 className="w-12 h-12 mx-auto text-gray-400 mb-3" />
                    <p className="text-gray-500 mb-4">No performance metrics available yet</p>
                    <Button
                      onClick={async () => {
                        try {
                          const currentYear = new Date().getFullYear();
                          const currentMonth = new Date().getMonth() + 1;
                          await performanceTrackingService.calculateMetrics(id, 'Monthly', currentYear, currentMonth);
                          toast.success('Performance metrics calculated successfully');
                          loadPerformanceData();
                        } catch (error) {
                          toast.error('Failed to calculate performance metrics');
                        }
                      }}
                    >
                      Calculate Current Month Metrics
                    </Button>
                  </div>
                ) : (
                  <div className="space-y-6">
                    {/* Latest Metric Summary - Show Performance Review if available, otherwise Performance Metrics */}
                    {(performanceReviews.length > 0 || performanceMetrics[0]) && (
                      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                        <div className="text-center p-4 bg-blue-50 rounded-lg">
                          <div className="text-3xl font-bold text-blue-600">
                            {performanceReviews.length > 0
                              ? performanceReviews[0].overallGrade
                              : performanceMetrics[0]?.performanceGrade}
                          </div>
                          <div className="text-sm text-gray-600 mt-1">Overall Grade</div>
                          <div className="text-xs text-gray-500 mt-1">
                            {performanceReviews.length > 0
                              ? `${performanceReviews[0].overallScore.toFixed(1)}/5.0`
                              : `${performanceMetrics[0]?.overallPerformanceScore.toFixed(1)}%`}
                          </div>
                          <div className="text-xs text-gray-400 mt-1">
                            {performanceReviews.length > 0 ? 'From Review' : 'From Metrics'}
                          </div>
                        </div>
                        <div className="text-center p-4 bg-green-50 rounded-lg">
                          <div className="text-2xl font-bold text-green-600">
                            {performanceMetrics[0]?.onTimeDeliveryRate.toFixed(1) ?? '0.0'}%
                          </div>
                          <div className="text-sm text-gray-600 mt-1">On-Time Delivery</div>
                          <div className="text-xs text-gray-500 mt-1">
                            {performanceMetrics[0]?.onTimeDeliveries ?? 0}/{performanceMetrics[0]?.totalOrders ?? 0} orders
                          </div>
                        </div>
                        <div className="text-center p-4 bg-purple-50 rounded-lg">
                          <div className="text-2xl font-bold text-purple-600">
                            {performanceMetrics[0]?.qualityAcceptanceRate.toFixed(1) ?? '0.0'}%
                          </div>
                          <div className="text-sm text-gray-600 mt-1">Quality Rate</div>
                          <div className="text-xs text-gray-500 mt-1">
                            {performanceMetrics[0]?.defectRate.toFixed(1) ?? '0.0'}% defect rate
                          </div>
                        </div>
                        <div className="text-center p-4 bg-orange-50 rounded-lg">
                          <div className="text-2xl font-bold text-orange-600">
                            {performanceMetrics[0]?.complianceScore.toFixed(0) ?? '0'}%
                          </div>
                          <div className="text-sm text-gray-600 mt-1">Compliance</div>
                          <div className="text-xs text-gray-500 mt-1">
                            {performanceMetrics[0]?.contractViolations ?? 0} violations
                          </div>
                        </div>
                      </div>
                    )}

                    {/* Recent Performance History Table - Show Reviews if available, otherwise Metrics */}
                    <div>
                      <h3 className="font-semibold text-lg mb-3">Performance History</h3>
                      <div className="border rounded-lg overflow-hidden">
                        <table className="w-full">
                          <thead className="bg-gray-50">
                            <tr>
                              <th className="px-4 py-2 text-left text-sm font-medium text-gray-600">Period</th>
                              <th className="px-4 py-2 text-center text-sm font-medium text-gray-600">Grade</th>
                              <th className="px-4 py-2 text-center text-sm font-medium text-gray-600">Score</th>
                              <th className="px-4 py-2 text-center text-sm font-medium text-gray-600">Delivery</th>
                              <th className="px-4 py-2 text-center text-sm font-medium text-gray-600">Quality</th>
                              <th className="px-4 py-2 text-center text-sm font-medium text-gray-600">
                                {performanceReviews.length > 0 ? 'Status' : 'Orders'}
                              </th>
                            </tr>
                          </thead>
                          <tbody className="divide-y">
                            {performanceReviews.length > 0 ? (
                              performanceReviews.slice(0, 6).map((review) => (
                                <tr key={review.id} className="hover:bg-gray-50">
                                  <td className="px-4 py-3 text-sm">
                                    {review.reviewPeriod === 'Monthly' && review.reviewMonth
                                      ? format(new Date(getReviewYear(review), review.reviewMonth - 1), 'MMM yyyy')
                                      : review.reviewPeriod === 'Quarterly' && review.reviewQuarter
                                      ? `Q${review.reviewQuarter} ${getReviewYear(review)}`
                                      : getReviewYear(review)}
                                  </td>
                                  <td className="px-4 py-3 text-center">
                                    <Badge variant={
                                      review.overallGrade?.startsWith('A') ? 'default' :
                                      review.overallGrade?.startsWith('B') ? 'secondary' :
                                      review.overallGrade?.startsWith('C') ? 'outline' :
                                      'destructive'
                                    }>
                                      {review.overallGrade}
                                    </Badge>
                                  </td>
                                  <td className="px-4 py-3 text-center text-sm">
                                    {review.overallScore.toFixed(1)}/5.0
                                  </td>
                                  <td className="px-4 py-3 text-center text-sm">
                                    {review.deliveryPerformanceScore.toFixed(1)}
                                  </td>
                                  <td className="px-4 py-3 text-center text-sm">
                                    {review.qualityScore.toFixed(1)}
                                  </td>
                                  <td className="px-4 py-3 text-center text-sm">
                                    <Badge variant={
                                      review.status === 'Finalized' ? 'default' :
                                      review.status === 'Acknowledged' ? 'secondary' :
                                      review.status === 'Submitted' ? 'outline' :
                                      'secondary'
                                    }>
                                      {review.status}
                                    </Badge>
                                  </td>
                                </tr>
                              ))
                            ) : (
                              performanceMetrics.slice(0, 6).map((metric) => (
                                <tr key={metric.id} className="hover:bg-gray-50">
                                  <td className="px-4 py-3 text-sm">
                                    {metric.metricPeriod === 'Monthly' && metric.month
                                      ? format(new Date(metric.year, metric.month - 1), 'MMM yyyy')
                                      : metric.metricPeriod === 'Quarterly' && metric.quarter
                                      ? `Q${metric.quarter} ${metric.year}`
                                      : metric.year}
                                  </td>
                                  <td className="px-4 py-3 text-center">
                                    <Badge variant={
                                      metric.performanceGrade.startsWith('A') ? 'default' :
                                      metric.performanceGrade.startsWith('B') ? 'secondary' :
                                      metric.performanceGrade.startsWith('C') ? 'outline' :
                                      'destructive'
                                    }>
                                      {metric.performanceGrade}
                                    </Badge>
                                  </td>
                                  <td className="px-4 py-3 text-center text-sm">
                                    {metric.overallPerformanceScore.toFixed(1)}%
                                  </td>
                                  <td className="px-4 py-3 text-center text-sm">
                                    {metric.onTimeDeliveryRate.toFixed(1)}%
                                  </td>
                                  <td className="px-4 py-3 text-center text-sm">
                                    {metric.qualityAcceptanceRate.toFixed(1)}%
                                  </td>
                                  <td className="px-4 py-3 text-center text-sm">
                                    {metric.totalOrders}
                                  </td>
                                </tr>
                              ))
                            )}
                          </tbody>
                        </table>
                      </div>
                    </div>
                  </div>
                )}
              </CardContent>
            </Card>

            {/* Performance Trends Chart */}
            {performanceMetrics.length > 0 && (
              <PerformanceTrendsChart businessPartnerId={id} />
            )}

            {/* Quality Incidents Card */}
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <AlertTriangle className="w-5 h-5" />
                  Quality Incidents
                  {qualityIncidents.length > 0 && (
                    <Badge variant="secondary" className="ml-2">
                      {qualityIncidents.length}
                    </Badge>
                  )}
                </CardTitle>
              </CardHeader>
              <CardContent>
                {qualityIncidents.length === 0 ? (
                  <p className="text-center text-gray-500 py-8">No quality incidents recorded</p>
                ) : (
                  <div className="space-y-3">
                    {qualityIncidents.slice(0, 5).map((incident) => (
                      <div key={incident.id} className="p-4 border rounded-lg">
                        <div className="flex items-start justify-between mb-2">
                          <div>
                            <div className="font-semibold">{incident.incidentNumber}</div>
                            <div className="text-sm text-gray-600">{incident.incidentType}</div>
                          </div>
                          <Badge variant={
                            incident.status === 'Resolved' || incident.status === 'Closed' ? 'default' :
                            incident.status === 'Acknowledged' ? 'secondary' :
                            'destructive'
                          }>
                            {incident.status}
                          </Badge>
                        </div>
                        <p className="text-sm text-gray-700 mb-2">{incident.description}</p>
                        <div className="grid grid-cols-3 gap-2 text-xs text-gray-600">
                          <div>
                            <span className="font-medium">Date:</span> {format(new Date(incident.incidentDate), 'MMM dd, yyyy')}
                          </div>
                          <div>
                            <span className="font-medium">Severity:</span> {incident.severity}
                          </div>
                          <div>
                            <span className="font-medium">Cost:</span> ${incident.estimatedCost.toLocaleString()}
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>

            {/* Performance Reviews Card */}
            <Card>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Award className="w-5 h-5" />
                    <CardTitle>Performance Reviews</CardTitle>
                    {performanceReviews.length > 0 && (
                      <Badge variant="secondary" className="ml-2">
                        {performanceReviews.length}
                      </Badge>
                    )}
                  </div>
                  <Button
                    size="sm"
                    onClick={() => {
                      setSelectedReview(null);
                      setReviewDialogOpen(true);
                    }}
                  >
                    Create Review
                  </Button>
                </div>
              </CardHeader>
              <CardContent>
                {performanceReviews.length === 0 ? (
                  <p className="text-center text-gray-500 py-8">No performance reviews yet</p>
                ) : (
                  <div className="space-y-3">
                    {performanceReviews.map((review) => (
                      <div
                        key={review.id}
                        className="p-4 border rounded-lg hover:bg-gray-50 cursor-pointer transition-colors"
                        onClick={() => {
                          setSelectedReview(review);
                          setReviewDetailDialogOpen(true);
                        }}
                      >
                        <div className="flex items-start justify-between mb-2">
                          <div>
                            <div className="font-semibold">{review.reviewNumber}</div>
                            <div className="text-sm text-gray-600">
                              {review.reviewPeriod} {getReviewYear(review)}
                              {review.reviewMonth && ` - ${new Date(getReviewYear(review), review.reviewMonth - 1).toLocaleString('default', { month: 'long' })}`}
                              {review.reviewQuarter && ` - Q${review.reviewQuarter}`}
                            </div>
                          </div>
                          <div className="flex items-center gap-2">
                            <Badge variant={
                              review.status === 'Finalized' ? 'default' :
                              review.status === 'Submitted' ? 'secondary' :
                              'outline'
                            }>
                              {review.status}
                            </Badge>
                            {review.overallScore !== undefined && review.overallScore !== null && (
                              <Badge className={
                                review.overallScore >= 4.0 ? 'bg-green-100 text-green-800' :
                                review.overallScore >= 3.0 ? 'bg-blue-100 text-blue-800' :
                                review.overallScore >= 2.0 ? 'bg-yellow-100 text-yellow-800' :
                                'bg-red-100 text-red-800'
                              }>
                                {review.overallScore.toFixed(1)}
                              </Badge>
                            )}
                          </div>
                        </div>
                        <div className="text-sm text-gray-600">
                          <div>Reviewed: {format(new Date(review.reviewDate), 'MMM dd, yyyy')}</div>
                          {review.reviewedByName && <div>By: {review.reviewedByName}</div>}
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>
          </div>
        </TabsContent>
      </Tabs>

      {/* Suspend Dialog */}
      <Dialog open={rejectDialogOpen} onOpenChange={setRejectDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reject Business Partner</DialogTitle>
            <DialogDescription>
              Explain what the maker must correct before this partner can be submitted again.
            </DialogDescription>
          </DialogHeader>
          <Textarea
            value={rejectionReason}
            onChange={(event) => setRejectionReason(event.target.value)}
            placeholder="Required rejection reason"
          />
          <DialogFooter>
            <Button variant="outline" onClick={() => setRejectDialogOpen(false)}>Cancel</Button>
            <Button variant="destructive" onClick={handleRejectPartner} disabled={actionLoading || !rejectionReason.trim()}>
              {actionLoading ? 'Rejecting...' : 'Reject and return'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

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

      {/* Blacklist Dialog */}
      <Dialog open={blacklistDialogOpen} onOpenChange={setBlacklistDialogOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Blacklist Business Partner</DialogTitle>
            <DialogDescription>
              This will prevent the business partner from participating in any transactions. Please provide a reason.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="blacklist-reason">Reason for Blacklisting *</Label>
              <Textarea
                id="blacklist-reason"
                placeholder="Enter the reason for blacklisting this partner..."
                value={blacklistReason}
                onChange={(e) => setBlacklistReason(e.target.value)}
                rows={4}
                className="resize-none"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="blacklist-until">Blacklist Until (Optional)</Label>
              <Input
                id="blacklist-until"
                type="date"
                value={blacklistUntil}
                onChange={(e) => setBlacklistUntil(e.target.value)}
                min={new Date().toISOString().split('T')[0]}
              />
              <p className="text-xs text-gray-500">Leave empty for permanent blacklist</p>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => {
              setBlacklistDialogOpen(false);
              setBlacklistReason('');
              setBlacklistUntil('');
            }}>
              Cancel
            </Button>
            <Button onClick={handleBlacklist} disabled={actionLoading || !blacklistReason.trim()} variant="destructive">
              {actionLoading ? 'Blacklisting...' : 'Blacklist Partner'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Remove from Blacklist Dialog */}
      <Dialog open={removeBlacklistDialogOpen} onOpenChange={setRemoveBlacklistDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Remove from Blacklist</DialogTitle>
            <DialogDescription>
              Are you sure you want to remove this business partner from the blacklist? They will be able to participate in transactions again.
            </DialogDescription>
          </DialogHeader>
          {partner.blacklistReason && (
            <div className="bg-red-50 border border-red-200 rounded-lg p-4">
              <Label className="text-sm font-semibold text-red-900">Current Blacklist Reason:</Label>
              <p className="text-sm text-red-800 mt-1">{partner.blacklistReason}</p>
              {partner.blacklistDate && (
                <p className="text-xs text-red-600 mt-2">
                  Blacklisted on: {format(new Date(partner.blacklistDate), 'MMM dd, yyyy')}
                </p>
              )}
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setRemoveBlacklistDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleRemoveFromBlacklist} disabled={actionLoading} className="bg-green-600 hover:bg-green-700">
              {actionLoading ? 'Removing...' : 'Remove from Blacklist'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Performance Review Dialog */}
      <PerformanceReviewDialog
        open={reviewDialogOpen}
        onOpenChange={setReviewDialogOpen}
        businessPartnerId={id}
        review={selectedReview}
        onSuccess={loadPerformanceData}
      />

      {/* Performance Review Detail Dialog */}
      <PerformanceReviewDetailDialog
        open={reviewDetailDialogOpen}
        onOpenChange={setReviewDetailDialogOpen}
        review={selectedReview}
        onSuccess={loadPerformanceData}
        onEdit={() => {
          setReviewDetailDialogOpen(false);
          setReviewDialogOpen(true);
        }}
      />
    </div>
  );
}
