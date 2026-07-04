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

**Project Name:** [YOUR PROJECT NAME]
**Genre / Type:** [e.g. 3D platformer, 2D puzzle, mobile idle]
**Target Platform(s):** [PC / Android / iOS / WebGL / Console]
**Unity Version:** [e.g. 6000.0.30f1]
**Render Pipeline:** [Built-in / URP / HDRP]
**Status:** [Prototype / Alpha / Beta / Shipping]

---

## Repository Layout

```
Assets/
  _Project/               <- All custom project files live here
    Scripts/
      Gameplay/           <- Player, enemies, mechanics
      Systems/            <- Game loop, save, audio, events
      UI/                 <- Canvas, panels, HUD logic
      Utilities/          <- Extensions, helpers, constants
    Prefabs/
    ScriptableObjects/
    Scenes/
      Main/
      UI/
      Testing/
    Art/
      Materials/
      Textures/
      Shaders/
    Audio/
  ThirdParty/             <- Imported packages (read-only, don't edit)
  Plugins/                <- Native plugins
Packages/                 <- Unity Package Manager manifests
ProjectSettings/
```

**Rule:** Never place custom files outside `Assets/_Project/`. Never modify anything under `ThirdParty/` or `Plugins/`.

---

## C# Conventions

**Namespace root:** `[YourStudio].[ProjectName]` (e.g. `AcmeCo.StarRun`)
**Namespace mirrors folder:** `AcmeCo.StarRun.Gameplay`, `AcmeCo.StarRun.UI`, etc.

```csharp
// Standard MonoBehaviour header
using UnityEngine;

namespace AcmeCo.StarRun.Gameplay
{
    public class PlayerController : MonoBehaviour
    {
        // Serialized fields use [SerializeField], never public fields for inspector use
        [SerializeField] private float _moveSpeed = 5f;

        // Private fields use _camelCase
        private Rigidbody _rb;

        // Properties use PascalCase
        public float MoveSpeed => _moveSpeed;
    }
}
```

**Key rules:**
- No `Find()`, `FindObjectOfType()`, or `SendMessage()` — use dependency injection or events
- Prefer `UnityEvent` or C# `Action`/`event` over tight coupling
- `Update()` logic belongs in systems, not individual MonoBehaviours where avoidable
- ScriptableObjects for shared data, not static singletons
- All `Coroutine` starts must have a corresponding stop path

---

## Key Packages & Versions

| Package | Version | Notes |
|---|---|---|
| Input System | [x.x.x] | New input system only — no legacy Input.GetKey |
| DOTween | [x.x.x] | All tweening goes through DOTween |
| [Your other packages] | | |

---

## Scene Structure

**Main scene load order:** Bootstrap -> Persistent -> [Level]
- `Bootstrap.unity` — initializes systems, loads Persistent additively
- `Persistent.unity` — always loaded: GameManager, AudioManager, EventSystem
- Level scenes — loaded/unloaded additively, never standalone

**When editing scenes:** Always make sure Bootstrap is the active scene in Play Mode testing.

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

**Current sprint goal:** [e.g. "Implement basic player movement and camera follow"]

**In progress:**
- [ ] [Task]

**Recently completed:**
- [x] [Task]

**Known issues / tech debt:**
- [Issue and rough location]

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
| [YYYY-MM-DD] | [e.g. "Chose URP over HDRP"] | [e.g. "Mobile target requires lower overhead"] |

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
