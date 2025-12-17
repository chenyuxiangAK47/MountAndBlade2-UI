# QuickStartMod 菜单项注入后消失 - 给 ChatGPT

## 当前状态

### ✅ 已完成的修改（按照 ChatGPT 建议）

1. **禁用了 ViewModelMixin**：
   - 整个 `QuickStartViewModelMixin` 类已注释掉
   - UIExtenderEx 不再尝试注册 ViewModelMixin

2. **禁用了 PrefabExtension**：
   - 整个 `QuickStartMenuButtonExtension` 类已注释掉
   - 避免与 Harmony 注入的菜单项重叠

3. **实现了菜单项注入代码**：
   - 创建了 `QuickStartMenuInjectPatch` 类
   - 使用 `[HarmonyPostfix]` 在 `RefreshValues` 后注入菜单项
   - 根据探针日志，实现了创建 `InitialStateOption` 和 `InitialMenuOptionVM` 的逻辑

### ❌ 当前问题

**菜单项注入后消失** - 按钮不再显示

## 探针日志结果（rgl_log_25300.txt）

### MenuOptions 类型信息
```
[20:15:17.527] [QuickStart] MenuOptions 属性类型: TaleWorlds.Library.MBBindingList`1[[TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuOptionVM, ...]]
[20:15:17.527] [QuickStart] MenuOptions item 类型: TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuOptionVM
[20:15:17.527] [QuickStart] ========== Item 类型构造函数 ==========
[20:15:17.527] [QuickStart]  - InitialStateOption initialStateOption
[20:15:17.527] [QuickStart] ========== Item 类型属性 ==========
[20:15:17.527] [QuickStart]  - DisabledHint (HintViewModel)
[20:15:17.527] [QuickStart]  - EnabledHint (HintViewModel)
[20:15:17.527] [QuickStart]  - NameText (String)
[20:15:17.527] [QuickStart]  - IsDisabled (Boolean)
[20:15:17.527] [QuickStart]  - IsHidden (Boolean)
```

### 菜单项注入日志（最新测试 - rgl_log_6148.txt）
```
[20:20:40.789] [QuickStart] ========== 开始注入菜单项 ==========
[20:20:40.789] [QuickStart] MenuOptions 类型: TaleWorlds.Library.MBBindingList`1[[...]]
[20:20:40.789] [QuickStart] Item 类型: TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuOptionVM
[20:20:40.789] [QuickStart] InitialStateOption 类型: TaleWorlds.MountAndBlade.InitialStateOption
[20:20:40.789] [QuickStart] 创建 InitialStateOption 失败，尝试其他构造函数
[20:20:40.789] [QuickStart] 无法创建 InitialStateOption
```

**问题**：
- ✅ InitialStateOption 类型找到了
- ❌ **创建 InitialStateOption 失败** - 所有构造函数尝试都失败了
- ❌ 因此无法创建 InitialMenuOptionVM，菜单项无法注入

## 当前代码

### QuickStartPatches.cs
```csharp
[HarmonyPatch(typeof(InitialMenuVM))]
public static class QuickStartMenuInjectPatch
{
    private static bool _added = false;

    [HarmonyPostfix]
    [HarmonyPatch("RefreshValues")]
    public static void Postfix(InitialMenuVM __instance)
    {
        if (_added) return;

        try
        {
            // 1) 获取 MenuOptions
            var menuOptionsProp = AccessTools.Property(__instance.GetType(), "MenuOptions");
            var listObj = menuOptionsProp.GetValue(__instance);
            
            // 2) 获取 item 类型
            var itemType = listObj.GetType().GetGenericArguments()[0];
            
            // 3) 查找 InitialStateOption 类型
            var stateOptionType = Type.GetType("TaleWorlds.MountAndBlade.InitialStateOption, TaleWorlds.MountAndBlade");
            // ... 尝试从所有程序集中查找 ...
            
            // 4) 创建 Action
            Action quickStartAction = () => { /* 快速开始逻辑 */ };
            
            // 5) 创建 InitialStateOption
            // 尝试 3 参数构造函数：(string name, Action action, Func<bool> isEnabled)
            // 如果失败，尝试 2 参数构造函数：(string name, Action action)
            
            // 6) 创建 InitialMenuOptionVM
            var item = Activator.CreateInstance(itemType, stateOption);
            
            // 7) 插入到列表顶部
            var insertMethod = listType.GetMethod("Insert", new[] { typeof(int), itemType });
            insertMethod.Invoke(listObj, new object[] { 0, item });
            
            _added = true;
        }
        catch (Exception ex)
        {
            // 错误处理
        }
    }
}
```

## 问题分析

### 可能的原因

1. **InitialStateOption 创建失败**：
   - 构造函数参数不匹配
   - InitialStateOption 类型找不到
   - 创建过程中抛出异常

2. **InitialMenuOptionVM 创建失败**：
   - 传入的 InitialStateOption 参数不正确
   - 创建过程中抛出异常

3. **插入失败**：
   - Insert 方法调用失败
   - 插入后立即被移除（RefreshValues 再次调用？）

4. **防重复标记问题**：
   - `_added = true` 设置后，如果 RefreshValues 再次调用，会直接 return
   - 但如果第一次注入失败，后续就不会再尝试

## 需要帮助的问题

1. **InitialStateOption 的构造函数签名是什么？**
   - 探针日志只显示了 `InitialMenuOptionVM` 的构造函数参数是 `InitialStateOption initialStateOption`
   - 但我们需要知道如何创建 `InitialStateOption` 实例
   - 根据旧代码，可能是 `(string name, Action action, Func<bool> isEnabled)` 或 `(string name, Action action)`
   - 但实际签名是什么？需要反编译查看 `TaleWorlds.MountAndBlade.InitialStateOption` 的构造函数
   - **最新日志显示所有构造函数尝试都失败了**，说明参数不匹配

2. **如何确认菜单项是否成功创建和插入？**
   - 当前代码在创建和插入过程中可能抛出异常，但被 catch 吞掉了
   - 需要更详细的日志来定位问题

3. **RefreshValues 是否会多次调用？**
   - 如果 RefreshValues 多次调用，`_added` 标记可能导致后续调用直接跳过
   - 但如果第一次注入失败，后续就不会再尝试

4. **是否有其他方式注入菜单项？**
   - 是否应该 Patch `RefreshMenuOptions` 而不是 `RefreshValues`？
   - 或者应该在更早的时机注入？

## 技术环境

- **游戏版本**：Mount & Blade II: Bannerlord v1.3.11.104956
- **UIExtenderEx 版本**：2.13.2.0（从 Steam Workshop 安装）
- **Harmony 版本**：从 Steam Workshop 安装（Bannerlord.Harmony）
- **.NET Framework**：4.8
- **MenuOptions 类型**：`TaleWorlds.Library.MBBindingList<InitialMenuOptionVM>`
- **Item 类型**：`TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuOptionVM`
- **Item 构造函数**：`InitialMenuOptionVM(InitialStateOption initialStateOption)`

## 文件位置

- **Patches**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\SubModule\QuickStartPatches.cs`
- **SubModule**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\SubModule\QuickStartSubModule.cs`
- **最新日志**：`C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_25300.txt`

## 下一步建议

1. **添加更详细的日志**，记录每个步骤的成功/失败
2. **确认 InitialStateOption 的构造函数签名**（可能需要反编译查看）
3. **检查 RefreshValues 是否多次调用**，以及 `_added` 标记是否正确
4. **尝试 Patch RefreshMenuOptions 而不是 RefreshValues**

