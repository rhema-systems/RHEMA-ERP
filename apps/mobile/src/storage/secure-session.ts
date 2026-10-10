import * as Crypto from "expo-crypto";
import * as SecureStore from "expo-secure-store";
import { Platform } from "react-native";
import type { MobilePosOfflineGrant, ServerProfile } from "@/src/types/api";

const keys = {
  accessToken: "rhema.mobile.access-token",
  refreshToken: "rhema.mobile.refresh-token",
  tokenExpiry: "rhema.mobile.token-expiry",
  serverProfile: "rhema.mobile.server-profile",
  installationId: "rhema.mobile.installation-id",
  offlineGrant: "rhema.mobile.offline-grant",
  currentOfflineGrantId: "rhema.mobile.offline-grant.current",
} as const;

function offlineGrantKey(grantId: string): string {
  const normalized = grantId.trim().toLocaleLowerCase();
  if (!/^[0-9a-f-]{36}$/.test(normalized)) throw new Error("The offline grant ID is invalid.");
  return `rhema.mobile.offline-grant.${normalized}`;
}

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

export async function loadOfflineGrant(): Promise<MobilePosOfflineGrant | null> {
  const currentId = await getValue(keys.currentOfflineGrantId);
  if (currentId) return loadOfflineGrantById(currentId);

  const value = await getValue(keys.offlineGrant);
  if (!value) return null;
  try {
    const legacy = JSON.parse(value) as MobilePosOfflineGrant;
    await saveOfflineGrant(legacy);
    await deleteValue(keys.offlineGrant);
    return legacy;
  } catch {
    await deleteValue(keys.offlineGrant);
    return null;
  }
}

export async function loadOfflineGrantById(grantId: string): Promise<MobilePosOfflineGrant | null> {
  const key = offlineGrantKey(grantId);
  const value = await getValue(key);
  if (!value) return null;
  try {
    const grant = JSON.parse(value) as MobilePosOfflineGrant;
    if (grant.id.trim().toLocaleLowerCase() !== grantId.trim().toLocaleLowerCase()) {
      await deleteValue(key);
      return null;
    }
    return grant;
  } catch {
    await deleteValue(key);
    return null;
  }
}

export async function saveOfflineGrant(grant: MobilePosOfflineGrant): Promise<void> {
  await Promise.all([
    setValue(offlineGrantKey(grant.id), JSON.stringify(grant)),
    setValue(keys.currentOfflineGrantId, grant.id),
    deleteValue(keys.offlineGrant),
  ]);
}

export async function clearOfflineGrant(): Promise<void> {
  // Clear the active-session pointer while retaining encrypted grants referenced by durable outbox
  // work. An expired grant can still authorize a command recorded inside its signed window.
  await Promise.all([
    deleteValue(keys.currentOfflineGrantId),
    deleteValue(keys.offlineGrant),
  ]);
}

export async function deleteOfflineGrant(grantId: string): Promise<void> {
  const currentId = await getValue(keys.currentOfflineGrantId);
  await Promise.all([
    deleteValue(offlineGrantKey(grantId)),
    currentId?.trim().toLocaleLowerCase() === grantId.trim().toLocaleLowerCase()
      ? deleteValue(keys.currentOfflineGrantId)
      : Promise.resolve(),
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
