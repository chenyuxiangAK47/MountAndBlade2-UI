# dnSpy 手动搜索角色创建类型 - 详细步骤

## 步骤 1：打开 Bannerlord DLL

1. 打开 **dnSpy**
2. 点击菜单：**文件 → 打开**（或按 `Ctrl+O`）
3. 导航到：
   ```
   D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\
   ```
4. **先打开这个 DLL**：
   - ✅ `TaleWorlds.CampaignSystem.dll`

## 步骤 2：使用搜索功能

### 方法 A：全局搜索（推荐）

1. 在 dnSpy 中按 **`Ctrl+Shift+K`** 打开搜索窗口
2. 在搜索框中输入：`CharacterCreation`
3. 选择搜索范围：**所有已加载的程序集**
4. 点击 **搜索** 或按 `Enter`

### 方法 B：在程序集中搜索

1. 在左侧 **程序集资源管理器** 中，展开 `TaleWorlds.CampaignSystem`
2. 右键点击 `TaleWorlds.CampaignSystem` → **搜索**
3. 输入：`CharacterCreation`
4. 点击搜索

## 步骤 3：查看搜索结果

搜索结果会显示所有包含 `CharacterCreation` 的类型。**重点关注以下类型**：

### 最可能找到的类型：

1. **`CharacterCreationState`** 或类似
   - 位置：可能在 `TaleWorlds.CampaignSystem.GameState` 命名空间下
   - 这是游戏状态类

2. **`CharacterCreationVM`** 或类似
   - 位置：可能在 `TaleWorlds.CampaignSystem.CharacterCreation` 命名空间下
   - 这是 ViewModel 类

3. **`CultureSelectionVM`** 或类似
   - 文化选择页面的 ViewModel

4. **`BackgroundSelectionVM`** 或类似
   - 背景选择页面的 ViewModel

## 步骤 4：双击打开类型，查看详细信息

找到类型后，**双击打开**，然后查看：

### 查看属性（Properties）

在右侧代码窗口中，查找以下属性：

#### 选项集合属性：
- `Options` / `CultureOptions` / `BackgroundOptions`
- 类型应该是：`MBBindingList<T>` 或 `IEnumerable<T>`

#### 命令属性：
- `NextCommand` / `ContinueCommand` / `DoneCommand`
- 类型应该是：`ICommand` 或 `Action`

#### 状态属性：
- `CurrentStage` / `CurrentPage`
- `CanAdvance` / `IsNextEnabled`
- `Title` / `CurrentTitle`

### 查看方法（Methods）

查找以下方法：

#### 选择方法：
- `SelectOption(...)`
- `SelectCulture(...)`
- `OnOptionSelected(...)`

#### 前进方法：
- `ExecuteNext()`
- `ExecuteContinue()`
- `OnNext()`
- `Finalize()`
- `Done()`

## 步骤 5：记录信息

找到后，**把以下信息记录下来**（可以截图或复制文本）：

```
类型全名：TaleWorlds.CampaignSystem.CharacterCreation.CharacterCreationVM

属性：
- CultureOptions: MBBindingList<CultureOptionVM>
- NextCommand: ICommand
- CanAdvance: bool
- CurrentStage: int

方法：
- SelectCulture(CultureObject culture): void
- ExecuteNext(): void
- OnNext(): void
```

## 步骤 6：查看选项类型

找到选项集合后，查看选项对象的类型（例如 `CultureOptionVM`），查看它有哪些属性：

- `IsSelected: bool`
- `Culture: CultureObject`
- `Name: string` / `Text: string`

## 如果找不到

如果搜索 `CharacterCreation` 找不到，尝试搜索：

1. `CultureSelection` - 文化选择
2. `BackgroundSelection` - 背景选择
3. `ChildhoodSelection` - 童年选择
4. `YouthSelection` - 青年选择
5. `CharacterCreationStage` - 角色创建阶段

## 快速操作提示

- **搜索**：`Ctrl+Shift+K`
- **转到定义**：`F12`
- **查找引用**：`Shift+F12`
- **展开所有**：在类型上右键 → **展开所有**

## 完成后

把找到的类型、属性、方法信息发给我，我会据此更新代码！


