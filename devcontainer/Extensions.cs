using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;

namespace devcontainer
{
    public static class Extensions
    {
        private const string NameGroup = "name";
        private const string DefaultGroup = "default";
        public static readonly Regex TemplateVarMatcher = new Regex(@"(?!(""))\$\{(?<name>[^}:]+)(\:-?(?<default>.*))?\}", 
            RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.Multiline);

        public static string PerformTemplateSubstitutions(this string templateContent, IReadOnlyDictionary<string, string> values, bool passthroughUnknowns = false)
        {
            return TemplateVarMatcher.Replace(templateContent, match => 
            {
                var found = values.TryGetValue(match.Groups[NameGroup].Value, out var value);
                if (!found)
                    value = passthroughUnknowns ? match.Value : 
                        match.Groups[DefaultGroup].Success? match.Groups[DefaultGroup].Value : string.Empty;
                return value;
            });
        }

        public static IReadOnlyDictionary<TKey, TValue> MergeWithUpdates<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> first, IReadOnlyDictionary<TKey, TValue> second)
        {
            return first.Concat(second)
                .GroupBy(kvp => kvp.Key, kvp => kvp.Value)
                .ToDictionary(g => g.Key, g => g.Last());
        }

        public static IReadOnlyDictionary<string, string> ToReadOnlyDictionary(this IDictionary dictionary)
        {
            var result = new Dictionary<string, string>();
            foreach (DictionaryEntry entry in dictionary)
                result.Add(entry.Key.ToString(), entry.Value.ToString());

            return result;
        }

        public static void Process(this IReadOnlyDictionary<string, string> env, string sourceFilename, string destFilename, bool overwrite = false, bool passthroughUnknowns = false)
        {
            var text = File.ReadAllText(sourceFilename)
                .PerformTemplateSubstitutions(env, passthroughUnknowns);
            Directory.CreateDirectory(Path.GetDirectoryName(destFilename));

            // RESPECT the flag
            if (!File.Exists(destFilename) || overwrite)
                File.WriteAllText(destFilename, text);
        }

        public static void CopyTo(this string sourceFolder, string destinationFolder, string mask = "*.*", bool createFolders = false, bool recurseFolders = false, bool overwrite = false, 
            Action<string, string> onCopyFile = null, bool overWrite = false)
        {
            try
            {
                var exDir = sourceFolder;
                var dir = new DirectoryInfo(exDir);
                SearchOption so = (recurseFolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

                foreach (string sourceFile in Directory.GetFiles(dir.ToString(), mask, so))
                {
                    FileInfo srcFile = new FileInfo(sourceFile);
                    string srcFileName = srcFile.Name;

                    var destFile = new FileInfo(destinationFolder + srcFile.FullName.Replace(sourceFolder, ""));

                    if (!Directory.Exists(destFile.DirectoryName ) && createFolders)
                        Directory.CreateDirectory(destFile.DirectoryName);

                    if (onCopyFile == null)
                        File.Copy(srcFile.FullName, destFile.FullName, overwrite);
                    else
                        onCopyFile(srcFile.FullName, destFile.FullName);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}

