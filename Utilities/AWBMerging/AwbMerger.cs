using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Globalization;
using AemulusModManager.Utilities;

namespace AemulusModManager.Utilities.AwbMerging
{
    internal class AwbMerger
    {
        private static void RunAcbEditor(string args)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}/Dependencies/SonicAudioTools/AcbEditor.exe";
            if (!File.Exists(startInfo.FileName))
            {
                Utilities.ParallelLogger.Log($"[ERROR] Couldn't find {startInfo.FileName}. Please check if it was blocked by your anti-virus.");
                return;
            }
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.UseShellExecute = false;
            startInfo.Arguments = args;
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
            }
        }
        private static async Task RunAwbUnpacker(string args, string extension)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}/Dependencies/AwbTools/AWB_unpacker.exe";
            if (!File.Exists(startInfo.FileName))
            {
                Utilities.ParallelLogger.Log($"[ERROR] Couldn't find {startInfo.FileName}. Please check if it was blocked by your anti-virus.");
                return;
            }
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.UseShellExecute = false;
            startInfo.Arguments = args;
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
            }

            string awbPath = Path.ChangeExtension(args, null);
            Directory.CreateDirectory(awbPath);

            List<string> files = new List<string>(Directory.EnumerateFiles($@"{args}_extracted_files"));
            var tasks = new List<Task>();
            foreach (var file in files)
                tasks.Add(Task.Run(() => { File.Move(file, $@"{awbPath}/{Convert.ToString(int.Parse(Path.GetFileNameWithoutExtension(file), NumberStyles.HexNumber)).PadLeft(5, '0')}_streaming{extension}"); }));
            await Task.WhenAll(tasks);
            Directory.Delete($@"{args}_extracted_files", true);
        }
        private static void RunAwbRepacker(string args)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}/Dependencies/AwbTools/AWB_repacker.exe";
            if (!File.Exists(startInfo.FileName))
            {
                Utilities.ParallelLogger.Log($"[ERROR] Couldn't find {startInfo.FileName}. Please check if it was blocked by your anti-virus.");
                return;
            }
            startInfo.CreateNoWindow = true;
            startInfo.WorkingDirectory = args;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.UseShellExecute = false;
            startInfo.Arguments = $@"{args}/*";

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
            }

            File.Move($@"{startInfo.WorkingDirectory}/OUT.AWB", Path.ChangeExtension(args, ".awb"), true);
        }
        public static bool AcbExists(string path)
        {
            return File.Exists(Path.ChangeExtension(path, ".acb"));
        }
        public static bool AwbExists(string path)
        {
            return File.Exists(Path.ChangeExtension(path, ".awb"));
        }
        public static bool SoundArchiveExists(string path)
        {
            return AcbExists(path) || AwbExists(path);
        }
        private static async Task CopyAndUnpackArchive(string acbPath, string ogAcbPath, string extension)
        {
            ogAcbPath = Path.ChangeExtension(ogAcbPath, ".acb");
            acbPath = Path.ChangeExtension(acbPath, ".acb");
            string ogAwbPath = Path.ChangeExtension(ogAcbPath, ".awb");
            string awbPath = Path.ChangeExtension(acbPath, ".awb");
            Directory.CreateDirectory(Path.GetDirectoryName(acbPath));

            if (AwbExists(ogAwbPath))
            {
                Utilities.ParallelLogger.Log($"[INFO] Copying over {ogAwbPath} to use as base.");
                File.Copy(ogAwbPath, awbPath, true);
            }
            else if (AwbExists(ogAwbPath = $@"{Path.GetDirectoryName(ogAwbPath)}/{Path.GetFileNameWithoutExtension(ogAwbPath)}_streamfiles.awb"))
            {
                awbPath = $@"{Path.GetDirectoryName(acbPath)}/{Path.GetFileName(ogAwbPath)}";
                Utilities.ParallelLogger.Log($"[INFO] Copying over {ogAwbPath} to use as base.");
                File.Copy(ogAwbPath, awbPath, true);
            }

            if (AcbExists(ogAcbPath))
            {
                Utilities.ParallelLogger.Log($"[INFO] Copying over {ogAcbPath} to use as base.");
                File.Copy(ogAcbPath, acbPath, true);
                Utilities.ParallelLogger.Log($"[INFO] Unpacking {acbPath}");
                RunAcbEditor(acbPath);
            }
            else
            {
                Utilities.ParallelLogger.Log($"[INFO] Unpacking {awbPath}");
                await RunAwbUnpacker(awbPath, extension);
            }
        }
        public static async Task Merge(List<string> ModList, string game, string modDir)
        {
            List<string> acbs = new List<string>();
            foreach(string mod in ModList)
            {
                List<string> directories = new List<string>(Directory.EnumerateDirectories(mod, "*", SearchOption.AllDirectories));
                string[] AemIgnore = File.Exists($@"{mod}/Ignore.aem") ? File.ReadAllLines($@"{mod}/Ignore.aem") : null;
                var checkDirectories = new List<Task>();

                foreach (string dir in directories)
                {
                    checkDirectories.Add(Task.Run(async () =>
                    {
                        var relativePath = Path.GetRelativePath(mod, dir);
                        string acbPath = Path.Join(modDir, relativePath);
                        string ogAcbPath = Path.Join($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}/Original/{game}", relativePath);

                        if (SoundArchiveExists(ogAcbPath))
                        {
                            List<string> files = new List<string>(Directory.GetFiles(dir));
                            var copyFiles = new List<Task>();

                            foreach (string file in files)
                            {
                                if (AemIgnore != null && AemIgnore.Any(file.Contains))
                                    continue;
                                if (!Directory.Exists(acbPath))
                                {
                                    await CopyAndUnpackArchive(acbPath, ogAcbPath, Path.GetExtension(file));
                                    acbs.Add(acbPath);
                                }
                                copyFiles.Add(Task.Run(() =>
                                {
                                    string fileName = Path.GetFileNameWithoutExtension(file).IndexOf('_') == -1 ? $@"{Path.GetFileNameWithoutExtension(file).PadLeft(5, '0')}{Path.GetExtension(file)}" : $@"{Path.GetFileName(file).Substring(0, Path.GetFileName(file).IndexOf('_')).PadLeft(5, '0')}_streaming{Path.GetExtension(file)}";
                                    File.Copy(file, $@"{acbPath}/{fileName}", true);
                                    Utilities.ParallelLogger.Log($"[INFO] Copying over {file} to {acbPath}");
                                }));
                            }
                            await Task.WhenAll(copyFiles);
                        }
                    }));
                }
                await Task.WhenAll(checkDirectories);
            }
            var mergingTasks = new List<Task>();
            foreach(string acb in acbs)
            {
                mergingTasks.Add(Task.Run(() =>
                {
                    Utilities.ParallelLogger.Log($"[INFO] Repacking {acb}");
                    if (AcbExists(acb))
                        RunAcbEditor(acb);
                    else
                        RunAwbRepacker(acb);
                    Directory.Delete(acb, true);
                }));
            }
            await Task.WhenAll(mergingTasks);
        }
    }
}
