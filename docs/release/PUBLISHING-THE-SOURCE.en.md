# Before making the repository public

Publishing cannot be undone: the code is copied, indexed, and archived by third parties the same day.
This is the review done before that decision is taken, and what is left to decide. The Spanish
version is at [PUBLISHING-THE-SOURCE.es.md](PUBLISHING-THE-SOURCE.es.md).

## What was reviewed

The whole history, not just the current tree: **76 commits** and **696 distinct files** ever added,
across every branch.

| Looked for | Result |
|---|---|
| The developer's account name | 0 occurrences |
| The computer name | 0 occurrences |
| The developer's local paths | 0 occurrences |
| Private keys, GitHub tokens, cloud credentials | 0 occurrences |
| Video files, databases, certificates, `.env` | none, in any commit |
| Email addresses | only the account that signs the commits |
| Example paths in tests | all fictional, with invented names |

Two matches turned out to be the opposite of a leak: one is the secret scanner's own list of
patterns, and the other is a sample JWT header whose body reads literally `body.signature`, used to
check that diagnostics redact credentials.

## What did turn up

**The title of a show from the personal library, as example data in tests.** It had already been
redacted once, in `docs: redact the personal library from evidence and fixtures`, replaced by an
invented name. That redaction **was incomplete**: it changed the Spanish title and left the English
one, because the pattern being searched for was written in Spanish. It is complete now in the current
tree.

**The history still holds both forms.** Redacting in a later commit does not erase the earlier ones.
Removing it from the past means rewriting history, which changes every commit identifier.

**The default branch counted too.** The redaction lived only on the working branch: `main`'s tree
kept showing the title until **2026-08-08**, and `main` is the first thing a public repository
shows. That day `main` was fast-forwarded to the branch — nothing rewritten — and since then
`prepare-release.ps1` blocks any publication with `main` left behind, so this no longer depends on
anybody remembering.

## What is left to decide

**Whether rewriting the history is worth it.** What would remain exposed is the title of a very
well-known show used as a filename example. It reveals no path, no account, and no inventory: it
reveals that somebody once used that title as a test case. Rewriting would invalidate the identifiers
of all 76 commits and any existing clone.

**The email of the account that signs the commits will be public.** It is an organisation address
rather than a personal one, but that is better known beforehand than afterwards.

## The check no longer depends on anybody remembering

`RepositoryPrivacyTests` walks the versioned files on every run of the suite and fails if it finds the
account name, the computer name, the profile path, the repository path, or the name of any folder git
ignores beside the repository — which are, precisely, the personal library.

**No personal data is written into that test.** Every value is read from the machine it runs on, so it
works for anybody and does not add to the repository the very thing it exists to keep out.

What it cannot do is recognise a translation: a show named in one language in a folder and in another
inside a test is invisible to it, which is exactly how the last one survived. That remains a person's
judgement, and saying so is better than implying the check is total.
