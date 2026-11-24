# SecureFile — working rules for AI sessions (read this, not the whole plan)

AES-256-CBC + HMAC file encryption desktop app. .NET 10, WPF (required), Avalonia (bonus later).
Layers: `SecureFile.Wpf → SecureFile.Presentation (ViewModels, CommunityToolkit.Mvvm) → SecureFile.Core`.

## Token rules
- Do NOT read `docs/PROJECT_PLAN.md` (1,200 lines) or `docs/LEARNING_LOG.md` (1,700 lines) in full. `grep -n` the
  step row or section you need and read only that range.
- The task prompt names the files to touch; read only those (and their tests). No exploratory sweeps.
- Terse chat updates. No restating of the plan.

## Process (PROJECT_PLAN §1.7 Cadence)
- Core: full tests. ViewModels: a few tests each (main path, state, one error). WPF: no unit tests; one end-to-end
  integration test + a manual smoke checklist.
- Per step: `dotnet build SecureFile.slnx -c Release` + the affected test project. Run `dotnet format SecureFile.slnx
  whitespace` after creating files (tools write LF; repo wants CRLF).
- `scripts/verify.ps1` runs ONCE, via the pre-push hook on `git push` — do not run it manually per commit.
- One commit per step, Conventional Commits, **no AI attribution** (no Co-Authored-By, no "Generated with").
- One reviewer agent per stage/slice, only if the prompt asks for it.

## Code rules (build-enforced: warnings are errors)
- XML docs on all public AND private members; `<remarks>` explain why. Complexity ≤ 10 (CA1502), Maintainability
  Index ≥ 60 per method (CA1505) — split long methods early.
- Presentation awaits use `ConfigureAwait(true)` (UI thread); Core uses `ConfigureAwait(false)`.
- ADR-13: Core throws documented exceptions; ViewModel catches only specific expected ones (never `Exception`);
  WPF has global safety nets + local log. No empty catch, no `throw ex;`, no `async void` outside WPF handlers.
- xUnit v3: no lone surrogates in `[InlineData]` (serialized as UTF-8 → U+FFFD); put them in the test body.
- Edit files with the Edit/Write tools, not Python/heredoc scripts (quoting breaks, files get truncated).
