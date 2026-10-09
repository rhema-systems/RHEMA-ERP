import { Ionicons } from "@expo/vector-icons";
import { Link, Redirect } from "expo-router";
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from "react-native";
import { environmentColor } from "@/src/config/environment";
import { offlineGrantMinutesRemaining } from "@/src/offline/grant";
import { useSession } from "@/src/session/session-context";
import { colors } from "@/src/ui/theme";

export default function HomeScreen() {
  const session = useSession();
  const canRequestOffline = Boolean(
    session.bootstrap?.currentTillSessionId
    && session.bootstrap.offlinePolicy
    && session.user?.permissions.includes("MobilePOS.Offline.Use")
    && session.user.permissions.includes("MobilePOS.Till.Operate"),
  );

  if (session.status === "signedOut" || session.status === "mfaRequired") return <Redirect href="/login" />;
  if (session.status === "initializing") {
    return (
      <View style={styles.loadingPage}>
        <ActivityIndicator size="large" color={colors.blue} />
        <Text style={styles.loadingText}>Checking your RHEMA mobile assignment…</Text>
      </View>
    );
  }

  return (
    <ScrollView style={styles.page} contentContainerStyle={styles.content}>
      <View style={styles.topRow}>
        <View>
          <Text style={styles.eyebrow}>RHEMA FIELD POS</Text>
          <Text style={styles.title}>Good day, {session.user?.firstName || session.user?.username}</Text>
        </View>
        <Link href="/account" asChild>
          <Pressable accessibilityRole="button" accessibilityLabel="Open account" style={styles.accountButton}>
            <Ionicons name="person-outline" size={22} color={colors.navy} />
          </Pressable>
        </Link>
      </View>

      <View style={[styles.environmentBadge, { backgroundColor: `${environmentColor(session.profile.environment)}14` }]}>
        <View style={[styles.dot, { backgroundColor: environmentColor(session.profile.environment) }]} />
        <Text style={[styles.environmentText, { color: environmentColor(session.profile.environment) }]}>{session.profile.environment}</Text>
        <Text numberOfLines={1} style={styles.environmentUrl}>{session.profile.apiBaseUrl}</Text>
      </View>

      {session.status === "enrollmentPending" && (
        <StatusCard
          icon="phone-portrait-outline"
          tone="warning"
          title="Device approval required"
          message="This installation is registered. An authorized administrator must assign it to an active store and till."
          detail={session.pendingDevice ? `${session.pendingDevice.deviceName} · ${String(session.pendingDevice.status)}` : undefined}
          actionLabel="Check approval"
          onAction={() => void session.refreshBootstrap()}
        />
      )}

      {session.status === "blocked" && (
        <StatusCard
          icon="shield-outline"
          tone="danger"
          title="Mobile access unavailable"
          message={session.error?.message ?? "Your account, store assignment, till, or device is not ready for Mobile POS."}
          detail={session.error?.correlationId ? `Reference: ${session.error.correlationId}` : undefined}
          actionLabel="Retry"
          onAction={() => void session.refreshBootstrap()}
        />
      )}

      {session.status === "ready" && session.bootstrap && (
        <>
          <View style={styles.assignmentCard}>
            <View style={styles.assignmentIcon}><Ionicons name="storefront-outline" size={25} color={colors.blue} /></View>
            <View style={styles.assignmentMain}>
              <Text style={styles.assignmentLabel}>Assigned store</Text>
              <Text style={styles.assignmentName}>{session.bootstrap.store.name}</Text>
              <Text style={styles.assignmentCode}>{session.bootstrap.store.code}</Text>
            </View>
            <View style={styles.activePill}><Text style={styles.activeText}>Active</Text></View>
          </View>

          <View style={styles.grid}>
            <Metric icon="calculator-outline" label="Till" value={session.bootstrap.till.tillNumber} />
            <Metric icon="cash-outline" label="Currency" value={session.bootstrap.store.currencyCode} />
          </View>

          <View style={styles.infoCard}>
            <Text style={styles.sectionTitle}>Walk-in sales customer</Text>
            <Text style={styles.customerName}>{session.bootstrap.store.defaultWalkInCustomerName}</Text>
            <Text style={styles.customerCode}>{session.bootstrap.store.defaultWalkInCustomerCode}</Text>
            <Text style={styles.infoText}>Used automatically for walk-in sales. Search and choose another approved customer when required.</Text>
          </View>

          {session.bootstrap.currentTillSessionId
            && session.user?.permissions.includes("MobilePOS.Till.Operate")
            && session.user.permissions.includes("MobilePOS.Invoice.Create")
            && session.user.permissions.includes("Finance.AR.Invoices.Create") ? (
            <Link href="/sale" asChild>
              <Pressable accessibilityRole="button" style={styles.saleButton}>
                <View style={styles.saleIcon}><Ionicons name="cart-outline" size={23} color={colors.white} /></View>
                <View style={styles.featureMain}>
                  <Text style={styles.saleTitle}>Start a sale</Text>
                  <Text style={styles.saleText}>Search the governed catalogue and calculate the total with RHEMA Finance.</Text>
                </View>
                <Ionicons name="arrow-forward" size={20} color={colors.white} />
              </Pressable>
            </Link>
          ) : (
            <View style={[styles.featureButton, styles.buttonDisabled]}>
              <View style={styles.featureIcon}><Ionicons name="cart-outline" size={22} color={colors.blue} /></View>
              <View style={styles.featureMain}>
                <Text style={styles.featureTitle}>Start a sale</Text>
                <Text style={styles.featureText}>{session.bootstrap.currentTillSessionId ? "Your role is missing a required Mobile POS or Finance invoice permission." : "Open your assigned cashier till session first."}</Text>
              </View>
            </View>
          )}

          {session.user?.permissions.includes("MobilePOS.Customer.View") ? (
            <Link href="/customers" asChild>
              <Pressable accessibilityRole="button" style={styles.featureButton}>
                <View style={styles.featureIcon}><Ionicons name="people-outline" size={22} color={colors.blue} /></View>
                <View style={styles.featureMain}>
                  <Text style={styles.featureTitle}>Customers and balances</Text>
                  <Text style={styles.featureText}>Use the store default or find another approved customer.</Text>
                </View>
                <Ionicons name="chevron-forward" size={19} color={colors.muted} />
              </Pressable>
            </Link>
          ) : (
            <View style={[styles.featureButton, styles.buttonDisabled]}>
              <View style={styles.featureIcon}><Ionicons name="people-outline" size={22} color={colors.blue} /></View>
              <View style={styles.featureMain}>
                <Text style={styles.featureTitle}>Customers and balances</Text>
                <Text style={styles.featureText}>Your role needs the View Mobile POS Customers permission.</Text>
              </View>
            </View>
          )}

          <Link href="/sync" asChild>
            <Pressable accessibilityRole="button" style={styles.featureButton}>
              <View style={styles.featureIcon}><Ionicons name="sync-outline" size={22} color={colors.blue} /></View>
              <View style={styles.featureMain}>
                <Text style={styles.featureTitle}>Sync & exceptions</Text>
                <Text style={styles.featureText}>Submit pending device work and review server decisions.</Text>
              </View>
              <Ionicons name="chevron-forward" size={19} color={colors.muted} />
            </Pressable>
          </Link>

          <View style={styles.infoCard}>
            <View style={styles.sectionRow}>
              <Text style={styles.sectionTitle}>Offline authorization</Text>
              <View style={session.offlineGrant ? styles.openPill : styles.closedPill}>
                <Text style={session.offlineGrant ? styles.openText : styles.closedText}>
                  {session.offlineGrant ? "Ready" : "Not active"}
                </Text>
              </View>
            </View>
            {session.offlineGrant ? (
              <>
                <Text style={styles.grantHeadline}>
                  {offlineGrantMinutesRemaining(session.offlineGrant)} minutes remaining
                </Text>
                <Text style={styles.infoText}>
                  {session.offlineGrant.policy.allowedCommandTypes.join(", ")} · {session.offlineGrant.policy.allowedPaymentMethods.length} offline tender(s)
                </Text>
                <Text style={styles.grantLimit}>
                  {formatGrantLimits(session.offlineGrant.policy.currencyCode, session.offlineGrant.policy.maximumTransactionAmount, session.offlineGrant.policy.maximumAggregateAmount, session.offlineGrant.policy.maximumTransactionCount)}
                </Text>
              </>
            ) : (
              <Text style={styles.infoText}>{offlineUnavailableReason(session)}</Text>
            )}
            {session.offlineGrantError && (
              <Text accessibilityRole="alert" style={styles.inlineError}>
                {session.offlineGrantError.message}
                {session.offlineGrantError.correlationId ? ` Reference: ${session.offlineGrantError.correlationId}` : ""}
              </Text>
            )}
            <Pressable
              accessibilityRole="button"
              disabled={!canRequestOffline || session.offlineGrantBusy}
              onPress={() => void session.requestOfflineGrant().catch(() => undefined)}
              style={[styles.grantButton, (!canRequestOffline || session.offlineGrantBusy) && styles.buttonDisabled]}
            >
              {session.offlineGrantBusy
                ? <ActivityIndicator size="small" color={colors.white} />
                : <Ionicons name="cloud-offline-outline" size={18} color={colors.white} />}
              <Text style={styles.grantButtonText}>{session.offlineGrant ? "Renew authorization" : "Authorize offline use"}</Text>
            </Pressable>
          </View>

          <View style={styles.infoCard}>
            <View style={styles.sectionRow}>
              <Text style={styles.sectionTitle}>Till session</Text>
              <View style={session.bootstrap.currentTillSessionId ? styles.openPill : styles.closedPill}>
                <Text style={session.bootstrap.currentTillSessionId ? styles.openText : styles.closedText}>
                  {session.bootstrap.currentTillSessionId ? "Open" : "Not open"}
                </Text>
              </View>
            </View>
            <Text style={styles.infoText}>
              {session.bootstrap.currentTillSessionId
                ? "Your active cashier session is recognized by Finance custody controls."
                : "Open the assigned physical till before recording sales or requesting offline authorization."}
            </Text>
            <Link href="/till-session" asChild>
              <Pressable accessibilityRole="button" style={styles.cardAction}>
                <Text style={styles.cardActionText}>{session.bootstrap.currentTillSessionId ? "View till session" : "Open till session"}</Text>
                <Ionicons name="chevron-forward" size={17} color={colors.blue} />
              </Pressable>
            </Link>
          </View>

          <View style={styles.infoCard}>
            <Text style={styles.sectionTitle}>Accepted tenders</Text>
            {session.bootstrap.till.paymentMethods.length === 0 ? (
              <Text style={styles.infoText}>No payment methods are configured for this till.</Text>
            ) : session.bootstrap.till.paymentMethods.map(method => (
              <View key={method.paymentMethodId} style={styles.tenderRow}>
                <Text style={styles.tenderName}>{method.name}</Text>
                <Text style={styles.tenderMode}>{method.allowOffline ? "Online + offline" : "Online"}</Text>
              </View>
            ))}
          </View>

          <Pressable accessibilityRole="button" onPress={() => void session.refreshBootstrap()} style={styles.secondaryButton}>
            <Ionicons name="refresh-outline" size={18} color={colors.blue} />
            <Text style={styles.secondaryText}>Refresh assignment</Text>
          </Pressable>
        </>
      )}
    </ScrollView>
  );
}

function offlineUnavailableReason(session: ReturnType<typeof useSession>): string {
  if (!session.user?.permissions.includes("MobilePOS.Offline.Use") || !session.user.permissions.includes("MobilePOS.Till.Operate")) {
    return "Your assigned role does not authorize offline Mobile POS operation.";
  }
  if (!session.bootstrap?.offlinePolicy) return "No active offline policy is assigned to this store.";
  if (!session.bootstrap.currentTillSessionId) return "Open your assigned cashier till session before requesting offline authorization.";
  return "Request a time-limited authorization before working without a connection.";
}

function formatGrantLimits(currency: string, transaction?: number, aggregate?: number, count?: number): string {
  const limits = [
    transaction != null ? `${currency} ${transaction.toLocaleString()} per transaction` : null,
    aggregate != null ? `${currency} ${aggregate.toLocaleString()} total` : null,
    count != null ? `${count} transaction${count === 1 ? "" : "s"}` : null,
  ].filter((value): value is string => Boolean(value));
  return limits.length > 0 ? `Limits: ${limits.join(" · ")}` : "No amount or transaction-count limit is configured.";
}

function Metric({ icon, label, value }: { icon: React.ComponentProps<typeof Ionicons>["name"]; label: string; value: string }) {
  return (
    <View style={styles.metric}>
      <Ionicons name={icon} size={22} color={colors.blue} />
      <Text style={styles.metricLabel}>{label}</Text>
      <Text style={styles.metricValue}>{value}</Text>
    </View>
  );
}

function StatusCard({ icon, tone, title, message, detail, actionLabel, onAction }: {
  icon: React.ComponentProps<typeof Ionicons>["name"];
  tone: "warning" | "danger";
  title: string;
  message: string;
  detail?: string;
  actionLabel: string;
  onAction: () => void;
}) {
  const danger = tone === "danger";
  return (
    <View style={[styles.statusCard, { backgroundColor: danger ? colors.dangerBg : colors.warningBg }]}>
      <Ionicons name={icon} size={30} color={danger ? colors.danger : colors.warning} />
      <Text style={styles.statusTitle}>{title}</Text>
      <Text style={styles.statusMessage}>{message}</Text>
      {detail && <Text style={styles.statusDetail}>{detail}</Text>}
      <Pressable accessibilityRole="button" onPress={onAction} style={styles.statusAction}>
        <Text style={styles.statusActionText}>{actionLabel}</Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: colors.background },
  content: { paddingHorizontal: 20, paddingTop: 54, paddingBottom: 38 },
  loadingPage: { flex: 1, alignItems: "center", justifyContent: "center", backgroundColor: colors.background, padding: 28 },
  loadingText: { marginTop: 16, color: colors.slate, fontSize: 14 },
  topRow: { flexDirection: "row", alignItems: "center", justifyContent: "space-between" },
  eyebrow: { color: colors.blue, fontSize: 11, fontWeight: "700", letterSpacing: 1.8 },
  title: { marginTop: 5, maxWidth: 285, color: colors.ink, fontSize: 23, lineHeight: 29, fontWeight: "700" },
  accountButton: { width: 44, height: 44, borderRadius: 14, backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0", alignItems: "center", justifyContent: "center" },
  environmentBadge: { marginTop: 18, minHeight: 36, borderRadius: 10, flexDirection: "row", alignItems: "center", paddingHorizontal: 12 },
  dot: { width: 7, height: 7, borderRadius: 4 },
  environmentText: { marginLeft: 7, fontSize: 11, fontWeight: "700" },
  environmentUrl: { flex: 1, marginLeft: 10, color: colors.muted, fontSize: 11 },
  assignmentCard: { marginTop: 20, flexDirection: "row", alignItems: "center", borderRadius: 18, padding: 17, backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  assignmentIcon: { width: 48, height: 48, borderRadius: 14, backgroundColor: colors.paleBlue, alignItems: "center", justifyContent: "center" },
  assignmentMain: { flex: 1, marginLeft: 13 },
  assignmentLabel: { color: colors.muted, fontSize: 12 },
  assignmentName: { marginTop: 2, color: colors.ink, fontSize: 17, fontWeight: "700" },
  assignmentCode: { marginTop: 2, color: colors.slate, fontSize: 12 },
  activePill: { paddingHorizontal: 9, paddingVertical: 5, borderRadius: 999, backgroundColor: colors.successBg },
  activeText: { color: colors.success, fontSize: 11, fontWeight: "700" },
  grid: { marginTop: 12, flexDirection: "row", gap: 12 },
  metric: { flex: 1, minHeight: 126, padding: 16, borderRadius: 16, backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  metricLabel: { marginTop: 14, color: colors.muted, fontSize: 12 },
  metricValue: { marginTop: 4, color: colors.ink, fontSize: 18, fontWeight: "700" },
  infoCard: { marginTop: 12, padding: 17, borderRadius: 16, backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  sectionRow: { flexDirection: "row", alignItems: "center", justifyContent: "space-between" },
  sectionTitle: { color: colors.ink, fontSize: 14, fontWeight: "700" },
  customerName: { marginTop: 12, color: colors.navy, fontSize: 18, fontWeight: "700" },
  customerCode: { marginTop: 2, color: colors.slate, fontSize: 12 },
  infoText: { marginTop: 10, color: colors.slate, fontSize: 13, lineHeight: 19 },
  grantHeadline: { marginTop: 12, color: colors.success, fontSize: 16, fontWeight: "700" },
  grantLimit: { marginTop: 6, color: colors.muted, fontSize: 12, lineHeight: 18 },
  inlineError: { marginTop: 10, padding: 10, borderRadius: 9, backgroundColor: colors.dangerBg, color: colors.danger, fontSize: 12, lineHeight: 17 },
  grantButton: { marginTop: 14, minHeight: 46, flexDirection: "row", gap: 8, borderRadius: 11, backgroundColor: colors.blue, alignItems: "center", justifyContent: "center" },
  grantButtonText: { color: colors.white, fontSize: 13, fontWeight: "700" },
  buttonDisabled: { opacity: 0.45 },
  featureButton: { marginTop: 12, minHeight: 76, padding: 14, flexDirection: "row", alignItems: "center", borderRadius: 16, backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  saleButton: { marginTop: 12, minHeight: 82, padding: 14, flexDirection: "row", alignItems: "center", borderRadius: 16, backgroundColor: colors.blue },
  saleIcon: { width: 44, height: 44, borderRadius: 13, alignItems: "center", justifyContent: "center", backgroundColor: "rgba(255,255,255,0.16)" },
  saleTitle: { color: colors.white, fontSize: 15, fontWeight: "700" },
  saleText: { marginTop: 4, color: "#DCE6FF", fontSize: 12, lineHeight: 17 },
  featureIcon: { width: 44, height: 44, borderRadius: 13, alignItems: "center", justifyContent: "center", backgroundColor: colors.paleBlue },
  featureMain: { flex: 1, marginLeft: 12, marginRight: 8 },
  featureTitle: { color: colors.ink, fontSize: 14, fontWeight: "700" },
  featureText: { marginTop: 4, color: colors.slate, fontSize: 12, lineHeight: 17 },
  openPill: { paddingHorizontal: 9, paddingVertical: 5, borderRadius: 999, backgroundColor: colors.successBg },
  openText: { color: colors.success, fontSize: 11, fontWeight: "700" },
  closedPill: { paddingHorizontal: 9, paddingVertical: 5, borderRadius: 999, backgroundColor: colors.warningBg },
  closedText: { color: colors.warning, fontSize: 11, fontWeight: "700" },
  tenderRow: { marginTop: 11, flexDirection: "row", justifyContent: "space-between", borderTopWidth: 1, borderTopColor: "#F2F4F7", paddingTop: 11 },
  tenderName: { color: colors.ink, fontSize: 13, fontWeight: "600" },
  tenderMode: { color: colors.muted, fontSize: 12 },
  cardAction: { marginTop: 14, minHeight: 42, paddingHorizontal: 12, flexDirection: "row", alignItems: "center", justifyContent: "space-between", borderRadius: 10, backgroundColor: colors.paleBlue },
  cardActionText: { color: colors.blue, fontSize: 12, fontWeight: "700" },
  secondaryButton: { marginTop: 18, minHeight: 48, flexDirection: "row", gap: 8, borderRadius: 12, borderWidth: 1, borderColor: "#B2CCFF", backgroundColor: colors.white, alignItems: "center", justifyContent: "center" },
  secondaryText: { color: colors.blue, fontSize: 14, fontWeight: "700" },
  statusCard: { marginTop: 24, padding: 20, borderRadius: 18 },
  statusTitle: { marginTop: 14, color: colors.ink, fontSize: 20, fontWeight: "700" },
  statusMessage: { marginTop: 8, color: colors.slate, fontSize: 14, lineHeight: 21 },
  statusDetail: { marginTop: 10, color: colors.muted, fontSize: 12 },
  statusAction: { marginTop: 18, minHeight: 46, borderRadius: 11, backgroundColor: colors.white, alignItems: "center", justifyContent: "center" },
  statusActionText: { color: colors.blue, fontSize: 14, fontWeight: "700" },
});
