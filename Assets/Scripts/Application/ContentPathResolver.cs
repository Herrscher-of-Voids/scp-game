using System;
using System.IO;

namespace Scp.Application
{
    public static class ContentPathResolver
    {
        public static string FindScpDirectory(string startDirectory)
        {
            return FindDataPath(startDirectory, Path.Combine("Assets", "Data", "Scps"), true);
        }

        /// <summary>
        /// 中文：从调用目录向上查找正式设施 JSON；返回文件路径而非目录，供所有引擎共用同一目录文件。
        /// English: Searches upward from the caller directory for the official facility JSON; returns the file path so every engine shares one catalogue.
        /// </summary>
        /// <param name="startDirectory">中文：开始向父目录搜索的位置。English: Directory from which the parent search starts.</param>
        /// <returns>中文：o5-facilities.json 的完整路径。English: Full path to o5-facilities.json.</returns>
        public static string FindFacilityFile(string startDirectory)
        {
            return FindDataPath(startDirectory, Path.Combine("Assets", "Data", "Facilities", "o5-facilities.json"), false);
        }

        /// <summary>
        /// 中文：执行确定性的向上路径搜索，并在发布输出目录提供同结构回退；找不到时抛出明确异常。
        /// English: Performs deterministic upward path discovery with a same-layout publish-output fallback and throws a clear exception when absent.
        /// </summary>
        private static string FindDataPath(string startDirectory, string relativePath, bool directory)
        {
            var current = new DirectoryInfo(startDirectory);
            while (current != null)
            {
                string candidate = Path.Combine(current.FullName, relativePath);
                if (directory ? Directory.Exists(candidate) : File.Exists(candidate)) return candidate;
                current = current.Parent;
            }

            string outputCandidate = Path.Combine(AppContext.BaseDirectory, relativePath);
            if (directory ? Directory.Exists(outputCandidate) : File.Exists(outputCandidate)) return outputCandidate;
            throw new FileNotFoundException(relativePath + " could not be located.", outputCandidate);
        }
    }
}
