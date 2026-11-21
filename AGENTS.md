# AGENTS.md

## Agent skills

### Issue tracker

Issues are tracked in GitHub Issues on `a-cim/grug-bot`, via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

The default five labels: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `CONTEXT.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.

### Code review

Use `/mattpocock-skills:code-review`, comparing against `main` by default. The spec is the GitHub issue the commits reference. For exact behaviour, also check `planning/05-message-format.md`, `planning/06-test-plan.md` and `planning/decisions.md`. Coding standards are in `CODING_STANDARDS.md`, `.editorconfig` and the ADRs in `docs/adr/`.

## Conventions

### Commits and PR titles

Use [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/): `type(scope): summary`.

- **Types:** `feat`, `fix`, `docs`, `test`, `build`, `ci`, `refactor`, `chore`.
- **Scope** is optional and names the area touched, e.g. `core`, `mod`, `discord`, `config`, `packaging`, `planning`.
- **Summary:** lower case, imperative ("add", not "added"), no trailing period.
- **Breaking changes:** add `!` after the type or scope, and explain in a `BREAKING CHANGE:` footer.
- **PR titles** follow the same format, so the merge commit reads the same way. Put `Closes #N` in the PR body.

Examples: `feat(discord): relay General chat to the chat channel`, `fix(packaging): match game DLLs by file name`, `ci: build and test GrugBot.Core`.
