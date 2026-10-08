# Panda Motion Relay
## Initial Development Instructions

対象リポジトリ:

```text
SILL-BILL/panda-motion-relay
```

プロジェクト名:

```text
Panda Motion Relay
```

この指示書は、Panda Motion Relay の初期実装を開始するためのものです。

`AGENTS.md` のルールを最優先で守ってください。

---

# 1. 開発方針

主開発環境は:

```text
Unity 6.x
```

最低対応環境は:

```text
Unity 2022.3.22f1
```

VRChatローカル検証環境は:

```text
Unity 2022.3.22f1
VRChat Creator Companion
World SDK
```

Unity 6で開発しますが、Unity 6専用パッケージにはしません。

Unity 2022.3.22f1で動かないAPIを安易に使用しないでください。

Unity 6固有APIが必要な場合は、互換レイヤーまたはバージョン分岐に隔離してください。

---

# 2. ローカルデータをコミットしない

以下は必ずGit管理対象外にしてください。

```text
LocalOnly/
WorkSpace/
```

`.gitignore` に以下を追加してください。

```gitignore
# Local developer data
**/LocalOnly/
**/WorkSpace/
```

以下もコミット禁止です。

```text
ローカルSSH設定
秘密鍵
Codexローカル設定
Unity MCPローカル設定
認証情報
私物テストモデル
ライセンス上再配布できないFBX
VRChat検証用の非公開素材
一時生成データ
```

`git add -f` で `LocalOnly/` や `WorkSpace/` を追加しないでください。

コミット前に必ず `git status` を確認してください。

---

# 3. 初期パッケージ構成

UPMパッケージとして、最低限以下を用意してください。

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

`package.json` の初期値は以下を基準にしてください。

```json
{
  "name": "com.sillbill.panda-motion-relay",
  "displayName": "Panda Motion Relay",
  "version": "0.1.0",
  "unity": "2022.3",
  "unityRelease": "22f1",
  "description": "Timeline-based motion relay, retargeting and bake tools for Unity.",
  "author": {
    "name": "Gonsaku"
  },
  "license": "MIT",
  "dependencies": {
    "com.unity.timeline": "1.7.6"
  }
}
```

Unity 6側でTimelineのバージョンが異なる場合でも、Unity 2022.3互換性を破壊するAPI使用を避けてください。

---

# 4. Development プロジェクト

想定構成:

```text
Development~/
├─ panda-motion-relay-dev-unity6/
├─ panda-motion-relay-dev-2022/
└─ panda-motion-relay-vrchat-2022/
```

役割:

```text
panda-motion-relay-dev-unity6
    主開発
    Unity MCP利用
    実装
    デバッグ
    Editor Test

panda-motion-relay-dev-2022
    Unity 2022.3.22f1互換チェック

panda-motion-relay-vrchat-2022
    Creator Companionで作成
    VRC SDKローカル互換確認
```

VRChat用プロジェクトはCreator Companionで作成してください。

Panda Motion Relay本体にVRC SDK依存を追加しないでください。

---

# 5. Panda Motion Relay の目的

今回解決したい問題は以下です。

従来:

```text
CharaA ON
    ↓
CharaA OFF
CharaB ON
    ↓
揺れものシミュレーション再初期化
    ↓
スカートや髪がブルンと跳ねる
```

新方式:

```text
Driver_A ─┐
          ├─ Motion Relay ─→ Target
Driver_B ─┘

Target
    常時Active
    常時表示
    Secondary Motion継続
```

Driverのメッシュは制作中は表示して構いません。

制作完了後はDriverのRendererまたはMesh子GameObjectだけを非表示にします。

DriverのAnimator、Armature、Constraint、Root GameObjectは必要に応じてActiveを維持します。

---

# 6. 最初の実装ゴール

いきなり全機能を作らないでください。

最初のマイルストーンは:

```text
Driver_A
    ↓
Target
```

の1ソースRelayです。

Humanoid DriverからHumanoid Targetへ、Timeline上でTargetがDriverのPoseを追従できることを確認してください。

この段階では以下は後回しで構いません。

```text
Generic Mapping
Bake
Key Reduction
Blend
VRChat専用機能
複数Source切替
```

まず1ソースRelayを安定させてください。

---

# 7. Timeline Track

最終的には専用Timeline Trackを用意します。

概念:

```text
Panda Motion Relay Track
Target: Character_Target

[ Driver_A ][ Driver_B ][ Driver_A ]
```

各ClipがSource Animatorを持つ構成を想定します。

Baseline Transition:

```text
Cut
```

Driver切替は1フレーム単位で厳密に行えること。

自動Cross Fadeは入れないでください。

将来、明示的なオプションとしてBlendを追加する余地は残して構いません。

---

# 8. Targetは常時生存

Panda Motion Relayの最重要ルールです。

Target Characterは:

```text
常時Active
常時表示
```

を基本とします。

Source切替のためにTargetを `SetActive(false)` にしないでください。

Targetを複数複製してActivation Trackで切り替える方式には戻さないでください。

揺れものシミュレーションを維持することがこのツールの主目的の一つです。

---

# 9. Humanoid Target

Humanoid Targetでは:

```text
Animator.GetBoneTransform(HumanBodyBones.xxx)
```

等を使用して、Humanoid Mapping済みBoneを識別します。

Secondary BoneはRelay/Bake対象に含めないでください。

例:

```text
Bake / Relay対象
Hips
Spine
Chest
Neck
Head
UpperArm
LowerArm
Hand
UpperLeg
LowerLeg
Foot
Humanoid Finger Bones

対象外
Skirt
Hair
Ribbon
Tail
Accessory
Custom secondary chains
```

---

# 10. Source切替

複数Source対応時の標準動作:

```text
Frame 99  : Driver_A
Frame 100 : Driver_B
```

Frame 100で完全にDriver_Bへ切り替わること。

中間補間を勝手に生成しないでください。

既存制作では切替位置を厳格に合わせて運用するため、Blendに依存しない設計を優先します。

---

# 11. Generic Target

Generic Targetにも対応できる設計にしてください。

ただし最初から全部実装する必要はありません。

GenericではBone Mappingが必要です。

名前一致だけに依存しないでください。

将来のMapping UI例:

```text
Semantic Slot    Source Bone        Target Bone
------------------------------------------------
Root             Root_Motion        CharacterRoot
Hips             pelvis             J_Bip_C_Hips
Spine            spine_01           J_Bip_C_Spine
Chest            spine_03           J_Bip_C_Chest
Head             head               J_Bip_C_Head
UpperArm_L       upperarm_l         J_Bip_L_UpperArm
...
```

必要機能候補:

```text
Manual Mapping
Auto Map
Mapping Profile Save
Rest Pose Difference Compensation
Per-Bone Position / Rotation / Scale options
```

Rest Pose差を無視した単純Local Rotationコピーは禁止です。

---

# 12. Scale方針

Humanoid:

```text
Scale Bake = OFF
```

をデフォルトにしてください。

Generic:

```text
Off
Animated Only
All Mapped Bones
```

を将来的に選択できる設計にしてください。

Genericのデフォルト候補は:

```text
Animated Only
```

です。

変化していないScale Curveを無駄に生成しないでください。

---

# 13. Bake方針

最終的なVRChat向け等では、Target専用のGeneric AnimationClipへBakeする方式を想定します。

Humanoid TargetをGeneric Transform AnimationへBakeする場合:

```text
Motion Root
    Position + Rotation 必要時

Hips
    Position + Rotation

その他Humanoid Mapping Bone
    Rotation

Secondary Bone
    Bakeしない

Scale
    Humanoidでは原則Bakeしない
```

全Transformを無差別にBakeしないでください。

---

# 14. Quaternion

Rotation BakeはQuaternionを使用してください。

EulerベースのBakeを標準方式にしないでください。

Hips等でのジンバルロックやEuler Flipを避けることが目的です。

必要に応じてQuaternion Continuityを保証してください。

---

# 15. Key Reduction

毎フレーム全BoneへKeyを残したままにしないでください。

Bake後の容量増加を抑えるためKey Reductionを用意します。

ただし、Source切替境界は保護してください。

例:

```text
Frame 99  : Driver_A
Frame 100 : Driver_B
```

最適化処理によって:

```text
99 -> 101を補間
```

のようになり、CutがBlend化することは禁止です。

Source切替Frameは最優先で保持してください。

---

# 16. Bake容量対策

以下を基本にしてください。

```text
Humanoid Mapping Boneのみ
Positionは必要なBoneのみ
RotationはQuaternion
Scaleは必要な場合のみ
Secondary Bone除外
Key Reduction
Cut Boundary Protection
```

将来的にBake UIで以下を表示できると理想です。

```text
Raw Key Count
Reduced Key Count
Estimated / Actual Clip Size
```

ただし初期v0.1.0で必須ではありません。

---

# 17. Driver Mesh表示切替補助

将来的にEditor UIから:

```text
Show Driver Meshes
Hide Driver Meshes
```

を実行できると便利です。

ただし変更対象はRendererまたはMesh子Objectの表示のみ。

Driver RootやAnimator、Armature、Constraintを停止しないでください。

---

# 18. VRChat

Panda Motion Relay自体はVRC SDK非依存にしてください。

VRChat環境では最終的に:

```text
Panda Motion Relayで制作
        ↓
Bake
        ↓
標準AnimationClip / Timeline
        ↓
Target Character
        ↓
VRC PhysBone等はRuntime Simulation
```

を目指します。

VRChatへのアップロード権限がないため、現時点で必須なのはローカルSDK互換確認までです。

README等で実機検証済みと誤認させる表現をしないでください。

---

# 19. テスト項目

最低限、将来的に以下をテスト対象にしてください。

```text
1 Source Relay
Source Cut Switching
Humanoid Bone Mapping
Secondary Bone Exclusion
Generic Mapping
Rest Pose Compensation
Quaternion Bake
Quaternion Continuity
Bake Curve Scope
Scale Policy
Key Reduction
Cut Boundary Protection
Unity 2022.3 compile
Unity 6 compile
VRChat SDK local compile
```

---

# 20. 開発順序

推奨順序:

```text
Phase 1
Package骨格
1 Source Humanoid Relay
Timeline Preview

Phase 2
複数Source
Cut Switching

Phase 3
Humanoid Mapping Bone限定Bake
Quaternion Bake

Phase 4
Key Reduction
Cut Boundary Protection
Bake Size確認

Phase 5
Generic Mapping
Rest Pose Compensation
Scale対応

Phase 6
VRChat local verification
Driver Mesh helper
Polish
```

機能をまとめて巨大実装しないでください。

各PhaseごとにUnity 6で確認し、その後Unity 2022.3互換確認を行ってください。

---

# 21. Git運用

変更前:

```text
git status
```

変更後:

```text
Editor Test
Unity Console確認
git diff
git status
```

を確認してください。

`LocalOnly/` と `WorkSpace/` がStagedに入っていた場合はコミット禁止です。

重要:

> Unity 6で動いた = 完了ではありません。

> Unity 2022.3.22f1を通過して初めて互換性確認完了です。

> LocalOnlyとWorkSpaceは法によりコミット禁止です。
