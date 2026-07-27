'use client';

import { useEffect, useState, type ComponentType } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
	  Building2,
	  FileCheck,
	  Clock,
	  CheckCircle,
	  XCircle,
	  ArrowRight,
	  Plus,
	  Eye,
	} from 'lucide-react';
import { useRouter } from 'next/navigation';
import {
	  businessPartnerRegistrationService,
	  type BusinessPartnerRegistrationDto,
} from '@/services/businessPartnerRegistrationService';

const STATUS_CONFIG: Record<string, { badgeClass: string; iconClass: string; icon: ComponentType<any> }> = {
	  Draft: { badgeClass: 'bg-gray-500', iconClass: 'text-gray-500', icon: FileCheck },
	  Submitted: { badgeClass: 'bg-blue-500', iconClass: 'text-blue-500', icon: Clock },
	  UnderReview: { badgeClass: 'bg-yellow-500', iconClass: 'text-yellow-500', icon: Clock },
	  Approved: { badgeClass: 'bg-green-500', iconClass: 'text-green-500', icon: CheckCircle },
	  Rejected: { badgeClass: 'bg-red-500', iconClass: 'text-red-500', icon: XCircle },
};

export default function BusinessPartnerPage() {
	  const router = useRouter();
	  const [registrations, setRegistrations] = useState<BusinessPartnerRegistrationDto[]>([]);
	  const [loading, setLoading] = useState(true);
	  const [error, setError] = useState<string | null>(null);

	  useEffect(() => {
	    const loadRegistrations = async () => {
	      try {
	        const data = await businessPartnerRegistrationService.getMyRegistrations();
	        setRegistrations(data);
	      } catch (err: any) {
	        console.error('Error loading registrations', err);
	        setError(err.message || 'Failed to load registrations');
	      } finally {
	        setLoading(false);
	      }
	    };

	    void loadRegistrations();
	  }, []);

	  const totalRegistrations = registrations.length;
	  const pendingReviewCount = registrations.filter(
	    (r) => r.status === 'Submitted' || r.status === 'UnderReview'
	  ).length;
	  const approvedCount = registrations.filter((r) => r.status === 'Approved').length;
	  const rejectedCount = registrations.filter((r) => r.status === 'Rejected').length;

	  const stats = [
	    {
	      title: 'Total Registrations',
	      value: totalRegistrations.toString(),
	      icon: Building2,
	      color: 'text-blue-600',
	      bgColor: 'bg-blue-100',
	    },
	    {
	      title: 'Pending Review',
	      value: pendingReviewCount.toString(),
	      icon: Clock,
	      color: 'text-yellow-600',
	      bgColor: 'bg-yellow-100',
	    },
	    {
	      title: 'Approved',
	      value: approvedCount.toString(),
	      icon: CheckCircle,
	      color: 'text-green-600',
	      bgColor: 'bg-green-100',
	    },
	    {
	      title: 'Rejected',
	      value: rejectedCount.toString(),
	      icon: XCircle,
	      color: 'text-red-600',
	      bgColor: 'bg-red-100',
	    },
	  ];

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Business Partner Registration</h1>
          <p className="text-gray-600 mt-2">
            Register your company as a supplier or contractor
          </p>
        </div>
        <Button
          size="lg"
          onClick={() => router.push('/supplier-application')}
        >
          <Plus className="mr-2 h-5 w-5" />
          New Registration
        </Button>
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

      {/* Information Card */}
      <Card className="bg-blue-50 border-blue-200">
        <CardContent className="p-6">
          <div className="flex items-start space-x-4">
            <div className="bg-blue-500 p-3 rounded-lg">
              <FileCheck className="h-6 w-6 text-white" />
            </div>
            <div className="flex-1">
              <h3 className="font-semibold text-lg mb-2">
                How to Register as a Business Partner
              </h3>
              <ul className="space-y-2 text-gray-700">
                <li className="flex items-start">
                  <span className="font-semibold mr-2">1.</span>
                  <span>Complete the multi-step registration form with your company details</span>
                </li>
                <li className="flex items-start">
                  <span className="font-semibold mr-2">2.</span>
                  <span>Upload required documents (Business License, Tax Clearance, etc.)</span>
                </li>
                <li className="flex items-start">
                  <span className="font-semibold mr-2">3.</span>
                  <span>Submit your registration for review</span>
                </li>
                <li className="flex items-start">
                  <span className="font-semibold mr-2">4.</span>
                  <span>Wait for approval from our procurement team</span>
                </li>
                <li className="flex items-start">
                  <span className="font-semibold mr-2">5.</span>
                  <span>Once approved, you can start doing business with us</span>
                </li>
              </ul>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Registrations List */}
      <Card>
        <CardHeader>
          <CardTitle>My Registrations</CardTitle>
          <CardDescription>
            View and track your business partner registration applications
          </CardDescription>
        </CardHeader>
	        <CardContent>
	          {loading ? (
	            <div className="text-center py-12">
	              <Building2 className="h-12 w-12 text-gray-400 mx-auto mb-4" />
	              <p className="text-gray-600">Loading your registrations...</p>
	            </div>
	          ) : error ? (
	            <div className="text-center py-12">
	              <h3 className="text-lg font-semibold mb-2 text-red-600">Failed to load registrations</h3>
	              <p className="text-gray-600 mb-4">{error}</p>
	              <Button onClick={() => router.push('/supplier-application')}>
	                <Plus className="mr-2 h-4 w-4" />
	                Start New Registration
	              </Button>
	            </div>
	          ) : registrations.length === 0 ? (
	            <div className="text-center py-12">
	              <Building2 className="h-12 w-12 text-gray-400 mx-auto mb-4" />
	              <h3 className="text-lg font-semibold mb-2">No Registrations Yet</h3>
	              <p className="text-gray-600 mb-6">
	                Start by creating your first business partner registration
	              </p>
	              <Button onClick={() => router.push('/supplier-application')}>
	                <Plus className="mr-2 h-4 w-4" />
	                Create Registration
	              </Button>
	            </div>
	          ) : (
	            <div className="space-y-4">
	              {registrations.map((registration) => {
	                const statusConfig =
	                  STATUS_CONFIG[registration.status as keyof typeof STATUS_CONFIG] || STATUS_CONFIG.Draft;
	                const StatusIcon = statusConfig.icon;
	                const dateToShow = registration.submittedDate || registration.createdAt;

	                return (
	                  <div
	                    key={registration.id}
	                    className="flex items-center justify-between p-4 border rounded-lg hover:bg-gray-50 transition-colors"
	                  >
	                    <div className="flex items-center space-x-4">
	                      <div className="bg-blue-100 p-3 rounded-lg">
	                        <Building2 className="h-6 w-6 text-blue-600" />
	                      </div>
	                      <div>
	                        <h3 className="font-semibold">{registration.companyName}</h3>
	                        <div className="flex items-center space-x-3 mt-1">
	                          <Badge variant="outline">{registration.partnerType}</Badge>
	                          <span className="text-sm text-gray-600">
	                            {registration.status === 'Draft' ? 'Created on ' : 'Submitted on '}
	                            {dateToShow ? new Date(dateToShow).toLocaleDateString() : 'N/A'}
	                          </span>
	                        </div>
	                      </div>
	                    </div>
	                    <div className="flex items-center space-x-4">
	                      <div className="flex items-center space-x-2">
	                        <StatusIcon className={`h-5 w-5 ${statusConfig.iconClass}`} />
	                        <Badge className={`${statusConfig.badgeClass} text-white`}>
	                          {registration.status}
	                        </Badge>
	                      </div>
	                      {registration.status === 'Draft' ? (
	                        <Button
	                          variant="outline"
	                          size="sm"
	                          onClick={() => router.push(`/register/business-partner?id=${registration.id}`)}
	                        >
	                          <FileCheck className="mr-2 h-4 w-4" />
	                          Continue Editing
	                        </Button>
	                      ) : (
	                        <Button
	                          variant="outline"
	                          size="sm"
	                          onClick={() => router.push(`/register/business-partner/status/${registration.id}`)}
	                        >
	                          <Eye className="mr-2 h-4 w-4" />
	                          {registration.status === 'MoreInfoRequired' ? 'View Feedback & Update' : 'View Details'}
	                        </Button>
	                      )}
	                    </div>
	                  </div>
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
              <h3 className="text-lg font-semibold mb-2">
                Need Assistance?
              </h3>
              <p className="text-gray-600 mb-4">
                If you have questions about the registration process or need help with your application,
                our support team is ready to assist you.
              </p>
              <div className="flex space-x-3">
                <Button variant="outline">
                  View FAQ
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

