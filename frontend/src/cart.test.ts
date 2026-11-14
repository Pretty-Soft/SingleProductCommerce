import { describe, expect, it } from "vitest";
import { addCartSelection, changeCartSelectionQuantity, removeCartSelection } from "./cart";

describe("cart selections", () => {
  const quote = (currencyCode = "BDT", token = "signed-price", unitPrice = 10) => ({ token, currencyCode, unitPrice });

  it("merges quantities for the same variant and selected attributes regardless of option order", () => {
    const first = addCartSelection([], "variant-1", ["option-b", "option-a"], 2, quote("BDT", "quote-first", 20)).items;
    const merged = addCartSelection(first, "variant-1", ["option-a", "option-b"], 3, quote("BDT", "quote-new", 99)).items;

    expect(merged).toEqual([{
      key: "variant-1:BDT:option-a:option-b",
      variantId: "variant-1",
      optionIds: ["option-a", "option-b"],
      quantity: 5,
      currencyCode: "BDT",
      unitPrice: 20,
      quoteToken: "quote-first"
    }]);
  });

  it("keeps different product configurations as separate lines", () => {
    const withVariant = addCartSelection([], "variant-1", ["option-a"], 1, quote()).items;
    const withOtherVariant = addCartSelection(withVariant, "variant-2", ["option-a"], 1, quote()).items;
    const withOtherOption = addCartSelection(withOtherVariant, "variant-1", ["option-b"], 1, quote()).items;

    expect(withOtherOption).toHaveLength(3);
  });

  it("keeps each cart in the currency captured when its first selection is added", () => {
    const items = addCartSelection([], "variant-1", ["option-a"], 1, quote()).items;
    const afterCurrencyChange = addCartSelection(items, "variant-1", ["option-a"], 1, quote("USD"));

    expect(afterCurrencyChange.items).toEqual(items);
    expect(afterCurrencyChange.error).toContain("Your cart uses BDT");
  });

  it("removes a line when its quantity is reduced to zero", () => {
    const items = addCartSelection([], "variant-1", [], 1, quote()).items;

    expect(changeCartSelectionQuantity(items, "variant-1:BDT", -1)).toEqual([]);
  });

  it("removes the requested configuration without changing other lines", () => {
    const items = addCartSelection([], "variant-1", ["option-a"], 2, quote()).items;
    const otherLine = addCartSelection(items, "variant-1", ["option-b"], 1, quote()).items;

    expect(removeCartSelection(otherLine, "variant-1:BDT:option-a")).toEqual([otherLine[1]]);
  });
});
