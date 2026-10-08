import type { Schemas } from "@/api/client";

type Role = "Admin" | "ReleaseManager" | "Viewer";

function hasRole(user: Schemas["CurrentUserResponse"] | undefined, ...roles: Role[]): boolean {
  return !!user && user.roles.some((role) => (roles as string[]).includes(role));
}

export const canManageReleases = (user: Schemas["CurrentUserResponse"] | undefined) => hasRole(user, "Admin", "ReleaseManager");
export const canManageUsers = (user: Schemas["CurrentUserResponse"] | undefined) => hasRole(user, "Admin");
