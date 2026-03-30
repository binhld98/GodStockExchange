# God Stock Exchange

## Commit message guideline

This repository enforces a clear, consistent commit message format to make history easier to read, automate releases, and link changes to tracking systems.

Commit messages must follow this template:

```
<type>[optional scope]: <short description>

[optional body]

[optional footer(s)]
```

### Note that:

The first line should be short, all lowercase and concise (recommended ≲ 50 characters).

If present, wrap the body at ~72 characters per line and use it to explain *what* and *why* rather than *how*.

Use the footer for metadata such as issue or ticket references (for example: `Closes D2IQ-12345`).

For breaking changes include a `BREAKING CHANGE: <description>` entry in the body or footer.

### Allowed types

- feat — a new feature
- fix — a bug fix
- chore — non-production changes (e.g., dependency updates, tooling) that do not modify src or test files
- refactor — code changes that neither fix a bug nor add a feature
- docs — documentation updates (README, markdown files)
- style — formatting or whitespace changes that do not affect code behavior
- test — adding or updating tests
- perf — performance improvements
- ci — continuous integration changes
- build — changes to the build system or external dependencies
- revert — reverts a previous commit

### Example

```
fix: fix foo to enable bar

This fixes the broken behavior of the component by doing xyz.

BREAKING CHANGE
Before this fix foo wasn't enabled at all, behavior changes from <old> to <new>

Closes D2IQ-12345
```

If you’re unsure about a commit type, ask or check recent commits for examples.

Happy committing 🚀