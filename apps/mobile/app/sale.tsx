import { Ionicons } from "@expo/vector-icons";
import * as Crypto from "expo-crypto";
import { Link, Redirect } from "expo-router";
import { useEffect, useMemo, useRef, useState, type RefObject } from "react";
import {
  ActivityIndicator,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from "react-native";
import { ApiProblem, mobileApi } from "@/src/api/client";
import { BarcodeScannerModal } from "@/components/barcode-scanner-modal";
import { BankAccountPickerModal } from "@/components/bank-account-picker-modal";
import { printReceiptAsync, shareReceiptPdfAsync } from "@/src/receipts/output";
import { completeWithCanonicalReceipt } from "@/src/receipts/completion";
import { searchSessionCatalogue, searchSessionCustomers } from "@/src/offline/catalogue-runtime";
import { cacheSessionReceipt } from "@/src/offline/receipt-runtime";
import { openSessionOutbox } from "@/src/offline/sync-runtime";
import { isZcsSmartPosAdapter, ZcsHardwareScannerAdapter } from "@/src/hardware/zcs-smartpos";
import type { BarcodeScan } from "@/src/scanning/barcode";
import { buildCompleteSaleRequest, sumTenderDrafts, type TenderDraft } from "@/src/sales/checkout";
import { useSession } from "@/src/session/session-context";
import { getInstallationId } from "@/src/storage/secure-session";
import type {
  MobilePosCatalogueItem,
  MobilePosBankAccountOption,
  MobilePosCustomerSearchResult,
  MobilePosPaymentMethod,
  MobilePosProvisionalSaleReceipt,
  MobilePosReceipt,
  MobilePosSalePreview,
  MobilePosSaleResult,
} from "@/src/types/api";
import { colors } from "@/src/ui/theme";

interface CartLine {
  clientLineId: string;
  item: MobilePosCatalogueItem;
  quantity: number;
  discountPercentage: number;
}

interface PendingIdentity {
  clientMutationId: string;
  localReference: string;
  occurredAtUtc: string;
}

const previewPermissions = [
  "MobilePOS.Till.Operate",
  "MobilePOS.Invoice.Create",
  "Finance.AR.Invoices.Create",
];
const completionPermissions = [
  ...previewPermissions,
  "MobilePOS.Invoice.Post",
  "MobilePOS.Payment.Collect",
  "Finance.AR.Invoices.ApprovePost",
  "Finance.AR.Payments.Receive",
];

export default function SaleScreen() {
  const session = useSession();
  const bootstrap = session.bootstrap;
  const defaultCustomer = useMemo<MobilePosCustomerSearchResult | null>(() => bootstrap ? ({
    businessPartnerId: bootstrap.store.defaultWalkInBusinessPartnerId,
    businessPartnerRoleId: bootstrap.store.defaultWalkInBusinessPartnerRoleId,
    code: bootstrap.store.defaultWalkInCustomerCode,
    name: bootstrap.store.defaultWalkInCustomerName,
    currencyCode: bootstrap.store.currencyCode,
    isDefaultWalkInCustomer: true,
  }) : null, [bootstrap]);
  const permissions = session.user?.permissions ?? [];
  const missingPreviewPermission = previewPermissions.find(permission => !permissions.includes(permission));
  const missingCompletionPermission = completionPermissions.find(permission => !permissions.includes(permission));
  const canSearchCustomers = permissions.includes("MobilePOS.Customer.View");
  const canApplyDiscount = permissions.includes("MobilePOS.Discount.Apply");
  const canReprintReceipt = permissions.includes("MobilePOS.Receipt.Reprint");

  const [catalogueQuery, setCatalogueQuery] = useState("");
  const [catalogueResults, setCatalogueResults] = useState<MobilePosCatalogueItem[]>([]);
  const [scannerOpen, setScannerOpen] = useState(false);
  const [customerQuery, setCustomerQuery] = useState("");
  const [customerResults, setCustomerResults] = useState<MobilePosCustomerSearchResult[]>([]);
  const [customer, setCustomer] = useState<MobilePosCustomerSearchResult | null>(defaultCustomer);
  const [cart, setCart] = useState<CartLine[]>([]);
  const [preview, setPreview] = useState<MobilePosSalePreview | null>(null);
  const [tenders, setTenders] = useState<TenderDraft[]>([]);
  const [bankAccounts, setBankAccounts] = useState<MobilePosBankAccountOption[]>([]);
  const [bankAccountTenderId, setBankAccountTenderId] = useState<string | null>(null);
  const [pendingIdentity, setPendingIdentity] = useState<PendingIdentity | null>(null);
  const [result, setResult] = useState<MobilePosSaleResult | null>(null);
  const [receipt, setReceipt] = useState<MobilePosReceipt | null>(null);
  const [queuedSale, setQueuedSale] = useState<{
    localReference: string;
    total: number;
    currencyCode: string;
    provisionalReceipt: boolean;
    receipt?: MobilePosProvisionalSaleReceipt;
  } | null>(null);
  const [busy, setBusy] = useState<"catalogue" | "customer" | "bankAccounts" | "preview" | "complete" | "reprint" | "print" | "share" | null>(null);
  const [error, setError] = useState<ApiProblem | null>(null);
  const [outputMessage, setOutputMessage] = useState<string | null>(null);
  const [catalogueNotice, setCatalogueNotice] = useState<string | null>(null);
  const [customerNotice, setCustomerNotice] = useState<string | null>(null);
  const [hardwareScanning, setHardwareScanning] = useState(false);
  const catalogueInputRef = useRef<TextInput>(null);
  const zcsScanner = useMemo(() => new ZcsHardwareScannerAdapter(), []);
  const usesZcsScanner = isZcsSmartPosAdapter(bootstrap?.device.scannerAdapterKey);

  const onlineMethods = useMemo(
    () => bootstrap?.till.paymentMethods.filter(method => method.allowOnline) ?? [],
    [bootstrap],
  );

  useEffect(() => {
    setCustomer(defaultCustomer);
  }, [defaultCustomer]);

  useEffect(() => () => { void zcsScanner.stop(); }, [zcsScanner]);

  if (session.status === "signedOut" || session.status === "mfaRequired") return <Redirect href="/login" />;
  if (session.status !== "ready" || !bootstrap) return <Redirect href="/" />;

  const invalidatePreview = () => {
    setPreview(null);
    setTenders([]);
    setPendingIdentity(null);
    setResult(null);
    setError(null);
  };

  const runCatalogueSearch = async (rawTerm: string) => {
    const term = rawTerm.trim();
    if (!term) return setError(problem("Enter an item name, code, or barcode."));
    setBusy("catalogue");
    setError(null);
    setCatalogueNotice(null);
    try {
      setCatalogueResults(await mobileApi.searchCatalogue(await getInstallationId(), term));
    } catch (caught) {
      const problem = asProblem(caught);
      if (problem.status > 0 || !session.user) {
        setError(problem);
      } else {
        try {
          const cached = await searchSessionCatalogue(session.user, bootstrap, term);
          setCatalogueResults(cached);
          setCatalogueNotice(cached.length > 0
            ? "Network unavailable. Showing the latest catalogue saved on this device."
            : "Network unavailable and no saved catalogue item matched the search.");
        } catch (cacheError) {
          setError(asProblem(cacheError));
        }
      }
    } finally {
      setBusy(null);
    }
  };

  const acceptBarcodeScan = (scan: BarcodeScan) => {
    setScannerOpen(false);
    setHardwareScanning(false);
    setCatalogueQuery(scan.value);
    void runCatalogueSearch(scan.value);
  };

  const triggerHardwareScanner = async () => {
    setError(null);
    setCatalogueQuery("");
    try {
      await zcsScanner.start(acceptBarcodeScan);
      setHardwareScanning(true);
      catalogueInputRef.current?.focus();
      await zcsScanner.trigger();
    } catch (caught) {
      setHardwareScanning(false);
      await zcsScanner.stop();
      setError(asProblem(caught));
    }
  };

  const cancelHardwareScanner = async () => {
    setHardwareScanning(false);
    try {
      await zcsScanner.stop();
    } catch (caught) {
      setError(asProblem(caught));
    }
  };

  const submitCatalogueSearch = (rawValue: string) => {
    if (!hardwareScanning) {
      void runCatalogueSearch(rawValue);
      return;
    }
    try {
      zcsScanner.acceptKeyboardWedge(rawValue);
    } catch (caught) {
      setError(asProblem(caught));
    } finally {
      setHardwareScanning(false);
      void zcsScanner.stop();
    }
  };

  const searchCustomers = async () => {
    const term = customerQuery.trim();
    if (term.length < 2) return setError(problem("Enter at least two characters to search approved customers."));
    setBusy("customer");
    setError(null);
    setCustomerNotice(null);
    try {
      setCustomerResults(await mobileApi.searchCustomers(await getInstallationId(), term));
    } catch (caught) {
      const problem = asProblem(caught);
      if (problem.status > 0 || !session.user) {
        setError(problem);
      } else {
        try {
          const cached = await searchSessionCustomers(session.user, bootstrap, term);
          setCustomerResults(cached);
          setCustomerNotice(cached.length > 0
            ? "Network unavailable. Showing approved customers saved on this device."
            : "Network unavailable and no saved approved customer matched the search.");
        } catch (cacheError) {
          setError(asProblem(cacheError));
        }
      }
    } finally {
      setBusy(null);
    }
  };

  const chooseCustomer = (selected: MobilePosCustomerSearchResult) => {
    setCustomer(selected);
    setCustomerResults([]);
    setCustomerQuery("");
    invalidatePreview();
  };

  const addItem = (item: MobilePosCatalogueItem) => {
    setCart(current => {
      const existing = current.find(line => line.item.inventoryItemId === item.inventoryItemId);
      if (existing) {
        return current.map(line => line === existing
          ? { ...line, quantity: allowedQuantity(line.quantity + 1, item) }
          : line);
      }
      return [...current, {
        clientLineId: Crypto.randomUUID(),
        item,
        quantity: 1,
        discountPercentage: 0,
      }];
    });
    setCatalogueResults([]);
    setCatalogueQuery("");
    invalidatePreview();
  };

  const changeQuantity = (clientLineId: string, delta: number) => {
    setCart(current => current
      .map(line => line.clientLineId === clientLineId
        ? { ...line, quantity: allowedQuantity(line.quantity + delta, line.item) }
        : line)
      .filter(line => line.quantity > 0));
    invalidatePreview();
  };

  const removeLine = (clientLineId: string) => {
    setCart(current => current.filter(line => line.clientLineId !== clientLineId));
    invalidatePreview();
  };

  const changeDiscount = (clientLineId: string, rawValue: string) => {
    const parsed = Number(rawValue);
    const discountPercentage = Number.isFinite(parsed) ? Math.min(100, Math.max(0, parsed)) : 0;
    setCart(current => current.map(line => line.clientLineId === clientLineId
      ? { ...line, discountPercentage }
      : line));
    invalidatePreview();
  };

  const calculatePreview = async () => {
    if (missingPreviewPermission) return setError(problem(`Your role is missing ${missingPreviewPermission}.`));
    if (!bootstrap.currentTillSessionId) return setError(problem("Open your assigned cashier till session before starting a sale."));
    if (!customer) return setError(problem("Select a customer for this sale."));
    if (cart.length === 0) return setError(problem("Add at least one item to the sale."));
    setBusy("preview");
    setError(null);
    try {
      const calculated = await mobileApi.previewSale({
        installationId: await getInstallationId(),
        businessPartnerId: customer.businessPartnerId,
        businessPartnerRoleId: customer.businessPartnerRoleId,
        lines: cart.map(line => ({
          clientLineId: line.clientLineId,
          inventoryItemId: line.item.inventoryItemId,
          quantity: line.quantity,
          discountPercentage: line.discountPercentage,
        })),
      });
      setPreview(calculated);
      setPendingIdentity(null);
      const firstMethod = onlineMethods.find(method => !method.requiresBankAccount) ?? onlineMethods[0];
      setTenders(firstMethod ? [{
        paymentMethodId: firstMethod.paymentMethodId,
        amountText: calculated.totalAmount.toFixed(calculated.currencyDecimalPlaces),
        externalReference: "",
      }] : []);
    } catch (caught) {
      setError(asProblem(caught));
    } finally {
      setBusy(null);
    }
  };

  const addTender = (method: MobilePosPaymentMethod) => {
    if (tenders.some(tender => tender.paymentMethodId === method.paymentMethodId)) return;
    setTenders(current => [...current, { paymentMethodId: method.paymentMethodId, amountText: "", externalReference: "" }]);
    setPendingIdentity(null);
  };

  const updateTender = (paymentMethodId: string, patch: Partial<TenderDraft>) => {
    setTenders(current => current.map(tender => tender.paymentMethodId === paymentMethodId ? { ...tender, ...patch } : tender));
    setPendingIdentity(null);
  };

  const removeTender = (paymentMethodId: string) => {
    setTenders(current => current.filter(tender => tender.paymentMethodId !== paymentMethodId));
    setPendingIdentity(null);
  };

  const openBankAccountPicker = async (paymentMethodId: string) => {
    setBusy("bankAccounts");
    setError(null);
    try {
      const accounts = bankAccounts.length > 0
        ? bankAccounts
        : await mobileApi.getEligibleBankAccounts(await getInstallationId());
      setBankAccounts(accounts);
      setBankAccountTenderId(paymentMethodId);
    } catch (caught) {
      setError(asProblem(caught));
    } finally {
      setBusy(null);
    }
  };

  const completeSale = async () => {
    if (!preview) return;
    if (missingCompletionPermission) return setError(problem(`Your role is missing ${missingCompletionPermission}.`));
    if (tenders.length === 0) return setError(problem("Select at least one tender."));
    for (const tender of tenders) {
      const method = onlineMethods.find(item => item.paymentMethodId === tender.paymentMethodId);
      if (!method) return setError(problem("A selected tender is no longer available for this till."));
      if (method.requiresBankAccount && !tender.bankAccountId) return setError(problem(`${method.name} requires a bank account selection.`));
      if ((method.requiresReference || method.requireExternalAuthorizationReference) && !tender.externalReference.trim()) {
        return setError(problem(`${method.name} requires the provider or transaction reference.`));
      }
    }
    const tenderTotal = sumTenderDrafts(tenders, preview.currencyDecimalPlaces);
    const tolerance = 1 / (10 ** preview.currencyDecimalPlaces);
    if (Math.abs(tenderTotal - preview.totalAmount) >= tolerance) {
      return setError(problem(`Tender total must equal ${money(preview.totalAmount, preview.currencyCode, preview.currencyDecimalPlaces)}.`));
    }

    const identity = pendingIdentity ?? createIdentity();
    setPendingIdentity(identity);
    setBusy("complete");
    setError(null);
    const request = buildCompleteSaleRequest({
      installationId: await getInstallationId(),
      ...identity,
      preview,
      tenders,
    });
    try {
      const completed = await completeWithCanonicalReceipt({
        complete: () => mobileApi.completeSale(request),
        loadReceipt: result => mobileApi.getReceipt(result.saleId, request.installationId),
        persistReceipt: current => cacheSessionReceipt(session.user!, bootstrap, current),
      });
      setResult(completed.result);
      if (completed.receipt) setReceipt(completed.receipt);
      if (completed.outputError) setError(receiptOutputProblem("sale", completed.outputStage!, completed.outputError));
    } catch (caught) {
      const failure = asProblem(caught);
      const grant = session.offlineGrant;
      const retryable = failure.status === 0 || failure.status === 408 || failure.status === 429 || failure.status >= 500;
      const allowedTenderIds = new Set(grant?.policy.allowedPaymentMethods.map(method => method.paymentMethodId) ?? []);
      const offlineTenderAllowed = tenders.every(tender => {
        const method = bootstrap.till.paymentMethods.find(item => item.paymentMethodId === tender.paymentMethodId);
        return Boolean(method?.allowOffline && allowedTenderIds.has(tender.paymentMethodId));
      });
      const discountAllowed = cart.every(line => line.discountPercentage === 0) || grant?.policy.allowDiscounts === true;
      const offlineAmountAllowed = grant?.policy.maximumTransactionAmount == null
        || preview.totalAmount <= grant.policy.maximumTransactionAmount;
      if (retryable && grant && session.user && bootstrap.currentTillSessionId
        && grant.policy.allowedCommandTypes.includes("CashSale")
        && offlineTenderAllowed && discountAllowed && offlineAmountAllowed) {
        try {
          await (await openSessionOutbox(session.user, bootstrap)).enqueue({
            clientMutationId: identity.clientMutationId,
            localReference: identity.localReference,
            commandType: "CashSale",
            schemaVersion: 1,
            tillSessionId: bootstrap.currentTillSessionId,
            offlineGrantId: grant.id,
            payload: request,
          });
          const provisionalReceipt = grant.policy.allowProvisionalReceipt ? {
            receiptKind: "SALE_PROVISIONAL" as const,
            receiptId: identity.clientMutationId,
            copyType: "PROVISIONAL" as const,
            copyNumber: 0 as const,
            reprintCount: 0 as const,
            generatedAtUtc: new Date().toISOString(),
            qrReference: `RHEMA|MOBILEPOS|PROVISIONAL|${identity.localReference}|${identity.clientMutationId}`,
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
            businessDate: identity.occurredAtUtc.slice(0, 10),
            deviceId: bootstrap.device.id,
            deviceName: bootstrap.device.deviceName,
            cashierUserId: session.user.id,
            cashierName: bootstrap.userName,
            businessPartnerId: preview.businessPartnerId,
            businessPartnerRoleId: preview.businessPartnerRoleId,
            customerCode: preview.customerCode,
            customerName: preview.customerName,
            usedStoreDefaultCustomer: preview.usedStoreDefaultCustomer,
            localReference: identity.localReference,
            occurredAtUtc: identity.occurredAtUtc,
            currencyCode: preview.currencyCode,
            subTotal: preview.subTotal,
            taxAmount: preview.taxAmount,
            discountAmount: preview.discountAmount,
            totalAmount: preview.totalAmount,
            lines: preview.lines.map((line, index) => ({
              sequence: index + 1,
              description: line.description,
              quantity: line.quantity,
              unitPrice: line.unitPrice,
              discountAmount: line.discountAmount,
              taxAmount: line.taxAmount,
              lineTotal: line.lineTotal,
              unitOfMeasureCode: line.unitOfMeasureCode,
            })),
            tenders: request.tenders.map((tender, index) => {
              const method = onlineMethods.find(item => item.paymentMethodId === tender.paymentMethodId);
              return {
                sequence: index + 1,
                paymentMethodCode: method?.code ?? tender.paymentMethodId,
                paymentMethodName: method?.name ?? "Tender",
                amount: tender.amount,
                externalReference: tender.externalReference,
              };
            }),
          } satisfies MobilePosProvisionalSaleReceipt : undefined;
          setQueuedSale({
            localReference: identity.localReference,
            total: preview.totalAmount,
            currencyCode: preview.currencyCode,
            provisionalReceipt: grant.policy.allowProvisionalReceipt,
            receipt: provisionalReceipt,
          });
          setError(null);
        } catch (queueError) {
          setError(asProblem(queueError));
        }
      } else if (retryable && grant && !offlineAmountAllowed) {
        setError(problem("This sale exceeds the signed offline per-transaction limit."));
      } else {
        setError(failure);
      }
    } finally {
      setBusy(null);
    }
  };

  const startNewSale = () => {
    setCart([]);
    setPreview(null);
    setTenders([]);
    setPendingIdentity(null);
    setResult(null);
    setReceipt(null);
    setQueuedSale(null);
    setError(null);
    setOutputMessage(null);
    setCustomer(defaultCustomer);
  };

  const createReprintCopy = async () => {
    if (!result || !canReprintReceipt) return;
    setBusy("reprint");
    setError(null);
    setOutputMessage(null);
    try {
      const reprint = await mobileApi.recordReceiptReprint(result.saleId, {
        installationId: await getInstallationId(),
        clientEventId: Crypto.randomUUID(),
        reason: "Cashier requested another receipt copy",
      });
      setReceipt(reprint);
      try {
        await cacheSessionReceipt(session.user!, bootstrap, reprint);
      } catch (cacheError) {
        setError(receiptOutputProblem("sale reprint", "persist", cacheError));
      }
    } catch (caught) {
      setError(asProblem(caught));
    } finally {
      setBusy(null);
    }
  };

  const printCurrentReceipt = async (current: MobilePosReceipt | MobilePosProvisionalSaleReceipt) => {
    setBusy("print");
    setError(null);
    setOutputMessage(null);
    try {
      const printResult = await printReceiptAsync(current, bootstrap.device.printerAdapterKey);
      const label = current.copyType === "PROVISIONAL"
        ? "Provisional sale slip"
        : current.copyType === "REPRINT" ? `Reprint copy ${current.copyNumber}` : "Original receipt";
      setOutputMessage(`${label} sent to ${printResult.adapterLabel}.`);
    } catch (caught) {
      setError(asProblem(caught));
    } finally {
      setBusy(null);
    }
  };

  const shareCurrentReceipt = async (current: MobilePosReceipt | MobilePosProvisionalSaleReceipt) => {
    setBusy("share");
    setError(null);
    setOutputMessage(null);
    try {
      await shareReceiptPdfAsync(current);
      setOutputMessage("The receipt PDF was saved on this device and opened in the share sheet.");
    } catch (caught) {
      setError(asProblem(caught));
    } finally {
      setBusy(null);
    }
  };

  if (queuedSale) {
    return (
      <ScrollView style={styles.page} contentContainerStyle={styles.content}>
        <View style={styles.successIcon}><Ionicons name="cloud-offline-outline" size={31} color={colors.white} /></View>
        <Text style={styles.successTitle}>Sale saved securely</Text>
        <Text style={styles.successNumber}>{queuedSale.localReference}</Text>
        <Text style={styles.successAmount}>{money(queuedSale.total, queuedSale.currencyCode, preview?.currencyDecimalPlaces ?? 2)}</Text>
        <View style={styles.card}>
          <Text style={styles.sectionTitle}>Pending synchronization</Text>
          <Text style={styles.meta}>The same sale identity and tender evidence are retained in the encrypted device workflow. RHEMA will create the final invoice, payments, and canonical receipt after server validation.</Text>
          <Text style={styles.receiptNote}>{queuedSale.provisionalReceipt ? "The signed policy permits a clearly marked provisional sale slip." : "The signed policy does not permit a provisional receipt."}</Text>
        </View>
        {queuedSale.receipt && <>
          <SaleReceiptCard receipt={queuedSale.receipt} />
          {error && <ErrorBox message={`${error.message}${error.correlationId ? ` Reference: ${error.correlationId}` : ""}`} />}
          {outputMessage && <View style={styles.outputMessage}><Ionicons name="checkmark-circle-outline" size={19} color={colors.success} /><Text style={styles.outputMessageText}>{outputMessage}</Text></View>}
          <View style={styles.outputActions}>
            <Pressable accessibilityRole="button" disabled={busy !== null} onPress={() => void printCurrentReceipt(queuedSale.receipt!)} style={[styles.outputButton, busy !== null && styles.disabled]}><Ionicons name="print-outline" size={18} color={colors.blue} /><Text style={styles.outputButtonText}>{isZcsSmartPosAdapter(bootstrap.device.printerAdapterKey) ? "Built-in print" : "System print"}</Text></Pressable>
            <Pressable accessibilityRole="button" disabled={busy !== null} onPress={() => void shareCurrentReceipt(queuedSale.receipt!)} style={[styles.outputButton, busy !== null && styles.disabled]}><Ionicons name="share-social-outline" size={18} color={colors.blue} /><Text style={styles.outputButtonText}>Share PDF</Text></Pressable>
          </View>
        </>}
        <Link href="/sync" asChild>
          <Pressable accessibilityRole="button" style={styles.primaryButton}>
            <Text style={styles.primaryButtonText}>Open Sync & exceptions</Text>
          </Pressable>
        </Link>
        <Pressable accessibilityRole="button" onPress={startNewSale} style={styles.secondaryButton}>
          <Text style={styles.secondaryButtonText}>Start another sale</Text>
        </Pressable>
      </ScrollView>
    );
  }

  if (result) {
    return (
      <ScrollView style={styles.page} contentContainerStyle={styles.content}>
        <View style={styles.successIcon}><Ionicons name="checkmark" size={34} color={colors.white} /></View>
        <Text style={styles.successTitle}>Sale completed</Text>
        <Text style={styles.successNumber}>{receipt?.invoiceNumber ?? result.invoiceNumber}</Text>
        <Text style={styles.successAmount}>{money(result.totalAmount, result.currencyCode, preview?.currencyDecimalPlaces ?? 2)}</Text>
        {error && <ErrorBox message={`${error.message}${error.correlationId ? ` Reference: ${error.correlationId}` : ""}`} />}
        {receipt ? (
          <View style={styles.receiptCard}>
            {receipt.copyType === "REPRINT" && <Text style={styles.reprintMark}>REPRINT · COPY {receipt.copyNumber}</Text>}
            <Text style={styles.receiptTenant}>{receipt.tenantName || "RHEMA ERP"}</Text>
            <Text style={styles.receiptMeta}>{receipt.storeName} · {receipt.locationName || receipt.storeCode}</Text>
            <Text style={styles.receiptMeta}>{receipt.tillNumber} · {receipt.tillSessionNumber}</Text>
            <View style={styles.receiptDivider} />
            <LabelValue label="Customer" value={`${receipt.customerName} (${receipt.customerCode})`} />
            <LabelValue label="Cashier" value={receipt.cashierName} />
            <LabelValue label="Invoice" value={`${receipt.invoiceNumber} · ${receipt.invoiceStatus}`} />
            <LabelValue label="Reference" value={receipt.localReference} />
            <View style={styles.receiptDivider} />
            {receipt.lines.map(line => (
              <View key={line.sequence} style={styles.receiptLine}>
                <View style={styles.grow}>
                  <Text style={styles.lineTitle}>{line.description}</Text>
                  <Text style={styles.meta}>{line.quantity} {line.unitOfMeasureCode} × {money(line.unitPrice, receipt.currencyCode, 2)}</Text>
                </View>
                <Text style={styles.receiptLineAmount}>{money(line.lineTotal, receipt.currencyCode, 2)}</Text>
              </View>
            ))}
            <View style={styles.receiptDivider} />
            <LabelValue label="Subtotal" value={money(receipt.subTotal, receipt.currencyCode, 2)} />
            <LabelValue label="Discount" value={money(receipt.discountAmount, receipt.currencyCode, 2)} />
            <LabelValue label="Tax" value={money(receipt.taxAmount, receipt.currencyCode, 2)} />
            <View style={styles.totalRow}><Text style={styles.totalLabel}>Total</Text><Text style={styles.totalValue}>{money(receipt.totalAmount, receipt.currencyCode, 2)}</Text></View>
            <View style={styles.receiptDivider} />
            {receipt.tenders.map(tender => (
              <View key={tender.sequence} style={styles.receiptTender}>
                <View style={styles.grow}>
                  <Text style={styles.lineTitle}>{tender.paymentMethodName}</Text>
                  <Text style={styles.meta}>{tender.paymentNumber}{tender.externalReference ? ` · ${tender.externalReference}` : ""}</Text>
                </View>
                <Text style={styles.receiptLineAmount}>{money(tender.amount, receipt.currencyCode, 2)}</Text>
              </View>
            ))}
            <Text selectable style={styles.qrReference}>{receipt.qrReference}</Text>
          </View>
        ) : (
          <View style={styles.card}>
            <LabelValue label="Invoice status" value={result.invoiceStatus} />
            <LabelValue label="Local reference" value={result.localReference} />
            {result.tenders.map(tender => (
              <LabelValue key={tender.tenderId} label={tender.paymentNumber} value={money(tender.amount, result.currencyCode, preview?.currencyDecimalPlaces ?? 2)} />
            ))}
          </View>
        )}
        <Text style={styles.receiptNote}>This receipt is projected from the canonical RHEMA invoice and allocated payment records. Generating another copy records an audit event and does not repost the sale.</Text>
        {outputMessage && <View style={styles.outputMessage}><Ionicons name="checkmark-circle-outline" size={19} color={colors.success} /><Text style={styles.outputMessageText}>{outputMessage}</Text></View>}
        {receipt && (
          <View style={styles.outputActions}>
             <Pressable accessibilityRole="button" disabled={busy !== null} onPress={() => void printCurrentReceipt(receipt)} style={[styles.outputButton, busy !== null && styles.disabled]}>
              {busy === "print" ? <ActivityIndicator color={colors.blue} /> : <><Ionicons name="print-outline" size={18} color={colors.blue} /><Text style={styles.outputButtonText}>{isZcsSmartPosAdapter(bootstrap.device.printerAdapterKey) ? "Built-in print" : "System print"}</Text></>}
            </Pressable>
             <Pressable accessibilityRole="button" disabled={busy !== null} onPress={() => void shareCurrentReceipt(receipt)} style={[styles.outputButton, busy !== null && styles.disabled]}>
              {busy === "share" ? <ActivityIndicator color={colors.blue} /> : <><Ionicons name="share-social-outline" size={18} color={colors.blue} /><Text style={styles.outputButtonText}>Share PDF</Text></>}
            </Pressable>
          </View>
        )}
        {receipt && canReprintReceipt && (
          <Pressable accessibilityRole="button" disabled={busy !== null} onPress={() => void createReprintCopy()} style={[styles.secondaryButton, busy !== null && styles.disabled]}>
            {busy === "reprint" ? <ActivityIndicator color={colors.blue} /> : <Text style={styles.secondaryButtonText}>Generate audited reprint copy</Text>}
          </Pressable>
        )}
        <Pressable accessibilityRole="button" onPress={startNewSale} style={styles.primaryButton}>
          <Text style={styles.primaryButtonText}>Start another sale</Text>
        </Pressable>
        <Link href="/" asChild><Pressable style={styles.secondaryButton}><Text style={styles.secondaryButtonText}>Back to dashboard</Text></Pressable></Link>
      </ScrollView>
    );
  }

  return (
    <ScrollView style={styles.page} contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
      <View style={styles.header}>
        <Link href="/" asChild><Pressable accessibilityRole="button" accessibilityLabel="Back" style={styles.backButton}><Ionicons name="arrow-back" size={22} color={colors.navy} /></Pressable></Link>
        <View style={styles.headerText}><Text style={styles.title}>New sale</Text><Text style={styles.subtitle}>{bootstrap.store.name} · {bootstrap.till.tillNumber}</Text></View>
      </View>

      {!bootstrap.currentTillSessionId && <ErrorBox message="Open your assigned cashier till session before starting a sale." />}
      {missingPreviewPermission && <ErrorBox message={`Your role is missing ${missingPreviewPermission}.`} />}
      {error && <ErrorBox message={`${error.message}${error.correlationId ? ` Reference: ${error.correlationId}` : ""}`} />}

      <SectionTitle number="1" title="Customer" />
      {customer && (
        <View style={styles.customerCard}>
          <View style={styles.grow}><Text style={styles.customerName}>{customer.name}</Text><Text style={styles.meta}>{customer.code}{customer.isDefaultWalkInCustomer ? " · Store default" : " · Approved customer"}</Text></View>
          {!customer.isDefaultWalkInCustomer && defaultCustomer && <Pressable onPress={() => chooseCustomer(defaultCustomer)}><Text style={styles.textAction}>Use walk-in</Text></Pressable>}
        </View>
      )}
      {canSearchCustomers && (
        <>
          <SearchBar label="Find another approved customer" value={customerQuery} onChangeText={setCustomerQuery} onSearch={() => void searchCustomers()} busy={busy === "customer"} />
          {customerNotice && <View style={styles.offlineNotice}><Ionicons name="cloud-offline-outline" size={18} color={colors.blue} /><Text style={styles.offlineNoticeText}>{customerNotice}</Text></View>}
          {customerResults.map(item => <ResultRow key={item.businessPartnerRoleId} title={item.name} detail={item.code} onPress={() => chooseCustomer(item)} />)}
        </>
      )}

      <SectionTitle number="2" title="Items" />
      <SearchBar
        busy={busy === "catalogue"}
        hardwareScanning={hardwareScanning}
        inputRef={catalogueInputRef}
        label="Item name, code or barcode"
        onChangeText={setCatalogueQuery}
        onHardwareScan={usesZcsScanner ? () => void (hardwareScanning ? cancelHardwareScanner() : triggerHardwareScanner()) : undefined}
        onScan={() => setScannerOpen(true)}
        onSearch={() => void runCatalogueSearch(catalogueQuery)}
        onSubmitValue={submitCatalogueSearch}
        value={catalogueQuery}
      />
      {catalogueNotice && <View style={styles.offlineNotice}><Ionicons name="cloud-offline-outline" size={18} color={colors.blue} /><Text style={styles.offlineNoticeText}>{catalogueNotice}</Text></View>}
      <BarcodeScannerModal visible={scannerOpen} onClose={() => setScannerOpen(false)} onScan={acceptBarcodeScan} />
      <BankAccountPickerModal
        accounts={bankAccounts}
        onClose={() => setBankAccountTenderId(null)}
        onSelect={account => {
          if (bankAccountTenderId) updateTender(bankAccountTenderId, { bankAccountId: account.bankAccountId });
          setBankAccountTenderId(null);
        }}
        selectedId={bankAccountTenderId ? tenders.find(tender => tender.paymentMethodId === bankAccountTenderId)?.bankAccountId : undefined}
        visible={bankAccountTenderId !== null}
      />
      {catalogueResults.map(item => (
        <ResultRow
          key={item.inventoryItemId}
          title={item.name}
          detail={`${item.itemCode} · ${money(item.unitPrice, item.currencyCode, 2)} · ${item.unitOfMeasureCode}${item.availableQuantity != null ? ` · ${item.availableQuantity} available` : ""}`}
          disabled={!item.isAvailable}
          onPress={() => addItem(item)}
        />
      ))}
      {cart.map(line => (
        <View key={line.clientLineId} style={styles.cartLine}>
          <View style={styles.grow}><Text style={styles.lineTitle}>{line.item.name}</Text><Text style={styles.meta}>{line.item.itemCode} · {money(line.item.unitPrice, line.item.currencyCode, 2)} each</Text>{canApplyDiscount && <View style={styles.discountInputWrap}><Text style={styles.meta}>Discount %</Text><TextInput accessibilityLabel={`${line.item.name} discount percentage`} keyboardType="decimal-pad" maxLength={6} onChangeText={value => changeDiscount(line.clientLineId, value)} style={styles.discountInput} value={String(line.discountPercentage)} /></View>}</View>
          <View style={styles.quantityControl}>
            <Pressable accessibilityLabel={`Decrease ${line.item.name}`} onPress={() => changeQuantity(line.clientLineId, -1)} style={styles.quantityButton}><Ionicons name="remove" size={17} color={colors.blue} /></Pressable>
            <Text style={styles.quantity}>{line.quantity}</Text>
            <Pressable accessibilityLabel={`Increase ${line.item.name}`} onPress={() => changeQuantity(line.clientLineId, 1)} style={styles.quantityButton}><Ionicons name="add" size={17} color={colors.blue} /></Pressable>
          </View>
          <Pressable accessibilityLabel={`Remove ${line.item.name}`} onPress={() => removeLine(line.clientLineId)} style={styles.removeButton}><Ionicons name="trash-outline" size={18} color={colors.danger} /></Pressable>
        </View>
      ))}
      {cart.length > 0 && !preview && (
        <Pressable disabled={busy !== null || Boolean(missingPreviewPermission) || !bootstrap.currentTillSessionId} onPress={() => void calculatePreview()} style={[styles.primaryButton, (busy !== null || Boolean(missingPreviewPermission) || !bootstrap.currentTillSessionId) && styles.disabled]}>
          {busy === "preview" ? <ActivityIndicator color={colors.white} /> : <Text style={styles.primaryButtonText}>Calculate with RHEMA Finance</Text>}
        </Pressable>
      )}

      {preview && (
        <>
          <SectionTitle number="3" title="Server calculated total" />
          <View style={styles.card}>
            <LabelValue label="Subtotal" value={money(preview.subTotal, preview.currencyCode, preview.currencyDecimalPlaces)} />
            <LabelValue label="Discount" value={money(preview.discountAmount, preview.currencyCode, preview.currencyDecimalPlaces)} />
            <LabelValue label="Tax" value={money(preview.taxAmount, preview.currencyCode, preview.currencyDecimalPlaces)} />
            <View style={styles.totalRow}><Text style={styles.totalLabel}>Total</Text><Text style={styles.totalValue}>{money(preview.totalAmount, preview.currencyCode, preview.currencyDecimalPlaces)}</Text></View>
          </View>

          <SectionTitle number="4" title="Payment" />
          {tenders.map(tender => {
            const method = onlineMethods.find(item => item.paymentMethodId === tender.paymentMethodId);
            if (!method) return null;
            const needsReference = method.requiresReference || method.requireExternalAuthorizationReference;
            return (
              <View key={tender.paymentMethodId} style={styles.tenderCard}>
                <View style={styles.rowBetween}><View><Text style={styles.lineTitle}>{method.name}</Text><Text style={styles.meta}>{method.type}</Text></View>{tenders.length > 1 && <Pressable onPress={() => removeTender(method.paymentMethodId)}><Text style={styles.removeText}>Remove</Text></Pressable>}</View>
                <TextInput accessibilityLabel={`${method.name} amount`} keyboardType="decimal-pad" onChangeText={value => updateTender(method.paymentMethodId, { amountText: value })} placeholder="Amount" placeholderTextColor={colors.muted} style={styles.input} value={tender.amountText} />
                {needsReference && <TextInput accessibilityLabel={`${method.name} reference`} autoCapitalize="characters" onChangeText={value => updateTender(method.paymentMethodId, { externalReference: value })} placeholder="Provider or transaction reference" placeholderTextColor={colors.muted} style={styles.input} value={tender.externalReference} />}
                {method.requiresBankAccount && (() => {
                  const selected = bankAccounts.find(account => account.bankAccountId === tender.bankAccountId);
                  return (
                    <Pressable
                      accessibilityLabel={`Select bank account for ${method.name}`}
                      disabled={busy !== null}
                      onPress={() => void openBankAccountPicker(method.paymentMethodId)}
                      style={[styles.bankSelector, busy !== null && styles.disabled]}
                    >
                      <Ionicons color={selected ? colors.success : colors.blue} name="business-outline" size={18} />
                      <View style={styles.grow}>
                        <Text style={styles.bankSelectorTitle}>{selected?.accountName ?? "Select bank account"}</Text>
                        <Text style={styles.bankSelectorMeta}>{selected ? `${selected.bankName} · ${selected.maskedAccountNumber}` : "Required for this payment method"}</Text>
                      </View>
                      {busy === "bankAccounts" ? <ActivityIndicator color={colors.blue} size="small" /> : <Ionicons color={colors.muted} name="chevron-forward" size={18} />}
                    </Pressable>
                  );
                })()}
              </View>
            );
          })}
          <View style={styles.methodWrap}>
            {onlineMethods.filter(method => !tenders.some(tender => tender.paymentMethodId === method.paymentMethodId)).map(method => (
              <Pressable key={method.paymentMethodId} onPress={() => addTender(method)} style={styles.methodButton}><Ionicons name="add" size={16} color={colors.blue} /><Text style={styles.methodButtonText}>{method.name}</Text></Pressable>
            ))}
          </View>
          <View style={styles.tenderTotal}><Text style={styles.meta}>Tendered</Text><Text style={styles.tenderTotalValue}>{money(sumTenderDrafts(tenders, preview.currencyDecimalPlaces), preview.currencyCode, preview.currencyDecimalPlaces)}</Text></View>
          <Pressable disabled={busy !== null || Boolean(missingCompletionPermission)} onPress={() => void completeSale()} style={[styles.primaryButton, (busy !== null || Boolean(missingCompletionPermission)) && styles.disabled]}>
            {busy === "complete" ? <ActivityIndicator color={colors.white} /> : <Text style={styles.primaryButtonText}>Complete sale</Text>}
          </Pressable>
          {missingCompletionPermission && <Text style={styles.permissionNote}>Completion requires {missingCompletionPermission}.</Text>}
        </>
      )}
    </ScrollView>
  );
}

function SearchBar({ label, value, onChangeText, onSearch, onSubmitValue, onScan, onHardwareScan, hardwareScanning = false, inputRef, busy }: { label: string; value: string; onChangeText: (value: string) => void; onSearch: () => void; onSubmitValue?: (value: string) => void; onScan?: () => void; onHardwareScan?: () => void; hardwareScanning?: boolean; inputRef?: RefObject<TextInput | null>; busy: boolean }) {
  return <View style={styles.searchRow}><TextInput ref={inputRef} accessibilityLabel={label} autoCapitalize="none" onChangeText={onChangeText} onSubmitEditing={event => onSubmitValue ? onSubmitValue(event.nativeEvent.text) : onSearch()} placeholder={hardwareScanning ? "Scan a barcode with the Z92S scanner" : label} placeholderTextColor={colors.muted} returnKeyType="search" style={[styles.input, styles.searchInput]} value={value} />{onHardwareScan && <Pressable accessibilityLabel={hardwareScanning ? "Stop Z92S hardware scanner" : "Trigger Z92S hardware scanner"} disabled={busy} onPress={onHardwareScan} style={styles.scanButton}>{hardwareScanning ? <Ionicons name="stop-circle-outline" size={21} color={colors.danger} /> : <Ionicons name="scan-outline" size={21} color={colors.blue} />}</Pressable>}{onScan && <Pressable accessibilityLabel="Open camera barcode scanner" disabled={busy || hardwareScanning} onPress={onScan} style={styles.scanButton}><Ionicons name="barcode-outline" size={21} color={colors.blue} /></Pressable>}<Pressable disabled={busy || hardwareScanning} onPress={onSearch} style={[styles.searchButton, hardwareScanning && styles.disabled]}>{busy ? <ActivityIndicator size="small" color={colors.white} /> : <Ionicons name="search" size={19} color={colors.white} />}</Pressable></View>;
}

function ResultRow({ title, detail, onPress, disabled = false }: { title: string; detail: string; onPress: () => void; disabled?: boolean }) {
  return <Pressable disabled={disabled} onPress={onPress} style={[styles.resultRow, disabled && styles.disabled]}><View style={styles.grow}><Text style={styles.lineTitle}>{title}</Text><Text style={styles.meta}>{detail}</Text></View><Ionicons name="add-circle-outline" size={22} color={colors.blue} /></Pressable>;
}

function SectionTitle({ number, title }: { number: string; title: string }) {
  return <View style={styles.sectionHeading}><View style={styles.numberBadge}><Text style={styles.numberText}>{number}</Text></View><Text style={styles.sectionTitle}>{title}</Text></View>;
}

function LabelValue({ label, value }: { label: string; value: string }) {
  return <View style={styles.labelValue}><Text style={styles.meta}>{label}</Text><Text style={styles.labelValueText}>{value}</Text></View>;
}

function SaleReceiptCard({ receipt }: { receipt: MobilePosReceipt | MobilePosProvisionalSaleReceipt }) {
  return <View style={styles.receiptCard}>
    {receipt.copyType === "PROVISIONAL" && <Text style={styles.provisionalMark}>PROVISIONAL · PENDING SYNCHRONIZATION</Text>}
    {receipt.copyType === "REPRINT" && <Text style={styles.reprintMark}>REPRINT · COPY {receipt.copyNumber}</Text>}
    <Text style={styles.receiptTenant}>{receipt.tenantName || "RHEMA ERP"}</Text>
    <Text style={styles.receiptMeta}>SALES RECEIPT</Text>
    <Text style={styles.receiptMeta}>{receipt.storeName} · {receipt.locationName || receipt.storeCode}</Text>
    <Text style={styles.receiptMeta}>{receipt.tillNumber} · {receipt.tillSessionNumber}</Text>
    <View style={styles.receiptDivider} />
    <LabelValue label="Customer" value={`${receipt.customerName} (${receipt.customerCode})`} />
    <LabelValue label="Cashier" value={receipt.cashierName} />
    {receipt.receiptKind === "SALE"
      ? <LabelValue label="Invoice" value={`${receipt.invoiceNumber} · ${receipt.invoiceStatus}`} />
      : <LabelValue label="Status" value="Pending synchronization" />}
    <LabelValue label="Reference" value={receipt.localReference} />
    <View style={styles.receiptDivider} />
    {receipt.lines.map(line => <View key={line.sequence} style={styles.receiptLine}>
      <View style={styles.grow}><Text style={styles.lineTitle}>{line.description}</Text><Text style={styles.meta}>{line.quantity} {line.unitOfMeasureCode} × {money(line.unitPrice, receipt.currencyCode, 2)}</Text></View>
      <Text style={styles.receiptLineAmount}>{money(line.lineTotal, receipt.currencyCode, 2)}</Text>
    </View>)}
    <View style={styles.receiptDivider} />
    <LabelValue label="Subtotal" value={money(receipt.subTotal, receipt.currencyCode, 2)} />
    <LabelValue label="Discount" value={money(receipt.discountAmount, receipt.currencyCode, 2)} />
    <LabelValue label="Tax" value={money(receipt.taxAmount, receipt.currencyCode, 2)} />
    <View style={styles.totalRow}><Text style={styles.totalLabel}>Total</Text><Text style={styles.totalValue}>{money(receipt.totalAmount, receipt.currencyCode, 2)}</Text></View>
    <View style={styles.receiptDivider} />
    {receipt.tenders.map(tender => <View key={tender.sequence} style={styles.receiptTender}>
      <View style={styles.grow}>
        <Text style={styles.lineTitle}>{tender.paymentMethodName}</Text>
        <Text style={styles.meta}>{"paymentNumber" in tender ? tender.paymentNumber : "Pending server payment number"}{tender.externalReference ? ` · ${tender.externalReference}` : ""}</Text>
      </View>
      <Text style={styles.receiptLineAmount}>{money(tender.amount, receipt.currencyCode, 2)}</Text>
    </View>)}
    <Text selectable style={styles.qrReference}>{receipt.qrReference}</Text>
    {receipt.copyType === "PROVISIONAL" && <Text style={styles.receiptNote}>This is not a final Finance receipt. The invoice and payment numbers will be assigned after successful synchronization.</Text>}
  </View>;
}

function ErrorBox({ message }: { message: string }) {
  return <View accessibilityRole="alert" style={styles.errorBox}><Ionicons name="alert-circle-outline" size={20} color={colors.danger} /><Text style={styles.errorText}>{message}</Text></View>;
}

function allowedQuantity(quantity: number, item: MobilePosCatalogueItem): number {
  if (quantity <= 0) return 0;
  if (item.availableQuantity == null) return quantity;
  return Math.min(quantity, Math.max(0, item.availableQuantity));
}

function createIdentity(): PendingIdentity {
  const id = Crypto.randomUUID();
  return {
    clientMutationId: id,
    localReference: `MOB-${new Date().toISOString().replace(/\D/g, "").slice(0, 14)}-${id.slice(0, 8).toUpperCase()}`,
    occurredAtUtc: new Date().toISOString(),
  };
}

function asProblem(error: unknown): ApiProblem {
  return error instanceof ApiProblem ? error : problem(error instanceof Error ? error.message : "An unexpected error occurred.");
}

function receiptOutputProblem(subject: string, stage: "load" | "persist", error: unknown): ApiProblem {
  const source = asProblem(error);
  const message = stage === "load"
    ? `The ${subject} completed, but its canonical receipt could not be loaded. The transaction will not be queued or posted again.`
    : `The ${subject} completed and its receipt is available, but the receipt could not be saved on this device.`;
  return new ApiProblem(message, source.status, source.code ?? "MOBILE_POS_RECEIPT_OUTPUT_FAILED", source.correlationId);
}

function problem(message: string): ApiProblem {
  return new ApiProblem(message, 400, "CHECKOUT_INPUT_REQUIRED");
}

function money(value: number, currency: string, decimalPlaces: number): string {
  return `${currency} ${value.toLocaleString(undefined, { minimumFractionDigits: decimalPlaces, maximumFractionDigits: decimalPlaces })}`;
}

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: colors.background },
  content: { paddingHorizontal: 20, paddingTop: 52, paddingBottom: 42 },
  header: { flexDirection: "row", alignItems: "center" },
  backButton: { width: 42, height: 42, borderRadius: 13, alignItems: "center", justifyContent: "center", backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  headerText: { flex: 1, marginLeft: 13 },
  title: { color: colors.ink, fontSize: 22, fontWeight: "700" },
  subtitle: { marginTop: 2, color: colors.muted, fontSize: 11 },
  grow: { flex: 1 },
  sectionHeading: { marginTop: 25, marginBottom: 10, flexDirection: "row", alignItems: "center" },
  numberBadge: { width: 25, height: 25, borderRadius: 8, alignItems: "center", justifyContent: "center", backgroundColor: colors.paleBlue },
  numberText: { color: colors.blue, fontSize: 11, fontWeight: "800" },
  sectionTitle: { marginLeft: 9, color: colors.ink, fontSize: 15, fontWeight: "700" },
  customerCard: { minHeight: 66, flexDirection: "row", alignItems: "center", padding: 14, borderRadius: 14, backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  customerName: { color: colors.ink, fontSize: 15, fontWeight: "700" },
  meta: { marginTop: 4, color: colors.muted, fontSize: 11, lineHeight: 16 },
  textAction: { color: colors.blue, fontSize: 12, fontWeight: "700" },
  searchRow: { marginTop: 10, flexDirection: "row", gap: 9 },
  input: { minHeight: 46, marginTop: 9, paddingHorizontal: 13, borderRadius: 11, borderWidth: 1, borderColor: colors.line, backgroundColor: colors.white, color: colors.ink, fontSize: 14 },
  searchInput: { flex: 1, marginTop: 0 },
  searchButton: { width: 48, height: 48, alignItems: "center", justifyContent: "center", borderRadius: 12, backgroundColor: colors.blue },
  scanButton: { width: 48, height: 48, alignItems: "center", justifyContent: "center", borderRadius: 12, borderWidth: 1, borderColor: "#B2CCFF", backgroundColor: colors.white },
  resultRow: { minHeight: 66, marginTop: 8, flexDirection: "row", alignItems: "center", padding: 13, borderRadius: 13, backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  lineTitle: { color: colors.ink, fontSize: 13, fontWeight: "700" },
  cartLine: { marginTop: 8, flexDirection: "row", alignItems: "center", padding: 13, borderRadius: 14, backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  discountInputWrap: { marginTop: 9, flexDirection: "row", alignItems: "center", gap: 8 },
  discountInput: { width: 76, minHeight: 38, paddingHorizontal: 10, borderRadius: 9, borderWidth: 1, borderColor: colors.line, backgroundColor: "#F9FAFB", color: colors.ink, fontSize: 13, textAlign: "right" },
  quantityControl: { marginLeft: 8, flexDirection: "row", alignItems: "center" },
  quantityButton: { width: 30, height: 30, borderRadius: 9, alignItems: "center", justifyContent: "center", backgroundColor: colors.paleBlue },
  quantity: { minWidth: 28, color: colors.ink, fontSize: 13, fontWeight: "700", textAlign: "center" },
  removeButton: { marginLeft: 8, width: 32, height: 32, alignItems: "center", justifyContent: "center" },
  primaryButton: { marginTop: 16, minHeight: 50, alignItems: "center", justifyContent: "center", borderRadius: 12, backgroundColor: colors.blue },
  primaryButtonText: { color: colors.white, fontSize: 14, fontWeight: "700" },
  secondaryButton: { marginTop: 10, minHeight: 48, alignItems: "center", justifyContent: "center", borderRadius: 12, borderWidth: 1, borderColor: "#B2CCFF", backgroundColor: colors.white },
  secondaryButtonText: { color: colors.blue, fontSize: 14, fontWeight: "700" },
  disabled: { opacity: 0.42 },
  errorBox: { marginTop: 14, flexDirection: "row", gap: 9, padding: 13, borderRadius: 11, backgroundColor: colors.dangerBg },
  errorText: { flex: 1, color: colors.danger, fontSize: 12, lineHeight: 18 },
  card: { padding: 15, borderRadius: 15, backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  labelValue: { paddingVertical: 8, flexDirection: "row", alignItems: "center", justifyContent: "space-between", borderBottomWidth: 1, borderBottomColor: "#F2F4F7" },
  labelValueText: { maxWidth: "62%", color: colors.ink, fontSize: 12, fontWeight: "700", textAlign: "right" },
  totalRow: { marginTop: 10, paddingTop: 12, flexDirection: "row", alignItems: "center", justifyContent: "space-between", borderTopWidth: 1, borderTopColor: colors.line },
  totalLabel: { color: colors.ink, fontSize: 15, fontWeight: "700" },
  totalValue: { color: colors.navy, fontSize: 20, fontWeight: "800" },
  tenderCard: { marginBottom: 9, padding: 14, borderRadius: 14, backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  rowBetween: { flexDirection: "row", alignItems: "center", justifyContent: "space-between" },
  removeText: { color: colors.danger, fontSize: 11, fontWeight: "700" },
  bankSelector: { minHeight: 58, marginTop: 9, paddingHorizontal: 12, flexDirection: "row", gap: 10, alignItems: "center", borderRadius: 11, borderWidth: 1, borderColor: "#B2CCFF", backgroundColor: colors.paleBlue },
  bankSelectorTitle: { color: colors.ink, fontSize: 12, fontWeight: "700" },
  bankSelectorMeta: { marginTop: 2, color: colors.muted, fontSize: 10 },
  methodWrap: { flexDirection: "row", flexWrap: "wrap", gap: 8 },
  methodButton: { minHeight: 38, paddingHorizontal: 11, flexDirection: "row", gap: 5, alignItems: "center", borderRadius: 10, borderWidth: 1, borderColor: "#B2CCFF", backgroundColor: colors.white },
  methodButtonText: { color: colors.blue, fontSize: 11, fontWeight: "700" },
  tenderTotal: { marginTop: 13, flexDirection: "row", alignItems: "center", justifyContent: "space-between" },
  tenderTotalValue: { color: colors.ink, fontSize: 16, fontWeight: "800" },
  permissionNote: { marginTop: 8, color: colors.warning, fontSize: 11, textAlign: "center" },
  successIcon: { alignSelf: "center", marginTop: 38, width: 66, height: 66, borderRadius: 22, alignItems: "center", justifyContent: "center", backgroundColor: colors.success },
  successTitle: { marginTop: 20, color: colors.ink, fontSize: 24, fontWeight: "800", textAlign: "center" },
  successNumber: { marginTop: 6, color: colors.slate, fontSize: 14, fontWeight: "700", textAlign: "center" },
  successAmount: { marginTop: 10, marginBottom: 22, color: colors.navy, fontSize: 28, fontWeight: "800", textAlign: "center" },
  receiptNote: { marginTop: 14, color: colors.slate, fontSize: 12, lineHeight: 18, textAlign: "center" },
  receiptCard: { marginTop: 4, padding: 18, borderRadius: 18, borderWidth: 1, borderColor: colors.line, backgroundColor: colors.white },
  receiptTenant: { color: colors.navy, fontSize: 18, fontWeight: "800", textAlign: "center" },
  receiptMeta: { marginTop: 3, color: colors.slate, fontSize: 12, textAlign: "center" },
  receiptDivider: { height: 1, marginVertical: 14, backgroundColor: colors.line },
  receiptLine: { flexDirection: "row", alignItems: "center", gap: 12, paddingVertical: 5 },
  receiptTender: { flexDirection: "row", alignItems: "center", gap: 12, paddingVertical: 5 },
  receiptLineAmount: { color: colors.ink, fontSize: 13, fontWeight: "700" },
  reprintMark: { marginBottom: 12, color: colors.danger, fontSize: 13, fontWeight: "800", letterSpacing: 1.5, textAlign: "center" },
  provisionalMark: { marginBottom: 12, padding: 8, borderWidth: 2, borderColor: colors.warning, color: colors.warning, fontSize: 11, fontWeight: "800", letterSpacing: 1, textAlign: "center" },
  qrReference: { marginTop: 16, color: colors.slate, fontSize: 10, textAlign: "center" },
  outputActions: { marginTop: 10, flexDirection: "row", gap: 10 },
  outputButton: { flex: 1, minHeight: 48, flexDirection: "row", gap: 7, alignItems: "center", justifyContent: "center", borderRadius: 12, borderWidth: 1, borderColor: "#B2CCFF", backgroundColor: colors.white },
  outputButtonText: { color: colors.blue, fontSize: 13, fontWeight: "700" },
  outputMessage: { marginTop: 12, flexDirection: "row", gap: 8, alignItems: "center", padding: 11, borderRadius: 11, backgroundColor: colors.successBg },
  outputMessageText: { flex: 1, color: colors.success, fontSize: 11, lineHeight: 16 },
  offlineNotice: { marginBottom: 10, flexDirection: "row", gap: 8, alignItems: "center", padding: 11, borderRadius: 11, backgroundColor: colors.paleBlue },
  offlineNoticeText: { flex: 1, color: colors.navy, fontSize: 11, lineHeight: 16 },
});
