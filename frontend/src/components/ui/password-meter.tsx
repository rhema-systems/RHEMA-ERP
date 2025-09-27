'use client';

import { useMemo } from 'react';
import { Check, X } from 'lucide-react';

interface SecuritySettings {
  passwordMinLength: number;
  passwordRequireUppercase: boolean;
  passwordRequireLowercase: boolean;
  passwordRequireDigits: boolean;
  passwordRequireSpecialChars: boolean;
}

interface PasswordMeterProps {
  password: string;
  securitySettings?: SecuritySettings;
}

interface PasswordRule {
  name: string;
  test: (password: string, settings: SecuritySettings) => boolean;
  message: string;
}

export default function PasswordMeter({ password, securitySettings }: PasswordMeterProps) {
  const rules: PasswordRule[] = useMemo(() => [
    {
      name: 'length',
      test: (pwd, settings) => pwd.length >= settings.passwordMinLength,
      message: `At least ${securitySettings?.passwordMinLength || 8} characters`,
    },
    {
      name: 'uppercase',
      test: (pwd, settings) => !settings.passwordRequireUppercase || /[A-Z]/.test(pwd),
      message: 'One uppercase letter',
    },
    {
      name: 'lowercase',
      test: (pwd, settings) => !settings.passwordRequireLowercase || /[a-z]/.test(pwd),
      message: 'One lowercase letter',
    },
    {
      name: 'digits',
      test: (pwd, settings) => !settings.passwordRequireDigits || /[0-9]/.test(pwd),
      message: 'One number',
    },
    {
      name: 'special',
      test: (pwd, settings) => !settings.passwordRequireSpecialChars || /[^A-Za-z0-9]/.test(pwd),
      message: 'One special character',
    },
  ], [securitySettings?.passwordMinLength]);

  const passwordStrength = useMemo(() => {
    if (!securitySettings) return { score: 0, passed: [], failed: [] };

    const passed: string[] = [];
    const failed: string[] = [];

    rules.forEach(rule => {
      if (rule.test(password || '', securitySettings)) {
        passed.push(rule.name);
      } else {
        failed.push(rule.name);
      }
    });

    const score = Math.round((passed.length / rules.length) * 100);
    return { score, passed, failed };
  }, [password, securitySettings, rules]);

  if (!securitySettings) {
    return null;
  }

  const getStrengthColor = (score: number) => {
    if (score < 40) return 'bg-red-500';
    if (score < 70) return 'bg-yellow-500';
    return 'bg-green-500';
  };

  const getStrengthText = (score: number) => {
    if (score < 40) return 'Weak';
    if (score < 70) return 'Fair';
    return 'Strong';
  };

  return (
    <div className="space-y-3">
      {/* Password Strength Bar */}
      <div className="space-y-1">
        <div className="flex justify-between text-xs text-slate-600 dark:text-slate-400">
          <span>Password Strength</span>
          <span>{getStrengthText(passwordStrength.score)}</span>
        </div>
        <div className="h-2 bg-slate-200 dark:bg-slate-700 rounded-full overflow-hidden">
          <div
            className={`h-full transition-all duration-300 ${getStrengthColor(passwordStrength.score)}`}
            style={{ width: `${passwordStrength.score}%` }}
          />
        </div>
      </div>

      {/* Password Requirements */}
      <div className="space-y-1">
        {rules.map(rule => {
          const isRequired = rule.name === 'uppercase' ? securitySettings.passwordRequireUppercase
                           : rule.name === 'lowercase' ? securitySettings.passwordRequireLowercase
                           : rule.name === 'digits' ? securitySettings.passwordRequireDigits
                           : rule.name === 'special' ? securitySettings.passwordRequireSpecialChars
                           : true;

          if (!isRequired && rule.name !== 'length') return null;

          const isPassed = passwordStrength.passed.includes(rule.name);
          
          return (
            <div
              key={rule.name}
              className={`flex items-center space-x-2 text-xs ${
                isPassed
                  ? 'text-green-600 dark:text-green-400'
                  : 'text-slate-500 dark:text-slate-400'
              }`}
            >
              {isPassed ? (
                <Check className="h-3 w-3" />
              ) : (
                <X className="h-3 w-3" />
              )}
              <span>{rule.message}</span>
            </div>
          );
        })}
      </div>
    </div>
  );
}