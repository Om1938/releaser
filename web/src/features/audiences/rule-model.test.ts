import { describe, expect, it } from "vitest";
import type { Schemas } from "@/api/client";
import { describeRule, emptyRule, ruleDraftSchema, toApiRule, toDraft } from "./rule-model";

describe("audience rule model", () => {
  it("splits comma and newline separated ids, removing blanks and duplicates", () => {
    expect(toApiRule({ ...emptyRule("customer"), values: "customer-a, customer-b\n\ncustomer-a" })).toEqual({
      kind: "customer",
      customerIds: ["customer-a", "customer-b"],
    });
  });

  it("round-trips every rule kind through the editor draft", () => {
    const rules: Schemas["AudienceRule"][] = [
      { kind: "everyone" },
      { kind: "group", groups: ["internal-qa"] },
      { kind: "attribute", name: "plan", values: ["enterprise"] },
      { kind: "platform", platforms: ["MacOS"] },
      { kind: "currentVersion", minimum: "1.0.0", maximumExclusive: null },
    ];
    for (const rule of rules) {
      expect(toApiRule(toDraft(rule))).toEqual(rule);
    }
  });

  it("requires values for identity rules and a valid version range", () => {
    expect(ruleDraftSchema.safeParse(emptyRule("group")).success).toBe(false);
    expect(ruleDraftSchema.safeParse({ ...emptyRule("currentVersion"), minimum: "v1" }).success).toBe(false);
    expect(ruleDraftSchema.safeParse({ ...emptyRule("currentVersion"), minimum: "1.0.0" }).success).toBe(true);
  });

  it("summarises long lists", () => {
    expect(describeRule({ kind: "customer", customerIds: ["a", "b", "c", "d", "e"] })).toBe("Customers / tenants: a, b, c +2");
  });
});
