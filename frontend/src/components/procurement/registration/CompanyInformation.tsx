'use client';

import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { type RegistrationFormData } from '@/services/businessPartnerRegistrationService';

interface CompanyInformationProps {
  formData: RegistrationFormData;
  updateFormData: (data: Partial<RegistrationFormData>) => void;
}

export default function CompanyInformation({
  formData,
  updateFormData,
}: CompanyInformationProps) {
  return (
    <div className="space-y-6">
      {/* Required Fields Notice */}
      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
        <p className="text-sm text-blue-800">
          <span className="text-red-500 font-bold">*</span> indicates required
          fields
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Partner Type */}
        <div className="space-y-2">
          <Label htmlFor="partnerType">
            Partner Type <span className="text-red-500">*</span>
          </Label>
          <Select
            value={formData.partnerType}
            onValueChange={(value) => updateFormData({ partnerType: value })}
          >
            <SelectTrigger id="partnerType">
              <SelectValue placeholder="Select partner type" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="Supplier">Supplier</SelectItem>
              <SelectItem value="Contractor">Contractor</SelectItem>
              <SelectItem value="Both">Both (Supplier & Contractor)</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-2">
          <Label htmlFor="registrationCategory">
            Registration Category <span className="text-red-500">*</span>
          </Label>
          <Select
            value={formData.registrationCategory}
            onValueChange={(value) =>
              updateFormData({
                registrationCategory: value as 'Goods' | 'Works' | 'Services',
              })
            }
          >
            <SelectTrigger id="registrationCategory">
              <SelectValue placeholder="Select Goods, Works, or Services" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="Goods">Goods</SelectItem>
              <SelectItem value="Works">Works</SelectItem>
              <SelectItem value="Services">Services</SelectItem>
            </SelectContent>
          </Select>
          <p className="text-xs text-muted-foreground">
            This selects the Published TDC evidence pack used for submission.
          </p>
        </div>

        {/* Company Name */}
        <div className="space-y-2">
          <Label htmlFor="companyName">
            Company Name <span className="text-red-500">*</span>
          </Label>
          <Input
            id="companyName"
            value={formData.companyName}
            onChange={(e) => updateFormData({ companyName: e.target.value })}
            placeholder="Enter company name"
            required
          />
        </div>

        {/* Trading Name */}
        <div className="space-y-2">
          <Label htmlFor="tradingName">Trading Name</Label>
          <Input
            id="tradingName"
            value={formData.tradingName || ''}
            onChange={(e) => updateFormData({ tradingName: e.target.value })}
            placeholder="Enter trading name (if different)"
          />
        </div>

        {/* Registration Number */}
        <div className="space-y-2">
          <Label htmlFor="registrationNumber">
            Company Registration Number
          </Label>
          <Input
            id="registrationNumber"
            value={formData.registrationNumber || ''}
            onChange={(e) =>
              updateFormData({ registrationNumber: e.target.value })
            }
            placeholder="Enter registration number"
          />
        </div>

        {/* Tax Number */}
        <div className="space-y-2">
          <Label htmlFor="taxNumber">Tax Number</Label>
          <Input
            id="taxNumber"
            value={formData.taxNumber || ''}
            onChange={(e) => updateFormData({ taxNumber: e.target.value })}
            placeholder="Enter tax number"
          />
        </div>

        {/* VAT Number */}
        <div className="space-y-2">
          <Label htmlFor="vatNumber">VAT Number</Label>
          <Input
            id="vatNumber"
            value={formData.vatNumber || ''}
            onChange={(e) => updateFormData({ vatNumber: e.target.value })}
            placeholder="Enter VAT number"
          />
        </div>

        {/* Industry Type */}
        <div className="space-y-2">
          <Label htmlFor="industryType">Industry Type</Label>
          <Input
            id="industryType"
            value={formData.industryType || ''}
            onChange={(e) => updateFormData({ industryType: e.target.value })}
            placeholder="e.g., Construction, IT Services"
          />
        </div>

        {/* Years in Business */}
        <div className="space-y-2">
          <Label htmlFor="yearsInBusiness">Years in Business</Label>
          <Input
            id="yearsInBusiness"
            type="number"
            value={formData.yearsInBusiness || ''}
            onChange={(e) =>
              updateFormData({
                yearsInBusiness: parseInt(e.target.value) || undefined,
              })
            }
            placeholder="Enter years in business"
            min="0"
          />
        </div>

        {/* Number of Employees */}
        <div className="space-y-2">
          <Label htmlFor="numberOfEmployees">Number of Employees</Label>
          <Input
            id="numberOfEmployees"
            type="number"
            value={formData.numberOfEmployees || ''}
            onChange={(e) =>
              updateFormData({
                numberOfEmployees: parseInt(e.target.value) || undefined,
              })
            }
            placeholder="Enter number of employees"
            min="1"
          />
        </div>

        {/* Annual Revenue */}
        <div className="space-y-2">
          <Label htmlFor="annualRevenue">Annual Revenue (USD)</Label>
          <Input
            id="annualRevenue"
            type="number"
            value={formData.annualRevenue || ''}
            onChange={(e) =>
              updateFormData({
                annualRevenue: parseFloat(e.target.value) || undefined,
              })
            }
            placeholder="Enter annual revenue"
            min="0"
            step="0.01"
          />
        </div>
      </div>

      {/* Website */}
      <div className="space-y-2">
        <Label htmlFor="website">Website</Label>
        <Input
          id="website"
          type="url"
          value={formData.website || ''}
          onChange={(e) => updateFormData({ website: e.target.value })}
          placeholder="https://www.example.com"
        />
      </div>
    </div>
  );
}
