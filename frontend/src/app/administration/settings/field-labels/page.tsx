'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Save, RefreshCw, Tag, Package, Settings } from 'lucide-react';
import { useToast } from '@/hooks/use-toast';
import { fieldLabelService } from '@/services/fieldLabelService';

type FieldLabels = Record<string, string>;

interface ModuleLabels {
  module: string;
  displayName: string;
  description: string;
  icon: React.ReactNode;
  labels: FieldLabels;
  defaultLabels: FieldLabels;
}

const defaultInventoryItemLabels: FieldLabels = {
  Brand: 'Brand',
  Manufacturer: 'Manufacturer',
  Style: 'Style',
  Feature: 'Feature',
};

export default function FieldLabelsSettingsPage() {
  const { toast } = useToast();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [activeModule, setActiveModule] = useState('InventoryItem');
  
  const [moduleLabels, setModuleLabels] = useState<ModuleLabels[]>([
    {
      module: 'InventoryItem',
      displayName: 'Inventory Items',
      description: 'Configure field labels for inventory item forms',
      icon: <Package className="h-5 w-5" />,
      labels: { ...defaultInventoryItemLabels },
      defaultLabels: { ...defaultInventoryItemLabels },
    },
  ]);

  useEffect(() => {
    loadFieldLabels();
  }, []);

  const loadFieldLabels = async () => {
    try {
      setLoading(true);
      
      // Load labels for each module
      const updatedModules = await Promise.all(
        moduleLabels.map(async (mod) => {
          try {
            const labels = await fieldLabelService.getFieldLabels(mod.module);
            return {
              ...mod,
              labels: { ...mod.defaultLabels, ...labels },
            };
          } catch (error) {
            console.error(`Error loading labels for ${mod.module}:`, error);
            return mod;
          }
        })
      );
      
      setModuleLabels(updatedModules);
    } catch (error) {
      console.error('Error loading field labels:', error);
      toast({
        title: 'Error',
        description: 'Failed to load field labels',
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  };

  const handleLabelChange = (module: string, fieldName: string, value: string) => {
    setModuleLabels(prev =>
      prev.map(mod =>
        mod.module === module
          ? { ...mod, labels: { ...mod.labels, [fieldName]: value } }
          : mod
      )
    );
  };

  const handleSave = async (module: string) => {
    const mod = moduleLabels.find(m => m.module === module);
    if (!mod) return;

    try {
      setSaving(true);
      await fieldLabelService.updateFieldLabels(module, mod.labels);
      
      // Clear cache to force reload
      fieldLabelService.clearCache();
      
      toast({
        title: 'Success',
        description: `Field labels for ${mod.displayName} saved successfully`,
      });
    } catch (error) {
      console.error('Error saving field labels:', error);
      toast({
        title: 'Error',
        description: 'Failed to save field labels',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const handleReset = (module: string) => {
    setModuleLabels(prev =>
      prev.map(mod =>
        mod.module === module
          ? { ...mod, labels: { ...mod.defaultLabels } }
          : mod
      )
    );
    
    toast({
      title: 'Reset',
      description: 'Labels reset to defaults. Click Save to apply.',
    });
  };

  const currentModule = moduleLabels.find(m => m.module === activeModule);

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Field Labels</h1>
          <p className="text-muted-foreground">
            Customize field labels to match your business terminology
          </p>
        </div>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/administration">Administration</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/administration/system">System</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Field Labels</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Info Card */}
      <Card className="bg-blue-50 border-blue-200 dark:bg-blue-950 dark:border-blue-800">
        <CardContent className="pt-6">
          <div className="flex items-start gap-3">
            <Tag className="h-5 w-5 text-blue-600 dark:text-blue-400 mt-0.5" />
            <div>
              <h3 className="font-medium text-blue-900 dark:text-blue-100">
                Customize Your Field Labels
              </h3>
              <p className="text-sm text-blue-700 dark:text-blue-300 mt-1">
                Change the display names of fields to match your organization's terminology. 
                For example, you can rename "Brand" to "Make" or "Manufacturer" to "Vendor" 
                based on your business needs.
              </p>
            </div>
          </div>
        </CardContent>
      </Card>

      {loading ? (
        <Card>
          <CardContent className="py-12">
            <div className="flex items-center justify-center text-muted-foreground">
              <RefreshCw className="h-5 w-5 animate-spin mr-2" />
              Loading field labels...
            </div>
          </CardContent>
        </Card>
      ) : (
        <Tabs value={activeModule} onValueChange={setActiveModule}>
          <TabsList className="mb-4">
            {moduleLabels.map(mod => (
              <TabsTrigger key={mod.module} value={mod.module} className="flex items-center gap-2">
                {mod.icon}
                {mod.displayName}
              </TabsTrigger>
            ))}
          </TabsList>

          {moduleLabels.map(mod => (
            <TabsContent key={mod.module} value={mod.module}>
              <Card>
                <CardHeader>
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      {mod.icon}
                      <div>
                        <CardTitle>{mod.displayName} Field Labels</CardTitle>
                        <CardDescription>{mod.description}</CardDescription>
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => handleReset(mod.module)}
                        disabled={saving}
                      >
                        <RefreshCw className="h-4 w-4 mr-2" />
                        Reset to Defaults
                      </Button>
                      <Button
                        size="sm"
                        onClick={() => handleSave(mod.module)}
                        disabled={saving}
                      >
                        <Save className="h-4 w-4 mr-2" />
                        {saving ? 'Saving...' : 'Save Changes'}
                      </Button>
                    </div>
                  </div>
                </CardHeader>
                <CardContent>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                    {Object.entries(mod.labels).map(([fieldName, label]) => (
                      <div key={fieldName} className="space-y-2">
                        <div className="flex items-center justify-between">
                          <Label htmlFor={`${mod.module}-${fieldName}`} className="text-sm font-medium">
                            {fieldName}
                          </Label>
                          <span className="text-xs text-muted-foreground">
                            Default: {mod.defaultLabels[fieldName]}
                          </span>
                        </div>
                        <Input
                          id={`${mod.module}-${fieldName}`}
                          value={label}
                          onChange={(e) => handleLabelChange(mod.module, fieldName, e.target.value)}
                          placeholder={mod.defaultLabels[fieldName]}
                        />
                        <p className="text-xs text-muted-foreground">
                          This label will appear in forms and dialogs for the {fieldName.toLowerCase()} field.
                        </p>
                      </div>
                    ))}
                  </div>
                </CardContent>
              </Card>
            </TabsContent>
          ))}
        </Tabs>
      )}

      {/* Preview Card */}
      {currentModule && (
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">Preview</CardTitle>
            <CardDescription>
              See how your custom labels will appear in the application
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="border rounded-lg p-4 bg-muted/30">
              <h4 className="font-medium mb-4">Sample Form Fields</h4>
              <div className="grid grid-cols-4 gap-4">
                {Object.entries(currentModule.labels).map(([fieldName, label]) => (
                  <div key={fieldName} className="space-y-2">
                    <Label className="text-sm">{label}</Label>
                    <Input placeholder={`Enter ${label.toLowerCase()}...`} disabled />
                  </div>
                ))}
              </div>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
