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

  it("provides about and WhatsApp footer labels in each language", () => {
    for (const language of storeLanguages) {
      const labels = storeText(language.code);
      expect(labels.aboutUs.length).toBeGreaterThan(0);
      expect(labels.whatsappMessage.length).toBeGreaterThan(0);
    }
  });

  it("provides landing page and review labels in each language", () => {
    for (const language of storeLanguages) {
      const labels = storeText(language.code);
      expect(labels.whyChoose.length).toBeGreaterThan(0);
      expect(labels.productDetails.length).toBeGreaterThan(0);
      expect(labels.howToUse.length).toBeGreaterThan(0);
      expect(labels.reviews.length).toBeGreaterThan(0);
      expect(labels.authentic.length).toBeGreaterThan(0);
      expect(labels.fastDelivery.length).toBeGreaterThan(0);
      expect(labels.easyReturn.length).toBeGreaterThan(0);
      expect(labels.originalProduct.length).toBeGreaterThan(0);
      expect(labels.trustedByThousands.length).toBeGreaterThan(0);
      expect(labels.customerSupport.length).toBeGreaterThan(0);
      expect(labels.productSections.length).toBeGreaterThan(0);
      expect(labels.keyBenefits.length).toBeGreaterThan(0);
      expect(labels.shippingReturns.length).toBeGreaterThan(0);
    }
  });
});
