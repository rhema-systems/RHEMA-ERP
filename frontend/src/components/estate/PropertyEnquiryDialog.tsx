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
import PhoneInput from '@/components/ui/phone-input';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import { Textarea } from '@/components/ui/textarea';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/hooks/use-toast';
import { settingsService } from '@/services/settings';
import { isValidPhoneNumberForCountry } from '@/lib/phone-number';
import {
  externalEstateListingsService,
  type ExternalEstateListing,
  type ExternalListingEnquiry,
  type EnquiryPartnerProfile,
  type PublicEnquiryContactChallenge,
  type PublicEnquiryContactChannel,
  type PublicEnquiryContactVerification,
} from '@/services/external-estate-listings.service';

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
function normalizePhone(value: string) {
  return value.replace(/[^\d+]/g, '');
}

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
  const { toast } = useToast();
  const [message, setMessage] = useState('');
  const [contactName, setContactName] = useState('');
  const [contactEmail, setContactEmail] = useState('');
  const [contactPhone, setContactPhone] = useState('');
  const [preferredContactMethod, setPreferredContactMethod] =
    useState<PublicEnquiryContactChannel>('Email');
  const [contactChallenge, setContactChallenge] =
    useState<PublicEnquiryContactChallenge | null>(null);
  const [otpCode, setOtpCode] = useState('');
  const [contactVerification, setContactVerification] =
    useState<PublicEnquiryContactVerification | null>(null);
  const [contactBusy, setContactBusy] = useState(false);
  const [showPortalRedirect, setShowPortalRedirect] = useState(false);
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

  const selectedContact =
    preferredContactMethod === 'Email'
      ? contactEmail.trim().toLowerCase()
      : normalizePhone(contactPhone);
  const selectedContactIsValid =
    preferredContactMethod === 'Email'
      ? EMAIL_PATTERN.test(selectedContact)
      : isValidPhoneNumberForCountry(selectedContact);

  const resetContactVerification = () => {
    setContactChallenge(null);
    setOtpCode('');
    setContactVerification(null);
    setShowPortalRedirect(false);
  };

  const requestContactOtp = async () => {
    if (contactBusy || !selectedContactIsValid) return;
    setContactBusy(true);
    try {
      const challenge =
        await externalEstateListingsService.requestPublicEnquiryContactChallenge(
          {
            listingId: listing.id,
            channel: preferredContactMethod,
            contact: selectedContact,
            captchaToken: captchaToken || undefined,
          }
        );
      setContactChallenge(challenge);
      setOtpCode('');
      setContactVerification(null);
      toast({
        title: 'Verification code sent',
        description: `Enter the code sent to ${challenge.maskedContact}.`,
        variant: 'success',
      });
    } catch (e) {
      toast({
        title: 'Could not send verification code',
        description:
          e instanceof Error ? e.message : 'Check the contact and try again.',
        variant: 'destructive',
      });
      captcha.current?.reset();
      setCaptchaToken(null);
    } finally {
      setContactBusy(false);
    }
  };

  const verifyContactOtp = async () => {
    if (contactBusy || otpCode.length !== 6 || !contactChallenge) return;
    setContactBusy(true);
    try {
      const verification =
        await externalEstateListingsService.verifyPublicEnquiryContact({
          listingId: listing.id,
          channel: preferredContactMethod,
          contact: selectedContact,
          otpCode,
        });
      setContactVerification(verification);
      setContactName(verification.profile.contactName || '');
      if (verification.profile.contactEmail) {
        setContactEmail(verification.profile.contactEmail);
      }
      if (verification.profile.contactPhone) {
        setContactPhone(verification.profile.contactPhone);
      }
      setShowPortalRedirect(verification.profile.requiresPortalLogin);
      toast({
        title: 'Contact verified',
        description: verification.profile.requiresPortalLogin
          ? 'Your contact is linked to a customer account.'
          : 'You can now complete and send the enquiry.',
        variant: 'success',
      });
    } catch (e) {
      toast({
        title: 'Could not verify contact',
        description:
          e instanceof Error ? e.message : 'Check the code and try again.',
        variant: 'destructive',
      });
    } finally {
      setContactBusy(false);
    }
  };
  const submit = async () => {
    if (busy || !message.trim()) return;
    if (publicMode && !contactVerification) {
      toast({
        title: 'Verify your contact first',
        description:
          'Request and enter the one-time code before sending the enquiry.',
        variant: 'destructive',
      });
      return;
    }
    if (publicMode && contactVerification?.profile.requiresPortalLogin) {
      setShowPortalRedirect(true);
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const ticket = publicMode
        ? await externalEstateListingsService.createPublicEnquiry(listing.id, {
            submissionId,
            contactName: contactName.trim(),
            contactPhone:
              preferredContactMethod === 'Phone' ? selectedContact : undefined,
            contactEmail:
              preferredContactMethod === 'Email' ? selectedContact : undefined,
            preferredContactMethod,
            contactVerificationToken:
              contactVerification?.verificationToken || '',
            message: message.trim(),
          })
        : await externalEstateListingsService.createEnquiry(listing.id, {
            submissionId,
            message: message.trim(),
            businessPartnerId: partnerId || undefined,
            captchaToken: captchaToken || undefined,
          });
      onCreated(ticket);
    } catch (e) {
      const description =
        e instanceof Error
          ? e.message
          : 'The enquiry could not be sent. Your message has been retained.';
      if (publicMode) {
        toast({
          title: 'Enquiry could not be sent',
          description,
          variant: 'destructive',
        });
      } else {
        setError(description);
      }
      captcha.current?.reset();
      setCaptchaToken(null);
    } finally {
      setBusy(false);
    }
  };
  return (
    <>
      <Dialog
        open
        onOpenChange={(open) => {
          if (!open && !busy) onClose();
        }}
      >
        <DialogContent
          className="max-h-[90vh] overflow-y-auto text-foreground sm:max-w-2xl"
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
              <fieldset className="space-y-2 sm:col-span-2">
                <legend className="text-sm font-medium">
                  Preferred contact method
                </legend>
                <RadioGroup
                  aria-label="Preferred contact method"
                  className="flex gap-6"
                  value={preferredContactMethod}
                  onValueChange={(value) => {
                    setPreferredContactMethod(
                      value as PublicEnquiryContactChannel
                    );
                    resetContactVerification();
                  }}
                  disabled={busy || contactBusy}
                >
                  <div className="flex items-center gap-2">
                    <RadioGroupItem
                      id="enquiry-contact-email-method"
                      value="Email"
                    />
                    <Label htmlFor="enquiry-contact-email-method">Email</Label>
                  </div>
                  <div className="flex items-center gap-2">
                    <RadioGroupItem
                      id="enquiry-contact-phone-method"
                      value="Phone"
                    />
                    <Label htmlFor="enquiry-contact-phone-method">Phone</Label>
                  </div>
                </RadioGroup>
              </fieldset>
              <div className="space-y-2 sm:col-span-2">
                <Label htmlFor="enquiry-contact-name">Name</Label>
                <Input
                  id="enquiry-contact-name"
                  value={contactName}
                  maxLength={200}
                  autoComplete="name"
                  onChange={(e) => setContactName(e.target.value)}
                  disabled={busy || contactBusy}
                  placeholder="Your full name"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="enquiry-contact-phone">Phone</Label>
                <PhoneInput
                  id="enquiry-contact-phone"
                  value={contactPhone}
                  onChange={(value) => {
                    setContactPhone(value);
                    resetContactVerification();
                  }}
                  disabled={
                    busy ||
                    contactBusy ||
                    preferredContactMethod !== 'Phone' ||
                    Boolean(contactChallenge)
                  }
                  error={
                    preferredContactMethod === 'Phone' &&
                    Boolean(contactPhone) &&
                    !isValidPhoneNumberForCountry(normalizePhone(contactPhone))
                  }
                  countrySelectLabel="Phone country calling code"
                  placeholder="National phone number"
                  showCountryLengthHint
                />
                {preferredContactMethod === 'Phone' &&
                  contactPhone &&
                  !isValidPhoneNumberForCountry(
                    normalizePhone(contactPhone)
                  ) && (
                    <p className="text-xs text-destructive">
                      Enter a valid phone number after the country code.
                    </p>
                  )}
              </div>
              <div className="space-y-2">
                <Label htmlFor="enquiry-contact-email">Email</Label>
                <Input
                  id="enquiry-contact-email"
                  type="email"
                  value={contactEmail}
                  maxLength={320}
                  autoComplete="email"
                  onChange={(e) => {
                    setContactEmail(e.target.value);
                    resetContactVerification();
                  }}
                  disabled={
                    busy ||
                    contactBusy ||
                    preferredContactMethod !== 'Email' ||
                    Boolean(contactChallenge)
                  }
                  aria-invalid={
                    preferredContactMethod === 'Email' &&
                    Boolean(contactEmail) &&
                    !EMAIL_PATTERN.test(contactEmail.trim())
                  }
                  placeholder="Email address"
                />
                {preferredContactMethod === 'Email' &&
                  contactEmail &&
                  !EMAIL_PATTERN.test(contactEmail.trim()) && (
                    <p className="text-xs text-destructive">
                      Enter a valid email address.
                    </p>
                  )}
              </div>
              <div className="space-y-3 rounded-lg border border-primary/20 bg-primary/5 p-4 sm:col-span-2">
                {!contactChallenge ? (
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                    <div>
                      <p className="text-sm font-semibold">Verify your contact</p>
                      <p className="text-xs text-muted-foreground">
                        We will send a six-digit code to the selected contact.
                      </p>
                    </div>
                    <Button
                      type="button"
                      className="min-h-11 w-full px-5 shadow-sm sm:w-auto"
                      onClick={() => void requestContactOtp()}
                      disabled={
                        busy ||
                        contactBusy ||
                        !selectedContactIsValid ||
                        securityLoading ||
                        securityError ||
                        Boolean(security?.captchaEnabled && !captchaToken)
                      }
                    >
                      {contactBusy ? 'Sending code…' : 'Send verification code'}
                    </Button>
                  </div>
                ) : !contactVerification ? (
                  <>
                    <p className="text-sm text-muted-foreground">
                      Enter the six-digit code sent to{' '}
                      {contactChallenge.maskedContact}.
                    </p>
                    <div className="flex flex-col gap-2 sm:flex-row">
                      <Input
                        aria-label="Verification code"
                        inputMode="numeric"
                        autoComplete="one-time-code"
                        value={otpCode}
                        maxLength={6}
                        onChange={(event) =>
                          setOtpCode(
                            event.target.value.replace(/\D/g, '').slice(0, 6)
                          )
                        }
                        disabled={busy || contactBusy}
                        placeholder="6-digit code"
                      />
                      <Button
                        type="button"
                        onClick={() => void verifyContactOtp()}
                        disabled={busy || contactBusy || otpCode.length !== 6}
                      >
                        {contactBusy ? 'Verifying…' : 'Verify contact'}
                      </Button>
                      <Button
                        type="button"
                        variant="ghost"
                        onClick={resetContactVerification}
                        disabled={busy || contactBusy}
                      >
                        Change contact
                      </Button>
                    </div>
                  </>
                ) : (
                  <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                    <p className="text-sm font-medium text-emerald-700">
                      {contactVerification.profile.requiresPortalLogin
                        ? 'Verified customer contact'
                        : 'Contact verified'}
                    </p>
                    <Button
                      type="button"
                      variant="ghost"
                      onClick={resetContactVerification}
                      disabled={busy || contactBusy}
                    >
                      Use another contact
                    </Button>
                  </div>
                )}
                {contactVerification?.profile.requiresPortalLogin && (
                  <p className="text-sm text-amber-700">
                    This contact belongs to an existing customer. Sign in to the
                    external portal to submit the enquiry under that account.
                  </p>
                )}
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
          {security && (!publicMode || !contactChallenge) && (
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
                      !selectedContactIsValid ||
                      !contactVerification ||
                      contactVerification.profile.requiresPortalLogin)
                ) ||
                Boolean(
                  !publicMode && security?.captchaEnabled && !captchaToken
                )
              }
            >
              {busy ? 'Sending…' : 'Send enquiry'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <ConfirmationDialog
        open={showPortalRedirect}
        onOpenChange={setShowPortalRedirect}
        title="Continue in the customer portal"
        description="This verified contact is already linked to a customer account. Sign in to the external portal so this enquiry is recorded against your customer profile."
        confirmText="Go to customer sign in"
        cancelText="Stay on this page"
        onConfirm={() => {
          window.location.assign(
            contactVerification?.profile.externalPortalPath ||
              '/login?redirect=%2Fexternal-portal%2Fproperty-listings'
          );
        }}
      />
    </>
  );
}
