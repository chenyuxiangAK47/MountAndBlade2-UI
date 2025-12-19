# QuickStartMod ViewModelMixin 注册失败 - 给 ChatGPT（强类型后仍失败）

## 问题状态

### ✅ 已完成的修改（按照 ChatGPT 建议）

1. **在 .csproj 中添加了引用**：
   - `TaleWorlds.MountAndBlade.ViewModelCollection.dll`
   - `TaleWorlds.MountAndBlade.GauntletUI.dll`

2. **ViewModelMixin 改为强类型**：
   - 从 `BaseViewModelMixin<ViewModel>` 改为 `BaseViewModelMixin<InitialMenuVM>`
   - 构造函数参数从 `ViewModel` 改为 `InitialMenuVM`
   - 添加了 `using TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu;`

3. **ViewModelMixin 特性**：
   - 使用完整类型名：`"TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuVM"`

### ❌ 仍然存在的问题

**即使使用了强类型 `BaseViewModelMixin<InitialMenuVM>`，UIExtenderEx 注册 ViewModelMixin 时仍然失败**

## 错误信息（最新测试 - rgl_log_12340.txt）

```
[18:02:40.900] [QuickStart] UIExtenderEx enable FAILED: System.Reflection.TargetInvocationException: 
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

## 探针日志结果（最新测试）

```
[18:02:40.766] [QuickStart] InitialMenuVM type found? True
[18:02:40.766] [QuickStart] InitialMenuVM full name: TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuVM
[18:02:40.766] [QuickStart] InitialMenuVM.RefreshValues found? True
[18:02:40.767] [QuickStart] UIExtenderEx 已加载: Bannerlord.UIExtenderEx, Version=2.13.2.0
[18:02:40.900] [QuickStart] UIExtenderEx enable FAILED: ...
```

**结论**：
- ✅ 类型存在且能找到
- ✅ RefreshValues 方法存在
- ✅ ViewModelMixin 已使用强类型 `BaseViewModelMixin<InitialMenuVM>`
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
using TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu;

namespace QuickStartMod
{
    // ViewModelMixin：向 InitialMenuVM 注入 ExecuteQuickStart 方法
    // 按照 ChatGPT 建议：使用强类型 BaseViewModelMixin<InitialMenuVM>
    // 使用完整类型名（RefreshValues 方法已确认存在，UIExtenderEx 应该能自动找到）
    [ViewModelMixin("TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuVM")]
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

## 核心问题

**即使使用了强类型 `BaseViewModelMixin<InitialMenuVM>`，UIExtenderEx 仍然失败**

可能的原因：
1. **ViewModelMixin 特性是否需要显式指定 refresh 方法名？**
   - 虽然 RefreshValues 已确认存在，但是否需要显式指定？
   - 尝试 `[ViewModelMixin("...", "RefreshValues")]` 编译失败（参数类型不匹配）

2. **UIExtenderEx 版本问题？**
   - 当前版本：2.13.2.0
   - 是否该版本的 ViewModelMixin 注册逻辑有特殊要求？

3. **是否还有其他必需的引用或配置？**
   - 是否需要引用其他 DLL？
   - 是否需要特定的编译设置？

## 需要帮助的问题

1. **为什么即使使用了强类型 `BaseViewModelMixin<InitialMenuVM>`，UIExtenderEx 仍然失败？**
   - 是否 ViewModelMixin 特性需要显式指定 refresh 方法名？
   - 如果需要，正确的语法是什么？（尝试 `[ViewModelMixin("...", "RefreshValues")]` 编译失败）

2. **UIExtenderEx 2.13.2.0 版本的 ViewModelMixin 特性签名是什么？**
   - 是否支持第二个参数（refresh 方法名）？
   - 如果需要指定，正确的语法是什么？

3. **是否有其他必需的引用或配置？**
   - 是否需要引用其他 DLL？
   - 是否需要特定的编译设置？

4. **是否有替代方案？**
   - 如果 ViewModelMixin 无法工作，是否有其他方式实现主菜单按钮功能？
   - 例如：直接使用 Harmony 补丁来添加按钮功能？

## 技术环境

- **游戏版本**：Mount & Blade II: Bannerlord v1.3.11.104956
- **UIExtenderEx 版本**：2.13.2.0（从 Steam Workshop 安装）
- **Harmony 版本**：从 Steam Workshop 安装（Bannerlord.Harmony）
- **.NET Framework**：4.8
- **InitialMenuVM 完整类型名**：`TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuVM`
- **RefreshValues 方法**：已确认存在
- **ViewModelMixin 基类**：`BaseViewModelMixin<InitialMenuVM>`（强类型）

## 文件位置

- **SubModule**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\SubModule\QuickStartSubModule.cs`
- **ViewModelMixin**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\SubModule\QuickStartViewModelMixin.cs`
- **UIExtension**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\SubModule\QuickStartUIExtension.cs`
- **最新日志**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\logs\rgl_log_12340.txt`










