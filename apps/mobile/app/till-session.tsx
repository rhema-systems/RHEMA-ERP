import { Ionicons } from "@expo/vector-icons";
import { Link, Redirect } from "expo-router";
import { useCallback, useEffect, useState } from "react";
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from "react-native";
import { ApiProblem, mobileApi } from "@/src/api/client";
import { loadSessionOutboxSummary } from "@/src/offline/sync-runtime";
import { getInstallationId } from "@/src/storage/secure-session";
import type { MobilePosTillCloseSubmission, MobilePosTillReconciliation, MobilePosTillSession } from "@/src/types/api";
import { useSession } from "@/src/session/session-context";
import { colors } from "@/src/ui/theme";

const denominations = [200, 100, 50, 20, 10, 5, 2, 1, 0.5, 0.2, 0.1] as const;

export default function TillSessionScreen() {
  const session = useSession();
  const [tillSession, setTillSession] = useState<MobilePosTillSession | null>(null);
  const [reconciliation, setReconciliation] = useState<MobilePosTillReconciliation | null>(null);
  const [closeSubmission, setCloseSubmission] = useState<MobilePosTillCloseSubmission | null>(null);
  const [counts, setCounts] = useState<Record<string, string>>({});
  const [varianceReason, setVarianceReason] = useState("");
  const [openingFloat, setOpeningFloat] = useState("0");
  const [openingNotes, setOpeningNotes] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiProblem | null>(null);
  const canOperate = session.user?.permissions.includes("MobilePOS.Till.Operate")
    && session.user.permissions.includes("Finance.CashTills.Operate");
  const canClose = session.user?.permissions.includes("MobilePOS.Till.Close")
    && session.user.permissions.includes("Finance.CashTills.Operate");

  const refresh = useCallback(async () => {
    if (session.status !== "ready") return;
    setBusy(true);
    setError(null);
    try {
      const installationId = await getInstallationId();
      const current = await mobileApi.getCurrentTillSession(installationId) ?? null;
      setTillSession(current);
      setReconciliation(current
        ? await mobileApi.getTillReconciliation(current.id, installationId)
        : null);
      setCloseSubmission(current
        ? await mobileApi.getTillCloseSubmission(current.id, installationId) ?? null
        : null);
    } catch (caught) {
      setError(problem(caught));
    } finally {
      setBusy(false);
    }
  }, [session.status]);

  useEffect(() => { void refresh(); }, [refresh]);

  const open = async () => {
    const amount = Number(openingFloat);
    if (!Number.isFinite(amount) || amount < 0) {
      setError(new ApiProblem("Opening float must be zero or a positive amount.", 0, "OPENING_FLOAT_INVALID"));
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const opened = await mobileApi.openTillSession({
        installationId: await getInstallationId(),
        openingFloatAmount: amount,
        openingNotes: openingNotes.trim() || undefined,
      });
      setTillSession(opened);
      setReconciliation(await mobileApi.getTillReconciliation(opened.id, await getInstallationId()));
      await session.refreshBootstrap();
    } catch (caught) {
      setError(problem(caught));
    } finally {
      setBusy(false);
    }
  };

  const submitClose = async () => {
    if (!tillSession || !session.user || !session.bootstrap) return;
    const countLines = denominations
      .map(denomination => ({ denomination, quantity: Math.trunc(Number(counts[String(denomination)] ?? "0")) }))
      .filter(line => Number.isFinite(line.quantity) && line.quantity > 0);
    if (countLines.length === 0) {
      setError(new ApiProblem("Enter at least one denomination count.", 0, "TILL_COUNT_REQUIRED"));
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const outbox = await loadSessionOutboxSummary(session.user, session.bootstrap);
      const pendingClientMutationIds = outbox.messages
        .filter(message => message.tillSessionId === tillSession.id)
        .map(message => message.clientMutationId);
      const result = await mobileApi.submitTillClose(tillSession.id, {
        installationId: await getInstallationId(),
        countLines,
        varianceReason: varianceReason.trim() || undefined,
        sessionRowVersion: tillSession.rowVersion,
        pendingClientMutationIds,
      });
      setTillSession(result.session);
      setCloseSubmission(result);
      setReconciliation(await mobileApi.getTillReconciliation(result.session.id, await getInstallationId()));
      await session.refreshBootstrap();
    } catch (caught) {
      setError(problem(caught));
    } finally {
      setBusy(false);
    }
  };

  if (session.status === "signedOut" || session.status === "mfaRequired") return <Redirect href="/login" />;
  if (session.status !== "ready" || !session.bootstrap) return <Redirect href="/" />;

  return (
    <ScrollView style={styles.page} contentContainerStyle={styles.content}>
      <View style={styles.header}>
        <Link href="/" asChild><Pressable accessibilityRole="button" style={styles.back}><Ionicons name="arrow-back" size={20} color={colors.navy} /></Pressable></Link>
        <View style={styles.headerText}><Text style={styles.title}>Till session</Text><Text style={styles.subtitle}>{session.bootstrap.store.code} · {session.bootstrap.till.tillNumber}</Text></View>
      </View>

      {error && <View style={styles.error}><Ionicons name="alert-circle-outline" size={19} color={colors.danger} /><Text style={styles.errorText}>{error.message}{error.correlationId ? ` Reference: ${error.correlationId}` : ""}</Text></View>}
      {busy && !tillSession ? <ActivityIndicator style={styles.loader} color={colors.blue} /> : tillSession ? (
        <>
          <View style={styles.hero}>
            <View><Text style={styles.label}>Active Finance custody</Text><Text style={styles.sessionNumber}>{tillSession.sessionNumber}</Text></View>
            <View style={styles.openPill}><Text style={styles.openText}>{statusLabel(tillSession.status)}</Text></View>
          </View>
          <View style={styles.grid}>
            <Metric label="Opening float" value={money(tillSession.openingFloatAmount, tillSession.currency)} />
            <Metric label="Expected cash" value={money(tillSession.expectedClosingAmount, tillSession.currency)} />
          </View>
          <View style={styles.card}>
            <Row label="Cashier" value={tillSession.cashierName} />
            <Row label="Business date" value={new Date(tillSession.businessDate).toLocaleDateString()} />
            <Row label="Opened" value={new Date(tillSession.openedAt).toLocaleString()} />
            <Row label="Canonical movements" value={money(tillSession.transactionMovementAmount, tillSession.currency)} />
            <Row label="Posted deposits" value={money(tillSession.depositedAmount, tillSession.currency)} />
          </View>
          {reconciliation && (
            <View style={styles.card}>
              <View style={styles.reconciliationHeader}>
                <View style={styles.reconciliationTitle}>
                  <Text style={styles.cardTitle}>Server reconciliation</Text>
                  <Text style={styles.note}>{reconciliation.completedSaleCount} completed sale(s), {reconciliation.offlineSaleCount} recorded offline</Text>
                </View>
                <Ionicons name={reconciliation.salesAndTendersBalance ? "checkmark-circle" : "alert-circle"} size={24} color={reconciliation.salesAndTendersBalance ? colors.success : colors.danger} />
              </View>
              <Row label="Sales total" value={money(reconciliation.salesTotal, tillSession.currency)} />
              <Row label="Tender total" value={money(reconciliation.tenderTotal, tillSession.currency)} />
              <Row label="Difference" value={money(reconciliation.salesTenderDifference, tillSession.currency)} />
              {reconciliation.tenders.map(tender => (
                <View key={tender.paymentMethodId} style={styles.tenderRow}>
                  <View style={styles.tenderDetail}>
                    <Text style={styles.tenderName}>{tender.paymentMethodName}</Text>
                    <Text style={styles.tenderMeta}>{tender.tenderCount} tender(s) · {tender.canonicalPaymentCount} Finance payment(s){tender.offlineTenderCount ? ` · ${tender.offlineTenderCount} offline` : ""}</Text>
                  </View>
                  <Text style={styles.tenderAmount}>{money(tender.amount, tillSession.currency)}</Text>
                </View>
              ))}
              {(reconciliation.pendingSaleCount > 0 || reconciliation.rejectedSaleCount > 0 || reconciliation.incompleteTenderCount > 0) && (
                <Text style={styles.warning}>Exceptions: {reconciliation.pendingSaleCount} pending sale(s), {reconciliation.rejectedSaleCount} rejected sale(s), {reconciliation.incompleteTenderCount} incomplete tender(s).</Text>
              )}
            </View>
          )}
          {closeSubmission ? (
            <View style={styles.card}>
              <Text style={styles.cardTitle}>Day-end submitted</Text>
              <Row label="Review state" value={closeStatusLabel(closeSubmission.status)} />
              <Row label="Counted cash" value={money(closeSubmission.session.countedClosingAmount, tillSession.currency)} />
              <Row label="Variance" value={money(closeSubmission.session.varianceAmount, tillSession.currency)} />
              <Row label="Pending sync evidence" value={String(closeSubmission.pendingMutationCount)} />
              <Text style={styles.note}>Finance must independently review the count. Final approval remains blocked until every retained mutation synchronizes or an authorized HQ reviewer records a reasoned exception.</Text>
            </View>
          ) : statusLabel(tillSession.status) === "Open" ? (
            <View style={styles.card}>
              <Text style={styles.cardTitle}>Declare closing cash</Text>
              <Text style={styles.note}>Enter the physical quantity of each denomination. The server calculates counted cash and variance from this evidence.</Text>
              <View style={styles.denominationList}>
                {denominations.map(denomination => (
                  <View key={denomination} style={styles.denominationRow}>
                    <Text style={styles.denominationLabel}>{money(denomination, tillSession.currency)}</Text>
                    <TextInput
                      accessibilityLabel={`${denomination} denomination quantity`}
                      keyboardType="number-pad"
                      value={counts[String(denomination)] ?? ""}
                      onChangeText={value => setCounts(current => ({ ...current, [String(denomination)]: value.replace(/[^0-9]/g, "") }))}
                      placeholder="0"
                      style={styles.quantityInput}
                    />
                  </View>
                ))}
              </View>
              <Row label="Counted total" value={money(countedTotal(counts), tillSession.currency)} />
              <Text style={styles.inputLabel}>Variance reason (required if non-zero)</Text>
              <TextInput accessibilityLabel="Variance reason" value={varianceReason} onChangeText={setVarianceReason} multiline style={[styles.input, styles.notes]} />
              <Pressable accessibilityRole="button" disabled={!canClose || busy} onPress={() => void submitClose()} style={[styles.primary, (!canClose || busy) && styles.disabled]}>
                {busy ? <ActivityIndicator color={colors.white} /> : <Text style={styles.primaryText}>Submit day end for review</Text>}
              </Pressable>
              {!canClose && <Text style={styles.permission}>Your role needs Mobile POS till-close and Finance cash-till operation permissions.</Text>}
            </View>
          ) : null}
          <Text style={styles.note}>Expected cash is calculated by Finance from the opening float, canonical custody entries, and posted deposit allocations.</Text>
        </>
      ) : (
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Open assigned till</Text>
          <Text style={styles.note}>Opening the session records personal custody of this physical till. Another operator cannot share it.</Text>
          <Text style={styles.inputLabel}>Opening float</Text>
          <TextInput accessibilityLabel="Opening float" keyboardType="decimal-pad" value={openingFloat} onChangeText={setOpeningFloat} style={styles.input} />
          <Text style={styles.inputLabel}>Opening notes (optional)</Text>
          <TextInput accessibilityLabel="Opening notes" value={openingNotes} onChangeText={setOpeningNotes} multiline style={[styles.input, styles.notes]} />
          <Pressable accessibilityRole="button" disabled={!canOperate || busy} onPress={() => void open()} style={[styles.primary, (!canOperate || busy) && styles.disabled]}>
            {busy ? <ActivityIndicator color={colors.white} /> : <Text style={styles.primaryText}>Open till session</Text>}
          </Pressable>
          {!canOperate && <Text style={styles.permission}>Your role needs both Mobile POS till operation and Finance cash-till operation permissions.</Text>}
        </View>
      )}
    </ScrollView>
  );
}

function problem(value: unknown): ApiProblem {
  return value instanceof ApiProblem ? value : new ApiProblem(value instanceof Error ? value.message : "The till session request failed.", 0, "TILL_SESSION_FAILED");
}

function money(value: number, currency: string): string {
  return `${currency} ${value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

function statusLabel(value: string | number): string {
  if (typeof value === "string") return value;
  return ({ 1: "Open", 2: "Pending review", 3: "Closed", 4: "Cancelled" } as Record<number, string>)[value] ?? `Status ${value}`;
}

function closeStatusLabel(value: string | number): string {
  if (typeof value === "string") return value;
  return ({ 1: "Ready for review", 2: "Pending sync", 3: "HQ exception resolved", 4: "Finalized" } as Record<number, string>)[value] ?? `Status ${value}`;
}

function countedTotal(counts: Record<string, string>): number {
  return denominations.reduce((sum, denomination) => {
    const quantity = Math.trunc(Number(counts[String(denomination)] ?? "0"));
    return sum + (Number.isFinite(quantity) && quantity > 0 ? denomination * quantity : 0);
  }, 0);
}

function Metric({ label, value }: { label: string; value: string }) {
  return <View style={styles.metric}><Text style={styles.metricLabel}>{label}</Text><Text style={styles.metricValue}>{value}</Text></View>;
}

function Row({ label, value }: { label: string; value: string }) {
  return <View style={styles.row}><Text style={styles.rowLabel}>{label}</Text><Text style={styles.rowValue}>{value}</Text></View>;
}

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: colors.background },
  content: { paddingHorizontal: 20, paddingTop: 52, paddingBottom: 42 },
  header: { flexDirection: "row", alignItems: "center" },
  back: { width: 42, height: 42, borderRadius: 13, alignItems: "center", justifyContent: "center", backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  headerText: { flex: 1, marginLeft: 13 },
  title: { color: colors.ink, fontSize: 22, fontWeight: "700" },
  subtitle: { marginTop: 2, color: colors.muted, fontSize: 11 },
  loader: { marginTop: 50 },
  error: { marginTop: 16, flexDirection: "row", gap: 9, padding: 13, borderRadius: 11, backgroundColor: colors.dangerBg },
  errorText: { flex: 1, color: colors.danger, fontSize: 12, lineHeight: 18 },
  hero: { marginTop: 20, padding: 18, flexDirection: "row", justifyContent: "space-between", alignItems: "center", borderRadius: 16, backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  label: { color: colors.muted, fontSize: 11 },
  sessionNumber: { marginTop: 5, color: colors.ink, fontSize: 19, fontWeight: "800" },
  openPill: { paddingHorizontal: 10, paddingVertical: 6, borderRadius: 999, backgroundColor: colors.successBg },
  openText: { color: colors.success, fontSize: 10, fontWeight: "800" },
  grid: { marginTop: 12, flexDirection: "row", gap: 10 },
  metric: { flex: 1, padding: 15, borderRadius: 14, backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  metricLabel: { color: colors.muted, fontSize: 10 },
  metricValue: { marginTop: 5, color: colors.ink, fontSize: 15, fontWeight: "700" },
  card: { marginTop: 18, padding: 18, borderRadius: 16, backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  cardTitle: { color: colors.ink, fontSize: 18, fontWeight: "700" },
  note: { marginTop: 12, color: colors.slate, fontSize: 12, lineHeight: 18 },
  row: { minHeight: 42, flexDirection: "row", justifyContent: "space-between", alignItems: "center", borderBottomWidth: 1, borderBottomColor: "#F2F4F7" },
  rowLabel: { color: colors.muted, fontSize: 11 },
  rowValue: { maxWidth: "62%", color: colors.ink, fontSize: 12, fontWeight: "600", textAlign: "right" },
  reconciliationHeader: { flexDirection: "row", alignItems: "flex-start", justifyContent: "space-between", gap: 10 },
  reconciliationTitle: { flex: 1 },
  tenderRow: { minHeight: 54, flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: 12, borderBottomWidth: 1, borderBottomColor: "#F2F4F7" },
  tenderDetail: { flex: 1 },
  tenderName: { color: colors.ink, fontSize: 12, fontWeight: "700" },
  tenderMeta: { marginTop: 3, color: colors.muted, fontSize: 9 },
  tenderAmount: { color: colors.ink, fontSize: 12, fontWeight: "700" },
  warning: { marginTop: 12, color: colors.warning, fontSize: 11, lineHeight: 16 },
  denominationList: { marginTop: 12, gap: 8 },
  denominationRow: { flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: 12 },
  denominationLabel: { color: colors.ink, fontSize: 12, fontWeight: "600" },
  quantityInput: { width: 92, minHeight: 40, paddingHorizontal: 12, borderRadius: 10, borderWidth: 1, borderColor: "#D0D5DD", backgroundColor: colors.white, color: colors.ink, textAlign: "right" },
  inputLabel: { marginTop: 18, marginBottom: 7, color: colors.navy, fontSize: 12, fontWeight: "700" },
  input: { minHeight: 48, paddingHorizontal: 13, borderRadius: 11, borderWidth: 1, borderColor: "#D0D5DD", backgroundColor: colors.white, color: colors.ink, fontSize: 14 },
  notes: { minHeight: 82, paddingTop: 12, textAlignVertical: "top" },
  primary: { marginTop: 20, minHeight: 50, alignItems: "center", justifyContent: "center", borderRadius: 12, backgroundColor: colors.blue },
  primaryText: { color: colors.white, fontSize: 14, fontWeight: "700" },
  disabled: { opacity: 0.45 },
  permission: { marginTop: 10, color: colors.warning, fontSize: 10, lineHeight: 15 },
});
