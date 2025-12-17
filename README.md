# QuickStart Mod for Mount & Blade II: Bannerlord

在主菜单添加"快速开始"按钮，点击后直接进入沙盒模式并给予初始资源。

## 功能

- ✅ 在主菜单顶部添加"快速开始"按钮
- ✅ 点击后直接启动新游戏（沙盒模式）
- ✅ 自动给予初始金币（可在代码中配置）

## 技术实现

- **框架**: Harmony + UIExtenderEx
- **方法**: 使用 Harmony Patch 注入菜单项到 `InitialMenuVM.MenuOptions`
- **强类型**: 使用 `TextObject` 和 `InitialStateOption` 强类型创建，不使用反射

## 安装

1. 将 `QuickStartMod` 文件夹复制到 `Mount & Blade II Bannerlord\Modules\` 目录
2. 在游戏启动器中启用 `QuickStartMod`
3. 确保已安装依赖：
   - `Bannerlord.Harmony`
   - `Bannerlord.UIExtenderEx`

## 编译

使用 MSBuild 编译：

```powershell
$env:BANNERLORD_INSTALL_DIR = "游戏安装路径"
cd "Modules\QuickStartMod"
& "MSBuild路径" QuickStartMod.csproj /p:Configuration=Win64_Shipping_Client /p:Platform=x64 /t:Build
```

## 问题与解决方案

详细的问题排查和解决方案请查看 [问题解决方案.md](./问题解决方案.md)

## 许可证

MIT License
