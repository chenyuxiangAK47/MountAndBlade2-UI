using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu;

namespace QuickStartMod
{
    // ✅ 恢复原版本：使用 PatchAll + TargetMethods（这是之前能工作的版本）
    [HarmonyPatch(typeof(InitialMenuVM))]
    public static class QuickStartMenuInjectPatch
    {
        // ✅ 按照 ChatGPT 要求：必须 patch 所有 ctor + Declared RefreshValues
        // Patch 多个"可能会在填充菜单项后被调用"的点
        public static IEnumerable<MethodBase> TargetMethods()
        {
            // 1) ✅ 修复：获取所有声明的构造函数（不是只获取一个）
            var ctors = AccessTools.GetDeclaredConstructors(typeof(InitialMenuVM));
            foreach (var ctor in ctors)
            {
                if (ctor != null)
                {
                    TaleWorlds.Library.Debug.Print($"[QuickStart] TargetMethods: 找到构造函数 {ctor.ToString()}", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                    yield return ctor;
                }
            }

            // 2) ✅ 修复：使用 DeclaredMethod（只获取声明的，避免父类同名方法导致 patch 到错误 MethodInfo）
            var rv = AccessTools.DeclaredMethod(typeof(InitialMenuVM), "RefreshValues");
            if (rv != null)
            {
                TaleWorlds.Library.Debug.Print($"[QuickStart] TargetMethods: 找到 RefreshValues {rv.ToString()}", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                yield return rv;
            }
            else
            {
                TaleWorlds.Library.Debug.Print("[QuickStart] TargetMethods: RefreshValues (DeclaredMethod) 未找到", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
            }

            // 3) 自动扫：名字含 Refresh/Menu/Option 的 void 无参方法
            var ms = AccessTools.GetDeclaredMethods(typeof(InitialMenuVM));
            foreach (var m in ms)
            {
                if (m.IsSpecialName) continue;
                if (m.ReturnType != typeof(void)) continue;
                if (m.GetParameters().Length != 0) continue;

                var n = m.Name;
                if (n.Contains("Refresh") && (n.Contains("Menu") || n.Contains("Option")))
                {
                    TaleWorlds.Library.Debug.Print("[QuickStart] TargetMethods: 找到方法 " + n, 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                    yield return m;
                }
            }
        }

        // ✅ 按照 ChatGPT 要求：只保留一个 Postfix 重载，签名必须明确
        // Postfix(InitialMenuVM __instance, MethodBase __originalMethod)
        public static void Postfix(InitialMenuVM __instance, MethodBase __originalMethod)
        {
            try
            {
                // ✅ 按照 ChatGPT 要求：必须打印详细的 Postfix hit 日志
                var methodName = (__originalMethod != null) ? __originalMethod.Name : "null";
                var declaringType = (__originalMethod != null) ? __originalMethod.DeclaringType?.Name : "null";
                var methodToString = (__originalMethod != null) ? __originalMethod.ToString() : "null";
                var count = (__instance != null && __instance.MenuOptions != null) ? __instance.MenuOptions.Count : 0;
                
                // ✅ 关键日志：必须能在 rgl_log 中搜索到 "POSTFIX_HIT"
                TaleWorlds.Library.Debug.Print(
                    $"[QuickStart] POSTFIX_HIT: {declaringType}.{methodName} | {methodToString} | MenuOptions.Count={count}",
                    0,
                    TaleWorlds.Library.Debug.DebugColor.Green);
                
                EnsureQuickStart(__instance);
            }
            catch (Exception ex)
            {
                TaleWorlds.Library.Debug.Print("[QuickStart] Postfix error: " + ex, 0, TaleWorlds.Library.Debug.DebugColor.Red);
                TaleWorlds.Library.Debug.Print("[QuickStart] StackTrace: " + ex.StackTrace, 0, TaleWorlds.Library.Debug.DebugColor.Red);
            }
        }

        private static void EnsureQuickStart(InitialMenuVM vm)
        {
            if (vm?.MenuOptions == null)
            {
                TaleWorlds.Library.Debug.Print("[QuickStart] EnsureQuickStart: vm 或 MenuOptions 为 null", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                return;
            }

            // ⭐关键：原版菜单还没填充时（Count=0），先别插。等下一次"真的填完"再插
            if (vm.MenuOptions.Count == 0)
            {
                TaleWorlds.Library.Debug.Print("[QuickStart] EnsureQuickStart: MenuOptions.Count == 0，跳过", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                return;
            }

            // 检查是否已存在（用 NameText 检测 - 使用唯一标识符）
            foreach (var opt in vm.MenuOptions)
            {
                try
                {
                    var nameText = opt?.NameText;
                    if (nameText != null && nameText.ToString().Contains("QS MOD"))
                    {
                        TaleWorlds.Library.Debug.Print("[QuickStart] EnsureQuickStart: 菜单项已存在，跳过", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                        return;
                    }
                }
                catch { }
            }

            TaleWorlds.Library.Debug.Print("[QuickStart] EnsureQuickStart: 开始创建菜单项...", 0, TaleWorlds.Library.Debug.DebugColor.Green);

            // 创建 TextObject - 使用唯一标识符避免与 DLC 混淆
            var name = new TextObject("{=qs_quickstart}QS MOD 快速开始");
            var hint = new TextObject("{=qs_quickstart_hint}直接进入沙盒并给予初始资源");

            // 创建委托
            Func<(bool, TextObject)> isDisabledAndReason = () => (false, new TextObject(""));
            Func<bool> isHidden = () => false;

            // 点击动作：不再直接反射 Game.StartNewGame(false)，而是复用原版"沙盒模式"菜单项的执行逻辑
            Action action = () =>
            {
                try
                {
                    // 1) 独一无二的按钮点击日志（rgl_log）
                    TaleWorlds.Library.Debug.Print("[QuickStart] >>> QS BUTTON CLICKED <<<", 0, TaleWorlds.Library.Debug.DebugColor.Green);

                    // 2) 屏幕提示（用户可见，绝不会"找错日志"）
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[QS MOD] 快速开始按钮已点击！正在启动新游戏...",
                        new TaleWorlds.Library.Color(0.2f, 0.8f, 0.2f)));

                    // 3) 文件日志（写到 Modules/QuickStartMod/qs_runtime.log）
                    try
                    {
                        var logPath = System.IO.Path.Combine(
                            System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                            "..", "..", "..", "Modules", "QuickStartMod", "qs_runtime.log");
                        var logDir = System.IO.Path.GetDirectoryName(logPath);
                        if (!System.IO.Directory.Exists(logDir))
                            System.IO.Directory.CreateDirectory(logDir);
                        
                        var logMsg = string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] [QuickStart] >>> QS BUTTON CLICKED <<<\n", System.DateTime.Now);
                        System.IO.File.AppendAllText(logPath, logMsg);
                    }
                    catch (Exception logEx)
                    {
                        // 文件日志失败不影响主流程
                        TaleWorlds.Library.Debug.Print("[QuickStart] 文件日志写入失败: " + logEx.Message, 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                    }

                    TaleWorlds.Library.Debug.Print("[QuickStart] ========== 按钮被点击！尝试复用沙盒菜单项 ==========", 0, TaleWorlds.Library.Debug.DebugColor.Green);

                    // 标记为快速开始模式，后续通过 OnApplicationTick 延迟发金币 / 自动角色创建
                    QuickStartHelper.IsQuickStartMode = true;
                    QuickStartHelper.AutoSkipCharCreation = true;
                    QuickStartHelper.CharCreationDone = false;
                    QuickStartHelper.SeenCharacterCreation = false; // 重置标志，准备检测角色创建状态
                    QuickStartHelper.PendingGold = true;
                    QuickStartHelper.GoldDone = false;

                    // 重置角色创建自动跳过器的状态
                    QuickStartCharCreationSkipper.ResetState();

                    // 在当前菜单项中查找“沙盒模式”按钮
                    InitialMenuOptionVM sandboxOption = null;
                    foreach (var opt in vm.MenuOptions)
                    {
                        try
                        {
                            var text = opt?.NameText?.ToString() ?? string.Empty;
                            if (string.IsNullOrEmpty(text))
                                continue;

                            // 兼容中英文关键字
                            if (text.Contains("沙盒") || text.Contains("Sandbox"))
                            {
                                sandboxOption = opt;
                                break;
                            }
                        }
                        catch
                        {
                            // 忽略单个菜单项异常，继续找
                        }
                    }

                    if (sandboxOption != null)
                    {
                        TaleWorlds.Library.Debug.Print("[QuickStart] 找到原版沙盒菜单项，调用其 ExecuteAction()", 0, TaleWorlds.Library.Debug.DebugColor.Green);

                        try
                        {
                            // 为了避免签名变动导致编译错误，这里用反射调用 ExecuteAction
                            var method = typeof(InitialMenuOptionVM).GetMethod(
                                "ExecuteAction",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                            if (method != null)
                            {
                                method.Invoke(sandboxOption, null);
                            }
                            else
                            {
                                TaleWorlds.Library.Debug.Print("[QuickStart] InitialMenuOptionVM.ExecuteAction 未找到，放弃启动以避免未知状态", 0, TaleWorlds.Library.Debug.DebugColor.Red);
                                InformationManager.DisplayMessage(new InformationMessage(
                                    "快速开始：未找到沙盒按钮执行方法，已安全中止。",
                                    new TaleWorlds.Library.Color(0.8f, 0.2f, 0.2f)));
                            }
                        }
                        catch (Exception exInvoke)
                        {
                            TaleWorlds.Library.Debug.Print("[QuickStart] 复用沙盒 ExecuteAction 失败: " + exInvoke, 0, TaleWorlds.Library.Debug.DebugColor.Red);
                            InformationManager.DisplayMessage(new InformationMessage(
                                "快速开始：沙盒按钮执行失败 - " + exInvoke.Message,
                                new TaleWorlds.Library.Color(0.8f, 0.2f, 0.2f)));
                        }
                    }
                    else
                    {
                        TaleWorlds.Library.Debug.Print("[QuickStart] 未找到沙盒菜单项，为避免走非官方路径，取消启动。", 0, TaleWorlds.Library.Debug.DebugColor.Red);
                        InformationManager.DisplayMessage(new InformationMessage(
                            "快速开始：未找到原版沙盒菜单项，已取消启动以避免潜在崩溃。",
                            new TaleWorlds.Library.Color(0.8f, 0.2f, 0.2f)));

                        // 找不到沙盒按钮时，不应继续保持“待发放金币”状态，直接清理标志
                        QuickStartHelper.IsQuickStartMode = false;
                        QuickStartHelper.PendingGold = false;
                    }
                }
                catch (Exception ex)
                {
                    TaleWorlds.Library.Debug.Print("[QuickStart] 快速开始执行失败: " + ex, 0, TaleWorlds.Library.Debug.DebugColor.Red);
                    InformationManager.DisplayMessage(new InformationMessage(
                        "快速开始：执行失败 - " + ex.Message,
                        new TaleWorlds.Library.Color(0.8f, 0.2f, 0.2f)));
                }
            };

            // 创建 InitialStateOption（使用 orderIndex: -100 确保在顶部）
            var stateOption = new InitialStateOption(
                "quick_start",
                name,
                orderIndex: -100,
                action: action,
                isDisabledAndReason: isDisabledAndReason,
                enabledHint: hint,
                isHidden: isHidden
            );

            // 创建 InitialMenuOptionVM
            var newVm = new InitialMenuOptionVM(stateOption);

            // 插入到最上面
            vm.MenuOptions.Insert(0, newVm);

            // ⭐有些版本 UI 不会立刻刷新，强制通知一次
            NotifyMenuOptionsChanged(vm);

            // ✅ 按照 ChatGPT 要求：必须打印 ENSURE_INSERTED 日志，方便搜索验证
            TaleWorlds.Library.Debug.Print(
                $"[QuickStart] ENSURE_INSERTED: 菜单项已插入，现在 MenuOptions.Count={vm.MenuOptions.Count}",
                0,
                TaleWorlds.Library.Debug.DebugColor.Green);
        }

        private static void NotifyMenuOptionsChanged(InitialMenuVM vm)
        {
            try
            {
                // ViewModel 里通常有 protected OnPropertyChanged(string)
                var m = AccessTools.Method(typeof(ViewModel), "OnPropertyChanged", new[] { typeof(string) });
                if (m != null)
                {
                    m.Invoke(vm, new object[] { "MenuOptions" });
                    TaleWorlds.Library.Debug.Print("[QuickStart] NotifyMenuOptionsChanged: OnPropertyChanged 调用成功", 0, TaleWorlds.Library.Debug.DebugColor.Green);
                }
                else
                {
                    // 兜底：再 refresh 一次（注意不要无限递归）
                    TaleWorlds.Library.Debug.Print("[QuickStart] NotifyMenuOptionsChanged: 找不到 OnPropertyChanged，使用 RefreshValues 兜底", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                    vm.RefreshValues();
                }
            }
            catch (Exception ex)
            {
                TaleWorlds.Library.Debug.Print("[QuickStart] NotifyMenuOptionsChanged 失败: " + ex.Message, 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
            }
        }
    }

    // ✅ 按照 ChatGPT 要求：直接 Patch StartNarrativeStage，这是 CurrentMenu 初始化的确切时机点
    // 不要用 OnApplicationTick 猜，而是直接 Patch 关键生命周期点
    [HarmonyPatch]
    public static class QuickStartCharacterCreationManagerPatch
    {
        static Type TargetType()
        {
            return Type.GetType(
                "TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationManager, TaleWorlds.CampaignSystem");
        }

        static MethodBase TargetMethod()
        {
            var t = TargetType();
            if (t == null)
            {
                TaleWorlds.Library.Debug.Print(
                    "[QuickStart] CharacterCreationManagerPatch: TargetType is null (可能 CampaignSystem 还未加载)",
                    0,
                    TaleWorlds.Library.Debug.DebugColor.Yellow);
                return null;
            }

            var startNarrativeMethod = AccessTools.Method(t, "StartNarrativeStage");
            if (startNarrativeMethod != null)
            {
                TaleWorlds.Library.Debug.Print(
                    "[QuickStart] CharacterCreationManagerPatch: Found StartNarrativeStage method",
                    0,
                    TaleWorlds.Library.Debug.DebugColor.Green);
                return startNarrativeMethod;
            }

            TaleWorlds.Library.Debug.Print(
                "[QuickStart] CharacterCreationManagerPatch: StartNarrativeStage method not found",
                0,
                TaleWorlds.Library.Debug.DebugColor.Red);
            return null;
        }

        // ✅ Postfix: 在 StartNarrativeStage 执行后调用，此时 CurrentMenu 已经设置
        // 按照 ChatGPT 要求：绑定 manager，不要每次都查找
        static void Postfix(object __instance)
        {
            try
            {
                if (!QuickStartHelper.AutoSkipCharCreation || QuickStartHelper.CharCreationDone)
                    return;

                // ✅ 按照 ChatGPT 要求：绑定 manager，后续使用绑定的 manager 而不是每次都查找
                QuickStartCharCreationSkipper.BindManager(__instance);
            }
            catch (Exception ex)
            {
                TaleWorlds.Library.Debug.Print(
                    "[QuickStart] StartNarrativeStage Postfix error: " + ex,
                    0,
                    TaleWorlds.Library.Debug.DebugColor.Red);
            }
        }
    }
}
