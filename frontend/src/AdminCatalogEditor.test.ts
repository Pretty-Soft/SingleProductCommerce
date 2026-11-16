import { describe, expect, it } from "vitest";
import { sharesVariantEditScope } from "./AdminCatalogEditor";

describe("catalog variant edit scope", () => {
  it("shares edits across variant details, prices, and inventory", () => {
    expect(sharesVariantEditScope("variants", "inventory")).toBe(true);
    expect(sharesVariantEditScope("pricing", "variants")).toBe(true);
    expect(sharesVariantEditScope("inventory", "pricing")).toBe(true);
  });

  it("keeps unrelated catalog sections in their own edit scope", () => {
    expect(sharesVariantEditScope("attributes", "inventory")).toBe(false);
    expect(sharesVariantEditScope("inventory", "attributes")).toBe(false);
    expect(sharesVariantEditScope("variants", null)).toBe(false);
  });
});
