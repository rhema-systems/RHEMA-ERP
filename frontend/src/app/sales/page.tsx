'use client';

import { useRouter } from 'next/navigation';
import { ShoppingCart, Package, ClipboardList, TrendingUp, Truck, FileText } from 'lucide-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';

export default function SalesPage() {
  const router = useRouter();

  const modules = [
    {
      title: 'Sales Orders',
      description: 'Create and manage sales orders, track lifecycle from draft to delivery',
      icon: ShoppingCart,
      href: '/sales/orders',
      color: 'text-blue-600',
      bgColor: 'bg-blue-50',
    },
    {
      title: 'Delivery Notes',
      description: 'Manage delivery notes, pack, ship, and confirm deliveries',
      icon: Truck,
      href: '/sales/deliveries',
      color: 'text-green-600',
      bgColor: 'bg-green-50',
    },
    {
      title: 'Sales Agreements',
      description: 'Manage customer agreements, leases, contracts, and milestones',
      icon: FileText,
      href: '/sales/agreements',
      color: 'text-purple-600',
      bgColor: 'bg-purple-50',
    },
  ];

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div>
        <h1 className="text-3xl font-bold flex items-center gap-2">
          <TrendingUp className="h-8 w-8 text-blue-600" />
          Sales & Distribution
        </h1>
        <p className="text-gray-500 mt-1">
          Manage sales orders, deliveries, and customer interactions
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {modules.map((module) => {
          const Icon = module.icon;
          return (
            <Card
              key={module.title}
              className="cursor-pointer hover:shadow-lg transition-all duration-200 hover:border-blue-300"
              onClick={() => router.push(module.href)}
            >
              <CardHeader>
                <div className={`w-12 h-12 ${module.bgColor} rounded-lg flex items-center justify-center mb-3`}>
                  <Icon className={`h-6 w-6 ${module.color}`} />
                </div>
                <CardTitle className="text-lg">{module.title}</CardTitle>
                <CardDescription>{module.description}</CardDescription>
              </CardHeader>
              <CardContent>
                <Button variant="outline" className="w-full">
                  Open →
                </Button>
              </CardContent>
            </Card>
          );
        })}
      </div>
    </div>
  );
}
