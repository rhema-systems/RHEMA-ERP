'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from '@/components/ui/tabs';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { FileText, LogOut, RefreshCw, ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';
import { supplierApplicantAccessService as service } from '@/services/procurement-supplier-applicant-access.service';
import type {
  SupplierApplicantPaymentMethod,
  SupplierApplicantPortal,
  SupplierRegistrationCategory,
} from '@/types/procurement-supplier-applicant-access';

export default function SupplierApplicantPortalPage() {
  const router = useRouter();
  const [portal, setPortal] = useState<SupplierApplicantPortal | null>(null);
  const [methods, setMethods] = useState<SupplierApplicantPaymentMethod[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [companyName, setCompanyName] = useState('');
  const [category, setCategory] =
    useState<SupplierRegistrationCategory>('Goods');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [taxNumber, setTaxNumber] = useState('');
  const [physicalAddress, setPhysicalAddress] = useState('');
  const [city, setCity] = useState('');
  const [country, setCountry] = useState('Ghana');
  const [paymentMethodId, setPaymentMethodId] = useState('');
  const [paymentReference, setPaymentReference] = useState('');
  const [documentType, setDocumentType] = useState('');
  const [file, setFile] = useState<File | null>(null);

  const hydrate = useCallback((value: SupplierApplicantPortal) => {
    setPortal(value);
    setCompanyName(value.companyName);
    setCategory(value.registrationCategory || 'Goods');
    setEmail(value.email || '');
    setPhone(value.phone || '');
    try {
      const raw = JSON.parse(value.registrationData || '{}');
      const data =
        typeof raw.registrationData === 'string'
          ? JSON.parse(raw.registrationData)
          : raw;
      setTaxNumber(data.taxNumber || '');
      setPhysicalAddress(data.physicalAddress || '');
      setCity(data.city || '');
      setCountry(data.country || 'Ghana');
    } catch {
      // Retain editable defaults when a legacy draft has non-standard JSON.
    }
  }, []);

  const load = useCallback(async () => {
    if (!service.getSessionToken()) {
      router.replace('/supplier-application');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const value = await service.portal();
      hydrate(value);
      if (value.paymentOnly) {
        const options = await service.paymentMethods();
        setMethods(options);
        setPaymentMethodId(options[0]?.id || '');
      }
    } catch (loadError) {
      const message =
        loadError instanceof Error ? loadError.message : 'Could not load application.';
      setError(message);
    } finally {
      setBusy(false);
    }
  }, [hydrate, router]);

  useEffect(() => {
    void load();
  }, [load]);

  const registrationData = useMemo(
    () =>
      JSON.stringify({
        companyName,
        partnerType: 'Supplier',
        registrationCategory: category,
        email: email || null,
        phone: phone || null,
        taxNumber,
        physicalAddress,
        city,
        country,
      }),
    [
      category,
      city,
      companyName,
      country,
      email,
      phone,
      physicalAddress,
      taxNumber,
    ]
  );

  const save = async () => {
    setBusy(true);
    try {
      hydrate(
        await service.updateApplication({
          companyName,
          registrationCategory: category,
          email: email || undefined,
          phone: phone || undefined,
          registrationData,
        })
      );
      toast.success('Application saved.');
    } catch (saveError) {
      toast.error(
        saveError instanceof Error ? saveError.message : 'Application was not saved.'
      );
    } finally {
      setBusy(false);
    }
  };

  const submit = async () => {
    setBusy(true);
    try {
      hydrate(await service.submit());
      toast.success('Application submitted for review.');
    } catch (submitError) {
      toast.error(
        submitError instanceof Error ? submitError.message : 'Submission failed.'
      );
    } finally {
      setBusy(false);
    }
  };

  const pay = async () => {
    if (!portal) return;
    setBusy(true);
    try {
      await service.recordPayment({
        paymentMethodId,
        paymentReference: paymentReference || undefined,
        rowVersion: portal.tokenRowVersion,
      });
      await load();
      toast.success('Payment posted and the application is unlocked.');
    } catch (paymentError) {
      toast.error(
        paymentError instanceof Error ? paymentError.message : 'Payment failed.'
      );
    } finally {
      setBusy(false);
    }
  };

  const upload = async () => {
    if (!file || !documentType) return;
    setBusy(true);
    try {
      const form = new FormData();
      form.append('file', file);
      form.append('documentType', documentType);
      await service.uploadDocument(form);
      setFile(null);
      setDocumentType('');
      await load();
      toast.success('Document uploaded.');
    } catch (uploadError) {
      toast.error(
        uploadError instanceof Error ? uploadError.message : 'Upload failed.'
      );
    } finally {
      setBusy(false);
    }
  };

  const exit = () => {
    service.clearSession();
    router.replace('/supplier-application');
  };

  if (error) {
    return (
      <main className="min-h-screen bg-slate-100 p-6">
        <Alert variant="destructive" className="mx-auto max-w-2xl">
          <AlertDescription>{error}</AlertDescription>
          <Button className="mt-4" variant="outline" onClick={exit}>
            Return to token login
          </Button>
        </Alert>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-slate-100">
      <header className="border-b bg-white">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-5 py-4">
          <div>
            <div className="flex items-center gap-2 font-semibold">
              <ShieldCheck className="h-5 w-5 text-emerald-600" />
              Restricted supplier applicant portal
            </div>
            <p className="text-xs text-slate-500">
              Application, documents, payment and status only
            </p>
          </div>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" onClick={() => void load()}>
              <RefreshCw className="mr-2 h-4 w-4" />
              Refresh
            </Button>
            <Button variant="outline" size="sm" onClick={exit}>
              <LogOut className="mr-2 h-4 w-4" />
              End session
            </Button>
          </div>
        </div>
      </header>

      <div className="mx-auto grid max-w-6xl gap-5 px-5 py-6">
        <Card>
          <CardContent className="flex flex-wrap items-center justify-between gap-3 pt-6">
            <div>
              <div className="text-sm text-slate-500">Application</div>
              <div className="text-xl font-semibold">
                {portal?.registrationNumber || 'Loading…'}
              </div>
            </div>
            <div className="flex flex-wrap gap-2">
              <Badge>{portal?.status || 'Loading'}</Badge>
              <Badge variant="outline">Token {String(portal?.tokenStatus ?? '')}</Badge>
              <Badge variant="outline">
                Payment {String(portal?.paymentStatus ?? '')}
              </Badge>
            </div>
          </CardContent>
        </Card>

        {portal?.paymentOnly && (
          <Alert className="border-amber-300 bg-amber-50">
            <AlertDescription>
              The effective configuration requires payment before application editing
              and document submission are unlocked.
            </AlertDescription>
          </Alert>
        )}

        <Tabs defaultValue={portal?.paymentOnly ? 'payment' : 'application'}>
          <TabsList className="grid w-full grid-cols-4 md:w-[620px]">
            <TabsTrigger value="application" disabled={portal?.paymentOnly}>
              Application
            </TabsTrigger>
            <TabsTrigger value="documents" disabled={portal?.paymentOnly}>
              Documents
            </TabsTrigger>
            <TabsTrigger value="status">Status</TabsTrigger>
            <TabsTrigger value="payment">Payment</TabsTrigger>
          </TabsList>

          <TabsContent value="application">
            <Card>
              <CardHeader>
                <CardTitle>Application details</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-4 md:grid-cols-2">
                {[
                  ['Company name', companyName, setCompanyName],
                  ['Email', email, setEmail],
                  ['Phone', phone, setPhone],
                  ['Tax number', taxNumber, setTaxNumber],
                  ['Physical address', physicalAddress, setPhysicalAddress],
                  ['City', city, setCity],
                  ['Country', country, setCountry],
                ].map(([label, value, setter]) => (
                  <div className="grid gap-2" key={label as string}>
                    <Label>{label as string}</Label>
                    <Input
                      value={value as string}
                      disabled={!portal?.canEdit}
                      onChange={(event) =>
                        (setter as (value: string) => void)(event.target.value)
                      }
                    />
                  </div>
                ))}
                <div className="grid gap-2">
                  <Label>Category</Label>
                  <select
                    className="h-10 rounded-md border bg-white px-3"
                    value={category}
                    disabled={!portal?.canEdit}
                    onChange={(event) =>
                      setCategory(event.target.value as SupplierRegistrationCategory)
                    }
                  >
                    <option>Goods</option>
                    <option>Works</option>
                    <option>Services</option>
                  </select>
                </div>
                <div className="flex gap-2 md:col-span-2">
                  <Button disabled={busy || !portal?.canEdit} onClick={save}>
                    Save application
                  </Button>
                  <Button
                    variant="secondary"
                    disabled={busy || !portal?.canSubmit}
                    onClick={submit}
                  >
                    Submit for review
                  </Button>
                </div>
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="documents">
            <Card>
              <CardHeader>
                <CardTitle>Application evidence</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-4">
                <div className="grid gap-3 rounded-lg border p-4 md:grid-cols-3">
                  <Input
                    placeholder="Document type"
                    value={documentType}
                    onChange={(event) => setDocumentType(event.target.value)}
                  />
                  <Input
                    type="file"
                    onChange={(event) => setFile(event.target.files?.[0] || null)}
                  />
                  <Button disabled={busy || !file || !documentType} onClick={upload}>
                    Upload document
                  </Button>
                </div>
                {(portal?.documents || []).map((document) => (
                  <div
                    key={document.id}
                    className="flex items-center justify-between gap-4 rounded-lg border p-3"
                  >
                    <div className="flex items-center gap-3">
                      <FileText className="h-5 w-5 text-slate-500" />
                      <div>
                        <div className="font-medium">{document.documentName}</div>
                        <div className="text-xs text-slate-500">
                          {document.documentType} ·{' '}
                          {Math.ceil(document.fileSize / 1024)} KB
                        </div>
                      </div>
                    </div>
                    <Badge variant={document.isVerified ? 'default' : 'outline'}>
                      {document.isRejected
                        ? 'Rejected'
                        : document.isVerified
                          ? 'Verified'
                          : 'Submitted'}
                    </Badge>
                  </div>
                ))}
                {!portal?.documents?.length && (
                  <div className="rounded-lg border border-dashed p-8 text-center text-sm text-slate-500">
                    No application documents have been uploaded.
                  </div>
                )}
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="status">
            <Card>
              <CardHeader>
                <CardTitle>Review progress</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-3">
                {(portal?.statusHistory || []).map((entry) => (
                  <div key={entry.id} className="rounded-lg border p-3">
                    <div className="flex justify-between gap-3">
                      <strong>{entry.toStatus}</strong>
                      <span className="text-xs text-slate-500">
                        {new Date(entry.changedAt).toLocaleString()}
                      </span>
                    </div>
                    {entry.notes && (
                      <p className="mt-1 text-sm text-slate-600">{entry.notes}</p>
                    )}
                  </div>
                ))}
                {portal?.rejectionReason && (
                  <Alert variant="destructive">
                    <AlertDescription>{portal.rejectionReason}</AlertDescription>
                  </Alert>
                )}
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="payment">
            <Card>
              <CardHeader>
                <CardTitle>Token fee</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-4">
                <div className="text-2xl font-semibold">
                  {portal?.currencyCode} {portal?.totalAmount?.toFixed(2)}
                </div>
                {portal?.paymentOnly ? (
                  <>
                    <div className="grid gap-2">
                      <Label>Payment method</Label>
                      <select
                        className="h-10 rounded-md border bg-white px-3"
                        value={paymentMethodId}
                        onChange={(event) => setPaymentMethodId(event.target.value)}
                      >
                        {methods.map((method) => (
                          <option key={method.id} value={method.id}>
                            {method.name}
                          </option>
                        ))}
                      </select>
                    </div>
                    <div className="grid gap-2">
                      <Label>Payment reference</Label>
                      <Input
                        value={paymentReference}
                        onChange={(event) => setPaymentReference(event.target.value)}
                      />
                    </div>
                    <Button
                      className="w-fit"
                      disabled={busy || !paymentMethodId}
                      onClick={pay}
                    >
                      Record configured payment
                    </Button>
                  </>
                ) : (
                  <Alert>
                    <AlertDescription>
                      No payment action is outstanding for this token.
                    </AlertDescription>
                  </Alert>
                )}
              </CardContent>
            </Card>
          </TabsContent>
        </Tabs>
      </div>
    </main>
  );
}
