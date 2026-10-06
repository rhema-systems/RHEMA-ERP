'use client';

import React from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { toast } from 'sonner';
import {
  BookTemplate,
  FileText,
  Loader2,
  Save,
  Search,
  Upload,
} from 'lucide-react';

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

const TEMPLATE_PICKER_LIMIT = 8;

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
  const searchParams = useSearchParams();
  const scopedQuery = searchParams.get('q')?.trim() ?? '';
  const [templates, setTemplates] = React.useState<
    CentralDocumentGenerationTemplate[]
  >([]);
  const [selectedCode, setSelectedCode] = React.useState<string>('');
  const [form, setForm] = React.useState<TemplateForm | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [isUploadingTemplate, setIsUploadingTemplate] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [templateSearch, setTemplateSearch] = React.useState(scopedQuery);
  const [selectedModule, setSelectedModule] = React.useState<string>('all');
  const uploadInputRef = React.useRef<HTMLInputElement | null>(null);

  const selectedTemplate = React.useMemo(
    () => templates.find((template) => template.templateCode === selectedCode),
    [selectedCode, templates]
  );

  const modules = React.useMemo(
    () =>
      Array.from(
        new Set(
          templates
            .map((template) => template.module)
            .filter((module) => module.trim().length > 0)
        )
      ).sort((left, right) => left.localeCompare(right)),
    [templates]
  );

  const filteredTemplates = React.useMemo(() => {
    const query = templateSearch.trim().toLowerCase();

    return templates.filter((template) => {
      const matchesModule =
        selectedModule === 'all' || template.module === selectedModule;
      const matchesQuery =
        !query ||
        [
          template.title,
          template.templateCode,
          template.module,
          template.documentType,
          template.metadataTemplateCode,
          template.sourceLabel,
        ]
          .filter(Boolean)
          .some((value) => value.toLowerCase().includes(query));

      return matchesModule && matchesQuery;
    });
  }, [selectedModule, templateSearch, templates]);

  const visibleTemplates = React.useMemo(
    () => filteredTemplates.slice(0, TEMPLATE_PICKER_LIMIT),
    [filteredTemplates]
  );

  const loadTemplates = React.useCallback(async () => {
    setError(null);
    const data = await documentManagementService.getGenerationTemplates();
    setTemplates(data);
    setSelectedCode((current) => {
      if (current) return current;
      const query = scopedQuery.toLowerCase();
      return (
        data.find((template) =>
          [template.module, template.title, template.documentType]
            .filter(Boolean)
            .some((value) => value.toLowerCase().includes(query))
        )?.templateCode ||
        data[0]?.templateCode ||
        ''
      );
    });
  }, [scopedQuery]);

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

    const useUploadedBody = selectedTemplate?.hasWordTemplate === true;

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
          body: useUploadedBody ? selectedTemplate?.body : form.body,
          isActive: form.isActive,
          requiresApproval: form.requiresApproval,
          approvalRole: form.approvalRole || null,
          signatureRole: form.signatureRole || null,
          defaultDispatchChannel: form.defaultDispatchChannel || null,
        }
      );
      setTemplates((current) => {
        const exists = current.some(
          (template) => template.templateCode === saved.templateCode
        );

        if (!exists) {
          return [saved, ...current];
        }

        return current.map((template) =>
          template.templateCode === saved.templateCode ? saved : template
        );
      });
      setSelectedCode(saved.templateCode);
      setForm(toForm(saved));
      toast.success('Document template saved');
    } catch (caughtError) {
      const message =
        caughtError instanceof Error
          ? caughtError.message
          : 'Could not save the document template.';
      setError(message);
      toast.error(message);
    } finally {
      setIsSaving(false);
    }
  };

  const uploadWordTemplate = async (file: File) => {
    if (!form) {
      return;
    }

    setIsUploadingTemplate(true);
    setError(null);
    try {
      const saved =
        await documentManagementService.uploadGenerationTemplateWordFile(
          form.templateCode,
          file
        );
      setTemplates((current) =>
        current.map((template) =>
          template.templateCode === saved.templateCode ? saved : template
        )
      );
      setForm(toForm(saved));
      toast.success('Word template uploaded', {
        description: saved.templateFileName ?? file.name,
      });
    } catch (caughtError) {
      const message =
        caughtError instanceof Error
          ? caughtError.message
          : 'Could not upload the Word template.';
      setError(message);
      toast.error(message);
    } finally {
      setIsUploadingTemplate(false);
    }
  };

  const openUploadPicker = () => {
    uploadInputRef.current?.click();
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
          <Button
            type="button"
            variant="outline"
            onClick={openUploadPicker}
            disabled={!form || isUploadingTemplate}
          >
            {isUploadingTemplate ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Upload className="mr-2 h-4 w-4" />
            )}
            Upload Document
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

      <div className="grid gap-4 xl:grid-cols-[minmax(320px,420px)_minmax(0,1fr)]">
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              Template Picker
              {isLoading ? (
                <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
              ) : null}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-4">
              <div className="relative">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  className="pl-9"
                  value={templateSearch}
                  onChange={(event) => setTemplateSearch(event.target.value)}
                  placeholder="Search code, title, module"
                  aria-label="Search document templates"
                />
              </div>

              {modules.length > 0 ? (
                <div className="flex flex-wrap gap-2">
                  <Button
                    type="button"
                    size="sm"
                    variant={selectedModule === 'all' ? 'default' : 'outline'}
                    onClick={() => setSelectedModule('all')}
                  >
                    All
                  </Button>
                  {modules.slice(0, 10).map((module) => (
                    <Button
                      key={module}
                      type="button"
                      size="sm"
                      variant={
                        selectedModule === module ? 'default' : 'outline'
                      }
                      onClick={() => setSelectedModule(module)}
                    >
                      {module}
                    </Button>
                  ))}
                </div>
              ) : null}

              <div className="text-xs text-muted-foreground">
                Showing {visibleTemplates.length} of {filteredTemplates.length}
                {templates.length !== filteredTemplates.length
                  ? ` matched from ${templates.length}`
                  : ''}{' '}
                templates.
              </div>

              <div className="space-y-2">
                {visibleTemplates.map((template) => (
                  <button
                    key={template.templateCode}
                    type="button"
                    className={`w-full rounded-md border p-3 text-left transition-colors ${
                      selectedCode === template.templateCode
                        ? 'border-primary bg-primary/5'
                        : 'border-border hover:bg-muted/60'
                    }`}
                    onClick={() => setSelectedCode(template.templateCode)}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <div className="truncate text-sm font-medium">
                          {template.title}
                        </div>
                        <div className="mt-1 truncate text-xs text-muted-foreground">
                          {template.templateCode}
                        </div>
                      </div>
                      {template.hasWordTemplate ? (
                        <Badge variant="outline">Word</Badge>
                      ) : null}
                    </div>
                    <div className="mt-3 flex flex-wrap gap-2 text-xs">
                      <Badge variant="secondary">{template.module}</Badge>
                      <Badge variant="outline">
                        {template.metadataTemplateCode}
                      </Badge>
                    </div>
                  </button>
                ))}

                {!isLoading && filteredTemplates.length === 0 ? (
                  <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
                    No document templates match the current search.
                  </div>
                ) : null}
              </div>

              {filteredTemplates.length > TEMPLATE_PICKER_LIMIT ? (
                <div className="rounded-md border bg-muted/40 p-3 text-xs text-muted-foreground">
                  Narrow the search or choose a module to see the remaining{' '}
                  {filteredTemplates.length - TEMPLATE_PICKER_LIMIT} templates.
                </div>
              ) : null}
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
                    onChange={(event) =>
                      updateForm('module', event.target.value)
                    }
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
                <Label>
                  {selectedTemplate?.hasWordTemplate
                    ? 'Template body from uploaded document'
                    : 'Template body'}
                </Label>
                <Textarea
                  className="min-h-[320px] font-mono text-xs"
                  value={form.body}
                  readOnly={selectedTemplate?.hasWordTemplate === true}
                  onChange={(event) => updateForm('body', event.target.value)}
                />
              </div>
              <div className="rounded-md border p-3">
                <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <div className="min-w-0">
                    <Label>Upload document template</Label>
                    <p className="mt-1 truncate text-xs text-muted-foreground">
                      {selectedTemplate?.templateFileName ||
                        'No .docx source uploaded'}
                    </p>
                  </div>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={openUploadPicker}
                    disabled={isUploadingTemplate}
                  >
                    {isUploadingTemplate ? (
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    ) : (
                      <Upload className="mr-2 h-4 w-4" />
                    )}
                    Upload Document
                  </Button>
                  <input
                    ref={uploadInputRef}
                    className="sr-only"
                    type="file"
                    accept=".docx,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                    onChange={(event) => {
                      const file = event.target.files?.[0];
                      if (file) void uploadWordTemplate(file);
                      event.currentTarget.value = '';
                    }}
                  />
                </div>
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
