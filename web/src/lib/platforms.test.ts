import { describe, expect, it } from "vitest";
import { inStandardOrder, manifestUrlIn, missingPlatforms, siblingManifestUrl } from "./platforms";

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
  it("lists supported platforms the release does not have, in standard order", () => {
    expect(missingPlatforms({ platforms: ["MacOS"] }, ["MacOS", "LinuxX64", "Windows"])).toEqual(["Windows", "LinuxX64"]);
  });

  it("never suggests a platform the application does not support", () => {
    expect(missingPlatforms({ platforms: ["Windows"] }, ["Windows", "MacOS"])).toEqual(["MacOS"]);
  });
});

describe("inStandardOrder", () => {
  it("orders platforms canonically", () => {
    expect(inStandardOrder(["LinuxArm64", "Windows", "MacOS"])).toEqual(["Windows", "MacOS", "LinuxArm64"]);
  });
});
