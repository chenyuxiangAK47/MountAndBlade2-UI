using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem;

namespace QuickStartMod
{
    // 角色创建阶段“自动驾驶”工具：
    // - 只在 QuickStartHelper.AutoSkipCharCreation 为 true 时工作
    // - 进入 CharacterCreation 状态后，自动：
    //   1）在当前页面里选一个选项（文化页尽量选瓦兰迪亚）
    //   2）调用 Next/Continue/Done 等推进方法
    // - 一旦离开 CharacterCreation 状态，就标记 CharCreationDone，整个流程结束
    internal static class QuickStartCharCreationSkipper
    {
        // ✅ 按照 ChatGPT 要求：绑定 manager，不要每次都查找
        private static object _boundManager = null;
        
        private static float _cooldown;
        private static int _lastStepHash;
        private static string _lastStateName;
        private static bool _startNarrativeCalled = false; // 确保 StartNarrativeStage 只调用一次
        private static int _phase = 0; // 阶段：0=设置文化, 1=推进阶段, 2=选择选项

        // ✅ 按照 ChatGPT 要求：在 StartNarrativeStage Postfix 中调用，绑定 manager
        public static void BindManager(object manager)
        {
            _boundManager = manager;
            QuickStartHelper.InNarrative = true;
            QuickStartHelper.SeenCharacterCreation = true;
            _cooldown = 0.2f; // 等待 0.2s 让 CurrentMenu 完全初始化
            WriteFileLog("[QuickStart] CharCreation: BindManager() called, manager bound");
            TaleWorlds.Library.Debug.Print(
                "[QuickStart] CC: StartNarrativeStage postfix, CurrentMenu ready, manager bound",
                0,
                TaleWorlds.Library.Debug.DebugColor.Green);
        }

        // 重置所有状态（在按钮点击时调用）
        public static void ResetState()
        {
            _boundManager = null; // ✅ 重置绑定的 manager
            _cooldown = 0f;
            _lastStepHash = 0;
            _lastStateName = null;
            _startNarrativeCalled = false;
            _phase = 0;
            QuickStartHelper.InNarrative = false; // 重置 Narrative 标志
            WriteFileLog("[QuickStart] CharCreation: ResetState() called");
        }

        // ✅ 按照 ChatGPT 要求：不要用 OnApplicationTick 猜，而是使用绑定的 manager
        // 每帧最多做一件事（选项 OR Next），加 cooldown，避免同帧连推导致状态机不一致
        public static void RunOnCharCreationState(object stateInstance, float dt)
        {
            if (!QuickStartHelper.AutoSkipCharCreation || QuickStartHelper.CharCreationDone)
                return;

            // ✅ 如果还没绑定 manager，只设置文化，不推进阶段
            if (_boundManager == null)
            {
                // 还没进入 NarrativeStage，只设置文化
                try
                {
                    Type stateType = stateInstance.GetType();
                    const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                    PropertyInfo managerProp = stateType.GetProperty("CharacterCreationManager", BF);
                    if (managerProp != null && managerProp.CanRead)
                    {
                        object manager = managerProp.GetValue(stateInstance, null);
                        if (manager != null)
                        {
                            // 只设置文化，不推进阶段（按照 ChatGPT 要求：先只做文化，确认不崩）
                            if (_phase == 0)
                            {
                                bool cultureSet = TrySetCulture(manager);
                                if (cultureSet)
                                {
                                    _phase = 1;
                                    WriteFileLog("[QuickStart] CharCreation: Culture set, waiting for StartNarrativeStage");
                                }
                            }
                        }
                    }
                }
                catch { }
                return; // 等待 StartNarrativeStage 被调用并绑定 manager
            }

            // ✅ 按照 ChatGPT 要求：每帧最多做一件事，必须 cooldown，禁止同帧多步推进
            _cooldown -= dt;
            if (_cooldown > 0f)
                return;

            try
            {
                // ✅ 使用绑定的 manager，不要每次都查找
                object manager = _boundManager;
                if (manager == null)
                {
                    return;
                }

                // ✅ 关键：检查 CurrentMenu 是否真的存在（StartNarrativeStage 应该已经设置了它）
                Type managerType = manager.GetType();
                const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                PropertyInfo currentMenuProp = managerType.GetProperty("CurrentMenu", BF);
                if (currentMenuProp == null || !currentMenuProp.CanRead)
                {
                    _cooldown = 0.15f;
                    return;
                }

                object currentMenu = currentMenuProp.GetValue(manager, null);
                if (currentMenu == null)
                {
                    // CurrentMenu 仍然为 null，说明 StartNarrativeStage 可能还没完成
                    // 继续等待，不要强行推进
                    _cooldown = 0.15f;
                    return;
                }

                // 标记已见过角色创建状态
                if (!QuickStartHelper.SeenCharacterCreation)
                {
                    QuickStartHelper.SeenCharacterCreation = true;
                    try
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            "[QS MOD] 自动跳过角色创建中...",
                            new TaleWorlds.Library.Color(0.2f, 0.8f, 0.2f)));
                    }
                    catch { }
                    WriteFileLog("[QuickStart] CharCreation: InNarrative=true, CurrentMenu!=null, starting auto-select");
                }

                // 检查当前步骤，避免重复操作
                int stepHash = ComputeStepHashForManager(manager);
                if (stepHash == _lastStepHash && _cooldown > 0f)
                {
                    _cooldown -= dt;
                    return;
                }

                _lastStepHash = stepHash;

                // ✅ 按照 ChatGPT 要求：每帧最多做一件事（选项 OR Next），禁止同帧多步推进
                // 先选择选项，下一帧再切换菜单
                if (_phase == 2)
                {
                    // Phase 2: 选择选项
                    bool optionSelected = TrySelectCurrentMenuOption(manager);
                    if (optionSelected)
                    {
                        _phase = 3; // 下一阶段：切换菜单
                        _cooldown = 0.15f; // 等待 0.15s 让选项生效
                        return;
                    }
                }
                else if (_phase == 3)
                {
                    // Phase 3: 切换菜单
                    bool switched = TrySwitchToNextMenu(manager);
                    if (switched)
                    {
                        _phase = 2; // 回到选择选项阶段
                        _cooldown = 0.20f;
                        return;
                    }
                    else
                    {
                        // 切换失败，可能是菜单结束了，等待下一阶段
                        _phase = 4; // 问卷结束
                        _cooldown = 0.25f;
                        WriteFileLog("[QuickStart] CharCreation: Narrative menu ended, waiting for NextStage");
                    }
                }
                else if (_phase < 2)
                {
                    // 还没到选择阶段，继续等待
                    _phase = 2;
                    _cooldown = 0.1f;
                }
                else
                {
                    // Phase 4: 问卷结束，等待 NextStage
                    _cooldown = 0.25f;
                }
            }
            catch (Exception ex)
            {
                var logMsg = "[QuickStart] CharCreation: RunOnCharCreationState error: " + ex.Message;
                Debug.Print(logMsg, 0, Debug.DebugColor.Red);
                WriteFileLog(logMsg);
                _cooldown = 0.25f;
            }
        }

        // ⚠️ 保留旧的 Tick 方法作为备用（通过 OnApplicationTick 调用）
        // 但主要逻辑应该通过 Harmony Patch 调用 RunOnCharCreationState
        public static void Tick(float dt)
        {
            // 添加详细日志，确认是否进入了 Tick 方法
            if (!QuickStartHelper.AutoSkipCharCreation || QuickStartHelper.CharCreationDone)
            {
                // 只在第一次跳过时记录日志，避免日志过多
                if (_lastStepHash == 0)
                {
                    var logMsg = string.Format(
                        "[QuickStart] CharCreation: Tick skipped - AutoSkip={0}, Done={1}",
                        QuickStartHelper.AutoSkipCharCreation,
                        QuickStartHelper.CharCreationDone);
                    Debug.Print(logMsg, 0, Debug.DebugColor.Yellow);
                    WriteFileLog(logMsg);
                }
                return;
            }

            _cooldown -= dt;
            if (_cooldown > 0f)
                return;

            Game game = Game.Current;
            if (game == null)
            {
                if (_lastStepHash == 0)
                {
                    WriteFileLog("[QuickStart] CharCreation: Tick - Game.Current is null");
                }
                return;
            }

            var gsm = game.GameStateManager;
            var state = gsm != null ? gsm.ActiveState : null;
            if (state == null)
            {
                if (_lastStepHash == 0)
                {
                    WriteFileLog("[QuickStart] CharCreation: Tick - ActiveState is null");
                }
                return;
            }

            LogStateIfChanged(state);

            var stateType = state.GetType();
            var stateTypeName = stateType.FullName ?? stateType.Name ?? string.Empty;

            // 检查是否是角色创建状态
            bool isCharCreation = stateTypeName.IndexOf("CharacterCreationState", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 stateTypeName.IndexOf("CharacterCreation", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isCharCreation)
            {
                // 关键修复：没见过角色创建前，任何状态都不能算 done
                if (!QuickStartHelper.SeenCharacterCreation)
                {
                    // 还没见过角色创建，继续等待
                    _cooldown = 0.25f;
                    return;
                }

                // 见过角色创建后，只有进入 MapState 才算真正完成
                if (stateTypeName.IndexOf("MapState", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (!QuickStartHelper.CharCreationDone)
                    {
                        QuickStartHelper.CharCreationDone = true;
                        QuickStartHelper.AutoSkipCharCreation = false;
                        var logMsg = "[QuickStart] CharCreation: done (entered MapState)";
                        Debug.Print(logMsg, 0, Debug.DebugColor.Green);
                        WriteFileLog(logMsg);
                    }
                }
                _cooldown = 0.25f;
                return;
            }

            // 走到这里才是真正进了角色创建状态
            QuickStartHelper.SeenCharacterCreation = true;

            // ✅ 按照 ChatGPT 要求：检测当前 Stage 并自动跳过
            // 先尝试获取 manager 并检测当前 Stage
            try
            {
                Type stateTypeLocal = state.GetType();
                const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                PropertyInfo managerProp = stateTypeLocal.GetProperty("CharacterCreationManager", BF);
                if (managerProp != null && managerProp.CanRead)
                {
                    object manager = managerProp.GetValue(state, null);
                    if (manager != null)
                    {
                        // ✅ 检测当前 Stage 并自动跳过
                        AutoSkipCurrentStage(manager, dt);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                WriteFileLog("[QuickStart] CharCreation: Failed to get manager in Tick: " + ex.Message);
            }

            // ✅ 如果 AutoSkipCurrentStage 没有处理，调用 RunOnCharCreationState（它会检查 InNarrative 和 CurrentMenu）
            RunOnCharCreationState(state, dt);
        }

        private static void LogStateIfChanged(object state)
        {
            try
            {
                Type t = state.GetType();
                string name = t.FullName;
                if (string.IsNullOrEmpty(name))
                {
                    name = t.Name;
                }

                if (name == _lastStateName)
                {
                    return;
                }

                _lastStateName = name;

                var logMsg = "[QuickStart] ActiveState = " + name;
                Debug.Print(logMsg, 0, Debug.DebugColor.Yellow);
                
                // 同时写入文件日志
                WriteFileLog(logMsg);
            }
            catch
            {
                // 任何日志失败都忽略
            }
        }

        // 文件日志辅助方法
        private static void WriteFileLog(string message)
        {
            try
            {
                var logPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "Modules", "QuickStartMod", "qs_runtime.log");
                var logDir = System.IO.Path.GetDirectoryName(logPath);
                if (!System.IO.Directory.Exists(logDir))
                    System.IO.Directory.CreateDirectory(logDir);
                
                var logMsg = string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] {1}\n", System.DateTime.Now, message);
                System.IO.File.AppendAllText(logPath, logMsg);
            }
            catch
            {
                // 文件日志失败不影响主流程
            }
        }

        // 查找 CharacterCreationManager 对象（角色创建的核心管理器）
        // 根据反编译结果：CharacterCreationState 有 CharacterCreationManager 属性
        private static object FindCharacterCreationManager(object state)
        {
            if (state == null)
                return null;

            Type stateType = state.GetType();
            string stateTypeName = stateType.FullName ?? stateType.Name ?? string.Empty;

            // 检查是否是 CharacterCreationState
            if (stateTypeName.IndexOf("CharacterCreation", StringComparison.OrdinalIgnoreCase) < 0)
                return null;

            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // 查找 CharacterCreationManager 属性（根据反编译结果，这是 public 属性）
            PropertyInfo managerProp = stateType.GetProperty("CharacterCreationManager", BF);
            if (managerProp != null && managerProp.CanRead)
            {
                try
                {
                    object manager = managerProp.GetValue(state, null);
                    if (manager != null)
                    {
                        var logMsg = "[QuickStart] CharCreation: found CharacterCreationManager via property";
                        Debug.Print(logMsg, 0, Debug.DebugColor.Green);
                        WriteFileLog(logMsg);
                        return manager;
                    }
                }
                catch (Exception ex)
                {
                    var logMsg = "[QuickStart] CharCreation: failed to get CharacterCreationManager: " + ex.Message;
                    Debug.Print(logMsg, 0, Debug.DebugColor.Yellow);
                    WriteFileLog(logMsg);
                }
            }

            // 备用：查找字段
            FieldInfo[] fields = stateType.GetFields(BF);
            foreach (FieldInfo f in fields)
            {
                Type fieldType = f.FieldType;
                string fieldTypeName = fieldType.FullName ?? fieldType.Name ?? string.Empty;

                if (fieldTypeName.IndexOf("CharacterCreationManager", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    try
                    {
                        object manager = f.GetValue(state);
                        if (manager != null)
                        {
                            var logMsg = "[QuickStart] CharCreation: found CharacterCreationManager via field " + f.Name;
                            Debug.Print(logMsg, 0, Debug.DebugColor.Green);
                            WriteFileLog(logMsg);
                            return manager;
                        }
                    }
                    catch { }
                }
            }

            return null;
        }

        // 递归在 State 对象内部查找"看起来像角色创建 VM"的对象（保留作为备用）
        private static object FindVmDeep(object root, int depth)
        {
            if (root == null || depth < 0)
            {
                return null;
            }

            Type t = root.GetType();
            string tn = t.FullName;
            if (string.IsNullOrEmpty(tn))
            {
                tn = t.Name;
            }

            string lower = tn.ToLowerInvariant();

            // 命中条件：类型名里包含 vm/viewmodel，且含有 character 或 creation 关键字
            if ((lower.IndexOf("vm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 lower.IndexOf("viewmodel", StringComparison.OrdinalIgnoreCase) >= 0) &&
                (lower.IndexOf("character", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 lower.IndexOf("creation", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                Debug.Print(
                    "[QuickStart] CharCreation: VM candidate = " + tn,
                    0,
                    Debug.DebugColor.Yellow);
                return root;
            }

            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // 扫描属性
            PropertyInfo[] props = t.GetProperties(BF);
            int i;
            for (i = 0; i < props.Length; i++)
            {
                PropertyInfo p = props[i];
                if (!p.CanRead)
                {
                    continue;
                }

                object v = null;
                try
                {
                    v = p.GetValue(root, null);
                }
                catch
                {
                }

                if (v == null)
                {
                    continue;
                }

                object found = FindVmDeep(v, depth - 1);
                if (found != null)
                {
                    return found;
                }
            }

            // 扫描字段
            FieldInfo[] fields = t.GetFields(BF);
            for (i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                object v = null;
                try
                {
                    v = f.GetValue(root);
                }
                catch
                {
                }

                if (v == null)
                {
                    continue;
                }

                object found = FindVmDeep(v, depth - 1);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        // 旧版一层查找方法暂时保留（目前未调用），防止后续需要做对比
        private static object FindCharacterCreationVm(object state)
        {
            Type t = state.GetType();
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // 先找字段
            foreach (FieldInfo f in t.GetFields(BF))
            {
                object v = f.GetValue(state);
                if (v == null)
                    continue;

                string n = v.GetType().Name.ToLowerInvariant();
                if (n.IndexOf("character", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    n.IndexOf("creation", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    n.IndexOf("vm", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return v;
                }
            }

            // 再找属性
            foreach (PropertyInfo p in t.GetProperties(BF))
            {
                if (!p.CanRead)
                    continue;

                object v = null;
                try
                {
                    v = p.GetValue(state, null);
                }
                catch
                {
                }

                if (v == null)
                    continue;

                string n = v.GetType().Name.ToLowerInvariant();
                if (n.IndexOf("character", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    n.IndexOf("creation", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    n.IndexOf("vm", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return v;
                }
            }

            return null;
        }

        private static int ComputeStepHash(object vm)
        {
            try
            {
                Type t = vm.GetType();
                string title = ReadAnyStringLike(t, vm, "Title", "CurrentStageTitle", "CurrentTitle");
                if (title == null)
                    title = string.Empty;

                IEnumerable opts = GetOptionsEnumerable(vm);
                int optCount = 0;
                if (opts != null)
                {
                    foreach (object _ in opts)
                    {
                        optCount++;
                    }
                }

                bool canNext = ReadAnyBoolLike(t, vm, "IsNextEnabled", "CanAdvance", "CanContinue");

                // 日志：当前角色创建步骤的基本信息
                Debug.Print(
                    "[QuickStart] CharCreation: step info => Title=\"" + title + "\", Options=" + optCount + ", CanAdvance=" + canNext,
                    0,
                    Debug.DebugColor.Yellow);

                unchecked
                {
                    int hash = title.GetHashCode();
                    hash = hash * 397 ^ optCount;
                    hash = hash * 397 ^ (canNext ? 1 : 0);
                    return hash;
                }
            }
            catch
            {
                // 出现异常就返回一个变化比较大的值，避免卡死
                return Environment.TickCount;
            }
        }

        private static void TrySelectOption(object vm)
        {
            IEnumerable opts = GetOptionsEnumerable(vm);
            if (opts == null)
                return;

            List<object> list = new List<object>();
            foreach (object o in opts)
            {
                list.Add(o);
            }

            if (list.Count == 0)
                return;

            object chosen = ChooseVlandiaIfPossible(list);
            if (chosen == null)
            {
                chosen = list[0];
            }

            Type vt = vm.GetType();
            MethodInfo[] mt = vt.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            MethodInfo selectMethod = null;
            foreach (MethodInfo m in mt)
            {
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length != 1)
                    continue;

                if (!ps[0].ParameterType.IsAssignableFrom(chosen.GetType()))
                    continue;

                string name = m.Name.ToLowerInvariant();
                if (name.IndexOf("select", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("option", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    selectMethod = m;
                    break;
                }
            }

            if (selectMethod != null)
            {
                try
                {
                    selectMethod.Invoke(vm, new object[] { chosen });
                    Debug.Print(
                        "[QuickStart] CharCreation: option selected via " + selectMethod.Name,
                        0,
                        Debug.DebugColor.Green);
                }
                catch
                {
                    // 选择失败不影响主流程
                }

                return;
            }

            // 没有“选择方法”就尝试直接把选项的 IsSelected 设为 true
            TrySetBoolProperty(chosen, "IsSelected", true);
        }

        private static IEnumerable GetOptionsEnumerable(object vm)
        {
            Type t = vm.GetType();
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // 先扫属性
            PropertyInfo[] props = t.GetProperties(BF);
            int i;
            for (i = 0; i < props.Length; i++)
            {
                PropertyInfo p = props[i];
                if (!p.CanRead)
                    continue;

                if (!typeof(IEnumerable).IsAssignableFrom(p.PropertyType))
                    continue;

                string n = p.Name.ToLowerInvariant();
                if (n.IndexOf("option", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                try
                {
                    IEnumerable v = p.GetValue(vm, null) as IEnumerable;
                    if (v != null)
                        return v;
                }
                catch
                {
                }
            }

            // 再扫字段，兼容某些把集合藏在字段里的实现
            FieldInfo[] fields = t.GetFields(BF);
            for (i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                if (!typeof(IEnumerable).IsAssignableFrom(f.FieldType))
                {
                    continue;
                }

                string n = f.Name.ToLowerInvariant();
                if (n.IndexOf("option", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                try
                {
                    IEnumerable v = f.GetValue(vm) as IEnumerable;
                    if (v != null)
                    {
                        return v;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static object ChooseVlandiaIfPossible(List<object> options)
        {
            foreach (object o in options)
            {
                Type ot = o.GetType();
                foreach (PropertyInfo p in ot.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!p.CanRead || p.PropertyType != typeof(string))
                        continue;

                    string s = null;
                    try
                    {
                        s = (string)p.GetValue(o, null);
                    }
                    catch
                    {
                    }

                    if (string.IsNullOrEmpty(s))
                        continue;

                    string low = s.ToLowerInvariant();
                    if (low.IndexOf("vlandia", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.IndexOf("瓦兰迪亚", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return o;
                    }
                }

                try
                {
                    string ts = o.ToString();
                    if (!string.IsNullOrEmpty(ts))
                    {
                        string low2 = ts.ToLowerInvariant();
                        if (low2.IndexOf("vlandia", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ts.IndexOf("瓦兰迪亚", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return o;
                        }
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static bool TryInvokeAdvance(object vm)
        {
            Type t = vm.GetType();
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo[] methods = t.GetMethods(BF);

            string[] prefer = new string[]
            {
                "executenext",
                "executecontinue",
                "executefinalize",
                "executedone",
                "executeconfirm",
                "onnext",
                "continue",
                "finalize",
                "done"
            };

            MethodInfo pick = null;
            int i;

            // 1) 先找无参方法
            for (i = 0; i < prefer.Length; i++)
            {
                string key = prefer[i];
                int j;
                for (j = 0; j < methods.Length; j++)
                {
                    MethodInfo m = methods[j];
                    if (m.GetParameters().Length != 0)
                    {
                        continue;
                    }

                    string name = m.Name.ToLowerInvariant();
                    if (name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        pick = m;
                        break;
                    }
                }

                if (pick != null)
                {
                    break;
                }
            }

            if (pick != null)
            {
                try
                {
                    pick.Invoke(vm, null);
                    Debug.Print(
                        "[QuickStart] CharCreation: advance via " + pick.Name,
                        0,
                        Debug.DebugColor.Green);
                    return true;
                }
                catch
                {
                    // 失败则继续尝试 Command 路线
                }
            }

            // 2) 尝试属性里的 Action / ICommand（例如 NextCommand）
            PropertyInfo[] props = t.GetProperties(BF);
            for (i = 0; i < props.Length; i++)
            {
                PropertyInfo p = props[i];
                if (!p.CanRead)
                {
                    continue;
                }

                string n = p.Name.ToLowerInvariant();
                if (n.IndexOf("next", StringComparison.OrdinalIgnoreCase) < 0 &&
                    n.IndexOf("continue", StringComparison.OrdinalIgnoreCase) < 0 &&
                    n.IndexOf("done", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                object cmd = null;
                try
                {
                    cmd = p.GetValue(vm, null);
                }
                catch
                {
                }

                if (cmd == null)
                {
                    continue;
                }

                if (TryExecuteCommandObject(cmd))
                {
                    Debug.Print(
                        "[QuickStart] CharCreation: advance via command property " + p.Name,
                        0,
                        Debug.DebugColor.Green);
                    return true;
                }
            }

            // 3) 尝试字段里的 Action / ICommand
            FieldInfo[] fields = t.GetFields(BF);
            for (i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                string n = f.Name.ToLowerInvariant();
                if (n.IndexOf("next", StringComparison.OrdinalIgnoreCase) < 0 &&
                    n.IndexOf("continue", StringComparison.OrdinalIgnoreCase) < 0 &&
                    n.IndexOf("done", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                object cmd = null;
                try
                {
                    cmd = f.GetValue(vm);
                }
                catch
                {
                }

                if (cmd == null)
                {
                    continue;
                }

                if (TryExecuteCommandObject(cmd))
                {
                    Debug.Print(
                        "[QuickStart] CharCreation: advance via command field " + f.Name,
                        0,
                        Debug.DebugColor.Green);
                    return true;
                }
            }

            return false;
        }

        // 尝试执行一个“命令对象”：
        // - 如果是 System.Action：直接 Invoke()
        // - 如果有 Execute()/ExecuteAction() 等无参方法：尝试调用
        private static bool TryExecuteCommandObject(object cmd)
        {
            if (cmd == null)
            {
                return false;
            }

            // System.Action
            System.Action act = cmd as System.Action;
            if (act != null)
            {
                try
                {
                    act();
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            // 有些 ICommand 类型有 Execute(object) / Execute() 之类
            Type t = cmd.GetType();
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo[] methods = t.GetMethods(BF);
            int i;

            for (i = 0; i < methods.Length; i++)
            {
                MethodInfo m = methods[i];
                string name = m.Name.ToLowerInvariant();
                if (name.IndexOf("execute", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                ParameterInfo[] ps = m.GetParameters();
                try
                {
                    if (ps.Length == 0)
                    {
                        m.Invoke(cmd, null);
                        return true;
                    }

                    if (ps.Length == 1)
                    {
                        m.Invoke(cmd, new object[] { null });
                        return true;
                    }
                }
                catch
                {
                    // 继续尝试其他 Execute
                }
            }

            return false;
        }

        private static string ReadAnyStringLike(Type t, object obj, params string[] names)
        {
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (string n in names)
            {
                PropertyInfo p = t.GetProperty(n, BF);
                if (p != null && p.CanRead && p.PropertyType == typeof(string))
                {
                    try
                    {
                        return (string)p.GetValue(obj, null);
                    }
                    catch
                    {
                    }
                }
            }

            return null;
        }

        private static bool ReadAnyBoolLike(Type t, object obj, params string[] names)
        {
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (string n in names)
            {
                PropertyInfo p = t.GetProperty(n, BF);
                if (p != null && p.CanRead && p.PropertyType == typeof(bool))
                {
                    try
                    {
                        return (bool)p.GetValue(obj, null);
                    }
                    catch
                    {
                    }
                }
            }

            // 默认当作“可以推进”，宁可多点一次 Next，也不要卡死不动
            return true;
        }

        private static void TrySetBoolProperty(object obj, string propName, bool value)
        {
            if (obj == null)
                return;

            try
            {
                PropertyInfo p = obj.GetType().GetProperty(
                    propName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (p != null && p.CanWrite && p.PropertyType == typeof(bool))
                {
                    p.SetValue(obj, value, null);
                }
            }
            catch
            {
            }
        }

        // ========== CharacterCreationContent 专用方法 ==========

        // ✅ 按照 ChatGPT 要求：检测当前 Stage 并自动跳过
        // 根据 Stage 类型执行相应操作：
        // - CultureStage: 设置文化后调用 NextStage()
        // - FaceGeneratorStage: 直接调用 NextStage() 跳过
        // - NarrativeStage: 使用现有的自动选择逻辑
        // - 其他 Stage: 直接调用 NextStage() 跳过
        private static void AutoSkipCurrentStage(object manager, float dt)
        {
            try
            {
                Type managerType = manager.GetType();
                const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                // 1) 获取 CurrentStage
                PropertyInfo currentStageProp = managerType.GetProperty("CurrentStage", BF);
                if (currentStageProp == null || !currentStageProp.CanRead)
                {
                    // 如果获取不到 CurrentStage，使用旧的逻辑
                    return;
                }

                object currentStage = currentStageProp.GetValue(manager, null);
                if (currentStage == null)
                {
                    // CurrentStage 为 null，可能还在初始化，等待下一帧
                    _cooldown = 0.1f;
                    return;
                }

                Type stageType = currentStage.GetType();
                string stageTypeName = stageType.Name;

                // 2) 根据 Stage 类型执行相应操作
                if (stageTypeName.IndexOf("CultureStage", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Stage 0: 文化选择阶段
                    if (_phase == 0)
                    {
                        bool cultureSet = TrySetCulture(manager);
                        if (cultureSet)
                        {
                            _phase = 1;
                            _cooldown = 0.2f; // 等待 0.2s 让 UI 更新
                            WriteFileLog("[QuickStart] CharCreation: Culture set, will call NextStage() next frame");
                        }
                    }
                    else if (_phase == 1)
                    {
                        // 文化已设置，调用 NextStage() 进入下一个阶段
                        TryInvokeNextStage(manager);
                        _phase = 2; // 进入等待阶段
                        _cooldown = 0.2f;
                    }
                }
                else if (stageTypeName.IndexOf("FaceGeneratorStage", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Stage 1: 捏脸阶段 - 直接跳过
                    if (_cooldown <= 0f)
                    {
                        TryInvokeNextStage(manager);
                        _cooldown = 0.2f;
                        WriteFileLog("[QuickStart] CharCreation: Skipped FaceGeneratorStage");
                    }
                }
                else if (stageTypeName.IndexOf("NarrativeStage", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Stage 2: 问卷阶段 - 使用现有的自动选择逻辑
                    // 如果 manager 还没绑定，先绑定
                    if (_boundManager == null)
                    {
                        _boundManager = manager;
                        QuickStartHelper.InNarrative = true;
                        _cooldown = 0.2f; // 等待 0.2s 让 CurrentMenu 完全初始化
                        WriteFileLog("[QuickStart] CharCreation: Bound manager in NarrativeStage");
                    }
                    // 使用绑定的 manager 进行问卷阶段的自动选择
                    RunOnCharCreationState(null, dt); // 传入 null，使用绑定的 manager
                }
                else
                {
                    // Stage 3-6: 其他阶段（BannerEditor, ClanNaming, Review, Options）- 直接跳过
                    if (_cooldown <= 0f)
                    {
                        TryInvokeNextStage(manager);
                        _cooldown = 0.2f;
                        WriteFileLog($"[QuickStart] CharCreation: Skipped {stageTypeName}");
                    }
                }
            }
            catch (Exception ex)
            {
                WriteFileLog("[QuickStart] CharCreation: AutoSkipCurrentStage error: " + ex.Message);
                _cooldown = 0.25f;
            }
        }

        // ✅ 调用 NextStage() 方法
        private static bool TryInvokeNextStage(object manager)
        {
            try
            {
                Type managerType = manager.GetType();
                const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                MethodInfo nextStageMethod = managerType.GetMethod("NextStage", BF, null, Type.EmptyTypes, null);
                if (nextStageMethod != null)
                {
                    nextStageMethod.Invoke(manager, null);
                    WriteFileLog("[QuickStart] CharCreation: Called NextStage()");
                    return true;
                }
                else
                {
                    WriteFileLog("[QuickStart] CharCreation: NextStage() method not found");
                    return false;
                }
            }
            catch (Exception ex)
            {
                WriteFileLog("[QuickStart] CharCreation: NextStage() FAILED: " + ex.Message);
                return false;
            }
        }

        // 计算 CharacterCreationContent 的步骤哈希
        // 计算当前步骤的哈希值（用于避免重复操作）
        // 根据反编译结果：需要从 CharacterCreationManager 获取 CurrentMenu 和 CharacterCreationContent.SelectedCulture
        private static int ComputeStepHashForManager(object manager)
        {
            try
            {
                Type managerType = manager.GetType();
                const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                // 1) 获取当前菜单信息（从 CharacterCreationManager.CurrentMenu）
                string currentMenuInfo = "unknown";
                PropertyInfo currentMenuProp = managerType.GetProperty("CurrentMenu", BF);
                if (currentMenuProp != null && currentMenuProp.CanRead)
                {
                    object currentMenu = currentMenuProp.GetValue(manager, null);
                    if (currentMenu != null)
                    {
                        Type menuType = currentMenu.GetType();
                        // 根据反编译结果，NarrativeMenu 有 StringId 属性
                        PropertyInfo menuIdProp = menuType.GetProperty("StringId", BF) ?? 
                                                 menuType.GetProperty("Id", BF) ?? 
                                                 menuType.GetProperty("MenuId", BF);
                        if (menuIdProp != null && menuIdProp.CanRead)
                        {
                            object menuId = menuIdProp.GetValue(currentMenu, null);
                            currentMenuInfo = menuId?.ToString() ?? "null";
                        }
                    }
                }

                // 2) 检查是否有选中的文化（从 CharacterCreationManager.CharacterCreationContent.SelectedCulture）
                bool hasCulture = false;
                PropertyInfo contentProp = managerType.GetProperty("CharacterCreationContent", BF);
                if (contentProp != null && contentProp.CanRead)
                {
                    object content = contentProp.GetValue(manager, null);
                    if (content != null)
                    {
                        Type contentType = content.GetType();
                        PropertyInfo cultureProp = contentType.GetProperty("SelectedCulture", BF);
                        if (cultureProp != null && cultureProp.CanRead)
                        {
                            object culture = cultureProp.GetValue(content, null);
                            hasCulture = culture != null;
                        }
                    }
                }

                unchecked
                {
                    int hash = currentMenuInfo.GetHashCode();
                    hash = hash * 397 ^ (hasCulture ? 1 : 0);
                    return hash;
                }
            }
            catch
            {
                return Environment.TickCount;
            }
        }

        // 尝试设置文化为瓦兰迪亚
        // 根据反编译结果：通过 CharacterCreationManager.CharacterCreationContent.SetSelectedCulture() 设置文化
        // 修改为返回 bool，表示是否成功设置了文化（或已经设置过）
        private static bool TrySetCulture(object manager)
        {
            try
            {
                Type managerType = manager.GetType();
                const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                // 1) 获取 CharacterCreationContent 属性
                PropertyInfo contentProp = managerType.GetProperty("CharacterCreationContent", BF);
                if (contentProp == null || !contentProp.CanRead)
                    return false;

                object content = contentProp.GetValue(manager, null);
                if (content == null)
                    return false;

                Type contentType = content.GetType();

                // 2) 检查是否已经设置了文化
                PropertyInfo cultureProp = contentType.GetProperty("SelectedCulture", BF);
                if (cultureProp == null || !cultureProp.CanRead)
                    return false;

                object currentCulture = cultureProp.GetValue(content, null);
                if (currentCulture != null)
                {
                    // 已经设置了文化，返回 true 表示"已完成"
                    WriteFileLog("[QuickStart] CharCreation: Culture already set");
                    return true;
                }

                // 3) 获取所有可用文化（通过 GetCultures() 方法）
                MethodInfo getCulturesMethod = contentType.GetMethod("GetCultures", BF);
                if (getCulturesMethod == null || getCulturesMethod.GetParameters().Length != 0)
                    return false;

                object culturesObj = getCulturesMethod.Invoke(content, null);
                if (culturesObj == null)
                    return false;

                // 4) 查找瓦兰迪亚文化
                Type cultureType = null;
                try
                {
                    Assembly campaignSystem = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == "TaleWorlds.CampaignSystem");
                    if (campaignSystem != null)
                    {
                        cultureType = campaignSystem.GetType("TaleWorlds.CampaignSystem.CultureObject");
                    }
                }
                catch { }

                if (cultureType == null)
                    return false;

                object vlandiaCulture = null;
                if (culturesObj is System.Collections.IEnumerable enumerable)
                {
                    foreach (object culture in enumerable)
                    {
                        if (culture != null && cultureType.IsAssignableFrom(culture.GetType()))
                        {
                            PropertyInfo nameProp = culture.GetType().GetProperty("Name", BF) ??
                                                   culture.GetType().GetProperty("StringId", BF);
                            if (nameProp != null && nameProp.CanRead)
                            {
                                object nameObj = nameProp.GetValue(culture, null);
                                string name = nameObj?.ToString() ?? "";
                                if (name.IndexOf("vlandia", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    name.IndexOf("瓦兰迪亚", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    vlandiaCulture = culture;
                                    break;
                                }
                            }
                        }
                    }
                }

                // 5) 如果找到了瓦兰迪亚，使用 SetSelectedCulture() 方法设置
                if (vlandiaCulture != null)
                {
                    MethodInfo setCultureMethod = contentType.GetMethod("SetSelectedCulture", BF);
                    if (setCultureMethod != null)
                    {
                        ParameterInfo[] parameters = setCultureMethod.GetParameters();
                        if (parameters.Length == 2 && 
                            cultureType.IsAssignableFrom(parameters[0].ParameterType) &&
                            managerType.IsAssignableFrom(parameters[1].ParameterType))
                        {
                            setCultureMethod.Invoke(content, new object[] { vlandiaCulture, manager });
                            var logMsg = "[QuickStart] CharCreation: set culture to Vlandia via SetSelectedCulture()";
                            Debug.Print(logMsg, 0, Debug.DebugColor.Green);
                            WriteFileLog(logMsg);
                            return true;
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                var logMsg = "[QuickStart] CharCreation: failed to set culture: " + ex.Message;
                Debug.Print(logMsg, 0, Debug.DebugColor.Red);
                WriteFileLog(logMsg);
                return false;
            }
        }

        // ✅ 按照 ChatGPT 要求：每帧只做一步，返回 bool 表示是否成功
        // 根据反编译结果：使用 CharacterCreationManager.GetSuitableNarrativeMenuOptions() 和 OnNarrativeMenuOptionSelected()
        private static bool TrySelectCurrentMenuOption(object manager)
        {
            try
            {
                Type managerType = manager.GetType();
                const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                // 1) 检查 CurrentMenu 是否存在
                PropertyInfo currentMenuProp = managerType.GetProperty("CurrentMenu", BF);
                if (currentMenuProp == null || !currentMenuProp.CanRead)
                {
                    WriteFileLog("[QuickStart] CharCreation: TrySelectCurrentMenuOption - CurrentMenu property not found");
                    return false;
                }

                object currentMenu = currentMenuProp.GetValue(manager, null);
                if (currentMenu == null)
                {
                    WriteFileLog("[QuickStart] CharCreation: TrySelectCurrentMenuOption - CurrentMenu is null");
                    return false;
                }

                // 2) 获取 GetSuitableNarrativeMenuOptions() 方法
                MethodInfo getOptionsMethod = managerType.GetMethod("GetSuitableNarrativeMenuOptions", BF);
                if (getOptionsMethod == null || getOptionsMethod.GetParameters().Length != 0)
                {
                    WriteFileLog("[QuickStart] CharCreation: TrySelectCurrentMenuOption - GetSuitableNarrativeMenuOptions method not found");
                    return false;
                }

                object optionsObj = getOptionsMethod.Invoke(manager, null);
                if (optionsObj == null)
                {
                    WriteFileLog("[QuickStart] CharCreation: TrySelectCurrentMenuOption - GetSuitableNarrativeMenuOptions returned null");
                    return false;
                }

                // 3) 获取第一个可用选项
                object firstOption = null;
                int optionCount = 0;
                if (optionsObj is System.Collections.IEnumerable enumerable)
                {
                    foreach (object opt in enumerable)
                    {
                        optionCount++;
                        if (firstOption == null)
                        {
                            firstOption = opt;
                        }
                    }
                }

                if (firstOption == null)
                {
                    WriteFileLog("[QuickStart] CharCreation: TrySelectCurrentMenuOption - No suitable options found (count: " + optionCount + ")");
                    return false;
                }

                WriteFileLog("[QuickStart] CharCreation: TrySelectCurrentMenuOption - Found " + optionCount + " suitable options, selecting first");

                // 4) 使用 OnNarrativeMenuOptionSelected() 方法选择选项
                MethodInfo selectMethod = managerType.GetMethod("OnNarrativeMenuOptionSelected", BF);
                if (selectMethod == null)
                {
                    WriteFileLog("[QuickStart] CharCreation: TrySelectCurrentMenuOption - OnNarrativeMenuOptionSelected method not found");
                    return false;
                }

                ParameterInfo[] parameters = selectMethod.GetParameters();
                if (parameters.Length != 1)
                {
                    WriteFileLog("[QuickStart] CharCreation: TrySelectCurrentMenuOption - OnNarrativeMenuOptionSelected has wrong parameter count: " + parameters.Length);
                    return false;
                }

                // 检查参数类型是否匹配
                Type optionType = firstOption.GetType();
                Type paramType = parameters[0].ParameterType;
                
                if (!paramType.IsAssignableFrom(optionType))
                {
                    WriteFileLog(string.Format(
                        "[QuickStart] CharCreation: TrySelectCurrentMenuOption - Type mismatch: option={0}, param={1}",
                        optionType.FullName,
                        paramType.FullName));
                    return false;
                }

                // 调用方法
                selectMethod.Invoke(manager, new object[] { firstOption });
                var logMsg = "[QuickStart] CharCreation: selected option via OnNarrativeMenuOptionSelected()";
                Debug.Print(logMsg, 0, Debug.DebugColor.Green);
                WriteFileLog(logMsg);
                return true; // ✅ 成功选择选项
            }
            catch (Exception ex)
            {
                var logMsg = string.Format(
                    "[QuickStart] CharCreation: failed to select option: {0}\nStackTrace: {1}",
                    ex.Message,
                    ex.StackTrace);
                Debug.Print(logMsg, 0, Debug.DebugColor.Red);
                WriteFileLog(logMsg);
                
                // 如果是内部异常，也记录
                if (ex.InnerException != null)
                {
                    var innerMsg = string.Format(
                        "[QuickStart] CharCreation: InnerException: {0}\nInnerStackTrace: {1}",
                        ex.InnerException.Message,
                        ex.InnerException.StackTrace);
                    WriteFileLog(innerMsg);
                }
                
                return false; // ✅ 异常时返回 false
            }
        }

        // 尝试前进到下一个菜单（参数应该是 manager，不是 content）
        private static bool TrySwitchToNextMenu(object manager)
        {
            try
            {
                Type t = manager.GetType();
                const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                // 根据反编译结果，有 TrySwitchToNextMenu() 方法
                MethodInfo nextMethod = t.GetMethod("TrySwitchToNextMenu", BF);
                if (nextMethod != null && nextMethod.GetParameters().Length == 0)
                {
                    object result = nextMethod.Invoke(manager, null);
                    if (result is bool success && success)
                    {
                        var logMsg = "[QuickStart] CharCreation: switched to next menu via TrySwitchToNextMenu()";
                        Debug.Print(logMsg, 0, Debug.DebugColor.Green);
                        WriteFileLog(logMsg);
                        return true;
                    }
                    else
                    {
                        WriteFileLog("[QuickStart] CharCreation: TrySwitchToNextMenu() returned false");
                    }
                }
                else
                {
                    WriteFileLog("[QuickStart] CharCreation: TrySwitchToNextMenu() method not found");
                }
            }
            catch (Exception ex)
            {
                Debug.Print(
                    "[QuickStart] CharCreation: failed to advance: " + ex.Message,
                    0,
                    Debug.DebugColor.Yellow);
            }

            return false;
        }

        // ✅ 核心方法：确保 NarrativeMenu 就绪（CurrentMenu != null）
        // 如果 CurrentMenu 为 null，会尝试调用 StartNarrativeStage() 或 NextStage() 来推进阶段
        private static bool EnsureNarrativeMenuReady(object manager)
        {
            Type t = manager.GetType();
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // 1) 先读 CurrentMenu
            PropertyInfo pCurrentMenu = t.GetProperty("CurrentMenu", BF);
            if (pCurrentMenu == null || !pCurrentMenu.CanRead)
            {
                WriteFileLog("[QuickStart] CharCreation: EnsureNarrativeMenuReady - CurrentMenu property not found");
                return false;
            }

            object currentMenu = pCurrentMenu.GetValue(manager, null);
            if (currentMenu != null)
            {
                // CurrentMenu 已经就绪
                return true;
            }

            // 2) 如果 NarrativeMenus 已经构建好了，并且包含 InputMenuId=="start"，直接 StartNarrativeStage（只调用一次）
            if (!_startNarrativeCalled && HasStartMenu(t, manager))
            {
                MethodInfo miStart = t.GetMethod("StartNarrativeStage", BF, null, Type.EmptyTypes, null);
                if (miStart != null)
                {
                    try
                    {
                        miStart.Invoke(manager, null);
                        _startNarrativeCalled = true;
                        WriteFileLog("[QuickStart] CharCreation: Called StartNarrativeStage()");
                    }
                    catch (Exception ex)
                    {
                        WriteFileLog("[QuickStart] CharCreation: StartNarrativeStage() FAILED: " + ex.Message);
                    }
                }

                // 重新读一次 CurrentMenu
                currentMenu = pCurrentMenu.GetValue(manager, null);
                if (currentMenu != null)
                {
                    WriteFileLog("[QuickStart] CharCreation: CurrentMenu is now ready after StartNarrativeStage()");
                    return true;
                }
            }

            // 3) NarrativeMenus 还没准备好：大概率还在 CultureStage，需要推进 Stage
            //    NextStage 通常是 public（根据反编译结果），用反射调用一次即可
            MethodInfo miNextStage = t.GetMethod("NextStage", BF, null, Type.EmptyTypes, null);
            if (miNextStage != null)
            {
                try
                {
                    miNextStage.Invoke(manager, null);
                    WriteFileLog("[QuickStart] CharCreation: Invoked NextStage() to advance stage");
                }
                catch (Exception ex)
                {
                    WriteFileLog("[QuickStart] CharCreation: NextStage() FAILED: " + ex.Message);
                }
            }
            else
            {
                // 有些版本叫 AdvanceStage / GoToNextStage / etc. 你可用 dnSpy 搜一下
                WriteFileLog("[QuickStart] CharCreation: NextStage() not found (need check method name in dnSpy)");
            }

            // 4) 推进后不要立刻强行继续，留给下一帧再判断
            return false;
        }

        // 检查 NarrativeMenus 中是否包含 InputMenuId=="start" 的菜单
        private static bool HasStartMenu(Type managerType, object manager)
        {
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // 找 NarrativeMenus 属性/字段
            object menusObj = null;
            PropertyInfo pMenus = managerType.GetProperty("NarrativeMenus", BF);
            if (pMenus != null && pMenus.CanRead)
            {
                try
                {
                    menusObj = pMenus.GetValue(manager, null);
                }
                catch { }
            }

            if (menusObj == null)
            {
                FieldInfo fMenus = managerType.GetField("_narrativeMenus", BF) ?? managerType.GetField("NarrativeMenus", BF);
                if (fMenus != null)
                {
                    try
                    {
                        menusObj = fMenus.GetValue(manager);
                    }
                    catch { }
                }
            }

            if (menusObj == null)
            {
                WriteFileLog("[QuickStart] CharCreation: HasStartMenu - NarrativeMenus not found");
                return false;
            }

            if (menusObj is System.Collections.IEnumerable en)
            {
                foreach (object m in en)
                {
                    if (m == null) continue;
                    Type mt = m.GetType();
                    PropertyInfo pId = mt.GetProperty("InputMenuId", BF);
                    if (pId != null && pId.CanRead)
                    {
                        try
                        {
                            object idObj = pId.GetValue(m, null);
                            string id = idObj?.ToString() ?? "";
                            if (string.Equals(id, "start", StringComparison.OrdinalIgnoreCase))
                            {
                                WriteFileLog("[QuickStart] CharCreation: HasStartMenu - Found start menu");
                                return true;
                            }
                        }
                        catch { }
                    }
                }
            }

            WriteFileLog("[QuickStart] CharCreation: HasStartMenu - No start menu found");
            return false;
        }
    }
}


