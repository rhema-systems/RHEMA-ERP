/// <reference types="expo/types" />

declare namespace NodeJS {
  interface ProcessEnv {
    EXPO_PUBLIC_RHEMA_ENVIRONMENT?: "TEST" | "UAT" | "PRODUCTION";
    EXPO_PUBLIC_API_BASE_URL?: string;
  }
}
