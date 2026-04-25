# Unity Client Development Rules

These rules are MANDATORY for all code contributions in this Unity project. They override generic "good-practice" defaults.

---

## A. General Workflow Rules

### A.1 Spec-First: No Out-of-Spec Code
- NEVER implement any feature, system, field, or behavior that is not explicitly described in the spec/task for the current work.
- If the spec says something does NOT exist, do NOT create it, even if useful.
- If unsure whether something is in scope, **ask the user first** rather than implementing it.
- This rule overrides any assumption of "good practice" or "helpful addition".

### A.2 Act Decisively — Don't Over-Ask
- When the user gives a task, DO IT. Do not stop to ask permission for obvious follow-up work (related cleanups, deleting orphan code/prefab references tied to removed scripts, fixing compile errors, deleting now-empty directories).
- Use judgment to pick the best approach and execute. Only ask when the answer is genuinely ambiguous AND the wrong choice would be costly to reverse (e.g., deleting shared prefabs/scenes, destructive asset operations, breaking public script API referenced by scenes).
- Acceptable without asking: removing orphan fields after a type is removed, deleting legacy dead scripts, removing orphan comments, fixing dead imports, deleting empty files/dirs (and their `.meta` files).
- NOT acceptable without asking: rewriting unrelated systems, bulk-renaming serialized fields (breaks scene/prefab references), bulk reformatting.
- Report what you did at the end — don't ask "should I also do X?" when X is clearly part of finishing the job.

### A.3 Evidence-Backed Explanations — No Fabrication
- Any claim about how a system, component, prefab, scene, or behavior works MUST be backed by a concrete code reference: `file_path:line_number` (or a line range).
- Format: use clickable markdown links, e.g. [SaiBehaviour.cs:12](Assets/SaiGame/Scripts/Common/SaiBehaviour.cs#L12). For ranges: `#L100-L130`.
- If a claim cannot be backed by code (Unity engine behavior, external API, user-provided info), state that explicitly — e.g. *"not verified in code"* or *"based on user description"*.
- If code was not read / grepped to confirm a claim, DO NOT state it as fact.

### A.4 Temporary Script Management
- Mark all temporary/migration/editor-only scripts clearly (e.g. prefix `_Temp` or folder `Editor/_Temp/`).
- Remind user to delete them after execution and provide an "End-of-Session Report".

### A.5 Mandatory Compliance Note
- Start or end every response with a brief "✅ Compliance" note, or "⚠️ Warning" if rules cannot be met.

### A.6 Language Restriction (Code and Comments)
- All source code, identifiers, inline comments, and technical documentation must be written in English only.
- Vietnamese (or any non-English language) is forbidden in code and comments.
- The only exception is dedicated multilingual localization/translation files.

---

## B. C# / Unity Code Rules

### B.1 One Top-Level Type Per File
- Each `.cs` file MUST contain exactly ONE top-level type (class / struct / enum / interface).
- Do NOT merge multiple classes or enums into the same file, even if they are small or closely related.
- File name MUST match the type name exactly (e.g. `QuestClaimRecord.cs` contains `QuestClaimRecord`).
- Nested types are allowed when they are genuinely private to the parent type.

### B.2 Namespace / Folder Alignment
- Namespace hierarchy should mirror the folder path under `Assets/SaiGame/Scripts/`.
- Keep scripts grouped by feature domain (e.g. `5_Quest/`, `3_ItemContainer/`), not by type (not `Models/`, `Responses/` at the top level).

### B.3 Serialization & Inspector Fields
- Prefer `[SerializeField] private` fields over `public` fields for inspector-exposed values.
- Public fields are only allowed on plain DTO / response / request classes (JSON mapping).
- Use `[Tooltip("...")]` for any field whose purpose isn't obvious from its name.
- Do NOT rename a `[SerializeField]` field without using `[FormerlySerializedAs("oldName")]` — it breaks existing scene/prefab references.

### B.4 Inspector Labels (Custom Editors / PropertyDrawers)
- Do NOT abbreviate labels in custom editors.
- If a label is long, write it in lowercase — do NOT UPPERCASE or truncate it to fit.
- Example: prefer `"quest definition status"` over `"QUEST DEF STATUS"`.

### B.5 Base Classes
- Use `SaiBehaviour` as the base for MonoBehaviours that need auto-reset/load-component hooks ([SaiBehaviour.cs](Assets/SaiGame/Scripts/Common/SaiBehaviour.cs)).
- Use `SaiSingleton<T>` for singleton MonoBehaviours ([SaiSingleton.cs](Assets/SaiGame/Scripts/Common/SaiSingleton.cs)).
- Do NOT roll your own singleton pattern — extend `SaiSingleton<T>`.

### B.6 Performance — Cache Component Lookups
- NEVER call `GetComponent<>()`, `FindObjectOfType<>()`, `GameObject.Find()`, or `Resources.Load()` inside `Update()` / `FixedUpdate()` / `LateUpdate()`.
- Cache references in `Awake()` / `Start()` / `LoadComponents()` (the `SaiBehaviour` hook).
- Prefer direct `[SerializeField]` references assigned in the inspector over runtime lookup.

### B.7 No Magic Strings for Unity Identifiers
- Do NOT hard-code scene names, tag names, layer names, animator parameter names, or `PlayerPrefs` keys as inline string literals scattered through code.
- Define them once as `const string` in a dedicated constants file (or as an enum where applicable).

### B.8 Logging
- Use `Debug.Log` / `Debug.LogWarning` / `Debug.LogError` appropriately — do NOT use `Debug.Log` for errors.
- Production code should not ship with noisy `Debug.Log` calls — wrap debug-only logs or remove them before milestone completion.

---

## C. Asset & Project Management Rules

### C.1 Meta Files
- ALWAYS commit `.meta` files alongside their assets. Never delete or ignore `.meta` files.
- When deleting an asset, delete its `.meta` file too.
- When moving/renaming an asset, move/rename its `.meta` file in the same commit.

### C.2 Scene & Prefab Changes
- Do NOT modify `.unity` scene files or `.prefab` files directly by text editing unless explicitly asked. Use the Unity Editor.
- Flag any unexpected scene/prefab diffs to the user before committing them.

### C.3 Asset References
- Prefer direct serialized references (drag-and-drop in inspector) over `Resources.Load` / `Addressables.LoadAsync` for assets used at scene startup.
- Reserve `Resources` / `Addressables` for genuinely dynamic loading.

---

## D. Testing Rules

### D.1 Tests Are Written On Demand
- NEVER write test files while implementing a feature unless the user explicitly asks.
- When asked to write tests, use Unity Test Framework (NUnit-based).

### D.2 Test Location
- Edit Mode tests: `Assets/Tests/EditMode/`
- Play Mode tests: `Assets/Tests/PlayMode/`
- Each test folder MUST contain an `*.asmdef` assembly definition configured for the appropriate test mode.
- NEVER place test files in the same directory as source scripts.

### D.3 Test Naming
- Test file names: `{FeatureUnderTest}Tests.cs` (e.g. `QuestClaimRecordTests.cs`).
- Test method names: `MethodUnderTest_Scenario_ExpectedResult` pattern.

---

## E. Reporting

### E.1 Compliance Check Footer
- Include a brief "✅ Compliance" or "⚠️ Warning" note at the start or end of every response.
- State the Ticket / Task ID source when available. If not found, ask the user.

### E.2 End-of-Task Report
- After completing a task, briefly list: files changed, assets touched, any temporary scripts to clean up, and any follow-ups the user should be aware of.
