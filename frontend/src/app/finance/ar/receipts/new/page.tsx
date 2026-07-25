// Keep one implementation of the receipt form so the canonical receipts route
// and the legacy payments route behave identically during the URL transition.
export { default } from '../../payments/new/page';
