import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { PropertyEnquiryDetails } from './PropertyEnquiryDetails';

describe('PropertyEnquiryDetails', () => {
  it('shows the listed asset type and linked customer name instead of an internal id', () => {
    render(
      <PropertyEnquiryDetails
        property={{
          source: 'estate-public-listing', listingId: 'listing-1', listingReference: 'PROP-001',
          listingName: 'East Legon shop', listingType: 'Rent', assetType: 'Facility',
          currency: 'GHS', parentAssetId: 'asset-1', businessPartnerName: 'Public enquirer',
        }}
        prospect={{
          ticketId: 'ticket-1', leadId: 'lead-1', businessPartnerId: 'bp-123',
          businessPartnerName: 'Rhema Customer Ltd', status: 'Opportunity',
          agreedAmount: 2000, currency: 'GHS', depositRequirementType: 'Full',
          requiredDeposit: 2000, clearedDeposit: 0, depositThresholdMet: false,
        }}
      />
    );

    expect(screen.getByText('Facility')).toBeInTheDocument();
    expect(screen.getByText('Rhema Customer Ltd')).toBeInTheDocument();
    expect(screen.queryByText(/bp-123/)).not.toBeInTheDocument();
  });

  it('uses a readable fallback when the linked customer has no name', () => {
    render(
      <PropertyEnquiryDetails
        property={{
          source: 'estate-public-listing', listingId: 'listing-1', listingReference: 'PROP-001',
          listingName: 'East Legon shop', listingType: 'Rent', currency: 'GHS',
          parentAssetId: 'asset-1', businessPartnerId: 'bp-123', businessPartnerName: '',
        }}
        prospect={{
          ticketId: 'ticket-1', leadId: 'lead-1', businessPartnerId: 'bp-123',
          status: 'Opportunity', agreedAmount: 2000, currency: 'GHS',
          depositRequirementType: 'Full', requiredDeposit: 2000, clearedDeposit: 0,
          depositThresholdMet: false,
        }}
      />
    );
    expect(screen.getByText('Linked customer')).toBeInTheDocument();
    expect(screen.queryByText(/bp-123/)).not.toBeInTheDocument();
  });
});
