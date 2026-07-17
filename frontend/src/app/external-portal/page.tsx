'use client';

import { useEffect, useState } from 'react';
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
  Briefcase,
  ClipboardList,
  Loader2,
} from 'lucide-react';
import Link from 'next/link';
import { authService } from '@/services/auth';
import { businessPartnerRegistrationService, type BusinessPartnerRegistrationDto } from '@/services/businessPartnerRegistrationService';
import * as tenderBidService from '@/services/tenderBidService';

export default function ExternalPortalDashboard() {
  const user = authService.getStoredUser();
  const [registrations, setRegistrations] = useState<BusinessPartnerRegistrationDto[]>([]);
  const [bidsCount, setBidsCount] = useState(0);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const loadData = async () => {
      try {
        setLoading(true);
        // Fetch user's registrations
        const regs = await businessPartnerRegistrationService.getMyRegistrations();
        setRegistrations(regs);

        // Fetch user's bids count
        try {
          const bids = await tenderBidService.getMyBids();
          setBidsCount(bids.length);
        } catch {
          // User may not have a business partner yet
          setBidsCount(0);
        }
      } catch (error) {
        console.error('Error loading dashboard data:', error);
      } finally {
        setLoading(false);
      }
    };

    loadData();
  }, []);

  // Calculate stats from real data
  const totalRegistrations = registrations.length;
  const pendingReviewCount = registrations.filter(
    r => r.status === 'Submitted' || r.status === 'UnderReview'
  ).length;
  const approvedCount = registrations.filter(r => r.status === 'Approved').length;
  const draftCount = registrations.filter(r => r.status === 'Draft').length;

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
      title: 'Browse Tenders',
      description: 'View available tenders and submit bids',
      icon: Briefcase,
      href: '/external-portal/tenders',
      color: 'bg-indigo-500',
      available: true,
    },
    {
      title: 'My Bids',
      description: 'View and manage your submitted bids',
      icon: ClipboardList,
      href: '/external-portal/my-bids',
      color: 'bg-teal-500',
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
    {
      title: 'Property Listings',
      description: 'Search available lands, buildings, and apartments for sale or rent',
      icon: MapPin,
      href: '/external-portal/property-listings',
      color: 'bg-emerald-500',
      available: true,
    },
    {
      title: 'Estate Documents',
      description: 'View dispatched Estate letters and document references',
      icon: FileText,
      href: '/external-portal/estate-documents',
      color: 'bg-sky-500',
      available: true,
    },
    {
      title: 'Estate Services',
      description: 'Request maintenance, change of use, searches, CTCs, and related Estate services',
      icon: ClipboardList,
      href: '/external-portal/estate-services',
      color: 'bg-cyan-500',
      available: true,
    },
  ];

  // Get recent applications from real data
  const recentApplications = registrations.slice(0, 5).map(reg => ({
    id: reg.id,
    type: 'Business Partner Registration',
    status: reg.status,
    submittedDate: reg.submittedDate || reg.createdAt,
    statusColor: reg.status === 'Approved' ? 'bg-green-500' :
                 reg.status === 'Rejected' ? 'bg-red-500' :
                 reg.status === 'Draft' ? 'bg-gray-500' :
                 'bg-yellow-500',
    icon: Building2,
    applicationNumber: reg.applicationNumber,
  }));

  const stats = [
    {
      title: 'Total Registrations',
      value: loading ? '-' : totalRegistrations.toString(),
      icon: FileText,
      color: 'text-blue-600',
      bgColor: 'bg-blue-100',
    },
    {
      title: 'Pending Review',
      value: loading ? '-' : pendingReviewCount.toString(),
      icon: Clock,
      color: 'text-yellow-600',
      bgColor: 'bg-yellow-100',
    },
    {
      title: 'Approved',
      value: loading ? '-' : approvedCount.toString(),
      icon: CheckCircle,
      color: 'text-green-600',
      bgColor: 'bg-green-100',
    },
    {
      title: 'My Bids',
      value: loading ? '-' : bidsCount.toString(),
      icon: ClipboardList,
      color: 'text-purple-600',
      bgColor: 'bg-purple-100',
    },
  ];

  return (
    <div className="space-y-6">
      {/* Welcome Section */}
      <div className="bg-gradient-to-r from-blue-600 to-blue-800 rounded-lg p-6 text-white">
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
            <Link href="/external-portal/business-partner">
              <Button variant="outline" size="sm">
                View All
              </Button>
            </Link>
          </div>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex items-center justify-center py-8">
              <Loader2 className="h-6 w-6 animate-spin text-gray-400" />
              <span className="ml-2 text-gray-500">Loading...</span>
            </div>
          ) : recentApplications.length === 0 ? (
            <div className="text-center py-8">
              <Building2 className="h-12 w-12 text-gray-300 mx-auto mb-4" />
              <p className="text-gray-500 mb-4">No applications yet</p>
              <Link href="/external-portal/business-partner">
                <Button>Start Your First Registration</Button>
              </Link>
            </div>
          ) : (
            <div className="space-y-4">
              {recentApplications.map((application) => {
                const Icon = application.icon;
                return (
                  <Link
                    key={application.id}
                    href={`/external-portal/business-partner/${application.id}`}
                    className="block"
                  >
                    <div className="flex items-center justify-between p-4 border rounded-lg hover:bg-gray-50 transition-colors">
                      <div className="flex items-center space-x-4">
                        <div className="bg-blue-100 p-3 rounded-lg">
                          <Icon className="h-5 w-5 text-blue-600" />
                        </div>
                        <div>
                          <p className="font-medium">{application.type}</p>
                          <p className="text-sm text-gray-500">{application.applicationNumber}</p>
                          {application.submittedDate && (
                            <p className="text-sm text-gray-600">
                              Submitted on {new Date(application.submittedDate).toLocaleDateString()}
                            </p>
                          )}
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
                  </Link>
                );
              })}
            </div>
          )}
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

