import { Ionicons } from "@expo/vector-icons";
import { useMemo, useState } from "react";
import { Modal, Pressable, SafeAreaView, ScrollView, StyleSheet, Text, TextInput, View } from "react-native";
import type { MobilePosBankAccountOption } from "@/src/types/api";
import { colors } from "@/src/ui/theme";

interface Props {
  visible: boolean;
  accounts: MobilePosBankAccountOption[];
  selectedId?: string;
  onClose: () => void;
  onSelect: (account: MobilePosBankAccountOption) => void;
}

export function BankAccountPickerModal({ visible, accounts, selectedId, onClose, onSelect }: Props) {
  const [query, setQuery] = useState("");
  const results = useMemo(() => {
    const term = query.trim().toLocaleLowerCase();
    if (!term) return accounts;
    return accounts.filter(account => [
      account.accountName,
      account.bankName,
      account.maskedAccountNumber,
      account.currencyCode,
    ].some(value => value.toLocaleLowerCase().includes(term)));
  }, [accounts, query]);

  const close = () => {
    setQuery("");
    onClose();
  };

  return (
    <Modal animationType="slide" onRequestClose={close} presentationStyle="pageSheet" visible={visible}>
      <SafeAreaView style={styles.page}>
        <View style={styles.header}>
          <View style={styles.headerText}>
            <Text style={styles.title}>Select bank account</Text>
            <Text style={styles.subtitle}>Active accounts in the store currency and your Finance scope</Text>
          </View>
          <Pressable accessibilityLabel="Close bank account selector" onPress={close} style={styles.closeButton}>
            <Ionicons color={colors.ink} name="close" size={22} />
          </Pressable>
        </View>
        <TextInput
          accessibilityLabel="Search bank accounts"
          autoCapitalize="none"
          onChangeText={setQuery}
          placeholder="Search account or bank"
          placeholderTextColor={colors.muted}
          style={styles.search}
          value={query}
        />
        <ScrollView contentContainerStyle={styles.results} keyboardShouldPersistTaps="handled">
          {results.map(account => {
            const selected = account.bankAccountId === selectedId;
            return (
              <Pressable
                accessibilityLabel={`Select ${account.accountName}`}
                key={account.bankAccountId}
                onPress={() => { setQuery(""); onSelect(account); }}
                style={[styles.account, selected && styles.accountSelected]}
              >
                <View style={styles.icon}><Ionicons color={colors.blue} name="business-outline" size={20} /></View>
                <View style={styles.grow}>
                  <Text style={styles.accountName}>{account.accountName}</Text>
                  <Text style={styles.accountMeta}>{account.bankName} · {account.maskedAccountNumber} · {account.currencyCode}</Text>
                </View>
                {selected && <Ionicons color={colors.success} name="checkmark-circle" size={22} />}
              </Pressable>
            );
          })}
          {results.length === 0 && (
            <Text style={styles.empty}>No eligible bank account matches this search. Ask Finance to verify the account status, currency, GL mapping, and access scope.</Text>
          )}
        </ScrollView>
      </SafeAreaView>
    </Modal>
  );
}

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: colors.background },
  header: { paddingHorizontal: 20, paddingTop: 20, paddingBottom: 12, flexDirection: "row", alignItems: "center" },
  headerText: { flex: 1 },
  title: { color: colors.ink, fontSize: 20, fontWeight: "800" },
  subtitle: { marginTop: 4, color: colors.muted, fontSize: 11, lineHeight: 16 },
  closeButton: { width: 42, height: 42, alignItems: "center", justifyContent: "center", borderRadius: 13, backgroundColor: colors.white, borderWidth: 1, borderColor: colors.line },
  search: { minHeight: 48, marginHorizontal: 20, paddingHorizontal: 14, borderRadius: 12, borderWidth: 1, borderColor: colors.line, backgroundColor: colors.white, color: colors.ink, fontSize: 14 },
  results: { paddingHorizontal: 20, paddingVertical: 12, paddingBottom: 40 },
  account: { minHeight: 70, marginBottom: 9, padding: 13, flexDirection: "row", alignItems: "center", borderRadius: 14, borderWidth: 1, borderColor: "#EAECF0", backgroundColor: colors.white },
  accountSelected: { borderColor: colors.blue, backgroundColor: colors.paleBlue },
  icon: { width: 38, height: 38, marginRight: 11, alignItems: "center", justifyContent: "center", borderRadius: 11, backgroundColor: colors.paleBlue },
  grow: { flex: 1 },
  accountName: { color: colors.ink, fontSize: 14, fontWeight: "700" },
  accountMeta: { marginTop: 4, color: colors.muted, fontSize: 11, lineHeight: 16 },
  empty: { marginTop: 30, color: colors.muted, fontSize: 13, lineHeight: 20, textAlign: "center" },
});
