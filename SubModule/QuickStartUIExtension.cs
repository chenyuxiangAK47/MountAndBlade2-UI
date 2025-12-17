using System;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using TaleWorlds.Library;

namespace QuickStartMod
{
    // ❌ 禁用 PrefabExtension（按照 ChatGPT 建议，改用 Harmony 注入菜单项，避免重叠）
    // 最稳方案是完全不用 UIExtenderEx 改 prefab，只通过 Harmony 往 MenuOptions 注入菜单项
    /*
    [PrefabExtension("InitialScreen", "descendant::NavigatableListPanel[@Id='MenuItems']")]
    internal class QuickStartMenuButtonExtension : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Prepend; // 添加到菜单顶部
        private XmlDocument _document;

        public QuickStartMenuButtonExtension()
        {
            // 立即输出，确保 PrefabExtension 被创建时就能看到
            System.Diagnostics.Debug.WriteLine("[QuickStartMod] PrefabExtension 构造函数被调用！");
            System.Console.WriteLine("[QuickStartMod] PrefabExtension 构造函数被调用！");
            
            // 立即显示在游戏中
            try
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[QuickStartMod] 步骤 10: PrefabExtension 构造函数被调用！", 
                    new TaleWorlds.Library.Color(0.0f, 1.0f, 1.0f)));
            }
            catch { }
            
            try
            {
                QuickStartLogger.LogStep(10, "QuickStartMenuButtonExtension 构造函数开始执行", true);
                
                _document = new XmlDocument();
            // 在 MenuItems 列表的开头添加快速开始按钮
            // 按钮结构完全按照原版菜单项设计，确保样式一致
            // 使用 ExecuteCommand + CommandParameter.Click 传递参数，通过 Harmony 补丁拦截
            _document.LoadXml(@"
                <Widget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""CoverChildren"" HorizontalAlignment=""Center"" MarginTop=""16"" MarginBottom=""16"">
                    <Children>
                        <ButtonWidget 
                            DoNotPassEventsToChildren=""true"" 
                            UpdateChildrenStates=""true"" 
                            WidthSizePolicy=""StretchToParent"" 
                            HeightSizePolicy=""CoverChildren"" 
                            HorizontalAlignment=""Center"" 
                            VerticalAlignment=""Center"" 
                            Command.Click=""ExecuteCommand""
                            CommandParameter.Click=""QuickStart""
                            IsDisabled=""false""
                            IsVisible=""true""
                            IsHitTestVisible=""true"">
                            <Children>
                                <ListPanel 
                                    UpdateChildrenStates=""true"" 
                                    WidthSizePolicy=""CoverChildren"" 
                                    HeightSizePolicy=""CoverChildren"" 
                                    StackLayout.LayoutMethod=""HorizontalCentered"" 
                                    HorizontalAlignment=""Center"" 
                                    VerticalAlignment=""Center"">
                                    <Children>
                                        <ImageWidget 
                                            WidthSizePolicy=""Fixed"" 
                                            HeightSizePolicy=""Fixed"" 
                                            SuggestedWidth=""46"" 
                                            SuggestedHeight=""20"" 
                                            HorizontalAlignment=""Left"" 
                                            VerticalAlignment=""Center"" 
                                            PositionYOffset=""-2"" 
                                            Brush=""HoverIndicatorBrush"" />
                                        <TextWidget 
                                            WidthSizePolicy=""CoverChildren"" 
                                            HeightSizePolicy=""CoverChildren"" 
                                            MaxWidth=""320"" 
                                            HorizontalAlignment=""Center"" 
                                            MarginLeft=""8"" 
                                            MarginRight=""8"" 
                                            Brush=""InitialMenuButtonBrush"" 
                                            ClipContents=""false"" 
                                            Text=""快速开始"" 
                                            CanBreakWords=""false"" />
                                        <ImageWidget 
                                            WidthSizePolicy=""Fixed"" 
                                            HeightSizePolicy=""Fixed"" 
                                            SuggestedWidth=""46"" 
                                            SuggestedHeight=""20"" 
                                            HorizontalAlignment=""Right"" 
                                            VerticalAlignment=""Center"" 
                                            PositionYOffset=""-2"" 
                                            Brush=""HoverIndicatorBrushFlipped"" />
                                    </Children>
                                </ListPanel>
                            </Children>
                        </ButtonWidget>
                    </Children>
                </Widget>");
                
                QuickStartLogger.LogSuccess("PrefabExtension XML 文档创建成功");
            }
            catch (Exception ex)
            {
                QuickStartLogger.LogError("PrefabExtension 构造函数", ex, true);
                // 创建一个空的 XML 文档，避免返回 null
                _document = new XmlDocument();
                _document.LoadXml("<Widget/>");
            }
        }

        [PrefabExtensionXmlDocument]
        public XmlDocument GetPrefabExtension() => _document;
    }
    */
}

