# 修复总结 - 按 ChatGPT 要求

## 一、改动点

### 1. 添加 BUILD_ID 指纹（`QuickStartSubModule.cs`）

**改动位置：** 第 20 行

**改动内容：**
```csharp
// ✅ 按照 ChatGPT 要求：每次编译手动改 BUILD_ID，确保运行的是最新 DLL
private const string QS_BUILD_ID = "2025-12-19-2100";
```

**为什么会修复：**
- 每次编译手动改 BUILD_ID，游戏启动时打印，确保运行的是最新 DLL
- 避免"运行旧 DLL"导致的"分析都是幻觉"问题

### 2. 增强 DLL 指纹日志（`QuickStartSubModule.cs`）

**改动位置：** 第 75-104 行

**改动内容：**
```csharp
// ✅ 按照 ChatGPT 要求：必须在一进来就打 BUILD_ID + DLL 指纹
TaleWorlds.Library.Debug.Print(
    $"[QuickStart] ========== BUILD={QS_BUILD_ID} ==========",
    ...);
TaleWorlds.Library.Debug.Print(
    $"[QuickStart] Ver={version}, DllTime={fileTime}",
    ...);
```

**为什么会修复：**
- 游戏启动时立即打印 BUILD_ID，任何分析都必须基于包含 BUILD_ID 的日志
- 如果日志中没有 BUILD_ID 或 BUILD_ID 不匹配，说明运行的是旧 DLL

### 3. Patch 验证日志（`QuickStartSubModule.cs`）

**改动位置：** 第 420-461 行

**改动内容：**
- 在 PatchAll 后验证每个方法的 `Harmony.GetPatchInfo(method).Postfixes.Count`
- 如果 postfix count = 0，说明 patch 失败，立即发现问题

**为什么会修复：**
- 不是"觉得 patch 了"，而是 Harmony 确认 postfix count > 0
- 如果看到 postfix count = 0，那就是没 patch 上，别继续跑任何功能测试

### 4. 绑定 manager（`QuickStartCharCreationSkipper.cs`）

**改动位置：** 第 18-36 行

**改动内容：**
```csharp
// ✅ 按照 ChatGPT 要求：绑定 manager，不要每次都查找
private static object _boundManager = null;

public static void BindManager(object manager)
{
    _boundManager = manager;
    QuickStartHelper.InNarrative = true;
    QuickStartHelper.SeenCharacterCreation = true;
    _cooldown = 0.2f; // 等待 0.2s 让 CurrentMenu 完全初始化
    ...
}
```

**为什么会修复：**
- 在 StartNarrativeStage Postfix 中绑定 manager，后续使用绑定的 manager 而不是每次都查找
- 避免"CurrentMenu == null"时强行推进导致状态机半初始化

### 5. Patch StartNarrativeStage（`QuickStartPatches.cs`）

**改动位置：** 第 302-360 行

**改动内容：**
- 使用 `[HarmonyPatch]` + `TargetType()` + `TargetMethod()` 动态 patch
- Postfix 中调用 `QuickStartCharCreationSkipper.BindManager(__instance)`

**为什么会修复：**
- 不要用 OnApplicationTick 猜，而是直接 Patch 关键生命周期点
- StartNarrativeStage 是 CurrentMenu 初始化的确切时机点，在这里绑定 manager 最稳

### 6. 每帧只做一步（`QuickStartCharCreationSkipper.cs`）

**改动位置：** 第 151-180 行

**改动内容：**
```csharp
// ✅ 按照 ChatGPT 要求：每帧最多做一件事（选项 OR Next），禁止同帧多步推进
if (_phase == 2)
{
    // Phase 2: 选择选项
    bool optionSelected = TrySelectCurrentMenuOption(manager);
    if (optionSelected)
    {
        _phase = 3; // 下一阶段：切换菜单
        _cooldown = 0.15f; // 等待 0.15s 让选项生效
        return;
    }
}
else if (_phase == 3)
{
    // Phase 3: 切换菜单
    bool switched = TrySwitchToNextMenu(manager);
    ...
}
```

**为什么会修复：**
- 每帧最多做一件事（选择选项 OR Next），加 cooldown
- 避免同帧连推导致状态机不一致（这是 native crash 常见诱因）

### 7. TrySelectCurrentMenuOption 返回 bool（`QuickStartCharCreationSkipper.cs`）

**改动位置：** 第 1229-1343 行

**改动内容：**
- 方法签名从 `void TrySelectCurrentMenuOption(object manager)` 改为 `bool TrySelectCurrentMenuOption(object manager)`
- 成功返回 `true`，失败返回 `false`

**为什么会修复：**
- 返回 bool 可以判断是否成功选择选项，用于控制每帧只做一步的逻辑

## 二、预期日志关键字

在 `rgl_log.txt` 中搜索以下关键字：

| 关键字 | 说明 | 期望结果 |
|--------|------|----------|
| `BUILD=` | BUILD_ID 指纹 | 应该显示最新的 BUILD_ID（例如：`BUILD=2025-12-19-2100`） |
| `PATCH_VERIFY` | Patch 验证日志 | 每个构造函数和 RefreshValues 都应该有 postfix count > 0 |
| `PATCH_OK` | Patch 总结 | 应该显示 "总共成功 patch X 个方法"，X > 0 |
| `POSTFIX_HIT` | Postfix 触发日志 | 每次构造函数或 RefreshValues 被调用时都应该出现 |
| `ENSURE_INSERTED` | 按钮插入日志 | 按钮成功插入到菜单时出现 |
| `BindManager` | Manager 绑定日志 | StartNarrativeStage Postfix 中绑定 manager 时出现 |
| `CurrentMenu ready` | CurrentMenu 就绪日志 | StartNarrativeStage Postfix 中 CurrentMenu 已初始化时出现 |

## 三、验证步骤

### 步骤 1：编译并检查 BUILD_ID

```powershell
cd "D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod"
msbuild QuickStartMod.csproj /p:Configuration=Release /p:Platform=x64
```

**检查：** 确认 `QuickStartSubModule.cs` 中的 `QS_BUILD_ID` 是最新的（每次编译手动改）

### 步骤 2：启动游戏到主菜单

1. 启动 Bannerlord
2. 等待进入主菜单界面
3. **检查：主菜单顶部是否显示 "QS MOD 快速开始" 按钮**

### 步骤 3：检查日志 - BUILD_ID

打开 `rgl_log.txt`，搜索：

```
BUILD=
```

**期望看到：**
```
[QuickStart] ========== BUILD=2025-12-19-2100 ==========
[QuickStart] Ver=1.0.0.0, DllTime=2025-12-19 21:00:00
[QuickStart] Path=D:\...\QuickStartMod.dll
```

**如果看不到 BUILD_ID 或 BUILD_ID 不匹配，说明运行的是旧 DLL，需要重新编译。**

### 步骤 4：检查日志 - Patch 验证

在 `rgl_log.txt` 中搜索：

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

### 步骤 5：检查日志 - Postfix 触发

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

### 步骤 6：检查日志 - Manager 绑定

在 `rgl_log.txt` 中搜索：

```
BindManager
```

**期望看到：**
```
[QuickStart] CC: StartNarrativeStage postfix, CurrentMenu ready, manager bound
[QuickStart] CharCreation: BindManager() called, manager bound
```

**如果看不到 `BindManager`，说明 StartNarrativeStage Postfix 没有被触发，需要检查。**

### 步骤 7：点击按钮测试

1. 点击 "QS MOD 快速开始" 按钮
2. **检查：是否显示屏幕提示 "[QS MOD] 快速开始按钮已点击！正在启动新游戏..."**
3. **检查：是否进入角色创建界面**
4. **检查：是否自动选择文化 Vlandia**
5. **检查：是否自动跳过问卷（如果已实现）**

### 步骤 8：完整流程验证

1. 点击 "QS MOD 快速开始"
2. 等待自动跳过角色创建（如果已实现）
3. 进入大地图
4. **检查：是否获得 100,000 第纳尔**

## 四、改动文件清单

| 文件 | 改动行数 | 改动内容 |
|------|---------|---------|
| `QuickStartMod/SubModule/QuickStartSubModule.cs` | 20 | 添加 BUILD_ID 常量 |
| `QuickStartMod/SubModule/QuickStartSubModule.cs` | 75-104 | 增强 DLL 指纹日志 |
| `QuickStartMod/SubModule/QuickStartSubModule.cs` | 420-461 | 添加 Patch 验证日志 |
| `QuickStartMod/SubModule/QuickStartCharCreationSkipper.cs` | 18-36 | 添加 BindManager 方法 |
| `QuickStartMod/SubModule/QuickStartCharCreationSkipper.cs` | 40-75 | 修改 RunOnCharCreationState 使用绑定的 manager |
| `QuickStartMod/SubModule/QuickStartCharCreationSkipper.cs` | 151-180 | 每帧只做一步，加 cooldown |
| `QuickStartMod/SubModule/QuickStartCharCreationSkipper.cs` | 1229-1343 | TrySelectCurrentMenuOption 返回 bool |
| `QuickStartMod/SubModule/QuickStartPatches.cs` | 302-360 | 修改 StartNarrativeStage Postfix 调用 BindManager |

## 五、注意事项

1. **BUILD_ID 必须每次编译手动改**：确保运行的是最新 DLL
2. **Patch 验证必须通过**：如果 postfix count = 0，不要继续测试
3. **每帧只做一步**：避免同帧多步推进导致状态机不一致
4. **使用绑定的 manager**：不要每次都查找，避免 CurrentMenu == null 时强行推进

## 六、如果验证失败

### 问题 1：看不到 BUILD_ID 或 BUILD_ID 不匹配

**可能原因：**
- 运行的是旧 DLL
- BUILD_ID 没有在代码中更新

**解决：**
- 确认 `QuickStartSubModule.cs` 中的 `QS_BUILD_ID` 是最新的
- 重新编译并确认 DLL 路径正确

### 问题 2：PATCH_VERIFY 显示 postfix count = 0

**可能原因：**
- Harmony PatchAll 失败
- TargetMethods() 返回的方法没有被正确 patch

**解决：**
- 检查 `rgl_log.txt` 中是否有 "Harmony PatchAll 失败" 的错误
- 检查 `TargetMethods()` 是否返回了正确的方法

### 问题 3：看不到 BindManager 日志

**可能原因：**
- StartNarrativeStage Postfix 没有被触发
- CharacterCreationManager 类型没有找到

**解决：**
- 检查 `rgl_log.txt` 中是否有 "CharacterCreationManagerPatch: TargetType is null" 的日志
- 确认 CampaignSystem 已加载

### 问题 4：CurrentMenu 仍然为 null

**可能原因：**
- StartNarrativeStage 还没有被调用
- manager 没有正确绑定

**解决：**
- 确认 BindManager 被调用（查看日志）
- 确认 StartNarrativeStage Postfix 被触发

