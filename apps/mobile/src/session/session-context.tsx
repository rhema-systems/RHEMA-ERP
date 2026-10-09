import Constants from "expo-constants";
import * as Device from "expo-device";
import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { ApiProblem, mobileApi } from "@/src/api/client";
import { defaultServerProfile, validateServerProfile } from "@/src/config/environment";
import {
  clearTokens,
  getInstallationId,
  loadServerProfile,
  loadTokens,
  saveServerProfile,
  saveTokens,
} from "@/src/storage/secure-session";
import type {
  LoginRequest,
  MobilePosBootstrap,
  MobilePosDevice,
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
  pendingDevice: MobilePosDevice | null;
  error: ApiProblem | null;
  signIn: (profile: ServerProfile, request: LoginRequest) => Promise<void>;
  submitMfa: (code: string) => Promise<void>;
  refreshBootstrap: () => Promise<void>;
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
  const [pendingDevice, setPendingDevice] = useState<MobilePosDevice | null>(null);
  const [error, setError] = useState<ApiProblem | null>(null);
  const [pendingLogin, setPendingLogin] = useState<PendingLogin | null>(null);

  const resolveMobileAccess = useCallback(async (currentUser: UserInfo, allowEnrollment: boolean) => {
    const installationId = await getInstallationId();
    try {
      const result = await mobileApi.bootstrap(installationId);
      setBootstrap(result);
      setPendingDevice(null);
      setError(null);
      setStatus("ready");
      return;
    } catch (caught) {
      const problem = asApiProblem(caught);
      if (!allowEnrollment || !mayRequestEnrollment(currentUser) || ![401, 403, 404].includes(problem.status)) {
        setError(problem);
        setStatus("blocked");
        return;
      }
    }

    try {
      const device = await mobileApi.requestEnrollment(await buildEnrollmentRequest());
      setPendingDevice(device);
      if (isActiveDevice(device)) {
        const result = await mobileApi.bootstrap(installationId);
        setBootstrap(result);
        setPendingDevice(null);
        setError(null);
        setStatus("ready");
      } else {
        setBootstrap(null);
        setError(null);
        setStatus("enrollmentPending");
      }
    } catch (caught) {
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
      const checked = validateServerProfile(nextProfile);
      await saveServerProfile(checked);
      setProfile(checked);
      await completeLogin(request);
    } catch (caught) {
      await clearTokens();
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

  const switchTenant = useCallback(async (tenantCode: string) => {
    setStatus("initializing");
    setError(null);
    try {
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
      await clearTokens();
      setUser(null);
      setBootstrap(null);
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
          setStatus("signedOut");
          return;
        }
        const currentUser = await mobileApi.me();
        setUser(currentUser);
        await resolveMobileAccess(currentUser, true);
      } catch (caught) {
        await clearTokens();
        setUser(null);
        setError(asApiProblem(caught));
        setStatus("signedOut");
      }
    })();
  }, [resolveMobileAccess]);

  const value = useMemo<SessionContextValue>(() => ({
    status,
    profile,
    user,
    bootstrap,
    pendingDevice,
    error,
    signIn,
    submitMfa,
    refreshBootstrap,
    switchTenant,
    signOut,
  }), [status, profile, user, bootstrap, pendingDevice, error, signIn, submitMfa, refreshBootstrap, switchTenant, signOut]);

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): SessionContextValue {
  const value = useContext(SessionContext);
  if (!value) throw new Error("useSession must be used inside SessionProvider.");
  return value;
}
