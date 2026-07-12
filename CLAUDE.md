# CLAUDE.md — Unity Project Context

> This file is automatically loaded by Claude Code at session start.
> Keep it up to date as your project evolves. Agents read this before acting.

## Mandatory Dependencies

Voltron's three-tier agent model relies on three external tools. Setup/scaffold accounts for all of them; if any is missing, run the install command before invoking agents.

| Tool | Purpose | Install (cross-platform) | Alternative |
|---|---|---|---|
| **beads** ([gastownhall/beads](https://github.com/gastownhall/beads)) | Dependency-aware task tracking — drives the bead graph that scrum-master uses to enforce task ordering. | `curl -fsSL https://raw.githubusercontent.com/gastownhall/beads/main/scripts/install.sh | bash` | `brew install beads` (macOS / Linux) |
| **stringer** ([davetashner/stringer](https://github.com/davetashner/stringer)) | Codebase baseline analysis — read by code-analyst before every audit. | `go install github.com/davetashner/stringer/cmd/stringer@latest` (needs Go) | Pre-built binary from [releases](https://github.com/davetashner/stringer/releases/latest), or `brew install davetashner/tap/stringer` (macOS) |
| **alexandria** ([7ports/project-alexandria](https://github.com/7ports/project-alexandria)) | Tooling/setup guides — every agent calls `mcp__alexandria__quick_setup` before installing any tool, and `update_guide` after. | `git clone` + `npm install` in `mcp-server/` + register MCP server in `~/.claude.json` | (none — required setup) |

Verify all three by running `mcp__project-voltron__setup_voltron` — it hard-fails with install commands if any are missing.

---

---

## Project Identity

**Project Name:** project-cucumber
**Genre / Type:** 2D top-down survivors (Vampire-Survivors-like)
**Target Platform(s):** PC
**Unity Version:** 6000.4.2f1
**Render Pipeline:** URP 2D (com.unity.render-pipelines.universal 17.4.0; Renderer2D.asset in Assets/Settings/)
**Status:** Prototype / active development

---

## Repository Layout

> **NOTE:** This project does NOT use the `Assets/_Project/` convention from the Voltron template.
> All custom scripts are flat in `Assets/Scripts/`; agents must look there, not under `Assets/_Project/`.

```
Assets/
  Scripts/          <- 43 flat .cs files — global namespace, no subdirectory grouping
  Prefabs/
    enemies/        <- chaser.prefab, shooter.prefab, slime.prefab, enemyProjectile.prefab
      bosses/       <- boss.prefab, diggy.prefab, ziggy.prefab
    pickups/        <- XP1.prefab–XP4.prefab, itemPickup.prefab, questItem.prefab
    blood.prefab, damageNumber.prefab, floatingLevelText.prefab,
    levelUpBurst.prefab, star.prefab, uiLevelUpBurst.prefab
  Scenes/
    SampleScene.unity   <- single scene (no Bootstrap/Persistent multi-scene pattern)
  Materials/        <- ParticleColored.mat, TelegraphLine.mat
  Sprites/          <- sprite sheets, .asset slices (cave tileset, decorations, etc.)
  Settings/         <- UniversalRP.asset, Renderer2D.asset (URP 2D pipeline assets)
  Editor/           <- editor-only utilities
  SmallScaleInt/    <- third-party character creator asset (read-only, don't edit)
Packages/           <- Unity Package Manager manifests
ProjectSettings/
```

**Rules:**
- Scripts live in `Assets/Scripts/` (flat). Do NOT create subdirectories there without explicit instruction.
- Do NOT modify anything under `Assets/SmallScaleInt/` (third-party asset).
- No `ThirdParty/` or `Plugins/` directories currently exist in this project.

---

## C# Conventions

> **IMPORTANT — No namespaces.** All 43 scripts are in the **global namespace**. Do NOT invent or add namespace declarations when writing new scripts. The Voltron template shows a namespaced example; that template does NOT apply to this project.

```csharp
// Actual project pattern — no namespace block
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 5f;

    private Rigidbody2D _rb;

    public float MoveSpeed => _moveSpeed;
}
```

**Key rules:**
- **No namespaces** — scripts in the global namespace (match existing files)
- Serialized inspector fields use `[SerializeField] private` — not `public`
- Private fields use `_camelCase`; properties/methods use `PascalCase`
- No `Find()`, `FindObjectOfType()`, or `SendMessage()` — wire via inspector or events
- Prefer C# `Action`/`event` over tight coupling
- All `Coroutine` starts must have a corresponding stop path

---

## Key Packages & Versions

| Package | Version | Notes |
|---|---|---|
| com.unity.render-pipelines.universal | 17.4.0 | URP 2D — Renderer2D.asset; no HDRP |
| com.unity.inputsystem | 1.19.0 | New Input System only — no legacy Input.GetKey |
| com.unity.2d.animation | 14.0.4 | 2D skeletal animation |
| com.unity.2d.sprite | 1.0.0 | 2D sprite tools |
| com.unity.2d.tilemap | 1.0.0 | Tilemap support |
| com.unity.2d.tilemap.extras | 7.0.1 | Rule tiles, animated tiles |
| com.unity.2d.spriteshape | 14.0.1 | Sprite shape rendering |
| com.unity.2d.aseprite | 4.0.1 | Aseprite importer |
| com.unity.2d.psdimporter | 13.0.2 | PSD file importer |
| com.unity.cinemachine | 3.1.7 | Camera follow system |
| com.unity.ugui | 2.0.0 | UI Toolkit / Canvas UI |
| com.unity.2d.tooling | 2.0.1 | 2D project tools and extensions |

> **No DOTween** is in the manifest — do not add DOTween calls. Use Unity coroutines or Cinemachine for any camera/value animation needs.

---

## Scene Structure

> **NOTE:** This project uses a **single scene**, not the Bootstrap/Persistent multi-scene pattern from the Voltron template. Do NOT create Bootstrap or Persistent scenes; all game systems live in SampleScene.

**Single scene:** `Assets/Scenes/SampleScene.unity`
- Contains all game systems: GameController, spawners, player, UI, camera, environment
- Play Mode testing always uses SampleScene as the only active scene

**When editing scenes:** Save the scene after any hierarchy/component changes before exiting or running Play Mode.

---

## Verification Commands

Before completing any task, run these checks:

```bash
# Check for compile errors (requires Unity MCP)
# Use: read_console tool — look for [Error] or [Exception] entries

# Check scene is not dirty / unsaved
# Use: editor-application-get-state tool

# After script changes, wait for recompile
# Use: editor-application-get-state — wait until isCompiling = false
```

**Definition of done for any code task:**
1. No compile errors in Unity console
2. No null reference exceptions in Play Mode for the affected feature
3. Prefab references are set (no missing references in inspector)
4. Changes committed to git with a descriptive message

---

## Active Work

<!-- Update this section frequently — agents use it to understand current focus -->

**Current sprint goal:** Polish and expand combat loop — additional enemy behaviors, weapon tuning, item balance

**In progress:**
- (check `bd list --status=in_progress` for current claims)

**Recently shipped (from git log):**
- [x] Editor wiring: wall physics, XP drop array, item-choice panel
- [x] Timer format, wall collision, bounce-scaling, timed XP ×2, XP drop visuals, item-choice menu
- [x] Five weapon items via boss drops: cone, bounce, fire, explosion, freeze
- [x] ×10 damage/health rebalance + projectile-size stat and upgrade
- [x] Enemy HP scales +50% every 7 minutes (spawn-time; bosses included)
- [x] Time-based enemy progression, boss blood-on-hit, capped XP-gain upgrade
- [x] Balanced enemy spawn distribution + random multi-boss spawner
- [x] Shooter aim locked at telegraph, configurable level-up increments, bullet pierce
- [x] Shooter enemy, boss bullet-hell, enemy projectiles, item drops, 5-choice level-up menu, level-gated spawns
- [x] XP pickup-radius stat + upgrade; boss enemy (250 HP, world-space health bar, catch-up leash)
- [x] Defense/regen stats, base+mult stat system, flat+percent level-up upgrades, animated chaser, quest items + off-screen indicators
- [x] Damage feedback — hit flash, screen shake+flash, pooled damage numbers
- [x] HUD counters, per-level spawn scaling, LEVEL UP! text, collision fixes, pause menu
- [x] Combat batch: projectile range/fade, player HP + contact damage, level-up menu, game-over, enemy blood
- [x] Level-up + XP bar, movement fixes, trigger collisions + Walls layer
- [x] XP system — pooling, enemy health/XP drops, pickup homing, chaser, off-screen spawner

**Known issues / tech debt:**
- Enemy body collider currently also bumps the player (pending a dedicated EnemyBody physics layer to separate contact damage from body collision)
- Item-choice menu and level-up menu both freeze time via `Time.timeScale = 0` — possible early-unpause if both try to restore simultaneously (`itemChoiceMenuController.cs`, `levelUpMenuController.cs`)

---

## Agent Team Roles

### Orchestrator (slash command — runs in the main Claude Code session)

| Command | File | Purpose |
|---|---|---|
| `/scrum-master` | `.claude/commands/scrum-master.md` | Work breakdown, task assignment, sprint coordination, dispatch to specialists |

**Why a slash command, not a subagent:** the scrum-master must run in your main chat session so it can stream real-time agent output and channel communication between you and the specialist agents. Subagent contexts cannot do any of that. Always invoke with `/scrum-master`.

### Specialist subagents (defined in `.claude/agents/`)

| Agent | File | Purpose |
|---|---|---|
| `project-planner` | `project-planner.md` | Tech stack research, architecture design, project planning |
| `scene-architect` | `scene-architect.md` | GameObject hierarchy, prefabs, scene setup |
| `csharp-dev` | `csharp-dev.md` | Script writing, refactoring, C# logic |
| `shader-artist` | `shader-artist.md` | Materials, shaders, VFX Graph, render features |
| `build-validator` | `build-validator.md` | Console monitoring, compile checks, Play Mode testing |
| `asset-manager` | `asset-manager.md` | Folder structure, import settings, asset organization |

**Invoke specialists with:** `@agent-project-planner`, `@agent-scene-architect`, `@agent-csharp-dev`, etc. (Note: `/scrum-master` will dispatch these for you — you rarely need to invoke them directly.)

---

## Agent Invocation Modes

**Primary dispatch is `run_agent_in_docker`.** The scrum-master launches specialist agents inside Docker containers automatically — that is how >95% of Unity work runs (C# scripts, shader/material file edits, manifest changes, folder/asset structure, planning, research).

**Narrow exception: the `Agent` tool for Unity Editor work.** Four managers (`scene-architect`, `build-validator`, plus the Editor-preview slices of `shader-artist` and `asset-manager`) require a live Unity Editor with Coplay MCP, which Docker cannot provide. The scrum-master dispatches these from the host via the `Agent` tool. **The scrum-master will tell you which tasks need this.**

| Agent | Invocation | Docker? | Reason |
|---|---|---|---|
| `csharp-dev` | `run_agent_in_docker` | ✓ | File editing only — no Editor access needed |
| `shader-artist` | `run_agent_in_docker` for file tasks; `Agent` tool for Editor preview | ✓ / ✗ | Shader file editing works in Docker; visual preview + material assignment require Editor |
| `asset-manager` | `run_agent_in_docker` for folder tasks; `Agent` tool for import settings | ✓ / ✗ | Folder/manifest work in Docker; import settings (texture/audio/mesh) require Editor |
| `project-planner` | `run_agent_in_docker` | ✓ | Research only — no Editor access needed |
| `scene-architect` | **`Agent` tool (Editor exception)** | ✗ | Requires live Unity MCP — scene hierarchy, prefabs, components |
| `build-validator` | **`Agent` tool (Editor exception)** | ✗ | Requires live Unity MCP — Play Mode, console, compile state |

**For Editor-exception agents:** The scrum-master will prepare the complete task description and dispatch via the `Agent` tool itself; you do not need to copy-paste anything. (If your harness requires user-mediated invocation, the scrum-master will tell you.)

**Prerequisites:**
- Docker must be installed and running (for `run_agent_in_docker` agents)
- `Dockerfile.voltron` must exist in the project root (generated by `scaffold_project`)
- Unity Editor must be open with Coplay MCP connected (for Editor-exception agents)

---

## MCP Tools Available

- **Unity MCP** — direct Editor control (GameObjects, console, Play Mode, screenshots)
- **git** — version control operations
- **github** — PR/issue management
- **memory** — persist decisions and patterns across sessions
- **fetch** — Unity docs, package changelogs, API references
- **alexandria** — tooling setup guides; **mandatory** — call `quick_setup` before installing any tool (no exceptions), `update_guide` after. Alexandria is for non-project-specific documentation only (tool setup, platform quirks, version notes) — project-specific knowledge stays in CLAUDE.md

---

## Important Project Decisions

<!-- Use this as a living log — add entries as decisions are made -->

| Date | Decision | Reason |
|---|---|---|
| 2026-07-08 | Chose URP 2D (Renderer2D) over HDRP or Built-in | 2D top-down genre; lightweight render path; Renderer2D.asset already configured |
| 2026-07-08 | Single-scene architecture (SampleScene.unity only) | Prototype stage; no need for additive scene loading yet |
| 2026-07-08 | Scripts in global namespace (no namespace declarations) | Consistent with all 43 existing scripts; avoids churn |
| 2026-07-08 | **Editor exception: scrum-master dispatches `scene-architect` and `build-validator` via the `Agent` tool, NOT `run_agent_in_docker`** | These agents require a live Unity Editor + Coplay MCP, which Docker cannot provide. This OVERRIDES any generic "never use the Agent tool" guidance. The user's only manual carve-out is build-validator Play-Mode testing; all other Editor wiring (scene/prefab/inspector/Physics-matrix/UI) is dispatched by the scrum-master automatically. |

---

## Agent Auto-Update

Voltron agents are kept current automatically. At the start of each session:
1. Agents will be auto-updated if the installed version differs from the local Voltron installation
2. If you see `[VOLTRON] Updated N agent(s)` in your context, acknowledge the update to the user

---

## Session Closeout Protocol

At the end of each working session, submit a reflection to help Project Voltron improve its agent templates:

```
mcp__project-voltron__submit_reflection({
  project_name: "[this project's name]",
  project_type: "unity",
  session_summary: "[what was accomplished]",
  agents_used: ["list", "of", "agents", "invoked"],
  agent_feedback: [{ agent: "...", needs_improvement: "...", suggested_change: "..." }],
  overall_notes: "..."
})
```

Even a brief reflection is valuable. Focus on gaps in agent instructions that required workarounds.

If the session included any tool setup, API integration, or platform-specific discoveries, also call `mcp__alexandria__update_guide` to record findings in the knowledge base.

---

## Things Claude Should Never Do

- Modify files under `ThirdParty/` or `Plugins/`
- Use deprecated Unity APIs (`OnGUI`, legacy `Input`, `WWW`)
- Add `using` statements for packages not listed in `Packages/manifest.json`
- Delete or rename scenes without checking `EditorBuildSettings`
- Run Play Mode tests while a scene has unsaved changes

<!-- BEGIN BEADS INTEGRATION v:1 profile:minimal hash:ca08a54f -->
## Beads Issue Tracker

This project uses **bd (beads)** for issue tracking. Run `bd prime` to see full workflow context and commands.

### Quick Reference

```bash
bd ready              # Find available work
bd show <id>          # View issue details
bd update <id> --claim  # Claim work
bd close <id>         # Complete work
```

### Rules

- Use `bd` for ALL task tracking — do NOT use TodoWrite, TaskCreate, or markdown TODO lists
- Run `bd prime` for detailed command reference and session close protocol
- Use `bd remember` for persistent knowledge — do NOT use MEMORY.md files

## Session Completion

**When ending a work session**, you MUST complete ALL steps below. Work is NOT complete until `git push` succeeds.

**MANDATORY WORKFLOW:**

1. **File issues for remaining work** - Create issues for anything that needs follow-up
2. **Run quality gates** (if code changed) - Tests, linters, builds
3. **Update issue status** - Close finished work, update in-progress items
4. **PUSH TO REMOTE** - This is MANDATORY:
   ```bash
   git pull --rebase
   bd dolt push
   git push
   git status  # MUST show "up to date with origin"
   ```
5. **Clean up** - Clear stashes, prune remote branches
6. **Verify** - All changes committed AND pushed
7. **Hand off** - Provide context for next session

**CRITICAL RULES:**
- Work is NOT complete until `git push` succeeds
- NEVER stop before pushing - that leaves work stranded locally
- NEVER say "ready to push when you are" - YOU must push
- If push fails, resolve and retry until it succeeds
<!-- END BEADS INTEGRATION -->
