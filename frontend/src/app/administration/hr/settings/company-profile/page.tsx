'use client';

import { useEffect, useMemo } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, Building2 } from 'lucide-react';
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
import { COMPANY_LEGAL_FORMS, type CompanyLegalForm } from '@/types/hr/company-profile';

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
  postalCode: z.string().trim().max(20).optional(),
  phonePrimary: z.string().trim().max(50).optional(),
  hrEmail: z.string().trim().max(200).email('Enter a valid email address.').optional().or(z.literal('')),
  generalEmail: z.string().trim().max(200).email('Enter a valid email address.').optional().or(z.literal('')),
  website: z.string().trim().max(200).optional(),

  defaultSignatoryName: z.string().trim().max(200).optional(),
  defaultSignatoryTitle: z.string().trim().max(200).optional(),
  signatureImageUrl: z.string().trim().max(500).optional(),
  companySealImageUrl: z.string().trim().max(500).optional(),
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
      postalCode: data.postalCode ?? '',
      phonePrimary: data.phonePrimary ?? '',
      hrEmail: data.hrEmail ?? '',
      generalEmail: data.generalEmail ?? '',
      website: data.website ?? '',

      defaultSignatoryName: data.defaultSignatoryName ?? '',
      defaultSignatoryTitle: data.defaultSignatoryTitle ?? '',
      signatureImageUrl: data.signatureImageUrl ?? '',
      companySealImageUrl: data.companySealImageUrl ?? '',
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
        postalCode: orNull(values.postalCode),
        phonePrimary: orNull(values.phonePrimary),
        hrEmail: orNull(values.hrEmail),
        generalEmail: orNull(values.generalEmail),
        website: orNull(values.website),

        defaultSignatoryName: orNull(values.defaultSignatoryName),
        defaultSignatoryTitle: orNull(values.defaultSignatoryTitle),
        signatureImageUrl: orNull(values.signatureImageUrl),
        companySealImageUrl: orNull(values.companySealImageUrl),
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
            <FieldRow>
              <TextField form={form} name="digitalAddress" label="Digital address (GhanaPost GPS)" />
              <TextField form={form} name="city" label="City" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="region" label="Region" />
              <SelectField
                form={form}
                name="countryId"
                label="Country"
                options={countryOptions}
                allowEmpty
                emptyLabel="Not set"
              />
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
            <FieldRow>
              <TextField form={form} name="logoUrl" label="Logo URL" />
              <TextField form={form} name="signatureImageUrl" label="Signature image URL" />
            </FieldRow>
            <TextField form={form} name="companySealImageUrl" label="Company seal image URL" />
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
    </div>
  );
}
