export interface CartLine {
  key: string;
  variantId: string;
  optionIds: string[];
  quantity: number;
  currencyCode: string;
  unitPrice: number;
  quoteToken: string;
}

export interface CartQuote {
  token: string;
  currencyCode: string;
  unitPrice: number;
}

export function addCartSelection(
  items: CartLine[],
  variantId: string,
  selectedOptionIds: string[],
  quantity: number,
  quote: CartQuote
): { items: CartLine[]; error?: string } {
  if (items.some((item) => item.currencyCode !== quote.currencyCode)) {
    return {
      items,
      error: `Your cart uses ${items[0].currencyCode}. Clear it before adding items in ${quote.currencyCode}.`
    };
  }

  const optionIds = [...new Set(selectedOptionIds)].sort();
  const key = [variantId, quote.currencyCode, ...optionIds].join(":");
  const addQuantity = Math.max(1, Math.trunc(quantity));
  const existing = items.find((item) => item.key === key);
  return {
    items: existing
      ? items.map((item) => item.key === key ? { ...item, quantity: item.quantity + addQuantity } : item)
      : [...items, { key, variantId, optionIds, quantity: addQuantity, currencyCode: quote.currencyCode, unitPrice: quote.unitPrice, quoteToken: quote.token }]
  };
}

export function changeCartSelectionQuantity(items: CartLine[], key: string, change: number): CartLine[] {
  return items
    .map((item) => item.key === key ? { ...item, quantity: item.quantity + change } : item)
    .filter((item) => item.quantity > 0);
}

export function removeCartSelection(items: CartLine[], key: string): CartLine[] {
  return items.filter((item) => item.key !== key);
}
