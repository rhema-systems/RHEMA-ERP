'use client';

import React from 'react';
import Link from 'next/link';
import { BookTemplate, FileText, Loader2, Plus, Workflow } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  documentManagementService,
  type CentralDocumentMetadataTemplate,
} from '@/services/document-management.service';

const initialTemplateForm = {
  module: 'Estate / Facilities',
  documentType: '',
  templateCode: '',
  sourceLabel: 'Source: Estate / Facilities -> Central DMS',
  requiredFields: 'Document Date, Prepared By, Approval Status',
  relationships: 'Source Module Record, Workflow Case',
  retentionRule: 'Operational record retention',
  accessProfile: 'Module restricted',
};

const splitList = (value: string) =>
  value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);

export default function DmsMetadataTemplatesSetupPage() {
  const [templates, setTemplates] = React.useState<
    CentralDocumentMetadataTemplate[]
  >([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [form, setForm] = React.useState(initialTemplateForm);

  React.useEffect(() => {
    let mounted = true;

    const load = async () => {
      try {
        const data = await documentManagementService.getMetadataTemplates();
        if (mounted) {
          setTemplates(data);
        }
      } finally {
        if (mounted) {
          setIsLoading(false);
        }
      }
    };

    void load();

    return () => {
      mounted = false;
    };
  }, []);

  const updateForm = (
    field: keyof typeof initialTemplateForm,
    value: string
  ) => {
    setForm((current) => ({ ...current, [field]: value }));
  };

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);

    if (
      !form.module.trim() ||
      !form.documentType.trim() ||
      !form.templateCode.trim()
    ) {
      setError('Module, document type, and template code are required.');
      return;
    }

    setIsSaving(true);
    try {
      const created = await documentManagementService.createMetadataTemplate({
        module: form.module.trim(),
        documentType: form.documentType.trim(),
        templateCode: form.templateCode.trim(),
        sourceLabel: form.sourceLabel.trim(),
        requiredFields: splitList(form.requiredFields),
        relationships: splitList(form.relationships),
        retentionRule: form.retentionRule.trim(),
        accessProfile: form.accessProfile.trim(),
        isActive: true,
      });
      setTemplates((current) => [created, ...current]);
      setForm({
        ...initialTemplateForm,
        documentType: '',
        templateCode: '',
      });
    } catch {
      setError(
        'Could not save the metadata template. Please check the values and try again.'
      );
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="space-y-2">
          <Badge variant="outline" className="w-fit">
            Administration / DMS setup
          </Badge>
          <div>
            <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
              DMS Metadata Templates
            </h1>
            <p className="mt-2 max-w-4xl text-sm text-muted-foreground">
              Define document metadata requirements by module and document type
              before source modules publish records into Central DMS.
            </p>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/document-management">
              <FileText className="mr-2 h-4 w-4" />
              Central DMS
            </Link>
          </Button>
          <Button asChild>
            <Link href="/administration/workflow?q=Document%20Management">
              <Workflow className="mr-2 h-4 w-4" />
              Workflow setup
            </Link>
          </Button>
        </div>
      </div>

      <Card className="border-border bg-card text-card-foreground">
        <CardHeader>
          <CardTitle className="text-base">Create Metadata Template</CardTitle>
        </CardHeader>
        <CardContent>
          <form className="space-y-4" onSubmit={handleSubmit}>
            <div className="grid gap-4 md:grid-cols-3">
              <div className="space-y-2">
                <Label htmlFor="dms-template-module">Module</Label>
                <Input
                  id="dms-template-module"
                  value={form.module}
                  onChange={(event) => updateForm('module', event.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="dms-template-document-type">
                  Document type
                </Label>
                <Input
                  id="dms-template-document-type"
                  value={form.documentType}
                  onChange={(event) =>
                    updateForm('documentType', event.target.value)
                  }
                  placeholder="Lease agreement"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="dms-template-code">Template code</Label>
                <Input
                  id="dms-template-code"
                  value={form.templateCode}
                  onChange={(event) =>
                    updateForm('templateCode', event.target.value)
                  }
                  placeholder="EST-LEASE"
                />
              </div>
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="dms-template-fields">Required fields</Label>
                <Textarea
                  id="dms-template-fields"
                  value={form.requiredFields}
                  onChange={(event) =>
                    updateForm('requiredFields', event.target.value)
                  }
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="dms-template-relationships">
                  Relationships
                </Label>
                <Textarea
                  id="dms-template-relationships"
                  value={form.relationships}
                  onChange={(event) =>
                    updateForm('relationships', event.target.value)
                  }
                />
              </div>
            </div>

            <div className="grid gap-4 md:grid-cols-3">
              <div className="space-y-2 md:col-span-1">
                <Label htmlFor="dms-template-retention">Retention rule</Label>
                <Input
                  id="dms-template-retention"
                  value={form.retentionRule}
                  onChange={(event) =>
                    updateForm('retentionRule', event.target.value)
                  }
                />
              </div>
              <div className="space-y-2 md:col-span-1">
                <Label htmlFor="dms-template-access">Access profile</Label>
                <Input
                  id="dms-template-access"
                  value={form.accessProfile}
                  onChange={(event) =>
                    updateForm('accessProfile', event.target.value)
                  }
                />
              </div>
              <div className="space-y-2 md:col-span-1">
                <Label htmlFor="dms-template-source">Source label</Label>
                <Input
                  id="dms-template-source"
                  value={form.sourceLabel}
                  onChange={(event) =>
                    updateForm('sourceLabel', event.target.value)
                  }
                />
              </div>
            </div>

            {error ? <p className="text-sm text-destructive">{error}</p> : null}

            <Button type="submit" disabled={isSaving}>
              {isSaving ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Plus className="mr-2 h-4 w-4" />
              )}
              Save template
            </Button>
          </form>
        </CardContent>
      </Card>

      {isLoading ? (
        <Card className="border-border bg-card text-card-foreground">
          <CardContent className="flex items-center justify-center gap-2 py-8 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />
            Loading metadata templates
          </CardContent>
        </Card>
      ) : null}

      <div className="grid gap-4 lg:grid-cols-2">
        {templates.map((template) => (
          <Card
            key={template.templateCode}
            className="border-border bg-card text-card-foreground"
          >
            <CardHeader>
              <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-md border bg-muted">
                <BookTemplate className="h-5 w-5 text-primary" />
              </div>
              <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <CardTitle className="text-base">
                    {template.documentType}
                  </CardTitle>
                  <CardDescription className="mt-1">
                    {template.module}
                  </CardDescription>
                </div>
                <Badge variant="outline" className="w-fit">
                  {template.templateCode}
                </Badge>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              <p className="text-xs text-muted-foreground">
                {template.sourceLabel}
              </p>
              <div>
                <div className="text-sm font-medium">Required fields</div>
                <div className="mt-2 flex flex-wrap gap-2">
                  {template.requiredFields.map((field) => (
                    <Badge key={field} variant="secondary">
                      {field}
                    </Badge>
                  ))}
                </div>
              </div>
              <div className="grid gap-3 md:grid-cols-2">
                <div className="rounded-md border bg-background p-3">
                  <div className="text-sm font-medium">Retention</div>
                  <div className="mt-1 text-sm text-muted-foreground">
                    {template.retentionRule}
                  </div>
                </div>
                <div className="rounded-md border bg-background p-3">
                  <div className="text-sm font-medium">Access</div>
                  <div className="mt-1 text-sm text-muted-foreground">
                    {template.accessProfile}
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}
