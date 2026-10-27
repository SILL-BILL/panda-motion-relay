# External development workspaces

Development Projects are intentionally stored outside this package repository.

Recommended sibling layout on every development machine:

```text
<workspace>/
├─ panda-motion-relay/                 # Git repository / authoritative UPM source
├─ panda-motion-relay-dev-unity6/      # Primary development, Unity CLI/MCP
├─ panda-motion-relay-dev-2022/        # Unity 2022.3.22f1 compatibility gate
└─ panda-motion-relay-vrchat-2022/     # World project created with VCC
```

For example, the shared workspace can be `F:/Unity` on this machine or a different location on a home PC. Keep the directory relationship the same.

Create ordinary development projects with the required installed Editor versions, and create the VRChat World project through VRChat Creator Companion (its official VPM CLI also provides project creation). Do not construct the VRChat SDK environment by hand.

In each external project's `Packages/manifest.json`, add:

```json
"com.sillbill.panda-motion-relay": "file:../../panda-motion-relay"
```

Unity's local paths are relative to the `Packages` directory containing the manifest. `file:../panda-motion-relay` would point inside the Unity project; `file:../../panda-motion-relay` reaches the sibling repository. Verify `PackageInfo.resolvedPath` rather than relying on the manifest text alone.

For automated package tests in the two development projects, include:

```json
"testables": ["com.sillbill.panda-motion-relay"]
```

Use Unity 6 for implementation and primary tests, then verify compilation and all current tests with Unity 2022.3.22f1. Verify SDK coexistence locally in the VCC World project when relevant. Do not claim VRChat upload/client verification from Editor-only checks.

**Do not create a Development Project inside panda-motion-relay and reference a parent that contains it as a local UPM package.** This relationship reproducibly breaks TimelineAsset MonoScript type association in Unity 6 / Timeline 1.8.13. Package repositories and consuming projects must be siblings or otherwise non-nested.

The external projects are local environments, not package source copies or Git repositories. Edit package code only in `panda-motion-relay`. Keep private models and personal scenes in each project's `Assets/LocalOnly` or `WorkSpace`; do not commit them or copy them into package source to satisfy tests. Never copy Library, Temp, Logs, UserSettings, credentials or local MCP configuration into this repository.

Unity local-path reference: https://docs.unity3d.com/6000.0/Documentation/Manual/upm-localpath.html
