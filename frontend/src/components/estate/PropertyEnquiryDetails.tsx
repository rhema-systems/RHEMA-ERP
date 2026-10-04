import React from 'react';
import type { PropertyListingContext } from '@/services/ehcTicketService';
import type { PropertyEnquiryProspect } from '@/services/propertyEnquiryService';

export function PropertyEnquiryDetails({
  property,
  prospect,
  createdAt,
  assignedToName,
}: {
  property?: PropertyListingContext | null;
  prospect?: PropertyEnquiryProspect | null;
  createdAt?: string | null;
  assignedToName?: string | null;
}) {
  if (!property && !prospect) return null;

  return (
    <section
      className="space-y-4 rounded-lg border bg-muted p-4 text-foreground"
      aria-label="Property enquiry details"
    >
      {property ? (
        <div className="space-y-2">
          <h3 className="font-semibold">{property.listingName}</h3>
          <p className="text-sm text-muted-foreground">
            {property.listingReference} · {property.listingType}
          </p>
          <p className="text-sm">
            <span className="font-medium">Asset type:</span>{' '}
            {property.assetType || 'Not recorded'}
          </p>
          <p className="text-sm">
            {property.location || 'Location not recorded'}
          </p>
          <p className="font-medium">
            {property.price == null
              ? 'Price on request'
              : new Intl.NumberFormat(undefined, {
                  style: 'currency',
                  currency: property.currency,
                  maximumFractionDigits: 0,
                }).format(property.price)}
          </p>
        </div>
      ) : null}
      <div className="grid gap-3 border-t pt-3 text-sm md:grid-cols-2">
        <div className="space-y-1">
          <p className="font-semibold">Original enquirer</p>
          <p>{property?.contactName || 'Not recorded'}</p>
          <p>{property?.contactEmail || 'No email recorded'}</p>
          <p>{property?.contactPhone || 'No phone recorded'}</p>
          {property?.alternativePhoneNumber ? (
            <p>{property.alternativePhoneNumber}</p>
          ) : null}
          {property?.preferredContactMethod ? (
            <p>
              <span className="font-medium">Preferred contact:</span>{' '}
              {property.preferredContactMethod}
            </p>
          ) : null}
        </div>
        <div className="space-y-1">
          <p className="font-semibold">Enquiry record</p>
          <p>
            <span className="font-medium">Source:</span>{' '}
            {property?.source || 'Public property listing'}
          </p>
          <p>
            <span className="font-medium">Submitted:</span>{' '}
            {createdAt ? new Date(createdAt).toLocaleString() : 'Not recorded'}
          </p>
          <p>
            <span className="font-medium">Assigned to:</span>{' '}
            {assignedToName || 'Unassigned'}
          </p>
          <p>
            <span className="font-medium">Business partner:</span>{' '}
            {prospect?.businessPartnerId
              ? prospect.businessPartnerName ||
                (property?.businessPartnerId === prospect.businessPartnerId
                  ? property.businessPartnerName
                  : null) ||
                'Linked customer'
              : property?.businessPartnerId
                ? property.businessPartnerName || 'Linked customer'
                : 'Public prospect — not linked'}
          </p>
          {prospect?.businessPartnerLinkedAt ? (
            <p>
              <span className="font-medium">Customer status:</span> Linked{' '}
              {new Date(prospect.businessPartnerLinkedAt).toLocaleString()}
            </p>
          ) : null}
        </div>
      </div>
    </section>
  );
}
