import { describe, expect, it } from "vitest";
import { manifestUrlIn } from "./platforms";

describe("manifestUrlIn", () => {
  it("joins the release folder with the platform's standard file name", () => {
    expect(manifestUrlIn("https://cdn.example.com/app/1.2.0/", "MacOS")).toBe("https://cdn.example.com/app/1.2.0/latest-mac.yml");
    expect(manifestUrlIn("https://cdn.example.com/app/1.2.0", "LinuxArm64")).toBe("https://cdn.example.com/app/1.2.0/latest-linux-arm64.yml");
  });
});
