using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;
namespace CrusaderTrailManager {
 public class Manager:Form {
  const string Exe="Stronghold Crusader Definitive Edition.exe";
  string root;Profile profile=new Profile();bool dirty,loading;int[][][] missions;
  List<LordPack> packs=new List<LordPack>();List<LordAsset> assets=new List<LordAsset>();
  ComboBox trail=new StyledComboBox(),replacement=new StyledComboBox(),config=new StyledComboBox(),castle=new StyledComboBox();
  ListBox missionList=new ListBox();ListView opponents=new ListView(),library=new ListView();
  CheckBox enabled=new CheckBox();Label status=new Label(),selected=new Label(),packInfo=new Label();TextBox gamePath=new TextBox();
  PictureBox portrait=new PictureBox();
  static string SettingsPath{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CrusaderTrialManager/game-folder.txt");}}
  class Choice {public int BuiltIn=-1;public LordPack Pack;public override string ToString(){return Pack!=null?"Custom · "+Pack.Name:BuiltIn<0?"Keep original lord":Catalog.Lords[BuiltIn];}}
  [STAThread]public static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);try{Application.Run(new Manager());}catch(Exception e){MessageBox.Show(e.Message,"Crusader Trial Manager");}}
  public Manager(){
   Text="Crusader Trial Manager";ClientSize=new Size(1020,845);MinimumSize=new Size(1036,884);Font=new Font("Segoe UI",10);BackColor=Theme.Background;DoubleBuffered=true;Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
   LabelAt(this,"Crusader Trial Manager",24,25,760,42).Font=new Font("Georgia",25,FontStyle.Bold);
   LabelAt(this,"Choose your rivals. Shape every battle.",26,67,950,25);
   gamePath.ReadOnly=true;gamePath.SetBounds(24,106,795,28);Controls.Add(gamePath);ButtonAt(this,"Game folder…",833,103,163,ChooseFolder);
   enabled.Text="Enable replacements";enabled.SetBounds(24,148,280,30);enabled.CheckedChanged+=delegate{if(!loading)dirty=true;};Controls.Add(enabled);
   var edit=new Panel();edit.SetBounds(24,235,972,517);Controls.Add(edit);var lib=new Panel();lib.SetBounds(24,235,972,517);lib.Visible=false;Controls.Add(lib);
   var missionsNav=new StyledButton{Text="Mission opponents",Accent=true,Location=new Point(24,187),Size=new Size(210,36)};var libraryNav=new StyledButton{Text="Custom lord library",Location=new Point(248,187),Size=new Size(230,36)};Controls.Add(missionsNav);Controls.Add(libraryNav);
   ButtonAt(this,"Import AI / castle…",500,187,225,ImportAsset);
   missionsNav.Click+=delegate{edit.Visible=true;lib.Visible=false;missionsNav.Accent=true;libraryNav.Accent=false;missionsNav.Invalidate();libraryNav.Invalidate();};libraryNav.Click+=delegate{edit.Visible=false;lib.Visible=true;missionsNav.Accent=false;libraryNav.Accent=true;missionsNav.Invalidate();libraryNav.Invalidate();};
   trail.Items.AddRange(new object[]{"Crusader · 1–50","Warchest · 51–80","Extreme · 1–20"});DropDown(edit,trail,16,18,210);
   missionList.SetBounds(16,62,210,435);missionList.IntegralHeight=false;edit.Controls.Add(missionList);
   LabelAt(edit,"Original opponent",248,20,280,25);LabelAt(edit,"Replacement",545,20,260,25);
   opponents.SetBounds(248,62,692,155);opponents.View=View.Details;opponents.FullRowSelect=true;opponents.MultiSelect=false;opponents.HideSelection=false;opponents.Columns.Add("Opponent",245);opponents.Columns.Add("Replace with",210);opponents.Columns.Add("Castle",235);edit.Controls.Add(opponents);
   selected.Text="Select an opponent above";selected.SetBounds(248,236,540,28);selected.Font=new Font(Font,FontStyle.Bold);edit.Controls.Add(selected);
   DropDown(edit,replacement,248,274,480);replacement.SelectedIndexChanged+=delegate{UpdateConfig();};
   LabelAt(edit,"AI profile",248,319,150,24);DropDown(edit,config,248,345,480);
   LabelAt(edit,"Castle",248,398,150,24);DropDown(edit,castle,248,424,480);
   portrait.SetBounds(765,245,154,154);portrait.SizeMode=PictureBoxSizeMode.Zoom;edit.Controls.Add(portrait);
   ButtonAt(edit,"Apply replacement",248,470,220,Apply);ButtonAt(edit,"Reset this opponent",485,470,243,ResetOpponent);
   LabelAt(lib,"Installed Workshop lords appear here automatically. You can also import a lord folder.",16,18,920,30);
   library.SetBounds(16,65,650,245);library.View=View.Details;library.FullRowSelect=true;library.MultiSelect=false;library.Columns.Add("Custom lord",265);library.Columns.Add("AI profiles",100);library.Columns.Add("Castles",285);lib.Controls.Add(library);
   packInfo.SetBounds(16,330,920,62);lib.Controls.Add(packInfo);library.SelectedIndexChanged+=delegate{if(library.SelectedIndices.Count>0){var p=(LordPack)library.SelectedItems[0].Tag;packInfo.Text=p.Folder+Environment.NewLine+"Select this lord from a mission opponent’s replacement menu.";}};
   ButtonAt(lib,"Import lord folder…",693,65,230,ImportPack);ButtonAt(lib,"Refresh installed lords",693,111,230,RefreshPacks);
   LabelAt(lib,"Supported: native .lordjson AI profiles, .aivjson castles and optional avatar.png.\nScript-based packages require separate support.",693,170,235,110);
   status.SetBounds(24,768,605,56);Controls.Add(status);ButtonAt(this,"Save changes",646,772,165,delegate{Save();});ButtonAt(this,"Save & play",829,772,167,delegate{if(Save())Process.Start("steam://rungameid/3024040");});
   var menu=new ContextMenuStrip();menu.Items.Add("Open mod log",null,delegate{if(root!=null){string p=Path.Combine(root,"status.log");if(File.Exists(p))Process.Start("notepad.exe","\""+p+"\"");else MessageBox.Show("Start the game once to create a log.");}});status.ContextMenuStrip=menu;
   trail.SelectedIndexChanged+=delegate{RefreshMissions();};missionList.SelectedIndexChanged+=delegate{RefreshOpponents();};opponents.SelectedIndexChanged+=delegate{SelectOpponent();};
   FormClosing+=delegate(object s,FormClosingEventArgs e){if(!AllowLeave())e.Cancel=true;};
   ButtonAt(this,"Install mod from this package",746,146,250,InstallMod);LabelAt(this,"VERSION 1.0",852,35,144,24);ConfigureLayout(edit,lib);Theme.Apply(this);Controls[0].ForeColor=Theme.Gold;selected.ForeColor=Theme.Text;
   trail.SelectedIndex=0;string folder=FindGame();if(folder!=null)LoadGame(folder);else{gamePath.Text="Choose your Stronghold Crusader: Definitive Edition folder.";status.Text="Select a game folder to get started.";}
  }
  static string FindGame(){if(File.Exists(SettingsPath)){string s=File.ReadAllText(SettingsPath).Trim();if(File.Exists(Path.Combine(s,Exe)))return s;}var paths=new List<string>();string steam=null;try{steam=Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam","SteamPath",null) as string;}catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}if(steam!=null){paths.Add(steam);string vdf=Path.Combine(steam,"steamapps/libraryfolders.vdf");if(File.Exists(vdf))foreach(Match m in Regex.Matches(File.ReadAllText(vdf),"\"path\"\\s+\"([^\"]+)\""))paths.Add(m.Groups[1].Value.Replace("\\\\","\\"));}paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"));foreach(string p in paths){string f=Path.Combine(p,"steamapps/common/Stronghold Crusader Definitive Edition");if(File.Exists(Path.Combine(f,Exe)))return f;}return null;}
  void ChooseFolder(){if(!AllowLeave())return;using(var d=new FolderBrowserDialog{Description="Select the game installation folder",ShowNewFolderButton=false})if(d.ShowDialog(this)==DialogResult.OK)LoadGame(d.SelectedPath);}
  void LoadGame(string folder){if(!File.Exists(Path.Combine(folder,Exe)))throw new InvalidOperationException("The selected folder does not contain the game.");var next=Profile.Load(Path.Combine(folder,"TrailManager/profile.xml"));var roster=Catalog.ReadMissions(folder);loading=true;try{root=Path.Combine(folder,"TrailManager");gamePath.Text=folder;profile=next;missions=roster;enabled.Checked=profile.Enabled;RefreshPacks();RefreshMissions();dirty=false;status.Text="Start a new mission from the trail menu to use your saved replacements.";}finally{loading=false;}try{Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));File.WriteAllText(SettingsPath,folder);}catch{}}
  void RefreshMissions(){int previous=missionList.SelectedIndex;missionList.Items.Clear();if(missions==null)return;for(int m=0;m<missions[trail.SelectedIndex].Length;m++)missionList.Items.Add("Mission "+(m+1+(trail.SelectedIndex==1?50:0)));missionList.SelectedIndex=Math.Min(Math.Max(previous,0),missionList.Items.Count-1);}
  int Mission{get{return missionList.SelectedIndex+1;}}
  Rule FindRule(int slot){return profile.Rules.Find(r=>r.Trail==trail.SelectedIndex && r.Mission==Mission && r.Player==slot);}
  string RuleName(Rule r){if(r==null)return "Original";if(!string.IsNullOrEmpty(r.CustomLordFolder)){var p=packs.Find(x=>string.Equals(x.Folder,r.CustomLordFolder,StringComparison.OrdinalIgnoreCase));return p!=null?p.Name:Path.GetFileName(r.CustomLordFolder)+" (unavailable)";}if(r.ReplacementLord>=0)return Catalog.Lords[r.ReplacementLord];return r.CastleVariant>=0?"Original":"Original · existing override";}
  void RefreshOpponents(){opponents.Items.Clear();if(missions==null || missionList.SelectedIndex<0)return;int[] roster=missions[trail.SelectedIndex][missionList.SelectedIndex];var totals=roster.Where(id=>id>0).GroupBy(id=>id).ToDictionary(g=>g.Key,g=>g.Count());var counts=new Dictionary<int,int>();for(int slot=1;slot<=8;slot++){int id=roster[slot];if(id<=0)continue;if(!counts.ContainsKey(id))counts[id]=0;counts[id]++;string name=Catalog.Name(id)+(totals[id]>1?" "+counts[id]:"")+" · player "+slot;var item=new ListViewItem(new[]{name,RuleName(FindRule(slot)),CastleName(FindRule(slot))});item.Tag=slot;opponents.Items.Add(item);}if(opponents.Items.Count>0)opponents.Items[0].Selected=true;else SelectOpponent();}
  void RefreshPacks(){if(root==null)return;assets=LordAsset.Discover(gamePath.Text,Path.Combine(root,"LordAssets"));packs=LordPack.Discover(gamePath.Text,Path.Combine(root,"Lords"),profile.Rules.Select(r=>r.CustomLordFolder));library.Items.Clear();foreach(var p in packs){var item=new ListViewItem(new[]{p.Name,p.Configs.Length.ToString(),p.Castles.Length.ToString()});item.Tag=p;library.Items.Add(item);}replacement.Items.Clear();replacement.Items.Add(new Choice());for(int i=0;i<Catalog.Lords.Length;i++)replacement.Items.Add(new Choice{BuiltIn=i});foreach(var p in packs)replacement.Items.Add(new Choice{Pack=p});replacement.SelectedIndex=0;SelectOpponent();packInfo.Text=packs.Count+" custom lords available. Workshop files are read from their installed folders.";}
  void SelectOpponent(){if(opponents.SelectedItems.Count==0){selected.Text="Select an opponent above";replacement.Enabled=false;config.Enabled=false;castle.Enabled=false;return;}replacement.Enabled=true;selected.Text=opponents.SelectedItems[0].Text;var rule=FindRule((int)opponents.SelectedItems[0].Tag);int index=0;if(rule!=null){if(!string.IsNullOrEmpty(rule.CustomLordFolder)){for(int i=0;i<replacement.Items.Count;i++){var c=(Choice)replacement.Items[i];if(c.Pack!=null && string.Equals(c.Pack.Folder,rule.CustomLordFolder,StringComparison.OrdinalIgnoreCase)){index=i;break;}}if(index==0)status.Text="The selected custom lord is missing. Restore its files or choose another replacement.";}else index=rule.ReplacementLord+1;}replacement.SelectedIndex=index;UpdateConfig();if(rule!=null && !string.IsNullOrEmpty(rule.CustomConfig))for(int i=0;i<config.Items.Count;i++)if(string.Equals(config.Items[i].ToString(),rule.CustomConfig,StringComparison.OrdinalIgnoreCase))config.SelectedIndex=i;RestoreCastle(rule);if(rule!=null && !string.IsNullOrEmpty(rule.AiFile))for(int i=0;i<config.Items.Count;i++){var a=config.Items[i] as LordAsset;if(a!=null && string.Equals(a.Path,rule.AiFile,StringComparison.OrdinalIgnoreCase))config.SelectedIndex=i;}}
  void UpdateConfig(){config.Items.Clear();config.Enabled=false;if(portrait.Image!=null){portrait.Image.Dispose();portrait.Image=null;}var choice=replacement.SelectedItem as Choice;if(choice==null)return;if(choice.Pack!=null){foreach(string f in choice.Pack.Configs)config.Items.Add(Path.GetFileName(f));config.Enabled=true;string avatar=Path.Combine(choice.Pack.Folder,"avatar.png");if(File.Exists(avatar))try{using(var img=Image.FromFile(avatar))portrait.Image=new Bitmap(img);}catch{} }else {config.Items.Add("Built-in AI (default)");foreach(var asset in assets.Where(a=>a.Lord==SelectedBuiltIn() && a.AI))config.Items.Add(asset);config.Enabled=true;}if(config.Items.Count>0)config.SelectedIndex=0;UpdateCastles();}
  void Apply(){if(opponents.SelectedItems.Count==0)return;var c=replacement.SelectedItem as Choice;if(c==null)return;int slot=(int)opponents.SelectedItems[0].Tag;if(c.BuiltIn<0 && c.Pack==null && castle.SelectedIndex<=0 && config.SelectedIndex<=0){ResetOpponent();return;}var r=new Rule{Trail=trail.SelectedIndex,Mission=Mission,Player=slot,ReplacementLord=c.BuiltIn};if(c.Pack!=null){r.CustomLordFolder=c.Pack.Folder;r.CustomConfig=config.SelectedItem.ToString();if(castle.SelectedIndex>0)r.CustomCastle=Path.GetFileName(c.Pack.Castles[castle.SelectedIndex-1]);}else {var ca=castle.SelectedItem as LordAsset;var ia=config.SelectedItem as LordAsset;if(ca!=null)r.CastleFile=ca.Path;else r.CastleVariant=castle.SelectedIndex-1;if(ia!=null)r.AiFile=ia.Path;}int old=profile.Rules.FindIndex(x=>x.Trail==r.Trail && x.Mission==r.Mission && x.Player==slot);if(old<0)profile.Rules.Add(r);else profile.Rules[old]=r;dirty=true;opponents.SelectedItems[0].SubItems[1].Text=RuleName(r);opponents.SelectedItems[0].SubItems[2].Text=CastleName(r);status.Text="Replacement assigned. Save changes before starting the mission.";}
  void ResetOpponent(){if(opponents.SelectedItems.Count==0)return;int slot=(int)opponents.SelectedItems[0].Tag;profile.Rules.RemoveAll(r=>r.Trail==trail.SelectedIndex && r.Mission==Mission && r.Player==slot);dirty=true;opponents.SelectedItems[0].SubItems[1].Text="Original";opponents.SelectedItems[0].SubItems[2].Text="Default";replacement.SelectedIndex=0;UpdateConfig();status.Text="Original opponent restored. Save changes to apply.";}
  void ImportPack(){if(root==null)throw new InvalidOperationException("Choose a game folder first.");using(var d=new FolderBrowserDialog{Description="Select a custom lord folder containing .lordjson and .aivjson files",ShowNewFolderButton=false})if(d.ShowDialog(this)==DialogResult.OK){var p=LordPack.Import(d.SelectedPath,Path.Combine(root,"Lords"));RefreshPacks();status.Text="Imported "+p.Name+". Choose it as a mission replacement.";}}
  bool Save(){try{if(root==null)throw new InvalidOperationException("Choose a game folder first.");profile.Enabled=enabled.Checked;profile.Save(Path.Combine(root,"profile.xml"));dirty=false;status.Text="Saved. Start a new mission from the trail menu.";return true;}catch(Exception e){MessageBox.Show(this,e.Message,"Could not save changes");return false;}}
  bool AllowLeave(){if(!dirty)return true;var c=MessageBox.Show(this,"Save your changes?","Unsaved changes",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);return c==DialogResult.No || c==DialogResult.Yes && Save();}
  string CastleName(Rule r){return r==null?"Default":!string.IsNullOrEmpty(r.CustomCastle)?Path.GetFileNameWithoutExtension(r.CustomCastle):!string.IsNullOrEmpty(r.CastleFile)?Path.GetFileNameWithoutExtension(r.CastleFile):r.CastleVariant>=0?"Castle "+(r.CastleVariant+1):"Default";}
  void UpdateCastles(){castle.Items.Clear();castle.Items.Add("Default / let the game choose");var c=replacement.SelectedItem as Choice;if(c!=null && c.Pack!=null){foreach(string f in c.Pack.Castles)castle.Items.Add(Path.GetFileNameWithoutExtension(f));}else {for(int i=1;i<=8;i++)castle.Items.Add("Castle "+i);foreach(var asset in assets.Where(a=>a.Lord==SelectedBuiltIn() && !a.AI))castle.Items.Add(asset);}castle.Enabled=opponents.SelectedItems.Count>0;castle.SelectedIndex=0;}
  void RestoreCastle(Rule rule){if(rule==null)return;var c=replacement.SelectedItem as Choice;if(c!=null && c.Pack!=null && !string.IsNullOrEmpty(rule.CustomCastle)){for(int i=0;i<c.Pack.Castles.Length;i++)if(string.Equals(Path.GetFileName(c.Pack.Castles[i]),rule.CustomCastle,StringComparison.OrdinalIgnoreCase))castle.SelectedIndex=i+1;}else if(!string.IsNullOrEmpty(rule.CastleFile)){for(int i=0;i<castle.Items.Count;i++){var a=castle.Items[i] as LordAsset;if(a!=null && string.Equals(a.Path,rule.CastleFile,StringComparison.OrdinalIgnoreCase))castle.SelectedIndex=i;}}else if(rule.CastleVariant>=0 && rule.CastleVariant+1<castle.Items.Count)castle.SelectedIndex=rule.CastleVariant+1;}
  int SelectedBuiltIn(){var c=replacement.SelectedItem as Choice;if(c==null || c.Pack!=null)return -1;if(c.BuiltIn>=0)return c.BuiltIn;if(missions==null || missionList.SelectedIndex<0 || opponents.SelectedItems.Count==0)return -1;return missions[trail.SelectedIndex][missionList.SelectedIndex][(int)opponents.SelectedItems[0].Tag]-1;}
  void ImportAsset(){int lord=SelectedBuiltIn();if(lord<0)throw new InvalidOperationException("Select a built-in lord or an original mission opponent first.");using(var d=new OpenFileDialog{Title="Import an AI profile or castle for "+Catalog.Lords[lord],Filter="Native AI profiles and castles|*.lordjson;*.aivjson",CheckFileExists=true,Multiselect=true})if(d.ShowDialog(this)==DialogResult.OK){foreach(string f in d.FileNames)LordAsset.Import(f,lord,Path.Combine(root,"LordAssets"));assets=LordAsset.Discover(gamePath.Text,Path.Combine(root,"LordAssets"));UpdateConfig();status.Text="Imported files for "+Catalog.Lords[lord]+". Choose them under AI profile or Castle, then apply and save.";}}
  void ConfigureLayout(Panel edit,Panel lib){
   var all=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
   edit.Anchor=all;lib.Anchor=all;gamePath.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
   missionList.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left;opponents.Anchor=all;
   foreach(Control c in edit.Controls){if(c==missionList || c==opponents)continue;if(c.Top>=230){c.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;if(c is ComboBox)c.Anchor|=AnchorStyles.Right;}}
   portrait.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;
   library.Anchor=all;
   // Reserve the library's right-hand actions while expanding its list and details.
   library.Width=lib.Width-322;library.Height=lib.Height-155;
   foreach(Control c in lib.Controls){if(c==library)continue;if(c.Left>=690)c.Anchor=AnchorStyles.Top|AnchorStyles.Right;else if(c==packInfo){c.Top=lib.Height-75;c.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;}else c.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;}
   foreach(Control c in Controls){if(c==edit || c==lib || c==gamePath)continue;if(c.Top>=760)c.Anchor=AnchorStyles.Bottom|(c is StyledButton?AnchorStyles.Right:AnchorStyles.Left|AnchorStyles.Right);else if(c.Left>=740)c.Anchor=AnchorStyles.Top|AnchorStyles.Right;}
   Action fitColumns=delegate{int width=Math.Max(100,opponents.ClientSize.Width-20);opponents.Columns[0].Width=(int)(width*.36);opponents.Columns[1].Width=(int)(width*.31);opponents.Columns[2].Width=width-opponents.Columns[0].Width-opponents.Columns[1].Width;int lw=Math.Max(100,library.ClientSize.Width-20);library.Columns[0].Width=lw-200;library.Columns[1].Width=100;library.Columns[2].Width=100;};
   opponents.Resize+=delegate{fitColumns();};library.Resize+=delegate{fitColumns();};fitColumns();
  }
  void InstallMod(){
   if(root==null)throw new InvalidOperationException("Choose your game folder first.");
   string script=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Install.ps1");if(!File.Exists(script))throw new FileNotFoundException("Extract the complete download before installing. Install.ps1 is missing.");
   var start=new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File \""+script+"\" -GamePath \""+gamePath.Text+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
   using(var process=Process.Start(start)){var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();process.WaitForExit();string message=output.Result+errors.Result;if(process.ExitCode!=0)throw new InvalidOperationException(message.Trim());status.Text="Mod installed. Start a new trail mission to use your saved settings.";}
  }
  static Label LabelAt(Control parent,string text,int x,int y,int w,int h){var l=new Label{Text=text,Location=new Point(x,y),Size=new Size(w,h)};parent.Controls.Add(l);return l;}
  static void DropDown(Control parent,ComboBox box,int x,int y,int w){box.DropDownStyle=ComboBoxStyle.DropDownList;box.SetBounds(x,y,w,32);parent.Controls.Add(box);}
  void ButtonAt(Control parent,string text,int x,int y,int w,Action action){var b=new StyledButton{Accent=text=="Apply replacement" || text=="Save & play",Text=text,Location=new Point(x,y),Size=new Size(w,36),FlatStyle=FlatStyle.Flat};b.Click+=delegate{try{action();}catch(Exception e){MessageBox.Show(this,e.Message,"Could not complete action",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};parent.Controls.Add(b);}
 }
}









