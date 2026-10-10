import { Ionicons } from "@expo/vector-icons";
import { Link, Redirect, useRouter } from "expo-router";
import { useEffect, useMemo, useState } from "react";
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from "react-native";
import { ApiProblem, mobileApi } from "@/src/api/client";
import { useSession } from "@/src/session/session-context";
import { getInstallationId } from "@/src/storage/secure-session";
import type { MobilePosCustomerSearchResult, OutstandingInvoice } from "@/src/types/api";
import { colors } from "@/src/ui/theme";

export default function CustomersScreen() {
  const session = useSession();
  const router = useRouter();
  const defaultCustomer = useMemo<MobilePosCustomerSearchResult | null>(() => session.bootstrap ? ({
    businessPartnerId: session.bootstrap.store.defaultWalkInBusinessPartnerId,
    businessPartnerRoleId: session.bootstrap.store.defaultWalkInBusinessPartnerRoleId,
    code: session.bootstrap.store.defaultWalkInCustomerCode,
    name: session.bootstrap.store.defaultWalkInCustomerName,
    currencyCode: session.bootstrap.store.currencyCode,
    isDefaultWalkInCustomer: true,
  }) : null, [session.bootstrap]);
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<MobilePosCustomerSearchResult[]>([]);
  const [selected, setSelected] = useState<MobilePosCustomerSearchResult | null>(defaultCustomer);
  const [invoices, setInvoices] = useState<OutstandingInvoice[] | null>(null);
  const [busy, setBusy] = useState<"search" | "invoices" | null>(null);
  const [error, setError] = useState<ApiProblem | null>(null);

  useEffect(() => {
    setSelected(defaultCustomer);
    setResults([]);
    setInvoices(null);
    setError(null);
  }, [defaultCustomer]);

  if (session.status === "signedOut" || session.status === "mfaRequired") return <Redirect href="/login" />;
  if (session.status !== "ready" || !session.bootstrap) return <Redirect href="/" />;

  const permitted = session.user?.permissions.includes("MobilePOS.Customer.View") === true;
  const canCollect = session.user?.permissions.includes("MobilePOS.Till.Operate") === true
    && session.user.permissions.includes("MobilePOS.Payment.Collect")
    && session.user.permissions.includes("Finance.AR.Payments.Receive")
    && Boolean(session.bootstrap.currentTillSessionId);

  const search = async () => {
    const term = query.trim();
    if (term.length < 2) {
      setError(new ApiProblem("Enter at least two characters to search approved customers.", 400, "SEARCH_TERM_REQUIRED"));
      return;
    }
    setBusy("search");
    setError(null);
    try {
      setResults(await mobileApi.searchCustomers(await getInstallationId(), term));
    } catch (caught) {
      setError(asProblem(caught));
    } finally {
      setBusy(null);
    }
  };

  const chooseCustomer = async (customer: MobilePosCustomerSearchResult) => {
    setSelected(customer);
    setInvoices(null);
    setBusy("invoices");
    setError(null);
    try {
      setInvoices(await mobileApi.getOutstandingInvoices(await getInstallationId(), customer));
    } catch (caught) {
      setError(asProblem(caught));
    } finally {
      setBusy(null);
    }
  };

  return (
    <ScrollView style={styles.page} contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
      <View style={styles.header}>
        <Link href="/" asChild>
          <Pressable accessibilityRole="button" accessibilityLabel="Back" style={styles.backButton}>
            <Ionicons name="arrow-back" size={22} color={colors.navy} />
          </Pressable>
        </Link>
        <View style={styles.headerText}>
          <Text style={styles.title}>Customer lookup</Text>
          <Text style={styles.subtitle}>Approved and transaction-ready customers only</Text>
        </View>
      </View>

      {!permitted ? (
        <View style={styles.errorBox}>
          <Ionicons name="lock-closed-outline" size={22} color={colors.danger} />
          <Text style={styles.errorText}>Your role needs the View Mobile POS Customers permission.</Text>
        </View>
      ) : (
        <>
          {defaultCustomer && (
            <View style={styles.defaultCard}>
              <View style={styles.cardTop}>
                <Text style={styles.cardEyebrow}>STORE DEFAULT</Text>
                <View style={styles.defaultPill}><Text style={styles.defaultPillText}>Walk-in</Text></View>
              </View>
              <Text style={styles.customerName}>{defaultCustomer.name}</Text>
              <Text style={styles.customerCode}>{defaultCustomer.code}</Text>
              <Pressable accessibilityRole="button" onPress={() => void chooseCustomer(defaultCustomer)} style={styles.outlineButton}>
                <Text style={styles.outlineButtonText}>Use default and view balances</Text>
              </Pressable>
            </View>
          )}

          <Text style={styles.sectionLabel}>Find another approved customer</Text>
          <View style={styles.searchRow}>
            <View style={styles.searchField}>
              <Ionicons name="search-outline" size={19} color={colors.muted} />
              <TextInput
                accessibilityLabel="Search approved customers"
                autoCapitalize="none"
                onChangeText={setQuery}
                onSubmitEditing={() => void search()}
                placeholder="Name, code, phone or email"
                placeholderTextColor={colors.muted}
                returnKeyType="search"
                style={styles.searchInput}
                value={query}
              />
            </View>
            <Pressable accessibilityRole="button" disabled={busy !== null} onPress={() => void search()} style={styles.searchButton}>
              {busy === "search" ? <ActivityIndicator size="small" color={colors.white} /> : <Ionicons name="arrow-forward" size={20} color={colors.white} />}
            </Pressable>
          </View>

          {error && (
            <View accessibilityRole="alert" style={styles.errorBox}>
              <Ionicons name="alert-circle-outline" size={20} color={colors.danger} />
              <Text style={styles.errorText}>{error.message}{error.correlationId ? ` Reference: ${error.correlationId}` : ""}</Text>
            </View>
          )}

          {results.map(customer => (
            <Pressable key={customer.businessPartnerRoleId} accessibilityRole="button" onPress={() => void chooseCustomer(customer)} style={styles.resultCard}>
              <View style={styles.resultAvatar}><Text style={styles.resultAvatarText}>{customer.name.charAt(0).toUpperCase()}</Text></View>
              <View style={styles.resultMain}>
                <Text style={styles.resultName}>{customer.name}</Text>
                <Text style={styles.resultMeta}>{customer.code}{customer.phone ? ` · ${customer.phone}` : ""}</Text>
              </View>
              <Ionicons name="chevron-forward" size={18} color={colors.muted} />
            </Pressable>
          ))}
          {query.trim().length >= 2 && busy !== "search" && results.length === 0 && !error && (
            <Text style={styles.emptyText}>No approved, transaction-ready customer matched this search.</Text>
          )}

          {selected && (
            <View style={styles.invoiceSection}>
              <Text style={styles.sectionLabel}>Outstanding invoices · {selected.name}</Text>
              {busy === "invoices" ? (
                <ActivityIndicator style={styles.invoiceLoader} color={colors.blue} />
              ) : invoices?.length === 0 ? (
                <View style={styles.clearCard}>
                  <Ionicons name="checkmark-circle-outline" size={24} color={colors.success} />
                  <Text style={styles.clearText}>No outstanding invoices.</Text>
                </View>
              ) : invoices?.map(invoice => (
                <View key={invoice.id} style={styles.invoiceCard}>
                  <View style={styles.cardTop}>
                    <Text style={styles.invoiceNumber}>{invoice.invoiceNumber}</Text>
                    <Text style={styles.invoiceAmount}>{money(invoice.balanceAmount, invoice.currencyCode)}</Text>
                  </View>
                  <Text style={styles.invoiceMeta}>Invoice {date(invoice.invoiceDate)} · Due {invoice.dueDate ? date(invoice.dueDate) : "not set"}</Text>
                  {invoice.daysOverdue > 0 && <Text style={styles.overdue}>{invoice.daysOverdue} day(s) overdue</Text>}
                  {canCollect && <Pressable
                    accessibilityRole="button"
                    onPress={() => router.push({
                      pathname: "/collection",
                      params: {
                        businessPartnerId: selected.businessPartnerId,
                        businessPartnerRoleId: selected.businessPartnerRoleId,
                        customerCode: selected.code,
                        customerName: selected.name,
                        initialInvoiceId: invoice.id,
                      },
                    })}
                    style={styles.collectButton}
                  >
                    <Ionicons name="cash-outline" size={17} color={colors.white} />
                    <Text style={styles.collectButtonText}>Collect payment</Text>
                  </Pressable>}
                </View>
              ))}
            </View>
          )}
        </>
      )}
    </ScrollView>
  );
}

function asProblem(error: unknown): ApiProblem {
  return error instanceof ApiProblem
    ? error
    : new ApiProblem(error instanceof Error ? error.message : "An unexpected error occurred.", 0, "CLIENT_ERROR");
}

function money(value: number, currency: string): string {
  return `${currency} ${value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

function date(value: string): string {
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleDateString();
}

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: colors.background },
  content: { paddingHorizontal: 20, paddingTop: 52, paddingBottom: 36 },
  header: { flexDirection: "row", alignItems: "center" },
  backButton: { width: 42, height: 42, borderRadius: 13, alignItems: "center", justifyContent: "center", backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  headerText: { flex: 1, marginLeft: 13 },
  title: { color: colors.ink, fontSize: 22, fontWeight: "700" },
  subtitle: { marginTop: 2, color: colors.muted, fontSize: 11 },
  defaultCard: { marginTop: 24, padding: 17, borderRadius: 17, backgroundColor: colors.paleBlue, borderWidth: 1, borderColor: "#B2CCFF" },
  cardTop: { flexDirection: "row", alignItems: "center", justifyContent: "space-between" },
  cardEyebrow: { color: colors.blue, fontSize: 10, fontWeight: "800", letterSpacing: 1.3 },
  defaultPill: { paddingHorizontal: 9, paddingVertical: 5, borderRadius: 99, backgroundColor: colors.white },
  defaultPillText: { color: colors.blue, fontSize: 10, fontWeight: "700" },
  customerName: { marginTop: 12, color: colors.ink, fontSize: 18, fontWeight: "700" },
  customerCode: { marginTop: 3, color: colors.slate, fontSize: 12 },
  outlineButton: { marginTop: 14, minHeight: 43, alignItems: "center", justifyContent: "center", borderRadius: 11, borderWidth: 1, borderColor: "#84ADFF", backgroundColor: colors.white },
  outlineButtonText: { color: colors.blue, fontSize: 13, fontWeight: "700" },
  sectionLabel: { marginTop: 24, marginBottom: 9, color: colors.muted, fontSize: 11, fontWeight: "700", letterSpacing: 0.7, textTransform: "uppercase" },
  searchRow: { flexDirection: "row", gap: 9 },
  searchField: { flex: 1, minHeight: 48, flexDirection: "row", alignItems: "center", paddingHorizontal: 13, borderRadius: 12, borderWidth: 1, borderColor: colors.line, backgroundColor: colors.white },
  searchInput: { flex: 1, marginLeft: 8, color: colors.ink, fontSize: 14 },
  searchButton: { width: 48, height: 48, alignItems: "center", justifyContent: "center", borderRadius: 12, backgroundColor: colors.blue },
  errorBox: { marginTop: 14, flexDirection: "row", gap: 9, padding: 13, borderRadius: 11, backgroundColor: colors.dangerBg },
  errorText: { flex: 1, color: colors.danger, fontSize: 12, lineHeight: 18 },
  resultCard: { marginTop: 9, minHeight: 68, flexDirection: "row", alignItems: "center", padding: 13, borderRadius: 14, backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  resultAvatar: { width: 42, height: 42, borderRadius: 13, alignItems: "center", justifyContent: "center", backgroundColor: colors.paleBlue },
  resultAvatarText: { color: colors.blue, fontSize: 17, fontWeight: "800" },
  resultMain: { flex: 1, marginLeft: 11 },
  resultName: { color: colors.ink, fontSize: 14, fontWeight: "700" },
  resultMeta: { marginTop: 4, color: colors.muted, fontSize: 11 },
  emptyText: { marginTop: 16, color: colors.muted, fontSize: 13, lineHeight: 19, textAlign: "center" },
  invoiceSection: { marginTop: 2 },
  invoiceLoader: { marginTop: 20 },
  clearCard: { flexDirection: "row", alignItems: "center", gap: 9, padding: 15, borderRadius: 14, backgroundColor: colors.successBg },
  clearText: { color: colors.success, fontSize: 13, fontWeight: "600" },
  invoiceCard: { marginBottom: 9, padding: 14, borderRadius: 14, backgroundColor: colors.white, borderWidth: 1, borderColor: "#EAECF0" },
  invoiceNumber: { color: colors.ink, fontSize: 13, fontWeight: "700" },
  invoiceAmount: { color: colors.navy, fontSize: 14, fontWeight: "800" },
  invoiceMeta: { marginTop: 8, color: colors.slate, fontSize: 11 },
  overdue: { marginTop: 6, color: colors.danger, fontSize: 11, fontWeight: "700" },
  collectButton: { minHeight: 40, marginTop: 12, flexDirection: "row", alignItems: "center", justifyContent: "center", gap: 7, borderRadius: 10, backgroundColor: colors.blue },
  collectButtonText: { color: colors.white, fontSize: 12, fontWeight: "800" },
});
