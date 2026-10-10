const crypto = require("crypto");
const fs = require("fs");
const path = require("path");
const { withAppBuildGradle, withDangerousMod } = require("@expo/config-plugins");

const PLUGIN_START = "// RHEMA_ZCS_SMARTPOS_START";
const PLUGIN_END = "// RHEMA_ZCS_SMARTPOS_END";
const DEFAULT_ENABLED_ENV = "RHEMA_ZCS_ENABLED";
const DEFAULT_SDK_DIR_ENV = "RHEMA_ZCS_SDK_DIR";

const artifactDefinitions = [
  {
    key: "jar",
    relativePath: path.join("libs", "SmartPos_1.8.1_R231213.jar"),
    sha256: "3A65BF1A26D59730C014D79BA7B5AA8744BAB6E0B5275A2C055B996F4A7277E9",
    androidPath: path.join("app", "libs", "zcs-smartpos-1.8.1.jar"),
  },
  {
    key: "arm64",
    relativePath: path.join("libs", "arm64-v8a", "libSmartPosJni.so"),
    sha256: "DE5CBF76EAFC3D0FBF1767BD0E8007E6217A2C81A6FDACE02FBD511A1D9FF9BF",
    androidPath: path.join("app", "src", "main", "jniLibs", "arm64-v8a", "libSmartPosJni.so"),
  },
  {
    key: "armv7",
    relativePath: path.join("libs", "armeabi-v7a", "libSmartPosJni.so"),
    sha256: "7D8821DD051F83072023744019F1EDD86AB7CF0B0EAE2D7FD674DF7ED844A090",
    androidPath: path.join("app", "src", "main", "jniLibs", "armeabi-v7a", "libSmartPosJni.so"),
  },
];

function sha256(filePath) {
  return crypto.createHash("sha256").update(fs.readFileSync(filePath)).digest("hex").toUpperCase();
}

function isEnabled(value) {
  return ["1", "true", "yes", "on"].includes(String(value ?? "").trim().toLowerCase());
}

function resolveZcsSdkArtifacts(sdkDirectory) {
  if (!sdkDirectory) {
    throw new Error(`Set ${DEFAULT_SDK_DIR_ENV} to the extracted SmartPos_1.8.1_R231213_SDK directory.`);
  }

  const resolvedDirectory = path.resolve(sdkDirectory);
  if (!fs.existsSync(resolvedDirectory) || !fs.statSync(resolvedDirectory).isDirectory()) {
    throw new Error(`The configured ZCS SDK directory does not exist: ${resolvedDirectory}`);
  }

  return artifactDefinitions.map(definition => {
    const sourcePath = path.join(resolvedDirectory, definition.relativePath);
    if (!fs.existsSync(sourcePath)) {
      throw new Error(`The ZCS SDK artifact is missing: ${sourcePath}`);
    }
    const actualHash = sha256(sourcePath);
    if (actualHash !== definition.sha256) {
      throw new Error(`The ZCS SDK artifact hash is invalid for ${definition.relativePath}. Expected ${definition.sha256}; received ${actualHash}.`);
    }
    return { ...definition, sourcePath, actualHash };
  });
}

function removeGeneratedGradleBlock(contents) {
  const pattern = new RegExp(`${PLUGIN_START.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}[\\s\\S]*?${PLUGIN_END.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}\\r?\\n?`, "g");
  return contents.replace(pattern, "").trimEnd() + "\n";
}

function withZcsSmartPos(config, options = {}) {
  const enabledEnv = options.enabledEnv || DEFAULT_ENABLED_ENV;
  const sdkDirectoryEnv = options.sdkDirectoryEnv || DEFAULT_SDK_DIR_ENV;
  const enabled = isEnabled(process.env[enabledEnv]);

  config = withAppBuildGradle(config, appConfig => {
    let contents = removeGeneratedGradleBlock(appConfig.modResults.contents);
    if (enabled) {
      contents += `\n${PLUGIN_START}\ndependencies {\n    implementation files('libs/zcs-smartpos-1.8.1.jar')\n}\n${PLUGIN_END}\n`;
    }
    appConfig.modResults.contents = contents;
    return appConfig;
  });

  return withDangerousMod(config, ["android", async modConfig => {
    const androidRoot = modConfig.modRequest.platformProjectRoot;
    const destinations = artifactDefinitions.map(item => path.join(androidRoot, item.androidPath));

    if (!enabled) {
      for (const destination of destinations) fs.rmSync(destination, { force: true });
      return modConfig;
    }

    const artifacts = resolveZcsSdkArtifacts(process.env[sdkDirectoryEnv]);
    for (const artifact of artifacts) {
      const destination = path.join(androidRoot, artifact.androidPath);
      fs.mkdirSync(path.dirname(destination), { recursive: true });
      fs.copyFileSync(artifact.sourcePath, destination);
    }
    return modConfig;
  }]);
}

module.exports = withZcsSmartPos;
module.exports.artifactDefinitions = artifactDefinitions;
module.exports.isEnabled = isEnabled;
module.exports.removeGeneratedGradleBlock = removeGeneratedGradleBlock;
module.exports.resolveZcsSdkArtifacts = resolveZcsSdkArtifacts;
