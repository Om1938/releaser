# ADR 0012 — Dashboard stack and dependency policy

**Status:** Accepted

- shadcn/ui with the **Radix** base ("radix-nova" style), using components added through the shadcn CLI and MCP registry. Forms use shadcn's `Field` components with React Hook Form `Controller` and Zod. This was the pattern recommended by the registry's `form-rhf-demo` at the time of writing.
- TypeScript is pinned to **5.9.x**, not 7.x, because `openapi-typescript` (`^5`) and `typescript-eslint` (`<6.1`) don't support newer compilers yet.
- The API client is `openapi-fetch` over types generated from the server's build-time OpenAPI document. Number handling is strict on the server (`JsonNumberHandling.Strict`) so numeric fields generate as `number` rather than `number | string`.
- pnpm `minimumReleaseAge: 1440` (one day) is enforced locally and in the container build as a supply-chain guard.
- The light-mode `--destructive` token is darkened (0.577 → 0.505 L) so destructive text on its 10% tint meets WCAG AA. axe checks in Playwright enforce this.
