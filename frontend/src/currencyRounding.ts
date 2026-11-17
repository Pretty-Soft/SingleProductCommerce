export function roundCurrency(amount: number, currencyCode: string): number {
  const places = new Intl.NumberFormat("en", { style: "currency", currency: currencyCode }).resolvedOptions().maximumFractionDigits ?? 2;
  const factor = 10 ** places;
  return Math.sign(amount) * Math.round((Math.abs(amount) + Number.EPSILON * Math.abs(amount)) * factor) / factor;
}
