import { describe, expect, it } from "vitest";
import { addCartSelection, changeCartSelectionQuantity, removeCartSelection } from "./cart";

describe("cart selections", () => {
  it("merges quantities for the same variant and selected attributes regardless of option order", () => {
    const first = addCartSelection([], "variant-1", ["option-b", "option-a"], 2, "BDT").items;
    const merged = addCartSelection(first, "variant-1", ["option-a", "option-b"], 3, "BDT").items;

    expect(merged).toEqual([{
      key: "variant-1:BDT:option-a:option-b",
      variantId: "variant-1",
      optionIds: ["option-a", "option-b"],
      quantity: 5,
      currencyCode: "BDT"
    }]);
  });

  it("keeps different product configurations as separate lines", () => {
    const withVariant = addCartSelection([], "variant-1", ["option-a"], 1, "BDT").items;
    const withOtherVariant = addCartSelection(withVariant, "variant-2", ["option-a"], 1, "BDT").items;
    const withOtherOption = addCartSelection(withOtherVariant, "variant-1", ["option-b"], 1, "BDT").items;

    expect(withOtherOption).toHaveLength(3);
  });

  it("keeps each cart in the currency captured when its first selection is added", () => {
    const items = addCartSelection([], "variant-1", ["option-a"], 1, "BDT").items;
    const afterCurrencyChange = addCartSelection(items, "variant-1", ["option-a"], 1, "USD");

    expect(afterCurrencyChange.items).toEqual(items);
    expect(afterCurrencyChange.error).toContain("Your cart uses BDT");
  });

  it("removes a line when its quantity is reduced to zero", () => {
    const items = addCartSelection([], "variant-1", [], 1, "BDT").items;

    expect(changeCartSelectionQuantity(items, "variant-1:BDT", -1)).toEqual([]);
  });

  it("removes the requested configuration without changing other lines", () => {
    const items = addCartSelection([], "variant-1", ["option-a"], 2, "BDT").items;
    const otherLine = addCartSelection(items, "variant-1", ["option-b"], 1, "BDT").items;

    expect(removeCartSelection(otherLine, "variant-1:BDT:option-a")).toEqual([otherLine[1]]);
  });
});
