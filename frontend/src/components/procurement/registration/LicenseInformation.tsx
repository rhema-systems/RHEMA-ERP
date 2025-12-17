'use client';

import { useState, useEffect } from 'react';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Plus, X, Award } from 'lucide-react';
import { type RegistrationFormData } from '@/services/businessPartnerRegistrationService';
import { licenseTypeService, type LicenseTypeDto } from '@/services/partnerConfigService';
import { toast } from 'sonner';

interface LicenseInformationProps {
  formData: RegistrationFormData;
  updateFormData: (data: Partial<RegistrationFormData>) => void;
}

export default function LicenseInformation({ formData, updateFormData }: LicenseInformationProps) {
  const [licenseTypes, setLicenseTypes] = useState<LicenseTypeDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedLicenseTypeId, setSelectedLicenseTypeId] = useState('');
  const [licenseNumber, setLicenseNumber] = useState('');
  const [issueDate, setIssueDate] = useState('');
  const [expiryDate, setExpiryDate] = useState('');
  const [issuingAuthority, setIssuingAuthority] = useState('');

  const licenses = formData.licenses || [];

  useEffect(() => {
    loadLicenseTypes();
  }, []);

  const loadLicenseTypes = async () => {
    try {
      const data = await licenseTypeService.getActive();
      setLicenseTypes(data);
    } catch (error) {
      console.error('Error loading license types:', error);
      toast.error('Failed to load license types');
    } finally {
      setLoading(false);
    }
  };

  const handleAddLicense = () => {
    if (!selectedLicenseTypeId) {
      toast.error('Please select a license type');
      return;
    }

    if (!licenseNumber) {
      toast.error('Please enter a license number');
      return;
    }

    if (!issueDate) {
      toast.error('Please enter an issue date');
      return;
    }

    if (!issuingAuthority) {
      toast.error('Please enter the issuing authority');
      return;
    }

    const newLicense = {
      licenseTypeId: selectedLicenseTypeId,
      licenseNumber,
      issueDate,
      expiryDate: expiryDate || undefined,
      issuingAuthority,
    };

    updateFormData({
      licenses: [...licenses, newLicense],
    });

    // Reset form
    setSelectedLicenseTypeId('');
    setLicenseNumber('');
    setIssueDate('');
    setExpiryDate('');
    setIssuingAuthority('');
    toast.success('License added successfully');
  };

  const handleRemoveLicense = (index: number) => {
    const updatedLicenses = licenses.filter((_, i) => i !== index);
    updateFormData({ licenses: updatedLicenses });
    toast.success('License removed');
  };

  const getLicenseTypeName = (licenseTypeId: string): string => {
    const licenseType = licenseTypes.find((lt) => lt.id === licenseTypeId);
    return licenseType?.licenseName || 'Unknown License';
  };

  // Show message if partner type is Supplier (licenses typically for contractors)
  if (formData.partnerType === 'Supplier') {
    return (
      <div className="space-y-6">
        <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
          <h3 className="font-semibold text-blue-900 mb-2">License Information</h3>
          <p className="text-sm text-blue-800">
            License information is typically required for contractors. As a supplier, you may skip this step
            or add any relevant certifications or licenses your company holds.
          </p>
        </div>

        <div className="text-center py-8">
          <Award className="w-12 h-12 mx-auto mb-3 text-gray-400" />
          <p className="text-gray-600">This section is optional for suppliers</p>
          <p className="text-sm text-gray-500 mt-1">You can proceed to the next step</p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
        <h3 className="font-semibold text-blue-900 mb-2">License Requirements</h3>
        <ul className="text-sm text-blue-800 space-y-1">
          <li>• Add all relevant professional licenses and certifications</li>
          <li>• Ensure license information is accurate and up-to-date</li>
          <li>• Include expiry dates where applicable</li>
          <li>• Contractors must provide all mandatory licenses</li>
        </ul>
      </div>

      {/* Add License Form */}
      <Card>
        <CardContent className="pt-6">
          <div className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="licenseType">License Type</Label>
                <Select
                  value={selectedLicenseTypeId}
                  onValueChange={setSelectedLicenseTypeId}
                  disabled={loading}
                >
                  <SelectTrigger id="licenseType">
                    <SelectValue placeholder={loading ? 'Loading...' : 'Select license type'} />
                  </SelectTrigger>
                  <SelectContent>
                    {licenseTypes.map((type) => (
                      <SelectItem key={type.id} value={type.id}>
                        {type.licenseName}
                        {type.isMandatory && <Badge className="ml-2 text-xs">Required</Badge>}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label htmlFor="licenseNumber">License Number</Label>
                <Input
                  id="licenseNumber"
                  value={licenseNumber}
                  onChange={(e) => setLicenseNumber(e.target.value)}
                  placeholder="Enter license number"
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="issueDate">Issue Date</Label>
                <Input
                  id="issueDate"
                  type="date"
                  value={issueDate}
                  onChange={(e) => setIssueDate(e.target.value)}
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="expiryDate">Expiry Date (Optional)</Label>
                <Input
                  id="expiryDate"
                  type="date"
                  value={expiryDate}
                  onChange={(e) => setExpiryDate(e.target.value)}
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="issuingAuthority">Issuing Authority</Label>
              <Input
                id="issuingAuthority"
                value={issuingAuthority}
                onChange={(e) => setIssuingAuthority(e.target.value)}
                placeholder="Enter issuing authority"
              />
            </div>

            <Button
              onClick={handleAddLicense}
              disabled={!selectedLicenseTypeId || !licenseNumber || !issueDate || !issuingAuthority}
              className="w-full"
            >
              <Plus className="w-4 h-4 mr-2" />
              Add License
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Added Licenses List */}
      {licenses.length > 0 && (
        <div>
          <h3 className="text-lg font-semibold mb-3">Added Licenses ({licenses.length})</h3>
          <div className="space-y-2">
            {licenses.map((license, index) => (
              <Card key={index}>
                <CardContent className="p-4">
                  <div className="flex items-start justify-between">
                    <div className="flex-1">
                      <div className="flex items-center gap-2 mb-2">
                        <Award className="w-5 h-5 text-blue-600" />
                        <p className="font-medium">{getLicenseTypeName(license.licenseTypeId)}</p>
                      </div>
                      <div className="grid grid-cols-2 gap-2 text-sm text-gray-600">
                        <div>
                          <span className="font-medium">License #:</span> {license.licenseNumber}
                        </div>
                        <div>
                          <span className="font-medium">Issued:</span>{' '}
                          {new Date(license.issueDate).toLocaleDateString()}
                        </div>
                        {license.expiryDate && (
                          <div>
                            <span className="font-medium">Expires:</span>{' '}
                            {new Date(license.expiryDate).toLocaleDateString()}
                          </div>
                        )}
                        <div>
                          <span className="font-medium">Authority:</span> {license.issuingAuthority}
                        </div>
                      </div>
                    </div>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => handleRemoveLicense(index)}
                    >
                      <X className="w-4 h-4" />
                    </Button>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        </div>
      )}

      {licenses.length === 0 && formData.partnerType !== 'Supplier' && (
        <div className="text-center py-8 text-gray-500">
          <Award className="w-12 h-12 mx-auto mb-3 text-gray-400" />
          <p>No licenses added yet</p>
          <p className="text-sm">Add licenses using the form above</p>
        </div>
      )}
    </div>
  );
}

