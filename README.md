# Panda Motion Relay

Timeline上のDriverから、常時ActiveのTargetへHumanoid姿勢を転送するUnity UPMパッケージです。
Targetを切り替えたり無効化したりせず、揺れもの用のボーンを独立したまま維持する制作ツールを目指しています。

## 現在の実装（Phase 2B / 0.1.0）

- 複数Humanoid SourceをClipごとに指定できるTimeline Track / Clip。
- 隣接ClipのExact Cut。前後Scrub・直接Seek・繰り返しSeekに対応。
- AvatarのHumanPose（マッスル・身体位置・身体回転）を使ったリターゲット。
- Motion Root Relayは明示オプション・初期値OFF。ON時はSource Animator RootのWorld Position／RotationをTarget Animator Rootへ転送。
- TargetのScale・Humanoid Mapping外のボーンのLocal姿勢を保持。OFF時はRoot配置も保持。
- Editor Timeline PreviewとPlay Modeでの評価。
- 自動Blendなし。重複Clipは警告してRelayを停止。

Bake、Key Reduction、Generic Mapping、Driver Mesh補助UIは後続Phaseです。Blendは実装していません。
現段階では、本番用の完成したリターゲット／Bakeツールとしての利用は想定していません。

## 対応環境

最低対応：**Unity 2022.3.22f1**、Timeline **1.7.6**。
主開発：**Unity 6**。パッケージ本体にUnity MCPやVRChat SDKの依存はありません。

## インストール

Unity Package Managerの「Add package from disk」で、このリポジトリの`package.json`を選びます。
開発プロジェクトは`Development~/`配下に置き、ローカルパッケージとして参照します。
Editor Testを表示するにはプロジェクトの`Packages/manifest.json`に以下を追加します。

```json
"testables": ["com.sillbill.panda-motion-relay"]
```

## Timelineでの使い方

1. 独立したDriver / Targetを配置し、それぞれのAnimatorに有効なHumanoid Avatarを設定します。
2. PlayableDirectorとTimeline Assetを用意します。
3. 通常のAnimation TrackをDriver Animatorへバインドし、Driverのアニメーションを配置します。
4. `Motion Relay Track`を追加し、Target Animatorへバインドします。
5. Relay Trackに`Motion Relay Clip`を配置し、Inspectorの`Source`へDriver Animatorを指定します。
6. 複数Sourceを使う場合は、Clipを`[ Driver_A ][ Driver_B ][ Driver_A ]`のように隣接配置し、各Clipの`Source`を指定します。
7. Clipの区間をDriverのアニメーションに合わせ、Timeline PreviewまたはPlay Modeで確認します。

Sourceの評価規則は`clip.start <= 現在時刻 < clip.end`です。
Clipの開始時刻を含み、終了時刻を含みません。例えばFrame 100からDriver_BのClipが始まる場合、Frame 99はDriver_A、Frame 100はDriver_Bです。
Clipのウェイトによる混合や過去のSourceとの補間は行いません。複数Clipが占有するOverlapは警告し、その間は転送しません。
Gapでは保留中の転送も取消し、Targetへ新たな書き込みを行いません。前Sourceの追従を続けたり、次Sourceを先読みしたりはしません。
Gapに入る前の姿勢を積極的に復元することはなく、他のAnimationや制御もなければTargetのTransformは直前の値のままです。

TargetとDriverのRoot・Animator・Armature・ConstraintはActiveを維持してください。
Driverのメッシュを隠す場合はRendererの表示を変更してください。
TargetへRelay Trackを複数バインドする運用は未対応です。

## Motion Root Relay（オプション）

Timelineの`Motion Relay Track`を選択し、Inspectorの **Relay Motion Root** をONにします。
設定はTrack単位で全Relay Clipに適用され、初期値は **OFF** です。変更後はTimelineのGraphを再構築して評価します（スクリプトから変更する場合は`director.RebuildGraph()`）。

| 設定 | Target Rootの位置・回転 | Target Scale |
| --- | --- | --- |
| OFF（初期値） | 保持 | 保持 |
| ON | 選択Source Animator.transformのWorld Position／Rotationへ一致 | 保持 |

Source／Targetが異なるParent配下にあっても、Target Parent自体を変更せずWorld座標で転送します。
Humanoidの身体位置はRoot相対に正規化してから適用するため、Rootの移動を身体へ二重加算しません。
RootとPoseは同じ選択Sourceを同じ適用処理で参照し、Exact Cutの境界Frameで同時に切り替わります。
Source間の位置差を自動補正しません。Driver_AがX=0、Driver_BがX=3なら、Cut FrameでTargetもX=3へ移動します。

標準ParentConstraintの評価後World Motionを読む経路は、Editor TestからPlay Modeに入り、Anchor→Constraint→Driver→Targetの座標一致で検証しています。
Play Modeではアニメーションと標準Constraint評価後のLateUpdateでRootとPoseを読みます。
Editor Previewでは更新コールバックで最終Transformを読みますが、Edit Mode中のConstraint評価そのものをRelayが強制する機能はありません。
今回の生成RigではEdit Modeの`PlayableDirector.Evaluate()`だけではParentConstraintがSourceを更新しなかったため、**純粋なEdit ModeでのParentConstraint付きTimeline Previewは未検証**です。実Sceneで別途確認が必要です。

Custom Source／Target Root、Relative Offset、Position Only／Rotation Only、Blend、Bake、Generic Mappingは未対応です。
RootのScaleは正の均一Scaleを使用してください。
Humanoid外の髪・スカート等のLocal姿勢は転送対象外ですが、親の身体ボーンには追従します。
PhysBoneやVRChat固有Constraintとの実行順序は未検証です。

Timelineの`ProcessFrame`はアニメーションがTransformへ反映される前に実行されます。
このため、RelayはPlay ModeではLateUpdate、Editor PreviewではEditorの更新コールバックで適用します。
スクリプトから同期サンプリングする場合は、評価直後に明示的に転送を確定してください。

```csharp
director.time = sampleTime;
director.Evaluate();
SillBill.PandaMotionRelay.MotionRelayEvaluation.ApplyPendingPoses();
```

この明示適用はConstraintの評価を実行しません。Constraintを使う場合は、Unity側でそのFrameのConstraintが評価された後に呼んでください。

Clip外や無効な設定ではTargetを変更しません。通常のTimeline Previewの終了時は、登録したボーンの姿勢をUnityが復元します。
スクリプトからの`Evaluate()`単独利用やPlay Mode停止時にRelay自身が初期姿勢へ戻すことはありません。

## 検証

Editor Testは第三者モデルを使わず、テスト内でHumanoid Avatarを生成します。
姿勢転送、サイズ差、Root配置とScaleの保持、Secondary Bone除外、無効な設定、Timeline前後シークとTrack順、Editor Preview、Play Modeを確認します。
Phase 2Aでは、24／30／60fpsでのCut直前・Cut Frame・直後、A→B→A、Gap、Overlap、保留中の転送取消も検証します。
Phase 2Bでは、Motion Root ON/OFF、別Parent、Scale保持、身体位置の二重移動防止、Root／Pose同時Cut、実ParentConstraint評価後の転送も検証します。
実行結果と現在の制限は[VERIFICATION.md](VERIFICATION.md)を参照してください。

VRChat SDK環境での検証、VRChatアップロード、クライアントでの確認はまだ行っていません。

## 開発ポリシー

詳細は[AGENTS.md](AGENTS.md)と[初期開発指示書](PANDA_MOTION_RELAY_INITIAL_DEVELOPMENT_INSTRUCTIONS.md)を参照してください。
`Development~/`配下のAssets・Packagesのmanifest類・ProjectSettingsは、開発環境の復元用として管理します。
Unity生成物、`LocalOnly/`、`WorkSpace/`、認証情報・ローカルMCP設定・私物や再配布不可の素材はGit管理対象外です。

MIT License / Gonsaku
