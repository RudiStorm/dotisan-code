# Contributing to Dotisan

1. Create a focused branch from `master`.
2. Make the smallest change that solves the issue.
3. Add or update tests for behavior changes.
4. Run `dotnet test Dotisan.sln -c Release --no-restore` and the relevant generated-project checks.
5. Open a pull request describing the user-visible impact and verification performed.

Generated applications are consumer-facing artifacts. Changes to templates must include a generated-project smoke test.
