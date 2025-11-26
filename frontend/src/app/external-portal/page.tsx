'use client';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  Building2,
  FileText,
  MapPin,
  CheckCircle,
  Clock,
  AlertCircle,
  ArrowRight,
  TrendingUp,
} from 'lucide-react';
import Link from 'next/link';
import { authService } from '@/services/auth';

export default function ExternalPortalDashboard() {
  const user = authService.getStoredUser();

  const quickActions = [
    {
      title: 'Register as Business Partner',
      description: 'Submit your company registration as a supplier or contractor',
      icon: Building2,
      href: '/external-portal/business-partner',
      color: 'bg-blue-500',
      available: true,
    },
    {
      title: 'Apply for Permit',
      description: 'Submit permit applications for various activities',
      icon: FileText,
      href: '/external-portal/permits',
      color: 'bg-green-500',
      available: false,
    },
    {
      title: 'Land Registration',
      description: 'Register land ownership and property details',
      icon: MapPin,
      href: '/external-portal/land-registration',
      color: 'bg-purple-500',
      available: false,
    },
  ];

  const recentApplications = [
    {
      id: 1,
      type: 'Business Partner Registration',
      status: 'Under Review',
      submittedDate: '2024-11-20',
      statusColor: 'bg-yellow-500',
      icon: Building2,
    },
    {
      id: 2,
      type: 'Document Verification',
      status: 'Approved',
      submittedDate: '2024-11-18',
      statusColor: 'bg-green-500',
      icon: CheckCircle,
    },
  ];

  const stats = [
    {
      title: 'Total Applications',
      value: '2',
      icon: FileText,
      color: 'text-blue-600',
      bgColor: 'bg-blue-100',
    },
    {
      title: 'Pending Review',
      value: '1',
      icon: Clock,
      color: 'text-yellow-600',
      bgColor: 'bg-yellow-100',
    },
    {
      title: 'Approved',
      value: '1',
      icon: CheckCircle,
      color: 'text-green-600',
      bgColor: 'bg-green-100',
    },
    {
      title: 'Action Required',
      value: '0',
      icon: AlertCircle,
      color: 'text-red-600',
      bgColor: 'bg-red-100',
    },
  ];

  return (
    <div className="space-y-6">
      {/* Welcome Section */}
      <div className="bg-gradient-to-r from-blue-600 to-blue-800 rounded-lg p-6 text-white">
        <h1 className="text-3xl font-bold mb-2">
          Welcome back, {user?.firstName}!
        </h1>
        <p className="text-blue-100">
          Manage your applications, registrations, and submissions all in one place.
        </p>
      </div>

      {/* Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        {stats.map((stat) => {
          const Icon = stat.icon;
          return (
            <Card key={stat.title}>
              <CardContent className="p-6">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-medium text-gray-600">
                      {stat.title}
                    </p>
                    <p className="text-3xl font-bold mt-2">{stat.value}</p>
                  </div>
                  <div className={`${stat.bgColor} p-3 rounded-lg`}>
                    <Icon className={`h-6 w-6 ${stat.color}`} />
                  </div>
                </div>
              </CardContent>
            </Card>
          );
        })}
      </div>

      {/* Quick Actions */}
      <Card>
        <CardHeader>
          <CardTitle>Quick Actions</CardTitle>
          <CardDescription>
            Start a new application or registration
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            {quickActions.map((action) => {
              const Icon = action.icon;
              return (
                <Link
                  key={action.title}
                  href={action.available ? action.href : '#'}
                  className={action.available ? '' : 'pointer-events-none'}
                >
                  <Card className="hover:shadow-lg transition-shadow cursor-pointer h-full">
                    <CardContent className="p-6">
                      <div className={`${action.color} w-12 h-12 rounded-lg flex items-center justify-center mb-4`}>
                        <Icon className="h-6 w-6 text-white" />
                      </div>
                      <h3 className="font-semibold mb-2 flex items-center justify-between">
                        {action.title}
                        {!action.available && (
                          <Badge variant="secondary" className="text-xs">
                            Coming Soon
                          </Badge>
                        )}
                      </h3>
                      <p className="text-sm text-gray-600 mb-4">
                        {action.description}
                      </p>
                      {action.available && (
                        <Button variant="ghost" size="sm" className="w-full">
                          Get Started
                          <ArrowRight className="ml-2 h-4 w-4" />
                        </Button>
                      )}
                    </CardContent>
                  </Card>
                </Link>
              );
            })}
          </div>
        </CardContent>
      </Card>

      {/* Recent Applications */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle>Recent Applications</CardTitle>
              <CardDescription>
                Track the status of your recent submissions
              </CardDescription>
            </div>
            <Button variant="outline" size="sm">
              View All
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {recentApplications.map((application) => {
              const Icon = application.icon;
              return (
                <div
                  key={application.id}
                  className="flex items-center justify-between p-4 border rounded-lg hover:bg-gray-50 transition-colors"
                >
                  <div className="flex items-center space-x-4">
                    <div className="bg-blue-100 p-3 rounded-lg">
                      <Icon className="h-5 w-5 text-blue-600" />
                    </div>
                    <div>
                      <p className="font-medium">{application.type}</p>
                      <p className="text-sm text-gray-600">
                        Submitted on {new Date(application.submittedDate).toLocaleDateString()}
                      </p>
                    </div>
                  </div>
                  <div className="flex items-center space-x-4">
                    <Badge
                      className={`${application.statusColor} text-white`}
                    >
                      {application.status}
                    </Badge>
                    <Button variant="ghost" size="sm">
                      View Details
                      <ArrowRight className="ml-2 h-4 w-4" />
                    </Button>
                  </div>
                </div>
              );
            })}
          </div>
        </CardContent>
      </Card>

      {/* Help Section */}
      <Card className="bg-gradient-to-r from-purple-50 to-blue-50 border-purple-200">
        <CardContent className="p-6">
          <div className="flex items-start justify-between">
            <div className="flex-1">
              <h3 className="text-lg font-semibold mb-2 flex items-center">
                <TrendingUp className="h-5 w-5 mr-2 text-purple-600" />
                Need Help?
              </h3>
              <p className="text-gray-600 mb-4">
                Our support team is here to assist you with any questions or issues.
              </p>
              <div className="flex space-x-3">
                <Button variant="outline">
                  View Documentation
                </Button>
                <Button>
                  Contact Support
                </Button>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

