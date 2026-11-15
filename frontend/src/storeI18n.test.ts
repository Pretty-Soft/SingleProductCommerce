import { describe, expect, it } from "vitest";
import { storeLanguages, storeText } from "./storeI18n";

describe("storefront language support", () => {
  it("exposes English, Bangla, and Spanish", () => {
    expect(storeLanguages.map(language => language.code)).toEqual(["en", "bn", "es"]);
  });

  it("provides checkout and cart labels in each language", () => {
    for (const language of storeLanguages) {
      const labels = storeText(language.code);
      expect(labels.customerName.length).toBeGreaterThan(0);
      expect(labels.placeOrder.length).toBeGreaterThan(0);
      expect(labels.emptyCart.length).toBeGreaterThan(0);
    }
  });
});
