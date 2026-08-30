'use client';

import React, { useEffect, useState } from 'react';
import { FileInput, ShieldX } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { CivilEngineeringInformationRequestsPanel } from '@/components/projects/civil-engineering/CivilEngineeringInformationRequestsPanel';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import {
  documentManagementService,
  type CentralDocumentRecord,
} from '@/services/document-management.service';

const permission = 'civil-engineering.design-input.respond';

export default function CivilEngineeringDesignInputsPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canRespond = hasPermission(permission);
  const [documents, setDocuments] = useState<CentralDocumentRecord[]>([]);

  useEffect(() => {
    if (!canRespond) return;
    documentManagementService
      .getRecords()
      .then(setDocuments)
      .catch((error) => {
        const value = error as { message?: string };
        toast({
          title: 'Unable to load DMS evidence',
          description:
            value.message ||
            'Published response evidence is temporarily unavailable.',
          variant: 'destructive',
        });
      });
  }, [canRespond, toast]);

  if (!canRespond) {
    return (
      <Alert>
        <ShieldX className="h-4 w-4" />
        <AlertTitle>Section response access required</AlertTitle>
        <AlertDescription>
          You do not have permission to respond to Civil Engineering design
          input requests.
        </AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-5">
      <div>
        <h1 className="flex items-center gap-2 text-2xl font-semibold">
          <FileInput className="h-6 w-6" /> Civil design inputs
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Respond to requests assigned to your current HR section.
        </p>
      </div>
      <CivilEngineeringInformationRequestsPanel
        assignedOnly
        documents={documents}
        canRespond
      />
    </div>
  );
}
