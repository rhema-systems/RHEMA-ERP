'use client';

import React, { useEffect, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import ReCAPTCHA from 'react-google-recaptcha';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { settingsService } from '@/services/settings';
import { externalEstateListingsService, type ExternalEstateListing, type ExternalListingEnquiry, type EnquiryPartnerProfile } from '@/services/external-estate-listings.service';

export function PropertyEnquiryDialog({ listing, onClose, onCreated, publicMode = false }: {
  listing: ExternalEstateListing;
  onClose: () => void;
  onCreated: (ticket: ExternalListingEnquiry) => void;
  publicMode?: boolean;
}) {
  const [message, setMessage] = useState('');
  const [contactName, setContactName] = useState('');
  const [contactEmail, setContactEmail] = useState('');
  const [contactPhone, setContactPhone] = useState('');
  const [contactReference, setContactReference] = useState('');
  const [profiles, setProfiles] = useState<EnquiryPartnerProfile[]>([]);
  const [partnerId, setPartnerId] = useState('');
  const [profilesLoading, setProfilesLoading] = useState(!publicMode);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [submissionId] = useState(() => crypto.randomUUID());
  const [captchaToken, setCaptchaToken] = useState<string | null>(null);
  const captcha = useRef<ReCAPTCHA>(null);
  const { data: security, isLoading: securityLoading, isError: securityError } = useQuery({
    queryKey: ['security', 'public'], queryFn: () => settingsService.getPublicSecuritySettings(),
  });
  useEffect(() => {
    if (publicMode) {
      setProfilesLoading(false);
      return;
    }
    let active = true;
    externalEstateListingsService.getEnquiryProfiles().then(items => {
      if (!active) return;
      setProfiles(items); setPartnerId(items[0]?.id || ''); setProfilesLoading(false);
    }).catch(() => { if (active) { setError('Could not load your business partner details. Close this dialog and try again.'); } });
    return () => { active = false; };
  }, [publicMode]);
  const isRent = listing.externalListingType === 'Rent' || listing.externalListingType === 'SaleAndRent';
  const isLease = listing.externalListingType === 'Lease' || listing.externalListingType === 'SaleAndLease';
  const price = isRent || isLease
    ? listing.externalMonthlyRent ?? listing.externalListingPrice
    : listing.externalSalePrice ?? listing.externalListingPrice;
  const listingLabel = listing.externalListingType === 'Sale'
    ? 'For sale'
    : isLease
      ? listing.externalListingType === 'SaleAndLease'
        ? 'For sale or lease'
        : 'For lease'
      : listing.externalListingType === 'SaleAndRent'
        ? 'For sale or rent'
        : 'For rent';
  const chargeCadence = isLease ? ' / year' : isRent ? ' / month' : '';
  const formattedPrice = price == null ? 'Price on request' : new Intl.NumberFormat(undefined, {
    style: 'currency', currency: listing.externalListingCurrency || 'GHS', maximumFractionDigits: 0,
  }).format(price);
  const submit = async () => {
    if (busy || !message.trim()) return;
    if (publicMode && (!contactName.trim() || !contactPhone.trim())) {
      setError('Enter your name and phone number before sending the enquiry.');
      return;
    }
    setBusy(true); setError(null);
    try {
      const payload = {
        submissionId,
        message: message.trim(),
        businessPartnerId: publicMode ? undefined : partnerId || undefined,
        captchaToken: captchaToken || undefined,
        contactName: publicMode ? contactName.trim() : undefined,
        contactEmail: publicMode && contactEmail.trim() ? contactEmail.trim() : undefined,
        contactPhone: publicMode ? contactPhone.trim() : undefined,
        contactReference: publicMode && contactReference.trim() ? contactReference.trim() : undefined,
      };
      const ticket = publicMode
        ? await externalEstateListingsService.createPublicEnquiry(listing.id, payload)
        : await externalEstateListingsService.createEnquiry(listing.id, payload);
      onCreated(ticket);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'The enquiry could not be sent. Your message has been retained.');
      captcha.current?.reset(); setCaptchaToken(null);
    } finally { setBusy(false); }
  };
  return <Dialog open onOpenChange={open => { if (!open && !busy) onClose(); }}>
    <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-xl" onInteractOutside={e => { if (busy) e.preventDefault(); }} onEscapeKeyDown={e => { if (busy) e.preventDefault(); }}>
      <DialogHeader>
        <DialogTitle>Enquire about this property</DialogTitle>
        <DialogDescription>Send your questions to Sales and Marketing.</DialogDescription>
      </DialogHeader>
      <div className="rounded-md border bg-slate-50 p-4 space-y-1">
        <p className="font-semibold">{listing.name}</p>
        <p className="text-sm text-slate-600">{listing.assetCode}</p>
        <p className="text-sm">{listing.location || 'Location not specified'}</p>
        <p className="text-sm">{listingLabel} · {formattedPrice}{chargeCadence}</p>
      </div>
      {publicMode ? <div className="grid gap-3 sm:grid-cols-2">
        <div className="space-y-2 sm:col-span-2">
          <Label htmlFor="enquiry-contact-name">Name</Label>
          <Input id="enquiry-contact-name" value={contactName} onChange={e => setContactName(e.target.value)} disabled={busy} placeholder="Your full name" />
        </div>
        <div className="space-y-2">
          <Label htmlFor="enquiry-contact-phone">Phone</Label>
          <Input id="enquiry-contact-phone" value={contactPhone} onChange={e => setContactPhone(e.target.value)} disabled={busy} placeholder="Phone number" />
        </div>
        <div className="space-y-2">
          <Label htmlFor="enquiry-contact-email">Email</Label>
          <Input id="enquiry-contact-email" type="email" value={contactEmail} onChange={e => setContactEmail(e.target.value)} disabled={busy} placeholder="Email address" />
        </div>
        <div className="space-y-2 sm:col-span-2">
          <Label htmlFor="enquiry-contact-reference">ID / card number</Label>
          <Input id="enquiry-contact-reference" value={contactReference} onChange={e => setContactReference(e.target.value)} disabled={busy} placeholder="Optional reference" />
        </div>
      </div> : profiles.length > 0 ? <div className="space-y-2">
        <Label htmlFor="enquiry-partner">Business partner</Label>
        <select id="enquiry-partner" className="w-full rounded-md border p-2" value={partnerId} onChange={e => setPartnerId(e.target.value)} disabled={busy}>
          {profiles.map(p => <option key={p.id} value={p.id}>{p.partnerName}</option>)}
        </select>
      </div> : !profilesLoading ? <p className="text-sm text-slate-600">Your signed-in portal contact details will be included.</p> : null}
      <div className="space-y-2">
        <Label htmlFor="property-enquiry-message">Your enquiry</Label>
        <Textarea id="property-enquiry-message" value={message} onChange={e => setMessage(e.target.value)} maxLength={4000} rows={5} disabled={busy} placeholder="What would you like to know about this property?" />
        <p className="text-xs text-slate-500">{message.length}/4,000 characters</p>
      </div>
      {security?.captchaEnabled && security.captchaSiteKey ? <ReCAPTCHA ref={captcha} sitekey={security.captchaSiteKey} onChange={setCaptchaToken} onExpired={() => setCaptchaToken(null)} /> : null}
      {(error || securityError) && <Alert variant="destructive"><AlertDescription>{error || 'Could not load the enquiry security settings. Please try again.'}</AlertDescription></Alert>}
      <DialogFooter>
        <Button variant="outline" onClick={onClose} disabled={busy}>Cancel</Button>
        <Button onClick={() => void submit()} disabled={busy || profilesLoading || securityLoading || securityError || !message.trim() || Boolean(publicMode && (!contactName.trim() || !contactPhone.trim())) || Boolean(security?.captchaEnabled && !captchaToken)}>{busy ? 'Sending…' : 'Send enquiry'}</Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>;
}
