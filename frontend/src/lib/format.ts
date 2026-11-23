const priceFormat = new Intl.NumberFormat(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const dateFormat = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" });

export const formatPrice = (price: number) => priceFormat.format(price);
export const formatDate = (iso: string) => dateFormat.format(new Date(iso));

/** Returns a valid price or null. Accepts "12", "12.5", "12.50". */
export function parsePrice(value: string): number | null {
  const price = Number(value);
  if (!value.trim() || !Number.isFinite(price) || price < 0.01 || price > 1_000_000) return null;
  return Math.round(price * 100) / 100;
}
