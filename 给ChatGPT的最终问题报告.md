# QuickStartMod 最终问题报告 - 给 ChatGPT

## 当前状态（最新测试结果）

### ✅ 已解决的问题
1. **SubModule 已能正常加载** - 静态构造函数和 `OnSubModuleLoad` 都能正常执行
2. **Harmony 补丁问题已解决** - 已移除 `PatchAll()`，不再导致异常
3. **UIExtenderEx 已找到** - 程序集已加载，类型也能找到
4. **InitialMenuVM 类型已找到** - 通过探针日志确认：
   - 完整类型名：`TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuVM`
   - `RefreshValues` 方法存在：`InitialMenuVM.RefreshValues found? True`
5. **ViewModelMixin 特性已更新** - 使用完整类型名：`"TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuVM"`

### ❌ 仍然存在的问题

**即使找到了正确的类型和 RefreshValues 方法，UIExtenderEx 注册 ViewModelMixin 时仍然失败**，错误信息（最新测试）：

```
[QuickStart] UIExtenderEx enable FAILED: System.Reflection.TargetInvocationException: 
Exception has been thrown by the target of an invocation. 
---> System.NullReferenceException: Object reference not set to an instance of an object.

   at HarmonyLib.AccessTools.IsDeclaredMember[T](T member)
   at Bannerlord.UIExtenderEx.Patches.ViewModelWithMixinPatch.Patch(Harmony harmony, Type viewModelType, String refreshMethodName) 
   in /_/src/Bannerlord.UIExtenderEx/Patches/ViewModelWithMixinPatch.cs:line 51
   at Bannerlord.UIExtenderEx.Components.ViewModelComponent.RegisterViewModelMixin(Type mixinType, String refreshMethodName, Boolean handleDerived) 
   in /_/src/Bannerlord.UIExtenderEx/Components/ViewModelComponent.cs:line 123
   at Bannerlord.UIExtenderEx.UIExtenderRuntime.Register(IEnumerable`1 types) 
   in /_/src/Bannerlord.UIExtenderEx/UIExtenderRuntime.cs:line 113
```

## 关键发现

### 探针日志结果（最新测试 - rgl_log_33748.txt）
```
[17:41:57.430] [QuickStart] 搜索所有包含 'InitialMenuVM' 的类类型...
[17:41:57.436] [QuickStart] 找到可能的类型: TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuVM (IsClass: True)
[17:41:57.436] [QuickStart] InitialMenuVM type found? True
[17:41:57.436] [QuickStart] InitialMenuVM full name: TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuVM
[17:41:57.436] [QuickStart] InitialMenuVM.RefreshValues found? True
[17:41:57.436] [QuickStart] UIExtenderEx 已加载: Bannerlord.UIExtenderEx, Version=2.13.2.0
[17:41:57.577] [QuickStart] UIExtenderEx enable FAILED: System.Reflection.TargetInvocationException...
```

**结论**：
- ✅ 类型存在且能找到（确认是类类型，不是委托）
- ✅ RefreshValues 方法存在
- ✅ ViewModelMixin 特性已使用完整类型名
- ❌ **但 UIExtenderEx 在 patch 时仍然失败**（NullReferenceException 在 `AccessTools.IsDeclaredMember`）

## 当前代码

### QuickStartViewModelMixin.cs
```csharp
using System;
using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.Library;

namespace QuickStartMod
{
    // ViewModelMixin：向 InitialMenuVM 注入 ExecuteQuickStart 方法
    // 根据探针日志，正确的完整类型名是：TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuVM
    // RefreshValues 方法已确认存在
    // ⚠️ 问题：使用 BaseViewModelMixin<ViewModel> 而不是 BaseViewModelMixin<InitialMenuVM>
    // 因为编译时找不到 InitialMenuVM 类型（游戏 DLL 只在运行时可用）
    [ViewModelMixin("TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuVM")]
    public class QuickStartViewModelMixin : BaseViewModelMixin<ViewModel>
    {
        public QuickStartViewModelMixin(ViewModel vm) : base(vm)
        {
            // ... 构造函数代码 ...
        }

        [DataSourceMethod]
        public void ExecuteQuickStart()
        {
            // ... 实现代码 ...
        }
    }
}
```

### 关键问题点

1. **ViewModelMixin 特性**：已使用完整类型名 `"TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuVM"`
2. **基类泛型参数**：当前使用 `BaseViewModelMixin<ViewModel>`（因为编译时找不到 `InitialMenuVM`）
3. **RefreshValues 方法**：已确认存在，但 UIExtenderEx 在 patch 时仍然失败

## 可能的原因

根据 ChatGPT 之前的分析，可能的原因：

1. **泛型参数不匹配**：
   - 当前：`BaseViewModelMixin<ViewModel>`（基类）
   - 建议：`BaseViewModelMixin<InitialMenuVM>`（具体类型）
   - 问题：编译时找不到 `InitialMenuVM`，无法使用强类型

2. **ViewModelMixin 特性参数**：
   - 当前：只指定了类型名
   - 可能：需要显式指定 refresh 方法名（虽然已确认存在）

3. **UIExtenderEx 版本兼容性**：
   - 当前版本：2.13.2.0
   - 可能：该版本的 ViewModelMixin 注册逻辑有特殊要求

## 核心问题

**即使所有条件都满足，UIExtenderEx 仍然失败：**
- ✅ 类型找到了：`TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuVM`
- ✅ RefreshValues 方法找到了
- ✅ ViewModelMixin 特性使用了完整类型名
- ❌ 但 UIExtenderEx 在 `ViewModelWithMixinPatch.Patch` 时仍然抛出 NullReferenceException

**错误发生在**：`HarmonyLib.AccessTools.IsDeclaredMember[T](T member)` - 说明传入的 member 是 null

## 需要帮助的问题

1. **为什么即使找到了正确的类型和 RefreshValues 方法，UIExtenderEx 仍然失败？**
   - 是否因为使用了 `BaseViewModelMixin<ViewModel>` 而不是 `BaseViewModelMixin<InitialMenuVM>`？
   - UIExtenderEx 在 patch 时，是否通过泛型参数来查找目标类型？
   - 如果泛型参数是 `ViewModel`（基类），而特性中指定的是 `InitialMenuVM`（子类），是否会导致类型推断失败？

2. **如何在不编译时依赖 InitialMenuVM 的情况下，使用强类型的 BaseViewModelMixin？**
   - 是否可以使用反射在运行时获取类型并创建 Mixin？
   - 或者是否有其他方式实现 ViewModelMixin？
   - 是否可以使用 `MakeGenericType` 在运行时创建泛型类型？

3. **ViewModelMixin 特性是否需要显式指定 refresh 方法名？**
   - 虽然 RefreshValues 已确认存在，但是否需要显式指定？
   - 如果需要，正确的语法是什么？（之前尝试 `[ViewModelMixin("...", "RefreshValues")]` 编译失败）

4. **是否有其他方式实现主菜单按钮功能？**
   - 如果 ViewModelMixin 无法工作，是否有替代方案？
   - 例如：直接使用 Harmony 补丁来添加按钮功能？
   - 或者使用其他 UIExtenderEx 功能？

5. **UIExtenderEx 2.13.2.0 版本是否有已知问题？**
   - 是否有其他 Mod 成功使用 ViewModelMixin 的示例？
   - 是否需要特定版本的 UIExtenderEx？

## 技术环境

- **游戏版本**：Mount & Blade II: Bannerlord v1.3.11.104956
- **UIExtenderEx 版本**：2.13.2.0（从 Steam Workshop 安装）
- **Harmony 版本**：从 Steam Workshop 安装（Bannerlord.Harmony）
- **.NET Framework**：4.8
- **InitialMenuVM 完整类型名**：`TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuVM`
- **RefreshValues 方法**：已确认存在

## 文件位置

- **SubModule**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\SubModule\QuickStartSubModule.cs`
- **ViewModelMixin**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\SubModule\QuickStartViewModelMixin.cs`
- **UIExtension**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\SubModule\QuickStartUIExtension.cs`
- **最新日志**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\logs\rgl_log_33748.txt`

## 下一步建议

如果这次测试后按钮仍然不出现，建议：

1. **尝试使用运行时反射创建强类型的 ViewModelMixin**
2. **检查 UIExtenderEx 的 ViewModelMixin 特性是否需要其他参数**
3. **考虑使用 Harmony 补丁作为替代方案**

