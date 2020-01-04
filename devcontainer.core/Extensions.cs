using System.Collections.Specialized;
using System.Text;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;

using CliWrap;
using System.Diagnostics;
using Mono.Unix;

namespace devcontainer
{
    internal static class Extensions
    {
        private const string NameGroup = "name";
        private const string DefaultGroup = "default";
        public static readonly Regex TemplateVarMatcher = new Regex(@"(?!(""))\$\{(template\:)(?<name>[^}:]+)(\:-?(?<default>.*))?\}", 
            RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.Multiline);
        public static string PerformTemplateSubstitutions(this string templateContent, IDictionary<string, string> values, bool passthroughUnknowns = false,
            Regex matcher = null)
        {
            matcher = matcher ?? TemplateVarMatcher;
            return matcher.Replace(templateContent, match => 
            {
                var name = match.Groups[NameGroup].Value;
                var found = values.TryGetValue(name, out var value);
                if (!found)
                    value = passthroughUnknowns ? match.Value : 
                        match.Groups[DefaultGroup].Success? match.Groups[DefaultGroup].Value : string.Empty;
                return value;
            });
        }

        public static IDictionary<TKey, TValue> MergeWithUpdates<TKey, TValue>(this IDictionary<TKey, TValue> first, IDictionary<TKey, TValue> second)
        {
            return first.Concat(second)
                .GroupBy(kvp => kvp.Key, kvp => kvp.Value)
                .ToDictionary(g => g.Key, g => g.Last());
        }
        public static IDictionary<TKey, TValue> MergeInPlaceWithUpdates<TKey, TValue>(this IDictionary<TKey, TValue> first, IDictionary<TKey, TValue> second)
        {
            var merged = first.Concat(second)
                .GroupBy(kvp => kvp.Key, kvp => kvp.Value)
                .ToDictionary(g => g.Key, g => g.Last());
            first.Clear();
            foreach (var kvp in merged)
                first.Add(kvp.Key, kvp.Value);

            return first;
        }

        public static bool CreateOrMerge(this string envFilename, IDictionary<string, string> env)
        {
            try
            {
                if (!File.Exists(envFilename))
                    env.WriteEnvFile(envFilename);
                else
                    envFilename
                        .LoadEnvFile()
                        .MergeWithUpdates(env)
                        .WriteEnvFile(envFilename);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Exception: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
            return true;
        }

        public static bool CreateOrMerge(this string envFilename, string key, string value)
        {
            var env = new Dictionary<string, string> { { key, value } };
            try
            {
                if (!File.Exists(envFilename))
                    env.WriteEnvFile(envFilename);
                else
                    envFilename
                        .LoadEnvFile()
                        .MergeWithUpdates(env)
                        .WriteEnvFile(envFilename);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Exception: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
            return true;
        }

        public static IDictionary<string, string> ToDictionary(this IDictionary dictionary)
        {
            var result = new Dictionary<string, string>();
            foreach (DictionaryEntry entry in dictionary)
                result.Add(entry.Key.ToString(), entry.Value.ToString());

            return result;
        }

        public static void Process(this IDictionary<string, string> env, string sourceFilename, string destFilename, bool overwrite = false, bool passthroughUnknowns = false)
        {
            var text = File.ReadAllText(sourceFilename)
                .PerformTemplateSubstitutions(env, passthroughUnknowns);
            Directory.CreateDirectory(Path.GetDirectoryName(destFilename));

            // RESPECT the flag
            if (!File.Exists(destFilename) || overwrite)
                File.WriteAllText(destFilename, text);
        }

        public static void CopyTo(this string sourceFolder, string destinationFolder, string mask = "*.*", 
            bool createFolders = true, bool recurseFolders = true, bool overwrite = false, 
            Action<string, string> onCopyFile = null, bool copyPermissions = true)
        {
            try
            {
                var dir = new DirectoryInfo(sourceFolder);
                var so = (recurseFolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

                foreach (string sourceFile in Directory.GetFiles(dir.ToString(), mask, so))
                {
                    var srcFile = new FileInfo(sourceFile);
                    string srcFileName = srcFile.Name;

                    var dstFile = new FileInfo(destinationFolder + srcFile.FullName.Replace(sourceFolder, ""));

                    if (!Directory.Exists(dstFile.DirectoryName ) && createFolders)
                    {
                        Directory.CreateDirectory(dstFile.DirectoryName);
                        Console.WriteLine($"Creating folder {dstFile.DirectoryName}...");
                    }

                    if (onCopyFile == null)
                    {
                        Console.WriteLine($"Using default copy, overwrite is: {overwrite}");
                        Console.WriteLine($"Copying {srcFile.FullName} -> {dstFile.FullName}");
                        File.Copy(srcFile.FullName, dstFile.FullName, overwrite);
                    }
                    else
                    {
                        Console.WriteLine($"Using custom copy for {srcFile.FullName} -> {dstFile.FullName} ...");
                        onCopyFile(srcFile.FullName, dstFile.FullName);
                    }

                    
                    if (File.Exists(dstFile.FullName) && copyPermissions)
                    {
                        var srcFilePermissions = new UnixFileInfo(srcFile.FullName);
                        var dstFilePermissions = new UnixFileInfo(dstFile.FullName);
                        // If the file was copied and we've been asked to copy permissions, do so
                        // https://stackoverflow.com/a/45135498/802203
                        dstFilePermissions.FileAccessPermissions = srcFilePermissions.FileAccessPermissions;
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

        public static IDictionary<string, string> LoadEnvFile(this string filename)
        {
            var result = new Dictionary<string, string>();
            if (File.Exists(filename))
                try
                {
                    using (var reader = new StreamReader(filename))
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine().Trim();
                        
                        // Ignore blank lines and any lines that begin with #
                        // https://docs.docker.com/compose/env-file/
                        if (line.StartsWith("#") || string.IsNullOrEmpty(line))
                            continue;
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

        public static ICli SetEnvironmentVariables(this ICli cli, IDictionary<string, string> env)
        {
            foreach (var kvp in env)
                cli = cli.SetEnvironmentVariable(kvp.Key, kvp.Value);
            
            return cli;                
        }

        public static string Bash(this string cmd)
        {
            var escapedArgs = cmd.Replace("\"", "\\\"");
            
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{escapedArgs}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result;
        }

        // Determine if a given file is binary or not
        // Adapted from: https://stackoverflow.com/a/26652983/802203
        private static char NUL = (char)0; // Null char
        private static char BS = (char)8; // Back Space
        private static char CR = (char)13; // Carriage Return
        private static char SUB = (char)26; // Substitute
        internal static bool isControlChar(this int ch)
        {
            return (ch > NUL && ch < BS)
                || (ch > CR && ch < SUB);
        }
        public static bool IsBinary(this string path)
        {
            int ch;
            using (var reader = new StreamReader(path))
                while ((ch = reader.Read()) != -1)
                    if (ch.isControlChar())
                        return true;
            return false;
        }

        private static readonly Regex RequiredVariables = new Regex(@"\$\{(?<variable>\w+):?(\?(?<prompt>""[^""\\]*(?:\\.[^""\\]*)*""))", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        // TODO: Add timeout and cancellation support for hook execution
        public static bool ExecuteHook(this string hookFilename, IDictionary<string, string> hookEnv, string workingDirectory = null, IDictionary<string, string> environment = null, bool forceReEntry = false)
        {
            // Initialize a blank environent if nothing has been provided
            environment = environment ?? new Dictionary<string, string>();
            workingDirectory = workingDirectory ?? Environment.CurrentDirectory;

            try
            {
                if (File.Exists(hookFilename))
                {
                    try
                    {
                        // If the file isn't a binary file, load it and find variables of the form ${varName?Prompt to enter varName here}
                        if (!hookFilename.IsBinary())
                        {
                            var matches = RequiredVariables.Matches(File.ReadAllText(hookFilename));
                            foreach (Match match in matches)
                            {
                                var variableName = match.Groups["variable"].Value;
                                var prompt = match.Groups["prompt"].Value;

                                // Skip if pre-activate hook env already defines this required variable
                                if (!forceReEntry && hookEnv.TryGetValue(variableName, out var definedValue))
                                {
                                    Console.WriteLine($"Skipping user-input of ${variableName} because it was already defined with value \"{definedValue}\"");
                                    continue;
                                }
                                
                                // Print the prompt and have the user enter the value
                                Console.Write($"{prompt} > ");
                                var variableValue = Console.ReadLine();

                                // which we'll store in templateEnv
                                hookEnv[variableName] = variableValue;
                            }
                        }

                        // Now when we execute the hook, it will have the correct environment
                        // pre-defined or otherwise
                        var result = Cli.Wrap(hookFilename)
                            .SetEnvironmentVariables(environment)
                            .SetEnvironmentVariables(hookEnv) // Hook env. variables supersede passed-in env
                            .SetWorkingDirectory(Environment.CurrentDirectory)
                            .SetStandardOutputCallback(l => Console.WriteLine(l))
                            .SetStandardErrorCallback(l => Console.Error.WriteLine(l))
                            .Execute();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error running hook \"{hookFilename}\"! {ex.Message}\n{ex.StackTrace}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Exception: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
            return true;
        }

        public static void CopyTo(this IDictionary<string, string> env, StringDictionary outEnv)
        {
            foreach (var kvp in env)
                outEnv.Add(kvp.Key, kvp.Value);
        }
    }
}
