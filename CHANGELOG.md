# Changelog

## 0.1.1 — Production Preview

- Support fractional-frame Relay Clip timing in Local, World → Root and World → Hips bakes. Exact cuts use the authored boundary times, including gap edges, so clips do not need to be snapped again.
- Sample the normal Timeline frame grid plus actual clip boundaries and outgoing poses immediately before cuts. Keep integer-only bakes compatible and report invalid or unrepresentable clip timing with context.

## 0.1.0 — Production Preview (2026-11-02)

- Includes multi-source exact cuts, Humanoid relay, optional motion-root transfer, nested Evaluation Director and target-specific Local / World → Root / World → Hips Generic baking.
- Correct World → Hips (Global = 1) to use the fixed Hips-parent space of an independent identity-root output hierarchy, retaining constant effective character scale and internal helpers. The previous checkpoint's original-world-parent reference is superseded.
- Apply full-range World validation to both World modes; reject unsupported scale, shear, mapped translation and animated internal helpers before saving.
- Enable Relay Motion Root by default for new tracks; preserve explicitly serialized false and true values.
- Document the required Remove Start Offset OFF setting for World → Root Timeline playback, identity-root Hips replay and the three output modes.
- Add identity-hierarchy, Timeline playback and saved-track compatibility regression coverage while retaining prior behavior checks under the updated specification.
- Verify 183 tests in each of Unity 6000.0.84f1 and Unity 2022.3.22f1, four-condition production-copy Timeline acceptance, and World baked-output Timeline inspection in a local VCC SDK environment. VRChat upload/client, Quest and PhysBone simulation remain unverified.

## Unreleased — Driver evaluation fix

- Fixed bake source animation being skipped by Animator visibility culling.
- Temporarily set only the selected Relay Track's unique Source Driver Animators to AlwaysAnimate, including World preflight, and restore each original mode after success, cancellation or exceptions.
- Preserve Target/unrelated Animator culling, renderer visibility, active states, graph setup and existing Local/World/legacy Global curve calculations.
- Add 12 generated-rig cases covering independent known Head/Arm/Leg animation, hidden Drivers, all three modes and restoration; verify all 154 tests and real-scene Source inputs in Unity 6 and Unity 2022.3.

## Unreleased — Phase 3D

- Add World Preserve Root (`BakeSpace.World = 2`) to record the final evaluated Target world motion for an independent same-skeleton Generic character; preserve Local = 0 and legacy Global = 1 (World to Hips).
- Validate the entire World range before recording: reject unsupported scale, shear, mapped-bone translation and animated internal helpers without adding scale or secondary curves.
- Add three explicit Bake Mode labels and Japanese guidance for Director roles, Humanoid-body Track selection and continuously active Drivers; improve target/source errors.
- Add 23 generated-rig regression cases and verify production baking plus native Generic Animator Controller replay in Unity 6 and Unity 2022.3; retain existing 119 tests.

## Unreleased — Phase 3C

- Documentation: add a Japanese production-use manual, reorganize the README as a Quick Start, and label version 0.1.0 as Production Preview / Internal Production Use.
- Verification: confirm clean local-UPM installation in Unity 6 / Unity 2022.3, HTTPS Branch / Commit-ref installation, and generated Humanoid Preview plus Local/Global Bake; retain Local/Global, nested Timeline and baked-output VRChat workflows.
- Verification / documentation: record production-like Local/Global bake results in standard Unity 2022.3 and baked-output inspection in VRC SDK 3.10.6 / ClientSim ON, including Global Generic Animator Controller playback.
- Documentation: recommend baking in standard Unity and inspecting baked clips in VCC; record the unresolved, non-blocking VCC Relay Preview frame-138 root discrepancy.
- Separate optional Evaluation Director from Relay Director for direct Master/Child Timeline baking; omitted evaluation preserves the single-Director API.
- Resolve the unique Control Clip and map Relay time using start, clipIn and timeScale; reject ambiguous, out-of-range, looping, prefab-instantiated and multi-level control configurations.
- Evaluate Character and Prop timelines through the Master while keeping binding and exposed-reference resolution on the Relay Director.
- Restore affected Director clocks, playback modes, states and graph validity, including cancellation and exceptions.
- Add generated nested Constraint, mapping, exact-cut, first-sample, Local/Global replay and restoration regression coverage.

## Unreleased — Phase 3B

- Add Local / Global Bake Space to the Humanoid Generic Bake window; default to Local with unchanged Phase 3 behavior.
- In Global mode, freeze the Hips parent's reference matrix and rotation at the first fully evaluated sample, and fold final world Hips motion into that fixed space.
- Keep other mapped bones in local quaternion rotation and omit separate Animator-root curves in Global mode to avoid double motion.
- Retain exact-cut protection, gap sampling, overlap rejection, scale/secondary-bone exclusion and scene-state restoration.
- Document replay on the equivalent frozen hierarchy without replaying ancestor motion a second time.

## Unreleased — Phase 3

- Add a Play Mode Editor bake window and raw per-frame Generic Transform clip exporter for Humanoid targets.
- Bake only mapped bones: hips position/quaternion rotation, other mapped-bone quaternion rotation, and optional Animator-root position/rotation in parent space.
- Exclude all scale and unmapped secondary-bone curves; use standard Unity Transform binding paths.
- Preserve exact-cut boundaries with step tangents after Quaternion continuity processing; no key reduction or compression.
- Validate sources, target, track overlap, frame alignment and new output paths before sampling; reject silent asset overwrite.
- Restore scene transforms, activation/render states and Director state on completion or cancellation; discard only that Director's queued pose writes.
- Add generated finger-rig tests and independent Generic replay comparisons. Generic playback requires a matching hierarchy configured for Generic, rather than a second Humanoid retarget pass.

## Unreleased — Phase 2B

- Add an optional track-level Relay Motion Root setting, disabled by default.
- Relay the selected Animator root's late-evaluated world position and rotation together with its Humanoid pose; preserve target scale.
- Normalize HumanPose body placement in the Animator parent's coordinate space for independent source/target hierarchies.
- Register target root position/rotation for Timeline preview restoration when enabled.
- Add tests for root settings, application order, parent spaces, exact cuts, secondary-bone/visibility preservation and native ParentConstraint evaluation in Play Mode.
- Pure Edit Mode ParentConstraint preview remains unverified; Relay does not force native constraint evaluation.

## Unreleased — Phase 2A

- Multiple Humanoid sources using each Relay Clip's own Source Animator.
- Exact cuts determined by half-open clip intervals, independent of blend weights and seek history.
- Cancel pending pose application in gaps and overlaps; warn on overlap without selecting a priority source.
- Preserve Phase 1 target root, scale, secondary-bone pose and activation/visibility behavior.
- Add boundary-frame, scrub, direct-seek, gap, overlap, Editor preview and Play Mode playback tests.

## 0.1.0 — Phase 1

- UPM package skeleton; Unity 2022.3.22f1 minimum, Timeline 1.7.6 dependency.
- One-source Humanoid relay using Avatar muscle retargeting, with a dedicated Timeline track and clip.
- Preserve target root, scale and unmapped secondary bones; no activation changes or automatic blending.
- Synthetic Humanoid Editor tests; later phases will add switching, baking and Generic mapping.
