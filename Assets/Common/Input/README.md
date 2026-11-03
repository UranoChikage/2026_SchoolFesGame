# SwitchInput（Switch/Joy-Con 共通入力）

全ゲーム（Pirates / Chambara …）が Joy-Con の生データを読むための共通窓口。
`namespace Common`、ファイルは `SwitchInput.cs` 1つだけ。

## 使い方（ゲーム側）
```csharp
using Common;

SwitchInput.IsConnected(i)                 // i番目のコントローラーが繋がっているか
SwitchInput.ConnectedCount                 // 繋がっている数
SwitchInput.GetAccel(i)                    // 加速度（Vector3）
SwitchInput.GetGyro(i)                     // 角速度（Vector3）
SwitchInput.ButtonDown(i, SwitchButton.Shoulder2)   // 押した瞬間
SwitchInput.Button(i, SwitchButton.Plus)            // 押している間
SwitchInput.ButtonUp(i, SwitchButton.Plus)          // 離した瞬間
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
- `GetAccel` / `GetGyro`
- `ButtonDown` / `Button` / `ButtonUp` と `ToSdk`（SwitchButton → SDKのボタン名の変換）

他のファイルは変更不要。

## Chambara で Joy-Con 版を作るには
`IChambaraInputContext` を実装するクラスを作り、`Read()` の中で `SwitchInput.GetAccel/GetGyro` を読んで
構え(pose)・振り(swing)に変換する。`DebugInputContext` と差し替えるだけでよい。
