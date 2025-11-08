using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AtlusScriptLibrary.Common.Libraries;
using AtlusScriptLibrary.Common.Text.Encodings;
using FlowFormatVersion = AtlusScriptLibrary.FlowScriptLanguage.FormatVersion;
using MessageFormatVersion = AtlusScriptLibrary.MessageScriptLanguage.FormatVersion;
using AtlusScriptLibrary.FlowScriptLanguage.Compiler;
using AtlusScriptLibrary.MessageScriptLanguage.Compiler;
using LibellusLibrary.Event;
using AtlusScriptLibrary.MessageScriptLanguage;
using AtlusScriptLibrary.MessageScriptLanguage.Decompiler;
using AtlusScriptLibrary.Common.Text;

namespace AemulusModManager.Utilities.FileMerging
{
    public class AtlusScriptMerger
    {
        internal class AtlusScriptFileInfo
        {
            public string baseFile { get; set; } = null;
            public string destFile { get; set; }

            public string baseFlow { get; set; } = null;
            // public string basePm1Json { get; set; } = null;
            public List<string> imports = new List<string>();
        }

        private Dictionary<string, AtlusScriptFileInfo> bfs;
        private Dictionary<string, AtlusScriptFileInfo> bmds;
        private Dictionary<string, AtlusScriptFileInfo> pm1s;

        private string game;
        private string vanillaFileDir;
        private string buildDir;
        private string lang;

        private Library library;
        private Encoding encoding;
        private FlowFormatVersion flowVersion;
        private MessageFormatVersion msgVersion;

        private static string[] scriptExtensions = { ".flow", ".msg", ".bf", ".bmd", ".pm1" };

        public AtlusScriptMerger(string _game, string _buildDir, string _language)
        {
            game = _game;
            vanillaFileDir = Path.Combine(Utils.originalDir, _game);
            buildDir = _buildDir;
            lang = _language;

            bfs = new Dictionary<string, AtlusScriptFileInfo>();
            bmds = new Dictionary<string, AtlusScriptFileInfo>();
            pm1s = new Dictionary<string, AtlusScriptFileInfo>();

            encoding = GetEncodingByGame(_game, _language);
            LibraryLookup.SetLibraryPath(Path.Combine(Utils.aemDir, "Libraries"));

            switch (game)
            {
                // would like to add non-efigs language support at some point but i dont know enough about the individual games to do it
                case "Persona 3 FES":
                    library = LibraryLookup.GetLibrary("P3F");
                    flowVersion = FlowFormatVersion.Version1;
                    msgVersion = MessageFormatVersion.Version1;
                    break;
                case "Persona 3 Portable":
                    library = LibraryLookup.GetLibrary("P3P");
                    flowVersion = FlowFormatVersion.Version1;
                    msgVersion = MessageFormatVersion.Version1;
                    break;
                case "Persona 4 Golden (PC 32-Bit)":
                case "Persona 4 Golden (Vita)":
                    library = LibraryLookup.GetLibrary("P4G");
                    flowVersion = FlowFormatVersion.Version1;
                    msgVersion = MessageFormatVersion.Version1;
                    break;
                case "Persona 5":
                    library = LibraryLookup.GetLibrary("P5");
                    flowVersion = FlowFormatVersion.Version3BigEndian;
                    msgVersion = MessageFormatVersion.Version1BigEndian;
                    break;
                case "Persona 5 Royal (PS4)":
                    library = LibraryLookup.GetLibrary("P5R");
                    flowVersion = FlowFormatVersion.Version3BigEndian;
                    msgVersion = MessageFormatVersion.Version1;
                    break;
                case "Persona 5 Royal (Switch)":
                    library = LibraryLookup.GetLibrary("P5R");
                    flowVersion = FlowFormatVersion.Version3BigEndian;
                    msgVersion = MessageFormatVersion.Version1BigEndian;
                    break;
                case "Persona Q": // script tools doesnt actually include a library for q1 but q2 is v similar internally so should be good enough for now
                case "Persona Q2":
                    library = LibraryLookup.GetLibrary("PQ2");
                    flowVersion = FlowFormatVersion.Version2;
                    msgVersion = MessageFormatVersion.Version1;
                    break;
                default:
                    library = null;
                    flowVersion = FlowFormatVersion.Version1;
                    msgVersion = MessageFormatVersion.Version1;
                    Utilities.ParallelLogger.Log("[WARNING] Unrecognized game. Script Tools arguments have been set to default.");
                    break;
            }
        }

        public static Encoding GetEncodingByGame(string game, string language)
        {
            AtlusEncoding.SetCharsetDirectory(Path.Combine(Utils.aemDir, "Charsets"));
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Encoding _encoding = null;
            switch (game)
            {
                // would like to add non-efigs language support at some point but i dont know enough about the individual games to do it
                case "Persona 3 FES":
                    _encoding = AtlusEncoding.Create("P3");
                    break;
                case "Persona 3 Portable":
                    _encoding = AtlusEncoding.Create("P3P_EFIGS");
                    break;
                case "Persona 4 Golden (PC 32-Bit)":
                case "Persona 4 Golden (Vita)":
                    _encoding = AtlusEncoding.Create("P4G_EFIGS");
                    break;
                case "Persona 5":
                    _encoding = AtlusEncoding.Create("P5");
                    break;
                case "Persona 5 Royal (PS4)":
                    _encoding = AtlusEncoding.Create("P5");
                    break;
                case "Persona 5 Royal (Switch)":
                    // TODO: just turn language into an enum by god
                    switch (language)
                    {
                        case "Japanese":
                            _encoding = AtlusEncoding.Persona5RoyalJapanese;
                            break;
                        case "Korean":
                            _encoding = AtlusEncoding.Create("P5_Korean");
                            break;
                        case "Chinese (Simplified)":
                            _encoding = AtlusEncoding.Create("P5R_CHS");
                            break;
                        case "Chinese (Traditional)":
                            _encoding = AtlusEncoding.Create("P5R_CHT");
                            break;
                        default:
                            _encoding = AtlusEncoding.Create("P5R_EFIGS");
                            break;
                    }
                    break;
                case "Persona Q": // script tools doesnt actually include a library for q1 but q2 is v similar internally so should be good enough for now
                case "Persona Q2":
                    _encoding = ShiftJISEncoding.Instance;
                    break;
                default:
                    _encoding = Encoding.UTF8;
                    Utilities.ParallelLogger.Log("[WARNING] Unrecognized encoding. Defaulting to UTF8.");
                    break;

            }

            return _encoding;
        }

        public async Task FindImports(List<string> modList)
        {
            foreach (var mod in modList)
            {
                string[] aemIgnore = File.Exists($"{mod}/Ignore.aem") ? File.ReadAllLines($"{mod}/Ignore.aem") : null;
                var imports = Directory.EnumerateFiles(mod, "*", SearchOption.AllDirectories).Where(x => !(aemIgnore != null && aemIgnore.Any(x.Contains)) && scriptExtensions.Contains(Path.GetExtension(x).ToLower()));
                var tasks = new List<Task>();

                foreach (var import in imports)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        var extension = Path.GetExtension(import).ToLower();

                        // sanitize file names to ensure we can find correct source file later
                        var fileName = Path.GetFileNameWithoutExtension(import);
                        while (Path.GetExtension(fileName) != String.Empty)
                        {
                            fileName = Path.GetFileNameWithoutExtension(fileName);
                        }

                        var relativePath = Path.Combine(Path.GetDirectoryName(Path.GetRelativePath(mod, import)), fileName).ToLower();
                        var dataFolder = String.Empty;
                        if (game == "Persona 4 Golden (PC 32-Bit)")
                        {
                            dataFolder = relativePath.Substring(0, relativePath.IndexOf(Path.DirectorySeparatorChar));
                            relativePath = relativePath.Substring(relativePath.IndexOf(Path.DirectorySeparatorChar) + 1);
                        }

                        var originalFile = Path.Combine(vanillaFileDir, dataFolder, relativePath);
                        var fileType = ".bmd";
                        ref var scriptFiles = ref bmds;

                        switch (extension)
                        {
                            case ".bf":
                            case ".flow":
                                scriptFiles = ref bfs;
                                fileType = ".bf";
                                break;
                            case ".pm1":
                                scriptFiles = ref pm1s;
                                fileType = ".pm1";
                                break;
                            case ".msg":
                                if (File.Exists(Path.ChangeExtension(import, ".flow")) || File.Exists(Path.ChangeExtension(import, ".bf")) || File.Exists(Path.ChangeExtension(originalFile, ".bf")))
                                {
                                    scriptFiles = ref bfs;
                                    fileType = ".bf";
                                }
                                else if (File.Exists(Path.ChangeExtension(import, ".pm1")) || File.Exists(Path.ChangeExtension(originalFile, ".pm1")))
                                {
                                    scriptFiles = ref pm1s;
                                    fileType = ".pm1";
                                }
                                break;
                            case ".bmd":
                                if (File.Exists(Path.ChangeExtension(import, ".pm1")) || File.Exists(Path.ChangeExtension(originalFile, ".pm1")))
                                {
                                    scriptFiles = ref pm1s;
                                    fileType = ".pm1";
                                }
                                break;
                            default:
                                break;
                        }

                        relativePath = Path.ChangeExtension(relativePath, fileType);
                        originalFile = Path.ChangeExtension(originalFile, fileType);
                        if (!scriptFiles.ContainsKey(relativePath))
                        {
                            scriptFiles.Add(relativePath, new AtlusScriptFileInfo());
                            scriptFiles[relativePath].baseFile = File.Exists(originalFile) ? originalFile : null;
                            // scriptFiles[relativePath].basePm1Json = File.Exists(originalFile) ? Path.ChangeExtension(originalFile, ".json") : null;
                            scriptFiles[relativePath].destFile = Path.Combine(buildDir, dataFolder, relativePath);
                        }

                        // var pmdJson = Path.ChangeExtension(import, ".json");
                        switch (extension)
                        {
                            case ".bf":
                                if (!File.Exists(Path.ChangeExtension(import, ".flow"))) { scriptFiles[relativePath].baseFile = import; }
                                break;
                            case ".flow":
                                if (scriptFiles[relativePath].baseFlow == null) { scriptFiles[relativePath].baseFlow = import; }
                                else { scriptFiles[relativePath].imports.Add(import); }
                                break;
                            case ".pm1":
                                if (scriptFiles[relativePath].baseFile == null)
                                {
                                    scriptFiles[relativePath].baseFile = import;
                                    // scriptFiles[relativePath].basePm1Json = pmdJson;
                                }
                                else if (!File.Exists(Path.ChangeExtension(import, ".msg")))
                                {
                                    var prunedImport = await TryPruneDuplicateMessages(scriptFiles[relativePath].baseFile, import, true);
                                    if (prunedImport == null) { return; }

                                    scriptFiles[relativePath].imports.Add(prunedImport);
                                }

                                // if (File.Exists(pmdJson)) { scriptFiles[relativePath].basePm1Json = pmdJson; }
                                break;
                            case ".bmd":
                                if (scriptFiles[relativePath].baseFile == null) { scriptFiles[relativePath].baseFile = import; }
                                else if (!File.Exists(Path.ChangeExtension(import, ".msg"))) // legacy support for precompiled bmds/pm1s, exports msg file matching new format if none exists then registers that instead of full bmd/pm1
                                {
                                    var prunedImport = await TryPruneDuplicateMessages(scriptFiles[relativePath].baseFile, import);
                                    if (prunedImport == null) { return; }

                                    scriptFiles[relativePath].imports.Add(prunedImport);
                                }
                                break;
                            default:
                                scriptFiles[relativePath].imports.Add(import);
                                break;
                        }
                    }));
                }
                await Task.WhenAll(tasks);
            }
        }

        public async Task MergeBfs()
        {
            if (bfs.Count == 0) { return; }

            var bfTasks = new List<Task>();
            foreach (var bf in bfs)
            {
                bfTasks.Add(Task.Run(() =>
                {
                    if (bf.Value.imports.Count == 0)
                    {
                        File.Copy(bf.Value.baseFile, bf.Value.destFile, true);
                        return;
                    }

                    var compiler = new FlowScriptCompiler(flowVersion);
                    compiler.Encoding = encoding;
                    compiler.Library = library;
                    compiler.OverwriteExistingMsgs = true;
                    compiler.ProcedureHookMode = ProcedureHookMode.ImportedOnly;

                    using (var baseBfStream = bf.Value.baseFile == null ? null : new FileStream(bf.Value.baseFile, FileMode.Open, FileAccess.Read))
                    {
                        try
                        {
                            if (!compiler.TryCompileWithImports(baseBfStream, bf.Value.imports, bf.Value.baseFlow, out var compiledBf, out var sources))
                            {
                                Utilities.ParallelLogger.Log($"[ERROR] Failed to compile {bf.Key}.");
                                return;
                            }
                            compiledBf.ToFile(bf.Value.destFile);
                        }
                        catch (Exception ex)
                        {
                            var importList = string.Join(", ", bf.Value.imports);
                            Utilities.ParallelLogger.Log($"[ERROR] Failed to compile {bf.Key} with files {importList}. Error: {ex.Message}");
                        }
                    }
                }));
            }
            await Task.WhenAll(bfTasks);
        }

        public async Task MergeBmds()
        {
            if (bmds.Count == 0) { return; }

            var bmdTasks = new List<Task>();
            foreach (var bmd in bmds)
            {
                bmdTasks.Add(Task.Run(() =>
                {
                    if (bmd.Value.imports.Count == 0)
                    {
                        File.Copy(bmd.Value.baseFile, bmd.Value.destFile, true);
                        return;
                    }
                    CompileBmd(bmd);
                }));
            }
            await Task.WhenAll(bmdTasks);
        }

        private void CompileBmd(KeyValuePair<string, AtlusScriptFileInfo> bmd)
        {
            var baseBmd = Path.ChangeExtension(bmd.Value.baseFile, ".bmd");
            var destBmd = Path.ChangeExtension(bmd.Value.destFile, ".bmd");

            var compiler = new MessageScriptCompiler(msgVersion, encoding);
            compiler.Library = library;
            compiler.OverwriteExistingMsgs = true;

            using (var baseBmdStream = baseBmd == null ? null : new FileStream(baseBmd, FileMode.Open, FileAccess.Read))
            {
                try
                {
                    if (!compiler.TryCompileWithImports(baseBmdStream, bmd.Value.imports, out var compiledBmd))
                    {
                        Utilities.ParallelLogger.Log($"[ERROR] Failed to compile {bmd.Key}");
                        return;
                    }

                    compiledBmd.ToFile(destBmd);
                }
                catch (Exception ex)
                {
                    var importList = string.Join(", ", bmd.Value.imports);
                    Utilities.ParallelLogger.Log($"[ERROR] Failed to compile {bmd.Key} with files {importList}. Error: {ex.Message}");
                }
            }
        }

        public async Task MergePm1s()
        {
            if (pm1s.Count == 0) { return; }

            var pm1Tasks = new List<Task>();
            foreach (var pm1 in pm1s)
            {
                pm1Tasks.Add(Task.Run(async () =>
                {
                    if (pm1.Value.imports.Count == 0)
                    {
                        File.Copy(pm1.Value.baseFile, pm1.Value.destFile, true);
                        return;
                    }

                    var destJson = Path.ChangeExtension(pm1.Value.destFile, ".json");

                    // for vanilla pm1s this should already be done when unpacking
                    if (!File.Exists(Path.ChangeExtension(pm1.Value.baseFile, ".bmd")) || !File.Exists(Path.ChangeExtension(pm1.Value.baseFile, ".json")))
                    {
                        await ExtractPm1Bmd(pm1.Value.baseFile);
                    }

                    File.Copy(Path.ChangeExtension(pm1.Value.baseFile, ".json"), destJson);
                    CompileBmd(pm1);

                    var pmd = await PolyMovieData.LoadPmd(destJson);
                    pmd.SavePmd(pm1.Value.destFile);

                    File.Delete(destJson);
                    File.Delete(Path.ChangeExtension(pm1.Value.destFile, ".bmd"));
                }));
            }
            await Task.WhenAll(pm1Tasks);
        }

        private async Task<string> TryPruneDuplicateMessages(string baseFile, string importFile, bool isPm1 = false)
        {
            var baseBmd = baseFile;
            var importBmd = importFile;

            if (isPm1)
            {
                baseBmd = Path.ChangeExtension(baseBmd, ".bmd");
                importBmd = Path.ChangeExtension(importBmd, ".bmd");

                if (!File.Exists(baseBmd)) { await ExtractPm1Bmd(baseFile); }
                if (!File.Exists(importBmd)) { await ExtractPm1Bmd(importFile); }
            }

            var baseScript = MessageScript.FromFile(baseBmd, msgVersion, encoding);
            var importScript = MessageScript.FromFile(importBmd, msgVersion, encoding);

            foreach(var dialog in baseScript.Dialogs)
            {
                if (importScript.Dialogs.Contains(dialog))
                {
                    importScript.Dialogs.Remove(dialog);
                }
            }
            if (importScript.Dialogs.Count == 0) { return null; }

            var msgFile = Path.ChangeExtension(baseBmd, ".msg");
            using (var decompiler = new MessageScriptDecompiler(new FileTextWriter(msgFile)))
            {
                decompiler.Library = library;

                try
                {
                    Utilities.ParallelLogger.Log($"[INFO] Dumping changes from {importFile}.");
                    decompiler.Decompile(importScript);
                }
                catch (Exception ex)
                {
                    Utilities.ParallelLogger.Log($"[ERROR] Failed to dump changes from {importFile}.");
                    return null;
                }
            }
            return msgFile;

        }

        public static async Task ExtractPm1Bmd(string pm1)
        {
            Utilities.ParallelLogger.Log($"[INFO] Extracting bmd from {pm1}");

            var pmdReader = new PmdReader();
            var pmd = await pmdReader.ReadPmd(pm1);
            await pmd.ExtractPmd(Path.GetDirectoryName(pm1), Path.GetFileName(pm1));
        }
    }
}
