import type { RuntimeEnvironment, ServerProfile } from "@/src/types/api";

const configuredEnvironment = process.env.EXPO_PUBLIC_RHEMA_ENVIRONMENT;
const configuredBaseUrl = process.env.EXPO_PUBLIC_API_BASE_URL?.trim();
const isDevelopment = typeof __DEV__ !== "undefined" && __DEV__;

export const defaultServerProfile: ServerProfile = {
  environment: configuredEnvironment ?? "TEST",
  apiBaseUrl: configuredBaseUrl ?? (isDevelopment ? "http://10.0.2.2:5000" : ""),
};

export function validateServerProfile(input: ServerProfile): ServerProfile {
  const environment = input.environment.toUpperCase() as RuntimeEnvironment;
  if (!["TEST", "UAT", "PRODUCTION"].includes(environment)) {
    throw new Error("Select TEST, UAT, or PRODUCTION.");
  }

  const raw = input.apiBaseUrl.trim().replace(/\/+$/, "");
  if (!raw) throw new Error("Enter the RHEMA ERP server URL.");

  let url: URL;
  try {
    url = new URL(raw);
  } catch {
    throw new Error("Enter a valid absolute server URL.");
  }
  if (!['http:', 'https:'].includes(url.protocol)) {
    throw new Error("The server URL must use HTTP or HTTPS.");
  }
  if (environment === "PRODUCTION" && url.protocol !== "https:") {
    throw new Error("Production Mobile POS connections require HTTPS.");
  }
  if (url.username || url.password || url.search || url.hash) {
    throw new Error("Use only the RHEMA ERP server origin, without credentials, query text, or fragments.");
  }

  return { environment, apiBaseUrl: raw };
}

export function environmentColor(environment: RuntimeEnvironment): string {
  if (environment === "PRODUCTION") return "#B42318";
  if (environment === "UAT") return "#B54708";
  return "#175CD3";
}
