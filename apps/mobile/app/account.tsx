import { Ionicons } from "@expo/vector-icons";
import { Link, Redirect } from "expo-router";
import { useState } from "react";
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from "react-native";
import { useSession } from "@/src/session/session-context";
import { colors } from "@/src/ui/theme";

export default function AccountScreen() {
  const session = useSession();
  const [busyTenant, setBusyTenant] = useState<string | null>(null);

  if (session.status === "signedOut" || session.status === "mfaRequired") return <Redirect href="/login" />;

  const switchTenant = async (code: string) => {
    setBusyTenant(code);
    try { await session.switchTenant(code); } finally { setBusyTenant(null); }
  };

  return (
    <ScrollView style={styles.page} contentContainerStyle={styles.content}>
      <View style={styles.header}>
        <Link href="/" asChild>
          <Pressable accessibilityRole="button" accessibilityLabel="Back" style={styles.backButton}>
            <Ionicons name="arrow-back" size={22} color={colors.navy} />
          </Pressable>
        </Link>
        <Text style={styles.title}>Account & access</Text>
      </View>

      <View style={styles.profileCard}>
        <View style={styles.avatar}><Text style={styles.avatarText}>{(session.user?.firstName || session.user?.username || "R").charAt(0).toUpperCase()}</Text></View>
        <Text style={styles.name}>{[session.user?.firstName, session.user?.lastName].filter(Boolean).join(" ") || session.user?.username}</Text>
        <Text style={styles.email}>{session.user?.email}</Text>
        <Text style={styles.tenant}>{session.user?.currentTenantName ?? session.user?.currentTenantCode}</Text>
      </View>

      <Text style={styles.sectionLabel}>Accessible tenants</Text>
      <View style={styles.card}>
        {(session.user?.accessibleTenants ?? []).map(tenant => {
          const current = tenant.tenantId === session.user?.currentTenantId;
          return (
            <Pressable
              key={tenant.tenantId}
              accessibilityRole="button"
              disabled={current || busyTenant !== null}
              onPress={() => void switchTenant(tenant.tenantCode)}
              style={[styles.tenantRow, current && styles.currentTenant]}
            >
              <View style={styles.tenantIcon}><Ionicons name="business-outline" size={20} color={colors.blue} /></View>
              <View style={styles.tenantText}>
                <Text style={styles.tenantName}>{tenant.tenantName}</Text>
                <Text style={styles.tenantCode}>{tenant.tenantCode} · {tenant.accessLevel}</Text>
              </View>
              {busyTenant === tenant.tenantCode ? <ActivityIndicator color={colors.blue} /> : current ? <Text style={styles.currentText}>Current</Text> : <Ionicons name="chevron-forward" size={18} color={colors.muted} />}
            </Pressable>
          );
        })}
      </View>

      <Text style={styles.sectionLabel}>Device</Text>
      <View style={styles.card}>
        <InfoRow label="Status" value={session.pendingDevice ? String(session.pendingDevice.status) : session.bootstrap ? "Active" : "Unavailable"} />
        <InfoRow label="Store" value={session.bootstrap?.store.name ?? session.pendingDevice?.storeName ?? "Not assigned"} />
        <InfoRow label="Till" value={session.bootstrap?.till.tillNumber ?? session.pendingDevice?.tillNumber ?? "Not assigned"} />
        <InfoRow label="Printer" value={session.bootstrap?.device.printerAdapterKey ?? session.pendingDevice?.printerAdapterKey ?? "system-print"} />
        <InfoRow label="Scanner" value={session.bootstrap?.device.scannerAdapterKey ?? session.pendingDevice?.scannerAdapterKey ?? "camera-manual"} last />
      </View>

      <Pressable accessibilityRole="button" onPress={() => void session.signOut()} style={styles.logoutButton}>
        <Ionicons name="log-out-outline" size={20} color={colors.danger} />
        <Text style={styles.logoutText}>Sign out</Text>
      </Pressable>
    </ScrollView>
  );
}

function InfoRow({ label, value, last = false }: { label: string; value: string; last?: boolean }) {
  return (
    <View style={[styles.infoRow, last && styles.lastRow]}>
      <Text style={styles.infoLabel}>{label}</Text><Text style={styles.infoValue}>{value}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: colors.background },
  content: { paddingHorizontal: 20, paddingTop: 52, paddingBottom: 36 },
  header: { flexDirection: "row", alignItems: "center" },
  backButton: { width: 42, height: 42, borderRadius: 13, alignItems: "center", justifyContent: "center", backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  title: { marginLeft: 13, color: colors.ink, fontSize: 22, fontWeight: "700" },
  profileCard: { marginTop: 24, alignItems: "center", padding: 22, borderRadius: 18, backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  avatar: { width: 60, height: 60, borderRadius: 20, backgroundColor: colors.paleBlue, alignItems: "center", justifyContent: "center" },
  avatarText: { color: colors.blue, fontSize: 24, fontWeight: "800" },
  name: { marginTop: 12, color: colors.ink, fontSize: 19, fontWeight: "700" },
  email: { marginTop: 3, color: colors.slate, fontSize: 13 },
  tenant: { marginTop: 8, color: colors.blue, fontSize: 12, fontWeight: "700" },
  sectionLabel: { marginTop: 24, marginBottom: 9, color: colors.muted, fontSize: 12, fontWeight: "700", letterSpacing: 0.8, textTransform: "uppercase" },
  card: { borderRadius: 16, overflow: "hidden", backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  tenantRow: { minHeight: 68, flexDirection: "row", alignItems: "center", paddingHorizontal: 14, borderBottomWidth: 1, borderBottomColor: "#F2F4F7" },
  currentTenant: { backgroundColor: "#F9FAFB" },
  tenantIcon: { width: 40, height: 40, borderRadius: 12, alignItems: "center", justifyContent: "center", backgroundColor: colors.paleBlue },
  tenantText: { flex: 1, marginLeft: 11 },
  tenantName: { color: colors.ink, fontSize: 14, fontWeight: "600" },
  tenantCode: { marginTop: 3, color: colors.muted, fontSize: 11 },
  currentText: { color: colors.success, fontSize: 11, fontWeight: "700" },
  infoRow: { minHeight: 50, flexDirection: "row", alignItems: "center", justifyContent: "space-between", paddingHorizontal: 15, borderBottomWidth: 1, borderBottomColor: "#F2F4F7" },
  lastRow: { borderBottomWidth: 0 },
  infoLabel: { color: colors.muted, fontSize: 13 },
  infoValue: { maxWidth: "65%", color: colors.ink, fontSize: 13, fontWeight: "600", textAlign: "right" },
  logoutButton: { marginTop: 26, minHeight: 50, flexDirection: "row", gap: 8, alignItems: "center", justifyContent: "center", borderRadius: 12, borderWidth: 1, borderColor: "#FECDCA", backgroundColor: colors.white },
  logoutText: { color: colors.danger, fontSize: 14, fontWeight: "700" },
});
