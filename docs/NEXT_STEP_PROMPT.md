# Next step — copy-paste prompt for a new chat

Last finished (2026-11-24): Stage 3 (WPF window, Steps 3.1–3.7). PR for `feature/stage-3-wpf-ui` is open; once it is
merged, start Stage 4 from an updated `main`.

**Model and effort for the next chat:** Sonnet 5.5, effort **medium** for 4.1; the large-file runs (4.2) are mostly
waiting. Opus 5.5 is not needed.

```text
Create branch feature/stage-4-testing from updated main (git switch main; git pull). Follow CLAUDE.md.
Step 4.1: integration tests on the real file system (PROJECT_PLAN §9.2; grep "^### 9.2" and read only that range) in
tests/SecureFile.Integration.Tests: only cases not already covered by the Core and ViewModel tests (permissions,
overwrite, cancel leaves nothing, golden files). Then 4.2 (1 GB and >4 GB runs; manual or nightly, not per commit), 4.3
TESTPLAN.md manual script, 4.4 usability check. Per step: build + tests, one commit, mark the plan row, CHANGELOG
line, and REPLACE THIS FILE at the end of the step.
```

**Still open from Stage 3 (cheap, do whenever convenient)**
- Not verified by hand: FastPass Tab Stops, Narrator, dark mode, 150%/200% scaling, real drag-and-drop, Open folder click.
- UI tests run locally only: `$env:SECUREFILE_UI_TESTS = "1"; dotnet test --project tests/SecureFile.Wpf.UiTests -c Release`.

**Open items for the final hardening stage**
- Flaky test (cleanup race in `Dispose()`): `FileDecryptionTests.DecryptAsync_CancelledFromTheProgressReport_ThrowsAndLeavesNothing(length: 3145728)`;
  make the temp-folder cleanup tolerant, then run the suite repeatedly.
- Deferred review items: Unix errno values in `ErrorMessageProvider`, `IsSameFile` case rule on Linux, `FileErrorLog`
  tests, `DateTime` vs `Stopwatch` for the repeat-error window, About dialog width at large text scaling.
- GitGuardian flags the public RFC 7914 test vector in `KeyDerivationTests.cs` (false positive; mark it in its dashboard).
