'use client';

import React from 'react';
import Link from 'next/link';
import { BookTemplate, Edit3, FileText, Loader2, Save } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  documentManagementService,
  type CentralDocumentGenerationTemplate,
} from '@/services/document-management.service';

const toCsv = (items: string[]) => items.join(', ');
const splitCsv = (value: string) =>
  value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);

type TemplateForm = {
  templateCode: string;
  title: string;
  titleTemplate: string;
  module: string;
  sourceLabel: string;
  documentType: string;
  metadataTemplateCode: string;
  accessProfile: string;
  mergeFields: string;
  body: string;
  isActive: boolean;
  requiresApproval: boolean;
  approvalRole: string;
  signatureRole: string;
  defaultDispatchChannel: string;
};

const toForm = (template: CentralDocumentGenerationTemplate): TemplateForm => ({
  templateCode: template.templateCode,
  title: template.title,
  titleTemplate: template.titleTemplate,
  module: template.module,
  sourceLabel: template.sourceLabel,
  documentType: template.documentType,
  metadataTemplateCode: template.metadataTemplateCode,
  accessProfile: template.accessProfile,
  mergeFields: toCsv(template.mergeFields),
  body: template.body,
  isActive: template.isActive,
  requiresApproval: template.requiresApproval,
  approvalRole: template.approvalRole ?? '',
  signatureRole: template.signatureRole ?? '',
  defaultDispatchChannel: template.defaultDispatchChannel ?? '',
});

export default function DmsDocumentTemplatesSetupPage() {
  const [templates, setTemplates] = React.useState<
    CentralDocumentGenerationTemplate[]
  >([]);
  const [selectedCode, setSelectedCode] = React.useState<string>('');
  const [form, setForm] = React.useState<TemplateForm | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const selectedTemplate = React.useMemo(
    () => templates.find((template) => template.templateCode === selectedCode),
    [selectedCode, templates]
  );

  const loadTemplates = React.useCallback(async () => {
    setError(null);
    const data = await documentManagementService.getGenerationTemplates();
    setTemplates(data);
    setSelectedCode((current) => current || data[0]?.templateCode || '');
  }, []);

  React.useEffect(() => {
    let mounted = true;

    const load = async () => {
      try {
        await loadTemplates();
      } catch {
        if (mounted) {
          setError('Could not load document templates.');
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
  }, [loadTemplates]);

  React.useEffect(() => {
    if (selectedTemplate) {
      setForm(toForm(selectedTemplate));
    }
  }, [selectedTemplate]);

  const updateForm = <K extends keyof TemplateForm>(
    key: K,
    value: TemplateForm[K]
  ) => {
    setForm((current) => (current ? { ...current, [key]: value } : current));
  };

  const saveTemplate = async () => {
    if (!form) {
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      const saved = await documentManagementService.saveGenerationTemplate(
        form.templateCode,
        {
          title: form.title,
          titleTemplate: form.titleTemplate,
          module: form.module,
          sourceLabel: form.sourceLabel,
          documentType: form.documentType,
          metadataTemplateCode: form.metadataTemplateCode,
          accessProfile: form.accessProfile,
          mergeFields: splitCsv(form.mergeFields),
          body: form.body,
          isActive: form.isActive,
          requiresApproval: form.requiresApproval,
          approvalRole: form.approvalRole || null,
          signatureRole: form.signatureRole || null,
          defaultDispatchChannel: form.defaultDispatchChannel || null,
        }
      );
      setTemplates((current) =>
        current.map((template) =>
          template.templateCode === saved.templateCode ? saved : template
        )
      );
      setForm(toForm(saved));
    } catch {
      setError('Could not save the document template.');
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
          <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
            Document Templates
          </h1>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/administration/document-management/metadata-templates">
              <BookTemplate className="mr-2 h-4 w-4" />
              Metadata Templates
            </Link>
          </Button>
          <Button asChild>
            <Link href="/document-management/records">
              <FileText className="mr-2 h-4 w-4" />
              Document Register
            </Link>
          </Button>
        </div>
      </div>

      {error ? (
        <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
          {error}
        </div>
      ) : null}

      <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_460px]">
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              Central Template Registry
              {isLoading ? (
                <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
              ) : null}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[920px] text-left text-sm">
                <thead className="border-b text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="px-3 py-2 font-medium">Template</th>
                    <th className="px-3 py-2 font-medium">Module</th>
                    <th className="px-3 py-2 font-medium">Metadata</th>
                    <th className="px-3 py-2 font-medium">Approval</th>
                    <th className="px-3 py-2 font-medium">Source</th>
                    <th className="px-3 py-2 font-medium"></th>
                  </tr>
                </thead>
                <tbody>
                  {templates.map((template) => (
                    <tr key={template.templateCode} className="border-b">
                      <td className="px-3 py-3 align-top">
                        <div className="font-medium">{template.title}</div>
                        <div className="mt-1 text-xs text-muted-foreground">
                          {template.templateCode}
                        </div>
                      </td>
                      <td className="px-3 py-3 align-top">{template.module}</td>
                      <td className="px-3 py-3 align-top">
                        <Badge variant="outline">
                          {template.metadataTemplateCode}
                        </Badge>
                      </td>
                      <td className="px-3 py-3 align-top">
                        {template.requiresApproval ? (
                          <Badge variant="secondary">
                            {template.approvalRole || 'Approval required'}
                          </Badge>
                        ) : (
                          <Badge variant="outline">No approval</Badge>
                        )}
                      </td>
                      <td className="px-3 py-3 align-top text-xs">
                        {template.sourceLabel}
                      </td>
                      <td className="px-3 py-3 align-top">
                        <Button
                          size="sm"
                          variant={
                            selectedCode === template.templateCode
                              ? 'default'
                              : 'outline'
                          }
                          className="gap-2"
                          onClick={() => setSelectedCode(template.templateCode)}
                        >
                          <Edit3 className="h-4 w-4" />
                          Edit
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>

        {form ? (
          <Card className="border-border bg-card text-card-foreground">
            <CardHeader>
              <CardTitle className="text-base">Template Editor</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label>Code</Label>
                  <Input value={form.templateCode} disabled />
                </div>
                <div className="space-y-2">
                  <Label>Module</Label>
                  <Input
                    value={form.module}
                    onChange={(event) => updateForm('module', event.target.value)}
                  />
                </div>
              </div>
              <div className="space-y-2">
                <Label>Display title</Label>
                <Input
                  value={form.title}
                  onChange={(event) => updateForm('title', event.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label>Generated title</Label>
                <Input
                  value={form.titleTemplate}
                  onChange={(event) =>
                    updateForm('titleTemplate', event.target.value)
                  }
                />
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label>Metadata template</Label>
                  <Input
                    value={form.metadataTemplateCode}
                    onChange={(event) =>
                      updateForm('metadataTemplateCode', event.target.value)
                    }
                  />
                </div>
                <div className="space-y-2">
                  <Label>Access profile</Label>
                  <Input
                    value={form.accessProfile}
                    onChange={(event) =>
                      updateForm('accessProfile', event.target.value)
                    }
                  />
                </div>
              </div>
              <div className="space-y-2">
                <Label>Source label</Label>
                <Input
                  value={form.sourceLabel}
                  onChange={(event) =>
                    updateForm('sourceLabel', event.target.value)
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>Merge fields</Label>
                <Textarea
                  value={form.mergeFields}
                  onChange={(event) =>
                    updateForm('mergeFields', event.target.value)
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>Template body</Label>
                <Textarea
                  className="min-h-[320px] font-mono text-xs"
                  value={form.body}
                  onChange={(event) => updateForm('body', event.target.value)}
                />
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label>Approval role</Label>
                  <Input
                    value={form.approvalRole}
                    onChange={(event) =>
                      updateForm('approvalRole', event.target.value)
                    }
                  />
                </div>
                <div className="space-y-2">
                  <Label>Signature role</Label>
                  <Input
                    value={form.signatureRole}
                    onChange={(event) =>
                      updateForm('signatureRole', event.target.value)
                    }
                  />
                </div>
              </div>
              <div className="space-y-2">
                <Label>Dispatch channel</Label>
                <Input
                  value={form.defaultDispatchChannel}
                  onChange={(event) =>
                    updateForm('defaultDispatchChannel', event.target.value)
                  }
                />
              </div>
              <div className="flex flex-wrap gap-4">
                <label className="flex items-center gap-2 text-sm">
                  <Checkbox
                    checked={form.isActive}
                    onCheckedChange={(checked) =>
                      updateForm('isActive', checked === true)
                    }
                  />
                  Active
                </label>
                <label className="flex items-center gap-2 text-sm">
                  <Checkbox
                    checked={form.requiresApproval}
                    onCheckedChange={(checked) =>
                      updateForm('requiresApproval', checked === true)
                    }
                  />
                  Approval required
                </label>
              </div>
              <Button
                className="w-full gap-2"
                onClick={() => void saveTemplate()}
                disabled={isSaving}
              >
                {isSaving ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Save className="h-4 w-4" />
                )}
                Save template
              </Button>
            </CardContent>
          </Card>
        ) : null}
      </div>
    </div>
  );
}
