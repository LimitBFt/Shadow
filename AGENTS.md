# AGENTS.md

Long-term project rules for Codex and other coding agents working on this Unity project.

- Use the project's current Unity version. Do not upgrade Unity unless explicitly approved.
- Put all custom assets under `Assets/_Project`.
- Use C# namespaces that begin with `ShadowPath`.
- Keep one main public class per file.
- Use `[SerializeField] private` for Inspector references.
- Do not repeatedly create Meshes, GameObjects, or arrays inside `Update`.
- Do not directly perform large YAML edits on Unity scenes or Prefabs.
- Do not modify generated folders such as `Library`, `Temp`, `Logs`, or `obj`.
- Do not install third-party Packages without approval.
- Handle only one clearly defined feature per task.
- Before modifying files, inspect the existing implementation and Git status.
- After modifying files, report changed files, test results, and any Unity Editor manual steps.
- Prefer EditMode automated tests for math algorithms.
- Do not claim tests passed unless they were actually run.
- Do not create Git commits unless explicitly requested.

