import * as Crypto from "expo-crypto";
import * as SecureStore from "expo-secure-store";
import { Platform } from "react-native";
import type { ServerProfile } from "@/src/types/api";

const keys = {
  accessToken: "rhema.mobile.access-token",
  refreshToken: "rhema.mobile.refresh-token",
  tokenExpiry: "rhema.mobile.token-expiry",
  serverProfile: "rhema.mobile.server-profile",
  installationId: "rhema.mobile.installation-id",
} as const;

async function getValue(key: string): Promise<string | null> {
  if (Platform.OS === "web") return globalThis.localStorage?.getItem(key) ?? null;
  return SecureStore.getItemAsync(key);
}

async function setValue(key: string, value: string): Promise<void> {
  if (Platform.OS === "web") {
    globalThis.localStorage?.setItem(key, value);
    return;
  }
  await SecureStore.setItemAsync(key, value, {
    keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
  });
}

async function deleteValue(key: string): Promise<void> {
  if (Platform.OS === "web") {
    globalThis.localStorage?.removeItem(key);
    return;
  }
  await SecureStore.deleteItemAsync(key);
}

export async function loadTokens(): Promise<{ accessToken: string; refreshToken: string } | null> {
  const [accessToken, refreshToken] = await Promise.all([
    getValue(keys.accessToken),
    getValue(keys.refreshToken),
  ]);
  return accessToken && refreshToken ? { accessToken, refreshToken } : null;
}

export async function saveTokens(accessToken: string, refreshToken: string, expiresAt?: string): Promise<void> {
  await Promise.all([
    setValue(keys.accessToken, accessToken),
    setValue(keys.refreshToken, refreshToken),
    expiresAt ? setValue(keys.tokenExpiry, expiresAt) : deleteValue(keys.tokenExpiry),
  ]);
}

export async function clearTokens(): Promise<void> {
  await Promise.all([
    deleteValue(keys.accessToken),
    deleteValue(keys.refreshToken),
    deleteValue(keys.tokenExpiry),
  ]);
}

export async function loadServerProfile(): Promise<ServerProfile | null> {
  const value = await getValue(keys.serverProfile);
  if (!value) return null;
  try {
    return JSON.parse(value) as ServerProfile;
  } catch {
    await deleteValue(keys.serverProfile);
    return null;
  }
}

export async function saveServerProfile(profile: ServerProfile): Promise<void> {
  await setValue(keys.serverProfile, JSON.stringify(profile));
}

export async function getInstallationId(): Promise<string> {
  const existing = await getValue(keys.installationId);
  if (existing) return existing;
  const created = `${Crypto.randomUUID()}-${Crypto.randomUUID()}`;
  await setValue(keys.installationId, created);
  return created;
}
