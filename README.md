# Panda Motion Relay

Timeline上のDriverから、常時ActiveのTargetへHumanoid姿勢を転送するUnity UPMパッケージです。
Targetを切り替えたり無効化したりせず、揺れもの用のボーンを独立したまま維持する制作ツールを目指しています。

## 現在の実装（Phase 1 / 0.1.0）

- 1ソースHumanoid Relayと専用Timeline Track / Clip。
- AvatarのHumanPose（マッスル・身体位置・身体回転）を使ったリターゲット。
- TargetのRoot配置・Scale・Humanoid Mapping外のボーンのLocal姿勢を保持。
- Editor Timeline PreviewとPlay Modeでの評価。
- 自動Blendなし。重複Clipは警告してRelayを停止。

複数ソースの厳密なCut切替、Bake、Key Reduction、Generic Mapping、Driver Mesh補助UIは後続Phaseです。
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
5. Relay Trackに`Motion Relay Clip`を1つ配置し、Inspectorの`Source`へDriver Animatorを指定します。
6. Clipの区間をDriverのアニメーションに合わせ、Timeline PreviewまたはPlay Modeで確認します。

TargetとDriverのRoot・Animator・Armature・ConstraintはActiveを維持してください。
Driverのメッシュを隠す場合はRendererの表示を変更してください。
TargetへRelay Trackを複数バインドする運用は未対応です。

RelayはDriverのRootに対する身体姿勢を転送し、TargetのRootを移動しません。
Driver Rootの移動・ParentConstraintによる世界座標の移動をTargetへ転送するMotion Root機能は後続Phaseです。
RootのScaleは正の均一Scaleを使用してください。
Humanoid外の髪・スカート等のLocal姿勢は転送対象外ですが、親の身体ボーンには追従します。
実際のPhysBoneやConstraintとの実行順序は、SDK検証Phaseで確認します。

Timelineの`ProcessFrame`はアニメーションがTransformへ反映される前に実行されます。
このため、RelayはPlay ModeではLateUpdate、Editor PreviewではEditorの更新コールバックで適用します。
スクリプトから同期サンプリングする場合は、評価直後に明示的に転送を確定してください。

```csharp
director.time = sampleTime;
director.Evaluate();
SillBill.PandaMotionRelay.MotionRelayEvaluation.ApplyPendingPoses();
```

Clip外や無効な設定ではTargetを変更しません。通常のTimeline Previewの終了時は、登録したボーンの姿勢をUnityが復元します。
スクリプトからの`Evaluate()`単独利用やPlay Mode停止時にRelay自身が初期姿勢へ戻すことはありません。

## 検証

Editor Testは第三者モデルを使わず、テスト内でHumanoid Avatarを生成します。
姿勢転送、サイズ差、Root配置とScaleの保持、Secondary Bone除外、無効な設定、Timeline前後シークとTrack順、Editor Preview、Play Modeを確認します。
実行結果と現在の制限は[VERIFICATION.md](VERIFICATION.md)を参照してください。

VRChat SDK環境での検証、VRChatアップロード、クライアントでの確認はまだ行っていません。

## 開発ポリシー

詳細は[AGENTS.md](AGENTS.md)と[初期開発指示書](PANDA_MOTION_RELAY_INITIAL_DEVELOPMENT_INSTRUCTIONS.md)を参照してください。
`Development~/`配下のAssets・Packagesのmanifest類・ProjectSettingsは、開発環境の復元用として管理します。
Unity生成物、`LocalOnly/`、`WorkSpace/`、認証情報・ローカルMCP設定・私物や再配布不可の素材はGit管理対象外です。

MIT License / Gonsaku
