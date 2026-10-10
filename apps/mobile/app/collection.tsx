import { Ionicons } from "@expo/vector-icons";
import * as Crypto from "expo-crypto";
import { Link, Redirect, useLocalSearchParams, useRouter } from "expo-router";
import { useEffect, useMemo, useState } from "react";
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from "react-native";
import { BankAccountPickerModal } from "@/components/bank-account-picker-modal";
import { ApiProblem, mobileApi } from "@/src/api/client";
import {
  buildCompleteCollectionRequest,
  requiresPartialPayment,
  sumCollectionAllocations,
  type CollectionAllocationDraft,
} from "@/src/collections/checkout";
import {
  isRetryableTransportFailure,
  loadSessionBankAccounts,
  loadSessionOutstandingInvoices,
} from "@/src/offline/collection-runtime";
import { isZcsSmartPosAdapter } from "@/src/hardware/zcs-smartpos";
import { openSessionOutbox } from "@/src/offline/sync-runtime";
import { printReceiptAsync, shareReceiptPdfAsync } from "@/src/receipts/output";
import { sumTenderDrafts, type TenderDraft } from "@/src/sales/checkout";
import { useSession } from "@/src/session/session-context";
import { getInstallationId } from "@/src/storage/secure-session";
import type {
  MobilePosBankAccountOption,
  MobilePosCollectionReceipt,
  MobilePosCollectionResult,
  MobilePosCustomerSearchResult,
  MobilePosPaymentMethod,
  MobilePosProvisionalCollectionReceipt,
  OutstandingInvoice,
} from "@/src/types/api";
import { colors } from "@/src/ui/theme";

const requiredPermissions = [
  "MobilePOS.Till.Operate",
  "MobilePOS.Payment.Collect",
  "Finance.AR.Payments.Receive",
];

export default function CollectionScreen() {
  const session = useSession();
  const router = useRouter();
  const params = useLocalSearchParams<{
    businessPartnerId?: string;
    businessPartnerRoleId?: string;
    customerCode?: string;
    customerName?: string;
    initialInvoiceId?: string;
  }>();
  const bootstrap = session.bootstrap;
  const permissions = session.user?.permissions ?? [];
  const missingPermission = requiredPermissions.find(permission => !permissions.includes(permission));
  const canReprintReceipt = permissions.includes("MobilePOS.Receipt.Reprint");
  const customer = useMemo<MobilePosCustomerSearchResult | null>(() => {
    if (!params.businessPartnerId || !params.businessPartnerRoleId) return null;
    return {
      businessPartnerId: params.businessPartnerId,
      businessPartnerRoleId: params.businessPartnerRoleId,
      code: params.customerCode ?? "Customer",
      name: params.customerName ?? "Approved customer",
      currencyCode: bootstrap?.store.currencyCode ?? "GHS",
      isDefaultWalkInCustomer: false,
    };
  }, [bootstrap?.store.currencyCode, params.businessPartnerId, params.businessPartnerRoleId, params.customerCode, params.customerName]);
  const onlineMethods = useMemo(
    () => bootstrap?.till.paymentMethods.filter(method => method.allowOnline) ?? [],
    [bootstrap],
  );
  const offlineMethods = useMemo(() => {
    const allowed = new Set(session.offlineGrant?.policy.allowedPaymentMethods.map(method => method.paymentMethodId) ?? []);
    return bootstrap?.till.paymentMethods.filter(method => method.allowOffline && allowed.has(method.paymentMethodId)) ?? [];
  }, [bootstrap, session.offlineGrant]);

  const [invoices, setInvoices] = useState<OutstandingInvoice[]>([]);
  const [usingCachedInvoices, setUsingCachedInvoices] = useState(false);
  const [invoiceCacheTime, setInvoiceCacheTime] = useState<string | null>(null);
  const [allocations, setAllocations] = useState<CollectionAllocationDraft[]>([]);
  const [tenders, setTenders] = useState<TenderDraft[]>([]);
  const [bankAccounts, setBankAccounts] = useState<MobilePosBankAccountOption[]>([]);
  const [bankAccountTenderId, setBankAccountTenderId] = useState<string | null>(null);
  const [identity, setIdentity] = useState<{ clientMutationId: string; localReference: string; occurredAtUtc: string } | null>(null);
  const [result, setResult] = useState<MobilePosCollectionResult | null>(null);
  const [receipt, setReceipt] = useState<MobilePosCollectionReceipt | null>(null);
  const [outputMessage, setOutputMessage] = useState<string | null>(null);
  const [queuedCollection, setQueuedCollection] = useState<{
    localReference: string;
    totalAmount: number;
    currencyCode: string;
    allocations: Array<{ invoiceNumber: string; amount: number }>;
    provisionalReceipt: boolean;
    receipt?: MobilePosProvisionalCollectionReceipt;
  } | null>(null);
  const [busy, setBusy] = useState<"load" | "bankAccounts" | "complete" | "reprint" | "print" | "share" | null>(null);
  const [error, setError] = useState<ApiProblem | null>(null);

  useEffect(() => {
    if (session.status !== "ready" || !customer) return;
    let active = true;
    setBusy("load");
    setError(null);
    void (async () => {
      try {
        if (!session.user || !bootstrap) throw new Error("The signed-in Mobile POS session is unavailable.");
        const loaded = await loadSessionOutstandingInvoices(
          session.user,
          bootstrap,
          await getInstallationId(),
          customer,
        );
        if (!active) return;
        const current = loaded.invoices;
        setInvoices(current);
        setUsingCachedInvoices(loaded.source === "Cached");
        setInvoiceCacheTime(loaded.source === "Cached" ? loaded.cachedAtUtc ?? null : null);
        const initial = current.map(invoice => ({
          invoiceId: invoice.id,
          amountText: invoice.id === params.initialInvoiceId ? invoice.balanceAmount.toFixed(2) : "",
        }));
        setAllocations(initial);
        const initialTotal = sumCollectionAllocations(initial);
        const methods = loaded.source === "Cached" ? offlineMethods : onlineMethods;
        const firstMethod = methods.find(method => !method.requiresBankAccount) ?? methods[0];
        setTenders(firstMethod && initialTotal > 0 ? [{
          paymentMethodId: firstMethod.paymentMethodId,
          amountText: initialTotal.toFixed(2),
          externalReference: "",
        }] : []);
      } catch (caught) {
        if (active) setError(asProblem(caught));
      } finally {
        if (active) setBusy(null);
      }
    })();
    return () => { active = false; };
  }, [bootstrap, customer, offlineMethods, onlineMethods, params.initialInvoiceId, session.status, session.user]);

  if (session.status === "signedOut" || session.status === "mfaRequired") return <Redirect href="/login" />;
  if (session.status !== "ready" || !bootstrap) return <Redirect href="/" />;
  if (!customer) return <Redirect href="/customers" />;

  const allocationTotal = sumCollectionAllocations(allocations);
  const tenderTotal = sumTenderDrafts(tenders, 2);
  const totalsMatch = Math.abs(allocationTotal - tenderTotal) < 0.01;
  const paymentMethods = usingCachedInvoices ? offlineMethods : onlineMethods;
  const submitDisabled = busy !== null
    || invoices.length === 0
    || Boolean(missingPermission)
    || !bootstrap.currentTillSessionId
    || allocationTotal <= 0
    || tenders.length === 0
    || !totalsMatch;

  const updateAllocation = (invoiceId: string, amountText: string) => {
    const next = allocations.map(item => item.invoiceId === invoiceId ? { ...item, amountText } : item);
    setAllocations(next);
    const singleTender = tenders.length === 1 ? tenders[0] : undefined;
    if (singleTender) {
      setTenders([{ ...singleTender, amountText: sumCollectionAllocations(next).toFixed(2) }]);
    }
    setIdentity(null);
    setResult(null);
  };

  const addTender = (method: MobilePosPaymentMethod) => {
    if (tenders.some(tender => tender.paymentMethodId === method.paymentMethodId)) return;
    const remaining = Math.max(0, allocationTotal - tenderTotal);
    setTenders(current => [...current, {
      paymentMethodId: method.paymentMethodId,
      amountText: remaining > 0 ? remaining.toFixed(2) : "",
      externalReference: "",
    }]);
    setIdentity(null);
  };

  const updateTender = (paymentMethodId: string, patch: Partial<TenderDraft>) => {
    setTenders(current => current.map(item => item.paymentMethodId === paymentMethodId ? { ...item, ...patch } : item));
    setIdentity(null);
  };

  const openBankAccountPicker = async (paymentMethodId: string) => {
    setBusy("bankAccounts");
    setError(null);
    try {
      const accounts = bankAccounts.length > 0
        ? bankAccounts
        : session.user
          ? await loadSessionBankAccounts(session.user, bootstrap, await getInstallationId())
          : [];
      setBankAccounts(accounts);
      setBankAccountTenderId(paymentMethodId);
    } catch (caught) {
      setError(asProblem(caught));
    } finally {
      setBusy(null);
    }
  };

  const complete = async () => {
    if (missingPermission) return setError(problem(`Your role is missing ${missingPermission}.`));
    if (!bootstrap.currentTillSessionId) return setError(problem("Open your assigned till session before collecting a payment."));
    for (const tender of tenders) {
      const method = paymentMethods.find(item => item.paymentMethodId === tender.paymentMethodId);
      if (!method) return setError(problem("A selected tender is no longer available for this till."));
      if (method.requiresBankAccount && !tender.bankAccountId) return setError(problem(`${method.name} requires a bank account selection.`));
      if ((method.requiresReference || method.requireExternalAuthorizationReference) && !tender.externalReference.trim()) {
        return setError(problem(`${method.name} requires the provider or transaction reference.`));
      }
    }
    const nextIdentity = identity ?? createIdentity();
    setIdentity(nextIdentity);
    setBusy("complete");
    setError(null);
    try {
      const request = buildCompleteCollectionRequest({
        installationId: await getInstallationId(),
        ...nextIdentity,
        businessPartnerId: customer.businessPartnerId,
        businessPartnerRoleId: customer.businessPartnerRoleId,
        invoices,
        allocations,
        tenders,
      });
      if (usingCachedInvoices) {
        await queueOfflineCollection(request);
      } else {
        try {
          const completed = await mobileApi.completeCollection(request);
          setResult(completed);
          try {
            setReceipt(await mobileApi.getCollectionReceipt(completed.collectionId, await getInstallationId()));
          } catch (receiptError) {
            setError(asProblem(receiptError));
          }
        } catch (caught) {
          if (!isRetryableTransportFailure(caught)) throw caught;
          await queueOfflineCollection(request);
        }
      }
    } catch (caught) {
      setError(asProblem(caught));
    } finally {
      setBusy(null);
    }
  };

  const queueOfflineCollection = async (request: ReturnType<typeof buildCompleteCollectionRequest>) => {
    const grant = session.offlineGrant;
    if (!grant || !session.user || !bootstrap.currentTillSessionId) {
      throw problem("A current signed offline authorization is required to queue this collection.");
    }
    if (!grant.policy.allowedCommandTypes.includes("CashReceipt")) {
      throw problem("The signed offline policy does not authorize customer collections.");
    }
    if (requiresPartialPayment(invoices, allocations)
      && (!grant.policy.allowPartialPayment || !grant.policy.allowedCommandTypes.includes("PartialPayment"))) {
      throw problem("The signed offline policy does not authorize partial invoice payments.");
    }
    const allowedTenderIds = new Set(grant.policy.allowedPaymentMethods.map(method => method.paymentMethodId));
    if (!request.tenders.every(tender => {
      const method = bootstrap.till.paymentMethods.find(item => item.paymentMethodId === tender.paymentMethodId);
      return Boolean(method?.allowOffline && allowedTenderIds.has(tender.paymentMethodId));
    })) {
      throw problem("One or more selected tenders are not authorized for offline collection.");
    }
    if (grant.policy.maximumTransactionAmount != null
      && request.allocations.reduce((total, item) => total + item.amount, 0) > grant.policy.maximumTransactionAmount) {
      throw problem("This collection exceeds the signed offline per-transaction limit.");
    }
    await (await openSessionOutbox(session.user, bootstrap)).enqueue({
      clientMutationId: request.clientMutationId,
      localReference: request.localReference,
      commandType: "CashReceipt",
      schemaVersion: 1,
      tillSessionId: bootstrap.currentTillSessionId,
      offlineGrantId: grant.id,
      payload: request,
    });
    const invoiceById = new Map(invoices.map(invoice => [invoice.id, invoice]));
    const occurredAtUtc = request.occurredAtUtc ?? new Date().toISOString();
    const provisionalReceipt = grant.policy.allowProvisionalReceipt ? {
      receiptKind: "COLLECTION_PROVISIONAL" as const,
      receiptId: request.clientMutationId,
      copyType: "PROVISIONAL" as const,
      copyNumber: 0 as const,
      reprintCount: 0 as const,
      generatedAtUtc: new Date().toISOString(),
      qrReference: `RHEMA|MOBILEPOS|PROVISIONAL|${request.localReference}|${request.clientMutationId}`,
      tenantId: session.user.currentTenantId ?? "",
      tenantCode: session.user.currentTenantCode ?? "",
      tenantName: session.user.currentTenantName ?? "RHEMA ERP",
      storeId: bootstrap.store.id,
      storeCode: bootstrap.store.code,
      storeName: bootstrap.store.name,
      locationName: bootstrap.store.code,
      tillId: bootstrap.till.id,
      tillNumber: bootstrap.till.tillNumber,
      tillName: bootstrap.till.name,
      tillSessionId: bootstrap.currentTillSessionId,
      tillSessionNumber: "PENDING SYNC",
      businessDate: occurredAtUtc.slice(0, 10),
      deviceId: bootstrap.device.id,
      deviceName: bootstrap.device.deviceName,
      cashierUserId: session.user.id,
      cashierName: bootstrap.userName,
      businessPartnerId: customer.businessPartnerId,
      businessPartnerRoleId: customer.businessPartnerRoleId,
      customerCode: customer.code,
      customerName: customer.name,
      localReference: request.localReference,
      occurredAtUtc,
      currencyCode: bootstrap.store.currencyCode,
      totalAmount: request.allocations.reduce((total, item) => total + item.amount, 0),
      wasRecordedOffline: true as const,
      allocations: request.allocations.map((item, index) => ({
        sequence: index + 1,
        invoiceId: item.invoiceId,
        invoiceNumber: invoiceById.get(item.invoiceId)?.invoiceNumber ?? item.invoiceId,
        amount: item.amount,
      })),
      tenders: request.tenders.map((item, index) => {
        const method = paymentMethods.find(value => value.paymentMethodId === item.paymentMethodId);
        return {
          sequence: index + 1,
          paymentMethodCode: method?.code ?? item.paymentMethodId,
          paymentMethodName: method?.name ?? "Tender",
          amount: item.amount,
          externalReference: item.externalReference,
        };
      }),
    } satisfies MobilePosProvisionalCollectionReceipt : undefined;
    setResult(null);
    setReceipt(null);
    setQueuedCollection({
      localReference: request.localReference,
      totalAmount: request.allocations.reduce((total, item) => total + item.amount, 0),
      currencyCode: bootstrap.store.currencyCode,
      allocations: request.allocations.map(item => ({
        invoiceNumber: invoiceById.get(item.invoiceId)?.invoiceNumber ?? item.invoiceId,
        amount: item.amount,
      })),
      provisionalReceipt: grant.policy.allowProvisionalReceipt,
      receipt: provisionalReceipt,
    });
  };

  const createReprintCopy = async () => {
    if (!result || !canReprintReceipt) return;
    setBusy("reprint");
    setError(null);
    setOutputMessage(null);
    try {
      setReceipt(await mobileApi.recordCollectionReceiptReprint(result.collectionId, {
        installationId: await getInstallationId(),
        clientEventId: Crypto.randomUUID(),
        reason: "Cashier requested another collection receipt copy",
      }));
    } catch (caught) {
      setError(asProblem(caught));
    } finally {
      setBusy(null);
    }
  };

  const printCurrentReceipt = async (current: MobilePosCollectionReceipt | MobilePosProvisionalCollectionReceipt) => {
    setBusy("print");
    setError(null);
    setOutputMessage(null);
    try {
      const printed = await printReceiptAsync(current, bootstrap.device.printerAdapterKey);
      const label = current.copyType === "PROVISIONAL"
        ? "Provisional collection slip"
        : current.copyType === "REPRINT" ? `Reprint copy ${current.copyNumber}` : "Original collection receipt";
      setOutputMessage(`${label} sent to ${printed.adapterLabel}.`);
    } catch (caught) {
      setError(asProblem(caught));
    } finally {
      setBusy(null);
    }
  };

  const shareCurrentReceipt = async (current: MobilePosCollectionReceipt | MobilePosProvisionalCollectionReceipt) => {
    setBusy("share");
    setError(null);
    setOutputMessage(null);
    try {
      await shareReceiptPdfAsync(current);
      setOutputMessage("The collection receipt PDF was saved on this device and opened in the share sheet.");
    } catch (caught) {
      setError(asProblem(caught));
    } finally {
      setBusy(null);
    }
  };

  if (result) {
    return (
      <ScrollView style={styles.page} contentContainerStyle={styles.content}>
        <View style={styles.successIcon}><Ionicons name="checkmark" color={colors.white} size={34} /></View>
        <Text style={styles.successTitle}>Collection recorded</Text>
        <Text style={styles.successAmount}>{money(result.totalAmount, result.currencyCode)}</Text>
        <Text style={styles.successMeta}>{result.customerName} · {result.localReference}</Text>
        {error && <ErrorBox message={`${error.message}${error.correlationId ? ` Reference: ${error.correlationId}` : ""}`} />}
        {receipt ? <CollectionReceiptCard receipt={receipt} /> : (
          <View style={styles.summaryCard}>
            {result.allocations.map(item => <SummaryRow key={item.invoiceId} label={item.invoiceNumber} value={money(item.amount, result.currencyCode)} />)}
            {result.tenders.map(item => <SummaryRow key={item.tenderId} label={item.paymentNumber} value={item.paymentStatus} />)}
          </View>
        )}
        {outputMessage && <View style={styles.outputMessage}><Ionicons name="checkmark-circle-outline" size={19} color={colors.success} /><Text style={styles.outputMessageText}>{outputMessage}</Text></View>}
        {receipt && <View style={styles.outputActions}>
          <Pressable disabled={busy !== null} onPress={() => void printCurrentReceipt(receipt)} style={[styles.outputButton, busy !== null && styles.disabled]}><Ionicons name="print-outline" size={18} color={colors.blue} /><Text style={styles.outputButtonText}>{isZcsSmartPosAdapter(bootstrap.device.printerAdapterKey) ? "Built-in print" : "System print"}</Text></Pressable>
          <Pressable disabled={busy !== null} onPress={() => void shareCurrentReceipt(receipt)} style={[styles.outputButton, busy !== null && styles.disabled]}><Ionicons name="share-social-outline" size={18} color={colors.blue} /><Text style={styles.outputButtonText}>Share PDF</Text></Pressable>
        </View>}
        {receipt && canReprintReceipt && <Pressable disabled={busy !== null} onPress={() => void createReprintCopy()} style={[styles.secondaryButton, busy !== null && styles.disabled]}>{busy === "reprint" ? <ActivityIndicator color={colors.blue} /> : <Text style={styles.secondaryButtonText}>Generate audited reprint copy</Text>}</Pressable>}
        <Pressable onPress={() => router.replace("/customers")} style={styles.primaryButton}><Text style={styles.primaryButtonText}>Back to customers</Text></Pressable>
      </ScrollView>
    );
  }

  if (queuedCollection) {
    return (
      <ScrollView style={styles.page} contentContainerStyle={styles.content}>
        <View style={[styles.successIcon, styles.queuedIcon]}><Ionicons name="cloud-upload-outline" color={colors.white} size={34} /></View>
        <Text style={styles.successTitle}>Collection queued</Text>
        <Text style={styles.successAmount}>{money(queuedCollection.totalAmount, queuedCollection.currencyCode)}</Text>
        <Text style={styles.successMeta}>{customer.name} · {queuedCollection.localReference}</Text>
        <View style={styles.pendingNotice}>
          <Ionicons name="time-outline" size={19} color={colors.warning} />
          <Text style={styles.pendingNoticeText}>
            {queuedCollection.provisionalReceipt
              ? "Provisional offline receipt. Finance payment numbers will be assigned after synchronization."
              : "Pending synchronization. The signed policy does not permit a provisional receipt."}
          </Text>
        </View>
        <View style={styles.summaryCard}>
          {queuedCollection.allocations.map(item => <SummaryRow key={item.invoiceNumber} label={item.invoiceNumber} value={money(item.amount, queuedCollection.currencyCode)} />)}
        </View>
        {queuedCollection.receipt && <>
          <CollectionReceiptCard receipt={queuedCollection.receipt} />
          {error && <ErrorBox message={`${error.message}${error.correlationId ? ` Reference: ${error.correlationId}` : ""}`} />}
          {outputMessage && <View style={styles.outputMessage}><Ionicons name="checkmark-circle-outline" size={19} color={colors.success} /><Text style={styles.outputMessageText}>{outputMessage}</Text></View>}
          <View style={styles.outputActions}>
            <Pressable disabled={busy !== null} onPress={() => void printCurrentReceipt(queuedCollection.receipt!)} style={[styles.outputButton, busy !== null && styles.disabled]}><Ionicons name="print-outline" size={18} color={colors.blue} /><Text style={styles.outputButtonText}>{isZcsSmartPosAdapter(bootstrap.device.printerAdapterKey) ? "Built-in print" : "System print"}</Text></Pressable>
            <Pressable disabled={busy !== null} onPress={() => void shareCurrentReceipt(queuedCollection.receipt!)} style={[styles.outputButton, busy !== null && styles.disabled]}><Ionicons name="share-social-outline" size={18} color={colors.blue} /><Text style={styles.outputButtonText}>Share PDF</Text></Pressable>
          </View>
        </>}
        <Pressable onPress={() => router.replace("/sync")} style={styles.primaryButton}><Text style={styles.primaryButtonText}>Open sync queue</Text></Pressable>
        <Pressable onPress={() => router.replace("/customers")} style={styles.secondaryButton}><Text style={styles.secondaryButtonText}>Back to customers</Text></Pressable>
      </ScrollView>
    );
  }

  return (
    <>
      <ScrollView style={styles.page} contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <View style={styles.header}>
          <Link href="/customers" asChild><Pressable accessibilityLabel="Back" style={styles.backButton}><Ionicons name="arrow-back" size={22} color={colors.navy} /></Pressable></Link>
          <View style={styles.grow}><Text style={styles.title}>Collect customer payment</Text><Text style={styles.subtitle}>{customer.name} · {customer.code}</Text></View>
        </View>
        <View style={styles.onlineNotice}>
          <Ionicons name={usingCachedInvoices ? "cloud-offline-outline" : "cloud-done-outline"} size={18} color={usingCachedInvoices ? colors.warning : colors.blue} />
          <Text style={styles.onlineText}>
            {usingCachedInvoices
              ? `Using balances cached ${formatCacheTime(invoiceCacheTime)}. The server will revalidate them during synchronization.`
              : "Online Finance collection. Current balances are checked again when you submit."}
          </Text>
        </View>
        {missingPermission && <ErrorBox message={`Your role is missing ${missingPermission}.`} />}
        {!bootstrap.currentTillSessionId && <ErrorBox message="Open your assigned till session before collecting a payment." />}
        {error && <ErrorBox message={`${error.message}${error.correlationId ? ` Reference: ${error.correlationId}` : ""}`} />}

        <SectionTitle number="1" title="Invoice allocations" />
        {busy === "load" ? <ActivityIndicator color={colors.blue} /> : invoices.length === 0 ? (
          <View style={styles.emptyCard}><Text style={styles.emptyText}>This customer has no outstanding invoices.</Text></View>
        ) : invoices.map(invoice => (
          <View key={invoice.id} style={styles.invoiceCard}>
            <View style={styles.rowBetween}><Text style={styles.invoiceNumber}>{invoice.invoiceNumber}</Text><Text style={styles.balance}>{money(invoice.balanceAmount, invoice.currencyCode)}</Text></View>
            <Text style={styles.meta}>Due {invoice.dueDate ? date(invoice.dueDate) : "not set"}{invoice.daysOverdue > 0 ? ` · ${invoice.daysOverdue} day(s) overdue` : ""}</Text>
            <TextInput accessibilityLabel={`Amount for ${invoice.invoiceNumber}`} keyboardType="decimal-pad" onChangeText={value => updateAllocation(invoice.id, value)} placeholder="Collection amount" placeholderTextColor={colors.muted} style={styles.input} value={allocations.find(item => item.invoiceId === invoice.id)?.amountText ?? ""} />
          </View>
        ))}
        <View style={styles.totalRow}><Text style={styles.totalLabel}>Allocation total</Text><Text style={styles.totalValue}>{money(allocationTotal, bootstrap.store.currencyCode)}</Text></View>

        <SectionTitle number="2" title="Split tender" />
        {tenders.map(tender => {
          const method = paymentMethods.find(item => item.paymentMethodId === tender.paymentMethodId);
          if (!method) return null;
          const selectedAccount = bankAccounts.find(account => account.bankAccountId === tender.bankAccountId);
          return (
            <View key={tender.paymentMethodId} style={styles.tenderCard}>
              <View style={styles.rowBetween}><Text style={styles.invoiceNumber}>{method.name}</Text>{tenders.length > 1 && <Pressable onPress={() => { setTenders(current => current.filter(item => item.paymentMethodId !== method.paymentMethodId)); setIdentity(null); }}><Text style={styles.removeText}>Remove</Text></Pressable>}</View>
              <TextInput accessibilityLabel={`${method.name} amount`} keyboardType="decimal-pad" onChangeText={value => updateTender(method.paymentMethodId, { amountText: value })} placeholder="Amount" placeholderTextColor={colors.muted} style={styles.input} value={tender.amountText} />
              {(method.requiresReference || method.requireExternalAuthorizationReference) && <TextInput accessibilityLabel={`${method.name} reference`} autoCapitalize="characters" onChangeText={value => updateTender(method.paymentMethodId, { externalReference: value })} placeholder="Provider or transaction reference" placeholderTextColor={colors.muted} style={styles.input} value={tender.externalReference} />}
              {method.requiresBankAccount && <Pressable onPress={() => void openBankAccountPicker(method.paymentMethodId)} style={styles.bankSelector}><Ionicons name="business-outline" size={18} color={selectedAccount ? colors.success : colors.blue} /><View style={styles.grow}><Text style={styles.bankTitle}>{selectedAccount?.accountName ?? "Select bank account"}</Text><Text style={styles.meta}>{selectedAccount ? `${selectedAccount.bankName} · ${selectedAccount.maskedAccountNumber}` : "Required for this payment method"}</Text></View><Ionicons name="chevron-forward" size={18} color={colors.muted} /></Pressable>}
            </View>
          );
        })}
        <View style={styles.methodWrap}>{paymentMethods.filter(method => !tenders.some(item => item.paymentMethodId === method.paymentMethodId)).map(method => <Pressable key={method.paymentMethodId} onPress={() => addTender(method)} style={styles.methodButton}><Ionicons name="add" size={16} color={colors.blue} /><Text style={styles.methodText}>{method.name}</Text></Pressable>)}</View>
        <View style={styles.totalRow}><Text style={styles.totalLabel}>Tender total</Text><Text style={[styles.totalValue, !totalsMatch && styles.mismatch]}>{money(tenderTotal, bootstrap.store.currencyCode)}</Text></View>
        <Pressable disabled={submitDisabled} onPress={() => void complete()} style={[styles.primaryButton, submitDisabled && styles.disabled]}>{busy === "complete" ? <ActivityIndicator color={colors.white} /> : <Text style={styles.primaryButtonText}>Record collection</Text>}</Pressable>
      </ScrollView>
      <BankAccountPickerModal visible={bankAccountTenderId !== null} accounts={bankAccounts} selectedId={tenders.find(item => item.paymentMethodId === bankAccountTenderId)?.bankAccountId} onClose={() => setBankAccountTenderId(null)} onSelect={account => { if (bankAccountTenderId) updateTender(bankAccountTenderId, { bankAccountId: account.bankAccountId }); setBankAccountTenderId(null); }} />
    </>
  );
}

function createIdentity() {
  const id = Crypto.randomUUID();
  return { clientMutationId: id, localReference: `COL-${id.slice(0, 8).toUpperCase()}`, occurredAtUtc: new Date().toISOString() };
}
function problem(message: string) { return new ApiProblem(message, 400, "COLLECTION_VALIDATION"); }
function asProblem(error: unknown) { return error instanceof ApiProblem ? error : problem(error instanceof Error ? error.message : "An unexpected error occurred."); }
function money(value: number, currency: string) { return `${currency} ${value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`; }
function date(value: string) { const parsed = new Date(value); return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleDateString(); }
function formatCacheTime(value: string | null) { if (!value) return "earlier"; const parsed = new Date(value); return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString(); }
function ErrorBox({ message }: { message: string }) { return <View accessibilityRole="alert" style={styles.errorBox}><Ionicons name="alert-circle-outline" size={20} color={colors.danger} /><Text style={styles.errorText}>{message}</Text></View>; }
function SectionTitle({ number, title }: { number: string; title: string }) { return <View style={styles.sectionHeading}><View style={styles.numberBadge}><Text style={styles.numberText}>{number}</Text></View><Text style={styles.sectionTitle}>{title}</Text></View>; }
function SummaryRow({ label, value }: { label: string; value: string }) { return <View style={styles.summaryRow}><Text style={styles.meta}>{label}</Text><Text style={styles.summaryValue}>{value}</Text></View>; }
function CollectionReceiptCard({ receipt }: { receipt: MobilePosCollectionReceipt | MobilePosProvisionalCollectionReceipt }) {
  return <View style={styles.receiptCard}>
    {receipt.copyType === "PROVISIONAL" && <Text style={styles.provisionalMark}>PROVISIONAL · PENDING SYNCHRONIZATION</Text>}
    {receipt.copyType === "REPRINT" && <Text style={styles.reprintMark}>REPRINT · COPY {receipt.copyNumber}</Text>}
    <Text style={styles.receiptTenant}>{receipt.tenantName || "RHEMA ERP"}</Text>
    <Text style={styles.receiptMeta}>CUSTOMER COLLECTION RECEIPT</Text>
    <Text style={styles.receiptMeta}>{receipt.storeName} · {receipt.tillNumber}</Text>
    <View style={styles.receiptDivider} />
    <SummaryRow label="Customer" value={`${receipt.customerName} (${receipt.customerCode})`} />
    <SummaryRow label="Cashier" value={receipt.cashierName} />
    <SummaryRow label="Reference" value={receipt.localReference} />
    <View style={styles.receiptDivider} />
    {receipt.allocations.map(item => <SummaryRow key={`${item.sequence}-${item.invoiceId}`} label={item.invoiceNumber} value={money(item.amount, receipt.currencyCode)} />)}
    <View style={styles.totalRow}><Text style={styles.totalLabel}>Total</Text><Text style={styles.totalValue}>{money(receipt.totalAmount, receipt.currencyCode)}</Text></View>
    <View style={styles.receiptDivider} />
    {receipt.tenders.map(item => <SummaryRow key={item.sequence} label={item.paymentMethodName} value={"paymentNumber" in item ? item.paymentNumber : "Pending server number"} />)}
    <Text selectable style={styles.qrReference}>{receipt.qrReference}</Text>
    {receipt.copyType === "PROVISIONAL" && <Text style={styles.receiptNote}>This is not a final Finance receipt. Canonical payment numbers are assigned only after successful synchronization.</Text>}
  </View>;
}

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: colors.background }, content: { paddingHorizontal: 20, paddingTop: 52, paddingBottom: 40 },
  header: { flexDirection: "row", alignItems: "center", gap: 13 }, grow: { flex: 1 }, backButton: { width: 42, height: 42, borderRadius: 13, alignItems: "center", justifyContent: "center", backgroundColor: colors.white, borderWidth: 1, borderColor: colors.line },
  title: { color: colors.ink, fontSize: 21, fontWeight: "800" }, subtitle: { marginTop: 3, color: colors.muted, fontSize: 11 },
  onlineNotice: { marginTop: 18, flexDirection: "row", alignItems: "center", gap: 9, padding: 12, borderRadius: 12, backgroundColor: colors.paleBlue }, onlineText: { flex: 1, color: colors.slate, fontSize: 11, lineHeight: 16 },
  errorBox: { marginTop: 12, flexDirection: "row", gap: 9, padding: 13, borderRadius: 11, backgroundColor: colors.dangerBg }, errorText: { flex: 1, color: colors.danger, fontSize: 12, lineHeight: 18 },
  sectionHeading: { marginTop: 24, marginBottom: 10, flexDirection: "row", alignItems: "center", gap: 9 }, numberBadge: { width: 25, height: 25, borderRadius: 8, alignItems: "center", justifyContent: "center", backgroundColor: colors.paleBlue }, numberText: { color: colors.blue, fontSize: 11, fontWeight: "800" }, sectionTitle: { color: colors.ink, fontSize: 15, fontWeight: "800" },
  invoiceCard: { marginBottom: 9, padding: 14, borderRadius: 14, backgroundColor: colors.white, borderWidth: 1, borderColor: colors.line }, rowBetween: { flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: 10 }, invoiceNumber: { color: colors.ink, fontSize: 13, fontWeight: "800" }, balance: { color: colors.navy, fontSize: 14, fontWeight: "800" }, meta: { marginTop: 4, color: colors.muted, fontSize: 11, lineHeight: 16 },
  input: { minHeight: 46, marginTop: 10, paddingHorizontal: 13, borderRadius: 11, borderWidth: 1, borderColor: colors.line, backgroundColor: "#FCFCFD", color: colors.ink, fontSize: 14 },
  totalRow: { marginTop: 7, padding: 14, flexDirection: "row", alignItems: "center", justifyContent: "space-between", borderRadius: 13, backgroundColor: colors.navy }, totalLabel: { color: "#D0D5DD", fontSize: 12, fontWeight: "700" }, totalValue: { color: colors.white, fontSize: 18, fontWeight: "800" }, mismatch: { color: "#FEC84B" },
  tenderCard: { marginBottom: 9, padding: 14, borderRadius: 14, backgroundColor: colors.white, borderWidth: 1, borderColor: colors.line }, removeText: { color: colors.danger, fontSize: 11, fontWeight: "700" }, bankSelector: { minHeight: 54, marginTop: 10, paddingHorizontal: 12, flexDirection: "row", alignItems: "center", gap: 10, borderRadius: 11, borderWidth: 1, borderColor: colors.line, backgroundColor: colors.paleBlue }, bankTitle: { color: colors.ink, fontSize: 12, fontWeight: "700" },
  methodWrap: { flexDirection: "row", flexWrap: "wrap", gap: 8 }, methodButton: { minHeight: 38, paddingHorizontal: 12, flexDirection: "row", alignItems: "center", gap: 5, borderRadius: 10, borderWidth: 1, borderColor: "#84ADFF", backgroundColor: colors.white }, methodText: { color: colors.blue, fontSize: 11, fontWeight: "700" },
  primaryButton: { minHeight: 50, marginTop: 18, alignItems: "center", justifyContent: "center", borderRadius: 13, backgroundColor: colors.blue }, primaryButtonText: { color: colors.white, fontSize: 14, fontWeight: "800" }, disabled: { opacity: 0.45 },
  emptyCard: { padding: 20, borderRadius: 14, backgroundColor: colors.white }, emptyText: { color: colors.muted, textAlign: "center", fontSize: 13 },
  successIcon: { width: 68, height: 68, marginTop: 50, alignSelf: "center", alignItems: "center", justifyContent: "center", borderRadius: 23, backgroundColor: colors.success }, successTitle: { marginTop: 18, color: colors.ink, textAlign: "center", fontSize: 23, fontWeight: "800" }, successAmount: { marginTop: 8, color: colors.navy, textAlign: "center", fontSize: 29, fontWeight: "900" }, successMeta: { marginTop: 7, color: colors.muted, textAlign: "center", fontSize: 12 }, summaryCard: { marginTop: 22, padding: 15, borderRadius: 15, backgroundColor: colors.white, borderWidth: 1, borderColor: colors.line }, summaryRow: { minHeight: 38, flexDirection: "row", alignItems: "center", justifyContent: "space-between" }, summaryValue: { color: colors.ink, fontSize: 12, fontWeight: "700" },
  queuedIcon: { backgroundColor: colors.warning }, pendingNotice: { marginTop: 18, flexDirection: "row", gap: 9, padding: 13, borderRadius: 12, backgroundColor: colors.warningBg }, pendingNoticeText: { flex: 1, color: colors.slate, fontSize: 12, lineHeight: 18 },
  secondaryButton: { minHeight: 48, marginTop: 10, alignItems: "center", justifyContent: "center", borderRadius: 13, borderWidth: 1, borderColor: colors.line, backgroundColor: colors.white }, secondaryButtonText: { color: colors.navy, fontSize: 14, fontWeight: "800" },
  receiptCard: { marginTop: 18, padding: 18, borderRadius: 18, borderWidth: 1, borderColor: colors.line, backgroundColor: colors.white }, receiptTenant: { color: colors.navy, fontSize: 18, fontWeight: "800", textAlign: "center" }, receiptMeta: { marginTop: 3, color: colors.slate, fontSize: 11, textAlign: "center" }, receiptDivider: { height: 1, marginVertical: 14, backgroundColor: colors.line }, receiptNote: { marginTop: 14, color: colors.slate, fontSize: 11, lineHeight: 17, textAlign: "center" }, qrReference: { marginTop: 16, color: colors.muted, fontSize: 9, lineHeight: 13, textAlign: "center" }, reprintMark: { marginBottom: 12, color: colors.danger, fontSize: 13, fontWeight: "800", letterSpacing: 1.5, textAlign: "center" }, provisionalMark: { marginBottom: 12, padding: 8, borderWidth: 2, borderColor: colors.warning, color: colors.warning, fontSize: 11, fontWeight: "800", letterSpacing: 1, textAlign: "center" },
  outputActions: { marginTop: 14, flexDirection: "row", gap: 10 }, outputButton: { minHeight: 48, flex: 1, flexDirection: "row", alignItems: "center", justifyContent: "center", gap: 7, borderRadius: 12, borderWidth: 1, borderColor: "#84ADFF", backgroundColor: colors.white }, outputButtonText: { color: colors.blue, fontSize: 12, fontWeight: "800" }, outputMessage: { marginTop: 12, flexDirection: "row", gap: 8, padding: 12, borderRadius: 12, backgroundColor: colors.successBg }, outputMessageText: { flex: 1, color: colors.success, fontSize: 11, lineHeight: 16 },
});
