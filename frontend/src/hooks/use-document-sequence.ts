'use client';

import { useEffect, useMemo, useState } from 'react';
import {
  documentNumberingService,
  formatDocumentNumberSample,
} from '@/services/document-numbering.service';
import type { DocumentSequenceDefinition } from '@/types/document-numbering';

export function useDocumentSequence(module: string, documentType: string) {
  const [sequence, setSequence] = useState<DocumentSequenceDefinition | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let isMounted = true;

    const loadSequence = async () => {
      try {
        setLoading(true);
        const definition = await documentNumberingService.getDefinition(module, documentType);
        if (isMounted) setSequence(definition);
      } catch (error) {
        console.error(`Failed to load ${module}/${documentType} document sequence`, error);
        if (isMounted) setSequence(null);
      } finally {
        if (isMounted) setLoading(false);
      }
    };

    loadSequence();

    return () => {
      isMounted = false;
    };
  }, [documentType, module]);

  const sampleNumber = useMemo(() => formatDocumentNumberSample(sequence), [sequence]);
  const allowManualEntry = Boolean(sequence?.allowManualEntry);

  return {
    sequence,
    loading,
    allowManualEntry,
    sampleNumber,
  };
}
