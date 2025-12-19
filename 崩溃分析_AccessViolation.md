# 崩溃分析：Access Violation (0xC0000005)

## 崩溃信息

- **崩溃类型**：`ExceptionCode: 0xC0000005` (Access Violation - 访问违规)
- **崩溃地址**：`0x7ffc4dba24f7` (系统 DLL 地址)
- **崩溃时机**：进入世界地图后立即崩溃
- **崩溃日志**：`crashes/logs/rgl_log_13284.txt`

## 关键发现

### 1. Harmony Patch 未应用

从日志看到：
```
[QuickStart] 跳过 Harmony PatchAll（避免TargetMethod 返回 null 导致异常）
```

这说明：
- **Harmony PatchAll 被跳过了**（可能是旧版本代码）
- **CharacterCreationStateTickPatch 没有应用**
- **角色创建自动跳过功能没有工作**

### 2. 游戏流程

从日志时间线：
1. `[13:11:15.285]` - 按钮被点击
2. `[13:11:19.462]` - `OnGameStart Campaign`（游戏开始）
3. 之后进入世界地图
4. **立即崩溃**（Access Violation）

### 3. 可能的原因

#### A. 角色创建未完成
- 由于 Harmony Patch 未应用，角色创建可能没有正确完成
- 进入世界地图时，某些角色数据可能未初始化
- 导致访问空指针

#### B. 金币发放逻辑问题
- `OnApplicationTick` 中的金币发放逻辑可能在 `MapState` 时访问了未初始化的对象
- `Hero.MainHero` 可能为 null 或未完全初始化

#### C. 状态机问题
- 从 `CharacterCreationState` 到 `MapState` 的转换可能不完整
- 某些游戏状态对象可能未正确初始化

## 解决方案

### 1. 立即修复：确保 Harmony Patch 应用

**问题**：代码中可能还有"跳过 Harmony PatchAll"的逻辑

**修复**：
- 检查 `QuickStartSubModule.cs` 中是否还有跳过逻辑
- 确保 `Harmony.PatchAll()` 总是被调用
- 添加更详细的日志来验证 Patch 是否成功

### 2. 增强金币发放的安全性

**问题**：`OnApplicationTick` 中的金币发放可能在对象未初始化时执行

**修复**：
```csharp
// 在访问 Hero.MainHero 之前，增加更多检查
if (heroType == null || heroObj == null)
{
    // 增加延迟，等待对象初始化
    _goldWaitTime = 0f; // 重置等待时间
    return;
}

// 检查 MainHero 是否真的可用
var isValid = heroType.GetProperty("IsValid", ...)?.GetValue(heroObj, null);
if (isValid == null || !(bool)isValid)
{
    return; // 继续等待
}
```

### 3. 延迟金币发放

**问题**：进入 MapState 后立即发放金币可能太早

**修复**：
- 增加等待时间从 `0.5f` 到 `2.0f` 秒
- 在发放前检查更多游戏状态

### 4. 添加异常捕获

**问题**：崩溃时没有足够的错误信息

**修复**：
- 在 `OnApplicationTick` 的金币发放逻辑中添加 try-catch
- 记录详细的错误信息到日志

## 下一步行动

1. **重新编译**：确保使用最新代码（没有"跳过 Harmony PatchAll"的逻辑）
2. **增强日志**：添加更多调试信息
3. **测试**：重新测试，查看是否还有崩溃
4. **如果仍然崩溃**：查看新的崩溃日志，定位具体是哪个对象访问失败

## 相关文件

- `QuickStartMod/SubModule/QuickStartSubModule.cs` - 主模块加载
- `QuickStartMod/SubModule/QuickStartPatches.cs` - Harmony Patch
- `QuickStartMod/SubModule/QuickStartCharCreationSkipper.cs` - 角色创建跳过逻辑

