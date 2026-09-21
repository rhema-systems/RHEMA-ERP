import { describe, expect, it } from 'vitest';
import { quantitySurveyConfigurationError } from './quantity-survey-configuration-error';

describe('QS configuration validation feedback', () => {
  it('preserves field errors and the server code behind a generic detail', () => {
    expect(quantitySurveyConfigurationError({ response: {
      detail: 'Quantity-survey configuration validation failed.', code: 'QS_CONFIGURATION_400',
      errors: { 'QS-DEC-008': ['Valuation template is unpublished.', 'Payment term is inactive.'] },
    } })).toBe('Quantity-survey configuration validation failed. Valuation template is unpublished. Payment term is inactive. (QS_CONFIGURATION_400)');
  });
  it('handles string-array errors without exposing unknown objects', () => {
    expect(quantitySurveyConfigurationError({ message: 'Invalid selection', response: {
      errors: ['Choose a published version.', null, { internal: 'hidden' }, 'Choose a published version.'],
    } })).toBe('Invalid selection Choose a published version.');
  });
  it('retains ordinary failure messages and handles absent errors', () => {
    expect(quantitySurveyConfigurationError(new Error('Network unavailable'))).toBe('Network unavailable');
    expect(quantitySurveyConfigurationError(null)).toBe('The request failed.');
  });
});
