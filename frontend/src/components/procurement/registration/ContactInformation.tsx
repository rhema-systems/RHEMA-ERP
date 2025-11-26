'use client';

import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { type RegistrationFormData } from '@/services/businessPartnerRegistrationService';
import { COUNTRIES } from '@/lib/countries';

interface ContactInformationProps {
  formData: RegistrationFormData;
  updateFormData: (data: Partial<RegistrationFormData>) => void;
}

export default function ContactInformation({ formData, updateFormData }: ContactInformationProps) {
  return (
    <div className="space-y-6">
      {/* Required Fields Notice */}
      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
        <p className="text-sm text-blue-800">
          <span className="text-red-500 font-bold">*</span> indicates required fields
        </p>
      </div>

      {/* Company Contact Information */}
      <div>
        <h3 className="text-lg font-semibold mb-4">Company Contact Information</h3>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {/* Email */}
          <div className="space-y-2">
            <Label htmlFor="email">
              Email Address <span className="text-red-500">*</span>
            </Label>
            <Input
              id="email"
              type="email"
              value={formData.email}
              onChange={(e) => updateFormData({ email: e.target.value })}
              placeholder="company@example.com"
              required
            />
          </div>

          {/* Phone */}
          <div className="space-y-2">
            <Label htmlFor="phone">
              Phone Number <span className="text-red-500">*</span>
            </Label>
            <Input
              id="phone"
              type="tel"
              value={formData.phone}
              onChange={(e) => updateFormData({ phone: e.target.value })}
              placeholder="+1 (555) 123-4567"
              required
            />
          </div>

          {/* Alternate Phone */}
          <div className="space-y-2">
            <Label htmlFor="alternatePhone">Alternate Phone</Label>
            <Input
              id="alternatePhone"
              type="tel"
              value={formData.alternatePhone || ''}
              onChange={(e) => updateFormData({ alternatePhone: e.target.value })}
              placeholder="+1 (555) 987-6543"
            />
          </div>
        </div>
      </div>

      {/* Physical Address */}
      <div>
        <h3 className="text-lg font-semibold mb-4">Physical Address</h3>
        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="physicalAddress">
              Street Address <span className="text-red-500">*</span>
            </Label>
            <Textarea
              id="physicalAddress"
              value={formData.physicalAddress || ''}
              onChange={(e) => updateFormData({ physicalAddress: e.target.value })}
              placeholder="Enter street address"
              rows={3}
              required
            />
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="space-y-2">
              <Label htmlFor="city">
                City <span className="text-red-500">*</span>
              </Label>
              <Input
                id="city"
                value={formData.city || ''}
                onChange={(e) => updateFormData({ city: e.target.value })}
                placeholder="Enter city"
                required
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="postalCode">Postal Code</Label>
              <Input
                id="postalCode"
                value={formData.postalCode || ''}
                onChange={(e) => updateFormData({ postalCode: e.target.value })}
                placeholder="Enter postal code"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="country">
                Country <span className="text-red-500">*</span>
              </Label>
              <Select
                value={formData.country || ''}
                onValueChange={(value) => updateFormData({ country: value })}
              >
                <SelectTrigger id="country">
                  <SelectValue placeholder="Select country">
                    {formData.country && (
                      <span className="flex items-center gap-2">
                        <span className="text-xl">
                          {COUNTRIES.find(c => c.name === formData.country)?.flag}
                        </span>
                        <span>{formData.country}</span>
                      </span>
                    )}
                  </SelectValue>
                </SelectTrigger>
                <SelectContent className="max-h-[300px]">
                  {COUNTRIES.map((country) => (
                    <SelectItem key={country.code} value={country.name}>
                      <span className="flex items-center gap-2">
                        <span className="text-xl">{country.flag}</span>
                        <span>{country.name}</span>
                      </span>
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
        </div>
      </div>

      {/* Primary Contact Person */}
      <div>
        <h3 className="text-lg font-semibold mb-4">Primary Contact Person</h3>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="contactPersonName">Full Name</Label>
            <Input
              id="contactPersonName"
              value={formData.contactPersonName || ''}
              onChange={(e) => updateFormData({ contactPersonName: e.target.value })}
              placeholder="Enter contact person name"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="contactPersonTitle">Job Title</Label>
            <Input
              id="contactPersonTitle"
              value={formData.contactPersonTitle || ''}
              onChange={(e) => updateFormData({ contactPersonTitle: e.target.value })}
              placeholder="e.g., General Manager"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="contactPersonEmail">Email</Label>
            <Input
              id="contactPersonEmail"
              type="email"
              value={formData.contactPersonEmail || ''}
              onChange={(e) => updateFormData({ contactPersonEmail: e.target.value })}
              placeholder="contact@example.com"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="contactPersonPhone">Phone</Label>
            <Input
              id="contactPersonPhone"
              type="tel"
              value={formData.contactPersonPhone || ''}
              onChange={(e) => updateFormData({ contactPersonPhone: e.target.value })}
              placeholder="+1 (555) 123-4567"
            />
          </div>
        </div>
      </div>

      {/* Banking Information */}
      <div>
        <h3 className="text-lg font-semibold mb-4">Banking Information (Optional)</h3>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="bankName">Bank Name</Label>
            <Input
              id="bankName"
              value={formData.bankName || ''}
              onChange={(e) => updateFormData({ bankName: e.target.value })}
              placeholder="Enter bank name"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="bankAccountNumber">Account Number</Label>
            <Input
              id="bankAccountNumber"
              value={formData.bankAccountNumber || ''}
              onChange={(e) => updateFormData({ bankAccountNumber: e.target.value })}
              placeholder="Enter account number"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="bankBranchCode">Branch Code / Routing Number</Label>
            <Input
              id="bankBranchCode"
              value={formData.bankBranchCode || ''}
              onChange={(e) => updateFormData({ bankBranchCode: e.target.value })}
              placeholder="Enter branch code"
            />
          </div>
        </div>
      </div>
    </div>
  );
}

