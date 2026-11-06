# CornerPet

Windows 桌寵與 VPN 共享素材庫。現有版本：**v0.3.4**。

## 功能

- 匯入 GIF／PNG／JPEG／BMP，透明置頂顯示。
- 一般模式可拖曳，遊戲模式滑鼠穿透。
- **F7** 切換模式，**F8** 在主螢幕與副螢幕間移動。
- F7／F8 以全域鍵盤掛接觀察後傳遞，讓 Wuwa Tappo 等其他程式共用；不註冊獨占熱鍵。
- 重用顯示尺寸的繪圖緩衝區，隱藏時停止動畫並釋放圖片解碼器。
- 工業風控制台，沒有內建角色。
- 獨立素材主機：上傳、靜態縮圖、每頁 12 筆、下載套用與本機離線快取。
- SQLite 素材清單、SHA-256 去重與下載驗證、Windows DPAPI 保護共享密碼。

## 建置

Windows 10／11、.NET Framework 4.x。無 NuGet 套件需求；SQLite 使用 Windows 的 `winsqlite3.dll`。

在 PowerShell 執行：

```powershell
.\build.ps1
# 選用 64 位元：
.\build.ps1 -Architecture x64
```

輸出：

- `CornerPet.exe`：桌寵與素材庫客戶端。
- `CornerPet-LibraryHost.exe`：共享素材主機。

執行前請先從系統匣結束其他版本的桌寵。控制台標題會顯示版本。

## 共享素材庫

1. 主機和朋友先連上同一個 Radmin VPN 網路。
2. 主機開啟 `CornerPet-LibraryHost.exe`，選擇 VPN 的 `26.x.x.x` 位址、連接埠和共享密碼，再按「啟動服務」。
3. 朋友從桌寵控制台開啟「素材庫」，輸入 `http://主機位址:34827` 與密碼。
4. 瀏覽、上傳，或選取素材後「下載並套用」。動畫在使用者電腦上播放。

`127.0.0.1` 僅限本機測試。主機視窗關閉即停止分享；已下載素材仍能離線使用。單檔上限 20 MB，長寬各不超過 4096，動畫另有影格數與總像素量限制。

素材服務使用 HTTP，預期透過可信任的 VPN 使用，沒有內建 HTTPS。程式不自動修改防火牆、不設定路由器轉發，也不安裝開機服務。這是小群體原型，尚無個人帳號或公開網際網路部署功能。

## 本機資料

資料位於 `%LOCALAPPDATA%\CornerPet`：

- `settings.xml`：桌寵設定。
- `library-client.xml`／`library-host.xml`：連線設定，密碼以 DPAPI 加密。
- `SharedLibrary/`：主機資料庫、原圖和縮圖。
- `Downloads/`：本機素材快取。
- `shortcuts-status.txt`：快捷鍵處理方式及 F7／F8 動作次數，不記錄其他按鍵。

備份素材庫時先停止主機，再複製整個 `SharedLibrary`。不要把本機設定、密碼、素材或資料庫提交到 Git。

## 驗證

```powershell
Start-Process .\CornerPet.exe -ArgumentList '--self-test' -Wait
Get-Content .\test-results.txt

Start-Process .\CornerPet.exe -ArgumentList '--library-test' -Wait
Get-Content .\library-test-results.txt
```

桌寵檢查會短暫建立視窗；素材庫檢查使用合成圖片、臨時資料夾和 loopback 連線。提交前的本機驗證：49 項桌寵檢查、22 項素材庫檢查通過。這些不等於所有遊戲、跨電腦 VPN 或 Wuwa Tappo 實際操作的相容性保證。

## 程式結構

| 檔案 | 用途 |
| --- | --- |
| `CornerPet.cs` | 桌寵、控制台與設定 |
| `Native.cs` | Win32 逐像素透明繪圖緩衝區 |
| `GlobalShortcuts.cs` | 共用 F7／F8 的背景鍵盤掛接 |
| `ScreenPlacement.cs` | 主／副螢幕選擇與位置對應 |
| `ImageAsset.cs` | 磁碟快照與圖片解碼器生命週期 |
| `LibraryWindow.cs` | 本機／共享素材介面 |
| `LibraryClient.cs` | HTTP 存取、快取及下載驗證 |
| `LibraryStore.cs` | SQLite、素材驗證及縮圖 |
| `LibraryServer.cs` | 有共享密碼驗證的 HTTP 服務 |
| `HostProgram.cs` | 主機管理介面 |
| `SelfTest.cs`／`LibraryTests.cs` | 整合與回歸檢查 |

本儲存庫不包含角色圖像、使用者素材、執行檔或本機密碼，也未指定開源授權。
