import Constants from "expo-constants";
import * as Device from "expo-device";
import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { ApiProblem, mobileApi } from "@/src/api/client";
import { defaultServerProfile, validateServerProfile } from "@/src/config/environment";
import { isOfflineGrantUsable } from "@/src/offline/grant";
import { synchronizeSessionCatalogue } from "@/src/offline/catalogue-runtime";
import {
  clearOfflineGrant,
  clearTokens,
  getInstallationId,
  loadOfflineGrant,
  loadServerProfile,
  loadTokens,
  saveOfflineGrant,
  saveServerProfile,
  saveTokens,
} from "@/src/storage/secure-session";
import type {
  LoginRequest,
  MobilePosBootstrap,
  MobilePosDevice,
  MobilePosOfflineGrant,
  ServerProfile,
  UserInfo,
} from "@/src/types/api";

export type SessionStatus = "initializing" | "signedOut" | "mfaRequired" | "enrollmentPending" | "ready" | "blocked";

interface PendingLogin {
  username: string;
  password: string;
  tenantCode?: string;
  rememberMe: boolean;
}

interface SessionContextValue {
  status: SessionStatus;
  profile: ServerProfile;
  user: UserInfo | null;
  bootstrap: MobilePosBootstrap | null;
  offlineGrant: MobilePosOfflineGrant | null;
  offlineGrantBusy: boolean;
  offlineGrantError: ApiProblem | null;
  pendingDevice: MobilePosDevice | null;
  error: ApiProblem | null;
  signIn: (profile: ServerProfile, request: LoginRequest) => Promise<void>;
  submitMfa: (code: string) => Promise<void>;
  refreshBootstrap: () => Promise<void>;
  requestOfflineGrant: () => Promise<void>;
  switchTenant: (tenantCode: string) => Promise<void>;
  signOut: () => Promise<void>;
}

const SessionContext = createContext<SessionContextValue | null>(null);

function asApiProblem(error: unknown): ApiProblem {
  if (error instanceof ApiProblem) return error;
  return new ApiProblem(error instanceof Error ? error.message : "An unexpected error occurred.", 0, "CLIENT_ERROR");
}

function isActiveDevice(device: MobilePosDevice): boolean {
  return device.status === "Active" || device.status === 2;
}

function mayRequestEnrollment(user: UserInfo): boolean {
  return user.permissions.includes("MobilePOS.Device.Enroll");
}

async function buildEnrollmentRequest() {
  return {
    installationId: await getInstallationId(),
    deviceName: Device.deviceName ?? Device.modelName ?? "RHEMA mobile device",
    manufacturer: Device.manufacturer ?? undefined,
    model: Device.modelName ?? undefined,
    operatingSystemVersion: Device.osVersion ?? undefined,
    appVersion: Constants.expoConfig?.version ?? undefined,
    printerAdapterKey: "system-print",
    scannerAdapterKey: "camera-manual",
  };
}

export function SessionProvider({ children }: React.PropsWithChildren) {
  const [status, setStatus] = useState<SessionStatus>("initializing");
  const [profile, setProfile] = useState<ServerProfile>(defaultServerProfile);
  const [user, setUser] = useState<UserInfo | null>(null);
  const [bootstrap, setBootstrap] = useState<MobilePosBootstrap | null>(null);
  const [offlineGrant, setOfflineGrant] = useState<MobilePosOfflineGrant | null>(null);
  const [offlineGrantBusy, setOfflineGrantBusy] = useState(false);
  const [offlineGrantError, setOfflineGrantError] = useState<ApiProblem | null>(null);
  const [pendingDevice, setPendingDevice] = useState<MobilePosDevice | null>(null);
  const [error, setError] = useState<ApiProblem | null>(null);
  const [pendingLogin, setPendingLogin] = useState<PendingLogin | null>(null);

  const resolveMobileAccess = useCallback(async (currentUser: UserInfo, allowEnrollment: boolean) => {
    const installationId = await getInstallationId();
    try {
      const result = await mobileApi.bootstrap(installationId);
      const cachedGrant = await loadOfflineGrant();
      if (cachedGrant && isOfflineGrantUsable(cachedGrant, result, currentUser.currentTenantId)) {
        setOfflineGrant(cachedGrant);
      } else {
        await clearOfflineGrant();
        setOfflineGrant(null);
      }
      setBootstrap(result);
      setPendingDevice(null);
      setError(null);
      setOfflineGrantError(null);
      setStatus("ready");
      if (currentUser.permissions.includes("MobilePOS.Offline.Use")
        && currentUser.permissions.includes("MobilePOS.Till.Operate")
        && currentUser.permissions.includes("MobilePOS.Invoice.Create")) {
        void synchronizeSessionCatalogue(currentUser, result, installationId).catch(() => {
          // A failed warm-up must not block online use. Grant issuance retries synchronously.
        });
      }
      return;
    } catch (caught) {
      const problem = asApiProblem(caught);
      if (!allowEnrollment || !mayRequestEnrollment(currentUser) || ![401, 403, 404].includes(problem.status)) {
        await clearOfflineGrant();
        setOfflineGrant(null);
        setError(problem);
        setStatus("blocked");
        return;
      }
    }

    try {
      await clearOfflineGrant();
      setOfflineGrant(null);
      const device = await mobileApi.requestEnrollment(await buildEnrollmentRequest());
      setPendingDevice(device);
      if (isActiveDevice(device)) {
        const result = await mobileApi.bootstrap(installationId);
        setBootstrap(result);
        setPendingDevice(null);
        setError(null);
        setOfflineGrantError(null);
        setStatus("ready");
        if (currentUser.permissions.includes("MobilePOS.Offline.Use")
          && currentUser.permissions.includes("MobilePOS.Till.Operate")
          && currentUser.permissions.includes("MobilePOS.Invoice.Create")) {
          void synchronizeSessionCatalogue(currentUser, result, installationId).catch(() => {
            // A failed warm-up must not block online use. Grant issuance retries synchronously.
          });
        }
      } else {
        await clearOfflineGrant();
        setOfflineGrant(null);
        setBootstrap(null);
        setError(null);
        setStatus("enrollmentPending");
      }
    } catch (caught) {
      await clearOfflineGrant();
      setOfflineGrant(null);
      setError(asApiProblem(caught));
      setStatus("blocked");
    }
  }, []);

  const completeLogin = useCallback(async (request: LoginRequest) => {
    const response = await mobileApi.login(request);
    if (response.requiresTwoFactor) {
      setPendingLogin({
        username: request.username,
        password: request.password,
        tenantCode: request.tenantCode,
        rememberMe: request.rememberMe,
      });
      setStatus("mfaRequired");
      return;
    }
    if (!response.token || !response.refreshToken) {
      throw new ApiProblem("The server did not return a complete authenticated session.", 500, "INVALID_LOGIN_RESPONSE");
    }
    await saveTokens(response.token, response.refreshToken, response.expiresAt);
    const currentUser = response.user ?? await mobileApi.me();
    setUser(currentUser);
    setPendingLogin(null);
    await resolveMobileAccess(currentUser, true);
  }, [resolveMobileAccess]);

  const signIn = useCallback(async (nextProfile: ServerProfile, request: LoginRequest) => {
    setStatus("initializing");
    setError(null);
    try {
      await clearOfflineGrant();
      setOfflineGrant(null);
      setOfflineGrantError(null);
      const checked = validateServerProfile(nextProfile);
      await saveServerProfile(checked);
      setProfile(checked);
      await completeLogin(request);
    } catch (caught) {
      await Promise.all([clearTokens(), clearOfflineGrant()]);
      setUser(null);
      setError(asApiProblem(caught));
      setStatus("signedOut");
      throw caught;
    }
  }, [completeLogin]);

  const submitMfa = useCallback(async (code: string) => {
    if (!pendingLogin) throw new ApiProblem("The sign-in challenge has expired. Start again.", 400, "MFA_STATE_MISSING");
    setStatus("initializing");
    setError(null);
    try {
      await completeLogin({ ...pendingLogin, twoFactorCode: code });
    } catch (caught) {
      setError(asApiProblem(caught));
      setStatus("mfaRequired");
      throw caught;
    }
  }, [completeLogin, pendingLogin]);

  const refreshBootstrap = useCallback(async () => {
    if (!user) return;
    setStatus("initializing");
    await resolveMobileAccess(user, true);
  }, [resolveMobileAccess, user]);

  const requestOfflineGrant = useCallback(async () => {
    if (!user || !bootstrap) {
      throw new ApiProblem("Refresh your Mobile POS assignment before requesting offline authorization.", 409, "MOBILE_CONTEXT_REQUIRED");
    }
    if (!user.permissions.includes("MobilePOS.Offline.Use") || !user.permissions.includes("MobilePOS.Till.Operate")) {
      throw new ApiProblem("Your assigned role does not authorize offline Mobile POS operation.", 403, "OFFLINE_PERMISSION_REQUIRED");
    }
    if (!bootstrap.currentTillSessionId) {
      throw new ApiProblem("Open your assigned cashier till session before requesting offline authorization.", 409, "TILL_SESSION_REQUIRED");
    }
    if (!bootstrap.offlinePolicy) {
      throw new ApiProblem("The assigned store does not have an active offline policy.", 409, "OFFLINE_POLICY_REQUIRED");
    }

    setOfflineGrantBusy(true);
    setOfflineGrantError(null);
    try {
      const grant = await mobileApi.issueOfflineGrant(await getInstallationId());
      if (!isOfflineGrantUsable(grant, bootstrap, user.currentTenantId)) {
        await clearOfflineGrant();
        setOfflineGrant(null);
        throw new ApiProblem(
          "The issued offline authorization does not match the current Mobile POS assignment. Refresh and try again.",
          409,
          "OFFLINE_GRANT_CONTEXT_MISMATCH",
        );
      }
      await synchronizeSessionCatalogue(user, bootstrap, await getInstallationId());
      await saveOfflineGrant(grant);
      setOfflineGrant(grant);
    } catch (caught) {
      const problem = asApiProblem(caught);
      setOfflineGrantError(problem);
      throw problem;
    } finally {
      setOfflineGrantBusy(false);
    }
  }, [bootstrap, user]);

  const switchTenant = useCallback(async (tenantCode: string) => {
    setStatus("initializing");
    setError(null);
    try {
      await clearOfflineGrant();
      setOfflineGrant(null);
      setOfflineGrantError(null);
      const response = await mobileApi.selectTenant(tenantCode);
      await saveTokens(response.token, response.refreshToken, response.expiresAt);
      setUser(response.user);
      setBootstrap(null);
      setPendingDevice(null);
      await resolveMobileAccess(response.user, true);
    } catch (caught) {
      setError(asApiProblem(caught));
      setStatus("blocked");
      throw caught;
    }
  }, [resolveMobileAccess]);

  const signOut = useCallback(async () => {
    try {
      await mobileApi.logout();
    } catch {
      // Local credentials must still be removed when the session is already expired.
    } finally {
      await Promise.all([clearTokens(), clearOfflineGrant()]);
      setUser(null);
      setBootstrap(null);
      setOfflineGrant(null);
      setOfflineGrantError(null);
      setPendingDevice(null);
      setPendingLogin(null);
      setError(null);
      setStatus("signedOut");
    }
  }, []);

  useEffect(() => {
    void (async () => {
      try {
        const savedProfile = await loadServerProfile();
        if (savedProfile) setProfile(validateServerProfile(savedProfile));
        const tokens = await loadTokens();
        if (!savedProfile || !tokens) {
          await clearOfflineGrant();
          setStatus("signedOut");
          return;
        }
        const currentUser = await mobileApi.me();
        setUser(currentUser);
        await resolveMobileAccess(currentUser, true);
      } catch (caught) {
        await Promise.all([clearTokens(), clearOfflineGrant()]);
        setUser(null);
        setOfflineGrant(null);
        setError(asApiProblem(caught));
        setStatus("signedOut");
      }
    })();
  }, [resolveMobileAccess]);

  useEffect(() => {
    if (!offlineGrant) return undefined;
    const remaining = Date.parse(offlineGrant.expiresAtUtc) - Date.now();
    if (!Number.isFinite(remaining) || remaining <= 0) {
      void clearOfflineGrant();
      setOfflineGrant(null);
      return undefined;
    }
    const timeout = setTimeout(() => {
      void clearOfflineGrant();
      setOfflineGrant(null);
    }, Math.min(remaining + 1_000, 2_147_000_000));
    return () => clearTimeout(timeout);
  }, [offlineGrant]);

  const value = useMemo<SessionContextValue>(() => ({
    status,
    profile,
    user,
    bootstrap,
    offlineGrant,
    offlineGrantBusy,
    offlineGrantError,
    pendingDevice,
    error,
    signIn,
    submitMfa,
    refreshBootstrap,
    requestOfflineGrant,
    switchTenant,
    signOut,
  }), [status, profile, user, bootstrap, offlineGrant, offlineGrantBusy, offlineGrantError, pendingDevice, error, signIn, submitMfa, refreshBootstrap, requestOfflineGrant, switchTenant, signOut]);

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): SessionContextValue {
  const value = useContext(SessionContext);
  if (!value) throw new Error("useSession must be used inside SessionProvider.");
  return value;
}
