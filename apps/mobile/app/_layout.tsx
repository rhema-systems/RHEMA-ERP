import { Ionicons } from "@expo/vector-icons";
import { Stack } from "expo-router";
import { StatusBar } from "expo-status-bar";
import { Pressable, StyleSheet, Text, View } from "react-native";
import { SafeAreaProvider } from "react-native-safe-area-context";
import { SessionProvider } from "@/src/session/session-context";
import { colors } from "@/src/ui/theme";

export function ErrorBoundary({ error, retry }: { error: Error; retry: () => void }) {
  const reference = `MOBILE-${Date.now().toString(36).toUpperCase()}`;
  if (__DEV__) console.error("RHEMA Mobile root failure", { reference, name: error.name, message: error.message });

  return (
    <SafeAreaProvider>
      <View style={styles.failurePage}>
        <View style={styles.failureIcon}><Ionicons name="alert-circle-outline" size={34} color={colors.danger} /></View>
        <Text style={styles.failureTitle}>RHEMA Field POS could not start</Text>
        <Text style={styles.failureText}>Retry the application. If the problem continues, give support reference {reference}.</Text>
        <Pressable accessibilityRole="button" onPress={retry} style={styles.retryButton}>
          <Text style={styles.retryText}>Retry</Text>
        </Pressable>
      </View>
    </SafeAreaProvider>
  );
}

export default function RootLayout() {
  return (
    <SafeAreaProvider>
      <SessionProvider>
        <StatusBar style="dark" />
        <Stack screenOptions={{ headerShown: false, animation: "fade" }} />
      </SessionProvider>
    </SafeAreaProvider>
  );
}

const styles = StyleSheet.create({
  failurePage: { flex: 1, alignItems: "center", justifyContent: "center", padding: 28, backgroundColor: colors.background },
  failureIcon: { width: 64, height: 64, borderRadius: 20, alignItems: "center", justifyContent: "center", backgroundColor: colors.dangerBg },
  failureTitle: { marginTop: 20, color: colors.ink, fontSize: 22, lineHeight: 28, fontWeight: "700", textAlign: "center" },
  failureText: { marginTop: 10, color: colors.slate, fontSize: 15, lineHeight: 22, textAlign: "center" },
  retryButton: { marginTop: 24, minWidth: 160, paddingVertical: 14, borderRadius: 12, backgroundColor: colors.blue, alignItems: "center" },
  retryText: { color: colors.white, fontSize: 16, fontWeight: "700" },
});
