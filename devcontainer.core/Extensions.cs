using System.Text;
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
                var name = match.Groups[NameGroup].Value;
                var found = values.TryGetValue(name, out var value);
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

        public static void CopyTo(this string sourceFolder, string destinationFolder, string mask = "*.*", bool createFolders = true, bool recurseFolders = true, bool overwrite = false, 
            Action<string, string> onCopyFile = null, bool overWrite = false)
        {
            try
            {
                var exDir = sourceFolder;
                var dir = new DirectoryInfo(exDir);
                SearchOption so = (recurseFolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

                foreach (string sourceFile in Directory.GetFiles(dir.ToString(), mask, so))
                {
                    var srcFile = new FileInfo(sourceFile);
                    string srcFileName = srcFile.Name;

                    var destFile = new FileInfo(destinationFolder + srcFile.FullName.Replace(sourceFolder, ""));

                    if (!Directory.Exists(destFile.DirectoryName ) && createFolders)
                    {
                        Directory.CreateDirectory(destFile.DirectoryName);
                        Console.WriteLine($"Creating folder {destFile.DirectoryName}...");
                    }

                    if (onCopyFile == null)
                    {
                        Console.WriteLine("Using default copy");
                        if (File.Exists(destFile.FullName) && !overWrite)
                        {
                            Console.Error.WriteLine($"Destination file {destFile.FullName} exists and over-writing is forbidden. Skipping.");
                            continue;
                        }
                        else
                        {
                            Console.WriteLine($"Copying {srcFile.FullName} -> {destFile.FullName}");
                            File.Copy(srcFile.FullName, destFile.FullName, overwrite);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Using custom copy for {srcFile.FullName} -> {destFile.FullName} ...");
                        onCopyFile(srcFile.FullName, destFile.FullName);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public static string EnsureDirectoriesExist(this string path)
        {   
            Directory.CreateDirectory(path);
            return path;
        }

        public static void WriteEnvFile(this IDictionary<string, string> env, string filename)
        {
            var text = new StringBuilder();
            foreach (var kvp in env)
                text.AppendLine($"{kvp.Key}={kvp.Value}");
            File.WriteAllText(filename, text.ToString());
        }
        public static void AppendToEnvFile(this IDictionary<string, string> env, string filename)
        {
            var text = new StringBuilder();
            foreach (var kvp in env)
                text.AppendLine($"{kvp.Key}={kvp.Value}");
            File.AppendAllText(filename, text.ToString());
        }

        public static IReadOnlyDictionary<string, string> LoadEnvFile(this string filename)
        {
            var result = new Dictionary<string, string>();
            try
            {
                using (var reader = new StreamReader(filename))
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var parts = line.Split('=');
                    result.Add(parts[0], parts[1]);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Exception: {ex.Message}\n{ex.StackTrace}");
            }

            return result;
        }

        public static bool RunningInContainer()
        {
            var filename = $"/proc/1/cgroup";
            if (!File.Exists(filename))
                return false;
            var text = File.ReadAllText(filename);
            return text.Contains("/docker/");
        }
    }
}

