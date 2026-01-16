# EasyScreenRecord for Windows - 開発計画

## 1. プロジェクト概要
既存のmacOS版 [EasyScreenRecord](https://github.com/doggs696/EasyScreenRecord_Win) のWindowsネイティブ移植版を作成します。
「スマートズーム」「軽量」「モダンなUI」というコンセプトを継承し、Windowsの最新APIを用いて再現します。

## 2. 要件定義

### 2.1 技術スタック
| カテゴリ | 技術選定 | 理由 |
|---|---|---|
| **言語** | C# (.NET 8) | Windows APIへのアクセスが容易で、パフォーマンスと開発効率のバランスが良い。 |
| **UIフレームワーク** | WPF (Windows Presentation Foundation) | 自由度の高い描画、透明ウィンドウの扱い、オーバーレイ表示に強みがある。 |
| **キャプチャAPI** | Windows.Graphics.Capture | Windows 10/11標準の高性能キャプチャAPI。低遅延。 |
| **エンコード** | Media Foundation | OS標準のハードウェアアクセラレーション対応エンコーダー。 |
| **入力/フォーカス検知** | UI Automation / Global Hooks | テキストカーソル(キャレット)位置の正確な取得とキー入力検知のため。 |

### 2.2 主要機能
1.  **スマートズーム録画**
    *   **キャレット追従**: タイピング中のカーソル位置を自動追尾。
    *   **スムーズアニメーション**: ズームイン・アウト時の滑らかな補間処理。
    *   **トリガー**: キー入力、マウス操作、テキスト選択を検知してズーム発動。
2.  **高品質な範囲選択**
    *   画面全体をディミング(暗転)し、録画対象のみを明るく表示。
    *   直感的なドラッグ＆ドロップでの範囲指定。
3.  **キー入力可視化 (OSD)**
    *   録画中にタイプした内容をリアルタイムで画面上に字幕表示。
4.  **プレミアムUI**
    *   Windows 11ライクなモダンな設定画面。
    *   タスクトレイ常駐型で邪魔にならない設計。

### 2.3 アーキテクチャ (Windows版)

```mermaid
graph TD
    User[ユーザー] --> UI_Layer
    
    subgraph UI_Layer [Presentation Layer (WPF)]
        TrayIcon[タスクトレイアイコン]
        Selector[範囲選択オーバーレイ]
        Settings[設定ウィンドウ]
        Status[録画ステータス表示]
    end

    subgraph Logic_Layer [Application Logic]
        RecManager[Recording Manager]
        ZoomEngine[Smart Zoom Logic]
        InputListener[Input & Caret Listener]
    end

    subgraph Native_Layer [Windows API]
        WGC[Windows.Graphics.Capture]
        MF[Media Foundation (Video Enc)]
        UIA[UI Automation (Focus/Caret)]
    end

    UI_Layer --> RecManager
    RecManager --> ZoomEngine
    RecManager --> WGC
    RecManager --> MF
    InputListener --> UIA
    ZoomEngine --> InputListener
    ZoomEngine --> RecManager
```

## 3. 制作計画 (Production Plan)

### Phase 1: 環境構築とプロトタイピング
*   [ ] ソリューション構成の作成 (`src/EasyScreenRecord.Win`)
*   [ ] 既存のMac版コードを `reference/` に移動
*   [ ] 基本的なWPFアプリケーショの立ち上げ (トレイ常駐、設定画面)

### Phase 2: キャプチャと録画の基本実装
*   [ ] `Windows.Graphics.Capture` を使用した画面キャプチャの実装
*   [ ] キャプチャしたフレームを `Media Foundation` でMP4に保存するパイプライン構築
*   [ ] マウスカーソルの描画合成

### Phase 3: スマートズームエンジンの実装
*   [ ] `UI Automation` を用いたキャレット位置取得ロジックの実装
*   [ ] キャレット位置に基づく「ターゲット矩形」の計算
*   [ ] キャプチャフレームに対する「切り抜き」と「拡大(スケーリング)」処理の実装
*   [ ] ズームのスムージング処理 (線形補間/イージング)

### Phase 4: UIの作り込みと入力可視化
*   [ ] 範囲選択画面の実装 (半透明ウィンドウ、ラバーバンド選択)
*   [ ] キーボードフックによる入力検知と画面オーバーレイ表示
*   [ ] 全体的なデザイン調整 (Fluent Design準拠)

### Phase 5: テストと最適化
*   [ ] 長時間録画テスト
*   [ ] メモリリークチェック
*   [ ] DPIスケーリング対応確認

## 次のステップ
まずはフォルダ構造を整理し、Windows用のプロジェクト(WPF)を作成します。
Mac版のソースコードは参照用に残しておき、`reference` フォルダへ移動することを推奨します。
