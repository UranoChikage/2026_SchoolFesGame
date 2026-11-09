# SwitchInput（Switch/Joy-Con 共通入力）

全ゲーム（Pirates / Chambara …）が Joy-Con の生データを読むための共通窓口。
`namespace Common`、ファイルは `SwitchInput.cs` 1つだけ。

## 使い方（ゲーム側）
```csharp
using Common;

SwitchInput.IsConnected(i)                 // i番目のコントローラーが繋がっているか
SwitchInput.ConnectedCount                 // 繋がっている数
SwitchInput.Acceleration(i)                // 加速度（Vector3, 単位G）
SwitchInput.AngularVelocity(i)             // 角速度（Vector3）
SwitchInput.IsStill(i)                     // 静止判定。閾値は AccelerationNoise(0.02G) / AngularVelocityNoise(0.005)
SwitchInput.Angle(i)                       // 角度（オイラー角・度, Vector3。Directionから算出）
SwitchInput.TryGetDirection(i, out x, out y, out z)  // 向き＝正規直交な3ベクトル。零行列(未取得)ならfalseで捨てる
SwitchInput.ButtonDown(i, SwitchButton.Confirm)     // 押した瞬間
SwitchInput.Button(i, SwitchButton.Option)          // 押している間
SwitchInput.ButtonUp(i, SwitchButton.Option)        // 離した瞬間
// SwitchButton: Confirm(決定) / Cancel / Option / Up / Down / Left / Right
```
- 繋がっていないindexを渡しても落ちない（false / Vector3.zero が返る）。
- `Joycon` や `JoyconManager` をゲーム側から直接触らないこと。

## ルール
- ここには「物理ボタン・加速度」だけを置く。「発射」「舵取り」「振り判定」などゲームの意味づけは各ゲーム側で行う。
- PCデバッグ用のキーボード/マウスも各ゲーム側（Pirates: `GameInput` / Chambara: `DebugInputContext`）。
- indexの割り当ては各ゲームが決める。Pirates: 0=舵取り, 1〜3=砲手。

## SDKを差し替えるとき
`SwitchInput.cs` の `#region SDK呼び出し` の中だけ書き換える。
- 接続数（`ConnectedCount`）
- `Acceleration` / `AngularVelocity` / `TryGetDirection`（`Angle` はDirectionから計算するので触らない）
- `ReadButton`（抽象ボタンが押されているかのbool。押した瞬間/離した瞬間は `SwitchInput` が前フレームとの差で求める）と `ToSdk`（抽象ボタン → SDKのボタンの対応。現状は Confirm=ZR / Cancel=R / Option=Plus / 方向=十字キー）

他のファイルは変更不要。

## Chambara で Joy-Con 版を作るには
`IChambaraInputContext` を実装するクラスを作り、`Read()` の中で `SwitchInput.Acceleration/AngularVelocity` を読んで
構え(pose)・振り(swing)に変換する。`DebugInputContext` と差し替えるだけでよい。
