# 决明-Z

决明-Z 是一个 Terraria 原版运行时辅助工具，不是 tModLoader Mod。它的目标是把重复操作尽量收束到游戏可识别的动作路径里执行，减少误触和不可预期行为。

本项目完全由 AI 编写，用户仅负责提出需求、实机场景验收和测试反馈。使用前请自行判断所在平台和服务器规则。

本项目灵感来源和开发方向深度借鉴@Jkstring的TerrariaHelper

## 仓库内容

- `src/JueMingZ/`：主程序源码。
- `tests/JueMingZ.Tests/`：控制台测试项目。
- `scripts/*.ps1`：构建、测试包和本地审计脚本。
- `external/ThirdParty/0Harmony.dll`：当前构建需要嵌入的 Harmony 依赖。


## 构建

本项目目标框架为 `.NET Framework 4.7.2`，默认按 x86 构建。

构建前需要准备 Terraria 编译引用到 `external/TerrariaRefs/`，或用 `JueMingZTerrariaRefsDir` 指向你的本机引用目录。需要的文件是：

```text
Terraria.exe
Microsoft.Xna.Framework.dll
Microsoft.Xna.Framework.Game.dll
Microsoft.Xna.Framework.Graphics.dll
ReLogic.dll
```

常用命令：

```powershell
dotnet build JueMingZ.sln -c Release -p:Platform=x86
dotnet run --project tests\JueMingZ.Tests\JueMingZ.Tests.csproj -c Release -p:Platform=x86
```

如果引用文件不在默认目录：

```powershell
dotnet build JueMingZ.sln -c Release -p:Platform=x86 -p:JueMingZTerrariaRefsDir="C:\path\to\TerrariaRefs"
```

## 日志与反馈

运行时日志和诊断默认写到：

```text
%USERPROFILE%\Documents\My Games\Terraria\JueMing-Z
```

反馈问题时，请尽量附上对应时间段的日志、诊断快照和复现场景。

## 免责声明

本项目仅用于学习与辅助用途，涉及游戏输入与行为自动化。请谨慎使用，并遵守所在平台、服务器和社区规则。
