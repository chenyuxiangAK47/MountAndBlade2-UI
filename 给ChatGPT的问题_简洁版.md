# QuickStartMod ViewModelMixin 注册失败 - 给 ChatGPT

## 问题描述

UIExtenderEx 在注册 ViewModelMixin 时失败，即使所有条件都满足。

## 已确认的信息

✅ **类型找到了**：`TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuVM`  
✅ **RefreshValues 方法找到了**：`InitialMenuVM.RefreshValues found? True`  
✅ **ViewModelMixin 特性已使用完整类型名**  
❌ **但 UIExtenderEx 在 patch 时仍然失败**

## 错误信息

```
[QuickStart] UIExtenderEx enable FAILED: System.Reflection.TargetInvocationException: 
Exception has been thrown by the target of an invocation. 
---> System.NullReferenceException: Object reference not set to an instance of an object.

   at HarmonyLib.AccessTools.IsDeclaredMember[T](T member)
   at Bannerlord.UIExtenderEx.Patches.ViewModelWithMixinPatch.Patch(Harmony harmony, Type viewModelType, String refreshMethodName) 
   in /_/src/Bannerlord.UIExtenderEx/Patches/ViewModelWithMixinPatch.cs:line 51
```

**关键点**：错误发生在 `AccessTools.IsDeclaredMember[T](T member)` - 说明传入的 member 是 null

## 当前代码

```csharp
[ViewModelMixin("TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuVM")]
public class QuickStartViewModelMixin : BaseViewModelMixin<ViewModel>
{
    public QuickStartViewModelMixin(ViewModel vm) : base(vm) { }
    
    [DataSourceMethod]
    public void ExecuteQuickStart() { /* ... */ }
}
```

## 核心问题

**使用了 `BaseViewModelMixin<ViewModel>` 而不是 `BaseViewModelMixin<InitialMenuVM>`**

原因：编译时找不到 `InitialMenuVM` 类型（游戏 DLL 只在运行时可用）

## 需要帮助

1. **如何在不编译时依赖 InitialMenuVM 的情况下，使用强类型的 BaseViewModelMixin？**
   - 是否可以使用反射在运行时创建泛型类型？
   - 或者是否有其他方式实现 ViewModelMixin？

2. **为什么即使找到了正确的类型和 RefreshValues 方法，UIExtenderEx 仍然失败？**
   - 是否因为泛型参数是 `ViewModel`（基类），而特性中指定的是 `InitialMenuVM`（子类）？

3. **是否有替代方案实现主菜单按钮功能？**
   - 如果 ViewModelMixin 无法工作，是否有其他方式？

## 技术环境

- **游戏版本**：Mount & Blade II: Bannerlord v1.3.11.104956
- **UIExtenderEx 版本**：2.13.2.0（Steam Workshop）
- **InitialMenuVM 完整类型名**：`TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuVM`
- **RefreshValues 方法**：已确认存在







