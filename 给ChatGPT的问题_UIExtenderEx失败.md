# QuickStartMod UIExtenderEx 注册失败报告 - 给 ChatGPT

## 问题状态

### ✅ 已解决的问题
1. **SubModule 已能正常加载** - 静态构造函数和 `OnSubModuleLoad` 都能正常执行
2. **Harmony 补丁问题已解决** - 已移除 `PatchAll()`，不再导致异常
3. **UIExtenderEx 已找到** - 程序集已加载，类型也能找到

### ❌ 新问题：UIExtenderEx 注册 ViewModelMixin 时失败

## 错误信息

```
[17:17:38.519] [QuickStart] UIExtenderEx enable FAILED: System.Reflection.TargetInvocationException: 
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

## 错误分析

- **错误类型**：`System.NullReferenceException`（空引用异常）
- **发生位置**：`ViewModelWithMixinPatch.Patch` → `HarmonyLib.AccessTools.IsDeclaredMember[T]`
- **调用链**：`Register(Assembly)` → `RegisterViewModelMixin` → `ViewModelWithMixinPatch.Patch`
- **根本原因**：在注册 ViewModelMixin 时，Harmony 尝试检查某个成员是否为声明成员，但该成员为 `null`

## 代码结构

### QuickStartSubModule.cs（UIExtenderEx 初始化部分）
```csharp
// 调用 Register
var registerMethod = uiExtenderType.GetMethod("Register", new[] { typeof(Assembly) });
if (registerMethod != null)
{
    registerMethod.Invoke(_uiExtender, new object[] { typeof(QuickStartSubModule).Assembly });
    TaleWorlds.Library.Debug.Print("[QuickStart] UIExtender.Register 成功", 0, TaleWorlds.Library.Debug.DebugColor.Green);
}

// 调用 Enable
var enableMethod = uiExtenderType.GetMethod("Enable");
if (enableMethod != null)
{
    enableMethod.Invoke(_uiExtender, null);  // ← 这里失败了
    TaleWorlds.Library.Debug.Print("[QuickStart] UIExtenderEx enabled", 0, TaleWorlds.Library.Debug.DebugColor.Green);
}
```

### QuickStartViewModelMixin.cs（ViewModelMixin 实现）
```csharp
using System;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.Library;
using System.Reflection;

namespace QuickStartMod
{
    [ViewModelMixin("TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenuVM")]
    public class QuickStartViewModelMixin : BaseViewModelMixin<ViewModel>
    {
        public QuickStartViewModelMixin(ViewModel vm) : base(vm)
        {
            TaleWorlds.Library.Debug.Print("[QuickStart] ViewModelMixin 构造函数被调用！", 0, Debug.DebugColor.Magenta);
            try
            {
                QuickStartLogger.LogStep(20, "QuickStartViewModelMixin 构造函数开始执行", true);
                QuickStartLogger.LogSuccess("ViewModelMixin 已创建并注入到 InitialMenuVM", true);
            }
            catch (Exception ex)
            {
                QuickStartLogger.LogError("ViewModelMixin 构造函数", ex, true);
            }
        }

        [DataSourceMethod]
        public void ExecuteQuickStart()
        {
            // ... 实现代码 ...
        }
    }
}
```

## 可能的原因

1. **ViewModelMixin 特性中的类型名称不正确**
   - 当前使用：`"TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenuVM"`
   - 可能该类型在运行时不存在或名称已更改

2. **BaseViewModelMixin<ViewModel> 泛型参数不正确**
   - 当前使用：`BaseViewModelMixin<ViewModel>`（使用反射避免编译时依赖）
   - 可能应该使用具体的类型，或者泛型参数导致 Harmony 无法正确识别

3. **DataSourceMethod 特性有问题**
   - `ExecuteQuickStart` 方法有 `[DataSourceMethod]` 特性
   - 可能 Harmony 在检查该方法时出现问题

4. **ViewModelMixin 的构造函数参数不正确**
   - 当前使用：`BaseViewModelMixin<ViewModel>`
   - 可能基类期望的参数类型不匹配

## 需要帮助的问题

1. **为什么 `AccessTools.IsDeclaredMember` 会收到 `null`？**
   - 是 ViewModelMixin 的某个成员为 `null`？
   - 还是目标 ViewModel 类型找不到？

2. **ViewModelMixin 特性中的类型名称是否正确？**
   - `"TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenuVM"` 是否是正确的完整类型名？
   - 是否需要使用不同的命名空间？

3. **BaseViewModelMixin<ViewModel> 是否正确？**
   - 使用 `ViewModel` 作为泛型参数是否会导致问题？
   - 是否应该使用反射来获取正确的类型？

4. **如何调试 ViewModelMixin 注册问题？**
   - 如何确认目标 ViewModel 类型是否存在？
   - 如何查看 Harmony 在检查哪些成员？

5. **是否有其他方式实现 ViewModelMixin？**
   - 是否需要使用不同的基类？
   - 是否需要不同的特性标记方式？

## 技术环境

- **游戏版本**：Mount & Blade II: Bannerlord v1.3.11.104956
- **UIExtenderEx 版本**：2.13.2.0（从 Steam Workshop 安装）
- **Harmony 版本**：从 Steam Workshop 安装（Bannerlord.Harmony）
- **.NET Framework**：4.8
- **错误**：`System.NullReferenceException` 在 `ViewModelWithMixinPatch.Patch` 中

## 完整错误堆栈

```
[17:17:38.519] [QuickStart] UIExtenderEx enable FAILED: System.Reflection.TargetInvocationException: 
Exception has been thrown by the target of an invocation. 
---> System.NullReferenceException: Object reference not set to an instance of an object.

   at HarmonyLib.AccessTools.IsDeclaredMember[T](T member)
   at Bannerlord.UIExtenderEx.Patches.ViewModelWithMixinPatch.Patch(Harmony harmony, Type viewModelType, String refreshMethodName) 
   in /_/src/Bannerlord.UIExtenderEx/Patches/ViewModelWithMixinPatch.cs:line 51
   at Bannerlord.UIExtenderEx.Components.ViewModelComponent.RegisterViewModelMixin(Type mixinType, String refreshMethodName, Boolean handleDerived) 
   in /_/src/Bannerlord.UIExtenderEx/Components/ViewModelComponent.cs:line 123
   at Bannerlord.UIExtenderEx.UIExtenderRuntime.Register(IEnumerable`1 types) 
   in /_/src/Bannerlord.UIExtenderEx/UIExtenderRuntime.cs:line 113
   --- End of inner exception stack trace ---
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Object[] arguments, Signature sig, Boolean constructor)
   at System.Reflection.RuntimeMethodInfo.UnsafeInvokeInternal(Object obj, Object[] parameters, Object[] arguments)
   at System.Reflection.RuntimeMethodInfo.Invoke(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
   at System.Reflection.MethodBase.Invoke(Object obj, Object[] parameters)
   at QuickStartMod.QuickStartSubModule.OnSubModuleLoad() 
   in D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\SubModule\QuickStartSubModule.cs:line 135
```

## 文件位置

- **SubModule**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\SubModule\QuickStartSubModule.cs`
- **ViewModelMixin**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\SubModule\QuickStartViewModelMixin.cs`
- **日志文件**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\logs\rgl_log_13824.txt`

## 下一步行动

1. **确认 ViewModelMixin 特性中的类型名称是否正确**
2. **检查 BaseViewModelMixin 的泛型参数是否正确**
3. **尝试使用不同的方式实现 ViewModelMixin**
4. **添加更多调试信息来定位问题**

