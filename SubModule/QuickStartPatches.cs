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
    // ✅ 按照 ChatGPT 建议：Patch 多个刷新点，只在 MenuOptions.Count > 0 时插入
    // 解决"插入成功但不显示"的问题
    [HarmonyPatch(typeof(InitialMenuVM))]
    public static class QuickStartMenuInjectPatch
    {
        // Patch 多个"可能会在填充菜单项后被调用"的点
        public static IEnumerable<MethodBase> TargetMethods()
        {
            // 1) 构造函数（有些版本会在 ctor 里做第一次 refresh）
            var ctor = AccessTools.Constructor(typeof(InitialMenuVM));
            if (ctor != null)
            {
                TaleWorlds.Library.Debug.Print("[QuickStart] TargetMethods: 找到构造函数", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                yield return ctor;
            }

            // 2) RefreshValues（保留，但不再在 Count=0 时插）
            var rv = AccessTools.Method(typeof(InitialMenuVM), "RefreshValues");
            if (rv != null)
            {
                TaleWorlds.Library.Debug.Print("[QuickStart] TargetMethods: 找到 RefreshValues", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                yield return rv;
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
                    TaleWorlds.Library.Debug.Print($"[QuickStart] TargetMethods: 找到方法 {n}", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                    yield return m;
                }
            }
        }

        public static void Postfix(InitialMenuVM __instance, MethodBase __originalMethod)
        {
            try
            {
                TaleWorlds.Library.Debug.Print($"[QuickStart] Postfix hit: {__originalMethod?.Name}, MenuOptions.Count={__instance?.MenuOptions?.Count ?? 0}", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                EnsureQuickStart(__instance);
            }
            catch (Exception ex)
            {
                TaleWorlds.Library.Debug.Print($"[QuickStart] Postfix error: {ex}", 0, TaleWorlds.Library.Debug.DebugColor.Red);
                TaleWorlds.Library.Debug.Print($"[QuickStart] StackTrace: {ex.StackTrace}", 0, TaleWorlds.Library.Debug.DebugColor.Red);
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

            // 检查是否已存在（用 NameText 检测）
            foreach (var opt in vm.MenuOptions)
            {
                try
                {
                    var nameText = opt?.NameText;
                    if (nameText != null && nameText.ToString().Contains("快速开始"))
                    {
                        TaleWorlds.Library.Debug.Print("[QuickStart] EnsureQuickStart: 菜单项已存在，跳过", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
                        return;
                    }
                }
                catch { }
            }

            TaleWorlds.Library.Debug.Print("[QuickStart] EnsureQuickStart: 开始创建菜单项...", 0, TaleWorlds.Library.Debug.DebugColor.Green);

            // 创建 TextObject
            var name = new TextObject("{=qs_quickstart}快速开始");
            var hint = new TextObject("{=qs_quickstart_hint}直接进入沙盒并给予初始资源");

            // 创建委托
            Func<(bool, TextObject)> isDisabledAndReason = () => (false, new TextObject(""));
            Func<bool> isHidden = () => false;

            // 点击动作
            Action action = () =>
            {
                try
                {
                    TaleWorlds.Library.Debug.Print("[QuickStart] ========== 按钮被点击！==========", 0, TaleWorlds.Library.Debug.DebugColor.Green);
                    InformationManager.DisplayMessage(new InformationMessage(
                        "快速开始：正在启动新游戏...",
                        new TaleWorlds.Library.Color(0.2f, 0.8f, 0.2f)));

                    QuickStartHelper.IsQuickStartMode = true;

                    // 启动新游戏（沙盒模式）
                    var gameType = typeof(TaleWorlds.Core.Game);
                    var currentProperty = gameType.GetProperty("Current");
                    if (currentProperty != null)
                    {
                        var game = currentProperty.GetValue(null);
                        if (game != null)
                        {
                            var startNewGameMethod = gameType.GetMethod("StartNewGame", new[] { typeof(bool) });
                            if (startNewGameMethod != null)
                            {
                                startNewGameMethod.Invoke(game, new object[] { false }); // false = 沙盒模式
                                TaleWorlds.Library.Debug.Print("[QuickStart] StartNewGame 调用成功", 0, TaleWorlds.Library.Debug.DebugColor.Green);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    TaleWorlds.Library.Debug.Print($"[QuickStart] 快速开始执行失败: {ex}", 0, TaleWorlds.Library.Debug.DebugColor.Red);
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"快速开始：执行失败 - {ex.Message}",
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

            TaleWorlds.Library.Debug.Print($"[QuickStart] EnsureQuickStart: 注入完成。现在 MenuOptions.Count={vm.MenuOptions.Count}", 0, TaleWorlds.Library.Debug.DebugColor.Green);
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
                TaleWorlds.Library.Debug.Print($"[QuickStart] NotifyMenuOptionsChanged 失败: {ex.Message}", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
            }
        }
    }
}
