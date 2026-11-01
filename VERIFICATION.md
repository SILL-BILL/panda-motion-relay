# Production Preview / UPM Clean Installation Verification

Verified on 2026-11-01 (Asia/Tokyo), preparation branch `chore/production-preview-prep`, based on `a8003ec7347d5c55b805af47c623cfaa49cf6ea3`. Readiness: **READY WITH LIMITATIONS** for Production Preview / Internal Production Use. Documentation is prepared locally and remains uncommitted/unpushed for maintainer review; no new release/tag or main merge.

## Package and documentation audit

`com.sillbill.panda-motion-relay`, display name Panda Motion Relay, version 0.1.0, minimum Unity 2022.3.22f1, Timeline dependency 1.7.6, MIT license and author Gonsaku are retained without modifying package.json. Runtime/Editor separation and Editor-only Tests assembly definitions are unchanged. No VRC SDK, Unity MCP or development-tool dependency is introduced.

Required package structure, README, CHANGELOG and LICENSE are present. `Development~/` contains setup documentation rather than a Unity project. The delivered Git package contained 74 files with no LocalOnly/WorkSpace, development project, private model, UnityPackage, image/video evidence, test XML, logs or local configuration. Repository Git metadata is not an installed package asset. The new `Documentation~/Manual.md` is plain package documentation: local import did not create `Documentation~.meta`; no speculative `.meta` files were added.

README now serves as the overview, status, installation and Quick Start entry. Its old nested-development-project instruction and pre-bake status text were replaced; useful detailed behavior, script sampling notes and development references were retained in the Japanese 20-chapter Manual. The Manual explains terms, generated-independent user setup, Source/Target selection, exact cuts, root transfer, Local/Global bake/replay, nested evaluation, VRChat workflow and troubleshooting. Links to local files were checked. No private production character names or credentials are used in the Manual.

## Clean installation and minimum smoke

Four new, non-Git Unity projects were created outside the package repository, with no VRC SDK, MCP or private test assets. Actual UPM requests used `UnityEditor.PackageManager.Client.Add`: local `file:` requests exercise the backend used by Add package from disk; HTTPS requests exercise Git package installation. The Package Manager GUI dialogs themselves were not clicked during these batch tests.

| Clean project / Unity | Installed request | Resolved source / Timeline | Result |
| --- | --- | --- | --- |
| Local / 6000.0.84f1 | Local repository folder (`file:`) | Local, version 0.1.0 / Timeline 1.8.13 | PASS |
| Local / 2022.3.22f1 | Local repository folder (`file:`) | Local, version 0.1.0 / Timeline 1.7.6 | PASS |
| Git / 6000.0.84f1 | HTTPS Branch URL below | Git commit a8003ec / Timeline 1.8.13 | PASS |
| Git / 2022.3.22f1 | HTTPS Commit URL below | Git commit a8003ec / Timeline 1.7.6 | PASS |

Both local requests resolved PackageInfo.resolvedPath to the sibling package repository, not a copied package source. Git requests resolved into each project's Library/PackageCache; packages-lock recorded the intended commit. Compile errors 0 in final runs. Each of the four projects invoked the Panda bake menu and constructed its Bake Window, created the Timeline/Relay Track, bound a generated Humanoid Target, assigned two generated Humanoid Sources, and verified Play Mode source-root cuts at frames 23/24/25 and 47/48/49 with Target active. Each generated Local and Global bake successfully. Normal-use smoke ran **without testables**; users do not need the test setup below.

Verified exact Git URLs:

```text
https://github.com/SILL-BILL/panda-motion-relay.git#feat/bake-evaluation-director
https://github.com/SILL-BILL/panda-motion-relay.git#a8003ec7347d5c55b805af47c623cfaa49cf6ea3
```

HTTPS read without credentials was also verified. Branch versus Commit formats are documented only after successful UPM installs; SSH installation was not tested. These URLs currently deliver the approved checkpoint's code and its older documentation. The new Manual will not appear in a remote package until the maintainer approves a later documentation commit/push. No claim is made that the uncommitted prep branch has been published.

## Clean automated tests and build isolation

After the normal-use smoke, the two local clean projects explicitly added Test Framework and `testables` for developer verification only.

| Environment | Framework resolved | Tests | Process / final Console |
| --- | --- | --- | --- |
| Clean Unity 6000.0.84f1 | 1.6.0, editor builtin (manifest request 1.4.6) | 119 passed, 0 failed/skipped | Exit 0; errors 0 / warnings 0 |
| Clean Unity 2022.3.22f1 | 1.1.33 | 119 passed, 0 failed/skipped | Exit 0; errors 0 / expected headless warning 1 |

Both XML files contain 119 passing leaf cases. The Unity 2022 warning corresponds to the headless GfxDevice ambient/reflection-probe notice. Early install-helper compilation required qualifying PackageInfo to avoid two Unity types with that name; the first Unity 6 smoke helper also emitted a deprecated FindObjectOfType warning. These were external verification-helper issues, not package source failures; later Git smoke used the supported FindObjectsByType API. No package implementation change was needed.

CompilationPipeline's Player assembly list excluded both Panda Editor and Tests assemblies in all four normal-use projects. This verifies assembly selection, not a standalone Player build/export. No private asset is needed by the generated smoke or package tests.

## Manual usability and VCC read-only check

The agent reproduced the Manual's single-Timeline setup sequence using generated Humanoid rigs and public Unity/package APIs, without using private scenes or previous development notes as setup instructions. The steps covered named Driver/Target objects, normal Animation Tracks, Relay Binding/Source, the Manual's 24fps three-clip sequence, root transfer, Play Mode cuts, Bake Window and both bake spaces. This assisted walkthrough passed in the four clean projects. It prompted explicit Manual instructions for selecting the Timeline Asset's Frame Rate and distinguishing seconds from frames, selecting the Humanoid Animator rather than a root-only Animator, and preparing Generic/frozen replay hierarchies.

This is an **API-assisted walkthrough**, not an independent novice user study or an end-to-end mouse/keyboard GUI test. Menu registration/window construction were exercised; complete visual interaction with every Package Manager/Timeline inspector control remains a manual acceptance check. This is the readiness qualification rather than a package-code blocker.

The existing VCC project's Package Manager cache was inspected read-only: version 0.1.0, Local source, resolved to the same repository, errors empty, package not testable by default. Panda Runtime/Editor assemblies are present, and the prior SDK 3.10.6 / ClientSim baked-output inspection Console remains errors 0. No new VCC runtime smoke, SDK/settings modification or claim of direct VCC Relay Preview compatibility was introduced by this preparation.

Evidence remains in each external clean project's WorkSpace: install request/result, smoke setup/result, UPM lock data, logs and full local-project test XML. The Japanese Manual and README are the user instructions; this section records the actual automation scope. Existing limitations, including the non-blocking VCC frame-138 Relay Preview discrepancy, remain applicable. VRChat upload/client, Quest and full PhysBone simulation remain unverified.

---

# Phase 3C Evaluation Director verification

Verified on 2026-10-31 (Asia/Tokyo). Implementation on `feat/bake-evaluation-director`, based on `e598a7953e3c6f49d5bb6f9e7f41818bcf435d7a`, subsequently checkpointed as `1f6d4ad7794bf62cb3c57240f61694eb42e889f4`. The original verification below describes the implementation before that checkpoint; later cross-version and VCC results follow in the next section.

## Design and changed files

- `Editor/Bake/HumanoidGenericBaker.cs`: adds optional `GenericBakeRequest.evaluationDirector`. Existing `director` remains the Relay Director for track ownership, target binding, Source exposed references, frame rate and bake range. The Evaluation Director defaults to it.
- `Editor/Bake/BakeEvaluationScope.cs` and its `.meta`: inspect enabled ControlTracks with `ControlPlayableAsset.updateDirector`, resolving sources against the Evaluation Director. Exactly one direct clip must control the Relay Director. Map each Relay time with `control.start + (relayTime - control.clipIn) / control.timeScale` and validate the entire range before evaluation.
- `Editor/Bake/BakeDirectorState.cs` and its `.meta`: snapshot the Evaluation Director and its directly controlled Directors; restore clocks, update modes, playback states, graph validity and root-playable speeds. Restore parents before children, rebuilding a previously live child graph if parent teardown destroyed it. Discard deferred pose writes after restoration, then restore the existing scene snapshot.
- `Editor/Bake/GenericBakeWindow.cs`: exposes Relay Director and optional Evaluation Director without adding a mapping mode.
- `Tests/Editor/EvaluationDirectorBakeTests.cs` and its `.meta`: 31 new generated-rig cases, alongside the existing 88 cases.
- `README.md`, `CHANGELOG.md`, `VERIFICATION.md`: usage, scope and measured verification.

Only the Evaluation Director advances during sampling. Each evaluation still yields one player-loop frame for native constraints before `ApplyPendingPoses()` and target sampling. Local/Global curve math, quaternion continuity, cut tangents and runtime relay code are unchanged.

## First sample and restoration

The earlier diagnostic recorded a null mixer Source at frame 0. That symptom did not reproduce in the generated nested bake regression: with all three graphs stopped, the target deliberately moved to an incorrect pose and the Control Clip activation enabled, the first sample selects the expected actual mixer Source and matches the reference pose. The real-data bake also selects `Yanagi_001` at frame 0. No frame is skipped or shifted, and no additional baker priming pass was necessary. The reference capture initializes its graph at time 0; the baker itself retains one Master evaluation and one native-frame wait per sample.

Normal completion, cancellation and an injected sampling exception verify all three Directors' time, update mode, play state and graph validity plus target scene restoration. Additional cases cover initially invalid/mixed graphs and initially playing Directors. An early mixed-graph test exposed delayed child graph destruction during parent teardown; restoration now rebuilds the original child graph after that teardown has drained.

## Automated verification

| Environment | Compilation | Full test result | Final Console |
| --- | --- | --- | --- |
| Unity 6000.0.84f1 / Timeline 1.8.13 | Passed | 119 passed, 0 failed, 0 skipped | Errors 0, warnings 0 |
| Unity 2022.3.22f1 / Timeline 1.7.6 | Passed | 119 passed, 0 failed, 0 skipped; process exit 0 | Errors 0, expected headless warning 1, unexpected warnings 0 |

Unity 6 completed first, including the real-data smoke, followed by the Unity 2022 compatibility gate. Both full XML files contain all 119 passing leaf cases; the 31 new cases are in addition to the retained 88. Unity 6 used the connected Editor/Unity CLI, and Unity 2022 used `-batchmode -nographics -runTests -testPlatform EditMode`. No Unity 6-only package API was introduced.

The Unity 2022 warning was confirmed by a targeted Console-message probe: `GfxDevice renderer is null`, so Unity cannot update ambient/reflection probes under `-nographics`. It is an expected rendering-environment warning, not a package/test warning. The initial cold incremental build briefly referenced its old source list before discovering the two new Editor files; Bee automatically reran the build successfully in the same invocation. The final Console had no compile errors, and the subsequent targeted probe compilation was also clean. No Library deletion was needed.

Unity 6's CLI history buffer still contains earlier diagnostic events; the final live Console counts above come from its `groundTruth` counters, not that history. A local test observer preserves the complete XML across Play Mode domain reloads; the package tests do not depend on the observer. Unity 2022 evidence is in the external project's `WorkSpace/phase3c-tests.xml`, `phase3c-tests.log`, `phase3c-console.txt` and `phase3c-console-messages.txt`.

The 31 added cases cover default/single Director behavior; start offset, clipIn, speed and combined time mapping; unsafe mapping rejection; sibling Prop-driven ParentConstraint; cuts; actual frame-0 Source; Local/Global playback; and normal/cancel/exception/playing/graph restoration. The Constraint regression deliberately demonstrates a difference above 1 unit when only the Character child is evaluated, then checks Master evaluation against bake samples. All rigs and clips are generated; tests do not depend on private production assets.

An intermediate single-Director test failed because its fixture evaluated the Master between its child-only reference and bake, moving the sibling Prop input. Removing that unrelated fixture evaluation restored the intended comparison; no production sampling change was needed for that failure. Earlier failed runs and individual reruns are retained locally and are not the final full-suite result.

## Real-data Local and Global smoke test

Unity 6000.0.84f1, copied scene `Assets/LocalOnly/MotionRelaySmoke/01_RelayValidation.unity`. Original imported scene and Timelines were not edited. Relay Director: `TL_Test_Character`; Evaluation Director: Master `TL_Test`; sibling Prop Director: `TL_Test_Prop`; target Humanoid Animator: `Yanagi_Bake`. Relay Motion Root enabled. The unique direct Control Clip has start 0, clipIn 0, speed 1.

Compared 375 samples at 24 fps (frames 0 through terminal frame 374), measuring the same target's Hips, Head, both Hands, both Feet and LeftIndexDistal world position and rotation. These are target-preview versus target-bake comparisons, not source/target retargeting differences. The earlier child-only diagnostic measured 8.00353 units / 179.34377 degrees maximum error; the new Master-scoped sample comparison is zero.

| Measurement | Local | Global |
| --- | --- | --- |
| Maximum sampling position error, Unity units | 0 | 0 |
| Maximum sampling rotation error, degrees | 0 | 0 |
| Maximum clip replay position error, Unity units | 0.000000999156 | 0.00000953731 |
| Maximum clip replay rotation error, degrees | 0 | 0 |
| Scale curves | 0 | 0 |
| Unmapped / secondary-bone curves | 0 | 0 |
| Animator-root curves | 7 (position + quaternion) | 0 |
| Unexpected Source selections, frames 0–373 | 0 | 0 |
| All three Director states restored | Yes | Yes |
| Result (position < 0.001, angle < 0.1 degrees) | PASS | PASS |

Local replay uses an independent Generic hierarchy under the same evaluated parent; Global uses an independent hierarchy frozen at the first-sample parent/root pose, with no ancestor animation. Global was performed only after the Local sampling/replay/curve/state checks passed. Both generated clips are new local assets, not overwrites of the previous failed smoke clip.

The ParentConstraint-driven interval, including frame 144 where the earlier discrepancy began at `ConSrc`, matches Master preview. Actual mixer ownership is Normal (`Yanagi_001`) at frames 0 and 137, Float (`Yanagi_001_RD`) at 138 and 239, and Normal at 240. Terminal frame 374 has no active Relay Source, consistent with the existing half-open interval rule. There is no hidden blend or source timing change.

Local evidence in the external Unity 6 project's `WorkSpace/phase3c/`: complete test XML/event log, `reference-frame-zero.json`, `Local.json`, `Global.json`, `status.txt`, and the local smoke harness. Generated clips: `Assets/LocalOnly/MotionRelaySmoke/Baked/Phase3C_Local.anim` and `Phase3C_Global.anim`. These private/local artifacts are outside the package repository.

## Limits and repository safety

This phase supports one unambiguous direct Master-to-Relay Control Clip. It rejects missing/multiple clips, ambiguous hierarchy search, prefab-instantiated Directors, multilevel control, nonpositive/nonfinite speed, out-of-range mapping and child-time wrapping. Arbitrary nested traversal, split Control Clips, custom mapping UI, key reduction, Generic mapping and blending remain outside scope. Global still requires the equivalent frozen reference hierarchy; arbitrary nonuniform/sheared hierarchy reproduction is not claimed.

No runtime/package dependency, minimum Unity version or VRChat dependency change. This phase does not claim VRChat upload/client verification or real PhysBone simulation. The real-data smoke was performed in Unity 6; the Unity 2022 gate uses generated automated tests.

Final repository check: only the five modified source/document files and six new source/meta files listed above; index empty, no LocalOnly/WorkSpace/private assets staged, `git diff --check` passed, HEAD unchanged at the baseline. Unity 6 is left outside Play Mode with the copied validation scene and Bake Window open, Relay set to Character and Evaluation set to Master.

---

# Cross-version real-world bake and VCC baked-output verification

Verified on 2026-11-01 (Asia/Tokyo), using Phase 3C checkpoint `1f6d4ad7794bf62cb3c57240f61694eb42e889f4`. This follow-up changes documentation only. Runtime, Editor, Tests, package metadata and dependencies are unchanged; the existing 119/119 passing automated results in each Unity version are reused without rerunning the suite for documentation.

## Standard VRChat workflow

Perform production Preview, Local/Global Bake and numerical Generic replay verification in Unity 6 or standard Unity 2022.3.22f1. Bring the verified AnimationClips into the VCC-created project and inspect the baked outputs with ClientSim ON. VCC is the baked-deliverable inspection environment; direct VCC Relay Preview/Bake is outside the standard workflow guarantee.

## Production-like bake results

The normal Unity 2022.3.22f1 project used the same production-like UnityPackage as the cross-version smoke, preserving imported originals and working on LocalOnly copies. Master-scoped Character/Prop evaluation, exact cuts, frame-144 ParentConstraint synchronization and all three Director states restored: PASS. The table includes the earlier Unity 6 results for comparison. Errors are maximum world-position differences in Unity units; all listed sampling/replay rotation differences are 0 degrees.

| Environment | Mode | Sampling position error | Clip replay position error | Result |
| --- | --- | --- | --- | --- |
| Unity 6000.0.84f1 | Local | 0 | 0.000000999156 | PASS |
| Unity 6000.0.84f1 | Global | 0 | 0.00000953731 | PASS |
| Standard Unity 2022.3.22f1 | Local | 0 | 0.000000547960 | PASS |
| Standard Unity 2022.3.22f1 | Global | 0.000000562309 | 0.00000962149 | PASS |

The standard Unity 2022 smoke ended with Console errors 0 / warnings 0; the batch Editor log also contains the expected headless rendering notice. Both modes retain scale/secondary exclusion, with Local Animator-root curves 7 and Global root curves 0.

## VCC final baked-output inspection: PASS

Environment: Unity 2022.3.22f1, VRC SDK Base + Worlds 3.10.6, ClientSim ON. Only the Local/Global `.anim` and `.meta` files marked PASS by the normal Unity 2022 cross-version smoke were copied into `Assets/LocalOnly/MotionRelayFinalInspection/Baked/`. Source and copied files remained hash-identical. No rebake or curve editing was performed in VCC.

The inspection scene retained VRCWorld and VRC Scene Descriptor. Copied authoring Timelines contained no Relay tracks. Local replay used the matching animated parent hierarchy; Global used a frozen first-reference hierarchy without adding original ancestor motion. Generic characters did not undergo a Humanoid retarget pass.

Compared 375 frames at 24 fps (0–374) against the normal Unity 2022 PASS references, measuring Hips, Head, both Hands, both Feet and LeftIndexDistal world pose. Both clips were replayed programmatically with `AnimationClip.SampleAnimation`; Global additionally passed standard Generic Animator Controller playback, with `Animator.isHuman == false` and no `SampleAnimation` in that additional check.

| VCC replay check | Max world position difference | Max rotation difference | Result |
| --- | --- | --- | --- |
| Local, matching animated parent | 0.000000547960 | 0 degrees | PASS |
| Global, frozen reference parent | 0.00000962149 | 0 degrees | PASS |
| Global, native Generic Animator Controller | 0.00001519077 | 0 degrees | PASS |

| Curve binding scope (read-only inspection) | Local | Global |
| --- | --- | --- |
| Animator-root curves | 7 | 0 |
| Scale curves | 0 | 0 |
| Secondary / unmapped curves | 0 | 0 |

Rendered checkpoints 0, 137, 138, 144, 239, 240 and 350 cover normal, entry, Float movement, exit and return poses. Representative images showed no large body-pose break; numerical comparisons cover world trajectory and double-motion errors. ClientSim initialized and maintained Play Mode, with no SDK compile conflict or major playback error. Each clip's Console recorded errors 0 and warning 1: the VRC Scene Descriptor Reference Camera was unset. This is a VRChat scene setup warning, not a Panda Motion Relay warning. Native playback logs also included duplicate AudioListener notices from the inspection scene; scene listeners were disabled afterward so ClientSim supplies its own. That audio-only cleanup did not alter the tested animation data.

ClientSim remained ON and its complete settings matched before/after. Read-only hash monitoring found no new changes to original Test assets, ProjectSettings, SDK/package files (including the previously monitored WorldQualitySettings sample asset), source baked clips or imported clips. No manual repair or rollback was performed. Inspection scenes, copied Timelines, controller, camera helpers, reports and images remain outside the repository in the VCC project's LocalOnly/WorkSpace directories.

## Known VCC Relay Preview discrepancy and limits

Direct Relay Preview in VCC observed a frame-138 Driver-root versus Target-root position difference of 0.001580238 units, rotation difference 0 degrees. It also reproduced with ClientSim OFF, so it is not established as ClientSim-ON-specific. The cause is unresolved and no fix is claimed. It is a known, non-blocking limitation for the standard workflow above, which uses verified baked outputs for VCC inspection. It remains relevant if direct VCC Relay Preview/Bake is required in the future.

The baked-output PASS does not claim that direct VCC Relay Preview/Bake passed. It also does not claim VRChat upload, client/hardware verification, Quest build/upload or full PhysBone simulation. Secondary-motion verification here means only that baked clips contain no secondary/unmapped curves.

Local evidence: standard Unity 2022 project's `WorkSpace/cross-version-smoke/` and VCC project's `WorkSpace/final-baked-inspection/` (provenance/hashes, numerical results, curve counts, controller result, Console captures, settings equality, monitored hashes and rendered images). The earlier direct-Relay attempts are retained separately in `WorkSpace/vcc-smoke-restart/`, `vcc-bake-off-verify-on/` and `vcc-sdk3106-smoke/`. These artifacts are not repository content.

---

# Phase 3B Bake Space verification

Verified on 2026-10-30 (Asia/Tokyo). Implementation on `feat/bake-space-local-global`, based on save point `87dee935`. The results below describe the approved Phase 3B implementation before its checkpoint.

## Contract

- Default request and window selection: Local. The existing Local root/hips/other-bone sampling paths are retained.
- Global freezes the Hips parent's world-to-local matrix and world rotation at the first fully evaluated sample of the Relay bake range, after the existing Constraint/Relay apply point. It does not use the Director's pre-bake pose or update the reference on subsequent frames.
- Global writes Hips position transformed by the fixed matrix and quaternion rotation relative to the fixed parent rotation, using standard local Transform curves. Other mapped bones still receive local quaternion rotation only.
- Global omits separate Animator-root curves even with Relay Motion Root enabled; preview evaluation still uses the track setting normally.
- Quaternion continuity, protected cut tangents, gap sampling, overlap rejection, scale/secondary-bone exclusion and restoration/cancellation remain in the existing pipeline.
- Replay requires the equivalent frozen first-sample parent hierarchy. Do not replay the original ancestor motion simultaneously. Arbitrary output roots, custom reference transforms and animated/nonuniform scale or shear reproduction are not provided.

## Tests

The 67 existing cases are unchanged in outcome. Added 21 cases: default/invalid mode, overlap rejection, Local curve behavior with/without separate root, Global curve scope, upper-parent translation and rotation, a nonzero first-sample reference, Local/Global numeric comparison on the same Timeline, frozen-hierarchy Hips world replay, root-motion double-transfer prevention, cut frames, native ParentConstraint, scale/secondary-bone exclusion, scene restore, gap and cancellation.

The generated comparison hierarchy is AnimatedParent → Character Animator → Armature → Hips. It animates the ancestor and Humanoid hips, and uses a non-identity Armature offset plus positive uniform scales. Each Global frame is replayed on an independent Generic hierarchy frozen at the first captured root pose. World Hips error must be below 0.001 m and 0.1 degrees. Other mapped-bone local rotations are checked independently. The Local/Global comparison verifies both bake modes sample the same final motion while producing different Hips curves, with parent-motion differences exceeding 1 m and 20 degrees. The Global cut case explicitly checks frame 23, frame 24 (incoming source), frame 25 and the sub-frame interval immediately before the cut at 24 fps.

| Environment | Result |
| --- | --- |
| Unity 6000.0.84f1 / Timeline 1.8.13 | 88/88 passed, 0 failed/skipped; Console Error 0 / Warning 0 |
| Unity 2022.3.22f1 / Timeline 1.7.6 | 88/88 passed, 0 failed/skipped; Console Error 0 / Warning 0 |

Verified in the external sibling development projects; no package manifest or dependency changes. No private model or licensed asset was used. The connected MCP initially pointed to the separate test-data project, so writes and execution used Unity CLI with the primary project's explicit path. A temporary LocalOnly test-result observer exported the complete Unity 6 XML across domain reloads and was then removed; package tests do not depend on it.

Initial new Play Mode test wrappers failed during their own domain-reload initialization. Moving EnterPlayMode/ExitPlayMode into each test's iterator, matching the existing tests, resolved this. The complete corrected run passed all 88 cases; earlier diagnostic results are retained locally without being counted as the final verification.

Evidence: `Development~/LocalOnly/Verification/Phase3B/` (complete Unity 6 XML, CLI summary, UI state, Console samples and compatibility results).

# External development layout verification (historical)

Verified on 2026-10-27 (Asia/Tokyo). Phase 3 was approved and checkpointed as `5bfaa9b2d33728b2db8e7bd7082b620b4673097f` (`feat: add humanoid-mapped generic bake`). Migration work is on `chore/externalize-development-projects`.

Consuming Unity projects now use independent sibling directories outside the package repository. The repository is the only package source; the local dependency is `file:../../panda-motion-relay`, relative to each project's `Packages` directory. PackageInfo confirms the resolved source is the repository itself.

| External environment | Initial validation |
| --- | --- |
| Unity 6000.0.84f1 / Timeline 1.8.13 | Compilation passed; 67/67 tests passed; current Console Error 0 / Warning 0 |
| Unity 2022.3.22f1 / Timeline 1.7.6 | Compilation passed; 67/67 tests passed; Console probe Error 0 / Warning 0 |
| VCC-created World / Unity 2022.3.22f1 / SDK 3.10.5 | Panda and VRC SDK Base/Worlds loaded; compilation passed; Console probe Error 0 / Warning 0 |

The external Unity 6 project returned TimelineAsset from MonoScript.GetClass. A fresh LocalOnly Timeline loaded as both TimelineAsset and PlayableAsset with Missing Script Count 0, appeared in the Director Object Picker's visible candidate list, and opened normally in Timeline with an Animation Track. The Phase 3 Bake Window opened successfully. The Unity CLI Pipeline server is reachable at the external project's path; machine-level MCP configuration was not changed.

The VCC World project was created with Creator Companion's official VPM CLI and official World template, not by assembling SDK files manually. This is local Editor SDK coexistence verification only. **Actual VRChat upload and client-side verification have not been performed.**

Detailed evidence and post-removal verification results are recorded locally in `Development~/LocalOnly/Verification/PermanentExternalMigration/`. Older evidence below describes the historical nested layout; its project asset paths are not current development setup instructions.

After removing all three old nested projects, Unity 6 and Unity 2022.3.22f1 were each tested again: **67/67 passed, 0 failed, 0 skipped** in both environments. A second fresh Timeline created after removal again had valid type association, Missing Script Count 0 and a Director picker entry; Timeline and the Phase 3 Bake Window opened successfully. Runtime, Editor, Tests and package.json remain unchanged from the Phase 3 checkpoint.

# Phase 3 verification (historical)

Verified on 2026-10-27 (Asia/Tokyo).
Approved Phase 2B was checkpointed locally as `eaefef65b3cce5664ff4cf171dbc5a6d123c5b29` (`feat: add optional motion root relay`), without pushing.
The following describes the Phase 3 validation before its approved local checkpoint.

| Environment | Compilation | Tests | Exit/result |
| --- | --- | --- | --- |
| Unity 6000.0.84f1, Timeline 1.8.13 | Passed | 67 passed, 0 failed, 0 skipped | Connected Editor / Unity CLI |
| Unity 2022.3.22f1, Timeline 1.7.6 | Passed | 67 passed, 0 failed, 0 skipped | Batch process exit code 0 |

All 47 previous cases remain, plus 20 Phase 3 cases. The test rig optionally creates mapped finger bones, including Left Index Proximal, without any private or licensed model dependency.
The final Unity 6 full-run summary is saved locally as `phase3-unity6-results.json`; 2022's XML independently records all 67 cases.
Final Unity 6 status after smoke cleanup: compilation successful, Play Mode stopped, Console errors **0**, warnings **0**. Expected invalid-configuration warnings are asserted by tests; no unexpected warning failed either run.

## Implemented bake contract

- Entry point: `Tools > Panda Motion Relay > Bake Humanoid to Generic`. Select Director and Relay Track, then choose a new `.anim` path with SaveFilePanelInProject.
- Bake runs in unpaused Play Mode on a normal coroutine. Each Timeline frame is evaluated, then the next Update samples the target after native constraints and Relay LateUpdate. It does not wait on rendering/Game View visibility.
- Reuse the preview graph so AnimationTrack scene offsets/defaults are not reset. After sampling, restore the original clock, update mode and play state, discard that Director's queued Relay writes, and restore scene transforms/parents, active state, Renderer and Animator enabled state.
- Bake range is the first selected Relay Clip start through the last end, inclusive of the ending frame. Clip time zero corresponds to the range's first frame. Timeline frame rate is used directly; boundaries must be frame-aligned.
- Enumerate only `Animator.GetBoneTransform(HumanBodyBones)` mappings. Use `AnimationUtility.CalculateTransformPath` relative to Target Animator.transform.
- Hips: local position and quaternion rotation. Other mapped body/finger bones: quaternion rotation only. Optional Animator root: world position/rotation converted to target-parent local curves. No scale, secondary-bone or Euler curves.
- Keep every sampled frame. Maintain quaternion sign continuity, call EnsureQuaternionContinuity, then apply Constant right tangents to the frame before ownership boundaries. No reduction/compression/blend.
- Gaps record the actual sequentially evaluated target state without filling from a source. Overlap, invalid/missing Director/Timeline/target/source, ambiguous mapped paths, non-frame-aligned boundaries and invalid/existing output paths are refused before sampling.
- Existing output assets are protected by both initial validation and a second check immediately before CreateAsset. Tests verify existing GUID and file bytes remain unchanged.

## Replay verification and errors

Tests observe the evaluated target Transform hierarchy at each bake frame, then stop Relay and sample the saved Generic clip independently on an identical hierarchy configured for Generic (Avatar unset). Root, hips, head, hands, feet, mapped fingers and secondary-child world/local poses are compared at every frame, including A→B→A and cut-adjacent/sub-frame positions.

| Scenario | Unity 6 max world position error | Unity 2022 max world position error | Max quaternion angular error |
| --- | --- | --- | --- |
| Mapped body/fingers and exact cuts | 0.000000287 m | 0.000000246 m | 0° |
| Motion Root + non-Identity/scaled Target Parent + native ParentConstraint | 0.000004769 m | 0.000004769 m | 0° |
| Internal gap | 0.000000584 m | 0.000000584 m | 0° |

Assertions use position epsilon 0.001 m and quaternion angular epsilon 0.1°. All stored curve frames, scale/secondary exclusions, default/optional root behavior, frame rate, new asset creation, scene/Director restoration and cancellation cleanup passed.
The native ParentConstraint is evaluated in Play Mode from an Editor coroutine, not approximated by a custom solver.

## Unity 6 Editor smoke / visual check

Performed through live Editor/Unity CLI operations on a temporary generated scene with an actual PlayableDirector and Timeline: Driver A → Driver B constrained to an animated vehicle anchor → Driver A. Opened the bake window, generated a 3-second / 30fps clip using the same Editor bake runner, stopped Relay, and sampled the generated clip alone on the target hierarchy configured for Generic.

- Generated clip: 94 Transform curves, 91 keys per curve, zero scale curves and zero secondary/mesh-child curves.
- Cut check: frame 29 root `(0,0,0)`; frame 30 approximately `(1.37,0,-0.10)` from the constrained animated anchor; frame 60 returns to `(0,0,0)`. These are authored source offsets, without automatic correction.
- Preview versus standalone baked frame 30: max world position difference `0.000000632 m`, max rotation difference `0°`; the teal body/limbs and magenta secondary-hair marker match visually in paired Game View PNGs.
- Director time restored to `1.0` after bake. Play Mode was subsequently stopped; the original SampleScene remained active with two roots and `isDirty=false`.
- This was an agent-operated Editor smoke using generated primitives, not a production-character or VRChat client test.

Local-only smoke evidence:

```text
Development~/LocalOnly/Verification/phase3-manual-smoke.json
Development~/panda-motion-relay-dev-unity6/Assets/LocalOnly/Phase3Smoke/Recorded.anim
Development~/panda-motion-relay-dev-unity6/Assets/LocalOnly/Phase3Smoke/preview-b.png
Development~/panda-motion-relay-dev-unity6/Assets/LocalOnly/Phase3Smoke/baked-b.png
Development~/panda-motion-relay-dev-unity6/Assets/LocalOnly/Phase3Smoke/baked-return-a.png
Development~/LocalOnly/Verification/phase3-unity2022-results.xml
Development~/LocalOnly/Verification/phase3-unity2022-tests.log
Development~/LocalOnly/Verification/phase3-all-roundtrip-errors.txt
```

A temporary LocalOnly test-result observer was used to export complete results across domain reloads, then removed. Test behavior and production code do not depend on it.

## Limits

The output is a target-specific **Generic Transform** clip, not a Humanoid muscle clip. Replay requires the same hierarchy with Generic configuration (no Humanoid retarget pass). During diagnosis, sampling through a Humanoid Avatar added about 0.0077 m of hips translation despite the saved curve containing the correct value; Generic replay removes that extra conversion. Bake never changes the original target's Avatar.
Unpaused Play Mode is required for reliable native Constraint evaluation. Pure Edit Mode Constraint baking is not implemented. If the target parent moves, its motion must also be reproduced on playback. Arbitrary script/physics internal state is not rewound; only the captured scene/Director properties are restored.
Current limits are 240 fps and 100000 frames. Key reduction, curve compression, custom FPS UI, Generic rig mapping/scale transfer, relative offsets, Blend, batch baking, mesh helpers and VRChat-specific paths remain out of scope. No production asset or VRChat upload/client verification was performed.

## Authored files

```text
.gitignore
Editor/Bake/HumanoidGenericBaker.cs (+ .meta)
Editor/Bake/BakeSceneState.cs (+ .meta)
Editor/Bake/GenericBakeWindow.cs (+ .meta)
Runtime/Timeline/MotionRelayEvaluation.cs
Runtime/Timeline/MotionRelayMixer.cs
Tests/Editor/GenericBakeTests.cs (+ .meta)
Tests/Editor/HumanoidRelayTests.cs
README.md
CHANGELOG.md
VERIFICATION.md
```

The existing automatic InputSystem_Actions.inputactions.meta change is outside the authored Phase 3 diff. LocalOnly/WorkSpace (and their folder metadata) are ignored, generated private test clips are not staged, and no Phase 3 files are staged. The authored diff passes `git diff --check`.

---

# Phase 2B verification (historical)

Verified on 2026-10-27 (Asia/Tokyo).
Phase 2A was approved and checkpointed locally as `2839534` (`feat: add exact-cut multi-source motion relay`). That commit was not pushed.
Phase 2B is on `feat/phase2-motion-root`, uncommitted and unpushed pending maintainer review.

| Environment | Timeline | Compilation | Tests |
| --- | --- | --- | --- |
| Unity 6000.0.84f1, primary development project | 1.8.13 | Passed | 47 passed, 0 failed, 0 skipped |
| Unity 2022.3.22f1, compatibility development project | 1.7.6 | Passed | 47 passed, 0 failed, 0 skipped |

The 29 prior cases are retained; `Tests/Editor/MotionRootRelayTests.cs` adds 18 cases.
Unity 6 live inspection, compilation and tests used Unity CLI because Unity MCP tools were not exposed in this chat.
Unity 2022.3.22f1 ran the same assembly in batch mode and exited with code 0. Its XML explicitly records the native ParentConstraint test as passed.
The final Unity 6 Console reported zero current errors and warnings, compilation successful and Play Mode stopped. Expected overlap/invalid-source warnings are asserted by the tests.

## Motion Root behavior and application order

- Track Inspector option: **Relay Motion Root**, serialized as `MotionRelayTrack.relayMotionRoot`, default **false**. Existing tracks remain OFF.
- OFF: target root position, rotation and local scale are preserved.
- ON: source Animator.transform world position and rotation are copied to target Animator.transform. Target scale and target parent are preserved. No local-space root copying, relative offset or interpolation is used.
- `ProcessFrame` selects one source using Phase 2A's `start <= time < end` rule. It queues the source, not a position/rotation snapshot.
- At late apply, root position/rotation and HumanPose are sampled from that same source. Play Mode uses the existing execution-order-32000 LateUpdate; Editor preview uses the existing Editor update callback.
- The implementation applies **target world root, then HumanPose**, and restores the requested root and protected scale/secondary transforms after HumanPose application.
- The order-comparison test exercises both root-before-pose and pose-before-root against root world pose, the arm muscle and hips local position. Both orders passed for the translated/rotated, different-size generated rigs. Root-before-pose was retained as the explicit implementation order and is covered by all integrated root/pose tests.
- Empirical inspection showed HumanPose's normalized body placement is in the Animator root's parent space. Removing local root placement before retargeting avoids double movement under transformed ancestors. This normalization uses local coordinates only for the body; Motion Root itself is transferred in world coordinates.
- Gaps and overlaps cancel pending pose/root application. No new target writes occur; overlaps warn without choosing a source.

## Added verification

- Default OFF and JSON serialization round trip.
- World position/rotation, independent translated/rotated source and target parents, and positive uniform parent scales.
- Different source root scales with target root/mapped-bone scale preserved.
- Late root reads after `ProcessFrame` and automatic Editor preview reads after parent-space changes.
- Root and pose switch together at frames 99/100/101 and 199/200/201 at 24/30/60 fps, including backward and repeated seeks.
- Full target Transform snapshots remain unchanged in gaps/overlaps, even when a previous source was queued.
- Secondary-bone local position/rotation/scale, target and driver renderer states, and activation preserved through cuts.
- **Real native ParentConstraint**: an Editor coroutine enters Play Mode, moves/rotates an anchor, evaluates the Timeline, then verifies Source B matches the anchor and Target root/pose match the selected source. The sequence crosses the cut in both directions without manually solving the constraint or manually flushing the relay.

## Known limits and verification boundary

The native ParentConstraint evaluation test runs in **Play Mode from the Editor Test assembly**, in both supported Editors. It is not a claim of pure Edit Mode Constraint preview verification.
During exploratory pure Edit Mode tests, the generated rig's ParentConstraint did not move its Source after `PlayableDirector.Evaluate()` plus Editor updates, including an animated anchor. Because the native constraint result was not produced, that setup cannot prove post-constraint relay in Edit Mode. The final Edit Mode test instead verifies late reading of an updated parent-space world transform. **Manual pure Edit Mode Timeline Preview with ParentConstraint still needs validation in a real scene.**
Relay does not force native Constraint evaluation. Explicit `ApplyPendingPoses()` must be called after the caller has allowed the relevant constraint evaluation to finish. Custom scripts writing the source after Relay's LateUpdate, non-uniform scale/shear and VRChat-specific execution order are unverified.

Custom roots, relative offsets, position-only/rotation-only modes, Blend, Bake, key reduction, Generic/scale mapping, visibility helpers and VRC-specific code remain out of scope. No private model, VRChat upload or client-side verification was used.

Local-only evidence:

```text
Development~/LocalOnly/Verification/phase2b-unity6-results.json
Development~/LocalOnly/Verification/phase2b-unity2022-results.xml
Development~/LocalOnly/Verification/phase2b-unity2022-tests.log
```

Authored changes: `Runtime/Mapping/HumanoidPoseRelay.cs`, `Runtime/Timeline/MotionRelayMixer.cs`, `Runtime/Timeline/MotionRelayTrack.cs`, `Editor/Timeline/MotionRelayClipEditor.cs`, `Tests/Editor/MotionRootRelayTests.cs` and its `.meta`, `README.md`, `CHANGELOG.md`, and this file.
The pre-existing Unity-generated change to `Development~/panda-motion-relay-dev-unity6/Assets/InputSystem_Actions.inputactions.meta` was excluded from the Phase 2A checkpoint and is not part of the authored Phase 2B implementation.
No Phase 2B files are staged; LocalOnly/WorkSpace and generated caches remain untracked. The authored diff passes `git diff --check`.

---

# Phase 2A verification (historical)

Verified on 2026-10-27 (Asia/Tokyo), based on `cc3e71c` on branch `feat/phase2-exact-cut`.
Implementation and validation only; no commit or push has been performed for Phase 2A.

| Environment | Timeline | Compilation | Tests |
| --- | --- | --- | --- |
| Unity 6000.0.84f1, primary development project | 1.8.13 | Passed | 29 passed, 0 failed |
| Unity 2022.3.22f1, compatibility development project | 1.7.6 | Passed | 29 passed, 0 failed |

The 11 Phase 1 cases are retained. Phase 2A adds 18 cases in `Tests/Editor/ExactCutRelayTests.cs`, sharing the synthetic Humanoid fixture through a partial test class.
Unity 6 was inspected and tested through the installed Unity CLI against the running `panda-motion-relay-dev-unity6` Editor. The chat session did not expose Unity MCP tools, so the CLI's shared Editor connection was used for live inspection, compilation, test execution and Console checks.
Unity 2022.3.22f1 ran with `-batchmode -nographics -runTests -testPlatform EditMode` and exited with code 0.
The final Unity 6 Console reported zero current errors and warnings, with Play Mode stopped.

## Cut evaluation

- A clip owns `start <= current director time < end`. At an adjacent boundary the incoming clip owns the time exclusively.
- Each selected clip resolves its own exposed Source Animator. A source change disposes the old pose handler and creates a handler for the newly selected pair.
- Clip weights do not blend, ease or prioritize pose sources. Current time determines selection, independent of seek history.
- Pose application still runs after animation evaluation: Editor update for preview, LateUpdate in Play Mode, or explicit `ApplyPendingPoses()` after programmatic `Evaluate()`.
- A gap cancels pending application and does not write to the target. It neither restores a prior pose nor continues following an old source.
- Two or more occupying clips, even with the same Source, invalidate the overlap. One warning is emitted per continuous invalid encounter, pending application is canceled and no source wins.

## Added coverage

- Driver A and B independently; A→B→A sequence.
- Cut frames 99/100/101 and 199/200/201 at 24, 30 and 60 fps, including reverse seeks.
- Forward frame-by-frame evaluation, arbitrary jumps, repeated seeks and replacement of an unflushed source.
- Gap boundaries/interior and timeline end: full target Transform snapshot remains unchanged, including after a queued previous source.
- Overlap rejection and warning suppression/re-entry, including repeated references to the same source.
- Invalid incoming source does not reuse an earlier source.
- Target root, scale, secondary-bone local pose, renderer visibility and activation preserved across repeated cuts.
- Automatic Editor preview across cuts and real GameTime Play Mode playback through A→B→A without manual flushing.

Local evidence (ignored):

```text
Development~/LocalOnly/Verification/phase2a-unity6-results.json
Development~/LocalOnly/Verification/phase2a-unity2022-results.xml
Development~/LocalOnly/Verification/phase2a-unity2022-tests.log
```

Motion-root/ParentConstraint world-motion transfer, Bake, key reduction, Generic/scale mapping, Blend, Driver Mesh UI and VRChat-specific code are outside this phase.
No production-model or VRChat SDK, upload or client verification was performed.

---

# Phase 1 verification (historical)

Verified on 2026-10-08 (Asia/Tokyo).

| Environment | Timeline | Compilation | Tests |
| --- | --- | --- | --- |
| Unity 6000.0.84f1, primary development project | 1.8.13 | Passed | 11 passed, 0 failed |
| Unity 2022.3.22f1, compatibility development project | 1.7.6 | Passed | 11 passed, 0 failed |

Assembly: `SillBill.PandaMotionRelay.Editor.Tests`.
Unity 6 tests ran through the connected Editor. Unity 2022.3.22f1 tests ran with `-batchmode -nographics -runTests -testPlatform EditMode`.
Both runs include an Editor coroutine that enters Play Mode, verifies LateUpdate pose application and exits Play Mode.
The final Unity 2022 test process exited with code 0.

## Covered behavior

- Avatar muscle retargeting between differently sized synthetic Humanoid rigs.
- Preservation of target root position/rotation, mapped-bone scale and unmapped secondary-bone local pose.
- Removal of driver world placement from body pose, including different positive uniform root scales.
- No activation of an inactive target; safe repeated disposal.
- Missing Avatar and self-relay rejection.
- Clip does not advertise blending or extrapolation.
- Forward and backward Timeline sampling; both orders of Animation and Relay tracks.
- Automatic Editor preview application after a seek.
- Automatic Play Mode LateUpdate application after a seek.

All rigs and motion curves are generated in the tests; there is no dependency on licensed models or local private assets.
The final Unity 6 Console reported no current errors or warnings, and the Editor was left out of Play Mode.
`git diff --check` passed. No LocalOnly / WorkSpace content was staged during the initial verification. At that point no commits or pushes had been made.

## Phase 1 checkpoint preparation

On 2026-10-08 the existing Unity 6 test assembly was rerun: 11 passed, 0 failed.
The Unity 2022.3.22f1 result above was reused; that environment was not rerun for this checkpoint because the Phase 1 code was unchanged.
The ignore rules now cover nested Unity generated data and IDE caches while allowing development Assets, ProjectSettings and package manifests to be tracked.
The existing VCC-generated `Packages/.gitignore` and VPM Resolver policy were preserved.
The staged whitespace check reports existing whitespace in Unity-serialized files and VCC-supplied template/Resolver files; these files were preserved as supplied. The check passes for the authored package code and documentation (excluding Unity-generated `.meta` files).

## Local evidence

The Unity 2022 XML report and full Editor log are local-only:

```text
Development~/LocalOnly/Verification/unity2022-results.xml
Development~/LocalOnly/Verification/unity2022-tests.log
```

The 2022 log includes Unity Licensing Client validation/token messages during startup; these did not prevent compilation or the passing test run.

## Remaining verification and scope

This is the first milestone from the initial development instructions, not completion of all six phases.
No real production character, PhysBone simulation, VRChat SDK, upload or client-side verification has been performed.
Secondary-bone exclusion here means preserving their local pose, not proof of a complete physics integration.
Different source rest poses, finger-rich production Avatars, constraint timing and non-uniform root scales need additional coverage before production use.
At the Phase 1 checkpoint, motion-root transfer, multi-source exact cuts, bake, key reduction and Generic mapping remained later milestones. Multi-source exact cuts have since been verified in Phase 2A above.

Programmatic `PlayableDirector.Evaluate()` must be followed by `MotionRelayEvaluation.ApplyPendingPoses()` for synchronous sampling.
Ordinary Editor preview and Play Mode playback apply pending poses automatically.
