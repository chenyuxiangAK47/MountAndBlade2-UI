# dnSpy 查找角色创建相关类型指南

## 步骤 1：打开游戏 DLL

在 dnSpy 中：
1. **文件 → 打开**（或按 `Ctrl+O`）
2. 导航到：
   ```
   D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\
   ```
3. 打开以下 DLL：
   - ✅ `TaleWorlds.CampaignSystem.dll`（角色创建逻辑）
   - ✅ `TaleWorlds.MountAndBlade.GauntletUI.dll`（UI ViewModel）

## 步骤 2：搜索角色创建相关类型

### 方法 A：使用搜索功能
1. 在 dnSpy 中按 `Ctrl+Shift+K` 打开搜索窗口
2. 搜索关键词：
   - `CharacterCreation`
   - `CharacterCreationVM`
   - `CharacterCreationState`
   - `CultureSelection`
   - `BackgroundSelection`

### 方法 B：手动浏览
在左侧“程序集资源管理器”中：
1. 展开 `TaleWorlds.CampaignSystem`
2. 展开 `TaleWorlds.CampaignSystem.GameState`
3. 查找包含 `CharacterCreation` 的类型

## 步骤 3：查看关键类型（重点查找）

### 1. CharacterCreationState 或类似
- 查看是否有 `ViewModel` 或 `Data` 属性
- 记录类型全名

### 2. CharacterCreationVM 或类似 ViewModel
查找以下内容：

#### 选项集合属性：
- `Options` / `CultureOptions` / `BackgroundOptions`
- 类型：`IEnumerable<T>` 或 `MBBindingList<T>`

#### 选择方法：
- `SelectOption(object option)`
- `SelectCulture(CultureObject culture)`
- `OnOptionSelected(...)`

#### 前进方法：
- `ExecuteNext()`
- `ExecuteContinue()`
- `OnNext()`
- `Finalize()`

#### 命令属性：
- `NextCommand` (类型：`ICommand` 或 `Action`)
- `ContinueCommand`
- `DoneCommand`

#### 状态属性：
- `CurrentStage` / `CurrentPage`
- `CanAdvance` / `IsNextEnabled`
- `Title` / `CurrentTitle`

## 步骤 4：记录信息

找到后，记录以下信息：

```
类型全名：TaleWorlds.CampaignSystem.CharacterCreation.CharacterCreationVM
选项属性：CultureOptions (类型：MBBindingList<CultureOptionVM>)
选择方法：SelectCulture(CultureObject culture)
前进方法：ExecuteNext()
命令属性：NextCommand (类型：ICommand)
```

## 步骤 5：查看选项类型

找到选项集合后，查看选项对象的类型：
- `CultureOptionVM`
- `BackgroundOptionVM`
- 查看选项对象有哪些属性：
  - `IsSelected`
  - `Culture` / `Background`
  - `Name` / `Text`

## 常见位置

根据 Bannerlord 的代码结构，角色创建相关类型通常在：

1. **TaleWorlds.CampaignSystem.GameState**
   - `CharacterCreationState`
   - `CharacterCreationContentState`

2. **TaleWorlds.CampaignSystem.CharacterCreation**
   - `CharacterCreationVM`
   - `CharacterCreationStageVM`
   - `CultureSelectionVM`
   - `BackgroundSelectionVM`

3. **TaleWorlds.MountAndBlade.GauntletUI.CharacterCreation**
   - UI 相关的 ViewModel

## 如果找不到

如果搜索不到，尝试：
1. 查看 `SandBox` 模块的 DLL
2. 查看 `StoryMode` 模块的 DLL（如果安装了）
3. 在 dnSpy 中搜索所有已加载的程序集


