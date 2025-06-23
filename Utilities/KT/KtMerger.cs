using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AemulusModManager.Utilities.KT
{
    public static class KtMerger
    {
        public static string[] original_data = new string[] { "0x0018d31b.file", "0x272c6efb.file", "0x282630a0.file",
                "0x321d1476.file", "0x37552941.file", "0x3ab77c3b.file", "0x468421e2.file", "0x55b31a83.file",
                "0x5af9ec1e.file", "0x677fed08.file", "0x8a4bdbd7.file", "0x99596bfb.file", "0x9bdb529c.file",
                "0xab0a4b3d.file", "0xbb926ba0.file", "0xc42cd365.file", "0xcb51f50c.file", "0xd2b2d491.file" };

        public static async Task Backup(string modPath)
        {
            var backupPath = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}/Original/Persona 5 Strikers/motor_rsc";
            Directory.CreateDirectory($@"{backupPath}/data");

            var backupTasks = new List<Task>();
            foreach (var file in original_data)
            {
                backupTasks.Add(Task.Run(() =>
                {
                    ParallelLogger.Log($@"[INFO] Backing up {modPath}\data\{file}");
                    if (File.Exists($@"{modPath}\data\{file}"))
                        File.Copy($@"{modPath}/data/{file}", $@"{backupPath}/data/{file}", true);
                    else
                        ParallelLogger.Log($@"[ERROR] Couldn't find {modPath}\data\{file}");
                }));
            }
            foreach (var rdb in Directory.GetFiles(modPath, "*.rdb"))
            {
                backupTasks.Add(Task.Run(() =>
                {
                    ParallelLogger.Log($"[INFO] Backing up {rdb}");
                    File.Copy(rdb, $@"{backupPath}/{Path.GetFileName(rdb)}", true);
                }));
            }
            await Task.WhenAll(backupTasks);
        }

        public static string GetChecksumString(string filePath)
        {
            string checksumString = null;

            // get md5 checksum of file
            using (var md5 = MD5.Create())
            {
                using (var stream = new BufferedStream(File.OpenRead(filePath), 12000000))
                {
                    // get hash
                    byte[] currentFileSum = md5.ComputeHash(stream);
                    // convert hash to string
                    checksumString = BitConverter.ToString(currentFileSum).Replace("-", "");
                }
            }

            return checksumString;
        }
        public static async Task Restart(string modPath)
        {
            ParallelLogger.Log($"[INFO] Restoring directory to original state...");
            // Just in case its missing for some reason
            Directory.CreateDirectory($@"{modPath}\data");
            // Clear data directory
            Parallel.ForEach(Directory.GetFiles($@"{modPath}\data"), file =>
            {
                // Delete if not found in Original
                if (!File.Exists($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 5 Strikers\motor_rsc\data\{Path.GetFileName(file)}"))
                {
                    ParallelLogger.Log($@"[INFO] Deleting {file}...");
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception e)
                    {
                        ParallelLogger.Log($"[ERROR] Couldn't delete {file} ({e.Message})");
                    }
                }
                // Overwrite if file size/date modified are different
                else if (new FileInfo(file).Length != new FileInfo($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 5 Strikers\motor_rsc\data\{Path.GetFileName(file)}").Length
                    || File.GetLastWriteTime(file) != File.GetLastWriteTime($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 5 Strikers\motor_rsc\data\{Path.GetFileName(file)}"))
                {
                    ParallelLogger.Log($@"[INFO] Reverting {file} to original...");
                    try
                    {
                        File.Copy($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 5 Strikers\motor_rsc\data\{Path.GetFileName(file)}", file, true);
                    }
                    catch (Exception e)
                    {
                        ParallelLogger.Log($"[ERROR] Couldn't overwrite {file} ({e.Message})");
                    }
                }
            });

            // Copy over original files that may have accidentally been deleted
            var restoreTasks = new List<Task>();
            foreach (var file in original_data)
            {
                restoreTasks.Add(Task.Run(() =>
                {
                    if (!File.Exists($@"{modPath}\data\{file}") && File.Exists($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 5 Strikers\motor_rsc\data\{file}"))
                    {
                        ParallelLogger.Log($"[INFO] Restoring {file}...");
                        try
                        {
                            File.Copy($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 5 Strikers\motor_rsc\data\{file}", $@"{modPath}\data\{file}", true);
                        }
                        catch (Exception e)
                        {
                            ParallelLogger.Log($"[ERROR] Couldn't copy over {file} ({e.Message})");
                        }
                    }
                }));
            }
            await Task.WhenAll(restoreTasks);

            // Copy over backed up original rdbs
            var revertTasks = new List<Task>();
            foreach (var file in Directory.GetFiles(modPath, "*.rdb"))
            {
                revertTasks.Add(Task.Run(() =>
                {
                    ParallelLogger.Log($@"[INFO] Reverting {file} to original...");
                    try
                    {
                        File.Copy($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 5 Strikers\motor_rsc\{Path.GetFileName(file)}", file, true);
                    }
                    catch (Exception e)
                    {
                        ParallelLogger.Log($"[ERROR] Couldn't overwrite {file} ({e.Message})");
                    }
                }));
            }
            await Task.WhenAll(revertTasks);

        }
        public static async Task Merge(List<string> ModList, string modDir)
        {
            foreach (var mod in ModList)
            {
                if (!Directory.Exists(mod))
                {
                    ParallelLogger.Log($"[ERROR] Cannot find {mod}");
                    continue;
                }

                // Run prebuild.bat
                if (File.Exists($@"{mod}\prebuild.bat") && new FileInfo($@"{mod}\prebuild.bat").Length > 0)
                {
                    ParallelLogger.Log($@"[INFO] Running {mod}\prebuild.bat...");

                    ProcessStartInfo ProcessInfo;

                    ProcessInfo = new ProcessStartInfo();
                    ProcessInfo.FileName = Path.GetFullPath($@"{mod}\prebuild.bat");
                    ProcessInfo.CreateNoWindow = true;
                    ProcessInfo.UseShellExecute = false;
                    ProcessInfo.WorkingDirectory = Path.GetFullPath(mod);

                    using (Process process = new Process())
                    {
                        process.StartInfo = ProcessInfo;
                        process.Start();
                        process.WaitForExit();
                    }

                    ParallelLogger.Log($@"[INFO] Finished running {mod}\prebuild.bat!");
                }

                if (!Directory.Exists($@"{mod}\data"))
                {
                    ParallelLogger.Log($"[WARNING] No data folder found in {mod}, skipping...");
                    continue;
                }

                // Copy over .files, hashing them when neccessary
                var tasks = new List<Task>();
                foreach (var file in Directory.GetFiles($@"{mod}\data", "*", SearchOption.AllDirectories))
                {
                    tasks.Add(Task.Run(() =>
                    {
                        string fileName = Path.GetFileName(file);
                        if (Path.GetExtension(file).ToLower() != ".file")
                            fileName = Hash(Path.GetFileName(file));
                        ParallelLogger.Log($@"[INFO] Copying over {file} to {modDir}\data\{fileName}");
                        try
                        {
                            File.Copy(file, $@"{modDir}\data\{fileName.ToLower()}", true);
                        }
                        catch (Exception e)
                        {
                            ParallelLogger.Log($"[ERROR] Couldn't copy over {file} ({e.Message})");
                        }
                    }));
                }
                await Task.WhenAll(tasks);
            }

        }

        public static async Task Patch(string modDir)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\rdb_tool.exe";
            if (!File.Exists(startInfo.FileName))
            {
                ParallelLogger.Log($"[ERROR] Couldn't find {startInfo.FileName}. Please check if it was blocked by your anti-virus.");
                return;
            }

            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.RedirectStandardOutput = true;
            startInfo.UseShellExecute = false;

            var tasks = new List<Task>();
            foreach (var rdb in Directory.GetFiles(modDir, "*.rdb"))
            {
                tasks.Add(Task.Run(() =>
                {
                    ParallelLogger.Log($"[INFO] Patching {rdb}...");
                    using (Process process = new Process())
                    {
                        process.StartInfo = startInfo;
                        process.StartInfo.Arguments = $@"""{rdb}"" ""{rdb}""";
                        process.Start();
                        while (!process.HasExited)
                        {
                            string text = process.StandardOutput.ReadLine();
                            if (text != "" && text != null)
                                ParallelLogger.Log($"[INFO] {text}");
                        }
                    }
                }));
            }
            await Task.WhenAll(tasks);
        }

        public static void UpperAll(string modDir)
        {
            Utilities.ParallelLogger.Log($"[INFO] Attempting to rename all in {modDir} to uppercase for platform support.");
            Stack<string> directoryStack = new Stack<string>();
            directoryStack.Push(modDir);
            while (directoryStack.Count > 0)
            {
                string currentDir = directoryStack.Pop();
                if (Directory.Exists(currentDir))
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(currentDir);
                    foreach (DirectoryInfo subdir in directoryInfo.GetDirectories())
                    {
                        string newName = subdir.FullName.ToUpper();
                        if (newName != subdir.FullName)
                        {
                            Directory.Move(subdir.FullName, $@"{subdir.FullName}temp");
                            Directory.Move($@"{subdir.FullName}temp", newName);
                        }
                        directoryStack.Push(newName);
                    }
                    foreach (FileInfo file in directoryInfo.GetFiles())
                    {
                        string name = Path.GetFileNameWithoutExtension(file.Name);
                        string extension = Path.GetExtension(file.Name);

                        string newName = Path.Combine(directoryInfo.FullName, name.ToUpper() + extension.ToUpper());
                        if (newName != file.FullName)
                        {
                            File.Move(file.FullName, $@"{file.FullName}temp");
                            File.Move($@"{file.FullName}temp", newName);
                        }
                    }
                }
                else
                {
                    throw new DirectoryNotFoundException("Directory not found: " + currentDir);
                }
            }
        }

        public static string Hash(string file)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            if (Regex.IsMatch(fileName, "[a-fA-F0-9]") && fileName.Length == 8)
                return $"0x{fileName}.file";

            string ext = $"R_{Path.GetExtension(file).ToUpper().Replace(".", "")}";
            byte[] HASH_PREFIX = { 0xEF, 0xBC, 0xBB };
            byte[] HASH_SUFFIX = { 0xEF, 0xBC, 0xBD };
            byte HASH_KEY = 0x1F;
            // R_<ext><prefix><fileName><suffix>
            byte[] hash = Encoding.UTF8.GetBytes(ext).Concat(HASH_PREFIX).Concat(Encoding.UTF8.GetBytes(fileName)).Concat(HASH_SUFFIX).ToArray();

            int iv = hash[0] * HASH_KEY;
            int key = HASH_KEY;
            byte[] text = SliceArray(hash, 1, hash.Length);
            unchecked
            {
                foreach (var ch in text)
                {
                    var state = key;
                    key *= HASH_KEY;
                    iv += HASH_KEY * state * (sbyte)ch;
                }
            }
            // Convert hash to byte string and switch endianness
            byte[] ivBytes = BitConverter.GetBytes((uint)iv);
            Array.Reverse(ivBytes);
            return $"0x{BitConverter.ToString(ivBytes).ToLower().Replace("-", "")}.file";
        }

        private static byte[] SliceArray(byte[] source, int start, int end)
        {
            int length = end - start;
            byte[] dest = new byte[length];
            Array.Copy(source, start, dest, 0, length);
            return dest;
        }
    }
}
