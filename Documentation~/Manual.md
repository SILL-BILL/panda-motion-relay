# Panda Motion Relay Manual

Version 0.1.0 — Production Preview / Internal Production Use（実戦投入前プレビュー版・社内／個人制作向け）

このマニュアルは、HumanoidキャラクターをUnityのTimelineで動かしたことがあるアニメーター向けです。読み始める場所は[インストール](#3-インストール)、最初の作業は[最小構成](#5-最小構成)です。数値実績は[VERIFICATION.md](../VERIFICATION.md)、概要は[README.md](../README.md)を参照してください。

## 1. このツールでできること

複数の動作用キャラクターから、表示用キャラクターへHumanoid姿勢を転送します。Timelineで転送元を切り替えても、表示用キャラクター自体は常時Activeを維持できます。髪・スカート等のHumanoid Mapping外のボーンを直接駆動しないことで、揺れものを別の仕組みで動かす余地を残します。揺れものシステム全体の状態維持を保証するものではありません。

制作後は、同一骨格のTarget専用Generic AnimationClipへBakeできます。別キャラクターに使い回すHumanoid muscle Clipを出力するツールではありません。MITライセンスの独立UPMパッケージで、VRChat SDKやUnity MCPは利用に不要です。

身体姿勢の転送にはAnimator AvatarのHumanPose（マッスル・身体位置・身体回転）を使います。身体位置をRoot相対に正規化し、Driverの配置を身体へ二重加算しない経路です。TargetのScaleとMapping外の骨のLocal姿勢を保持します。

## 2. 対応環境

| 項目 | 条件 |
| --- | --- |
| 最低Unity | 2022.3.22f1 |
| 主開発環境 | Unity 6（検証版6000.0.84f1） |
| Timeline | package.jsonの依存1.7.6。Unity 6の既存Projectでは検証済み1.8.13も使用 |
| Source / Target | 有効なHumanoid Avatarを持つ独立したAnimator |
| Bake | EditorのPlay Mode、Pause解除 |
| 公開範囲 | Production Preview。全Unity・VRChat環境の保証ではありません |

## 3. インストール

### Add package from disk

1. Unity Projectの外へRepositoryを取得・展開し、`package.json`があるフォルダーを保存します。
2. Unityで`Window > Package Manager`を開きます。Unityの版によっては`Window > Package Management > Package Manager`です。
3. 左上の`+`から`Add package from disk...`を選びます。
4. `panda-motion-relay/package.json`を選び、Resolveとコンパイルの完了を待ちます。Timeline依存もPackage Managerが解決します。
5. ConsoleにCompile Errorがないことと、`Tools > Panda Motion Relay > Bake Humanoid to Generic`があることを確認します。

参照中のRepositoryフォルダーは移動・削除しないでください。Unity ProjectをそのRepositoryの内側に作らないでください。

```text
workspace/
├─ panda-motion-relay/    ← package.json
└─ my-animation-project/  ← Assets / Packages / ProjectSettings
```

通常利用に`testables`設定は不要です。開発者がパッケージのEditor TestをTest Runnerへ公開する場合だけ、Projectの`Packages/manifest.json`に`"testables": ["com.sillbill.panda-motion-relay"]`を追加し、Unity Test Frameworkを用意します。TestsはEditor専用で、Player用Assemblyに含めません。

### Add package from git URL

Gitがインストールされ、Unityから実行できる環境を用意します。Package Managerの`+ > Add package from git URL...`へ、検証済みの次のどちらかを入力します。

Branch追従（Unity 6の新規Projectで導入・Preview・Bake確認済み）：

```text
https://github.com/SILL-BILL/panda-motion-relay.git#feat/bake-evaluation-director
```

Commit固定（Unity 2022.3.22f1の新規Projectで確認済み）：

```text
https://github.com/SILL-BILL/panda-motion-relay.git#a8003ec7347d5c55b805af47c623cfaa49cf6ea3
```

再現性を保ちたい案件はCommit固定を使い、Branch追従は更新で内容が変わることに注意してください。両URLは同じ機能コードの0.1.0へ解決しました。認証情報なしのHTTPS読取も確認済みです。SSH URL導入は今回検証していないため、導入例として扱いません。

上記は過去の承認済みLocal／Global機能コードの導入確認URLです。Phase 3DのWorld → Root実装と今回のManual追記は未commit・未pushのため、そのCommitには含まれません。タグ付きReleaseもまだ用意していません。World → Rootを使う際は、この実装を含む承認済みの配布版かローカルPackageを用意してください。

Unity公式の[ローカルPackage導入](https://docs.unity3d.com/2022.3/Documentation/Manual/upm-ui-local.html)と[Git依存関係](https://docs.unity3d.com/ja/2021.3/Manual/upm-git.html)も参照できます。

## 4. 用語

| 用語 | このツールでの意味 |
| --- | --- |
| Driver | 元のモーションやConstraintで動かすキャラクター |
| Target | 転送結果を表示する、常時Activeのキャラクター |
| Source | あるRelay Clipが選ぶDriverのAnimator |
| Motion Relay Track | Target AnimatorをBindingするTimelineの行 |
| Motion Relay Clip | 区間ごとのSourceを指定するTimeline Clip |
| Relay Motion Root | SourceのAnimator Rootの最終World位置・回転も転送するTrack設定 |
| Relay Director | Motion Relay Trackを所有するTimelineを再生するPlayableDirector |
| Evaluation Director | Bake中に全体を評価するDirector。必要ならMasterを指定 |
| Local Bake | 現在の親階層・親の動きを活かすTransform Curveの出力 |
| World → Root | 評価後Targetの最終World動作を、独立モデルのAnimator Rootと身体のCurveへ記録する出力 |
| World → Hips（従来のGlobal） | 最終World Motionを、固定Reference空間に対するHips Curveへ集約する出力 |
| Generic Clip | 骨格内のTransform Pathを直接動かす、Target専用のAnimationClip |

## 5. 最小構成

初回はConstraintなし・同じ骨格のコピー3体で始めると、転送とRoot配置を切り分けやすくなります。モデルには利用権のあるHumanoidを使ってください。本パッケージはキャラクターモデルを同梱しません。

```text
Driver_Normal ─┐
               ├─ Motion Relay ─→ Target
Driver_Float  ─┘
```

1. ProjectにSceneを作って保存します。
2. キャラクターを3体配置し、`Driver_Normal`、`Driver_Float`、`Target`と命名します。
3. 各キャラクターで、骨格を制御するAnimatorとHumanoid Avatarを確認します。
4. 空のGameObjectを作り`RelayTimeline`と命名します。
5. `RelayTimeline`を選び、`Window > Sequencing > Timeline`を開いて`Create`でTimeline Assetを保存します。PlayableDirectorがこのObjectに付き、そのPlayableに作成Assetが入っていることを確認します。
6. 次章以降に従いDriverのAnimation Track、TargetのRelay TrackとSourceを設定します。

Scene内の3体のAnimator、Armature、親ObjectをActiveにしたまま使います。

## 6. Driver / Target準備

モデルのImport Settingsで`Rig > Animation Type = Humanoid`にし、Avatarの`Configure`で必要な骨が割り当てられていることを確認します。各SceneインスタンスのAnimatorに、その有効なAvatarが設定されている必要があります。Root移動専用でAvatarを持たないAnimatorではなく、Humanoid骨格を扱うAnimatorを選んでください。

DriverとTargetは別Object・別骨格にします。片方をもう片方の子にはしません。Targetの身体に別AnimatorやAnimation Trackから同時に姿勢を書き込む構成は避けます。初回は正の均一Scaleを使ってください。

TimelineへDriverごとの通常の`Animation Track`を作り、各Driver AnimatorをBindingします。既存Humanoid AnimationClipを置き、Driverだけで期待どおり再生されることを先に確認します。Float用のDriverは、必要なら乗り物・PropのConstraintへ追従させます。

制作中はDriverのメッシュを表示して構いません。最終的に隠すときはRendererの表示をOFFにします。DriverのRoot、Animator、Armature、Constraintを無効化しないでください。表示切替専用UIは未実装です。

## 7. TimelineへのMotion Relay Track追加

`RelayTimeline`をHierarchyで選んだ状態で、Timelineの空白Track領域を右クリックし、追加メニューから`Motion Relay Track`を選びます。Unityの版によって名前空間のサブメニュー内に表示されます。Track左側のBinding欄へ、TargetのHumanoid Animatorをドラッグします。

同じTargetへBindingする有効なRelay Trackは1本にしてください。Root移動用ObjectやモデルAssetそのものをBindingしないでください。SceneインスタンスのAnimatorを使います。

## 8. Motion Relay Clip / Source設定

Relay Trackの空白部分を右クリックし、`Motion Relay Clip`を追加します。追加メニューの接頭辞が`Add`等になることがあります。

Timeline上のRelay Clipを選択し、Inspectorの`Source`へDriverのHumanoid Animatorを指定します。SourceはそのTimelineを再生するDirectorで解決されるScene参照です。Timelineウィンドウを別Objectへ切り替えず、対象Directorを選択した状態で設定してください。

例として、ProjectウィンドウでTimeline Assetを選び、Inspectorの`Frame Rate`を24にします。Clip Inspectorの`Start`と`Duration`を次のように設定します。秒表示の場合は0／1／2秒から各1秒、Frame表示の場合は下表の値です。24fpsの「24 Frame」と「24秒」を取り違えないでください。

| Clip | Start Frame | Duration Frame | Source |
| --- | --- | --- | --- |
| 1 | 0 | 24 | Driver_Normal |
| 2 | 24 | 24 | Driver_Float |
| 3 | 48 | 24 | Driver_Normal |

設定後にTimeline上でClipの端が0／24／48／72 Frameへ揃っていることを確認します。

Driver側のアニメーションも、この区間で必要な姿勢を出すように配置します。Driver用Animation ClipとRelay Clipは別物です。

```text
Driver animation:  [ Normal ][ Float ][ Normal ]
Relay selection:  [ Normal ][ Float ][ Normal ]
Frame:             0       24      48       72
```

## 9. Exact Cut

所有区間は`start <= t < end`です。上の例ならFrame 23はNormal、Frame 24はFloat、Frame 48はNormal、終端Frame 72はSourceなしです。境界をTimeline Frameに揃え、Clipを隙間なく隣接させます。自動Blend、EaseやClip WeightによるSource混合はありません。

Overlapすると警告し、その区間は転送しません。Gapでは転送と保留中の書き込みを取り消します。他の制御がなければ直前のTarget姿勢がTransform値として残りますが、前Sourceへの追従を続けるわけではありません。

## 10. Relay Motion Root

Relay Track自体を選択し、Inspectorの`Relay Motion Root`を設定します。初期値はOFFです。

| 設定 | Target Animator Root | Target Scale |
| --- | --- | --- |
| OFF | 元の位置・回転を保持 | 保持 |
| ON | 選択Source Animator Rootの最終World位置・回転へ転送 | 保持 |

FloatやVehicleへのConstraint追従もTargetへ伝えたい場合はONが候補です。異なるParent下でもTarget Parent自体は変更しません。Rootと身体姿勢は同じSourceから同時に切り替わります。Source間の配置差を補正しないため、切替直前に両Driverの立ち位置・向きを意図どおり合わせてください。

設定変更後はTimelineのPreviewを一度終了し、再評価してください。スクリプトから設定を変えた場合は`director.RebuildGraph()`でGraphへ反映します。

## 11. Preview確認

まずTimelineのScrubでSourceとTargetの姿勢を確認します。次にSceneを保存してPlay Modeに入り、Timelineを再生します。前述の例なら23/24/25、47/48/49、終端72を見て、1Frameずれ・意図しないBlend・Target非Active化がないことを確認します。

ParentConstraint付きの確認はPlay Modeを基準にします。RelayはPlay ModeのLateUpdateでアニメーション・標準Constraint評価後の姿勢を読みます。Editor Previewでは更新コールバックを使いますが、Edit ModeのConstraint評価自体を強制しません。純粋なEdit ModeでのParentConstraint Preview／Bakeを保証しません。

通常のTimeline Preview終了時は、登録したボーンの姿勢をUnityが復元します。スクリプトのEvaluate単独利用やPlay Mode終了時にRelay自身が初期姿勢へ戻す機能はありません。

### スクリプトによる確認（通常操作では不要）

`ProcessFrame`だけではアニメーション反映前なので、同期サンプリングでは最終評価後に適用を確定します。

```csharp
director.time = sampleTime;
director.Evaluate();
SillBill.PandaMotionRelay.MotionRelayEvaluation.ApplyPendingPoses();
```

この呼び出し自体はConstraintを評価しません。Constraint付きサンプリングでは、Unityの該当FrameのConstraint評価が完了してから適用してください。

## 12. Local Bake

Localは現在のHierarchyとParent Motionを活かすBakeです。Root Motion用Animatorと身体用Animatorを分けた制作構成を維持したい場合に向きます。

### Bake前の必須確認：通常用・Float用の両Driverを常時Activeにする

**Sourceの切替はRelay Clipで行います。通常用・Float用のDriver本体は、使っていない区間も含めて、Bake中ずっと両方Activeにしてください。** Targetも常時Activeを維持します。この準備はLocal／World → Root／World → Hipsのすべてに必要です。

Activation TrackのチェックをONにするだけでは、この条件を満たしません。Trackが有効でも、緑色のActive Clipがない区間ではキャラクターが非Activeになるためです。

検証用のScene／Timelineコピーで、次のように準備すると確実です。

1. 通常用・Float用のDriver本体や親Objectの有効／無効を制御する、**両方のActivation Trackをミュート**します。モーションを再生するAnimation Trackや、Prop等を評価するControl Trackはミュートしません。
2. Hierarchyで、**両Driver本体とその親ObjectをすべてActive**にします。Animatorを有効にし、Armatureと必要なConstraintも動作する状態を維持します。TargetもActive・Animator有効にします。
3. Driverを非表示にしたい場合は、**RendererだけをOFF**にします。DriverのRoot ObjectやAnimatorを無効化して隠さないでください。
4. Scene／Timelineを保存してからPlay Modeに入り、通常区間・Float区間のどちらでも両DriverがActiveのままであることを確認します。

Bake開始時は、選択したRelay Trackの**全ClipのSource**を検証します。現在の区間で使うSourceだけがActiveでも、別区間のSourceが非Activeなら開始できません。`Every Relay Clip requires an active, valid, independent Humanoid source.` が出た場合は、この準備と各ClipのSource指定を確認してください。

### Bakeの実行

1. Previewを確認してSceneを保存し、**Play Mode・Pause解除**にします。
2. Relay Trackを所有するDirectorのObjectを選び、`Tools > Panda Motion Relay > Bake Humanoid to Generic`を開きます。
3. `Relay Director`（転送設定があるTimelineを動かすObject）へ、Relay Trackが入っている側のObjectを指定します。
4. `Evaluation Director`（全体をまとめて動かすObject）は、単一Timelineなら空欄にします。複数Timelineをまとめて動かす構成では14章の探し方・指定表を使います。
5. `Relay Track`を選択し、`Bake Mode`を`Local — Use Existing Parent`にします。
6. `Bake to New Animation Clip`を押し、ProjectのAssets配下に**新規**`.anim`の保存先を指定します。既存Assetの上書きは拒否されます。
7. Progress終了後、生成Clipを同一骨格のGeneric hierarchyで確認します。Progress表示中の取消が可能です。

終了・取消・例外時に、影響するDirectorの時刻／更新モード／再生状態／Graph有無と、SceneのTransform／Parent／Active／Renderer／Animator有効状態を復元します。任意のスクリプトや揺れものSimulationの内部状態を巻き戻す機能はありません。

採録する値は、Unityのアニメーション・標準Constraint・Relay LateUpdateを経たTarget実Transformです。次のUpdateで採録するため、非表示のGame Viewで停止しやすいWaitForEndOfFrameには依存しません。

| 対象 | Local出力 |
| --- | --- |
| Humanoid Mapping済みHips | Local Position + Quaternion Rotation |
| 他のMapping済み骨（指を含む） | Local Quaternion Rotation |
| Animator Root（Relay Motion Root ON時） | Parent空間のLocal Position + Quaternion Rotation、計7 Curve |
| Scale／Mapping外の骨 | 出力なし |

採録範囲は最初のRelay Clip start〜最後のend（終了Frameを含む）です。TimelineのFrame Rateで1Frameごとに採録し、Clip時刻0が採録開始時刻に対応します。Frame Rateは0より大きく240以下、範囲は最大100000 Frameです。境界のFrame整列、Overlap、Source／Target、出力先を採録前に検証します。

Quaternion符号の連続性とCut直前のConstant Tangentを保持します。Key Reduction／Compressionはまだ行わず、各FrameのKeyが残ります。Gapもその時点のTarget実Transformを採録します。

## 13. World Bake：Rootへ記録／Hipsへ集約

### World — Record Motion to Root（独立モデル用・Rootへ記録）

元の入れ子RootやParentConstraintを持たないモデルで、RootのWorld移動・回転も含めて再現したい場合に使います。12章と同じ操作で`Bake Mode`から`World — Record Motion to Root`を選び、新規`.anim`へ保存します。

採録するのは**Master・Constraint・Relay評価後のTargetの最終World Pose**です。Source Rootを直接記録する別機能ではありません。PreviewでTargetへ動きが転送できていることを先に確認し、SourceのRoot移動も必要な場合はRelay Motion RootをONにします。

| 対象 | World → Root出力 |
| --- | --- |
| Animator Root（Animatorが付いているObject） | World位置・回転をIdentity Parent基準のLocal Curveに保存。Position 3＋Quaternion 4 |
| Hips | 内部Skeletonの親World Poseから再構成したLocal Position 3＋Quaternion 4 |
| その他のMapped Bone | 親World回転から再構成したLocal Quaternionのみ |
| Scale／Secondary・Unmapped Bone | 出力なし |

**再生先の親は位置0・回転0・Scale 1とし、元の親AnimationやConstraintを重ねません。** 内部SkeletonのPath・固定Helper・一定Scaleは、採録開始FrameのTargetと同等にします。Scale Curveを出さないため、Character自身の一定Scaleには採録開始FrameのTargetのWorld Scaleを使います。親から外しても見た目の大きさを保った検品用コピーを用意すると確認しやすくなります。上位のRoot用Objectを複製する必要はありません。

「全体を移動するObject」への任意Bindingではなく、Clipを同一骨格CharacterのAnimator Rootへ適用します。Avatarなしの外側Rootだけを対象にするGeneric Root-only Bakeは別の未実装機能です。

この方式だけは、全範囲の最終姿勢を先に評価して検証し、その後もう一度評価してCurveを採録します。検証中も取消可能です。非均一／可変／0以下のScale、Mirror、Shear、Hierarchyの親変更、Hips以外のMapped Bone Translation、内部のUnmapped Helperの移動・回転変化は拒否します。最初のFrameで不変の内部配置・Scaleを確定し、必要な追加Curveを黙って省略したClipは保存しません。Scale／Shearの静的異常は開始時にも検証します。

この検証は揺れものSimulationを記録するためのものではありません。任意のスクリプト・物理状態が時間のSeekで再現しない場合は結果を保証しません。個別モデルの検証結果を確認してください。

### World — Consolidate Motion to Hips（従来のGlobal）

12章と同じ操作で`Bake Mode`から`World — Consolidate Motion to Hips`を選び、別の新規`.anim`へ保存します。最終World Motionを、採録開始時の固定Referenceに対するHips Position／Quaternion Rotationへ集約します。既存APIの`BakeSpace.Global = 1`はこの方式のままです。新方式は`BakeSpace.World = 2`、Localは従来どおり0です。

**GlobalはAnimator Root Curveを出しません。再生先は採録開始時と同等のNeutral／Frozen Reference hierarchyを用意し、元のRoot／Parent Motionを重ねないでください。**

Referenceは、最初のFrameのConstraintとRelayが完了した時点の「Hipsの親」のWorld行列・回転です。Bake前の任意の配置やワールド原点ではありません。この行列を固定して通常の`m_LocalPosition`／`m_LocalRotation`へ変換します。他のMapping済み骨はLocal Quaternion Rotationのままで、Scale／Secondary Curveは出しません。Relay Motion RootのON/OFFはPreview生成にはそのまま反映されます。

| 目的 | 選択の目安 |
| --- | --- |
| 現在のRoot／Character階層と親のAnimationを維持 | Local — Use Existing Parent |
| Root移動用とCharacter用Animatorを分離運用 | Local — Use Existing Parent |
| 元の入れ子・Constraintなしで、モデルRootのWorld軌道も再現 | World — Record Motion to Root |
| 上位Root／Parentの移動をHipsへ集約し、モデルRootは固定 | World — Consolidate Motion to Hips（旧Global） |

Globalは任意Hierarchy・任意原点でWorld Poseを一致させる機能ではありません。可変／非一様ScaleやShearを含む全身再現は保証しません。

## 14. Relay Director / Evaluation Director

### 英語名より「どちらのObjectを入れる欄か」で考える

ここでいうDirectorは、Timelineを動かす`Playable Director`コンポーネントです。Bake Windowには、そのコンポーネントが付いた**Hierarchy内のObject**を指定します。ProjectウィンドウにあるTimeline Assetや、キャラクターのAnimatorを入れる欄ではありません。

| Bake Windowの英語表示 | 日本語での役割 | 指定するもの |
| --- | --- | --- |
| Relay Director | **転送設定がある側** | Motion Relay Trackが入っているTimelineを動かすObject |
| Evaluation Director | **全体をまとめて動かす側** | キャラクターと乗り物・小道具など、必要なTimelineをまとめて動かすObject。Timelineが1つだけなら空欄 |
| Relay Track | **どの転送設定をBakeするか** | TargetのHumanoid AnimatorをBindingしたMotion Relay Track |

例えば、キャラクター用と乗り物用のTimelineを、1つの親Timelineから動かす場合です。次のObject名は説明用なので、自分のSceneでは名前が違っていても役割で選んでください。

| 説明用のObject名 | 担当する仕事 | Bake Windowでの指定先 |
| --- | --- | --- |
| Character | キャラクター用Timelineを動かす。ここにRelay Trackがある | Relay Director |
| Prop | 乗り物・小道具用Timelineを動かす | このObjectを直接指定する欄はない |
| Master | CharacterとPropの両Timelineをまとめて動かす | Evaluation Director |

### Hierarchyから指定するObjectを探す手順

1. キャラクター用Timelineを表示しているObjectをHierarchyで選びます。Inspectorの`Playable Director`に入っているTimelineを開き、**Motion Relay Trackがあること**を確認します。このObjectをBake Windowの`Relay Director`へドラッグします。
2. キャラクターと乗り物・小道具を一緒に動かす親Timelineがある場合は、そのTimelineを動かすObjectをHierarchyで選びます。親TimelineのControl Track／Control Clipから、キャラクター側と乗り物側のDirectorが参照されていることを確認します。このObjectを`Evaluation Director`へドラッグします。
3. `Relay Track`欄では、TargetのHumanoid AnimatorをBindingしているRelay Trackを選びます。Root移動専用AnimatorをBindingした別のTrackを選ばないでください。
4. Timelineが1つだけで完結している場合は、2の作業は不要です。`Evaluation Director`を空欄にすると、`Relay Director`と同じObjectが使われます。

**Hierarchyで見た目が親子になっているだけでは、まとめて再生する設定にはなりません。** Evaluation Directorを選ぶ際は、親TimelineのControl Clipで必要な子Directorを動かしていることを確認してください。

### 設定の早見表

| 構成 | Relay Director | Evaluation Director |
| --- | --- | --- |
| 単一Timeline | Relay Track所有Director | 空欄（同じDirectorを使用） |
| Master直下にCharacterとProp | Character Director | Master Director |

BindingとSource参照はRelay Directorで解決します。Bake範囲とFrame RateもRelay Timeline基準です。Evaluation Directorは実際に時間を進める役割で、Target BindingをMasterへ移す必要はありません。

## 15. Nested Timeline

```text
Master Director
├─ Control Clip → Character Director / Timeline ← Relay Track
└─ Control Clip → Prop Director / Timeline → Constraint入力
```

この場合はEvaluation DirectorへMasterを指定します。Character単独ではProp側のConstraint入力が進まないためです。MasterがCharacterとPropを同じScopeで評価し、Relay Directorを別途二重評価しません。

Masterに、Relay Directorを直接制御する有効なControl Clipが**一意**に必要です。Control Clipの`updateDirector`が有効で、解決されたSource GameObjectにRelay Directorがある構成を使います。採録範囲全体を同じControl Clip内に収めます。

Master時刻への変換は`control.start + (relayTime - control.clipIn) / control.timeScale`です。複数Control Clipにまたがる範囲、曖昧なsearchHierarchy、Prefab生成Director、多段Nested制御、0以下／非有限の速度、範囲外やChild時間のループは拒否されます。

## 16. Bake済みGeneric Clipの再生

Bake成果物はTarget専用Transform Curveです。同一骨格の確認用コピーを作り、元のHumanoid Avatarを外すか適切なGeneric Avatarを使います。元TargetのAvatarをBake操作が変更することはありません。

1. 確認用コピーのAnimatorでHumanoid再Retargetを通さない状態にします。
2. 別のAnimator Controllerに生成ClipをStateとして入れるか、通常のTimeline Animation Trackへ配置してそのAnimatorへBindingします。
3. その確認用キャラクターをRelayや別の身体Animationから同時駆動しないでください。
4. LocalではBake時と同等のParent Motionを再現します。既にClipが持つAnimator Root Motionを別途重ねないでください。
5. World → RootではIdentity Parentと開始Frameと同等の内部Skeleton／Character一定Scaleを用意します。World → Hips（旧Global）では開始Frameの固定Reference階層を再現します。どちらも元の上位Root／Parent Animationを重ねません。
6. 頭・手足・Hips・全体位置、Cut前後、最初と最後のFrameを比較します。

FBXのRig設定を変えて参照元全体をGeneric化する必要はありません。検品用コピーで適切な再生設定にしてください。別キャラクターへのHumanoid再Retargetでは一致を保証しません。

## 17. VRChat制作フロー

```text
Unity 6 / 通常Unity 2022.3
  Preview → Bake → Generic再生を数値確認
                         ↓ 完成Clip
VCC / ClientSim ON
  Bake済みClipを再生して納品物を検品
```

VCCは完成Clipの検品環境です。VCC上のリアルタイムRelay Preview／Bakeは標準保証範囲外です。Frame 138でRoot位置差0.001580238、回転差0°を観測し、ClientSim OFFでも再現しています。原因未解決・未修正ですが、標準フローではKnown / Non-blockingとして扱います。

VRC SDKを本パッケージへ依存追加する必要はありません。検品用World ProjectはVCCで作成してください。Upload、Client実機、Quest Build／Upload、PhysBone完全Simulationの検証実績はありません。

## 18. Troubleshooting

| 症状 | 確認すること |
| --- | --- |
| Trackがメニューに出ない | Package ManagerのResolve完了、Console Compile Error、Timeline依存、対象Timelineを開いているか |
| TargetへBindできない | SceneインスタンスのAnimatorか。Root移動専用でなくHumanoid骨格側か |
| Sourceを指定できない | Relay Clipを選んでいるか。正しいDirectorを選択し、SourceにSceneのHumanoid Animatorを指定しているか |
| Poseが動かない | Source/Target Avatar、Active、Animator enabled、Clip内時刻、Binding、他の身体Animationとの競合 |
| Rootが動かない | Relay TrackのRelay Motion Rootと、Preview再評価。初期値OFF |
| Cutで飛ぶ | 切替前後のDriverのWorld配置・向き。Relative Offset補正はない。境界がFrame整列しているか |
| Overlap warning | ClipのStart／Durationを確認し、重なりを解消する |
| Bakeを実行できない | Play Mode・Pause解除、Relay Trackの存在、未使用のAssets配下出力先、Validationメッセージ |
| `Every Relay Clip requires an active, valid, independent Humanoid source.` が出る | 全ClipのSourceを確認。通常用・Float用の両DriverのActivation Trackをミュートし、本体・親Objectを常時Active、Animatorを有効にする（12章）。SourceにはTarget自身やRoot移動専用Animatorではなく、独立した有効なHumanoid Animatorを指定する |
| BakeだけConstraint結果がずれる | CharacterのほかにPropを評価する必要がないか。NestedならEvaluation DirectorへMasterを指定 |
| Local Bake再生がずれる | Generic設定、同一骨格Path、同等のParent Motion、Root Motionの二重適用 |
| Globalで二重移動する | 元のRoot／Parent Animationを外す。任意原点ではなく開始FrameのFrozen Reference階層で再生 |
| World → RootでScale／TranslationのErrorが出る | 非均一・可変・負ScaleやShearを除く。Mapped骨のHips以外の移動、内部HelperのAnimationが必要な構成は今回未対応。表示されたTransform名を確認し、Scale／追加Positionを暗黙にBakeする設定だと思わないこと |
| World → Rootの位置は合うが身体サイズが違う | 再生先の親をIdentityにし、Character自身の一定Scaleを採録開始FrameのTarget World Scaleに合わせる。内部SkeletonのPath・固定Helperも同じ構成を使う |
| Timeline Missing Script | ProjectをPackage Repository内に置いていないか。Siblingへ分離し、Local参照の解決先を確認する |

Project自体を含む親フォルダーをLocal UPM Packageとして参照する構成は避けてください。Unity 6／Timeline 1.8.13でTimelineAssetの型関連付けを壊すことが確認されています。このレイアウト問題をLibrary削除やTimeline YAMLの繰り返し編集で解決しようとしないでください。

## 19. Known Limitations

- Blend、Relative Offset、Custom Source/Target Root、Position Only／Rotation Onlyは未対応。
- Generic Mapping、Scale Bake、Secondary Bone Bake、Key Reduction、Batch Bake、Custom FPS UIは未対応。
- Avatarなしの外側Root／任意TransformだけのGeneric Root Bakeは次Phase候補で、今回のWorld → Rootとは別機能。
- TargetはHumanoidのみ。同一Targetへの複数Relay Track同時Bindingは未対応。
- Humanoid Mapping外の髪・スカート等のLocal姿勢は転送しませんが、親の身体ボーンには追従します。
- Motion RootはAnimator.transformを使います。正の均一Scaleを前提とし、非一様／可変ScaleやShearの再現を保証しません。
- Pure Edit Mode ParentConstraint Bake保証なし。PhysBone等やVRChat固有Constraintとの実行順序・実機Simulation保証なし。
- Nested Bakeは15章の一意な直接Control Clipのみ。VCCリアルタイムRelay Previewは標準保証外。
- 個別案件ではSource・Target・Parent構成を確認し、納品用Clipを別階層で再生検証してから採用してください。

## 20. 検証済み環境

Unity 6000.0.84f1／Timeline 1.8.13とUnity 2022.3.22f1／Timeline 1.7.6で、既存119件を含む各142 Editor Test成功。Local／World → Root／World → Hipsの実戦Bakeが両Unityで数値PASSし、新方式のNative Generic Animator Controller再生も375FrameでPASSしています。従来方式のVCC成果物検品実績と新方式の追加結果はVERIFICATIONを参照してください。

新規Projectへの導入試験、Exact Cut／Constraint／状態復元の実測値、再現手順の範囲は[VERIFICATION.md](../VERIFICATION.md)が正本です。ここでの検証は全案件やVRChat Upload／実機の保証ではありません。

## 開発用Projectの補足

Package Repositoryがコードの正本です。開発ProjectはSiblingへ作り、`Packages/manifest.json`から参照する場合は`"com.sillbill.panda-motion-relay": "file:../../panda-motion-relay"`を指定します。相対パスはProject Rootではなく`Packages`基準です。解決先はPackageInfo.resolvedPathで確認します。

外部ProjectはGit管理せず、`Development~/`は[セットアップ説明](../Development~/README.md)専用です。Unity 6を開発環境、Unity 2022.3.22f1を互換ゲート、VCCを完成Clipの検品環境として使います。個人・再配布不可Assetは外部ProjectのLocalOnly／WorkSpaceで扱い、Packageへ含めません。

開発方針は[AGENTS.md](../AGENTS.md)、設計の出発点は[初期開発指示書](../PANDA_MOTION_RELAY_INITIAL_DEVELOPMENT_INSTRUCTIONS.md)を参照してください。
