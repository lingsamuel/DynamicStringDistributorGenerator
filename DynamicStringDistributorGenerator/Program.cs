using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Strings;
using Mutagen.Bethesda.Synthesis;
using Newtonsoft.Json;
using Noggog;

namespace DynamicStringDistributorGenerator {
    public class Config {
        public string OutputPath = "D:/DSD/";
    }

    public class INFO_NAM1 {
        public string? form_id;
        public string? type;
        public int index;
        public string? original;
        public string? @string;
    }

    public class Program {
        protected static Lazy<Config> Config = null!;

        public static async Task<int> Main(string[] args) {
            Console.WriteLine(args);
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch, new PatcherPreferences() {
                })
                .SetAutogeneratedSettings("settings", "settings.json", out Config)
                .SetTypicalOpen(GameRelease.SkyrimSE, "DialogTags.esp")
                .Run(args);
        }

        public static Dictionary<string, List<INFO_NAM1>> Output = new Dictionary<string, List<INFO_NAM1>>();
        public static Dictionary<string, uint> LoadOrder = new Dictionary<string, uint>();

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state) {
            var counter = 0;

            // uint lo = 0;
            // uint feLo = 0;
            // foreach (var listing in state.LoadOrder.ListedOrder) {
            //     var mod = listing.Mod;
            //     if (mod == null) {
            //         throw new Exception("Mod is null");
            //     }
            //
            //     if (mod.ModHeader.Flags.HasFlag(SkyrimModHeader.HeaderFlag.Light)) {
            //         LoadOrder[mod.ModKey.ToString()] = (0xFE000000) | (feLo << 12);
            //         Console.WriteLine($"{mod.ModKey.ToString()} at {LoadOrder[mod.ModKey.ToString()]:X8}");
            //         feLo++;
            //     } else if (mod.ModHeader.Flags.HasFlag(SkyrimModHeader.HeaderFlag.Master)) {
            //         LoadOrder[mod.ModKey.ToString()] = lo << 24;
            //         Console.WriteLine($"{mod.ModKey.ToString()} at {LoadOrder[mod.ModKey.ToString()]:X8}");
            //         lo += 1;
            //     }
            // }
            //
            // foreach (var listing in state.LoadOrder.ListedOrder) {
            //     var mod = listing.Mod;
            //     if (mod == null) {
            //         throw new Exception("Mod is null");
            //     }
            //
            //     if (mod.ModHeader.Flags.HasFlag(SkyrimModHeader.HeaderFlag.Master)) {
            //         continue;
            //     } else if (mod.ModHeader.Flags.HasFlag(SkyrimModHeader.HeaderFlag.Light)) {
            //         continue;
            //     }
            //
            //     LoadOrder[mod.ModKey.ToString()] = lo << 24;
            //     Console.WriteLine($"{mod.ModKey.ToString()} at {LoadOrder[mod.ModKey.ToString()]:X8}");
            //     lo += 1;
            // }

            counter = 0;
            var dialogResponses = state.LoadOrder.ListedOrder.DialogResponses().WinningContextOverrides(state.LinkCache).ToList();
            foreach (var dialogResponse in dialogResponses) {
                if (counter % 1000 == 0) {
                    Console.WriteLine($"Summary: Processed {counter}/{dialogResponses.Count} records");
                }

                counter++;

                var formKey = dialogResponse.Record.FormKey;
                var modKey = formKey.ModKey;
                var name = modKey.ToString();
                if (!LoadOrder.ContainsKey(name)) {
                    throw new Exception("LoadOrder is null");
                }

                if (!Output.ContainsKey(name)) {
                    Output[name] = new List<INFO_NAM1>();
                }

                for (int i = 0; i < dialogResponse.Record.Responses.Count; i++) {
                    dialogResponse.Record.Responses[i].Text.TryLookup(Language.English, out var engStr);
                    dialogResponse.Record.Responses[i].Text.TryLookup(Language.Chinese, out var chnStr);
                    if (engStr.IsNullOrWhitespace() || chnStr.IsNullOrWhitespace()) {
                        var str = dialogResponse.Record.Responses[i].Text.String;
                        if (str.IsNullOrWhitespace()) {
                            continue;
                        }

                        Output[name].Add(new INFO_NAM1() {
                            type = "INFO NAM1",
                            index = i + 1,
                            @string = "[Gen] " + str,
                            // original = str,
                            form_id = $"{(formKey.ID):X6}|{modKey}",
                        });
                    } else {
                        Output[name].Add(new INFO_NAM1() {
                            type = "INFO NAM1",
                            index = i + 1,
                            @string = "[Gen] " + chnStr,
                            original = engStr,
                            form_id = $"{(formKey.ID):X6}|{modKey}",
                        });
                    }
                }
            }

            foreach (var (key, value) in Output) {
                if (value.Count == 0) {
                    continue;
                }

                var dir = Path.Join(Config.Value.OutputPath, key);
                Directory.CreateDirectory(dir);
                var json = JsonConvert.SerializeObject(value, Formatting.Indented,
                    new JsonSerializerSettings {
                        TypeNameHandling = TypeNameHandling.Auto,
                        Formatting = Formatting.Indented,
                        NullValueHandling = NullValueHandling.Ignore,
                    });
                var file = Path.Join(dir, key.ToString() + ".Dialogue.json");
                File.WriteAllText(file, json);
                Console.WriteLine($"Write file {file}");
            }
        }
    }
}