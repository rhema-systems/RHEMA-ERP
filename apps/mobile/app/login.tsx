import { Ionicons } from "@expo/vector-icons";
import { Redirect } from "expo-router";
import { useState } from "react";
import {
  ActivityIndicator,
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from "react-native";
import { ApiProblem } from "@/src/api/client";
import { environmentColor } from "@/src/config/environment";
import { useSession } from "@/src/session/session-context";
import type { RuntimeEnvironment } from "@/src/types/api";
import { colors } from "@/src/ui/theme";

const environments: RuntimeEnvironment[] = ["TEST", "UAT", "PRODUCTION"];

export default function LoginScreen() {
  const session = useSession();
  const [environment, setEnvironment] = useState<RuntimeEnvironment>(session.profile.environment);
  const [apiBaseUrl, setApiBaseUrl] = useState(session.profile.apiBaseUrl);
  const [tenantCode, setTenantCode] = useState("");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [twoFactorCode, setTwoFactorCode] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [errorText, setErrorText] = useState<string | null>(null);

  if (["ready", "enrollmentPending", "blocked"].includes(session.status)) return <Redirect href="/" />;

  const submit = async () => {
    setSubmitting(true);
    setErrorText(null);
    try {
      if (session.status === "mfaRequired") {
        if (!/^\d{6}$/.test(twoFactorCode)) throw new Error("Enter the six-digit authenticator code.");
        await session.submitMfa(twoFactorCode);
      } else {
        await session.signIn(
          { environment, apiBaseUrl },
          {
            username: username.trim(),
            password,
            tenantCode: tenantCode.trim() || undefined,
            rememberMe: true,
          },
        );
      }
    } catch (caught) {
      const suffix = caught instanceof ApiProblem && caught.correlationId ? ` Reference: ${caught.correlationId}.` : "";
      setErrorText(`${caught instanceof Error ? caught.message : "Sign-in failed."}${suffix}`);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <KeyboardAvoidingView style={styles.page} behavior={Platform.OS === "ios" ? "padding" : undefined}>
      <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={styles.content}>
        <View style={styles.brandMark}>
          <View style={styles.brandBarSmall} /><View style={styles.brandBarMedium} /><View style={styles.brandBarTall} />
        </View>
        <Text style={styles.brand}>RHEMA-ERP</Text>
        <Text style={styles.product}>FIELD POS & REVENUE COLLECTION</Text>

        <View style={styles.card}>
          <Text style={styles.heading}>{session.status === "mfaRequired" ? "Verify your sign in" : "Sign in to your till"}</Text>
          <Text style={styles.subheading}>
            {session.status === "mfaRequired" ? "Enter the current code from your authenticator app." : "Use your approved RHEMA ERP account and tenant."}
          </Text>

          {session.status !== "mfaRequired" && (
            <>
              <Text style={styles.label}>Environment</Text>
              <View style={styles.segmentRow}>
                {environments.map(item => (
                  <Pressable
                    key={item}
                    accessibilityRole="button"
                    onPress={() => setEnvironment(item)}
                    style={[styles.segment, environment === item && { borderColor: environmentColor(item), backgroundColor: `${environmentColor(item)}12` }]}
                  >
                    <Text style={[styles.segmentText, environment === item && { color: environmentColor(item) }]}>{item}</Text>
                  </Pressable>
                ))}
              </View>

              <Field label="Server URL" icon="server-outline" value={apiBaseUrl} onChangeText={setApiBaseUrl} placeholder="https://erp.example.com" autoCapitalize="none" keyboardType="url" />
              <Field label="Tenant code" icon="business-outline" value={tenantCode} onChangeText={setTenantCode} placeholder="Optional; your default tenant is used" autoCapitalize="characters" />
              <Field label="Username or email" icon="person-outline" value={username} onChangeText={setUsername} placeholder="name@company.com" autoCapitalize="none" />

              <Text style={styles.label}>Password</Text>
              <View style={styles.inputShell}>
                <Ionicons name="lock-closed-outline" size={19} color={colors.muted} />
                <TextInput value={password} onChangeText={setPassword} placeholder="Enter your password" secureTextEntry={!showPassword} style={styles.input} placeholderTextColor="#98A2B3" />
                <Pressable accessibilityLabel={showPassword ? "Hide password" : "Show password"} onPress={() => setShowPassword(value => !value)}>
                  <Ionicons name={showPassword ? "eye-off-outline" : "eye-outline"} size={20} color={colors.muted} />
                </Pressable>
              </View>
            </>
          )}

          {session.status === "mfaRequired" && (
            <Field label="Authenticator code" icon="shield-checkmark-outline" value={twoFactorCode} onChangeText={value => setTwoFactorCode(value.replace(/\D/g, "").slice(0, 6))} placeholder="000000" keyboardType="number-pad" />
          )}

          {(errorText ?? session.error?.message) && (
            <View style={styles.errorBox}>
              <Ionicons name="alert-circle-outline" size={19} color={colors.danger} />
              <Text style={styles.errorText}>{errorText ?? session.error?.message}</Text>
            </View>
          )}

          <Pressable accessibilityRole="button" disabled={submitting} onPress={() => void submit()} style={[styles.primaryButton, submitting && styles.disabled]}>
            {submitting ? <ActivityIndicator color={colors.white} /> : <Text style={styles.primaryText}>{session.status === "mfaRequired" ? "Verify" : "Sign in"}</Text>}
          </Pressable>
        </View>
        <Text style={styles.securityNote}>Session credentials are protected by the device secure store. Production connections require HTTPS.</Text>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

function Field({ label, icon, ...props }: React.ComponentProps<typeof TextInput> & { label: string; icon: React.ComponentProps<typeof Ionicons>["name"] }) {
  return (
    <>
      <Text style={styles.label}>{label}</Text>
      <View style={styles.inputShell}>
        <Ionicons name={icon} size={19} color={colors.muted} />
        <TextInput {...props} style={styles.input} placeholderTextColor="#98A2B3" />
      </View>
    </>
  );
}

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: colors.background },
  content: { flexGrow: 1, paddingHorizontal: 22, paddingTop: 58, paddingBottom: 34 },
  brandMark: { height: 38, flexDirection: "row", alignItems: "flex-end", gap: 4 },
  brandBarSmall: { width: 8, height: 18, borderRadius: 3, backgroundColor: "#53B1FD" },
  brandBarMedium: { width: 8, height: 28, borderRadius: 3, backgroundColor: "#1570EF" },
  brandBarTall: { width: 8, height: 38, borderRadius: 3, backgroundColor: "#6938EF" },
  brand: { marginTop: 10, color: colors.navy, fontSize: 26, fontWeight: "800", letterSpacing: 0.3 },
  product: { color: colors.muted, marginTop: 2, fontSize: 11, fontWeight: "700", letterSpacing: 1.8 },
  card: { marginTop: 38, borderRadius: 22, backgroundColor: colors.white, padding: 22, borderWidth: 1, borderColor: "#EAECF0", shadowColor: "#101828", shadowOpacity: 0.1, shadowRadius: 18, shadowOffset: { width: 0, height: 8 }, elevation: 4 },
  heading: { color: colors.ink, fontSize: 24, lineHeight: 30, fontWeight: "700" },
  subheading: { marginTop: 6, marginBottom: 18, color: colors.slate, fontSize: 14, lineHeight: 20 },
  label: { marginTop: 14, marginBottom: 7, color: colors.slate, fontSize: 13, fontWeight: "600" },
  inputShell: { minHeight: 52, flexDirection: "row", alignItems: "center", gap: 10, borderWidth: 1, borderColor: colors.line, borderRadius: 12, paddingHorizontal: 14, backgroundColor: colors.white },
  input: { flex: 1, color: colors.ink, fontSize: 15, paddingVertical: 13 },
  segmentRow: { flexDirection: "row", gap: 7 },
  segment: { flex: 1, minHeight: 38, alignItems: "center", justifyContent: "center", borderRadius: 9, borderWidth: 1, borderColor: colors.line },
  segmentText: { color: colors.slate, fontSize: 11, fontWeight: "700" },
  errorBox: { marginTop: 16, flexDirection: "row", gap: 9, padding: 12, borderRadius: 10, backgroundColor: colors.dangerBg },
  errorText: { flex: 1, color: colors.danger, fontSize: 13, lineHeight: 18 },
  primaryButton: { marginTop: 22, minHeight: 52, alignItems: "center", justifyContent: "center", borderRadius: 12, backgroundColor: colors.blue },
  primaryText: { color: colors.white, fontSize: 16, fontWeight: "700" },
  disabled: { opacity: 0.6 },
  securityNote: { marginTop: 22, paddingHorizontal: 12, color: colors.muted, fontSize: 12, lineHeight: 18, textAlign: "center" },
});
