const { withAppBuildGradle } = require("@expo/config-plugins");

const PLUGIN_START = "// RHEMA_ANDROID_RELEASE_SIGNING_START";
const PLUGIN_END = "// RHEMA_ANDROID_RELEASE_SIGNING_END";

const signingBlock = `${PLUGIN_START}
def rhemaReleaseSigningEnabled = (System.getenv("RHEMA_ANDROID_SIGNING_ENABLED") ?: "false").toBoolean()
def rhemaReleaseSigningValues = [
    storeFile: System.getenv("RHEMA_ANDROID_KEYSTORE_PATH"),
    storePassword: System.getenv("RHEMA_ANDROID_KEYSTORE_PASSWORD"),
    keyAlias: System.getenv("RHEMA_ANDROID_KEY_ALIAS"),
    keyPassword: System.getenv("RHEMA_ANDROID_KEY_PASSWORD")
]

gradle.taskGraph.whenReady { graph ->
    def releaseRequested = graph.allTasks.any { task -> task.path.toLowerCase().contains("release") }
    if (releaseRequested) {
        if (!rhemaReleaseSigningEnabled) {
            throw new GradleException("RHEMA Android release signing is not enabled.")
        }
        def missing = rhemaReleaseSigningValues.findAll { entry -> entry.value == null || entry.value.trim().isEmpty() }.keySet()
        if (!missing.isEmpty()) {
            throw new GradleException("Missing RHEMA Android release signing environment values: " + missing.join(", "))
        }
    }
}

if (rhemaReleaseSigningEnabled) {
    android {
        signingConfigs {
            rhemaRelease {
                storeFile file(rhemaReleaseSigningValues.storeFile)
                storePassword rhemaReleaseSigningValues.storePassword
                keyAlias rhemaReleaseSigningValues.keyAlias
                keyPassword rhemaReleaseSigningValues.keyPassword
                enableV1Signing true
                enableV2Signing true
            }
        }
        buildTypes {
            release {
                signingConfig signingConfigs.rhemaRelease
            }
        }
    }
}
${PLUGIN_END}`;

function removeGeneratedGradleBlock(contents) {
  const escape = value => value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const pattern = new RegExp(`${escape(PLUGIN_START)}[\\s\\S]*?${escape(PLUGIN_END)}\\r?\\n?`, "g");
  return contents.replace(pattern, "").trimEnd();
}

function applyReleaseSigningGradle(contents) {
  return `${removeGeneratedGradleBlock(contents)}\n\n${signingBlock}\n`;
}

function withAndroidReleaseSigning(config) {
  return withAppBuildGradle(config, appConfig => {
    appConfig.modResults.contents = applyReleaseSigningGradle(appConfig.modResults.contents);
    return appConfig;
  });
}

module.exports = withAndroidReleaseSigning;
module.exports.applyReleaseSigningGradle = applyReleaseSigningGradle;
module.exports.removeGeneratedGradleBlock = removeGeneratedGradleBlock;
