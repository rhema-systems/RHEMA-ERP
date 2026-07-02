import { getStoredToken } from '@/services/api.service';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'https://localhost:53484/api';

export const DOCUMENT_TYPES = {
  financeJournalVoucher: 'Finance.JournalVoucher',
  financeTrialBalance: 'Finance.TrialBalance',
  financeIncomeStatement: 'Finance.IncomeStatement',
  financeBalanceSheet: 'Finance.BalanceSheet',
  financeCashFlowStatement: 'Finance.CashFlowStatement',
  financeMultiCurrencyDetail: 'Finance.MultiCurrencyDetail',
  financeDetailedLedger: 'Finance.DetailedLedger',
} as const;

export interface RenderedDocumentFile {
  blob: Blob;
  fileName: string;
  contentType: string;
}

type DocumentOptions = { format?: string; copyType?: string };
type DocumentParameters = Record<string, string | number | boolean | string[] | null | undefined>;

class DocumentOutputService {
  async fetchDocument(
    documentType: string,
    entityId: string,
    options: DocumentOptions = {}
  ): Promise<RenderedDocumentFile> {
    const format = options.format || 'pdf';
    const copyType = options.copyType || 'Original';
    const url = new URL(`${API_BASE_URL}/documents/${encodeURIComponent(documentType)}/${encodeURIComponent(entityId)}`);
    url.searchParams.set('format', format);
    url.searchParams.set('copyType', copyType);

    const token = getStoredToken();
    const response = await fetch(url.toString(), {
      method: 'GET',
      headers: {
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
    });

    if (!response.ok) {
      const message = await this.readErrorMessage(response);
      throw new Error(message || `Failed to render document (${response.status} ${response.statusText})`);
    }

    const blob = await response.blob();
    const contentType = response.headers.get('content-type') || blob.type || 'application/octet-stream';
    const fileName = this.getFileName(response.headers.get('content-disposition'), `${documentType}-${entityId}.${format}`);

    return { blob, fileName, contentType };
  }

  async fetchReportDocument(
    documentType: string,
    parameters: DocumentParameters = {},
    options: DocumentOptions = {}
  ): Promise<RenderedDocumentFile> {
    const format = options.format || 'pdf';
    const copyType = options.copyType || 'Original';
    const url = new URL(`${API_BASE_URL}/documents/${encodeURIComponent(documentType)}`);
    url.searchParams.set('format', format);
    url.searchParams.set('copyType', copyType);
    this.appendParameters(url, parameters);

    const token = getStoredToken();
    const response = await fetch(url.toString(), {
      method: 'GET',
      headers: {
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
    });

    if (!response.ok) {
      const message = await this.readErrorMessage(response);
      throw new Error(message || `Failed to render document (${response.status} ${response.statusText})`);
    }

    const blob = await response.blob();
    const contentType = response.headers.get('content-type') || blob.type || 'application/octet-stream';
    const fileName = this.getFileName(response.headers.get('content-disposition'), `${documentType}.${format}`);

    return { blob, fileName, contentType };
  }

  async downloadDocument(
    documentType: string,
    entityId: string,
    options: DocumentOptions = {}
  ): Promise<void> {
    const file = await this.fetchDocument(documentType, entityId, options);
    this.downloadFile(file);
  }

  async downloadReportDocument(
    documentType: string,
    parameters: DocumentParameters = {},
    options: DocumentOptions = {}
  ): Promise<void> {
    const file = await this.fetchReportDocument(documentType, parameters, options);
    this.downloadFile(file);
  }

  async printDocument(
    documentType: string,
    entityId: string,
    options: DocumentOptions = {}
  ): Promise<void> {
    const file = await this.fetchDocument(documentType, entityId, { ...options, format: options.format || 'pdf' });
    await this.printFile(file);
  }

  async printReportDocument(
    documentType: string,
    parameters: DocumentParameters = {},
    options: DocumentOptions = {}
  ): Promise<void> {
    const file = await this.fetchReportDocument(documentType, parameters, { ...options, format: options.format || 'pdf' });
    await this.printFile(file);
  }

  private downloadFile(file: RenderedDocumentFile): void {
    const objectUrl = URL.createObjectURL(file.blob);

    try {
      const link = document.createElement('a');
      link.href = objectUrl;
      link.download = file.fileName;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    } finally {
      URL.revokeObjectURL(objectUrl);
    }
  }

  private async printFile(file: RenderedDocumentFile): Promise<void> {
    const objectUrl = URL.createObjectURL(file.blob);
    const iframe = document.createElement('iframe');
    iframe.style.position = 'fixed';
    iframe.style.right = '0';
    iframe.style.bottom = '0';
    iframe.style.width = '0';
    iframe.style.height = '0';
    iframe.style.border = '0';
    iframe.title = file.fileName;

    const cleanup = () => {
      setTimeout(() => {
        if (iframe.parentNode) {
          iframe.parentNode.removeChild(iframe);
        }
        URL.revokeObjectURL(objectUrl);
      }, 10_000);
    };

    await new Promise<void>((resolve, reject) => {
      iframe.onload = () => {
        try {
          iframe.contentWindow?.focus();
          iframe.contentWindow?.print();
          resolve();
        } catch (error) {
          const popup = window.open(objectUrl, '_blank', 'noopener,noreferrer');
          if (!popup) {
            reject(error);
            return;
          }
          resolve();
        } finally {
          cleanup();
        }
      };

      iframe.onerror = () => {
        cleanup();
        reject(new Error('Unable to open the generated document for printing.'));
      };

      iframe.src = objectUrl;
      document.body.appendChild(iframe);
    });
  }

  private appendParameters(url: URL, parameters: DocumentParameters): void {
    Object.entries(parameters).forEach(([key, value]) => {
      if (value === null || value === undefined || value === '') return;
      if (Array.isArray(value)) {
        if (value.length > 0) {
          url.searchParams.set(key, value.join(','));
        }
        return;
      }

      url.searchParams.set(key, String(value));
    });
  }

  private async readErrorMessage(response: Response): Promise<string | null> {
    const text = await response.text().catch(() => '');
    if (!text) return null;

    try {
      const data = JSON.parse(text);
      return data.message || data.title || data.error || text;
    } catch {
      return text;
    }
  }

  private getFileName(contentDisposition: string | null, fallback: string): string {
    if (!contentDisposition) {
      return this.safeFileName(fallback);
    }

    const encoded = contentDisposition.match(/filename\*=UTF-8''([^;]+)/i)?.[1];
    if (encoded) {
      return this.safeFileName(decodeURIComponent(encoded));
    }

    const plain = contentDisposition.match(/filename="?([^";]+)"?/i)?.[1];
    return this.safeFileName(plain || fallback);
  }

  private safeFileName(fileName: string): string {
    return fileName.replace(/[\\/:*?"<>|]/g, '-');
  }
}

export const documentOutputService = new DocumentOutputService();
