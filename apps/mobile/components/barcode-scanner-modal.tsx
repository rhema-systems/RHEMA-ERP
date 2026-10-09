import { Ionicons } from "@expo/vector-icons";
import { CameraView, useCameraPermissions, type BarcodeScanningResult, type BarcodeType } from "expo-camera";
import { useEffect, useState } from "react";
import { ActivityIndicator, Modal, Pressable, SafeAreaView, StyleSheet, Text, View } from "react-native";
import { createBarcodeScan, type BarcodeScan } from "@/src/scanning/barcode";
import { colors } from "@/src/ui/theme";

const barcodeTypes: BarcodeType[] = [
  "ean13", "ean8", "upc_a", "upc_e", "code128", "code93", "code39", "itf14", "codabar", "datamatrix", "qr",
];

export function BarcodeScannerModal({
  visible,
  onClose,
  onScan,
}: {
  visible: boolean;
  onClose: () => void;
  onScan: (scan: BarcodeScan) => void;
}) {
  const [permission, requestPermission] = useCameraPermissions();
  const [scanLocked, setScanLocked] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!visible) return;
    setScanLocked(false);
    setError(null);
  }, [visible]);

  const acceptScan = (result: BarcodeScanningResult) => {
    if (scanLocked) return;
    try {
      const scan = createBarcodeScan(result.data, "camera", result.type);
      setScanLocked(true);
      onScan(scan);
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "The barcode could not be read.");
    }
  };

  return (
    <Modal animationType="slide" onRequestClose={onClose} statusBarTranslucent visible={visible}>
      <SafeAreaView style={styles.page}>
        <View style={styles.header}>
          <View style={styles.headerText}>
            <Text style={styles.title}>Scan an item</Text>
            <Text style={styles.subtitle}>Align one product barcode inside the frame.</Text>
          </View>
          <Pressable accessibilityLabel="Close barcode scanner" onPress={onClose} style={styles.closeButton}>
            <Ionicons name="close" size={24} color={colors.white} />
          </Pressable>
        </View>

        {!permission ? (
          <View style={styles.permissionState}><ActivityIndicator color={colors.white} /><Text style={styles.permissionText}>Checking camera access...</Text></View>
        ) : !permission.granted ? (
          <View style={styles.permissionState}>
            <Ionicons name="camera-outline" size={38} color={colors.white} />
            <Text style={styles.permissionTitle}>Camera access is required</Text>
            <Text style={styles.permissionText}>RHEMA Field POS uses the camera only to read item barcodes.</Text>
            {permission.canAskAgain ? (
              <Pressable onPress={() => void requestPermission()} style={styles.permissionButton}><Text style={styles.permissionButtonText}>Allow camera</Text></Pressable>
            ) : (
              <Text style={styles.permissionText}>Enable camera access in Android Settings, or use manual or keyboard scanner entry.</Text>
            )}
          </View>
        ) : (
          <View style={styles.cameraWrap}>
            <CameraView
              barcodeScannerSettings={{ barcodeTypes }}
              facing="back"
              onBarcodeScanned={scanLocked ? undefined : acceptScan}
              style={StyleSheet.absoluteFill}
            />
            <View pointerEvents="none" style={styles.guide}>
              <View style={styles.scanFrame} />
              <Text style={styles.guideText}>EAN, UPC, Code 128, Data Matrix and QR are supported.</Text>
            </View>
            {error && <View accessibilityRole="alert" style={styles.errorBox}><Text style={styles.errorText}>{error}</Text></View>}
          </View>
        )}
      </SafeAreaView>
    </Modal>
  );
}

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: "#050B16" },
  header: { minHeight: 94, paddingHorizontal: 20, paddingTop: 20, paddingBottom: 14, flexDirection: "row", alignItems: "center" },
  headerText: { flex: 1 },
  title: { color: colors.white, fontSize: 22, fontWeight: "800" },
  subtitle: { marginTop: 3, color: "#D0D5DD", fontSize: 12 },
  closeButton: { width: 44, height: 44, alignItems: "center", justifyContent: "center", borderRadius: 14, backgroundColor: "rgba(255,255,255,0.12)" },
  cameraWrap: { flex: 1, overflow: "hidden" },
  guide: { ...StyleSheet.absoluteFillObject, alignItems: "center", justifyContent: "center", backgroundColor: "rgba(0,0,0,0.16)" },
  scanFrame: { width: "82%", height: 190, borderWidth: 3, borderColor: "#53B1FD", borderRadius: 20, backgroundColor: "transparent" },
  guideText: { width: "82%", marginTop: 22, color: colors.white, fontSize: 12, lineHeight: 18, textAlign: "center" },
  permissionState: { flex: 1, paddingHorizontal: 30, alignItems: "center", justifyContent: "center" },
  permissionTitle: { marginTop: 16, color: colors.white, fontSize: 19, fontWeight: "700", textAlign: "center" },
  permissionText: { marginTop: 9, color: "#D0D5DD", fontSize: 13, lineHeight: 20, textAlign: "center" },
  permissionButton: { minWidth: 180, minHeight: 48, marginTop: 22, alignItems: "center", justifyContent: "center", borderRadius: 12, backgroundColor: colors.blue },
  permissionButtonText: { color: colors.white, fontSize: 14, fontWeight: "700" },
  errorBox: { position: "absolute", left: 20, right: 20, bottom: 30, padding: 13, borderRadius: 12, backgroundColor: colors.dangerBg },
  errorText: { color: colors.danger, fontSize: 12, lineHeight: 18, textAlign: "center" },
});
