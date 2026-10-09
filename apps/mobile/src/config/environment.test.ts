import { describe, expect, it } from "vitest";
import { environmentColor, validateServerProfile } from "@/src/config/environment";

describe("Mobile POS server profile", () => {
  it("normalizes the server origin and removes trailing slashes", () => {
    expect(validateServerProfile({ environment: "UAT", apiBaseUrl: "https://uat.example.com///" })).toEqual({
      environment: "UAT",
      apiBaseUrl: "https://uat.example.com",
    });
  });

  it("requires HTTPS for production", () => {
    expect(() => validateServerProfile({ environment: "PRODUCTION", apiBaseUrl: "http://erp.example.com" }))
      .toThrow("Production Mobile POS connections require HTTPS.");
  });

  it.each([
    "https://user:secret@erp.example.com",
    "https://erp.example.com?tenant=other",
    "https://erp.example.com/#token",
  ])("rejects a server URL containing credentials or request state: %s", apiBaseUrl => {
    expect(() => validateServerProfile({ environment: "TEST", apiBaseUrl }))
      .toThrow("Use only the RHEMA ERP server origin");
  });

  it("exposes a distinct environment color", () => {
    expect(new Set([
      environmentColor("TEST"),
      environmentColor("UAT"),
      environmentColor("PRODUCTION"),
    ]).size).toBe(3);
  });
});
