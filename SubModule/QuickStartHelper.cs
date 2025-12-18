namespace QuickStartMod
{
    // 快速开局模式标志和辅助类
    public static class QuickStartHelper
    {
        // 是否通过“快速开始”按钮进入的新战役
        public static bool IsQuickStartMode { get; set; }

        // 是否还有一笔待发放的启动资金
        public static bool PendingGold { get; set; }

        // 当前战役中是否已经发过启动资金
        public static bool GoldDone { get; set; }

        // 启动资金数额（第纳尔）
        public const int QuickStartGold = 100000;
    }
}

