namespace QuickStartMod
{
    // 快速开局模式标志和辅助类
    public static class QuickStartHelper
    {
        // 是否通过“快速开始”按钮进入的新战役
        public static bool IsQuickStartMode { get; set; }

        // 是否在快速开始模式下自动跳过角色创建问卷
        public static bool AutoSkipCharCreation { get; set; }

        // 当前战役中是否已经完成一次自动角色创建（防止重复操作）
        public static bool CharCreationDone { get; set; }

        // 是否已经见过 CharacterCreationState（用于防止误判 done）
        public static bool SeenCharacterCreation { get; set; }

        // 是否已进入 NarrativeStage（CurrentMenu 已设置，可以安全地自动选择选项）
        public static bool InNarrative { get; set; }

        // 是否还有一笔待发放的启动资金
        public static bool PendingGold { get; set; }

        // 当前战役中是否已经发过启动资金
        public static bool GoldDone { get; set; }

        // 启动资金数额（第纳尔）
        public const int QuickStartGold = 100000;
    }
}

