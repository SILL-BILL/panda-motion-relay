# Phase 1 verification

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
Motion-root transfer, multi-source exact cuts, bake, key reduction and Generic mapping remain later milestones.

Programmatic `PlayableDirector.Evaluate()` must be followed by `MotionRelayEvaluation.ApplyPendingPoses()` for synchronous sampling.
Ordinary Editor preview and Play Mode playback apply pending poses automatically.
