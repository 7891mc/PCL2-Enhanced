# 编译说明 / Build Notes

本项目为 Windows 桌面应用（.NET Framework 4.8 + WPF），Release 单文件编译流程如下。

## 环境要求 / Requirements

- Windows 10/11 x64
- **.NET Framework 4.8 Developer Pack**（必须，WPF 编译需要）
- Visual Studio 2022 或 **VS Build Tools 2022**，工作负载勾选「.NET 桌面开发 / .NET desktop development」
- 方案含三个项目：`Plain Craft Launcher 2`（主程序）、`PCLCS`（UI 库）、`MeloongCore`（核心库，submodule 已在仓库中转为普通目录，无需再拉取）

## 编译步骤 / Steps

### 方式一：Visual Studio（推荐）
1. 打开 `Plain Craft Launcher 2.sln`
2. 生成 → 配置管理器 → 选择 **Release / x64**
3. 生成解决方案（先 PCLCS、MeloongCore，后主程序）
4. 产物：`Plain Craft Launcher 2\bin\x64\Release\Plain Craft Launcher 2.exe`

### 方式二：MSBuild 命令行
WPF 项目直接使用 .NET CLI 的 MSBuild 会因任务宿主（MSB4216）失败，需使用 VS Build Tools 的 MSBuild，并在主项目 `.vbproj` 中追加：

```xml
<Import Project="$(MSBuildToolsPath)\Microsoft.WinFX.targets" />
<UsingTask TaskName="MarkupCompilePass1" AssemblyFile="$(WINDIR)\Microsoft.NET\Framework64\v4.0.30319\Microsoft.Build.Tasks.v4.0.dll" />
<UsingTask TaskName="MarkupCompilePass2" AssemblyFile="$(WINDIR)\Microsoft.NET\Framework64\v4.0.30319\Microsoft.Build.Tasks.v4.0.dll" />
```

并将 `PCLCS`、`MeloongCore` 的 `ProjectReference` 临时替换为 `Reference`（先编译两个库，再编译主程序）。

发布单文件：`msbuild ... /p:Configuration=Release /p:Platform=x64`，随后将依赖 DLL 与 `Plain Craft Launcher 2.exe` 打包为单文件 zip（本改造版已验证：Release 编译 0 错误，启动冒烟测试通过）。

## 验证 / Verification

- 编译输出 0 Error（本改造版在 Windows 端已验证）
- 启动 PCL2 主界面正常、版本页/下载页/设置页导航正常
