# Contributing

Thanks for helping. Everyone taking part is expected to follow the [Code of Conduct](CODE_OF_CONDUCT.md). Releaser deliberately stays small, so please read the [PRD scope](PRD.md#8-explicit-non-goals) and the [ADRs](docs/decisions/README.md) before proposing larger features.

1. Open an issue (bug report or feature request form) first for anything beyond a small fix, especially new targeting rules, lifecycle states or updater adapters.
2. Follow [docs/development.md](docs/development.md) to run the stack and tests.
3. Write the failing test first for domain behaviour. Keep slices self-contained.
4. Keep the build clean: `dotnet build` (warnings are errors), `pnpm lint`, `pnpm typecheck`.
5. Update docs, and add an ADR if you make a non-obvious decision. Regenerate the OpenAPI document and TS types when endpoints change.
6. Use conventional, descriptive commit messages. One logical change per PR, and fill in the pull request template.

By contributing you agree that your contributions are licensed under the Apache License 2.0.
