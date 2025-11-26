'use client';

import { Card, CardContent } from '@/components/ui/card';
import { FileText, Clock } from 'lucide-react';

export default function PermitsPage() {
  return (
    <div className="max-w-4xl mx-auto">
      <Card>
        <CardContent className="p-12 text-center">
          <div className="bg-yellow-100 p-6 rounded-full w-24 h-24 mx-auto mb-6 flex items-center justify-center">
            <FileText className="h-12 w-12 text-yellow-600" />
          </div>
          <h1 className="text-3xl font-bold mb-4">Permit Applications</h1>
          <div className="flex items-center justify-center space-x-2 mb-4">
            <Clock className="h-5 w-5 text-gray-500" />
            <span className="text-lg text-gray-600">Coming Soon</span>
          </div>
          <p className="text-gray-600 max-w-md mx-auto">
            This feature is currently under development. You'll soon be able to submit and track
            permit applications for various activities directly from this portal.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}

