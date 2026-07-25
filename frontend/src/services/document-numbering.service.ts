import { apiService } from './api.service';
import type {
  DocumentSequenceDefinition,
  GenerateDocumentNumberRequest,
  GeneratedDocumentNumber,
  UpdateDocumentSequenceDefinition,
} from '@/types/document-numbering';

const endpoint = '/document-sequences';

function padSequence(value: number, length: number) {
  return String(value).padStart(Math.max(length, 1), '0');
}

export function formatDocumentNumberSample(
  sequence?: Pick<DocumentSequenceDefinition, 'format' | 'nextNumber' | 'minimumDigits'> | null,
) {
  if (!sequence?.format) return 'Assigned on save';

  const now = new Date();
  const yyyy = String(now.getFullYear());
  const yy = yyyy.slice(-2);
  const mm = String(now.getMonth() + 1).padStart(2, '0');
  const dd = String(now.getDate()).padStart(2, '0');
  const sequenceNumber = Number(sequence.nextNumber || 1);

  return sequence.format
    .replace(/\{YYYY\}/gi, yyyy)
    .replace(/\{YY\}/gi, yy)
    .replace(/\{MM\}/gi, mm)
    .replace(/\{DD\}/gi, dd)
    .replace(/\{SEQ\}/gi, padSequence(sequenceNumber, sequence.minimumDigits || 1))
    .replace(/\{(#+)\}/g, (_match, hashes: string) => padSequence(sequenceNumber, hashes.length));
}

export const documentNumberingService = {
  getDefinitions(module?: string): Promise<DocumentSequenceDefinition[]> {
    return apiService.get<DocumentSequenceDefinition[]>(endpoint, module ? { module } : undefined);
  },

  async getDefinition(module: string, documentType: string): Promise<DocumentSequenceDefinition | null> {
    const definitions = await this.getDefinitions(module);
    return definitions.find((definition) =>
      definition.documentType === documentType &&
      definition.isActive &&
      definition.isDefault
    ) ?? definitions.find((definition) =>
      definition.documentType === documentType &&
      definition.isActive
    ) ?? definitions.find((definition) =>
      definition.documentType === documentType
    ) ?? null;
  },

  ensureDefaults(): Promise<void> {
    return apiService.post<void>(`${endpoint}/ensure-defaults`);
  },

  generate(request: GenerateDocumentNumberRequest): Promise<GeneratedDocumentNumber> {
    return apiService.post<GeneratedDocumentNumber>(`${endpoint}/generate`, request);
  },

  updateDefinition(
    id: string,
    request: UpdateDocumentSequenceDefinition
  ): Promise<DocumentSequenceDefinition> {
    return apiService.put<DocumentSequenceDefinition>(`${endpoint}/${id}`, request);
  },
};
