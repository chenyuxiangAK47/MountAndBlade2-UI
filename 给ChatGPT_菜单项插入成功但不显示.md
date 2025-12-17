# 给 ChatGPT：菜单项插入成功但不显示

## 问题描述

使用强类型 `TextObject` 和 `InitialStateOption` 成功创建并插入了菜单项，日志显示插入成功，但按钮在游戏中不显示。

## 关键日志信息

**最新日志文件：** `rgl_log_48896.txt`

**关键日志片段：**
```
[21:37:02.767] [QuickStart] ========== 开始注入菜单项 ==========
[21:37:02.767] [QuickStart] MenuOptions 数量: 0
[21:37:02.767] [QuickStart] TextObject 创建成功: name=快速开始
[21:37:02.767] [QuickStart] 开始创建 InitialStateOption...
[21:37:02.767] [QuickStart] InitialStateOption 创建成功
[21:37:02.767] [QuickStart] 开始创建 InitialMenuOptionVM...
[21:37:02.767] [QuickStart] InitialMenuOptionVM 创建成功: NameText=快速开始
[21:37:02.767] [QuickStart] 插入前 MenuOptions 数量: 0
[21:37:02.767] [QuickStart] 插入后 MenuOptions 数量: 1
[21:37:02.767] [QuickStart] 第一个菜单项: NameText=快速开始, Type=TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuOptionVM
[21:37:02.767] [QuickStart] ========== 菜单项注入完成 ==========
```

## 问题分析

### 1. 插入成功但时机不对？

从日志看：
- **MenuOptions 数量: 0** - 在 `RefreshValues` 被调用时，`MenuOptions` 还是空的
- **插入后数量: 1** - 插入操作成功
- **第一个菜单项正确** - 插入的项确实是我们的"快速开始"

**可能的问题：**
- `RefreshValues` 被调用时，原版菜单项还没有被填充
- 我们插入后，可能后续还有其他逻辑会重新填充 `MenuOptions`，覆盖了我们的插入
- 或者 `RefreshValues` 被多次调用，我们的插入被后续的调用覆盖了

### 2. 是否需要在其他时机插入？

可能需要在：
- `RefreshValues` 之后（但我们已经用了 Postfix）
- 或者需要 Patch 其他方法，比如菜单项填充完成后的某个方法

### 3. 是否需要监听 MenuOptions 的变化？

可能需要：
- 监听 `MenuOptions` 的 `CollectionChanged` 事件
- 或者在菜单项被填充后再次插入

## 当前代码实现

### QuickStartPatches.cs

```csharp
using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu;

namespace QuickStartMod
{
    [HarmonyPatch(typeof(InitialMenuVM))]
    public static class QuickStartMenuInjectPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("RefreshValues")]
        public static void Postfix(InitialMenuVM __instance)
        {
            try
            {
                TaleWorlds.Library.Debug.Print("[QuickStart] ========== 开始注入菜单项 ==========", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                TaleWorlds.Library.Debug.Print($"[QuickStart] MenuOptions 数量: {__instance.MenuOptions.Count}", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);

                // 检查是否已存在
                bool alreadyExists = false;
                foreach (var opt in __instance.MenuOptions)
                {
                    try
                    {
                        var nameText = opt?.NameText;
                        if (nameText != null && nameText.ToString().Contains("快速开始"))
                        {
                            TaleWorlds.Library.Debug.Print("[QuickStart] 菜单项已存在，跳过", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                            alreadyExists = true;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        TaleWorlds.Library.Debug.Print($"[QuickStart] 检查菜单项失败: {ex.Message}", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                    }
                }

                if (alreadyExists) return;

                // 创建 TextObject
                var name = new TextObject("{=qs_quickstart}快速开始");
                var hint = new TextObject("{=qs_quickstart_hint}直接进入沙盒并给予初始资源");

                // 创建委托
                Func<(bool, TextObject)> isDisabledAndReason = () => (false, new TextObject(""));
                Func<bool> isHidden = () => false;

                // 点击动作
                Action action = () =>
                {
                    // ... 快速开始逻辑 ...
                };

                // 创建 InitialStateOption
                var stateOption = new InitialStateOption(
                    "quick_start",
                    name,
                    orderIndex: 0,
                    action: action,
                    isDisabledAndReason: isDisabledAndReason,
                    enabledHint: hint,
                    isHidden: isHidden
                );

                // 创建 InitialMenuOptionVM
                var vm = new InitialMenuOptionVM(stateOption);

                // 插入到最上面
                __instance.MenuOptions.Insert(0, vm);

                TaleWorlds.Library.Debug.Print($"[QuickStart] 插入后 MenuOptions 数量: {__instance.MenuOptions.Count}", 0, TaleWorlds.Library.Debug.DebugColor.Green);
                TaleWorlds.Library.Debug.Print("[QuickStart] ========== 菜单项注入完成 ==========", 0, TaleWorlds.Library.Debug.DebugColor.Green);
            }
            catch (Exception ex)
            {
                TaleWorlds.Library.Debug.Print($"[QuickStart] 注入菜单项失败: {ex}", 0, TaleWorlds.Library.Debug.DebugColor.Red);
            }
        }
    }
}
```

## 需要 ChatGPT 帮助的问题

1. **时机问题：**
   - `RefreshValues` 被调用时，`MenuOptions` 还是空的（数量为 0），这是正常的吗？
   - 是否需要在其他时机插入，比如菜单项填充完成后？

2. **多次调用问题：**
   - `RefreshValues` 是否会被多次调用？
   - 如果是，我们的插入是否会被后续调用覆盖？

3. **正确的插入时机：**
   - 应该 Patch 哪个方法？
   - 是否需要在菜单项填充完成后再次插入？

4. **其他可能的问题：**
   - 是否需要监听 `MenuOptions` 的变化？
   - 是否需要使用其他方式确保菜单项显示？

## 环境信息

- **游戏版本：** Mount & Blade II: Bannerlord (War Sails)
- **Mod 框架：** Harmony + UIExtenderEx
- **目标：** 在主菜单注入"快速开始"按钮
- **当前状态：** 插入成功（日志显示），但按钮不显示

## 请求

请提供：
1. 正确的插入时机和方法
2. 如何确保菜单项在菜单项填充完成后仍然存在
3. 是否需要 Patch 其他方法或使用其他机制

谢谢！

