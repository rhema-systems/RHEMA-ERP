const path = require("path");
const { getDefaultConfig } = require("expo/metro-config");

const projectRoot = __dirname;
const config = getDefaultConfig(projectRoot);

const singletons = new Set([
  "react",
  "react-dom",
  "react-native",
  "react-native-web",
  "expo",
  "expo-modules-core",
  "expo-router",
  "react-native-safe-area-context",
  "react-native-screens",
]);

const packageName = (moduleName) => moduleName.startsWith("@")
  ? moduleName.split("/").slice(0, 2).join("/")
  : moduleName.split("/")[0];
const singletonOrigin = path.join(projectRoot, "package.json");
const configuredResolveRequest = config.resolver.resolveRequest;

// Resolve React/RN singleton imports from this application even if the repository later
// adopts a root workspace. This prevents a second web-frontend React copy entering Metro.
config.resolver.resolveRequest = (context, moduleName, platform) => {
  const upstream = configuredResolveRequest ?? context.resolveRequest;
  if (!moduleName.startsWith(".") && !path.isAbsolute(moduleName) && singletons.has(packageName(moduleName))) {
    return upstream({ ...context, originModulePath: singletonOrigin }, moduleName, platform);
  }
  return upstream(context, moduleName, platform);
};

module.exports = config;
