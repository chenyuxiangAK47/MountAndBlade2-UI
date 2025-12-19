# 修复总结：误判 done 问题

## 🐛 问题根源

**核心问题**：代码在 `VideoPlaybackState` 就判定角色创建结束了，但实际上角色创建还没开始。

### 问题流程

```
按钮点击 → Game.Current is null → ActiveState is null → VideoPlaybackState → 误判 done → CharacterCreationState 时已经关闭
```

### 真实流程

```
按钮点击 → Game.Current is null → ActiveState is null → VideoPlaybackState（片头/加载）→ CharacterCreationState → MapState
```

## ✅ 修复方案

### 1. 添加 `SeenCharacterCreation` 标志

**位置**：`QuickStartHelper.cs`

```csharp
// 是否已经见过 CharacterCreationState（用于防止误判 done）
public static bool SeenCharacterCreation { get; set; }
```

**作用**：只有见过 `CharacterCreationState` 后，离开它才算 done。

### 2. 修复误判 done 的逻辑

**位置**：`QuickStartCharCreationSkipper.cs` 的 `Tick()` 方法

**修复前**：
```csharp
if (stateTypeName.IndexOf("CharacterCreation", StringComparison.OrdinalIgnoreCase) < 0)
{
    // 直接判定 done ❌
    QuickStartHelper.CharCreationDone = true;
}
```

**修复后**：
```csharp
if (!isCharCreation)
{
    // 关键修复：没见过角色创建前，任何状态都不能算 done
    if (!QuickStartHelper.SeenCharacterCreation)
    {
        // 还没见过角色创建，继续等待 ✅
        return;
    }

    // 见过角色创建后，只有进入 MapState 才算真正完成 ✅
    if (stateTypeName.IndexOf("MapState", StringComparison.OrdinalIgnoreCase) >= 0)
    {
        QuickStartHelper.CharCreationDone = true;
    }
}
```

### 3. 创建 Harmony Patch 直接 Patch CharacterCreationState.OnTick

**位置**：`QuickStartPatches.cs`

**新增类**：`QuickStartCharacterCreationStateTickPatch`

**作用**：
- 直接 Patch `CharacterCreationState.OnTick(float)` 方法
- 避免了 `Game.Current` 为 null 的时机问题
- 只要角色创建状态在运行，就一定能进入 Postfix

**关键代码**：
```csharp
[HarmonyPatch]
public static class QuickStartCharacterCreationStateTickPatch
{
    static Type TargetType()
    {
        return Type.GetType(
            "TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationState, TaleWorlds.CampaignSystem");
    }

    static MethodBase TargetMethod()
    {
        var t = TargetType();
        // 尝试查找 OnTick(float) 或 Tick(float)
        return AccessTools.Method(t, "OnTick", new[] { typeof(float) })
            ?? AccessTools.Method(t, "Tick", new[] { typeof(float) });
    }

    static void Postfix(object __instance, float dt)
    {
        if (!QuickStartHelper.AutoSkipCharCreation || QuickStartHelper.CharCreationDone)
            return;

        QuickStartHelper.SeenCharacterCreation = true;
        QuickStartCharCreationSkipper.RunOnCharCreationState(__instance, dt);
    }
}
```

### 4. 新增 `RunOnCharCreationState` 方法

**位置**：`QuickStartCharCreationSkipper.cs`

**作用**：
- 从 `CharacterCreationState` 实例直接获取 `CharacterCreationManager`
- 执行自动跳过逻辑（设置文化、选择选项、切换菜单）
- 避免了通过 `Game.Current` 和 `ActiveState` 查找的时机问题

### 5. 修复 UIExtenderEx.Enable() 的 AmbiguousMatchException

**位置**：`QuickStartSubModule.cs`

**修复**：
- 尝试多个重载，选择无参数版本
- 如果失败，不影响功能（按钮通过 Harmony 注入）

### 6. 添加屏幕提示

**位置**：`QuickStartCharCreationSkipper.cs` 的 `RunOnCharCreationState()` 方法

**作用**：第一次进入角色创建状态时显示 `[QS MOD] 自动跳过角色创建中...` 提示

## 📋 修改的文件

1. ✅ `QuickStartHelper.cs` - 添加 `SeenCharacterCreation` 标志
2. ✅ `QuickStartCharCreationSkipper.cs` - 修复误判 done 逻辑，新增 `RunOnCharCreationState` 方法
3. ✅ `QuickStartPatches.cs` - 新增 `QuickStartCharacterCreationStateTickPatch` Harmony Patch
4. ✅ `QuickStartSubModule.cs` - 修复 UIExtenderEx.Enable() 问题
5. ✅ `QuickStartPatches.cs` - 按钮点击时重置 `SeenCharacterCreation` 标志

## 🎯 预期效果

修复后，角色创建自动跳过应该能够：

1. ✅ **正确等待**：在 `VideoPlaybackState` 等状态时不会误判 done
2. ✅ **及时捕获**：通过 Harmony Patch 直接捕获 `CharacterCreationState.OnTick`
3. ✅ **可靠执行**：不依赖 `Game.Current` 的初始化时机
4. ✅ **用户可见**：显示屏幕提示确认自动跳过功能正在运行

## 🔄 下一步

1. **重新编译**：
   ```powershell
   msbuild QuickStartMod.csproj /p:Configuration=Release /p:Platform=x64
   ```

2. **测试**：
   - 点击 "QS MOD 快速开始" 按钮
   - 观察是否显示 `[QS MOD] 自动跳过角色创建中...` 提示
   - 查看 `qs_runtime.log` 确认是否进入 `RunOnCharCreationState`

3. **验证**：
   - 角色创建是否自动跳过
   - 文化是否自动设置为瓦兰迪亚
   - 是否自动选择第一个选项并切换到下一个菜单

---

**修复时间**：2025-12-19  
**修复依据**：ChatGPT 的问题分析和解决方案

