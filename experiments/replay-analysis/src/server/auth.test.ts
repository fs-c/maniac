import { describe, expect, it } from "vitest";
import { safeReturnTo } from "./auth.ts";

describe("safeReturnTo", () => {
  it("allows local vite origins", () => {
    expect(safeReturnTo("http://127.0.0.1:5173/")).toBe("http://127.0.0.1:5173/");
    expect(safeReturnTo("http://localhost:5173/foo")).toBe("http://localhost:5173/");
  });

  it("rejects remote hosts", () => {
    expect(safeReturnTo("https://example.com/")).toBe("http://127.0.0.1:5173/");
  });
});
