import { Ionicons } from "@expo/vector-icons";
import * as Crypto from "expo-crypto";
import { Link, Redirect } from "expo-router";
import { useCallback, useEffect, useMemo, useState } from "react";
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from "react-native";
import { ApiProblem, mobileApi } from "@/src/api/client";
import { type CachedReceiptDocument, type CanonicalMobilePosReceipt } from "@/src/offline/receipt-cache";
import { cacheSessionReceipt, hydrateSynchronizedSessionReceipts, loadSessionReceipts } from "@/src/offline/receipt-runtime";
import { printReceiptAsync, shareReceiptPdfAsync } from "@/src/receipts/output";
import { useSession } from "@/src/session/session-context";
import { getInstallationId } from "@/src/storage/secure-session";
import { colors } from "@/src/ui/theme";

type BusyState = "load" | "reprint" | "print" | "share" | null;

export default function ReceiptsScreen() {
  const session = useSession();
  const user = session.user;
  const bootstrap = session.bootstrap;
  const [documents, setDocuments] = useState<CachedReceiptDocument[]>([]);
  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const [outputReceipt, setOutputReceipt] = useState<CanonicalMobilePosReceipt | null>(null);
  const [busy, setBusy] = useState<BusyState>("load");
  const [error, setError] = useState<ApiProblem | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const selected = useMemo(
    () => documents.find(item => receiptKey(item.receipt) === selectedKey) ?? null,
    [documents, selectedKey],
  );
  const canReprint = user?.permissions.includes("MobilePOS.Receipt.Reprint") ?? false;

  const refresh = useCallback(async (hydrate: boolean) => {
    if (!user || !bootstrap) return;
    setBusy("load");
    setError(null);
    setNotice(null);
    try {
      if (hydrate) {
        const result = await hydrateSynchronizedSessionReceipts(user, bootstrap);
        if (result.cached > 0) setNotice(`${result.cached} synchronized receipt(s) were downloaded to this device.`);
        if (result.failed > 0) setNotice(`${result.failed} synchronized receipt(s) could not be downloaded. Saved receipts remain available.`);
      }
      const loaded = await loadSessionReceipts(user, bootstrap);
      setDocuments(loaded);
      setSelectedKey(current => current && loaded.some(item => receiptKey(item.receipt) === current)
        ? current
        : loaded[0] ? receiptKey(loaded[0].receipt) : null);
    } catch (caught) {
      setError(asProblem(caught, "Saved receipts could not be loaded."));
    } finally {
      setBusy(null);
    }
  }, [bootstrap, user]);

  useEffect(() => { void refresh(true); }, [refresh]);

  if (session.status === "signedOut" || session.status === "mfaRequired") return <Redirect href="/login" />;
  if (session.status !== "ready" || !bootstrap || !user) return <Redirect href="/" />;

  const createAuditedReprint = async () => {
    if (!selected || !canReprint) return;
    setBusy("reprint");
    setError(null);
    setNotice(null);
    setOutputReceipt(null);
    try {
      const request = {
        installationId: await getInstallationId(),
        clientEventId: Crypto.randomUUID(),
        reason: "Cashier requested a copy from saved receipts",
      };
      const reprint = selected.receipt.receiptKind === "SALE"
        ? await mobileApi.recordReceiptReprint(selected.receipt.receiptId, request)
        : await mobileApi.recordCollectionReceiptReprint(selected.receipt.receiptId, request);
      setOutputReceipt(reprint);
      try {
        await cacheSessionReceipt(user, bootstrap, reprint);
        setDocuments(await loadSessionReceipts(user, bootstrap));
        setSelectedKey(receiptKey(reprint));
        setNotice(`Audited reprint copy ${reprint.copyNumber} is ready for output.`);
      } catch (cacheError) {
        const source = asProblem(cacheError, "The receipt cache failed.");
        setError(new ApiProblem("The audited reprint was created, but it could not be saved on this device.", source.status, source.code ?? "MOBILE_POS_RECEIPT_CACHE_FAILED", source.correlationId));
        setNotice(`Audited reprint copy ${reprint.copyNumber} is ready for output but was not saved locally.`);
      }
    } catch (caught) {
      setError(asProblem(caught, "The audited reprint could not be created."));
    } finally {
      setBusy(null);
    }
  };

  const printReprint = async () => {
    if (!outputReceipt) return;
    setBusy("print");
    setError(null);
    try {
      const result = await printReceiptAsync(outputReceipt, bootstrap.device.printerAdapterKey);
      setNotice(`Reprint copy ${outputReceipt.copyNumber} was sent to ${result.adapterLabel}.`);
    } catch (caught) {
      setError(asProblem(caught, "The receipt could not be printed."));
    } finally {
      setBusy(null);
    }
  };

  const shareReprint = async () => {
    if (!outputReceipt) return;
    setBusy("share");
    setError(null);
    try {
      await shareReceiptPdfAsync(outputReceipt);
      setNotice(`Reprint copy ${outputReceipt.copyNumber} is ready to share.`);
    } catch (caught) {
      setError(asProblem(caught, "The receipt PDF could not be shared."));
    } finally {
      setBusy(null);
    }
  };

  return (
    <ScrollView style={styles.page} contentContainerStyle={styles.content}>
      <View style={styles.header}>
        <Link href="/" asChild><Pressable accessibilityLabel="Back" style={styles.backButton}><Ionicons name="arrow-back" size={21} color={colors.navy} /></Pressable></Link>
        <View style={styles.grow}><Text style={styles.title}>Saved receipts</Text><Text style={styles.subtitle}>{bootstrap.store.code} · {bootstrap.till.tillNumber}</Text></View>
        <Pressable accessibilityLabel="Refresh receipts" disabled={busy !== null} onPress={() => void refresh(true)} style={[styles.refreshButton, busy !== null && styles.disabled]}>
          {busy === "load" ? <ActivityIndicator size="small" color={colors.blue} /> : <Ionicons name="refresh" size={20} color={colors.blue} />}
        </Pressable>
      </View>

      <View style={styles.scopeNotice}>
        <Ionicons name="shield-checkmark-outline" size={19} color={colors.blue} />
        <Text style={styles.scopeText}>Only canonical Finance receipts saved for this tenant, user, device, store, and till are shown. Provisional slips are excluded.</Text>
      </View>
      {error && <Message tone="error" text={`${error.message}${error.correlationId ? ` Reference: ${error.correlationId}` : ""}`} />}
      {notice && <Message tone="notice" text={notice} />}

      {busy === "load" && documents.length === 0 ? <ActivityIndicator style={styles.loader} color={colors.blue} /> : documents.length === 0 ? (
        <View style={styles.emptyCard}>
          <Ionicons name="receipt-outline" size={34} color={colors.muted} />
          <Text style={styles.emptyTitle}>No canonical receipts saved</Text>
          <Text style={styles.emptyText}>Complete an online sale or collection, or synchronize queued work, to save its final Finance receipt here.</Text>
        </View>
      ) : (
        <>
          <Text style={styles.sectionTitle}>Receipt history</Text>
          {documents.map(document => {
            const receipt = document.receipt;
            const active = receiptKey(receipt) === selectedKey;
            return <Pressable key={receiptKey(receipt)} onPress={() => { setSelectedKey(receiptKey(receipt)); setOutputReceipt(null); }} style={[styles.listCard, active && styles.listCardActive]}>
              <View style={[styles.kindIcon, receipt.receiptKind === "SALE" ? styles.saleIcon : styles.collectionIcon]}><Ionicons name={receipt.receiptKind === "SALE" ? "cart-outline" : "cash-outline"} size={19} color={receipt.receiptKind === "SALE" ? colors.blue : colors.success} /></View>
              <View style={styles.grow}>
                <Text style={styles.receiptNumber}>{document.receiptNumber}</Text>
                <Text style={styles.receiptMeta}>{receipt.customerName} · {formatDate(receipt.occurredAtUtc)}</Text>
                <Text style={styles.receiptMeta}>{receipt.copyType === "REPRINT" ? `Audited reprint copy ${receipt.copyNumber}` : "Original"}</Text>
              </View>
              <Text style={styles.amount}>{money(receipt.totalAmount, receipt.currencyCode)}</Text>
            </Pressable>;
          })}
        </>
      )}

      {selected && (
        <View style={styles.detailCard}>
          <Text style={styles.detailHeading}>{selected.receipt.receiptKind === "SALE" ? "Sales receipt" : "Customer collection receipt"}</Text>
          <DetailRow label="Reference" value={selected.receiptNumber} />
          <DetailRow label="Local reference" value={selected.receipt.localReference} />
          <DetailRow label="Customer" value={`${selected.receipt.customerName} (${selected.receipt.customerCode})`} />
          <DetailRow label="Cashier" value={selected.receipt.cashierName} />
          <DetailRow label="Date" value={formatDate(selected.receipt.occurredAtUtc)} />
          {selected.receipt.receiptKind === "SALE"
            ? selected.receipt.lines.map(line => <DetailRow key={line.sequence} label={`${line.quantity} ${line.unitOfMeasureCode} · ${line.description}`} value={money(line.lineTotal, selected.receipt.currencyCode)} />)
            : selected.receipt.allocations.map(allocation => <DetailRow key={allocation.sequence} label={allocation.invoiceNumber} value={money(allocation.amount, selected.receipt.currencyCode)} />)}
          <View style={styles.totalRow}><Text style={styles.totalLabel}>Total</Text><Text style={styles.totalValue}>{money(selected.receipt.totalAmount, selected.receipt.currencyCode)}</Text></View>
          {selected.receipt.tenders.map(tender => <DetailRow key={tender.sequence} label={tender.paymentMethodName} value={tender.paymentNumber} />)}
          <Text selectable style={styles.qrReference}>{selected.receipt.qrReference}</Text>

          {canReprint ? (
            <Pressable disabled={busy !== null} onPress={() => void createAuditedReprint()} style={[styles.primaryButton, busy !== null && styles.disabled]}>
              {busy === "reprint" ? <ActivityIndicator color={colors.white} /> : <><Ionicons name="copy-outline" size={18} color={colors.white} /><Text style={styles.primaryText}>Create audited reprint</Text></>}
            </Pressable>
          ) : <Text style={styles.permissionText}>Your assigned role needs the Reprint Mobile POS Receipts permission to generate another copy.</Text>}

          {outputReceipt && (
            <View style={styles.outputActions}>
              <Pressable disabled={busy !== null} onPress={() => void printReprint()} style={[styles.outputButton, busy !== null && styles.disabled]}><Ionicons name="print-outline" size={18} color={colors.blue} /><Text style={styles.outputText}>Print copy {outputReceipt.copyNumber}</Text></Pressable>
              <Pressable disabled={busy !== null} onPress={() => void shareReprint()} style={[styles.outputButton, busy !== null && styles.disabled]}><Ionicons name="share-social-outline" size={18} color={colors.blue} /><Text style={styles.outputText}>Share PDF</Text></Pressable>
            </View>
          )}
        </View>
      )}
    </ScrollView>
  );
}

function receiptKey(receipt: CanonicalMobilePosReceipt): string {
  return `${receipt.receiptKind}:${receipt.receiptId}:${receipt.copyType}:${receipt.copyNumber}`;
}
function money(value: number, currency: string): string { return `${currency} ${value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`; }
function formatDate(value: string): string { const date = new Date(value); return Number.isNaN(date.getTime()) ? value : date.toLocaleString(); }
function asProblem(error: unknown, fallback: string): ApiProblem { return error instanceof ApiProblem ? error : new ApiProblem(error instanceof Error ? error.message : fallback, 0, "MOBILE_POS_RECEIPT_CACHE_FAILED"); }
function DetailRow({ label, value }: { label: string; value: string }) { return <View style={styles.detailRow}><Text style={styles.detailLabel}>{label}</Text><Text selectable style={styles.detailValue}>{value}</Text></View>; }
function Message({ tone, text }: { tone: "error" | "notice"; text: string }) { const danger = tone === "error"; return <View accessibilityRole={danger ? "alert" : undefined} style={[styles.message, { backgroundColor: danger ? colors.dangerBg : colors.successBg }]}><Ionicons name={danger ? "alert-circle-outline" : "checkmark-circle-outline"} size={19} color={danger ? colors.danger : colors.success} /><Text style={[styles.messageText, { color: danger ? colors.danger : colors.success }]}>{text}</Text></View>; }

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: colors.background }, content: { paddingHorizontal: 20, paddingTop: 52, paddingBottom: 42 },
  header: { flexDirection: "row", alignItems: "center", gap: 12 }, grow: { flex: 1 },
  backButton: { width: 42, height: 42, borderRadius: 13, alignItems: "center", justifyContent: "center", backgroundColor: colors.white, borderWidth: 1, borderColor: colors.line },
  refreshButton: { width: 42, height: 42, borderRadius: 13, alignItems: "center", justifyContent: "center", backgroundColor: colors.white, borderWidth: 1, borderColor: colors.line },
  title: { color: colors.ink, fontSize: 22, fontWeight: "800" }, subtitle: { marginTop: 3, color: colors.muted, fontSize: 11 }, disabled: { opacity: 0.5 },
  scopeNotice: { marginTop: 18, padding: 13, flexDirection: "row", gap: 9, borderRadius: 12, backgroundColor: colors.paleBlue }, scopeText: { flex: 1, color: colors.slate, fontSize: 11, lineHeight: 17 },
  message: { marginTop: 12, padding: 13, flexDirection: "row", gap: 9, borderRadius: 12 }, messageText: { flex: 1, fontSize: 12, lineHeight: 18 }, loader: { marginTop: 40 },
  emptyCard: { marginTop: 22, padding: 28, alignItems: "center", borderRadius: 16, backgroundColor: colors.white, borderWidth: 1, borderColor: colors.line }, emptyTitle: { marginTop: 11, color: colors.ink, fontSize: 15, fontWeight: "700" }, emptyText: { marginTop: 5, color: colors.muted, fontSize: 11, lineHeight: 17, textAlign: "center" },
  sectionTitle: { marginTop: 24, marginBottom: 9, color: colors.ink, fontSize: 15, fontWeight: "700" }, listCard: { marginBottom: 9, padding: 13, flexDirection: "row", alignItems: "center", gap: 11, borderRadius: 14, backgroundColor: colors.white, borderWidth: 1, borderColor: colors.line }, listCardActive: { borderColor: colors.blue, backgroundColor: colors.paleBlue },
  kindIcon: { width: 40, height: 40, borderRadius: 12, alignItems: "center", justifyContent: "center" }, saleIcon: { backgroundColor: colors.paleBlue }, collectionIcon: { backgroundColor: colors.successBg }, receiptNumber: { color: colors.ink, fontSize: 13, fontWeight: "700" }, receiptMeta: { marginTop: 2, color: colors.muted, fontSize: 10 }, amount: { color: colors.navy, fontSize: 12, fontWeight: "800" },
  detailCard: { marginTop: 14, padding: 17, borderRadius: 16, backgroundColor: colors.white, borderWidth: 1, borderColor: colors.line }, detailHeading: { marginBottom: 9, color: colors.ink, fontSize: 16, fontWeight: "800" }, detailRow: { paddingVertical: 9, flexDirection: "row", gap: 12, justifyContent: "space-between", borderBottomWidth: 1, borderBottomColor: "#F2F4F7" }, detailLabel: { flex: 1, color: colors.muted, fontSize: 11 }, detailValue: { flex: 1, color: colors.ink, fontSize: 11, fontWeight: "600", textAlign: "right" },
  totalRow: { paddingVertical: 13, flexDirection: "row", justifyContent: "space-between" }, totalLabel: { color: colors.ink, fontSize: 14, fontWeight: "800" }, totalValue: { color: colors.navy, fontSize: 15, fontWeight: "800" }, qrReference: { marginTop: 12, color: colors.muted, fontSize: 9, lineHeight: 13, textAlign: "center" },
  primaryButton: { marginTop: 18, minHeight: 50, flexDirection: "row", gap: 8, alignItems: "center", justifyContent: "center", borderRadius: 12, backgroundColor: colors.blue }, primaryText: { color: colors.white, fontSize: 13, fontWeight: "700" }, permissionText: { marginTop: 16, padding: 11, color: colors.warning, backgroundColor: colors.warningBg, fontSize: 11, lineHeight: 17, borderRadius: 10 },
  outputActions: { marginTop: 10, flexDirection: "row", gap: 9 }, outputButton: { flex: 1, minHeight: 46, flexDirection: "row", gap: 7, alignItems: "center", justifyContent: "center", borderRadius: 11, borderWidth: 1, borderColor: "#B2CCFF", backgroundColor: colors.white }, outputText: { color: colors.blue, fontSize: 11, fontWeight: "700" },
});
