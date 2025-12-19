# 修复说明 - 按 ChatGPT 要求

## 一、改动前后差异点

### 改动 1：TargetMethods() - 修复构造函数和 RefreshValues 获取方式

**改动前：**
```csharp
// 只获取一个构造函数
var ctor = AccessTools.Constructor(typeof(InitialMenuVM));

// 使用 Method（可能获取到父类同名方法）
var rv = AccessTools.Method(typeof(InitialMenuVM), "RefreshValues");
```

**改动后：**
```csharp
// ✅ 获取所有声明的构造函数
var ctors = AccessTools.GetDeclaredConstructors(typeof(InitialMenuVM));
foreach (var ctor in ctors) { ... }

// ✅ 使用 DeclaredMethod（只获取声明的，避免父类同名方法）
var rv = AccessTools.DeclaredMethod(typeof(InitialMenuVM), "RefreshValues");
```

**为什么会修复：**
- `AccessTools.Constructor` 只返回一个构造函数，但 `InitialMenuVM` 可能有多个构造函数（无参、有参等），只 patch 一个会导致其他构造函数路径的按钮注入失败
- `AccessTools.Method` 会搜索整个继承链，可能找到父类的 `RefreshValues`，导致 patch 到错误的方法
- `AccessTools.DeclaredMethod` 只获取当前类声明的，确保 patch 到正确的方法

### 改动 2：Postfix 日志增强

**改动前：**
```csharp
TaleWorlds.Library.Debug.Print(
    $"[QuickStart] Postfix hit: {declaringType}.{methodName}, MenuOptions.Count={count}",
    0,
    TaleWorlds.Library.Debug.DebugColor.Yellow);
```

**改动后：**
```csharp
// ✅ 添加可搜索的关键字 "POSTFIX_HIT"
// ✅ 添加 method.ToString() 完整签名
TaleWorlds.Library.Debug.Print(
    $"[QuickStart] POSTFIX_HIT: {declaringType}.{methodName} | {methodToString} | MenuOptions.Count={count}",
    0,
    TaleWorlds.Library.Debug.DebugColor.Green);
```

**为什么会修复：**
- 使用统一的关键字 `POSTFIX_HIT` 便于在日志中搜索验证
- 打印完整的方法签名（`ToString()`）便于调试
- 颜色改为 Green 更醒目

### 改动 3：Patch 验证日志

**改动前：**
```csharp
_harmony.PatchAll(...);
TaleWorlds.Library.Debug.Print("[QuickStart] Harmony PatchAll 完成（按钮注入）", ...);
```

**改动后：**
```csharp
_harmony.PatchAll(...);

// ✅ 验证每个被 patch 的方法的 PatchInfo
foreach (var ctor in ctors) {
    var patchInfo = Harmony.GetPatchInfo(ctor);
    var postfixCount = patchInfo?.Postfixes?.Count ?? 0;
    TaleWorlds.Library.Debug.Print(
        $"[QuickStart] PATCH_VERIFY: {ctor.ToString()} | Postfix count={postfixCount}",
        ...);
}

// ✅ 打印总结
TaleWorlds.Library.Debug.Print(
    $"[QuickStart] PATCH_OK: 总共成功 patch {totalPatched} 个方法",
    ...);
```

**为什么会修复：**
- 在 PatchAll 后立即验证每个方法是否真的被 patch 成功
- 打印 `PatchInfo.Postfixes.Count` 确认 postfix 数量
- 如果 postfix count = 0，说明 patch 失败，可以立即发现问题

### 改动 4：EnsureQuickStart 日志增强

**改动前：**
```csharp
TaleWorlds.Library.Debug.Print("[QuickStart] EnsureQuickStart: 注入完成。现在 MenuOptions.Count=" + vm.MenuOptions.Count, ...);
```

**改动后：**
```csharp
// ✅ 使用统一关键字 "ENSURE_INSERTED"
TaleWorlds.Library.Debug.Print(
    $"[QuickStart] ENSURE_INSERTED: 菜单项已插入，现在 MenuOptions.Count={vm.MenuOptions.Count}",
    ...);
```

**为什么会修复：**
- 使用统一关键字 `ENSURE_INSERTED` 便于搜索验证
- 确认按钮是否真的被插入到菜单

## 二、可搜索的日志关键字

在 `rgl_log.txt` 或 `qs_runtime.log` 中搜索以下关键字：

| 关键字 | 说明 | 期望结果 |
|--------|------|----------|
| `PATCH_VERIFY` | Patch 验证日志 | 每个构造函数和 RefreshValues 都应该有 postfix count > 0 |
| `PATCH_OK` | Patch 总结 | 应该显示 "总共成功 patch X 个方法"，X > 0 |
| `POSTFIX_HIT` | Postfix 触发日志 | 每次构造函数或 RefreshValues 被调用时都应该出现 |
| `ENSURE_INSERTED` | 按钮插入日志 | 按钮成功插入到菜单时出现 |

## 三、验证步骤

### 步骤 1：编译并启动游戏

```powershell
cd "D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod"
msbuild QuickStartMod.csproj /p:Configuration=Release /p:Platform=x64
```

### 步骤 2：启动游戏到主菜单

1. 启动 Bannerlord
2. 等待进入主菜单界面
3. **检查：主菜单顶部是否显示 "QS MOD 快速开始" 按钮**

### 步骤 3：检查日志 - Patch 验证

打开 `rgl_log.txt`（通常在 `Documents\Mount and Blade II Bannerlord\logs\`），搜索：

```
PATCH_VERIFY
```

**期望看到：**
```
[QuickStart] PATCH_VERIFY: Void .ctor(...) | Postfix count=1
[QuickStart] PATCH_VERIFY: Void RefreshValues() | Postfix count=1
[QuickStart] PATCH_OK: 总共成功 patch 2 个方法
```

**如果看到 `Postfix count=0`，说明 patch 失败，需要检查。**

### 步骤 4：检查日志 - Postfix 触发

在 `rgl_log.txt` 中搜索：

```
POSTFIX_HIT
```

**期望看到：**
```
[QuickStart] POSTFIX_HIT: InitialMenuVM..ctor | Void .ctor(...) | MenuOptions.Count=X
[QuickStart] POSTFIX_HIT: InitialMenuVM.RefreshValues | Void RefreshValues() | MenuOptions.Count=X
```

**如果看不到 `POSTFIX_HIT`，说明 Postfix 没有被触发，需要检查。**

### 步骤 5：检查日志 - 按钮插入

在 `rgl_log.txt` 中搜索：

```
ENSURE_INSERTED
```

**期望看到：**
```
[QuickStart] ENSURE_INSERTED: 菜单项已插入，现在 MenuOptions.Count=X
```

**如果看不到 `ENSURE_INSERTED`，说明按钮没有被插入，需要检查。**

### 步骤 6：点击按钮测试

1. 点击 "QS MOD 快速开始" 按钮
2. **检查：是否显示屏幕提示 "[QS MOD] 快速开始按钮已点击！正在启动新游戏..."**
3. **检查：是否进入角色创建界面**

### 步骤 7：完整流程验证

1. 点击 "QS MOD 快速开始"
2. 等待自动跳过角色创建（如果已实现）
3. 进入大地图
4. **检查：是否获得 100,000 第纳尔**

## 四、改动文件清单

| 文件 | 改动行数 | 改动内容 |
|------|---------|---------|
| `QuickStartMod/SubModule/QuickStartPatches.cs` | 20-36 | TargetMethods() - 改为 GetDeclaredConstructors 和 DeclaredMethod |
| `QuickStartMod/SubModule/QuickStartPatches.cs` | 56-74 | Postfix() - 增强日志，添加 POSTFIX_HIT 关键字 |
| `QuickStartMod/SubModule/QuickStartPatches.cs` | 256 | EnsureQuickStart() - 添加 ENSURE_INSERTED 关键字 |
| `QuickStartMod/SubModule/QuickStartSubModule.cs` | 416-450 | OnSubModuleLoad() - 添加 PATCH_VERIFY 和 PATCH_OK 日志 |

## 五、注意事项

1. **最小改动原则**：只修复了 ChatGPT 指出的问题，没有重构其他代码
2. **保留原有逻辑**：EnsureQuickStart 的插入逻辑、按钮点击逻辑等都没有改动
3. **日志增强**：所有关键点都添加了可搜索的关键字，便于验证
4. **向后兼容**：改动不影响现有功能，只是增强了稳定性和可调试性

## 六、如果验证失败

### 问题 1：看不到 PATCH_VERIFY 或 postfix count = 0

**可能原因：**
- Harmony PatchAll 失败
- TargetMethods() 返回的方法没有被正确 patch

**检查：**
- 查看 `rgl_log.txt` 中是否有 "Harmony PatchAll 失败" 的错误
- 检查 `TargetMethods()` 是否返回了正确的方法

### 问题 2：看不到 POSTFIX_HIT

**可能原因：**
- Postfix 没有被触发（方法没有被调用）
- Postfix 签名不匹配

**检查：**
- 确认 Postfix 签名是 `Postfix(InitialMenuVM __instance, MethodBase __originalMethod)`
- 检查 `InitialMenuVM` 的构造函数和 `RefreshValues` 是否真的被调用了

### 问题 3：看不到 ENSURE_INSERTED

**可能原因：**
- `EnsureQuickStart()` 没有被调用
- `MenuOptions.Count == 0` 导致提前返回

**检查：**
- 查看是否有 "EnsureQuickStart: MenuOptions.Count == 0，跳过" 的日志
- 确认 Postfix 是否真的调用了 `EnsureQuickStart()`

