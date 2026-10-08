import { describe, expect, it } from "vitest";
import { manifestUrlIn, missingPlatforms, shippedPlatforms, siblingManifestUrl } from "./platforms";

describe("manifestUrlIn", () => {
  it("joins the release folder with the platform's standard file name", () => {
    expect(manifestUrlIn("https://cdn.example.com/app/1.2.0/", "MacOS")).toBe("https://cdn.example.com/app/1.2.0/latest-mac.yml");
    expect(manifestUrlIn("https://cdn.example.com/app/1.2.0", "LinuxArm64")).toBe("https://cdn.example.com/app/1.2.0/latest-linux-arm64.yml");
  });
});

describe("siblingManifestUrl", () => {
  it("replaces only the file name of the path", () => {
    expect(siblingManifestUrl("https://cdn.example.com/app/1.0/latest.yml", "MacOS")).toBe("https://cdn.example.com/app/1.0/latest-mac.yml");
  });

  it("keeps a query string, even one containing slashes, and drops the fragment", () => {
    expect(siblingManifestUrl("https://cdn.example.com/app/1.0/latest.yml?token=a/b#x", "MacOS")).toBe(
      "https://cdn.example.com/app/1.0/latest-mac.yml?token=a/b",
    );
  });
});

describe("missingPlatforms", () => {
  it("lists platforms the release does not have, in standard order", () => {
    expect(missingPlatforms({ platforms: ["MacOS"] })).toEqual(["Windows", "LinuxX64", "LinuxArm64", "LinuxArmv7l"]);
  });
});

describe("shippedPlatforms", () => {
  it("unions platforms of non-withdrawn releases so a single-platform hotfix does not narrow the default", () => {
    expect(
      shippedPlatforms([
        { platforms: ["Windows"], state: "Available" },
        { platforms: ["Windows", "MacOS", "LinuxX64"], state: "Deprecated" },
        { platforms: ["LinuxArm64"], state: "Withdrawn" },
      ]),
    ).toEqual(["Windows", "MacOS", "LinuxX64"]);
  });

  it("is empty for an application without releases", () => {
    expect(shippedPlatforms([])).toEqual([]);
  });
});
