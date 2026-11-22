using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Aster.Core;
using Microsoft.Win32;
namespace Aster.App;

/// <summary>Plain-language settings, coalesced live edits, scoped resets and non-destructive undo.</summary>
public sealed class PersonalizationWindow:Window
{
 readonly AppModel model;readonly Action<string> tools;readonly bool testMode;
 readonly ListBox navigation=new();readonly TextBox search=new();readonly ScrollViewer contentScroll=new();readonly StackPanel sections=new();
 readonly TextBlock status=new(){Text="Changes save automatically on this laptop.",FontSize=12};
 readonly Border preview=new(){Height=44,CornerRadius=new(22),Background=Brushes.Black,BorderBrush=new SolidColorBrush(Color.FromRgb(70,70,70)),BorderThickness=new(1),HorizontalAlignment=HorizontalAlignment.Center};
 readonly LivingEye previewEyes=new(){Width=30,Height=22,VerticalAlignment=VerticalAlignment.Center};
 readonly Button undoButton=new(){Content="Undo last change",MinHeight=44,IsEnabled=false};
 readonly TextBlock noResults=Text("No matching settings. Try eyes, music or sound.",14);
 readonly TextBlock clock=new(){FontSize=12,VerticalAlignment=VerticalAlignment.Center,Margin=new(0,0,14,0)};
 readonly List<Section> groups=[];readonly List<Action<Preferences>> refreshers=[];readonly List<Func<Preferences,Preferences>> pending=[];
 QuietSwitch? startupControl;bool startupLoading;
 readonly DispatcherTimer save=new(){Interval=TimeSpan.FromMilliseconds(180)};bool refreshing;
 (Preferences Before,Preferences After)? lastEdit;
 public bool Exiting{get;set;}
 sealed class Section(string category,string title,Border box,StackPanel panel){public string Category=category,Terms=category+" "+title;public Border Box=box;public StackPanel Panel=panel;}
 public PersonalizationWindow(AppModel model,Action<string> tools,bool testMode=false)
 {
  this.model=model;this.tools=tools;this.testMode=testMode;DataContext=model;Title="Aster · Make it yours";Width=1040;Height=810;MinWidth=840;MinHeight=620;WindowStartupLocation=WindowStartupLocation.CenterScreen;
  Background=Brush("#101010");Foreground=Brushes.White;FontFamily=new("Segoe UI");FontSize=14;
  foreach(var key in new[]{"TextBrush","AccentBrush"})Resources[key]=Brushes.White;
  Resources["BackgroundBrush"]=Brush("#101010");Resources["CardBrush"]=Brush("#1C1C1C");Resources["BorderBrush"]=Brush("#363636");Resources["MutedBrush"]=Brush("#BBBBBB");
  Resources[SystemColors.HighlightBrushKey]=Brush("#424242");Resources[SystemColors.HighlightTextBrushKey]=Brushes.White;
  InstallStyles();
  var root=new Grid{Margin=new(24)};root.ColumnDefinitions.Add(new(){Width=new(190)});root.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});Content=root;
  var sidebar=new DockPanel{Margin=new(0,0,20,0)};root.Children.Add(sidebar);
  var brand=new StackPanel{Margin=new(8,2,0,24)};brand.Children.Add(new LivingEye{Width=42,Height=30,HorizontalAlignment=HorizontalAlignment.Left});brand.Children.Add(Text("ASTER",22,true));brand.Children.Add(Text("Make it yours.",12));DockPanel.SetDock(brand,Dock.Top);sidebar.Children.Add(brand);
  var local=new Button{Content="Open local tools ↗",MinHeight=42};local.Click+=(_,_)=>tools("Overview");DockPanel.SetDock(local,Dock.Bottom);sidebar.Children.Add(local);
  foreach(var category in new[]{"Look & feel","Eyes","Activities","Alerts","Privacy & startup"})navigation.Items.Add(new ListBoxItem{Content=category,MinHeight=46,Padding=new(10),FontSize=14});navigation.SelectionChanged+=(_,_)=>Filter();sidebar.Children.Add(navigation);
  var right=new Grid();Grid.SetColumn(right,1);root.Children.Add(right);foreach(var height in new[]{GridLength.Auto,GridLength.Auto,GridLength.Auto,new GridLength(1,GridUnitType.Star),GridLength.Auto})right.RowDefinitions.Add(new(){Height=height});
  var header=new DockPanel();var close=new Button{Content="Done",MinWidth=82};close.Click+=(_,_)=>Close();DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);var intro=new StackPanel();intro.Children.Add(Text("Make it feel like you.",27,true));intro.Children.Add(Text("Pick a section or search. Try a change and watch your island.",12));header.Children.Add(intro);right.Children.Add(header);
  var previewBox=new Border{Background=Brush("#181818"),CornerRadius=new(20),Padding=new(18),Margin=new(0,16,0,14)};Grid.SetRow(previewBox,1);right.Children.Add(previewBox);
  var previewStack=new StackPanel();previewBox.Child=previewStack;previewStack.Children.Add(Text("LIVE LOOK",10,true));var pill=new Grid();previewEyes.HorizontalAlignment=HorizontalAlignment.Left;previewEyes.Margin=new(11,0,0,0);pill.Children.Add(previewEyes);clock.HorizontalAlignment=HorizontalAlignment.Right;pill.Children.Add(clock);preview.Child=pill;previewStack.Children.Add(preview);
  search.MinHeight=44;search.ToolTip="Search size, eyes, music, brightness, privacy…";AutomationProperties.SetName(search,"Find a setting");var searchHost=new Grid();var hint=Text("Search size, eyes, music, brightness…",12);hint.Margin=new(12,13,12,0);hint.IsHitTestVisible=false;hint.Foreground=Brush("#AAAAAA");searchHost.Children.Add(search);searchHost.Children.Add(hint);search.TextChanged+=(_,_)=>{hint.Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;Filter();};Grid.SetRow(searchHost,2);right.Children.Add(searchHost);
  var scroll=contentScroll;scroll.Content=sections;scroll.PanningMode=PanningMode.VerticalOnly;scroll.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;scroll.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;scroll.Padding=new(0,4,8,0);Grid.SetRow(scroll,3);right.Children.Add(scroll);
  var footer=new DockPanel{Margin=new(0,12,0,0)};var undo=undoButton;undo.Click+=(_,_)=>UndoLast();DockPanel.SetDock(undo,Dock.Right);footer.Children.Add(undo);footer.Children.Add(status);Grid.SetRow(footer,4);right.Children.Add(footer);
  Build();sections.Children.Add(noResults);navigation.SelectedIndex=0;save.Tick+=(_,_)=>FlushPending();model.PropertyChanged+=ModelChanged;Refresh();
  Closing+=(_,e)=>{FlushPending();if(!Exiting){e.Cancel=true;Hide();}};Closed+=(_,_)=>{save.Stop();model.PropertyChanged-=ModelChanged;};
 }
 void InstallStyles()
 {
  string ns="xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";
  string[] definitions={
   "<Style TargetType=\"ScrollBar\"><Setter Property=\"Width\" Value=\"7\"/><Setter Property=\"Template\"><Setter.Value><ControlTemplate TargetType=\"ScrollBar\"><Track x:Name=\"PART_Track\" IsDirectionReversed=\"True\"><Track.DecreaseRepeatButton><RepeatButton Opacity=\"0\" Command=\"ScrollBar.PageUpCommand\"/></Track.DecreaseRepeatButton><Track.IncreaseRepeatButton><RepeatButton Opacity=\"0\" Command=\"ScrollBar.PageDownCommand\"/></Track.IncreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType=\"Thumb\"><Border Background=\"#777777\" CornerRadius=\"3\"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></ControlTemplate></Setter.Value></Setter></Style>",
   "<Style TargetType=\"ListBoxItem\"><Setter Property=\"HorizontalContentAlignment\" Value=\"Stretch\"/><Setter Property=\"Template\"><Setter.Value><ControlTemplate TargetType=\"ListBoxItem\"><Border x:Name=\"Face\" CornerRadius=\"12\" Background=\"Transparent\" Padding=\"12\" Margin=\"0,2\"><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property=\"IsSelected\" Value=\"True\"><Setter TargetName=\"Face\" Property=\"Background\" Value=\"#363636\"/></Trigger><Trigger Property=\"IsKeyboardFocused\" Value=\"True\"><Setter TargetName=\"Face\" Property=\"BorderBrush\" Value=\"White\"/><Setter TargetName=\"Face\" Property=\"BorderThickness\" Value=\"1\"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>",
   "<Style TargetType=\"Slider\"><Setter Property=\"Template\"><Setter.Value><ControlTemplate TargetType=\"Slider\"><Grid Height=\"44\" Margin=\"8,0\"><Track x:Name=\"PART_Track\" Minimum=\"{TemplateBinding Minimum}\" Maximum=\"{TemplateBinding Maximum}\" Value=\"{TemplateBinding Value}\"><Track.DecreaseRepeatButton><RepeatButton Command=\"Slider.DecreaseLarge\" Focusable=\"False\"><RepeatButton.Template><ControlTemplate TargetType=\"RepeatButton\"><Grid Background=\"Transparent\" Height=\"44\"><Border Height=\"4\" VerticalAlignment=\"Center\" CornerRadius=\"2\" Background=\"#DDDDDD\"/></Grid></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.IncreaseRepeatButton><RepeatButton Command=\"Slider.IncreaseLarge\" Focusable=\"False\"><RepeatButton.Template><ControlTemplate TargetType=\"RepeatButton\"><Grid Background=\"Transparent\" Height=\"44\"><Border Height=\"4\" VerticalAlignment=\"Center\" CornerRadius=\"2\" Background=\"#444444\"/></Grid></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton><Track.Thumb><Thumb Width=\"18\" Height=\"18\"><Thumb.Template><ControlTemplate TargetType=\"Thumb\"><Border Background=\"White\" CornerRadius=\"9\" BorderBrush=\"#BBBBBB\" BorderThickness=\"1\"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>"};
  foreach(var definition in definitions){var style=(Style)System.Windows.Markup.XamlReader.Parse(definition.Replace("<Style ","<Style "+ns+" "));Resources[style.TargetType]=style;}
 }
 static SolidColorBrush Brush(string hex)=>(SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
 static TextBlock Text(string text,double size=14,bool bold=false)=>new(){Text=text,FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,Foreground=Brushes.White,TextWrapping=TextWrapping.Wrap,Margin=new(0,2,0,5)};
 Section Group(string category,string title,string description)
 {
  var panel=new StackPanel();panel.Children.Add(Text(title,20,true));panel.Children.Add(Text(description,12));var box=new Border{Background=Brush("#1A1A1A"),CornerRadius=new(18),Padding=new(18),Margin=new(0,4,0,12),Child=panel};var group=new Section(category,title,box,panel);groups.Add(group);sections.Children.Add(box);return group;
 }
 void Row(Section group,string title,string description,FrameworkElement control)
 {
  group.Terms+=" "+title+" "+description;var row=new Grid{Margin=new(0,12,0,4)};row.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=new(255)});var label=new StackPanel{Margin=new(0,0,14,0),VerticalAlignment=VerticalAlignment.Center};label.Children.Add(Text(title,14,true));label.Children.Add(Text(description,11));row.Children.Add(label);Grid.SetColumn(control,1);row.Children.Add(control);group.Panel.Children.Add(row);AutomationProperties.SetName(control,title);
 }
 void Toggle(Section group,string title,string description,Func<Preferences,bool> read,Func<Preferences,bool,Preferences> patch)
 {
  var button=new QuietSwitch{HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};Row(group,title,description,button);button.Checked+=(_,_)=>{if(!refreshing)Queue(p=>patch(p,true));};button.Unchecked+=(_,_)=>{if(!refreshing)Queue(p=>patch(p,false));};refreshers.Add(p=>button.IsChecked=read(p));
 }
 void Range(Section group,string title,string description,double min,double max,double step,Func<Preferences,double> read,Func<Preferences,double,Preferences> patch,string suffix="")
 {
  var panel=new StackPanel();var value=Text("",12);value.HorizontalAlignment=HorizontalAlignment.Right;var slider=new Slider{Minimum=min,Maximum=max,TickFrequency=step,IsSnapToTickEnabled=true,IsMoveToPointEnabled=true,MinHeight=44};panel.Children.Add(value);panel.Children.Add(slider);AutomationProperties.SetName(slider,title);Row(group,title,description,panel);slider.ValueChanged+=(_,_)=>{value.Text=FriendlyValue(title,slider.Value,suffix);if(!refreshing)Queue(p=>patch(p,slider.Value));};refreshers.Add(p=>{slider.Value=read(p);value.Text=FriendlyValue(title,slider.Value,suffix);});
 }
 static string FriendlyValue(string title,double value,string suffix)=>title switch{"Overall size" or "Eye size"=>$"{value*100:0}%","Glass depth"=>$"{value*100:0}% solid","Night dimming"=>$"{value*100:0}% softer","Eye energy"=>value<.8?"Calm":value<=1.1?"Balanced":"Lively","Edge glow"=>value<.8?"Subtle":value<=1.4?"Soft":"Bright",_=>$"{value:0.##}{suffix}"};
 void Choice(Section group,string title,string description,(string Name,Func<Preferences,bool> Selected,Func<Preferences,Preferences> Apply)[] options)
 {
  var panel=new WrapPanel();foreach(var option in options){var button=new Button{Content=option.Name,Padding=new(10,6,10,6),FontSize=12,MinHeight=44};button.Click+=(_,_)=>Queue(option.Apply);panel.Children.Add(button);refreshers.Add(p=>button.Background=Brush(option.Selected(p)?"#505050":"#242424"));}Row(group,title,description,panel);
 }
 void Action(Section group,string title,string description,string label,Action action){var button=new Button{Content=label,MinHeight=44};button.Click+=(_,_)=>{FlushPending();ClearUndo();action();};Row(group,title,description,button);}
 void Build()
 {
  var look=Group("Look & feel","Your island","Simple choices. The island stays monochrome and your artwork keeps its original colors.");
  Toggle(look,"Show island now","Hide it for this session. It returns on the next launch.",p=>p.ShowOverlay,(p,v)=>p with{ShowOverlay=v});
  Range(look,"Overall size","Small and quiet, or easier to see.",.85,1.2,.05,p=>p.IslandScale,(p,v)=>p with{IslandScale=v});
  Range(look,"Small island width","How wide the resting pill feels.",180,300,10,p=>p.CompactWidth,(p,v)=>p with{CompactWidth=v}," px");
  Range(look,"Open island width","More room for cards and controls.",540,680,20,p=>p.OverlayWidth,(p,v)=>p with{OverlayWidth=v}," px");
  Range(look,"Space from the top","Move it closer to the edge or a little lower.",0,100,2,p=>p.TopOffset,(p,v)=>p with{TopOffset=v}," px");
  Toggle(look,"Small clock","Show the time beside the eyes.",p=>p.ShowClock,(p,v)=>p with{ShowClock=v});
  var screens=System.Windows.Forms.Screen.AllScreens;Choice(look,"Screen","Choose where the island lives.",screens.Select((s,i)=>("Screen "+(i+1),(Func<Preferences,bool>)(p=>p.Monitor==i),(Func<Preferences,Preferences>)(p=>p with{Monitor=i}))).ToArray());
  var glass=Group("Look & feel","Glass & night","Keep the look soft and comfortable.");
  Range(glass,"Glass depth","Lower values let a little more of the desktop through.",.8,1,.02,p=>p.GlassOpacity,(p,v)=>p with{GlassOpacity=v});
  Range(glass,"Edge glow","A softer edge or a brighter highlight.",.4,2,.1,p=>p.GlassShine,(p,v)=>p with{GlassShine=v});
  Toggle(glass,"Night mode","Dimmer glass and gentler movement.",p=>p.NightMode,(p,v)=>p with{NightMode=v});
  Range(glass,"Night dimming","How much softer the content looks at night.",.05,.3,.01,p=>p.NightDim,(p,v)=>p with{NightDim=v});
  Choice(glass,"Font","Choose a familiar typeface.",new[]{"Segoe UI","Segoe UI Variable","Arial","Calibri"}.Select(f=>(f,(Func<Preferences,bool>)(p=>p.Font==f),(Func<Preferences,Preferences>)(p=>p with{Font=f}))).ToArray());
  Action(glass,"Reset the look","Keeps your privacy permissions, alerts and saved data.","Restore original look",()=>Queue(AppearanceDefaults.Apply));
  var eye=Group("Eyes","A little personality","Curious eyes without screen capture, camera access or uploads.");
  Toggle(eye,"Show the eyes","Keep the pair visible when artwork is not showing.",p=>p.EyesVisible,(p,v)=>p with{EyesVisible=v});
  Toggle(eye,"Follow the pointer","Let the eyes glance toward your cursor.",p=>p.EyesFollowPointer,(p,v)=>p with{EyesFollowPointer=v});
  Toggle(eye,"Natural blinks","Soft, occasional blinks.",p=>p.EyesBlink,(p,v)=>p with{EyesBlink=v});
  Toggle(eye,"Happy expressions","Friendly crescent smiles near the island.",p=>p.EyesHappy,(p,v)=>p with{EyesHappy=v});
  Range(eye,"Eye size","Smaller and subtle, or a little more expressive.",.75,1.1,.05,p=>p.EyeScale,(p,v)=>p with{EyeScale=v});
  Range(eye,"Eye energy","Calm glances on the left; quicker glances on the right.",.5,1.4,.1,p=>p.EyeSpeed,(p,v)=>p with{EyeSpeed=v});
  var motion=Group("Eyes","Movement","Physics-based motion follows your display’s refresh rate. Windows controls the available refresh rate.");
  Choice(motion,"Motion feel","Choose a ready-made feel without adjusting spring numbers.",new[]{("Soft",320d,40d),("Smooth",420d,38d),("Quick",600d,47d)}.Select(x=>(x.Item1,(Func<Preferences,bool>)(p=>Math.Abs(p.SpringStiffness-x.Item2)<1&&Math.Abs(p.SpringDamping-x.Item3)<1),(Func<Preferences,Preferences>)(p=>p with{SpringStiffness=x.Item2,SpringDamping=x.Item3,SpringMass=1}))).ToArray());
  Toggle(motion,"Reduce movement","Use still eyes and short, direct state changes.",p=>p.ReducedMotion,(p,v)=>p with{ReducedMotion=v});
  Toggle(motion,"Open when you hover","Hover over the small pill to open it. The tucked top edge always stays reachable.",p=>p.HoverToOpen,(p,v)=>p with{HoverToOpen=v});
  Toggle(motion,"Tuck away for big apps","Hide in the top edge for maximized and fullscreen windows.",p=>p.AutoStowMaximized,(p,v)=>p with{AutoStowMaximized=v});
  Action(motion,"Display refresh rate","Choose 120 Hz in Windows if your display supports it.","Open display settings ↗",()=>Open("ms-settings:display"));
  var media=Group("Activities","Music & atmosphere","Windows supplies media information and album covers. No artwork searches.");
  Toggle(media,"Music on the island","Show the music activity and controls.",p=>p.ShowMedia,(p,v)=>p with{ShowMedia=v});
  Toggle(media,"Album covers","Show the current artwork instead of the eyes when available.",p=>p.ShowArtwork,(p,v)=>p with{ShowArtwork=v});
  Toggle(media,"Spotify wallpaper","Use the existing soft, blurred cover-art atmosphere. Your original wallpaper returns when disabled.",p=>p.SpotifyWallpaper,(p,v)=>p with{SpotifyWallpaper=v});
  Toggle(media,"Headphone cards","Show connection, disconnection and output-selection cards.",p=>p.HeadphoneCards,(p,v)=>p with{HeadphoneCards=v});
  var focus=Group("Activities","Focus & local tools","Keep useful things close. Original files are never deleted from the shelf.");
  Range(focus,"Focus length","Minutes used when starting over or beginning a fresh session.",1,180,1,p=>p.FocusMinutes,(p,v)=>p with{FocusMinutes=(int)v}," min");
  Range(focus,"Break length","A short break, ready when you need it.",1,30,1,p=>p.BreakMinutes,(p,v)=>p with{BreakMinutes=(int)v}," min");
  Action(focus,"Start timer over","This replaces the current focus countdown.","Use the chosen focus time",()=>model.ResetFocusCommand.Execute("focus"));
  Toggle(focus,"Battery cards","Show battery information on the island.",p=>p.ShowBattery,(p,v)=>p with{ShowBattery=v});
  Action(focus,"Files, notes & tasks","Add, open or remove shelf entries; edit notes and tasks.","Open local tools ↗",()=>tools("File shelf"));
  Action(focus,"Notes & tasks","Write a note, tick off tasks or change their content.","Open notes & tasks ↗",()=>tools("Notes & tasks"));
  Action(focus,"App shortcuts","Add or remove your local application shortcuts.","Manage shortcuts ↗",()=>tools("Launchpad"));
  var alerts=Group("Alerts","Sound & brightness","Device connections recover automatically after startup failures, sleep and output changes.");
  Toggle(alerts,"Volume in the island","Use the island for volume keys and level changes.",p=>p.VolumeHudEnabled,(p,v)=>p with{VolumeHudEnabled=v,ReplaceVolumeKeys=v});
  Toggle(alerts,"Brightness in the island","Show changes and enable Ctrl+Alt+Up/Down. Firmware keys may still show Windows’ popup.",p=>p.BrightnessHudEnabled,(p,v)=>p with{BrightnessHudEnabled=v,ReplaceBrightnessKeys=v});
  Range(alerts,"Level popup time","How long volume and brightness cards stay visible.",.8,3,.1,p=>p.HudSeconds,(p,v)=>p with{HudSeconds=v}," sec");
  Action(alerts,"Hardware brightness keys","Teach supported function keys in the advanced local controls.","Key setup ↗",()=>tools("Appearance"));
  var notifications=Group("Alerts","Notifications","Messages stay in session memory. Dismiss removes an island entry, not the Windows message.");
  var connect=new Button{MinHeight=44,Command=model.ConnectNotificationsCommand};connect.SetBinding(ContentControl.ContentProperty,new System.Windows.Data.Binding(nameof(AppModel.NotificationConnectionLabel)));Row(notifications,"Windows notification access","Choose Connect to use Windows’ permission prompt.",connect);connect.Click+=(_,_)=>{FlushPending();ClearUndo();status.Text="Windows manages notification permission.";};var permissionInfo=Text("",11);permissionInfo.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding(nameof(AppModel.NotificationStatus)));AutomationProperties.SetLiveSetting(permissionInfo,AutomationLiveSetting.Polite);notifications.Panel.Children.Add(permissionInfo);
  Toggle(notifications,"Notification popups","Keep the inbox but choose whether new arrivals pop out.",p=>p.NotificationBanners,(p,v)=>p with{NotificationBanners=v});
  Toggle(notifications,"Message previews","Allow message text in temporary island banners.",p=>p.NotificationPreviews,(p,v)=>p with{NotificationPreviews=v});
  Range(notifications,"Notification time","Time to read a pop-out before it rests again.",2,8,.5,p=>p.NotificationSeconds,(p,v)=>p with{NotificationSeconds=v}," sec");
  Action(notifications,"Windows corner banners","Keep Notification Center on. Windows controls whether its own banners appear.","Windows notification settings ↗",()=>model.NotificationSettingsCommand.Execute(null));
  Action(notifications,"Clear island inbox","Hides displayed entries; original Windows notifications stay.","Clear island entries",()=>model.ClearNotificationsCommand.Execute(null));
  var privacy=Group("Privacy & startup","Your choices","No account, analytics or automatic weather requests.");
  var startup=new QuietSwitch{HorizontalAlignment=HorizontalAlignment.Right};startupControl=startup;startup.IsChecked=StartupEnabled();
  Row(privacy,"Start with Windows","Show the island after you sign in.",startup);startup.Checked+=(_,_)=>SetStartup(true);startup.Unchecked+=(_,_)=>SetStartup(false);
  Action(privacy,"Clipboard history","Off on each launch. Enable only for this session; clear it at any time.","Clipboard controls ↗",()=>tools("Clipboard"));
  Action(privacy,"Optional weather","Explicit permission and a manual fetch are required. Location is entered by you.","Weather & network controls ↗",()=>tools("Weather"));
  Action(privacy,"Saved data","Review local storage and erase Aster data from the privacy tools.","Storage & privacy ↗",()=>tools("Privacy"));
  Action(privacy,"Advanced controls","Extra layout and motion numbers are kept out of the way here.","Advanced appearance ↗",()=>tools("Appearance"));
 }
 bool StartupEnabled(){try{using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");return (key?.GetValue("Aster") as string)?.Contains("Aster.App.exe",StringComparison.OrdinalIgnoreCase)==true;}catch{return false;}}
 void SetStartup(bool enabled){if(testMode||startupLoading)return;FlushPending();ClearUndo();try{using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");if(enabled)key.SetValue("Aster","\""+Environment.ProcessPath+"\"");else key.DeleteValue("Aster",false);status.Text=enabled?"Aster will start after sign-in.":"Automatic startup is off.";}catch{startupLoading=true;try{if(startupControl is not null)startupControl.IsChecked=StartupEnabled();}finally{startupLoading=false;}status.Text="Windows could not change startup. Try Windows Settings → Apps → Startup.";}}
 void ClearUndo(){lastEdit=null;undoButton.IsEnabled=false;}

 void Open(string uri){if(testMode)return;try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri){UseShellExecute=true});}catch{status.Text="Open this page from Windows Settings.";}}
 Preferences Draft(){var p=model.Preferences;foreach(var patch in pending)p=patch(p);return p.Validated();}
 void Queue(Func<Preferences,Preferences> patch){if(refreshing)return;pending.Add(patch);status.Text="Saving your choice…";UpdatePreview(Draft());save.Stop();save.Start();}
 public void FlushPending(){save.Stop();if(pending.Count==0)return;var before=model.Preferences;var after=Draft();pending.Clear();lastEdit=(before,after);undoButton.IsEnabled=true;model.UpdatePreferences(after);status.Text="Saved on this laptop. You can undo your last change.";Refresh();}
 public void UndoLast(){FlushPending();if(lastEdit is not{} edit)return;lastEdit=null;undoButton.IsEnabled=false;model.UpdatePreferences(PreferenceEdits.Undo(model.Preferences,edit.Before,edit.After));status.Text="Last change undone. Your other choices are kept.";Refresh();}
 void Refresh(){refreshing=true;try{var p=Draft();foreach(var update in refreshers)update(p);UpdatePreview(p);}finally{refreshing=false;}}
 void UpdatePreview(Preferences p){preview.Width=p.CompactWidth*p.IslandScale;preview.Background=new SolidColorBrush(Color.FromArgb((byte)(p.GlassOpacity*255),8,8,8));byte edge=(byte)(50+35*p.GlassShine);preview.BorderBrush=new SolidColorBrush(Color.FromRgb(edge,edge,edge));clock.FontFamily=new(p.Font);preview.Opacity=p.NightMode?1-p.NightDim:1;clock.Text=model.ClockText;clock.Visibility=p.ShowClock?Visibility.Visible:Visibility.Hidden;previewEyes.Visibility=p.EyesVisible?Visibility.Visible:Visibility.Collapsed;previewEyes.HorizontalAlignment=p.ShowClock?HorizontalAlignment.Left:HorizontalAlignment.Center;previewEyes.Margin=p.ShowClock?new Thickness(11,0,0,0):new Thickness(0);clock.HorizontalAlignment=p.EyesVisible?HorizontalAlignment.Right:HorizontalAlignment.Center;preview.CornerRadius=new(22);}
 void ModelChanged(object? sender,PropertyChangedEventArgs e){if(e.PropertyName==nameof(AppModel.Preferences))Refresh();if(e.PropertyName==nameof(AppModel.ClockText))clock.Text=model.ClockText;}
 void Filter(){contentScroll.ScrollToTop();string query=search.Text.Trim();string category=(navigation.SelectedItem as ListBoxItem)?.Content as string??"Look & feel";foreach(var group in groups)group.Box.Visibility=(query.Length==0?group.Category==category:group.Terms.Contains(query,StringComparison.OrdinalIgnoreCase))?Visibility.Visible:Visibility.Collapsed;noResults.Visibility=groups.Any(g=>g.Box.Visibility==Visibility.Visible)?Visibility.Collapsed:Visibility.Visible;}
 internal int VisibleGroupCount=>groups.Count(g=>g.Box.Visibility==Visibility.Visible);
 internal void SearchForTest(string query)=>search.Text=query;
 internal void SelectForTest(string category){foreach(ListBoxItem item in navigation.Items)if(item.Content as string==category)navigation.SelectedItem=item;}
}

public sealed class QuietSwitch:ToggleButton
{
 readonly SpringValue value=new(0);readonly System.Diagnostics.Stopwatch clock=new();bool running;
 public QuietSwitch(){Width=54;Height=44;MinHeight=44;Cursor=Cursors.Hand;Template=new ControlTemplate(typeof(QuietSwitch));Checked+=(_,_)=>Move(1);Unchecked+=(_,_)=>Move(0);Unloaded+=(_,_)=>Stop();GotKeyboardFocus+=(_,_)=>InvalidateVisual();LostKeyboardFocus+=(_,_)=>InvalidateVisual();}
 void Move(double target){value.Target=target;if(!IsLoaded){value.Snap(target);InvalidateVisual();return;}if(!running){running=true;clock.Restart();CompositionTarget.Rendering+=Frame;}}
 void Stop(){if(!running)return;running=false;CompositionTarget.Rendering-=Frame;}
 void Frame(object? sender,EventArgs e){double dt=Math.Min(clock.Elapsed.TotalSeconds,.05);clock.Restart();if((DataContext as AppModel)?.Preferences.ReducedMotion==true)value.Snap(value.Target);else value.Step(dt,new(500,44,1));InvalidateVisual();if(value.Settled)Stop();}
 protected override void OnRender(DrawingContext dc){dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,ActualWidth,ActualHeight));double t=Math.Clamp(value.Position,0,1);byte shade=(byte)(58+165*t);dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(shade,shade,shade)),IsKeyboardFocused?new Pen(Brushes.White,1.5):null,new Rect(5,10,44,24),12,12);dc.DrawEllipse(t>.5?Brushes.Black:Brushes.White,null,new(17+20*t,22),8,8);}
}
