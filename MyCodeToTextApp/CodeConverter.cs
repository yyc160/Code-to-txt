using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MyCodeToTextApp
{
    public class CodeConverter
    {
        public event EventHandler<string> StatusUpdated;
        private FileSystemWatcher watcher;
        private bool isRegenerating = false;

        public void ConvertCodeToText(string projectDir, string outputDir, List<string> includeRules, List<string> excludeRules)
        {
            Directory.CreateDirectory(outputDir);
            string outputFilePath = Path.Combine(outputDir, "代码输出.txt");
            string structureFilePath = Path.Combine(outputDir, "项目结构.txt");

            try
            {
                // 获取唯一的文件名
                outputFilePath = GetUniqueFileName(outputFilePath);
                structureFilePath = GetUniqueFileName(structureFilePath);

                // 生成项目结构
                GenerateProjectStructure(projectDir, structureFilePath, includeRules, excludeRules);
                StatusUpdated?.Invoke(this, $"项目结构已生成到: {structureFilePath}");

                // 生成代码文本
                using (StreamWriter writer = new StreamWriter(outputFilePath, false, Encoding.UTF8))
                {
                    // 只写入代码内容
                    ProcessDirectory(projectDir, projectDir, writer, includeRules, excludeRules);
                }
                StatusUpdated?.Invoke(this, $"代码已生成到: {outputFilePath}");
            }
            catch (Exception ex)
            {
                StatusUpdated?.Invoke(this, $"转换过程中发生错误: {ex.Message}");
                throw;
            }
        }

        private void GenerateProjectStructure(string projectDir, string outputFile, List<string> includeRules, List<string> excludeRules)
        {
            StringBuilder structure = new StringBuilder();
            GenerateStructureRecursive(projectDir, projectDir, "", structure, includeRules, excludeRules);

            File.WriteAllText(outputFile, structure.ToString(), Encoding.UTF8);
        }

        private void GenerateStructureRecursive(string rootDir, string currentDir, string indent,
      StringBuilder structure, List<string> includeRules, List<string> excludeRules)
        {
            // 检查当前目录是否应该被排除
            string relativeDirPath = Path.GetRelativePath(rootDir, currentDir);
            if (ShouldExcludeDirectory(currentDir, excludeRules, rootDir))
            {
                return;
            }

            string directoryName = Path.GetFileName(currentDir);
            if (string.IsNullOrEmpty(directoryName))
            {
                directoryName = currentDir; // 使用完整路径（针对根目录）
            }

            if (currentDir != rootDir) // 不显示根目录
            {
                structure.AppendLine($"{indent}📁 {directoryName}");
            }

            string nextIndent = currentDir == rootDir ? "" : indent + "  ";

            try
            {
                // 获取所有文件并排序
                var files = Directory.GetFiles(currentDir)
                    .Select(f => new FileInfo(f))
                    .Where(f => ShouldIncludeFile(f.FullName, includeRules, excludeRules, rootDir))
                    .OrderBy(f => f.Name);

                // 获取所有目录并排序
                var directories = Directory.GetDirectories(currentDir)
                    .Select(d => new DirectoryInfo(d))
                    .Where(d => !ShouldExcludeDirectory(d.FullName, excludeRules, rootDir))
                    .OrderBy(d => d.Name);

                // 先处理文件
                foreach (var file in files)
                {
                    structure.AppendLine($"{nextIndent}📄 {file.Name}");
                }

                // 再处理目录
                foreach (var dir in directories)
                {
                    // 递归处理子目录
                    GenerateStructureRecursive(rootDir, dir.FullName, nextIndent, structure, includeRules, excludeRules);
                }
            }
            catch (Exception ex)
            {
                structure.AppendLine($"{nextIndent}Error accessing directory: {ex.Message}");
            }
        }

        private void ProcessDirectory(string rootDir, string currentDir, StreamWriter writer,
         List<string> includeRules, List<string> excludeRules)
        {
            try
            {
                // 获取并处理文件
                foreach (string filePath in Directory.GetFiles(currentDir).OrderBy(f => f))
                {
                    if (ShouldIncludeFile(filePath, includeRules, excludeRules, rootDir))
                    {
                        WriteFileToOutput(filePath, writer, rootDir);
                    }
                    else
                    {
                        // 添加调试信息
                        string relativePath = Path.GetRelativePath(rootDir, filePath);
                        StatusUpdated?.Invoke(this, $"跳过文件: {relativePath}");
                    }
                }

                // 获取并处理子目录
                foreach (string subDir in Directory.GetDirectories(currentDir).OrderBy(d => d))
                {
                    string relativePath = Path.GetRelativePath(rootDir, subDir);
                    bool shouldExclude = excludeRules.Any(rule =>
                    {
                        if (rule.EndsWith("/*"))
                        {
                            string baseRule = rule.Substring(0, rule.Length - 2);
                            return relativePath.StartsWith(baseRule, StringComparison.OrdinalIgnoreCase);
                        }
                        return MatchesPattern(relativePath, rule);
                    });

                    if (!shouldExclude)
                    {
                        ProcessDirectory(rootDir, subDir, writer, includeRules, excludeRules);
                    }
                    else
                    {
                        StatusUpdated?.Invoke(this, $"跳过目录: {relativePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                StatusUpdated?.Invoke(this, $"处理目录时出错: {ex.Message}");
            }
        }
        private bool ShouldExcludeFile(string filePath, List<string> excludeRules, string rootDir)
        {
            try
            {
                string relativePath = Path.GetRelativePath(rootDir, filePath);

                foreach (var rule in excludeRules)
                {
                    // 处理特殊情况：直接匹配文件扩展名
                    if (rule.StartsWith("*."))
                    {
                        if (filePath.EndsWith(rule.Substring(1), StringComparison.OrdinalIgnoreCase))
                            return true;
                        continue;
                    }

                    // 处理目录规则
                    if (MatchesPattern(relativePath, rule))
                    {
                        StatusUpdated?.Invoke(this, $"文件 {relativePath} 被规则 {rule} 排除");
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                StatusUpdated?.Invoke(this, $"检查排除规则时出错: {ex.Message}");
                return false;
            }
        }


        private bool ShouldIncludeFile(string filePath, List<string> includeRules, List<string> excludeRules, string rootDir)
        {
            // 首先检查是否应该排除
            if (ShouldExcludeFile(filePath, excludeRules, rootDir))
            {
                return false;
            }

            // 如果没有被排除，则检查是否符合包含规则
            string relativePath = Path.GetRelativePath(rootDir, filePath);
            return includeRules.Any(rule => MatchesPattern(relativePath, rule));
        }


        private bool ShouldExcludeDirectory(string directoryPath, List<string> excludeRules, string rootDir)
        {
            if (string.IsNullOrEmpty(directoryPath)) return false;

            // 获取相对路径并标准化分隔符
            string relativePath = Path.GetRelativePath(rootDir, directoryPath).Replace('\\', '/');

            foreach (var rule in excludeRules)
            {
                // 标准化规则中的分隔符
                string normalizedRule = rule.Replace('\\', '/');

                // 处理目录通配符
                if (normalizedRule.EndsWith("/*"))
                {
                    string baseDir = normalizedRule.Substring(0, normalizedRule.Length - 2);
                    if (relativePath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                // 处理完整路径匹配
                else if (MatchesPattern(relativePath, normalizedRule))
                {
                    return true;
                }
            }

            return false;
        }

        private bool MatchesPattern(string path, string pattern)
        {
            try
            {
                // 标准化路径分隔符
                path = path.Replace('\\', '/').TrimStart('/');
                pattern = pattern.Replace('\\', '/').TrimStart('/');

                // 处理 **/ 模式（匹配任意深度的目录）
                if (pattern.StartsWith("**/"))
                {
                    pattern = pattern.Substring(3); // 移除 "**)/"
                    return MatchesWildcardPattern(path, pattern);
                }

                // 处理目录通配符
                if (pattern.EndsWith("/*"))
                {
                    string basePattern = pattern.Substring(0, pattern.Length - 2);
                    return path.StartsWith(basePattern, StringComparison.OrdinalIgnoreCase);
                }

                // 处理扩展名通配符（如 *.cs）
                if (pattern.StartsWith("*."))
                {
                    return path.EndsWith(pattern.Substring(1), StringComparison.OrdinalIgnoreCase);
                }

                // 构建正则表达式模式
                string regexPattern = "^" + Regex.Escape(pattern)
                    .Replace("\\*", "[^/]*")  // * 匹配任意字符，但不跨越目录边界
                    .Replace("\\?", ".")      // ? 匹配单个字符
                    + "$";

                return Regex.IsMatch(path, regexPattern, RegexOptions.IgnoreCase);
            }
            catch (Exception ex)
            {
                StatusUpdated?.Invoke(this, $"模式匹配错误: {ex.Message}");
                return false;
            }
        }
        private bool MatchesWildcardPattern(string path, string pattern)
        {
            // 对于 **/ 模式，我们需要检查路径的任何部分是否匹配
            string[] pathParts = path.Split('/');

            // 从每个可能的起始点开始匹配
            for (int i = 0; i < pathParts.Length; i++)
            {
                string remainingPath = string.Join("/", pathParts.Skip(i));
                if (MatchesPattern(remainingPath, pattern))
                {
                    return true;
                }
            }
            return false;
        }


        private string GetUniqueFileName(string filePath)
        {
            if (!File.Exists(filePath))
                return filePath;

            string directory = Path.GetDirectoryName(filePath);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);

            // 检查是否文件名已经包含数字后缀
            string baseFileName = fileNameWithoutExt;
            int currentNumber = 1;

            // 如果文件名已经有数字后缀，提取基础名称和数字
            if (fileNameWithoutExt.LastIndexOf('(') > 0)
            {
                int lastOpenBracket = fileNameWithoutExt.LastIndexOf('(');
                int lastCloseBracket = fileNameWithoutExt.LastIndexOf(')');
                if (lastCloseBracket > lastOpenBracket && lastCloseBracket == fileNameWithoutExt.Length - 1)
                {
                    string numberPart = fileNameWithoutExt.Substring(lastOpenBracket + 1, lastCloseBracket - lastOpenBracket - 1);
                    if (int.TryParse(numberPart, out int number))
                    {
                        baseFileName = fileNameWithoutExt.Substring(0, lastOpenBracket).TrimEnd();
                        currentNumber = number + 1;
                    }
                }
            }

            string newFilePath = filePath;
            while (File.Exists(newFilePath))
            {
                string newFileName = $"{baseFileName}({currentNumber}){extension}";
                newFilePath = Path.Combine(directory, newFileName);
                currentNumber++;
            }

            return newFilePath;
        }

        private void WriteFileToOutput(string filePath, StreamWriter writer, string rootDir)
        {
            try
            {
                string relativePath = Path.GetRelativePath(rootDir, filePath);
                writer.WriteLine($"\n==== {relativePath} ====\n");

                // 读取并写入文件内容
                string content = File.ReadAllText(filePath, Encoding.UTF8);
                writer.WriteLine(content);

                writer.WriteLine(); // 添加空行分隔
                StatusUpdated?.Invoke(this, $"已处理: {relativePath}");
            }
            catch (Exception ex)
            {
                StatusUpdated?.Invoke(this, $"处理文件 {filePath} 时出错: {ex.Message}");
            }
        }

        public void StartWatching(string path)
        {
            watcher = new FileSystemWatcher
            {
                Path = path,
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
            };

            watcher.Changed += OnChanged;
            watcher.Created += OnChanged;
            watcher.Deleted += OnChanged;
            watcher.Renamed += OnRenamed;

            watcher.EnableRaisingEvents = true;
        }

        public void StopWatching()
        {
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                watcher = null;
            }
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            if (!isRegenerating)
            {
                isRegenerating = true;
                StatusUpdated?.Invoke(this, $"文件变化: {e.FullPath} - {e.ChangeType}");

                Task.Delay(500).ContinueWith(async _ =>
                {
                    try
                    {
                        if (Application.OpenForms.Count > 0 && Application.OpenForms[0] is Form1 form)
                        {
                            await form.Invoke((Func<Task>)(async () => await form.GenerateCodeText()));
                        }
                    }
                    finally
                    {
                        isRegenerating = false;
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private void OnRenamed(object source, RenamedEventArgs e)
        {
            if (!isRegenerating)
            {
                isRegenerating = true;
                StatusUpdated?.Invoke(this, $"文件重命名: {e.OldFullPath} 到 {e.FullPath}");

                Task.Delay(500).ContinueWith(async _ =>
                {
                    try
                    {
                        if (Application.OpenForms.Count > 0 && Application.OpenForms[0] is Form1 form)
                        {
                            await form.Invoke((Func<Task>)(async () => await form.GenerateCodeText()));
                        }
                    }
                    finally
                    {
                        isRegenerating = false;
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }
    }
}