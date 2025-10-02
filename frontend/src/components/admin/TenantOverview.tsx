'use client';

import React, { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card';
import { Badge } from '../ui/badge';
import { Button } from '../ui/button';
import { Avatar, AvatarFallback, AvatarImage } from '../ui/avatar';
import { Progress } from '../ui/progress';
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from '../ui/tabs';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '../ui/dialog';
import {
  Building,
  Users,
  Settings,
  Activity,
  Globe,
  Database,
  Server,
  Zap,
  Calendar,
  Shield,
  Mail,
  Phone,
  MapPin,
  Palette,
  Clock,
  TrendingUp,
  AlertCircle,
  CheckCircle,
  XCircle,
  BarChart3,
  PieChart,
  Wifi,
  WifiOff
} from 'lucide-react';
import { Tenant } from '../../services/admin-api.service';
import { cn } from '../../lib/utils';
import { ClientOnly } from '../ui/client-only';

interface TenantOverviewProps {
  tenant: Tenant;
  isOpen: boolean;
  onClose: () => void;
  onEdit?: (tenant: Tenant) => void;
}

interface TenantStats {
  totalUsers: number;
  activeUsers: number;
  storageUsed: number;
  storageLimit: number;
  apiCallsThisMonth: number;
  lastActivity: Date;
  uptime: number;
  healthScore: number;
}

interface TenantActivity {
  id: string;
  type: 'user_login' | 'user_signup' | 'config_change' | 'api_call' | 'error';
  description: string;
  timestamp: Date;
  user?: string;
  severity: 'low' | 'medium' | 'high';
}

// Mock data - in real app, this would come from API
const generateMockStats = (): TenantStats => ({
  totalUsers: Math.floor(Math.random() * 500) + 50,
  activeUsers: Math.floor(Math.random() * 200) + 20,
  storageUsed: Math.floor(Math.random() * 8000) + 1000, // MB
  storageLimit: 10240, // 10GB
  apiCallsThisMonth: Math.floor(Math.random() * 50000) + 5000,
  lastActivity: new Date(Date.now() - Math.random() * 1000 * 60 * 60 * 24), // within 24h
  uptime: 99.5 + Math.random() * 0.5,
  healthScore: 85 + Math.random() * 15
});

const generateMockActivity = (): TenantActivity[] => [
  {
    id: '1',
    type: 'user_login',
    description: 'User john.doe@company.com logged in',
    timestamp: new Date(Date.now() - 1000 * 60 * 15), // 15 min ago
    user: 'john.doe@company.com',
    severity: 'low'
  },
  {
    id: '2',
    type: 'config_change',
    description: 'LDAP settings updated',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 2), // 2 hours ago
    user: 'admin@company.com',
    severity: 'medium'
  },
  {
    id: '3',
    type: 'user_signup',
    description: 'New user registered: jane.smith@company.com',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 6), // 6 hours ago
    user: 'jane.smith@company.com',
    severity: 'low'
  },
  {
    id: '4',
    type: 'error',
    description: 'API rate limit exceeded (recovered)',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 12), // 12 hours ago
    severity: 'high'
  },
  {
    id: '5',
    type: 'api_call',
    description: 'High API usage detected (15,000 calls today)',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 18), // 18 hours ago
    severity: 'medium'
  }
];

export function TenantOverview({
  tenant,
  isOpen,
  onClose,
  onEdit
}: TenantOverviewProps) {
  const [activeTab, setActiveTab] = useState('overview');
  const [stats] = useState<TenantStats>(generateMockStats());
  const [activity] = useState<TenantActivity[]>(generateMockActivity());

  const formatFileSize = (bytes: number) => {
    if (bytes === 0) return '0 MB';
    const mb = bytes;
    const gb = mb / 1024;
    return gb >= 1 ? `${gb.toFixed(2)} GB` : `${mb.toFixed(0)} MB`;
  };

  const formatDate = (date: Date) => {
    return new Intl.DateTimeFormat('en-US', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    }).format(date);
  };

  const getTimeAgo = (date: Date) => {
    const now = new Date();
    const diff = now.getTime() - date.getTime();
    const minutes = Math.floor(diff / (1000 * 60));
    const hours = Math.floor(diff / (1000 * 60 * 60));
    const days = Math.floor(diff / (1000 * 60 * 60 * 24));

    if (minutes < 60) return `${minutes}m ago`;
    if (hours < 24) return `${hours}h ago`;
    return `${days}d ago`;
  };

  const getActivityIcon = (type: TenantActivity['type']) => {
    switch (type) {
      case 'user_login':
        return <Users className="h-3 w-3" />;
      case 'user_signup':
        return <Users className="h-3 w-3" />;
      case 'config_change':
        return <Settings className="h-3 w-3" />;
      case 'api_call':
        return <Server className="h-3 w-3" />;
      case 'error':
        return <AlertCircle className="h-3 w-3" />;
      default:
        return <Activity className="h-3 w-3" />;
    }
  };

  const getActivityColor = (severity: TenantActivity['severity']) => {
    switch (severity) {
      case 'low':
        return 'text-green-600 bg-green-100';
      case 'medium':
        return 'text-yellow-600 bg-yellow-100';
      case 'high':
        return 'text-red-600 bg-red-100';
      default:
        return 'text-gray-600 bg-gray-100';
    }
  };

  const getHealthScoreColor = (score: number) => {
    if (score >= 90) return 'text-green-600';
    if (score >= 70) return 'text-yellow-600';
    return 'text-red-600';
  };

  const storageUsagePercent = (stats.storageUsed / stats.storageLimit) * 100;

  if (!isOpen) return null;

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-6xl max-h-[90vh] overflow-hidden">
        <DialogHeader className="pb-4 border-b">
          <div className="flex items-center justify-between">
            <div className="flex items-center space-x-4">
              <Avatar className="h-12 w-12">
                <AvatarImage src={tenant.logoUrl} />
                <AvatarFallback>
                  <Building className="h-6 w-6" />
                </AvatarFallback>
              </Avatar>
              <div>
                <DialogTitle className="text-xl">
                  {tenant.name}
                </DialogTitle>
                <DialogDescription className="flex items-center space-x-2">
                  <span>{tenant.code}</span>
                  <Badge variant={tenant.isActive ? 'default' : 'secondary'}>
                    {tenant.isActive ? 'Active' : 'Inactive'}
                  </Badge>
                </DialogDescription>
              </div>
            </div>

            <div className="flex items-center space-x-2">
              {onEdit && (
                <Button variant="outline" size="sm" onClick={() => onEdit(tenant)}>
                  <Settings className="h-4 w-4 mr-2" />
                  Edit Tenant
                </Button>
              )}
            </div>
          </div>
        </DialogHeader>

        <div className="flex-1 overflow-hidden">
          <Tabs value={activeTab} onValueChange={setActiveTab} className="h-full">
            <TabsList className="grid w-full grid-cols-4">
              <TabsTrigger value="overview">Overview</TabsTrigger>
              <TabsTrigger value="analytics">Analytics</TabsTrigger>
              <TabsTrigger value="activity">Activity</TabsTrigger>
              <TabsTrigger value="settings">Settings</TabsTrigger>
            </TabsList>

            <div className="mt-4 overflow-y-auto max-h-96">
              <TabsContent value="overview" className="space-y-6">
                {/* Key Stats */}
                <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                  <Card>
                    <CardContent className="p-4">
                      <div className="flex items-center justify-between">
                        <div>
                          <p className="text-sm font-medium text-muted-foreground">Total Users</p>
                          <p className="text-2xl font-bold">{stats.totalUsers.toLocaleString()}</p>
                        </div>
                        <Users className="h-8 w-8 text-blue-600" />
                      </div>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardContent className="p-4">
                      <div className="flex items-center justify-between">
                        <div>
                          <p className="text-sm font-medium text-muted-foreground">Active Users</p>
                          <p className="text-2xl font-bold">{stats.activeUsers.toLocaleString()}</p>
                        </div>
                        <Activity className="h-8 w-8 text-green-600" />
                      </div>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardContent className="p-4">
                      <div className="flex items-center justify-between">
                        <div>
                          <p className="text-sm font-medium text-muted-foreground">API Calls</p>
                          <p className="text-2xl font-bold">{stats.apiCallsThisMonth.toLocaleString()}</p>
                        </div>
                        <Server className="h-8 w-8 text-purple-600" />
                      </div>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardContent className="p-4">
                      <div className="flex items-center justify-between">
                        <div>
                          <p className="text-sm font-medium text-muted-foreground">Health Score</p>
                          <p className={cn("text-2xl font-bold", getHealthScoreColor(stats.healthScore))}>
                            {stats.healthScore.toFixed(0)}%
                          </p>
                        </div>
                        <Shield className={cn("h-8 w-8", getHealthScoreColor(stats.healthScore))} />
                      </div>
                    </CardContent>
                  </Card>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  {/* Storage Usage */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg flex items-center">
                        <Database className="h-5 w-5 mr-2" />
                        Storage Usage
                      </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                      <div className="space-y-2">
                        <div className="flex justify-between text-sm">
                          <span>Used: {formatFileSize(stats.storageUsed)}</span>
                          <span>Limit: {formatFileSize(stats.storageLimit)}</span>
                        </div>
                        <Progress value={storageUsagePercent} className="h-2" />
                        <div className="text-xs text-muted-foreground">
                          {storageUsagePercent.toFixed(1)}% of storage quota used
                        </div>
                      </div>
                    </CardContent>
                  </Card>

                  {/* System Status */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg flex items-center">
                        <Zap className="h-5 w-5 mr-2" />
                        System Status
                      </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                      <div className="flex items-center justify-between">
                        <span className="text-sm">Uptime</span>
                        <div className="flex items-center space-x-2">
                          <Badge variant="outline" className="text-green-600">
                            {stats.uptime.toFixed(2)}%
                          </Badge>
                          <CheckCircle className="h-4 w-4 text-green-600" />
                        </div>
                      </div>
                      
                      <div className="flex items-center justify-between">
                        <span className="text-sm">LDAP Connection</span>
                        <div className="flex items-center space-x-2">
                          <Badge variant={tenant.ldapEnabled ? 'default' : 'secondary'}>
                            {tenant.ldapEnabled ? 'Connected' : 'Disabled'}
                          </Badge>
                          {tenant.ldapEnabled ? (
                            <Wifi className="h-4 w-4 text-green-600" />
                          ) : (
                            <WifiOff className="h-4 w-4 text-gray-400" />
                          )}
                        </div>
                      </div>

                      <div className="flex items-center justify-between">
                        <span className="text-sm">Last Activity</span>
                        <span className="text-sm text-muted-foreground">
                          {getTimeAgo(stats.lastActivity)}
                        </span>
                      </div>
                    </CardContent>
                  </Card>
                </div>

                {/* Contact & Configuration Info */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg flex items-center">
                        <Mail className="h-5 w-5 mr-2" />
                        Contact Information
                      </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                      {tenant.contactEmail && (
                        <div className="flex items-center space-x-3">
                          <Mail className="h-4 w-4 text-muted-foreground" />
                          <span className="text-sm">{tenant.contactEmail}</span>
                        </div>
                      )}
                      
                      {tenant.contactPhone && (
                        <div className="flex items-center space-x-3">
                          <Phone className="h-4 w-4 text-muted-foreground" />
                          <span className="text-sm">{tenant.contactPhone}</span>
                        </div>
                      )}
                      
                      {tenant.address && (
                        <div className="flex items-center space-x-3">
                          <MapPin className="h-4 w-4 text-muted-foreground" />
                          <span className="text-sm">{tenant.address}</span>
                        </div>
                      )}
                      
                      {tenant.domain && (
                        <div className="flex items-center space-x-3">
                          <Globe className="h-4 w-4 text-muted-foreground" />
                          <span className="text-sm">{tenant.domain}</span>
                        </div>
                      )}
                    </CardContent>
                  </Card>

                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg flex items-center">
                        <Settings className="h-5 w-5 mr-2" />
                        Configuration
                      </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                      <div className="grid grid-cols-2 gap-4 text-sm">
                        <div>
                          <div className="font-medium">Self Registration</div>
                          <div className="text-muted-foreground">
                            {tenant.allowSelfRegistration ? 'Enabled' : 'Disabled'}
                          </div>
                        </div>
                        
                        <div>
                          <div className="font-medium">Email Verification</div>
                          <div className="text-muted-foreground">
                            {tenant.requireEmailVerification ? 'Required' : 'Optional'}
                          </div>
                        </div>
                        
                        <div>
                          <div className="font-medium">User Audience</div>
                          <div className="text-muted-foreground">
                            {tenant.userAudience === 1 ? 'Internal' : 
                             tenant.userAudience === 2 ? 'External' : 'Both'}
                          </div>
                        </div>
                        
                        <div>
                          <div className="font-medium">Priority</div>
                          <div className="text-muted-foreground">
                            {tenant.defaultPriority}
                          </div>
                        </div>
                      </div>
                    </CardContent>
                  </Card>
                </div>
              </TabsContent>

              <TabsContent value="analytics" className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  {/* Usage Trends */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg flex items-center">
                        <TrendingUp className="h-5 w-5 mr-2" />
                        Usage Trends
                      </CardTitle>
                    </CardHeader>
                    <CardContent>
                      <div className="space-y-4">
                        <div className="flex items-center justify-between text-sm">
                          <span>Daily Active Users</span>
                          <span className="font-mono">{Math.floor(stats.activeUsers * 0.8)}</span>
                        </div>
                        <div className="flex items-center justify-between text-sm">
                          <span>Weekly Active Users</span>
                          <span className="font-mono">{Math.floor(stats.activeUsers * 1.2)}</span>
                        </div>
                        <div className="flex items-center justify-between text-sm">
                          <span>Monthly Active Users</span>
                          <span className="font-mono">{stats.activeUsers}</span>
                        </div>
                        <div className="flex items-center justify-between text-sm">
                          <span>Growth Rate</span>
                          <span className="font-mono text-green-600">+{(Math.random() * 20).toFixed(1)}%</span>
                        </div>
                      </div>
                    </CardContent>
                  </Card>

                  {/* Resource Usage */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg flex items-center">
                        <BarChart3 className="h-5 w-5 mr-2" />
                        Resource Usage
                      </CardTitle>
                    </CardHeader>
                    <CardContent>
                      <div className="space-y-4">
                        <div>
                          <div className="flex justify-between text-sm mb-1">
                            <span>CPU Usage</span>
                            <span>{(Math.random() * 40 + 20).toFixed(0)}%</span>
                          </div>
                          <Progress value={Math.random() * 40 + 20} className="h-2" />
                        </div>
                        
                        <div>
                          <div className="flex justify-between text-sm mb-1">
                            <span>Memory Usage</span>
                            <span>{(Math.random() * 60 + 30).toFixed(0)}%</span>
                          </div>
                          <Progress value={Math.random() * 60 + 30} className="h-2" />
                        </div>
                        
                        <div>
                          <div className="flex justify-between text-sm mb-1">
                            <span>Network I/O</span>
                            <span>{(Math.random() * 50 + 10).toFixed(0)}%</span>
                          </div>
                          <Progress value={Math.random() * 50 + 10} className="h-2" />
                        </div>
                      </div>
                    </CardContent>
                  </Card>
                </div>
              </TabsContent>

              <TabsContent value="activity" className="space-y-4">
                <Card>
                  <CardHeader>
                    <CardTitle className="text-lg flex items-center">
                      <Activity className="h-5 w-5 mr-2" />
                      Recent Activity
                    </CardTitle>
                  </CardHeader>
                  <CardContent>
                    <div className="space-y-4">
                      {activity.map((item) => (
                        <div key={item.id} className="flex items-start space-x-3 pb-4 border-b last:border-0">
                          <div className={cn(
                            "p-1 rounded-full",
                            getActivityColor(item.severity)
                          )}>
                            {getActivityIcon(item.type)}
                          </div>
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center justify-between">
                              <p className="text-sm font-medium">{item.description}</p>
                              <span className="text-xs text-muted-foreground">
                                {getTimeAgo(item.timestamp)}
                              </span>
                            </div>
                            {item.user && (
                              <p className="text-xs text-muted-foreground">
                                by {item.user}
                              </p>
                            )}
                          </div>
                        </div>
                      ))}
                    </div>
                  </CardContent>
                </Card>
              </TabsContent>

              <TabsContent value="settings" className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  {/* Branding */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg flex items-center">
                        <Palette className="h-5 w-5 mr-2" />
                        Branding
                      </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                      <div className="flex items-center space-x-3">
                        <div className="w-8 h-8 rounded border overflow-hidden bg-muted">
                          {tenant.logoUrl ? (
                            <img src={tenant.logoUrl} alt="Logo" className="w-full h-full object-contain" />
                          ) : (
                            <Building className="h-4 w-4 m-2" />
                          )}
                        </div>
                        <div>
                          <div className="text-sm font-medium">Logo</div>
                          <div className="text-xs text-muted-foreground">
                            {tenant.logoUrl ? 'Custom logo set' : 'No logo'}
                          </div>
                        </div>
                      </div>
                      
                      {tenant.primaryColor && (
                        <div className="flex items-center space-x-3">
                          <div 
                            className="w-8 h-8 rounded border"
                            style={{ backgroundColor: tenant.primaryColor }}
                          />
                          <div>
                            <div className="text-sm font-medium">Primary Color</div>
                            <div className="text-xs text-muted-foreground font-mono">
                              {tenant.primaryColor}
                            </div>
                          </div>
                        </div>
                      )}
                    </CardContent>
                  </Card>

                  {/* Security & Access */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg flex items-center">
                        <Shield className="h-5 w-5 mr-2" />
                        Security & Access
                      </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                      <div className="grid grid-cols-1 gap-3 text-sm">
                        <div className="flex justify-between">
                          <span>Default for Public</span>
                          <Badge variant={tenant.isDefaultForPublicUsers ? 'default' : 'secondary'}>
                            {tenant.isDefaultForPublicUsers ? 'Yes' : 'No'}
                          </Badge>
                        </div>
                        
                        <div className="flex justify-between">
                          <span>Default for Internal</span>
                          <Badge variant={tenant.isDefaultForInternalUsers ? 'default' : 'secondary'}>
                            {tenant.isDefaultForInternalUsers ? 'Yes' : 'No'}
                          </Badge>
                        </div>
                        
                        <div className="flex justify-between">
                          <span>LDAP Enabled</span>
                          <Badge variant={tenant.ldapEnabled ? 'default' : 'secondary'}>
                            {tenant.ldapEnabled ? 'Yes' : 'No'}
                          </Badge>
                        </div>
                      </div>
                    </CardContent>
                  </Card>
                </div>
              </TabsContent>
            </div>
          </Tabs>
        </div>
      </DialogContent>
    </Dialog>
  );
}

export default TenantOverview;