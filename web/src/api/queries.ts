import { queryOptions } from "@tanstack/react-query";
import { api, unwrap } from "./client";

/** Query keys and fetchers shared by routes (loaders) and components. */
export const queries = {
  me: () =>
    queryOptions({
      queryKey: ["me"],
      queryFn: () => unwrap(api.GET("/api/admin/v1/auth/me")),
      retry: false,
      staleTime: 60_000,
    }),
  system: () => queryOptions({ queryKey: ["system"], queryFn: () => unwrap(api.GET("/api/admin/v1/system")), staleTime: 300_000 }),
  applications: () => queryOptions({ queryKey: ["applications"], queryFn: () => unwrap(api.GET("/api/admin/v1/applications")) }),
  application: (appId: string) =>
    queryOptions({
      queryKey: ["applications", appId],
      queryFn: () => unwrap(api.GET("/api/admin/v1/applications/{appId}", { params: { path: { appId } } })),
    }),
  channels: (appId: string) =>
    queryOptions({
      queryKey: ["applications", appId, "channels"],
      queryFn: () => unwrap(api.GET("/api/admin/v1/applications/{appId}/channels", { params: { path: { appId } } })),
    }),
  releases: (appId: string) =>
    queryOptions({
      queryKey: ["applications", appId, "releases"],
      queryFn: () => unwrap(api.GET("/api/admin/v1/applications/{appId}/releases", { params: { path: { appId } } })),
    }),
  release: (appId: string, releaseId: string) =>
    queryOptions({
      queryKey: ["applications", appId, "releases", releaseId],
      queryFn: () => unwrap(api.GET("/api/admin/v1/applications/{appId}/releases/{releaseId}", { params: { path: { appId, releaseId } } })),
    }),
  releaseNote: (appId: string, releaseId: string) =>
    queryOptions({
      queryKey: ["applications", appId, "releases", releaseId, "notes"],
      queryFn: async () => {
        const { data, response } = await api.GET("/api/admin/v1/applications/{appId}/releases/{releaseId}/notes", { params: { path: { appId, releaseId } } });
        return response.status === 404 ? null : (data ?? null);
      },
    }),
  releaseNoteRevisions: (appId: string, releaseId: string) =>
    queryOptions({
      queryKey: ["applications", appId, "releases", releaseId, "notes", "revisions"],
      queryFn: () =>
        unwrap(api.GET("/api/admin/v1/applications/{appId}/releases/{releaseId}/notes/revisions", { params: { path: { appId, releaseId } } })),
    }),
  audiences: (appId: string) =>
    queryOptions({
      queryKey: ["applications", appId, "audiences"],
      queryFn: () => unwrap(api.GET("/api/admin/v1/applications/{appId}/audiences", { params: { path: { appId } } })),
    }),
  deployments: (appId: string) =>
    queryOptions({
      queryKey: ["applications", appId, "deployments"],
      queryFn: () => unwrap(api.GET("/api/admin/v1/applications/{appId}/deployments", { params: { path: { appId } } })),
    }),
  policies: (appId: string) =>
    queryOptions({
      queryKey: ["applications", appId, "policies"],
      queryFn: () => unwrap(api.GET("/api/admin/v1/applications/{appId}/policies", { params: { path: { appId } } })),
    }),
  contextKeys: (appId: string) =>
    queryOptions({
      queryKey: ["applications", appId, "context-keys"],
      queryFn: () => unwrap(api.GET("/api/admin/v1/applications/{appId}/context-keys", { params: { path: { appId } } })),
    }),
  audit: (filter: { appId?: string; action?: string; before?: number }) =>
    queryOptions({
      queryKey: ["audit", filter],
      queryFn: () => unwrap(api.GET("/api/admin/v1/audit", { params: { query: { ...filter, limit: 50 } } })),
    }),
  users: () => queryOptions({ queryKey: ["users"], queryFn: () => unwrap(api.GET("/api/admin/v1/users")) }),
};

/** Everything under one application; invalidate after any mutation that may change it. */
export const applicationScope = (appId: string) => ["applications", appId] as const;
