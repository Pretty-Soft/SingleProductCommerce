import { describe, expect, it } from "vitest";
import { canAddSectionItem, sharesVariantEditScope } from "./AdminCatalogEditor";

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

describe("catalog child add actions", () => {
  it("allows add actions to start their section edit when none is active", () => {
    expect(canAddSectionItem("variants", null, 1, 100)).toBe(true);
    expect(canAddSectionItem("attributes", null, 0, 30)).toBe(true);
  });

  it("keeps section limits and concurrent edits enforced", () => {
    expect(canAddSectionItem("variants", "basics", 1, 100)).toBe(false);
    expect(canAddSectionItem("attributes", "inventory", 0, 30)).toBe(false);
    expect(canAddSectionItem("variants", null, 100, 100)).toBe(false);
    expect(canAddSectionItem("attributes", null, 0, 30, true)).toBe(false);
  });
});
