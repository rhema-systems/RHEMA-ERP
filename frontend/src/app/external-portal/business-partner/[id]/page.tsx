'use client';

import React, { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { ArrowLeft, Building2, FileText, Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  SupplierBankAccountsPanel,
  SupplierContactsPanel,
} from '@/components/procurement/SupplierContactBankDetails';
import {
  bankAccountsFromRegistrationData,
  contactsFromRegistrationData,
  parseRegistrationData,
  registrationString,
} from '@/lib/supplier-registration-details';
import {
  businessPartnerRegistrationService,
  type BusinessPartnerRegistrationDetailDto,
} from '@/services/businessPartnerRegistrationService';

export default function ExternalBusinessPartnerRegistrationDetailsPage() {
  const params = useParams();
  const id = Array.isArray(params?.id) ? params.id[0] : params?.id || '';
  const [registration, setRegistration] =
    useState<BusinessPartnerRegistrationDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      try {
        setLoading(true);
        setError(null);
        // Resolve the id against the current supplier's collection before asking
        // for the detail record. This prevents this portal route from becoming an
        // arbitrary registration lookup even if an API policy changes later.
        const owned =
          await businessPartnerRegistrationService.getMyRegistrations();
        if (!owned.some((item) => item.id === id)) {
          throw new Error(
            'This registration is not available in your account.'
          );
        }
        const detail = await businessPartnerRegistrationService.getById(id);
        if (!cancelled) setRegistration(detail);
      } catch (loadError) {
        if (!cancelled) {
          setError(
            loadError instanceof Error
              ? loadError.message
              : 'Could not load this registration.'
          );
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    void load();
    return () => {
      cancelled = true;
    };
  }, [id]);

  const data = useMemo(
    () => parseRegistrationData(registration?.registrationData),
    [registration?.registrationData]
  );
  const contacts = useMemo(() => contactsFromRegistrationData(data), [data]);
  const bankAccounts = useMemo(
    () => bankAccountsFromRegistrationData(data),
    [data]
  );

  if (loading) {
    return (
      <div className="flex min-h-64 items-center justify-center gap-2 text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin" />
        Loading registration details…
      </div>
    );
  }

  if (error || !registration) {
    return (
      <Card className="mx-auto max-w-2xl">
        <CardHeader>
          <CardTitle>Registration unavailable</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <p className="text-sm text-muted-foreground">
            {error || 'Could not load this registration.'}
          </p>
          <Button asChild variant="outline">
            <Link href="/external-portal">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Back to dashboard
            </Link>
          </Button>
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <Button asChild variant="ghost" className="mb-2 px-0">
            <Link href="/external-portal">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Back to dashboard
            </Link>
          </Button>
          <h1 className="text-3xl font-bold">{registration.companyName}</h1>
          <p className="mt-1 text-muted-foreground">
            Application {registration.applicationNumber}
          </p>
        </div>
        <Badge className="text-sm">{registration.status}</Badge>
      </div>

      <Tabs defaultValue="company" className="space-y-4">
        <TabsList className="h-auto flex-wrap justify-start">
          <TabsTrigger value="company">Company Details</TabsTrigger>
          <TabsTrigger value="contacts">
            Contacts ({contacts.length})
          </TabsTrigger>
          <TabsTrigger value="bank-accounts">
            Bank Accounts ({bankAccounts.length})
          </TabsTrigger>
          <TabsTrigger value="documents">
            Documents ({registration.documents?.length || 0})
          </TabsTrigger>
        </TabsList>

        <TabsContent value="company">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Building2 className="h-5 w-5" />
                Submitted Company Information
              </CardTitle>
            </CardHeader>
            <CardContent className="grid gap-4 md:grid-cols-2">
              <Detail label="Company Name" value={registration.companyName} />
              <Detail label="Supplier Type" value={registration.partnerType} />
              <Detail
                label="Registration Category"
                value={registration.registrationCategory}
              />
              <Detail
                label="Registration Number"
                value={registration.registrationNumber}
              />
              <Detail
                label="Tax Number"
                value={registrationString(data, 'taxNumber')}
              />
              <Detail label="Email" value={registration.email} />
              <Detail label="Phone" value={registration.phone} />
              <Detail
                label="Address"
                value={[
                  registrationString(data, 'physicalAddress'),
                  registrationString(data, 'city'),
                  registrationString(data, 'country'),
                ]
                  .filter(Boolean)
                  .join(', ')}
              />
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="contacts">
          <SupplierContactsPanel contacts={contacts} />
        </TabsContent>

        <TabsContent value="bank-accounts">
          <SupplierBankAccountsPanel
            accounts={bankAccounts}
            maskAccountNumbers
          />
        </TabsContent>

        <TabsContent value="documents">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <FileText className="h-5 w-5" />
                Submitted Documents
              </CardTitle>
            </CardHeader>
            <CardContent>
              {!registration.documents?.length ? (
                <p className="py-8 text-center text-muted-foreground">
                  No documents are attached to this registration.
                </p>
              ) : (
                <div className="space-y-3">
                  {registration.documents.map((document) => (
                    <div
                      key={document.id}
                      className="flex items-center justify-between rounded-lg border p-4"
                    >
                      <div>
                        <p className="font-medium">{document.documentName}</p>
                        <p className="text-sm text-muted-foreground">
                          {document.documentType}
                        </p>
                      </div>
                      <Badge
                        variant={document.isVerified ? 'default' : 'outline'}
                      >
                        {document.isRejected
                          ? 'Rejected'
                          : document.isVerified
                            ? 'Verified'
                            : 'Pending review'}
                      </Badge>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}

function Detail({ label, value }: { label: string; value?: string }) {
  return (
    <div>
      <Label className="text-muted-foreground">{label}</Label>
      <p className="font-semibold">{value || 'Not provided'}</p>
    </div>
  );
}
