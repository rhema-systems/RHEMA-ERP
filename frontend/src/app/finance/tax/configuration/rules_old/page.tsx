import { redirect } from 'next/navigation';

export default function LegacyTaxRulesPage() {
    redirect('/finance/tax/configuration/rules');
}
