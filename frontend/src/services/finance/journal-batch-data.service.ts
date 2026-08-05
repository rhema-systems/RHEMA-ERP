import { apiService } from '@/services/api.service';
import type {
    CreateJournalBatch,
    CreateJournalBatchEntry,
    JournalBatchDetail,
    JournalBatchImportPreview,
    JournalBatchListResult,
    JournalBatchPostingRun,
    JournalBatchValidation,
    ReviewJournalBatchItem,
} from '@/types/journal-batches';

function download(blob: Blob, fileName: string) {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
}

class JournalBatchDataService {
    getBatches(query?: Record<string, unknown>) {
        return apiService.get<JournalBatchListResult>('/finance/journal-batches', query);
    }

    getBatch(id: string) {
        return apiService.get<JournalBatchDetail>(`/finance/journal-batches/${id}`);
    }

    createBatch(dto: CreateJournalBatch) {
        return apiService.post<JournalBatchDetail>('/finance/journal-batches', dto);
    }

    updateBatch(id: string, dto: {
        description: string;
        expectedDebitTotal: number;
        expectedJournalCount?: number;
        notes?: string;
        rowVersion: string;
    }) {
        return apiService.put<JournalBatchDetail>(`/finance/journal-batches/${id}`, dto);
    }

    deleteBatch(id: string) {
        return apiService.delete<void>(`/finance/journal-batches/${id}`);
    }

    createJournal(id: string, dto: CreateJournalBatchEntry) {
        return apiService.post<JournalBatchDetail>(`/finance/journal-batches/${id}/entries`, dto);
    }

    updateJournal(id: string, journalEntryId: string, dto: {
        transactionDate?: string;
        description?: string;
        reference?: string;
        bookClassification?: string;
        transactions?: CreateJournalBatchEntry['journalEntry']['transactions'];
    }) {
        return apiService.put<JournalBatchDetail>(`/finance/journal-batches/${id}/entries/${journalEntryId}`, dto);
    }

    attachJournal(id: string, journalEntryId: string) {
        return apiService.post<JournalBatchDetail>(`/finance/journal-batches/${id}/entries/attach`, { journalEntryId });
    }

    removeJournal(id: string, journalEntryId: string) {
        return apiService.delete<JournalBatchDetail>(`/finance/journal-batches/${id}/entries/${journalEntryId}`);
    }

    validate(id: string) {
        return apiService.post<JournalBatchValidation>(`/finance/journal-batches/${id}/validate`, {});
    }

    submit(id: string) {
        return apiService.post<JournalBatchDetail>(`/finance/journal-batches/${id}/submit`, {});
    }

    withdraw(id: string, comment?: string) {
        return apiService.post<JournalBatchDetail>(`/finance/journal-batches/${id}/withdraw`, { comment });
    }

    reviewStage(id: string, decisions: ReviewJournalBatchItem[], stageComment?: string) {
        return apiService.post<JournalBatchDetail>(`/finance/journal-batches/${id}/review-stage`, {
            decisions,
            stageComment,
        });
    }

    post(id: string, journalBatchItemIds: string[]) {
        return apiService.post<JournalBatchPostingRun>(`/finance/journal-batches/${id}/posting-runs`, {
            journalBatchItemIds,
            idempotencyKey: crypto.randomUUID(),
        });
    }

    copy(id: string, rejectedOnly = false, options: { entryDate?: string; fiscalPeriodId?: string; includeAttachments?: boolean } = {}) {
        const action = rejectedOnly ? 'copy-rejected' : 'copy';
        return apiService.post<JournalBatchDetail>(`/finance/journal-batches/${id}/${action}`, options);
    }

    createReversal(id: string, reason: string, reversalDate: string) {
        return apiService.post<JournalBatchDetail>(`/finance/journal-batches/${id}/reversal-batch`, {
            reason,
            reversalDate,
        });
    }

    async downloadTemplate() {
        download(await apiService.downloadBlob('/finance/journal-batches/import-template'), 'journal-batch-import-template.xlsx');
    }

    async exportBatch(id: string, batchNumber: string) {
        download(await apiService.downloadBlob(`/finance/journal-batches/${id}/export`), `${batchNumber}.xlsx`);
    }

    previewImport(file: File) {
        const form = new FormData();
        form.append('file', file);
        return apiService.request<JournalBatchImportPreview>('/finance/journal-batches/imports/preview', {
            method: 'POST',
            body: form,
        });
    }

    commitImport(sessionId: string, previewToken: string) {
        return apiService.post<JournalBatchDetail>(`/finance/journal-batches/imports/${sessionId}/commit`, {
            previewToken,
            idempotencyKey: crypto.randomUUID(),
        });
    }

    async downloadImportErrors(sessionId: string) {
        download(
            await apiService.downloadBlob(`/finance/journal-batches/imports/${sessionId}/errors`),
            'journal-batch-import-errors.xlsx',
        );
    }
}

export const journalBatchDataService = new JournalBatchDataService();
