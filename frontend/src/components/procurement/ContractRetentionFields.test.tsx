import React, { useState } from 'react';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { ContractRetentionFields, DEFAULT_CONTRACT_RETENTION_PERCENTAGE, validateContractRetention } from './ContractRetentionFields';

afterEach(cleanup);

describe('contract retention terms', () => {
  it('does not invent a mandatory retention rate', () => {
    expect(DEFAULT_CONTRACT_RETENTION_PERCENTAGE).toBe(0);
    expect(validateContractRetention(0)).toBeNull();
  });

  it.each([5, 10, 100])('requires a clause for %s percent retention', (rate) => {
    expect(validateContractRetention(rate, '  ')).toMatch(/clause/);
    expect(validateContractRetention(rate, 'Approved deduction and release terms')).toBeNull();
  });

  it.each([-1, 101, NaN, Infinity])('rejects invalid percentage %s', (rate) => {
    expect(validateContractRetention(rate, 'Terms')).toMatch(/between 0 and 100/);
  });

  it('limits the clause to the existing database capacity', () => {
    expect(validateContractRetention(5, 'x'.repeat(2001))).toMatch(/2000/);
  });

  it('handles an existing contract whose stored clause is null', () => {
    render(<ContractRetentionFields percentage={0} clause={null}
      onPercentageChange={() => undefined} onClauseChange={() => undefined} />);
    expect(screen.getByRole('spinbutton', { name: 'Retention %' })).toHaveValue(0);
    expect(screen.queryByRole('textbox', { name: /Retention clause/ })).toBeNull();
  });

  it('shows the clause only when relevant and retains entered conditions', () => {
    function Form() {
      const [percentage, setPercentage] = useState(0);
      const [clause, setClause] = useState('');
      return <ContractRetentionFields percentage={percentage} clause={clause}
        onPercentageChange={setPercentage} onClauseChange={setClause} />;
    }
    render(<Form />);
    expect(screen.queryByRole('textbox', { name: /Retention clause/ })).toBeNull();
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Retention %' }), { target: { value: '5' } });
    const field = screen.getByRole('textbox', { name: /Retention clause/ });
    expect(field).toBeRequired();
    fireEvent.change(field, { target: { value: 'Release after acceptance and approval.' } });
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Retention %' }), { target: { value: '0' } });
    expect(screen.getByRole('textbox', { name: /Retention clause/ })).toHaveValue('Release after acceptance and approval.');
    expect(screen.getByRole('textbox', { name: /Retention clause/ })).not.toBeRequired();
  });
});
