import { Ionicons } from "@expo/vector-icons";
import * as Crypto from "expo-crypto";
import { Link, Redirect } from "expo-router";
import { useEffect, useMemo, useState } from "react";
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
import { printReceiptAsync, shareReceiptPdfAsync } from "@/src/receipts/output";
import type { BarcodeScan } from "@/src/scanning/barcode";
import { buildCompleteSaleRequest, sumTenderDrafts, type TenderDraft } from "@/src/sales/checkout";
import { useSession } from "@/src/session/session-context";
import { getInstallationId } from "@/src/storage/secure-session";
import type {
  MobilePosCatalogueItem,
  MobilePosCustomerSearchResult,
  MobilePosPaymentMethod,
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
  const [pendingIdentity, setPendingIdentity] = useState<PendingIdentity | null>(null);
  const [result, setResult] = useState<MobilePosSaleResult | null>(null);
  const [receipt, setReceipt] = useState<MobilePosReceipt | null>(null);
  const [busy, setBusy] = useState<"catalogue" | "customer" | "preview" | "complete" | "reprint" | "print" | "share" | null>(null);
  const [error, setError] = useState<ApiProblem | null>(null);
  const [outputMessage, setOutputMessage] = useState<string | null>(null);

  const onlineMethods = useMemo(
    () => bootstrap?.till.paymentMethods.filter(method => method.allowOnline) ?? [],
    [bootstrap],
  );

  useEffect(() => {
    setCustomer(defaultCustomer);
  }, [defaultCustomer]);

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
    try {
      setCatalogueResults(await mobileApi.searchCatalogue(await getInstallationId(), term));
    } catch (caught) {
      setError(asProblem(caught));
    } finally {
      setBusy(null);
    }
  };

  const acceptBarcodeScan = (scan: BarcodeScan) => {
    setScannerOpen(false);
    setCatalogueQuery(scan.value);
    void runCatalogueSearch(scan.value);
  };

  const searchCustomers = async () => {
    const term = customerQuery.trim();
    if (term.length < 2) return setError(problem("Enter at least two characters to search approved customers."));
    setBusy("customer");
    setError(null);
    try {
      setCustomerResults(await mobileApi.searchCustomers(await getInstallationId(), term));
    } catch (caught) {
      setError(asProblem(caught));
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

  const completeSale = async () => {
    if (!preview) return;
    if (missingCompletionPermission) return setError(problem(`Your role is missing ${missingCompletionPermission}.`));
    if (tenders.length === 0) return setError(problem("Select at least one tender."));
    for (const tender of tenders) {
      const method = onlineMethods.find(item => item.paymentMethodId === tender.paymentMethodId);
      if (!method) return setError(problem("A selected tender is no longer available for this till."));
      if (method.requiresBankAccount) return setError(problem(`${method.name} requires a bank account selection that is not available in this checkout yet.`));
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
    try {
      const completed = await mobileApi.completeSale(buildCompleteSaleRequest({
        installationId: await getInstallationId(),
        ...identity,
        preview,
        tenders,
      }));
      setResult(completed);
      setReceipt(await mobileApi.getReceipt(completed.saleId, await getInstallationId()));
    } catch (caught) {
      setError(asProblem(caught));
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
      setReceipt(await mobileApi.recordReceiptReprint(result.saleId, {
        installationId: await getInstallationId(),
        clientEventId: Crypto.randomUUID(),
        reason: "Cashier requested another receipt copy",
      }));
    } catch (caught) {
      setError(asProblem(caught));
    } finally {
      setBusy(null);
    }
  };

  const printCurrentReceipt = async () => {
    if (!receipt) return;
    setBusy("print");
    setError(null);
    setOutputMessage(null);
    try {
      await printReceiptAsync(receipt);
      setOutputMessage(`${receipt.copyType === "REPRINT" ? `Reprint copy ${receipt.copyNumber}` : "Original receipt"} sent to the system print service.`);
    } catch (caught) {
      setError(asProblem(caught));
    } finally {
      setBusy(null);
    }
  };

  const shareCurrentReceipt = async () => {
    if (!receipt) return;
    setBusy("share");
    setError(null);
    setOutputMessage(null);
    try {
      await shareReceiptPdfAsync(receipt);
      setOutputMessage("The receipt PDF was saved on this device and opened in the share sheet.");
    } catch (caught) {
      setError(asProblem(caught));
    } finally {
      setBusy(null);
    }
  };

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
            <Pressable accessibilityRole="button" disabled={busy !== null} onPress={() => void printCurrentReceipt()} style={[styles.outputButton, busy !== null && styles.disabled]}>
              {busy === "print" ? <ActivityIndicator color={colors.blue} /> : <><Ionicons name="print-outline" size={18} color={colors.blue} /><Text style={styles.outputButtonText}>System print</Text></>}
            </Pressable>
            <Pressable accessibilityRole="button" disabled={busy !== null} onPress={() => void shareCurrentReceipt()} style={[styles.outputButton, busy !== null && styles.disabled]}>
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
          {customerResults.map(item => <ResultRow key={item.businessPartnerRoleId} title={item.name} detail={item.code} onPress={() => chooseCustomer(item)} />)}
        </>
      )}

      <SectionTitle number="2" title="Items" />
      <SearchBar label="Item name, code or barcode" value={catalogueQuery} onChangeText={setCatalogueQuery} onSearch={() => void runCatalogueSearch(catalogueQuery)} onScan={() => setScannerOpen(true)} busy={busy === "catalogue"} />
      <BarcodeScannerModal visible={scannerOpen} onClose={() => setScannerOpen(false)} onScan={acceptBarcodeScan} />
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
                {method.requiresBankAccount && <Text style={styles.bankWarning}>Bank account selection is required and is not yet available in this checkout.</Text>}
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

function SearchBar({ label, value, onChangeText, onSearch, onScan, busy }: { label: string; value: string; onChangeText: (value: string) => void; onSearch: () => void; onScan?: () => void; busy: boolean }) {
  return <View style={styles.searchRow}><TextInput accessibilityLabel={label} autoCapitalize="none" onChangeText={onChangeText} onSubmitEditing={onSearch} placeholder={label} placeholderTextColor={colors.muted} returnKeyType="search" style={[styles.input, styles.searchInput]} value={value} />{onScan && <Pressable accessibilityLabel="Open camera barcode scanner" disabled={busy} onPress={onScan} style={styles.scanButton}><Ionicons name="barcode-outline" size={21} color={colors.blue} /></Pressable>}<Pressable disabled={busy} onPress={onSearch} style={styles.searchButton}>{busy ? <ActivityIndicator size="small" color={colors.white} /> : <Ionicons name="search" size={19} color={colors.white} />}</Pressable></View>;
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
  bankWarning: { marginTop: 9, color: colors.warning, fontSize: 11, lineHeight: 16 },
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
  qrReference: { marginTop: 16, color: colors.slate, fontSize: 10, textAlign: "center" },
  outputActions: { marginTop: 10, flexDirection: "row", gap: 10 },
  outputButton: { flex: 1, minHeight: 48, flexDirection: "row", gap: 7, alignItems: "center", justifyContent: "center", borderRadius: 12, borderWidth: 1, borderColor: "#B2CCFF", backgroundColor: colors.white },
  outputButtonText: { color: colors.blue, fontSize: 13, fontWeight: "700" },
  outputMessage: { marginTop: 12, flexDirection: "row", gap: 8, alignItems: "center", padding: 11, borderRadius: 11, backgroundColor: colors.successBg },
  outputMessageText: { flex: 1, color: colors.success, fontSize: 11, lineHeight: 16 },
});
