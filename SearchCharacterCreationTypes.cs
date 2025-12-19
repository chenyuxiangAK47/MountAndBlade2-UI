using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace QuickStartMod
{
    /// <summary>
    /// 自动搜索 Bannerlord DLL 中角色创建相关的类型、方法和属性
    /// 使用方法：编译后运行，会输出所有找到的类型信息
    /// </summary>
    class SearchCharacterCreationTypes
    {
        static void Main(string[] args)
        {
            string bannerlordPath = @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client";
            
            if (!Directory.Exists(bannerlordPath))
            {
                Console.WriteLine($"错误：找不到 Bannerlord 路径：{bannerlordPath}");
                Console.WriteLine("请修改脚本中的 bannerlordPath 变量为正确的路径");
                Console.ReadKey();
                return;
            }

            List<string> dllFiles = new List<string>
            {
                Path.Combine(bannerlordPath, "TaleWorlds.CampaignSystem.dll"),
                Path.Combine(bannerlordPath, "TaleWorlds.MountAndBlade.GauntletUI.dll"),
                Path.Combine(bannerlordPath, "TaleWorlds.MountAndBlade.dll"),
                Path.Combine(bannerlordPath, "TaleWorlds.Core.dll")
            };

            Console.WriteLine("=== Bannerlord 角色创建类型搜索工具 ===\n");

            foreach (string dllPath in dllFiles)
            {
                if (!File.Exists(dllPath))
                {
                    Console.WriteLine($"⚠️  跳过（文件不存在）：{Path.GetFileName(dllPath)}");
                    continue;
                }

                Console.WriteLine($"\n📦 正在分析：{Path.GetFileName(dllPath)}");
                Console.WriteLine(new string('-', 80));

                try
                {
                    Assembly assembly = Assembly.LoadFrom(dllPath);
                    SearchAssembly(assembly);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 加载失败：{ex.Message}");
                }
            }

            Console.WriteLine("\n\n=== 搜索完成 ===");
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }

        static void SearchAssembly(Assembly assembly)
        {
            string[] keywords = new[]
            {
                "CharacterCreation",
                "CharacterCreationVM",
                "CharacterCreationState",
                "CultureSelection",
                "BackgroundSelection",
                "ChildhoodSelection",
                "YouthSelection"
            };

            var relevantTypes = new List<Type>();

            // 搜索所有类型
            foreach (Type type in assembly.GetTypes())
            {
                string typeName = type.Name;
                string fullName = type.FullName ?? "";

                foreach (string keyword in keywords)
                {
                    if (typeName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        fullName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        relevantTypes.Add(type);
                        break;
                    }
                }
            }

            if (relevantTypes.Count == 0)
            {
                Console.WriteLine("   ❌ 未找到相关类型");
                return;
            }

            // 按命名空间分组显示
            var grouped = relevantTypes.GroupBy(t => t.Namespace ?? "(无命名空间)").OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                Console.WriteLine($"\n📁 命名空间：{group.Key}");
                Console.WriteLine(new string(' ', 2) + new string('-', 78));

                foreach (Type type in group.OrderBy(t => t.Name))
                {
                    PrintTypeInfo(type);
                }
            }
        }

        static void PrintTypeInfo(Type type)
        {
            Console.WriteLine($"\n  🔷 {type.Name}");
            Console.WriteLine($"     完整名称：{type.FullName}");
            Console.WriteLine($"     基类：{(type.BaseType != null ? type.BaseType.Name : "(无)")}");

            // 查找属性
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            var relevantProps = properties.Where(p =>
            {
                string name = p.Name.ToLowerInvariant();
                return name.Contains("option") ||
                       name.Contains("culture") ||
                       name.Contains("background") ||
                       name.Contains("next") ||
                       name.Contains("continue") ||
                       name.Contains("command") ||
                       name.Contains("stage") ||
                       name.Contains("title") ||
                       name.Contains("canadvance") ||
                       name.Contains("selected");
            }).ToList();

            if (relevantProps.Count > 0)
            {
                Console.WriteLine($"     📋 相关属性：");
                foreach (var prop in relevantProps)
                {
                    string access = (prop.GetGetMethod(true)?.IsPublic ?? false) ? "public" : "private";
                    Console.WriteLine($"        • {prop.Name} ({prop.PropertyType.Name}) [{access}]");
                }
            }

            // 查找方法
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            var relevantMethods = methods.Where(m =>
            {
                if (m.IsSpecialName) return false; // 跳过属性访问器
                string name = m.Name.ToLowerInvariant();
                return name.Contains("select") ||
                       name.Contains("option") ||
                       name.Contains("culture") ||
                       name.Contains("background") ||
                       name.Contains("next") ||
                       name.Contains("continue") ||
                       name.Contains("execute") ||
                       name.Contains("finalize") ||
                       name.Contains("done") ||
                       name.Contains("confirm");
            }).Distinct().ToList();

            if (relevantMethods.Count > 0)
            {
                Console.WriteLine($"     🔧 相关方法：");
                foreach (var method in relevantMethods)
                {
                    string access = method.IsPublic ? "public" : (method.IsPrivate ? "private" : "protected");
                    string parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    if (string.IsNullOrEmpty(parameters))
                        parameters = "(无参数)";
                    Console.WriteLine($"        • {method.Name}({parameters}) [{access}]");
                }
            }

            // 查找字段（可能包含命令对象）
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            var relevantFields = fields.Where(f =>
            {
                string name = f.Name.ToLowerInvariant();
                return name.Contains("command") ||
                       name.Contains("action") ||
                       name.Contains("option") ||
                       name.Contains("vm");
            }).ToList();

            if (relevantFields.Count > 0)
            {
                Console.WriteLine($"     📦 相关字段：");
                foreach (var field in relevantFields)
                {
                    string access = field.IsPublic ? "public" : (field.IsPrivate ? "private" : "protected");
                    Console.WriteLine($"        • {field.Name} ({field.FieldType.Name}) [{access}]");
                }
            }
        }
    }
}


