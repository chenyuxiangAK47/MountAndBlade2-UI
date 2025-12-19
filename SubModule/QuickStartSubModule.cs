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
using TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu;

namespace QuickStartMod
{
    // 独立的 QuickStart Mod - 主菜单快速开始按钮
    public class QuickStartSubModule : MBSubModuleBase
    {
        // ✅ 按照 ChatGPT 要求：每次编译手动改 BUILD_ID，确保运行的是最新 DLL
        private const string QS_BUILD_ID = "2025-12-19-2100";
        
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
                // ✅ 按照 ChatGPT 要求：必须在一进来就打 BUILD_ID + DLL 指纹，确保运行的是最新 DLL
                var thisAssembly = typeof(QuickStartSubModule).Assembly;
                var location = thisAssembly.Location;
                var version = thisAssembly.GetName().Version?.ToString() ?? "null";
                var fileTime = System.IO.File.Exists(location) 
                    ? System.IO.File.GetLastWriteTime(location).ToString("yyyy-MM-dd HH:mm:ss")
                    : "no_file";
                
                TaleWorlds.Library.Debug.Print(
                    $"[QuickStart] ========== BUILD={QS_BUILD_ID} ==========",
                    0,
                    TaleWorlds.Library.Debug.DebugColor.Yellow);
                TaleWorlds.Library.Debug.Print(
                    $"[QuickStart] Ver={version}, DllTime={fileTime}",
                    0,
                    TaleWorlds.Library.Debug.DebugColor.Yellow);
                TaleWorlds.Library.Debug.Print(
                    $"[QuickStart] Path={location}",
                    0,
                    TaleWorlds.Library.Debug.DebugColor.Yellow);
                TaleWorlds.Library.Debug.Print(
                    "[QuickStart] =====================================",
                    0,
                    TaleWorlds.Library.Debug.DebugColor.Yellow);
                
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
                    
                           // 调用 Enable（尝试多个重载，避免 AmbiguousMatchException）
                           // 根据 ChatGPT 建议：如果不需要 UIExtenderEx 的 API，可以跳过 Enable
                           // 但为了兼容性，我们尝试调用 Enable
                           MethodInfo enableMethod = null;
                           
                           // 尝试无参数版本
                           try
                           {
                               enableMethod = uiExtenderType.GetMethod("Enable", Type.EmptyTypes);
                           }
                           catch (AmbiguousMatchException)
                           {
                               // 如果有多个重载，尝试获取所有 Enable 方法，选择无参数的
                               var methods = uiExtenderType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                                   .Where(m => m.Name == "Enable" && m.GetParameters().Length == 0)
                                   .ToArray();
                               if (methods.Length > 0)
                               {
                                   enableMethod = methods[0];
                               }
                           }
                           
                           if (enableMethod != null)
                           {
                               enableMethod.Invoke(_uiExtender, null);
                               TaleWorlds.Library.Debug.Print("[QuickStart] UIExtenderEx enabled", 0, TaleWorlds.Library.Debug.DebugColor.Green);
                           }
                           else
                           {
                               // Enable 失败不影响功能（按钮通过 Harmony 注入）
                               TaleWorlds.Library.Debug.Print("[QuickStart] UIExtender.Enable 未找到或调用失败（不影响功能）", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                           }
                }
                catch (System.Exception ex)
                {
                    TaleWorlds.Library.Debug.Print("[QuickStart] UIExtenderEx enable FAILED: " + ex, 0, TaleWorlds.Library.Debug.DebugColor.Red);
                    TaleWorlds.Library.Debug.Print("[QuickStart] StackTrace: " + ex.StackTrace, 0, TaleWorlds.Library.Debug.DebugColor.Red);
                }
                
                // ✅ 恢复到最简单的版本：使用 PatchAll + TargetMethods（这是之前能工作的版本）
                try
                {
                    if (_harmony == null)
                    {
                        _harmony = new Harmony("QuickStartMod");
                    }
                    
                    TaleWorlds.Library.Debug.Print("[QuickStart] 开始应用 Harmony PatchAll（按钮注入）...", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                    
                    // 1) PatchAll 用于按钮注入（使用 TargetMethods）
                    _harmony.PatchAll(typeof(QuickStartMod.QuickStartMenuInjectPatch).Assembly);
                    
                    // ✅ 按照 ChatGPT 要求：验证 Patch 是否成功，打印每个被 patch 的方法的 PatchInfo
                    try
                    {
                        var targetType = typeof(InitialMenuVM);
                        var ctors = AccessTools.GetDeclaredConstructors(targetType);
                        var refreshValues = AccessTools.DeclaredMethod(targetType, "RefreshValues");
                        
                        int totalPatched = 0;
                        foreach (var ctor in ctors)
                        {
                            if (ctor != null)
                            {
                                var patchInfo = Harmony.GetPatchInfo(ctor);
                                var postfixCount = patchInfo?.Postfixes?.Count ?? 0;
                                TaleWorlds.Library.Debug.Print(
                                    $"[QuickStart] PATCH_VERIFY: {ctor.ToString()} | Postfix count={postfixCount}",
                                    0,
                                    postfixCount > 0 ? TaleWorlds.Library.Debug.DebugColor.Green : TaleWorlds.Library.Debug.DebugColor.Red);
                                if (postfixCount > 0) totalPatched++;
                            }
                        }
                        
                        if (refreshValues != null)
                        {
                            var patchInfo = Harmony.GetPatchInfo(refreshValues);
                            var postfixCount = patchInfo?.Postfixes?.Count ?? 0;
                            TaleWorlds.Library.Debug.Print(
                                $"[QuickStart] PATCH_VERIFY: {refreshValues.ToString()} | Postfix count={postfixCount}",
                                0,
                                postfixCount > 0 ? TaleWorlds.Library.Debug.DebugColor.Green : TaleWorlds.Library.Debug.DebugColor.Red);
                            if (postfixCount > 0) totalPatched++;
                        }
                        
                        TaleWorlds.Library.Debug.Print(
                            $"[QuickStart] PATCH_OK: 总共成功 patch {totalPatched} 个方法",
                            0,
                            totalPatched > 0 ? TaleWorlds.Library.Debug.DebugColor.Green : TaleWorlds.Library.Debug.DebugColor.Red);
                    }
                    catch (Exception verifyEx)
                    {
                        TaleWorlds.Library.Debug.Print("[QuickStart] PATCH_VERIFY 失败: " + verifyEx.Message, 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                    }
                    
                    TaleWorlds.Library.Debug.Print("[QuickStart] Harmony PatchAll 完成（按钮注入）", 0, TaleWorlds.Library.Debug.DebugColor.Green);
                    
                    // 2) 手动 patch StartNarrativeStage（因为它在 CampaignSystem 中，可能加载较晚）
                    try
                    {
                        var managerType = Type.GetType("TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationManager, TaleWorlds.CampaignSystem");
                        if (managerType != null)
                        {
                            var startNarrativeMethod = AccessTools.Method(managerType, "StartNarrativeStage");
                            if (startNarrativeMethod != null)
                            {
                                var patchMethod = AccessTools.Method(typeof(QuickStartCharacterCreationManagerPatch), "Postfix");
                                if (patchMethod != null)
                                {
                                    _harmony.Patch(startNarrativeMethod, postfix: new HarmonyMethod(patchMethod));
                                    TaleWorlds.Library.Debug.Print("[QuickStart] Patched: CharacterCreationManager.StartNarrativeStage", 0, TaleWorlds.Library.Debug.DebugColor.Green);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        TaleWorlds.Library.Debug.Print("[QuickStart] Failed to patch StartNarrativeStage: " + ex.Message, 0, TaleWorlds.Library.Debug.DebugColor.Red);
                    }
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

            // A) 角色创建“自动驾驶”：只在快速开始模式启用时工作
            if (QuickStartHelper.IsQuickStartMode &&
                QuickStartHelper.AutoSkipCharCreation &&
                !QuickStartHelper.CharCreationDone)
            {
                QuickStartCharCreationSkipper.Tick(dt);
            }

            // 1) 只有在快速开始模式且存在待发放金币时才继续金币逻辑
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

            // 进入 MapState 后再缓冲更长时间，确保所有对象都已初始化
            _goldWaitTime += dt;
            if (_goldWaitTime < 2.0f) // 从 0.5 秒增加到 2.0 秒，确保游戏完全初始化
            {
                // 在等待期间，每 0.5 秒检查一次对象是否可用
                if (_goldWaitTime > 0.5f && (int)(_goldWaitTime * 2) != (int)((_goldWaitTime - dt) * 2))
                {
                    Debug.Print(
                        "[QuickStart] Waiting for game initialization... (" + _goldWaitTime.ToString("F1") + "s)",
                        0,
                        Debug.DebugColor.Yellow);
                }
                return;
            }

            try
            {
                // 反射拿到 Hero.MainHero
                var heroType = Type.GetType("TaleWorlds.CampaignSystem.Hero, TaleWorlds.CampaignSystem");
                if (heroType == null)
                {
                    Debug.Print(
                        "[QuickStart] Gold grant: Hero type not found, will retry",
                        0,
                        Debug.DebugColor.Yellow);
                    _goldWaitTime = 1.5f; // 重置等待时间，稍后重试
                    return;
                }

                var mainHeroProp = heroType.GetProperty("MainHero", BindingFlags.Public | BindingFlags.Static);
                if (mainHeroProp == null)
                {
                    Debug.Print(
                        "[QuickStart] Gold grant: MainHero property not found, will retry",
                        0,
                        Debug.DebugColor.Yellow);
                    _goldWaitTime = 1.5f;
                    return;
                }

                var heroObj = mainHeroProp.GetValue(null, null);
                if (heroObj == null)
                {
                    Debug.Print(
                        "[QuickStart] Gold grant: MainHero is null, will retry",
                        0,
                        Debug.DebugColor.Yellow);
                    _goldWaitTime = 1.5f;
                    return;
                }

                // 额外检查：尝试访问 Hero 的某个属性，确保对象真的可用
                try
                {
                    var nameProp = heroType.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                    if (nameProp != null)
                    {
                        var name = nameProp.GetValue(heroObj, null);
                        if (name == null)
                        {
                            Debug.Print(
                                "[QuickStart] Gold grant: Hero.Name is null, object may not be fully initialized, will retry",
                                0,
                                Debug.DebugColor.Yellow);
                            _goldWaitTime = 1.5f;
                            return;
                        }
                    }
                }
                catch (Exception checkEx)
                {
                    Debug.Print(
                        "[QuickStart] Gold grant: Hero object validation failed: " + checkEx.Message + ", will retry",
                        0,
                        Debug.DebugColor.Yellow);
                    _goldWaitTime = 1.5f;
                    return;
                }

                // 反射调用 ChangeHeroGold(int)
                var changeGold = heroType.GetMethod(
                    "ChangeHeroGold",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(int) },
                    null);

                if (changeGold == null)
                {
                    Debug.Print(
                        "[QuickStart] Gold grant: ChangeHeroGold method not found",
                        0,
                        Debug.DebugColor.Red);
                    QuickStartHelper.GoldDone = true; // 标记为完成，避免无限重试
                    QuickStartHelper.PendingGold = false;
                    return;
                }

                // 调用方法
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
                    "[QuickStart] Gold grant FAILED on MapState: " + ex.Message,
                    0,
                    Debug.DebugColor.Red);
                Debug.Print(
                    "[QuickStart] StackTrace: " + ex.StackTrace,
                    0,
                    Debug.DebugColor.Red);
                
                // 如果是访问违规或空引用，标记为完成，避免反复尝试导致崩溃
                if (ex is NullReferenceException || ex is System.Reflection.TargetInvocationException)
                {
                    Debug.Print(
                        "[QuickStart] Gold grant: Critical error detected, marking as done to prevent crash",
                        0,
                        Debug.DebugColor.Red);
                    QuickStartHelper.GoldDone = true;
                }
                else
                {
                    // 其他错误，重置等待时间，稍后重试
                    _goldWaitTime = 1.5f;
                    return;
                }
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

