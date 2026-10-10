const { withAndroidManifest } = require("@expo/config-plugins");

const allowedEnvironments = new Set(["TEST", "UAT", "PRODUCTION"]);

function resolveEnvironment(value = process.env.EXPO_PUBLIC_RHEMA_ENVIRONMENT) {
  const environment = String(value || "TEST").trim().toUpperCase();
  if (!allowedEnvironments.has(environment)) {
    throw new Error(`Unsupported RHEMA mobile environment: ${environment}.`);
  }
  return environment;
}

function applyAndroidSecurityAttributes(androidManifest, environment) {
  const application = androidManifest?.manifest?.application?.[0];
  if (!application) throw new Error("The generated Android manifest has no application element.");
  application.$ = application.$ || {};
  application.$["android:allowBackup"] = "false";
  application.$["android:usesCleartextTraffic"] = environment === "TEST" ? "true" : "false";
  return androidManifest;
}

function withMobileSecurity(config) {
  const environment = resolveEnvironment();
  return withAndroidManifest(config, modConfig => {
    modConfig.modResults = applyAndroidSecurityAttributes(modConfig.modResults, environment);
    return modConfig;
  });
}

module.exports = withMobileSecurity;
module.exports.applyAndroidSecurityAttributes = applyAndroidSecurityAttributes;
module.exports.resolveEnvironment = resolveEnvironment;
