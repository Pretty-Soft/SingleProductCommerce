import { describe, expect, it } from "vitest";
import { addCartSelection, changeCartSelectionQuantity, removeCartSelection } from "./cart";

describe("cart selections", () => {
  it("merges quantities for the same variant and selected attributes regardless of option order", () => {
    const first = addCartSelection([], "variant-1", ["option-b", "option-a"], 2);
    const merged = addCartSelection(first, "variant-1", ["option-a", "option-b"], 3);

    expect(merged).toEqual([{
      key: "variant-1:option-a:option-b",
      variantId: "variant-1",
      optionIds: ["option-a", "option-b"],
      quantity: 5
    }]);
  });

  it("keeps different product configurations as separate lines", () => {
    const withVariant = addCartSelection([], "variant-1", ["option-a"], 1);
    const withOtherVariant = addCartSelection(withVariant, "variant-2", ["option-a"], 1);
    const withOtherOption = addCartSelection(withOtherVariant, "variant-1", ["option-b"], 1);

    expect(withOtherOption).toHaveLength(3);
  });

  it("removes a line when its quantity is reduced to zero", () => {
    const items = addCartSelection([], "variant-1", [], 1);

    expect(changeCartSelectionQuantity(items, "variant-1", -1)).toEqual([]);
  });

  it("removes the requested configuration without changing other lines", () => {
    const items = addCartSelection([], "variant-1", ["option-a"], 2);
    const otherLine = addCartSelection(items, "variant-1", ["option-b"], 1);

    expect(removeCartSelection(otherLine, "variant-1:option-a")).toEqual([otherLine[1]]);
  });
});
