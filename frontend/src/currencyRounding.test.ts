import { expect, test } from "vitest";
import { roundCurrency } from "./currencyRounding";

test("rounds a final unit price before multiplying quantity", () => {
  expect(roundCurrency(1.005, "BDT")).toBe(1.01);
  expect(roundCurrency(169.9915, "BDT")).toBe(169.99);
  expect(roundCurrency(125.5, "JPY")).toBe(126);
  expect(roundCurrency(1.2345, "KWD")).toBe(1.235);
  expect(roundCurrency(1.005, "BDT") * 2).toBe(2.02);
});
