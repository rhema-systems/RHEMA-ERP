import { redirect } from 'next/navigation';

export default function LegacyTaxRatesPage() {
    redirect('/finance/tax/configuration/taxes');
}
