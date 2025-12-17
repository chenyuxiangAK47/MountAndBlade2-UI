using System;
using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.ViewModelCollection.InitialMenu;

namespace QuickStartMod
{
    // ❌ 彻底禁用 ViewModelMixin（按照 ChatGPT 建议，改用 ExecuteCommand + Harmony 补丁）
    /*
    [ViewModelMixin("InitialMenuVM")]
    public class QuickStartViewModelMixin : BaseViewModelMixin<InitialMenuVM>
    {
        public QuickStartViewModelMixin(InitialMenuVM vm) : base(vm)
        {
            // 立即输出，确保 ViewModelMixin 被创建时就能看到
            System.Diagnostics.Debug.WriteLine("[QuickStartMod] ViewModelMixin 构造函数被调用！");
            System.Console.WriteLine("[QuickStartMod] ViewModelMixin 构造函数被调用！");
            
            // 立即显示在游戏中
            try
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[QuickStartMod] 步骤 20: ViewModelMixin 构造函数被调用！", 
                    new TaleWorlds.Library.Color(1.0f, 0.0f, 1.0f)));
            }
            catch { }
            
            try
            {
                QuickStartLogger.LogStep(20, "QuickStartViewModelMixin 构造函数开始执行", true);
                QuickStartLogger.LogSuccess("ViewModelMixin 已创建并注入到 InitialMenuVM", true);
            }
            catch (Exception ex)
            {
                QuickStartLogger.LogError("ViewModelMixin 构造函数", ex, true);
            }
        }

        // 注入 ExecuteQuickStart 命令方法
        // [DataSourceMethod] 特性是必需的，用于标记可绑定的命令方法
        [DataSourceMethod]
        public void ExecuteQuickStart()
        {
            try
            {
                QuickStartLogger.LogStep(30, "========== 按钮被点击！==========", true);
                
                // 先显示提示信息，确认点击事件成功
                QuickStartLogger.LogStep(31, "显示提示信息");
                InformationManager.DisplayMessage(new InformationMessage(
                    "快速开始：正在启动新游戏...", 
                    new TaleWorlds.Library.Color(0.2f, 0.8f, 0.2f)));
                
                // 设置快速开始标志
                QuickStartLogger.LogStep(32, "设置快速开始标志");
                QuickStartHelper.IsQuickStartMode = true;
                
                // 启动新游戏（沙盒模式）- 使用反射调用 StartNewGame
                QuickStartLogger.LogStep(33, "开始获取 Game 类型");
                var gameType = typeof(TaleWorlds.Core.Game);
                QuickStartLogger.LogStep(34, "获取 Game.Current 属性");
                var currentProperty = gameType.GetProperty("Current");
                
                if (currentProperty == null)
                {
                    QuickStartLogger.LogError("获取 Game.Current 属性", new Exception("Game.Current 属性为 null"), true);
                    InformationManager.DisplayMessage(new InformationMessage(
                        "快速开始：无法启动游戏（找不到 Game.Current 属性）", 
                        new TaleWorlds.Library.Color(0.8f, 0.2f, 0.2f)));
                    return;
                }
                
                QuickStartLogger.LogStep(35, "获取 Game.Current 值");
                var game = currentProperty.GetValue(null);
                
                if (game != null)
                {
                    QuickStartLogger.LogStep(36, "查找 StartNewGame 方法");
                    var startNewGameMethod = gameType.GetMethod("StartNewGame", new[] { typeof(bool) });
                    if (startNewGameMethod != null)
                    {
                        QuickStartLogger.LogStep(37, "调用 StartNewGame(false) - 沙盒模式", true);
                        startNewGameMethod.Invoke(game, new object[] { false }); // false = 沙盒模式
                        QuickStartLogger.LogSuccess("StartNewGame 调用成功", true);
                    }
                    else
                    {
                        QuickStartLogger.LogError("查找 StartNewGame 方法", new Exception("找不到 StartNewGame 方法"), true);
                        InformationManager.DisplayMessage(new InformationMessage(
                            "快速开始：无法启动游戏（找不到 StartNewGame 方法）", 
                            new TaleWorlds.Library.Color(0.8f, 0.2f, 0.2f)));
                    }
                }
                else
                {
                    QuickStartLogger.LogError("获取 Game.Current", new Exception("Game.Current 为 null"), true);
                    InformationManager.DisplayMessage(new InformationMessage(
                        "快速开始：无法启动游戏（Game.Current 为 null）", 
                        new TaleWorlds.Library.Color(0.8f, 0.2f, 0.2f)));
                }
            }
            catch (Exception ex)
            {
                QuickStartLogger.LogError("ExecuteQuickStart 执行", ex, true);
                InformationManager.DisplayMessage(new InformationMessage(
                    $"快速开始：执行失败 - {ex.Message}", 
                    new TaleWorlds.Library.Color(0.8f, 0.2f, 0.2f)));
            }
        }
    }
    */
}

