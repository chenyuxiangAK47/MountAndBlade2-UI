# QuickStartMod 问题 - 给 ChatGPT

## 问题描述

我正在开发一个 Mount & Blade II: Bannerlord Mod（QuickStartMod），用于在主菜单添加"快速开始"按钮。遇到了一个奇怪的问题：

**现象**：
- ✅ DLL 文件已成功编译并存在
- ✅ 游戏日志显示 DLL 已被加载：`Loading assembly: ..\..\Modules\QuickStartMod\bin\Win64_Shipping_Client\QuickStartMod.dll`
- ✅ 游戏日志显示 `Loading submodules...`
- ❌ 但是静态构造函数和 `OnSubModuleLoad` 都没有执行（没有日志文件）
- ❌ 主菜单中没有显示"快速开始"按钮

**关键发现**：
使用 PowerShell 尝试反射加载 DLL 时出现异常：
```powershell
[System.Reflection.Assembly]::LoadFrom("QuickStartMod.dll").GetTypes()
# 错误：ReflectionTypeLoadException: 无法加载一个或多个请求的类型
```

## 技术栈

- **游戏版本**：Mount & Blade II: Bannerlord v1.3.11.104956
- **.NET Framework**：4.8
- **编译工具**：MSBuild 16.11.6
- **依赖框架**：
  - Harmony（从 Steam Workshop 安装）
  - UIExtenderEx（从 Steam Workshop 安装）

## 代码结构

### SubModule.xml
```xml
<SubModule>
    <Name value="QuickStartMod" />
    <DLLName value="QuickStartMod.dll" />
    <SubModuleClassType value="QuickStartMod.QuickStartSubModule" />
    <Tags>
        <Tag key="DedicatedServerType" value="none" />
    </Tags>
</SubModule>
```

### QuickStartSubModule.cs（关键部分）
```csharp
namespace QuickStartMod
{
    public class QuickStartSubModule : MBSubModuleBase
    {
        // 静态构造函数：在类加载时立即执行
        static QuickStartSubModule()
        {
            var tempLog = Path.Combine(Path.GetTempPath(), "QuickStartMod_Static.log");
            File.AppendAllText(tempLog, $"[{DateTime.Now}] 静态构造函数执行！\n");
        }

        protected override void OnSubModuleLoad()
        {
            var tempLog = Path.Combine(Path.GetTempPath(), "QuickStartMod_OnLoad.log");
            File.AppendAllText(tempLog, $"[{DateTime.Now}] OnSubModuleLoad 开始执行！\n");
            
            // UIExtenderEx 初始化
            _uiExtender = UIExtender.Create("QuickStartMod");
            _uiExtender.Register(typeof(QuickStartSubModule).Assembly);
            _uiExtender.Enable();
        }
    }
}
```

### 项目文件引用
```xml
<Reference Include="0Harmony">
    <HintPath>D:\SteamLibrary\steamapps\workshop\content\261550\2859188632\bin\Win64_Shipping_Client\0Harmony.dll</HintPath>
    <Private>False</Private>
</Reference>
<Reference Include="Bannerlord.UIExtenderEx">
    <HintPath>D:\SteamLibrary\steamapps\workshop\content\261550\2859222409\bin\Win64_Shipping_Client\Bannerlord.UIExtenderEx.dll</HintPath>
    <Private>False</Private>
</Reference>
```

## 问题分析（根据 ChatGPT 反馈更新）

### 已确认的事实
1. DLL 文件存在且是最新编译的
2. 游戏日志显示 DLL 已被加载：`Loading assembly: ..\..\Modules\QuickStartMod\bin\Win64_Shipping_Client\QuickStartMod.dll`
3. 游戏日志显示 `Loading submodules...`
4. **但是静态构造函数和 OnSubModuleLoad 都没有执行**
5. **UIExtenderEx 弹窗显示 "CRITICAL ERROR"**（用户反馈）

### 关键线索（ChatGPT 分析）
**ChatGPT 指出**：UIExtenderEx 的 "CRITICAL ERROR" 弹窗**不是在说"你没把 UIExtenderEx 勾上/排序不对"**，它真正的含义是：

> **UIExtenderEx 在启动过程中捕获到了"致命异常"**（通常发生在 *扫描你的程序集 / 应用你的 PrefabExtension 或 ViewModelMixin* 的时候），所以它建议立刻退出。

**为什么会跟现象完美吻合**：
- DLL 显示 Loaded，但 SubModule/静态构造/OnSubModuleLoad 都没日志、按钮也不出现
- **结论：QuickStartMod 很可能"类型加载失败"了（TypeLoad / MissingMethod / MissingAssembly）**

**Bannerlord 的加载机制**：
- Bannerlord 会先把 DLL load 进来（所以日志里看到 `Loading assembly ... QuickStartMod.dll`）
- **但当它准备实例化 `SubModuleClassType` 指向的那个类时**，如果该类型在加载阶段就炸了（比如引用了一个不存在/版本不匹配的 UIExtenderEx API），它就会 **实例化失败**：
  - 失败了 → **静态构造不会执行**
  - 失败了 → **OnSubModuleLoad 不会被调用**
  - UIExtenderEx 可能在扫描/注册时也会爆 → 给你弹 **CRITICAL ERROR**

**反射加载测试失败**：使用 PowerShell 尝试反射加载 DLL 时出现 `ReflectionTypeLoadException`，说明：
- DLL 有依赖问题
- 某些类型无法加载
- 这可能导致类无法被实例化

### 可能的原因（ChatGPT 分析）

**ChatGPT 指出两个高危点**：

#### 高危点 A：ViewModelMixin 目标写法可能不对
- 当前代码：`[ViewModelMixin("InitialMenuVM")]`
- 很多版本/用法要求 **完整类型名**（至少 namespace）
- ✅ 更稳的写法：`[ViewModelMixin("TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenuVM")]`
- 如果目标类型字符串不匹配，UIExtenderEx 在扫描/注入时可能直接报致命错误

#### 高危点 B：Mixin 泛型不匹配
- 当前代码：`BaseViewModelMixin<ViewModel>`（使用基类）
- 应该使用：`BaseViewModelMixin<InitialMenuVM>`（使用具体类型）
- 如果真实代码里 mixin 泛型不是 `InitialMenuVM`，而是 `ViewModel`，有些版本会直接炸

#### 其他可能原因
1. **类型加载阶段就失败**：在 SubModule 类里声明了字段/using/UIExtender 类型，CLR 在加载 SubModule 类型时就要解析 UIExtenderEx 程序集和方法签名——版本不匹配就直接 TypeLoadException
2. **依赖加载顺序问题**：Harmony 和 UIExtenderEx 可能需要在 QuickStartMod 之前加载，但游戏可能没有按正确顺序加载
3. **依赖路径问题**：编译时引用的 DLL 路径（Steam Workshop）可能与运行时解析路径不同
4. **类无法被实例化**：因为依赖问题，`QuickStartSubModule` 类可能无法被加载，导致静态构造函数和 OnSubModuleLoad 都没有执行

## 已按照 ChatGPT 建议做的修改

### Step 1：改用 Debug.Print（已完成）
- ✅ 将日志改为 `TaleWorlds.Library.Debug.Print`，确保能在 rgl_log 中看到
- ✅ 静态构造函数和 OnSubModuleLoad 都使用 Debug.Print

### Step 2：移除 UIExtenderEx 强类型引用（已完成）
- ✅ 暂时移除 `using Bannerlord.UIExtenderEx;`
- ✅ 将 `private UIExtender _uiExtender` 改为 `private object _uiExtender`
- ✅ 暂时不初始化 UIExtenderEx，先确认 SubModule 能跑起来
- ✅ 使用反射检查 UIExtenderEx 是否已加载

### Step 3：修正 ViewModelMixin（已完成）
- ✅ 使用完整类型名：`[ViewModelMixin("TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenuVM")]`
- ✅ 修正泛型：`BaseViewModelMixin<InitialMenuVM>`（而不是 `BaseViewModelMixin<ViewModel>`）
- ✅ 添加 `using TaleWorlds.MountAndBlade.ViewModelCollection;`

## 需要帮助的问题

1. **为什么 DLL 加载了，但类没有被实例化？**
   - Bannerlord 是如何加载 SubModule 类的？
   - 如果依赖有问题，游戏会如何处理？会静默失败吗？

2. **如何解决 ReflectionTypeLoadException？**
   - 是否需要将 Harmony 和 UIExtenderEx 的 DLL 复制到 QuickStartMod 的 bin 目录？
   - 或者是否需要修改项目配置，将依赖 DLL 设置为 `Private=True`？

3. **依赖加载顺序问题**
   - Bannerlord 如何确保依赖模块在需要它们的模块之前加载？
   - SubModule.xml 中的 `<DependedModules>` 是否足够？

4. **如何调试这个问题？**
   - 是否有其他方式可以确认 SubModule 类是否被正确实例化？
   - 如何查看 LoaderExceptions 的详细信息？

5. **UIExtenderEx CRITICAL ERROR 的真正原因**
   - 如何在 rgl_log 中找到 UIExtenderEx 的异常栈？
   - 搜索关键词应该是什么？

## 文件位置

- **DLL**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\bin\Win64_Shipping_Client\QuickStartMod.dll`
- **SubModule.xml**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\SubModule.xml`
- **源代码**：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod\SubModule\`

## 依赖模块位置

- **Harmony**：`D:\SteamLibrary\steamapps\workshop\content\261550\2859188632\bin\Win64_Shipping_Client\0Harmony.dll`
- **UIExtenderEx**：`D:\SteamLibrary\steamapps\workshop\content\261550\2859222409\bin\Win64_Shipping_Client\Bannerlord.UIExtenderEx.dll`


