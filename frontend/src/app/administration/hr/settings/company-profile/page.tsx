'use client';

import { useEffect, useMemo, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, Building2, ShieldCheck, Stamp, Upload } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  TextField,
  TextareaField,
  SelectField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { companyProfileService } from '@/services/hr/company-profile.service';
import { countryService } from '@/services/hr/country.service';
import {
  COMPANY_LEGAL_FORMS,
  type CompanyLegalForm,
  type CompanySealAsset,
  type CompanySealAssetKind,
} from '@/types/hr/company-profile';
import { Badge } from '@/components/ui/badge';
import { AddressFields } from '@/components/reference/AddressFields';

/**
 * The tenant's company (legal-employer) profile.
 *
 * This is the letterhead. `LegalName`, `RegisteredAddress`, `LogoUrl`, `DocumentFooterText`,
 * `DefaultSignatoryName`, `DefaultSignatoryTitle` and `OfferAcceptanceInstructions` are read by
 * `OfferLetterService` and `ProbationLetterService` on every offer and confirmation letter, and
 * `LegalName` again by every templated email. Until this screen existed there was no way to author
 * any of it: the provider fell back to the Tenant record and `Company:*` configuration, so letters
 * went out under whatever the tenant happened to be called.
 *
 * The remaining fields — statutory numbers, incorporation details, contact addresses — are held
 * for reference and for templates that want them. Nothing rejects a blank one.
 */
const profileSchema = z.object({
  legalName: z.string().trim().min(1, 'Legal name is required.').max(200),
  tradingName: z.string().trim().max(200).optional(),
  legalForm: z.string().min(1),
  registrationNumber: z.string().trim().max(100).optional(),
  dateOfIncorporation: z.string().optional(),
  countryOfIncorporationId: z.string().optional(),

  taxIdentificationNumber: z.string().trim().max(50).optional(),
  vatNumber: z.string().trim().max(50).optional(),
  ssnitEmployerNumber: z.string().trim().max(50).optional(),
  otherStatutoryRegistrations: z.string().trim().max(500).optional(),

  registeredAddress: z.string().trim().max(500).optional(),
  digitalAddress: z.string().trim().max(100).optional(),
  city: z.string().trim().max(100).optional(),
  region: z.string().trim().max(100).optional(),
  countryId: z.string().optional(),
  /** Administrative area of the registered address; city and region are snapshots of it. */
  geoAreaId: z.string().optional(),
  postalCode: z.string().trim().max(20).optional(),
  phonePrimary: z.string().trim().max(50).optional(),
  hrEmail: z.string().trim().max(200).email('Enter a valid email address.').optional().or(z.literal('')),
  generalEmail: z.string().trim().max(200).email('Enter a valid email address.').optional().or(z.literal('')),
  website: z.string().trim().max(200).optional(),

  defaultSignatoryName: z.string().trim().max(200).optional(),
  defaultSignatoryTitle: z.string().trim().max(200).optional(),
  logoUrl: z.string().trim().max(500).optional(),
  offerAcceptanceInstructions: z.string().trim().max(2000).optional(),
  documentFooterText: z.string().trim().max(1000).optional(),
});

type ProfileForm = z.input<typeof profileSchema>;

/** The API sends `DateTime?`; the native date input wants exactly `YYYY-MM-DD`. */
const toDateInput = (iso: string | null | undefined) => (iso ? iso.slice(0, 10) : '');

/** Empty means "not set", and the API models these as nullable — send null, not "". */
const orNull = (v: string | undefined) => {
  const trimmed = v?.trim();
  return trimmed ? trimmed : null;
};

export default function CompanyProfilePage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'company-profile'],
    queryFn: () => companyProfileService.get(),
  });

  const { data: countries } = useQuery({
    queryKey: ['hr', 'countries', 'active'],
    queryFn: () => countryService.getActive(),
  });

  const countryOptions = useMemo(
    () => (countries ?? []).map((c) => ({ value: c.id, label: c.name })),
    [countries],
  );

  const form = useForm<ProfileForm>({
    resolver: zodResolver(profileSchema) as any,
    defaultValues: { legalName: '', legalForm: 'LimitedCompany' },
  });

  // Seed from whatever is in force — the saved row, or the Tenant/config-resolved defaults.
  useEffect(() => {
    if (!data) return;
    form.reset({
      legalName: data.legalName ?? '',
      tradingName: data.tradingName ?? '',
      legalForm: data.legalForm ?? 'LimitedCompany',
      registrationNumber: data.registrationNumber ?? '',
      dateOfIncorporation: toDateInput(data.dateOfIncorporation),
      countryOfIncorporationId: data.countryOfIncorporationId ?? '',

      taxIdentificationNumber: data.taxIdentificationNumber ?? '',
      vatNumber: data.vatNumber ?? '',
      ssnitEmployerNumber: data.ssnitEmployerNumber ?? '',
      otherStatutoryRegistrations: data.otherStatutoryRegistrations ?? '',

      registeredAddress: data.registeredAddress ?? '',
      digitalAddress: data.digitalAddress ?? '',
      city: data.city ?? '',
      region: data.region ?? '',
      countryId: data.countryId ?? '',
      geoAreaId: data.geoAreaId ?? '',
      postalCode: data.postalCode ?? '',
      phonePrimary: data.phonePrimary ?? '',
      hrEmail: data.hrEmail ?? '',
      generalEmail: data.generalEmail ?? '',
      website: data.website ?? '',

      defaultSignatoryName: data.defaultSignatoryName ?? '',
      defaultSignatoryTitle: data.defaultSignatoryTitle ?? '',
      logoUrl: data.logoUrl ?? '',
      offerAcceptanceInstructions: data.offerAcceptanceInstructions ?? '',
      documentFooterText: data.documentFooterText ?? '',
    });
  }, [data, form]);

  const save = useMutation({
    mutationFn: (values: ProfileForm) =>
      // Built explicitly rather than spread: the read model carries id, tenantId, legalFormName,
      // countryName, countryOfIncorporationName and the audit columns, none of which the PUT models.
      companyProfileService.update({
        legalName: values.legalName.trim(),
        tradingName: orNull(values.tradingName),
        legalForm: values.legalForm as CompanyLegalForm,
        registrationNumber: orNull(values.registrationNumber),
        dateOfIncorporation: orNull(values.dateOfIncorporation),
        countryOfIncorporationId: orNull(values.countryOfIncorporationId),

        taxIdentificationNumber: orNull(values.taxIdentificationNumber),
        vatNumber: orNull(values.vatNumber),
        ssnitEmployerNumber: orNull(values.ssnitEmployerNumber),
        otherStatutoryRegistrations: orNull(values.otherStatutoryRegistrations),

        registeredAddress: orNull(values.registeredAddress),
        digitalAddress: orNull(values.digitalAddress),
        city: orNull(values.city),
        region: orNull(values.region),
        countryId: orNull(values.countryId),
        geoAreaId: orNull(values.geoAreaId),
        postalCode: orNull(values.postalCode),
        phonePrimary: orNull(values.phonePrimary),
        hrEmail: orNull(values.hrEmail),
        generalEmail: orNull(values.generalEmail),
        website: orNull(values.website),

        defaultSignatoryName: orNull(values.defaultSignatoryName),
        defaultSignatoryTitle: orNull(values.defaultSignatoryTitle),
        logoUrl: orNull(values.logoUrl),
        offerAcceptanceInstructions: orNull(values.offerAcceptanceInstructions),
        documentFooterText: orNull(values.documentFooterText),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'company-profile'] });
      toast({ title: 'Company profile saved', description: 'Letters and emails will use these details.' });
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'Could not save the company profile',
        description: e?.message ?? 'Unexpected error.',
      }),
  });

  const neverSaved = !!data && !data.createdBy && !data.updatedBy;

  if (isLoading) {
    return (
      <div className="space-y-6 p-6">
        <Skeleton className="h-10 w-72" />
        <Skeleton className="h-96 w-full" />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Company Profile"
        description="The legal-employer details that appear on offer letters, confirmation letters and outgoing email."
        backHref="/administration/hr/settings"
      />

      {neverSaved && (
        <Alert>
          <Building2 className="h-4 w-4" />
          <AlertTitle>These details have not been confirmed yet</AlertTitle>
          <AlertDescription>
            Nothing has been saved here, so letters are going out under details derived from the
            tenant record and application configuration. Check the legal name and registered address
            in particular — they are what a candidate sees at the top of an offer.
          </AlertDescription>
        </Alert>
      )}

      <form onSubmit={form.handleSubmit((v) => save.mutate(v))} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle>Legal identity</CardTitle>
            <CardDescription>
              The legal name is what every letter and email calls this organisation.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <TextField form={form} name="legalName" label="Legal name" required />
              <TextField form={form} name="tradingName" label="Trading name" />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={form}
                name="legalForm"
                label="Legal form"
                options={COMPANY_LEGAL_FORMS}
                required
              />
              <TextField form={form} name="registrationNumber" label="Registration number" />
            </FieldRow>
            <FieldRow>
              <DateField form={form} name="dateOfIncorporation" label="Date of incorporation" />
              <SelectField
                form={form}
                name="countryOfIncorporationId"
                label="Country of incorporation"
                options={countryOptions}
                allowEmpty
                emptyLabel="Not set"
              />
            </FieldRow>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Statutory &amp; tax</CardTitle>
            <CardDescription>Employer registrations held with GRA, SSNIT and others.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <TextField form={form} name="taxIdentificationNumber" label="Tax identification number (TIN)" />
              <TextField form={form} name="vatNumber" label="VAT number" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="ssnitEmployerNumber" label="SSNIT employer number" />
              <TextField form={form} name="postalCode" label="Postal code" />
            </FieldRow>
            <TextareaField
              form={form}
              name="otherStatutoryRegistrations"
              label="Other statutory registrations"
              rows={2}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Registered address &amp; contact</CardTitle>
            <CardDescription>
              The registered address is printed on letters. When it is blank, letters fall back to
              city, region and country instead.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <TextareaField form={form} name="registeredAddress" label="Registered address" rows={2} />

            {/*
              ⚠ City and Region are read-only once the country has a division scheme: the server
              rewrites both from the chosen area, so editable boxes would discard what you type.
              They stay editable for a country with no scheme, which is what letters fall back to.
            */}
            <AddressFields
              countryId={form.watch('countryId') || ''}
              onCountryChange={(v) => form.setValue('countryId', v, { shouldDirty: true })}
              geoAreaId={form.watch('geoAreaId') || ''}
              onGeoAreaChange={(v) => form.setValue('geoAreaId', v, { shouldDirty: true })}
              fallback={(schemeLoaded) => (
                <FieldRow>
                  <TextField
                    form={form}
                    name="city"
                    label="City"
                    disabled={schemeLoaded}
                    hint={schemeLoaded ? 'Set from the address above' : undefined}
                  />
                  <TextField
                    form={form}
                    name="region"
                    label="Region"
                    disabled={schemeLoaded}
                    hint={schemeLoaded ? 'Set from the address above' : undefined}
                  />
                </FieldRow>
              )}
            />

            <FieldRow>
              <TextField form={form} name="digitalAddress" label="Digital address (GhanaPost GPS)" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="phonePrimary" label="Primary phone" type="tel" />
              <TextField form={form} name="website" label="Website" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="hrEmail" label="HR email" type="email" />
              <TextField form={form} name="generalEmail" label="General email" type="email" />
            </FieldRow>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Documents &amp; signature</CardTitle>
            <CardDescription>
              What appears at the top and bottom of an offer or confirmation letter. The signature
              and seal images are available to letter templates as{' '}
              <code>SignatureImageUrl</code> and <code>CompanySealImageUrl</code>; a template that
              does not reference them simply renders without them.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <TextField form={form} name="defaultSignatoryName" label="Default signatory name" />
              <TextField
                form={form}
                name="defaultSignatoryTitle"
                label="Default signatory title"
                placeholder="Head of Human Resources"
              />
            </FieldRow>
            <TextField form={form} name="logoUrl" label="Logo URL" />
            <TextareaField
              form={form}
              name="offerAcceptanceInstructions"
              label="Offer acceptance instructions"
              rows={3}
            />
            <TextareaField form={form} name="documentFooterText" label="Document footer text" rows={2} />
          </CardContent>
        </Card>

        <div className="flex justify-end">
          <Button type="submit" disabled={save.isPending}>
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Save company profile
          </Button>
        </div>
      </form>

      {/*
        Outside the profile form on purpose. Editing the letterhead and replacing the seal are
        different acts under different permissions, and putting the seal inside a form whose Save
        button is Write-gated would imply otherwise.
      */}
      <SealAssetsPanel />
    </div>
  );
}

// ── the seal and the signature ──────────────────────────────────────────────

/**
 * The images that make a generated document look authentic.
 *
 * ⚠ **Replacing one is Admin-gated, not Write-gated.** HR maintains the company profile; changing
 * what stamps a document as authentic sits with the tier that already holds the settings described
 * as moving trust boundaries. A user without it sees the history and no buttons.
 *
 * ⚠ **Every image is kept.** Overwriting would destroy the answer to the question that matters
 * after a compromise — which documents carry the seal that leaked.
 */
function SealAssetsPanel() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data: assets, isLoading } = useQuery({
    queryKey: ['hr', 'company-seal-assets'],
    queryFn: () => companyProfileService.getSealAssets(),
  });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'company-seal-assets'] });

  const history = assets ?? [];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <ShieldCheck className="h-5 w-5" /> Seal &amp; signature
        </CardTitle>
        <CardDescription>
          The images embedded in generated offer and confirmation letters. Replacing one retires the
          image it supersedes rather than overwriting it, so it stays possible to say which letters
          carry which seal.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-6">
        {isLoading ? (
          <Skeleton className="h-32 w-full" />
        ) : (
          <>
            <SealKindRow kind="Seal" label="Company seal" history={history} onChanged={invalidate} toast={toast} />
            <SealKindRow kind="Signature" label="Authorised signature" history={history} onChanged={invalidate} toast={toast} />
          </>
        )}
      </CardContent>
    </Card>
  );
}

function SealKindRow({
  kind,
  label,
  history,
  onChanged,
  toast,
}: {
  kind: CompanySealAssetKind;
  label: string;
  history: CompanySealAsset[];
  onChanged: () => Promise<unknown>;
  toast: ReturnType<typeof useToast>['toast'];
}) {
  const fileRef = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);

  const mine = history.filter((a) => a.kind === kind);
  const current = mine.find((a) => a.isCurrent) ?? null;
  const retired = mine.filter((a) => !a.isCurrent);

  const upload = async (file: File) => {
    setBusy(true);
    try {
      await companyProfileService.replaceSealAsset(kind, file);
      await onChanged();
      toast({ title: `${label} replaced`, description: 'The previous image was retired, not overwritten.' });
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Upload failed.', variant: 'destructive' });
    } finally {
      setBusy(false);
      if (fileRef.current) fileRef.current.value = '';
    }
  };

  const retire = async () => {
    setBusy(true);
    try {
      await companyProfileService.retireSealAsset(kind);
      await onChanged();
      toast({
        title: `${label} withdrawn`,
        description: 'Letters will render without it until a replacement is uploaded.',
      });
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Could not withdraw it.', variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="rounded-md border p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-1">
          <div className="flex items-center gap-2 font-medium">
            <Stamp className="h-4 w-4 text-muted-foreground" />
            {label}
            {current ? <Badge variant="secondary">In force</Badge> : <Badge variant="outline">None</Badge>}
          </div>
          {current ? (
            <p className="text-sm text-muted-foreground">
              {current.fileName || 'Uploaded image'} · in force since{' '}
              {current.effectiveFrom.slice(0, 10)}
              {current.uploadedBy ? ` · uploaded by ${current.uploadedBy}` : ''}
            </p>
          ) : (
            <p className="text-sm text-muted-foreground">
              No image is in force. Letters render without one, and any legacy image the tenant was
              carrying is used until you upload a replacement.
            </p>
          )}
        </div>

        <div className="flex gap-2">
          <input
            ref={fileRef}
            type="file"
            accept="image/*"
            className="hidden"
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) void upload(file);
            }}
          />
          <Button variant="outline" size="sm" disabled={busy} onClick={() => fileRef.current?.click()}>
            {busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Upload className="mr-2 h-4 w-4" />}
            {current ? 'Replace' : 'Upload'}
          </Button>
          {current && (
            <Button variant="outline" size="sm" disabled={busy} onClick={() => void retire()}>
              Withdraw
            </Button>
          )}
        </div>
      </div>

      {retired.length > 0 && (
        <div className="mt-3 border-t pt-3">
          <div className="text-xs uppercase tracking-wide text-muted-foreground">
            Previously used
          </div>
          <ul className="mt-1 space-y-1 text-sm text-muted-foreground">
            {retired.map((a) => (
              <li key={a.id}>
                {a.fileName || 'Image'} · {a.effectiveFrom.slice(0, 10)} to{' '}
                {a.retiredOn?.slice(0, 10) ?? '—'}
                {a.retiredReason ? ` · ${a.retiredReason}` : ''}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
