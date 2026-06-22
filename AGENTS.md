# AGENTS.md

本文件记录 ScreenGuides 项目的本地协作与发布规则。

## 修改与提交

- 每次修改项目文件后，都必须同步修改版本号。
- 每次修改完成后，都必须进行一次本地 git commit，并推送到远端。
- 版本号当前采用 `v0.x.y` 测试版节奏；未指定版本号时，默认递增 patch 版本。

## 构建与发布产物

- 发布单文件程序时使用：

```powershell
dotnet publish -c Release
```

- 每次生成单文件程序后，必须将发布目录中的 `ScreenGuides.exe` 重命名为：

```text
屏幕辅助线工具.exe
```

- 发布目录为：

```text
bin\Release\net8.0-windows\win-x64\publish
```

## GitHub Releases

- 不要在每次修改后自动上传 GitHub Release 附件。
- 只有在用户明确要求上传 Release 时，才执行上传。
- 上传 Release 时，不直接上传 exe。
- 上传前必须将 `屏幕辅助线工具.exe` 压缩为：

```text
ScreenGuides.zip
```

- GitHub Release 附件上传 `ScreenGuides.zip`，不要上传 exe 文件。
