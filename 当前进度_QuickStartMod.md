## QuickStartMod 当前进度（快速开始 & 金币发放）

### 1. 当前功能状态

- **QuickStart 按钮**
  - 已通过 Harmony 在 `InitialMenuVM` 的菜单中成功注入“快速开始”按钮。
  - 点击“快速开始”：
    - 不再直接调用 `Game.StartNewGame(false)`。
    - 改为在菜单项列表中查找原版“沙盒模式”按钮（`InitialMenuOptionVM`），并通过反射调用其 `ExecuteAction()`，**完全复用原版沙盒开局路径**。
  - 如果未找到沙盒按钮，会安全地提示并放弃启动，不再尝试走“野路子”的开局流程。

- **金币发放逻辑**
  - 点击“快速开始”按钮时，只做**打标记**：
    - `QuickStartHelper.IsQuickStartMode = true;`
    - `QuickStartHelper.PendingGold = true;`
    - `QuickStartHelper.GoldDone = false;`
  - 真正发放金币移动到了 `QuickStartSubModule.OnApplicationTick(float dt)`：
    - 每帧轮询当前游戏状态，只在满足以下条件时执行：
      - `QuickStartHelper.IsQuickStartMode == true`
      - `QuickStartHelper.PendingGold == true`
      - `QuickStartHelper.GoldDone == false`
      - `Game.Current?.GameStateManager?.ActiveState` 的类型名为 `"MapState"`（已经进入大地图）
      - 延迟时间 `_goldWaitTime >= 0.5f`，避免刚切换状态就动经济系统
      - 能通过反射获取 `Hero.MainHero`，并成功找到其实例方法 `ChangeHeroGold(int)`
    - 满足条件后调用 `Hero.MainHero.ChangeHeroGold(QuickStartHelper.QuickStartGold)` 发放金币（当前为 **100000 第纳尔**）。
    - 成功或失败后都会：
      - 设置 `QuickStartHelper.GoldDone` / `QuickStartHelper.PendingGold` / `QuickStartHelper.IsQuickStartMode` 为结束状态；
      - 将 `_goldWaitTime` 清零，确保本战役只发一次，不影响后续正常游戏。
  - **原版沙盒按钮**在 `IsQuickStartMode == false` 时完全不受影响，点击只会按照原版逻辑开局，不会发钱。

### 2. 已经解决的问题

- **资源加载阶段原生崩溃**
  - 之前在加载 XML / Prefab / NavalDLC 资源时频繁出现原生 C++ 崩溃，Managed 日志没有堆栈。
  - 通过：
    - 去掉直接调用 `Game.StartNewGame(false)` 的做法；
    - 去掉在 `OnGameStart` / `CampaignBehavior` / `CampaignEvents` 里过早发钱的逻辑；
    - 改为**复用原版沙盒按钮 + 通过 `OnApplicationTick` 延迟到 MapState 后再发钱**；
  - 现在已验证：启用 QuickStartMod 时，无论是点原版“沙盒模式”还是点“快速开始”，都可以稳定进入战役，不再在加载资源阶段崩溃。

- **兼容旧版 MSBuild 的编译问题**
  - 将所有 C# 6+ 语法（如 `$""` 字符串插值）改为 C# 4 兼容写法（`string.Format` 或 `+` 拼接）。
  - 调整 `.csproj` 中的 `HintPath` 为相对路径，指向本地 `bin\Win64_Shipping_Client\TaleWorlds.*.dll` 和 Steam 工坊的 `0Harmony.dll` / `Bannerlord.UIExtenderEx.dll`。
  - 在本机安装 VS 2022 Build Tools 后，使用：
    - `msbuild QuickStartMod.csproj /p:Configuration=Release`
    - 成功生成 `bin\Win64_Shipping_Client\QuickStartMod.dll` 并在游戏中通过测试。

### 3. 仍待解决 / 规划中的功能

- **跳过角色创建问卷**
  - 目标：在“快速开始”路径下，自动选择：
    - 默认文化（计划为 **瓦兰迪亚**）；
    - 所有出身 / 童年 / 青年等选项统一选择**第一个**，或一套固定模板；
  - 当前状态：
    - 仍然完全走原版角色创建 UI，玩家需要手动点击；
    - 为了避免再次干扰战役初始化，目前还没有对角色创建 `State` / `ViewModel` 做 Harmony Patch。
  - 计划方向（待选）：
    - A. 保持 UI 流程不变，只在进入大地图后，把 `Hero.MainHero` 的文化/属性/技能等重置为预设模板（对稳定性最安全）。
    - B. 研究并 Patch 角色创建流程，在 VM 层自动选择第一个选项并连按“下一步”，真正跳过问卷（但风险更高，需要进一步反编译分析）。

### 4. 推送到 GitHub 的建议步骤（在本机执行）

> 假设 GitHub 仓库为 `MountAndBlade2-UI`，本地已有该仓库的克隆。

1. **拷贝模块源码到仓库**
   - 在文件管理器中，将整个 `Modules\QuickStartMod` 目录复制到本地 `MountAndBlade2-UI` 仓库下合适的位置（例如 `Mods/QuickStartMod` 子目录）。
2. **在仓库目录中执行 Git 命令**

```bash
cd "<本地 MountAndBlade2-UI 仓库路径>"
git add .
git commit -m "Add QuickStartMod (quick-start menu, safe gold grant, crash fix)"
git push origin main   # 或 master，视仓库默认分支而定
```

3. **在 GitHub 仓库 README 或相关文档中添加链接**
   - 简要说明：
     - QuickStartMod 的功能（快速开始 + 启动资金 100000）；
     - 关键技术点：Harmony Patch 主菜单、复用原版沙盒按钮、通过 `OnApplicationTick` 延迟金币发放；
     - 当前仍在开发中的“跳过角色创建问卷”计划。


