# QuickStartMod 按钮消失问题 - 给 ChatGPT

## 当前状态

### ✅ 已解决的问题
1. **UIExtenderEx 已成功启用** - 日志显示 `[QuickStart] UIExtenderEx enabled`
2. **PrefabExtension 已成功注册** - 按钮曾经显示出来过
3. **探针日志已打印所有方法** - 确认 InitialMenuVM 只有 2 个 Execute* 方法

### ❌ 当前问题

**按钮消失了** - 重新启用 ViewModelMixin 后，按钮不再显示

## 关键发现

### 探针日志结果（rgl_log_23536.txt）

InitialMenuVM 只有 2 个 Execute* 方法：
1. `ExecuteNavigateToDLCStorePage` (参数: 无)
2. `ExecuteCommand` (参数: String, Object[])

**结论**：没有 `ExecuteSandbox`、`ExecuteAction` 等方法，必须通过 ViewModelMixin 注入 `ExecuteQuickStart`

### 按钮显示历史

1. **第一次成功**：禁用 ViewModelMixin，按钮显示但无法点击（只是文本）
2. **第二次失败**：重新启用 ViewModelMixin，按钮消失

## 当前代码

### QuickStartViewModelMixin.cs
```csharp
using System;
using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu;

namespace QuickStartMod
{
    // ✅ 重新启用 ViewModelMixin（使用短名称，避免完整类型名问题）
    [ViewModelMixin("InitialMenuVM")]
    public class QuickStartViewModelMixin : BaseViewModelMixin<InitialMenuVM>
    {
        public QuickStartViewModelMixin(InitialMenuVM vm) : base(vm)
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

### QuickStartUIExtension.cs
```csharp
[PrefabExtension("InitialScreen", "descendant::NavigatableListPanel[@Id='MenuItems']")]
internal class QuickStartMenuButtonExtension : PrefabExtensionInsertPatch
{
    // ...
    Command.Click="ExecuteQuickStart"
    // ...
}
```

### QuickStartMod.csproj（已添加引用）
```xml
<Reference Include="TaleWorlds.MountAndBlade.ViewModelCollection">
  <HintPath>$(BANNERLORD_INSTALL_DIR)\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.ViewModelCollection.dll</HintPath>
  <Private>False</Private>
</Reference>
<Reference Include="TaleWorlds.MountAndBlade.GauntletUI">
  <HintPath>$(BANNERLORD_INSTALL_DIR)\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.GauntletUI.dll</HintPath>
  <Private>False</Private>
</Reference>
```

## 问题分析

### 可能的原因

1. **ViewModelMixin 注册失败导致整个 UIExtenderEx 注册失败**
   - 之前禁用 ViewModelMixin 时，UIExtenderEx 能成功启用
   - 重新启用后，可能又遇到了之前的 NullReferenceException

2. **UIExtenderEx 在注册 ViewModelMixin 时失败，导致后续 PrefabExtension 也不注册**
   - 如果 ViewModelMixin 注册失败，UIExtenderEx 可能会跳过整个 Assembly 的注册

3. **按钮 XML 中的 Command.Click 绑定失败**
   - 如果 ViewModelMixin 未成功注入 `ExecuteQuickStart`，按钮可能被隐藏或禁用

## 需要帮助的问题

1. **为什么禁用 ViewModelMixin 时按钮能显示，但重新启用后按钮消失？**
   - 是否 ViewModelMixin 注册失败会导致整个 UIExtenderEx 注册失败？
   - 或者 PrefabExtension 和 ViewModelMixin 必须同时成功才能显示按钮？

2. **如何让 ViewModelMixin 成功注册？**
   - 之前使用完整类型名 `"TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuVM"` 失败
   - 现在使用短名称 `"InitialMenuVM"` 是否也会失败？
   - 是否需要其他配置或参数？

3. **是否有替代方案？**
   - 如果 ViewModelMixin 无法工作，是否可以使用 Harmony 补丁直接添加方法到 InitialMenuVM？
   - 或者使用 `ExecuteCommand` 方法（需要传递参数）？

4. **UIExtenderEx 的注册机制是什么？**
   - 如果 ViewModelMixin 注册失败，是否会影响 PrefabExtension 的注册？
   - 是否可以分别注册 PrefabExtension 和 ViewModelMixin？

## 技术环境

- **游戏版本**：Mount & Blade II: Bannerlord v1.3.11.104956
- **UIExtenderEx 版本**：2.13.2.0（从 Steam Workshop 安装）
- **Harmony 版本**：从 Steam Workshop 安装（Bannerlord.Harmony）
- **.NET Framework**：4.8
- **InitialMenuVM 完整类型名**：`TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuVM`
- **ViewModelMixin 基类**：`BaseViewModelMixin<InitialMenuVM>`（强类型）
- **ViewModelMixin 特性**：`[ViewModelMixin("InitialMenuVM")]`（短名称）

## 文件位置

- **SubModule**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\SubModule\QuickStartSubModule.cs`
- **ViewModelMixin**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\SubModule\QuickStartViewModelMixin.cs`
- **UIExtension**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\SubModule\QuickStartUIExtension.cs`
- **最新日志**：`C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_23536.txt`

## 最新日志分析（rgl_log_22288.txt）

从最新日志看：
- ✅ 探针日志成功打印了所有方法
- ✅ InitialMenuVM 类型找到，RefreshValues 方法找到
- ❌ **UIExtenderEx enable FAILED** - 和之前一样的错误：
  ```
  [19:33:37.984] [QuickStart] UIExtenderEx enable FAILED: System.Reflection.TargetInvocationException: 
  Exception has been thrown by the target of an invocation. 
  ---> System.NullReferenceException: Object reference not set to an instance of an object.
  
     at HarmonyLib.AccessTools.IsDeclaredMember[T](T member)
     at Bannerlord.UIExtenderEx.Patches.ViewModelWithMixinPatch.Patch(Harmony harmony, Type viewModelType, String refreshMethodName) 
     in /_/src/Bannerlord.UIExtenderEx/Patches/ViewModelWithMixinPatch.cs:line 51
  ```

**结论**：即使使用短名称 `"InitialMenuVM"`，ViewModelMixin 仍然失败，导致整个 UIExtenderEx 注册失败，按钮消失。

## 下一步建议

1. **检查最新日志**，确认 ViewModelMixin 是否注册失败
2. **如果 ViewModelMixin 失败**，考虑使用 Harmony 补丁直接添加方法到 InitialMenuVM
3. **或者**，尝试分别注册 PrefabExtension 和 ViewModelMixin（如果 UIExtenderEx 支持）
4. **或者**，使用 Harmony 补丁在运行时动态添加 `ExecuteQuickStart` 方法到 InitialMenuVM 实例

