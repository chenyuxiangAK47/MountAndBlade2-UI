## 场景描述

- 游戏：Mount & Blade II: Bannerlord v1.3.11.104956（含 NavalDLC）
- 启用模块：`Bannerlord.Harmony`、`Bannerlord.UIExtenderEx`、`Native`、`SandBoxCore`、`BirthAndDeath`、`Sandbox`、`StoryMode`、`CustomBattle`、`NavalDLC`、`QuickStartMod`
- QuickStartMod 的主要功能：
  - 在主菜单顶部注入一个“快速开始”按钮（Harmony Patch 注入 `InitialMenuVM.MenuOptions`）
  - 按钮点击后调用 `Game.StartNewGame(false)` 启动沙盒模式
  - 通过 `QuickStartHelper.IsQuickStartMode` 标志，在 `OnSessionLaunched` / `OnCampaignStart` 给主角 100000 金币

## 崩溃目录

- 路径：`Modules/crashes/2025-12-18_02.17.32/`
- 关键文件：
  - `rgl_log_33168.txt`
  - `rgl_log_errors_33168.txt`
  - `crash_tags.txt`
  - `module_list.txt`

## 日志关键信息

### 1. 启动参数 & 版本

`crash_tags.txt`：

```text
[Runtime][Arguments][/singleplayer _MODULES_*Bannerlord.Harmony*Bannerlord.UIExtenderEx*Native*SandBoxCore*BirthAndDeath*Sandbox*StoryMode*CustomBattle*NavalDLC*QuickStartMod*_MODULES_]
[Runtime][Build Version][v1.3.11.104956]
```

### 2. 托管日志中 QuickStartMod 的输出

在 `rgl_log_33168.txt` 里，只能看到 QuickStartMod 的以下日志：

```text
[10:17:26.282] [QuickStart] Postfix hit: .ctor, MenuOptions.Count=0 
[10:17:26.282] [QuickStart] EnsureQuickStart: vm 或 MenuOptions 为 null 
...（上面两行重复 2 次）...
[10:17:26.282] AddGlobalLayer 
[10:17:26.286] [QuickStart] OnGameStart Campaign 
```

说明：

- Patch 的 `Postfix` 在 `InitialMenuVM` 构造函数上被触发，但此时 `MenuOptions == null`，我们在代码里是直接 `return`，**不会抛异常**。
- `OnGameStart` 只是在启动战役模式时打了一行日志，目前还没有更复杂的逻辑。
- 日志中 **没有看到任何 `[QuickStart] ... FAILED` 或 Exception 的堆栈**。

### 3. 崩溃时段周围的引擎日志

崩溃前的最后一大段日志，全部都是在加载 XML 资源和 Prefab：

```text
[10:17:25.914] Initializing new game begin...
...
[10:17:26.220] opening ..\..\Modules\SandBox/ModuleData/conversation_animations.xml
...
[10:17:26.282] [QuickStart] Postfix hit: .ctor, MenuOptions.Count=0 
[10:17:26.282] [QuickStart] EnsureQuickStart: vm 或 MenuOptions 为 null 
...（QuickStart 相同两行又重复 2 次）...
[10:17:26.282] AddGlobalLayer 
[10:17:26.286] [QuickStart] OnGameStart Campaign 
...
[10:17:26.733] opening ..\..\Modules\NavalDLC/ModuleData/naval_skill_sets.xml
...
[10:17:26.845] opening ..\..\Modules\NavalDLC/ModuleData/naval_weapons.xml
...
[10:17:26.988] opening ..\..\Modules\SandBox/ModuleData/education_equipment_templates.xml
```

同时前面还有大量资源缺失的警告，例如：

```text
Unable to find item to add dependency(depender empire_helmet_a)
Unable to find particle system with name waterfall_splash
...
```

但 `rgl_log_errors_33168.txt` 只包含头信息，并 **没有**给出托管异常堆栈。

## 目前的判断

1. 从 `rgl_log_33168.txt` 和 `rgl_log_errors_33168.txt` 来看，**没有任何由 QuickStartMod 抛出的托管异常**；  
   - `EnsureQuickStart` 在 `vm == null` / `MenuOptions == null` 时只是打日志然后 `return`。
2. 崩溃发生在引擎加载 XML 资源、NavalDLC 物品/技能/Prefab 阶段，日志里没有 .NET 异常栈，**更像是原生层（C++）崩溃** 或资源问题，而不是我们 C# mod 直接 throw。
3. 崩溃目录下同时存在上一轮的 `rgl_log_49220.txt` 等多个 log，看起来是这几次都是在**加载阶段**挂掉，而不是进战役后脚本崩溃。

## 想请 ChatGPT 帮忙确认的问题

1. **从这些日志片段是否能进一步判断崩溃更可能来自哪个模块？**  
   - 特别是 NavalDLC 相关 XML / Prefab / 粒子系统大量 `Unable to find` 的警告，会不会在 v1.3.11 某些组合下触发原生崩溃？
2. **QuickStartMod 目前的 Harmony Patch 是否有潜在风险？**
   - Patch 目标：`InitialMenuVM` 的构造函数、`RefreshValues` 以及若干 `void` 无参的 `*Refresh*`/`*Menu*`/`*Option*` 方法。
   - Postfix 代码中我们已经做了：
     - 判空：`if (vm == null || vm.MenuOptions == null) return;`
     - 计数判断：`if (vm.MenuOptions.Count == 0) return;`
     - 去重判断：`HasQuickStart` 保证不会重复插入。
   - 在你看来，这种 Postfix 方式有可能在 **加载战役 / 初始菜单** 时和引擎的 UI 生命周期冲突到导致原生崩溃吗？（目前日志没看到迹象）
3. **有没有推荐的进一步定位方式？**
   - 例如：
     - 只启用：`Native + SandBoxCore + Sandbox + StoryMode + Harmony + UIExtenderEx + QuickStartMod`，关闭所有工坊 mod，看是否还崩。
     - 或者用某种方式读取 `.dmp` 里的调用栈（如果有现成流程的话）。
4. **这些 `Unable to find item/particle system` 的警告在这版本是否常见？**  
   - 在 vanilla + NavalDLC 的情况下，这些 warning 是否“正常存在但不影响稳定”，还是已经说明安装包/资源有损坏的可能？

如果需要，我可以再把 QuickStartMod 的关键源码（`QuickStartPatches.cs` 和 `QuickStartSubModule.cs` 的相关片段）贴出来，方便你一起 review 是否有明显问题。

## 再次改成 OnApplicationTick + MapState 延迟发金币，仍在加载阶段原生崩溃（2025-12-18 05:11:22）

这一次，我按照“尽量远离战役初始化逻辑”的思路，把所有 `CampaignBehaviorBase` / `CampaignEvents` 监听都删掉，仅在 `MBSubModuleBase.OnApplicationTick` 里轮询战役状态，等确定已经进大地图后再一次性发钱，但在启用 QuickStartMod 的情况下，点击“沙盒模式”依旧会在加载阶段 native 崩溃。

### 1. 当前 QuickStartMod 的发金币实现（OnApplicationTick + MapState）

- 点击“快速开始”按钮时，仅做三件事：
  1. `QuickStartHelper.IsQuickStartMode = true;`
  2. `QuickStartHelper.PendingGold = true;`
  3. `QuickStartHelper.GoldDone = false;`
  4. 找到当前 `InitialMenuVM.MenuOptions` 中的原版“沙盒模式”菜单项，反射调用其 `ExecuteAction()`，完全复用原版开局路径。

- 不再注册任何 `CampaignBehaviorBase`、`CampaignEvents` 监听，真正发金币的逻辑放在 `OnApplicationTick` 中，伪代码如下：

```csharp
protected override void OnApplicationTick(float dt)
{
    base.OnApplicationTick(dt);

    if (!QuickStartHelper.IsQuickStartMode)
        return;

    if (!QuickStartHelper.PendingGold || QuickStartHelper.GoldDone)
        return;

    _goldWaitTime += dt;
    if (_goldWaitTime < 1.5f)
        return; // 给战役一点缓冲时间

    var game = Game.Current;
    if (game == null)
        return;

    if (Campaign.Current == null || Hero.MainHero == null || MobileParty.MainParty == null)
        return;

    var gsm = game.GameStateManager;
    if (gsm == null || !(gsm.ActiveState is MapState))
        return; // 必须已经进入大地图

    try
    {
        int amount = QuickStartHelper.QuickStartGold; // 100000
        GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, amount, true);

        QuickStartHelper.GoldDone = true;
        QuickStartHelper.PendingGold = false;
        QuickStartHelper.IsQuickStartMode = false;
        _goldWaitTime = 0f;

        Debug.Print("[QuickStart] Gold granted safely on MapState: " + amount, 0, Debug.DebugColor.Green);
    }
    catch (Exception ex)
    {
        Debug.Print("[QuickStart] Gold grant FAILED on OnApplicationTick: " + ex, 0, Debug.DebugColor.Red);

        QuickStartHelper.PendingGold = false;
        QuickStartHelper.IsQuickStartMode = false;
        _goldWaitTime = 0f;
    }
}
```

关键点：

- **完全不再使用 `CampaignBehaviorBase` / `CampaignEvents`**；
- 只有在：
  - `Campaign.Current != null`，
  - `Hero.MainHero != null`，
  - `MobileParty.MainParty != null`，
  - `Game.Current.GameStateManager.ActiveState is MapState`  
  的情况下才会真正执行 `GiveGoldAction`；
- 并且只执行一次（`GoldDone` 标志）。

### 2. 最新一次崩溃的目录与 QuickStartMod 日志

- 崩溃目录：`Modules/crashes/2025-12-18_05.11.22/`
- 关键文件：
  - `rgl_log_20672.txt`
  - `rgl_log_errors_20672.txt`
  - `crash_tags.txt`
  - `module_list.txt`

`crash_tags.txt` 再次确认启用模块和版本：

```text
[Runtime][Arguments][/singleplayer _MODULES_*Bannerlord.Harmony*Bannerlord.UIExtenderEx*Native*SandBoxCore*BirthAndDeath*Sandbox*StoryMode*CustomBattle*NavalDLC*QuickStartMod*_MODULES_]
[Runtime][Build Version][v1.3.11.104956]
```

在 `rgl_log_20672.txt` 中，与 QuickStartMod 直接相关的日志非常少：

```text
[11:23:03.694] Loading xml file: $BASE/Modules/QuickStartMod/SubModule.xml.
...
[11:23:03.696] Loading xml file: $BASE/Modules/QuickStartMod/SubModule.xml.
...
[11:23:11.873] [QuickStart] OnSubModuleLoad ENTER
```

之后就进入大量引擎资源加载、NavalDLC 相关 XML/Prefab/粒子等的日志，中间**没有任何 `[QuickStart] Gold granted ...` 或 `[QuickStart] ... FAILED`**，说明：

- 在这次崩溃过程中，`OnApplicationTick` 里的发金币逻辑 **还根本没来得及跑到“条件满足”的分支**（至少没有输出任何我们的 Debug 日志）。

### 3. 崩溃时段周围的引擎日志特征（与上一轮类似）

在最新的 `rgl_log_20672.txt` 中，崩溃时间点附近同样是在加载各类 XML、尤其是 NavalDLC 和资源预设，典型片段类似（截取一段相似的加载日志）：

```text
[11:23:12.466] Loading xml file: $BASE/Modules/SandBox/ModuleData/project.mbproj.
...
[11:23:12.466] Loading xml file: $BASE/Modules/StoryMode/ModuleData/project.mbproj.
...
[11:23:12.466] Loading xml file: $BASE/Modules/NavalDLC/ModuleData/project.mbproj.
...
[11:23:12.467] opening ..\..\Modules\Native/ModuleData/action_sets.xml
...
Unable to find particle system with name light_house_flame
Unable to find particle system with name outdoor_smoke_medium
Unable to find particle system with name outdoor_fire_medium
...
Emitter hierarchy does not match invalid_particle-invalid_particle
...
```

`rgl_log_errors_20672.txt` 仍然只有头几行“Starting new log file...”和时间戳，没有任何托管异常堆栈或明确的 .NET 错误信息，和之前几次崩溃的模式完全一致：**看起来是原生层（C++）在资源加载 / 初始化阶段崩溃**。

### 4. 目前的怀疑与困惑

综合现在几轮尝试，我这边的直觉是：

1. 只要 QuickStartMod 以某种方式参与“新战役启动”的路径（即便只是挂了一个 `OnApplicationTick` 延迟发金币），在当前 `v1.3.11 + NavalDLC` 组合下，就会 **显著增加在加载阶段原生崩溃的概率**；
2. 但是从 `rgl_log` 来看：
   - QuickStartMod 本身没有抛出托管异常；
   - OnApplicationTick 版本在崩溃前甚至还没输出任何“准备发金币”的日志；
   - 崩溃总是在引擎加载 NavalDLC / SandBox / StoryMode 的各种 XML / 粒子 / Prefab 时发生；
3. 这让我很难判断：
   - 是不是 QuickStartMod 只是“触发了某种时序”，放大了 NavalDLC 在这个版本本来就存在的资源加载 bug；
   - 还是说我们在某个生命周期（比如主菜单 VM / SubModuleLoad / UIExtenderEx 激活）上做了什么，间接破坏了引擎内部的一些假设，最终在深处崩溃。

### 5. 想请 ChatGPT 帮忙的进一步问题（在 OnApplicationTick 方案的基础上）

1. 在你对 Bannerlord（特别是 v1.3.11 + NavalDLC）的了解里，**有没有已知的“只在加载阶段、没有托管异常堆栈”的原生崩溃模式**，尤其是与：
   - 粒子系统缺失（大量 `Unable to find particle system...`、`Emitter hierarchy does not match invalid_particle-invalid_particle`）；
   - NavalDLC 的 `project.mbproj` / Prefabs 加载  
   相关的？
2. 像现在这样，QuickStartMod 只在：
   - 主菜单 VM 上做 Harmony Postfix 注入一个按钮（复用原版沙盒按钮的 `ExecuteAction`）；
   - `MBSubModuleBase.OnApplicationTick` 里，在进入 `MapState` 后**一次性发金币**；  
   从生命周期/引擎调用栈的角度看，这种玩法 **理论上还会有什么潜在踩雷点** 吗？比如：
   - OnApplicationTick 调用 `GiveGoldAction.ApplyBetweenCharacters` 是否有已知的线程/时序风险？
   - Mod 的 SubModule 静态构造里写日志 / 调用 `Debug.Print`，是否有可能参与某些早期初始化的竞态？
3. 现在 `.dmp` 文件在崩溃目录里也有（例如 `dump.dmp`），**有没有推荐的分析流程**，能大概看出：
   - 崩溃时的 C++ 调用栈在做什么（比如在加载哪个 XML、哪个资源）？
   - 是否有模块名（NavalDLC / SandBox / QuickStartMod）相关的符号能帮助定位？
4. 站在一个“保守实用”的角度，你会建议：
   - 干脆放弃在“战役启动后自动发金币”，改成玩家进游戏后按一个快捷键触发（例如监听 `OnApplicationTick` + 键盘输入），以完全绕开战役初始化路径？
   - 还是有更“官方”的、经过验证的 hook 点（比如某些 `CampaignEvents` / `GameState` 切换）更适合作为发金币等经济修改的时机，在 v1.3.11 + NavalDLC 下相对安全？

我现在最大的困惑是：**从托管层看不到任何明显错误，但只要 QuickStartMod 参与战役启动流程，崩溃概率就会明显升高**。希望能从你的经验里拿到一些关于：

- “哪些生命周期点动东西是绝对安全的”，
- “哪些是已知高危”的经验规则，

以及是否有办法用 `.dmp` 更精细地确认崩溃到底是在谁的代码里发生的。
## 新的发金币实现仍然导致原生崩溃（2025-12-18 03:31:29）

这次我尝试把“给主角 100000 第纳尔”的逻辑从早期事件挪到一个 `CampaignBehaviorBase` 里，并在 Tick 中延迟执行，但只要在战役启动时注册这个 `Behavior`，点击“沙盒模式”依旧会在加载阶段 native 崩溃。

### 环境 & 模块

- 游戏版本：Bannerlord v1.3.11.104956（Steam）
- 启用模块顺序：  
  `Bannerlord.Harmony` → `Bannerlord.UIExtenderEx` → `Native` → `SandBoxCore` → `BirthAndDeath` → `Sandbox` → `StoryMode` → `CustomBattle` → `NavalDLC` → `QuickStartMod`
- QuickStartMod 目前已经确认：
  - 主菜单“快速开始”按钮通过 Harmony 注入 `InitialMenuVM`，点击时**不会再直接调用 `Game.StartNewGame(false)`**，而是：
    1. `QuickStartHelper.IsQuickStartMode = true;`
    2. 在 `vm.MenuOptions` 里找到原版“沙盒模式”的 `InitialMenuOptionVM sandbox`；
    3. 调用 `sandbox.ExecuteAction()`，完全复用原版沙盒开局路径。
  - 在**完全移除任何“发金币逻辑”时**，启用 QuickStartMod 点“快速开始”或点原版“沙盒模式”都能稳定进入战役。

### 第一版发金币逻辑（已确认会导致崩溃）

最开始我在 `QuickStartSubModule` 的 `OnGameStart` + `OnSessionLaunched` 里直接发金币：

```csharp
protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
{
    base.OnGameStart(game, gameStarterObject);

    // 只处理战役（沙盒/剧情）
    if (game == null || !(game.GameType is Campaign))
        return;

    TaleWorlds.Library.Debug.Print("[QuickStart] OnGameStart Campaign", 0, TaleWorlds.Library.Debug.DebugColor.Green);

    var starter = (CampaignGameStarter)gameStarterObject;

    CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>((gameStarter) =>
    {
        if (!QuickStartHelper.IsQuickStartMode)
            return;

        try
        {
            var hero = Hero.MainHero;
            if (hero != null)
            {
                hero.ChangeHeroGold(QuickStartHelper.QuickStartGold); // 或 GiveGoldAction.ApplyBetweenCharacters(...)
                _hasGivenQuickStartGold = true;
                QuickStartHelper.IsQuickStartMode = false;
            }
        }
        catch (Exception ex)
        {
            TaleWorlds.Library.Debug.Print($"[QuickStart] Grant gold FAILED: {ex}", 0, TaleWorlds.Library.Debug.DebugColor.Red);
        }
    }));
}
```

现象：

- 只要这段逻辑存在，**点击“沙盒模式”（无论是原版按钮还是“快速开始”代点）都会在“Initializing new game / 加载 XML 资源”阶段原生崩溃**；
- 一旦把整个 `OnGameStart` 注释掉，不再注册 `OnSessionLaunched` 监听，同样模块组合下就能稳定进家族选择与战役。

### 第二版：QuickStartGoldBehavior（Behavior + Tick 延迟），仍然崩溃

为了更安全，我改成在战役里挂一个 `CampaignBehaviorBase`，只在 Tick 阶段、战役完全跑起来后再发金币：

```csharp
// SubModule：只负责在战役游戏里注册行为
protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
{
    base.OnGameStart(game, gameStarterObject);

    if (game != null && game.GameType is Campaign && gameStarterObject is CampaignGameStarter campaignStarter)
    {
        campaignStarter.AddBehavior(new QuickStartGoldBehavior());
        Debug.Print("[QuickStart] OnGameStart: QuickStartGoldBehavior added", 0, Debug.DebugColor.Green);
    }
}
``>

```csharp
public class QuickStartGoldBehavior : CampaignBehaviorBase
{
    private bool _pending;
    private bool _done;
    private float _elapsed;

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnSessionLaunched));
        CampaignEvents.TickEvent.AddNonSerializedListener(this, new Action<float>(OnTick));
    }

    public override void SyncData(IDataStore dataStore)
    {
        // 一个战役只发一次
        dataStore.SyncData("qs_gold_done", ref _done);
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        if (_done) return;
        if (!QuickStartHelper.IsQuickStartMode) return;

        _pending = true;
        _elapsed = 0f;
        Debug.Print("[QuickStart] OnSessionLaunched -> pending gold grant", 0, Debug.DebugColor.Yellow);
    }

    private void OnTick(float dt)
    {
        if (!_pending || _done) return;
        if (!QuickStartHelper.IsQuickStartMode)
        {
            _pending = false;
            return;
        }

        _elapsed += dt;
        if (_elapsed < 1.0f) return; // 给战役一点初始化缓冲

        if (Hero.MainHero == null || MobileParty.MainParty == null)
            return;

        try
        {
            int amount = QuickStartHelper.QuickStartGold; // 100000
            GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, amount, true);

            _done = true;
            _pending = false;
            QuickStartHelper.IsQuickStartMode = false;

            Debug.Print($"[QuickStart] Gold granted via Behavior: {amount}", 0, Debug.DebugColor.Green);
        }
        catch (Exception ex)
        {
            Debug.Print($"[QuickStart] GoldBehavior FAILED: {ex}", 0, Debug.DebugColor.Red);
            _pending = false;
        }
    }
}
```

结果：

- 只要在 `OnGameStart` 里执行 `campaignStarter.AddBehavior(new QuickStartGoldBehavior())`，再次点击“沙盒模式”就会在加载阶段 native 崩溃；
- 最新一次崩溃目录：`Modules/crashes/2025-12-18_03.31.29/`，关键日志文件：`rgl_log_31624.txt`；
- 日志中关于 QuickStart 的输出只有：

```text
[11:31:15.184] [QuickStart] 静态构造函数执行！类已加载！
...
[11:31:25.630] [QuickStart] Postfix hit: .ctor, MenuOptions.Count=0
[11:31:25.630] [QuickStart] EnsureQuickStart: vm 或 MenuOptions 为 null
...（上面两行重复几次）...
[11:31:25.630] AddGlobalLayer
[11:31:25.634] [QuickStart] OnGameStart Campaign
```

- **完全没有** `[QuickStart] OnSessionLaunched -> pending gold grant` 或 `GoldBehavior FAILED` 的日志，说明 Behavior 的事件监听根本还没真正触发；
- `rgl_log_errors_31624.txt` 里依然只有 4 行头信息，没有任何托管异常栈；
- 崩溃依旧发生在：

```text
Initializing new game begin...
loading managed_core_parameters.xml
loading managed_campaign_parameters.xml
opening ..\..\Modules\Native/ModuleData/global_strings.xml
...
opening ..\..\Modules\SandBox/ModuleData/conversation_scenes.xml
...
opening ..\..\Modules\NavalDLC/ModuleData/naval_skill_sets.xml
...
opening ..\..\Modules\SandBoxCore/ModuleData/items/arm_armors.xml
...
（随后直接原生崩溃，未再继续）
```

也就是说：**仅仅是在战役启动时注册了一个自定义 `CampaignBehaviorBase`，在它真正执行任何逻辑之前，引擎就在加载 XML/资源阶段原生崩溃了。**

### 对照实验总结

1. 关闭 QuickStartMod → 点原版“沙盒模式”稳定进入战役；  
2. 启用 QuickStartMod，但**只保留按钮注入（不发金币、不注册 Behavior）** → 点快速开始（代点沙盒）和点原版“沙盒模式”都稳定；  
3. 只要在 `OnGameStart` 里：
   - 要么注册 `OnSessionLaunched` 然后在里面直接改金币；  
   - 要么注册 `QuickStartGoldBehavior` 这种 Behavior（哪怕 Behavior 里真正的逻辑几乎还没跑）；  
   就会在“Initializing new game / 加载 XML、NavalDLC 资源”阶段 native 崩溃。

### 想请 ChatGPT 帮忙的问题（第二批）

1. 在 Bannerlord 1.3.11 + NavalDLC 的环境下，从主菜单点击“沙盒模式”开始新战役时：  
   - **有哪些官方或社区推荐的“安全时机/事件”可以修改 `Hero.MainHero` 的金币？**  
   - `OnGameStart` / `OnSessionLaunchedEvent` / 自定义 `CampaignBehaviorBase` + `TickEvent` 中，哪一个理论上是“战役已经完全初始化、可以安全动经济系统”的？
2. 目前这种情况（仅仅调用一次 `campaignStarter.AddBehavior(new QuickStartGoldBehavior())` 就让这条链路变得不稳定），是否有已知原因？比如：  
   - 某些版本或 NavalDLC 的战役流程在 New Game 期间会立刻遍历所有 `CampaignBehaviorBase`，如果行为内部缺少某些初始化步骤/接口实现，会在 native 侧崩溃？  
   - 需要实现 `OnNewGameCreated` / `OnGameLoaded` 等方法做额外初始化，否则行为在某些序列化/反序列化路径上会出问题？
3. 有没有一个更“官方”的方式：  
   - **只在通过自定义入口（QuickStart 按钮）新建的战役里，给主角一次性加钱**，而不影响：
     - 主菜单原版“沙盒模式”；
     - 读档进入已有战役；
     - NavalDLC 自带脚本和战役逻辑。
4. 如果当前版本中，在 `OnGameStart` 里直接 `AddBehavior` 就有潜在坑，是否有推荐替代方案，比如：  
   - 使用别的全局事件（例如 `CampaignEvents.AfterNewGameCreatedEvent`，如果存在的话）；  
   - 或者完全不挂 Behavior，而是在某个更靠后的生命周期事件中，单次触发发金币逻辑？

总之，现在已经可以确认：**按钮注入 + 复用原版沙盒开局路径本身是安全的，真正引发 native 崩溃的是“在战役初始化路径上挂了某种发金币逻辑/行为”，哪怕它尚未真正执行。**  
希望能拿到一条在 1.3.11 + NavalDLC 下“既安全又简洁”的推荐写法，用于：

- 仅在 QuickStart 模式下、战役成功启动后，给主角发 100000 第纳尔；
- 且不破坏原版沙盒/剧情战役和 NavalDLC 的稳定性。