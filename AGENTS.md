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
