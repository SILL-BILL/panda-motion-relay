# Panda Motion Relay

Timelineで複数のDriverから常時ActiveのTargetへHumanoid姿勢を転送し、Target専用Generic AnimationClipへBakeするUnity UPM制作ツールです。Targetを切り替えず、モーションの転送元をFrame単位で切り替えます。

```text
Driver_Normal ─┐
               ├─ Motion Relay ─→ Target（常時Active）
Driver_Float  ─┘
```

## 対応環境

- 最低Unity **2022.3.22f1**。主開発は**Unity 6**。
- Timeline依存 **1.7.6**。Unity 6開発環境では1.8.13も検証済み。
- Driver／Targetは有効なHumanoid Avatarを持つ独立したAnimator。
- BakeはEditorのPlay Mode専用。VRChat SDK・Unity MCPへの依存はありません。

## 現在のステータス

**0.1.0 — Production Preview / Internal Production Use**（実戦投入前プレビュー版・社内／個人制作向け）。Exact Cut、Humanoid姿勢転送、オプションのMotion Root転送、3方式のBake、一意な直接Master/Child構成の評価に対応します。

Unity 6／通常Unity 2022.3で既存119件を含む各142自動テストと実戦BakeがPASS。World → RootはNative Generic Animator Controller再生も検証済みです。すべてのUnity／VRChat環境を保証する段階ではありません。

## インストール

RepositoryをUnity Projectの外へ取得し、Package Managerの`+ > Add package from disk...`から、このRepositoryの`package.json`を選びます。Resolve・コンパイル完了後、`Tools > Panda Motion Relay > Bake Humanoid to Generic`を確認してください。

```text
workspace/
├─ panda-motion-relay/    ← package.json
└─ my-animation-project/  ← Unity Project
```

ProjectをPackage Repositoryの内側に作らないでください。導入の詳細と検証済みGit URLは[Manualのインストール](Documentation~/Manual.md#3-インストール)／[検証記録](VERIFICATION.md)を参照してください。利用者の`testables`設定は不要です。

## 5分で始めるQuick Start

Humanoidモデルとモーションを用意した状態から始めます。初回は同じ骨格のコピーをDriver2体・Target1体として使うと確認が容易です。

1. Driver全員とTargetの本体・Animator・親階層を常時Activeにします。Driverを非ActiveにするActivation Trackはミュートし、隠す場合はRendererだけをOFFにしてSceneを保存します。
2. 空Objectを選び、Timelineウィンドウの`Create`でTimeline AssetとPlayableDirectorを用意します。
3. 通常のAnimation TrackをDriver AnimatorへBindingし、Driverのモーションを配置します。
4. `Motion Relay Track`を追加し、TargetのHumanoid AnimatorへBindingします。
5. `Motion Relay Clip`を3本追加し、各Clipの`Source`へNormal／Float／NormalのDriver Animatorを指定します。例：24fpsで`[0,24) [24,48) [48,72)`。
6. TimelineをScrub／Playして、Frame 23→24、47→48で転送元が正確に切り替わることを確認します。
7. Rootも転送する場合だけTrackの`Relay Motion Root`をONにします。Driverメッシュを隠す場合はRendererだけをOFFにします。
8. Scene保存後、Play Mode・Pause解除にして`Tools > Panda Motion Relay > Bake Humanoid to Generic`を開き、Director・身体側Relay Track・`Bake Mode`を指定して新規`.anim`へ保存します。

Exact Cutは`start <= t < end`です。Overlapは無効、Gapは新たな転送なし。詳細操作は[正式マニュアル](Documentation~/Manual.md)を参照してください。

## Bake Modeの選び方

| 目的 | 選択 | 再生条件 |
| --- | --- | --- |
| 元の親階層を再利用 | Local — Use Existing Parent（既定） | 同一骨格・同等のParent Motion |
| 親なし独立モデルでRootのWorld動作も再現 | World — Record Motion to Root | Identity Parent、開始Frameと同等の内部SkeletonとCharacter一定Scale |
| 独立モデルで全体移動をHipsへ集約 | World — Consolidate Motion to Hips（旧Global） | 開始Frameの固定Reference階層。元Root／Parent Motionを重ねない |

成果物はHumanoid muscle Clipではなく、同一内部HierarchyのTarget専用Generic Transform Clipです。Humanoid再Retargetでの一致は保証しません。全方式でScale／Secondary Curveは出しません。LocalはRelay Motion Root ON時にRoot Curveを持ち、World → Rootは最終Target World位置・回転を必ずRootへ記録し、World → HipsはRoot Curveを持ちません。

World → Rootは全範囲を先に検証し、非均一・可変・負Scale／Shearや、未採録のMapped Bone Translation・内部Helper Animationを拒否します。詳細は[Manualの13章](Documentation~/Manual.md)を参照してください。既存API／保存値の`Local=0`・`Global=1`の意味は維持し、新方式は`World=2`です。

## Nested TimelineのEvaluation Director

単一Timelineでは`Relay Director`へRelay Track所有Directorを指定し、`Evaluation Director`は空欄にします。

MasterがCharacterとPropをControl Clipで制御する場合は、**Relay Director = Character、Evaluation Director = Master**です。Prop側のConstraint入力も同じScopeで評価します。一意な直接Control Clipだけに対応します。時刻変換や拒否条件は[Manual](Documentation~/Manual.md#15-nested-timeline)を参照してください。

## 推奨VRChat制作フロー

1. Unity 6／通常Unity 2022.3でPreview・Bake・Generic再生を数値検証します。
2. 完成ClipをVCC Projectへ持ち込みます。
3. ClientSim ONでBake済みClipの位置・姿勢・Root Motionを検品します。

VCCは成果物の検品環境です。VCCでのリアルタイムRelay Preview／Bakeは標準保証範囲外です。Frame 138でRoot位置差0.001580238、回転差0°を観測し、ClientSim OFFでも再現しています。未修正のKnown / Non-blocking制限として記録しています。

## 詳細マニュアル

[Documentation~/Manual.md](Documentation~/Manual.md)に、用語・Driver/Target準備・Track/Source設定・Exact Cut・Motion Root・Bake・Generic再生・Nested Timeline・トラブル対処を掲載しています。

## Known Limitations

Blend、Relative Offset、Custom Root、Position Only／Rotation Only、Generic Mapping、Scale／Secondary Bone Bake、Key Reduction、Batch Bake、Driver Mesh補助UIは未対応です。非一様／可変Scale・Shear、純粋なEdit ModeでのParentConstraint Bake、PhysBone実機Simulationは保証しません。

対象Sceneは事前保存し、案件ごとに出力Clipを再生検証してください。制約の詳細は[Manual](Documentation~/Manual.md#19-known-limitations)にまとめています。

## Verification

[VERIFICATION.md](VERIFICATION.md)が試験結果・実測値・検証範囲の正本です。VCC SDK Base + Worlds 3.10.6／ClientSim ONでLocal／Global成果物とGlobal Generic Animator Controller再生を検証済みです。VRChat Upload／Client実機・Quest Build／Upload・PhysBone完全Simulationは未検証です。

開発環境は[Development~/README.md](Development~/README.md)、開発方針は[AGENTS.md](AGENTS.md)を参照してください。開発Project・個人Asset・LocalOnly／WorkSpaceはPackageへ含めません。

## License

[MIT](LICENSE) — Gonsaku / SILL-BILL。キャラクターモデルや実戦テスト用の私有Assetは同梱しません。
