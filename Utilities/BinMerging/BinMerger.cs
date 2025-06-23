using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AemulusModManager.Utilities;
using AemulusModManager.Utilities.AwbMerging;
using AemulusModManager.Utilities.FileMerging;

namespace AemulusModManager
{
    public static class binMerge
    {
        private static string aemDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        private static string exePath = Path.Combine(aemDir, "Dependencies", "PAKPack", "PAKPack.exe");

        private static string[] doNotCopy = { ".aem", ".tblpatch", ".tbp", ".xml", ".json", ".png", ".jpg", ".7z", ".zip", ".rar", ".bat", ".exe", ".dll", ".flow", ".msg", ".bf", ".bmd", ".pm1", ".back", ".bp", ".pnach", ".txt" };
        private static string[] pakExtensions = { ".pak", ".pac", ".pack", ".bin", ".abin", ".tpc", ".fpc", ".gsd", ".arc" };

        // Use PAKPack command
        public static void PAKPackCMD(string args)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.FileName = $"\"{exePath}\"";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.Arguments = args;
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();

                // Add this: wait until process does its work
                process.WaitForExit();
            }
        }

        public static List<string> getFileContents(string path)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.FileName = $"\"{exePath}\"";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.Arguments = $"list \"{path}\"";
            List<string> contents = new List<string>();
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                while (!process.StandardOutput.EndOfStream)
                {
                    string line = process.StandardOutput.ReadLine();
                    if (!line.Contains(" "))
                    {
                        contents.Add(line);
                    }
                }
                // Add this: wait until process does its work
                process.WaitForExit();
            }
            return contents;
        }
        private static int commonPrefixUtil(String str1, String str2)
        {
            String result = "";

            // Compare str1 and str2  
            for (int i = 0, j = 0;
                     i < str1.Length && j < str2.Length;
                     i++, j++)
            {
                if (!str1[i].ToString().Equals(str2[j].ToString(), StringComparison.InvariantCultureIgnoreCase))
                {
                    break;
                }
                result += str1[i];
            }

            return result.Length;
        }

        private static string FindLongest(IEnumerable<string> strs)
        {
            int longestPrefixLen = 0;
            string longestPrefix = String.Empty;

            foreach(var str in strs)
            {
                if(str.Length > longestPrefixLen)
                {
                    longestPrefixLen = str.Length;
                    longestPrefix = str;
                }
            }

            return longestPrefix;

        }

        private static List<string> ReadModsAem(string dir)
        {
            List<string> moddedFiles = new List<string>();
            string line;
            var list = $"{dir}/mods.aem";
            if (File.Exists(list))
            {
                using (StreamReader stream = new StreamReader(list))
                {
                    while ((line = stream.ReadLine()) != null)
                    {
                        moddedFiles.Add(line);
                    }
                }
            }
            return moddedFiles;
        }
        public static void TryDeleteDirectory(string path)
        {
            try
            {
                Directory.Delete(path, true);
            }
            catch (Exception ex)
            {
                Utilities.ParallelLogger.Log("[ERROR] An error occurred: " + ex.Message);
            }

        }

        public static bool ArchiveExists(string path, out string extension)
        {
            foreach (string pakExtension in pakExtensions)
            {
                if (File.Exists(Path.ChangeExtension(path, pakExtension)))
                { 
                    extension = pakExtension;
                    return true;
                }
            }
            extension = null;
            return false;
        }

        private static void UnpackBin(string bin, string game)
        {
            Utilities.ParallelLogger.Log($@"[INFO] Unpacking {bin}...");
            // Unpack and transfer modified parts if base already exists
            PAKPackCMD($"unpack \"{bin}\"");
            // Unpack fully before comparing to mods.aem
            foreach (var file in Directory.GetFiles(Path.ChangeExtension(bin, null), "*", SearchOption.AllDirectories))
            {
                if (pakExtensions.Contains(Path.GetExtension(file).ToLower()))
                {
                    UnpackBin(file, game);
            }
                else if (Path.GetExtension(file).ToLower() == ".spd")
            {
                    Directory.CreateDirectory(Path.ChangeExtension(file, null));
                    List<DDS> ddsFiles = spdUtils.getDDSFiles(file);
                    string spdFolder = Path.ChangeExtension(file, null);
                    foreach (var ddsFile in ddsFiles)
                    {
                        File.WriteAllBytes(Path.Combine(spdFolder, Path.ChangeExtension(ddsFile.name, ".dds")), ddsFile.file);
            }
                    List<SPDKey> spdKeys = spdUtils.getSPDKeys(file);
                    foreach (var spdKey in spdKeys)
                    {
                        File.WriteAllBytes(Path.Combine(spdFolder, Path.ChangeExtension(spdKey.id.ToString(), ".spdspr")), spdKey.file);
                    }
                }
                else if (Path.GetExtension(file) == ".spr" && game != "Persona Q" && game != "Persona Q2")
                {
                    Utilities.ParallelLogger.Log($@"[INFO] Unpacking {file}...");
                    string sprFolder = Path.ChangeExtension(file, null);
                    Directory.CreateDirectory(sprFolder);
                    Dictionary<string, int> tmxNames = sprUtils.getTmxNames(file);
                    foreach (string name in tmxNames.Keys)
                    {
                        byte[] tmx = sprUtils.extractTmx(file, name);
                        File.WriteAllBytes(Path.Combine(sprFolder, Path.ChangeExtension(name, ".tmx")), tmx);
                    }
                }

        }
        }

        public static async Task CopyAndUnpackBins(List<string> ModList, string buildDir, bool useCpk, string cpkLang, string game)
        {
            if (!File.Exists(exePath))
            {
                Utilities.ParallelLogger.Log($"[ERROR] Couldn't find {exePath}. Please check if it was blocked by your anti-virus.");
                return;
            }
            Utilities.ParallelLogger.Log("[INFO] Beginning to unpack...");
            // Copy over base PATCH1 file
            if (game == "Persona 5 Royal (Switch)")
            {
                var relativeUsmPath = "PATCH1/MOVIE/MOV000.USM";
                var originalUsmPath = Path.Combine(aemDir, "Original", game, relativeUsmPath);
                var moddedUsmPath = Path.Combine(buildDir, relativeUsmPath);
                if (File.Exists(originalUsmPath))
                {
                    Utilities.ParallelLogger.Log($"[INFO] Copying over base PATCH1 file");
                    Directory.CreateDirectory(Path.GetDirectoryName(moddedUsmPath));
                    File.Copy(originalUsmPath, moddedUsmPath, true);
                }
                else
                    Utilities.ParallelLogger.Log($@"[WARNING] {originalUsmPath} not found, try unpacking base files again");
            }
            foreach (var mod in ModList)
            {
                if (!Directory.Exists(mod))
                {
                    Utilities.ParallelLogger.Log($"[ERROR] Cannot find {mod}");
                    continue;
                }

                // Run prebuild.bat
                if (File.Exists($"{mod}/prebuild.bat") && new FileInfo($"{mod}/prebuild.bat").Length > 0)
                {
                    Utilities.ParallelLogger.Log($"[INFO] Running {mod}/prebuild.bat...");

                    ProcessStartInfo ProcessInfo;

                    ProcessInfo = new ProcessStartInfo();
                    ProcessInfo.FileName = Path.GetFullPath($"{mod}/prebuild.bat");
                    ProcessInfo.CreateNoWindow = true;
                    ProcessInfo.UseShellExecute = false;
                    ProcessInfo.WorkingDirectory = Path.GetFullPath(mod);

                    using (Process process = new Process())
                    {
                        process.StartInfo = ProcessInfo;
                        process.Start();
                        process.WaitForExit();
                    }

                    Utilities.ParallelLogger.Log($"[INFO] Finished running {mod}/prebuild.bat!");
                }

                List<string> modsAem = ReadModsAem(mod);
                var fileTasks = new List<Task>();
                string[] AemIgnore = File.Exists($"{mod}/Ignore.aem") ? File.ReadAllLines($"{mod}/Ignore.aem") : null;
                // Copy and overwrite everything thats not a bin
                foreach (var file in Directory.GetFiles(mod, "*", SearchOption.AllDirectories))
                {
                    fileTasks.Add(Task.Run(() =>
                    {
                        var relativePath = Path.GetRelativePath(mod, file);
                        // Copy everything except mods.aem and tblpatch to directory
                        if (doNotCopy.Contains(Path.GetExtension(file).ToLower())
                            || Path.GetDirectoryName(file).Contains("spdpatches")
                            || Path.GetFileNameWithoutExtension(file).ToLower() == "preview"
                            || relativePath.ToLower().Contains($"texture_override{Path.DirectorySeparatorChar}") //check if the file is in texture_override folder
                            || (game == "Persona 3 Portable" && relativePath.ToLower().Contains($"fmv{Path.DirectorySeparatorChar}")) //check if the file is an FMV for P3P
                            || ((game == "Persona 3 Portable" || game == "Persona 1 (PSP)") && relativePath.ToLower().Contains($"cheats{Path.DirectorySeparatorChar}")))
                        {
                            return;
                        }
                        string binPath = Path.Join(buildDir, relativePath);
                        string ogBinPath = Path.Join(aemDir, "Original", game, relativePath);

                        if ((AemIgnore != null && AemIgnore.Any(file.Contains)) || AwbMerger.SoundArchiveExists(Path.GetDirectoryName(ogBinPath)))
                            return;
                        
                        if (game != "Persona 1 (PSP)" && pakExtensions.Contains(Path.GetExtension(file).ToLower()))
                        {
                            if (File.Exists(ogBinPath) && modsAem.Count > 0)
                            {
                                // Check if mods.aem contains the modified parts of a bin
                                if (!modsAem.Exists(x => x.Contains($@"{relativePath.Replace('/', '\\')}\")))
                                {
                                    Utilities.ParallelLogger.Log($"[WARNING] Using {binPath} as base since nothing was specified in mods.aem");
                                    if (useCpk)
                                        binPath = Regex.Replace(binPath, "data0000[0-6]", Path.GetFileNameWithoutExtension(cpkLang));
                                    Directory.CreateDirectory(Path.GetDirectoryName(binPath));
                                    File.Copy(file, binPath, true);
                                    return;
                                }

                                UnpackBin(file, game);
                            }
                            else
                            {
                                if (useCpk)
                                {
                                    binPath = Regex.Replace(binPath, "data0000[0-6]", Path.GetFileNameWithoutExtension(cpkLang));
                                    binPath = Regex.Replace(binPath, "movie0000[0-2]", "movie");
                                }
                                Directory.CreateDirectory(Path.GetDirectoryName(binPath));
                                File.Copy(file, binPath, true);
                                Utilities.ParallelLogger.Log($"[INFO] Copying over {file} to {binPath}");
                            }
                        }
                        else if (game != "Persona 1 (PSP)" && Path.GetExtension(file).ToLower() == ".spd")
                        {
                            if (File.Exists(ogBinPath) && modsAem.Count > 0)
                            {
                                Utilities.ParallelLogger.Log($@"[INFO] Unpacking {file}...");
                                string spdFolder = Path.ChangeExtension(file, null);
                                Directory.CreateDirectory(spdFolder);

                                List<DDS> ddsFiles = spdUtils.getDDSFiles(file);
                                foreach (var ddsFile in ddsFiles)
                                {
                                    File.WriteAllBytes(Path.Combine(spdFolder, Path.ChangeExtension(ddsFile.name, ".dds")), ddsFile.file);
                                }
                                List<SPDKey> spdKeys = spdUtils.getSPDKeys(file);
                                foreach (var spdKey in spdKeys)
                                {
                                    File.WriteAllBytes(Path.Combine(spdFolder, Path.ChangeExtension(spdKey.id.ToString(), ".spdspr")), spdKey.file);
                                }
                            }
                            else
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(binPath));
                                File.Copy(file, binPath, true);
                                Utilities.ParallelLogger.Log($"[INFO] Copying over {file} to {binPath}");
                            }
                        }
                        else
                        {
                            if (useCpk)
                            {
                                binPath = Regex.Replace(binPath, "data0000[0-6]", Path.GetFileNameWithoutExtension(cpkLang));
                                binPath = Regex.Replace(binPath, "movie0000[0-2]", "movie");
                            }
                            Directory.CreateDirectory(Path.GetDirectoryName(binPath));
                            File.Copy(file, binPath, true);
                            Utilities.ParallelLogger.Log($"[INFO] Copying over {file} to {binPath}");
                        }
                    }));
                }
                await Task.WhenAll(fileTasks);

                // Copy over loose files specified by mods.aem
                var modsAemTasks = new List<Task>();
                foreach (var m in modsAem)
                {
                    modsAemTasks.Add(Task.Run(() =>
                    {
                        if (File.Exists($"{mod}/{m}"))
                        {
                            string dir = $"{buildDir}/{m}";
                            if (useCpk)
                            {
                                dir = Regex.Replace(dir, "data0000[0-6]", Path.GetFileNameWithoutExtension(cpkLang));
                                dir = Regex.Replace(dir, "movie0000[0-2]", "movie");
                            }
                            Directory.CreateDirectory(Path.GetDirectoryName(dir));
                            File.Copy($@"{mod}\{m}", dir, true);
                            Utilities.ParallelLogger.Log($@"[INFO] Copying over {mod}\{m} as specified by mods.aem");
                        }
                    }));
                }
                await Task.WhenAll(modsAemTasks);

                if (game != "Persona 1 (PSP)")
                {
                    // Go through mod directory again to delete unpacked files after bringing them in
                    var deletionTasks = new List<Task>();
                    foreach (var file in Directory.GetFiles(mod, "*", SearchOption.AllDirectories))
                    {
                        deletionTasks.Add(Task.Run(() =>
                        {
                            if (pakExtensions.Contains(Path.GetExtension(file).ToLower())
                            && Directory.Exists(Path.ChangeExtension(file, null))
                            && Path.GetFileNameWithoutExtension(file) != "result"
                            && Path.GetFileNameWithoutExtension(file) != "panel"
                            && Path.GetFileNameWithoutExtension(file) != "crossword")
                            {
                                TryDeleteDirectory(Path.ChangeExtension(file, null));
                            }
                        }));
                    }

                    if (File.Exists($@"{mod}/battle/result.pac") && Directory.Exists($@"{mod}/battle/result/result"))
                    {
                        foreach (var f in Directory.GetFiles($@"{mod}/battle/result/result"))
                        {
                            deletionTasks.Add(Task.Run(() =>
                            {
                                if (Path.GetExtension(f).ToLower() == ".gfs" || Path.GetExtension(f).ToLower() == ".gmd")
                                    File.Delete(f);
                            }));
                        }
                    }
                    if (File.Exists($@"{mod}/battle/result/result.spd") && Directory.Exists($@"{mod}/battle/result/result"))
                    {
                        foreach (var f in Directory.GetFiles($@"{mod}/battle/result/result"))
                        {
                            deletionTasks.Add(Task.Run(() =>
                            {
                                if (Path.GetExtension(f).ToLower() == ".dds" || Path.GetExtension(f).ToLower() == ".spdspr")
                                    File.Delete(f);
                            }));
                        }
                    }
                    if (File.Exists($@"{mod}/field/panel.bin") && Directory.Exists($@"{mod}/field/panel/panel"))
                        TryDeleteDirectory($@"{mod}/field/panel/panel");
                    if (Directory.Exists($@"{mod}/battle/result/result") && !Directory.GetFiles($@"{mod}/battle/result/result", "*", SearchOption.AllDirectories).Any())
                        TryDeleteDirectory($@"{mod}/battle/result/result");
                    if (Directory.Exists($@"{mod}/battle/result") && !Directory.GetFiles($@"{mod}/battle/result", "*", SearchOption.AllDirectories).Any())
                        TryDeleteDirectory($@"{mod}/battle/result");
                    if (Directory.Exists($@"{mod}/field/panel") && !Directory.EnumerateFileSystemEntries($@"{mod}/field/panel").Any())
                        TryDeleteDirectory($@"{mod}/field/panel");
                    if ((File.Exists($@"{mod}/minigame/crossword.pak") || File.Exists($@"{mod}/minigame/crossword.spd")) && Directory.Exists($@"{mod}/minigame/crossword"))
                    {
                        foreach (var f in Directory.GetFiles($@"{mod}/minigame/crossword"))
                        {
                            deletionTasks.Add(Task.Run(() =>
                            {
                                if (Path.GetExtension(f).ToLower() != ".pak")
                                    File.Delete(f);
                            }));
                        }
                    }
                    if (Directory.Exists($@"{mod}/minigame/crossword") && !Directory.GetFiles($@"{mod}/minigame/crossword", "*", SearchOption.AllDirectories).Any())
                        TryDeleteDirectory($@"{mod}/minigame/crossword");

                    await Task.WhenAll(deletionTasks);
                }
            }
            Utilities.ParallelLogger.Log("[INFO] Finished unpacking!");
        }

        public static async Task Merge(string modDir, string game)
        {
            Utilities.ParallelLogger.Log("[INFO] Beginning to merge...");
            // Check if loose folder matches vanilla bin file
            var copyTasks = new List<Task>();
            foreach (var d in Directory.GetDirectories(modDir, "*", SearchOption.AllDirectories))
            {
                copyTasks.Add(Task.Run(() =>
                {
                    var relativePath = Path.GetRelativePath(modDir, d);
                    string ogPath = Path.Combine(aemDir, "Original", game, relativePath);

                    if (ArchiveExists(ogPath, out string extension) && !File.Exists(Path.ChangeExtension(d, extension)))
                    {
                        ogPath = Path.ChangeExtension(ogPath, extension);
                        if (Path.GetFileName(ogPath) == "panel.bin" && !Directory.Exists($@"{d}/panel")) { return; }
                        if (Path.GetFileName(ogPath) == "result.pac" &&
                            (!Directory.Exists($@"{d}/result") ||
                            (!Directory.GetFiles($@"{d}/result", "*.GFS", SearchOption.TopDirectoryOnly).Any() && !Directory.GetFiles($@"{d}/result", "*.GMD", SearchOption.TopDirectoryOnly).Any())))
                            return;

                        if (Path.GetFileName(ogPath) == "crossword.pak" &&
                            !Directory.GetFiles(d, "*.dds", SearchOption.AllDirectories).Any() &&
                            !Directory.GetFiles(d, "*.spdspr", SearchOption.AllDirectories).Any() &&
                            !Directory.GetFiles(d, "*.bmd", SearchOption.AllDirectories).Any() &&
                            !Directory.GetFiles(d, "*.plg", SearchOption.AllDirectories).Any())
                            return;

                        Utilities.ParallelLogger.Log($"[INFO] Copying over {ogPath} to use as base.");
                        Directory.CreateDirectory(Path.GetDirectoryName(d));
                        File.Copy(ogPath, Path.ChangeExtension(d, extension));
                    }
                    if (game != "Persona Q2" && File.Exists(Path.ChangeExtension(ogPath, ".spr")) && !File.Exists(Path.ChangeExtension(d, ".spr")))
                    {
                        ogPath = Path.ChangeExtension(ogPath, ".spr");
                        Utilities.ParallelLogger.Log($"[INFO] Copying over {ogPath} to use as base.");
                        Directory.CreateDirectory(Path.GetDirectoryName(d));
                        File.Copy(ogPath, Path.ChangeExtension(d, ".spr"));
                    }
                    if (File.Exists(Path.ChangeExtension(ogPath, ".spd")) && !File.Exists(Path.ChangeExtension(d, ".spd")))
                    {
                        ogPath = Path.ChangeExtension(ogPath, ".spd");
                        if (Path.GetFileName(ogPath) == "result.spd")
                        {
                            if (!Directory.GetFiles(d, "*.dds", SearchOption.TopDirectoryOnly).Any()
                                && !Directory.GetFiles(d, "*.spdspr", SearchOption.TopDirectoryOnly).Any())
                                return;
                        }
                        if (Path.GetFileName(ogPath) == "crossword.spd")
                        {
                            if (!Directory.GetFiles(d, "*.dds", SearchOption.TopDirectoryOnly).Any()
                                && !Directory.GetFiles(d, "*.spdspr", SearchOption.TopDirectoryOnly).Any())
                                return;
                        }
                        Utilities.ParallelLogger.Log($"[INFO] Copying over {ogPath} to use as base.");
                        Directory.CreateDirectory(Path.GetDirectoryName(d));
                        File.Copy(ogPath, Path.ChangeExtension(d, ".spd"));
                    }
                }));
            }
            await Task.WhenAll(copyTasks);

            var mergeTasks = new List<Task>();
            foreach (var file in Directory.GetFiles(modDir, "*", SearchOption.AllDirectories).Where(x =>
                pakExtensions.Contains(Path.GetExtension(x).ToLower())
                || Path.GetExtension(x).Equals(".spd", StringComparison.InvariantCultureIgnoreCase)
                || (game != "Persona Q" && game != "Persona Q2" && Path.GetExtension(x).Equals(".spr", StringComparison.InvariantCultureIgnoreCase))))
            {
                mergeTasks.Add(Task.Run(() =>
                {
                    RepackArchive(game, file);
                }));
            }
            await Task.WhenAll(copyTasks);
            
            // Go through mod directory again to delete unpacked files after bringing them in
            var deletionTasks = new List<Task>();
            foreach (var file in Directory.GetFiles(modDir, "*", SearchOption.AllDirectories))
            {
                deletionTasks.Add(Task.Run(() =>
                {
                    if ((pakExtensions.Contains(Path.GetExtension(file)) || Path.GetExtension(file) == ".spd" || Path.GetExtension(file) == ".spr")
                    && Directory.Exists(Path.ChangeExtension(file, null))
                    && Path.GetFileNameWithoutExtension(file) != "result"
                    && Path.GetFileNameWithoutExtension(file) != "panel"
                    && Path.GetFileNameWithoutExtension(file) != "crossword")
                    {
                        TryDeleteDirectory(Path.ChangeExtension(file, null));
                    }
                }));
            }

            // Hardcoded cases TODO: reimplement extracted folders to have file extensions as part of the name, although would need to refactor every aemulus mod

            if (File.Exists($@"{modDir}/battle/result.pac") && !File.Exists($@"{modDir}/battle/result/result.spd") && Directory.Exists($@"{modDir}/battle/result"))
                TryDeleteDirectory($@"{modDir}/battle/result");
            if (Directory.Exists($@"{modDir}/battle/result/result"))
                TryDeleteDirectory($@"{modDir}/battle/result/result");
            if (Directory.Exists($@"{modDir}/minigame/crossword/crossword"))
                TryDeleteDirectory($@"{modDir}/minigame/crossword/crossword");
            if (Directory.Exists($@"{modDir}/field/panel/panel"))
                TryDeleteDirectory($@"{modDir}/field/panel/panel");
            if (Directory.Exists($@"{modDir}/field/panel") && !Directory.EnumerateFileSystemEntries($@"{modDir}/field/panel").Any())
                TryDeleteDirectory($@"{modDir}/field/panel");

            if (Directory.Exists($@"{modDir}/minigame/crossword/crossword"))
                TryDeleteDirectory($@"{modDir}/minigame/crossword/crossword");
            if (Directory.Exists($@"{modDir}/minigame/crossword"))
            {
                foreach (var file in Directory.GetFiles($@"{modDir}/minigame/crossword", "*", SearchOption.AllDirectories))
                    deletionTasks.Add(Task.Run(() =>
                    {
                        if (Path.GetExtension(file).ToLower() != ".pak")
                            File.Delete(file);
                    }));
            }
            if (Directory.Exists($@"{modDir}/minigame/crossword") && !Directory.EnumerateFileSystemEntries($@"{modDir}/minigame/crossword").Any())
                TryDeleteDirectory($@"{modDir}/minigame/crossword");
            await Task.WhenAll(deletionTasks);

            Utilities.ParallelLogger.Log("[INFO] Finished merging!");
            return;
        }

        private static void RepackArchive(string game, string archive)
        {
            Utilities.ParallelLogger.Log($@"[INFO] Merging {archive}...");
            if (pakExtensions.Contains(Path.GetExtension(archive).ToLower()))
            {
                string binFolder = Path.ChangeExtension(archive, null);
                if (!Directory.Exists(binFolder)) { return; }

                // Get contents of init_free
                List<string> contents = getFileContents(archive);

                foreach (var file in Directory.GetFiles(binFolder, "*", SearchOption.AllDirectories))
                {
                    // Get bin path used for PAKPack.exe
                    string binPath = Path.GetRelativePath(binFolder, file).Replace('\\', '/');
                    // Case for paths in Persona 5 event paks
                    if (contents.Contains($"../../../{binPath}"))
                    {
                        string args = $"replace \"{archive}\" ../../../{binPath} \"{file}\" \"{archive}\"";
                        PAKPackCMD(args);
                    }
                    else if (contents.Contains($"../../{binPath}"))
                    {
                        string args = $"replace \"{archive}\" ../../{binPath} \"{file}\" \"{archive}\"";
                        PAKPackCMD(args);
                    }
                    else if (contents.Contains($"../{binPath}"))
                    {
                        string args = $"replace \"{archive}\" ../{binPath} \"{file}\" \"{archive}\"";
                        PAKPackCMD(args);
                    }
                    // Check if more unpacking needs to be done to replace
                    else if (!contents.Contains(binPath))
                    {
                        var nestedArchives = contents.Where(x => pakExtensions.Contains(Path.GetExtension(x).ToLower())
                            || Path.GetExtension(x).Equals(".spd", StringComparison.InvariantCultureIgnoreCase)
                            || Path.GetExtension(x).Equals(".spr", StringComparison.InvariantCultureIgnoreCase)).ToList();

                        string bin = null;
                        string prefix = String.Empty;
                        if (nestedArchives.Exists(x => binPath.Contains(Path.ChangeExtension(x, null)))) { bin = FindLongest(nestedArchives.Where(x => binPath.Contains(Path.ChangeExtension(x, null)))); }
                        else if (nestedArchives.Exists(x => x.Substring(0, 6) == "../../" && binPath.Contains(Path.ChangeExtension(x.Substring(6), null))))
                        {
                            bin = FindLongest(nestedArchives.Where(x => binPath.Contains(Path.ChangeExtension(x.Substring(6), null))));
                            prefix = "../../";
                        }
                        else if (nestedArchives.Exists(x => x.Substring(0, 3) == "../" && binPath.Contains(Path.ChangeExtension(x.Substring(3), null))))
                        {
                            bin = FindLongest(nestedArchives.Where(x => binPath.Contains(Path.ChangeExtension(x.Substring(3), null))));
                            prefix = "../";
                        }

                        if (bin == null) { continue; } // nothing in archive to replace, ignore

                        // Unpack archive for future unpacking
                        string temp = $"{binFolder}_temp";
                        PAKPackCMD($"unpack \"{archive}\" \"{temp}\"");

                        File.Move(Path.Combine(temp, binPath), file);
                        RepackArchive(game, file);

                        string args = $"replace \"{archive}\" {prefix}{binPath} \"{file}\" \"{archive}\"";
                        PAKPackCMD(args);

                        TryDeleteDirectory(temp);
                    }
                    else
                    {
                        string args = $"replace \"{archive}\" {binPath} \"{file}\" \"{archive}\"";
                        PAKPackCMD(args);
                    }
                }
            }
            else if (Path.GetExtension(archive).ToLower() == ".spd")
            {
                string spdFolder = Path.ChangeExtension(archive, null);
                if (Directory.Exists(spdFolder))
                {
                    foreach (var spdFile in Directory.GetFiles(spdFolder, "*", SearchOption.AllDirectories))
                    {
                        if (Path.GetExtension(spdFile).ToLower() == ".dds")
                        {
                            Utilities.ParallelLogger.Log($"[INFO] Replacing {spdFile} in {archive}");
                            spdUtils.replaceDDS(archive, spdFile);
                        }
                        else if (Path.GetExtension(spdFile).ToLower() == ".spdspr")
                        {
                            spdUtils.replaceSPDKey(archive, spdFile);
                            Utilities.ParallelLogger.Log($"[INFO] Replacing {spdFile} in {archive}");
                        }
                    }
                }
            }
            else if (game != "Persona Q2" && Path.GetExtension(archive).ToLower() == ".spr")
            {
                string sprFolder = Path.ChangeExtension(archive, null);
                if (Directory.Exists(sprFolder))
                {
                    foreach (var sprFile in Directory.GetFiles(sprFolder, "*", SearchOption.AllDirectories))
                    {
                        Utilities.ParallelLogger.Log($"[INFO] Replacing {sprFile} in {archive}");
                        sprUtils.replaceTmx(archive, sprFile);
                    }
                }
            }
        }

        public static async Task Restart(string modDir, bool emptySND, string game, string cpkLang, string cheats, string cheatsWS, bool empty = false)
        {
            Utilities.ParallelLogger.Log("[INFO] Deleting current mod build...");
            // Revert appended cpks
            if (game == "Persona 4 Golden (PC 32-Bit)")
            {
                string path = Path.GetDirectoryName(modDir);
                string origPath = Path.Combine(aemDir, "Original", "Persona 4 Golden (PC 32-Bit)");
                var cpkPath = Path.Combine(path, cpkLang);
                var origMoviePath = Path.Combine(origPath, "movie.cpk");

                // Copy original cpk back if different
                if (File.Exists(Path.Combine(origPath, cpkLang)) && File.Exists(cpkPath)
                    && GetChecksumString(Path.Combine(origPath, cpkLang)) != GetChecksumString(cpkPath))
                {
                    Utilities.ParallelLogger.Log($@"[INFO] Reverting {cpkLang} back to original");
                    File.Copy(Path.Combine(origPath, cpkLang), cpkPath, true);
                }
                // Copy original cpk back if different
                if (File.Exists(origMoviePath) && File.Exists(cpkPath)
                    && GetChecksumString(origMoviePath) != GetChecksumString(Path.Combine(path, "movie.cpk")))
                {
                    Utilities.ParallelLogger.Log($@"[INFO] Reverting movie.cpk back to original");
                    File.Copy(origMoviePath, Path.Combine(path, "movie.cpk"), true);
                }
                // Delete modified pacs
                if (File.Exists($@"{path}/data00007.pac"))
                {
                    Utilities.ParallelLogger.Log($"[INFO] Deleting data00007.pac");
                    File.Delete($@"{path}/data00007.pac");
                }
                if (File.Exists($@"{path}/movie00003.pac"))
                {
                    Utilities.ParallelLogger.Log($"[INFO] Deleting movie00003.pac");
                    File.Delete($@"{path}/movie00003.pac");
                }
            }

            if (!emptySND || game == "Persona 3 FES")
            {
                Utilities.ParallelLogger.Log("[INFO] Keeping SND folder.");
                var deleteDirectories = new List<Task>();
                foreach (var dir in Directory.GetDirectories(modDir))
                {
                    deleteDirectories.Add(Task.Run(() =>
                    {
                        if (Path.GetFileName(dir).ToLower() != "snd")
                            TryDeleteDirectory(dir);
                    }));
                }
                await Task.WhenAll(deleteDirectories);

                // Delete top layer files too
                var deleteFiles = new List<Task>();
                foreach (var file in Directory.GetFiles(modDir))
                {
                    deleteFiles.Add(Task.Run(() =>
                    {
                        if (Path.GetExtension(file).ToLower() != ".elf" && Path.GetExtension(file).ToLower() != ".iso")
                            File.Delete(file);
                    }));
                }
                await Task.WhenAll(deleteFiles);
            }
            else
            {
                if (Directory.Exists(modDir))
                    TryDeleteDirectory(modDir);
                Directory.CreateDirectory(modDir);
            }
            if ((game == "Persona Q2" || game == "Persona Q") && empty)
            {
                File.Create($@"{modDir}/dummy.txt");
                MakeCpk(modDir, true, empty);
            }
            // Delete Aemulus pnaches in cheats folder
            var deleteCheats = new List<Task>();
            if (game == "Persona 3 FES" && cheats != null && Directory.Exists(cheats))
            {
                foreach (var pnach in Directory.GetFiles(cheats, "*_aem.pnach", SearchOption.TopDirectoryOnly))
                    deleteCheats.Add(Task.Run(() => { File.Delete(pnach); }));
            }
            // Delete Aemulus pnaches in cheats_ws folder
            if (game == "Persona 3 FES" && cheatsWS != null && Directory.Exists(cheatsWS))
            {
                foreach (var pnach in Directory.GetFiles(cheatsWS, "*_aem.pnach", SearchOption.TopDirectoryOnly))
                    deleteCheats.Add(Task.Run(() => { File.Delete(pnach); }));
            }
            await Task.WhenAll(deleteCheats);
        }

        public static string GetChecksumString(string filePath)
        {
            string checksumString = null;

            // get md5 checksum of file
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    // get hash
                    byte[] currentFileSum = md5.ComputeHash(stream);
                    // convert hash to string
                    checksumString = BitConverter.ToString(currentFileSum).Replace("-", "");
                }
            }

            return checksumString;
        }

        public static void MakeCpk(string buildDir, bool crc, bool empty = false)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.FileName = Path.Combine(aemDir, "Dependencies", "CpkMakeC", "cpkmakec.exe");
            if (!File.Exists(startInfo.FileName))
            {
                Utilities.ParallelLogger.Log($"[ERROR] Couldn't find {startInfo.FileName}. Please check if it was blocked by your anti-virus.");
                return;
            }
            var extension = Path.GetFileName(buildDir) == "PATCH1" ? "CPK" : "cpk";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.Arguments = $"\"{buildDir}\" \"{buildDir}\".{extension} -mode=FILENAME";
            if (crc)
                startInfo.Arguments += " -crc";
            if (!empty)
                Utilities.ParallelLogger.Log($"[INFO] Building {Path.GetFileName(buildDir)}.{extension}...");
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
            }
        }

        public static async Task LoadCheats(List<string> mods, string cheatsDir)
        {
            foreach (string mod in mods)
            {
                Utilities.ParallelLogger.Log($"[INFO] Searching for cheats in {mod}...");
                if (!Directory.Exists($@"{mod}/cheats"))
                {
                    Utilities.ParallelLogger.Log($"[INFO] No cheats folder found in {mod}");
                    continue;
                }
                // Copy over cheats
                var tasks = new List<Task>();
                foreach (var cheat in Directory.GetFiles($@"{mod}/cheats", "*.pnach", SearchOption.AllDirectories))
                {
                    tasks.Add(Task.Run(() =>
                    {
                        File.Copy(cheat, Path.Combine(cheatsDir, $"{Path.GetFileNameWithoutExtension(cheat)}_aem.pnach"), true);
                        Utilities.ParallelLogger.Log($"[INFO] Copied over {Path.GetFileNameWithoutExtension(cheat)}_aem.pnach to {cheatsDir}");
                    }));
                }
                await Task.WhenAll(tasks);
            }
        }
        public static async Task LoadCheatsWS(List<string> mods, string cheatsDir)
        {
            foreach (string mod in mods)
            {
                Utilities.ParallelLogger.Log($"[INFO] Searching for cheats_ws in {mod}...");
                if (!Directory.Exists($@"{mod}/cheats_ws"))
                {
                    Utilities.ParallelLogger.Log($"[INFO] No cheats_ws folder found in {mod}");
                    continue;
                }
                // Copy over cheats
                var tasks = new List<Task>();
                foreach (var cheat in Directory.GetFiles($@"{mod}/cheats_ws", "*.pnach", SearchOption.AllDirectories))
                {
                    tasks.Add(Task.Run(() =>
                    {
                        File.Copy(cheat, Path.Combine($"{Path.GetFileNameWithoutExtension(cheat)}_aem.pnach"), true);
                        Utilities.ParallelLogger.Log($"[INFO] Copied over {Path.GetFileNameWithoutExtension(cheat)}_aem.pnach to {cheatsDir}");
                    }));
                }
                await Task.WhenAll(tasks);
            }
        }
        public static async Task LoadTextures(List<string> mods, string texturesDir)
        {
            foreach (string mod in mods)
            {
                Utilities.ParallelLogger.Log($"[INFO] Searching for textures in {mod}...");
                if (!Directory.Exists($@"{mod}/texture_override".ToLower()))
                {
                    Utilities.ParallelLogger.Log($"[INFO] No textures folder found in {mod}");
                    continue;
                }

                // Copy over textures
                var tasks = new List<Task>();
                foreach (var texture in Directory.GetFiles($@"{mod}/texture_override", "*", SearchOption.AllDirectories))
                {
                    tasks.Add(Task.Run(() =>
                    {
                        var relativePath = Path.GetRelativePath($@"{mod}/texture_override", texture);
                        string binPath = Path.Combine(texturesDir, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(binPath));
                        File.Copy(texture, binPath, true);
                        Utilities.ParallelLogger.Log($"[INFO] Copied over {Path.GetFileName(texture)} to {binPath}");
                    }));
                }
                await Task.WhenAll(tasks);
            }
        }

        public static async Task LoadFMVs(List<string> mods, string buildDir)
        {
            List<string> copiedFmvs = new List<string>();
            foreach (string mod in mods)
            {
                if(!Directory.Exists($@"{mod}/FMV"))
                    continue;
                if (!Directory.Exists(Path.Combine(buildDir, "FMV")))
                    Directory.CreateDirectory(Path.Combine(buildDir, "FMV"));

                // Copy over FMVS
                var copyTasks = new List<Task>();
                foreach (var fmv in Directory.GetFiles($@"{mod}/FMV", "*.pmsf"))
                {
                    copyTasks.Add(Task.Run(() =>
                    {
                        copiedFmvs.Add(Path.GetFileName(fmv));
                        var destinationFmv = Path.Combine(buildDir, "FMV", Path.GetFileName(fmv));
                        if (File.Exists(destinationFmv))
                        {
                            if (Utils.SameFiles(fmv, destinationFmv))
                            {
                                Utilities.ParallelLogger.Log($"[INFO] Skipping {fmv} as it is already at {destinationFmv}");
                                return;
                            }
                        }
                        try
                        {
                            File.Copy(fmv, destinationFmv, true);
                            Utilities.ParallelLogger.Log($"[INFO] Copying {fmv} over {destinationFmv}");
                        }
                        catch (Exception e)
                        {
                            Utilities.ParallelLogger.Log($"[ERROR] Unable to copy {fmv} to {destinationFmv}: {e.Message}");
                        }
                    }));
                }
                await Task.WhenAll(copyTasks);
            } 
            if (Directory.Exists(Path.Combine(buildDir, "FMV")))
            // Delete any FMVs in the P3P FMV folder that weren't from one of the mods
            {
                var deletionTasks = new List<Task>();
                foreach (var file in Directory.EnumerateFiles(Path.Combine(buildDir, "FMV")).Where(f => !copiedFmvs.Contains(Path.GetFileName(f)))) {
                    deletionTasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            File.Delete(file);
                            Utilities.ParallelLogger.Log($"[INFO] Deleting unwanted FMV {file}");
                        }
                        catch (Exception e)
                        {
                            Utilities.ParallelLogger.Log($"[ERROR] Unable to delete unwatned FMV {file}: {e.Message}");
                        }
                    }));
                }
                await Task.WhenAll(deletionTasks);
            }
        }

        public static async Task LoadP3PCheats(List<string> mods, string cheatFile)
        {
            foreach (string mod in mods)
            {
                Utilities.ParallelLogger.Log($"[INFO] Searching for cheats in {mod}...");
                if (!Directory.Exists($@"{mod}/cheats"))
                {
                    Utilities.ParallelLogger.Log($"[INFO] No cheats folder found in {mod}");
                    continue;
                }

                // Work out what cheats should be in the ini
                var existingCheats = PPSSPPCheatFile.ParseCheats(cheatFile);
                foreach (var newCheatFile in Directory.GetFiles($@"{mod}/cheats", "*.ini"))
                {
                    Utilities.ParallelLogger.Log($"[INFO] Applying cheats from {newCheatFile}");
                    var newCheats = PPSSPPCheatFile.ParseCheats(newCheatFile);
                    var tasks = new List<Task>();
                    foreach(var cheat in newCheats.Cheats)
                    {
                        tasks.Add(Task.Run(() =>
                        {
                            var existingCheat = existingCheats.Cheats.FirstOrDefault(c => c.Name == cheat.Name);
                            if (existingCheat != null)
                                existingCheat.Contents = cheat.Contents;
                            else
                                existingCheats.Cheats.Add(cheat);
                        }));
                    }
                    await Task.WhenAll(tasks);
                }

                // Write the ini with cheats in it
                using (StreamWriter writer = new StreamWriter(cheatFile))
                {
                    writer.WriteLine($"_S {existingCheats.GameID}");
                    writer.WriteLine($"_G {existingCheats.GameName}");
                    writer.WriteLine();
                    foreach(var cheat in existingCheats.Cheats)
                    {
                        writer.WriteLine($"_C{(cheat.Enabled ? '1' : '0')} {cheat.Name}");
                        foreach (var line in cheat.Contents)
                            writer.WriteLine(line);
                    }
                }
            }

        }

        internal static async Task LoadP1PSPCheats(List<string> mods, string cheatFile)
        {
            foreach (string mod in mods)
            {
                Utilities.ParallelLogger.Log($"[INFO] Searching for cheats in {mod}...");
                if (!Directory.Exists($@"{mod}/cheats"))
                {
                    Utilities.ParallelLogger.Log($"[INFO] No cheats folder found in {mod}");
                    continue;
                }

                // Work out what cheats should be in the ini
                var existingCheats = PPSSPPCheatFile.ParseCheats(cheatFile);
                foreach (var newCheatFile in Directory.GetFiles($@"{mod}/cheats", "*.ini"))
                {
                    Utilities.ParallelLogger.Log($"[INFO] Applying cheats from {newCheatFile}");
                    var newCheats = PPSSPPCheatFile.ParseCheats(newCheatFile);
                    var tasks = new List<Task>();
                    foreach (var cheat in newCheats.Cheats)
                    {
                        tasks.Add(Task.Run(() =>
                        {
                            var existingCheat = existingCheats.Cheats.FirstOrDefault(c => c.Name == cheat.Name);
                            if (existingCheat != null)
                                existingCheat.Contents = cheat.Contents;
                            else
                                existingCheats.Cheats.Add(cheat);
                        }));
                    }
                    await Task.WhenAll(tasks);
                }

                // Write the ini with cheats in it
                using (StreamWriter writer = new StreamWriter(cheatFile))
                {
                    writer.WriteLine($"_S {existingCheats.GameID}");
                    writer.WriteLine($"_G {existingCheats.GameName}");
                    writer.WriteLine();
                    foreach (var cheat in existingCheats.Cheats)
                    {
                        writer.WriteLine($"_C{(cheat.Enabled ? '1' : '0')} {cheat.Name}");
                        foreach (var line in cheat.Contents)
                            writer.WriteLine(line);
                    }
                }
            }
        }
    }
}
