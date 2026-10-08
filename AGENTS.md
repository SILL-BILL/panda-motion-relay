# AGENTS.md

## Project

This repository contains **Panda Motion Relay**, a Unity Timeline-oriented motion relay, retargeting, and bake tool.

Repository:

```text
SILL-BILL/panda-motion-relay
```

Project name:

```text
Panda Motion Relay
```

Maintainer:

```text
Gonsaku
```

Panda Motion Relay is an independent Unity package.

Its core purpose is to allow one always-active target character to receive motion from one of multiple driver characters on Timeline, while allowing the motion source to switch at exact frames without activating or deactivating the target character.

The primary production motivation is to avoid resetting runtime secondary-motion systems such as VRC PhysBone or similar physics systems when changing between alternate character-control setups.

---

# Core Concept

The intended authoring model is:

```text
Driver_A ─┐
          ├─ Panda Motion Relay ─→ Target Character
Driver_B ─┘
```

Typical use case:

```text
Driver_A
Normal character control

Driver_B
Vehicle / prop constrained control
ParentConstraint or equivalent

Target
Always visible
Always active
Runtime secondary motion remains alive
```

During animation production, the driver character meshes may remain visible.

After authoring is complete, only the driver renderers or driver mesh GameObjects should be hidden.

Do not require the driver root GameObjects, Animators, Armatures, or Constraints to be deactivated.

---

# Primary Behavior

Panda Motion Relay must support Timeline-driven source switching.

The default transition behavior is an exact cut:

```text
Frame 99  -> Driver_A
Frame 100 -> Driver_B
```

No automatic blending may be introduced at a cut boundary.

A future optional blend mode may be supported, but exact cut behavior is the default and must remain available.

The target character must remain active and visible across source switches.

The purpose is to switch the source of the target pose, not the target GameObject itself.

---

# Development Environment Policy

Panda Motion Relay uses **Unity 6 as the primary development environment**, while maintaining compatibility with **Unity 2022.3.22f1**.

This distinction is mandatory.

## Primary Development

```text
Unity 6.x
```

Use Unity 6 for:

```text
Primary implementation
Unity MCP-assisted development
Debugging
Timeline authoring tests
Bake development
Mapping development
Editor tests
General development iteration
```

Unity 6 is the main development workshop.

It does not define the minimum supported Unity version.

## Minimum Supported Version

```text
Unity 2022.3.22f1
```

Panda Motion Relay must remain compatible with Unity 2022.3.22f1.

This version is the compatibility baseline for VRChat-related production workflows.

Do not silently raise the minimum supported Unity version.

The package metadata should continue to declare:

```json
"unity": "2022.3",
"unityRelease": "22f1"
```

unless the maintainer explicitly changes this policy.

## Compatibility Rule

Before introducing a Unity API, verify that the implementation can work in Unity 2022.3.22f1.

Do not use a Unity 6-only API merely because the primary development environment is Unity 6.

If version-dependent behavior is unavoidable, isolate it behind a small compatibility layer or a compile-time version check.

Example:

```csharp
#if UNITY_6000_0_OR_NEWER
    // Unity 6 implementation
#else
    // Unity 2022.3 implementation
#endif
```

Keep version-specific code localized.

Do not scatter Unity-version checks throughout unrelated systems.

---

# Development Project Layout

Use the following development layout:

```text
Development~/
├─ panda-motion-relay-dev-unity6/
├─ panda-motion-relay-dev-2022/
└─ panda-motion-relay-vrchat-2022/
```

Roles:

```text
panda-motion-relay-dev-unity6
    Primary development
    Unity MCP
    Implementation
    Debugging
    Automated tests

panda-motion-relay-dev-2022
    Unity 2022.3.22f1 compatibility verification
    Compile checks
    Smoke tests

panda-motion-relay-vrchat-2022
    VRChat SDK compatibility verification
    VRC PhysBone interaction tests
    Constraint interaction tests
    Android / mobile-oriented local checks
```

The VRChat project must be created through **VRChat Creator Companion**.

Do not manually recreate the VRChat SDK environment.

---

# Verification Flow

Important feature work must follow this order:

```text
1. Implement and test in Unity 6
        ↓
2. Verify compilation and behavior in Unity 2022.3.22f1
        ↓
3. Verify VRChat SDK compatibility when relevant
```

Do not consider a feature complete solely because it works in Unity 6.

Unity 2022.3 verification is mandatory for changes affecting:

```text
Timeline tracks
Timeline clips
PlayableGraph behavior
Animator interaction
AnimationClip generation
Animation baking
Bone mapping
Constraint handling
Serialization
Package dependencies
Assembly definitions
```

For VRChat-oriented changes, also verify them in the Creator Companion project when practical.

---

# Unity MCP Policy

Unity MCP may be used in the Unity 6 development project to reduce manual editor round-trips.

Typical uses include:

```text
Inspect Scene hierarchy
Inspect GameObjects and Components
Inspect Animator setup
Inspect Timeline setup
Read Console errors and warnings
Create test objects or scenes
Run Editor tests
Inspect generated assets
Check project state
```

MCP-specific setup belongs to the development environment.

Do not make the Panda Motion Relay package itself depend on Unity MCP, Unity CLI, or development-only MCP packages.

---

# Local Development Data Policy

For all SILL-BILL Unity projects, local-only development data must never be committed.

The following directories are always local-only by default:

```text
LocalOnly/
WorkSpace/
```

The repository `.gitignore` must include:

```gitignore
# Local developer data
**/LocalOnly/
**/WorkSpace/
```

Do not force-add these directories with `git add -f`.

Do not move private, licensed, or temporary development assets into tracked directories simply to make a test pass.

Typical local-only data includes:

```text
Temporary test assets
Personal test scenes
Imported production character models
Large local FBX files
Reference animations
Scratch assets
Local experiments
Generated debug data
Private or licensed assets
VRChat verification assets that must not be redistributed
```

Tests and production code must not depend on files stored in `LocalOnly/` or `WorkSpace/`.

If reusable test data is required, create a dedicated tracked test asset that is safe to redistribute.

Before committing, verify that no files from these directories are staged.

This is a default policy for SILL-BILL Unity repositories.

---

# Package Structure

Use a UPM-style package structure.

Recommended layout:

```text
panda-motion-relay/
├─ package.json
├─ README.md
├─ CHANGELOG.md
├─ LICENSE
├─ AGENTS.md
│
├─ Runtime/
│   ├─ Timeline/
│   ├─ Mapping/
│   └─ SillBill.PandaMotionRelay.asmdef
│
├─ Editor/
│   ├─ Timeline/
│   ├─ Mapping/
│   ├─ Retarget/
│   ├─ Bake/
│   ├─ UI/
│   └─ SillBill.PandaMotionRelay.Editor.asmdef
│
├─ Tests/
│   └─ Editor/
│
└─ Development~/
```

Keep runtime and editor-only responsibilities separated.

---

# Runtime and Authoring Separation

Panda Motion Relay is primarily an authoring tool.

The preferred final workflow is:

```text
Driver Characters
        ↓
Panda Motion Relay authoring
        ↓
Target Character preview
        ↓
Bake
        ↓
Standard Unity AnimationClip / Timeline data
```

The final baked content should not require Panda Motion Relay runtime behavior whenever the workflow allows it to be removed.

VRChat deployment should prefer standard Unity and VRChat-compatible runtime data rather than a custom Panda Motion Relay runtime dependency.

---

# VRChat Dependency Policy

The Panda Motion Relay package itself must **not depend on the VRChat SDK**.

VRChat-specific packages and components belong in the Creator Companion verification project.

If optional VRChat-specific integration is added in the future, it must remain isolated and optional.

Do not make the core package fail to compile when the VRChat SDK is absent.

---

# VRChat Verification Claims

Do not claim full VRChat verification unless that level of testing was actually performed.

The current expected verification level may be limited to local SDK testing.

When only local verification has been completed, use wording equivalent to:

```text
Verified locally in a VRChat World SDK environment.
Actual VRChat upload and client-side verification have not been performed.
```

Do not claim upload or client verification unless an actual upload and client-side test were completed.

---

# Humanoid Relay

For a Humanoid target:

- Use the Animator Avatar mapping to identify Humanoid bones.
- Relay only the intended Humanoid pose and required motion-root information.
- Do not automatically relay secondary-motion bones.
- Secondary bones such as skirt, hair, ribbon, tail, or accessory chains should remain available for runtime simulation.

The primary target is a continuously active character.

Do not solve source switching by enabling and disabling duplicate target characters.

---

# Generic Relay

Generic rigs must support explicit bone mapping.

Do not rely only on identical bone names.

Use semantic mapping slots where practical, for example:

```text
Root
Hips
Spine
Chest
Neck
Head
UpperArm_L
LowerArm_L
Hand_L
UpperLeg_L
LowerLeg_L
Foot_L
...
```

Source and target rigs may use different names.

Example:

```text
Source: pelvis
Target: J_Bip_C_Hips
Semantic slot: Hips
```

Generic mapping should support:

```text
Manual mapping
Auto-map suggestions
Saved mapping profiles
Rest-pose-aware rotation transfer
Per-bone channel control where required
```

Generic retargeting must account for rest-pose differences.

Do not perform naive direct local-rotation copying when source and target rest poses differ.

---

# Scale Policy

Scale handling depends on the target type.

## Humanoid Target

Default:

```text
Scale Bake: OFF
```

Do not bake scale to Humanoid-mapped bones unless an explicit approved use case requires it.

## Generic Target

Scale may be supported.

Preferred modes:

```text
Off
Animated Only
All Mapped Bones
```

Default Generic behavior should prefer:

```text
Animated Only
```

Do not create unnecessary scale curves for bones whose scale is constant.

---

# Bake Policy

The bake system is a core feature.

The primary production bake output may be a target-specific Generic AnimationClip.

This is acceptable when the clip is intended for the final target character rather than for later Humanoid retargeting.

## Humanoid-Mapped Bake Scope

When baking a Humanoid target to Generic Transform curves:

- Bake only Humanoid-mapped body bones.
- Include motion root data when required.
- Do not bake secondary-motion bones.
- Do not bake unrelated helper bones by default.

Typical policy:

```text
Motion Root
    Position + Rotation when required

Hips
    Position + Rotation

Other mapped Humanoid bones
    Rotation

Scale
    Off by default
```

## Rotation

Bake rotations as Quaternion curves.

Do not use Euler curves as the primary bake representation.

After writing rotation curves, preserve Quaternion continuity.

Avoid introducing artificial sign flips or discontinuities.

## Key Reduction

Do not leave every sampled frame on every curve unless necessary.

The bake pipeline should support key reduction.

However, source-switch boundaries are protected.

For an exact switch:

```text
Frame 99  -> Driver_A
Frame 100 -> Driver_B
```

the required boundary keys must not be removed or turned into an unintended blend.

Source-switch timing is authoritative.

Optimization must never change a cut into an interpolation.

## Position Curves

Do not automatically bake position curves for every bone.

Prefer position curves only where they are required, typically:

```text
Motion Root
Hips
Explicit Generic-mapped bones that genuinely animate translation
```

## Scale Curves

For Generic targets, prefer generating scale curves only for mapped bones whose scale actually changes when using the `Animated Only` mode.

---

# Source Switching

Exact cut switching is the baseline behavior.

Timeline authoring should allow a sequence conceptually equivalent to:

```text
[ Driver_A ][ Driver_B ][ Driver_A ][ Driver_C ]
```

At each boundary, the target pose source changes exactly at the defined frame.

Do not add hidden smoothing.

Do not automatically overlap or blend source clips.

Optional blending may be introduced later as an explicit user-controlled mode.

---

# Driver Visibility

Driver characters are normal authoring characters.

During production:

```text
Driver meshes: Visible
Driver Animator: Active
Driver Armature: Active
Driver Constraints: Active
```

After authoring:

```text
Driver meshes: Hidden
Driver Animator: Active when required for preview or source evaluation
Driver Armature: Active
Driver Constraints: Active
Target mesh: Visible
```

If helper UI is added, it may provide:

```text
Show Driver Meshes
Hide Driver Meshes
```

Such controls must affect render visibility only.

Do not deactivate the driver root object as a side effect.

---

# Secondary Motion

The target's secondary-motion systems must remain continuously alive whenever possible.

Examples include:

```text
VRC PhysBone
DynamicBone-like local development systems
Other runtime secondary-motion components
```

Panda Motion Relay must avoid baking or directly driving secondary bones unless explicitly requested.

The design goal is:

```text
Target body animation
    Continuous

Target secondary simulation
    Continuous

Motion source
    Switchable
```

The purpose is to avoid the visible reset or impulse that can occur when a complete character GameObject is toggled off and on.

---

# Testing Priorities

Automated and manual tests should prioritize:

```text
Exact source switching
No unintended blending
Humanoid mapping correctness
Generic mapping correctness
Rest-pose offset correctness
Quaternion bake continuity
Bake curve scope
Secondary-bone exclusion
Key reduction without cut corruption
Unity 2022.3 compatibility
Unity 6 compatibility
VRChat SDK local compatibility
Serialization stability
Undo safety for editor operations
```

---

# Commit and Repository Safety

Before committing:

```text
Run relevant tests
Check Unity Console
Check git status
Confirm LocalOnly is not staged
Confirm WorkSpace is not staged
Confirm private or licensed assets are not staged
```

Do not commit local SSH configuration, local Codex configuration, local MCP configuration, credentials, tokens, or private keys.

Repository-local `.git/config` settings are local machine configuration and must remain untracked.

---

# Development Principle

The project follows these rules:

> Unity 6 is the workshop.

> Unity 2022.3.22f1 is the compatibility gate.

> The Creator Companion VRChat project is the final local proving ground.

> LocalOnly and WorkSpace are never repository content.

> Exact Timeline cuts must remain exact.

> The target character stays alive while the motion source changes.

These rules are project law unless the maintainer explicitly changes them.
