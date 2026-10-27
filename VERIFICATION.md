# Phase 2A verification

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
