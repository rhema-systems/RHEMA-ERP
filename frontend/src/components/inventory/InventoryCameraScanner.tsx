'use client';

import { useEffect, useRef, useState } from 'react';
import jsQR from 'jsqr';
import { Camera, CameraOff } from 'lucide-react';
import { Button } from '@/components/ui/button';

type DetectedBarcode = { rawValue: string };
type BarcodeDetectorInstance = { detect(source: CanvasImageSource): Promise<DetectedBarcode[]> };
type BarcodeDetectorConstructor = new (options: { formats: string[] }) => BarcodeDetectorInstance;

export function InventoryCameraScanner({ onScan }: { onScan: (value: string) => void }) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const frameRef = useRef<number | undefined>(undefined);
  const detectorRef = useRef<BarcodeDetectorInstance | null>(null);
  const lastValueRef = useRef<{ value: string; at: number } | undefined>(undefined);
  const [active, setActive] = useState(false);
  const [error, setError] = useState<string>();

  const stop = () => {
    if (frameRef.current) cancelAnimationFrame(frameRef.current);
    streamRef.current?.getTracks().forEach(track => track.stop());
    streamRef.current = null;
    setActive(false);
  };

  useEffect(() => stop, []);

  const emit = (value: string) => {
    const now = Date.now();
    if (value && (lastValueRef.current?.value !== value || now - lastValueRef.current.at > 2000)) {
      lastValueRef.current = { value, at: now };
      onScan(value);
    }
  };

  const decodeFrame = async () => {
    const video = videoRef.current;
    const canvas = canvasRef.current;
    if (!video || !canvas || !streamRef.current) return;
    if (video.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA && video.videoWidth > 0) {
      canvas.width = video.videoWidth;
      canvas.height = video.videoHeight;
      const context = canvas.getContext('2d', { willReadFrequently: true });
      if (context) {
        context.drawImage(video, 0, 0, canvas.width, canvas.height);
        const pixels = context.getImageData(0, 0, canvas.width, canvas.height);
        let detected: DetectedBarcode[] = [];
        try { detected = detectorRef.current ? await detectorRef.current.detect(canvas) : []; }
        catch { detectorRef.current = null; }
        if (detected[0]?.rawValue) emit(detected[0].rawValue);
        else {
          const decoded = jsQR(pixels.data, pixels.width, pixels.height, { inversionAttempts: 'dontInvert' });
          if (decoded?.data) emit(decoded.data);
        }
      }
    }
    if (streamRef.current) frameRef.current = requestAnimationFrame(() => void decodeFrame());
  };

  const start = async () => {
    setError(undefined);
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: { ideal: 'environment' } }, audio: false });
      const Detector = (window as unknown as { BarcodeDetector?: BarcodeDetectorConstructor }).BarcodeDetector;
      detectorRef.current = null;
      if (Detector) {
        try {
          detectorRef.current = new Detector({ formats: ['qr_code', 'code_128', 'ean_13', 'ean_8', 'upc_a', 'upc_e'] });
        } catch {
          // jsQR remains the camera fallback when a browser exposes a partial detector implementation.
        }
      }
      streamRef.current = stream;
      if (videoRef.current) {
        videoRef.current.srcObject = stream;
        await videoRef.current.play();
      }
      setActive(true);
      frameRef.current = requestAnimationFrame(() => void decodeFrame());
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Camera access was denied.');
      stop();
    }
  };

  return (
    <div className="space-y-3 rounded-lg border bg-muted/20 p-3">
      <div className="flex items-center justify-between gap-3">
        <div><p className="text-sm font-medium">Camera scanner</p><p className="text-xs text-muted-foreground">QR and supported retail barcodes; handheld scanners can use manual input.</p></div>
        {active ? <Button type="button" size="sm" variant="outline" onClick={stop}><CameraOff className="mr-2 h-4 w-4" />Stop</Button> :
          <Button type="button" size="sm" variant="outline" onClick={() => void start()}><Camera className="mr-2 h-4 w-4" />Start</Button>}
      </div>
      <video ref={videoRef} playsInline muted className={`aspect-video w-full rounded-md bg-black object-cover ${active ? '' : 'hidden'}`} />
      <canvas ref={canvasRef} className="hidden" />
      {error && <p className="text-xs text-destructive">{error} Use the manual or handheld-scanner field below.</p>}
    </div>
  );
}
