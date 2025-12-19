# 给 ChatGPT：角色创建自动跳过失败分析

## 📋 问题描述

**目标**：实现 Bannerlord mod，点击"快速开始"按钮后自动跳过角色创建流程（文化选择、背景选择、问卷等），直接进入游戏。

**当前状态**：
- ✅ 按钮显示正常
- ✅ 按钮点击正常
- ✅ 金币发放正常（100,000 金币）
- ❌ **角色创建未自动跳过**（需要手动选择）

## 🔍 日志分析

### qs_runtime.log 关键日志

```
[2025-12-19 20:11:06.026] [QuickStart] >>> QS BUTTON CLICKED <<<
[2025-12-19 20:11:06.095] [QuickStart] CharCreation: Tick - Game.Current is null
[2025-12-19 20:11:06.115] [QuickStart] CharCreation: Tick - Game.Current is null
[2025-12-19 20:11:06.117] [QuickStart] CharCreation: Tick - Game.Current is null
[2025-12-19 20:11:08.676] [QuickStart] CharCreation: Tick - ActiveState is null
[2025-12-19 20:11:08.683] [QuickStart] CharCreation: Tick - ActiveState is null
[2025-12-19 20:11:08.695] [QuickStart] CharCreation: Tick - ActiveState is null
[2025-12-19 20:11:11.910] [QuickStart] CharCreation: Tick - ActiveState is null
[2025-12-19 20:11:14.872] [QuickStart] CharCreation: Tick - ActiveState is null
[2025-12-19 20:11:15.066] [QuickStart] ActiveState = TaleWorlds.MountAndBlade.VideoPlaybackState
[2025-12-19 20:11:15.067] [QuickStart] CharCreation: done (left character creation state: TaleWorlds.MountAndBlade.VideoPlaybackState)
```

### rgl_log_14508.txt 关键日志

```
[18:22:55.960] [QuickStart] UIExtenderEx enable FAILED: System.Reflection.AmbiguousMatchException: Ambiguous match found.
[18:22:55.960] [QuickStart] 跳过 Harmony PatchAll（避免 TargetMethod 返回 null 导致异常）
```

## 🐛 问题分析

### 核心问题：时机问题

从日志可以看出：

1. **按钮点击后，Game.Current 为 null**
   - 按钮点击时，游戏可能还在初始化阶段
   - `QuickStartCharCreationSkipper.Tick()` 在 `Game.Current == null` 时直接返回
   - 错过了角色创建状态的早期阶段

2. **当 Game.Current 不为 null 时，角色创建已经完成**
   - 日志显示直接跳到了 `VideoPlaybackState`
   - 说明角色创建状态在 `Game.Current` 为 null 时就已经开始并完成了
   - 或者角色创建状态存在时间很短，我们的 Tick 方法没有及时捕获

3. **Harmony 补丁未应用**
   - `UIExtenderEx.Enable()` 失败（AmbiguousMatchException）
   - Harmony PatchAll 被跳过
   - 但按钮仍然显示了（可能是通过其他方式注入的）

## 💡 可能的原因

### 1. 游戏初始化时序问题

角色创建状态可能在以下时机开始：
- 在 `Game.Current` 初始化之前
- 在 `GameStateManager` 初始化之前
- 在 `OnApplicationTick` 开始调用之前

### 2. 状态检测逻辑问题

当前代码在 `OnApplicationTick` 中检查：
```csharp
Game game = Game.Current;
if (game == null) return;

var gsm = game.GameStateManager;
var state = gsm != null ? gsm.ActiveState : null;
if (state == null) return;
```

如果角色创建状态在 `Game.Current` 为 null 时就开始，这个检查会错过。

### 3. 状态名称匹配问题

代码检查：
```csharp
if (stateTypeName.IndexOf("CharacterCreation", StringComparison.OrdinalIgnoreCase) < 0)
```

可能实际的状态类型名不包含 "CharacterCreation"。

## 🔧 需要 ChatGPT 帮助的问题

### 问题 1: 如何捕获角色创建状态的早期阶段？

角色创建状态可能在 `Game.Current` 为 null 时就开始，如何在这个阶段捕获并处理？

**可能的解决方案**：
- 使用 Harmony 补丁直接拦截角色创建状态的初始化
- 使用事件监听（如果有相关事件）
- 使用其他生命周期钩子（如 `OnSubModuleLoad` 中的延迟检查）

### 问题 2: 如何正确检测角色创建状态？

从反编译的源码看到：
- `CharacterCreationState` 类型：`TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationState`
- `CharacterCreationManager` 属性：`CharacterCreationState.CharacterCreationManager`

但实际运行时可能：
- 状态类型名不同
- 状态存在时间很短
- 状态在游戏完全初始化前就完成了

**需要确认**：
- 角色创建状态的实际类型名是什么？
- 角色创建状态何时开始？何时结束？
- 如何可靠地检测到角色创建状态？

### 问题 3: Harmony 补丁未应用的问题

日志显示 Harmony PatchAll 被跳过，但按钮仍然显示了。这说明：
- 按钮可能是通过其他方式注入的（不是 Harmony）
- 或者 Harmony 补丁部分应用了

**需要确认**：
- 为什么 Harmony PatchAll 被跳过？
- 按钮是如何显示的？
- 如何确保 Harmony 补丁正确应用？

### 问题 4: 最佳实现方案

当前实现方式：
- 在 `OnApplicationTick` 中每帧检查状态
- 使用反射查找 `CharacterCreationManager`
- 调用方法设置文化、选择选项、切换菜单

**可能的改进方案**：
1. **Harmony 补丁方案**：直接 Patch `CharacterCreationState` 的初始化方法，在初始化时自动设置
2. **事件监听方案**：监听角色创建相关事件，在事件触发时自动处理
3. **状态机方案**：使用状态机模式，在状态转换时自动处理

## 📝 当前代码结构

### QuickStartCharCreationSkipper.cs

```csharp
public static void Tick(float dt)
{
    if (!QuickStartHelper.AutoSkipCharCreation || QuickStartHelper.CharCreationDone)
        return;

    Game game = Game.Current;  // ← 这里为 null
    if (game == null) return;

    var gsm = game.GameStateManager;
    var state = gsm != null ? gsm.ActiveState : null;
    if (state == null) return;  // ← 这里也为 null

    // 查找 CharacterCreationManager
    object manager = FindCharacterCreationManager(state);
    // ...
}
```

### 关键方法

1. `FindCharacterCreationManager(state)` - 查找 CharacterCreationManager
2. `TrySetCulture(manager)` - 设置文化为瓦兰迪亚
3. `TrySelectCurrentMenuOption(manager)` - 选择当前菜单的第一个选项
4. `TrySwitchToNextMenu(manager)` - 切换到下一个菜单

## 🎯 期望的解决方案

希望 ChatGPT 能够：

1. **分析时机问题**：为什么 `Game.Current` 为 null 时角色创建就开始了？
2. **提供替代方案**：如何在游戏初始化早期阶段捕获角色创建状态？
3. **修复 Harmony 补丁**：如何确保 Harmony 补丁正确应用？
4. **优化检测逻辑**：如何更可靠地检测和处理角色创建状态？

## 📦 相关文件

- `QuickStartMod/SubModule/QuickStartCharCreationSkipper.cs` - 角色创建自动跳过逻辑
- `QuickStartMod/SubModule/QuickStartSubModule.cs` - 主模块，包含 OnApplicationTick
- `QuickStartMod/SubModule/QuickStartPatches.cs` - Harmony 补丁（按钮注入）
- `D:\Bannerlord_Decompiled\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterCreationContent\` - 反编译的角色创建相关源码

## 🔗 反编译源码参考

已反编译的关键类型：
- `CharacterCreationState` - 角色创建状态
- `CharacterCreationManager` - 角色创建管理器
- `CharacterCreationContent` - 角色创建内容
- `NarrativeMenu` - 叙事菜单
- `NarrativeMenuOption` - 菜单选项

关键方法：
- `CharacterCreationManager.TrySwitchToNextMenu()` - 切换到下一个菜单
- `CharacterCreationManager.OnNarrativeMenuOptionSelected()` - 选择菜单选项
- `CharacterCreationContent.SetSelectedCulture()` - 设置文化

---

**问题总结**：角色创建自动跳过功能因为时机问题（Game.Current 为 null 时角色创建就开始了）而无法工作。需要找到在游戏初始化早期阶段捕获和处理角色创建状态的方法。

