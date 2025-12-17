'use client';

import { useState, useEffect } from 'react';
import { type TenderFormData } from '@/app/procurement/tenders/new/page';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { UserPlus, Trash2, Mail, Search, Users } from 'lucide-react';
import { toast } from 'sonner';
import { tenderService, type InviteTenderersDto } from '@/services/tenderService';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';

interface TenderInvitationsProps {
  formData: TenderFormData;
  updateFormData: (data: Partial<TenderFormData>) => void;
  tenderId: string | null;
}

export default function TenderInvitations({ formData, updateFormData, tenderId }: TenderInvitationsProps) {
  const [businessPartners, setBusinessPartners] = useState<BusinessPartnerDto[]>([]);
  const [filteredPartners, setFilteredPartners] = useState<BusinessPartnerDto[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedPartnerIds, setSelectedPartnerIds] = useState<string[]>([]);
  // Don't send notifications during tender creation/editing - only when publishing
  const [sendNotifications, setSendNotifications] = useState(false);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    loadBusinessPartners();
  }, []);

  useEffect(() => {
    if (searchTerm) {
      const filtered = businessPartners.filter(
        (partner) =>
          partner.partnerName.toLowerCase().includes(searchTerm.toLowerCase()) ||
          partner.partnerCode.toLowerCase().includes(searchTerm.toLowerCase()) ||
          partner.email?.toLowerCase().includes(searchTerm.toLowerCase())
      );
      setFilteredPartners(filtered);
    } else {
      setFilteredPartners(businessPartners);
    }
  }, [searchTerm, businessPartners]);

  const loadBusinessPartners = async () => {
    try {
      setLoading(true);
      const partners = await businessPartnerService.getActivePartners();
      setBusinessPartners(partners);
      setFilteredPartners(partners);
    } catch (error: any) {
      console.error('Error loading business partners:', error);
      toast.error('Failed to load business partners');
    } finally {
      setLoading(false);
    }
  };

  const handleTogglePartner = (partnerId: string) => {
    setSelectedPartnerIds((prev) =>
      prev.includes(partnerId)
        ? prev.filter((id) => id !== partnerId)
        : [...prev, partnerId]
    );
  };

  const handleSelectAll = () => {
    if (selectedPartnerIds.length === filteredPartners.length) {
      setSelectedPartnerIds([]);
    } else {
      setSelectedPartnerIds(filteredPartners.map((p) => p.id));
    }
  };

  const handleInvite = async () => {
    if (selectedPartnerIds.length === 0) {
      toast.error('Please select at least one business partner to invite');
      return;
    }

    if (!tenderId) {
      toast.error('Please save the tender first before sending invitations');
      return;
    }

    console.log('TenderInvitations - handleInvite called');
    console.log('TenderInvitations - tenderId:', tenderId);
    console.log('TenderInvitations - selectedPartnerIds:', selectedPartnerIds);
    console.log('TenderInvitations - sendNotifications:', sendNotifications);

    try {
      setLoading(true);
      const inviteDto: InviteTenderersDto = {
        businessPartnerIds: selectedPartnerIds,
        sendNotifications,
      };

      await tenderService.inviteTenderers(tenderId, inviteDto);

      // Add to local state
      const invitedPartners = businessPartners.filter((p) => selectedPartnerIds.includes(p.id));
      const newInvitations = invitedPartners.map((partner) => ({
        id: crypto.randomUUID(),
        businessPartnerId: partner.id,
        businessPartnerName: partner.partnerName,
        invitedDate: new Date().toISOString(),
        status: 'Invited',
      }));

      updateFormData({
        invitations: [...formData.invitations, ...newInvitations],
      });

      toast.success(`Successfully invited ${selectedPartnerIds.length} business partner(s)`);
      setSelectedPartnerIds([]);
    } catch (error: any) {
      console.error('TenderInvitations - Error inviting partners:', error);
      toast.error(error.message || 'Failed to send invitations');
    } finally {
      setLoading(false);
    }
  };

  const formatDate = (dateString: string) => {
    if (!dateString) return '';
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' });
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Invited':
        return 'bg-blue-100 text-blue-700';
      case 'Viewed':
        return 'bg-yellow-100 text-yellow-700';
      case 'Submitted':
        return 'bg-green-100 text-green-700';
      case 'Declined':
        return 'bg-red-100 text-red-700';
      default:
        return 'bg-gray-100 text-gray-700';
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold">Tender Invitations</h3>
        <p className="text-sm text-gray-500">
          Invite business partners to participate in this tender
        </p>
      </div>

      {/* Business Partner Selection */}
      {tenderId && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Select Business Partners</CardTitle>
            <CardDescription>
              Choose business partners to invite to this tender
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {/* Search */}
            <div className="flex gap-2">
              <div className="flex-1 relative">
                <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-gray-400" />
                <Input
                  placeholder="Search by name, code, or email..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="pl-10"
                />
              </div>
              <Button
                type="button"
                variant="outline"
                onClick={handleSelectAll}
              >
                {selectedPartnerIds.length === filteredPartners.length ? 'Deselect All' : 'Select All'}
              </Button>
            </div>

            {/* Partner List */}
            <div className="border rounded-lg max-h-96 overflow-y-auto">
              {loading ? (
                <div className="p-8 text-center text-gray-500">
                  Loading business partners...
                </div>
              ) : filteredPartners.length === 0 ? (
                <div className="p-8 text-center text-gray-500">
                  No business partners found
                </div>
              ) : (
                <div className="divide-y">
                  {filteredPartners.map((partner) => {
                    const isInvited = formData.invitations.some(
                      (inv) => inv.businessPartnerId === partner.id
                    );
                    const isSelected = selectedPartnerIds.includes(partner.id);

                    return (
                      <div
                        key={partner.id}
                        className={`p-3 hover:bg-gray-50 ${
                          isInvited ? 'bg-gray-100 opacity-60' : ''
                        }`}
                      >
                        <label className="flex items-start gap-3 cursor-pointer">
                          <input
                            type="checkbox"
                            checked={isSelected}
                            onChange={() => handleTogglePartner(partner.id)}
                            disabled={isInvited}
                            className="mt-1 h-4 w-4 rounded border-gray-300"
                          />
                          <div className="flex-1">
                            <div className="flex items-center gap-2">
                              <span className="font-medium">{partner.partnerName}</span>
                              <span className="text-xs text-gray-500">({partner.partnerCode})</span>
                              {isInvited && (
                                <span className="px-2 py-0.5 text-xs bg-green-100 text-green-700 rounded">
                                  Already Invited
                                </span>
                              )}
                            </div>
                            <div className="text-sm text-gray-600 mt-1">
                              {partner.email && <span>{partner.email}</span>}
                              {partner.phone && <span className="ml-3">{partner.phone}</span>}
                            </div>
                            {partner.performanceRating && (
                              <div className="text-xs text-gray-500 mt-1">
                                Rating: {partner.performanceRating.toFixed(1)} / 5.0
                              </div>
                            )}
                          </div>
                        </label>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>

            {/* Info about notifications */}
            <div className="rounded-lg border bg-blue-50 border-blue-200 p-3">
              <div className="flex gap-2">
                <Mail className="h-5 w-5 text-blue-600 flex-shrink-0 mt-0.5" />
                <div className="text-sm text-blue-800">
                  <p className="font-medium">About Notifications</p>
                  <p className="text-xs mt-1">
                    Email and in-app notifications will be sent to invited suppliers when you <strong>publish the tender</strong>, not when you add them to the invitation list.
                  </p>
                </div>
              </div>
            </div>

            {/* Invite Button */}
            <Button
              type="button"
              onClick={handleInvite}
              disabled={selectedPartnerIds.length === 0 || loading}
              className="w-full"
            >
              <UserPlus className="h-4 w-4 mr-2" />
              Invite {selectedPartnerIds.length > 0 ? `${selectedPartnerIds.length} ` : ''}
              Business Partner{selectedPartnerIds.length !== 1 ? 's' : ''}
            </Button>
          </CardContent>
        </Card>
      )}

      {/* No Tender ID Warning */}
      {!tenderId && (
        <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
          <p className="text-sm text-yellow-800">
            Please save the tender first before sending invitations. You can skip this step and send invitations later.
          </p>
        </div>
      )}

      {/* Invited Partners List */}
      {formData.invitations.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              Invited Business Partners ({formData.invitations.length})
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {formData.invitations.map((invitation, index) => (
                <div
                  key={index}
                  className="p-4 border rounded-lg"
                >
                  <div className="flex items-start justify-between">
                    <div className="flex-1">
                      <div className="flex items-center gap-2">
                        <Users className="h-4 w-4 text-gray-500" />
                        <h4 className="font-medium">{invitation.businessPartnerName}</h4>
                        <span className={`px-2 py-0.5 text-xs rounded ${getStatusColor(invitation.status)}`}>
                          {invitation.status}
                        </span>
                      </div>
                      <div className="mt-2 space-y-1 text-sm text-gray-600">
                        <p>Invited: {formatDate(invitation.invitedDate)}</p>
                        {invitation.viewedDate && (
                          <p>Viewed: {formatDate(invitation.viewedDate)}</p>
                        )}
                        {invitation.responseDate && (
                          <p>Responded: {formatDate(invitation.responseDate)}</p>
                        )}
                        {invitation.declineReason && (
                          <p className="text-red-600">Decline Reason: {invitation.declineReason}</p>
                        )}
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Empty State */}
      {formData.invitations.length === 0 && (
        <div className="text-center py-12 text-gray-500 border-2 border-dashed border-gray-300 rounded-lg">
          <Mail className="h-12 w-12 mx-auto mb-4 text-gray-400" />
          <p>No business partners invited yet</p>
          <p className="text-sm mt-2">
            {tenderId
              ? 'Select business partners above to send invitations'
              : 'Save the tender first to send invitations'}
          </p>
        </div>
      )}
    </div>
  );
}


