import type { Schemas } from "@/api/client";
import { Badge } from "@/components/ui/badge";

const releaseVariants = {
  Available: "default",
  Deprecated: "secondary",
  Withdrawn: "destructive",
} as const satisfies Record<Schemas["ReleaseState"], "default" | "secondary" | "destructive" | "outline">;

const deploymentVariants = {
  Draft: "outline",
  Active: "default",
  Paused: "secondary",
  Completed: "outline",
  Cancelled: "destructive",
} as const satisfies Record<Schemas["DeploymentState"], "default" | "secondary" | "destructive" | "outline">;

export function ReleaseStateBadge({ state }: { state: Schemas["ReleaseState"] }) {
  return <Badge variant={releaseVariants[state]}>{state}</Badge>;
}

export function DeploymentStateBadge({ state }: { state: Schemas["DeploymentState"] }) {
  return <Badge variant={deploymentVariants[state]}>{state}</Badge>;
}
