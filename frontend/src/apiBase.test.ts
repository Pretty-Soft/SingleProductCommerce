import { describe, expect, it } from "vitest";
import { resolveApiBase } from "./apiBase";

describe("public API origin configuration", () => {
  it("uses a deployment URL over the packaged build URL", () => {
    expect(resolveApiBase("https://shop.example.test/", "https://api.example.test"))
      .toBe("https://shop.example.test");
  });

  it("rejects HTTP and credential-bearing URLs", () => {
    expect(() => resolveApiBase("http://shop.example.test", undefined)).toThrow(/HTTPS/);
    expect(() => resolveApiBase("https://user:secret@shop.example.test", undefined)).toThrow(/HTTPS/);
  });
});
