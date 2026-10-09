using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
namespace CrusaderTrailManager {
 public static class Catalog {
  public const string NativeHash="FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
  public static readonly string[] Lords={"Rat","Snake","Pig","Wolf","Saladin","Caliph","Sultan","Richard","Frederick","Phillip","Wazir","Emir","Nizar","Sheriff","Marshal","Abbot","Jewel","Sentinel","Nomad","Kahin","Canary","Trader / Merchant","Sergeant","Lioness","Crocodile","Baldwin","Bullseye","Surgeon","Baibars"};
  public static string Name(int id){return id>0 && id<=Lords.Length?Lords[id-1]:"Lord "+id;}
  public static int BuiltInFolder(string name){string n=name.Replace(" ","").Replace("_","").Replace("-","").ToLowerInvariant();if(n.StartsWith("the"))n=n.Substring(3);if(n=="merchant")return 21;if(n=="philip")return 9;if(n=="kahinah")return 19;for(int i=0;i<Lords.Length;i++)if(Lords[i].Split('/')[0].Trim().Replace(" ","").ToLowerInvariant()==n)return i;return -1;}
  public static int[][][] ReadMissions(string game){
   string path=Path.Combine(game,"Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll");
   byte[] b=File.ReadAllBytes(path);
   using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(b)).Replace("-","")!=NativeHash)throw new InvalidDataException("This game build needs a compatibility update before its missions can be edited.");
   int pe=BitConverter.ToInt32(b,60),count=BitConverter.ToUInt16(b,pe+6),sections=pe+24+BitConverter.ToUInt16(b,pe+20);
   Func<int,int> offset=rva=>{for(int i=0;i<count;i++){int s=sections+i*40,va=BitConverter.ToInt32(b,s+12),len=BitConverter.ToInt32(b,s+16);if(rva>=va && rva<va+len)return rva-va+BitConverter.ToInt32(b,s+20);}throw new InvalidDataException("Mission data was not found.");};
   var all=new int[3][][];int[] bases={0x2C8390,0x2CA2D0,0x2CB590},sizes={50,30,20};
   for(int t=0;t<3;t++){all[t]=new int[sizes[t]][];for(int m=0;m<sizes[t];m++){int p=offset(bases[t]+m*160);var roster=new int[9];for(int slot=1;slot<=8;slot++){roster[slot]=BitConverter.ToInt32(b,p+16+slot*4);if(roster[slot]<0 || roster[slot]>29)throw new InvalidDataException("Unexpected mission roster.");}all[t][m]=roster;}}
   return all;
  }
 }
 public class LordAsset {
  public int Lord;public string Path;public bool AI;
  public override string ToString(){return System.IO.Path.GetFileNameWithoutExtension(Path)+" (custom)";}
  public static List<LordAsset> Discover(string game,string owned){
   var roots=new List<string>{owned,System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AppData/LocalLow/Firefly Studios/Stronghold Crusader Definitive Edition/ExtendedLords")};var common=Directory.GetParent(game);if(common!=null && common.Parent!=null)roots.Add(System.IO.Path.Combine(common.Parent.FullName,"workshop/content/3024040"));
   var assets=new List<LordAsset>();var hashes=new HashSet<string>();
   foreach(string root in roots)if(Directory.Exists(root))Walk(root,-1,0,assets,hashes);
   return assets.OrderBy(a=>a.Path,StringComparer.OrdinalIgnoreCase).ToList();
  }
  static void Walk(string folder,int parent,int depth,List<LordAsset> assets,HashSet<string> hashes){
   if(depth>6 || (File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)return;
   int lord=Catalog.BuiltInFolder(System.IO.Path.GetFileName(folder));if(lord<0)lord=parent;
   try{if(lord>=0)foreach(string file in Directory.GetFiles(folder)){string ext=System.IO.Path.GetExtension(file).ToLowerInvariant();if(ext!=".lordjson" && ext!=".aivjson")continue;var info=new FileInfo(file);if(info.Length==0 || info.Length>8*1024*1024)continue;string hash;using(var sha=SHA256.Create())using(var stream=File.OpenRead(file))hash=lord+ext+BitConverter.ToString(sha.ComputeHash(stream));if(hashes.Add(hash))assets.Add(new LordAsset{Lord=lord,Path=System.IO.Path.GetFullPath(file),AI=ext==".lordjson"});}foreach(string sub in Directory.GetDirectories(folder))Walk(sub,lord,depth+1,assets,hashes);}catch(IOException){}catch(UnauthorizedAccessException){}
  }
  public static string Import(string file,int lord,string owned){
   if(lord<0 || lord>=Catalog.Lords.Length)throw new InvalidDataException("Select a built-in lord first.");string ext=System.IO.Path.GetExtension(file).ToLowerInvariant();if(ext!=".lordjson" && ext!=".aivjson")throw new InvalidDataException("Choose a .lordjson AI profile or .aivjson castle.");var info=new FileInfo(file);if(!info.Exists || info.Length==0 || info.Length>8*1024*1024)throw new InvalidDataException("Invalid or oversized asset.");
   string folder=System.IO.Path.Combine(System.IO.Path.GetFullPath(owned),Catalog.Lords[lord].Split('/')[0].Trim());Directory.CreateDirectory(folder);string target=System.IO.Path.Combine(folder,System.IO.Path.GetFileNameWithoutExtension(file)+"-"+Guid.NewGuid().ToString("N").Substring(0,8)+ext);File.Copy(file,target);return target;
  }
 }
 public class LordPack {
  public string Folder;public string Name;public string[] Configs;public string[] Castles;
  public override string ToString(){return Name;}
  public static LordPack Read(string folder){
   folder=Path.GetFullPath(folder);var configs=Directory.GetFiles(folder,"*.lordjson").OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToArray();var castles=Directory.GetFiles(folder,"*.aivjson").OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToArray();
   if(configs.Length==0 || castles.Length==0)throw new InvalidDataException("Choose a lord folder containing .lordjson AI profiles and .aivjson castles.");
   if(configs.Length>100 || castles.Length>100)throw new InvalidDataException("The lord pack contains too many files.");
   foreach(string p in configs.Concat(castles)){var f=new FileInfo(p);if(f.Length==0 || f.Length>8*1024*1024)throw new InvalidDataException("Invalid or oversized lord file: "+f.Name);}
   string name=Path.GetFileName(folder),label=Path.Combine(folder,"trail-lord-name.txt");if(File.Exists(label) && new FileInfo(label).Length<=1024){string text=File.ReadAllText(label).Trim();if(text.Length>0 && text.Length<=100)name=text;}
   return new LordPack{Folder=folder,Name=name,Configs=configs,Castles=castles};
  }
  public static List<LordPack> Discover(string game,string owned){return Discover(game,owned,new string[0]);}
  public static List<LordPack> Discover(string game,string owned,IEnumerable<string> preferred){
   var roots=preferred.Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();roots.Add(owned);
   var common=Directory.GetParent(game);if(common!=null && common.Parent!=null)roots.Add(Path.Combine(common.Parent.FullName,"workshop/content/3024040"));
   roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AppData/LocalLow/Firefly Studios/Stronghold Crusader Definitive Edition/CustomLords"));
   var packs=new List<LordPack>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(string root in roots)if(Directory.Exists(root))foreach(string folder in Folders(root,3)){if(!seen.Add(folder))continue;try{packs.Add(Read(folder));}catch(InvalidDataException){}catch(IOException){}catch(UnauthorizedAccessException){}}
   var fingerprints=new HashSet<string>();return packs.Where(p=>fingerprints.Add(Fingerprint(p))).OrderBy(p=>p.Name).ToList();
  }
  static IEnumerable<string> Folders(string root,int depth){yield return root;if(depth==0)yield break;string[] dirs;try{dirs=Directory.GetDirectories(root);}catch{yield break;}foreach(string d in dirs){if((File.GetAttributes(d)&FileAttributes.ReparsePoint)!=0)continue;foreach(string nested in Folders(d,depth-1))yield return nested;}}
  static string Fingerprint(LordPack p){using(var sha=SHA256.Create()){var files=p.Configs.Concat(p.Castles).ToList();string avatar=Path.Combine(p.Folder,"avatar.png");if(File.Exists(avatar))files.Add(avatar);var hashes=new List<string>();foreach(string f in files)using(var stream=File.OpenRead(f))hashes.Add(BitConverter.ToString(sha.ComputeHash(stream)));return p.Name+":"+string.Join(":",hashes.OrderBy(h=>h).ToArray());}}
  public static LordPack Import(string source,string library){
   var pack=Read(source);library=Path.GetFullPath(library).TrimEnd(Path.DirectorySeparatorChar);Directory.CreateDirectory(library);string target=Path.GetFullPath(Path.Combine(library,"lord-"+Guid.NewGuid().ToString("N")));
   if(!target.StartsWith(library+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Import target is outside the lord library.");Directory.CreateDirectory(target);
   try{foreach(string f in pack.Configs.Concat(pack.Castles))File.Copy(f,Path.Combine(target,Path.GetFileName(f)));File.WriteAllText(Path.Combine(target,"trail-lord-name.txt"),pack.Name);string avatar=Path.Combine(source,"avatar.png");if(File.Exists(avatar)){if(new FileInfo(avatar).Length>8*1024*1024)throw new InvalidDataException("Portrait exceeds the size limit.");File.Copy(avatar,Path.Combine(target,"avatar.png"));}return Read(target);}catch{Directory.Delete(target,true);throw;}
  }
 }
}
