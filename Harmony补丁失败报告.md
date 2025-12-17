# QuickStartMod Harmony 补丁失败报告 - 给 ChatGPT

## 🚨 问题严重性

**用户反馈**：**UI 功能是依赖 Harmony 的**，所以 Harmony 补丁失败会导致整个 UI 功能无法工作。

这意味着：
- ✅ SubModule 已经能正常加载（这是好消息）
- ❌ 但是 Harmony 补丁失败导致 `PatchAll()` 抛出异常
- ❌ 整个程序集的其他功能（包括 UI）可能因此无法正常工作

---

## 问题状态更新

### ✅ 已解决的问题
根据 ChatGPT 的建议修改后，**SubModule 已经能够正常加载了**！

**证据**（来自 `rgl_log_36096.txt`）：
```
[16:03:33.614] [QuickStart] 静态构造函数执行！类已加载！
[16:03:33.614] [QuickStart] DLL 路径: D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\bin\Win64_Shipping_Client\QuickStartMod.dll
[16:03:34.636] [QuickStart] OnSubModuleLoad ENTER
[16:03:34.636] [QuickStart] base.OnSubModuleLoad() 执行完成
[16:03:34.636] [QuickStart] Step 1: 检查 UIExtenderEx 是否已加载
[16:03:34.637] [QuickStart] UIExtenderEx 已加载: Bannerlord.UIExtenderEx, Version=2.13.2.0, Culture=neutral, PublicKeyToken=null
[16:03:34.637] [QuickStart] Step 2: OnSubModuleLoad OK (no UIExtender yet)
```

**结论**：按照 ChatGPT 的建议（移除 UIExtenderEx 强类型引用、使用 Debug.Print）后，SubModule 类已经能够正常实例化，静态构造函数和 OnSubModuleLoad 都能正常执行。

---

## ❌ 新发现的问题：Harmony 补丁失败

### 错误信息
```
[16:03:34.637] [QuickStart] Step 3: 开始初始化 Harmony
[16:03:34.693] [QuickStart] Step 3: Harmony 初始化失败: HarmonyLib.HarmonyException: 
Patching exception in method static System.Reflection.MethodBase QuickStartMod.QuickStartGoldPatch::TargetMethod() 
---> System.Exception: Method static System.Reflection.MethodBase QuickStartMod.QuickStartGoldPatch::TargetMethod() 
returned an unexpected result: null
```

### 问题严重性
**用户反馈**：**UI 功能是依赖 Harmony 的**，所以 Harmony 补丁失败会导致整个 UI 功能无法工作。

### 错误分析
- **错误类型**：`HarmonyLib.HarmonyException`
- **失败位置**：`QuickStartMod.QuickStartGoldPatch::TargetMethod()`
- **根本原因**：`TargetMethod()` 返回了 `null`，说明 Harmony 找不到要补丁的目标方法

---

## 代码结构

### QuickStartSubModule.cs（Harmony 初始化部分）
```csharp
// Step 3: 初始化 Harmony（Harmony 应该没问题，因为已经在依赖中）
try
{
    TaleWorlds.Library.Debug.Print("[QuickStart] Step 3: 开始初始化 Harmony", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
    _harmony = new Harmony("com.quickstartmod");
    _harmony.PatchAll(typeof(QuickStartSubModule).Assembly);
    TaleWorlds.Library.Debug.Print("[QuickStart] Step 3: Harmony 初始化成功", 0, TaleWorlds.Library.Debug.DebugColor.Green);
}
catch (Exception ex)
{
    TaleWorlds.Library.Debug.Print($"[QuickStart] Step 3: Harmony 初始化失败: {ex}", 0, TaleWorlds.Library.Debug.DebugColor.Red);
}
```

### QuickStartPatches.cs（补丁实现）
```csharp
using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace QuickStartMod
{
    // 补丁：在角色创建完成后给予金币
    [HarmonyPatch]
    public class QuickStartGoldPatch
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CharacterCreation.CharacterCreationState");
            if (type != null)
            {
                return AccessTools.Method(type, "OnFinalize");
            }
            return null;  // ← 这里返回了 null，导致 Harmony 报错
        }

        [HarmonyPostfix]
        static void Postfix()
        {
            // 金币会在 OnCampaignStart 中给予，这里只显示提示
            if (QuickStartHelper.IsQuickStartMode)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"快速开局：将在进入游戏后获得 {QuickStartHelper.QuickStartGold:N0} 金币"));
            }
        }
    }
}
```

**问题分析**：
- `TargetMethod()` 返回 `null` 的原因可能是：
  1. `TaleWorlds.CampaignSystem.CharacterCreation.CharacterCreationState` 类型在游戏启动时还不存在（可能在 Campaign 启动后才加载）
  2. `OnFinalize` 方法名称不对或不存在
  3. 类型命名空间不正确

---

## 需要帮助的问题

1. **为什么 `TargetMethod()` 返回 `null`？**
   - `TaleWorlds.CampaignSystem.CharacterCreation.CharacterCreationState` 类型在 `OnSubModuleLoad` 时是否已经加载？
   - 如果类型在 Campaign 启动后才加载，是否应该延迟补丁的注册？
   - `OnFinalize` 方法名称是否正确？是否需要完整的方法签名？

2. **如何正确实现 Harmony 补丁的 `TargetMethod()`？**
   - 如果目标类型在启动时不存在，应该如何处理？
   - 是否应该使用条件补丁（只在类型存在时才注册）？
   - 或者应该延迟到 Campaign 启动后再注册补丁？

3. **如果 Harmony 补丁失败，是否会影响 UIExtenderEx 的功能？**
   - **用户明确表示：UI 功能是依赖 Harmony 的**
   - 这是否意味着如果 `PatchAll()` 失败，整个程序集的其他功能也会受影响？
   - 是否应该将补丁注册改为逐个注册，而不是使用 `PatchAll()`？

4. **如何调试 Harmony 补丁问题？**
   - 如何确认目标类型和方法在运行时是否存在？
   - 如何查看 Harmony 的详细错误信息？
   - 是否应该添加日志来追踪 `TargetMethod()` 的执行过程？

5. **临时解决方案**
   - 是否可以暂时禁用 `QuickStartGoldPatch`，先让其他功能（UI）工作？
   - 或者将补丁改为可选（如果找不到目标方法就跳过）？

---

## 技术环境

- **游戏版本**：Mount & Blade II: Bannerlord v1.3.11.104956
- **Harmony 版本**：从 Steam Workshop 安装（Bannerlord.Harmony）
- **.NET Framework**：4.8
- **Harmony 错误**：`HarmonyLib.HarmonyException` - `TargetMethod()` 返回 `null`

---

## 完整错误堆栈（来自日志）

```
[16:03:34.693] [QuickStart] Step 3: Harmony 初始化失败: HarmonyLib.HarmonyException: 
Patching exception in method static System.Reflection.MethodBase QuickStartMod.QuickStartGoldPatch::TargetMethod() 
---> System.Exception: Method static System.Reflection.MethodBase QuickStartMod.QuickStartGoldPatch::TargetMethod() 
returned an unexpected result: null
   at HarmonyLib.PatchClassProcessor.RunMethod[S,T](T defaultIfNotExisting, T defaultIfFailing, Func`2 failOnResult, Object[] parameters)
   --- End of inner exception stack trace ---
   at HarmonyLib.PatchClassProcessor.ReportException(Exception exception, MethodBase original)
   at HarmonyLib.PatchClassProcessor.Patch()
   at HarmonyLib.CollectionExtensions.Do[T](IEnumerable`1 sequence, Action`1 action)
   at HarmonyLib.Harmony.PatchAll(Assembly assembly)
   at QuickStartMod.QuickStartSubModule.OnSubModuleLoad() 
   in D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\SubModule\QuickStartSubModule.cs:line 74
```

**关键信息**：
- 错误发生在 `Harmony.PatchAll(Assembly assembly)` 调用时
- Harmony 在扫描程序集时，发现 `QuickStartGoldPatch.TargetMethod()` 返回了 `null`
- Harmony 认为这是一个错误，抛出了异常，导致整个 `PatchAll()` 失败

---

## 下一步行动

1. **确认目标类型和方法是否存在**
   - 在 `TargetMethod()` 中添加日志，确认类型查找是否成功
   - 检查 `TaleWorlds.CampaignSystem.CharacterCreation.CharacterCreationState` 是否在 `OnSubModuleLoad` 时已加载

2. **修复 `TargetMethod()` 实现**
   - 如果类型不存在，应该如何处理？（抛出异常？返回 null？）
   - 或者改为延迟补丁注册（在 Campaign 启动后再注册）

3. **解决 Harmony 补丁失败导致 UI 无法工作的问题**
   - **关键问题**：用户说 UI 依赖 Harmony，但 Harmony 补丁失败导致整个功能无法工作
   - 是否应该将补丁改为可选（如果找不到目标方法就跳过，而不是抛出异常）？
   - 或者改为逐个注册补丁，而不是使用 `PatchAll()`？

4. **临时解决方案**
   - 暂时注释掉 `QuickStartGoldPatch`，先让 UI 功能工作
   - 或者修改 `TargetMethod()` 使其在找不到目标时抛出异常，但 Harmony 能正确处理

---

## 文件位置

- **补丁代码**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\SubModule\QuickStartPatches.cs`
- **SubModule**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\SubModule\QuickStartSubModule.cs`
- **日志文件**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\logs\rgl_log_36096.txt`

