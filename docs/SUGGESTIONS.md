# SecureFile: suggestions and roadmap

Written early (plan Step 8.1) because several decisions came up while building and testing, and the Team Lead should see
the thinking, not only the result. Priority uses **MoSCoW** (Must / Should / Could / Won't for this version) and effort uses
**S / M / L** (a day or less / a few days / a week or more of focused work). "Built" is checked against the code and the CHANGELOG.

## 1. The idea in one sentence

**Boring, proven cryptography; an interface a non-technical person can use without help; and a promise that can be checked:
nothing leaves your computer.** Everything below serves one of those three.

## 2. Decisions we made on purpose

### 2.1 One encryption design, chosen by the app, never by the user (and never at random)

| Option | Verdict | Why |
|---|---|---|
| The user picks the algorithm | Rejected | A non-expert cannot judge between algorithms, so a choice only invites a weaker pick. Every extra algorithm multiplies code, tests and file-format variants, and a format that lets the algorithm be chosen is easier to attack (downgrade). |
| The program picks a **random** algorithm per file, so attackers do not know which one | Rejected | Security must not depend on the attacker not knowing the method (**Kerckhoffs's principle**: a system should stay secure even if everything except the key is public). The method has to be stored in the file or tried one by one, so the secrecy buys almost nothing: with 3 algorithms an attacker tries 3 times as many guesses, while a password guess costs far more than that. It also makes the **weakest** algorithm in the pool the real strength, and it makes decryption slower and bugs more likely. |
| **One well-reviewed design, versioned in the file header, upgraded by the app between releases** | **Chosen** | This is how `age`, Signal and modern password managers work. The header already has a version number, so a stronger design later is "format version 2", and old files keep opening. |

What *is* random per file, and why it matters: the **salt** (so the same password gives a different key for every file) and
the **IV** (so the same content gives different ciphertext every time). Those are in the code today. The algorithm is fixed:
AES-256-CBC with an HMAC-SHA-256 check, key derived by PBKDF2-HMAC-SHA-256.

**The honest way to make an attacker's life harder** is not to hide the method but to make each password guess expensive
(see 3.1: Argon2id, more iterations) and to help people choose long passphrases (built: strength check, 8-character minimum).

### 2.2 No rules like "must contain a symbol"

Forced symbols and digits push people to `Password1!`. Advice from NIST SP 800-63B is to require **length**, check for
well-known passwords, and let people use spaces and long phrases. Built: an 8-character minimum, a confirmation box and a
strength bar that rates repeats, runs and common words as Weak (`PasswordStrengthEvaluator`).

### 2.3 AI is not allowed to make security decisions

| Idea | Verdict | Reason |
|---|---|---|
| AI picks the algorithm or the settings for a file | Rejected | The app chooses one design (2.1). A model whose answer can vary cannot be verified or tested, and it would need the file's size and sensitivity sent to a service, which breaks "nothing leaves your computer". |
| AI generates or suggests passwords | Rejected | A language model is not a source of cryptographic randomness, and the password would pass through someone else's service. Use the operating system's secure random generator (3.2). |
| AI suggests output file names | Rejected as stated | Names reveal what a file is. A local rule already does the useful part (`name.ext.enc`). Better: an option to hide the original name (3.1). |
| Local, rule-based "this looks sensitive" hint (`.kdbx`, `.pem`, `.pfx`, `.key`) | **Accepted (Could)** | No AI needed, no network, easy to explain and test. |
| An optional, off-by-default **local-model** assistant that sees only the extension and size, never contents or passwords, and only advises | Possible later (Won't for this version) | If the Team Lead wants an AI story: opt-in, local, clearly labelled, advice only. It must never be on the path that encrypts. |

## 3. Roadmap

### 3.1 Security and format (the engine)

| Suggestion | Why / benefit | Priority | Effort | Built? |
|---|---|---|---|---|
| AES-256-CBC + HMAC, PBKDF2, random salt and IV per file | Proven design, wrong password and tampering detected | Must | done | ✅ |
| Verify-then-commit decrypt, no partial output, atomic writes | A failed or cancelled run leaves nothing usable behind | Must | done | ✅ |
| **Format v2: chunked authenticated encryption (AES-256-GCM or XChaCha20-Poly1305 per chunk) with a key-check value** | Today a wrong password on a huge file is only found at the very end (review item 6). A key-check value fails in milliseconds, and per-chunk authentication gives early, precise error positions. Versioned header means v1 files still open | Should | L | ⏸ v2 |
| **Argon2id key derivation** (memory-hard) instead of PBKDF2 | Raises the cost of offline guessing on GPUs far more than extra PBKDF2 iterations. Parameters live in the header | Should | M | ⏸ v2 |
| Store the original file name (encrypted) inside the file, and an option to hide it in the output name | Better restore experience and a name that does not leak the content | Could | M | ⏸ v2 |
| Optional **key file** next to the password | Two things needed to open a file, for people who want more than a password | Could | M | ⏸ |
| Free-disk-space check before large runs | A full disk is reported at the start, not at 99 % | Could | S | ⏸ |
| "Secure delete" of the original | SSDs cannot guarantee it, so a promise we cannot keep | Won't (explain why) | n/a | ❌ |
| Written file-format specification (`FORMAT.md`) and test vectors | Other tools can interoperate; safe upgrades | Should | S | ⏸ |

### 3.2 Everyday experience (what people feel)

| Suggestion | Why / benefit | Priority | Effort | Built? |
|---|---|---|---|---|
| Content-based Encrypt/Decrypt (no mistakes with renamed files), live "next step" line | Fewer clicks, fewer errors | Must | done | ✅ |
| Password rules done right (length, confirm, strength, Caps Lock, show with copy blocked) | Fewer lost files | Must | done | ✅ |
| Cancel and close cleanly during an operation | No hidden leftover files | Must | done | ✅ |
| **Passphrase generator** ("blue river coffee lamp"), local word list, OS secure random, clipboard cleared after 30 s | Memorable and strong | Should | S | ⏸ |
| "This looks like a key or password file" hint (by extension, local rules) | Helps people notice what they are protecting | Could | S | ⏸ |
| Drop-zone text and drag highlight, file card with folder, date and exact size | Discoverability | Should | S | ⏸ |
| Right-click "Encrypt with SecureFile" in Explorer / Finder / file managers | Fits how people already work | Could | M | ⏸ v2 |
| Encrypt a whole folder (zip, then encrypt) and batches | A common real need | Could | M | ⏸ v2 |
| Arabic interface with right-to-left layout | Local users; the app already handles Arabic file names | Could | M | ⏸ |

### 3.3 Platforms and shipping

| Suggestion | Why / benefit | Priority | Effort | Built? |
|---|---|---|---|---|
| Windows x64 app (WPF), friends build as a self-contained zip | The required deliverable; nothing to install | Must | done | ✅ |
| Automated tests of the engine on **Windows, Ubuntu and macOS** | Proves the engine is portable and the format is the same everywhere | Should | done | ✅ (CI) |
| **Command-line version** (`securefile encrypt in out`), no echo when typing the password | The fastest route to Linux and macOS users and to automation, reusing the same engine | Should | M | ⏸ Stage 7 |
| **Avalonia app for macOS and Linux** reusing the same ViewModel | Same tool for every team member; proves the layers are clean | Could (bonus) | L | ⏸ Stage 7 |
| Windows Arm64 build | Snapdragon laptops | Could | S | ⏸ |
| Code signing, installer (winget / MSIX), reproducible build with hashes and an SBOM | Removes "unknown publisher" warnings; trust for enterprises | Should | M | ⏸ Stage 6 |

## 4. A short story for the presentation

1. **Choose proven building blocks, not clever ones.** One design, versioned, with a written reason for every option we refused.
2. **Make the safe path the easy path.** The app only offers the action that fits the file, blocks bad passwords before they
   cost someone a file, and leaves no half-written data behind when you cancel or close.
3. **Find your own bugs first.** An independent product-owner and tester review of the running design found 39 issues; the
   serious ones are fixed and the rest are written down with reasons (`docs/REVIEW_BACKLOG.md`).
4. **Be honest about limits.** No secure delete (SSDs), no hidden "AI", no claim of certification; known weak spots are listed.
5. **Next:** an authenticated chunked format with instant wrong-password detection, Argon2id, and the same engine on every desktop.
