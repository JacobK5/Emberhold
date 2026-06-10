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

> **CRITICAL:** `VERSION` (this file at the repo root) is the *single source of
> truth* the release pipeline reads for the tag + release name. It is **not** the
> `Program.Version` constant in `Emberhold/Program.cs` — that one only feeds the
> title-screen label. Keep the two in sync, but if you only update `Program.Version`
> the GitHub Release will be **mislabeled**. (This happened: `VERSION` was left at
> `0.16.0` from ~v0.16 through v0.34 while only `Program.Version` moved, so every
> release in that range published as "v0.16.0".) Always bump `VERSION`.

---

## Merging and triggering the build

```
git checkout master
git merge --no-ff feature/short-description
git push origin master
```

The push to `master` triggers `.github/workflows/build.yml`, which:

1. Builds `Emberhold-win-v{VERSION}-{sha}.exe` (Windows, self-contained)
2. Builds `Emberhold-mac-x64-v{VERSION}-{sha}.zip` and
   `Emberhold-mac-arm64-v{VERSION}-{sha}.zip` — each contains an ad-hoc-signed
   `Emberhold.app` bundle + `README-FIRST.txt`. The bundle wrapping matters:
   Gatekeeper assesses a signed .app as ONE unit, so a single right-click → Open
   unlocks it. Loose executable+dylib zips get blocked dylib-by-dylib
   (`libhostfxr.dylib` etc.) with no practical way to approve them all.
   Templates live in `packaging/mac/`.
3. Publishes a GitHub Release tagged `v{VERSION}-{sha}` with all files attached

The release appears at `https://github.com/JacobK5/Emberhold/releases`.

---

## Skipping a build

If a push to `master` doesn't need a release (doc edits, CI tweaks, comment
fixes), add `[skip ci]` anywhere in the commit message and GitHub Actions will
skip the run entirely:

```
git commit -m "Fix typo in AGENTS.md [skip ci]"
```

Don't use this for any commit that changes game code or assets.

---

## Summary checklist before merging to master

- [ ] Feature works end-to-end in the game
- [ ] Smoke test exits 0 with no exceptions (`--smoke 600`)
- [ ] `VERSION` bumped appropriately
- [ ] Commit message summarizes the change clearly
