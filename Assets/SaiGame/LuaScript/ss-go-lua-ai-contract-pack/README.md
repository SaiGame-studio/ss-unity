# ss-go Lua AI Contract Pack v1.0.6

Release date: 2026-07-11
Ticket: P2-T120

This is the self-contained package to send to studio members or AI-agent integrators. Copy or share this entire folder as one unit.

## Package Contents

- `CONTRACT.md` - Runtime contract for AI agents and humans.
- `AGENT_PROMPT.md` - Fixed prompt block for AI script generation.
- `.lua-libs/ss-go-game-api.lua` - EmmyLua API stub for editor autocomplete and AI context.
- `.luarc.json` - Lua Language Server workspace configuration.
- `.github/instructions/lua-scripting.instructions.md` - Copilot instruction file for `**/*.lua`.
- `VERSION` - Package version metadata.

## Recommended Use

For Studio UI AI generation:

1. Load `AGENT_PROMPT.md` before the studio member's request.
2. Append the expected payload schema and desired game logic.
3. Ask the model to return only one Lua script body.
4. Save and run the script through the normal ss-go script API.

For VS Code authoring:

Copy and paste the contents of this `ss-go-lua-ai-contract-pack-v1.0.6` folder into the root of the studio member's Lua workspace once. That single paste installs everything needed:

- `.github/instructions/lua-scripting.instructions.md` for Copilot on `**/*.lua`.
- `.luarc.json` for Lua 5.1 diagnostics.
- `.lua-libs/ss-go-game-api.lua` for `payload`, `ctx`, `output`, and `game.*` completion.
- `CONTRACT.md` and `AGENT_PROMPT.md` for AI prompt context.

After that paste, the workspace is ready for AI-assisted Lua authoring.

## Contract Source

This package is generated from the ss-go Lua Script Engine contract. The package must be updated whenever the runtime registers new `game.*` functions or changes sandbox constraints.
