# Agent Working Instructions

This document tells AI agents (Claude, Codex, etc.) how to work cleanly with
this repo's branching and build pipeline.

---

## The golden rule

**Every push to `master` triggers a full build and publishes a new GitHub Release.**
That means `.exe` and `.zip` artifacts get built, tagged, and posted publicly.
Do not push to `master` while iterating on a feature.

---

## Branching

Always create a feature branch for new work:

```
git checkout -b feature/short-description
```

Use `feature/` for new mechanics, `fix/` for bug fixes, `refactor/` for
cleanup. Work entirely on that branch. Push it to `origin` as often as you
like — it will not trigger a build.

```
git push origin feature/short-description
```

---

## Finishing a feature

Before merging, the feature must be smoke-tested and stable. Run a headless
smoke test to confirm nothing is crashing:

```powershell
$env:Path = [Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [Environment]::GetEnvironmentVariable("Path","User")
dotnet run --project Emberhold/Emberhold.csproj -- --smoke 600 --shot shot.png
```

600 frames = 10 seconds of gameplay. Exit code 0 and a clean `REPORT` line in
the output means it's ready. Read `shot.png` to do a quick visual sanity check.

---

## Bumping the version

When the feature is tested and ready to ship, bump `VERSION` before merging.
The file contains a single `MAJOR.MINOR.PATCH` line:

| Change | Bump |
|---|---|
| Small fix, tweak, or polish | patch (`0.0.2` → `0.0.3`) |
| New mechanic or feature | minor (`0.0.3` → `0.1.0`) |
| Major redesign or milestone | major (`0.1.0` → `1.0.0`) |

Edit `VERSION`, commit it on the feature branch, then merge.

---

## Merging and triggering the build

```
git checkout master
git merge --no-ff feature/short-description
git push origin master
```

The push to `master` triggers `.github/workflows/build.yml`, which:

1. Builds `Emberhold-win-v{VERSION}-{sha}.exe` (Windows, self-contained)
2. Builds `Emberhold-mac-v{VERSION}-{sha}.zip` (macOS x64 + arm64)
3. Publishes a GitHub Release tagged `v{VERSION}-{sha}` with both files attached

The release appears at `https://github.com/JacobK5/Emberhold/releases`.

---

## Summary checklist before merging to master

- [ ] Feature works end-to-end in the game
- [ ] Smoke test exits 0 with no exceptions (`--smoke 600`)
- [ ] `VERSION` bumped appropriately
- [ ] Commit message summarizes the change clearly
