using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
// 暂时移除 UIExtenderEx 的强类型引用，避免类型加载阶段就失败
// using Bannerlord.UIExtenderEx;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem.Actions;

namespace QuickStartMod
{
    // 独立的 QuickStart Mod - 主菜单快速开始按钮
    public class QuickStartSubModule : MBSubModuleBase
    {
        private static Harmony _harmony = null;
        // 暂时移除 UIExtender 的强类型引用，改用 object 或反射
        private object _uiExtender = null;
        // 用于 OnApplicationTick 延迟发放金币的计时
        private float _goldWaitTime = 0f;
        
        // 静态构造函数：在类加载时立即执行，比 OnSubModuleLoad 更早
        static QuickStartSubModule()
        {
            // 先写文件日志（不依赖任何游戏 API）
            try
            {
                var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "QuickStartMod_Static.log");
                var msg = string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] [QuickStart] 静态构造函数执行！类已加载！\n", DateTime.Now);
                msg += "DLL 路径: " + System.Reflection.Assembly.GetExecutingAssembly().Location + "\n";
                System.IO.File.AppendAllText(logPath, msg);
            }
            catch { }
            
            // 然后尝试使用 Debug.Print
            try
            {
                TaleWorlds.Library.Debug.Print("[QuickStart] 静态构造函数执行！类已加载！", 0, TaleWorlds.Library.Debug.DebugColor.Green);
                TaleWorlds.Library.Debug.Print("[QuickStart] DLL 路径: " + System.Reflection.Assembly.GetExecutingAssembly().Location, 0, TaleWorlds.Library.Debug.DebugColor.Green);
            }
            catch (Exception ex)
            {
                try
                {
                    var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "QuickStartMod_Static_Error.log");
                    var msg = string.Format("[{0}] Debug.Print 失败: {1}\n{2}\n", DateTime.Now, ex.Message, ex.StackTrace);
                    System.IO.File.AppendAllText(logPath, msg);
                }
                catch { }
            }
        }

        protected override void OnSubModuleLoad()
        {
            // 先写文件日志（不依赖任何游戏 API）
            try
            {
                var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "QuickStartMod_OnLoad.log");
                var msg = string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] [QuickStart] OnSubModuleLoad ENTER\n", DateTime.Now);
                System.IO.File.AppendAllText(logPath, msg);
            }
            catch { }
            
            // 使用 Debug.Print 确保能在 rgl_log 中看到
            try
            {
                TaleWorlds.Library.Debug.Print("[QuickStart] OnSubModuleLoad ENTER", 0, TaleWorlds.Library.Debug.DebugColor.Green);
            }
            catch { }
            
            try
            {
                base.OnSubModuleLoad();
                TaleWorlds.Library.Debug.Print("[QuickStart] base.OnSubModuleLoad() 执行完成", 0, TaleWorlds.Library.Debug.DebugColor.Green);
                
                // Step 1: 先启用 UIExtenderEx（这一步才决定按钮会不会出现）
                try
                {
                    TaleWorlds.Library.Debug.Print("[QuickStart] Step 1: 开始启用 UIExtenderEx", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                    
                        // 探针日志1：检查 MenuOptions 的类型和 item 类型（用于注入菜单项）
                        try
                        {
                            var vmType = Type.GetType("TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu.InitialMenuVM, TaleWorlds.MountAndBlade.ViewModelCollection");
                            if (vmType != null)
                            {
                                TaleWorlds.Library.Debug.Print("[QuickStart] ========== 检查 MenuOptions 类型 ==========", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                                
                                // 获取 MenuOptions 属性
                                var menuOptionsProp = vmType.GetProperty("MenuOptions");
                                if (menuOptionsProp != null)
                                {
                                    TaleWorlds.Library.Debug.Print("[QuickStart] MenuOptions 属性类型: " + menuOptionsProp.PropertyType.FullName, 0, TaleWorlds.Library.Debug.DebugColor.Green);
                                    
                                    // 检查是否是泛型集合
                                    if (menuOptionsProp.PropertyType.IsGenericType)
                                    {
                                        var genericArgs = menuOptionsProp.PropertyType.GetGenericArguments();
                                        if (genericArgs.Length > 0)
                                        {
                                            var itemType = genericArgs[0];
                                            TaleWorlds.Library.Debug.Print("[QuickStart] MenuOptions item 类型: " + itemType.FullName, 0, TaleWorlds.Library.Debug.DebugColor.Green);
                                            
                                            // 打印 item 类型的构造函数
                                            TaleWorlds.Library.Debug.Print("[QuickStart] ========== Item 类型构造函数 ==========", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                                            foreach (var ctor in itemType.GetConstructors())
                                            {
                                                var paramNames = new System.Collections.Generic.List<string>();
                                                foreach (var p in ctor.GetParameters())
                                                {
                                                    paramNames.Add(p.ParameterType.Name + " " + p.Name);
                                                }
                                                TaleWorlds.Library.Debug.Print("[QuickStart]  - " + string.Join(", ", paramNames), 0, TaleWorlds.Library.Debug.DebugColor.White);
                                            }
                                            
                                            // 打印 item 类型的属性
                                            TaleWorlds.Library.Debug.Print("[QuickStart] ========== Item 类型属性 ==========", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                                            foreach (var prop in itemType.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
                                            {
                                            TaleWorlds.Library.Debug.Print(string.Format("[QuickStart]  - {0} ({1})", prop.Name, prop.PropertyType.Name), 0, TaleWorlds.Library.Debug.DebugColor.White);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    TaleWorlds.Library.Debug.Print("[QuickStart] 找不到 MenuOptions 属性", 0, TaleWorlds.Library.Debug.DebugColor.Red);
                                }
                            }
                            else
                            {
                                TaleWorlds.Library.Debug.Print("[QuickStart] 无法找到 InitialMenuVM 类型", 0, TaleWorlds.Library.Debug.DebugColor.Red);
                            }
                        }
                        catch (Exception ex)
                        {
                            TaleWorlds.Library.Debug.Print("[QuickStart] 检查 MenuOptions FAILED: " + ex, 0, TaleWorlds.Library.Debug.DebugColor.Red);
                        }

                        // 探针日志2：检查 ViewModelMixinAttribute 的构造函数签名
                        try
                        {
                            var attrType = Type.GetType("Bannerlord.UIExtenderEx.Attributes.ViewModelMixinAttribute, Bannerlord.UIExtenderEx");
                            if (attrType != null)
                            {
                                TaleWorlds.Library.Debug.Print("[QuickStart] ========== ViewModelMixinAttribute constructors ==========", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                                foreach (var c in attrType.GetConstructors())
                                {
                                    TaleWorlds.Library.Debug.Print("[QuickStart]  - " + c, 0, TaleWorlds.Library.Debug.DebugColor.White);
                                }

                                TaleWorlds.Library.Debug.Print("[QuickStart] ========== ViewModelMixinAttribute writable properties ==========", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                                foreach (var p in attrType.GetProperties())
                                {
                                    TaleWorlds.Library.Debug.Print(string.Format("[QuickStart]  - {0} ({1}) CanWrite={2}", p.Name, p.PropertyType.FullName, p.CanWrite), 0, TaleWorlds.Library.Debug.DebugColor.White);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            TaleWorlds.Library.Debug.Print("[QuickStart] Reflect ViewModelMixinAttribute FAILED: " + ex, 0, TaleWorlds.Library.Debug.DebugColor.Red);
                        }

                        // 探针日志3：检查 InitialMenuVM 和 RefreshValues 方法是否存在（保留原有逻辑）
                        try
                        {
                            Type initialMenuVMType = null;
                        
                        // 方法1：尝试完整类型名
                        var typeName1 = "TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenuVM";
                        initialMenuVMType = Type.GetType(typeName1 + ", TaleWorlds.MountAndBlade");
                        
                        // 方法2：如果失败，尝试从所有程序集中查找
                        if (initialMenuVMType == null)
                        {
                            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                            {
                                try
                                {
                                    var type = assembly.GetType(typeName1);
                                    if (type != null)
                                    {
                                        initialMenuVMType = type;
                            TaleWorlds.Library.Debug.Print("[QuickStart] InitialMenuVM found in assembly: " + assembly.GetName().Name, 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                                        break;
                                    }
                                }
                                catch { }
                            }
                        }
                        
                        // 方法3：如果还是找不到，尝试查找所有包含 "InitialMenuVM" 的类（排除委托、接口等）
                        if (initialMenuVMType == null)
                        {
                            TaleWorlds.Library.Debug.Print("[QuickStart] 搜索所有包含 'InitialMenuVM' 的类类型...", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                            {
                                try
                                {
                                    var types = assembly.GetTypes();
                                    foreach (var type in types)
                                    {
                                        // 只查找类类型，排除委托、接口、枚举等
                                        if (type.IsClass && !type.IsAbstract && type.Name == "InitialMenuVM")
                                        {
                                            TaleWorlds.Library.Debug.Print(string.Format("[QuickStart] 找到可能的类型: {0} (IsClass: {1})", type.FullName, type.IsClass), 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                                            initialMenuVMType = type;
                                            break;
                                        }
                                    }
                                    if (initialMenuVMType != null) break;
                                }
                                catch { }
                            }
                            
                            // 如果还是找不到，尝试查找所有包含 "MenuVM" 的类
                            if (initialMenuVMType == null)
                            {
                                TaleWorlds.Library.Debug.Print("[QuickStart] 搜索所有包含 'MenuVM' 的类类型...", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                                {
                                    try
                                    {
                                        var types = assembly.GetTypes();
                                        foreach (var type in types)
                                        {
                                            if (type.IsClass && !type.IsAbstract && type.Name.Contains("MenuVM") && type.Name.Contains("Initial"))
                                            {
                                                TaleWorlds.Library.Debug.Print("[QuickStart] 找到可能的类型: " + type.FullName, 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                                                initialMenuVMType = type;
                                                break;
                                            }
                                        }
                                        if (initialMenuVMType != null) break;
                                    }
                                    catch { }
                                }
                            }
                        }
                        
                        TaleWorlds.Library.Debug.Print("[QuickStart] InitialMenuVM type found? " + (initialMenuVMType != null), 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                        
                        if (initialMenuVMType != null)
                        {
                            TaleWorlds.Library.Debug.Print("[QuickStart] InitialMenuVM full name: " + initialMenuVMType.FullName, 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                            
                            var refreshMethod = initialMenuVMType.GetMethod("RefreshValues", 
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            TaleWorlds.Library.Debug.Print("[QuickStart] InitialMenuVM.RefreshValues found? " + (refreshMethod != null), 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                            
                            if (refreshMethod == null)
                            {
                                // 尝试查找其他可能的 refresh 方法名
                                var refreshMethod2 = initialMenuVMType.GetMethod("Refresh", 
                                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                TaleWorlds.Library.Debug.Print("[QuickStart] InitialMenuVM.Refresh found? " + (refreshMethod2 != null), 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                                
                                // 列出所有方法名，帮助找到正确的 refresh 方法
                                var allMethods = initialMenuVMType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                var methodNames = new System.Collections.Generic.List<string>();
                                foreach (var m in allMethods)
                                {
                                    if (m.Name.Contains("Refresh") || m.Name.Contains("Update"))
                                    {
                                        methodNames.Add(m.Name);
                                    }
                                }
                                if (methodNames.Count > 0)
                                {
                                    TaleWorlds.Library.Debug.Print("[QuickStart] 找到可能的 refresh 方法: " + string.Join(", ", methodNames), 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                                }
                            }
                        }
                        else
                        {
                            TaleWorlds.Library.Debug.Print("[QuickStart] ⚠️ InitialMenuVM 类型未找到！这会导致 ViewModelMixin 注册失败", 0, TaleWorlds.Library.Debug.DebugColor.Red);
                        }
                    }
                    catch (Exception ex)
                    {
                        TaleWorlds.Library.Debug.Print("[QuickStart] 探针日志失败: " + ex.Message, 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                        TaleWorlds.Library.Debug.Print("[QuickStart] 探针日志堆栈: " + ex.StackTrace, 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                    }
                    
                    // 使用反射获取 UIExtenderEx 类型（不使用 Linq，避免依赖问题）
                    System.Reflection.Assembly uiExtenderExAssembly = null;
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var name = assembly.GetName().Name;
                        if (name != null && name.Contains("UIExtenderEx"))
                        {
                            uiExtenderExAssembly = assembly;
                            break;
                        }
                    }
                    
                    if (uiExtenderExAssembly == null)
                    {
                        TaleWorlds.Library.Debug.Print("[QuickStart] UIExtenderEx 未找到！", 0, TaleWorlds.Library.Debug.DebugColor.Red);
                        return;
                    }
                    
                    TaleWorlds.Library.Debug.Print("[QuickStart] UIExtenderEx 已加载: " + uiExtenderExAssembly.FullName, 0, TaleWorlds.Library.Debug.DebugColor.Green);
                    
                    // 获取 UIExtender 类型
                    var uiExtenderType = uiExtenderExAssembly.GetType("Bannerlord.UIExtenderEx.UIExtender");
                    if (uiExtenderType == null)
                    {
                        TaleWorlds.Library.Debug.Print("[QuickStart] 找不到 UIExtender 类型！", 0, TaleWorlds.Library.Debug.DebugColor.Red);
                        return;
                    }
                    
                    // 调用 UIExtender.Create("QuickStartMod")
                    var createMethod = uiExtenderType.GetMethod("Create", new[] { typeof(string) });
                    if (createMethod == null)
                    {
                        TaleWorlds.Library.Debug.Print("[QuickStart] 找不到 UIExtender.Create 方法！", 0, TaleWorlds.Library.Debug.DebugColor.Red);
                        return;
                    }
                    
                    _uiExtender = createMethod.Invoke(null, new object[] { "QuickStartMod" });
                    if (_uiExtender == null)
                    {
                        TaleWorlds.Library.Debug.Print("[QuickStart] UIExtender.Create 返回 null！", 0, TaleWorlds.Library.Debug.DebugColor.Red);
                        return;
                    }
                    
                           // 调用 Register（ViewModelMixin 已注释掉，只会注册 PrefabExtension）
                           var registerMethod = uiExtenderType.GetMethod("Register", new[] { typeof(Assembly) });
                           if (registerMethod != null)
                           {
                               registerMethod.Invoke(_uiExtender, new object[] { typeof(QuickStartSubModule).Assembly });
                               TaleWorlds.Library.Debug.Print("[QuickStart] UIExtender.Register 成功（ViewModelMixin 已禁用，只注册 PrefabExtension）", 0, TaleWorlds.Library.Debug.DebugColor.Green);
                           }
                    
                           // 调用 Enable（指定无参数，避免 AmbiguousMatchException）
                           var enableMethod = uiExtenderType.GetMethod("Enable", Type.EmptyTypes);
                           if (enableMethod != null)
                           {
                               enableMethod.Invoke(_uiExtender, null);
                               TaleWorlds.Library.Debug.Print("[QuickStart] UIExtenderEx enabled", 0, TaleWorlds.Library.Debug.DebugColor.Green);
                           }
                           else
                           {
                               TaleWorlds.Library.Debug.Print("[QuickStart] 找不到 UIExtender.Enable 方法！", 0, TaleWorlds.Library.Debug.DebugColor.Red);
                           }
                }
                catch (System.Exception ex)
                {
                    TaleWorlds.Library.Debug.Print("[QuickStart] UIExtenderEx enable FAILED: " + ex, 0, TaleWorlds.Library.Debug.DebugColor.Red);
                    TaleWorlds.Library.Debug.Print("[QuickStart] StackTrace: " + ex.StackTrace, 0, TaleWorlds.Library.Debug.DebugColor.Red);
                }
                
                // ✅ 使用 PatchAll 让 Harmony 自动调用 TargetMethods() 来 Patch 多个方法
                try
                {
                    if (_harmony == null)
                    {
                        _harmony = new Harmony("QuickStartMod");
                    }
                    _harmony.PatchAll(typeof(QuickStartMod.QuickStartMenuInjectPatch).Assembly);
                    TaleWorlds.Library.Debug.Print("[QuickStart] Harmony PatchAll 成功（使用 TargetMethods 自动 Patch）", 0, TaleWorlds.Library.Debug.DebugColor.Green);
                }
                catch (Exception ex)
                {
                    TaleWorlds.Library.Debug.Print("[QuickStart] Harmony PatchAll 失败: " + ex, 0, TaleWorlds.Library.Debug.DebugColor.Red);
                    TaleWorlds.Library.Debug.Print("[QuickStart] StackTrace: " + ex.StackTrace, 0, TaleWorlds.Library.Debug.DebugColor.Red);
                }
                
                TaleWorlds.Library.Debug.Print("[QuickStart] OnSubModuleLoad 执行完成", 0, TaleWorlds.Library.Debug.DebugColor.Green);
            }
            catch (System.Exception ex)
            {
                TaleWorlds.Library.Debug.Print("[QuickStart] OnSubModuleLoad EX=" + ex, 0, TaleWorlds.Library.Debug.DebugColor.Red);
                TaleWorlds.Library.Debug.Print("[QuickStart] StackTrace: " + ex.StackTrace, 0, TaleWorlds.Library.Debug.DebugColor.Red);
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
            
            // 清理 UIExtenderEx（使用反射）
            if (_uiExtender != null)
            {
                try
                {
                    var disableMethod = _uiExtender.GetType().GetMethod("Disable");
                    disableMethod?.Invoke(_uiExtender, null);
                }
                catch { }
                _uiExtender = null;
            }
            
            // 清理 Harmony 补丁
            _harmony?.UnpatchAll();
            _harmony = null;
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);

            // 只有在快速开始模式且存在待发放金币时才继续
            if (!QuickStartHelper.IsQuickStartMode)
                return;

            if (!QuickStartHelper.PendingGold || QuickStartHelper.GoldDone)
                return;

            var game = Game.Current;
            var gsm = game != null ? game.GameStateManager : null;
            var state = gsm != null ? gsm.ActiveState : null;
            if (state == null)
                return;

            // 不强依赖 MapState 类型本身，只比较类型名，尽量降低对 CampaignSystem 装载时序的影响
            if (!string.Equals(state.GetType().Name, "MapState", StringComparison.Ordinal))
                return;

            // 进入 MapState 后再缓冲一小段时间，避免刚切入地图瞬间就动经济系统
            _goldWaitTime += dt;
            if (_goldWaitTime < 0.5f)
                return;

            try
            {
                // 反射拿到 Hero.MainHero
                var heroType = Type.GetType("TaleWorlds.CampaignSystem.Hero, TaleWorlds.CampaignSystem");
                var mainHeroProp = heroType != null
                    ? heroType.GetProperty("MainHero", BindingFlags.Public | BindingFlags.Static)
                    : null;
                var heroObj = mainHeroProp != null ? mainHeroProp.GetValue(null, null) : null;

                if (heroType == null || heroObj == null)
                    return;

                // 反射调用 ChangeHeroGold(int)
                var changeGold = heroType.GetMethod(
                    "ChangeHeroGold",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(int) },
                    null);

                if (changeGold == null)
                    return;

                changeGold.Invoke(heroObj, new object[] { QuickStartHelper.QuickStartGold });

                Debug.Print(
                    "[QuickStart] Gold granted on MapState: " + QuickStartHelper.QuickStartGold,
                    0,
                    Debug.DebugColor.Green);

                QuickStartHelper.GoldDone = true;
            }
            catch (Exception ex)
            {
                Debug.Print(
                    "[QuickStart] Gold grant FAILED on MapState: " + ex,
                    0,
                    Debug.DebugColor.Red);
            }
            finally
            {
                // 不管成功失败，都收口，避免反复折腾导致更不稳定
                QuickStartHelper.PendingGold = false;
                QuickStartHelper.IsQuickStartMode = false;
                _goldWaitTime = 0f;
            }
        }
    }
}

