'use client';

import { Suspense, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { AlertTriangle, ArrowLeft, Camera, Check, Cloud, CloudOff, Loader2, QrCode, RotateCw, Save, X } from 'lucide-react';
import jsQR from 'jsqr';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { hasValidMobileSession } from '@/lib/mobile-session';
import {
  inspectionTemplateService,
  InspectionTemplate,
  InspectionTemplateChecklistItem,
} from '@/services/inspectionTemplateService';
import { fleetService, SubmitFleetAssetInspectionDto } from '@/services/fleetService';

const QUEUE_KEY = 'erp.fleet.prestart.pending.v1';
const DRAFT_KEY_PREFIX = 'erp.fleet.prestart.draft.v1';
const CATALOG_KEY = 'erp.fleet.inspection.catalog.v1';
const CATALOG_SYNC_KEY = 'erp.fleet.inspection.catalog.synced-at.v1';
const SUBMITTED_KEY = 'erp.fleet.inspection.submitted.v1';
const MOBILE_FLASH_KEY = 'erp.mobile.flash.v1';
const mobileShellClass = 'light mx-auto min-h-screen max-w-xl bg-gradient-to-b from-emerald-50 via-white to-slate-100 px-4 py-5 text-slate-950';
const mobileInputClass = 'bg-white text-slate-950 placeholder:text-slate-400 [color-scheme:light]';
const mobileTextareaClass = 'bg-white text-slate-950 placeholder:text-slate-400 [color-scheme:light]';

type PackagePayload = {
  templateId: string;
  templateName: string;
  templateCode?: string;
  assetId?: string | null;
  assetName?: string | null;
  assetNumber?: string | null;
  assetCategoryId?: string | null;
  inspectionKind?: string | null;
  sheetType?: string | null;
  allowPhotos?: boolean;
  checklistItems: InspectionTemplateChecklistItem[];
};

type PendingSubmission = {
  assetId: string;
  dto: SubmitFleetAssetInspectionDto;
  savedAtUtc: string;
  templateName?: string | null;
  assetName?: string | null;
  assetNumber?: string | null;
  inspectionKind?: string | null;
  overallResult?: string | null;
};

type SubmittedInspection = {
  inspectionId?: string | null;
  assetId: string;
  assetName?: string | null;
  assetNumber?: string | null;
  templateName?: string | null;
  inspectionKind?: string | null;
  overallResult?: string | null;
  status: string;
  submittedAtUtc: string;
  syncedAtUtc?: string | null;
  clientSubmissionId: string;
  defectId?: string | null;
  workOrderId?: string | null;
};

function readQueue(): PendingSubmission[] {
  try {
    return JSON.parse(localStorage.getItem(QUEUE_KEY) || '[]');
  } catch {
    return [];
  }
}

function writeQueue(items: PendingSubmission[]) {
  localStorage.setItem(QUEUE_KEY, JSON.stringify(items));
}

function readSubmitted(): SubmittedInspection[] {
  try {
    return JSON.parse(localStorage.getItem(SUBMITTED_KEY) || '[]');
  } catch {
    return [];
  }
}

function writeSubmitted(items: SubmittedInspection[]) {
  localStorage.setItem(SUBMITTED_KEY, JSON.stringify(items.slice(0, 100)));
}

function rememberSubmitted(item: SubmittedInspection) {
  const existing = readSubmitted().filter((current) => current.clientSubmissionId !== item.clientSubmissionId);
  writeSubmitted([item, ...existing]);
}

function readCatalog(): InspectionTemplate[] {
  try {
    return JSON.parse(localStorage.getItem(CATALOG_KEY) || '[]');
  } catch {
    return [];
  }
}

function writeCatalog(items: InspectionTemplate[]) {
  localStorage.setItem(CATALOG_KEY, JSON.stringify(items));
  localStorage.setItem(CATALOG_SYNC_KEY, new Date().toISOString());
}

function templateMatchesScan(
  template: InspectionTemplate,
  inspectionKind: string,
  assetId: string,
  assetCategoryId: string,
) {
  const kindMatches = !template.fleetInspectionKind || template.fleetInspectionKind === 'Any' || template.fleetInspectionKind === inspectionKind;
  const assetMatches = !template.assignedAssetId || template.assignedAssetId === assetId;
  const categoryMatches = !template.assignedAssetCategoryId || template.assignedAssetCategoryId === assetCategoryId;
  return template.isActive && template.isQrEnabled && template.mobileOfflineEnabled && kindMatches && assetMatches && categoryMatches;
}

function resolveCachedTemplate(
  catalog: InspectionTemplate[],
  templateId: string,
  inspectionKind: string,
  assetId: string,
  assetCategoryId: string,
) {
  const exact = catalog.find((template) => template.id === templateId);
  if (exact && templateMatchesScan(exact, inspectionKind, assetId, assetCategoryId)) return exact;

  const score = (template: InspectionTemplate) => template.assignedAssetId ? 3 : template.assignedAssetCategoryId ? 2 : 1;
  return catalog
    .filter((template) => templateMatchesScan(template, inspectionKind, assetId, assetCategoryId))
    .sort((left, right) => score(right) - score(left))[0] || null;
}

function normalizeToken(value?: string | null) {
  return (value || '').trim().replace(/[\s_/-]/g, '').toLowerCase();
}

function getChoiceOptions(type?: string | null) {
  const normalizedType = normalizeToken(type);
  if (['yesno', 'boolean'].includes(normalizedType)) return ['Yes', 'No'];
  if (['checklist', 'passfail', 'passfailflag', 'passfailna'].includes(normalizedType)) return ['Pass', 'Flagged', 'Fail'];
  return [];
}

function isFailedResponse(value?: string | null) {
  return ['fail', 'failed', 'no'].includes(normalizeToken(value));
}

function isFlaggedResponse(value?: string | null) {
  return ['flag', 'flagged', 'attention', 'conditionalpass'].includes(normalizeToken(value));
}

type ChoiceButtonVariant = 'default' | 'destructive' | 'outline';

function getChoiceVariant(value: string, selected: boolean): ChoiceButtonVariant {
  if (!selected) return 'outline';
  return isFailedResponse(value) ? 'destructive' : 'default';
}

function fileToDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(new Error('Photo could not be read.'));
    reader.onload = () => resolve(String(reader.result || ''));
    reader.readAsDataURL(file);
  });
}

async function compressImage(file: File): Promise<string> {
  const source = await fileToDataUrl(file);
  if (typeof window === 'undefined') return source;

  return new Promise((resolve) => {
    const image = new window.Image();
    image.onload = () => {
      const maxSide = 960;
      const scale = Math.min(1, maxSide / Math.max(image.width, image.height));
      const width = Math.max(1, Math.round(image.width * scale));
      const height = Math.max(1, Math.round(image.height * scale));
      const canvas = document.createElement('canvas');
      canvas.width = width;
      canvas.height = height;
      const context = canvas.getContext('2d');
      if (!context) {
        resolve(source);
        return;
      }

      context.drawImage(image, 0, 0, width, height);
      resolve(canvas.toDataURL('image/jpeg', 0.74));
    };
    image.onerror = () => resolve(source);
    image.src = source;
  });
}

function FleetInspectionMobilePage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const templateId = searchParams.get('templateId') || '';
  const assetId = searchParams.get('assetId') || '';
  const assetCategoryId = searchParams.get('assetCategoryId') || '';
  const inspectionKind = searchParams.get('inspectionKind') || 'PreTrip';
  const showSubmitted = searchParams.get('submitted') === '1';
  const scannedAssetName = searchParams.get('assetName') || '';
  const scannedAssetNumber = searchParams.get('assetNumber') || '';
  const draftKey = `${DRAFT_KEY_PREFIX}:${templateId}:${assetId}`;
  const shouldStartScanner = searchParams.get('scan') === '1';

  const scannerVideoRef = useRef<HTMLVideoElement | null>(null);
  const scannerCanvasRef = useRef<HTMLCanvasElement | null>(null);
  const scannerStreamRef = useRef<MediaStream | null>(null);
  const scannerTimerRef = useRef<number | null>(null);

  const [packagePayload, setPackagePayload] = useState<PackagePayload | null>(null);
  const [responses, setResponses] = useState<Record<string, string>>({});
  const [photos, setPhotos] = useState<Record<string, string>>({});
  const [notes, setNotes] = useState('');
  const [startedAtUtc, setStartedAtUtc] = useState(() => new Date().toISOString());
  const [online, setOnline] = useState(true);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [pendingCount, setPendingCount] = useState(0);
  const [catalogCount, setCatalogCount] = useState(0);
  const [catalogSyncedAt, setCatalogSyncedAt] = useState<string | null>(null);
  const [syncingCatalog, setSyncingCatalog] = useState(false);
  const [manualQrValue, setManualQrValue] = useState('');
  const [submittedItems, setSubmittedItems] = useState<SubmittedInspection[]>([]);
  const [selectedSubmitted, setSelectedSubmitted] = useState<SubmittedInspection | null>(null);
  const [refreshingSubmittedId, setRefreshingSubmittedId] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [scannerActive, setScannerActive] = useState(false);
  const [scannerStarting, setScannerStarting] = useState(false);
  const [scannerError, setScannerError] = useState<string | null>(null);
  const hasStoredToken = typeof window === 'undefined'
    ? true
    : !!(localStorage.getItem('authToken') || localStorage.getItem('token')) && hasValidMobileSession();

  const syncCatalog = useCallback(async (showFeedback = false) => {
    if (!navigator.onLine) {
      if (showFeedback) setError('Checklist sync requires an internet connection.');
      return [] as InspectionTemplate[];
    }

    setSyncingCatalog(true);
    try {
      const templates = await inspectionTemplateService.getOfflineCatalog();
      writeCatalog(templates);
      setCatalogCount(templates.length);
      setCatalogSyncedAt(localStorage.getItem(CATALOG_SYNC_KEY));
      if (showFeedback) setMessage(`${templates.length} inspection and service sheet(s) are available offline.`);
      return templates;
    } catch (syncError) {
      if (showFeedback) setError(syncError instanceof Error ? syncError.message : 'Fleet checklists could not be synchronized.');
      return [] as InspectionTemplate[];
    } finally {
      setSyncingCatalog(false);
    }
  }, []);

  const loadPackage = useCallback(async () => {
    setLoading(true);
    setError(null);
    setPackagePayload(null);
    if (!templateId) {
      setLoading(false);
      return;
    }

    const cached = resolveCachedTemplate(readCatalog(), templateId, inspectionKind, assetId, assetCategoryId);
    if (cached) {
      setPackagePayload({
        templateId: cached.id,
        templateName: cached.name,
        templateCode: cached.code,
        assetId: assetId || cached.assignedAssetId,
        assetName: scannedAssetName || null,
        assetNumber: scannedAssetNumber || null,
        assetCategoryId: assetCategoryId || cached.assignedAssetCategoryId,
        inspectionKind,
        sheetType: cached.sheetType,
        allowPhotos: cached.allowPhotos,
        checklistItems: cached.checklistItems,
      });
    }

    if (navigator.onLine && templateId) {
      try {
        const remote = await inspectionTemplateService.getQrPackage(templateId, {
          assetId: assetId || null,
          assetCategoryId: assetCategoryId || null,
          inspectionKind,
          includeEmbeddedPayload: false,
        });
        setPackagePayload({
          templateId: remote.templateId,
          templateName: remote.templateName,
          templateCode: remote.templateCode,
          assetId: remote.assetId,
          assetName: remote.assetName,
          assetNumber: remote.assetNumber,
          assetCategoryId: remote.assetCategoryId,
          inspectionKind: remote.inspectionKind,
          sheetType: remote.sheetType,
          allowPhotos: remote.allowPhotos,
          checklistItems: remote.checklistItems,
        });
        setError(null);
      } catch (requestError) {
        if (!cached) setError(requestError instanceof Error ? requestError.message : 'Checklist could not be loaded.');
      }
    } else if (!cached) {
      setError('This checklist is not stored on the device. Connect once and sync Fleet checklists before working offline.');
    }

    setLoading(false);
  }, [assetCategoryId, assetId, inspectionKind, scannedAssetName, scannedAssetNumber, templateId]);

  const syncPending = useCallback(async () => {
    if (!navigator.onLine) return;
    const queue = readQueue();
    if (queue.length === 0) {
      setPendingCount(0);
      return;
    }

    const remaining: PendingSubmission[] = [];
    for (const item of queue) {
      try {
        const inspection = await fleetService.submitAssetInspection(item.assetId, item.dto);
        rememberSubmitted({
          inspectionId: inspection.id,
          assetId: inspection.vehicleAssetId || item.assetId,
          assetName: inspection.vehicleAssetName || item.assetName || null,
          assetNumber: inspection.vehicleAssetNumber || item.assetNumber || null,
          templateName: inspection.inspectionTemplateName || item.templateName || null,
          inspectionKind: inspection.inspectionKind || item.inspectionKind || null,
          overallResult: inspection.overallResult || item.overallResult || null,
          status: inspection.status || 'Submitted',
          submittedAtUtc: inspection.completedAtUtc || new Date().toISOString(),
          syncedAtUtc: inspection.syncedAtUtc || new Date().toISOString(),
          clientSubmissionId: inspection.clientSubmissionId || item.dto.clientSubmissionId,
          defectId: inspection.defectId || null,
          workOrderId: inspection.workOrderId || null,
        });
      } catch {
        remaining.push(item);
      }
    }
    writeQueue(remaining);
    setPendingCount(remaining.length);
    setSubmittedItems(readSubmitted());
    if (remaining.length < queue.length) {
      localStorage.setItem(MOBILE_FLASH_KEY, `${queue.length - remaining.length} pending inspection(s) sent.`);
      setMessage(`${queue.length - remaining.length} pending inspection(s) sent.`);
    }
  }, []);

  const stopScanner = useCallback(() => {
    if (scannerTimerRef.current !== null) {
      window.clearInterval(scannerTimerRef.current);
      scannerTimerRef.current = null;
    }

    scannerStreamRef.current?.getTracks().forEach((track) => track.stop());
    scannerStreamRef.current = null;

    if (scannerVideoRef.current) {
      scannerVideoRef.current.srcObject = null;
    }

    setScannerActive(false);
    setScannerStarting(false);
  }, []);

  const openQrLink = useCallback((rawValue: string) => {
    const value = rawValue.trim();
    if (!value) {
      setError('Enter or paste an asset QR link.');
      return;
    }

    try {
      const url = new URL(value, window.location.origin);
      if (url.pathname.startsWith('/mobile/fleet/inspection')) {
        stopScanner();
        router.push(`${url.pathname}${url.search}`);
        return;
      }

      stopScanner();
      window.location.href = url.toString();
    } catch {
      setError('The QR link could not be opened.');
    }
  }, [router, stopScanner]);

  const openQrValue = useCallback(() => {
    openQrLink(manualQrValue);
  }, [manualQrValue, openQrLink]);

  const startScanner = useCallback(async () => {
    setScannerError(null);
    setError(null);

    if (!window.isSecureContext) {
      setScannerError('Camera scanning requires HTTPS. Use the secure mobile URL on the phone.');
      return;
    }

    if (!navigator.mediaDevices?.getUserMedia) {
      setScannerError('This browser does not expose camera scanning. Paste the QR link below instead.');
      return;
    }

    setScannerStarting(true);
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: false,
        video: {
          facingMode: { ideal: 'environment' },
        },
      });

      scannerStreamRef.current = stream;
      if (scannerVideoRef.current) {
        scannerVideoRef.current.srcObject = stream;
        await scannerVideoRef.current.play();
      }

      scannerTimerRef.current = window.setInterval(() => {
        const video = scannerVideoRef.current;
        const canvas = scannerCanvasRef.current;
        if (!video || !canvas || video.readyState < HTMLMediaElement.HAVE_CURRENT_DATA || !video.videoWidth || !video.videoHeight) return;

        const context = canvas.getContext('2d', { willReadFrequently: true });
        if (!context) return;

        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        context.drawImage(video, 0, 0, canvas.width, canvas.height);
        const imageData = context.getImageData(0, 0, canvas.width, canvas.height);
        const code = jsQR(imageData.data, imageData.width, imageData.height, { inversionAttempts: 'dontInvert' });
        if (code?.data) openQrLink(code.data);
      }, 350);

      setScannerActive(true);
    } catch (cameraError) {
      stopScanner();
      setScannerError(cameraError instanceof Error
        ? cameraError.message
        : 'Camera permission was not granted. Paste the QR link below instead.');
    } finally {
      setScannerStarting(false);
    }
  }, [openQrLink, stopScanner]);

  useEffect(() => {
    setOnline(navigator.onLine);
    setPendingCount(readQueue().length);
    setSubmittedItems(readSubmitted());
    const handleOnline = () => {
      setOnline(true);
      void syncCatalog(false).then(() => loadPackage());
      void syncPending();
    };
    const handleOffline = () => setOnline(false);
    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);
    const catalog = readCatalog();
    setCatalogCount(catalog.length);
    setCatalogSyncedAt(localStorage.getItem(CATALOG_SYNC_KEY));
    void (async () => {
      if (navigator.onLine) await syncCatalog(false);
      await loadPackage();
    })();
    void syncPending();
    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, [loadPackage, syncCatalog, syncPending]);

  useEffect(() => {
    if (searchParams.get('sync') === '1') {
      void syncCatalog(true).then(() => loadPackage());
    }
    if (searchParams.get('pending') === '1') {
      void syncPending();
    }
  }, [loadPackage, searchParams, syncCatalog, syncPending]);

  useEffect(() => () => stopScanner(), [stopScanner]);

  useEffect(() => {
    if (shouldStartScanner && hasStoredToken && !templateId && !packagePayload && !scannerActive && !scannerStarting) {
      void startScanner();
    }
  }, [hasStoredToken, packagePayload, scannerActive, scannerStarting, shouldStartScanner, startScanner, templateId]);

  useEffect(() => {
    try {
      const draft = JSON.parse(localStorage.getItem(draftKey) || 'null');
      if (draft) {
        setResponses(draft.responses || {});
        setPhotos(draft.photos || {});
        setNotes(draft.notes || '');
        setStartedAtUtc(draft.startedAtUtc || new Date().toISOString());
      }
    } catch {
      // Ignore malformed local drafts.
    }
  }, [draftKey]);

  useEffect(() => {
    localStorage.setItem(draftKey, JSON.stringify({ responses, photos, notes, startedAtUtc }));
  }, [draftKey, notes, photos, responses, startedAtUtc]);

  const checklistItems = packagePayload?.checklistItems || [];
  const missingRequired = useMemo(
    () => checklistItems.filter((item) => item.required && !responses[item.id]?.trim()),
    [checklistItems, responses],
  );

  const updateItemPhoto = async (itemId: string, file?: File | null) => {
    if (!file) return;
    setError(null);
    try {
      const photo = await compressImage(file);
      setPhotos((current) => ({ ...current, [itemId]: photo }));
    } catch (photoError) {
      setError(photoError instanceof Error ? photoError.message : 'Photo could not be attached.');
    }
  };

  const refreshSubmittedStatus = async (item: SubmittedInspection) => {
    if (!navigator.onLine) {
      setError('Connect to the server to refresh this inspection status.');
      return;
    }

    setRefreshingSubmittedId(item.clientSubmissionId);
    setError(null);
    try {
      const inspections = await fleetService.getAssetInspections(item.assetId, 100);
      const match = inspections.find((inspection) =>
        inspection.id === item.inspectionId || inspection.clientSubmissionId === item.clientSubmissionId);

      if (!match) {
        setError('This inspection was not found on the server yet.');
        return;
      }

      const updated: SubmittedInspection = {
        ...item,
        inspectionId: match.id,
        assetName: match.vehicleAssetName || item.assetName || null,
        assetNumber: match.vehicleAssetNumber || item.assetNumber || null,
        templateName: match.inspectionTemplateName || item.templateName || null,
        inspectionKind: match.inspectionKind || item.inspectionKind || null,
        overallResult: match.overallResult || item.overallResult || null,
        status: match.status || item.status,
        syncedAtUtc: match.syncedAtUtc || item.syncedAtUtc || null,
        defectId: match.defectId || null,
        workOrderId: match.workOrderId || null,
      };
      rememberSubmitted(updated);
      const next = readSubmitted();
      setSubmittedItems(next);
      setSelectedSubmitted(updated);
      setMessage('Inspection status refreshed.');
    } catch (refreshError) {
      setError(refreshError instanceof Error ? refreshError.message : 'Inspection status could not be refreshed.');
    } finally {
      setRefreshingSubmittedId(null);
    }
  };

  const submit = async () => {
    const resolvedAssetId = assetId || packagePayload?.assetId || '';
    if (!packagePayload?.templateId || !resolvedAssetId) {
      setError('This QR package is not assigned to an asset.');
      return;
    }
    if (missingRequired.length > 0) {
      setError(`${missingRequired.length} required checklist item(s) are incomplete.`);
      return;
    }

    const values = Object.values(responses);
    const overallResult = values.some(isFailedResponse)
      ? 'Fail'
      : values.some(isFlaggedResponse)
        ? 'ConditionalPass'
        : 'Pass';
    const dto: SubmitFleetAssetInspectionDto = {
      inspectionTemplateId: packagePayload.templateId,
      clientSubmissionId: crypto.randomUUID(),
      startedAtUtc,
      completedAtUtc: new Date().toISOString(),
      capturedOfflineAtUtc: online ? null : new Date().toISOString(),
      inspectionKind: packagePayload.inspectionKind || inspectionKind,
      overallResult,
      inspectionData: JSON.stringify({
        schema: 'FleetTripInspectionChecklist.v1',
        checklist: checklistItems.map((item) => ({
          id: item.id,
          item: item.item,
          type: item.type,
          required: !!item.required,
          order: item.order,
          value: responses[item.id] || '',
          photo: photos[item.id] || null,
        })),
      }),
      notes: notes.trim() || null,
    };

    setSubmitting(true);
    setError(null);
    try {
      if (!navigator.onLine) throw new Error('offline');
      const inspection = await fleetService.submitAssetInspection(resolvedAssetId, dto);
      rememberSubmitted({
        inspectionId: inspection.id,
        assetId: inspection.vehicleAssetId || resolvedAssetId,
        assetName: inspection.vehicleAssetName || packagePayload.assetName || null,
        assetNumber: inspection.vehicleAssetNumber || packagePayload.assetNumber || null,
        templateName: inspection.inspectionTemplateName || packagePayload.templateName || null,
        inspectionKind: inspection.inspectionKind || packagePayload.inspectionKind || inspectionKind,
        overallResult: inspection.overallResult || overallResult,
        status: inspection.status || 'Submitted',
        submittedAtUtc: inspection.completedAtUtc || new Date().toISOString(),
        syncedAtUtc: inspection.syncedAtUtc || new Date().toISOString(),
        clientSubmissionId: inspection.clientSubmissionId || dto.clientSubmissionId,
        defectId: inspection.defectId || null,
        workOrderId: inspection.workOrderId || null,
      });
      localStorage.removeItem(draftKey);
      localStorage.setItem(MOBILE_FLASH_KEY, 'Inspection sent successfully.');
      setMessage('Inspection sent successfully.');
      setTimeout(() => router.replace('/mobile?sent=1'), 900);
    } catch {
      const queue = readQueue();
      if (!queue.some((item) => item.dto.clientSubmissionId === dto.clientSubmissionId)) {
        queue.push({
          assetId: resolvedAssetId,
          dto,
          savedAtUtc: new Date().toISOString(),
          templateName: packagePayload.templateName,
          assetName: packagePayload.assetName,
          assetNumber: packagePayload.assetNumber,
          inspectionKind: packagePayload.inspectionKind || inspectionKind,
          overallResult,
        });
      }
      writeQueue(queue);
      setPendingCount(queue.length);
      rememberSubmitted({
        assetId: resolvedAssetId,
        assetName: packagePayload.assetName || null,
        assetNumber: packagePayload.assetNumber || null,
        templateName: packagePayload.templateName || null,
        inspectionKind: packagePayload.inspectionKind || inspectionKind,
        overallResult,
        status: 'Queued',
        submittedAtUtc: new Date().toISOString(),
        syncedAtUtc: null,
        clientSubmissionId: dto.clientSubmissionId,
      });
      localStorage.removeItem(draftKey);
      localStorage.setItem(MOBILE_FLASH_KEY, 'Inspection saved and will send when the phone is online.');
      setMessage('Inspection saved and will send when the phone is online.');
      setTimeout(() => router.replace('/mobile?queued=1'), 900);
    } finally {
      setSubmitting(false);
    }
  };

  if (loading && !packagePayload) {
    return <div className="light flex min-h-screen items-center justify-center bg-slate-50 text-slate-900" style={{ colorScheme: 'light' }}><Loader2 className="h-6 w-6 animate-spin" /></div>;
  }

  if (!hasStoredToken) {
    const redirect = typeof window === 'undefined' ? '/mobile/fleet/inspection' : `${window.location.pathname}${window.location.search}`;
    return (
      <main className="light mx-auto flex min-h-screen max-w-xl flex-col justify-center bg-gradient-to-b from-emerald-50 via-white to-slate-100 px-4 py-5 text-slate-950" style={{ colorScheme: 'light' }}>
        <Card className="rounded-2xl border-emerald-100 bg-white text-slate-950 shadow-xl shadow-slate-200/70">
          <CardContent className="space-y-4 p-5 text-center">
            <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-md bg-emerald-600 text-white shadow-lg shadow-emerald-200">
              <QrCode className="h-7 w-7" />
            </div>
            <div>
              <h1 className="text-xl font-semibold">Mobile Login</h1>
              <p className="mt-1 text-sm text-muted-foreground">Sign in to continue this inspection.</p>
            </div>
            <Button className="h-11 w-full" onClick={() => router.push(`/mobile?redirect=${encodeURIComponent(redirect)}`)}>
              Login
            </Button>
          </CardContent>
        </Card>
      </main>
    );
  }

  if (showSubmitted) {
    return (
      <main className={mobileShellClass} style={{ colorScheme: 'light' }}>
        <div className="mb-4 flex items-center justify-between gap-3">
          <div>
            <p className="text-xs font-medium text-muted-foreground">MOBILE INSPECTION</p>
            <h1 className="text-xl font-semibold">Submitted Requests</h1>
          </div>
          <Button variant="outline" onClick={() => router.push('/mobile')}>
            <ArrowLeft className="mr-2 h-4 w-4" />
            Back
          </Button>
        </div>

        {message && <div className="mb-4 rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-800">{message}</div>}
        {error && <div className="mb-4 flex gap-2 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />{error}</div>}

        <div className="space-y-3">
          {submittedItems.length === 0 ? (
            <div className="rounded-md border border-slate-200 bg-white p-5 text-center text-sm text-slate-500 shadow-sm">
              No submitted inspections on this device yet.
            </div>
          ) : submittedItems.map((item) => {
            const selected = selectedSubmitted?.clientSubmissionId === item.clientSubmissionId;
            return (
              <div
                key={item.clientSubmissionId}
                role="button"
                tabIndex={0}
                onClick={() => setSelectedSubmitted(selected ? null : item)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter' || event.key === ' ') setSelectedSubmitted(selected ? null : item);
                }}
                className="w-full rounded-md border border-slate-200 bg-white p-4 text-left text-slate-950 shadow-sm"
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <div className="truncate font-semibold">{item.assetNumber || item.assetName || 'Asset inspection'}</div>
                    <div className="truncate text-sm text-muted-foreground">{item.templateName || 'Inspection sheet'}</div>
                    <div className="mt-1 text-xs text-muted-foreground">{new Date(item.submittedAtUtc).toLocaleString()}</div>
                  </div>
                  <Badge variant={item.status === 'Queued' ? 'secondary' : item.status === 'Rejected' ? 'destructive' : 'default'}>
                    {item.status}
                  </Badge>
                </div>
                {selected ? (
                  <div className="mt-4 space-y-2 border-t pt-3 text-sm">
                    <div className="flex justify-between gap-3"><span className="text-muted-foreground">Result</span><span>{item.overallResult || '-'}</span></div>
                    <div className="flex justify-between gap-3"><span className="text-muted-foreground">Kind</span><span>{item.inspectionKind || '-'}</span></div>
                    <div className="flex justify-between gap-3"><span className="text-muted-foreground">Synced</span><span>{item.syncedAtUtc ? new Date(item.syncedAtUtc).toLocaleString() : 'Not yet'}</span></div>
                    <div className="flex justify-between gap-3"><span className="text-muted-foreground">Work Order</span><span>{item.workOrderId || '-'}</span></div>
                    <Button
                      type="button"
                      variant="outline"
                      className="mt-2 h-10 w-full"
                      disabled={!online || refreshingSubmittedId === item.clientSubmissionId}
                      onClick={(event) => {
                        event.stopPropagation();
                        void refreshSubmittedStatus(item);
                      }}
                    >
                      {refreshingSubmittedId === item.clientSubmissionId ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <RotateCw className="mr-2 h-4 w-4" />}
                      Refresh status
                    </Button>
                  </div>
                ) : null}
              </div>
            );
          })}
        </div>
      </main>
    );
  }

  if (!templateId && !packagePayload) {
    return (
      <main className={mobileShellClass} style={{ colorScheme: 'light' }}>
        <div className="mb-4 flex items-center justify-between gap-3">
          <div>
            <p className="text-xs font-medium text-muted-foreground">MOBILE INSPECTION</p>
            <h1 className="text-xl font-semibold">Asset QR</h1>
          </div>
          <Button variant="outline" onClick={() => router.push('/mobile')}>
            <ArrowLeft className="mr-2 h-4 w-4" />
            Back
          </Button>
        </div>

        <div className="mb-2 flex flex-wrap items-center justify-between gap-2 rounded-md border border-slate-200 bg-white/80 px-3 py-2 text-sm text-slate-700 shadow-sm">
          <span>{catalogCount} sheets offline | {pendingCount} pending</span>
          <div className="flex items-center gap-1">
            <Button size="sm" variant="ghost" onClick={() => void syncCatalog(true)} disabled={!online || syncingCatalog}>
              <RotateCw className={`mr-1 h-4 w-4 ${syncingCatalog ? 'animate-spin' : ''}`} /> Checklists
            </Button>
            <Button size="sm" variant="ghost" onClick={() => void syncPending()} disabled={!online || pendingCount === 0}>
              <Cloud className="mr-1 h-4 w-4" /> Submit
            </Button>
          </div>
        </div>

        {message && <div className="mb-4 rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-800">{message}</div>}
        {error && <div className="mb-4 flex gap-2 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />{error}</div>}

        <Card className="rounded-2xl border-emerald-100 bg-white text-slate-950 shadow-xl shadow-slate-200/70">
          <CardContent className="space-y-4 p-5">
            <div className="space-y-3 text-center">
              <div className="mx-auto flex h-20 w-20 items-center justify-center rounded-2xl bg-emerald-600 text-white shadow-lg shadow-emerald-200">
                <QrCode className="h-10 w-10" />
              </div>
              <div>
                <h2 className="text-lg font-semibold">Scan asset QR</h2>
                <p className="mt-1 text-sm text-slate-500">Point the phone camera at the asset label to open its checklist.</p>
              </div>
            </div>

            <div className="overflow-hidden rounded-2xl border border-slate-200 bg-slate-950">
              <video
                ref={scannerVideoRef}
                muted
                playsInline
                className={`aspect-[4/3] w-full object-cover ${scannerActive || scannerStarting ? 'block' : 'hidden'}`}
              />
              <canvas ref={scannerCanvasRef} className="hidden" />
              {!scannerActive && !scannerStarting ? (
                <div className="flex aspect-[4/3] flex-col items-center justify-center gap-3 bg-slate-900 px-6 text-center text-white">
                  <Camera className="h-10 w-10 text-emerald-300" />
                  <p className="text-sm text-slate-200">Tap Start camera to scan with this browser.</p>
                </div>
              ) : null}
            </div>

            {scannerError ? (
              <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
                {scannerError}
              </div>
            ) : null}

            <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
              <Button className="h-11 w-full" onClick={() => void startScanner()} disabled={scannerStarting || scannerActive}>
                {scannerStarting ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Camera className="mr-2 h-4 w-4" />}
                {scannerActive ? 'Scanning...' : 'Start camera'}
              </Button>
              <Button type="button" variant="outline" className="h-11 w-full" onClick={stopScanner} disabled={!scannerActive && !scannerStarting}>
                Stop camera
              </Button>
            </div>

            <div className="flex items-center gap-3 text-xs uppercase tracking-wide text-slate-400">
              <span className="h-px flex-1 bg-slate-200" />
              Paste fallback
              <span className="h-px flex-1 bg-slate-200" />
            </div>

            <div className="space-y-2">
              <Label htmlFor="asset-qr-link" className="text-slate-700">Asset QR link</Label>
              <Input
                id="asset-qr-link"
                className={mobileInputClass}
                value={manualQrValue}
                onChange={(event) => setManualQrValue(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') openQrValue();
                }}
              />
            </div>
            <Button className="h-11 w-full" onClick={openQrValue}>
              <QrCode className="mr-2 h-4 w-4" />
              Open checklist
            </Button>
          </CardContent>
        </Card>
      </main>
    );
  }

  return (
    <main className={mobileShellClass} style={{ colorScheme: 'light' }}>
      <div className="mb-4 flex items-start justify-between gap-3">
        <div className="min-w-0">
          <Button variant="ghost" className="-ml-3 mb-2 h-8 px-2" onClick={() => router.push('/mobile')}>
            <ArrowLeft className="mr-1 h-4 w-4" />
            Back
          </Button>
          <p className="text-xs font-medium text-muted-foreground">
            {(packagePayload?.sheetType || 'InspectionSheet').replace(/([a-z])([A-Z])/g, '$1 $2').toUpperCase()}
          </p>
          <h1 className="truncate text-xl font-semibold">{packagePayload?.assetName || 'Fleet asset'}</h1>
          <p className="truncate text-sm text-muted-foreground">{packagePayload?.assetNumber || packagePayload?.templateName}</p>
        </div>
        <Badge variant={online ? 'default' : 'secondary'} className="gap-1">
          {online ? <Cloud className="h-3.5 w-3.5" /> : <CloudOff className="h-3.5 w-3.5" />}
          {online ? 'Online' : 'Offline'}
        </Badge>
      </div>

      <div className="mb-2 flex flex-wrap items-center justify-between gap-2 rounded-md border border-slate-200 bg-white/80 px-3 py-2 text-sm text-slate-700 shadow-sm">
        <span>{catalogCount} sheets offline | {pendingCount} pending</span>
        <div className="flex items-center gap-1">
          <Button size="sm" variant="ghost" onClick={() => void syncCatalog(true).then(() => loadPackage())} disabled={!online || syncingCatalog}>
            <RotateCw className={`mr-1 h-4 w-4 ${syncingCatalog ? 'animate-spin' : ''}`} /> Checklists
          </Button>
          <Button size="sm" variant="ghost" onClick={() => void syncPending()} disabled={!online || pendingCount === 0}>
            <Cloud className="mr-1 h-4 w-4" /> Submit
          </Button>
        </div>
      </div>

      {catalogSyncedAt && (
        <p className="mb-4 text-xs text-muted-foreground">Checklist catalog updated {new Date(catalogSyncedAt).toLocaleString()}.</p>
      )}

      {message && <div className="mb-4 rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-800">{message}</div>}
      {error && <div className="mb-4 flex gap-2 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />{error}</div>}

      <Card className="rounded-2xl border-emerald-100 bg-white text-slate-950 shadow-xl shadow-slate-200/70">
        <CardHeader className="pb-3">
          <CardTitle className="text-base">{packagePayload?.templateName || 'Checklist'}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {checklistItems.map((item, index) => {
            const choiceOptions = getChoiceOptions(item.type);
            return (
              <div key={item.id} className="space-y-2 border-b pb-4 last:border-0">
                <Label className="text-sm leading-5">{index + 1}. {item.item}{item.required ? ' *' : ''}</Label>
                {choiceOptions.length > 0 ? (
                  <div className={`grid gap-2 ${choiceOptions.length === 2 ? 'grid-cols-2' : 'grid-cols-3'}`}>
                    {choiceOptions.map((value) => {
                      const selected = responses[item.id] === value;
                      return (
                        <Button
                          key={value}
                          type="button"
                          variant={getChoiceVariant(value, selected)}
                          className="h-10 px-2"
                          onClick={() => setResponses((current) => ({ ...current, [item.id]: value }))}
                        >
                          {selected && <Check className="mr-1 h-4 w-4" />}{value}
                        </Button>
                      );
                    })}
                  </div>
                ) : item.type === 'text' ? (
                  <Textarea className={mobileTextareaClass} value={responses[item.id] || ''} onChange={(event) => setResponses((current) => ({ ...current, [item.id]: event.target.value }))} />
                ) : (
                  <Input className={mobileInputClass} type="number" inputMode="decimal" value={responses[item.id] || ''} onChange={(event) => setResponses((current) => ({ ...current, [item.id]: event.target.value }))} />
                )}
                {packagePayload?.allowPhotos ? (
                  <div className="space-y-2">
                    <input
                      id={`photo-${item.id}`}
                      type="file"
                      accept="image/*"
                      capture="environment"
                      className="hidden"
                      onChange={(event) => {
                        const file = event.target.files?.[0];
                        void updateItemPhoto(item.id, file);
                        event.currentTarget.value = '';
                      }}
                    />
                    <div className="flex flex-wrap items-center gap-2">
                      <label
                        htmlFor={`photo-${item.id}`}
                        className="inline-flex h-10 cursor-pointer items-center justify-center rounded-md border border-slate-200 bg-white px-3 text-sm font-medium text-slate-800 shadow-sm hover:bg-slate-50"
                      >
                        <Camera className="mr-2 h-4 w-4" />
                        Photo
                      </label>
                      {photos[item.id] ? (
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          onClick={() => setPhotos((current) => {
                            const next = { ...current };
                            delete next[item.id];
                            return next;
                          })}
                        >
                          <X className="mr-1 h-4 w-4" />
                          Remove
                        </Button>
                      ) : (
                        <span className="text-xs text-muted-foreground">Optional evidence</span>
                      )}
                    </div>
                    {photos[item.id] ? (
                      <div className="overflow-hidden rounded-md border bg-muted/30">
                        <img src={photos[item.id]} alt={`${item.item} evidence`} className="h-36 w-full object-cover" />
                      </div>
                    ) : null}
                  </div>
                ) : null}
              </div>
            );
          })}

          <div className="space-y-2">
            <Label htmlFor="inspection-notes">Notes</Label>
            <Textarea id="inspection-notes" className={mobileTextareaClass} value={notes} onChange={(event) => setNotes(event.target.value)} />
          </div>

          <Button className="h-11 w-full" onClick={submit} disabled={submitting || !packagePayload}>
            {submitting ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
            {online ? 'Submit inspection' : 'Save inspection'}
          </Button>
        </CardContent>
      </Card>
    </main>
  );
}

export default function FleetInspectionMobileRoute() {
  return <Suspense fallback={<div className="light flex min-h-screen items-center justify-center bg-slate-50 text-slate-900" style={{ colorScheme: 'light' }}><Loader2 className="h-6 w-6 animate-spin" /></div>}><FleetInspectionMobilePage /></Suspense>;
}
