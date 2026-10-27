# Phase 3 verification

Verified on 2026-10-27 (Asia/Tokyo).
Approved Phase 2B was checkpointed locally as `eaefef65b3cce5664ff4cf171dbc5a6d123c5b29` (`feat: add optional motion root relay`), without pushing.
Phase 3 is on `feat/phase3-generic-bake`, uncommitted and unpushed pending maintainer approval.

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
