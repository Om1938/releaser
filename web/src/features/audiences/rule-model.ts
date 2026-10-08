import { z } from "zod";
import type { Schemas } from "@/api/client";
import { semverSchema, splitList } from "@/features/shared/schemas";

export const ruleKinds = ["everyone", "customer", "group", "user", "installation", "attribute", "platform", "currentVersion"] as const;
export type RuleKind = (typeof ruleKinds)[number];

export const ruleKindLabels: Record<RuleKind, string> = {
  everyone: "Everyone",
  customer: "Customers / tenants",
  group: "Groups",
  user: "Users",
  installation: "Installations",
  attribute: "Custom attribute",
  platform: "Platform",
  currentVersion: "Current version range",
};

/** What each kind needs; customer/user/group/attribute only match identity from verified context tokens. */
export const ruleKindHints: Record<RuleKind, string> = {
  everyone: "Matches every installation. Use for the default audience.",
  customer: "Customer (tenant) ids from verified context tokens (tid claim).",
  group: "Group keys from verified context tokens (grp claim).",
  user: "User ids from verified context tokens (sub claim).",
  installation: "Installation ids exactly as sent in the feed URL.",
  attribute: "A verified custom attribute (attrs claim) with allowed values.",
  platform: "Installations requesting the selected platform manifests.",
  currentVersion: "Installations currently running a version in [minimum, maximum).",
};

const platformValues = ["Windows", "MacOS", "LinuxX64", "LinuxArm64", "LinuxArmv7l"] as const;

export const ruleDraftSchema = z
  .object({
    kind: z.enum(ruleKinds),
    values: z.string(),
    name: z.string(),
    minimum: z.string(),
    maximumExclusive: z.string(),
    platforms: z.array(z.enum(platformValues)),
  })
  .superRefine((rule, ctx) => {
    const needsValues = rule.kind === "customer" || rule.kind === "group" || rule.kind === "user" || rule.kind === "installation" || rule.kind === "attribute";
    if (needsValues && splitList(rule.values).length === 0) {
      ctx.addIssue({ code: "custom", path: ["values"], message: "Enter at least one value." });
    }
    if (rule.kind === "attribute" && !rule.name.trim()) {
      ctx.addIssue({ code: "custom", path: ["name"], message: "Enter the attribute name." });
    }
    if (rule.kind === "platform" && rule.platforms.length === 0) {
      ctx.addIssue({ code: "custom", path: ["platforms"], message: "Select at least one platform." });
    }
    if (rule.kind === "currentVersion") {
      if (!rule.minimum && !rule.maximumExclusive) {
        ctx.addIssue({ code: "custom", path: ["minimum"], message: "Enter a minimum, a maximum, or both." });
      }
      for (const key of ["minimum", "maximumExclusive"] as const) {
        if (rule[key] && !semverSchema.safeParse(rule[key]).success) {
          ctx.addIssue({ code: "custom", path: [key], message: "Use a semantic version." });
        }
      }
    }
  });

export type RuleDraft = z.infer<typeof ruleDraftSchema>;

export const emptyRule = (kind: RuleKind = "customer"): RuleDraft => ({ kind, values: "", name: "", minimum: "", maximumExclusive: "", platforms: [] });

export function toApiRule(draft: RuleDraft): Schemas["AudienceRule"] {
  const values = splitList(draft.values);
  switch (draft.kind) {
    case "everyone":
      return { kind: "everyone" };
    case "customer":
      return { kind: "customer", customerIds: values };
    case "group":
      return { kind: "group", groups: values };
    case "user":
      return { kind: "user", userIds: values };
    case "installation":
      return { kind: "installation", installationIds: values };
    case "attribute":
      return { kind: "attribute", name: draft.name.trim(), values };
    case "platform":
      return { kind: "platform", platforms: draft.platforms };
    case "currentVersion":
      return { kind: "currentVersion", minimum: draft.minimum || null, maximumExclusive: draft.maximumExclusive || null };
  }
}

export function toDraft(rule: Schemas["AudienceRule"]): RuleDraft {
  const draft = emptyRule((rule.kind ?? "everyone") as RuleKind);
  if ("customerIds" in rule) return { ...draft, values: rule.customerIds.join("\n") };
  if ("groups" in rule) return { ...draft, values: rule.groups.join("\n") };
  if ("userIds" in rule) return { ...draft, values: rule.userIds.join("\n") };
  if ("installationIds" in rule) return { ...draft, values: rule.installationIds.join("\n") };
  if ("name" in rule) return { ...draft, name: rule.name, values: rule.values.join("\n") };
  if ("platforms" in rule) return { ...draft, platforms: rule.platforms };
  if ("minimum" in rule) return { ...draft, minimum: rule.minimum ?? "", maximumExclusive: rule.maximumExclusive ?? "" };
  return draft;
}

/** One-line human summary of a rule for tables. */
export function describeRule(rule: Schemas["AudienceRule"]): string {
  const draft = toDraft(rule);
  const values = splitList(draft.values);
  const shown = values.length > 3 ? `${values.slice(0, 3).join(", ")} +${values.length - 3}` : values.join(", ");
  switch (draft.kind) {
    case "everyone":
      return "Everyone";
    case "attribute":
      return `${draft.name} ∈ {${shown}}`;
    case "platform":
      return `Platform: ${draft.platforms.join(", ")}`;
    case "currentVersion":
      return `Version ${draft.minimum ? `≥ ${draft.minimum}` : ""}${draft.minimum && draft.maximumExclusive ? " and " : ""}${draft.maximumExclusive ? `< ${draft.maximumExclusive}` : ""}`;
    default:
      return `${ruleKindLabels[draft.kind]}: ${shown}`;
  }
}
