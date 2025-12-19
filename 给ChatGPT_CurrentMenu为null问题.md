# 给 ChatGPT：CurrentMenu 为 null 导致选择选项失败

## 📊 最新日志分析

### ✅ 成功的部分

1. **按钮点击成功**：`[QuickStart] >>> QS BUTTON CLICKED <<<`
2. **进入角色创建状态**：`[QuickStart] ActiveState = TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationState`
3. **找到 CharacterCreationManager**：`[QuickStart] CharCreation: found CharacterCreationManager via property`
4. **成功设置文化**：`[QuickStart] CharCreation: set culture to Vlandia via SetSelectedCulture()`

### ❌ 失败的部分

**核心问题**：`CurrentMenu is null`

```
[2025-12-19 20:46:35.414] [QuickStart] ActiveState = TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationState
[2025-12-19 20:46:35.414] [QuickStart] CharCreation: found CharacterCreationManager via property
[2025-12-19 20:46:35.415] [QuickStart] CharCreation: Manager found, starting auto-skip process
[2025-12-19 20:46:35.418] [QuickStart] CharCreation: set culture to Vlandia via SetSelectedCulture()
[2025-12-19 20:46:35.419] [QuickStart] CharCreation: TrySelectCurrentMenuOption - CurrentMenu is null
[2025-12-19 20:46:35.422] [QuickStart] CharCreation: All actions failed (culture/select/switch), will retry
```

**问题分析**：
- 进入 `CharacterCreationState` 后，`CharacterCreationManager` 存在
- 但 `CharacterCreationManager.CurrentMenu` 为 `null`
- 因为 `CurrentMenu` 为 `null`，无法获取选项，无法选择，无法切换菜单
- 代码不断重试，但 `CurrentMenu` 一直为 `null`，直到进入 `MapState`

## 🔍 从反编译源码分析

### CharacterCreationManager 的初始化流程

从 `CharacterCreationManager.cs` 看到：

```csharp
public CharacterCreationManager(CharacterCreationState state)
{
    _state = state;
    _stages = new MBList<CharacterCreationStageBase>();
    FaceGenHistory = new FaceGenHistory(...);
    _narrativeMenus = new MBList<NarrativeMenu>();
    SelectedOptions = new Dictionary<NarrativeMenu, NarrativeMenuOption>();
    CharacterCreationContent = new CharacterCreationContent();
    CampaignEventDispatcher.Instance.OnCharacterCreationInitialized(this);
    // ... 初始化 handlers
}

public NarrativeMenu CurrentMenu { get; private set; }  // 初始值为 null

public void StartNarrativeStage()
{
    NarrativeMenu currentMenu = NarrativeMenus.FirstOrDefault((NarrativeMenu m) => m.InputMenuId == "start");
    CurrentMenu = currentMenu;  // 这里才设置 CurrentMenu
    ModifyMenuCharacters();
}
```

**关键发现**：
- `CurrentMenu` 初始值为 `null`
- 需要调用 `StartNarrativeStage()` 才会设置 `CurrentMenu`
- 或者通过 `TrySwitchToNextMenu()` 切换菜单时才会设置

### CharacterCreationState 的激活流程

从 `CharacterCreationState.cs` 看到：

```csharp
protected override void OnActivate()
{
    base.OnActivate();
    CharacterCreationManager.OnStateActivated();
}

// CharacterCreationManager.OnStateActivated()
internal void OnStateActivated()
{
    if (_stageIndex == -1)
    {
        NextStage();  // 第一次激活时调用 NextStage
    }
}
```

**问题**：
- `OnActivate()` 调用 `CharacterCreationManager.OnStateActivated()`
- `OnStateActivated()` 调用 `NextStage()`
- 但 `NextStage()` 可能不会立即设置 `CurrentMenu`
- `CurrentMenu` 可能在后续的某个阶段才被设置

## 💡 需要 ChatGPT 帮助的问题

### 问题 1: CurrentMenu 何时被初始化？

**从源码看到**：
- `CurrentMenu` 在 `StartNarrativeStage()` 中被设置
- `StartNarrativeStage()` 查找 `InputMenuId == "start"` 的菜单

**需要确认**：
- `StartNarrativeStage()` 何时被调用？
- 是在 `OnStateActivated()` 时调用，还是在后续某个阶段调用？
- 是否需要等待某个事件或状态？

### 问题 2: 如何等待 CurrentMenu 初始化？

**当前问题**：
- 代码在 `CurrentMenu` 为 `null` 时就返回了
- 但 `CurrentMenu` 可能在后续的某个 tick 才被初始化

**可能的解决方案**：
1. **等待策略**：在 `CurrentMenu` 为 `null` 时不返回，而是等待一段时间
2. **事件监听**：监听 `CurrentMenu` 的变化事件（如果有）
3. **主动初始化**：调用 `StartNarrativeStage()` 来初始化 `CurrentMenu`

### 问题 3: 是否可以主动调用 StartNarrativeStage()？

**从源码看到**：
```csharp
public void StartNarrativeStage()
{
    NarrativeMenu currentMenu = NarrativeMenus.FirstOrDefault((NarrativeMenu m) => m.InputMenuId == "start");
    CurrentMenu = currentMenu;
    ModifyMenuCharacters();
}
```

**问题**：
- 这个方法是否是 public 的？
- 是否可以安全地调用？
- 调用后是否会有副作用？

### 问题 4: 选择选项的正确时机是什么？

**当前流程**：
1. 进入 `CharacterCreationState`
2. 设置文化
3. 尝试选择选项（但 `CurrentMenu` 为 `null`）
4. 失败，重试

**可能的问题**：
- 设置文化后，可能需要等待 UI 更新
- `CurrentMenu` 可能在设置文化后才初始化
- 或者需要先调用某个初始化方法

**需要确认**：
- 选择选项的正确时机是什么？
- 是否需要等待 `CurrentMenu` 初始化？
- 是否需要先调用某个初始化方法？

## 🔧 当前代码实现

### TrySelectCurrentMenuOption 方法

```csharp
private static void TrySelectCurrentMenuOption(object manager)
{
    // 1) 检查 CurrentMenu 是否存在
    PropertyInfo currentMenuProp = managerType.GetProperty("CurrentMenu", BF);
    object currentMenu = currentMenuProp.GetValue(manager, null);
    if (currentMenu == null)
    {
        WriteFileLog("[QuickStart] CharCreation: TrySelectCurrentMenuOption - CurrentMenu is null");
        return;  // ← 这里直接返回了，没有等待
    }
    
    // 2) 获取选项
    // ...
}
```

**问题**：当 `CurrentMenu` 为 `null` 时直接返回，没有等待它初始化。

## 💡 建议的解决方案

### 方案 1: 主动调用 StartNarrativeStage() ✅ 推荐

从反编译源码确认：`StartNarrativeStage()` 是 **public** 方法！

```csharp
public void StartNarrativeStage()
{
    NarrativeMenu currentMenu = NarrativeMenus.FirstOrDefault((NarrativeMenu m) => m.InputMenuId == "start");
    CurrentMenu = currentMenu;
    ModifyMenuCharacters();
}
```

**实现方式**：
- 检查 `CurrentMenu` 是否为 `null`
- 如果为 `null`，调用 `StartNarrativeStage()` 来初始化
- 然后再选择选项

**代码示例**：
```csharp
// 在 RunOnCharCreationState 中
if (manager != null)
{
    // 检查 CurrentMenu
    PropertyInfo currentMenuProp = managerType.GetProperty("CurrentMenu", BF);
    object currentMenu = currentMenuProp.GetValue(manager, null);
    
    if (currentMenu == null)
    {
        // 主动调用 StartNarrativeStage() 初始化
        MethodInfo startNarrativeMethod = managerType.GetMethod("StartNarrativeStage", BF);
        if (startNarrativeMethod != null && startNarrativeMethod.GetParameters().Length == 0)
        {
            startNarrativeMethod.Invoke(manager, null);
            WriteFileLog("[QuickStart] CharCreation: Called StartNarrativeStage() to initialize CurrentMenu");
        }
    }
}
```

### 方案 2: 等待 CurrentMenu 初始化

在 `CurrentMenu` 为 `null` 时，不立即返回，而是：
- 记录等待次数
- 如果等待次数超过阈值（如 10 次，约 2.5 秒），再返回
- 或者添加一个延迟，等待几个 tick

### 方案 3: 监听 CurrentMenu 的变化

如果 `CurrentMenu` 有 setter 或变化事件，可以：
- 监听变化事件
- 当 `CurrentMenu` 被设置时，再执行选择选项的逻辑

### 方案 4: 调整执行顺序

可能的问题是执行顺序不对：
- 当前：设置文化 → 选择选项
- 可能需要：等待 → 设置文化 → 等待 → 选择选项

## 📝 需要 ChatGPT 确认的信息

1. **CurrentMenu 的初始化时机**：
   - `StartNarrativeStage()` 何时被调用？
   - 是在 `OnStateActivated()` 时，还是在后续某个阶段？

2. **是否可以主动调用 StartNarrativeStage()**：
   - 这个方法是 public 的吗？
   - 是否可以安全地调用？

3. **选择选项的正确流程**：
   - 是否需要先等待 `CurrentMenu` 初始化？
   - 设置文化后，是否需要等待 UI 更新？
   - 选择选项的正确时机是什么？

4. **Harmony Patch 的问题**：
   - 为什么 Harmony PatchAll 被跳过？
   - `CharacterCreationState` 是否有 `OnTick` 方法？
   - 如果没有，应该 Patch 哪个方法？

## 🔗 相关源码位置

- **CharacterCreationManager.cs**：
  - `CurrentMenu` 属性定义
  - `StartNarrativeStage()` 方法
  - `OnStateActivated()` 方法

- **CharacterCreationState.cs**：
  - `OnActivate()` 方法
  - `CharacterCreationManager` 属性

---

**日志文件位置**：`Modules/QuickStartMod/qs_runtime.log`  
**反编译源码位置**：`D:\Bannerlord_Decompiled\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterCreationContent\`

**核心问题**：`CurrentMenu is null` - 需要找到 `CurrentMenu` 何时被初始化，以及如何等待或主动初始化它。

