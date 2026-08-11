using Scp.Domain;
using Scp.Simulation;

namespace Scp.Application
{
    /// <summary>
    /// 中文：从终局世界快照生成固定三部分尾声；输出只依赖持久化状态，重复查看不会消耗随机数或改变世界。
    /// English: Generates a fixed three-part epilogue from an ended world snapshot; output depends only on persisted state and repeated viewing never consumes randomness or mutates the world.
    /// </summary>
    public sealed class EpilogueService
    {
        public EpilogueReport CreateReport(WorldState world)
        {
            return new EpilogueReport
            {
                IsAvailable = world.Failure.IsEnded,
                Sections = new[]
                {
                    new EpilogueSection
                    {
                        Kind = EpilogueSectionKind.Outcome,
                        Title = "终局结果",
                        Body = "任期于第 " + world.Council.CurrentCycle + " 周期结束：" + Describe(world.Failure.EndReason) + "。"
                    },
                    new EpilogueSection
                    {
                        Kind = EpilogueSectionKind.Legacy,
                        Title = "任期影响",
                        Body = "结算资金 " + world.Funds + "；全球帷幕 " + world.Veil.Global + "/10000；直接处置人员 " + world.Facts.PersonnelTerminated + "；监督者特权 " + world.Facts.PrivilegeUses + " 次；Alpha-1 出动 " + world.Facts.AlphaOneDeployments + " 次。"
                    },
                    new EpilogueSection
                    {
                        Kind = EpilogueSectionKind.Archive,
                        Title = "档案状态",
                        Body = "世界快照、随机状态与命令记录已封存。该终局档可查看，但不可提交命令或继续推进。"
                    }
                }
            };
        }

        /// <summary>中文：兼容现有纯文本调用方，按结构化三部分稳定拼接。English: Keeps existing text callers compatible by stably joining the three structured sections.</summary>
        public string Create(WorldState world)
        {
            EpilogueReport report = CreateReport(world);
            return string.Join("\n\n", System.Array.ConvertAll(report.Sections, section => section.Title + "：" + section.Body));
        }

        private static string Describe(GameEndReason reason)
        {
            switch (reason)
            {
                case GameEndReason.FiscalCollapse: return "财政崩溃";
                case GameEndReason.VeilCollapse: return "帷幕崩塌";
                case GameEndReason.Impeached: return "议会弹劾";
                case GameEndReason.EthicsRemoval: return "伦理委员会武力罢免";
                case GameEndReason.ContainedOverseer: return "监督者成为收容对象";
                case GameEndReason.WorldRestarted: return "世界重启决议";
                case GameEndReason.KClassScenario: return "K 级情景";
                default: return "未记录";
            }
        }
    }
}
