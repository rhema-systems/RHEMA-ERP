import React from 'react';
import type { PropertyListingContext } from '@/services/ehcTicketService';

export function PropertyEnquiryDetails({ property }: { property?: PropertyListingContext | null }) {
  if (!property) return null;
  return <section className="rounded-lg border bg-slate-50 p-4 space-y-2" aria-label="Property enquiry details">
    <h3 className="font-semibold">{property.listingName}</h3>
    <p className="text-sm text-slate-600">{property.listingReference} · {property.listingType}</p>
    <p className="text-sm">{property.location}</p>
    <p className="font-medium">{property.price == null ? 'Price on request' : new Intl.NumberFormat(undefined, {
      style: 'currency', currency: property.currency, maximumFractionDigits: 0,
    }).format(property.price)}</p>
    <div className="border-t pt-2 text-sm space-y-1">
      <p><span className="font-medium">Business partner:</span> {property.businessPartnerName}</p>
      <p><span className="font-medium">Contact:</span> {property.contactName}</p>
      {property.contactEmail && <p>{property.contactEmail}</p>}
      {property.contactPhone && <p>{property.contactPhone}</p>}
      {property.contactReference && <p><span className="font-medium">ID / card:</span> {property.contactReference}</p>}
    </div>
  </section>;
}
