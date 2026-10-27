# Changelog

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
