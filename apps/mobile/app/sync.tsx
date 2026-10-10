import { Ionicons } from "@expo/vector-icons";
import { Link, Redirect } from "expo-router";
import { useCallback, useEffect, useState } from "react";
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from "react-native";
import { ApiProblem } from "@/src/api/client";
import { loadSessionOutboxSummary, synchronizeSessionOutbox, type OutboxSummary } from "@/src/offline/sync-runtime";
import { useSession } from "@/src/session/session-context";
import { colors } from "@/src/ui/theme";

export default function SyncScreen() {
  const session = useSession();
  const [summary, setSummary] = useState<OutboxSummary | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiProblem | null>(null);
  const user = session.user;
  const bootstrap = session.bootstrap;

  const refresh = useCallback(async (synchronize: boolean) => {
    if (!user || !bootstrap) return;
    setBusy(true);
    setError(null);
    try {
      setSummary(synchronize
        ? await synchronizeSessionOutbox(user, bootstrap)
        : await loadSessionOutboxSummary(user, bootstrap));
    } catch (caught) {
      setError(caught instanceof ApiProblem
        ? caught
        : new ApiProblem(caught instanceof Error ? caught.message : "The offline queue could not be read.", 0, "OUTBOX_READ_FAILED"));
    } finally {
      setBusy(false);
    }
  }, [bootstrap, user]);

  useEffect(() => {
    void refresh(false);
  }, [refresh]);

  if (session.status === "signedOut" || session.status === "mfaRequired") return <Redirect href="/login" />;
  if (session.status !== "ready" || !bootstrap || !user) return <Redirect href="/" />;

  return (
    <ScrollView style={styles.page} contentContainerStyle={styles.content}>
      <View style={styles.header}>
        <Link href="/" asChild>
          <Pressable accessibilityRole="button" style={styles.backButton}>
            <Ionicons name="arrow-back" size={20} color={colors.navy} />
          </Pressable>
        </Link>
        <View style={styles.headerText}>
          <Text style={styles.title}>Sync & exceptions</Text>
          <Text style={styles.subtitle}>{bootstrap.store.code} · {bootstrap.till.tillNumber}</Text>
        </View>
      </View>

      <View style={styles.metrics}>
        <Metric label="Pending" value={summary?.pending ?? 0} tone={colors.blue} />
        <Metric label="Rejected" value={summary?.rejected ?? 0} tone={colors.danger} />
        <Metric label="Review" value={(summary?.conflict ?? 0) + (summary?.manualReview ?? 0)} tone={colors.warning} />
      </View>

      {error && (
        <View style={styles.errorBox}>
          <Ionicons name="alert-circle-outline" size={19} color={colors.danger} />
          <Text style={styles.errorText}>{error.message}{error.correlationId ? ` Reference: ${error.correlationId}` : ""}</Text>
        </View>
      )}

      <Pressable
        accessibilityRole="button"
        disabled={busy}
        onPress={() => void refresh(true)}
        style={[styles.syncButton, busy && styles.disabled]}
      >
        {busy ? <ActivityIndicator color={colors.white} /> : <>
          <Ionicons name="sync" size={20} color={colors.white} />
          <Text style={styles.syncButtonText}>Sync pending work</Text>
        </>}
      </Pressable>

      <Text style={styles.sectionTitle}>Device work queue</Text>
      {!busy && summary?.messages.length === 0 && (
        <View style={styles.emptyCard}>
          <Ionicons name="checkmark-circle-outline" size={30} color={colors.success} />
          <Text style={styles.emptyTitle}>No unresolved device work</Text>
          <Text style={styles.emptyText}>All queued commands have reached a canonical server decision.</Text>
        </View>
      )}
      {summary?.messages.map(message => (
        <View key={message.clientMutationId} style={styles.messageCard}>
          <View style={styles.rowBetween}>
            <View style={styles.grow}>
              <Text style={styles.reference}>{message.localReference}</Text>
              <Text style={styles.command}>{message.commandType} · attempt {message.attemptCount}</Text>
            </View>
            <StatePill state={message.state} />
          </View>
          {message.errorDetail && <Text style={styles.errorDetail}>{message.errorDetail}</Text>}
          {message.errorCode && <Text selectable style={styles.errorCode}>{message.errorCode}</Text>}
          {message.retryAfterUtc && <Text style={styles.retryText}>Next automatic retry: {new Date(message.retryAfterUtc).toLocaleString()}</Text>}
          {["Rejected", "Conflict", "ManualReview"].includes(message.state) && (
            <Text style={styles.reviewText}>This item is retained for review. It will not be reposted automatically.</Text>
          )}
        </View>
      ))}
    </ScrollView>
  );
}

function Metric({ label, value, tone }: { label: string; value: number; tone: string }) {
  return <View style={styles.metric}><Text style={[styles.metricValue, { color: tone }]}>{value}</Text><Text style={styles.metricLabel}>{label}</Text></View>;
}

function StatePill({ state }: { state: string }) {
  const tone = state === "Rejected" ? colors.danger : ["Conflict", "ManualReview"].includes(state) ? colors.warning : colors.blue;
  return <View style={[styles.pill, { backgroundColor: `${tone}14` }]}><Text style={[styles.pillText, { color: tone }]}>{state}</Text></View>;
}

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: colors.background },
  content: { paddingHorizontal: 20, paddingTop: 52, paddingBottom: 42 },
  header: { flexDirection: "row", alignItems: "center" },
  backButton: { width: 42, height: 42, borderRadius: 13, alignItems: "center", justifyContent: "center", backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  headerText: { flex: 1, marginLeft: 13 },
  title: { color: colors.ink, fontSize: 22, fontWeight: "700" },
  subtitle: { marginTop: 2, color: colors.muted, fontSize: 11 },
  metrics: { marginTop: 22, flexDirection: "row", gap: 9 },
  metric: { flex: 1, paddingVertical: 14, alignItems: "center", borderRadius: 13, backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  metricValue: { fontSize: 22, fontWeight: "800" },
  metricLabel: { marginTop: 2, color: colors.muted, fontSize: 10, fontWeight: "700" },
  errorBox: { marginTop: 14, flexDirection: "row", gap: 9, padding: 13, borderRadius: 11, backgroundColor: colors.dangerBg },
  errorText: { flex: 1, color: colors.danger, fontSize: 12, lineHeight: 18 },
  syncButton: { marginTop: 16, minHeight: 50, flexDirection: "row", gap: 8, alignItems: "center", justifyContent: "center", borderRadius: 12, backgroundColor: colors.blue },
  syncButtonText: { color: colors.white, fontSize: 14, fontWeight: "700" },
  disabled: { opacity: 0.5 },
  sectionTitle: { marginTop: 25, marginBottom: 10, color: colors.ink, fontSize: 15, fontWeight: "700" },
  emptyCard: { alignItems: "center", padding: 24, borderRadius: 15, backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  emptyTitle: { marginTop: 10, color: colors.ink, fontSize: 14, fontWeight: "700" },
  emptyText: { marginTop: 4, color: colors.muted, fontSize: 11, lineHeight: 16, textAlign: "center" },
  messageCard: { marginBottom: 10, padding: 14, borderRadius: 14, backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  rowBetween: { flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: 10 },
  grow: { flex: 1 },
  reference: { color: colors.ink, fontSize: 13, fontWeight: "700" },
  command: { marginTop: 3, color: colors.muted, fontSize: 10 },
  pill: { paddingHorizontal: 9, paddingVertical: 5, borderRadius: 999 },
  pillText: { fontSize: 9, fontWeight: "800" },
  errorDetail: { marginTop: 11, color: colors.slate, fontSize: 11, lineHeight: 16 },
  errorCode: { marginTop: 5, color: colors.danger, fontSize: 9, fontWeight: "700" },
  retryText: { marginTop: 8, color: colors.muted, fontSize: 9 },
  reviewText: { marginTop: 9, color: colors.warning, fontSize: 10, lineHeight: 15 },
});
