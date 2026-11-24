# Next step — copy-paste prompt for a new chat

This file is replaced at the end of every step. It holds the prompt for starting the **next** step in a fresh chat,
so a long conversation does not use up tokens.

Last finished: **Step 1.6** (`DecryptAsync`), 2026-11-24.

```text
Do Step 1.7 (error model, ADR-13 Layer 1) of docs/PROJECT_PLAN.md on the existing branch feature/stage-1-core.
Don't create a new branch.

LEAN MODE: short lesson, one reviewer agent, a single Stryker pass, terse updates. No perfectionism.

Context (already done):
- Step 1.6 is committed: FileEncryptionService.DecryptAsync (verify-then-commit), AtomicFileWriter with
  isSensitive (hidden on Windows, 0600 on Linux/macOS, emptied on failure).
- AuthenticationFailedException was pulled forward into Step 1.6 (three standard constructors, derives from
  SecureFileException). SecureFileException and InvalidEncryptedFileException (with Reason) exist since Step 1.2.
- Decrypt can also throw CryptographicException (valid tag but bad padding: only a foreign writer that knows the
  password) and EndOfStreamException (file shrank while read). Both are documented on IFileEncryptionService.

Build:
- Go through the test catalog §9 (T-05 to T-11, T-22, T-23) and the ADR-13 table in docs/adr/0013-exception-handling.md.
  Make sure every failure maps to exactly one documented exception type, and add the missing ADR-13 rows
  (AuthenticationFailedException, CryptographicException after a valid tag, EndOfStreamException, the lone-surrogate
  ArgumentException that arrives only on await).
- Decide (and record in ADR-13) whether CryptographicException after a valid tag should be wrapped as
  InvalidEncryptedFileException(Damaged). Keep the inner exception if you wrap it.
- Only add code where a gap is found. Don't add new exception types without a reason.

Tests:
- One test per §9 failure row that isn't covered yet, each proving no partial output and no temp file.

Then: run scripts/verify.ps1, update the docs briefly (the LEARNING_LOG lesson and walkthrough, the plan row, CHANGELOG,
docs/NEXT_STEP_PROMPT.md for the next step: the vertical slice, Stage 2.1–2.3 + 2.6 + 3.1 simplest form),
commit with no AI attribution, push, and confirm CI is green.
```
