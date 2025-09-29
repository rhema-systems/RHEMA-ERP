'use client';

import React, { useState, useEffect, useRef } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { 
  emailTemplateService, 
  type EmailTemplate as EmailTemplateType, 
  type DatabaseTable, 
  type DatabaseColumn 
} from '../services/email-template.service';
import { RichTextEditor, type RichTextEditorRef } from './ui/rich-text-editor';
import { templatePreviewService, type PreviewData, type TemplateVariable } from '../services/template-preview.service';
import { 
  Dialog, 
  DialogContent, 
  DialogDescription, 
  DialogFooter, 
  DialogHeader, 
  DialogTitle 
} from './ui/dialog';
import { Button } from './ui/button';
import { Input } from './ui/input';
import { Label } from './ui/label';
import { Textarea } from './ui/textarea';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from './ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from './ui/tabs';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from './ui/select';
import { Badge } from './ui/badge';
import { ScrollArea } from './ui/scroll-area';
import { Separator } from './ui/separator';
import { Alert, AlertDescription, AlertTitle } from './ui/alert';
import { 
  Form, 
  FormControl, 
  FormDescription, 
  FormField, 
  FormItem, 
  FormLabel, 
  FormMessage 
} from './ui/form';
import { useToast } from '../hooks/use-toast';
import { 
  FileText, 
  Eye, 
  Code, 
  Database, 
  Plus, 
  Trash2, 
  Upload, 
  Download, 
  Save,
  Palette,
  Layout,
  Type,
  Image,
  Link2,
  Table,
  List,
  ToggleLeft,
  ToggleRight
} from 'lucide-react';

// Use types from service
type EmailTemplate = EmailTemplateType;

interface TemplatePreview {
  html: string;
  plainText: string;
}

// Schema
const templateSchema = z.object({
  name: z.string().min(1, 'Template name is required'),
  module: z.string().min(1, 'Module is required'),
  tableName: z.string().optional(),
  subject: z.string().min(1, 'Subject is required'),
  htmlBody: z.string().min(1, 'Email content is required'),
  plainTextBody: z.string().optional(),
  description: z.string().optional(),
  category: z.string().optional(),
});

type TemplateFormData = z.infer<typeof templateSchema>;

interface EmailTemplateDesignerProps {
  isOpen: boolean;
  onClose: () => void;
  template?: EmailTemplate;
  onSave: (template: EmailTemplate) => void;
}

export function EmailTemplateDesigner({ isOpen, onClose, template, onSave }: EmailTemplateDesignerProps) {
  const [activeTab, setActiveTab] = useState('design');
  const [selectedModule, setSelectedModule] = useState<string>('');
  const [selectedTable, setSelectedTable] = useState<string>('');
  const [selectedFields, setSelectedFields] = useState<string[]>([]);
  const [previewData, setPreviewData] = useState<TemplatePreview | null>(null);
  const [isCodeView, setIsCodeView] = useState(false);
  const [codeContent, setCodeContent] = useState('');
  const editorRef = useRef<RichTextEditorRef>(null);
  const codeEditorRef = useRef<HTMLTextAreaElement>(null);
  const { toast } = useToast();

  const form = useForm<TemplateFormData>({
    resolver: zodResolver(templateSchema),
    defaultValues: {
      name: '',
      module: '',
      tableName: '',
      subject: '',
      htmlBody: '',
      plainTextBody: '',
      description: '',
      category: '',
    }
  });

  // Load template data when editing
  useEffect(() => {
    if (template) {
      form.reset({
        name: template.name,
        module: template.module,
        tableName: template.tableName || '',
        subject: template.subject,
        htmlBody: template.htmlBody,
        plainTextBody: template.plainTextBody || '',
        description: template.description || '',
        category: template.category || '',
      });
      setSelectedModule(template.module);
      setSelectedTable(template.tableName || '');
      
      // Initialize both editor views
      setCodeContent(template.htmlBody);
      if (editorRef.current) {
        editorRef.current.setContent(template.htmlBody);
      }
      
      // Parse selected fields
      if (template.selectedFields) {
        try {
          const fields = JSON.parse(template.selectedFields);
          setSelectedFields(Array.isArray(fields) ? fields : []);
        } catch {
          setSelectedFields([]);
        }
      }
    } else {
      // Clear both views for new templates
      const defaultContent = '<p>Start typing your email content here...</p>';
      setCodeContent(defaultContent);
      if (editorRef.current) {
        editorRef.current.setContent(defaultContent);
      }
    }
  }, [template, form]);

  // Initialize editor when component mounts
  useEffect(() => {
    if (editorRef.current && !template) {
      // Set some default content for new templates
      const defaultContent = '<p>Start typing your email content here...</p>';
      editorRef.current.setContent(defaultContent);
      form.setValue('htmlBody', defaultContent);
    }
  }, [template, form]);

  // Fetch available modules
  const { data: modules } = useQuery({
    queryKey: ['template-modules'],
    queryFn: () => emailTemplateService.getModules()
  });

  // Fetch database tables
  const { data: tables } = useQuery({
    queryKey: ['database-tables'],
    queryFn: () => emailTemplateService.getTables()
  });

  // Fetch table columns
  const { data: columns } = useQuery({
    queryKey: ['table-columns', selectedTable],
    queryFn: () => selectedTable ? emailTemplateService.getColumns(selectedTable) : Promise.resolve([]),
    enabled: !!selectedTable
  });

  const handleModuleChange = (module: string) => {
    setSelectedModule(module);
    form.setValue('module', module);
  };

  const handleTableChange = (table: string) => {
    setSelectedTable(table);
    form.setValue('tableName', table);
    setSelectedFields([]); // Clear selected fields when table changes
  };

  const handleFieldToggle = (fieldName: string) => {
    const placeholder = `{{${selectedTable}.${fieldName}}}`;
    setSelectedFields(prev => 
      prev.includes(placeholder) 
        ? prev.filter(f => f !== placeholder)
        : [...prev, placeholder]
    );
  };

  const insertPlaceholder = (placeholder: string) => {
    if (isCodeView) {
      // Handle code view insertion
      if (codeEditorRef.current) {
        const textarea = codeEditorRef.current;
        const start = textarea.selectionStart;
        const end = textarea.selectionEnd;
        const before = textarea.value.substring(0, start);
        const after = textarea.value.substring(end);
        const newValue = before + placeholder + after;
        
        setCodeContent(newValue);
        form.setValue('htmlBody', newValue);
        
        // Restore cursor position after placeholder
        setTimeout(() => {
          if (codeEditorRef.current) {
            codeEditorRef.current.focus();
            codeEditorRef.current.setSelectionRange(start + placeholder.length, start + placeholder.length);
          }
        }, 0);
      }
    } else {
      // Handle design view insertion
      if (editorRef.current) {
        // The RichTextEditor now handles focus and cursor position internally
        editorRef.current.insertText(placeholder);
        
        // Update form value
        const content = editorRef.current.getContent();
        form.setValue('htmlBody', content);
      }
    }
  };

  // Handle editor content changes
  const handleEditorChange = (content: string) => {
    form.setValue('htmlBody', content);
    if (!isCodeView) {
      setCodeContent(content);
    }
  };

  // Handle code editor changes
  const handleCodeChange = (event: React.ChangeEvent<HTMLTextAreaElement>) => {
    const content = event.target.value;
    setCodeContent(content);
    form.setValue('htmlBody', content);
  };

  // Toggle between design and code view
  const toggleView = () => {
    if (isCodeView) {
      // Switching from code to design view
      if (editorRef.current) {
        editorRef.current.setContent(codeContent);
      }
    } else {
      // Switching from design to code view
      if (editorRef.current) {
        const currentContent = editorRef.current.getContent();
        setCodeContent(currentContent);
      }
    }
    setIsCodeView(!isCodeView);
  };

  const generatePreview = async () => {
    const formData = form.getValues();
    
    try {
      const previewData = await templatePreviewService.generatePreview(
        formData.htmlBody,
        formData.plainTextBody || '',
        selectedTable,
        selectedFields
      );
      
      setPreviewData({
        html: previewData.html,
        plainText: previewData.plainText
      });
    } catch (error) {
      console.error('Failed to generate preview:', error);
      toast({
        title: 'Preview Error',
        description: 'Failed to generate template preview',
        variant: 'destructive'
      });
    }
  };

  const handleSave = () => {
    const formData = form.getValues();
    const templateData: EmailTemplate = {
      ...formData,
      selectedFields: JSON.stringify(selectedFields),
      // Include the ID if we're editing an existing template
      ...(template?.id && { id: template.id }),
    };
    
    onSave(templateData);
    onClose();
  };

  const handleImportTemplate = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = (e) => {
      try {
        const templateData = JSON.parse(e.target?.result as string);
        form.reset(templateData);
        setSelectedModule(templateData.module || '');
        setSelectedTable(templateData.tableName || '');
        
        if (templateData.selectedFields) {
          const fields = JSON.parse(templateData.selectedFields);
          setSelectedFields(Array.isArray(fields) ? fields : []);
        }
        
        toast({
          title: 'Template Imported',
          description: 'Template has been imported successfully',
        });
      } catch (error) {
        toast({
          title: 'Import Failed',
          description: 'Invalid template file format',
          variant: 'destructive',
        });
      }
    };
    reader.readAsText(file);
  };

  const handleExportTemplate = () => {
    const formData = form.getValues();
    const exportData = {
      ...formData,
      selectedFields: JSON.stringify(selectedFields),
      exportedAt: new Date().toISOString(),
      exportedBy: 'Current User'
    };

    const blob = new Blob([JSON.stringify(exportData, null, 2)], { 
      type: 'application/json' 
    });
    
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${formData.name || 'template'}.json`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-[95vw] max-h-[95vh] overflow-hidden">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <FileText className="h-5 w-5" />
            {template ? 'Edit Email Template' : 'Create Email Template'}
          </DialogTitle>
          <DialogDescription>
            Design professional email templates with dynamic database field integration
          </DialogDescription>
        </DialogHeader>

        <div className="flex-1 overflow-hidden">
          <Tabs value={activeTab} onValueChange={setActiveTab} className="h-full flex flex-col">
            <TabsList className="grid w-full grid-cols-3">
              <TabsTrigger value="design" className="flex items-center gap-2">
                <Layout className="h-4 w-4" />
                Design & Preview
              </TabsTrigger>
              <TabsTrigger value="fields" className="flex items-center gap-2">
                <Database className="h-4 w-4" />
                Fields
              </TabsTrigger>
              <TabsTrigger value="settings" className="flex items-center gap-2">
                <FileText className="h-4 w-4" />
                Settings
              </TabsTrigger>
            </TabsList>

            <div className="flex-1 overflow-hidden">
              <TabsContent value="design" className="h-full space-y-3">
                {/* Subject Line - Inline */}
                <div className="flex items-end gap-4">
                  <div className="flex-1">
                    <Label htmlFor="subject">Subject</Label>
                    <Input
                      id="subject"
                      {...form.register('subject')}
                      placeholder="Enter email subject..."
                    />
                  </div>
                </div>

                {/* Editor and Preview Side by Side */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4 h-full">
                  {/* Editor Section */}
                  <Card>
                    <CardHeader className="pb-3">
                      <div className="flex items-center justify-between">
                        <CardTitle className="text-base">Editor</CardTitle>
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={toggleView}
                          className="flex items-center gap-2"
                        >
                          {isCodeView ? (
                            <>
                              <Layout className="h-4 w-4" />
                              Design
                            </>
                          ) : (
                            <>
                              <Code className="h-4 w-4" />
                              Code
                            </>
                          )}
                        </Button>
                      </div>
                    </CardHeader>
                    <CardContent>
                      {/* Editor Views */}
                      {isCodeView ? (
                        <div className="border border-input rounded-md">
                          <div className="border-b p-2 bg-muted/50">
                            <div className="flex items-center justify-between">
                              <span className="text-sm font-medium flex items-center gap-2">
                                <Code className="h-4 w-4" />
                                HTML Source
                              </span>
                              <Badge variant="secondary">Code Editor</Badge>
                            </div>
                          </div>
                          <Textarea
                            ref={codeEditorRef}
                            value={codeContent}
                            onChange={handleCodeChange}
                            placeholder="<p>Start typing your HTML content here...</p>"
                            className="min-h-[500px] border-0 rounded-t-none font-mono text-sm resize-none"
                            spellCheck={false}
                          />
                        </div>
                      ) : (
                        <div className="relative">
                          <Badge 
                            variant="secondary" 
                            className="absolute top-2 right-2 z-10"
                          >
                            Design Editor
                          </Badge>
                          <RichTextEditor
                            ref={editorRef}
                            value={form.watch('htmlBody')}
                            onChange={handleEditorChange}
                            placeholder="Start typing your email content here..."
                            minHeight="500px"
                          />
                        </div>
                      )}
                    </CardContent>
                  </Card>

                  {/* Preview Section */}
                  <Card>
                    <CardHeader className="pb-3">
                      <div className="flex items-center justify-between">
                        <CardTitle className="text-base">Preview</CardTitle>
                        <Button 
                          onClick={generatePreview} 
                          size="sm"
                          variant="outline"
                          className="flex items-center gap-1"
                        >
                          <Eye className="h-3 w-3" />
                          Refresh
                        </Button>
                      </div>
                    </CardHeader>
                    <CardContent>
                      <div className="border rounded-md p-4 bg-white min-h-[500px] overflow-auto">
                        {previewData ? (
                          <>
                            <div className="mb-4 pb-2 border-b">
                              <p className="text-sm text-muted-foreground">Subject:</p>
                              <p className="font-medium">{form.watch('subject')}</p>
                            </div>
                            <div 
                              dangerouslySetInnerHTML={{ __html: previewData.html }}
                              className="prose prose-sm max-w-none"
                            />
                          </>
                        ) : (
                          <div className="flex flex-col items-center justify-center h-full text-center py-12">
                            <Eye className="h-12 w-12 text-muted-foreground mb-4" />
                            <p className="text-muted-foreground">
                              Click "Refresh Preview" to see how your email will look with sample data
                            </p>
                          </div>
                        )}
                      </div>
                    </CardContent>
                  </Card>
                </div>

                {/* Placeholders at Bottom */}
                <Card>
                  <CardHeader className="pb-3">
                    <CardTitle className="text-base">Placeholders</CardTitle>
                  </CardHeader>
                  <CardContent>
                    <div className="flex flex-wrap gap-2">
                      {selectedFields.map((field, index) => (
                        <div key={index} className="flex items-center gap-2">
                          <Badge
                            variant="secondary"
                            className="cursor-pointer hover:bg-secondary/80"
                            onClick={() => insertPlaceholder(field)}
                          >
                            {field}
                          </Badge>
                          <Button
                            type="button"
                            variant="ghost"
                            size="sm"
                            onClick={() => setSelectedFields(prev => 
                              prev.filter(f => f !== field)
                            )}
                          >
                            <Trash2 className="h-3 w-3" />
                          </Button>
                        </div>
                      ))}
                      {selectedFields.length === 0 && (
                        <p className="text-sm text-muted-foreground text-center py-4 w-full">
                          Select database fields from the Fields tab to see placeholders here
                        </p>
                      )}
                    </div>
                  </CardContent>
                </Card>
              </TabsContent>

              <TabsContent value="fields" className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <Card>
                    <CardHeader>
                      <CardTitle>Module</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <Select value={selectedModule} onValueChange={handleModuleChange}>
                        <SelectTrigger>
                          <SelectValue placeholder="Select module" />
                        </SelectTrigger>
                        <SelectContent>
                          {modules?.map((module) => (
                            <SelectItem key={module} value={module}>
                              {module}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardHeader>
                      <CardTitle>Database Table</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <Select value={selectedTable} onValueChange={handleTableChange}>
                        <SelectTrigger>
                          <SelectValue placeholder="Select table" />
                        </SelectTrigger>
                        <SelectContent>
                          {tables?.map((table) => (
                            <SelectItem key={table.name} value={table.name}>
                              {table.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardHeader>
                      <CardTitle>Available Fields</CardTitle>
                      <CardDescription>
                        Select fields to use as placeholders
                      </CardDescription>
                    </CardHeader>
                    <CardContent>
                      <ScrollArea className="h-[300px]">
                        <div className="space-y-2">
                          {columns?.map((column) => {
                            const placeholder = `{{${selectedTable}.${column.name}}}`;
                            const isSelected = selectedFields.includes(placeholder);
                            
                            return (
                              <div
                                key={column.name}
                                className={`p-2 rounded border cursor-pointer transition-colors ${
                                  isSelected 
                                    ? 'bg-primary/10 border-primary' 
                                    : 'hover:bg-muted'
                                }`}
                                onClick={() => handleFieldToggle(column.name)}
                              >
                                <div className="flex items-center justify-between">
                                  <span className="font-medium">{column.name}</span>
                                  <Badge variant="outline" className="text-xs">
                                    {column.dataType}
                                  </Badge>
                                </div>
                                {column.description && (
                                  <p className="text-sm text-muted-foreground mt-1">
                                    {column.description}
                                  </p>
                                )}
                              </div>
                            );
                          })}
                          {!selectedTable && (
                            <p className="text-sm text-muted-foreground text-center py-4">
                              Select a table to see available fields
                            </p>
                          )}
                        </div>
                      </ScrollArea>
                    </CardContent>
                  </Card>
                </div>
              </TabsContent>


              <TabsContent value="settings" className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <Card>
                    <CardHeader>
                      <CardTitle>Template Information</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                      <div>
                        <Label htmlFor="name">Template Name</Label>
                        <Input
                          id="name"
                          {...form.register('name')}
                          placeholder="Enter template name..."
                        />
                      </div>
                      
                      <div>
                        <Label htmlFor="category">Category</Label>
                        <Input
                          id="category"
                          {...form.register('category')}
                          placeholder="e.g., Notifications, Reports..."
                        />
                      </div>

                      <div>
                        <Label htmlFor="description">Description</Label>
                        <Textarea
                          id="description"
                          {...form.register('description')}
                          placeholder="Describe what this template is used for..."
                          rows={3}
                        />
                      </div>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardHeader>
                      <CardTitle>Import / Export</CardTitle>
                      <CardDescription>
                        Import or export template configurations
                      </CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-4">
                      <div>
                        <Label>Import Template</Label>
                        <div className="flex items-center gap-2">
                          <input
                            type="file"
                            accept=".json"
                            onChange={handleImportTemplate}
                            className="hidden"
                            id="import-file"
                          />
                          <Button
                            type="button"
                            variant="outline"
                            onClick={() => document.getElementById('import-file')?.click()}
                            className="flex items-center gap-2"
                          >
                            <Upload className="h-4 w-4" />
                            Import JSON
                          </Button>
                        </div>
                      </div>

                      <div>
                        <Label>Export Template</Label>
                        <Button
                          type="button"
                          variant="outline"
                          onClick={handleExportTemplate}
                          className="flex items-center gap-2"
                        >
                          <Download className="h-4 w-4" />
                          Export JSON
                        </Button>
                      </div>
                    </CardContent>
                  </Card>
                </div>
              </TabsContent>
            </div>
          </Tabs>
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={handleSave} className="flex items-center gap-2">
            <Save className="h-4 w-4" />
            Save Template
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}