# Agent instructions

This repository's agent guide is [`CLAUDE.md`](CLAUDE.md), at this same root. Read it, then
`README.md`, `docs/architecture.md` and `docs/game-systems.md` before design work. This file
exists so that tools looking for `AGENTS.md` find it here and stop looking.

## Never search outside your own worktree

Every search, listing and glob stays inside the worktree you were given:

- no `..` in a path, and no `/` as a search root;
- nothing under `/var/lib/agent-orchestra` except your own worktree;
- no `find ..`, `rg … ..`, `ls -R /` or the like, even to look for this file.

The server hosts every room's repositories side by side. One `rg --files --hidden . ..`
walked 1,553 of them on 2026-11-25 and stalled the whole server. If a file is not in your
worktree, ask for its exact path rather than searching for it. Reading a file you were given
by its exact path, such as a blueprint under `/tmp`, is fine.
