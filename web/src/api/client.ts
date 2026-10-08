import createClient, { type Middleware } from "openapi-fetch";
import type { components, paths } from "./schema";

export type Schemas = components["schemas"];

/** Error raised for any non-2xx admin API response, carrying RFC 9457 problem details when available. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
    readonly code?: string,
    readonly fieldErrors: Record<string, string[]> = {},
  ) {
    super(message);
    this.name = "ApiError";
  }

  get isUnauthorized(): boolean {
    return this.status === 401;
  }
}

interface ProblemBody {
  title?: string;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
}

const csrf: Middleware = {
  onRequest({ request }) {
    request.headers.set("X-Releaser-Csrf", "1");
    return request;
  },
};

export const api = createClient<paths>({ baseUrl: "", credentials: "same-origin" });
api.use(csrf);

/** Unwraps an openapi-fetch result, turning problem responses into {@link ApiError}. */
export async function unwrap<T>(call: Promise<{ data?: T; error?: unknown; response: Response }>): Promise<T> {
  const { data, error, response } = await call;
  if (response.ok) {
    return data as T;
  }
  const problem = (error ?? {}) as ProblemBody;
  const fieldErrors = problem.errors ?? {};
  const firstFieldError = Object.values(fieldErrors)[0]?.[0];
  throw new ApiError(response.status, problem.detail ?? firstFieldError ?? problem.title ?? response.statusText, problem.code, fieldErrors);
}

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    return error.message;
  }
  return error instanceof Error ? error.message : "Something went wrong.";
}
