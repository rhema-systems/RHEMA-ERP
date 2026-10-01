'use client';

import React, { useEffect, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  PublicCaptchaChallenge,
  type PublicCaptchaChallengeHandle,
} from '@/components/security/PublicCaptchaChallenge';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { settingsService } from '@/services/settings';
import {
  externalEstateListingsService,
  type ExternalEstateListing,
  type ExternalListingEnquiry,
  type EnquiryPartnerProfile,
} from '@/services/external-estate-listings.service';

export function PropertyEnquiryDialog({
  listing,
  onClose,
  onCreated,
  publicMode = false,
}: {
  listing: ExternalEstateListing;
  onClose: () => void;
  onCreated: (ticket: ExternalListingEnquiry) => void;
  publicMode?: boolean;
}) {
  const [message, setMessage] = useState('');
  const [contactName, setContactName] = useState('');
  const [contactEmail, setContactEmail] = useState('');
  const [contactPhone, setContactPhone] = useState('');
  const [alternativePhone, setAlternativePhone] = useState('');
  const [preferredContactMethod, setPreferredContactMethod] = useState<
    'Email' | 'Phone' | 'Either'
  >('Email');
  const [profiles, setProfiles] = useState<EnquiryPartnerProfile[]>([]);
  const [partnerId, setPartnerId] = useState('');
  const [profilesLoading, setProfilesLoading] = useState(!publicMode);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [submissionId] = useState(() => crypto.randomUUID());
  const [captchaToken, setCaptchaToken] = useState<string | null>(null);
  const captcha = useRef<PublicCaptchaChallengeHandle>(null);
  const {
    data: security,
    isLoading: securityLoading,
    isError: securityError,
  } = useQuery({
    queryKey: ['security', 'public'],
    queryFn: () => settingsService.getPublicSecuritySettings(),
  });
  useEffect(() => {
    if (publicMode) {
      setProfilesLoading(false);
      return;
    }
    let active = true;
    externalEstateListingsService
      .getEnquiryProfiles()
      .then((items) => {
        if (!active) return;
        setProfiles(items);
        setPartnerId(items[0]?.id || '');
        setProfilesLoading(false);
      })
      .catch(() => {
        if (active) {
          setError(
            'Could not load your business partner details. Close this dialog and try again.'
          );
        }
      });
    return () => {
      active = false;
    };
  }, [publicMode]);
  const isRent =
    listing.externalListingType === 'Rent' ||
    listing.externalListingType === 'SaleAndRent';
  const isLease =
    listing.externalListingType === 'Lease' ||
    listing.externalListingType === 'SaleAndLease';
  const price =
    isRent || isLease
      ? (listing.externalMonthlyRent ?? listing.externalListingPrice)
      : (listing.externalSalePrice ?? listing.externalListingPrice);
  const listingLabel =
    listing.externalListingType === 'Sale'
      ? 'For sale'
      : isLease
        ? listing.externalListingType === 'SaleAndLease'
          ? 'For sale or lease'
          : 'For lease'
        : listing.externalListingType === 'SaleAndRent'
          ? 'For sale or rent'
          : 'For rent';
  const chargeCadence = isLease ? ' / year' : isRent ? ' / month' : '';
  const formattedPrice =
    price == null
      ? 'Price on request'
      : new Intl.NumberFormat(undefined, {
          style: 'currency',
          currency: listing.externalListingCurrency || 'GHS',
          maximumFractionDigits: 0,
        }).format(price);
  const submit = async () => {
    if (busy || !message.trim()) return;
    if (
      publicMode &&
      (!contactName.trim() || !contactPhone.trim() || !contactEmail.trim())
    ) {
      setError(
        'Enter your name, phone number and email address before sending the enquiry.'
      );
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const ticket = publicMode
        ? await externalEstateListingsService.createPublicEnquiry(listing.id, {
            submissionId,
            contactName: contactName.trim(),
            contactPhone: contactPhone.trim(),
            alternativePhoneNumber: alternativePhone.trim() || undefined,
            contactEmail: contactEmail.trim(),
            preferredContactMethod,
            message: message.trim(),
            captchaToken: captchaToken || undefined,
          })
        : await externalEstateListingsService.createEnquiry(listing.id, {
            submissionId,
            message: message.trim(),
            businessPartnerId: partnerId || undefined,
            captchaToken: captchaToken || undefined,
          });
      onCreated(ticket);
    } catch (e) {
      setError(
        e instanceof Error
          ? e.message
          : 'The enquiry could not be sent. Your message has been retained.'
      );
      captcha.current?.reset();
      setCaptchaToken(null);
    } finally {
      setBusy(false);
    }
  };
  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open && !busy) onClose();
      }}
    >
      <DialogContent
        className="max-h-[90vh] overflow-y-auto text-foreground sm:max-w-xl"
        onInteractOutside={(e) => {
          if (busy) e.preventDefault();
        }}
        onEscapeKeyDown={(e) => {
          if (busy) e.preventDefault();
        }}
      >
        <DialogHeader>
          <DialogTitle>Enquire about this property</DialogTitle>
          <DialogDescription>
            Send your questions to Sales and Marketing.
          </DialogDescription>
        </DialogHeader>
        <div className="rounded-md border bg-muted p-4 space-y-1">
          <p className="font-semibold">{listing.name}</p>
          <p className="text-sm text-muted-foreground">{listing.assetCode}</p>
          <p className="text-sm">
            {listing.location || 'Location not specified'}
          </p>
          <p className="text-sm">
            {listingLabel} · {formattedPrice}
            {chargeCadence}
          </p>
        </div>
        {publicMode ? (
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-2 sm:col-span-2">
              <Label htmlFor="enquiry-contact-name">Name</Label>
              <Input
                id="enquiry-contact-name"
                value={contactName}
                maxLength={200}
                autoComplete="name"
                onChange={(e) => setContactName(e.target.value)}
                disabled={busy}
                placeholder="Your full name"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="enquiry-contact-phone">Phone</Label>
              <Input
                id="enquiry-contact-phone"
                type="tel"
                value={contactPhone}
                maxLength={50}
                autoComplete="tel"
                onChange={(e) => setContactPhone(e.target.value)}
                disabled={busy}
                placeholder="Phone number"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="enquiry-contact-email">Email</Label>
              <Input
                id="enquiry-contact-email"
                type="email"
                value={contactEmail}
                maxLength={320}
                autoComplete="email"
                onChange={(e) => setContactEmail(e.target.value)}
                disabled={busy}
                placeholder="Email address"
              />
            </div>
            <div className="space-y-2 sm:col-span-2">
              <Label htmlFor="enquiry-alternative-phone">
                Alternative phone
              </Label>
              <Input
                id="enquiry-alternative-phone"
                type="tel"
                value={alternativePhone}
                maxLength={50}
                autoComplete="tel"
                onChange={(e) => setAlternativePhone(e.target.value)}
                disabled={busy}
                placeholder="Optional alternative number"
              />
            </div>
            <div className="space-y-2 sm:col-span-2">
              <Label htmlFor="enquiry-preferred-contact">
                Preferred contact method
              </Label>
              <select
                id="enquiry-preferred-contact"
                className="w-full rounded-md border bg-background p-2 text-foreground"
                value={preferredContactMethod}
                onChange={(e) =>
                  setPreferredContactMethod(
                    e.target.value as 'Email' | 'Phone' | 'Either'
                  )
                }
                disabled={busy}
              >
                <option value="Email">Email</option>
                <option value="Phone">Phone call</option>
                <option value="Either">Email or phone</option>
              </select>
            </div>
          </div>
        ) : profiles.length > 0 ? (
          <div className="space-y-2">
            <Label htmlFor="enquiry-partner">Business partner</Label>
            <select
              id="enquiry-partner"
              className="w-full rounded-md border bg-background p-2 text-foreground"
              value={partnerId}
              onChange={(e) => setPartnerId(e.target.value)}
              disabled={busy}
            >
              {profiles.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.partnerName}
                </option>
              ))}
            </select>
          </div>
        ) : !profilesLoading ? (
          <p className="text-sm text-muted-foreground">
            Your signed-in portal contact details will be included.
          </p>
        ) : null}
        <div className="space-y-2">
          <Label htmlFor="property-enquiry-message">Your enquiry</Label>
          <Textarea
            id="property-enquiry-message"
            value={message}
            onChange={(e) => setMessage(e.target.value)}
            maxLength={4000}
            rows={5}
            disabled={busy}
            placeholder="What would you like to know about this property?"
          />
          <p className="text-xs text-muted-foreground">
            {message.length}/4,000 characters
          </p>
        </div>
        {security && (
          <PublicCaptchaChallenge
            id="property-enquiry-captcha"
            ref={captcha}
            enabled={security.captchaEnabled}
            provider={security.captchaProvider}
            recaptchaSiteKey={security.recaptchaSiteKey}
            hCaptchaSiteKey={security.hCaptchaSiteKey}
            onChange={setCaptchaToken}
          />
        )}
        {(error || securityError) && (
          <Alert variant="destructive">
            <AlertDescription>
              {error ||
                'Could not load the enquiry security settings. Please try again.'}
            </AlertDescription>
          </Alert>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={busy}>
            Cancel
          </Button>
          <Button
            onClick={() => void submit()}
            disabled={
              busy ||
              profilesLoading ||
              securityLoading ||
              securityError ||
              !message.trim() ||
              Boolean(
                publicMode &&
                  (!contactName.trim() ||
                    !contactPhone.trim() ||
                    !contactEmail.trim())
              ) ||
              Boolean(security?.captchaEnabled && !captchaToken)
            }
          >
            {busy ? 'Sending…' : 'Send enquiry'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
