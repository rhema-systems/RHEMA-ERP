'use client';

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { FileText, LogOut, RefreshCw, ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';
import { supplierApplicantAccessService as service } from '@/services/procurement-supplier-applicant-access.service';
import type {
  SupplierApplicantPaymentMethod,
  SupplierApplicantPortal,
  SupplierRegistrationCategory,
} from '@/types/procurement-supplier-applicant-access';

function formatFileSize(bytes: number) {
  if (!bytes) return 'configured size';
  if (bytes < 1024 * 1024) return `${Math.ceil(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

type RegistrationDataRecord = Record<string, unknown>;

const editableRegistrationKeys = new Set(
  [
    'companyName',
    'partnerType',
    'registrationCategory',
    'email',
    'phone',
    'taxNumber',
    'physicalAddress',
    'city',
    'country',
  ].map((key) => key.toLowerCase())
);

function parseRegistrationData(value?: string): RegistrationDataRecord {
  if (!value?.trim()) return {};

  let current: unknown = JSON.parse(value);
  for (let depth = 0; depth < 3; depth += 1) {
    if (!current || Array.isArray(current) || typeof current !== 'object')
      break;
    const record = current as RegistrationDataRecord;
    const wrapper = Object.keys(record).find(
      (key) => key.toLowerCase() === 'registrationdata'
    );
    if (!wrapper) break;
    const nested = record[wrapper];
    if (typeof nested === 'string' && nested.trim()) {
      current = JSON.parse(nested);
      continue;
    }
    if (nested && !Array.isArray(nested) && typeof nested === 'object') {
      current = nested;
      continue;
    }
    break;
  }

  return current && !Array.isArray(current) && typeof current === 'object'
    ? (current as RegistrationDataRecord)
    : {};
}

function registrationString(data: RegistrationDataRecord, key: string) {
  const actualKey = Object.keys(data).find(
    (candidate) => candidate.toLowerCase() === key.toLowerCase()
  );
  const value = actualKey ? data[actualKey] : undefined;
  return typeof value === 'string' ? value : '';
}

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
  const [evidenceRequirementCode, setEvidenceRequirementCode] = useState('');
  const [classificationCode, setClassificationCode] = useState('');
  const [issueDate, setIssueDate] = useState('');
  const [expiryDate, setExpiryDate] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [fileInputKey, setFileInputKey] = useState(0);
  const [savedRegistrationData, setSavedRegistrationData] =
    useState<RegistrationDataRecord>({});

  const hydrate = useCallback((value: SupplierApplicantPortal) => {
    setPortal(value);
    setCompanyName(value.companyName);
    setCategory(value.registrationCategory || 'Goods');
    setEmail(value.email || '');
    setPhone(value.phone || '');
    setEvidenceRequirementCode((current) => {
      const requirements = value.evidenceReadiness?.requirements || [];
      if (
        requirements.some(
          (requirement) => requirement.requirementCode === current
        )
      ) {
        return current;
      }
      return (
        requirements.find(
          (requirement) => requirement.isMandatory && !requirement.isSatisfied
        )?.requirementCode ||
        requirements.find((requirement) => !requirement.isSatisfied)
          ?.requirementCode ||
        requirements[0]?.requirementCode ||
        ''
      );
    });
    try {
      const data = parseRegistrationData(value.registrationData);
      setSavedRegistrationData(data);
      setTaxNumber(registrationString(data, 'taxNumber'));
      setPhysicalAddress(registrationString(data, 'physicalAddress'));
      setCity(registrationString(data, 'city'));
      setCountry(registrationString(data, 'country') || 'Ghana');
    } catch {
      // Retain editable defaults when a legacy draft has non-standard JSON.
      setSavedRegistrationData({});
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
        loadError instanceof Error
          ? loadError.message
          : 'Could not load application.';
      setError(message);
    } finally {
      setBusy(false);
    }
  }, [hydrate, router]);

  useEffect(() => {
    void load();
  }, [load]);

  const registrationData = useMemo(() => {
    const preserved = Object.fromEntries(
      Object.entries(savedRegistrationData).filter(
        ([key]) => !editableRegistrationKeys.has(key.toLowerCase())
      )
    );
    return JSON.stringify({
      ...preserved,
      companyName,
      partnerType: 'Supplier',
      registrationCategory: category,
      email: email || null,
      phone: phone || null,
      taxNumber,
      physicalAddress,
      city,
      country,
    });
  }, [
    category,
    city,
    companyName,
    country,
    email,
    phone,
    physicalAddress,
    savedRegistrationData,
    taxNumber,
  ]);

  const evidenceRequirements = portal?.evidenceReadiness?.requirements || [];
  const selectedEvidenceRequirement = evidenceRequirements.find(
    (requirement) => requirement.requirementCode === evidenceRequirementCode
  );
  const documentType =
    selectedEvidenceRequirement?.documentType?.trim() ||
    selectedEvidenceRequirement?.name?.trim() ||
    '';
  const requiresClassification =
    selectedEvidenceRequirement?.kind === 'Classification' ||
    selectedEvidenceRequirement?.kind === 'DocumentAndClassification';
  const requiresExpiry =
    selectedEvidenceRequirement?.validityMode === 'CurrentOnSubmission' ||
    selectedEvidenceRequirement?.validityMode === 'MinimumRemainingDays';
  const requirementFileLimit =
    selectedEvidenceRequirement?.maxFileSizeBytes || 0;
  const exceedsRequirementFileLimit =
    file !== null &&
    requirementFileLimit > 0 &&
    file.size > requirementFileLimit;
  const uploadReady =
    Boolean(portal?.canEdit) &&
    Boolean(file) &&
    Boolean(selectedEvidenceRequirement) &&
    Boolean(documentType) &&
    (!requiresClassification || Boolean(classificationCode)) &&
    (!requiresExpiry || Boolean(expiryDate)) &&
    !exceedsRequirementFileLimit;

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
        saveError instanceof Error
          ? saveError.message
          : 'Application was not saved.'
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
        submitError instanceof Error
          ? submitError.message
          : 'Submission failed.'
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
      service.clearSession();
      toast.success(
        'Payment submitted. Return here after the application token is sent following trusted verification.'
      );
      router.replace('/supplier-application?tab=login&payment=pending');
    } catch (paymentError) {
      toast.error(
        paymentError instanceof Error ? paymentError.message : 'Payment failed.'
      );
    } finally {
      setBusy(false);
    }
  };

  const upload = async () => {
    if (!file || !selectedEvidenceRequirement || !uploadReady) return;
    setBusy(true);
    try {
      const form = new FormData();
      form.append('file', file);
      form.append('documentType', documentType);
      form.append(
        'evidenceRequirementCode',
        selectedEvidenceRequirement.requirementCode
      );
      if (classificationCode) {
        form.append('classificationCode', classificationCode);
      }
      if (issueDate) {
        form.append('issueDate', issueDate);
      }
      if (expiryDate) {
        form.append('expiryDate', expiryDate);
      }
      await service.uploadDocument(form);
      setFile(null);
      setClassificationCode('');
      setIssueDate('');
      setExpiryDate('');
      setFileInputKey((current) => current + 1);
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
              Payment, application, documents and status only
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
            <div className="flex flex-wrap items-center justify-end gap-2">
              <div className="flex flex-wrap gap-2">
                <Badge>{portal?.status || 'Loading'}</Badge>
                <Badge variant="outline">
                  Token {String(portal?.tokenStatus ?? '')}
                </Badge>
                <Badge variant="outline">
                  Payment {String(portal?.paymentStatus ?? '')}
                </Badge>
              </div>
              <Button
                type="button"
                disabled={busy || !portal?.canSubmit}
                onClick={submit}
              >
                Submit for review
              </Button>
            </div>
          </CardContent>
        </Card>

        {portal?.paymentOnly && (
          <Alert className="border-amber-300 bg-amber-50">
            <AlertDescription>
              The effective configuration requires a trusted provider or cashier
              to verify payment before application editing, document submission
              and status tracking are unlocked.
            </AlertDescription>
          </Alert>
        )}

        <Tabs defaultValue="payment">
          <TabsList className="grid w-full grid-cols-4 md:w-[620px]">
            <TabsTrigger value="payment">Payment</TabsTrigger>
            <TabsTrigger value="application" disabled={portal?.paymentOnly}>
              Application
            </TabsTrigger>
            <TabsTrigger value="documents" disabled={portal?.paymentOnly}>
              Documents
            </TabsTrigger>
            <TabsTrigger value="status" disabled={portal?.paymentOnly}>
              Status
            </TabsTrigger>
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
                      setCategory(
                        event.target.value as SupplierRegistrationCategory
                      )
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
                {evidenceRequirements.length ? (
                  <>
                    <div className="grid gap-3 rounded-lg border p-4 md:grid-cols-2">
                      <div className="grid gap-2 md:col-span-2">
                        <Label htmlFor="evidence-requirement">
                          Evidence requirement
                        </Label>
                        <select
                          id="evidence-requirement"
                          className="h-10 rounded-md border bg-white px-3"
                          value={evidenceRequirementCode}
                          disabled={busy || !portal?.canEdit}
                          onChange={(event) => {
                            setEvidenceRequirementCode(event.target.value);
                            setClassificationCode('');
                            setIssueDate('');
                            setExpiryDate('');
                            setFile(null);
                            setFileInputKey((current) => current + 1);
                          }}
                        >
                          {evidenceRequirements.map((requirement) => (
                            <option
                              key={requirement.requirementCode}
                              value={requirement.requirementCode}
                            >
                              {requirement.name}
                              {requirement.isMandatory
                                ? ' (required)'
                                : ' (optional)'}
                              {requirement.isSatisfied ? ' — supplied' : ''}
                            </option>
                          ))}
                        </select>
                      </div>

                      {selectedEvidenceRequirement && (
                        <div className="grid gap-2 rounded-md bg-slate-50 p-3 text-sm md:col-span-2">
                          <div className="flex flex-wrap items-center gap-2">
                            <Badge
                              variant={
                                selectedEvidenceRequirement.isSatisfied
                                  ? 'default'
                                  : 'outline'
                              }
                            >
                              {selectedEvidenceRequirement.isSatisfied
                                ? 'Requirement satisfied'
                                : 'Evidence needed'}
                            </Badge>
                            <span className="font-medium">
                              {selectedEvidenceRequirement.requirementCode}
                            </span>
                            <span className="text-slate-500">
                              {documentType}
                            </span>
                          </div>
                          <div className="text-xs text-slate-600">
                            Maximum{' '}
                            {formatFileSize(
                              selectedEvidenceRequirement.maxFileSizeBytes
                            )}
                            {selectedEvidenceRequirement.allowedMimeTypes.length
                              ? ` · ${selectedEvidenceRequirement.allowedMimeTypes.join(', ')}`
                              : ''}
                            {selectedEvidenceRequirement.validityMode ===
                            'MinimumRemainingDays'
                              ? ` · Must remain valid for at least ${selectedEvidenceRequirement.minimumRemainingDays || 0} days`
                              : selectedEvidenceRequirement.validityMode ===
                                  'CurrentOnSubmission'
                                ? ' · Must be current on submission'
                                : ''}
                          </div>
                          {selectedEvidenceRequirement.issues.length > 0 && (
                            <div className="text-xs text-amber-700">
                              {selectedEvidenceRequirement.issues.join('; ')}
                            </div>
                          )}
                        </div>
                      )}

                      {requiresClassification &&
                        selectedEvidenceRequirement && (
                          <div className="grid gap-2">
                            <Label htmlFor="evidence-classification">
                              {selectedEvidenceRequirement.classificationScheme ||
                                'Evidence'}{' '}
                              classification
                            </Label>
                            {selectedEvidenceRequirement.allowedClassifications
                              .length ? (
                              <select
                                id="evidence-classification"
                                className="h-10 rounded-md border bg-white px-3"
                                value={classificationCode}
                                disabled={busy || !portal?.canEdit}
                                onChange={(event) =>
                                  setClassificationCode(event.target.value)
                                }
                              >
                                <option value="">Select classification</option>
                                {selectedEvidenceRequirement.allowedClassifications.map(
                                  (classification) => (
                                    <option
                                      key={classification}
                                      value={classification}
                                    >
                                      {classification}
                                    </option>
                                  )
                                )}
                              </select>
                            ) : (
                              <Alert variant="destructive">
                                <AlertDescription>
                                  This evidence requirement needs a
                                  classification, but no permitted
                                  classifications are configured. Ask a
                                  procurement administrator to correct the
                                  published evidence pack.
                                </AlertDescription>
                              </Alert>
                            )}
                          </div>
                        )}

                      <div className="grid gap-2">
                        <Label htmlFor="evidence-issue-date">Issue date</Label>
                        <Input
                          id="evidence-issue-date"
                          type="date"
                          value={issueDate}
                          disabled={busy || !portal?.canEdit}
                          onChange={(event) => setIssueDate(event.target.value)}
                        />
                      </div>

                      {requiresExpiry && (
                        <div className="grid gap-2">
                          <Label htmlFor="evidence-expiry-date">
                            Expiry date
                          </Label>
                          <Input
                            id="evidence-expiry-date"
                            type="date"
                            value={expiryDate}
                            disabled={busy || !portal?.canEdit}
                            onChange={(event) =>
                              setExpiryDate(event.target.value)
                            }
                          />
                        </div>
                      )}

                      <div className="grid gap-2 md:col-span-2">
                        <Label htmlFor="evidence-file">Evidence file</Label>
                        <Input
                          key={fileInputKey}
                          id="evidence-file"
                          type="file"
                          accept={
                            selectedEvidenceRequirement?.allowedMimeTypes.join(
                              ','
                            ) || undefined
                          }
                          disabled={
                            busy ||
                            !portal?.canEdit ||
                            !selectedEvidenceRequirement
                          }
                          onChange={(event) =>
                            setFile(event.target.files?.[0] || null)
                          }
                        />
                        {exceedsRequirementFileLimit && (
                          <span className="text-xs text-destructive">
                            This file exceeds the requirement&apos;s{' '}
                            {formatFileSize(
                              selectedEvidenceRequirement?.maxFileSizeBytes || 0
                            )}{' '}
                            limit.
                          </span>
                        )}
                      </div>

                      <Button
                        className="w-fit md:col-span-2"
                        disabled={busy || !uploadReady}
                        onClick={upload}
                      >
                        Upload evidence
                      </Button>
                    </div>

                    <div className="grid gap-2">
                      {evidenceRequirements.map((requirement) => (
                        <div
                          key={requirement.requirementCode}
                          className="flex flex-wrap items-center justify-between gap-3 rounded-lg border p-3"
                        >
                          <div>
                            <div className="font-medium">
                              {requirement.name}
                            </div>
                            <div className="text-xs text-slate-500">
                              {requirement.requirementCode}
                              {requirement.matchedDocumentName
                                ? ` · ${requirement.matchedDocumentName}`
                                : ''}
                            </div>
                          </div>
                          <Badge
                            variant={
                              requirement.isSatisfied ? 'default' : 'outline'
                            }
                          >
                            {requirement.isSatisfied
                              ? 'Satisfied'
                              : requirement.isMandatory
                                ? 'Required'
                                : 'Optional'}
                          </Badge>
                        </div>
                      ))}
                    </div>
                  </>
                ) : (
                  <Alert className="border-amber-300 bg-amber-50">
                    <AlertDescription>
                      Evidence requirements are not available for the selected
                      supplier category. Save the application category and
                      refresh before uploading documents.
                    </AlertDescription>
                  </Alert>
                )}
                {(portal?.documents || []).map((document) => (
                  <div
                    key={document.id}
                    className="flex items-center justify-between gap-4 rounded-lg border p-3"
                  >
                    <div className="flex items-center gap-3">
                      <FileText className="h-5 w-5 text-slate-500" />
                      <div>
                        <div className="font-medium">
                          {document.documentName}
                        </div>
                        <div className="text-xs text-slate-500">
                          {document.documentType} ·{' '}
                          {Math.ceil(document.fileSize / 1024)} KB
                          {document.evidenceRequirementCode
                            ? ` · ${document.evidenceRequirementCode}`
                            : ''}
                        </div>
                      </div>
                    </div>
                    <Badge
                      variant={document.isVerified ? 'default' : 'outline'}
                    >
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
                      <p className="mt-1 text-sm text-slate-600">
                        {entry.notes}
                      </p>
                    )}
                  </div>
                ))}
                {portal?.rejectionReason && (
                  <Alert variant="destructive">
                    <AlertDescription>
                      {portal.rejectionReason}
                    </AlertDescription>
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
                        onChange={(event) =>
                          setPaymentMethodId(event.target.value)
                        }
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
                        onChange={(event) =>
                          setPaymentReference(event.target.value)
                        }
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
