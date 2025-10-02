'use client';
import React from 'react';

interface PasswordStrengthMeterProps {
  password: string;
  className?: string;
}

interface PasswordStrength {
  score: number; // 0-4
  label: string;
  color: string;
  bgColor: string;
  suggestions: string[];
}

export const PasswordStrengthMeter: React.FC<PasswordStrengthMeterProps> = ({ 
  password, 
  className = '' 
}) => {
  const calculateStrength = (password: string): PasswordStrength => {
    if (!password) {
      return {
        score: 0,
        label: '',
        color: 'text-gray-400',
        bgColor: 'bg-gray-200',
        suggestions: []
      };
    }

    let score = 0;
    const suggestions: string[] = [];

    // Length check
    if (password.length >= 8) {
      score += 1;
    } else {
      suggestions.push('Use at least 8 characters');
    }

    // Uppercase check
    if (/[A-Z]/.test(password)) {
      score += 1;
    } else {
      suggestions.push('Include uppercase letters');
    }

    // Lowercase check
    if (/[a-z]/.test(password)) {
      score += 1;
    } else {
      suggestions.push('Include lowercase letters');
    }

    // Number check
    if (/\d/.test(password)) {
      score += 1;
    } else {
      suggestions.push('Include numbers');
    }

    // Special character check
    if (/[^A-Za-z0-9]/.test(password)) {
      score += 1;
    } else {
      suggestions.push('Include special characters (!@#$%^&*)');
    }

    // Determine label and colors based on score
    let label = '';
    let color = '';
    let bgColor = '';

    switch (score) {
      case 0:
      case 1:
        label = 'Very Weak';
        color = 'text-red-600';
        bgColor = 'bg-red-500';
        break;
      case 2:
        label = 'Weak';
        color = 'text-orange-600';
        bgColor = 'bg-orange-500';
        break;
      case 3:
        label = 'Fair';
        color = 'text-yellow-600';
        bgColor = 'bg-yellow-500';
        break;
      case 4:
        label = 'Good';
        color = 'text-blue-600';
        bgColor = 'bg-blue-500';
        break;
      case 5:
        label = 'Strong';
        color = 'text-green-600';
        bgColor = 'bg-green-500';
        break;
      default:
        label = '';
        color = 'text-gray-400';
        bgColor = 'bg-gray-200';
    }

    return {
      score: Math.min(score, 4), // Cap at 4 for display purposes
      label,
      color,
      bgColor,
      suggestions
    };
  };

  const strength = calculateStrength(password);

  if (!password) {
    return null;
  }

  return (
    <div className={`password-strength-meter ${className}`}>
      <div className="flex items-center justify-between mb-1">
        <span className="text-sm font-medium text-gray-700">Password Strength</span>
        <span className={`text-sm font-medium ${strength.color}`}>
          {strength.label}
        </span>
      </div>
      
      <div className="w-full bg-gray-200 rounded-full h-2 mb-2">
        <div
          className={`h-2 rounded-full transition-all duration-300 ${strength.bgColor}`}
          style={{ width: `${(strength.score / 4) * 100}%` }}
        />
      </div>

      {strength.suggestions.length > 0 && (
        <div className="text-xs text-gray-600 space-y-1">
          <p className="font-medium">To improve your password:</p>
          <ul className="list-disc list-inside space-y-0.5 ml-2">
            {strength.suggestions.map((suggestion, index) => (
              <li key={index}>{suggestion}</li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
};

export default PasswordStrengthMeter;