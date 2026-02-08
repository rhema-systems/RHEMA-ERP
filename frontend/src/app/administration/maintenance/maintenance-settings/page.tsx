'use client';

import { useEffect, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { useToast } from '@/hooks/use-toast';
import { Loader2, Save, Settings as SettingsIcon } from 'lucide-react';
import maintenanceSettingsService, { MaintenanceSettingsDto, UpdateMaintenanceSettingsDto } from '@/services/maintenanceSettingsService';

export default function MaintenanceSettingsPage() {
  const { toast } = useToast();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [settings, setSettings] = useState<MaintenanceSettingsDto | null>(null);
  const [formData, setFormData] = useState<UpdateMaintenanceSettingsDto>({
    fleetComplianceDueSoonDays: 7,
    blockFleetDispatchWhenComplianceDueSoon: true
  });

  useEffect(() => {
    loadSettings();
  }, []);

  const loadSettings = async () => {
    try {
      setLoading(true);
      const data = await maintenanceSettingsService.getSettings();
      setSettings(data);
      setFormData({
        fleetComplianceDueSoonDays: data.fleetComplianceDueSoonDays ?? 7,
        blockFleetDispatchWhenComplianceDueSoon: !!data.blockFleetDispatchWhenComplianceDueSoon
      });
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to load maintenance settings',
        variant: 'destructive'
      });
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    try {
      if (formData.fleetComplianceDueSoonDays < 0 || formData.fleetComplianceDueSoonDays > 3650) {
        toast({
          title: 'Validation Error',
          description: 'Compliance due soon days must be between 0 and 3650.',
          variant: 'destructive'
        });
        return;
      }

      setSaving(true);
      const updated = await maintenanceSettingsService.updateSettings(formData);
      setSettings(updated);
      toast({
        title: 'Success',
        description: 'Maintenance settings updated successfully'
      });
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to update maintenance settings',
        variant: 'destructive'
      });
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-96">
        <Loader2 className="h-8 w-8 animate-spin text-blue-600" />
      </div>
    );
  }

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
            <SettingsIcon className="h-8 w-8" />
            Maintenance Settings
          </h1>
          <p className="text-muted-foreground mt-2">
            Configure maintenance module settings (including Fleet dispatch policies)
          </p>
        </div>
        <Button onClick={handleSave} disabled={saving}>
          {saving ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              Saving...
            </>
          ) : (
            <>
              <Save className="mr-2 h-4 w-4" />
              Save Settings
            </>
          )}
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Fleet Dispatch Policy</CardTitle>
          <CardDescription>
            Controls how compliance expiry affects fleet trip dispatch (vehicles only).
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          <div className="space-y-2 max-w-sm">
            <Label htmlFor="fleetComplianceDueSoonDays">Compliance “due soon” window (days)</Label>
            <p className="text-sm text-muted-foreground">
              Compliance items expiring within this window are treated as due soon (used for reminders and optional dispatch blocking).
            </p>
            <Input
              id="fleetComplianceDueSoonDays"
              type="number"
              min={0}
              max={3650}
              value={formData.fleetComplianceDueSoonDays}
              onChange={(e) =>
                setFormData(prev => ({
                  ...prev,
                  fleetComplianceDueSoonDays: Number.parseInt(e.target.value || '0', 10)
                }))
              }
            />
          </div>

          <div className="flex items-center justify-between space-x-4">
            <div className="flex-1 space-y-1">
              <Label htmlFor="blockFleetDispatchWhenComplianceDueSoon">Block dispatch when compliance is due soon</Label>
              <p className="text-sm text-muted-foreground">
                When enabled, dispatch is blocked if any critical compliance item is due within the configured window.
              </p>
            </div>
            <Switch
              id="blockFleetDispatchWhenComplianceDueSoon"
              checked={formData.blockFleetDispatchWhenComplianceDueSoon}
              onCheckedChange={(checked) =>
                setFormData(prev => ({ ...prev, blockFleetDispatchWhenComplianceDueSoon: checked }))
              }
            />
          </div>

          {settings ? (
            <div className="text-xs text-muted-foreground">
              Last updated: {settings.updatedAt ? new Date(settings.updatedAt).toLocaleString() : 'Never'}
            </div>
          ) : null}
        </CardContent>
      </Card>
    </div>
  );
}

