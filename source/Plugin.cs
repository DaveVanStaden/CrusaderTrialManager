using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace CrusaderTrailManager {
    [BepInPlugin("crusadertrailmanager", "Crusader Trial Manager", "1.0.0")]
    public class Plugin : BaseUnityPlugin {
        internal static Plugin Instance;
        internal static string Root;
        [ThreadStatic] internal static Session Current;
        static Dictionary<int,Prepared> activeLords=new Dictionary<int,Prepared>();
        private Harmony harmony;
        private static bool quitting;
        internal const string GameHash = "BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789";
        const string NativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        static bool nativeCompatible;
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]
        static extern IntPtr GetModuleHandle(string moduleName);
        private void Awake() {
            Instance = this;
            Root = Path.Combine(Paths.GameRootPath, "TrailManager");
            Directory.CreateDirectory(Root);
            Note("BOOT: 1.0.0 pid=" + System.Diagnostics.Process.GetCurrentProcess().Id);
            try {
                string hash;
                using(var stream = File.OpenRead(typeof(AIVLoader).Assembly.Location))
                using(var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                if(hash != GameHash) throw new InvalidOperationException("Unsupported managed game build. The mod is disabled until compatibility is verified.");
                using(var stream=File.OpenRead(Path.Combine(Paths.GameRootPath,"Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll")))
                using(var sha=SHA256.Create()) nativeCompatible=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","")==NativeHash;
                Note("NATIVE BUILD: full lord replacement supported=" + nativeCompatible);
                harmony = new Harmony("crusadertrailmanager");
                UnityEngine.Object.DontDestroyOnLoad(gameObject);
                harmony.Patch(AccessTools.Method(typeof(EditorDirector), "LoadSkirmishMap", new Type[]{typeof(int),typeof(int),typeof(int)}),
                    prefix:new HarmonyMethod(typeof(Plugin), "Begin"), finalizer:new HarmonyMethod(typeof(Plugin), "End"));
                harmony.Patch(AccessTools.Method(typeof(EngineInterface), "ImportAIV"), prefix:new HarmonyMethod(typeof(Plugin), "Import"));
                harmony.Patch(AccessTools.Method(typeof(EngineInterface), "loadMap"), prefix:new HarmonyMethod(typeof(Plugin), "BeforeMap"));
                harmony.Patch(AccessTools.Method(typeof(AIVLoader), "UploadDefaultAIV"), prefix:new HarmonyMethod(typeof(Plugin), "UploadCustom"));
                harmony.Patch(AccessTools.Method(typeof(OnScreenText),"getComputerName"),prefix:new HarmonyMethod(typeof(Plugin),"CustomName"));
                harmony.Patch(AccessTools.Method(typeof(CrusaderDE.MainViewModel),"getAIFace"),prefix:new HarmonyMethod(typeof(Plugin),"CustomFace"));
                Note("READY: version 1.0.0. Profile is read whenever a new trail mission starts.");
            } catch(Exception ex) {
                if(harmony != null) harmony.UnpatchSelf();
                Note("DISABLED: " + ex.Message);
            }
        }
        internal static void Note(string message) {
            try { if(!object.ReferenceEquals(Instance,null)) Instance.Logger.LogInfo(message); } catch { }
            try { File.AppendAllText(Path.Combine(Root, "status.log"), DateTime.Now.ToString("s") + " " + message + Environment.NewLine); } catch { }
        }
        private static void Begin(int __0, int __1, ref Session __state) {
            Note("ENTER LoadSkirmishMap: trail=" + __0 + " missionIndex=" + __1);
            __state = Current;
            Current = null;
            activeLords=new Dictionary<int,Prepared>();
            try {
                var lords = EngineInterface.getTrailMissionLords(__0, __1);
                Note("TRAIL=" + __0 + " MISSION=" + (__1+1) + " ROSTER=" + string.Join(",", lords.Select((lord, slot) => slot + ":" + lord).ToArray()));
                var profile = Profile.Load(Path.Combine(Root, "profile.xml"));
                if(!profile.Enabled) { Note("Profile disabled; loading the original mission."); return; }
                var session = new Session();
                foreach(var rule in profile.Rules.Where(r => r.Trail == __0 && r.Mission == __1+1)) {
                    if(rule.Player >= lords.Length || lords[rule.Player] <= 0) {
                        Note("SKIP: player slot " + rule.Player + " is not an AI in this mission."); continue;
                    }
                    var prepared = new Prepared();
                    if(!string.IsNullOrWhiteSpace(rule.CustomLordFolder)) {
                        var pack=LordPack.Read(rule.CustomLordFolder);
                        string configName=string.IsNullOrWhiteSpace(rule.CustomConfig)?Path.GetFileName(pack.Configs[0]):rule.CustomConfig;
                        if(Path.GetFileName(configName)!=configName)throw new InvalidDataException("Invalid AI profile name.");
                        var saved=JsonUtility.FromJson<CustomisationFileManager.LordRoot>(ReadAsset(Path.Combine(pack.Folder,configName),".lordjson"));
                        if(saved==null || saved.lord==null)throw new InvalidDataException("Missing lord object.");
                        var data=EngineInterface.CreateAILordConfigData(saved.lord);
                        if(data.opponent_type<1 || data.opponent_type>29)throw new InvalidDataException("Custom lord has an unsupported parent lord.");
                        prepared.Ai=data;prepared.ParentLord=data.opponent_type;
                        prepared.ReplacementLord=28+rule.Player;
                        prepared.CustomName=pack.Name;
                        string avatar=Path.Combine(pack.Folder,"avatar.png");
                        if(File.Exists(avatar)){if(new FileInfo(avatar).Length>8*1024*1024)throw new InvalidDataException("Portrait is too large.");prepared.Portrait=CrusaderDE.MainViewModel.Instance.LoadImageFile(File.ReadAllBytes(avatar));}
                        var castleFiles=pack.Castles;
                        if(!string.IsNullOrEmpty(rule.CustomCastle)) {
                            string path=Path.Combine(pack.Folder,rule.CustomCastle);
                            if(!castleFiles.Contains(path,StringComparer.OrdinalIgnoreCase))throw new InvalidDataException("The selected custom castle is no longer available.");
                            castleFiles=new[]{path};
                        }
                        prepared.Castles=castleFiles.Select(f=>{var castle=JsonUtility.FromJson<AIVLoader.SaveData>(ReadAsset(f,".aivjson"));ValidateCastle(castle);return castle.GetRawData();}).ToArray();
                    } else if(!string.IsNullOrWhiteSpace(rule.CastleFile)) {
                        var saved = JsonUtility.FromJson<AIVLoader.SaveData>(ReadAsset(rule.CastleFile, ".aivjson"));
                        ValidateCastle(saved);
                        prepared.Castles = new short[][]{saved.GetRawData()};
                    } else if(rule.Donor >= 0 || rule.ReplacementLord >= 0 || rule.CastleVariant>=0 || !string.IsNullOrWhiteSpace(rule.AiFile)) {
                        int donor=rule.Donor >= 0 ? rule.Donor : rule.ReplacementLord>=0?rule.ReplacementLord:lords[rule.Player]-1;
                        prepared.Castles = (rule.CastleVariant>=0?new[]{rule.CastleVariant}:Enumerable.Range(0,8)).Select(i => AIVLoader.getAIVData(donor,i,false,false)).ToArray();
                        if(prepared.Castles.Any(a => a == null || a.Length == 0)) throw new InvalidDataException("Built-in castle data is missing.");
                    }
                    if(!string.IsNullOrWhiteSpace(rule.AiFile)) {
                        var saved = JsonUtility.FromJson<CustomisationFileManager.LordRoot>(ReadAsset(rule.AiFile, ".lordjson"));
                        if(saved == null || saved.lord == null) throw new InvalidDataException("AI file does not contain a lord object.");
                        prepared.Ai = EngineInterface.CreateAILordConfigData(saved.lord);
                        prepared.KeepIdentity=true;prepared.ParentLord=rule.ReplacementLord>=0?rule.ReplacementLord+1:lords[rule.Player];prepared.ReplacementLord=28+rule.Player;
                    }
                    if(string.IsNullOrWhiteSpace(rule.CustomLordFolder) && !prepared.KeepIdentity)prepared.ReplacementLord=rule.ReplacementLord;
                    session.Players.Add(rule.Player, prepared);
                }
                var replacements=session.Players.Where(p=>p.Value.ReplacementLord>=0).ToArray();
                if(replacements.Length>0) {
                    if(!nativeCompatible) throw new InvalidOperationException("Full lord replacement is not supported for this native game build.");
                    var module=GetModuleHandle("CrusaderDE.dll");
                    if(module==IntPtr.Zero) throw new InvalidOperationException("Native game module is not loaded.");
                    // These RVAs and 0xA0-byte record layout are gated by NativeHash above.
                    int recordRva=new int[]{0x2C8390,0x2CA2D0,0x2CB590}[__0]+__1*0xA0;
                    var changes=new List<ScopedIntPatch.Entry>();
                    foreach(var pair in replacements) {
                        changes.Add(new ScopedIntPatch.Entry{Address=IntPtr.Add(module,recordRva+0x10+pair.Key*4),Original=lords[pair.Key],Replacement=pair.Value.ReplacementLord+1});
                    }
                    session.NativeModule=module;
                    session.OriginalRoster=lords;
                    session.Trail=__0;session.MissionIndex=__1;
                    session.TablePatch=new ScopedIntPatch(changes);
                    try {
                        var patched=EngineInterface.getTrailMissionLords(__0,__1);
                        foreach(var pair in replacements) {
                            if(patched[pair.Key]!=pair.Value.ReplacementLord+1) throw new InvalidOperationException("Native roster read-back failed.");
                            Note("LORD REPLACEMENT: player="+pair.Key+" original="+lords[pair.Key]+" replacement="+patched[pair.Key]);
                        }
                    } catch { session.TablePatch.Dispose();throw; }
                }
                // Commit only after all selected files have been parsed successfully.
                Current = session;
                activeLords=session.Players.Where(p=>p.Value.CustomName!=null || p.Value.KeepIdentity).ToDictionary(p=>p.Key,p=>p.Value);
                Note("PREPARED: " + session.Players.Count + " opponent replacements.");
            } catch(Exception ex) { Note("PROFILE ERROR: " + ex.Message + " Loading the original mission."); }
        }
        private static void Import(int __0, int __1, ref short[] __2, ref int __3) {
            var current = Current;
            Prepared p;
            if(current == null || !current.Players.TryGetValue(__0+1,out p) || p.Castles == null) return;
            if(__1 < 0 || __1 >= 8) { Note("SKIP: unexpected castle variant " + __1); return; }
            __2 = p.Castles[__1 % p.Castles.Length];
            __3 = 1;
            current.Imports++;
        }
        private static void BeforeMap(int campaignMapID, string fileName, int trailType, int trailID) {
            if(Current == null) {activeLords=new Dictionary<int,Prepared>();return;}
            foreach(var pair in Current.Players) {
                if(!pair.Value.Ai.HasValue) continue;
                var data = pair.Value.Ai.Value;
                if(pair.Value.CustomName!=null || pair.Value.KeepIdentity) {
                    // The native API inherits speech and parent identity from the live lineup.
                    // Seed that field only for this call; the mission table supplies the custom slot during map initialization.
                    var address=IntPtr.Add(Current.NativeModule,0x8574C44+pair.Key*4);
                    using(var seed=new ScopedIntPatch(new[]{new ScopedIntPatch.Entry{Address=address,Original=Marshal.ReadInt32(address),Replacement=pair.Value.ParentLord}}))
                        EngineInterface.setCustomLordConfig(ref data,pair.Key);
                } else EngineInterface.setCustomLordConfig(ref data, pair.Key);
                Note("CUSTOM AI SUPPLIED: player=" + pair.Key+" lord="+pair.Value.CustomName);
            }
        }
        private static bool UploadCustom(int __0,int __1) {
            Prepared p;if(Current==null || !Current.Players.TryGetValue(__1,out p) || (p.CustomName==null && !p.KeepIdentity))return true;
            for(int i=0;i<8;i++)EngineInterface.ImportAIV(__1-1,i,p.Castles[i%p.Castles.Length],1);
            return false;
        }
        private static bool CustomName(ref int __0,ref string __result) {
            Prepared p;if(__0<30 || __0>37 || !activeLords.TryGetValue(__0-29,out p))return true;
            if(p.KeepIdentity){__0=p.ParentLord;return true;}__result=p.CustomName;return false;
        }
        private static bool CustomFace(ref int __0,ref Noesis.ImageSource __result) {
            Prepared p;if(__0<30 || __0>37 || !activeLords.TryGetValue(__0-29,out p) )return true;
            if(p.KeepIdentity){__0=p.ParentLord;return true;}if(p.Portrait==null)return true;
            __result=p.Portrait;return false;
        }
        private static Exception End(Exception __exception, Session __state) {
            var session=Current;
            try {
                if(session != null) {
                    Note("LOAD RETURNED: imports=" + session.Imports + "; exception=" + (__exception == null ? "none" : __exception.Message));
                    if(session.TablePatch!=null) {
                        if(__exception==null) foreach(var pair in session.Players.Where(p=>p.Value.ReplacementLord>=0)) {
                            int live=Marshal.ReadInt32(IntPtr.Add(session.NativeModule,0x8574C44+pair.Key*4));
                            Note("LIVE LORD: player="+pair.Key+" expected="+(pair.Value.ReplacementLord+1)+" actual="+live+" match="+(live==pair.Value.ReplacementLord+1));
                        }
                    }
                }
            } catch(Exception ex) { Note("VERIFICATION ERROR: "+ex.Message); }
            finally {
                try {
                    if(session!=null && session.TablePatch!=null) {
                        session.TablePatch.Dispose();
                        var restored=EngineInterface.getTrailMissionLords(session.Trail,session.MissionIndex);
                        Note("MISSION TABLE RESTORED: "+restored.SequenceEqual(session.OriginalRoster));
                    }
                } catch(Exception ex) {Note("RESTORE ERROR: "+ex.Message);if(__exception==null)__exception=ex;}
                Current=__state;
            }
            return __exception;
        }
        private static string ReadAsset(string path, string extension) {
            if(!Path.IsPathRooted(path)) path = Path.Combine(Root,path);
            if(!string.Equals(Path.GetExtension(path),extension,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Expected file extension " + extension);
            var info = new FileInfo(path);
            if(!info.Exists || info.Length > 8*1024*1024) throw new InvalidDataException("File is missing or exceeds the size limit: " + path);
            return File.ReadAllText(path);
        }
        private static void ValidateCastle(AIVLoader.SaveData a) {
            if(a == null || a.frames == null || a.frames.Count < 1 || a.frames.Count > 32767 || a.miscItems == null || a.miscItems.Count > 32767)
                throw new InvalidDataException("Invalid castle structure.");
            if(a.pauseDelayAmount < 0 || a.pauseDelayAmount > 32767) throw new InvalidDataException("Invalid pause duration.");
            foreach(var f in a.frames) {
                if(f == null || f.itemType < 1 || f.itemType > 32767 || f.tilePositionOfsets == null || f.tilePositionOfsets.Count < 1 || f.tilePositionOfsets.Count > 32767
                    || f.tilePositionOfsets.Any(t => t < 0 || t > 32767)) throw new InvalidDataException("Invalid construction frame.");
            }
            foreach(var m in a.miscItems) {
                if(m.positionOfset < 0 || m.positionOfset > 32767 || m.itemType < 0 || m.itemType > 32767 || m.number < 0 || m.number > 32767)
                    throw new InvalidDataException("Invalid castle object.");
            }
        }
        private void OnApplicationQuit() { quitting=true; Note("LIFECYCLE Quit"); if(harmony != null) harmony.UnpatchSelf(); Current=null; }
        private void OnDestroy() {
            Note("LIFECYCLE Destroy; quitting=" + quitting + ". Runtime hooks remain until application quit.");
            // Scene teardown must not silently remove process-wide hooks.
        }
    }
    public class Session {
        internal Dictionary<int, Prepared> Players = new Dictionary<int, Prepared>(); internal int Imports;
        internal ScopedIntPatch TablePatch; internal IntPtr NativeModule; internal int[] OriginalRoster; internal int Trail, MissionIndex;
    }
    internal class Prepared { internal short[][] Castles; internal EngineInterface.AILordConfigTransferData? Ai; internal int ReplacementLord=-1,ParentLord;internal bool KeepIdentity;internal string CustomName;internal Noesis.TextureSource Portrait; }
}



