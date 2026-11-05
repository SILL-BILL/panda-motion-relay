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

**0.1.2 — Production Preview / Internal Production Use**（実戦投入前プレビュー版・社内／個人制作向け）。Exact Cut、Humanoid姿勢転送、オプションのMotion Root転送、3方式のBake、一意な直接Master/Child構成の評価に対応します。

Unity 6／通常Unity 2022.3で各190件の自動テストと、実戦Sceneコピーの4条件（Local Root OFF／ON、World → Root、World → Hips）のGeneric Timeline再生がPASS。非表示Driverの身体Motionも確認しています。

## インストール

RepositoryをUnity Projectの外へ取得し、Package Managerの`+ > Add package from disk...`から、このRepositoryの`package.json`を選びます。Resolve・コンパイル完了後、`Tools > Panda Motion Relay > Bake Humanoid to Generic`を確認してください。

実案件ではPackage Managerの`Add package from git URL...`へ次のタグ固定URLを指定します。

```text
https://github.com/SILL-BILL/panda-motion-relay.git#v0.1.2
```

Branch追従URLは使わず、`Packages/packages-lock.json`の解決Commitを確認してください。初回はScene／Timelineのコピーと新規Bake出力フォルダーで検証します。v0.1.0タグは移動せず、修正は新Version／新Tagで配布します。

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
7. 新規Trackの`Relay Motion Root`はONです。Targetの配置を保持したい場合はOFFにします。既存Trackの保存済み設定は変更されません。Driverメッシュを隠す場合はRendererだけをOFFにします。
8. Scene保存後、Play Mode・Pause解除にして`Tools > Panda Motion Relay > Bake Humanoid to Generic`を開き、Director・身体側Relay Track・`Bake Mode`を指定して新規`.anim`へ保存します。

Exact Cutは`start <= t < end`です。隣接Clipの境界が丸め誤差で1 ps以下重なる場合は、後続Clipのstartで切り替えます。実案件では約0.313 psの重なりを確認しました。許容範囲を超えるOverlapは拒否し、Gapでは新たな転送を行いません。詳細操作は[正式マニュアル](Documentation~/Manual.md)を参照してください。

3つのBake ModeでClipをSnapし直す必要はありません。PreviewとBakeのExact Cutは、同じClip境界の実時刻を使います。異なる境界がAnimationClipのfloat精度で同じキー時刻になる場合は、Clip名と時刻を示して停止します。Unity 6と2022.3の全Editorテストは各192件合格し、保存済み実戦データのコピーで4条件のBakeと再生、VCCのWorld SDK環境でWorld出力のローカル再生を確認しました。VRChatへのアップロード、実機、Quest、PhysBoneの確認と、ゴン作本人による実案件でのGUI確認は未実施です。

Bake中は使用するDriver Animatorを一時的に`AlwaysAnimate`へ切り替え、終了・取消・例外時に元のCulling Modeへ戻します。RendererがOFFでも身体アニメーションを採録できます。手動でCulling Modeを変更する必要はありません。Driver本体・親・Animatorは引き続き常時Activeにしてください。

## Bake Modeの選び方

| 目的 | 選択 | 再生条件 |
| --- | --- | --- |
| 元の親階層を再利用 | Local — Use Existing Parent（既定） | 同一骨格・同等のParent Motion |
| 親なし独立モデルでRootのWorld動作も再現 | World — Record Motion to Root | Identity Parent、開始Frameと同等の内部SkeletonとCharacter一定World Scale。TimelineのRemove Start OffsetはOFF |
| 独立モデルで全体移動をHipsへ集約 | World — Consolidate Motion to Hips | Rootの位置0・回転Identityで固定。同一内部SkeletonとCharacter一定World Scale。元Root／Parent Motionを重ねない |

成果物はHumanoid muscle Clipではなく、同一内部HierarchyのTarget専用Generic Transform Clipです。Humanoid再Retargetでの一致は保証しません。全方式でScale／Secondary Curveは出しません。LocalはRelay Motion Root ON時にRoot Curveを持ち、World → Rootは最終Target World位置・回転を必ずRootへ記録し、World → HipsはRoot Curveを持ちません。

**Important: World → Root ClipをTimelineで再生する場合は、Animation ClipのRemove Start OffsetをOFFにしてください。** ONではBakeしたRootの開始位置・回転が除去され、元のWorld配置を再現できません。World → HipsはRoot Curveがないため、この項目が表示されない場合があります。

両World方式は全範囲を先に検証し、非均一・可変・負Scale／Shearや、未採録のMapped Bone Translation・内部Helper Animationを拒否します。詳細は[Manualの13章](Documentation~/Manual.md)を参照してください。保存値は`Local=0`・`Global=1`・`World=2`を維持します。0.1.0のGlobalはIdentity Root向けのHips集約です。過去Checkpointの開始時World親空間への出力とは異なります。

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

[VERIFICATION.md](VERIFICATION.md)が試験結果・実測値・検証範囲の正本です。今回のVCC SDK Base + Worlds 3.10.5環境では、2022.3生成のWorld → Root／World → HipsをNative Generic Timelineで検品しました。VCCでBake・リアルタイムRelayは実行していません。VRChat Upload／Client実機・Quest Build／Upload・PhysBone完全Simulationは未検証です。

開発環境は[Development~/README.md](Development~/README.md)、開発方針は[AGENTS.md](AGENTS.md)を参照してください。開発Project・個人Asset・LocalOnly／WorkSpaceはPackageへ含めません。

## License

[MIT](LICENSE) — Gonsaku / SILL-BILL。キャラクターモデルや実戦テスト用の私有Assetは同梱しません。
